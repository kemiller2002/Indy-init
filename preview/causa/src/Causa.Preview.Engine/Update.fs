/// Transitions. Pure: (model, message) -> (model, effects). Every remote read is a GitHub request at a named commit,
/// so what is shown is exactly what the remote holds at that commit.
module Causa.Preview.Update

open System
open Causa.HistoricalCompatibility
open Causa.Preview.GitHub
open Causa.Preview.Model

type HttpOutcome =
    | Success of status: int * body: string
    | Failure of reason: string * status: int option
    | Cancelled
    | OutcomeUnknown of reason: string

type Msg =
    | Field of name: string * value: string
    | Connect
    | Disconnect
    | Back
    | Open of key: string
    | ShowCreate
    | CreateSubmit
    | AddObservation
    | AddHypothesis
    | Save
    | Reload
    | Clock of Timestamp
    /// The browser moved on its own (Back, Forward, a pasted or reloaded link): Limen LocationChanged or Initialize.
    | Navigated of hash: string
    | Http of correlationId: string * HttpOutcome

type Effects = (string * HttpRequest) list

/// A short display form of a commit or id; total for any length.
let private short (s: string) = if s.Length > 8 then s.Substring(0, 8) else s

let private request (m: Model) (purpose: Purpose) (r: HttpRequest) : Model * Effects =
    let id = $"r{m.NextId}"
    { m with NextId = m.NextId + 1; Pending = m.Pending.Add(id, purpose); Busy = true }, [ id, r ]

let private requests (m: Model) (items: (Purpose * HttpRequest) list) : Model * Effects =
    items |> List.fold (fun (m, fx) (p, r) -> let m', f = request m p r in m', fx @ f) (m, [])

let private fail (m: Model) (problem: string) = { m with Problem = Some problem; Busy = not m.Pending.IsEmpty }, []

let private settle (m: Model) = { m with Busy = not m.Pending.IsEmpty }

/// Cache-busting for head reads: the branch endpoint is cacheable, a head read must not be (PERSIST-005).
let private fresh (m: Model) (r: HttpRequest) = { r with Url = r.Url + (if r.Url.Contains "?" then "&" else "?") + $"cb={m.NextId}" }

let private setField (f: Form) (name: string) (value: string) : Form =
    match name with
    | "catalogRepo" -> { f with CatalogRepo = value }
    | "catalogBranch" -> { f with CatalogBranch = value }
    | "writeRepo" -> { f with WriteRepo = value }
    | "writeBranch" -> { f with WriteBranch = value }
    | "workspace" -> { f with Workspace = value }
    | "token" -> { f with Token = value }
    | "newTitle" -> { f with NewTitle = value }
    | "newObservation" -> { f with NewObservation = value }
    | "editObservation" -> { f with EditObservation = value }
    | "editHypothesis" -> { f with EditHypothesis = value }
    | _ -> f

let private repoOf (m: Model) (s: Source) = match s with Catalog -> m.Catalog | WorkingCopy -> m.Write
let private headOfSource (m: Model) (s: Source) = match s with Catalog -> m.CatalogHead | WorkingCopy -> m.WriteHead

let private pathOf (m: Model) (s: Source) (dir: string) =
    match s with
    | Catalog -> m.CatalogWorkspace |> Option.map (fun w -> $"{w}/investigations/{dir}")
    | WorkingCopy -> Some(Writer.directory m.Form.Workspace dir)

/// Reads the three files of one investigation at one commit.
let private openAt (m: Model) (s: Source) (dir: string) (commit: string) : Model * Effects =
    match repoOf m s, pathOf m s dir with
    | Some repo, Some p ->
        let m = { m with Reads = m.Reads.Add((dir, commit), []); Problem = None; Status = $"Reading {dir} at {commit |> short}…" }
        StorageReader.fileNames
        |> List.map (fun f -> ReadFile(s, dir, commit, f), fileAt m.Form.Token repo commit $"{p}/{f}")
        |> requests m
    | _ -> fail m "Not connected to that repository."

let private loadFiles (m: Model) (s: Source) (dir: string) (commit: string) (entries: (string * byte[]) list) : Model =
    let files = { DirectoryName = dir; Entries = entries |> List.sortBy fst }
    let result = HistoricalLoader.load files
    let entry =
        match result with
        | StructurallyVerified h -> Some { Source = s; DirName = dir; Title = h.State.Title.Value; Detail = $"{h.State.Version} events"; ExpectedState = None }
        | LoadRejected _ -> None
    let entries' =
        match entry with
        | Some e when s = WorkingCopy -> (m.Entries |> List.filter (fun x -> not (x.Source = WorkingCopy && x.DirName = dir))) @ [ e ]
        | _ -> m.Entries
    { m with
        Opened = Some { Source = s; DirName = dir; ReadAt = commit; Files = files; Result = result; Unsaved = [] }
        Entries = entries'
        Screen = DetailScreen
        Status = (match result with StructurallyVerified _ -> $"Loaded {dir} at {commit |> short}." | LoadRejected(e, _) -> "Refused: " + ReaderCode.code e.Code) }

/// Builds the complete successor file set and sends one conditional commit (PERSIST-006).
let private commitFiles (m: Model) (dir: string) (lines: string list) (message: string) : Model * Effects =
    match m.Write, m.WriteHead, Writer.validWorkspace m.Form.Workspace with
    | Some repo, Some head, true ->
        match Writer.fileSet dir lines with
        | Error e -> fail m e
        | Ok files ->
            let p = Writer.directory m.Form.Workspace dir
            let payload = files.Entries |> List.map (fun (f, b) -> $"{p}/{f}", b)
            let m = { m with Status = "Saving…"; Problem = None; Confirmed = None }
            request m (Commit(dir, files, true)) (createCommit m.Form.Token repo head message payload)
    | None, _, _ -> fail m "No writable workspace is connected (enter a writable repository on the Connect screen)."
    | _, None, _ -> fail m "The writable branch head is not known yet."
    | _, _, false -> fail m "The workspace name must match ^[a-z0-9][a-z0-9-]{0,63}$ (CONTRACT §3.2)."

let private onAnswer (m: Model) (purpose: Purpose) (outcome: HttpOutcome) : Model * Effects =
    let ok =
        match outcome with
        | Success(s, body) when s >= 200 && s < 300 -> Ok body
        | Success(404, _) -> Error "404"
        | Success(s, body) -> Error $"HTTP {s}: {messageOf body}"
        | Failure(r, Some s) -> Error $"HTTP {s} ({r})"
        | Failure(r, None) -> Error $"no answer ({r})"
        | Cancelled -> Error "cancelled"
        | OutcomeUnknown r -> Error $"outcome unknown ({r})"
    match purpose, ok with
    | CatalogHead, Ok body ->
        match headOf body, m.Catalog with
        | Ok head, Some repo -> request { m with CatalogHead = Some head } (ActiveCatalog head) (fileAt m.Form.Token repo head "catalog/active.json")
        | Error e, _ -> fail m ("Catalog: " + e)
        | _, None -> fail m "Catalog repository not set."
    | ActiveCatalog head, Ok b64 ->
        match Json.parse (Text.Encoding.UTF8.GetString(Convert.FromBase64String b64).TrimEnd('\n')), m.Catalog with
        | Ok(JObj o), Some repo ->
            match Json.tryField "manifest" o, Json.tryField "workspace" o with
            | Some(JStr manifest), Some(JStr ws) -> request { m with CatalogWorkspace = Some ws } (CatalogManifest(head, ws)) (fileAt m.Form.Token repo head manifest)
            | _ -> fail m "catalog/active.json names no manifest and workspace."
        | _ -> fail m "catalog/active.json is not a JSON object."
    | CatalogManifest(_, _), Ok b64 ->
        match Json.parse (Text.Encoding.UTF8.GetString(Convert.FromBase64String b64).TrimEnd('\n')) with
        | Ok(JObj o) ->
            let cases =
                match Json.tryField "cases" o with
                | Some(JArr cs) ->
                    cs
                    |> List.choose (function
                        | JObj c ->
                            match Json.tryField "investigationId" c, Json.tryField "title" c, Json.tryField "expectedVersion" c, Json.tryField "expectedLifecycle" c with
                            | Some(JStr id), Some(JStr t), Some(JInt v), Some(JStr l) ->
                                let expected = match Json.tryField "expectedStateSha256" c with Some(JStr x) -> Some x | _ -> None
                                Some { Source = Catalog; DirName = id; Title = t; Detail = $"{v} events · catalog lifecycle {l}"; ExpectedState = expected }
                            | _ -> None
                        | _ -> None)
                | _ -> []
            settle { m with Entries = cases @ (m.Entries |> List.filter (fun e -> e.Source = WorkingCopy)); Screen = ListScreen; Status = $"Catalog: {cases.Length} investigations." }, []
        | _ -> fail m "The catalog manifest is not a JSON object."
    | WriteHead, Ok body ->
        match headOf body, m.Write with
        | Ok head, Some repo ->
            request { m with WriteHead = Some head } (WriteListing head) (listAt m.Form.Token repo head $"workspaces/demo/{m.Form.Workspace}/investigations")
        | Error e, _ -> fail m ("Working copy: " + e)
        | _, None -> fail m "Writable repository not set."
    | WriteListing _, Error "404" -> settle { m with Status = m.Status + " Working copy is empty." }, []
    | WriteListing _, Ok body ->
        match entriesOf body with
        | Ok items ->
            let dirs = items |> List.filter (fun (n, t) -> t = "dir" && (Uuid.tryParse n).IsSome) |> List.map fst
            let wc = dirs |> List.map (fun d -> { Source = WorkingCopy; DirName = d; Title = "Working copy " + (d |> short); Detail = "open to read"; ExpectedState = None })
            settle { m with Entries = (m.Entries |> List.filter (fun e -> e.Source = Catalog)) @ wc }, []
        | Error e -> fail m ("Working copy listing: " + e)
    | ReadFile(s, dir, commit, file), Ok b64 ->
        let key = (dir, commit)
        let got = (m.Reads.TryFind key |> Option.defaultValue []) @ [ file, Convert.FromBase64String b64 ]
        if List.length got = 3 then settle (loadFiles { m with Reads = m.Reads.Remove key } s dir commit got), []
        else settle { m with Reads = m.Reads.Add(key, got) }, []
    | ReadFile(_, dir, commit, _), Error e -> fail { m with Reads = m.Reads.Remove((dir, commit)) } $"Reading {dir}: {e}"
    | Commit(dir, expected, _), Ok body ->
        match commitOidOf body, m.Write with
        | Ok oid, Some repo ->
            let p = Writer.directory m.Form.Workspace dir
            let m = { m with WriteHead = Some oid; Reads = m.Reads.Add((dir, oid), []); Status = $"Committed {oid |> short}; reading it back…" }
            StorageReader.fileNames |> List.map (fun f -> ReadBack(dir, oid, f, expected), fileAt m.Form.Token repo oid $"{p}/{f}") |> requests m
        | Error e, _ -> fail m ("Save refused: " + e + ". Nothing was saved; your unsaved changes are kept. Reload before retrying if the branch moved.")
        | _, None -> fail m "Writable repository not set."
    | Commit _, Error e when e.StartsWith "outcome unknown" ->
        fail m "The save outcome is unknown: the commit may or may not exist. Reload to check before saving again."
    | Commit _, Error e -> fail m ("Save failed: " + e + ". Your unsaved changes are kept.")
    | ReadBack(dir, oid, file, expected), Ok b64 ->
        let key = (dir, oid)
        let got = (m.Reads.TryFind key |> Option.defaultValue []) @ [ file, Convert.FromBase64String b64 ]
        if List.length got < 3 then settle { m with Reads = m.Reads.Add(key, got) }, []
        else
            let sameBytes = got |> List.forall (fun (f, b) -> expected.Entries |> List.exists (fun (g, c) -> g = f && c = b))
            let m = loadFiles { m with Reads = m.Reads.Remove key } WorkingCopy dir oid got
            if sameBytes then
                settle { m with Confirmed = Some $"Saved: commit {oid} read back byte-identical (PERSIST-007)." ; Form = { m.Form with NewTitle = ""; NewObservation = ""; EditObservation = ""; EditHypothesis = "" } }, []
            else fail m $"Read-back of {oid} differs from what was written; the save is NOT confirmed."
    | ReadBack(dir, oid, _, _), Error e -> fail { m with Reads = m.Reads.Remove((dir, oid)) } $"Read-back failed ({e}); the save is NOT confirmed. Reload to check."
    | (CatalogHead | ActiveCatalog _ | CatalogManifest _), Error e -> fail m ("Catalog: " + e)
    | WriteHead, Error e -> fail m ("Working copy: " + e)
    | WriteListing _, Error e -> fail m ("Working copy listing: " + e)

/// Applies a route the browser asked for. Data a route needs is read first; a route that needs a connection waits.
let private navigate (m: Model) (hash: string) : Model * Effects =
    let m = { m with PendingRoute = None }
    let waitFor () = { m with PendingRoute = Some hash; Screen = ConnectScreen }, []
    match Routing.parse hash with
    | Routing.RConnect -> { m with Screen = ConnectScreen }, []
    | Routing.RList -> if m.Catalog.IsSome then { m with Screen = ListScreen }, [] else waitFor ()
    | Routing.RNew -> if m.Write.IsSome then { m with Screen = CreateScreen } , [] elif m.Catalog.IsSome then { m with Screen = ListScreen }, [] else waitFor ()
    | Routing.RInvestigation(s, dir) ->
        match m.Opened, headOfSource m s with
        | Some o, _ when o.Source = s && o.DirName = dir -> { m with Screen = DetailScreen }, []
        | _, Some head -> openAt m s dir head
        | _ when m.Catalog.IsSome && not m.Pending.IsEmpty -> { m with PendingRoute = Some hash }, []
        | _ -> waitFor ()

let update (m: Model) (msg: Msg) : Model * Effects =
    match msg with
    | Clock now -> { m with Now = Some now }, []
    | Navigated hash -> navigate m hash
    | Field(name, value) -> { m with Form = setField m.Form name value }, []
    | Connect ->
        match RepoRef.parse m.Form.CatalogRepo m.Form.CatalogBranch with
        | None -> fail m "Enter the catalog repository as owner/name and a branch."
        | Some catalog ->
            let write = if String.IsNullOrWhiteSpace m.Form.WriteRepo then None else RepoRef.parse m.Form.WriteRepo m.Form.WriteBranch
            if not (String.IsNullOrWhiteSpace m.Form.WriteRepo) && write.IsNone then fail m "Enter the writable repository as owner/name and a branch."
            elif write.IsSome && not (Writer.validWorkspace m.Form.Workspace) then fail m "The workspace name must match ^[a-z0-9][a-z0-9-]{0,63}$."
            else
                let m = { initial with Form = m.Form; Now = m.Now; NextId = m.NextId; Catalog = Some catalog; Write = write; Status = "Connecting…"; PendingRoute = m.PendingRoute }
                let reqs = [ CatalogHead, fresh m (branchHead m.Form.Token catalog) ] @ (match write with Some w -> [ WriteHead, fresh m (branchHead m.Form.Token w) ] | None -> [])
                requests m reqs
    | Disconnect -> { initial with Now = m.Now }, []
    | Back -> { m with Screen = (if m.Catalog.IsSome then ListScreen else ConnectScreen); Problem = None }, []
    | ShowCreate -> { m with Screen = CreateScreen; Problem = None; Confirmed = None }, []
    | Open key ->
        let s, dir = if key.StartsWith "w:" then WorkingCopy, key.Substring 2 else Catalog, (if key.StartsWith "c:" then key.Substring 2 else key)
        match headOfSource m s with
        | Some head -> openAt { m with Confirmed = None } s dir head
        | None -> fail m "The branch head is not known yet."
    | CreateSubmit ->
        match m.Now with
        | None -> fail m "The browser clock has not been reported yet."
        | Some now ->
            let dir = Writer.newInvestigationId m.Form.Workspace m.Form.NewTitle now
            match Uuid.tryParse dir with
            | None -> fail m "Could not derive an investigation id."
            | Some id ->
                match Writer.eventsOf id 1L now (Writer.Start(m.Form.NewTitle, m.Form.NewObservation)) with
                | Error e -> fail m e
                | Ok lines -> commitFiles m dir lines $"causa: create {dir}"
    | AddObservation | AddHypothesis ->
        match m.Opened with
        | Some o when o.Source = WorkingCopy && (match o.Result with StructurallyVerified _ -> true | _ -> false) ->
            let cmd, text = if msg = AddObservation then Writer.RecordObservation m.Form.EditObservation, m.Form.EditObservation else Writer.RecordHypothesis m.Form.EditHypothesis, m.Form.EditHypothesis
            if Text.tryCreate text |> Option.isNone then fail m "Enter some text first."
            else
                let form = if msg = AddObservation then { m.Form with EditObservation = "" } else { m.Form with EditHypothesis = "" }
                { m with Opened = Some { o with Unsaved = o.Unsaved @ [ cmd ] }; Form = form; Problem = None; Confirmed = None }, []
        | Some o when o.Source = Catalog -> fail m "Historical catalog investigations are read-only (UI-006-O6)."
        | _ -> fail m "Open a working-copy investigation first."
    | Save ->
        match m.Opened, m.Now with
        | Some o, Some now when o.Source = WorkingCopy && not o.Unsaved.IsEmpty ->
            match Uuid.tryParse o.DirName with
            | None -> fail m "Invalid investigation id."
            | Some id ->
                let existing = Writer.linesOf o.Files
                let appended =
                    o.Unsaved
                    |> List.fold
                        (fun acc cmd ->
                            acc
                            |> Result.bind (fun (lines: string list) ->
                                Writer.eventsOf id (int64 (List.length lines + 1)) now cmd |> Result.map (fun l -> lines @ l)))
                        (Ok existing)
                match appended with
                | Error e -> fail m e
                | Ok lines -> commitFiles m o.DirName lines $"causa: {o.DirName} v{existing.Length} -> v{lines.Length}"
        | Some o, _ when o.Unsaved.IsEmpty -> fail m "Nothing to save."
        | _, None -> fail m "The browser clock has not been reported yet."
        | _ -> fail m "Open a working-copy investigation first."
    | Reload ->
        match m.Opened with
        | Some o ->
            match repoOf m o.Source with
            | Some repo ->
                // Independent reload: fetch a fresh head, then read the files at that commit.
                request { m with Confirmed = None } (ReadFile(o.Source, o.DirName, "HEAD?", "")) (fresh m (branchHead m.Form.Token repo))
            | None -> fail m "Not connected."
        | None -> fail m "Nothing is open."
    | Http(id, outcome) ->
        match m.Pending.TryFind id with
        | None -> m, []
        | Some purpose ->
            let m = { m with Pending = m.Pending.Remove id }
            match purpose, outcome with
            // Reload's head read: the commit is learned first, then the three files are read at it.
            | ReadFile(s, dir, "HEAD?", ""), Success(st, body) when st >= 200 && st < 300 ->
                match headOf body with
                | Ok head ->
                    let m = match s with Catalog -> { m with CatalogHead = Some head } | WorkingCopy -> { m with WriteHead = Some head }
                    openAt m s dir head
                | Error e -> fail m e
            | ReadFile(_, _, "HEAD?", ""), _ -> fail m "Reload: the branch head could not be read."
            | _ ->
                let m, fx = onAnswer m purpose outcome
                // A deep link waits for the connection; apply it once every connect read has answered.
                match m.PendingRoute with
                | Some hash when m.Pending.IsEmpty && m.Catalog.IsSome -> let m2, fx2 = navigate m hash in m2, fx @ fx2
                | _ -> m, fx
