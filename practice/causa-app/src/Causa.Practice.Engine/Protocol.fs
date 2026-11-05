// Practice only (FENCE.md). Limen protocol 1 codec and the one stateful cell at the browser boundary.
namespace Causa.Practice

open System
open System.Text.Json.Nodes

module Protocol =
    let private str (n: JsonNode) (k: string) = match n with null -> None | n -> (match n[k] with null -> None | v -> Some(v.GetValue<string>()))

    /// Browser-to-engine message -> input. Unknown message kinds are ignored (no state change).
    let decode (message: string) : Input option =
        let n = JsonNode.Parse message
        match str n "kind" with
        | Some "Initialize" -> Some Init
        | Some "Event" ->
            let e = n["event"]
            Some(Event(defaultArg (str e "name") "", str e "key", str e "value"))
        | Some "EffectResult" ->
            let r = n["result"]
            match str r "kind", str r "correlationId" with
            | Some "HttpResult", Some id ->
                let o = r["outcome"]
                let outcome =
                    match str o "kind" with
                    | Some "Success" -> Responded(o["status"].GetValue<int>(), (match o["body"] with null -> JsonObject() :> JsonNode | b -> b.DeepClone()))
                    | Some k -> NoResponse(k + (str o "reason" |> Option.map (fun r -> ": " + r) |> Option.defaultValue ""))
                    | None -> NoResponse "malformed outcome"
                Some(HttpDone(id, outcome))
            | _ -> None
        | _ -> None

    let encode (view: JsonObject) (effects: (string * HttpRequest) list) : string =
        let fx = JsonArray()
        effects
        |> List.iter (fun (id, r) ->
            let o = JsonObject()
            o["kind"] <- JsonValue.Create "Http"
            o["correlationId"] <- JsonValue.Create id
            o["method"] <- JsonValue.Create r.Method
            o["url"] <- JsonValue.Create r.Url
            let h = JsonObject()
            r.Headers |> List.iter (fun (k, v) -> h[k] <- JsonValue.Create v)
            o["headers"] <- h
            r.Body |> Option.iter (fun b -> o["body"] <- JsonValue.Create b)
            o["timeoutMs"] <- JsonValue.Create 30000
            fx.Add o)
        let out = JsonObject()
        out["view"] <- view
        out["effects"] <- fx
        out["cancellations"] <- JsonArray()
        out.ToJsonString()

    /// The only mutable state of the application: the current model, replaced by each pure update.
    let private model = ref Engine.initial

    let dispatch (now: DateTimeOffset) (fresh: unit -> Guid) (message: string) : string =
        let ctx = { Now = now; Fresh = fresh }
        let next, effects =
            match decode message with
            | Some input -> Engine.update ctx model.Value input
            | None -> model.Value, []
        model.Value <- next
        encode (View.project next) effects
