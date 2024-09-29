module UdpProxy.Exceptions

open Printf


exception DebugMessageError of string


let raiseMsg fmt =
    ksprintf (fun msg -> raise (DebugMessageError msg)) fmt
