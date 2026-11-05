/// Engine tests through the real Limen boundary (Dispatch.step, JSON in and out), against FakeGitHub seeded with the
/// actual causa-data catalog bytes. They prove the engine's logic; they are NOT real-provider acceptance evidence.
///   dotnet run --project tests/Engine.Tests -- <causa-data checkout>
module Program

open System
open System.IO
open Causa.HistoricalCompatibility
open Causa.Preview
open FakeGitHub

let mutable private failures = 0
let check name ok detail =
    if not ok then failures <- failures + 1
    printfn "%s %s%s" (if ok then "PASS" else "FAIL") name (if ok then "" else " — " + detail)

type Session = { Model: Model.Model; World: World; View: (string * JValue) list; Raw: string; Now: Timestamp; Pushed: string list; Real: bool }

let private send (s: Session) (message: JValue) : Session * JValue list =
    let m, out = Dispatch.step s.Now s.Model (Json.canonical message)
    match Json.parse out with
    | Ok(JObj o) ->
        let view = match Json.tryField "view" o with Some(JObj v) -> v | _ -> []
        let effects = match Json.tryField "effects" o with Some(JArr e) -> e | _ -> []
        { s with Model = m; View = view; Raw = out }, effects
    | _ -> failwith "engine answered non-JSON"

/// Sends one message, then answers every effect from the fake world until the engine is quiet.
let rec private settle (s: Session) (effects: JValue list) : Session =
    match effects with
    | [] -> s
    | JObj e :: rest when Json.tryField "kind" e = Some(JStr "Navigation") ->
        let url = match Json.tryField "url" e with Some(JStr u) -> u | _ -> ""
        settle { s with Pushed = s.Pushed @ [ url ] } rest
    | JObj e :: rest ->
        let id = match Json.tryField "correlationId" e with Some(JStr x) -> x | _ -> ""
        let w, outcome = if s.Real then s.World, RealGitHub.answer e else answer s.World e
        let s', more = send { s with World = w } (JObj [ "kind", JStr "EffectResult"; "result", JObj [ "kind", JStr "HttpResult"; "correlationId", JStr id; "outcome", outcome ] ])
        settle s' (rest @ more)
    | _ :: rest -> settle s rest

let private dispatch (s: Session) (message: JValue) = let s', fx = send s message in settle s' fx
let private event name = JObj [ "kind", JStr "Event"; "event", JObj [ "kind", JStr "Event"; "name", JStr name ] ]
let private eventKey name key = JObj [ "kind", JStr "Event"; "event", JObj [ "kind", JStr "Event"; "name", JStr name; "key", JStr key ] ]
let private setField name value = JObj [ "kind", JStr "Event"; "event", JObj [ "kind", JStr "Event"; "name", JStr ("set:" + name); "value", JStr value ] ]
/// Sets the clock the boundary supplies with the next user events (Dispatch.step's `now`).
let private at (iso: string) (s: Session) = { s with Now = (Timestamp.tryParse iso).Value }

let private v (s: Session) k = match Json.tryField k s.View with Some(JStr x) -> x | Some(JBool b) -> string b | Some(JInt n) -> string n | Some x -> Json.canonical x | None -> ""
let private items (s: Session) k = match Json.tryField k s.View with Some(JArr xs) -> xs |> List.choose (function JObj o -> Some o | _ -> None) | _ -> []
let private itemField k (o: (string * JValue) list) = match Json.tryField k o with Some(JStr x) -> x | _ -> ""

let private catalogTree (root: string) : Map<string, byte[]> =
    let files =
        [ "catalog/active.json"; "catalog/demonstration-v1/manifest.json" ]
        @ (Directory.GetDirectories(Path.Combine(root, "workspaces/demo/demonstration-v1/investigations"))
           |> Array.toList
           |> List.collect (fun d -> [ "manifest.json"; "events.ndjson"; "snapshot.json" ] |> List.map (fun f -> Path.GetRelativePath(root, Path.Combine(d, f)).Replace('\\', '/'))))
    files |> List.map (fun f -> f, File.ReadAllBytes(Path.Combine(root, f))) |> Map.ofList

let private connected (root: string) (writable: bool) =
    let world = seed [ "kemiller2002/causa-data", catalogTree root; "acme/causa-work", Map.ofList [ "README.md", "work\n"B ] ]
    let s = { Model = Model.initial; World = world; View = []; Raw = ""; Now = (Timestamp.tryParse "2026-11-05T15:00:00.000Z").Value; Pushed = []; Real = false }
    let s = at "2026-11-05T15:00:00.000Z" s
    let s = dispatch s (setField "token" "test-token-not-real")
    let s = if writable then dispatch (dispatch (dispatch s (setField "writeRepo" "acme/causa-work")) (setField "writeBranch" "main")) (setField "workspace" "preview-test") else s
    dispatch s (event "connect")

/// Real-provider acceptance: the same engine and boundary against api.github.com (via `gh`), writing only under
/// workspaces/demo/<workspace>/ in the named repository. Usage: <causa-data checkout> --real <owner/repo> <workspace>
let realProvider (repo: string) (workspace: string) =
    let gh (args: string list) =
        let info = Diagnostics.ProcessStartInfo("gh", RedirectStandardOutput = true, UseShellExecute = false)
        args |> List.iter info.ArgumentList.Add
        use p = Diagnostics.Process.Start info
        let out = p.StandardOutput.ReadToEnd()
        p.WaitForExit()
        out.Trim()
    let head () = gh [ "api"; $"repos/{repo}/branches/main"; "--jq"; ".commit.sha" ]
    let now () = (Timestamp.tryParse (DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", Globalization.CultureInfo.InvariantCulture))).Value
    let fresh () =
        let s = { Model = Model.initial; World = seed []; View = []; Raw = ""; Now = now (); Pushed = []; Real = true }
        let s = dispatch (dispatch (dispatch s (setField "writeRepo" repo)) (setField "writeBranch" "main")) (setField "workspace" workspace)
        dispatch s (event "connect")
    let s = fresh ()
    check "real: the catalog lists the four cases from kemiller2002/causa-data" ((items s "catalogItems").Length = 4) (v s "problem")
    let before = head ()
    let s = dispatch (dispatch (dispatch s (event "show-create")) (setField "newTitle" $"Preview acceptance {workspace}")) (setField "newObservation" "Created by the real-provider acceptance run of the Causa preview.")
    let s = dispatch { s with Now = now () } (event "create")
    check "real: create is confirmed by a byte-identical read-back from GitHub" ((v s "confirmed").Contains "read back byte-identical") (v s "problem")
    check "real: the branch head moved by exactly the save" (head () <> before) "head unchanged"
    let dir = s.Model.Opened |> Option.map (fun o -> o.DirName) |> Option.defaultValue ""
    let s = dispatch (dispatch s (setField "editObservation" "A second observation, saved separately.")) (event "add-observation")
    let s = dispatch (dispatch s (setField "editHypothesis" "The preview persists through one conditional commit per save.")) (event "add-hypothesis")
    let s = dispatch { s with Now = now () } (event "save")
    check "real: the edit is confirmed by read-back" ((v s "confirmed").Contains "read back byte-identical") (v s "problem")
    // Independent reload: a new session, no engine memory.
    let r = fresh ()
    check "real: a fresh session lists the stored investigation" (items r "workingItems" |> List.exists (fun i -> itemField "id" i = dir)) dir
    let r = dispatch r (eventKey "open" ("w:" + dir))
    check "real: the fresh session reads 2 observations and 1 hypothesis" ((items r "observations").Length = 2 && (items r "hypotheses").Length = 1) (v r "dDecoded")
    // Independent of the engine: read the stored files with gh and run the full reader on them.
    let raw f = Convert.FromBase64String(gh [ "api"; $"repos/{repo}/contents/workspaces/demo/{workspace}/investigations/{dir}/{f}?ref=main"; "--jq"; ".content" ] |> fun s -> s.Replace("\\n", "").Replace("\n", ""))
    let files = { DirectoryName = dir; Entries = StorageReader.fileNames |> List.map (fun f -> f, raw f) }
    check "real: the stored file set passes the full reader independently (stages 1-14, 16, 17)" (match HistoricalLoader.load files with StructurallyVerified h -> h.State.Version = 3L | _ -> false) (match HistoricalLoader.load files with LoadRejected(e, _) -> ReaderError.describe e | _ -> "")
    // Conflict: another writer moves the branch; the stale session's save is refused and writes nothing.
    let s = dispatch (dispatch s (setField "editObservation" "This save must be refused.")) (event "add-observation")
    let other = fresh ()
    let other = dispatch other (eventKey "open" ("w:" + dir))
    let other = dispatch (dispatch other (setField "editObservation" "Another writer saves first.")) (event "add-observation")
    let other = dispatch { other with Now = now () } (event "save")
    check "real: the other writer's save is confirmed" ((v other "confirmed").Contains "read back byte-identical") (v other "problem")
    let moved = head ()
    let s = dispatch { s with Now = now () } (event "save")
    check "real: a save against the moved head is refused by GitHub (expectedHeadOid)" ((v s "problem").StartsWith "Save refused") (v s "problem")
    check "real: the refused save wrote nothing" (head () = moved) "head changed"
    check "real: the unsaved change is kept" (v s "unsavedCount" = "1") (v s "unsavedCount")
    printfn "real-provider workspace: %s/workspaces/demo/%s/investigations/%s" repo workspace dir

[<EntryPoint>]
let main argv =
    match argv with
    | [| _; "--real"; repo; workspace |] ->
        realProvider repo workspace
        printfn "%s" (if failures = 0 then "all real-provider checks passed" else $"{failures} real-provider check(s) FAILED")
        (if failures = 0 then 0 else 1)
    | _ ->
    let root = match argv with [| r |] -> r | _ -> failwith "usage: <causa-data checkout> [--real <owner/repo> <workspace>]"

    // ---- read path against the real catalog bytes
    let s = connected root false
    check "connect lists the four catalog cases" ((items s "catalogItems").Length = 4) (v s "status")
    let apollo = items s "catalogItems" |> List.find (fun i -> (itemField "title" i).StartsWith "Apollo 1 ")
    let s = dispatch s (eventKey "open" (itemField "key" apollo))
    check "open Apollo 1: 432 of 432 decoded" ((v s "dDecoded").StartsWith "432 of 432") (v s "dDecoded")
    check "open Apollo 1: catalog digest matches" ((v s "dCatalogMatch").StartsWith "Replayed state equals") (v s "dCatalogMatch")
    check "historical case is read-only" (v s "dWritable" = "False") (v s "dWritable")
    let s2 = dispatch (dispatch s (setField "editObservation" "tamper")) (event "add-observation")
    check "editing a catalog case is refused" ((v s2 "problem").Contains "read-only") (v s2 "problem")
    check "the token never appears in the view" (not (s2.Raw.Contains "test-token-not-real")) "token leaked into the view"

    // ---- create, save, read back, reload
    let s = connected root true
    let s = dispatch s (event "show-create")
    let s = dispatch (dispatch s (setField "newTitle" "Kitchen breaker trips")) (setField "newObservation" "The kitchen breaker trips when the kettle and toaster run together.")
    let headBefore = headOf s.World "acme/causa-work"
    let s = dispatch (at "2026-11-05T15:01:00.000Z" s) (event "create")
    let headAfter = headOf s.World "acme/causa-work"
    check "create commits once to the writable repository" (headAfter <> headBefore) (v s "problem")
    check "create is confirmed by a byte-identical read-back" ((v s "confirmed").Contains "read back byte-identical") (v s "problem")
    let dir = s.Model.Opened |> Option.map (fun o -> o.DirName) |> Option.defaultValue ""
    let path f = $"workspaces/demo/preview-test/investigations/{dir}/{f}"
    check "the three files exist only under the working-copy prefix" (StorageReader.fileNames |> List.forall (fun f -> (fileOf s.World "acme/causa-work" (path f)).IsSome)) dir
    check "the catalog repository is unchanged" (headOf s.World "kemiller2002/causa-data" = "c0") "catalog head moved"
    let stored = StorageReader.fileNames |> List.map (fun f -> f, (fileOf s.World "acme/causa-work" (path f)).Value)
    check "the stored file set passes the full reader independently" (match HistoricalLoader.load { DirectoryName = dir; Entries = stored } with StructurallyVerified _ -> true | _ -> false) "stored set refused"
    check "the created investigation carries the title" ((utf8 (snd (stored |> List.find (fst >> (=) "events.ndjson")))).Contains "Kitchen breaker trips") "title missing"

    let s = dispatch (dispatch s (setField "editObservation" "It also trips with only the kettle after 10 minutes.")) (event "add-observation")
    let s = dispatch (dispatch s (setField "editHypothesis" "The kettle draws more current than its rating.")) (event "add-hypothesis")
    check "edits stay unsaved until Save" (v s "unsavedCount" = "2") (v s "unsavedCount")
    let s = dispatch (at "2026-11-05T15:02:00.000Z" s) (event "save")
    check "save is confirmed by read-back" ((v s "confirmed").Contains "read back byte-identical") (v s "problem")
    check "after save the history has 3 events" (v s "dVersion" = "3") (v s "dVersion")

    // Independent reload: a fresh engine (no in-memory state) connects and opens the stored investigation.
    let fresh = connected root true
    let fresh = { fresh with World = s.World }
    let fresh = dispatch fresh (event "connect")
    let working = items fresh "workingItems"
    check "a fresh session lists the stored working copy" (working |> List.exists (fun i -> itemField "id" i = dir)) (sprintf "%A" (working |> List.map (itemField "id")))
    let fresh = dispatch fresh (eventKey "open" ("w:" + dir))
    check "a fresh session reads 2 observations and 1 hypothesis back" ((items fresh "observations").Length = 2 && (items fresh "hypotheses").Length = 1) (v fresh "dDecoded")
    check "the fresh read equals what was saved" (fresh.Model.Opened |> Option.exists (fun o -> o.Files.Entries |> List.forall (fun (f, b) -> stored |> List.exists (fun (g, _) -> g = f) && b = (fileOf s.World "acme/causa-work" (path f)).Value))) "differs"

    // ---- negative: concurrent writer moves the branch; the save is refused and nothing is lost
    let s3 = dispatch (dispatch s (setField "editObservation" "A third observation.")) (event "add-observation")
    let s3 = { s3 with World = externalCommit "acme/causa-work" "other.txt" "x"B s3.World }
    let headMoved = headOf s3.World "acme/causa-work"
    let s3 = dispatch s3 (event "save")
    check "a save against a moved head is refused (expectedHeadOid)" ((v s3 "problem").StartsWith "Save refused") (v s3 "problem")
    check "nothing was written by the refused save" (headOf s3.World "acme/causa-work" = headMoved) "head changed"
    check "the unsaved change is kept" (v s3 "unsavedCount" = "1") (v s3 "unsavedCount")

    // ---- negative: unknown outcome is never success
    let s4 = { s3 with World = { s3.World with UnknownCommitOutcome = true } }
    let s4 = dispatch s4 (event "reload")
    let s4 = dispatch (dispatch s4 (setField "editObservation" "Fourth.")) (event "add-observation")
    let s4 = dispatch s4 (event "save")
    check "an unknown commit outcome is not reported as saved" ((v s4 "problem").Contains "unknown" && v s4 "hasConfirmed" = "False") (v s4 "problem")

    // ---- negative: a read-back that differs is not confirmed
    let s5 = connected root true
    let s5 = { s5 with World = { s5.World with CorruptReadBack = true } }
    let s5 = dispatch (dispatch (dispatch s5 (event "show-create")) (setField "newTitle" "Read-back probe")) (setField "newObservation" "Probe.")
    let s5 = dispatch s5 (event "create")
    check "a read-back that differs is NOT confirmed" (v s5 "hasConfirmed" = "False" && (v s5 "problem").Contains "NOT confirmed") (v s5 "problem")

    // ---- negative: invalid input
    let s6 = connected root true
    let s6 = dispatch (dispatch (dispatch s6 (event "show-create")) (setField "newTitle" "   ")) (setField "newObservation" "x")
    let s6 = dispatch s6 (event "create")
    check "a blank title is refused before any write" ((v s6 "problem").Contains "title" && headOf s6.World "acme/causa-work" = "c0") (v s6 "problem")
    let s7 = { Model = Model.initial; World = seed []; View = []; Raw = ""; Now = (Timestamp.tryParse "2026-11-05T15:00:00.000Z").Value; Pushed = []; Real = false }
    let s7 = dispatch (dispatch (dispatch (dispatch s7 (setField "writeRepo" "acme/x")) (setField "workspace" "../../fixtures")) (event "connect")) (event "connect")
    check "a workspace name outside ^[a-z0-9][a-z0-9-]{0,63}$ is refused" ((v s7 "problem").Contains "workspace") (v s7 "problem")

    // ---- routing (F#): the URL follows the model; Back and deep links drive the model
    let r = connected root false
    let apolloKey = items r "catalogItems" |> List.find (fun i -> (itemField "title" i).StartsWith "Apollo 1 ") |> itemField "key"
    let apolloId = apolloKey.Substring 2
    let r = dispatch r (eventKey "open" apolloKey)
    check "opening a case pushes its hash route" (List.tryLast r.Pushed = Some $"#/investigations/catalog/{apolloId}") (sprintf "%A" r.Pushed)
    let pushedBefore = r.Pushed.Length
    let loc hash = JObj [ "kind", JStr "LocationChanged"; "location", JObj [ "path", JStr "/"; "query", JStr ""; "hash", JStr hash ] ]
    let r = dispatch r (loc "#/investigations")
    check "Back to #/investigations shows the list" (v r "screenList" = "True") (v r "screenList")
    check "a browser move is never pushed again" (r.Pushed.Length = pushedBefore) (sprintf "%A" r.Pushed)
    let r = dispatch r (loc $"#/investigations/catalog/{apolloId}")
    check "Forward to a case shows it again" (v r "screenDetail" = "True" && (v r "dDecoded").StartsWith "432 of 432") (v r "dDecoded")
    let d = { Model = Model.initial; World = seed [ "kemiller2002/causa-data", catalogTree root ]; View = []; Raw = ""; Now = (Timestamp.tryParse "2026-11-05T15:00:00.000Z").Value; Pushed = []; Real = false }
    let init hash = JObj [ "kind", JStr "Initialize"; "protocolVersion", JInt 1L; "capabilities", JArr [ JStr "Http"; JStr "Navigation" ]; "location", JObj [ "path", JStr "/"; "query", JStr ""; "hash", JStr hash ] ]
    let d = dispatch d (init $"#/investigations/catalog/{apolloId}")
    check "a deep link before Connect waits on the Connect screen" (v d "screenConnect" = "True") (v d "screenConnect")
    let d = dispatch d (event "connect")
    check "after Connect the deep-linked case opens" (v d "screenDetail" = "True" && (v d "dDecoded").StartsWith "432 of 432") (v d "dDecoded")

    // CI-CADENCE-001: pure validation tests (owner: domain agent).
    failures <- failures + ValidationTests.run ()
    printfn "%s" (if failures = 0 then "all engine tests passed" else $"{failures} engine test(s) FAILED")
    if failures = 0 then 0 else 1
