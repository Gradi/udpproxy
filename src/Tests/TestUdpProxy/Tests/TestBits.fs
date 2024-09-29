module TestUdpProxy.Tests.TestBits

open FsUnit
open NUnit.Framework
open System
open UdpProxy


[<Test>]
let ``write (...) throws on invalid destination size`` () =
    (fun () -> Bits.write<int> 0 (Span<byte> [|  |]))
    |> should throw typeof<Exception>


[<Test>]
let ``write (...) doesn't fail `` ([<Range(0, 100)>] value: int) =
    (fun () -> Bits.write<int> value (Span<byte> [| 0uy; 0uy; 0uy; 0uy |]))
    |> should not' (throw typeof<Exception>)


[<Test>]
let ``write (...) writes correctly`` () =
    let input = 0xdeadbeefu
    let buffer : byte array = Array.zeroCreate 128

    Bits.write input (Span<byte> buffer)

    buffer[0] |> should equal 0xefuy
    buffer[1] |> should equal 0xbeuy
    buffer[2] |> should equal 0xaduy
    buffer[3] |> should equal 0xdeuy

    for i in 4..127 do
        buffer[i] |> should equal 0x00uy


[<Test>]
let ``read (...) throws on invalid source size`` () =
    (fun () -> Bits.read<int> (Span<byte> [|  |]) |> ignore)
    |> should throw typeof<Exception>


[<Test>]
let ``read (...) doesn't fail`` () =
    (fun () -> Bits.read<int> (Span<byte> [| 0uy; 0uy; 0uy; 0uy |]) |> ignore)
    |> should not' (throw typeof<Exception>)


[<Test>]
let ``write and read returns correct value`` ([<Random(100)>] value: int) =
    let buffer = [| 0uy; 0uy; 0uy; 0uy |]

    Bits.write value (Span<byte> buffer)

    Bits.read<int> (Span<byte> buffer)
    |> should equal value
