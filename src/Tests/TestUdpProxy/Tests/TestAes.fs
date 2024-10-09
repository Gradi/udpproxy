namespace TestUdpProxy.Tests

open System
open System.Security.Cryptography
open FsUnit
open NUnit.Framework


[<TestFixture>]
type TestAes () =

    [<Test>]
    member _.``EncryptCbc returns aligned bytes`` ([<Range(0, 1024, 1)>] size: int) =
        let input : byte array = Array.zeroCreate size
        TestContext.CurrentContext.Random.NextBytes input
        use aes = Aes.Create ()

        let output = aes.EncryptCbc (input, aes.IV, PaddingMode.PKCS7)

        (Array.length output) % 16
        |> should equal 0

    [<Test>]
    member _.``EncryptCbc then DecryptCbc returns same data`` ([<Range(0, 1024, 1)>] size: int) =
        let expectedOutput : byte array = Array.zeroCreate size
        TestContext.CurrentContext.Random.NextBytes expectedOutput

        use aes = Aes.Create ()

        let actualOutput =
            aes.DecryptCbc(aes.EncryptCbc (expectedOutput, aes.IV, PaddingMode.PKCS7), aes.IV, PaddingMode.PKCS7)

        Object.ReferenceEquals (expectedOutput, actualOutput)
        |> should be False

        (Seq.ofArray actualOutput)
        |> should equalSeq (Seq.ofArray expectedOutput)
