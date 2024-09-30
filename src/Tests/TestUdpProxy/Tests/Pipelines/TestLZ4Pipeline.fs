namespace TestUdpProxy.Tests.Pipelines

open FsUnit
open K4os.Compression.LZ4
open NUnit.Framework
open System
open System.Net
open TestUdpProxy
open TestUdpProxy.PipelineTestUtils
open UdpProxy
open UdpProxy.Pipelines


[<TestFixture>]
type TestLZ4Pipeline () =

    let getPipe (level: LZ4Level) =
        LZ4Pipeline (LanguagePrimitives.EnumToValue<LZ4Level, int> level, LoggerMock.logger) :> IPipeline

    [<Test>]
    member _.``Forward doesnt throw errors`` ([<Values>] level: LZ4Level) =
        (fun () -> runForwardShouldReturn (getPipe level) (rndUdp 100) |> ignore)
        |> should not' (throw typeof<Exception>)

    [<Test>]
    member _.``Forward actually compresses`` ([<Values>] level: LZ4Level) =
        let zerosSize = 1000
        let output =
            runForwardShouldReturn (getPipe level) { UdpPacket.Payload = Array.zeroCreate zerosSize
                                                     SourceEndpoint = IPEndPoint (IPAddress.Loopback, 80)
                                                     LocalSocket = Unchecked.defaultof<UdpSocket> }

        output.Length
        |> should be (lessThanOrEqualTo (zerosSize / 2))

    [<Test>]
    member _.``Reverse fails on invalid packet`` ([<Values>] level: LZ4Level) =
        (fun () -> runReverseShouldReturn (getPipe level) (rndUdp 1024) |> ignore)
        |> should throw typeof<Exceptions.DebugMessageError>

    [<Test>]
    member _.``Forward then Reverse returns same packet`` ([<Values>] level: LZ4Level) =
        let expectedPacket = rndUdp 1024
        let pipe = getPipe level
        let actualOutput =
            runForwardShouldReturn pipe expectedPacket
            |> runReverseShouldReturn pipe

        Object.ReferenceEquals (expectedPacket.Payload, actualOutput.Payload)
        |> should be False

        Seq.ofArray actualOutput.Payload
        |> should equalSeq (Seq.ofArray expectedPacket.Payload)
