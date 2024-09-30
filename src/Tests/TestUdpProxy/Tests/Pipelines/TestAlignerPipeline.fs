namespace TestUdpProxy.Tests.Pipelines

open FsUnit
open NUnit.Framework
open System
open TestUdpProxy
open TestUdpProxy.PipelineTestUtils
open UdpProxy
open UdpProxy.Pipelines


[<TestFixture>]
type TestAlignerPipeline () =

    let getPipe alignBy = AlignerPipeline (alignBy, CryptoRndMock (), LoggerMock.logger) :> IPipeline

    [<Test>]
    member _.``Ctors throws on invalid input`` () =
        (fun () -> getPipe 0 |> ignore)
        |> should throw typeof<Exception>

        (fun () -> getPipe -1 |> ignore)
        |> should throw typeof<Exception>

    [<Test>]
    member _.``Forward doesnt throw`` ([<Range(1, 1010, 10)>] alignBy: int) =
        (fun () -> runForwardShouldReturn (getPipe alignBy) (rndUdp 1024) |> ignore)
        |> should not' (throw typeof<Exception>)

    [<Test>]
    member _.``Forward returns packet with aligned size`` ([<Range(1, 1010, 10)>] alignBy: int) =
        let packetLength =
            (runForwardShouldReturn (getPipe alignBy) (rndUdp 132))
             .Length

        (packetLength % alignBy) |> should equal 0
        packetLength |> should be (greaterThan 0)

    [<Test;Repeat(10)>]
    member _.``Reverse throws on invalid packet`` () =
        (fun () -> runReverseShouldReturn (getPipe 1000) (rndUdp 122) |> ignore)
        |> should throw typeof<Exceptions.DebugMessageError>

    [<Test>]
    member _.``Forward then reverse returns same packet`` ([<Range(1, 1010, 10)>] alignBy: int) =
        let expectedPacket = rndUdp 132
        let pipe = getPipe alignBy
        let actualPacket =
            runForwardShouldReturn pipe expectedPacket
            |> runReverseShouldReturn pipe

        Object.ReferenceEquals (expectedPacket.Payload, actualPacket.Payload)
        |> should be False

        (Seq.ofArray actualPacket.Payload)
        |> should equalSeq (Seq.ofArray expectedPacket.Payload)
