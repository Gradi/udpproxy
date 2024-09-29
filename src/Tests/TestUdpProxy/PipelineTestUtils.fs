module TestUdpProxy.PipelineTestUtils

open System.Net
open System.Net.Sockets
open NUnit.Framework
open UdpProxy
open UdpProxy.Pipelines


let rndPayload size =
    let bytes: byte array = Array.zeroCreate size
    TestContext.CurrentContext.Random.NextBytes bytes
    bytes


let rndUdp size =
    let buffer = rndPayload size
    { UdpPacket.Payload = buffer; SourceEndpoint  = IPEndPoint(IPAddress.Loopback, 0); LocalSocket = Unchecked.defaultof<UdpSocket> }


let runForward (pipeline: IPipeline) (packet: UdpPacket) =
    let mutable finalPacket = None
    let next packet =
        finalPacket <- Some packet
        async { return () }

    pipeline.Forward packet next
    |> Async.RunSynchronously

    finalPacket


let runForwardShouldReturn pipeline packet =
    match runForward pipeline packet with
    | Some packet -> packet
    | None -> failwithf "Pipeline named %O should have returned some packet, but it did not." (pipeline.GetType())


let runReverse (pipeline: IPipeline) (packet: UdpPacket) =
    let mutable finalPacket = None
    let next packet =
        finalPacket <- Some packet
        async { return () }

    pipeline.Reverse packet next
    |> Async.RunSynchronously

    finalPacket


let runReverseShouldReturn pipeline packet =
    match runReverse pipeline packet with
    | Some packet -> packet
    | None -> failwithf "Pipeline named %O should have returned some packet, but it did non." (pipeline.GetType())
