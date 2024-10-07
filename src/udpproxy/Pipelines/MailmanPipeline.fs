namespace UdpProxy.Pipelines

open Serilog
open Serilog.Events
open UdpProxy.Services


type MailmanPipeline (outputEndpoints: Endpoint list, dns: Lazy<IDns>, sockets: Lazy<ISocketCollection>,
                      conntrack: Lazy<IConnectionTracking>, logger: ILogger) =

    let outputAddresses = lazy (
            outputEndpoints
            |> List.map (fun endpoint -> (endpoint, dns.Value.Resolve endpoint))
            |> List.map (fun (endpoint, addrs) -> List.map (fun addr -> (endpoint, addr)) addrs)
            |> List.collect id
            |> Array.ofList
        )

    interface IPipeline with

        member this.Name = sprintf "Mailman (%O)" outputEndpoints

        member this.Forward udpPacket _ =
            async {
                match outputAddresses.Value with
                | [|  |] ->
                    if logger.IsEnabled LogEventLevel.Debug then
                        logger.Debug "Mailman: Not sending out {$Udp}. Output endpoints are not specified."
                | _ ->

                    let endpoint, address = Array.randomChoice outputAddresses.Value

                    if logger.IsEnabled LogEventLevel.Debug then
                        logger.Debug ("Mailman: Sending out {$Udp} to {$Address}(resolved: {$ResolvedAddress}).", udpPacket,
                                      endpoint, address)

                    let key = udpPacket.SourceEndpoint
                    let outputSocket = sockets.Value.GetUniqueOutputSocket key address.AddressFamily
                    conntrack.Value.TrackConnection (udpPacket.SourceEndpoint, udpPacket.LocalSocket) outputSocket
                    do! outputSocket.Send udpPacket.Payload address

                    // Not calling next (intentionally)
            }

        member this.Reverse udpPacket next = next udpPacket

