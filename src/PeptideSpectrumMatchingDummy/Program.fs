namespace ProteomIQon

open System
open CLIArgumentParsing
open Argu
open System.IO
module console1 =
    let timeNow = System.DateTime.Now.ToLongTimeString()
    let processFile (inFilePath:string, outFilePath:string) =
        let inText = File.ReadAllText(inFilePath)
        let startTime = timeNow

        // Simulate long running task
        System.Threading.Thread.Sleep(3000)
        let outText =  inText + "\nspectrumMatching: " + startTime + "-" + timeNow

        let fileInfo = new System.IO.FileInfo(outFilePath + Path.GetFileNameWithoutExtension(inFilePath)+ ".ptsm");
        fileInfo.Directory.Create();
        File.WriteAllText(fileInfo.FullName, outText)

    [<EntryPoint>]
    let main argv = 
        printfn "%A" argv

        let parser = ArgumentParser.Create<CLIArguments>(programName =  (System.Reflection.Assembly.GetExecutingAssembly().GetName().Name)) 
        let results = parser.Parse argv
        let inputFilePath = results.GetResult Input1
        let outputFilePath = results.GetResult OutDir1
        processFile(inputFilePath, outputFilePath)
        0