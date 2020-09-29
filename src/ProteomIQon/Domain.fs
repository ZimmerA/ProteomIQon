namespace ProteomIQon

open BioFSharp
open BioFSharp.Mz
open MzIO.Binary

module Domain = 

    open BioFSharp.Mz.SearchDB

    type PaddingParams =
         {
            MaximumPaddingPoints    : int option
            Padding_MzTolerance     : float
            WindowSize              : int
            SpacingPerc             : float 
         }

    type YThreshold = 
        | Fixed of float
        | MinSpectrumIntensity

    type WaveletPeakPickingParams = 
        {
            /// Centroidization
            NumberOfScales          : int
            YThreshold              : YThreshold
            Centroid_MzTolerance    : float
            SNRS_Percentile         : float
            MinSNR                  : float
            PaddingParams           : PaddingParams option 
        } 

    type CentroidizationMode =
        | Manufacturer
        | Wavelet of WaveletPeakPickingParams
        
    type PeakPicking = 
        | ProfilePeaks
        | Centroid of CentroidizationMode 

    type PreprocessingParams =
        {
            Compress                    : BinaryDataCompressionType
            StartRetentionTime          : float option 
            EndRetentionTime            : float option 
            MS1PeakPicking              : PeakPicking
            MS2PeakPicking              : PeakPicking
        }

    type PeptideDBParams = 
        {
        Name                : string
        FastaPath           : string
        FastaHeaderToName   : string -> string
        Protease            : Digestion.Protease
        MinMissedCleavages  : int
        MaxMissedCleavages  : int
        MaxMass             : float
        MinPepLength        : int
        MaxPepLength        : int
        IsotopicMod         : SearchInfoIsotopic list 
        MassMode            : MassMode
        MassFunction        : IBioItem -> float  
        FixedMods           : SearchModification list            
        VariableMods        : SearchModification list
        VarModThreshold     : int
        }

    type NTerminalSeries = ((IBioItem -> float) -> AminoAcids.AminoAcid list -> PeakFamily<TaggedMass.TaggedMass> list)
    
    type CTerminalSeries = ((IBioItem -> float) -> AminoAcids.AminoAcid list -> PeakFamily<TaggedMass.TaggedMass> list)

    type AndromedaParams = {
        /// selects the minimum and maximum amount of peaks retained in a 100 Da window, all combinations are tested and the best result is kept.
        PMinPMax                : int*int
        MatchingIonTolerancePPM : float       
        }

    type PeptideSpectrumMatchingParams = 
        {
            // Charge Determination Params
            ChargeStateDeterminationParams  : ChargeState.ChargeDetermParams             
            // +/- ppm of ion m/z to obtain target peptides from SearchDB. 
            LookUpPPM                       : float
            // lowest m/z, highest m/z
            MS2ScanRange                    : float*float
            nTerminalSeries                 : NTerminalSeries
            cTerminalSeries                 : CTerminalSeries
            AndromedaParams                 : AndromedaParams
            ///
        }

    type PSMStatisticsParams = 
        {
            QValueThreshold             : float
            PepValueThreshold           : float
            FastaHeaderToName           : string -> string
            KeepTemporaryFiles          : bool
        }

    type WindowSize = 
        | Fixed of int
        | EstimateUsingAutoCorrelation of float
    
    type SecondDerivativeParams = 
        {
            MinSNR                       : float  
            PolynomOrder                 : int
            WindowSize                   : WindowSize
        }
    
    type WaveletParameters = FSharpStats'.Wavelet.Parameters 

    type XicProcessing = 
        | SecondDerivative of SecondDerivativeParams
        | Wavelet of WaveletParameters
        
    type XicExtraction = 
        {
            ScanTimeWindow               : float 
            MzWindow_Da                  : float 
            XicProcessing                : XicProcessing
        }
       
    type BaseLineCorrection = 
        {
            MaxIterations                : int 
            Lambda                       : int 
            P                            : float 
        }

    type QuantificationParams = 
        {
            PerformLabeledQuantification : bool
            XicExtraction                : XicExtraction
            BaseLineCorrection           : BaseLineCorrection option
        }

    type AlignmentBasedQuantificationParams = 
        {
            PerformLabeledQuantification : bool
            PerformLocalWarp             : bool
            XicExtraction                : XicExtraction
            BaseLineCorrection           : BaseLineCorrection option
        }

    type FDRMethod =
        |Conservative
        |MAYU
        |DecoyTargetRatio

    type QValueMethod =
        |Storey
        |LogisticRegression of FDRMethod

    type ProteinInferenceParams = 
        {
            ProteinIdentifierRegex : string
            Protein                : ProteinInference.IntegrationStrictness
            Peptide                : ProteinInference.PeptideUsageForQuantification
            GroupFiles             : bool
            GetQValue              : QValueMethod
        }

    type SpectralLibraryParams =
        {
            ChargeList          : float list
            MatchingTolerancePPM: float
        }

    type FilterOnField =
        {
            FieldName  : string
            UpperBound : float option
            LowerBound : float option
        }

    module FilterOnField =

        let create fieldName upperBound lowerBound =
            {
                FieldName  = fieldName
                UpperBound = upperBound
                LowerBound = lowerBound
            }

    type EssentialFields =
        {
            Light       : string
            Heavy       : string option
            ProteinIDs  : string
            PepSequence : string
            PepSequences: string
        }

    module EssentialFields =

        let create light heavy proteinIDs pepSequence pepSequences =
            {
                Light       = light
                Heavy       = heavy
                ProteinIDs  = proteinIDs
                PepSequence = pepSequence
                PepSequences= pepSequences
            }

    type AggregationMethod =
        |Sum
        |Mean
        |Median

    type Transform =
        |Log10
        |Log2
        |Ln
        |NoTransform

    type StatisticalMeasurement =
        |SEM
        |StDev
        |CV

    type TableSortParams =
        {
            SeparatorIn                 : string
            SeparatorOut                : char
            EssentialFields             : EssentialFields
            QuantFieldsToFilterOn       : FilterOnField[]
            ProtFieldsToFilterOn        : FilterOnField[]
            QuantColumnsOfInterest      : string[]
            ProtColumnsOfInterest       : string[]
            StatisticalMeasurements     : (string*StatisticalMeasurement)[]
            AggregatorFunction          : AggregationMethod
            AggregatorFunctionIntensity : AggregationMethod
            AggregatorPepToProt         : AggregationMethod
            Tukey                       : (string*float*Transform) []
        }
   

    type ConsensusSpectralLibraryParams =
        {
            RTTolerance: float
            iRTPeptides: string list
        }

    type SpectrumSelection =
        |First
        |All

    type SWATHAnalysisParams =
        {
            PeptideList         : string [] option
            MatchingTolerancePPM: float
            QueryOffsetRange    : float
            SpectrumSelectionF  : SpectrumSelection
            AggregationF        : AggregationMethod
            XicProcessing       : XicProcessing
        }
