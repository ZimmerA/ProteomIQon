namespace ProteomIQon

open System
open System.IO
open CLIArgumentParsing
open Argu
open QuantBasedAlignment
open System.Reflection
open ProteomIQon.Core.InputPaths

module console1 =
    open BioFSharp.Mz

    [<EntryPoint>]
    let main argv =
        let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some System.ConsoleColor.Red)
        let parser = ArgumentParser.Create<CLIArguments>(programName =  (System.Reflection.Assembly.GetExecutingAssembly().GetName().Name),errorHandler=errorHandler)     
        let directory = Environment.CurrentDirectory
        let getPathRelativeToDir = getRelativePath directory
        let results = parser.Parse argv
        let i = results.GetResult QuantifiedPeptides |> getPathRelativeToDir
        let o = results.GetResult OutputDirectory    |> getPathRelativeToDir
        let p = results.GetResult ParamFile          |> getPathRelativeToDir
        Logging.generateConfig o
        let logger = Logging.createLogger "QuantBasedAlignment"
        logger.Info (sprintf "InputFilePath -i = %s" i)
        logger.Info (sprintf "OutputFilePath -o = %s" o)
        logger.Info (sprintf "ParamFilePath -p = %s" p)
        logger.Trace (sprintf "CLIArguments: %A" results)
        Directory.CreateDirectory(o) |> ignore
        //let p =
        //    Json.ReadAndDeserialize<Dto.QuantificationParams> p
        //    |> Dto.QuantificationParams.toDomain
        if File.Exists i then
            logger.Info "single file detected"
            failwithf "%s is a file path, please specify a directory containing .quant files" i
        elif Directory.Exists i then
            logger.Info "directory found"
            let quantfiles =
                Directory.GetFiles(i,("*.quant"))
            logger.Trace (sprintf ".quant files : %A" quantfiles)
            let c =
                match results.TryGetResult Parallelism_Level with
                | Some c    -> c
                | None      -> 1
            logger.Trace (sprintf "Program is running on %i cores" c)
            alignFiles logger {Placeholder=true} o i
            |> ignore
        else
            failwith "The given path to the instrument output is neither a valid file path nor a valid directory path."
        logger.Info "Done"
        0
