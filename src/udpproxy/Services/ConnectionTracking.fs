namespace UdpProxy.Services

open System
open System.Collections.Generic
open System.Net
open UdpProxy


type IConnectionTracking =

    abstract TrackConnection: client: (IPEndPoint * UdpSocket) -> upstream: UdpSocket -> unit

    abstract TryGetClient: upstream: UdpSocket -> (IPEndPoint * UdpSocket) option


type private ConnectionInfo (client: IPEndPoint, upstream: UdpSocket) =

    let mutable clientSockets = [|  |]

    let clientSocketsSet = HashSet<obj> ()

    member _.Client = client

    member _.Upstream = upstream

    member this.AddClientSocket (socket: UdpSocket) =
        lock this (fun () ->
            if clientSocketsSet.Add socket then
                clientSockets <- Array.append clientSockets [| socket |])

    member this.GetRandomClientSocket () =
        lock this (fun () -> Array.randomChoice clientSockets)


type ConnectionTracking (ttl: TimeSpan, cacheFactory: ICacheFactory) =

    let entries = lazy (cacheFactory.Create "ConnectionTracking")

    interface IConnectionTracking with

        member this.TrackConnection client upstream =
            let client, clientSocket = client

            let connectionInfo =
                match entries.Value.TryGetTouch<ConnectionInfo> upstream with
                | Some info -> info
                | None ->
                    lock this (fun () ->
                        match entries.Value.TryGetTouch<ConnectionInfo> upstream with
                        | Some info -> info
                        | None ->
                            let info = ConnectionInfo (client, upstream)
                            entries.Value.PutWithTtl upstream info ttl
                            info)

            connectionInfo.AddClientSocket clientSocket

        member _.TryGetClient upstream =
            match entries.Value.TryGetTouch<ConnectionInfo> upstream with
            | None -> None
            | Some info -> Some (info.Client, info.GetRandomClientSocket ())
