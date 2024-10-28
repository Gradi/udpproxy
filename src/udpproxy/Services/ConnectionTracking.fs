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
#if EventSourceProviders
        ConnectionTrackingEventSource.Instance.AddClientSocket ()
#endif
        lock this (fun () ->
            if clientSocketsSet.Add socket then
                clientSockets <- Array.append clientSockets [| socket |])

    member this.GetRandomClientSocket () =
#if EventSourceProviders
        ConnectionTrackingEventSource.Instance.GetRandomClientSocket ()
#endif
        lock this (fun () -> Array.randomChoice clientSockets)


type ConnectionTracking (ttl: TimeSpan, cacheFactory: ICacheFactory) =

#if EventSourceProviders
    do
        ConnectionTrackingEventSource.Instance.Created ()
#endif

    let entries = lazy (cacheFactory.Create "ConnectionTracking")

    interface IConnectionTracking with

        member this.TrackConnection client upstream =
#if EventSourceProviders
            ConnectionTrackingEventSource.Instance.TrackConnectionEnter ()
#endif
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
#if EventSourceProviders
            ConnectionTrackingEventSource.Instance.TrackConnectionExit ()
#endif

        member _.TryGetClient upstream =
#if EventSourceProviders
            ConnectionTrackingEventSource.Instance.TryGetClientEnter ()
#endif
            let result =
                match entries.Value.TryGetTouch<ConnectionInfo> upstream with
                | None -> None
                | Some info -> Some (info.Client, info.GetRandomClientSocket ())
#if EventSourceProviders
            match result with
            | None -> ConnectionTrackingEventSource.Instance.TryGetClientNone ()
            | Some _ -> ConnectionTrackingEventSource.Instance.TryGetClientSome ()
            ConnectionTrackingEventSource.Instance.TryGetClientExit ()
#endif
            result

