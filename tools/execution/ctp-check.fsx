// ctp-check.fsx: execution-readiness checks for CTP-001, contract-triggered parallel execution
// (docs/execution/contract-triggered-parallel/PROTOCOL.md). F# only, no dependencies beyond the .NET SDK.
//
//   dotnet fsi tools/execution/ctp-check.fsx -- registry    --registry FILE
//   dotnet fsi tools/execution/ctp-check.fsx -- lifecycle   --log FILE [--registry FILE]
//   dotnet fsi tools/execution/ctp-check.fsx -- integration --integration FILE --log FILE [--registry FILE]
//   dotnet fsi tools/execution/ctp-check.fsx -- ownership   --ownership FILE --files FILE   (one path per line)
//   dotnet fsi tools/execution/ctp-check.fsx -- ci          --runs FILE
//   dotnet fsi tools/execution/ctp-check.fsx -- test        [--indy-init DIR]
//
// Every rule is a pure function from parsed input to a list of findings. A finding names its rule (CTP-*), its
// subject and why. Exit 1 when any finding is an ERROR. `ci` only reports (exit 0): it measures waste, it cannot
// prevent a push. `test` runs positive and negative cases, plus a replay of EXP-INDY-002's real registry, lifecycle,
// integration and CI files read from git at their historical commits (EXP-002-EVIDENCE.md, section 5).

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
    |> List.tryPick (fun r -> let m = Regex.Match(r, @"^one of (.+)$") in if m.Success then Some(m.Groups[1].Value.Split(',') |> Array.map (fun s -> s.Trim()) |> List.ofArray) else None)
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
                  err "CTP-C-TRANSITION-STRUCTURE" (id c) $"""transition '{str "raw" t}' must state from, event and to (and what the target state carries)"""
              let tests = strs "tests" t
              if not (tests |> List.exists isUnitOrUi) then err "CTP-C-TRANSITION-TEST" (id c) $"transition {name} has no UT-/UI- test obligation"
              if not (tests |> List.exists isAcceptance) then err "CTP-C-TRANSITION-TEST" (id c) $"transition {name} has no independent AT- acceptance obligation" ]
      // persistence expectation
      fun c ->
        let p = str "persistence" c
        [ if not (blank p) && not (persistenceModes.Contains p) then err "CTP-C-PERSISTENCE" (id c) $"persistence '{p}' is not one of none, local, durable-github"
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
let roleFor = Map.ofList [ "DEFINED", [ "coordinator" ]; "VALIDATED", [ "coordinator" ]; "RELEASED", [ "coordinator" ]
                           "IMPLEMENTING", [ "domain"; "ui" ]; "STAGED", [ "domain"; "ui" ]
                           "INTEGRATION_READY", [ "integration" ]; "VERIFIED", [ "integration" ] ]

let checkLifecycle (rows: JsonNode list) : Finding list =
    let key r = $"""{str "group" r} v{num "version" r |> Option.defaultValue 0}"""
    let byVersion = rows |> List.groupBy key
    let byGroup = rows |> List.groupBy (str "group")
    [ for r in rows do
          let s = str "status" r
          if s <> "BLOCKED" && (rank s).IsNone then err "CTP-L-STATE" (key r) $"unknown status {s}"
          match Map.tryFind s roleFor with
          | Some roles when not (List.contains (str "actor" r) roles) -> err "CTP-L-ROLE" (key r) $"""{s} recorded by '{str "actor" r}', expected {String.concat "/" roles}"""
          | _ -> ()
      for (k, rs) in byVersion do
          // immutability: one version, one hash
          let hashes = rs |> List.map (str "hash") |> List.distinct
          if hashes.Length > 1 then err "CTP-L-IMMUTABLE" k $"one version carries {hashes.Length} hashes; content changes need a new version"
          // ordering: no skipped state, no going back (BLOCKED is an annotation, not a state)
          let seen = rs |> List.map (str "status") |> List.filter ((<>) "BLOCKED") |> List.choose rank
          let firstMissing = seen |> List.tryPick (fun i -> if i > 0 && not (List.exists (fun j -> j = i - 1) seen) then Some states[i - 1] else None)
          match firstMissing with
          | Some m -> err "CTP-L-ORDER" k $"reached a later state without {m}"
          | None -> ()
          if seen <> (seen |> List.sort) then err "CTP-L-ORDER" k "states recorded out of order"
          // INTEGRATION_READY needs both sides staged, when both sides implement the group
          let stagedBy = rs |> List.filter (fun r -> str "status" r = "STAGED") |> List.map (str "actor") |> Set.ofList
          let implementedBy = rs |> List.filter (fun r -> List.contains (str "status" r) [ "IMPLEMENTING"; "STAGED" ]) |> List.map (str "actor") |> Set.ofList
          if rs |> List.exists (fun r -> str "status" r = "INTEGRATION_READY") && not (Set.isSubset implementedBy stagedBy) then
              err "CTP-L-STAGED" k "INTEGRATION_READY before every implementing side STAGED"
          // a revised version cites its change request
          match num "version" rs.Head with
          | Some v when v > 1 && rs |> List.exists (fun r -> str "status" r = "DEFINED" && blank (str "changeRequest" r)) ->
              err "CTP-L-CHANGE-REQUEST" k "a revised version must cite its change request (changeRequest: CR-n)"
          | _ -> ()
          // VERIFIED needs evidence: a candidate SHA with required CI passed and an independent acceptance run
          for r in rs |> List.filter (fun r -> str "status" r = "VERIFIED") do
              let ev = field "evidence" r
              let has k = ev |> Option.exists (fun e -> not (blank (str k e)))
              if not (has "candidateSha" && has "ciRun" && has "acceptanceRun") then
                  err "CTP-L-VERIFIED" k "VERIFIED needs evidence.candidateSha, evidence.ciRun (required CI passed) and evidence.acceptanceRun"
      // each group's versions start at 1 and rise by one
      for (g, rs) in byGroup do
          let vs = rs |> List.choose (num "version") |> List.distinct |> List.sort
          if vs <> [ 1 .. List.length vs ] then err "CTP-L-VERSION" g $"versions {vs} are not 1..n" ]

// =====================================================================================================================
// 3. Integration record ({integrations: [{id, groups:[{group, version, hash}], acceptance:{runRef, author, implementers,
//    passed, total}, defects:[{id, class, owner}], persistence:{mode, realProvider:{runRef, readBack}}}]})
// =====================================================================================================================
let defectClasses = set [ "incorrect-contract"; "domain-defect"; "ui-binding-defect"; "persistence-defect"; "environment-tooling"; "missing-mapping"; "agent-coordination" ]

let checkIntegration (record: JsonNode) (log: JsonNode list) : Finding list =
    let released = log |> List.filter (fun r -> str "status" r = "RELEASED")
    let latest g = released |> List.filter (fun r -> str "group" r = g) |> List.sortBy (fun r -> num "version" r |> Option.defaultValue 0) |> List.tryLast
    [ for i in arr "integrations" record do
          let name = str "id" i
          for g in arr "groups" i do
              match latest (str "group" g) with
              | None -> err "CTP-I-VERSION" name $"""{str "group" g} was never RELEASED"""
              | Some r when num "version" r <> num "version" g || str "hash" r <> str "hash" g ->
                  err "CTP-I-VERSION" name $"""{str "group" g} integrated at v{num "version" g |> Option.defaultValue 0} ({(str "hash" g).Substring(0, min 8 (str "hash" g).Length)}), latest release is v{num "version" r |> Option.defaultValue 0}"""
              | Some _ -> ()
          match field "acceptance" i with
          | None -> err "CTP-I-ACCEPTANCE" name "no independent browser acceptance run recorded for this integration"
          | Some a ->
              if blank (str "runRef" a) then err "CTP-I-ACCEPTANCE" name "acceptance.runRef: where the run's output is"
              if strs "implementers" a |> List.contains (str "author" a) then err "CTP-I-INDEPENDENT" name "the acceptance test's author implemented the code under test"
              if num "passed" a <> num "total" a then err "CTP-I-ACCEPTANCE" name "acceptance did not pass in full"
          for d in arr "defects" i do
              if not (defectClasses.Contains(str "class" d)) then err "CTP-I-DEFECT" (str "id" d) $"""class '{str "class" d}' is not a defect class"""
              if blank (str "owner" d) then err "CTP-I-DEFECT" (str "id" d) "defect has no owner"
          match field "persistence" i |> Option.map (str "mode") with
          | Some "durable-github" ->
              let rp = field "persistence" i |> Option.bind (field "realProvider")
              if rp |> Option.forall (fun p -> blank (str "runRef" p) || not (boolOr "readBack" p)) then
                  err "CTP-I-REAL-PROVIDER" name "durable GitHub persistence needs a real-provider write with an independent read-back (a REST double is component evidence only)"
          | _ -> () ]

// =====================================================================================================================
// 4. Ownership ({owners: {role: [globs]}}; files: one path per line)
// =====================================================================================================================
let globRx (g: string) = Regex("^" + (Regex.Escape g).Replace(@"\*\*/", "(.*/)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*") + "$")
let checkOwnership (ownership: JsonNode) (files: string list) : Finding list =
    let owners =
        match field "owners" ownership with
        | Some (:? JsonObject as o) -> o |> Seq.map (fun kv -> kv.Key, (try kv.Value.AsArray() |> Seq.map (fun v -> v.GetValue<string>()) |> List.ofSeq with _ -> [])) |> List.ofSeq
        | _ -> []
    [ if owners.IsEmpty then err "CTP-O-OWNERS" "ownership" "no owners"
      for f in files do
          match owners |> List.filter (fun (_, gs) -> gs |> List.exists (fun g -> (globRx g).IsMatch f)) |> List.map fst with
          | [] -> err "CTP-O-UNOWNED" f "no owner"
          | [ _ ] -> ()
          | many -> err "CTP-O-SHARED" f $"""owned by {String.Join(", ", many)}: shared files cause merge conflicts""" ]

// =====================================================================================================================
// 5. CI waste ({runs: [{workflow, head, conclusion}]}): measured, never a gate
// =====================================================================================================================
let ciReport (runs: JsonNode) : Finding list =
    arr "runs" runs
    |> List.filter (fun r -> (str "conclusion" r).StartsWith "cancelled" || (str "conclusion" r).StartsWith "superseded")
    |> List.map (fun r -> warn "CTP-CI-CANCELLED" (str "head" r) $"""{str "workflow" r} on {str "head" r} cancelled by a newer push (push once per candidate; see INTEGRATION-POLICY.md section 4)""")

// =====================================================================================================================
// EXP-INDY-002 adapter: maps that experiment's registry format onto the CTP schema field for field. It invents
// nothing: a uiAction listed without its own name stays unnamed; a free-text transition stays unstructured
// (raw); the registry's tests become unit tests and it held no acceptance obligations. EXP-002-EVIDENCE.md, sec. 5.
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

/// EXP-002's lifecycle rows use the actor "verification" for INTEGRATION_READY (the coordinator combined roles).
let lifecycleFromExp002 (rows: JsonNode list) = rows

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
    let good =
        j """{"schema":"indy-init.ctp-registry/1","contracts":[
          {"id":"INV-004","requirements":["INV-004"],"kind":"action","group":"CG-1","command":"StartInvestigation",
           "fsharp":{"input":"StartInvestigationInput","output":"InvestigationEvent list"},
           "controls":[{"uiAction":"SubmitStart","role":"button","accessibleName":"Start investigation"}],
           "transitions":[{"from":"none","event":"InvestigationStarted","to":"Open; save state Unsaved","carries":["id"],"tests":["UT-INV-004","UI-INV-004","AT-08"]}],
           "persistence":"none","tests":{"unit":["UT-INV-004"],"ui":["UI-INV-004"],"acceptance":["AT-08"]}},
          {"id":"INV-031","requirements":["INV-031"],"kind":"field","group":"CG-4","label":"Relationship",
           "fsharp":{"record":"RelationshipInput","property":"Kind","type":"RelationshipKind option"},
           "options":[{"value":"supports","label":"Supports"},{"value":"contradicts","label":"Contradicts"}],
           "validation":[{"rule":"one of supports, contradicts","error":"Invalid [INV-031]"}],
           "tests":{"unit":["UT-INV-031"],"acceptance":["AT-11"]}}]}"""
    let edit (path: string list) (v: JsonNode) (n: JsonNode) =
        let c = n.DeepClone()
        let rec go (node: JsonNode) (p: string list) =
            match p with
            | [ k ] -> (match Int32.TryParse(k: string) with | true, i -> node.AsArray()[i] <- v | _ -> node.AsObject()[k] <- v)
            | k :: rest -> go (match Int32.TryParse(k: string) with | true, i -> node.AsArray()[i] | _ -> node.AsObject()[k]) rest
            | [] -> ()
        go c path; c
    let jstr (s: string) : JsonNode = JsonValue.Create s
    let lifecycle =
        parseLines """{"group":"CG-1","version":1,"status":"DEFINED","hash":"a","actor":"coordinator"}
{"group":"CG-1","version":1,"status":"VALIDATED","hash":"a","actor":"coordinator"}
{"group":"CG-1","version":1,"status":"RELEASED","hash":"a","actor":"coordinator"}
{"group":"CG-1","version":1,"status":"IMPLEMENTING","hash":"a","actor":"domain"}
{"group":"CG-1","version":1,"status":"IMPLEMENTING","hash":"a","actor":"ui"}
{"group":"CG-1","version":1,"status":"STAGED","hash":"a","actor":"domain"}
{"group":"CG-1","version":1,"status":"STAGED","hash":"a","actor":"ui"}
{"group":"CG-1","version":1,"status":"INTEGRATION_READY","hash":"a","actor":"integration"}
{"group":"CG-1","version":1,"status":"VERIFIED","hash":"a","actor":"integration","evidence":{"candidateSha":"abc","ciRun":"42","acceptanceRun":"at.json"}}"""
    let integ =
        j """{"integrations":[{"id":"INT-1","groups":[{"group":"CG-1","version":1,"hash":"a"}],
            "acceptance":{"runRef":"acceptance-run.json","author":"verification","implementers":["domain","ui"],"passed":22,"total":22},
            "defects":[{"id":"D-3","class":"ui-binding-defect","owner":"ui"}],"persistence":{"mode":"local"}}]}"""
    let reg f = checkRegistry f |> List.map (fun x -> x.Rule) |> Set.ofList
    let life rows = checkLifecycle rows |> List.map (fun x -> x.Rule) |> Set.ofList
    let integration r l = checkIntegration r l |> List.map (fun x -> x.Rule) |> Set.ofList
    let drop (status: string) (actor: string) = lifecycle |> List.filter (fun r -> not (str "status" r = status && str "actor" r = actor))
    let synthetic : (string * Set<string> * string option) list =
        [ "registry: complete contracts pass", reg good, None
          "registry: an unnamed secondary control (CR-1)", reg (good |> edit [ "contracts"; "0"; "controls"; "0"; "accessibleName" ] (jstr "")), Some "CTP-C-CONTROL-NAME"
          "registry: an unlabelled option (CR-2)", reg (good |> edit [ "contracts"; "1"; "options"; "1"; "label" ] (jstr "")), Some "CTP-C-OPTION-LABEL"
          "registry: a free-text transition (CR-3)", reg (good |> edit [ "contracts"; "0"; "transitions"; "0"; "event" ] (jstr "")), Some "CTP-C-TRANSITION-STRUCTURE"
          "registry: a transition without an acceptance test (D-3)", reg (good |> edit [ "contracts"; "0"; "transitions"; "0"; "tests" ] (j """["UT-INV-004"]""")), Some "CTP-C-TRANSITION-TEST"
          "registry: a transition without an implementer test", reg (good |> edit [ "contracts"; "0"; "transitions"; "0"; "tests" ] (j """["AT-08"]""")), Some "CTP-C-TRANSITION-TEST"
          "registry: validation without error behaviour", reg (good |> edit [ "contracts"; "1"; "validation"; "0"; "error" ] (jstr "")), Some "CTP-C-ERROR"
          "registry: a field without its F# type", reg (good |> edit [ "contracts"; "1"; "fsharp"; "type" ] (jstr "")), Some "CTP-C-FSHARP"
          "registry: a field without an accessible name", reg (good |> edit [ "contracts"; "1"; "label" ] (jstr "")), Some "CTP-C-LABEL"
          "registry: no acceptance obligation", reg (good |> edit [ "contracts"; "1"; "tests"; "acceptance" ] (j "[]")), Some "CTP-C-TESTS"
          "registry: durable persistence without real-provider evidence", reg (good |> edit [ "contracts"; "0"; "persistence" ] (jstr "durable-github")), Some "CTP-C-PERSISTENCE"
          "registry: no requirement ID", reg (good |> edit [ "contracts"; "1"; "requirements" ] (j "[]")), Some "CTP-C-REQ"
          "registry: a nested field without a label", reg (good |> edit [ "contracts"; "1"; "fields" ] (j """[{"record":"R","property":"EvidenceId","type":"string","component":"select"}]""")), Some "CTP-C-NESTED-LABEL"
          "lifecycle: complete lifecycle passes", life lifecycle, None
          "lifecycle: released without validation", life (drop "VALIDATED" "coordinator"), Some "CTP-L-ORDER"
          "lifecycle: integration-ready before the UI staged", life (drop "STAGED" "ui"), Some "CTP-L-STAGED"
          "lifecycle: one version, two hashes", life (lifecycle @ parseLines """{"group":"CG-1","version":1,"status":"VERIFIED","hash":"b","actor":"integration","evidence":{"candidateSha":"x","ciRun":"1","acceptanceRun":"y"}}"""), Some "CTP-L-IMMUTABLE"
          "lifecycle: a revision without its change request", life (lifecycle @ parseLines """{"group":"CG-1","version":2,"status":"DEFINED","hash":"c","actor":"coordinator"}"""), Some "CTP-L-CHANGE-REQUEST"
          "lifecycle: a revision with its change request", life (lifecycle @ parseLines """{"group":"CG-1","version":2,"status":"DEFINED","hash":"c","actor":"coordinator","changeRequest":"CR-3"}"""), None
          "lifecycle: VERIFIED without CI evidence", life (lifecycle |> List.map (fun r -> if str "status" r = "VERIFIED" then r |> edit [ "evidence" ] (j "{}") else r)), Some "CTP-L-VERIFIED"
          "lifecycle: an implementer releasing a contract", life (lifecycle |> List.map (fun r -> if str "status" r = "RELEASED" then r |> edit [ "actor" ] (jstr "ui") else r)), Some "CTP-L-ROLE"
          "integration: matching versions and full acceptance pass", integration integ lifecycle, None
          "integration: a stale contract version", integration (integ |> edit [ "integrations"; "0"; "groups"; "0"; "hash" ] (jstr "old")) lifecycle, Some "CTP-I-VERSION"
          "integration: no browser acceptance", integration (integ |> edit [ "integrations"; "0"; "acceptance" ] null) lifecycle, Some "CTP-I-ACCEPTANCE"
          "integration: self-attested acceptance", integration (integ |> edit [ "integrations"; "0"; "acceptance"; "author" ] (jstr "ui")) lifecycle, Some "CTP-I-INDEPENDENT"
          "integration: a defect with no owner", integration (integ |> edit [ "integrations"; "0"; "defects"; "0"; "owner" ] (jstr "")) lifecycle, Some "CTP-I-DEFECT"
          "integration: durable persistence on a REST double", integration (integ |> edit [ "integrations"; "0"; "persistence" ] (j """{"mode":"durable-github","realProvider":{"runRef":"double","readBack":false}}""")) lifecycle, Some "CTP-I-REAL-PROVIDER"
          "integration: durable persistence with a real read-back", integration (integ |> edit [ "integrations"; "0"; "persistence" ] (j """{"mode":"durable-github","realProvider":{"runRef":"42077412742","readBack":true}}""")) lifecycle, None
          "ownership: disjoint owners pass", checkOwnership (j """{"owners":{"domain":["src/Engine/**"],"ui":["src/Ui/**"]}}""") [ "src/Engine/A.fs"; "src/Ui/B.fs" ] |> List.map (fun f -> f.Rule) |> Set.ofList, None
          "ownership: a shared file", checkOwnership (j """{"owners":{"domain":["src/**"],"ui":["src/Ui/**"]}}""") [ "src/Ui/B.fs" ] |> List.map (fun f -> f.Rule) |> Set.ofList, Some "CTP-O-SHARED"
          "ownership: an unowned file", checkOwnership (j """{"owners":{"domain":["src/Engine/**"]}}""") [ "web/index.html" ] |> List.map (fun f -> f.Rule) |> Set.ofList, Some "CTP-O-UNOWNED" ]
    let syntheticResults =
        synthetic |> List.map (fun (name, got, expect) ->
            let ok = match expect with None -> Set.isEmpty got | Some c -> got.Contains c
            name, ok, (if ok then "" else $"expected {expect}, got {Set.toList got}"))

    // ---- EXP-INDY-002 replay: the real files, committed as fixtures with their source commits ----
    // docs/execution/contract-triggered-parallel/evidence/exp-002/<commit-or-name>; each fixture is byte-compared with
    // `git show <commit>:<path>` when those commits are in the clone (PR #140's branch), else that check is NOT-RUN.
    let fx = Path.Combine(root, "docs/execution/contract-triggered-parallel/evidence/exp-002")
    let d = "docs/experiments/exp-indy-002"
    let fixtures = [ "registry.d544ead.json", "d544ead", $"{d}/CONTRACT-REGISTRY.json"; "registry.99824ee.json", "99824ee", $"{d}/CONTRACT-REGISTRY.json"
                     "5151cdf.CONTRACT-LIFECYCLE.jsonl", "5151cdf", $"{d}/CONTRACT-LIFECYCLE.jsonl"; "5151cdf.INTEGRATION-RESULTS.json", "5151cdf", $"{d}/INTEGRATION-RESULTS.json"
                     "5151cdf.CI-RUNS.json", "5151cdf", $"{d}/CI-RUNS.json" ]
    let readFx (name: string) = let p = Path.Combine(fx, name) in if File.Exists p then Some(File.ReadAllText p) else None
    let provenance =
        fixtures |> List.map (fun (name, commit, path) ->
            match git root $"show {commit}:{path}", readFx name with
            | Some fromGit, Some local -> $"EXP-002 fixture {name} equals {commit}:{path}", fromGit = local, ""
            | None, Some _ -> $"EXP-002 fixture {name}: NOT-RUN provenance check ({commit} not in this clone; fetch PR #140's branch to re-verify)", true, ""
            | _, None -> $"EXP-002 fixture {name} is missing", false, "")
    let exp =
        match readFx "registry.d544ead.json", readFx "registry.99824ee.json", readFx "5151cdf.CONTRACT-LIFECYCLE.jsonl", readFx "5151cdf.INTEGRATION-RESULTS.json", readFx "5151cdf.CI-RUNS.json" with
        | Some v1, Some final, Some lifeTxt, Some _, Some ciTxt ->
            let atV1 = checkRegistry (fromExp002 (JsonNode.Parse v1))
            let atFinal = checkRegistry (fromExp002 (JsonNode.Parse final))
            let lifeRows = parseLines lifeTxt
            let lf = checkLifecycle lifeRows
            let ci = ciReport (JsonNode.Parse ciTxt)
            let has (fs: Finding list) subject rule = (codesFor fs subject).Contains rule
            let lifeHas rule subject = lf |> List.exists (fun f -> f.Rule = rule && f.Subject = subject)
            [ // CR-1: secondary controls without accessible names, at the v1 release (d544ead)
              for i in [ "INV-009"; "INV-013"; "INV-014"; "INV-030"; "INV-034" ] do
                  yield $"EXP-002 CR-1: {i} flagged at release v1", has atV1 i "CTP-C-CONTROL-NAME", ""
              // the honest misses: a control the registry never listed cannot be found unnamed
              for i in [ "INV-039"; "INV-040" ] do
                  yield $"EXP-002 CR-1: {i} NOT flagged at v1 (stated gap: the control was absent, not unnamed)", not (has atV1 i "CTP-C-CONTROL-NAME"), ""
              // control: after CR-1/CR-2 the same contracts pass the naming rules (the adapter does not always fail)
              for i in [ "INV-009"; "INV-013"; "INV-014"; "INV-030"; "INV-034" ] do
                  yield $"EXP-002 control: {i} names complete after CR-1 (99824ee)", not (has atFinal i "CTP-C-CONTROL-NAME"), ""
              // CR-2: nested-field labels and option labels
              yield "EXP-002 CR-2: INV-030 nested fields flagged at v1", has atV1 "INV-030" "CTP-C-NESTED-LABEL", ""
              yield "EXP-002 CR-2: INV-031 option labels flagged at v1", has atV1 "INV-031" "CTP-C-OPTION-LABEL", ""
              yield "EXP-002 control: INV-030/031 labelled after CR-2", not (has atFinal "INV-030" "CTP-C-NESTED-LABEL") && not (has atFinal "INV-031" "CTP-C-OPTION-LABEL"), ""
              // CR-3 and D-3: unstructured transitions and missing acceptance obligations, at v1
              yield "EXP-002 CR-3: INV-015 save transition flagged as unstructured at v1", has atV1 "INV-015" "CTP-C-TRANSITION-STRUCTURE", ""
              yield "EXP-002 D-3: INV-004 start transition has no independent acceptance obligation at v1", has atV1 "INV-004" "CTP-C-TRANSITION-TEST", ""
              // lifecycle findings in the real log
              yield "EXP-002 lifecycle: CG-1-START v2 released without DEFINED/VALIDATED", lifeHas "CTP-L-ORDER" "CG-1-START v2", ""
              yield "EXP-002 lifecycle: revisions carry no change-request field", lifeHas "CTP-L-CHANGE-REQUEST" "CG-5-PERSISTENCE v3", ""
              yield "EXP-002 lifecycle: no group reached VERIFIED", lifeRows |> List.forall (fun r -> str "status" r <> "VERIFIED"), ""
              // CI waste measured, not prevented
              yield $"EXP-002 CI: {ci.Length} cancelled or superseded Preflight runs reported", ci.Length >= 3, "" ]
        | _ -> [ "EXP-002 replay: fixtures missing", false, "" ]
    let all = syntheticResults @ provenance @ exp
    all |> List.iter (fun (n, ok, why) -> printfn "%s  %s%s" (if ok then "PASS" else "FAIL") n (if blank why then "" else "  (" + why + ")"))
    let failed = all |> List.filter (fun (_, ok, _) -> not ok) |> List.length
    printfn "%d/%d cases pass" (all.Length - failed) all.Length
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
    | Some "integration" -> report (checkIntegration (JsonNode.Parse(read (need "--integration"))) (parseLines (read (need "--log"))))
    | Some "ownership" -> report (checkOwnership (JsonNode.Parse(read (need "--ownership"))) (File.ReadAllLines(need "--files") |> Array.filter (blank >> not) |> List.ofArray))
    | Some "ci" -> (report (ciReport (JsonNode.Parse(read (need "--runs"))))) |> ignore; 0
    | Some "test" -> tests (opt "--indy-init" args |> Option.defaultValue ".")
    | _ -> eprintfn "usage: ctp-check.fsx -- (registry|lifecycle|integration|ownership|ci|test) ..."; 2
exit code
