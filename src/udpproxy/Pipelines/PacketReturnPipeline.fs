namespace UdpProxy.Pipelines

open Serilog
open Serilog.Events
open UdpProxy.Services


type PacketReturnPipeline (conntrack: Lazy<IConnectionTracking>, logger: ILogger) =

    interface IPipeline with

        member this.Name = "PacketReturn"

        member this.Forward udpPacket next = next udpPacket

        member this.Reverse udpPacket _ =
            async {
                match conntrack.Value.TryGetClient udpPacket.LocalSocket with
                | None ->
                    if logger.IsEnabled LogEventLevel.Debug then
                        logger.Debug ("PacketReturn: Got orphaned packet ({$Udp}).", udpPacket)

                | Some (client, socket) ->
                    if logger.IsEnabled LogEventLevel.Debug then
                        logger.Debug ("PacketReturn: Sending packet ({$Udp}) back to {$Client} through {$LocalEndpoint}",
                                      udpPacket, client, socket.LocalEndpoint)

                    do! socket.Send udpPacket.Payload client

                    // Not calling next (intentionally)
            }
