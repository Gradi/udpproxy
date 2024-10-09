module TestUdpProxy.Tests.TestSpan

open FsUnit
open NUnit.Framework
open System
open UdpProxy


[<Test>]
let ``equals returns true for equal`` ([<Range(0, 10, 1)>] size: int) =
    let numbers = Array.init size (fun _ -> TestContext.CurrentContext.Random.Next ())

    Span.equals (Span<int> (Array.copy numbers)) (Span<int> (Array.copy numbers))
    |> should be True


[<Test>]
let ``equals returns false for non equal`` ([<Range(1, 10, 1)>] size0: int) ([<Range(0, 10, 1)>] size1: int) =
    let numbers0 = Array.init size0 (fun _ -> TestContext.CurrentContext.Random.Next ())
    let numbers1 = Array.init size1 (fun _ -> (TestContext.CurrentContext.Random.Next ()) + 1)

    Span.equals (Span<int> numbers0) (Span<int> numbers1)
    |> should be False


[<Test>]
let ``equalsRo returns true for equal`` ([<Range(0, 10, 1)>] size: int) =
    let numbers = Array.init size (fun _ -> TestContext.CurrentContext.Random.Next ())

    Span.equalsRo (ReadOnlySpan<int> (Array.copy numbers)) (ReadOnlySpan<int> (Array.copy numbers))
    |> should be True


[<Test>]
let ``equalsRo returns false for non equal`` ([<Range(1, 10, 1)>] size0: int) ([<Range(0, 10, 1)>] size1: int) =
    let numbers0 = Array.init size0 (fun _ -> TestContext.CurrentContext.Random.Next ())
    let numbers1 = Array.init size1 (fun _ -> (TestContext.CurrentContext.Random.Next ()) + 1)

    Span.equalsRo (ReadOnlySpan<int> numbers0) (ReadOnlySpan<int> numbers1)
    |> should be False


[<Test>]
let ``equalsMemRo returns true for equal`` ([<Range(0, 10, 1)>] size: int) =
    let numbers = Array.init size (fun _ -> TestContext.CurrentContext.Random.Next ())

    Span.equalsMemRo (ReadOnlyMemory<int> (Array.copy numbers)) (ReadOnlyMemory<int> (Array.copy numbers))
    |> should be True


[<Test>]
let ``equalsMemRo returns false for non equal`` ([<Range(1, 10, 1)>] size0: int) ([<Range(0, 10, 1)>] size1: int) =
    let numbers0 = Array.init size0 (fun _ -> TestContext.CurrentContext.Random.Next ())
    let numbers1 = Array.init size1 (fun _ -> (TestContext.CurrentContext.Random.Next ()) + 1)

    Span.equalsMemRo (ReadOnlyMemory<int> numbers0) (ReadOnlyMemory<int> numbers1)
    |> should be False
