namespace UdpProxy.Services

open System
open System.Security.Cryptography


type ICryptoRnd =

    abstract member Fill: bytes: Span<byte> -> unit

    abstract member NextInt: min: int -> max: int -> int


type CryptoRnd () =

    interface ICryptoRnd with

        member this.Fill(bytes) = RandomNumberGenerator.Fill bytes

        member this.NextInt min max =
            RandomNumberGenerator.GetInt32 (min, max)
