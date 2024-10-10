namespace UdpProxy

open Argu


type RunArgs =
    | [<Mandatory;AltCommandLine("-c")>] Config of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Config _ -> "Path to a config file."


type GenKeyArgs =
    | [<AltCommandLine("-l")>] Length of int

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Length _ -> "Key length. Default 32 bytes."


type CliArgs =
    | [<CliPrefix("")>] Run of ParseResults<RunArgs>
    | [<CliPrefix("")>] ReadPrint of ParseResults<RunArgs>
    | [<CliPrefix("")>] GenKey of ParseResults<GenKeyArgs>

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Run _ -> "Run proxy."
            | ReadPrint _ -> "Read config file, enrich it with default values and print to stdout."
            | GenKey _ -> "Generates encryption/hmac key and prints it to stdout."
