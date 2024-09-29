namespace UdpProxy.Services

open System
open System.Collections.Generic
open System.Net
open UdpProxy


type IConnectionTracking =

    abstract TrackClient: IPEndPoint -> UdpSocket -> unit

    abstract TryGetClientSocket: IPEndPoint -> UdpSocket option


type private ClientSockets () =

    let mutable sockets = [|  |]

    let set = HashSet<obj> ()


    member this.AddSocket (socket: UdpSocket) =
        lock this (fun () ->
            if set.Add socket then
                sockets <- Array.append sockets [| socket |])


    member this.GetRandomSocket () =
        lock this (fun () -> Array.randomChoice sockets)


type ConnectionTracking (ttl: TimeSpan, cacheFactory: ICacheFactory) =

    let entries = lazy (cacheFactory.Create ())

    interface IConnectionTracking with

        member this.TrackClient clientEndpoint clientSocket =
            ArgumentNullException.ThrowIfNull (clientEndpoint, nameof(clientEndpoint))
            ArgumentNullException.ThrowIfNull (clientSocket, nameof(clientSocket))

            let clientSockets =
                match entries.Value.TryGetTouch clientEndpoint : ClientSockets option with
                | Some sockets -> sockets
                | None ->
                    lock this (fun () ->
                        match entries.Value.TryGetTouch clientEndpoint : ClientSockets option with
                        | Some sockets -> sockets
                        | None ->
                            let sockets = ClientSockets ()
                            entries.Value.PutWithTtl clientEndpoint sockets ttl
                            sockets)

            clientSockets.AddSocket clientSocket

        member _.TryGetClientSocket clientEndpoint =
            ArgumentNullException.ThrowIfNull (clientEndpoint, nameof(clientEndpoint))

            match entries.Value.TryGetTouch clientEndpoint : ClientSockets option with
            | None -> None
            | Some sockets -> Some (sockets.GetRandomSocket ())
