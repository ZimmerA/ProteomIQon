namespace ProteomIQon

open System
open CLIArgumentParsing
open Argu
open System.IO
module console1 =
    let processFile (inFilePath:string, outFilePath:string) =
        let inText = File.ReadAllText(inFilePath)
        let startTime = System.DateTime.Now.ToLongTimeString()
        // Simulate long running task
        System.Threading.Thread.Sleep(3000)
        let endTime = System.DateTime.Now.ToLongTimeString()
        let outText =  inText + "\npreprocessing: " + startTime + "-" + endTime

        let fileInfo = new System.IO.FileInfo(outFilePath + Path.GetFileNameWithoutExtension(inFilePath)+ ".txt");
        fileInfo.Directory.Create();
        File.WriteAllText(fileInfo.FullName, outText)

    [<EntryPoint>]
    let main argv = 
        printfn "%A" argv

        let parser = ArgumentParser.Create<CLIArguments>(programName =  (System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)) 
        let results = parser.Parse argv
        let inputFilePath = results.GetResult InstrumentOutput
        let outputFilePath = results.GetResult OutputDirectory
        processFile(inputFilePath, outputFilePath)
        0
