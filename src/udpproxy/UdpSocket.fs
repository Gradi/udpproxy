namespace UdpProxy

open Serilog
open System
open System.Collections.Concurrent
open System.IO.Hashing
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
            let crc32 = Crc32.HashToUInt32 this.Payload
            sprintf "udp<%d bytes, crc32: 0x%x, from: %O -> to: %O>" this.Length crc32 this.SourceEndpoint this.LocalSocket.LocalEndpoint


and UdpSocket (localEndpoint: Choice<IPEndPoint, AddressFamily>, bufferSize: int, onNewPacket : UdpPacket -> Async<unit>, logger: ILogger) as this =

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


    let mutable receivingTask = None


    let sendQueue = ConcurrentBag<struct (byte array * IPEndPoint)> ()
    let newPacketEvent = new AutoResetEvent (false)
    let sendingTask =
        async {
            let waitHandles = [| cancelToken.Token.WaitHandle; newPacketEvent |]
            let token = cancelToken.Token

            while not token.IsCancellationRequested do
                try
                    WaitHandle.WaitAny waitHandles |> ignore

                    let mutable isContinue = true
                    while isContinue && (not token.IsCancellationRequested) do
                        isContinue <-
                            match sendQueue.TryTake () with
                            | false, _ -> false
                            | true, struct (payload, remoteEndpoint) ->
                                socket.Value.SendTo (ArraySegment<byte> payload, remoteEndpoint) |> ignore
                                true

                with
                | :? OperationCanceledException -> ()
                | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) -> ()
                | exc ->
                    logger.Error(exc, "UdpSocket<{$LocalEndpoint}>: Send error {ErrorMessage}", socket.Value.LocalEndPoint, exc.Message)
                    do! Async.Sleep (TimeSpan.FromSeconds 1.0)
        }
        |> Async.StartAsTask
        :> Task


    member _.LocalEndpoint = socket.Value.LocalEndPoint :?> IPEndPoint

    member _.Start () =
        match receivingTask with
        | Some _ -> ()
        | None ->

        socket.Value |> ignore
        receivingTask <-
            async {
                let buffer : byte array = Array.zeroCreate bufferSize
                let token = cancelToken.Token

                while not token.IsCancellationRequested do
                    try

                        let! message =
                            socket.Value.ReceiveMessageFromAsync(ArraySegment<byte> buffer, SocketFlags.None, anyEndpoint.Value, cancelToken.Token)
                             .AsTask()
                            |> Async.AwaitTask

                        if message.ReceivedBytes > 0 && message.RemoteEndPoint :? IPEndPoint then
                            let udpPayload : byte array = Array.zeroCreate message.ReceivedBytes
                            Array.Copy (buffer, 0, udpPayload, 0, message.ReceivedBytes)
                            receiveUdpPacket udpPayload (message.RemoteEndPoint :?> IPEndPoint)

                    with
                    | :? OperationCanceledException -> ()
                    | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) -> ()
                    | exc ->
                        logger.Error (exc, "UdpSocket<{$LocalEndpoint}>: Receive error: {ErrorMessage}", socket.Value.LocalEndPoint, exc.Message)
                        do! Async.Sleep (TimeSpan.FromSeconds 3.0)
            }
            |> Async.StartAsTask
            :> Task
            |> Some

    member _.Send (buffer: byte array) (endpoint: IPEndPoint) =
        async {
            sendQueue.Add (struct (buffer, endpoint))
            newPacketEvent.Set () |> ignore
        }

    interface IDisposable with

        member this.Dispose () =
            logger.Information ("UdpSocket<{$LocalEndpoint}>: Disposing...", if socket.IsValueCreated then box socket.Value.LocalEndPoint else box "<lazy not created>")

            cancelToken.Cancel ()
            newPacketEvent.Set () |> ignore

            match receivingTask with
            | Some task -> task.Wait ()
            | None -> ()
            sendingTask.Wait ()

            if socket.IsValueCreated then
                socket.Value.Dispose ()

            cancelToken.Dispose ()
            newPacketEvent.Dispose ()
