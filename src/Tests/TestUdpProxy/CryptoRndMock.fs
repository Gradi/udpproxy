namespace TestUdpProxy

open NUnit.Framework
open UdpProxy.Services


type internal CryptoRndMock () =

    interface ICryptoRnd with

        member this.Fill bytes =
            if bytes.Length = 0 then
                ()
            else
                TestContext.CurrentContext.Random.NextBytes bytes

        member this.NextInt min max = TestContext.CurrentContext.Random.Next (min, max)

