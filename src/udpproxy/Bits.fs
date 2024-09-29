module UdpProxy.Bits

open System
open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"

let write<'a when 'a : unmanaged> (value: 'a) (destination: Span<byte>) =
    if sizeof<'a> > destination.Length then
        failwithf "Can't write value of type \"%O\"(%d bytes) into destination of size %d bytes." typeof<'a> sizeof<'a> destination.Length
    else

        let mutable value : 'a = value
        let ptrValue: nativeptr<byte> = NativePtr.ofVoidPtr (NativePtr.toVoidPtr (&&value : nativeptr<'a>))

        for i in 0 .. (sizeof<'a> - 1) do
            destination[i] <- NativePtr.add ptrValue i |> NativePtr.read


let read<'a when 'a : unmanaged> (source: Span<byte>) =
    if sizeof<'a> > source.Length then
        failwithf "Can't read value of type \"%O\"(%d bytes) from source of size %d bytes."
                  typeof<'a> sizeof<'a> source.Length
    else

        let mutable result : 'a = Unchecked.defaultof<'a>
        let ptrResult : nativeptr<byte> = NativePtr.ofVoidPtr (NativePtr.toVoidPtr (&&result : nativeptr<'a>))

        for i in 0 .. (sizeof<'a> - 1) do
            NativePtr.set ptrResult i source[i]

        result
