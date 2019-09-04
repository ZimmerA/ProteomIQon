namespace ProteomIQon

open System
open CLIArgumentParsing
open Argu
open System.IO

module console1 =

    let appendWorld (filepath:string) =
        File.WriteAllText("World.txt",File.ReadAllText(filepath) + " you?")
    
    [<EntryPoint>]
    let main argv = 
        printfn "%A" argv

        let parser = ArgumentParser.Create<CLIArguments>(programName =  (System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)) 
        let results = parser.Parse argv
        let inputFilePath = results.GetResult InputFile

        appendWorld inputFilePath
        0
