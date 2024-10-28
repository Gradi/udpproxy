namespace UdpProxy.Services

open Serilog
open System
open System.Collections.Generic
open System.Diagnostics
open System.Threading
open UdpProxy.RWLock
open UdpProxy


type ICache =

    abstract TryGet<'a>: obj -> 'a option

    abstract TryGetTouch<'a>: obj -> 'a option

    abstract Put<'a>: obj -> 'a -> unit

    abstract PutWithTtl<'a>: obj -> 'a -> TimeSpan -> unit

    abstract Delete: obj -> unit

type ICacheFactory =

    abstract Create: name: string -> ICache


type private Entry (value: obj, ttl: TimeSpan, creationTime: TimeSpan) =

    let mutable lastAccessTime = creationTime

    member _.Value = value

    member _.Ttl = ttl

    member _.CreationTime = creationTime

    member _.LastAccessTime = lastAccessTime

    member _.Touch (time: TimeSpan) =
        lastAccessTime <- time

    member _.IsEntryOutdated (now: TimeSpan) = (now - lastAccessTime) >= ttl


type Cache (ttl: TimeSpan, timerInterval: TimeSpan, name: string, logger: ILogger) =

#if EventSourceProviders
    do
        CacheEventSource.Instance.Created ()
#endif

    let logger = logger.ForContext<Cache> ()
    let entries = Dictionary<obj, Entry> ()
    let locker = new ReaderWriterLockSlim ()

    let now () = Stopwatch.GetElapsedTime (0, Stopwatch.GetTimestamp ())

    let disposeValues (objs : obj seq) =
        for obj in objs do
            try
                match obj with
                | :? IDisposable as disposable -> disposable.Dispose ()
                | _ -> ()
            with
            | exc -> logger.Error (exc, "Cache<{Name}>: Error on disposing object of type {ObjectType}", name, (obj.GetType ()).FullName)

    let onTimer () =
#if EventSourceProviders
        CacheEventSource.Instance.OnTimerEnter ()
#endif
        let now = now ()

        let outdatedEntries, totalEntries =
            readLock locker (fun () ->
                let totalEntries = entries.Count
                let outdatedEntries =
                    entries
                    |> Seq.filter (fun kv -> kv.Value.IsEntryOutdated now)
                    |> Seq.map (fun kv -> (kv.Key, kv.Value.Value))
                    |> List.ofSeq
                (outdatedEntries, totalEntries))
#if EventSourceProviders
        CacheEventSource.Instance.OutdatedEntriesFetched ()
#endif

        if not (List.isEmpty outdatedEntries) then
            let afterDeleteEntries =
                writeLock locker (fun () ->
                    List.iter (fun (key, _) -> entries.Remove key |> ignore) outdatedEntries
                    entries.Count)

            logger.Debug ("Cache<{Name}>: Deleted {OutdatedCount} entries, before {BeforeCount}, after {AfterCount} entries.",
                          name, List.length outdatedEntries, totalEntries, afterDeleteEntries)
#if EventSourceProviders
            CacheEventSource.Instance.OutdatedEntriesDeleted afterDeleteEntries
#endif

            disposeValues (Seq.ofList outdatedEntries |> Seq.map snd)


    let timer =
        let timer = new System.Timers.Timer (timerInterval)
        timer.AutoReset <- false
        timer.Elapsed.Add (fun _ ->
            try
                onTimer ()
            with
            | exc -> logger.Error (exc, "Cache<{Name}>: onTimer() error.", name)

            timer.Start ())

        timer.Start ()
        timer

    member _.Cast<'a> (value: obj) =
        try
            value :?> 'a
        with
        | :? InvalidCastException ->
            let t = if isNull value then "null" else value.GetType().FullName
            failwithf "Can't cast value of type \"%s\ to type \"%O\"" t typeof<'a>


    interface ICache with

        member this.TryGet<'a> key =
#if EventSourceProviders
            CacheEventSource.Instance.TryGet ()
#endif
            ArgumentNullException.ThrowIfNull (key, nameof(key))

            readLock locker (fun () ->
                let result =
                    match entries.TryGetValue key with
                    | false, _ -> None
                    | true, value -> Some (this.Cast<'a> value.Value)
#if EventSourceProviders
                match result with
                | Some _ -> CacheEventSource.Instance.TryGetSome ()
                | None -> CacheEventSource.Instance.TryGetNone ()
#endif
                result)

        member this.TryGetTouch<'a> key =
#if EventSourceProviders
            CacheEventSource.Instance.TryGetTouch ()
#endif
            ArgumentNullException.ThrowIfNull (key, nameof(key))

            readLock locker (fun () ->
                let result =
                    match entries.TryGetValue key with
                    | false, _ -> None
                    | true, value ->
                        value.Touch (now ())
                        Some (this.Cast<'a> value.Value)
#if EventSourceProviders
                match result with
                | None -> CacheEventSource.Instance.TryGetTouchNone ()
                | Some _ -> CacheEventSource.Instance.TryGetTouchSome ()
#endif
                result)

        member _.Put<'a> (key: obj) (value: 'a) =
#if EventSourceProviders
            CacheEventSource.Instance.PutEnter ()
#endif
            ArgumentNullException.ThrowIfNull (key, nameof(key))
            writeLock locker (fun () -> entries[key] <- Entry (box value, ttl, now ()))
#if EventSourceProviders
            CacheEventSource.Instance.PutExit ()
#endif

        member _.PutWithTtl<'a> (key: obj) (value: 'a) (ttl: TimeSpan) =
#if EventSourceProviders
            CacheEventSource.Instance.PutWithTtlEnter ()
#endif
            ArgumentNullException.ThrowIfNull (key, nameof(key))
            if ttl < TimeSpan.Zero then failwithf "TTL is less than zero (%O)" ttl

            writeLock locker (fun () -> entries[key] <- Entry (box value, ttl, now ()))
#if EventSourceProviders
            CacheEventSource.Instance.PutWithTtlExit ()
#endif

        member _.Delete key =
#if EventSourceProviders
            CacheEventSource.Instance.DeleteEnter ()
#endif
            if not (isNull key) then
                writeLock locker (fun () -> entries.Remove key |> ignore)
#if EventSourceProviders
            CacheEventSource.Instance.DeleteExit ()
#endif

    interface IDisposable with

        member _.Dispose () =
            timer.Stop    ()
            timer.Dispose ()
            disposeValues (entries |> Seq.map _.Value.Value)
            entries.Clear ()


type CacheFactory (ttl: TimeSpan, timerInterval: TimeSpan, logger: ILogger) =

    let mutable allocatedCaches = []

    interface ICacheFactory with

        member this.Create name =
            lock this (fun () ->
                let cache = new Cache(ttl, timerInterval, name, logger)
                allocatedCaches <- cache :: allocatedCaches
                cache)

    interface IDisposable with

        member this.Dispose () =
            lock this (fun () ->
                for cache in allocatedCaches do
                    (cache :> IDisposable).Dispose ())
