// Practice only (FENCE.md). The whole application: a pure update over an immutable model, a pure projection to the
// Limen view, and the protocol codec. The kernel only renders the view and performs the HTTP effects it is handed.
namespace Causa.Practice

open System
open System.Text.Json.Nodes
open Causa.HistoricalCompatibility

type Route =
    | ConnectScreen
    | Browse
    | Open of string
    | CreateScreen

type Purpose =
    | Probe
    | ListDir
    | Read of dir: string * name: string * readBack: bool
    | Write of dir: string * name: string

type ListState =
    | NotListed
    | Listing
    | Listed of string list
    | ListFailed of string

type LoadState =
    | Fetching of Map<string, byte[]>
    | Loaded of Layers
    | LoadFailed of string

type SaveStatus =
    | Saving
    | Saved
    | SaveFailed of string

type ReadBack =
    | NotStarted
    | Reading of Map<string, byte[]>
    | ReadBackVerified of stateSha256: string
    | ReadBackFailed of string

type Created =
    { Files: (string * byte[]) list
      Written: string list
      Status: SaveStatus
      ReadBack: ReadBack }

type Model =
    { Draft: Map<string, string>
      Conn: Connection option
      Connected: bool
      Notice: string
      ConnectError: string
      FieldErrors: Map<string, string>
      Route: Route
      Listing: ListState
      Loads: Map<string, LoadState>
      Pending: Map<string, Purpose>
      Creates: Map<string, Created>
      Counter: int64 }

type HttpOutcome =
    | Responded of status: int * body: JsonNode
    | NoResponse of string

type Input =
    | Init
    | Event of name: string * key: string option * value: string option
    | HttpDone of correlationId: string * HttpOutcome

type Context = { Now: DateTimeOffset; Fresh: unit -> Guid }

module Engine =
    let fileNames = StorageReader.fileNames

    let defaults =
        Map.ofList
            [ "api", GitHub.realApi; "owner", "kemiller2002"; "repo", "causa-data"; "branch", "main"
              "path", "workspaces/demo/demonstration-v1/investigations"; "token", ""; "title", ""; "original", "" ]

    let initial =
        { Draft = defaults; Conn = None; Connected = false; Notice = ""; ConnectError = ""; FieldErrors = Map.empty; Route = ConnectScreen
          Listing = NotListed; Loads = Map.empty; Pending = Map.empty; Creates = Map.empty; Counter = 0L }

    let private draft (m: Model) k = m.Draft.TryFind k |> Option.defaultValue ""

    /// Issues requests: each gets a fresh correlation id recorded with its purpose.
    let private issue (ctx: Context) (m: Model) (reqs: (Purpose * (string -> HttpRequest)) list) : Model * (string * HttpRequest) list =
        reqs
        |> List.fold
            (fun (m: Model, out) (purpose, mk) ->
                let id = "r" + string (m.Counter + 1L) + "-" + ctx.Fresh().ToString("N")
                { m with Counter = m.Counter + 1L; Pending = m.Pending.Add(id, purpose) }, out @ [ id, mk id ])
            (m, [])

    let private readAll (dir: string) (readBack: bool) (c: Connection) =
        fileNames |> List.map (fun n -> Read(dir, n, readBack), GitHub.read c dir n)

    let private fetchIfNeeded ctx (m: Model) (dirs: string list) =
        match m.Conn with
        | None -> m, []
        | Some c ->
            let todo = dirs |> List.filter (fun d -> not (m.Loads.ContainsKey d))
            let m' = { m with Loads = todo |> List.fold (fun (l: Map<_, _>) d -> l.Add(d, Fetching Map.empty)) m.Loads }
            issue ctx m' (todo |> List.collect (fun d -> readAll d false c))

    let private relist ctx (m: Model) =
        match m.Conn with
        | Some c -> issue ctx { m with Listing = Listing } [ ListDir, GitHub.list c ]
        | None -> m, []

    // ------------------------------------------------------------------ user events

    let private connect ctx (m: Model) =
        let api = (draft m "api").Trim().TrimEnd('/')
        let errors =
            [ if not (GitHub.validApi api) then "api", "Enter an https URL."
              for k in [ "owner"; "repo"; "branch"; "path" ] do
                  if (draft m k).Trim() = "" then k, "Required." ]
        if not errors.IsEmpty then { m with FieldErrors = Map.ofList errors; ConnectError = "Check the highlighted fields." }, []
        else
            let token = (draft m "token").Trim()
            let c =
                { Api = api; Owner = (draft m "owner").Trim(); Repo = (draft m "repo").Trim(); Branch = (draft m "branch").Trim()
                  Path = (draft m "path").Trim().Trim('/'); Token = (if token = "" then None else Some token) }
            // the token leaves the draft as soon as it is used: it is held only inside the connection
            issue ctx { m with Conn = Some c; Connected = false; FieldErrors = Map.empty; ConnectError = ""; Notice = "Connecting…"; Draft = m.Draft.Add("token", "") } [ Probe, GitHub.probe c ]

    let private create ctx (m: Model) =
        match m.Conn with
        | Some { Token = None } -> { m with Notice = "Saving needs a token with write access: reconnect with one." }, []
        | None -> { m with Route = ConnectScreen }, []
        | Some c ->
            match Create.validate ctx.Now (ctx.Fresh(), ctx.Fresh(), ctx.Fresh()) (draft m "title") (draft m "original") with
            | Error errs -> { m with FieldErrors = Map.ofList errs }, []
            | Ok n ->
                match Create.files n with
                | Error e -> { m with Notice = "Could not build the investigation: " + e }, []
                | Ok files ->
                    let dir = n.InvestigationId.Text
                    let created = { Files = files; Written = []; Status = Saving; ReadBack = NotStarted }
                    let m' = { m with Creates = m.Creates.Add(dir, created); FieldErrors = Map.empty; Route = Open dir; Notice = "Saving…"; Draft = m.Draft.Add("title", "").Add("original", "") }
                    // one file at a time, in order: the manifest is written last
                    let name, bytes = List.head files
                    issue ctx m' [ Write(dir, name), (fun _ -> GitHub.write c dir name bytes) ]

    let private onEvent ctx (m: Model) (name: string) (key: string option) (value: string option) : Model * (string * HttpRequest) list =
        match name, key with
        | n, _ when n.StartsWith "draft:" -> { m with Draft = m.Draft.Add(n.Substring 6, defaultArg value "") }, []
        | "connect", _ -> connect ctx m
        | "disconnect", _ -> { initial with Draft = m.Draft }, []
        | "showBrowse", _ -> { m with Route = (if m.Connected then Browse else ConnectScreen) }, []
        | "showCreate", _ -> { m with Route = (if m.Connected then CreateScreen else ConnectScreen); FieldErrors = Map.empty }, []
        | "refresh", _ -> relist ctx { m with Loads = Map.empty }
        | "open", Some dir -> fetchIfNeeded ctx { m with Route = Open dir } [ dir ]
        | "create", _ -> create ctx m
        | "readBack", _ ->
            let dir = match m.Route with Open d -> d | _ -> ""
            match m.Conn, m.Creates.TryFind dir with
            | Some c, Some cr -> issue ctx { m with Creates = m.Creates.Add(dir, { cr with ReadBack = Reading Map.empty }) } (readAll dir true c)
            | _ -> m, []
        | other, _ -> { m with Notice = $"Unrecognized event: {other}" }, []

    // ------------------------------------------------------------------ effect results

    let private statusText (o: HttpOutcome) =
        match o with
        | Responded(s, body) ->
            let msg = match body with :? JsonObject as b when not (isNull b["message"]) -> ": " + b["message"].ToString() | _ -> ""
            $"HTTP {s}{msg}"
        | NoResponse r -> r

    let private collect (acc: Map<string, byte[]>) (name: string) (o: HttpOutcome) : Result<Map<string, byte[]>, string> =
        match o with
        | Responded(200, body) -> GitHub.bytes body |> Result.map (fun b -> acc.Add(name, b)) |> Result.mapError (fun e -> $"{name}: {e}")
        | other -> Error $"{name}: {statusText other}"

    let private rawOf dir (files: Map<string, byte[]>) = { Directory = dir; Files = fileNames |> List.map (fun n -> n, files[n]) }

    let private onResult ctx (m: Model) (purpose: Purpose) (o: HttpOutcome) : Model * (string * HttpRequest) list =
        match purpose, o with
        | Probe, Responded(200, _) ->
            relist ctx { m with Connected = true; Route = Browse; Notice = "Connected." }
        | Probe, other ->
            let hint = match other with Responded(401, _) -> " (the token was refused)" | Responded(404, _) -> " (no such repository, or the token cannot see it)" | _ -> ""
            { m with Conn = None; Connected = false; Notice = ""; ConnectError = $"Could not connect: {statusText other}{hint}" }, []
        | ListDir, Responded(200, body) ->
            match GitHub.names body with
            | Ok names ->
                let dirs = names |> List.filter (Uuid.tryParse >> Option.isSome) |> List.distinct |> List.sort
                fetchIfNeeded ctx { m with Listing = Listed dirs; Notice = $"{dirs.Length} investigation(s) found." } dirs
            | Error e -> { m with Listing = ListFailed e }, []
        | ListDir, Responded(404, _) -> { m with Listing = Listed []; Notice = "No investigations directory yet." }, []
        | ListDir, other -> { m with Listing = ListFailed(statusText other) }, []
        | Read(dir, name, false), o ->
            match m.Loads.TryFind dir with
            | Some(Fetching acc) ->
                match collect acc name o with
                | Error e -> { m with Loads = m.Loads.Add(dir, LoadFailed e) }, []
                | Ok acc when acc.Count = fileNames.Length -> { m with Loads = m.Loads.Add(dir, Loaded(Layers.load (rawOf dir acc))) }, []
                | Ok acc -> { m with Loads = m.Loads.Add(dir, Fetching acc) }, []
            | _ -> m, []
        | Read(dir, name, true), o ->
            match m.Creates.TryFind dir with
            | Some({ ReadBack = Reading acc } as cr) ->
                match collect acc name o with
                | Error e -> { m with Creates = m.Creates.Add(dir, { cr with ReadBack = ReadBackFailed e }) }, []
                | Ok acc when acc.Count = fileNames.Length ->
                    let layers = Layers.load (rawOf dir acc)
                    let sameBytes = cr.Files |> List.forall (fun (n, b) -> acc.TryFind n |> Option.exists (fun r -> r = b))
                    let rb =
                        match layers.Replayed with
                        | Ok r when Layers.isVerified layers && sameBytes -> ReadBackVerified r.StateSha256
                        | _ when not sameBytes -> ReadBackFailed "the bytes read back differ from the bytes written"
                        | _ -> ReadBackFailed "the read-back did not decode, replay and verify"
                    relist ctx { m with Creates = m.Creates.Add(dir, { cr with ReadBack = rb }); Loads = m.Loads.Add(dir, Loaded layers) }
                | Ok acc -> { m with Creates = m.Creates.Add(dir, { cr with ReadBack = Reading acc }) }, []
            | _ -> m, []
        | Write(dir, name), o ->
            match m.Creates.TryFind dir, m.Conn with
            | Some cr, Some c ->
                match o with
                | Responded(s, _) when s = 200 || s = 201 ->
                    let written = cr.Written @ [ name ]
                    match cr.Files |> List.tryFind (fun (n, _) -> not (List.contains n written)) with
                    | Some(next, bytes) -> issue ctx { m with Creates = m.Creates.Add(dir, { cr with Written = written }) } [ Write(dir, next), (fun _ -> GitHub.write c dir next bytes) ]
                    | None ->
                        // saved: read it back independently of the bytes in memory
                        issue ctx { m with Notice = "Saved. Reading it back…"; Creates = m.Creates.Add(dir, { cr with Written = written; Status = Saved; ReadBack = Reading Map.empty }) } (readAll dir true c)
                | other -> { m with Notice = ""; Creates = m.Creates.Add(dir, { cr with Status = SaveFailed $"{name}: {statusText other}" }) }, []
            | _ -> m, []

    let update (ctx: Context) (m: Model) (input: Input) : Model * (string * HttpRequest) list =
        match input with
        | Init -> m, []
        | Event(name, key, value) -> onEvent ctx m name key value
        | HttpDone(id, o) ->
            match m.Pending.TryFind id with
            | None -> m, []
            | Some p -> onResult ctx { m with Pending = m.Pending.Remove id } p o
