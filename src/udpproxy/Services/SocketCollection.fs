namespace UdpProxy.Services

open System.Net.Sockets
open Serilog
open System
open System.Net
open UdpProxy


type IPacketHandler =

    abstract HandleClientPacket : UdpPacket -> Async<unit>

    abstract HandleUpstreamPacket : UdpPacket -> Async<unit>


type ISocketCollection =

    abstract GetUniqueOutputSocket : key: obj -> AddressFamily -> UdpSocket

    abstract StartAllClientSockets : unit -> unit


type SocketCollection (inputEndpoints: IPEndPoint list, connectionTtl: TimeSpan, logger: ILogger,
                       cacheFactory: ICacheFactory, packetHandler: Lazy<IPacketHandler>) =

    let logger = logger.ForContext<SocketCollection> ()
    let upstreamSocketsCache = lazy (cacheFactory.Create "UdpSockets")

    let clientSockets =
        lazy (
            inputEndpoints
            |> List.map (fun en -> new UdpSocket (Choice1Of2 en, 1024 * 1024, packetHandler.Value.HandleClientPacket, logger))
            |> Array.ofList)


    interface ISocketCollection with

        member this.GetUniqueOutputSocket key addressFamily =
            let key = box (key, addressFamily)
            match upstreamSocketsCache.Value.TryGetTouch<UdpSocket> key with
            | Some socket -> socket
            | None ->
                lock this (fun () ->
                    match upstreamSocketsCache.Value.TryGetTouch<UdpSocket> key with
                    | Some socket -> socket
                    | None ->
                        let socket = new UdpSocket (Choice2Of2 addressFamily, 1024 * 1024, packetHandler.Value.HandleUpstreamPacket, logger)
                        upstreamSocketsCache.Value.PutWithTtl key socket connectionTtl
                        socket.Start ()
                        socket)


        member this.StartAllClientSockets () =
            clientSockets.Value
            |> Array.iter _.Start()

            let enp =
                clientSockets.Value
                |> Array.map _.LocalEndpoint.ToString()
                |> String.concat ", "

            logger.Information ("SocketCollection: Listening for connections on [{ListenEndpoints}].", enp)

    interface IDisposable with

        member this.Dispose () =
            match clientSockets.IsValueCreated with
            | false -> ()
            | true ->
                logger.Debug "SocketCollection: Disposing listening sockets..."
                clientSockets.Value
                |> Array.iter (fun s -> (s :> IDisposable).Dispose ())
