namespace UdpProxy

#if EventSourceProviders

open System.Diagnostics.Tracing


[<EventSource(Name = "Pipeline")>]
type PipelineEventSource private () as this =

    inherit EventSource ()

    static member Instance = new PipelineEventSource ()


    [<Event(1)>]
    member _.RndPadCreated () = this.WriteEvent 1

    [<Event(2)>]
    member _.RndPadRandomGeneration () = this.WriteEvent 2

    [<Event(3)>]
    member _.ForwardRndPad () = this.WriteEvent 3

    [<Event(4)>]
    member _.ReverseRndPad () = this.WriteEvent 4


    [<Event(5)>]
    member _.MailmanCreated () = this.WriteEvent 5

    [<Event(6)>]
    member _.ForwardMailman () = this.WriteEvent 6

    [<Event(7)>]
    member _.ReverseMailman () = this.WriteEvent 7


    [<Event(8)>]
    member _.PacketReturnCreated () = this.WriteEvent 8

    [<Event(9)>]
    member _.ForwardPacketReturn () = this.WriteEvent 9

    [<Event(10)>]
    member _.ReversePacketReturn () = this.WriteEvent 10


    [<Event(11)>]
    member _.LZ4Created () = this.WriteEvent 11

    [<Event(12)>]
    member _.LZ4Encode () = this.WriteEvent 12

    [<Event(13)>]
    member _.LZ4Decode () = this.WriteEvent 13

    [<Event(14)>]
    member _.ForwardLZ4 () = this.WriteEvent 14

    [<Event(15)>]
    member _.ReverseLZ4 () = this.WriteEvent 15


    [<Event(16)>]
    member _.AlignerCreated () = this.WriteEvent 16

    [<Event(17)>]
    member _.AlignerRandomGeneration () = this.WriteEvent 17

    [<Event(18)>]
    member _.ForwardAligner () = this.WriteEvent 18

    [<Event(19)>]
    member _.ReverseAligner () = this.WriteEvent 19


    [<Event(20)>]
    member _.AesCreated () = this.WriteEvent 20

    [<Event(21)>]
    member _.AesAesCreated () = this.WriteEvent 21

    [<Event(22)>]
    member _.AesEncrypt () = this.WriteEvent 22

    [<Event(23)>]
    member _.AesHmacHash () = this.WriteEvent 23

    [<Event(24)>]
    member _.AesDecrypt () = this.WriteEvent 24

    [<Event(25)>]
    member _.ForwardAes () = this.WriteEvent 25

    [<Event(26)>]
    member _.ReverseAes () = this.WriteEvent 26


[<EventSource(Name = "App")>]
type AppEventSource private () as this =

    inherit EventSource ()

    static member Instance = new AppEventSource ()


    [<Event(1)>]
    member _.Created () = this.WriteEvent 1

    [<Event(2)>]
    member _.ForwardEntrypointEnter () = this.WriteEvent 2

    [<Event(3)>]
    member _.ForwardEntrypointExit () = this.WriteEvent 3

    [<Event(4)>]
    member _.ReverseEntrypointEnter () = this.WriteEvent 4

    [<Event(5)>]
    member _.ReverseEntrypointExit () = this.WriteEvent 5

    [<Event(6)>]
    member _.Run () = this.WriteEvent 6

    [<Event(7)>]
    member _.Stop () = this.WriteEvent 7


[<EventSource(Name = "UdpSocket")>]
type UdpSocketEventSource private () as this =

    inherit EventSource ()

    static member Instance = new UdpSocketEventSource ()


    [<Event(1)>]
    member _.Created () = this.WriteEvent 1

    [<Event(2)>]
    member _.Bind () = this.WriteEvent 2

    [<Event(3)>]
    member _.ReceiveUdpPacketEnter () = this.WriteEvent 3

    [<Event(4)>]
    member _.ReceiveUdpPacketExit () = this.WriteEvent 4

    [<Event(5)>]
    member _.SendWaitingEnter () = this.WriteEvent 5

    [<Event(6)>]
    member _.SendWaitingExit () = this.WriteEvent 6

    [<Event(7)>]
    member _.SendDequeue () = this.WriteEvent 7

    [<Event(8)>]
    member _.SendSocketSendEnter () = this.WriteEvent 8

    [<Event(9)>]
    member _.SendSocketSendExit () = this.WriteEvent 9

    [<Event(10)>]
    member _.SendError () = this.WriteEvent 10

    [<Event(11)>]
    member _.Start () = this.WriteEvent 11

    [<Event(12)>]
    member _.ReceiveEnter () = this.WriteEvent 12

    [<Event(13)>]
    member _.ReceiveExit () = this.WriteEvent 13

    [<Event(14)>]
    member _.ReceiveError () = this.WriteEvent 14

    [<Event(15)>]
    member _.SendEnqueue () = this.WriteEvent 15

    [<Event(16)>]
    member _.DisposeSocket () = this.WriteEvent 16


[<EventSource(Name = "Cache")>]
type CacheEventSource private () as this =

    inherit EventSource ()

    static member Instance = new CacheEventSource ()

    [<Event(1)>]
    member _.TryGet () = this.WriteEvent 1

    [<Event(2)>]
    member _.TryGetNone () = this.WriteEvent 2

    [<Event(3)>]
    member _.TryGetSome () = this.WriteEvent 3

    [<Event(4)>]
    member _.TryGetTouch () = this.WriteEvent 4

    [<Event(5)>]
    member _.TryGetTouchNone () = this.WriteEvent 5

    [<Event(6)>]
    member _.TryGetTouchSome () = this.WriteEvent 6

    [<Event(7)>]
    member _.PutEnter () = this.WriteEvent 7

    [<Event(8)>]
    member _.PutExit () = this.WriteEvent 8

    [<Event(9)>]
    member _.PutWithTtlEnter () = this.WriteEvent 9

    [<Event(10)>]
    member _.PutWithTtlExit () = this.WriteEvent 10

    [<Event(11)>]
    member _.DeleteEnter () = this.WriteEvent 11

    [<Event(12)>]
    member _.DeleteExit () = this.WriteEvent 12

    [<Event(13)>]
    member _.OnTimerEnter () = this.WriteEvent 13

    [<Event(14)>]
    member _.OnTimerExit () = this.WriteEvent 14

    [<Event(15)>]
    member _.OutdatedEntriesFetched () = this.WriteEvent 15

    [<Event(16)>]
    member _.OutdatedEntriesDeleted (count: int) = this.WriteEvent (16, count)

    [<Event(17)>]
    member _.Created () = this.WriteEvent 17


[<EventSource(Name = "SocketCollection")>]
type SocketCollectionEventSource private () as this =

    inherit EventSource ()

    static member Instance = new SocketCollectionEventSource ()

    [<Event(1)>]
    member _.Created () = this.WriteEvent 1

    [<Event(2)>]
    member _.GetUniqueOutputSocketEnter () = this.WriteEvent 2

    [<Event(3)>]
    member _.GetUniqueOutputSocketExit () = this.WriteEvent 3


[<EventSource(Name = "Conntrack")>]
type ConnectionTrackingEventSource private () as this =

    inherit EventSource ()

    static member Instance = new ConnectionTrackingEventSource ()

    [<Event(1)>]
    member _.Created () = this.WriteEvent 1

    [<Event(2)>]
    member _.TrackConnectionEnter () = this.WriteEvent 2

    [<Event(3)>]
    member _.TrackConnectionExit () = this.WriteEvent 3

    [<Event(4)>]
    member _.TryGetClientEnter () = this.WriteEvent 4

    [<Event(5)>]
    member _.TryGetClientNone () = this.WriteEvent 5

    [<Event(6)>]
    member _.TryGetClientSome () = this.WriteEvent 6

    [<Event(7)>]
    member _.TryGetClientExit () = this.WriteEvent 7

    [<Event(8)>]
    member _.AddClientSocket () = this.WriteEvent 8

    [<Event(9)>]
    member _.GetRandomClientSocket () = this.WriteEvent 9

#endif
