namespace UdpProxy

open Serilog
open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

[<NoComparison;NoEquality>]
type UdpPacket =
    { SourceEndpoint: IPEndPoint
      LocalSocket: UdpSocket
      Payload: byte array }

        member this.Length = Array.length this.Payload

        override this.ToString () =
            sprintf "udp<%d bytes, %O->%O>" this.Length this.SourceEndpoint this.LocalSocket.LocalEndpoint

and private SocketStatus =
    | Created
    | Receiving
    | Disposed

and UdpSocket (localEndpoint: Choice<IPEndPoint, AddressFamily>, bufferSize: int, onNewPacket : UdpPacket -> Async<unit>, logger: ILogger) as this =

    let mutable status = Created
    let mutable receivingTask = Task.CompletedTask

    let logger = logger.ForContext<UdpSocket>()
    let cancelToken = new CancellationTokenSource ()

    let anyEndpoint = lazy (
        let addressFamily =
            match localEndpoint with
            | Choice1Of2 ip -> ip.AddressFamily
            | Choice2Of2 family -> family

        match addressFamily with
        | AddressFamily.InterNetwork -> IPEndPoint (IPAddress.Any, 0)
        | AddressFamily.InterNetworkV6 -> IPEndPoint (IPAddress.IPv6Any, 0)
        | v -> failwithf "Address family \"%O\" is not supported. Only IPv4 or IPv6." v)


    let socket = lazy (
        let socket = new Socket (anyEndpoint.Value.AddressFamily, SocketType.Dgram, ProtocolType.Udp)

        match anyEndpoint.Value.AddressFamily with
        | AddressFamily.InterNetwork ->
            socket.SetSocketOption (SocketOptionLevel.IP, SocketOptionName.PacketInformation, true)
        | AddressFamily.InterNetworkV6 ->
            socket.SetSocketOption (SocketOptionLevel.IPv6, SocketOptionName.PacketInformation, true)
        | _ -> ()
        socket.SendBufferSize <- bufferSize
        socket.ReceiveBufferSize <- bufferSize

        match localEndpoint with
        | Choice1Of2 ip ->
            socket.Bind ip
        | Choice2Of2 _ ->
            socket.Bind anyEndpoint.Value

        logger.Debug ("UdpSocket<{$LocalEndpoint}>: Socket created.", socket.LocalEndPoint)
        socket)

    let checkDisposed () =
        if status = Disposed then
            raise (ObjectDisposedException "Udp Socket is disposed.")

    let receiveUdpPacket (buffer: byte array) (sourceEndpoint: IPEndPoint) =
        async {
            try
                do! onNewPacket { SourceEndpoint = sourceEndpoint
                                  LocalSocket = this
                                  Payload = buffer }
            with
            | exc ->
                logger.Error (exc, "UdpSocket<{$LocalEndpoint}>: On new packet error.", socket.Value.LocalEndPoint)
        }
        |> Async.StartAsTask
        |> ignore

    member _.LocalEndpoint =
        checkDisposed ()
        socket.Value.LocalEndPoint :?> IPEndPoint

    member _.Start () =
        lock this (fun () ->
            checkDisposed ()
            if status = Receiving then
                ()
            else

                socket.Value |> ignore
                status <- Receiving
                receivingTask <-
                    async {
                        try
                            let buffer : byte array = Array.zeroCreate bufferSize

                            while not cancelToken.IsCancellationRequested do
                                let! message =
                                    socket.Value.ReceiveMessageFromAsync(ArraySegment<byte> buffer, SocketFlags.None, anyEndpoint.Value, cancelToken.Token)
                                     .AsTask()
                                    |> Async.AwaitTask

                                if message.ReceivedBytes > 0 && message.RemoteEndPoint :? IPEndPoint then
                                    let udpPayload : byte array = Array.zeroCreate message.ReceivedBytes
                                    Array.Copy (buffer, 0, udpPayload, 0, message.ReceivedBytes)
                                    receiveUdpPacket udpPayload (message.RemoteEndPoint :?> IPEndPoint)

                                Array.Clear buffer

                        with
                        | :? OperationCanceledException -> ()
                        | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) -> ()
                        | exc ->
                            logger.Error (exc, "UdpSocket<{$LocalEndpoint}>: Receive error. Socket now is not receiving.", socket.Value.LocalEndPoint)
                    }
                    |> Async.StartAsTask)

    member _.Send (buffer: byte array) (endpoint: IPEndPoint) =
        async {
            lock this (fun () ->
                checkDisposed ()
                socket.Value.SendTo (buffer, endpoint) |> ignore)
            return ()
        }

    interface IDisposable with

        member this.Dispose () =
            lock this (fun () ->
                if status <> Disposed then
                    status <- Disposed

                    cancelToken.Cancel ()
                    receivingTask.Wait ()

                    match socket.IsValueCreated with
                    | false -> ()
                    | true ->
                        logger.Debug ("UdpSocket<{$LocalEndpoint}>: Disposing...", socket.Value.LocalEndPoint)
                        socket.Value.Dispose ()

                    cancelToken.Dispose ())
