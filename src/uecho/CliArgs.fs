namespace UEcho


open Argu


type ServerArgs =
    | Ip of string
    | [<Mandatory>] Port of int
    | [<AltCommandLine("-v")>] Verbose

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Ip _ -> "Ip to listen on."
            | Port _ -> "Port to listen on."
            | Verbose -> "Be more verbose."


type OutputFormat =
    | Newline
    | Oneline
    | Csv


type ClientArgs =
    | [<Mandatory>] Host of string
    | [<Mandatory>] Port of int
    | [<AltCommandLine("-i")>] InteractiveMode
    | [<AltCommandLine("-p")>] Pattern of string
    | [<AltCommandLine("-s")>] SleepMs of int
    | OutputFormat of OutputFormat

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Host _ -> "Host to connect to."
            | Port _ -> "Port to connect to."
            | InteractiveMode -> "Run in interactive mode."
            | Pattern _ -> "Pattern to send when running in non interactive mode."
            | SleepMs _ -> "Sleep(milliseconds) between send receive cycles in non interactive mode."
            | OutputFormat _ -> "Output format when running in non interactive mode."


type CliArgs =
    | [<CliPrefix("")>] Server of ParseResults<ServerArgs>
    | [<CliPrefix("")>] Client of ParseResults<ClientArgs>

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Server _ -> "Start in server mode."
            | Client _ -> "Start in client mode."

