module UEcho.Program

open Argu
open CsvHelper
open System
open System.Diagnostics
open System.Globalization
open System.Net
open System.Net.Sockets
open System.Runtime.InteropServices
open System.Text
open System.Text.RegularExpressions
open System.Threading


[<NoComparison;NoEquality>]
type SendReceiveResult =
    { Index: int
      Duration: TimeSpan
      LastPing: TimeSpan
      MinPing: TimeSpan
      MaxPing: TimeSpan
      AvgPing: TimeSpan
      Result: Result<unit, string> }


let log fmt =
    Printf.ksprintf (fun msg ->
        printfn "%s: %s" (DateTime.Now.ToString("HH:mm:ss.fff")) msg) fmt


let logwhen whenTrue fmt =
    Printf.ksprintf (fun msg ->
        if whenTrue then
            log "%s" msg) fmt


let myMin<'a when 'a :> IComparable<'a>> (left: 'a) (right: 'a) =
    let compareResult = left.CompareTo right
    if compareResult < 0 then
        left
    else
        right


let myMax<'a when 'a :> IComparable<'a>> (left: 'a) (right: 'a) =
    let compareResult = right.CompareTo left
    if compareResult < 0 then
        left
    else
        right


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

        let verbose = serverArgs.Contains ServerArgs.Verbose


        use udpClient = new UdpClient (endpoint)
        (logwhen verbose) "Listening on %O" endpoint

        try
            while not cancelToken.IsCancellationRequested do
                let! payload = udpClient.ReceiveAsync(cancelToken).AsTask() |> Async.AwaitTask
                let msgPrefix = sprintf "(%d bytes from %O) " (Array.length payload.Buffer) payload.RemoteEndPoint

                match EchoRequest.tryParse payload.Buffer with
                | Error error -> (logwhen verbose) "%sCan't parse: %s" msgPrefix error
                | Ok echoRequest ->
                    match EchoRequest.verify echoRequest with
                    | Error errors -> (logwhen verbose) "%sVerify failed: %A" msgPrefix errors
                    | Ok () ->
                        (logwhen verbose) "%sOK!" msgPrefix
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


let pattern2request (pattern: string) =
    match pattern with
    | TextPattern request -> Some request
    | HexPattern request -> Some request
    | _ -> None


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


let sendReceiveRequests (udpClient: UdpClient)
                        (sleep: TimeSpan)
                        (requestFactory: unit -> EchoRequest.EchoRequest option)
                        (resultHandler:  SendReceiveResult -> Async<unit>)
                        (cancelToken: CancellationToken) : Async<unit> =
    async {
        let mutable index         = -1
        let duration              = Stopwatch.StartNew ()
        let mutable lastPing      = TimeSpan.Zero
        let mutable minPing       = TimeSpan.MaxValue
        let mutable maxPing       = TimeSpan.MinValue
        let mutable avgPing       = TimeSpan.Zero
        let mutable avgPingCount  = 0


        let makeResult result =
            { Index = index
              Duration = duration.Elapsed
              LastPing = lastPing
              MinPing = minPing
              MaxPing = maxPing
              AvgPing = TimeSpan.FromSeconds (avgPing.TotalSeconds / (float (max avgPingCount 1)))
              Result = result }

        let sleep () = async {
            if sleep > TimeSpan.Zero then
                do! Async.Sleep sleep
        }


        while not cancelToken.IsCancellationRequested do
            do! sleep ()
            try
                match requestFactory () with
                | None -> ()
                | Some request ->
                    let bytes = EchoRequest.serialize request
                    let pingStart = Stopwatch.GetTimestamp ()
                    let! _ = udpClient.SendAsync(bytes, Array.length bytes) |> Async.AwaitTask

                    use cancelToken = new CancellationTokenSource (TimeSpan.FromSeconds 5.0)
                    try
                        let! response = udpClient.ReceiveAsync(cancelToken.Token).AsTask() |> Async.AwaitTask
                        let ping = Stopwatch.GetElapsedTime pingStart

                        index        <- index + 1
                        lastPing     <- ping
                        minPing      <- min ping minPing
                        maxPing      <- max ping maxPing
                        avgPing      <- avgPing.Add ping
                        avgPingCount <- avgPingCount + 1

                        match response.Buffer = bytes with
                        | true ->
                            do! resultHandler (makeResult (Ok ()))
                        | false ->
                            do! resultHandler (makeResult (Error "response not equal to request"))
                    with
                    | :? OperationCanceledException ->
                        do! resultHandler (makeResult (Error "timeout"))
                    | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) ->
                        do! resultHandler (makeResult (Error "timeout"))

            with
            | :? OperationCanceledException -> ()
            | :? AggregateException as exc when (exc.InnerException :? OperationCanceledException) -> ()
    }


let printResult (format: OutputFormat) (csvWriter: Lazy<CsvWriter>) (result: SendReceiveResult) =
    let result2str result =
        match result with
        | Ok () -> "ok"
        | Error msg -> sprintf "fail: %s" msg

    let formatLine (r: SendReceiveResult) =
        sprintf "#%3d: %O, %.0fms, min %.0fms, max %.0fms, avg %.2fms, %s"
                r.Index
                (r.Duration.ToString("hh\\:mm\\:ss"))
                r.LastPing.TotalMilliseconds
                r.MinPing.TotalMilliseconds
                r.MaxPing.TotalMilliseconds
                r.AvgPing.TotalMilliseconds
                (result2str r.Result)

    match format with
    | Newline
    | Oneline ->
        if format = Oneline then
            printf "\r%s" (formatLine result)
        else
            printfn "%s" (formatLine result)

    | Csv ->
        csvWriter.Value.WriteField<int> result.Index
        csvWriter.Value.WriteField (result.Duration.ToString ("hh\\:mm\\:ss"))
        csvWriter.Value.WriteField<float> (Math.Round (result.LastPing.TotalMilliseconds, 2))
        csvWriter.Value.WriteField<float> (Math.Round (result.MinPing.TotalMilliseconds, 2))
        csvWriter.Value.WriteField<float> (Math.Round (result.MaxPing.TotalMilliseconds, 2))
        csvWriter.Value.WriteField<float> (Math.Round (result.AvgPing.TotalMilliseconds, 2))
        csvWriter.Value.WriteField (result2str result.Result, shouldQuote = true)
        csvWriter.Value.NextRecord ()



let runClient (clientArgs: ParseResults<ClientArgs>) (cancelToken: CancellationToken) =
    async {
        let isInteractive = clientArgs.Contains ClientArgs.InteractiveMode
        let outputFormat = clientArgs.GetResult (ClientArgs.OutputFormat, defaultValue = OutputFormat.Newline)
        let sleep = TimeSpan.FromMilliseconds (float (clientArgs.GetResult (ClientArgs.SleepMs, defaultValue = 0)))
        let requestFactory =
            if isInteractive then
                askAndMakeRequest
            else
                match clientArgs.TryGetResult ClientArgs.Pattern with
                | Some pattern ->
                    match pattern2request pattern with
                    | Some request -> (fun () -> Some request)
                    | None -> failwithf "Can't parse pattern: %s" pattern
                | None -> failwith "For non interactive mode you need to supply pattern in command line args. Check '--help'."

        let formatNotCsv = outputFormat <> OutputFormat.Csv

        let host = (clientArgs.GetResult ClientArgs.Host)
        let host =
            match Dns.GetHostAddresses host with
            | [||] -> failwithf "Can't resolve %s." host
            | ips -> IPEndPoint (Array.head ips, clientArgs.GetResult ClientArgs.Port)


        use udpClient = new UdpClient (host.AddressFamily)
        udpClient.Connect host
        (logwhen formatNotCsv) "Local endpoint %O" udpClient.Client.LocalEndPoint
        (logwhen formatNotCsv) "Will send to %O" host

        let csvWriter =
            lazy (
                let csvWriter = new CsvWriter (Console.Out, CultureInfo.InvariantCulture, leaveOpen = true)
                csvWriter.WriteField "Index"
                csvWriter.WriteField "Duration"
                csvWriter.WriteField "Ping"
                csvWriter.WriteField "Min ping"
                csvWriter.WriteField "Max ping"
                csvWriter.WriteField "Avg ping"
                csvWriter.WriteField "Result"
                csvWriter.NextRecord ()
                csvWriter
            )

        let handler result =
            async {
                printResult outputFormat csvWriter result
            }

        do! sendReceiveRequests udpClient sleep requestFactory handler cancelToken
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
