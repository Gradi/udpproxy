module UdpProxy.Program

open Argu
open Autofac
open Configuration
open Serilog
open System
open System.Globalization
open System.IO
open System.Runtime.InteropServices
open System.Threading
open UdpProxy.Services


let configureCache (container: ContainerBuilder) (config: CacheConfig) =
    container.Register<CacheFactory>(fun (c: IComponentContext) -> new CacheFactory (config.Ttl, config.TimerInterval, c.Resolve<ILogger> ()))
        .As<ICacheFactory>()
        .SingleInstance()
        |> ignore


let configureCryptoRnd (container: ContainerBuilder) =
    container.RegisterType<CryptoRnd>().SingleInstance().As<ICryptoRnd>() |> ignore


let configureDns (container: ContainerBuilder) (config: DnsConfig) =
    container.Register<Dns>(fun (c: IComponentContext) -> Dns (config.Timeout, c.Resolve<ILogger> ()))
        .SingleInstance()
        .As<IDns>()
        |> ignore


let configureSocketCollection (container: ContainerBuilder) (config: ConntrackConfig) (inputPorts: InputPorts) =
    container.Register<SocketCollection>(fun (c: IComponentContext) ->
        let dns = c.Resolve<IDns> ()
        let resolvedEnps =
            inputPorts
            |> List.map dns.Resolve
            |> List.collect id
        new SocketCollection (resolvedEnps, config.Ttl, c.Resolve<ILogger> (), c.Resolve<ICacheFactory>(), c.Resolve<Lazy<IPacketHandler>> ()))
        .SingleInstance()
        .As<ISocketCollection>()
        |> ignore


let configureConnectionTracking (container: ContainerBuilder) (config: ConntrackConfig) =
    container.Register<ConnectionTracking>(fun (c: IComponentContext) ->
        ConnectionTracking (config.Ttl, c.Resolve<ICacheFactory> ()))
        .SingleInstance()
        .As<IConnectionTracking>()
        |> ignore


let configurePipeline (container: ContainerBuilder) (pipelinesBuilders: PipelinesBuilders) =
    for pipelineBuilder in pipelinesBuilders do
        pipelineBuilder.Register container


let configureLogger (container: ContainerBuilder) (config: LogConfig) =
    container.Register<ILogger>(fun (c: IComponentContext) ->
        let serilogConf = LoggerConfiguration ()
        serilogConf.MinimumLevel.Is config.Level |> ignore

        if config.ConsoleLog then
            serilogConf.WriteTo.Console () |> ignore

        match config.LogFile with
        | Some file when not (String.IsNullOrWhiteSpace file) ->
            serilogConf.WriteTo.File (file, buffered = true) |> ignore
        | Some _
        | None -> ()

        let logger = serilogConf.CreateLogger ()
        Serilog.Log.Logger <- logger
        logger :> ILogger)
        .SingleInstance()
        .AsSelf()
        |> ignore


let runProxy (args: ParseResults<RunArgs>) =
    let configuration = parseFromJson (File.ReadAllText (args.GetResult Config)) |> addDefaults
    let builder = ContainerBuilder ()
    configureCache builder configuration.Cache
    configureCryptoRnd builder
    configureDns builder configuration.Dns
    configureSocketCollection builder configuration.Conntrack configuration.InputPorts
    configureConnectionTracking builder configuration.Conntrack
    configurePipeline builder configuration.PipelinesBuilders
    configureLogger builder configuration.Log
    builder.RegisterType<App>().SingleInstance().AsImplementedInterfaces() |> ignore

    async {
        use container = builder.Build ()
        use scope = container.BeginLifetimeScope ()
        use cancelToken = new CancellationTokenSource ()
        use _ = PosixSignalRegistration.Create (PosixSignal.SIGINT, (fun ctx ->
            ctx.Cancel <- true
            scope.Resolve<ILogger>().Information "Got Ctrl+C (SIGINT)."
            cancelToken.Cancel ()))

        do! scope.Resolve<IApp>().Run cancelToken.Token
    }



[<EntryPoint>]
let main argv =
    CultureInfo.CurrentCulture <- CultureInfo.InvariantCulture
    CultureInfo.CurrentUICulture <- CultureInfo.InvariantCulture
    CultureInfo.DefaultThreadCurrentCulture <- CultureInfo.InvariantCulture
    CultureInfo.DefaultThreadCurrentUICulture <- CultureInfo.InvariantCulture
    NewtonsoftJson.configure ()

    try
        let argumentParser = ArgumentParser.Create<CliArgs>()
        let arguments = argumentParser.ParseCommandLine argv

        let job =
            match arguments.GetAllResults () with
            | [] ->
                printfn "%s" (argumentParser.PrintUsage ())
                async { return () }

            | [ CliArgs.ReadPrint args ] ->
                let config = parseFromJson (File.ReadAllText (args.GetResult Config)) |> addDefaults
                let config = { config with PipelinesBuilders = [] } |> writeToJsonString
                printfn "%s" config
                async { return () }

            | [ CliArgs.Run args ] ->
                runProxy args

            | xs ->
                printfn "Too many (%d) arguments." (List.length xs)
                printfn "%s" (argumentParser.PrintUsage ())
                async { return () }

        Async.RunSynchronously job
        0
    with
    | :? ArguException as exc ->
        eprintfn "%s" exc.Message
        1
    | exc ->
        eprintfn "%O" exc
        1
