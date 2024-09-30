namespace TestUdpProxy.Tests.Pipelines

open FsUnit
open NUnit.Framework
open Serilog
open System
open TestUdpProxy
open TestUdpProxy.PipelineTestUtils
open UdpProxy.Pipelines


[<TestFixture>]
type TestRndPadPipeline () =

    let getPipe min max : IPipeline = RndPadPipeline (min, max, LoggerMock.logger, CryptoRndMock ())

    [<Test>]
    member _.BadCtorArgumentsCauseException () =
        (fun () -> RndPadPipeline (-1, 0, Log.Logger, CryptoRndMock ()) |> ignore)
        |> should throw typeof<Exception>

        (fun () -> RndPadPipeline (0, -1, Log.Logger, CryptoRndMock ()) |> ignore)
        |> should throw typeof<Exception>

        (fun () -> RndPadPipeline (2, 1, Log.Logger, CryptoRndMock ()) |> ignore)
        |> should throw typeof<Exception>

    [<Test>]
    member _.GoodCtorArgumentsCauseNoException () =
        (fun () -> RndPadPipeline (0, 0, Log.Logger, CryptoRndMock ()) |> ignore)
        |> should not' (throw typeof<Exception>)

        (fun () -> RndPadPipeline (1, 2, Log.Logger, CryptoRndMock ()) |> ignore)
        |> should not' (throw typeof<Exception>)

    [<Test>]
    member _.ForwardNotThrows ([<Range(0, 1024)>] packetSize: int) =
        (fun () -> runForward (getPipe 0 1024) (rndUdp packetSize) |> ignore)
        |> should not' (throw typeof<Exception>)

    [<Test>]
    member _.AfterForwardPacketSizeIsIncreased ([<Range(0, 1024)>] packetSize: int) =
        runForwardShouldReturn (getPipe 10 1024) (rndUdp packetSize)
        |> _.Payload.Length
        |> should be (greaterThanOrEqualTo (packetSize + 10))

    [<Test>]
    member _.ForwardReverseReturnsSamePacket ([<Range(0, 1024)>] packetSize: int) =
        let input = rndUdp packetSize
        let pipe = getPipe 10 1024
        let forwarded = runForwardShouldReturn pipe input
        let reversed = runReverseShouldReturn pipe forwarded

        forwarded.Payload
        |> Seq.ofArray
        |> should not' (equalSeq (input.Payload |> Seq.ofArray))

        reversed.Payload
        |> Seq.ofArray
        |> should equalSeq (input.Payload |> Seq.ofArray)
