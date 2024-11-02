namespace UdpProxy

open System
open System.Threading

type AsyncLock () =

    let event = new AutoResetEvent (true)

    member _.Lock (f: unit -> Async<'a>) : Async<'a> =
        async {
            let! _ = Async.AwaitWaitHandle event

            try
                return! f ()
            finally
                event.Set () |> ignore
        }

    interface IDisposable with

        member _.Dispose () = event.Dispose ()
