namespace ProteomIQon

open Argu

module CLIArgumentParsing = 
    open System.IO

    type CLIArguments =
        | [<Mandatory>] [<AltCommandLine("-i")>]  QuantifiedPeptides of path:string
        | [<Mandatory>] [<AltCommandLine("-o")>]  OutputDirectory  of path:string 
        | [<Mandatory>] [<AltCommandLine("-p")>]  ParamFile of path:string
        | [<Unique>]    [<AltCommandLine("-c")>]  Parallelism_Level of level:int
        | [<Unique>]    [<AltCommandLine("-l")>]  Log_Level of level:int
        | [<Unique>]    [<AltCommandLine("-v")>]  Verbosity_Level of level:int
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | QuantifiedPeptides _  -> "Specify quantified peptides, specify a directory containing .quant files."
                | OutputDirectory  _    -> "Specify the output directory."
                | ParamFile _           -> "Specify parameter file for peptide spectrum matching."
                | Parallelism_Level _   -> "Set the number of cores the programm can use. Parallelization occurs on file level. This flag is only of effect if a input directory (-i) is specified."
                | Log_Level _           -> "Set the log level."
                | Verbosity_Level _     -> "Set the verbosity level."
