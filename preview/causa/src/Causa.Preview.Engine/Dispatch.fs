/// The Limen boundary: one serialized BrowserToEngineMessage in, one EngineToBrowserMessage out. The engine keeps its
/// model between calls; the browser never holds application state.
module Causa.Preview.Dispatch

open Causa.HistoricalCompatibility
open Causa.Preview.Model
open Causa.Preview.Update

let private str k o = match Json.tryField k o with Some(JStr v) -> Some v | _ -> None

let private msgOfEvent (e: (string * JValue) list) : Msg option =
    let value = str "value" e |> Option.defaultValue ""
    match str "name" e with
    | Some "connect" -> Some Connect
    | Some "disconnect" -> Some Disconnect
    | Some "back" -> Some Back
    | Some "open" -> str "key" e |> Option.map Open
    | Some "show-create" -> Some ShowCreate
    | Some "create" -> Some CreateSubmit
    | Some "add-observation" -> Some AddObservation
    | Some "add-hypothesis" -> Some AddHypothesis
    | Some "save" -> Some Save
    | Some "reload" -> Some Reload
    | Some n when n.StartsWith "set:" -> Some(Field(n.Substring 4, value))
    | _ -> None

let private outcomeOf (o: (string * JValue) list) : HttpOutcome =
    let status = match Json.tryField "status" o with Some(JInt n) -> Some(int n) | _ -> None
    match str "kind" o with
    | Some "Success" ->
        let body = match Json.tryField "body" o with Some(JStr b) -> b | Some v -> Json.canonical v | None -> ""
        Success(status |> Option.defaultValue 0, body)
    | Some "Failure" -> Failure(str "reason" o |> Option.defaultValue "unknown", status)
    | Some "Cancelled" -> Cancelled
    | _ -> OutcomeUnknown(str "reason" o |> Option.defaultValue "unknown")

/// Decodes one browser message into engine messages. Unknown kinds change nothing (a LocationChanged is ignored).
let messagesOf (message: JValue) : Msg list * JValue option =
    match message with
    | JObj o ->
        match str "kind" o with
        | Some "LocationChanged" ->
            match Json.tryField "location" o with
            | Some(JObj l) -> [ Navigated(str "hash" l |> Option.defaultValue "") ], None
            | _ -> [], None
        | Some "Initialize" ->
            // Echo the host's offered protocol and contract: this engine needs nothing beyond protocol 1.3 Http.
            let handshake =
                match Json.tryField "handshake" o with
                | Some(JObj h) ->
                    Some(JObj [ "kind", JStr "Accepted"; "protocol", (Json.tryField "protocol" h |> Option.defaultValue JNull); "contract", (Json.tryField "contract" h |> Option.defaultValue JNull); "capabilities", JArr [] ])
                | _ -> None
            let route = match Json.tryField "location" o with Some(JObj l) -> (str "hash" l |> Option.defaultValue "") | _ -> ""
            (if route = "" || route = "#/" then [] else [ Navigated route ]), handshake
        | Some "Event" -> (match Json.tryField "event" o with Some(JObj e) -> (msgOfEvent e |> Option.toList), None | _ -> [], None)
        | Some "EffectResult" ->
            match Json.tryField "result" o with
            | Some(JObj r) ->
                match str "kind" r, str "correlationId" r, Json.tryField "outcome" r with
                | Some "HttpResult", Some id, Some(JObj out) -> [ Http(id, outcomeOf out) ], None
                | _ -> [], None
            | _ -> [], None
        | _ -> [], None
    | _ -> [], None

let private effectJson ((id, r): string * Causa.Preview.GitHub.HttpRequest) =
    JObj(
        [ "kind", JStr "Http"; "correlationId", JStr id; "method", JStr r.Method; "url", JStr r.Url
          "headers", JObj(r.Headers |> List.map (fun (k, v) -> k, JStr v)); "timeoutMs", JInt 30000L; "response", JStr r.Response ]
        @ (match r.Body with Some b -> [ "body", JStr b ] | None -> [])
    )

/// The pure step the WebAssembly export wraps: (model, message JSON) -> (model, response JSON).
/// The pure step the WebAssembly export wraps. `now` is the clock reading the boundary supplies for user events.
let step (now: Timestamp) (model: Model) (messageJson: string) : Model * string =
    match Json.parse messageJson with
    | Error e ->
        // A malformed message is a host bug; answer with the unchanged view and report it.
        let m = { model with Problem = Some $"Malformed host message ({e.Detail})." }
        m, Json.canonical (JObj [ "view", View.project m; "effects", JArr []; "cancellations", JArr [] ])
    | Ok message ->
        let msgs, handshake = messagesOf message
        let isUserEvent = match message with JObj o -> str "kind" o = Some "Event" | _ -> false
        let browserMoved = msgs |> List.exists (function Navigated _ -> true | _ -> false)
        let msgs = (if isUserEvent then [ Clock now ] else []) @ msgs
        let m, effects = msgs |> List.fold (fun (m, fx) msg -> let m', f = update m msg in m', fx @ f) (model, [])
        // The URL follows the model (push), except when the browser itself moved: then the model followed the URL.
        let navigation =
            let before, after = Routing.ofModel model, Routing.ofModel m
            if browserMoved || before = after then []
            else [ JObj [ "kind", JStr "Navigation"; "correlationId", JStr("nav" + string m.NextId); "operation", JStr "push"; "url", JStr(Routing.toHash after) ] ]
        let response =
            [ "view", View.project m; "effects", JArr((effects |> List.map effectJson) @ navigation); "cancellations", JArr [] ]
            @ (match handshake with Some h -> [ "handshake", h ] | None -> [])
        m, Json.canonical (JObj response)

/// The single mutable cell of the application: the WebAssembly host keeps the model between dispatches.
let mutable private current = initial

let handle (messageJson: string) : string =
    // Total at the boundary: a defect becomes a reported problem with the last good model, never a dead page.
    let m, out =
        let now =
            let t = System.DateTimeOffset.UtcNow
            Timestamp.tryParse (t.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture))
            |> Option.defaultWith (fun () -> failwith "the clock produced an unrepresentable instant")
        try step now current messageJson
        with ex ->
            let m = { current with Problem = Some("Internal error: " + ex.Message); Pending = Map.empty }
            m, Json.canonical (JObj [ "view", View.project m; "effects", JArr []; "cancellations", JArr [] ])
    current <- m
    out
