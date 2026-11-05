/// The Limen boundary: decodes BrowserToEngineMessage JSON, reads the clock, runs App.update, and encodes
/// EngineToBrowserMessage JSON. The single mutable model cell lives here and nowhere else.
module Exp002.Ui.Dispatch

open System.Text.Json
open System.Text.Json.Nodes
open Exp002.Contracts
open Exp002.Ui.App
open Exp002.Ui.View

let private str (e: JsonElement) (name: string) =
    match e.ValueKind with
    | JsonValueKind.Object ->
        match e.TryGetProperty name with
        | true, v when v.ValueKind = JsonValueKind.String -> Some(v.GetString())
        | _ -> None
    | _ -> None

let private child (e: JsonElement) (name: string) =
    match e.ValueKind with
    | JsonValueKind.Object ->
        match e.TryGetProperty name with
        | true, v -> Some v
        | _ -> None
    | _ -> None

/// Pure: one Limen message to zero or one App message.
let decodeMessage (json: string) : Msg option =
    use doc = JsonDocument.Parse json
    let root = doc.RootElement
    let hashOf () = child root "location" |> Option.bind (fun l -> str l "hash") |> Option.defaultValue ""
    match str root "kind" with
    | Some "Initialize" -> Some(Init(hashOf ()))
    | Some "LocationChanged" -> Some(LocationChanged(hashOf ()))
    | Some "Event" ->
        child root "event"
        |> Option.bind (fun ev ->
            str ev "name" |> Option.bind (fun name -> Events.decode name (str ev "key") (str ev "value")))
        |> Option.map Ui
    | Some "EffectResult" ->
        child root "result"
        |> Option.bind (fun r ->
            let corr = str r "correlationId" |> Option.defaultValue ""
            match str r "kind" with
            | Some "StorageResult" ->
                child r "outcome"
                |> Option.map (fun o ->
                    match str o "kind" with
                    | Some "Success" -> StorageResult(corr, StorageValue(str o "value"))
                    | _ -> StorageResult(corr, StorageFailed(str o "reason" |> Option.defaultValue "unknown")))
            | Some "NavigationResult" -> Some(NavigationDone corr)
            | _ -> None)
    | _ -> None

let private prim =
    function
    | S v -> JsonValue.Create v :> JsonNode
    | B v -> JsonValue.Create v :> JsonNode
    | N v -> JsonValue.Create v :> JsonNode

let private effectJson =
    function
    | StorageGet(c, k) -> JsonObject(dict [ "kind", JsonValue.Create "Storage" :> JsonNode; "operation", JsonValue.Create "get"; "correlationId", JsonValue.Create c; "key", JsonValue.Create k ])
    | StorageSet(c, k, v) ->
        JsonObject(dict [ "kind", JsonValue.Create "Storage" :> JsonNode; "operation", JsonValue.Create "set"; "correlationId", JsonValue.Create c; "key", JsonValue.Create k; "value", JsonValue.Create v ])
    | NavigatePush(c, url) -> JsonObject(dict [ "kind", JsonValue.Create "Navigation" :> JsonNode; "operation", JsonValue.Create "push"; "correlationId", JsonValue.Create c; "url", JsonValue.Create url ])

/// Pure: the view and effects as an EngineToBrowserMessage.
let encodeResponse (view: (string * ViewValue) list) (effects: Effect list) : string =
    let viewObj = JsonObject()
    for (k, v) in view do
        viewObj[k] <-
            match v with
            | Scalar p -> prim p
            | Items rows -> JsonArray(rows |> List.map (fun row -> JsonObject(row |> List.map (fun (f, p) -> System.Collections.Generic.KeyValuePair(f, prim p))) :> JsonNode) |> List.toArray)
    let response = JsonObject()
    response["view"] <- viewObj
    response["effects"] <- JsonArray(effects |> List.map (fun e -> effectJson e :> JsonNode) |> List.toArray)
    response["cancellations"] <- JsonArray()
    response.ToJsonString()

/// Pure step used by tests and by handle.
let step (ports: EnginePorts) (now: Timestamp) (model: Model) (json: string) : Model * string =
    let next, effects =
        match decodeMessage json with
        | Some msg -> update ports now model msg
        | None -> model, []
    next, encodeResponse (project next) effects

/// The only mutable state in the application: the current model, held at the boundary.
let mutable private cell: Model = init

/// Entry point for the [JSExport] shim. The clock is read here and only here.
let handleWith (ports: EnginePorts) (json: string) : string =
    let now = Timestamp(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
    let next, response = step ports now cell json
    cell <- next
    response
