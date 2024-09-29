module UdpProxy.RWLock

open System.Threading


let readLock (lock: ReaderWriterLockSlim) (f: unit -> 'a) =
    try
        lock.EnterReadLock ()
        f ()
    finally
        lock.ExitReadLock ()


let writeLock (lock: ReaderWriterLockSlim) (f : unit -> 'a) =
    try
        lock.EnterWriteLock ()
        f ()
    finally
        lock.ExitWriteLock ()

