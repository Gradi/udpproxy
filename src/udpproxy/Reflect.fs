module UdpProxy.Reflect

open System
open System.Collections
open System.Reflection
open Microsoft.FSharp.Reflection


[<AbstractClass>]
type private ListHelpers private () =

    static member Cast<'a> (items: obj list) : 'a list =
        items
        |> List.map (fun item -> item :?> 'a)


let unwrapGeneric (t: Type) =
    match t.IsGenericType with
    | true -> t.GetGenericTypeDefinition ()
    | false -> t


let getMethod (name: string) (t: Type) =
    let methods =
        t.GetMethods (BindingFlags.Public ||| BindingFlags.Instance ||| BindingFlags.Static ||| BindingFlags.NonPublic)
        |> Array.filter (fun m -> m.Name = name)

    match methods with
    | [|  |] -> failwithf "Can't find method named \"%s\" in \"%O\"." name t
    | [| m |] -> m
    | methods -> failwithf "Found %d methods named \"%s\" in \"%O\". Expected to find 1 method." (Array.length  methods)
                           name t


let tryGetMethod (name: string) (t: Type) =
    let methods =
        t.GetMethods (BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance ||| BindingFlags.Static)
        |> Array.filter (fun m -> m.Name = name)

    match methods with
    | [|  |] -> None
    | [| method |] -> Some method
    | _ -> None


let getProperty (name: string) (t: Type) =
    let props =
        t.GetProperties (BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance ||| BindingFlags.Static)
        |> Array.filter (fun p -> p.Name = name)

    match props with
    | [|  |] -> failwithf "Can't find property named \"%s\" in \"%O\"." name t
    | [| prop |] -> prop
    | props -> failwithf "Found %d properties named \"%s\" in \"%O\". Expected 1 property." (Array.length props) name t


let isOptionType (t: Type) = (unwrapGeneric t) = typedefof<Option<_>>


let isOption (obj: obj) =
    match obj with
    | null -> true
    | obj -> isOptionType (obj.GetType ())


let isOptionNone (obj: obj) =
    match obj with
    | null -> true
    | obj ->
        let isNoneProp = obj.GetType().GetProperty("IsNone")
        isNoneProp.GetMethod.Invoke(null, [| obj |]) :?> bool


let isOptionSome (obj: obj) =
    match obj with
    | null -> false
    | obj ->
        let isSomeProp = obj.GetType().GetProperty("IsSome")
        isSomeProp.GetMethod.Invoke(null, [| obj |]) :?> bool


let getOptionValue (obj: obj) =
    match obj with
    | null -> raise (ArgumentNullException(nameof(obj)))
    | obj -> (obj.GetType().GetProperty("Value")).GetValue obj


let makeOptionNone (elementType: Type) =
    typedefof<Option<_>>.MakeGenericType([| elementType |]).GetProperty("None").GetValue null


let makeOptionValue (value: obj) =
    match value with
    | null -> raise (ArgumentNullException (nameof(value)))
    | value ->
        typedefof<Option<_>>.MakeGenericType([| value.GetType() |]).GetMethod("Some").Invoke(null, [| value |])


let isListType (t: Type) = (unwrapGeneric t) = typedefof<List<_>>


let getListValue (obj: obj) : obj list =
    match obj with
    | null -> []
    | obj ->
        let t = obj.GetType ()
        match isListType t with
        | false -> failwithf "Expected object of \"List\" type but got \"%O\"" t
        | true ->
            (obj :?> IEnumerable)
            |> Seq.cast<obj>
            |> List.ofSeq


let emptyList (listElemType: Type) =
    typedefof<List<_>>.MakeGenericType([| listElemType |]).GetProperty("Empty").GetValue null


let castList (listElemType: Type) (items: obj list) =
    (getMethod (nameof(ListHelpers.Cast)) typedefof<ListHelpers>).MakeGenericMethod([| listElemType |]).Invoke (null, [| items |])


let isRecordType (t: Type) = FSharpType.IsRecord (t, BindingFlags.Public ||| BindingFlags.NonPublic)


let isUnionType (t: Type) = FSharpType.IsUnion (t, BindingFlags.Public ||| BindingFlags.NonPublic)


let tryGetComfortDefault (t: Type) : obj option =
    if isOptionType t then
        Some (makeOptionNone (t.GetGenericArguments()[0]))
    else if isListType t then
        Some (emptyList (t.GetGenericArguments()[0]))
    else
        None
