module TestUdpProxy.Tests.TestNewtonsoftJson

open FsUnit
open NUnit.Framework
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open System
open System.IO
open System.Net
open System.Net.Sockets
open UdpProxy.NewtonsoftJson
open UdpProxy.Reflect
open UdpProxy.Services

type private Container<'a> () =

        [<JsonProperty>]
        member val public Value = Unchecked.defaultof<'a> with get, set


[<AbstractClass>]
type BaseJsonConverterTest () as this =

    let createSerializer () =
        let settings = JsonSerializerSettings ()
        settings.Formatting <- Formatting.None
        settings.NullValueHandling <- NullValueHandling.Include
        settings.Converters.Insert (0, this.GetConverter ())
        JsonSerializer.Create settings

    let createContainerType (value: obj) =
        let t = if isNull value then typeof<obj> else value.GetType ()
        typedefof<Container<_>>.MakeGenericType [| t |]

    let createContainer (value: obj) =
        let containerType = createContainerType value
        let container = Activator.CreateInstance containerType
        (getProperty "Value" containerType).SetValue (container, value)
        container

    let readContainer (container: obj) =
        if isNull container then
            raise (ArgumentNullException (nameof(container)))

        (getProperty "Value" (container.GetType())).GetValue container

    let runConverter (data: obj) =
        use textWriter = new StringWriter ()
        let jsonWriter = new JsonTextWriter (textWriter)
        createSerializer().Serialize (jsonWriter, createContainer data)
        (jsonWriter :> IDisposable).Dispose ()
        let jsonStr = textWriter.ToString ()
        let jobj = JObject.Parse jsonStr
        jobj["Value"]

    [<Test>]
    member _.DoesNotThrow () =
        for sample in this.GetSampleObjects () do
            (fun () -> runConverter sample |> ignore)
            |> should not' (throw typeof<Exception>)

    [<Test>]
    member _.SerializeDeserializeReturnsEqualObject () =
        for sample in this.GetSampleObjects () do
            let serializer = createSerializer ()
            use stringWriter = new StringWriter ()
            let jsonWriter = new JsonTextWriter (stringWriter)

            serializer.Serialize (jsonWriter, createContainer sample)
            (jsonWriter :> IDisposable).Dispose ()

            use stringReader = new StringReader (stringWriter.ToString ())
            use jsonReader = new JsonTextReader (stringReader)

            let actualData =
                readContainer (serializer.Deserialize (jsonReader, createContainerType sample))

            actualData |> should be (equal sample)

    [<Test>]
    member _.StringRepresentationIsAsExpected () =
        for sample in this.GetSampleObjects () do
            let str = runConverter sample
            this.AssertValidString sample str

    abstract member GetSampleObjects : unit -> obj list

    abstract member GetConverter : unit -> JsonConverter

    abstract member AssertValidString : obj -> JToken -> unit


[<TestFixture>]
type TestOptionJsonConverter () =
    inherit BaseJsonConverterTest ()

    override _.GetSampleObjects() = [ None; Some "Hello World" ]

    override _.GetConverter() = OptionJsonConverter ()

    override _.AssertValidString obj jToken =
        match obj with
        | :? (string option) as option ->
            match option with
            | None -> jToken.Type |> should be (equal JTokenType.Null)
            | Some str -> jToken.ToString Formatting.None |> should be (equal (sprintf "\"%s\"" str))
        | _ -> Assert.Fail ()


[<TestFixture>]
type TestListJsonConverter () =
    inherit BaseJsonConverterTest ()

    override _.GetSampleObjects () = [ box ([]: int list); box ([ 1..10 ] : int list) ]

    override _.GetConverter () = ListJsonConverter ()

    override _.AssertValidString obj jToken =
        match obj with
        | :? (int list) as list ->
            match list with
            | [] ->
                jToken.Type |> should be (equal JTokenType.Array)
                (jToken :?> JArray).Count |> should be (equal 0)
            | items when List.length items = 10 -> jToken.ToString Formatting.None |> should be (equal "[1,2,3,4,5,6,7,8,9,10]")
            | _ -> Assert.Fail ()
        | _ -> Assert.Fail ()


type private SampleRecord =
    { Name: string
      Age: int
      Numbers: int array }


[<TestFixture>]
type TestRecordJsonConverter () =
    inherit BaseJsonConverterTest ()

    override _.GetSampleObjects () = [ box { Name = "Hi there"; Age = 18; Numbers = [| 1..10 |] } ]

    override _.GetConverter () = RecordJsonConverter ()

    override _.AssertValidString _ jToken =
        jToken.ToString (Formatting.None)
        |> should be (equal "{\"Name\":\"Hi there\",\"Age\":18,\"Numbers\":[1,2,3,4,5,6,7,8,9,10]}")


[<TestFixture>]
type TestEndpointJsonConverter () =
    inherit BaseJsonConverterTest ()

    override _.GetSampleObjects () =
        [
            Endpoint.PortOnly 56000
            Endpoint.IpPort (IPEndPoint (IPAddress.Parse ("56.98.244.78"), 766))
            Endpoint.IpPort (IPEndPoint (IPAddress.Parse ("2345:0425:2CA1:0000:0000:0567:5673:23b5"), 123))
            Endpoint.Host ("domain.com", 899)
        ]

    override _.GetConverter () = EndpointJsonConverter ()

    override _.AssertValidString obj jToken =
        let jToken = jToken.ToString Formatting.None

        match obj with
        | :? Endpoint as endPoint ->
            match endPoint with
            | Endpoint.PortOnly port -> jToken.ToString () |> should be (equal (port.ToString ()))
            | Endpoint.IpPort endpoint ->
                match endpoint.AddressFamily with
                | AddressFamily.InterNetwork ->
                    jToken
                    |> should be (equal "\"56.98.244.78:766\"")

                | AddressFamily.InterNetworkV6 ->
                    jToken
                    |> should be (equal "\"2345:425:2ca1::567:5673:23b5@123\"")

                | _ -> Assert.Fail ()

            | Endpoint.Host _ ->
                jToken
                |> should be (equal "\"domain.com:899\"")

        | _ -> Assert.Fail ()
