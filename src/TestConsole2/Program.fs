namespace ProteomIQon

open System
open CLIArgumentParsing
open Argu
open System.IO

module console1 =
    let timeNow = System.DateTime.Now.ToLongTimeString()
    let appendTwo (filepath:string) =
        let mutable text = timeNow

        for i in 1..20 do
            System.Threading.Thread.Sleep(3000)
        
        text <- text + "  now: " + timeNow
        File.WriteAllText(Path.GetFileName(filepath), timeNow)
    
    [<EntryPoint>]
    let main argv = 
        printfn "%A" argv

        let parser = ArgumentParser.Create<CLIArguments>(programName =  (System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)) 
        let results = parser.Parse argv
        let inputFilePath = results.GetResult InputFile

        appendTwo inputFilePath
        0
