namespace UdpProxy.Services

open System
open System.Net
open System.Threading
open Serilog
open Serilog.Events


[<NoComparison>]
type Endpoint =
    | PortOnly of int
    | IpPort of IPEndPoint
    | Host of string * int

        override this.ToString () =
            match this with
            | PortOnly port -> port.ToString ()
            | IpPort ip -> ip.ToString ()
            | Host (host, port) -> sprintf "%s:%d" host port


type IDns =

    abstract Resolve : host : string -> IPAddress list

    abstract Resolve : Endpoint -> IPEndPoint list


type Dns (timeout: TimeSpan, logger: ILogger) =

    interface  IDns with

        member _.Resolve (host: string) =
            async {
                try
                    logger.Debug ("Dns: Resolving {Hostname}", host)
                    use cancelToken = new CancellationTokenSource (timeout)
                    let! addresses = System.Net.Dns.GetHostAddressesAsync (host, cancelToken.Token) |> Async.AwaitTask

                    if logger.IsEnabled LogEventLevel.Debug then
                        let addrs = addresses |> Array.map _.ToString() |> String.concat ", "
                        logger.Debug ("Dns: Resolved {Hostname} into [{Addresses}]", host, addrs)

                    return addresses
                with
                | :? OperationCanceledException ->
                    return failwithf "Could not resolve \"%s\" into IPs: Timeout after \"%O\"" host timeout
                | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) ->
                    return failwithf "Could not resolve \"%s\" into IPs: Timeout after \"%O\"" host timeout
                | exc ->
                    return raise (Exception(sprintf "Could not resolve \"%s\" into IPs." host, exc))
            }
            |> Async.RunSynchronously
            |> List.ofArray

        member this.Resolve (endpoint: Endpoint) =
            match endpoint with
            | PortOnly port -> [ IPEndPoint (IPAddress.Any, port); IPEndPoint (IPAddress.IPv6Any, port) ]
            | IpPort ipport -> [ ipport ]
            | Host (host, port) ->
                (this :> IDns).Resolve host
                |> List.map (fun ip -> IPEndPoint (ip, port))
