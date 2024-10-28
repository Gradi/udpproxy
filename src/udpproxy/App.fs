namespace UdpProxy

open Serilog
open Serilog.Events
open System.Threading
open UdpProxy.Exceptions
open UdpProxy.Pipelines
open UdpProxy.Services


type IApp =

    abstract Run: CancellationToken -> Async<unit>


type App (pipelineStages: Lazy<IPipeline seq>, logger: ILogger, socketCollection: Lazy<ISocketCollection>) =

#if EventSourceProviders
    do
        AppEventSource.Instance.Created ()
#endif

    let logger = logger.ForContext<App> ()

    let pipelineStages = lazy ( pipelineStages.Value |> Array.ofSeq )

    let forwardEntrypoint =
        lazy (
            let forwardFolder (next: PipelineStage) (stage: IPipeline) =
                (fun udpPacket ->
                    async {
                        try
                            if logger.IsEnabled LogEventLevel.Debug then
                                logger.Debug ("App: FORWARD {$Udp} to \"{StageName}\"", udpPacket, stage.Name)

                            do! stage.Forward udpPacket next
                        with
                        | DebugMessageError errorMessage ->
                            if logger.IsEnabled LogEventLevel.Debug then
                                logger.Debug ("App: Forward stage \"{StageName}\" threw an error \"{ErrorMessage}\". Udp packet was \"{$Udp}\".",
                                              stage.Name, errorMessage, udpPacket)
                        | exc ->
                            logger.Error (exc, "App: Forward stage \"{StageName}({$StageType})\" threw an error. Udp packet was \"{$Udp}\".",
                                          stage.Name, stage.GetType(), udpPacket)
                    })

            let headFunc =
                pipelineStages.Value
                |> Array.rev
                |> Array.fold forwardFolder (fun _ ->
#if EventSourceProviders
                    AppEventSource.Instance.ForwardEntrypointExit ()
#endif
                    async { return () })

#if EventSourceProviders
            (fun packet ->
                AppEventSource.Instance.ForwardEntrypointEnter ()
                headFunc packet)
#else
            headFunc
#endif
        )

    let reverseEntrypoint =
        lazy (
            let reverseFolder (next: PipelineStage) (stage: IPipeline) =
                (fun udpPacket ->
                    async {
                        try
                            if logger.IsEnabled LogEventLevel.Debug then
                                logger.Debug ("App: REVERSE {$Udp} to \"{StageName}\"", udpPacket, stage.Name)

                            do! stage.Reverse udpPacket next
                        with
                        | DebugMessageError errorMessage ->
                            if logger.IsEnabled LogEventLevel.Debug then
                                logger.Debug ("App: Reverse stage \"{StageName}\" threw an error \"{ErrorMessage}\". Udp packet was \"{$Udp}\".",
                                                stage.Name, errorMessage, udpPacket)
                        | exc ->
                            logger.Error (exc, "App: Reverse stage \"{StageName}({$StageType})\" threw an error. Udp packet was \"{$Udp}\"",
                                          stage.Name, stage.GetType (), udpPacket)
                    })

            let headFunc =
                pipelineStages.Value
                |> Array.fold reverseFolder (fun _ ->
#if EventSourceProviders
                    AppEventSource.Instance.ReverseEntrypointExit ()
#endif
                    async { return () })

#if EventSourceProviders
            (fun packet ->
                AppEventSource.Instance.ReverseEntrypointEnter ()
                headFunc packet)
#else
            headFunc
#endif
        )


    interface IApp with

        member _.Run cancelToken =
            async {
                logger.Information "App: Starting up..."

                pipelineStages.Value |> ignore
                forwardEntrypoint.Value |> ignore
                reverseEntrypoint.Value |> ignore

                logger.Information ("App: Got {StageCount} pipelines.", Array.length pipelineStages.Value)
                for index, stage in pipelineStages.Value |> Array.indexed do
                    logger.Information ("App: Stage {PipelineIndex} - {PipelineName}", index, stage.Name)

                logger.Information "App: Starting listening sockets"
                socketCollection.Value.StartAllClientSockets()

                logger.Information "App: Proxy now is running..."
#if EventSourceProviders
                AppEventSource.Instance.Run ()
#endif
                let! _ = Async.AwaitWaitHandle cancelToken.WaitHandle
                logger.Information "App: Shutting down..."
#if EventSourceProviders
                AppEventSource.Instance.Stop ()
#endif
            }

    interface IPacketHandler with

        member this.HandleClientPacket udpPacket = forwardEntrypoint.Value udpPacket

        member this.HandleUpstreamPacket udpPacket = reverseEntrypoint.Value udpPacket
