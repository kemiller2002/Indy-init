/// The verification checks. Each check reads the causa-data checkout, never a fixture this suite generated, and
/// returns machine-readable evidence. Mutations (C06, C08, C17) start from real historical lines.
module Checks

open System
open System.IO
open System.Text
open Causa.HistoricalCompatibility
open Source

type CheckResult =
    { Id: string
      MissionCheck: string
      Title: string
      Failures: string list
      Evidence: (string * JValue) list }

let private jstr (s: string) = JStr s
let private jint (n: int) = JInt(int64 n)
let private jlist f xs = JArr(xs |> List.map f)

let check id missionCheck title (failures: string list) evidence =
    { Id = id; MissionCheck = missionCheck; Title = title; Failures = failures; Evidence = evidence }

let decodedOf (files: InvestigationFiles) : DecodedEvent list =
    match StorageReader.outcomes files with
    | Ok outs -> outs |> List.choose (function Decoded d -> Some d | _ -> None)
    | Error _ -> []

type Loaded =
    { Case: Case
      Files: InvestigationFiles
      Result: LoadResult }

let loadCase (c: Case) = let f = readInvestigation c.Path in { Case = c; Files = f; Result = HistoricalLoader.load f }

let verified (l: Loaded) =
    match l.Result with
    | StructurallyVerified h -> Some h
    | LoadRejected _ -> None

let describeResult =
    function
    | StructurallyVerified _ -> "structurally verified; admissibility not evaluated"
    | LoadRejected(e, c) -> ReaderError.describe e + (if c then " (conditional on admissibility)" else "")

// ---------------------------------------------------------------- C00 source pin

let sourcePin (pinPath: string) (cases: Case list) =
    let pin = readJsonFile pinPath
    let pinned = objs "cases" pin |> List.map (fun c -> field "caseKey" c, field "eventsSha256" c)
    let failures =
        pinned
        |> List.choose (fun (k, sha) ->
            match cases |> List.tryFind (fun c -> c.CaseKey = k) with
            | None -> Some $"case {k} is missing from catalog/historical-v1/manifest.json"
            | Some c ->
                let actual = Sha256.ofBytes (File.ReadAllBytes(Path.Combine(c.Path, "events.ndjson")))
                if actual <> sha then Some $"{k}: events.ndjson sha256 {actual} differs from the pinned {sha}" else None)
    check "C00" "-" "The causa-data checkout holds the pinned historical bytes" failures [ "pinned", jlist (fun (k, s) -> JObj [ "caseKey", jstr k; "eventsSha256", jstr s ]) pinned ]

// ---------------------------------------------------------------- C01..C05

let allLoad (loaded: Loaded list) =
    let failures = loaded |> List.choose (fun l -> match l.Result with LoadRejected _ -> Some $"{l.Case.CaseKey}: {describeResult l.Result}" | _ -> None)
    check "C01" "3" "Every historical investigation loads (stages 1-14, 16, 17)" failures
        [ "results", jlist (fun l -> JObj [ "caseKey", jstr l.Case.CaseKey; "result", jstr (describeResult l.Result) ]) loaded ]

let accounted (pinPath: string) (loaded: Loaded list) =
    let reported = match readJsonFile pinPath with JObj o -> (match Json.tryField "reportedCounts" o with Some(JObj r) -> r | _ -> []) | _ -> []
    let rows =
        loaded
        |> List.map (fun l ->
            let lineCount = List.length (lines l.Files)
            let outcomes = match StorageReader.outcomes l.Files with Ok o -> o | Error _ -> []
            let decoded = outcomes |> List.sumBy (function Decoded _ -> 1 | _ -> 0)
            let unregistered = outcomes |> List.sumBy (function Unregistered _ -> 1 | _ -> 0)
            let rejected = outcomes |> List.sumBy (function Rejected _ -> 1 | _ -> 0)
            let reportedCount = match Json.tryField l.Case.CaseKey reported with Some(JInt n) -> Some n | _ -> None
            let manifestCount = match verified l with Some h -> h.Read.Manifest.EventCount | None -> -1L
            l.Case, lineCount, decoded, unregistered, rejected, reportedCount, manifestCount)
    let failures =
        rows
        |> List.collect (fun (c, n, d, u, r, rep, m) ->
            [ if d + u + r <> n then $"{c.CaseKey}: {n} lines but {d + u + r} outcomes"
              if d <> n then $"{c.CaseKey}: {n - d} lines not typedly decoded (unregistered {u}, rejected {r})"
              if int64 n <> c.ExpectedVersion then $"{c.CaseKey}: {n} lines, catalog expects {c.ExpectedVersion}"
              if int64 n <> m then $"{c.CaseKey}: {n} lines, manifest eventCount {m}"
              match rep with
              | Some x when x <> int64 n -> $"{c.CaseKey}: reported {x}, actual {n}"
              | _ -> () ])
    check "C02" "1, 11" "Every historical event is accounted for: lines = outcomes = decoded = manifest = catalog" failures
        [ "cases",
          rows
          |> jlist (fun (c, n, d, u, r, rep, m) ->
              JObj
                  [ "caseKey", jstr c.CaseKey; "lines", jint n; "decoded", jint d; "unregistered", jint u; "rejected", jint r; "manifestEventCount", JInt m
                    "catalogExpectedVersion", JInt c.ExpectedVersion; "reportedByPriorReport", (rep |> Option.map JInt |> Option.defaultValue JNull) ])
          "totalEvents", jint (rows |> List.sumBy (fun (_, n, _, _, _, _, _) -> n)) ]

let orderPreserved (loaded: Loaded list) =
    let failures =
        loaded
        |> List.collect (fun l ->
            let raw = lines l.Files
            let ds = decodedOf l.Files
            (List.zip raw ds |> List.mapi (fun i (r, d) -> i, r, d))
            |> List.choose (fun (i, r, d) ->
                if d.Envelope.Line <> i + 1 then Some $"{l.Case.CaseKey}: decoded #{i + 1} came from line {d.Envelope.Line}"
                elif d.Envelope.AggregateVersion <> int64 (i + 1) then Some $"{l.Case.CaseKey}: line {i + 1} has aggregateVersion {d.Envelope.AggregateVersion}"
                elif d.Envelope.RawLine <> r then Some $"{l.Case.CaseKey}: line {i + 1} raw text not preserved"
                else None))
    check "C04" "4" "Event order is preserved: decoded i = line i = aggregateVersion i, raw line kept" failures []

let identities (loaded: Loaded list) =
    let failures =
        loaded
        |> List.collect (fun l ->
            let ds = decodedOf l.Files
            let ids = ds |> List.map (fun d -> d.Envelope.EventId)
            [ if List.length (List.distinct ids) <> List.length ids then $"{l.Case.CaseKey}: duplicate event ids"
              yield!
                  ds
                  |> List.choose (fun d ->
                      let e = d.Envelope
                      if Uuid.deriveEventId e.InvestigationId e.AggregateVersion e.CorrelationId <> e.EventId then Some $"{l.Case.CaseKey}: line {e.Line} id not derived"
                      elif e.InvestigationId.Text <> l.Case.InvestigationId then Some $"{l.Case.CaseKey}: line {e.Line} names {e.InvestigationId}"
                      else None) ])
    let groups = loaded |> List.map (fun l -> l.Case.CaseKey, List.length (StorageReader.commandGroups (decodedOf l.Files)))
    check "C05" "5" "Event and investigation identities are preserved and the §5.2 derivation holds for every event" failures
        [ "eventNamespace", jstr Uuid.eventNamespace.Text; "commandGroups", JObj(groups |> List.map (fun (k, n) -> k, jint n)) ]

// ---------------------------------------------------------------- C06 required fields, C08 unknown types

/// Mutates the payload of one real line and reports the outcome of the mutated line.
let private mutateOutcome (raw: string) (f: (string * JValue) list -> (string * JValue) list) : LineOutcome =
    match Json.parse raw with
    | Ok(JObj o) ->
        let payload = match Json.tryField "payload" o with Some(JObj p) -> p | _ -> []
        let o' = o |> List.map (fun (k, v) -> if k = "payload" then k, JObj(f payload) else k, v)
        let text = Json.canonical (JObj o')
        match Json.parse text with
        | Ok(JObj m) ->
            match Envelope.decode 1 text m with
            | Error d -> Rejected(ReaderError.create SchemaViolation "events.ndjson" (Some 1) d)
            | Ok env ->
                match EventDecoder.decodePayload env.EventType env.Payload with
                | Ok e -> Decoded { Envelope = env; Event = e }
                | Error d -> Rejected(ReaderError.create SchemaViolation "events.ndjson" (Some 1) d)
        | _ -> failwith "unreachable"
    | _ -> failwith "not an object"

let requiredFields (loaded: Loaded list) =
    let firstPerType =
        loaded
        |> List.collect (fun l -> decodedOf l.Files)
        |> List.distinctBy (fun d -> d.Envelope.EventType)
    let mutations =
        firstPerType
        |> List.collect (fun d ->
            let raw = d.Envelope.RawLine
            let keys = d.Envelope.Payload |> List.map fst
            let without k = List.filter (fun (n: string, _: JValue) -> n <> k)
            let retyped k = List.map (fun (n: string, v: JValue) -> if n = k then n, JInt 42L else n, v)
            let removed = keys |> List.map (fun k -> $"{d.Envelope.EventType} without '{k}'", mutateOutcome raw (without k))
            let wrongType = keys |> List.map (fun k -> $"{d.Envelope.EventType} with '{k}' as an integer", mutateOutcome raw (retyped k))
            let extra = [ $"{d.Envelope.EventType} with an undeclared field", mutateOutcome raw (fun p -> p @ [ "confidence", JInt 1L ]) ]
            removed @ wrongType @ extra)
    let failures = mutations |> List.choose (fun (name, o) -> match o with Rejected e when e.Code = SchemaViolation -> None | _ -> Some $"{name}: not rejected as SCHEMA_VIOLATION")
    check "C06" "6" "Required fields are validated: every field of every historical event type removed, retyped, and an undeclared field added" failures
        [ "mutations", jint (List.length mutations); "eventTypes", jint (List.length firstPerType) ]

let unknownTypes (apollo1: Loaded) =
    let raw = lines apollo1.Files
    let target = raw |> List.findIndex (fun l -> l.Contains "\"eventType\":\"EvidenceVerificationAssessed\"")
    let renamed = raw |> List.mapi (fun i l -> if i = target then l.Replace("\"eventType\":\"EvidenceVerificationAssessed\"", "\"eventType\":\"EvidenceVerificationAssessedV2\"") else l)
    let files = withLines renamed apollo1.Files
    let outcome = match StorageReader.outcomes files with Ok o -> List.item target o | Error e -> Rejected e
    let readResult = HistoricalLoader.load files
    let failures =
        [ match outcome with
          | Unregistered u when u.RawLine = List.item target renamed && u.EventType = "EvidenceVerificationAssessedV2" -> ()
          | _ -> "the renamed line was not returned as a lossless Unregistered outcome"
          match readResult with
          | LoadRejected(e, _) when e.Code = UnknownEventType -> ()
          | r -> $"history not refused with UNKNOWN_EVENT_TYPE: {describeResult r}"
          match StorageReader.outcomes files with
          | Ok o when List.length o = List.length raw -> ()
          | _ -> "a line disappeared from the outcomes" ]
    check "C08" "8" "Unknown event types are never silently ignored: kept whole, typed as Unregistered, history refused" failures
        [ "mutatedLine", jint (target + 1); "result", jstr (describeResult readResult) ]

// ---------------------------------------------------------------- C07 negative fixtures (independent oracle)

let negatives (root: string) =
    let fixtures = negativeFixtures root
    let baseEvents key =
        fixtureCases root |> List.tryFind (fun c -> c.CaseKey = key) |> Option.map (fun c -> File.ReadAllBytes(Path.Combine(c.Path, "events.ndjson")))
    let stage15 = ReaderCode.code HistoryInadmissible
    let rows =
        fixtures
        |> List.map (fun f ->
            match f.Kind, f.Files with
            | "append", _ -> f, "not-applicable", "writer-side check (CAUSA.VERSION_STALE); this adapter is read-only"
            | "successor", Some files0 ->
                let baseId = f.BaseScenarioKey |> Option.bind (fun k -> fixtureCases root |> List.tryFind (fun c -> c.CaseKey = k)) |> Option.map (fun c -> c.InvestigationId)
                let files = { files0 with DirectoryName = baseId |> Option.defaultValue files0.DirectoryName }
                let r =
                    match StorageReader.read files, f.BaseScenarioKey |> Option.bind baseEvents with
                    | Ok read, Some accepted ->
                        match HistoricalLoader.verify read with
                        | StructurallyVerified _ ->
                            match StorageReader.checkSuccessor accepted read with
                            | Ok() -> "accepted"
                            | Error e -> ReaderCode.code e.Code
                        | LoadRejected(e, _) -> ReaderCode.code e.Code
                    | Error e, _ -> ReaderCode.code e.Code
                    | _, None -> "base-missing"
                f, (if r = f.ExpectedCode then "agree" else "disagree"), r
            | _, Some files ->
                let r = HistoricalLoader.load files
                let code, conditional = match r with LoadRejected(e, c) -> ReaderCode.code e.Code, c | StructurallyVerified _ -> "structurally-valid-admissibility-not-evaluated", false
                let verdict =
                    if f.ExpectedCode = stage15 then
                        match r with
                        | LoadRejected(e, _) when e.Code = HistoryInadmissible -> "agree-adapter-detected"
                        | StructurallyVerified h when (match h.Admissibility with NotEvaluated _ -> true | _ -> false) -> "not-claimed-valid-admissibility-not-evaluated"
                        | _ -> "disagree"
                    elif code = f.ExpectedCode then (if conditional then "agree-conditional-on-admissibility" else "agree")
                    else "disagree"
                f, verdict, code
            | _, None -> f, "disagree", "fixture has no files")
    let failures = rows |> List.choose (fun (f, v, r) -> if v = "disagree" then Some $"{f.Name}: expected {f.ExpectedCode}, got {r}" else None)
    let count v = rows |> List.filter (fun (_, x, _) -> x = v) |> List.length
    check "C07" "7" "Malformed records are rejected: the 90 causa-data negative fixtures agree on the primary code, or are never reported valid" failures
        [ "fixtures", jint (List.length rows)
          "agree", jint (count "agree")
          "agreeConditionalOnAdmissibility", jint (count "agree-conditional-on-admissibility")
          "stage15AdapterDetected", jint (count "agree-adapter-detected")
          "stage15NotClaimedValid", jint (count "not-claimed-valid-admissibility-not-evaluated")
          "notApplicable", jint (count "not-applicable")
          "disagree", jint (count "disagree")
          "rows", rows |> jlist (fun (f, v, r) -> JObj [ "name", jstr f.Name; "expected", jstr f.ExpectedCode; "expectedDomainCode", (f.ExpectedDomainCode |> Option.map jstr |> Option.defaultValue JNull); "actual", jstr r; "verdict", jstr v ]) ]

// ---------------------------------------------------------------- C03, C09, C10, C11, C12

let discriminators (historical: Loaded list) (fixtures: Loaded list) =
    let types ls = ls |> List.collect (fun l -> decodedOf l.Files |> List.map (fun d -> EventDecoder.discriminator d.Event)) |> Set.ofList
    let hist = types historical
    let fix = types fixtures
    let all = Set.union hist fix
    let missing = Set.difference (Set.ofList EventDecoder.eventTypes) all
    let roundTrip =
        (historical @ fixtures)
        |> List.collect (fun l -> decodedOf l.Files)
        |> List.filter (fun d -> EventEncoder.canonicalLine d <> d.Envelope.RawLine)
        |> List.map (fun d -> $"{d.Envelope.EventType} line {d.Envelope.Line} does not round-trip")
    check "C03" "2" "Every registered discriminator is decoded from real data and round-trips (historical cases + frozen synthetic fixtures)"
        ((missing |> Set.toList |> List.map (fun t -> $"{t} never decoded")) @ roundTrip)
        [ "registered", jint (List.length EventDecoder.eventTypes); "inHistoricalCases", jint hist.Count; "onlyInSyntheticFixtures", jlist jstr (Set.difference fix hist |> Set.toList) ]

let deterministic (loaded: Loaded list) =
    let fingerprint (l: Loaded) =
        let f = readInvestigation l.Case.Path
        let ds = decodedOf f
        let lines = ds |> List.map EventEncoder.canonicalLine
        let state = match HistoricalLoader.load f with StructurallyVerified h -> h.StateSha256 | r -> describeResult r
        Sha256.ofText (String.Join("\n", lines)), state
    let failures =
        loaded |> List.choose (fun l -> let a = fingerprint l in let b = fingerprint l in if a <> b then Some $"{l.Case.CaseKey}: two runs differ" else None)
    check "C09" "9" "Repeated decoding and replay are deterministic (two independent reads from disk)" failures
        [ "fingerprints", loaded |> jlist (fun l -> let d, s = fingerprint l in JObj [ "caseKey", jstr l.Case.CaseKey; "decodedSha256", jstr d; "stateSha256", jstr s ]) ]

let provenance (root: string) (loaded: Loaded list) =
    let rows =
        loaded
        |> List.map (fun l ->
            let p = readJsonFile (Path.Combine(root, "catalog/historical-v1/provenance", l.Case.CaseKey + ".json"))
            let entries = objs "evidence" p
            let admitted =
                decodedOf l.Files
                |> List.choose (fun d -> match d.Event with EvidenceAdmitted e -> Some(e.EvidenceId.Text, (e, d.Envelope)) | _ -> None)
                |> Map.ofList
            let problems =
                entries
                |> List.choose (fun e ->
                    let id = field "evidenceId" e
                    match admitted.TryFind id with
                    | None -> Some $"{l.Case.CaseKey}: provenance evidence {id} not admitted"
                    | Some(ev, _) ->
                        let src = ev.Source.Value
                        let needles = [ field "claimId" e; field "sourceId" e ] @ (match Json.tryField "url" e with Some(JStr u) -> [ u ] | _ -> [])
                        needles |> List.tryFind (fun n -> not (src.Contains n)) |> Option.map (fun n -> $"{l.Case.CaseKey}: evidence {id} source text lacks '{n}'"))
            let unmapped = admitted |> Map.toList |> List.map fst |> List.filter (fun id -> not (entries |> List.exists (fun e -> field "evidenceId" e = id)))
            let actors = decodedOf l.Files |> List.countBy (fun d -> d.Envelope.Actor.ActorId.Value)
            l.Case.CaseKey, List.length entries, admitted.Count, unmapped, problems, actors)
    let failures = rows |> List.collect (fun (k, _, _, unmapped, problems, _) -> problems @ (unmapped |> List.map (fun u -> $"{k}: admitted evidence {u} has no provenance entry")))
    check "C10" "10" "Historical provenance is preserved: every provenance record's evidence is admitted with its claim, source and URL in the stored source text; actors and occurredAt are kept per event" failures
        [ "cases",
          rows
          |> jlist (fun (k, n, a, _, _, actors) ->
              JObj [ "caseKey", jstr k; "provenanceEntries", jint n; "admittedEvidence", jint a; "actors", JObj(actors |> List.map (fun (id, c) -> id, jint c)) ]) ]

let noLoss (loaded: Loaded list) =
    let failures =
        loaded
        |> List.collect (fun l ->
            let ds = decodedOf l.Files
            let rebuilt = Encoding.UTF8.GetBytes(String.Join("\n", ds |> List.map EventEncoder.canonicalLine) + "\n")
            let viaSuccessor =
                ds
                |> List.choose (fun d ->
                    match Successor.SuccessorContract.ofWire d.Event |> Result.bind Successor.SuccessorContract.toWire with
                    | Ok w when EventEncoder.canonicalLine { d with Event = w } = d.Envelope.RawLine -> None
                    | Ok _ -> Some $"{l.Case.CaseKey}: line {d.Envelope.Line} changes through the successor contract"
                    | Error e -> Some $"{l.Case.CaseKey}: line {d.Envelope.Line}: {e}")
            [ if Sha256.ofBytes rebuilt <> l.Case.EventsSha256 then $"{l.Case.CaseKey}: re-encoded events.ndjson does not reproduce the catalog eventsSha256" ] @ viaSuccessor)
    check "C11" "11" "No event or field disappears: re-encoding every typed event reproduces events.ndjson byte for byte, directly and through the proposed v4.1.5 union" failures []

let replayIndependent (historical: Loaded list) (root: string) (fixtures: Loaded list) =
    let golden (c: Case) = Path.Combine(root, "contract/v1.0.0/golden", c.CaseKey + ".state.json")
    let row (l: Loaded) (goldenFile: string option) =
        match l.Result with
        | LoadRejected _ -> l.Case.CaseKey, [ $"{l.Case.CaseKey}: {describeResult l.Result}" ], JNull
        | StructurallyVerified h ->
            let lifecycle = WireEnum.toWire Enums.lifecycle h.State.Lifecycle
            let goldenProblem =
                goldenFile
                |> Option.bind (fun g ->
                    let expected = File.ReadAllText(g).TrimEnd('\n')
                    if expected <> Json.canonical (ReplayAdapter.stateJson h.State) then Some $"{l.Case.CaseKey}: projection differs from {Path.GetFileName g}" else None)
            let problems =
                [ if h.StateSha256 <> l.Case.ExpectedStateSha256 then $"{l.Case.CaseKey}: state digest {h.StateSha256} <> expected {l.Case.ExpectedStateSha256}"
                  if lifecycle <> l.Case.ExpectedLifecycle then $"{l.Case.CaseKey}: lifecycle {lifecycle} <> expected {l.Case.ExpectedLifecycle}"
                  if h.State.Version <> l.Case.ExpectedVersion then $"{l.Case.CaseKey}: version {h.State.Version} <> expected {l.Case.ExpectedVersion}"
                  match goldenProblem with Some p -> p | None -> () ]
            l.Case.CaseKey, problems, JObj [ "caseKey", jstr l.Case.CaseKey; "lifecycle", jstr lifecycle; "version", JInt h.State.Version; "stateSha256", jstr h.StateSha256; "expectedStateSha256", jstr l.Case.ExpectedStateSha256; "independentGolden", (match goldenFile with Some g -> jstr (Path.GetFileName g) | None -> JNull) ]
    let rows = (historical |> List.map (fun l -> row l None)) @ (fixtures |> List.map (fun l -> row l (Some(golden l.Case))))
    check "C12" "12" "Replay is verified independently of parsing: the §8.3 fold reproduces snapshot.json byte for byte and matches the catalog / root-manifest state digests and the golden states"
        (rows |> List.collect (fun (_, p, _) -> p)) [ "cases", rows |> jlist (fun (_, _, e) -> e) ]

// ---------------------------------------------------------------- C13 byte identity, C17 adversarial tampering

let byteIdentity (root: string) (cases: Case list) =
    let copies = [ "workspaces/demo/historical-v1/investigations"; "workspaces/demo/demonstration-v1/investigations"; "templates/historical-v1/investigations"; "templates/demonstration-v1/investigations" ]
    let failures =
        cases
        |> List.collect (fun c ->
            StorageReader.fileNames
            |> List.choose (fun f ->
                let digests = copies |> List.map (fun d -> let p = Path.Combine(root, d, c.InvestigationId, f) in if File.Exists p then Sha256.ofBytes (File.ReadAllBytes p) else "missing")
                if List.length (List.distinct digests) = 1 && List.head digests <> "missing" then None else Some $"{c.CaseKey}/{f}: copies differ {digests}"))
    check "C13" "3" "The three historical cases are byte-identical in historical-v1, demonstration-v1 and both templates" failures [ "locations", jlist jstr copies ]

let tampering (apollo1: Loaded) =
    let raw = lines apollo1.Files
    let manifestText = Encoding.UTF8.GetString(apollo1.Files.Entries |> List.find (fst >> (=) "manifest.json") |> snd)
    let rehash (files: InvestigationFiles) =
        let ev = files.Entries |> List.find (fst >> (=) "events.ndjson") |> snd
        let n = List.length (lines files)
        match Json.parse (manifestText.TrimEnd('\n')) with
        | Ok(JObj m) ->
            let last = Json.parse (List.last (lines files)) |> function Ok(JObj o) -> (match Json.tryField "occurredAt" o with Some v -> v | None -> JNull) | _ -> JNull
            let m' =
                m
                |> List.map (fun (k, v) ->
                    match k with
                    | "eventsSha256" -> k, JStr(Sha256.ofBytes ev)
                    | "eventCount" | "aggregateVersion" -> k, JInt(int64 n)
                    | "updatedAt" -> k, last
                    | _ -> k, v)
            withEntry "manifest.json" (Encoding.UTF8.GetBytes(Json.canonical (JObj m') + "\n")) files
        | _ -> files
    let editText = raw |> List.mapi (fun i l -> if i = 0 then l.Replace("Historical investigation", "Historical inquiry") else l)
    let swapped = raw |> List.mapi (fun i l -> if i = 10 then List.item 11 raw elif i = 11 then List.item 10 raw else l)
    let dropped = raw |> List.take (List.length raw - 1)
    let cases =
        [ "edit one event without rehash", withLines editText apollo1.Files, EventsHashMismatch
          "edit one event and rehash the manifest", rehash (withLines editText apollo1.Files), SnapshotMismatch
          "swap two adjacent events and rehash", rehash (withLines swapped apollo1.Files), SequenceInvalid
          "drop the last event and rehash", rehash (withLines dropped apollo1.Files), SnapshotMismatch ]
    let rows = cases |> List.map (fun (name, files, expected) -> name, expected, HistoricalLoader.load files)
    let failures =
        rows |> List.choose (fun (name, expected, r) -> match r with LoadRejected(e, _) when e.Code = expected -> None | _ -> Some $"{name}: expected {ReaderCode.code expected}, got {describeResult r}")
    check "C17" "11 (adversarial)" "Tampering with real Apollo 1 bytes is detected at the right stage, even when hashes are recomputed" failures
        [ "cases", rows |> jlist (fun (n, e, r) -> JObj [ "mutation", jstr n; "expected", jstr (ReaderCode.code e); "actual", jstr (describeResult r) ]) ]

// ---------------------------------------------------------------- C14, C15 contract compatibility

let frozenCompatibility (loaded: Loaded list) =
    let all = loaded |> List.collect (fun l -> decodedOf l.Files)
    let lossless = all |> List.filter (fun d -> EventCompatibility.isFrozenLossless d.Event) |> List.length
    let lossy = all |> List.filter (fun d -> match (EventCompatibility.entry d.Event).Frozen with FrozenCaseLossy _ -> true | _ -> false) |> List.length
    let none = all |> List.filter (fun d -> match (EventCompatibility.entry d.Event).Frozen with NoFrozenCase _ -> true | _ -> false) |> List.length
    // Not a pass/fail on the data: this records the measured incompatibility. It fails only if the table misses a variant.
    let unmapped = all |> List.filter (fun d -> EventCompatibility.table |> List.exists (fun e -> e.Variant = EventCompatibility.variant d.Event) |> not)
    check "C14" "-" "Measured: how many historical events the frozen v4.1.4 CoreEvent union can represent"
        (unmapped |> List.map (fun d -> $"variant {EventCompatibility.variant d.Event} missing from the compatibility table"))
        [ "events", jint (List.length all); "frozenCaseNoFieldDropped", jint lossless; "frozenCaseFieldsDropped", jint lossy; "noFrozenCase", jint none
          "envelopeFieldsDroppedForEveryEvent", jlist jstr EventCompatibility.frozenEnvelopeDroppedFields ]

let successorContract (loaded: Loaded list) =
    let all = loaded |> List.collect (fun l -> decodedOf l.Files)
    let nameMismatch =
        all
        |> List.choose (fun d ->
            let expected =
                match (EventCompatibility.entry d.Event).Successor with
                | KeepCase c | WidenCase(c, _) | AddCase c -> c
            match Successor.SuccessorContract.ofWire d.Event with
            | Ok s when Successor.SuccessorContract.caseName s = expected -> None
            | Ok s -> Some $"{EventCompatibility.variant d.Event}: table says {expected}, successor gives {Successor.SuccessorContract.caseName s}"
            | Error e -> Some e)
        |> List.distinct
    let tableNames = EventCompatibility.table |> List.map (fun e -> match e.Successor with KeepCase c | WidenCase(c, _) | AddCase c -> c) |> Set.ofList
    let frozenKept = Successor.SuccessorContract.frozenCaseNames |> List.filter (fun n -> n <> "ExtensionAttached" && not (tableNames.Contains n))
    check "C15" "-" "The proposed successor keeps every frozen case name and the compatibility table agrees with it for every event"
        (nameMismatch @ (frozenKept |> List.map (fun n -> $"frozen case {n} is not kept")))
        [ "frozenCases", jint (List.length Successor.SuccessorContract.frozenCaseNames); "widened", jlist jstr Successor.SuccessorContract.widenedFrozenCases ]

let acceptanceLevels (loaded: Loaded list) =
    let label =
        function
        | Achieved -> JObj [ "status", jstr "achieved" ]
        | NotAchieved r -> JObj [ "status", jstr "not-achieved"; "reasons", jlist jstr (r |> List.truncate 5) ]
    let reports = loaded |> List.map (fun l -> l.Case.CaseKey, HistoricalLoader.acceptance l.Files (Some l.Case.ExpectedStateSha256))
    let failures =
        reports
        |> List.collect (fun (k, r) ->
            [ if r.LevelA_RawReadable <> Achieved then $"{k}: level A not achieved"
              if r.LevelB_Decoded <> Achieved then $"{k}: level B not achieved"
              if r.LevelCProjection_SnapshotReproduced <> Achieved then $"{k}: level C-projection not achieved"
              if r.LevelC_Authoritative = Achieved then $"{k}: level C claimed without an admissibility verdict" ])
    check "LV" "8" "Acceptance levels A, B, C reported separately; C is never claimed without admissibility" failures
        [ "cases",
          reports
          |> jlist (fun (k, r) ->
              JObj
                  [ "caseKey", jstr k; "levelA", label r.LevelA_RawReadable; "levelB", label r.LevelB_Decoded
                    "levelCProjection", label r.LevelCProjection_SnapshotReproduced; "levelC", label r.LevelC_Authoritative ]) ]
