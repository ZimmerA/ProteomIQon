namespace ProteomIQon

open Argu

module CLIArgumentParsing = 
    open System.IO
  
    type CLIArguments =
        | [<AltCommandLine("-i")>] InputFile of path:string
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | InputFile _ -> "specify mass spectrometry Output"
