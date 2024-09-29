module UdpProxy.JObject

open System
open Newtonsoft.Json.Linq


let getProp<'a> (name: string) (jobject: JObject) =
    match jobject.TryGetValue name with
    | false, _ -> failwithf "Missing property \"%s\" in JObject." name
    | true, value ->
        try
            value.ToObject<'a> ()
        with
        | exc -> raise (Exception(sprintf "Can't convert JToken of \"%O\" type into .NET type \"%O\"" value.Type typeof<'a>, exc))


let tryGetProp<'a> (name: string) (jobject: JObject) =
    match jobject.TryGetValue name with
    | false, _ -> None
    | true, value ->
        try
            Some <| value.ToObject<'a> ()
        with
        | exc -> raise (Exception (sprintf "Can't convert JToken of \"%O\" type into .NET type \"%O\"" value.Type typeof<'a>, exc))

