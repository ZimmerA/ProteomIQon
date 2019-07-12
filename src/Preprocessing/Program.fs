namespace ProteomIQon

open System.IO
open CLIArgumentParsing
open Argu

module console1 =

    [<EntryPoint>]
    let main argv = 
        printfn "%A" argv

        let parser = ArgumentParser.Create<CLIArguments>(programName =  (System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)) 
        let usage  = parser.PrintUsage()
        printfn "%s" usage
        let results = parser.Parse argv
        let i = results.GetResult InstrumentOutput
        let o = results.GetResult OutputDirectory
        let p = results.GetResult ParamFile     
        printfn "InputFilePath -i = %s" i
        printfn "InputFilePath -o = %s" o
        printfn "InputFilePath -p = %s" p
        Directory.CreateDirectory(o) |> ignore
        let processParams = 
                Json.ReadAndDeserialize<Dto.PreprocessingParams> p
                |> Dto.PreprocessingParams.toDomain  
        
        if File.Exists i || (Directory.Exists i && i.EndsWith(".d"))  then
            printfn "singleFile"
            Preprocessing.processFile processParams o i
        elif Directory.Exists i then 
            printfn "multiple files"
            let files = 
                Core.MzLite.Reader.getMSFilePaths i             
            let c = 
                match results.TryGetResult Parallelism_Level with 
                | Some c    -> c
                | None      -> 1
            files 
            |> FSharpAux.PSeq.map (Preprocessing.processFile processParams o) 
            |> FSharpAux.PSeq.withDegreeOfParallelism c
            |> Array.ofSeq
            |> ignore
        else 
            failwith "The given path to the instrument output is neither a valid file path nor a valid directory path."
        System.Console.ReadKey() |> ignore
        printfn "Hit any key to exit."
        0
        
