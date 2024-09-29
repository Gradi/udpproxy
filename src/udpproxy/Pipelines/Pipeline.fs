namespace UdpProxy.Pipelines

open UdpProxy


type PipelineStage = UdpPacket -> Async<unit>


type IPipeline =

        abstract member Name: string with get

        abstract member Forward: udpPacket: UdpPacket -> next: PipelineStage -> Async<Unit>

        abstract member Reverse: udpPacket: UdpPacket -> next: PipelineStage -> Async<Unit>


type InvertedPipeline (pipeline: IPipeline) =

    interface IPipeline with

        member this.Name = sprintf "%s (inverted)" pipeline.Name

        member this.Forward udpPacket next = pipeline.Reverse udpPacket next

        member this.Reverse udpPacket next = pipeline.Forward udpPacket next
