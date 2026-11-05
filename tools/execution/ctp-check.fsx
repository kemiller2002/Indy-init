// ctp-check.fsx: execution-readiness checks for CTP-001, contract-triggered parallel execution
// (docs/execution/contract-triggered-parallel/PROTOCOL.md). F# only, no dependencies beyond the .NET SDK.
//
//   dotnet fsi tools/execution/ctp-check.fsx -- registry    --registry FILE
//   dotnet fsi tools/execution/ctp-check.fsx -- lifecycle   --log FILE
//   dotnet fsi tools/execution/ctp-check.fsx -- integration --integration FILE --log FILE [--registry FILE]
//   dotnet fsi tools/execution/ctp-check.fsx -- ownership   --ownership FILE --files FILE   (one path per line)
//   dotnet fsi tools/execution/ctp-check.fsx -- ci          --runs FILE
//   dotnet fsi tools/execution/ctp-check.fsx -- test        [--indy-init DIR]
//
// Every rule is a pure function from parsed input to a list of findings. A finding names its rule (CTP-*), its
// subject and why. Exit 1 when any finding is an ERROR. `ci` only reports (exit 0): it measures waste, it cannot
// prevent a push. `test` runs positive and negative cases, plus a replay of EXP-INDY-002's real registry, lifecycle,
// integration and CI files, committed as fixtures whose SHA-256 is pinned in this file (EXP-002-EVIDENCE.md, sec. 5).

open System
open System.IO
open System.Text.Json.Nodes
open System.Text.RegularExpressions

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")

// ---- json helpers (total) ----
let field (k: string) (n: JsonNode) : JsonNode option =
    match n with
    | :? JsonObject as o -> (let mutable v : JsonNode = null in if o.TryGetPropertyValue(k, &v) && not (isNull v) then Some v else None)
    | _ -> None
let str k n = field k n |> Option.bind (fun v -> try Some(v.GetValue<string>()) with _ -> None) |> Option.defaultValue ""
let num k n = field k n |> Option.bind (fun v -> try Some(v.GetValue<int>()) with _ -> None)
let arr k n = field k n |> Option.map (fun v -> try v.AsArray() |> Seq.toList with _ -> []) |> Option.defaultValue []
let strs k n = arr k n |> List.choose (fun v -> try Some(v.GetValue<string>()) with _ -> None)
let boolOr k n = field k n |> Option.bind (fun v -> try Some(v.GetValue<bool>()) with _ -> None) |> Option.defaultValue false
let blank (s: string) = String.IsNullOrWhiteSpace s

type Severity = Error | Warning
type Finding = { Rule: string; Subject: string; Detail: string; Severity: Severity }
let err rule subject detail = { Rule = rule; Subject = subject; Detail = detail; Severity = Error }
let warn rule subject detail = { Rule = rule; Subject = subject; Detail = detail; Severity = Warning }

// =====================================================================================================================
// 1. Contract registry (CONTRACT-SCHEMA.json, schema indy-init.ctp-registry/1)
// =====================================================================================================================
let kinds = set [ "field"; "action"; "display"; "validation-rule"; "workflow-outcome" ]
let persistenceModes = set [ "none"; "local"; "durable-github" ]
let isUnitOrUi (t: string) = t.StartsWith "UT-" || t.StartsWith "UI-"
let isAcceptance (t: string) = t.StartsWith "AT-"
/// "one of a, b, c" in a validation rule names an enumerated field's values.
let enumeratedValues (c: JsonNode) =
    arr "validation" c |> List.map (str "rule")
    |> List.tryPick (fun r -> let m = Regex.Match(r, @"^(?:must be )?one of:?\s+(.+)$", RegexOptions.IgnoreCase) in if m.Success then Some(m.Groups[1].Value.Split(',') |> Array.map (fun s -> s.Trim()) |> List.ofArray) else None)
    |> Option.defaultValue []

let contractRules : (JsonNode -> Finding list) list =
    let id c = str "id" c
    [ // identity and traceability
      fun c ->
        [ if not (Regex.IsMatch(id c, @"^[A-Z][A-Z0-9]*-\d+$")) then err "CTP-C-ID" (id c) "the stable contract/field ID must look like INV-006"
          if (strs "requirements" c).IsEmpty then err "CTP-C-REQ" (id c) "requirements: at least one requirement ID"
          if not (kinds.Contains(str "kind" c)) then err "CTP-C-KIND" (id c) $"""kind '{str "kind" c}' is not one of {String.concat ", " kinds}"""
          if blank (str "group" c) then err "CTP-C-GROUP" (id c) "group: the release group (CG-*) the contract ships in" ]
      // typed F# boundary
      fun c ->
        let fs = field "fsharp" c
        let has k = fs |> Option.exists (fun f -> not (blank (str k f)))
        match str "kind" c with
        | "field" -> [ for k in [ "record"; "property"; "type" ] do if not (has k) then err "CTP-C-FSHARP" (id c) $"a field needs fsharp.{k}" ]
        | "action" when not (blank (str "command" c)) -> [ for k in [ "input"; "output" ] do if not (has k) then err "CTP-C-FSHARP" (id c) $"a command needs fsharp.{k}" ]
        | "validation-rule" | "display" -> [ if not (has "type") then err "CTP-C-FSHARP" (id c) "fsharp.type: the typed value shown or returned" ]
        | _ -> []
      // accessible names (CR-1, CR-2)
      fun c ->
        [ if (str "kind" c = "field" || str "kind" c = "display") && blank (str "label" c) then err "CTP-C-LABEL" (id c) "label: the accessible name"
          for ctl in arr "controls" c do
              if blank (str "accessibleName" ctl) then err "CTP-C-CONTROL-NAME" (id c) $"""control '{str "uiAction" ctl}' has no accessible name"""
          // EXP-002 CR-1 was "several UI actions under one label": names must also be distinct within a contract
          for (n, k) in arr "controls" c |> List.map (str "accessibleName") |> List.filter (blank >> not) |> List.countBy (fun n -> n.Trim().ToLowerInvariant()) do
              if k > 1 then err "CTP-C-CONTROL-NAME" (id c) $"{k} controls share the accessible name '{n}'"
          if str "kind" c = "action" && (arr "controls" c).IsEmpty && blank (str "command" c) then err "CTP-C-ACTION" (id c) "an action needs a command or at least one control"
          for f in arr "fields" c do
              for k in [ "label"; "component"; "type" ] do
                  if blank (str k f) then err "CTP-C-NESTED-LABEL" (id c) $"""nested field {str "record" f}.{str "property" f} has no {k}"""
          let values = enumeratedValues c
          let labelled = arr "options" c |> List.filter (fun o -> not (blank (str "label" o))) |> List.map (str "value") |> Set.ofList
          for v in values do if not (labelled.Contains v) then err "CTP-C-OPTION-LABEL" (id c) $"option '{v}' has no display label" ]
      // validation and error behaviour
      fun c ->
        [ for v in arr "validation" c do
              if blank (str "rule" v) then err "CTP-C-VALIDATION" (id c) "a validation entry needs a rule"
              if blank (str "error" v) then err "CTP-C-ERROR" (id c) $"""validation '{str "rule" v}' states no error behaviour""" ]
      // state transitions: structured, each with an implementer-side test and an independent acceptance test (CR-3, D-3)
      fun c ->
        [ for t in arr "transitions" c do
              let name = $"""{str "from" t} -{str "event" t}-> {str "to" t}"""
              if blank (str "from" t) || blank (str "event" t) || blank (str "to" t) then
                  err "CTP-C-TRANSITION-STRUCTURE" (id c) $"""transition '{str "raw" t}' must state from, event and to"""
              // what the target state holds must be stated (EXP-002 CR-3: Saved needs version and time; ["nothing"] is explicit)
              if (strs "carries" t |> List.filter (blank >> not)).IsEmpty then
                  err "CTP-C-TRANSITION-CARRIES" (id c) $"transition {name} does not state what the target state carries (use [\"nothing\"] when it carries nothing)"
              let tests = strs "tests" t
              if not (tests |> List.exists isUnitOrUi) then err "CTP-C-TRANSITION-TEST" (id c) $"transition {name} has no UT-/UI- test obligation"
              if not (tests |> List.exists isAcceptance) then err "CTP-C-TRANSITION-TEST" (id c) $"transition {name} has no independent AT- acceptance obligation" ]
      // persistence expectation
      fun c ->
        let p = str "persistence" c
        [ if not (persistenceModes.Contains p) then err "CTP-C-PERSISTENCE" (id c) $"persistence '{p}' must be one of none, local, durable-github"
          if p = "durable-github" && not (boolOr "realProviderEvidence" c) then
              err "CTP-C-PERSISTENCE" (id c) "durable-github persistence must declare realProviderEvidence: true (verified only by a real write and an independent read-back)" ]
      // test obligations
      fun c ->
        let t = field "tests" c
        let list k = t |> Option.map (strs k) |> Option.defaultValue []
        [ if (list "unit" @ list "ui").IsEmpty then err "CTP-C-TESTS" (id c) "tests.unit or tests.ui: an implementer-side obligation"
          if List.contains (str "kind" c) [ "field"; "action"; "workflow-outcome" ] && (list "acceptance").IsEmpty then
              err "CTP-C-TESTS" (id c) "tests.acceptance: an independent acceptance obligation" ] ]

let checkRegistry (registry: JsonNode) : Finding list =
    let contracts = arr "contracts" registry
    [ if str "schema" registry <> "indy-init.ctp-registry/1" then err "CTP-C-SCHEMA" "registry" "schema must be indy-init.ctp-registry/1"
      yield! contracts |> List.countBy (str "id") |> List.filter (snd >> (<) 1) |> List.map (fun (i, _) -> err "CTP-C-ID" i "duplicate contract ID")
      yield! contracts |> List.collect (fun c -> contractRules |> List.collect (fun r -> r c)) ]

// =====================================================================================================================
// 2. Lifecycle log (JSON lines: {t, group, version, status, hash, actor, changeRequest?, evidence?})
// =====================================================================================================================
let states = [ "DEFINED"; "VALIDATED"; "RELEASED"; "IMPLEMENTING"; "STAGED"; "INTEGRATION_READY"; "VERIFIED" ]
let rank (s: string) = List.tryFindIndex ((=) s) states
// Actors are CTP roles or the competition package's roles (AGENT-OWNERSHIP.md): the contract coordinator is the
// package's integration role; domain-side implementers are domain/persistence; UI-side are ui/ui-2/ui-3.
let domainSide = set [ "domain"; "persistence" ]
let uiSide = set [ "ui"; "ui-2"; "ui-3" ]
let coordinators = set [ "coordinator"; "integration" ]
let roleFor = Map.ofList [ "DEFINED", coordinators; "VALIDATED", coordinators; "RELEASED", coordinators
                           "IMPLEMENTING", domainSide + uiSide; "STAGED", domainSide + uiSide; "BLOCKED", domainSide + uiSide + coordinators
                           "INTEGRATION_READY", set [ "integration" ]; "VERIFIED", set [ "integration" ] ]

let checkLifecycle (rows: JsonNode list) : Finding list =
    let ver r = num "version" r
    let key r = $"""{str "group" r} v{ver r |> Option.map string |> Option.defaultValue "?"}"""
    let byVersion = rows |> List.groupBy key
    let byGroup = rows |> List.groupBy (str "group")
    [ if rows.IsEmpty then err "CTP-L-EMPTY" "lifecycle" "no lifecycle rows"
      for r in rows do
          let s = str "status" r
          if s <> "BLOCKED" && (rank s).IsNone then err "CTP-L-STATE" (key r) $"unknown status {s}"
          if (ver r).IsNone || ver r |> Option.exists (fun v -> v < 1) then err "CTP-L-VERSION" (key r) "version must be an integer >= 1"
          if blank (str "group" r) then err "CTP-L-GROUP" (key r) "group is required"
          match DateTimeOffset.TryParse(str "t" r) with
          | true, _ -> ()
          | _ -> err "CTP-L-TIME" (key r) "t must be an ISO-8601 time"
          if s <> "BLOCKED" && blank (str "hash" r) then err "CTP-L-IMMUTABLE" (key r) $"{s} row has no hash"
          match Map.tryFind s roleFor with
          | Some roles when not (roles.Contains(str "actor" r)) -> err "CTP-L-ROLE" (key r) $"""{s} recorded by '{str "actor" r}', expected one of {String.concat "/" roles}"""
          | _ -> ()
      for (g, rs) in byGroup do
          // time never goes backwards within a group
          let times = rs |> List.choose (fun r -> match DateTimeOffset.TryParse(str "t" r) with | true, t -> Some t | _ -> None)
          if times <> List.sort times then err "CTP-L-TIME" g "rows are not in time order"
          // versions start at 1 and rise by one; a revision changes content (a new hash)
          let vs = rs |> List.choose ver |> List.distinct |> List.sort
          if vs <> [ 1 .. List.length vs ] then err "CTP-L-VERSION" g $"versions {vs} are not 1..n"
          let hashOf v = rs |> List.tryFind (fun r -> ver r = Some v && str "status" r <> "BLOCKED") |> Option.map (str "hash")
          for v in vs |> List.filter (fun v -> v > 1) do
              if [ 1 .. v - 1 ] |> List.exists (fun u -> hashOf u = hashOf v && (hashOf v).IsSome) then
                  err "CTP-L-IMMUTABLE" $"{g} v{v}" "a revision with the same hash as an earlier version changes nothing"
      for (k, rs) in byVersion do
          let states' = rs |> List.filter (fun r -> str "status" r <> "BLOCKED")
          // immutability: one version, one hash
          let hashes = states' |> List.map (str "hash") |> List.distinct
          if hashes.Length > 1 then err "CTP-L-IMMUTABLE" k $"one version carries {hashes.Length} hashes; content changes need a new version"
          // ordering: no skipped state, no going back (BLOCKED is an annotation, not a state)
          let seen = states' |> List.map (str "status") |> List.choose rank
          match seen |> List.tryPick (fun i -> if i > 0 && not (List.exists (fun j -> j = i - 1) seen) then Some states[i - 1] else None) with
          | Some m -> err "CTP-L-ORDER" k $"reached a later state without {m}"
          | None -> ()
          if seen <> (seen |> List.sort) then err "CTP-L-ORDER" k "states recorded out of order"
          // INTEGRATION_READY needs STAGED by every side the group declares (default: a domain side and a UI side)
          let declared = rs |> List.tryFind (fun r -> str "status" r = "DEFINED") |> Option.map (strs "sides") |> Option.defaultValue []
          let sides = if declared.IsEmpty then [ "domain"; "ui" ] else declared
          let staged = states' |> List.filter (fun r -> str "status" r = "STAGED") |> List.map (str "actor") |> Set.ofList
          let missing = sides |> List.filter (fun sd -> Set.intersect staged (if sd = "ui" then uiSide else domainSide) |> Set.isEmpty)
          if states' |> List.exists (fun r -> str "status" r = "INTEGRATION_READY") && not missing.IsEmpty then
              err "CTP-L-STAGED" k $"""INTEGRATION_READY without STAGED from the {String.concat " and " missing} side"""
          // a revised version cites its change request, CR-n
          match ver rs.Head with
          | Some v when v > 1 && rs |> List.exists (fun r -> str "status" r = "DEFINED" && not (Regex.IsMatch(str "changeRequest" r, @"^CR-\d+$"))) ->
              err "CTP-L-CHANGE-REQUEST" k "a revised version must cite its change request (changeRequest: CR-n)"
          | _ -> ()
          // VERIFIED needs well-formed evidence: a commit SHA, a numeric CI run id, an acceptance run reference
          for r in states' |> List.filter (fun r -> str "status" r = "VERIFIED") do
              let ev = field "evidence" r
              let get k = ev |> Option.map (str k) |> Option.defaultValue ""
              if not (Regex.IsMatch(get "candidateSha", "^[0-9a-f]{7,40}$") && Regex.IsMatch(get "ciRun", @"^\d+$") && not (blank (get "acceptanceRun"))) then
                  err "CTP-L-VERIFIED" k "VERIFIED needs evidence.candidateSha (a commit), evidence.ciRun (a run id whose required CI passed) and evidence.acceptanceRun" ]

// =====================================================================================================================
// 3. Integration record ({integrations: [{id, groups:[{group, version, hash}], acceptance:{runRef, author, implementers,
//    passed, total}, defects:[{id, class, owner}], persistence:{mode, realProvider:{runRef, readBack}}}]})
// =====================================================================================================================
let defectClasses = set [ "incorrect-contract"; "domain-defect"; "ui-binding-defect"; "persistence-defect"; "environment-tooling"; "missing-mapping"; "agent-coordination" ]

let checkIntegration (record: JsonNode) (log: JsonNode list) (registry: JsonNode option) : Finding list =
    let released = log |> List.filter (fun r -> str "status" r = "RELEASED")
    let latest g = released |> List.filter (fun r -> str "group" r = g) |> List.sortBy (fun r -> num "version" r |> Option.defaultValue 0) |> List.tryLast
    let stagedSides g v =
        log |> List.filter (fun r -> str "group" r = g && num "version" r = Some v && str "status" r = "STAGED") |> List.map (str "actor") |> Set.ofList
    let durableGroups =
        registry |> Option.map (fun reg -> arr "contracts" reg |> List.filter (fun c -> str "persistence" c = "durable-github") |> List.map (str "group") |> Set.ofList)
        |> Option.defaultValue Set.empty
    let lower (s: string) = s.Trim().ToLowerInvariant()
    [ if (arr "integrations" record).IsEmpty then err "CTP-I-EMPTY" "integration" "no integrations recorded"
      for i in arr "integrations" record do
          let name = str "id" i
          if (arr "groups" i).IsEmpty then err "CTP-I-EMPTY" name "an integration names no groups"
          for g in arr "groups" i do
              let gid = str "group" g
              match latest gid, num "version" g with
              | None, _ -> err "CTP-I-VERSION" name $"{gid} was never RELEASED"
              | _, None -> err "CTP-I-VERSION" name $"{gid}: version must be an integer"
              | Some r, Some v when num "version" r <> Some v || str "hash" r <> str "hash" g ->
                  err "CTP-I-VERSION" name $"""{gid} integrated at v{v}, latest release is v{num "version" r |> Option.defaultValue 0} (hash must match too)"""
              | Some _, Some v ->
                  let st = stagedSides gid v
                  if Set.intersect st domainSide |> Set.isEmpty || Set.intersect st uiSide |> Set.isEmpty then
                      err "CTP-I-STAGED" name $"{gid} v{v} is not STAGED by both a domain-side and a UI-side owner in the lifecycle log"
          match field "acceptance" i with
          | None -> err "CTP-I-ACCEPTANCE" name "no independent browser acceptance run recorded for this integration"
          | Some a ->
              if blank (str "runRef" a) then err "CTP-I-ACCEPTANCE" name "acceptance.runRef: where the run's output is"
              let implementers = strs "implementers" a |> List.map lower
              if blank (str "author" a) || implementers.IsEmpty then err "CTP-I-INDEPENDENT" name "acceptance.author and acceptance.implementers are required"
              elif List.contains (lower (str "author" a)) implementers then err "CTP-I-INDEPENDENT" name "the acceptance test's author implemented the code under test"
              match num "passed" a, num "total" a with
              | Some p, Some t when t > 0 && p = t -> ()
              | Some _, Some t when t <= 0 -> err "CTP-I-ACCEPTANCE" name "acceptance ran no tests"
              | Some _, Some _ -> err "CTP-I-ACCEPTANCE" name "acceptance did not pass in full"
              | _ -> err "CTP-I-ACCEPTANCE" name "acceptance.passed and acceptance.total are required"
          for d in arr "defects" i do
              if not (defectClasses.Contains(str "class" d)) then err "CTP-I-DEFECT" (str "id" d) $"""class '{str "class" d}' is not a defect class"""
              if blank (str "owner" d) then err "CTP-I-DEFECT" (str "id" d) "defect has no owner"
          let mode = field "persistence" i |> Option.map (str "mode") |> Option.defaultValue ""
          if not (persistenceModes.Contains mode) then err "CTP-I-PERSISTENCE" name $"persistence.mode '{mode}' must be one of none, local, durable-github"
          let claimsDurable = arr "groups" i |> List.exists (fun g -> durableGroups.Contains(str "group" g))
          if claimsDurable && mode <> "durable-github" then
              err "CTP-I-REAL-PROVIDER" name "the registry declares durable-github persistence for an integrated group, but the integration records a weaker mode"
          if mode = "durable-github" then
              let rp = field "persistence" i |> Option.bind (field "realProvider")
              if rp |> Option.forall (fun p -> blank (str "runRef" p) || not (boolOr "readBack" p)) then
                  err "CTP-I-REAL-PROVIDER" name "durable GitHub persistence needs a real-provider write with an independent read-back (a REST double is component evidence only)" ]

// =====================================================================================================================
// 4. Ownership ({owners: {role: [globs]}}; files: one path per line)
// =====================================================================================================================
let globRx (g: string) = Regex("^" + (Regex.Escape g).Replace(@"\*\*/", "(.*/)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*") + "$")
let checkOwnership (ownership: JsonNode) (files: string list) : Finding list =
    let owners =
        match field "owners" ownership with
        | Some (:? JsonObject as o) -> o |> Seq.map (fun kv -> kv.Key, (try kv.Value.AsArray() |> Seq.map (fun v -> v.GetValue<string>()) |> List.ofSeq with _ -> [])) |> List.ofSeq
        | _ -> []
    let rec norm (f: string) = let f = f.Replace('\\', '/') in if f.StartsWith "./" then norm (f.Substring 2) elif f.StartsWith "/" then norm (f.Substring 1) else f
    [ if owners.IsEmpty then err "CTP-O-OWNERS" "ownership" "no owners"
      if files.IsEmpty then err "CTP-O-FILES" "ownership" "no files to check"
      for f in files |> List.map norm do
          match owners |> List.filter (fun (_, gs) -> gs |> List.exists (fun g -> (globRx g).IsMatch f)) |> List.map fst with
          | [] -> err "CTP-O-UNOWNED" f "no owner"
          | [ _ ] -> ()
          | many -> err "CTP-O-SHARED" f $"""owned by {String.Join(", ", many)}: shared files cause merge conflicts""" ]

// =====================================================================================================================
// 5. CI waste ({runs: [{workflow, head, conclusion}]}): measured, never a gate
// =====================================================================================================================
let ciReport (runs: JsonNode) : Finding list =
    arr "runs" runs
    |> List.filter (fun r -> str "conclusion" r = "cancelled")
    |> List.map (fun r -> warn "CTP-CI-CANCELLED" (str "head" r) $"""{str "workflow" r} on {str "head" r} cancelled by a newer push (push once per candidate; see INTEGRATION-POLICY.md section 4)""")

// =====================================================================================================================
// EXP-INDY-002 adapter: maps that experiment's registry format onto the CTP schema. It adds exactly three stated
// defaults, because EXP-002's format has no such fields: requirements = [the contract's own ID] (EXP-002 keys
// contracts by requirement ID); group = "CG-?" (EXP-002 keeps groups in the lifecycle log); persistence = "local" when
// the contract mentions persistence, else "none" (EXP-002's boundary was localStorage). Everything else is mapped as
// written: a uiAction listed without its own name stays unnamed (a contract's label names only its first action, and
// only on action contracts); a free-text transition stays unstructured; validation errors come from the contract's own
// failure text; the registry's tests become unit tests; it held no acceptance obligations. EXP-002-EVIDENCE.md, sec. 5.
// =====================================================================================================================
let fromExp002 (reg: JsonNode) : JsonNode =
    let jstr (s: string) : JsonNode = JsonValue.Create s
    let obj (kvs: (string * JsonNode) list) = let o = JsonObject() in kvs |> List.iter (fun (k, v) -> o[k] <- v); o :> JsonNode
    let jarr (xs: JsonNode list) = let a = JsonArray() in xs |> List.iter (fun x -> a.Add x); a :> JsonNode
    // an explicitly named secondary control, by the uiAction it names (as EXP-002's CR-1 and CR-2 added them)
    let namedBy = [ "submitLabel", "SubmitEditor"; "withdrawLabel", "WithdrawSelected"; "newActionLabel", "ShowStart"; "accessibleName", "OpenInvestigation" ]
    let contract (c: JsonNode) =
        let kind = str "kind" c
        let actions = (str "uiAction" c).Split(';', StringSplitOptions.RemoveEmptyEntries ||| StringSplitOptions.TrimEntries) |> List.ofArray
        let nameFor (i: int) (a: string) =
            let verb = a.Split(' ').[0]
            match namedBy |> List.tryFind (fun (_, v) -> v = verb) |> Option.map (fun (k, _) -> str k c) with
            | Some n when not (blank n) -> n
            | _ when verb = "OpenEditor" && a.Contains " New" && not (blank (str "openLabel" c)) -> str "openLabel" c
            | _ when verb = "OpenEditor" && not (a.Contains " New") && not (blank (str "reviseLabel" c)) -> str "reviseLabel" c
            | _ when i = 0 && kind = "action" -> str "label" c
            | _ -> ""
        let controls =
            (actions |> List.mapi (fun i a -> obj [ "uiAction", jstr a; "accessibleName", jstr (nameFor i a) ]))
            @ (if blank (str "itemEditLabel" c) then [] else [ obj [ "uiAction", jstr "OpenEditor item"; "accessibleName", jstr (str "itemEditLabel" c) ] ])
        let validation =
            strs "validation" c |> List.map (fun r -> obj [ "rule", jstr r; "error", jstr (if blank (str "failure" c) then "" else str "failure" c) ])
        let options =
            match field "optionLabels" c with
            | Some (:? JsonObject as o) -> o |> Seq.map (fun kv -> obj [ "value", jstr kv.Key; "label", jstr (kv.Value.GetValue<string>()) ]) |> List.ofSeq
            | _ -> []
        let fields =
            arr "fields" c |> List.map (fun f -> obj [ "record", jstr (str "record" f); "property", jstr (str "property" f); "type", jstr (str "fsType" f)
                                                       "label", jstr (str "label" f); "component", jstr (str "component" f) ])
        let transitions = if blank (str "transition" c) then [] else [ obj [ "raw", jstr (str "transition" c); "tests", jarr (strs "tests" c |> List.map jstr) ] ]
        obj [ "id", jstr (str "id" c); "requirements", jarr [ jstr (str "id" c) ]; "kind", jstr kind; "group", jstr "CG-?"
              "label", jstr (str "label" c); "command", jstr (str "command" c)
              "fsharp", obj [ "record", jstr (str "record" c); "property", jstr (str "property" c); "type", jstr (str "fsType" c)
                              "input", jstr (str "input" c); "output", jstr (str "output" c) ]
              "controls", jarr controls; "fields", jarr fields; "options", jarr options; "validation", jarr validation
              "transitions", jarr transitions
              "persistence", jstr (if field "persistence" c |> Option.isSome then "local" else "none")
              "tests", obj [ "unit", jarr (strs "tests" c |> List.map jstr); "ui", jarr []; "acceptance", jarr [] ] ]
    obj [ "schema", jstr "indy-init.ctp-registry/1"; "contracts", jarr (arr "contracts" reg |> List.map contract) ]

// =====================================================================================================================
// tests
// =====================================================================================================================
let git (root: string) (a: string) =
    let psi = Diagnostics.ProcessStartInfo("git", $"-C \"{root}\" {a}", RedirectStandardOutput = true, RedirectStandardError = true)
    use p = Diagnostics.Process.Start psi
    let out = p.StandardOutput.ReadToEnd()
    p.WaitForExit()
    if p.ExitCode = 0 then Some out else None

let parseLines (s: string) = s.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.map JsonNode.Parse |> List.ofArray
let codesFor (fs: Finding list) (subject: string) = fs |> List.filter (fun f -> f.Subject = subject) |> List.map (fun f -> f.Rule) |> Set.ofList

let tests (root: string) =
    let j (s: string) = JsonNode.Parse s
    let jstr (s: string) : JsonNode = JsonValue.Create s
    let edit (path: string list) (v: JsonNode) (n: JsonNode) =
        let c = n.DeepClone()
        let rec go (node: JsonNode) (p: string list) =
            match p with
            | [ k ] -> (match Int32.TryParse(k: string) with | true, i -> node.AsArray()[i] <- v | _ -> node.AsObject()[k] <- v)
            | k :: rest -> go (match Int32.TryParse(k: string) with | true, i -> node.AsArray()[i] | _ -> node.AsObject()[k]) rest
            | [] -> ()
        go c path; c
    let good =
        j """{"schema":"indy-init.ctp-registry/1","contracts":[
          {"id":"INV-004","requirements":["INV-004"],"kind":"action","group":"CG-1","command":"StartInvestigation",
           "fsharp":{"input":"StartInvestigationInput","output":"InvestigationEvent list"},
           "controls":[{"uiAction":"SubmitStart","role":"button","accessibleName":"Start investigation"},{"uiAction":"CancelStart","role":"button","accessibleName":"Cancel"}],
           "transitions":[{"from":"none","event":"InvestigationStarted","to":"Open; save state Unsaved","carries":["id","save state Unsaved"],"tests":["UT-INV-004","UI-INV-004","AT-08"]}],
           "persistence":"none","tests":{"unit":["UT-INV-004"],"ui":["UI-INV-004"],"acceptance":["AT-08"]}},
          {"id":"INV-031","requirements":["INV-031"],"kind":"field","group":"CG-4","label":"Relationship",
           "fsharp":{"record":"RelationshipInput","property":"Kind","type":"RelationshipKind option"},
           "options":[{"value":"supports","label":"Supports"},{"value":"contradicts","label":"Contradicts"}],
           "validation":[{"rule":"one of supports, contradicts","error":"Invalid [INV-031]"}],
           "persistence":"local","tests":{"unit":["UT-INV-031"],"acceptance":["AT-11"]}}]}"""
    let row (t: string) (v: int) (s: string) (h: string) (a: string) = $"""{{"t":"2026-11-07T10:{t}:00Z","group":"CG-1","version":{v},"status":"{s}","hash":"{h}","actor":"{a}"}}"""
    let lifecycle =
        parseLines (String.concat "\n" [ row "00" 1 "DEFINED" "a" "coordinator"; row "01" 1 "VALIDATED" "a" "coordinator"; row "02" 1 "RELEASED" "a" "integration"
                                         row "03" 1 "IMPLEMENTING" "a" "domain"; row "04" 1 "IMPLEMENTING" "a" "ui-2"; row "05" 1 "STAGED" "a" "persistence"
                                         row "06" 1 "STAGED" "a" "ui-2"; row "07" 1 "INTEGRATION_READY" "a" "integration"
                                         """{"t":"2026-11-07T10:08:00Z","group":"CG-1","version":1,"status":"VERIFIED","hash":"a","actor":"integration","evidence":{"candidateSha":"5151cdf","ciRun":"42115761982","acceptanceRun":"acceptance-run.json"}}""" ])
    let rev2 = parseLines """{"t":"2026-11-07T10:09:00Z","group":"CG-1","version":2,"status":"DEFINED","hash":"c","actor":"coordinator","changeRequest":"CR-3"}"""
    let integ =
        j """{"integrations":[{"id":"INT-1","groups":[{"group":"CG-1","version":1,"hash":"a"}],
            "acceptance":{"runRef":"acceptance-run.json","author":"reviewer","implementers":["domain","ui-2"],"passed":22,"total":22},
            "defects":[{"id":"D-3","class":"ui-binding-defect","owner":"ui-2"}],"persistence":{"mode":"local"}}]}"""
    let reg f = checkRegistry f |> List.map (fun x -> x.Rule) |> Set.ofList
    let life rows = checkLifecycle rows |> List.map (fun x -> x.Rule) |> Set.ofList
    let integration r l g = checkIntegration r l g |> List.map (fun x -> x.Rule) |> Set.ofList
    let drop (status: string) (actor: string) = lifecycle |> List.filter (fun r -> not (str "status" r = status && str "actor" r = actor))
    let mapRow (status: string) (f: JsonNode -> JsonNode) = lifecycle |> List.map (fun r -> if str "status" r = status then f r else r)
    let own o files = checkOwnership (j o) files |> List.map (fun f -> f.Rule) |> Set.ofList
    let ig path v = integration (integ |> edit path v) lifecycle None
    let synthetic : (string * Set<string> * string option) list =
        [ "registry: complete contracts pass", reg good, None
          "registry: an unnamed secondary control (CR-1)", reg (good |> edit [ "contracts"; "0"; "controls"; "1"; "accessibleName" ] (jstr "")), Some "CTP-C-CONTROL-NAME"
          "registry: two controls under one name (CR-1)", reg (good |> edit [ "contracts"; "0"; "controls"; "1"; "accessibleName" ] (jstr "start investigation ")), Some "CTP-C-CONTROL-NAME"
          "registry: an unlabelled option (CR-2)", reg (good |> edit [ "contracts"; "1"; "options"; "1"; "label" ] (jstr "")), Some "CTP-C-OPTION-LABEL"
          "registry: 'must be one of:' phrasing still needs option labels", reg (good |> edit [ "contracts"; "1"; "validation"; "0"; "rule" ] (jstr "must be one of: supports, contradicts, inconclusive")), Some "CTP-C-OPTION-LABEL"
          "registry: a free-text transition", reg (good |> edit [ "contracts"; "0"; "transitions"; "0"; "event" ] (jstr "")), Some "CTP-C-TRANSITION-STRUCTURE"
          "registry: a transition that does not say what it carries (CR-3)", reg (good |> edit [ "contracts"; "0"; "transitions"; "0"; "carries" ] (j "[]")), Some "CTP-C-TRANSITION-CARRIES"
          "registry: a transition without an acceptance test (D-3)", reg (good |> edit [ "contracts"; "0"; "transitions"; "0"; "tests" ] (j """["UT-INV-004"]""")), Some "CTP-C-TRANSITION-TEST"
          "registry: a transition without an implementer test", reg (good |> edit [ "contracts"; "0"; "transitions"; "0"; "tests" ] (j """["AT-08"]""")), Some "CTP-C-TRANSITION-TEST"
          "registry: validation without error behaviour", reg (good |> edit [ "contracts"; "1"; "validation"; "0"; "error" ] (jstr "")), Some "CTP-C-ERROR"
          "registry: a field without its F# type", reg (good |> edit [ "contracts"; "1"; "fsharp"; "type" ] (jstr "")), Some "CTP-C-FSHARP"
          "registry: a field without an accessible name", reg (good |> edit [ "contracts"; "1"; "label" ] (jstr "")), Some "CTP-C-LABEL"
          "registry: no acceptance obligation", reg (good |> edit [ "contracts"; "1"; "tests"; "acceptance" ] (j "[]")), Some "CTP-C-TESTS"
          "registry: persistence not stated", reg (good |> edit [ "contracts"; "1"; "persistence" ] (jstr "")), Some "CTP-C-PERSISTENCE"
          "registry: durable persistence without real-provider evidence", reg (good |> edit [ "contracts"; "0"; "persistence" ] (jstr "durable-github")), Some "CTP-C-PERSISTENCE"
          "registry: no requirement ID", reg (good |> edit [ "contracts"; "1"; "requirements" ] (j "[]")), Some "CTP-C-REQ"
          "registry: a nested field without a label", reg (good |> edit [ "contracts"; "1"; "fields" ] (j """[{"record":"R","property":"EvidenceId","type":"string","component":"select"}]""")), Some "CTP-C-NESTED-LABEL"
          "lifecycle: complete lifecycle (package role names) passes", life lifecycle, None
          "lifecycle: a revision with its change request passes", life (lifecycle @ rev2), None
          "lifecycle: released without validation", life (drop "VALIDATED" "coordinator"), Some "CTP-L-ORDER"
          "lifecycle: integration-ready before the UI side staged", life (drop "STAGED" "ui-2"), Some "CTP-L-STAGED"
          "lifecycle: integration-ready with the UI side never recorded", life (lifecycle |> List.filter (fun r -> str "actor" r <> "ui-2")), Some "CTP-L-STAGED"
          "lifecycle: one version, two hashes", life (lifecycle @ parseLines """{"t":"2026-11-07T10:09:00Z","group":"CG-1","version":1,"status":"VERIFIED","hash":"b","actor":"integration","evidence":{"candidateSha":"abcdef1","ciRun":"1","acceptanceRun":"y"}}"""), Some "CTP-L-IMMUTABLE"
          "lifecycle: a revision with the same hash as v1", life (lifecycle @ parseLines """{"t":"2026-11-07T10:09:00Z","group":"CG-1","version":2,"status":"DEFINED","hash":"a","actor":"coordinator","changeRequest":"CR-1"}"""), Some "CTP-L-IMMUTABLE"
          "lifecycle: a revision without its change request", life (lifecycle @ parseLines """{"t":"2026-11-07T10:09:00Z","group":"CG-1","version":2,"status":"DEFINED","hash":"c","actor":"coordinator"}"""), Some "CTP-L-CHANGE-REQUEST"
          "lifecycle: a change request that is not CR-n", life (lifecycle @ (rev2 |> List.map (edit [ "changeRequest" ] (jstr "lol")))), Some "CTP-L-CHANGE-REQUEST"
          "lifecycle: a string version", life (lifecycle @ (rev2 |> List.map (edit [ "version" ] (jstr "2")))), Some "CTP-L-VERSION"
          "lifecycle: rows out of time order", life (lifecycle @ (rev2 |> List.map (edit [ "t" ] (jstr "2026-11-07T09:00:00Z")))), Some "CTP-L-TIME"
          "lifecycle: VERIFIED without evidence", life (mapRow "VERIFIED" (edit [ "evidence" ] (j "{}"))), Some "CTP-L-VERIFIED"
          "lifecycle: VERIFIED with meaningless evidence", life (mapRow "VERIFIED" (edit [ "evidence" ] (j """{"candidateSha":"x","ciRun":"x","acceptanceRun":"x"}"""))), Some "CTP-L-VERIFIED"
          "lifecycle: VERIFIED evidence whose SHA is not a commit", life (mapRow "VERIFIED" (edit [ "evidence" ] (j """{"candidateSha":"main","ciRun":"42115761982","acceptanceRun":"a.json"}"""))), Some "CTP-L-VERIFIED"
          "lifecycle: an implementer releasing a contract", life (mapRow "RELEASED" (edit [ "actor" ] (jstr "ui"))), Some "CTP-L-ROLE"
          "lifecycle: a BLOCKED note without a hash is not an immutability breach", life (lifecycle @ parseLines """{"t":"2026-11-07T10:09:00Z","group":"CG-1","version":1,"status":"BLOCKED","actor":"domain","note":"waiting on CR"}"""), None
          "lifecycle: a BLOCKED note by an unknown actor", life (lifecycle @ parseLines """{"t":"2026-11-07T10:09:00Z","group":"CG-1","version":1,"status":"BLOCKED","actor":"nobody"}"""), Some "CTP-L-ROLE"
          "integration: matching versions and full acceptance pass", integration integ lifecycle None, None
          "integration: a stale contract version", ig [ "integrations"; "0"; "groups"; "0"; "hash" ] (jstr "old"), Some "CTP-I-VERSION"
          "integration: a group released but not staged", integration integ (drop "STAGED" "persistence") None, Some "CTP-I-STAGED"
          "integration: no browser acceptance", ig [ "integrations"; "0"; "acceptance" ] null, Some "CTP-I-ACCEPTANCE"
          "integration: acceptance that ran no tests", ig [ "integrations"; "0"; "acceptance" ] (j """{"runRef":"r","author":"reviewer","implementers":["ui"],"passed":0,"total":0}"""), Some "CTP-I-ACCEPTANCE"
          "integration: acceptance counts missing", ig [ "integrations"; "0"; "acceptance" ] (j """{"runRef":"r","author":"reviewer","implementers":["ui"]}"""), Some "CTP-I-ACCEPTANCE"
          "integration: self-attested acceptance", ig [ "integrations"; "0"; "acceptance"; "author" ] (jstr "UI-2"), Some "CTP-I-INDEPENDENT"
          "integration: no author or implementers", ig [ "integrations"; "0"; "acceptance"; "implementers" ] (j "[]"), Some "CTP-I-INDEPENDENT"
          "integration: nothing integrated", integration (j """{"integrations":[]}""") lifecycle None, Some "CTP-I-EMPTY"
          "integration: no groups", ig [ "integrations"; "0"; "groups" ] (j "[]"), Some "CTP-I-EMPTY"
          "integration: a defect with no owner", ig [ "integrations"; "0"; "defects"; "0"; "owner" ] (jstr ""), Some "CTP-I-DEFECT"
          "integration: persistence mode omitted", ig [ "integrations"; "0"; "persistence" ] (j "{}"), Some "CTP-I-PERSISTENCE"
          "integration: durable persistence on a REST double", ig [ "integrations"; "0"; "persistence" ] (j """{"mode":"durable-github","realProvider":{"runRef":"double","readBack":false}}"""), Some "CTP-I-REAL-PROVIDER"
          "integration: durable persistence with a real read-back", ig [ "integrations"; "0"; "persistence" ] (j """{"mode":"durable-github","realProvider":{"runRef":"42077412742","readBack":true}}"""), None
          "integration: a durable contract integrated as local", integration integ lifecycle (Some(j """{"contracts":[{"id":"INV-015","group":"CG-1","persistence":"durable-github"}]}""")), Some "CTP-I-REAL-PROVIDER"
          "ownership: disjoint owners pass", own """{"owners":{"domain":["src/Engine/**"],"ui":["src/Ui/**"]}}""" [ "src/Engine/A.fs"; "./src/Ui/B.fs" ], None
          "ownership: a shared file", own """{"owners":{"domain":["src/**"],"ui":["src/Ui/**"]}}""" [ "src/Ui/B.fs" ], Some "CTP-O-SHARED"
          "ownership: an unowned file", own """{"owners":{"domain":["src/Engine/**"]}}""" [ "web/index.html" ], Some "CTP-O-UNOWNED"
          "ownership: a dotfile keeps its name", own """{"owners":{"coordinator":[".gitignore"]}}""" [ ".gitignore" ], None
          "ownership: an empty file list", own """{"owners":{"domain":["src/**"],"ui":["src/**"]}}""" [], Some "CTP-O-FILES" ]
    let syntheticResults =
        synthetic |> List.map (fun (name, got, expect) ->
            let ok = match expect with None -> Set.isEmpty got | Some c -> got.Contains c
            name, ok, (if ok then "" else $"expected {expect}, got {Set.toList got}"))

    // ---- EXP-INDY-002 replay: the real files, committed as fixtures; their SHA-256 is pinned HERE, so editing a
    // fixture without editing this file fails, in every clone (shallow included). When PR #140's commits are present
    // the fixtures are also compared with `git show`; without them that comparison is reported NOT-RUN, never PASS.
    let fx = Path.Combine(root, "docs/execution/contract-triggered-parallel/evidence/exp-002")
    let d = "docs/experiments/exp-indy-002"
    let fixtures =
        [ "registry.d544ead.json", "2b943fb5561eb443ddae8520b0e2121bb95ba506633579c18e2c9c39ccb47f1e", Some("d544ead", $"{d}/CONTRACT-REGISTRY.json")
          "registry.99824ee.json", "ed9c6e9d188cd1a2512a2f3a21441437a1079c5d22a8731092f7da80cacc9484", Some("99824ee", $"{d}/CONTRACT-REGISTRY.json")
          "5151cdf.CONTRACT-LIFECYCLE.jsonl", "709551a8cd90255eb0907f675bd6b3ba2b55c7b93db3e59886518c1180e7659a", Some("5151cdf", $"{d}/CONTRACT-LIFECYCLE.jsonl")
          "5151cdf.INTEGRATION-RESULTS.json", "c8f4c70aab64b9e46acb5e21ed72f3d8d66e554660495371cd25db99ef255a76", Some("5151cdf", $"{d}/INTEGRATION-RESULTS.json")
          "5151cdf.CI-RUNS.json", "b6a0363e746c253e51cc4c2f4520994698b913cf51eb1353ff63610873a32229", Some("5151cdf", $"{d}/CI-RUNS.json")
          "files.5151cdf.txt", "e67219287d2cc75aadfa297a08bad9335209f03aaefb6d0bb43bf6af59cf1906", None
          "ownership.json", "a6945bfe6a00e921ff7953775fd266633c3ea7874ace568161f1337640118017", None ]
    let sha (b: byte[]) = Security.Cryptography.SHA256.HashData b |> Convert.ToHexString |> fun h -> h.ToLowerInvariant()
    let bytesFx (name: string) = let p = Path.Combine(fx, name) in if File.Exists p then Some(File.ReadAllBytes p) else None
    let readFx (name: string) = bytesFx name |> Option.map Text.Encoding.UTF8.GetString
    let pinOk (b: byte[]) (pinned: string) = sha b = pinned
    let integrity =
        (fixtures |> List.map (fun (name, pinned, _) ->
            match bytesFx name with
            | Some b -> $"EXP-002 fixture {name} has its pinned sha256", pinOk b pinned, (if pinOk b pinned then "" else $"sha256 {sha b}")
            | None -> $"EXP-002 fixture {name} is missing", false, ""))
        @ [ "EXP-002 control: a fixture changed by one byte fails its pin",
            (match fixtures, bytesFx "5151cdf.CI-RUNS.json" with
             | _, Some b -> not (pinOk (Array.append b [| 32uy |]) "b6a0363e746c253e51cc4c2f4520994698b913cf51eb1353ff63610873a32229")
             | _ -> false), "" ]
    let provenance =
        fixtures |> List.choose (fun (name, _, src) -> src |> Option.map (fun (commit, path) -> name, commit, path))
        |> List.map (fun (name, commit, path) ->
            match git root $"show {commit}:{path}", readFx name with
            | Some fromGit, Some local -> Some(($"EXP-002 fixture {name} equals {commit}:{path}", (fromGit = local), ""))
            | _ -> None)
    let notRun = provenance |> List.filter Option.isNone |> List.length
    let exp =
        match readFx "registry.d544ead.json", readFx "registry.99824ee.json", readFx "5151cdf.CONTRACT-LIFECYCLE.jsonl", readFx "5151cdf.CI-RUNS.json", readFx "files.5151cdf.txt", readFx "ownership.json" with
        | Some v1, Some final, Some lifeTxt, Some ciTxt, Some files, Some owners ->
            let atV1 = checkRegistry (fromExp002 (JsonNode.Parse v1))
            let atFinal = checkRegistry (fromExp002 (JsonNode.Parse final))
            let lifeRows = parseLines lifeTxt
            let lf = checkLifecycle lifeRows
            let ci = ciReport (JsonNode.Parse ciTxt)
            let has (fs: Finding list) subject rule = (codesFor fs subject).Contains rule
            let flagged (fs: Finding list) rule = fs |> List.filter (fun f -> f.Rule = rule) |> List.map (fun f -> f.Subject) |> Set.ofList
            let count (fs: Finding list) rule = fs |> List.filter (fun f -> f.Rule = rule) |> List.length
            let withTransitions (txt: string) = arr "contracts" (JsonNode.Parse txt) |> List.filter (fun c -> not (blank (str "transition" c))) |> List.map (str "id") |> Set.ofList
            let cr1 = set [ "INV-009"; "INV-013"; "INV-014"; "INV-030"; "INV-034" ]
            [ // CR-1: contract level, the rule flags exactly the five contracts that list several UI actions under one label
              yield "EXP-002 CR-1: at v1 the control-name rule flags exactly INV-009/-013/-014/-030/-034 (no other contract)", flagged atV1 "CTP-C-CONTROL-NAME" = cr1, ""
              yield "EXP-002 CR-1: at v1 it reports 8 unnamed controls (CR-1 changed 13 names)", count atV1 "CTP-C-CONTROL-NAME" = 8, ""
              yield "EXP-002 control: after CR-1 (99824ee) the rule flags no contract", flagged atFinal "CTP-C-CONTROL-NAME" |> Set.isEmpty, ""
              // the honest misses
              yield "EXP-002 CR-1 miss: INV-039 not flagged (its 'New investigation' control was absent from the registry)", not (has atV1 "INV-039" "CTP-C-CONTROL-NAME"), ""
              yield "EXP-002 CR-1 miss: INV-040 not flagged (its control was listed with a wrong name; correctness is not checkable)", not (has atV1 "INV-040" "CTP-C-CONTROL-NAME"), ""
              // CR-2
              yield "EXP-002 CR-2: INV-030 nested fields flagged at v1", has atV1 "INV-030" "CTP-C-NESTED-LABEL", ""
              yield "EXP-002 CR-2: INV-031 option labels flagged at v1", has atV1 "INV-031" "CTP-C-OPTION-LABEL", ""
              yield "EXP-002 control: INV-030/031 labelled after CR-2", not (has atFinal "INV-030" "CTP-C-NESTED-LABEL") && not (has atFinal "INV-031" "CTP-C-OPTION-LABEL"), ""
              // CR-3 and D-3: FORMAT-LEVEL only. EXP-002's registry has no structured transitions or acceptance obligations,
              // so the rules refuse every transition, before and after the fixes. This shows the format gap; it is NOT a
              // targeted detection of CR-3 or D-3 and has no falsifying control in EXP-002's data.
              yield "EXP-002 format: every transition is unstructured at v1 and at final (format gap, not a CR-3 detection)",
                    flagged atV1 "CTP-C-TRANSITION-STRUCTURE" = withTransitions v1 && flagged atFinal "CTP-C-TRANSITION-STRUCTURE" = withTransitions final, ""
              yield "EXP-002 format: no transition carries an acceptance obligation, at v1 and at final (format gap, not a D-3 detection)",
                    flagged atV1 "CTP-C-TRANSITION-TEST" = withTransitions v1 && flagged atFinal "CTP-C-TRANSITION-TEST" = withTransitions final, ""
              // lifecycle findings in the real log
              yield "EXP-002 lifecycle: CG-1-START v2 released without DEFINED/VALIDATED", lf |> List.exists (fun f -> f.Rule = "CTP-L-ORDER" && f.Subject = "CG-1-START v2"), ""
              yield "EXP-002 lifecycle: revisions carry no change-request id", lf |> List.exists (fun f -> f.Rule = "CTP-L-CHANGE-REQUEST" && f.Subject = "CG-5-PERSISTENCE v3"), ""
              yield "EXP-002 lifecycle: IMPLEMENTING and INTEGRATION_READY recorded by 'verification' (combined roles)",
                    lf |> List.exists (fun f -> f.Rule = "CTP-L-ROLE" && f.Detail.StartsWith "IMPLEMENTING recorded by 'verification'")
                    && lf |> List.exists (fun f -> f.Rule = "CTP-L-ROLE" && f.Detail.StartsWith "INTEGRATION_READY recorded by 'verification'"), ""
              yield "EXP-002 lifecycle: no group reached VERIFIED", lifeRows |> List.forall (fun r -> str "status" r <> "VERIFIED"), ""
              // ownership over the experiment's 37 files
              yield "EXP-002 ownership: 37 files, none unowned or shared",
                    (let fs = files.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray in fs.Length = 37 && checkOwnership (JsonNode.Parse owners) fs |> List.isEmpty), ""
              // CI: observed cancellations only (99824ee's "superseded" entry is a prediction; CI-F3 is outside `runs`)
              yield "EXP-002 CI: exactly 2 observed cancelled Preflight runs in the run log", ci.Length = 2, $"{ci.Length}" ]
        | _ -> [ "EXP-002 replay: fixtures missing", false, "" ]
    let all = syntheticResults @ integrity @ (provenance |> List.choose id) @ exp
    all |> List.iter (fun (n, ok, why) -> printfn "%s  %s%s" (if ok then "PASS" else "FAIL") n (if blank why || ok then "" else "  (" + why + ")"))
    if notRun > 0 then printfn "NOT-RUN  %d fixture(s) not compared with git: PR #140's commits are not in this clone (the pinned sha256 still holds)" notRun
    let failed = all |> List.filter (fun (_, ok, _) -> not ok) |> List.length
    printfn "%d/%d cases pass%s" (all.Length - failed) all.Length (if notRun > 0 then $", {notRun} not run" else "")
    if failed > 0 then 1 else 0

// =====================================================================================================================
let rec opt (k: string) = function | a :: v :: _ when a = k -> Some v | _ :: t -> opt k t | [] -> None
let need k = opt k args |> Option.defaultWith (fun () -> eprintfn "missing %s" k; exit 2)
let read (p: string) = File.ReadAllText p
let report (fs: Finding list) =
    fs |> List.iter (fun f -> printfn "%s %s %s: %s" (if f.Severity = Error then "ERROR" else "WARN ") f.Rule f.Subject f.Detail)
    let errors = fs |> List.filter (fun f -> f.Severity = Error) |> List.length
    printfn "%d error(s), %d warning(s)" errors (fs.Length - errors)
    if errors > 0 then 1 else 0

let code =
    match args |> List.tryHead with
    | Some "registry" -> report (checkRegistry (JsonNode.Parse(read (need "--registry"))))
    | Some "lifecycle" -> report (checkLifecycle (parseLines (read (need "--log"))))
    | Some "integration" ->
        report (checkIntegration (JsonNode.Parse(read (need "--integration"))) (parseLines (read (need "--log"))) (opt "--registry" args |> Option.map (read >> JsonNode.Parse)))
    | Some "ownership" -> report (checkOwnership (JsonNode.Parse(read (need "--ownership"))) (File.ReadAllLines(need "--files") |> Array.filter (blank >> not) |> List.ofArray))
    | Some "ci" -> (report (ciReport (JsonNode.Parse(read (need "--runs"))))) |> ignore; 0
    | Some "test" -> tests (opt "--indy-init" args |> Option.defaultValue ".")
    | _ -> eprintfn "usage: ctp-check.fsx -- (registry|lifecycle|integration|ownership|ci|test) ..."; 2
exit code
