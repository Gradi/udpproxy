module UEcho.Program

open System.Globalization
open System.Text
open System.Text.RegularExpressions
open Argu
open System
open System.Net
open System.Net.Sockets
open System.Runtime.InteropServices
open System.Threading


let log fmt =
    Printf.ksprintf (fun msg ->
        printfn "%s: %s" (DateTime.Now.ToString("HH:mm:ss.fff")) msg) fmt


let arrayFillUpto (bytes: byte array) (maxBytes: int) =
    let result: byte array = Array.zeroCreate maxBytes

    let wholeParts = maxBytes / (Array.length bytes)
    let remainder = maxBytes % (Array.length bytes)

    for i in 0 .. (wholeParts - 1) do
        Array.Copy(bytes, 0, result, (Array.length bytes) * i, Array.length bytes)

    Array.Copy(bytes, 0, result, wholeParts * (Array.length bytes), remainder)
    result


let runServer (serverArgs: ParseResults<ServerArgs>) (cancelToken: CancellationToken) =
    async {
        let endpoint =
            match serverArgs.TryGetResult ServerArgs.Ip with
            | Some ip -> IPEndPoint (IPAddress.Parse ip, serverArgs.GetResult ServerArgs.Port)
            | None -> IPEndPoint (IPAddress.Loopback, serverArgs.GetResult ServerArgs.Port)

        use udpClient = new UdpClient (endpoint)
        log "Listening on %O" endpoint

        try
            while not cancelToken.IsCancellationRequested do
                let! payload = udpClient.ReceiveAsync(cancelToken).AsTask() |> Async.AwaitTask
                let msgPrefix = sprintf "(%d bytes from %O) " (Array.length payload.Buffer) payload.RemoteEndPoint

                match EchoRequest.tryParse payload.Buffer with
                | Error error -> log "%sCan't parse: %s" msgPrefix error
                | Ok echoRequest ->
                    match EchoRequest.verify echoRequest with
                    | Error errors -> log "%sVerify failed: %A" msgPrefix errors
                    | Ok () ->
                        log "%sOK!" msgPrefix
                        let! _ = udpClient.SendAsync(payload.Buffer, payload.RemoteEndPoint).AsTask() |> Async.AwaitTask
                        ()

        with
        | :? OperationCanceledException -> ()
    }


let (|TextPattern|_|) (str: string) =
    let result = Regex.Match (str, "^text \"(.+)\" (\\d+)$")
    match result.Success with
    | false -> None
    | true ->
        let patternTxt = result.Groups[1].Value
        let patternBytes = Encoding.UTF8.GetBytes patternTxt
        let sizeBytes = Int32.Parse result.Groups[2].Value
        let patternBytes = arrayFillUpto patternBytes sizeBytes
        Some (EchoRequest.make patternBytes)

let (|HexPattern|_|) (str: string) =
    let result = Regex.Match (str, "^hex ((0x[0-9a-f]{2} ?)+) (\\d+)$")
    match result.Success with
    | false -> None
    | true ->
        let hexBytes =
            result.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun num -> Byte.Parse (num.Substring 2, NumberStyles.AllowHexSpecifier))
        let sizeBytes = Int32.Parse result.Groups[3].Value
        let hexBytes = arrayFillUpto hexBytes sizeBytes
        Some (EchoRequest.make hexBytes)


let askAndMakeRequest () =
    try
        printf "Type in pattern :>"
        let input = Console.ReadLine ()
        if String.IsNullOrWhiteSpace input then
            None
        else
            match input with
            | TextPattern request -> Some request
            | HexPattern request -> Some request
            | _ ->
                log "Bad input (%s)." input
                None
    with
    | exc ->
        log "%s" exc.Message
        None


let runClient (clientArgs: ParseResults<ClientArgs>) (cancelToken: CancellationToken) =
    async {
        let host = (clientArgs.GetResult ClientArgs.Host)
        let host =
            match Dns.GetHostAddresses host with
            | [||] -> failwithf "Can't resolve %s." host
            | ips -> IPEndPoint (Array.head ips, clientArgs.GetResult ClientArgs.Port)

        use udpClient = new UdpClient (host.AddressFamily)
        udpClient.Connect host
        log "Local endpoint %O" udpClient.Client.LocalEndPoint
        log "Will send to %O" host

        try
            while not cancelToken.IsCancellationRequested do
                match askAndMakeRequest () with
                | None -> ()
                | Some request ->
                    let bytes = EchoRequest.serialize request
                    let! _ = udpClient.SendAsync(bytes, cancelToken).AsTask() |> Async.AwaitTask

                    try
                        use cancelToken = new CancellationTokenSource (TimeSpan.FromSeconds 5.0)
                        let! response = udpClient.ReceiveAsync(cancelToken.Token).AsTask() |> Async.AwaitTask
                        match bytes = response.Buffer with
                        | true ->
                            log "OK!"
                        | false ->
                            log "Failure: Sent %d bytes, received %d bytes" (Array.length bytes) (Array.length response.Buffer)
                    with
                    | :? OperationCanceledException ->
                        log "Failure: Timeout on response."

        with
        | :? OperationCanceledException -> ()
    }



[<EntryPoint>]
let main argv =
    try
        let parser = ArgumentParser.Create<CliArgs> ()
        let args = parser.ParseCommandLine argv
        use cancelToken = new CancellationTokenSource ()
        use _ = PosixSignalRegistration.Create (PosixSignal.SIGINT, (fun ctx ->
            ctx.Cancel <- true
            cancelToken.Cancel ()))

        let job =
            match args.GetAllResults () with
            | [ ] ->
                printfn "%s" (parser.PrintUsage ())
                async { return () }
            | [ CliArgs.Server serverArgs ] -> runServer serverArgs cancelToken.Token
            | [ CliArgs.Client clientArgs ] -> runClient clientArgs cancelToken.Token
            | _ ->
                printfn "%s" (parser.PrintUsage ())
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
