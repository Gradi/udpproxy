module UdpProxy.PipelinesBuilders

open Autofac
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Serilog
open System
open UdpProxy.JObject
open UdpProxy.Pipelines
open UdpProxy.Services


type IPipelineBuilder =

    abstract Register : ContainerBuilder -> unit


type private PipelineBuilder (registration: ContainerBuilder -> unit) =

    interface IPipelineBuilder with

        member _.Register container = registration container


type private Metadata =
    { Name: string
      Inverted: bool }


type RegFunc = IComponentContext -> IPipeline


let private readMetadata (jobj: JObject) =
    { Name = getProp<string> "$type" jobj
      Inverted = tryGetProp<bool> "$inverse" jobj |> Option.defaultValue false }


type PipelineBuilderJsonConverter () =
    inherit JsonConverter ()

    let finish (metadata: Metadata) (reg: RegFunc) : IPipelineBuilder =
        let reg =
            match metadata.Inverted with
            | false -> reg
            | true -> (fun c -> InvertedPipeline (reg c))

        PipelineBuilder (fun container -> container.Register<IPipeline>(reg).SingleInstance().AsSelf() |> ignore)


    let readRndPad (jobj: JObject) : RegFunc =
        let minBytes = getProp<int> "min" jobj
        let maxBytes = getProp<int> "max" jobj

        (fun (c: IComponentContext) -> RndPadPipeline (minBytes, maxBytes, c.Resolve<ILogger> (), c.Resolve<ICryptoRnd> ()))


    let readMailman (jobj: JObject) : RegFunc =
        let outputEndpoints = getProp<Endpoint list> "output" jobj

        (fun (c: IComponentContext) -> MailmanPipeline (outputEndpoints, c.Resolve<Lazy<IDns>> (), c.Resolve<Lazy<ISocketCollection>>() ,
                                                        c.Resolve<Lazy<IConnectionTracking>>(), c.Resolve<ILogger> ()))


    override this.CanConvert objectType = objectType = typeof<IPipelineBuilder>

    override this.WriteJson (_, _, _) =
        raise (NotImplementedException ())

    override this.ReadJson (reader, _, _, _) =
        let jObj = JObject.Load reader
        let metadata = readMetadata jObj

        let pipeline : IPipelineBuilder =
            match metadata.Name.ToLower () with
            | "rndpad" -> readRndPad jObj |> finish metadata
            | "mailman" -> readMailman jObj |> finish metadata
            | t -> failwithf "Unknown pipeline type \"%s\"" t

        box pipeline
