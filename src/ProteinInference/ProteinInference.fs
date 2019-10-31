namespace ProteomIQon

open BioFSharp
open System.Data
open System.Data.SQLite
open BioFSharp.Mz
open BioFSharp.Mz.SearchDB
open BioFSharp.Mz.ProteinInference
open BioFSharp.IO
open FSharpAux
open System.Text.RegularExpressions
open PeptideClassification
open ProteomIQon
open FSharpAux.IO
open FSharpAux.IO.SchemaReader
open FSharpAux.IO.SchemaReader.Csv
open FSharpAux.IO.SchemaReader.Attribute
open GFF3
open Domain
open FSharp.Plotly
open FSharp.Stats

module ProteinInference =

    /// Represents one peptide-entry with its evidence class and the proteins it points to.
    type ClassInfo =
        {
            Sequence : string
            Class    : PeptideClassification.PeptideEvidenceClass
            Proteins : string []
        }

    /// Given a ggf3 and a fasta file, creates a collection of all theoretically possible peptides and the proteins they might
    /// originate from
    ///
    /// ProteinClassItem: For a single peptide Sequence, contains information about all proteins it might originate from and
    /// its evidence class.
    ///
    /// No experimental data
    let createClassItemCollection gff3Path (memoryDB: SQLiteConnection) regexPattern rawFolderPath=

        let logger = Logging.createLogger "ProteinInference_createClassItemCollection"

        logger.Trace (sprintf "Regex pattern: %s" regexPattern)

        let rawFilePaths = System.IO.Directory.GetFiles (rawFolderPath, "*.qpsm")
                           |> List.ofArray

        // retrieves peptide sequences and IDs from input files
        let psmInputs =
            rawFilePaths
            |> List.map (fun filePath ->
                Seq.fromFileWithCsvSchema<ProteomIQon.ProteinInference'.PSMInput>(filePath, '\t', true,schemaMode = SchemaModes.Fill)
                |> Seq.toList
                )

         //gets distinct IDs of all peptides represented in the raw files
        let inputPepSeqIDs =
            psmInputs
            |> List.collect (fun psmArray ->
                psmArray
                |> List.map (fun psm -> psm.PepSequenceID)
                )
            |> List.distinct

        //list of proteins tupled with list of possible peptides found in psm
        let accessionSequencePairs =
            let preparedProtPepFunc = ProteomIQon.SearchDB'.getProteinPeptideLookUpFromFileBy memoryDB
            inputPepSeqIDs
            |> List.map (fun pepID -> preparedProtPepFunc pepID)
            |> List.concat
            |> List.groupBy (fun (protein, _)-> protein)
            |> List.map (fun (protein, pepList) -> protein, (pepList |> List.map (fun (_,pep)-> BioArray.ofAminoAcidString pep)))

        /// Create proteinModelInfos: Group all genevariants (RNA) for the gene loci (gene)
        ///
        /// The proteinModelInfos
        logger.Trace "Reading GFF3 file"
        let proteinModelInfos =
           try
                GFF3.fromFileWithoutFasta gff3Path
                |> ProteomIQon.ProteinInference'.assignTranscriptsToGenes regexPattern
            with
            | err ->
                printfn "ERROR: Could not read gff3 file %s" gff3Path
                failwithf "%s" err.Message
        //reads from file to an array of FastaItems.
        logger.Trace "Assigning FastA sequences to protein model info"
        /// Assigned fasta sequences to model Infos
        let proteinModels =
            try
                accessionSequencePairs
                |> Seq.mapi (fun i (header,sequence) ->
                    let regMatch = System.Text.RegularExpressions.Regex.Match(header,regexPattern)
                    if regMatch.Success then
                        match Map.tryFind regMatch.Value proteinModelInfos with
                        | Some modelInfo ->
                            Some (createProteinModel modelInfo sequence)
                        | None ->
                            failwithf "Could not find protein with id %s in gff3 File" regMatch.Value
                    else
                        logger.Trace (sprintf "Could not extract id of header \"%s\" with regexPattern %s" header regexPattern)
                        createProteinModelInfo header "Placeholder" StrandDirection.Forward (sprintf "Placeholder%i" i) 0 Seq.empty Seq.empty
                        |> fun x -> createProteinModel x sequence
                        |> Some
                    )
            with
            | err ->
                printfn "Could not assign FastA sequences to RNAs"
                failwithf "%s" err.Message
        logger.Trace "Build classification map"
        try
            let ppRelationModel = ProteomIQon.ProteinInference'.createPeptideProteinRelation proteinModels

            let spliceVariantCount = PeptideClassification.createLocusSpliceVariantCount ppRelationModel

            let classified =
                ppRelationModel.GetArrayOfKeys
                |> Array.map (fun peptide ->
                    let proteinInfo = (ppRelationModel.TryGetByKey peptide).Value
                    let proteinIds = Seq.map (fun (x : PeptideClassification.ProteinModelInfo<string,string,string>) -> x.Id) proteinInfo
                    let (c,x) = PeptideClassification.classify spliceVariantCount (peptide, (ppRelationModel.TryGetByKey peptide).Value)
                    {
                        Sequence = BioArray.toString x;
                        Class = c;
                        Proteins = (proteinIds |> Seq.toArray)
                    }
                    )

            classified |> Array.map (fun ci -> ci.Sequence,(createProteinClassItem ci.Proteins ci.Class ci.Sequence)) |> Map.ofArray, psmInputs
        with
        | err ->
            printfn "\nERROR: Could not build classification map"
            failwithf "\t%s" err.Message

    let removeModification pepSeq =
        String.filter (fun c -> System.Char.IsLower c |> not && c <> '[' && c <> ']') pepSeq

    let proteinGroupToString (proteinGroup:string[]) =
        Array.reduce (fun x y ->  x + ";" + y) proteinGroup

    let readAndInferFile classItemCollection protein peptide groupFiles outDirectory rawFolderPath psmInputs (dbConnection: SQLiteConnection) (qValMethod: QValueMethod) =

        let logger = Logging.createLogger "ProteinInference_readAndInferFile"

        let rawFilePaths = System.IO.Directory.GetFiles (rawFolderPath, "*.qpsm")
                           |> Array.toList

        let outFiles: string list =
            rawFilePaths
            |> List.map (fun filePath ->
                let foldername = (rawFolderPath.Split ([|"\\"|], System.StringSplitOptions.None))
                outDirectory + @"\" + foldername.[foldername.Length - 1] + "\\" + (System.IO.Path.GetFileNameWithoutExtension filePath) + ".prot"
                )

        let dbParams = ProteomIQon.SearchDB'.getSDBParamsBy dbConnection

        // Array of prtoein Accessions tupled with their reverse digested peptides
        let reverseProteins =
            (ProteomIQon.SearchDB'.selectProteins dbConnection)
            |> Array.ofList
            |> Array.map (fun (name, sequence) ->
                name,
                Digestion.BioArray.digest dbParams.Protease 0 ((sequence |> String.rev) |> BioArray.ofAminoAcidString)
                |> Digestion.BioArray.concernMissCleavages dbParams.MinMissedCleavages dbParams.MaxMissedCleavages
                |> fun x -> x |> Array.map (fun y -> y.PepSequence |> List.toArray |> BioArray.toString)
                )

        logger.Trace "Map peptide sequences to proteins"
        let classifiedProteins =
            List.map2 (fun psmInput (outFile: string) ->
                try
                    psmInput
                    |> List.map (fun (s: ProteomIQon.ProteinInference'.PSMInput) ->
                        let s =
                            s.Seq

                        match Map.tryFind (removeModification s) classItemCollection with
                        | Some (x:ProteinClassItem<'sequence>) -> createProteinClassItem x.GroupOfProteinIDs x.Class s
                        | None -> failwithf "Could not find sequence %s in classItemCollection" s
                        ),
                        outFile
                with
                | err ->
                    printfn "Could not map sequences of file %s to proteins:" (System.IO.Path.GetFileNameWithoutExtension outFile)
                    failwithf "%s" err.Message
                ) psmInputs outFiles

        if groupFiles then
            logger.Trace "Create combined list"
            let combinedClasses =
                List.collect (fun (pepSeq,_) -> pepSeq) classifiedProteins
                |> BioFSharp.Mz.ProteinInference.inferSequences protein peptide

            // Peptide score Map
            let peptideScoreMap = ProteomIQon.ProteinInference'.createPeptideScoreMap psmInputs

            // Assigns scores to reverse digested Proteins using the peptideScoreMap
            let reverseProteinScores = ProteomIQon.ProteinInference'.createReverseProteinScores reverseProteins peptideScoreMap

            // Scores each inferred protein and assigns each protein where a reverted peptide was hit its score
            let combinedScoredClasses =
                combinedClasses
                |> Array.ofSeq
                |> Array.map (fun inferredPCI ->
                    // Looks up all peptides assigned to the protein and sums up their score
                    let peptideScore = (ProteomIQon.ProteinInference'.assignPeptideScores inferredPCI.PeptideSequence peptideScoreMap)
                    // Looks if the reverse protein has been randomly matched and assigns the score
                    let decoyScore = (ProteomIQon.ProteinInference'.assignDecoyScoreToTargetScore inferredPCI.GroupOfProteinIDs reverseProteinScores)

                    ProteomIQon.ProteinInference'.createInferredProteinClassItemScored
                        inferredPCI.GroupOfProteinIDs inferredPCI.Class inferredPCI.PeptideSequence
                        peptideScore
                        decoyScore
                        // Placeholder for q value
                        -1.
                        false
                        (decoyScore > peptideScore)
                    )

            // creates InferredProteinClassItemScored type for decoy proteins that have no match
            let reverseNoMatch =
                let proteinsPresent =
                    combinedScoredClasses
                    |> Array.collect (fun prots -> prots.GroupOfProteinIDs |> String.split ';')
                reverseProteinScores
                |> Map.toArray
                |> Array.map (fun (protein, score) ->
                    if (proteinsPresent |> Array.contains protein) then
                        ProteomIQon.ProteinInference'.createInferredProteinClassItemScored "HasMatch" PeptideEvidenceClass.Unknown [|""|] (-1.) (-1.) (-1.) false false
                    else
                        ProteomIQon.ProteinInference'.createInferredProteinClassItemScored protein PeptideEvidenceClass.Unknown [|""|] 0. score (-1.) true true
                )
                |> Array.filter (fun out -> out.Decoy <> false)

            // Assign q values to each protein (now also includes decoy only hits)
            let combinedScoredClassesQVal =
                if qValMethod = Domain.QValueMethod.LogisticRegression then
                    let fdr = (combinedScoredClasses |> Array.filter (fun x -> x.DecoyBigger) |> Array.length |> float |> (*)2.)/(combinedScoredClasses |> Array.filter (fun x -> not x.DecoyBigger) |> Array.length |> float)
                    ProteomIQon.ProteinInference'.calculateQValueLogReg fdr combinedScoredClasses reverseNoMatch
                else
                    ProteomIQon.ProteinInference'.calculateQValueStorey combinedScoredClasses reverseNoMatch

            let graph =
                let decoy, target = combinedScoredClassesQVal |> Array.partition (fun x -> x.DecoyBigger)
                // Histogram with relative abundance
                let freqTarget = FSharp.Stats.Distributions.Frequency.create 0.01 (target |> Array.map (fun x -> x.TargetScore))
                                 |> Map.toArray
                                 |> Array.map (fun x -> fst x, (float (snd x)) / (float target.Length))
                let freqDecoy  = FSharp.Stats.Distributions.Frequency.create 0.01 (decoy |> Array.map (fun x -> x.DecoyScore))
                                 |> Map.toArray
                                 |> Array.map (fun x -> fst x, (float (snd x)) / (float target.Length))
                // Histogram with absolute values
                let freqTarget1 = FSharp.Stats.Distributions.Frequency.create 0.01 (target |> Array.map (fun x -> x.TargetScore))
                                 |> Map.toArray
                let freqDecoy1  = FSharp.Stats.Distributions.Frequency.create 0.01 (decoy |> Array.map (fun x -> x.DecoyScore))
                                 |> Map.toArray
                let histogram =
                    [
                        Chart.Column freqTarget |> Chart.withTraceName "Target"
                            |> Chart.withAxisAnchor(Y=1);
                        Chart.Column freqDecoy |> Chart.withTraceName "Decoy"
                            |> Chart.withAxisAnchor(Y=1);
                        Chart.Column freqTarget1
                            |> Chart.withAxisAnchor(Y=2)
                            |> Chart.withMarkerStyle (Opacity = 0.)
                            |> Chart.withTraceName (Showlegend = false);
                        Chart.Column freqDecoy1
                            |> Chart.withAxisAnchor(Y=2)
                            |> Chart.withMarkerStyle (Opacity = 0.)
                            |> Chart.withTraceName (Showlegend = false)
                    ]
                    |> Chart.Combine

                let sortedQValues = 
                    combinedScoredClassesQVal 
                    |> Array.map 
                        (fun x -> if x.Decoy then
                                    x.DecoyScore, x.QValue
                                  else
                                    x.TargetScore, x.QValue
                        )
                    |> Array.sortBy (fun (score, qVal) -> score)

                [
                    Chart.Point sortedQValues |> Chart.withTraceName "Q-Values";
                    histogram
                ]
                |> Chart.Combine
                |> Chart.withY_AxisStyle("Relative Frequency / Q-Value",Side=StyleParam.Side.Left,Id=1, MinMax = (0., 1.))
                |> Chart.withY_AxisStyle("Absolute Frequency",Side=StyleParam.Side.Right,Id=2,Overlaying=StyleParam.AxisAnchorId.Y 1, MinMax = (0., float target.Length))
                |> Chart.withX_AxisStyle "Score"
                |> Chart.withSize (900., 900.)
                |> Chart.SaveHtmlAs (outDirectory + @"\QValueGraph")

            // Assign results to files in which they can be found
            classifiedProteins
            |> List.iter (fun (prots,outFile) ->
                let pepSeqSet = prots |> List.map (fun x -> x.PeptideSequence) |> Set.ofList
                logger.Trace (sprintf "Start with %s"(System.IO.Path.GetFileNameWithoutExtension outFile))
                let combinedInferenceresult =
                    combinedScoredClassesQVal |> Array.filter (fun inferredPCIS -> not inferredPCIS.Decoy)
                    |> Seq.choose (fun ic ->
                        let filteredPepSet =
                            ic.PeptideSequence
                            |> Array.filter (fun pep -> Set.contains pep pepSeqSet)
                        if filteredPepSet = [||] then
                            None
                        else
                            Some (ProteomIQon.ProteinInference'.createInferredProteinClassItemScored ic.GroupOfProteinIDs ic.Class [|proteinGroupToString filteredPepSet|] ic.TargetScore ic.DecoyScore ic.QValue ic.Decoy ic.DecoyBigger)
                        )

                combinedInferenceresult
                |> FSharpAux.IO.SeqIO.Seq.CSV "\t" true true
                |> Seq.write outFile
                logger.Trace (sprintf "File written to %s" outFile)
            )
        else
            classifiedProteins
            |> List.iter2 (fun psm (sequences,outFile) ->
                logger.Trace (sprintf "start inferring %s" (System.IO.Path.GetFileNameWithoutExtension outFile))
                let inferenceResult = BioFSharp.Mz.ProteinInference.inferSequences protein peptide sequences

                // Peptide Score Map
                let peptideScoreMap = ProteomIQon.ProteinInference'.createPeptideScoreMap [psm]

                // Assigns scores to reverse digested Proteins using the peptideScoreMap
                let reverseProteinScores = ProteomIQon.ProteinInference'.createReverseProteinScores reverseProteins peptideScoreMap

                // Scores each inferred protein and assigns each protein where a reverted peptide was hit its score
                let inferenceResultScored =
                    inferenceResult
                    |> Array.ofSeq
                    |> Array.map (fun inferredPCI ->
                        // Looks up all peptides assigned to the protein and sums up their score
                        let peptideScore = (ProteomIQon.ProteinInference'.assignPeptideScores inferredPCI.PeptideSequence peptideScoreMap)
                        // Looks if the reverse protein has been randomly matched and assigns the score
                        let decoyScore = (ProteomIQon.ProteinInference'.assignDecoyScoreToTargetScore inferredPCI.GroupOfProteinIDs reverseProteinScores)

                        ProteomIQon.ProteinInference'.createInferredProteinClassItemScored
                            inferredPCI.GroupOfProteinIDs inferredPCI.Class inferredPCI.PeptideSequence
                            peptideScore
                            decoyScore
                            // Placeholder for q value
                            -1.
                            false
                            (decoyScore > peptideScore)
                        )

                // creates InferredProteinClassItemScored type for decoy proteins that have no match
                let reverseNoMatch =
                    let proteinsPresent =
                        inferenceResultScored
                        |> Array.collect (fun prots -> prots.GroupOfProteinIDs |> String.split ';')
                    reverseProteinScores
                    |> Map.toArray
                    |> Array.map (fun (protein, score) ->
                        if (proteinsPresent |> Array.contains protein) then
                            ProteomIQon.ProteinInference'.createInferredProteinClassItemScored "HasMatch" PeptideEvidenceClass.Unknown [|""|] (-1.) (-1.) (-1.) false false
                        else
                            ProteomIQon.ProteinInference'.createInferredProteinClassItemScored protein PeptideEvidenceClass.Unknown [|""|] 0. score (-1.) true true
                    )
                    |> Array.filter (fun out -> out.Decoy <> false)

                // Assign q values to each protein (now also includes decoy only hits)
                let inferenceResultScoredQVal =
                    if qValMethod = Domain.QValueMethod.LogisticRegression then
                        let fdr = (inferenceResultScored |> Array.filter (fun x -> x.DecoyBigger) |> Array.length |> float |> (*)2.)/(inferenceResultScored |> Array.filter (fun x -> not x.DecoyBigger) |> Array.length |> float)
                        ProteomIQon.ProteinInference'.calculateQValueLogReg fdr inferenceResultScored reverseNoMatch
                    else
                        ProteomIQon.ProteinInference'.calculateQValueStorey inferenceResultScored reverseNoMatch

                let graph =
                    let decoy, target = inferenceResultScoredQVal |> Array.partition (fun x -> x.DecoyBigger)
                    // Histogram with relative abundance
                    let freqTarget = FSharp.Stats.Distributions.Frequency.create 0.01 (target |> Array.map (fun x -> x.TargetScore))
                                     |> Map.toArray
                                     |> Array.map (fun x -> fst x, (float (snd x)) / (float target.Length))
                    let freqDecoy  = FSharp.Stats.Distributions.Frequency.create 0.01 (decoy |> Array.map (fun x -> x.DecoyScore))
                                     |> Map.toArray
                                     |> Array.map (fun x -> fst x, (float (snd x)) / (float target.Length))
                    // Histogram with absolute values
                    let freqTarget1 = FSharp.Stats.Distributions.Frequency.create 0.01 (target |> Array.map (fun x -> x.TargetScore))
                                     |> Map.toArray
                    let freqDecoy1  = FSharp.Stats.Distributions.Frequency.create 0.01 (decoy |> Array.map (fun x -> x.DecoyScore))
                                     |> Map.toArray
                    let histogram =
                        [
                            Chart.Column freqTarget |> Chart.withTraceName "Target"
                                |> Chart.withAxisAnchor(Y=1);
                            Chart.Column freqDecoy |> Chart.withTraceName "Decoy"
                                |> Chart.withAxisAnchor(Y=1);
                            Chart.Column freqTarget1
                                |> Chart.withAxisAnchor(Y=2)
                                |> Chart.withMarkerStyle (Opacity = 0.)
                                |> Chart.withTraceName (Showlegend = false);
                            Chart.Column freqDecoy1
                                |> Chart.withAxisAnchor(Y=2)
                                |> Chart.withMarkerStyle (Opacity = 0.)
                                |> Chart.withTraceName (Showlegend = false)
                        ]
                        |> Chart.Combine

                    let sortedQValues = 
                        inferenceResultScoredQVal 
                        |> Array.map 
                            (fun x -> if x.Decoy then
                                        x.DecoyScore, x.QValue
                                      else
                                        x.TargetScore, x.QValue
                        )
                        |> Array.sortBy (fun (score, qVal) -> score)

                    [
                        Chart.Line sortedQValues |> Chart.withTraceName "Q-Values";
                        histogram
                    ]
                    |> Chart.Combine
                    |> Chart.withY_AxisStyle("Relative Frequency / Q-Value",Side=StyleParam.Side.Left,Id=1, MinMax = (0., 1.))
                    |> Chart.withY_AxisStyle("Absolute Frequency",Side=StyleParam.Side.Right,Id=2,Overlaying=StyleParam.AxisAnchorId.Y 1, MinMax = (0., float target.Length))
                    |> Chart.withX_AxisStyle "Score"
                    |> Chart.withSize (900., 900.)
                    |> Chart.SaveHtmlAs (outFile + @"_QValueGraph")


                inferenceResultScoredQVal
                |> Array.filter (fun inferredPCIS -> not inferredPCIS.Decoy)
                |> FSharpAux.IO.SeqIO.Seq.CSV "\t" true true
                |> Seq.write outFile
            ) psmInputs

    let inferProteins gff3Location dbConnection (proteinInferenceParams: ProteinInferenceParams) outDirectory rawFolderPath =

        let logger = Logging.createLogger "ProteinInference_inferProteins"

        logger.Trace (sprintf "InputFilePath = %s" rawFolderPath)
        logger.Trace (sprintf "InputGFF3Path = %s" gff3Location)
        logger.Trace (sprintf "OutputFilePath = %s" outDirectory)
        logger.Trace (sprintf "Protein inference parameters = %A" proteinInferenceParams)

        logger.Trace "Copy peptide DB into Memory."
        let memoryDB = SearchDB.copyDBIntoMemory dbConnection
        logger.Trace "Copy peptide DB into Memory: finished."

        logger.Trace "Start building ClassItemCollection"
        let classItemCollection, psmInputs = createClassItemCollection gff3Location memoryDB proteinInferenceParams.ProteinIdentifierRegex rawFolderPath
        logger.Trace "Classify and Infer Proteins"
        readAndInferFile classItemCollection proteinInferenceParams.Protein proteinInferenceParams.Peptide 
                         proteinInferenceParams.GroupFiles outDirectory rawFolderPath psmInputs dbConnection proteinInferenceParams.QValueMethod