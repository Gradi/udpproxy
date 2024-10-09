module UdpProxy.NewtonsoftJson

open System.Net
open System.Net.Sockets
open System.Text.RegularExpressions
open Microsoft.FSharp.Reflection
open Newtonsoft.Json
open System
open System.Reflection
open Newtonsoft.Json.Converters
open Newtonsoft.Json.Linq
open UdpProxy.PipelinesBuilders
open UdpProxy.Services


type ContractResolver () =
    inherit Newtonsoft.Json.Serialization.DefaultContractResolver ()

    override this.ResolvePropertyName(propertyName) =
        match propertyName with
        | null
        | "" -> propertyName
        | name ->
            let name = name.ToCharArray ()
            name[0] <- Char.ToLower name[0]
            String name


type OptionJsonConverter () =
    inherit JsonConverter ()

    override this.CanConvert objectType = Reflect.isOptionType objectType

    override this.WriteJson(writer, value, serializer) =
        match value with
        | null -> writer.WriteNull ()
        | value ->
            let value = Reflect.getOptionValue value
            serializer.Serialize (writer, value)

    override this.ReadJson(reader, objectType, _, serializer) =
        match reader.TokenType with
        | JsonToken.Null ->
            Reflect.makeOptionNone (objectType.GetGenericArguments()[0])
        | _ ->
            let value = serializer.Deserialize (reader, objectType.GetGenericArguments()[0])
            Reflect.makeOptionValue value


type ListJsonConverter () =
    inherit JsonConverter ()

    override this.CanConvert(objectType) = Reflect.isListType objectType

    override this.WriteJson(writer, value, serializer) =
        match value with
        | null -> writer.WriteNull ()
        | value ->
            writer.WriteStartArray ()
            for item in Reflect.getListValue value do
                serializer.Serialize (writer, item)
            writer.WriteEndArray ()

    override this.ReadJson(reader, objectType, _, serializer) =
        match reader.TokenType with
        | JsonToken.Null ->
            Reflect.emptyList (objectType.GetGenericArguments()[0])
        | JsonToken.StartArray ->
            let mutable items : obj list = []

            reader.Read () |> ignore
            while reader.TokenType <> JsonToken.EndArray do
                let newElement = serializer.Deserialize (reader, objectType.GetGenericArguments()[0])
                items <- items @ [ newElement ]
                reader.Read () |> ignore

            Reflect.castList (objectType.GetGenericArguments()[0]) items
        | token -> failwithf "Unexpected Json Token: \"%O\", expected null or array." token


type RecordJsonConverter () =
    inherit JsonConverter ()

    let resolveFieldName (prop: PropertyInfo) =
        match prop.GetCustomAttribute<JsonPropertyAttribute> () with
        | null -> prop.Name
        | attr when attr.PropertyName = null -> prop.Name
        | attr -> attr.PropertyName

    let tryGetPropertyDefault (prop: PropertyInfo) =
        match Reflect.tryGetMethod (sprintf "Default%s" prop.Name) prop.DeclaringType with
        | None -> None
        | Some method ->
            if method.IsStatic && (method.GetParameters() |> Array.length) = 0 then
                Some (method.Invoke (null, [||]))
            else
                None

    override this.CanConvert(objectType) = Reflect.isRecordType objectType

    override this.WriteJson(writer, value, serializer) =
        match value with
        | null -> writer.WriteNull ()
        | value ->
            let fields =
                FSharpType.GetRecordFields (value.GetType (), BindingFlags.Public ||| BindingFlags.NonPublic)
                |> Array.map (fun field -> {| Name = resolveFieldName field; Field = field |})

            writer.WriteStartObject ()
            for field in fields do
                writer.WritePropertyName field.Name
                serializer.Serialize (writer, field.Field.GetValue value)
            writer.WriteEndObject ()

    override this.ReadJson(reader, objectType, _, serializer) =
        match reader.TokenType with
        | JsonToken.StartObject
        | JsonToken.Null ->
            let makeRecord = FSharpValue.PreComputeRecordConstructor (objectType, BindingFlags.Public ||| BindingFlags.NonPublic)
            let fields =
                FSharpType.GetRecordFields (objectType, BindingFlags.Public ||| BindingFlags.NonPublic)
                |> Array.map (fun field -> {| Name = resolveFieldName field; Type = field.PropertyType; Prop = field |})
                |> Array.indexed

            let actualFieldValues : obj option array =
                Array.init (Array.length fields) (fun _ -> None)

            if reader.TokenType = JsonToken.StartObject then
                reader.Read () |> ignore
                while reader.TokenType <> JsonToken.EndObject do
                    if reader.TokenType <> JsonToken.PropertyName then
                        failwithf "Expected JSON reader to positioned at \"%O\", but actual position is \"%O\"." JsonToken.PropertyName reader.TokenType

                    let fieldName = reader.Value :?> string
                    reader.Read () |> ignore

                    match fields |> Array.filter (fun (_, f) -> f.Name = fieldName) with
                    | [|  |] -> JToken.Load reader |> ignore
                    | [| (index, field) |] ->
                        actualFieldValues[index] <- Some (serializer.Deserialize (reader, field.Type))
                    | fields ->
                        failwithf "Found %d fields named \"%s\" in record type \"%O\". Expected one or zero fields."
                                  (Array.length fields) fieldName objectType

                    reader.Read () |> ignore

            let actualFieldValues =
                actualFieldValues
                |> Array.indexed
                |> Array.map (fun (index, value) ->
                    match value with
                    | Some obj when not (isNull obj) -> Some obj
                    | Some _
                    | None ->
                        match tryGetPropertyDefault (snd (fields[index])).Prop with
                        | Some obj -> Some obj
                        | None ->
                            match Reflect.tryGetComfortDefault (snd (fields[index])).Type with
                            | None -> None
                            | Some value -> Some value)

            if Array.exists Option.isNone actualFieldValues then
                let missingFields =
                    actualFieldValues
                    |> Array.indexed
                    |> Array.filter (fun (_, value) -> Option.isNone value)
                    |> Array.map fst
                    |> Array.map (fun index -> fields[index] |> snd |> _.Name)
                    |> String.concat ", "

                failwithf "Can't deserialize record of \"%O\" type. Missing some fields (\"%s\")." objectType missingFields

            makeRecord (actualFieldValues |> Array.map _.Value)

        | token -> failwithf "Unexpected JSON token \"%O\", expected \"%O\"" token JsonToken.StartObject


type EndpointJsonConverter () =
    inherit JsonConverter ()

    let (|PortOnly|_|) (str: string) =
        match Regex.IsMatch (str, "^[0-9]+$") with
        | false -> None
        | true -> Some (Int32.Parse str)

    let (|IpV4AndPort|_|) (str: string) =
        let result = Regex.Match (str, "^([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}):([0-9]+)$")
        match result.Success with
        | false -> None
        | true -> Some (result.Groups[1].Value, Int32.Parse result.Groups[2].Value)

    let (|IpV6AndPort|_|) (str: string) =
        let result = Regex.Match (str, "^([0-9a-fA-F:].+)@([0-9]+)$")
        match result.Success with
        | false -> None
        | true -> Some (result.Groups[1].Value, Int32.Parse result.Groups[2].Value)

    let (|DomainAndPort|_|) (str: string) =
        let result = Regex.Match (str, "^([a-zA-Zа-яА-Я0-9-_.]+):([0-9]+)$")
        match result.Success with
        | false -> None
        | true -> Some (result.Groups[1].Value, Int32.Parse result.Groups[2].Value)


    override this.CanConvert(objectType) = typeof<Endpoint>.IsAssignableFrom objectType

    override this.WriteJson(writer, value, _) =
        match value with
        | null -> writer.WriteNull ()
        | value ->
            match value :?> Endpoint with
            | Endpoint.PortOnly port -> writer.WriteValue port
            | Endpoint.IpPort endPoint ->
                match endPoint.AddressFamily with
                | AddressFamily.InterNetwork ->
                    writer.WriteValue (sprintf "%O:%d" endPoint.Address endPoint.Port)
                | AddressFamily.InterNetworkV6 ->
                    writer.WriteValue (sprintf "%O@%d" endPoint.Address endPoint.Port)
                | family -> failwithf "AddressFamily \"%O\" is not supported. Only IPv4 or IPv6." family
            | Endpoint.Host (host, port) ->
                writer.WriteValue (sprintf "%s:%d" host port)

    override this.ReadJson(reader, _, _, _) =
        match reader.TokenType with
        | JsonToken.Integer -> Endpoint.PortOnly (int (reader.Value :?> int64))
        | JsonToken.String ->
            match reader.Value :?> string with
            | PortOnly port -> Endpoint.PortOnly port
            | IpV4AndPort (ipStr, port)
            | IpV6AndPort (ipStr, port) ->
                match IPAddress.TryParse ipStr with
                | false, _ -> failwithf "Could not parse \"%s\" into IP address" ipStr
                | true, ip -> Endpoint.IpPort (IPEndPoint (ip, port))
            | DomainAndPort (domain, port) -> Endpoint.Host (domain, port)
            | value -> failwithf "Could not parse \"%s\" into valid Endpoint." value
        | token -> failwithf "Unexpected JSON token \"%O\". Expected number or string." token


let configure () =
    let settings = JsonSerializerSettings ()
    settings.NullValueHandling <- NullValueHandling.Include
    settings.Formatting <- Formatting.Indented
    settings.MissingMemberHandling <- MissingMemberHandling.Ignore
    settings.ContractResolver <- ContractResolver ()
    settings.Converters.Add (OptionJsonConverter ())
    settings.Converters.Add (ListJsonConverter ())
    settings.Converters.Add (RecordJsonConverter ())
    settings.Converters.Add (EndpointJsonConverter ())
    settings.Converters.Add (PipelineBuilderJsonConverter ())
    settings.Converters.Add (StringEnumConverter () )
    JsonConvert.DefaultSettings <- (fun () -> settings)


