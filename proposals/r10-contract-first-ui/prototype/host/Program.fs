module Causa.Ui.Prototype.Host.Program

open System
open System.Collections.Concurrent
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.FileProviders
open Causa.Ui.Contracts
open Causa.Ui.Fixtures
open Causa.Ui.Prototype

/// Bindings -> the Limen ViewState JSON (text, number, flag, list of flat rows).
let private toJson (b: Bindings) : JsonObject =
    let prim p : JsonNode =
        match p with
        | BindingPrimitive.Text s -> JsonValue.Create s
        | BindingPrimitive.Number n -> JsonValue.Create n
        | BindingPrimitive.Flag f -> JsonValue.Create f
    let value v : JsonNode =
        match v with
        | BindingValue.Text s -> JsonValue.Create s
        | BindingValue.Number n -> JsonValue.Create n
        | BindingValue.Flag f -> JsonValue.Create f
        | BindingValue.Items rows ->
            JsonArray(rows |> List.map (fun r -> JsonObject(r |> Map.toSeq |> Seq.map (fun (k, p) -> Collections.Generic.KeyValuePair(k, prim p))) :> JsonNode) |> Array.ofList)
    JsonObject(b |> Map.toSeq |> Seq.map (fun (k, v) -> Collections.Generic.KeyValuePair(k, value v)))

let private str (e: JsonElement) (k: string) =
    match e.TryGetProperty k with
    | true, v when v.ValueKind = JsonValueKind.String -> Some(v.GetString() |> string)
    | _ -> None

/// One message in, the next state out. Pure apart from reading the message.
let private step (scenario: Scenarios.Scenario) (state: FixtureBackend.State option) (message: JsonElement) =
    match str message "kind", state with
    | Some "Initialize", _ | _, None ->
        let hash = match message.TryGetProperty "location" with | true, l -> str l "hash" |> Option.defaultValue "" | _ -> ""
        FixtureBackend.init scenario (Navigation.parse hash)
    | Some "Event", Some s ->
        let e = message.GetProperty "event"
        match Events.toIntent (str e "name" |> Option.defaultValue "") (str e "key") (str e "value") with
        | Some intent -> FixtureBackend.dispatch intent s
        | None -> s
    | _, Some s -> s

[<EntryPoint>]
let main args =
    let web = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../web"))
    let builder = WebApplication.CreateBuilder(args)
    let app = builder.Build()
    let sessions = ConcurrentDictionary<string, FixtureBackend.State>()
    app.UseDefaultFiles(DefaultFilesOptions(FileProvider = new PhysicalFileProvider(web))) |> ignore
    app.UseStaticFiles(StaticFileOptions(FileProvider = new PhysicalFileProvider(web))) |> ignore
    app.MapPost(
        "/dispatch",
        Func<HttpContext, Threading.Tasks.Task>(fun ctx ->
            task {
                let session = ctx.Request.Query["session"].ToString()
                let scenario = Scenarios.tryParse (ctx.Request.Query["scenario"].ToString()) |> Option.defaultValue Scenarios.ConnectedCatalog
                let! message = JsonDocument.ParseAsync(ctx.Request.Body)
                let next = step scenario (match sessions.TryGetValue session with | true, s -> Some s | _ -> None) message.RootElement
                sessions[session] <- next
                let body = JsonObject()
                body["view"] <- toJson (InvestigationListScreen.bindings (FixtureBackend.project next))
                body["effects"] <- JsonArray()
                body["cancellations"] <- JsonArray()
                ctx.Response.ContentType <- "application/json"
                do! ctx.Response.WriteAsync(body.ToJsonString())
            })
    ) |> ignore
    app.Run()
    0
