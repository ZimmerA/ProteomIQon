namespace ProteomIQon

open Argu

module CLIArgumentParsing = 
    open System.IO
    // Quantification
    type CLIArguments =
        | [<AltCommandLine("-i")>] Input1 of path:string
        | [<AltCommandLine("-i2")>] Input2 of path:string
        | [<AltCommandLine("-o")>] OutDir1  of path:string 
        | [<AltCommandLine("-d")>] Database of path:string
        | [<AltCommandLine("-p")>] ParamFile of path:string
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Input1 _ -> "Input 1"
                | Input2 _ -> "Input 2"
                | OutDir1  _ -> "Output 1"
                | Database _        -> "Peptide Database"
                | ParamFile _        -> "Param File"
