namespace UdpProxy

open Argu


type RunArgs =
    | [<Mandatory;AltCommandLine("-c")>] Config of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Config _ -> "Path to a config file."


type CliArgs =
    | [<CliPrefix("")>] Run of ParseResults<RunArgs>
    | [<CliPrefix("")>] ReadPrint of ParseResults<RunArgs>

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Run _ -> "Run proxy."
            | ReadPrint _ -> "Read config file, enrich it with default values and print to stdout."
