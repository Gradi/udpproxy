namespace TestUdpProxy.Tests.Pipelines

open System
open FsUnit
open NUnit.Framework
open NUnit.Framework.Constraints
open TestUdpProxy.PipelineTestUtils
open UdpProxy.Pipelines


[<TestFixture>]
type TestAesPipeline () =

    let getPipe () =
        let key: byte array = Array.zeroCreate 16
        let hmacKey: byte array = Array.zeroCreate 16
        TestContext.CurrentContext.Random.NextBytes key
        TestContext.CurrentContext.Random.NextBytes hmacKey
        AesPipeline (key, hmacKey) :> IPipeline

    [<Test>]
    member _.``Forward doesnt throw errors`` ([<Range(0, 50, 1)>] size: int) =
        (fun () -> runForwardShouldReturn (getPipe ()) (rndUdp size) |> ignore)
        |> should not' (be (ThrowsExceptionConstraint ()))

    [<Test>]
    member _.``Forward changes payload`` ([<Range(0, 50, 1)>] size: int) =
        let input = rndUdp size
        let output = runForwardShouldReturn (getPipe ()) input

        Object.ReferenceEquals (input.Payload, output.Payload)
        |> should be False

        output.Length |> should be (greaterThan size)

        (Seq.ofArray output.Payload)
        |> should not' (equalSeq (Seq.ofArray input.Payload))

    [<Test>]
    member _.``Reverse throws on invalid packet`` ([<Range(0, 50, 1)>] size: int) =
        (fun () -> runReverseShouldReturn (getPipe ()) (rndUdp size) |> ignore)
        |> should be (ThrowsExceptionConstraint ())

    [<Test>]
    member _.``Forward then Reverse returns same payload`` ([<Range(0, 50, 1)>] size: int) =
        let input = rndUdp size
        let pipe = getPipe ()
        let output =
            runForwardShouldReturn pipe input
            |> runReverseShouldReturn pipe

        Object.ReferenceEquals (input.Payload, output.Payload)
        |> should be False

        (Seq.ofArray output.Payload)
        |> should equalSeq (Seq.ofArray input.Payload)

    [<Test>]
    member _.``Reverse fails on byte change`` ([<Range(0, 50, 1)>] size: int) =
        let pipe = getPipe ()
        let encrypted = runForwardShouldReturn pipe (rndUdp size)

        for i in 0 .. (encrypted.Length - 1) do
            let encrypted = { encrypted with Payload = Array.copy encrypted.Payload }
            encrypted.Payload[i] <- encrypted.Payload[i] + 1uy

            (fun () -> runReverseShouldReturn pipe encrypted |> ignore)
            |> should be (ThrowsExceptionConstraint ())
