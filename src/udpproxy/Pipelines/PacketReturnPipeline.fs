namespace UdpProxy.Pipelines

open Serilog
open Serilog.Events
open UdpProxy.Services
open UdpProxy


type PacketReturnPipeline (conntrack: Lazy<IConnectionTracking>, logger: ILogger) =

#if EventSourceProviders
    do
        PipelineEventSource.Instance.PacketReturnCreated ()
#endif

    interface IPipeline with

        member this.Name = "PacketReturn"

        member this.Forward udpPacket next =
#if EventSourceProviders
            PipelineEventSource.Instance.ForwardPacketReturn ()
#endif
            next udpPacket

        member this.Reverse udpPacket _ =
            async {
#if EventSourceProviders
                PipelineEventSource.Instance.ReversePacketReturn ()
#endif
                match conntrack.Value.TryGetClient udpPacket.LocalSocket with
                | None ->
                    if logger.IsEnabled LogEventLevel.Debug then
                        logger.Debug ("PacketReturn: Got orphaned packet {$Udp}. Dropping.", udpPacket)

                | Some (client, socket) ->
                    if logger.IsEnabled LogEventLevel.Debug then
                        logger.Debug ("PacketReturn: Sending packet {$Udp} back to {$Client} through {$LocalEndpoint}",
                                      udpPacket, client, socket.LocalEndpoint)

                    do! socket.Send udpPacket.Payload client

                    // Not calling next (intentionally)
            }
