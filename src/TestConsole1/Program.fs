namespace ProteomIQon

open System
open CLIArgumentParsing
open Argu
open System.IO

module console1 =

    let appendOne (filepath:string) =
        let currentInt = (File.ReadAllText(filepath) |> int) + 1;
        File.WriteAllText(Path.GetFileName(filepath), currentInt |> string)
    
    [<EntryPoint>]
    let main argv = 
        printfn "%A" argv

        let parser = ArgumentParser.Create<CLIArguments>(programName =  (System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)) 
        let results = parser.Parse argv
        let inputFilePath = results.GetResult InputFile

        appendOne inputFilePath
        0
