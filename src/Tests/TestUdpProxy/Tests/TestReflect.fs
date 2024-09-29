module TestUdpProxy.Tests.TestReflect

open System
open FsUnit
open NUnit.Framework
open UdpProxy.Reflect



[<Test>]
let ``unwrapGeneric unwraps`` () =
    unwrapGeneric typedefof<obj>
    |> should equal typedefof<obj>

    unwrapGeneric typeof<Option<obj>>
    |> should not' (equal typeof<Option<obj>>)


[<Test>]
let ``isOption is true for Option`` () =
    isOption (box (Some 123))
    |> should be True

    isOption (box (None : int option))
    |> should be True


[<Test>]
let ``isOption is false for non Option`` () =
    isOption (obj ())
    |> should be False

    isOption (box DateTime.Now)
    |> should be False

    isOption (box [ 1; 2; 3 ])
    |> should be False


[<Test>]
let ``isOptionNone is true for None`` () =
    (box (None: int option))
    |> isOptionNone
    |> should be True


[<Test>]
let ``isOptionNone is false for Some`` () =
    (box (Some 123))
    |> isOptionNone
    |> should be False


[<Test>]
let ``isOptionSome is true for Some`` () =
    box (Some 123)
    |> isOptionSome
    |> should be True


[<Test>]
let ``isOptionSome is false for None`` () =
    box (None : int option)
    |> isOptionSome
    |> should be False


[<Test>]
let ``getOptionValue fails on null`` () =
    (fun () -> getOptionValue (Unchecked.defaultof<obj>) |> ignore)
    |> should throw typeof<ArgumentNullException>


[<Test>]
let ``getOptionValue returns value for Some`` () =
    let  input = box 123
    box (Some input)
    |> getOptionValue
    |> should be (equal input)


[<Test>]
let ``makeOptionNone returns None`` () =
    makeOptionNone typeof<int>
    |> should be (equal (None: int option))


[<Test>]
let ``makeOptionValue returns Some`` () =
    makeOptionValue (box 123)
    |> should be (equal (Some 123))


[<Test>]
let ``isListType returns true for list types`` () =
    isListType typeof<int list>
    |> should be True

    isListType typeof<obj list>
    |> should be True


[<Test>]
let ``isListType returns false for non list types`` () =
    isListType typeof<int>
    |> should be False

    isListType typeof<int option>
    |> should be False


[<Test>]
let ``getListValue returns list value`` () =
    box [ 1..10 ]
    |> getListValue
    |> Seq.ofList
    |> should equalSeq (Seq.ofList (List.map box  [ 1..10 ]))


[<Test>]
let ``getListValue fails for non list types`` () =
    (fun () -> getListValue (obj ()) |> ignore)
    |> should throw typeof<Exception>


[<Test>]
let ``emptyList returns empty list`` () =
    emptyList typeof<int>
    |> should be (equal (box ([] : int list)))


[<Test>]
let ``castList returns list`` () =
    [ 1..10 ]
    |> List.map box
    |> castList typeof<int>
    |> should be (equal (box [ 1..10 ]))


[<Test>]
let ``isRecordType returns false for non record types`` () =
    isRecordType typeof<obj>
    |> should be False

    isRecordType typeof<string>
    |> should be False


type private Sample = { Name: string }


[<Test>]
let ``isRecordType returns true for record types`` () =
    isRecordType typeof<Sample>
    |> should be True


[<Test>]
let ``isUnionType returns false for non union types`` () =
    isUnionType typeof<obj>
    |> should be False

    isUnionType typeof<string>
    |> should be False


type private Colors =
    | Red
    | Blue


[<Test>]
let ``isUnionType returns true for union types`` () =
    isUnionType typeof<Colors>
    |> should be True


[<Test>]
let ``tryGetComfortDefault returns comfort default values`` () =
    match tryGetComfortDefault typeof<Option<obj>> with
    | Some value ->
        value :? Option<obj>
        |> should be True

        Option.isNone (value :?> Option<obj>)
        |> should be True
    | None -> Assert.Fail "Expected 'Some None', got 'None'"

    match tryGetComfortDefault typeof<int list> with
    | Some value ->
        value :? (int list)
        |> should be True

        List.isEmpty (value :?> int list)
        |> should be True

    | None -> Assert.Fail "Expected 'Some []', got 'None'."


[<Test>]
let ``tryGetComfortDefault returns None for non comfort types`` () =
    tryGetComfortDefault typeof<obj>
    |> should be (equal (None: obj option))

    tryGetComfortDefault typeof<int>
    |> should be (equal (None: obj option))

    tryGetComfortDefault typeof<Colors>
    |> should be (equal (None: obj option))
