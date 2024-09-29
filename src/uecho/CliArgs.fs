namespace UEcho


open Argu


type ServerArgs =
    | Ip of string
    | [<Mandatory>] Port of int

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Ip _ -> "Ip to listen on."
            | Port _ -> "Port to listen on."


type ClientArgs =
    | [<Mandatory>] Host of string
    | [<Mandatory>] Port of int

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Host _ -> "Host to connect to."
            | Port _ -> "Port to connect to."


type CliArgs =
    | [<CliPrefix("")>] Server of ParseResults<ServerArgs>
    | [<CliPrefix("")>] Client of ParseResults<ClientArgs>

    interface IArgParserTemplate with

        member this.Usage =
            match this with
            | Server _ -> "Start in server mode."
            | Client _ -> "Start in client mode."

