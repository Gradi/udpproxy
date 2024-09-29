module UdpProxy.Configuration

open Microsoft.FSharp.Core
open Newtonsoft.Json
open Serilog.Events
open System
open UdpProxy.PipelinesBuilders
open UdpProxy.Services


type InputPorts = Endpoint list


type LogConfig =
    { [<JsonProperty("level")>]      Level: LogEventLevel
      [<JsonProperty("logFile")>]    LogFile: string option
      [<JsonProperty("consoleLog")>] ConsoleLog: bool }

        static member private DefaultLevel      () = LogEventLevel.Information
        static member private DefaultLogFile    () = None : string option
        static member private DefaultConsoleLog () = true


type CacheConfig =
    { [<JsonProperty("ttl")>]   Ttl: TimeSpan
      [<JsonProperty("timer")>] TimerInterval: TimeSpan }

        static member private DefaultTtl           () = TimeSpan.FromMinutes 10.0
        static member private DefaultTimerInterval () = TimeSpan.FromMinutes 1.0


type DnsConfig =
    { [<JsonProperty("timeout")>] Timeout: TimeSpan }

        static member private DefaultTimeout () = TimeSpan.FromSeconds 10.0


type ConntrackConfig =
    { [<JsonProperty("ttl")>] Ttl: TimeSpan }

        static member private DefaultTtl () = TimeSpan.FromMinutes 10.0


type PipelinesBuilders = IPipelineBuilder list


[<NoComparison;NoEquality>]
type ProxyConfig =
    { [<JsonProperty("log")>]         Log: LogConfig
      [<JsonProperty("cache")>]       Cache: CacheConfig
      [<JsonProperty("conntrack")>]   Conntrack: ConntrackConfig
      [<JsonProperty("dns")>]         Dns: DnsConfig
      [<JsonProperty("inputPorts")>]  InputPorts: InputPorts
      [<JsonProperty("pipeline")>]    PipelinesBuilders: PipelinesBuilders }


        static member private DefaultLog () = { LogConfig.Level = LogEventLevel.Verbose
                                                LogFile = None
                                                ConsoleLog = true }

        static member private DefaultCache () = { CacheConfig.Ttl = TimeSpan.FromMinutes 10.0
                                                  TimerInterval = TimeSpan.FromMinutes 1.0 }

        static member private DefaultConntrack () = { ConntrackConfig.Ttl = TimeSpan.FromMinutes 10.0 }

        static member private DefaultDns () = { DnsConfig.Timeout = TimeSpan.FromSeconds 5.0 }



let parseFromJson (json: string) : ProxyConfig =
    match JsonConvert.DeserializeObject (json, typeof<ProxyConfig>) with
    | null -> failwithf "JsonConvert returned null."
    | :? ProxyConfig as config -> config
    | obj -> failwithf "JsonConvert returned different object %O" (obj.GetType ())


let writeToJsonString (config: ProxyConfig) =
    JsonConvert.SerializeObject config


let addDefaults (config: ProxyConfig) =
    let inputPorts =
        if List.isEmpty config.InputPorts then
            [ PortOnly 13820 ]
        else
            config.InputPorts

    { config with InputPorts = inputPorts }
