/// Compiled package v4.1 (Indy Init event): the implementation-efficiency layer over package v4.
///
/// v4.1 adds no requirement, obligation or test binding: the v4 verification plan is used byte for byte, and the
/// preservation check proves it. What v4.1 adds is the material an implementing team would otherwise negotiate during
/// the timed build: frozen interface contracts (typechecked F#) whose machine types equal the v4 state machines, one
/// owner per file (including every contract, state-machine and unit test file), a decision table for every state
/// machine with reviewed guard partitions, a GitHub error classification, a Forma layout contract checked against the
/// published Forma release, a verification execution plan that shares setup without merging assertions, and a schedule
/// recomputed from interface-first dependencies. Every number in the schedule is an estimate; nothing here is measured.
module V41Package

open System
open System.Text.RegularExpressions
open Json
open Model

type Inputs =
    { /// The v4 verification plan, bytes and parsed (used unchanged).
      PlanBytes: byte[]
      Plan: Json
      /// The v4 freeze manifest: the plan digests and counts v4.1 must preserve.
      BaseManifest: Json
      Baseline: Json
      StateMachines: Json
      Tasks: WorkTask list
      Policy: Json
      /// The authored v4.1 inputs (requirements-compiled/v4.1/analysis).
      Interfaces: Json
      Structure: Json
      ErrorMatrix: Json
      Forma: Json
      Adjustments: Json
      Telemetry: Json
      GuardPartitions: Json
      /// Owner-approved amendments of v4's state machines (analysis/state-machine-amendments.json). v4.1 applies them
      /// over the frozen v4 machines; v4's files are never edited.
      MachineAmendments: Json
      /// The successor amendment record (analysis/amendment.json, v4.1.1 and later). None for v4.1 itself, whose
      /// generated output stays byte for byte as the owner approved it; every v4.1.1 behaviour below is gated on it.
      Amendment: Json option
      /// The committed Forma inventory, and the one extracted from the published package when it is supplied.
      FormaInventory: Json
      FormaExtract: Json option
      /// Every authored input and document as text, for the provenance check and the package manifest.
      AuthoredText: (string * string) list
      /// Reads a file of the source checkout (for literals cited from requirement documents).
      ReadSource: string -> string option }

type Check = { Id: string; Title: string; Problems: string list }

/// The amendment's list under a key; empty for v4.1 itself.
let amendmentList (key: string) (i: Inputs) = i.Amendment |> Option.map (arr key) |> Option.defaultValue []
let amendmentField (key: string) (i: Inputs) = i.Amendment |> Option.bind (field key) |> Option.defaultValue (JObj [])
/// The package version the compiler writes: 4.1.0, or the successor amendment's.
let packageVersion (i: Inputs) = i.Amendment |> Option.bind (strOpt "packageVersion") |> Option.defaultValue "4.1.0"

let private check id title problems = { Id = id; Title = title; Problems = problems }

let private sOr (key: string) (j: Json) = strOpt key j |> Option.defaultValue ""

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let private duplicates (xs: string list) = xs |> List.countBy id |> List.filter (fun (_, n) -> n > 1) |> List.map fst

let private starts (p: string) (s: string) = s.StartsWith(p, StringComparison.Ordinal)

/// The cases of the union `name` declared in an F# contract block.
let unionCases (name: string) (src: string) : string list =
    let m = Regex.Match(src, sprintf @"type %s =\s*\n((?:[ \t]*\|[^\n]*(?:\n|$))+)" (Regex.Escape name))
    Regex.Matches(m.Groups.[1].Value, @"\|\s*([A-Z][A-Za-z0-9]*)") |> Seq.map (fun x -> x.Groups.[1].Value) |> List.ofSeq

// ---- Forma inventory ---------------------------------------------------------------------------------------------

/// The published Forma surface an agent may use: the custom properties dist/all.css defines, the classes it styles,
/// the rem widths of its media queries, and the pattern ids of figma/component-contracts.json. Sorted and
/// de-duplicated, so the result is deterministic.
let formaInventory (version: string) (tarballSha256: string) (css: string) (contracts: Json) : Json =
    let distinct (rx: string) =
        Regex.Matches(css, rx) |> Seq.map (fun m -> m.Groups.[1].Value) |> Seq.distinct |> Seq.sort |> List.ofSeq
    let properties = distinct @"(--ef-[a-z0-9-]+)\s*:"
    // Hooks: properties the stylesheet reads (var(--ef-...)) but never defines, which authors set per instance
    // (the diagram's --ef-diagram-* geometry, for example). Setting a hook is using Forma, not inventing a token.
    let hooks = distinct @"var\(\s*(--ef-[a-z0-9-]+)" |> List.filter (fun p -> not (List.contains p properties))
    let classes = distinct @"\.(ef-[a-z0-9_-]+)"
    let patterns = arr "components" contracts |> List.map (str "id") |> List.distinct |> List.sort
    let breakpoints = distinct @"@media[^{]*?\(\s*(?:min|max)-width:\s*([0-9.]+rem)\s*\)"
    JObj
        [ "schema", JStr "indy-init.v41-forma-inventory/1"
          "description", JStr "The published Forma surface: custom properties defined by dist/all.css, classes it styles, and the pattern ids of figma/component-contracts.json. Generated by requirements-compiler v41 --forma-package from the npm tarball whose SHA-256 is recorded; an agent may not use a token, class or pattern that is not listed."
          "package", JStr "@echelon-foundry/design-system"
          "version", JStr version
          "tarballSha256", JStr tarballSha256
          "customProperties", jstrs properties
          "hookProperties", jstrs hooks
          "classes", jstrs classes
          "patterns", jstrs patterns
          "breakpoints", jstrs breakpoints
          "counts", JObj [ "customProperties", jint properties.Length; "hookProperties", jint hooks.Length; "classes", jint classes.Length; "patterns", jint patterns.Length; "breakpoints", jint breakpoints.Length ] ]

// ---- tests -------------------------------------------------------------------------------------------------------

type BoundTest = { Test: string; Kind: string; Requirement: string; Obligation: string; Categories: string list }

/// Every (obligation, test) binding of the plan, with the categories the acceptance contract assigns the test.
let boundTests (plan: Json) : BoundTest list =
    arr "requirements" plan
    |> List.collect (fun r ->
        arr "obligations" r
        |> List.collect (fun o ->
            let contract = field "acceptanceContract" o |> Option.defaultValue JNull
            let ti = field "testIdentity" contract |> Option.defaultValue JNull
            arr "tests" ti
            |> List.map (fun t ->
                { Test = str "id" t
                  Kind = sOr "kind" t
                  Requirement = str "requirement" r
                  Obligation = str "sourceObligation" o
                  Categories = strs "categories" t })))

let private allTestIds (plan: Json) = boundTests plan |> List.map (fun b -> b.Test) |> Set.ofList

/// The file a test lives in, by the test-identity conventions of the plan and of v4.1's contract, state-machine and
/// unit tests; None for records that are not code (Praxis process and review records).
let testFile (test: string) : string option =
    let m (rx: string) = let x = Regex.Match(test, rx) in if x.Success then Some x else None
    let g (x: Match) (n: int) = x.Groups.[n].Value
    [ @"^dotnet:Causa\.Acceptance\.(Deterministic|Live)\.([A-Za-z0-9]+)\.", (fun x -> sprintf "tests/Causa.Acceptance/%s/%s.fs" (g x 1) (g x 2))
      @"^dotnet:Causa\.Invariants\.([A-Za-z]+)\.(INV[0-9]+)\.", (fun x -> sprintf "tests/Causa.Invariants/%s/%s.fs" (g x 1) (g x 2))
      @"^dotnet:Causa\.Contract\.Tests\.([A-Za-z0-9]+)\.(Producer|Consumer[A-Za-z0-9]+)\.", (fun x -> sprintf "tests/Causa.Contract.Tests/%s/%s.fs" (g x 1) (g x 2))
      @"^dotnet:Causa\.([A-Za-z]+)\.Tests\.StateMachines\.([A-Za-z0-9]+)\.", (fun x -> sprintf "tests/Causa.%s.Tests/StateMachines/%s.fs" (g x 1) (g x 2))
      @"^dotnet:Causa\.(Domain|Persistence|App)\.Tests\.([A-Za-z0-9]+)(\.|$)", (fun x -> sprintf "tests/Causa.%s.Tests/%s.fs" (g x 1) (g x 2))
      @"^verifier:Causa\.Acceptance\.Independent\.([A-Za-z0-9]+)$", (fun x -> sprintf "verifier/Causa.Acceptance.Independent/%s.fs" (g x 1))
      @"^playwright:([a-z]+/[A-Za-z0-9-]+\.spec\.js) > ", (fun x -> sprintf "tests/browser/%s" (g x 1)) ]
    |> List.tryPick (fun (rx, f) -> m rx |> Option.map f)

let private capitalized (role: string) = role.Split('-') |> Array.map (fun p -> if p = "" then p else string (Char.ToUpperInvariant p.[0]) + p.Substring 1) |> String.concat ""

/// The role a consumer test belongs to: ConsumerUi2 is ui-2's; contract/view-ui-2.spec.js is ui-2's.
let consumerRole (consumers: string list) (test: string) : string option =
    let dotnet = Regex.Match(test, @"\.Consumer([A-Za-z0-9]+)\.")
    let browser = Regex.Match(test, @"^playwright:contract/[a-z]+-([a-z0-9-]+)\.spec\.js")
    if dotnet.Success then consumers |> List.tryFind (fun r -> capitalized r = dotnet.Groups.[1].Value)
    elif browser.Success then consumers |> List.tryFind ((=) browser.Groups.[1].Value)
    else None

/// The owner decision requests of v4.1 (proposals/owner-decision-requests.json), as (id, kind).
let private requests (i: Inputs) : (string * string) list =
    i.AuthoredText |> List.tryFind (fst >> (=) "proposals/owner-decision-requests.json")
    |> Option.map (snd >> Text.Encoding.UTF8.GetBytes >> parseBytes >> arr "requests" >> List.map (fun r -> str "id" r, sOr "kind" r)) |> Option.defaultValue []

let private requestIds (i: Inputs) = requests i |> List.map fst

/// Whether a decision is an owner decision request of the given kind.
let private requestOfKind (i: Inputs) (kind: string) (decision: string) = List.contains (decision, kind) (requests i)

/// The problems of the amendments themselves: each added transition, and each accepted machine left unchanged, names a
/// v4 machine, a state-machine-amendment request and the operator approval that records it; an added transition's
/// sources are baseline requirements. Whether a transition fits its machine (states, events, guards, Saved entered only
/// by a confirmation) is checked with the amended machine; that the approval selects exactly these transitions is
/// checked against the owner's record by the offline suite.
let amendmentProblems (i: Inputs) : string list =
    let v4Machines = arr "machines" i.StateMachines |> List.map (str "id")
    let requirementIds = arr "requirements" i.Baseline |> List.map (str "id") |> Set.ofList
    let cites (what: string) (x: Json) =
        [ if not (List.contains (sOr "machine" x) v4Machines) then yield sprintf "%s names machine %s, which v4 does not define" what (sOr "machine" x)
          if not (requestOfKind i "state-machine-amendment" (sOr "decision" x)) then yield sprintf "%s names decision '%s', which is not a state-machine-amendment request" what (sOr "decision" x)
          if String.IsNullOrWhiteSpace (sOr "approval" x) then yield sprintf "%s names no operator approval" what ]
    [ for t in arr "transitions" i.MachineAmendments do
          let what = sprintf "amendment %s" (sOr "id" t)
          yield! cites what t
          for s in strs "sources" t do
              if not (requirementIds.Contains s) then yield sprintf "%s: source %s is not a baseline requirement" what s
      for a in arr "accepted" i.MachineAmendments do
          yield! cites (sprintf "the accepted %s entry" (sOr "machine" a)) a ]

/// v4's state machines with v4.1's owner-approved amendments applied: each added transition joins its machine, marked
/// with its decision. No state or event is added, and v4's files are never edited (v4 is frozen at its tag).
let amendedMachines (i: Inputs) : Json list =
    let added = arr "transitions" i.MachineAmendments
    let transitionOf (t: Json) = match t with JObj ps -> JObj(ps |> List.filter (fun (k, _) -> k <> "machine" && k <> "approval")) | j -> j
    arr "machines" i.StateMachines
    |> List.map (fun m ->
        match added |> List.filter (fun t -> sOr "machine" t = str "id" m) with
        | [] -> m
        | ts -> setIn "transitions" (JArr(arr "transitions" m @ (ts |> List.map transitionOf))) m)

/// The state-machine test ids of a machine: one per transition and one for its illegal pairs, in the test project of
/// the machine's owner (analysis/project-structure.json stateMachines).
let private machineProject (i: Inputs) (machine: string) =
    arr "stateMachines" i.Structure |> List.tryFind (fun m -> sOr "machine" m = machine) |> Option.map (sOr "project") |> Option.defaultValue "Causa.Persistence.Tests"

let private machineTest (i: Inputs) (machine: string) (case: string) =
    sprintf "dotnet:%s.StateMachines.%s.%s" (machineProject i machine) (machine.Replace("-", "")) case

/// Tests v4.1 names outside the plan, with their owner: contract tests (producer side by the producer, consumer side
/// by each consumer), state-machine tests (by the machine's owner) and the unit tests source files require.
let namedTests (i: Inputs) : (string * string * string) list =
    let contract =
        arr "interfaces" i.Interfaces
        |> List.collect (fun x ->
            let consumers = strs "consumers" x
            (strs "producerTests" x |> List.map (fun t -> t, sOr "producer" x, "contract"))
            @ (strs "consumerTests" x |> List.map (fun t -> t, consumerRole consumers t |> Option.defaultValue "", "contract")))
    let machines =
        amendedMachines i
        |> List.collect (fun m ->
            let mid = str "id" m
            let owner = arr "stateMachines" i.Structure |> List.tryFind (fun s -> sOr "machine" s = mid) |> Option.map (sOr "owner") |> Option.defaultValue ""
            (machineTest i mid "EveryIllegalPairFailsClosed", owner, "state-machine")
            :: (arr "transitions" m |> List.map (fun t -> machineTest i mid (str "id" t), owner, "state-machine")))
    let required =
        arr "projects" i.Structure |> List.collect (arr "files") |> List.collect (fun f -> strs "requiredTests" f |> List.map (fun t -> t, str "owner" f, "unit"))
    // A required test that is also a contract test keeps the contract owner.
    let contractIds = contract |> List.map (fun (t, _, _) -> t) |> Set.ofList
    contract @ machines @ (required |> List.filter (fun (t, _, _) -> not (contractIds.Contains t)))

let private implementationRoles = [ "domain"; "persistence"; "ui" ]

/// The owner of a plan test file. Independent acceptance belongs to the reviewer; invariant tests to the domain lane
/// that writes them first (v4 T12), except live invariants (persistence) and browser invariants (ui). An acceptance
/// test belongs to the implementation role (domain, persistence, ui) of the first timed task covering its requirement,
/// moved to the lane a topology gives that task (C-role-lanes-two-ui: ui-2); else to integration if an integration
/// task covers it; else by requirement family.
let testOwner (tasks: WorkTask list) (policy: Json) (file: string) (requirement: string) : string =
    let covering = tasks |> List.filter (fun t -> t.Phase = "timed" && List.contains requirement t.Covers) |> List.sortBy (fun t -> t.Id)
    let laneOverride (t: WorkTask) =
        arr "topologies" policy
        |> List.tryPick (fun topo -> match field "laneOverrides" topo with Some(JObj ps) -> ps |> List.tryFind (fst >> (=) t.Id) |> Option.map (snd >> asStr) | _ -> None)
    if starts "verifier/" file then "reviewer"
    elif starts "tests/Causa.Invariants/Live/" file then "persistence"
    elif starts "tests/Causa.Invariants/" file then "domain"
    elif starts "tests/browser/invariants/" file then "ui"
    else
        match covering |> List.tryFind (fun t -> List.contains t.Owner implementationRoles) with
        | Some t -> laneOverride t |> Option.defaultValue t.Owner
        | None when covering |> List.exists (fun t -> t.Owner = "integration") -> "integration"
        | None when starts "PERSIST-" requirement -> "persistence"
        | None when starts "UI-" requirement || starts "WDL-" requirement -> "ui"
        | None -> "integration"

// ---- ownership manifest ------------------------------------------------------------------------------------------

type OwnedFile =
    { Path: string
      Owner: string
      Kind: string
      Project: string option
      DependsOn: string list
      Implements: string list
      Tests: string list
      Requirements: string list
      /// Owners claimed by other tests placed in the same file (a file with two owners).
      Conflicts: string list
      Contributors: string list }

let private ownedOf (kind: string) (project: string option) (j: Json) =
    { Path = str "path" j; Owner = str "owner" j; Kind = kind; Project = project; DependsOn = strs "dependsOn" j; Implements = strs "implements" j; Tests = []; Requirements = []; Conflicts = []; Contributors = strs "contributors" j }

let private projectOf (file: string) =
    match file.Split('/') |> List.ofArray with
    | "tests" :: "browser" :: _ -> None
    | "tests" :: p :: _ :: _ -> Some p
    | "verifier" :: p :: _ :: _ -> Some p
    | _ -> None

/// Every file of the event build with its single owner: the authored project files, shared files and test helpers,
/// and one generated file per test file named by the plan's bindings or by v4.1's contract, state-machine and unit
/// tests.
let ownedFiles (i: Inputs) : OwnedFile list =
    let projects = arr "projects" i.Structure
    let projectFile (p: Json) = ownedOf "project-file" None (JObj [ "path", JStr(str "path" p); "owner", JStr(str "owner" p) ])
    let sourceFiles = projects |> List.collect (fun p -> projectFile p :: (arr "files" p |> List.map (ownedOf "source" (Some(str "id" p)))))
    let testProjectFiles = arr "testProjects" i.Structure |> List.map projectFile
    let shared = arr "sharedFiles" i.Structure |> List.map (ownedOf "shared" None)
    let helpers = arr "testHelpers" i.Structure |> List.map (fun j -> ownedOf "test-helper" (projectOf (str "path" j)) j)
    let planTests =
        boundTests i.Plan
        |> List.choose (fun b -> testFile b.Test |> Option.map (fun f -> f, (b.Test, testOwner i.Tasks i.Policy f b.Requirement, Some b.Requirement)))
    let others = namedTests i |> List.choose (fun (t, owner, _) -> testFile t |> Option.map (fun f -> f, (t, owner, None)))
    let generated =
        planTests @ others
        |> List.groupBy fst
        |> List.sortBy fst
        |> List.map (fun (f, entries) ->
            let owners = entries |> List.map (fun (_, (_, o, _)) -> o) |> List.distinct
            { Path = f
              Owner = List.head owners
              Kind = "test"
              Project = projectOf f
              DependsOn = []
              Implements = []
              Tests = entries |> List.map (fun (_, (t, _, _)) -> t) |> List.distinct |> List.sort
              Requirements = entries |> List.choose (fun (_, (_, _, r)) -> r) |> List.distinct |> List.sort
              Conflicts = List.tail owners
              Contributors = [] })
    sourceFiles @ testProjectFiles @ shared @ helpers @ generated

/// The topologies that staff a role with a fallback (ui-2): those whose lane overrides name it.
let private ownerByTopology (i: Inputs) (owner: string) : (string * string) list =
    match arr "roles" i.Structure |> List.tryFind (fun r -> sOr "id" r = owner) |> Option.bind (strOpt "fallback") with
    | None -> []
    | Some fallback ->
        arr "topologies" i.Policy
        |> List.filter (fun t -> match field "laneOverrides" t with Some(JObj ps) -> not (ps |> List.exists (fun (_, v) -> asStr v = owner)) | _ -> true)
        |> List.map (fun t -> str "id" t, fallback)

let compileLists (i: Inputs) (files: OwnedFile list) : (string * string list) list =
    let sourceLists = arr "projects" i.Structure |> List.map (fun p -> str "id" p, files |> List.filter (fun f -> f.Kind = "source" && f.Project = Some(str "id" p)) |> List.map (fun f -> f.Path))
    let testLists =
        arr "testProjects" i.Structure
        |> List.map (fun p ->
            let mine = files |> List.filter (fun f -> f.Project = Some(str "id" p) && f.Path.EndsWith(".fs", StringComparison.Ordinal))
            // Helpers precede generated tests; each group keeps its declared or sorted order.
            str "id" p, (mine |> List.filter (fun f -> f.Kind = "test-helper") |> List.map (fun f -> f.Path)) @ (mine |> List.filter (fun f -> f.Kind = "test") |> List.map (fun f -> f.Path)))
    sourceLists @ testLists

let private ownershipJson (i: Inputs) (files: OwnedFile list) : Json =
    let fileJson (f: OwnedFile) =
        let byTopology = ownerByTopology i f.Owner
        JObj
            ([ "path", JStr f.Path; "owner", JStr f.Owner; "kind", JStr f.Kind ]
             @ (match f.Project with Some p -> [ "project", JStr p ] | None -> [])
             @ (if byTopology.IsEmpty then [] else [ "ownerByTopology", JObj(byTopology |> List.map (fun (t, o) -> t, JStr o)) ])
             @ (if f.Kind = "test" then [ "tests", jstrs f.Tests; "requirements", jstrs f.Requirements ] else [])
             @ (if f.Implements.IsEmpty then [] else [ "implements", jstrs f.Implements ]))
    let roles = arr "roles" i.Structure |> List.map (str "id")
    JObj
        [ "schema", JStr "indy-init.v41-agent-file-ownership/1"
          "description", JStr "One primary owner for every file of the event build. Generated by requirements-compiler v41 from analysis/project-structure.json, the v4 verification plan (one file per test file a bound test names) and v4.1's contract, state-machine and unit tests. A file owned by ui-2 belongs to ui in a topology without a ui-2 lane (ownerByTopology). A non-owner changes a file only through a pull request its owner approves; integration alone writes shared files and project files; every Compile list is written once, complete, at bootstrap."
          "roles", jstrs roles
          "rules", JArr(arr "rules" i.Structure)
          "counts", JObj(("files", jint files.Length) :: (roles |> List.map (fun r -> r, jint (files |> List.filter (fun f -> f.Owner = r) |> List.length))))
          "compileLists", JArr(compileLists i files |> List.map (fun (p, fs) -> JObj [ "project", JStr p; "files", jstrs fs ]))
          "files", JArr(files |> List.map fileJson) ]

// ---- state-machine contracts -------------------------------------------------------------------------------------

/// Every (state, event) pair of every v4 machine, with v4.1's owner-approved amendments: the transitions that may fire
/// (guard, next state, effects, a test case id each), or fail-closed behaviour. No state or event is added. Pairs with
/// several transitions must have exactly the guards reviewed in analysis/guard-partitions.json.
let stateMachineContracts (i: Inputs) : Json * string list =
    let policy = field "illegalInputPolicy" i.StateMachines |> Option.map (sOr "rule") |> Option.defaultValue ""
    let partitions = arr "groups" i.GuardPartitions
    let machine (m: Json) =
        let mid = str "id" m
        let states = arr "states" m |> List.map (str "id")
        let events = arr "events" m |> List.map (str "id")
        let transitions = arr "transitions" m
        let groups = transitions |> List.groupBy (fun t -> str "from" t, str "event" t) |> List.filter (fun (_, ts) -> ts.Length > 1)
        let problems =
            [ for t in transitions do
                  let tid = str "id" t
                  if not (List.contains (str "from" t) states) then yield sprintf "%s %s: from-state %s is not a state of the machine" mid tid (str "from" t)
                  if not (List.contains (str "to" t) states) then yield sprintf "%s %s: next state %s is not a state of the machine" mid tid (str "to" t)
                  if not (List.contains (str "event" t) events) then yield sprintf "%s %s: event %s is not an event of the machine" mid tid (str "event" t)
                  if (arr "sources" t).IsEmpty then yield sprintf "%s %s: no source requirement" mid tid
              for id in duplicates (transitions |> List.map (str "id")) do
                  yield sprintf "%s: transition id %s is used twice" mid id
              // Deterministic: guards are prose, so a pair with several transitions must match a reviewed partition.
              for ((s, e), ts) in groups do
                  let guards = ts |> List.map (sOr "guard") |> List.sort
                  if guards |> List.exists String.IsNullOrWhiteSpace then yield sprintf "%s (%s, %s): %d transitions and one has no guard" mid s e ts.Length
                  for g in duplicates guards do
                      yield sprintf "%s (%s, %s): guard '%s' selects more than one transition" mid s e g
                  match partitions |> List.filter (fun p -> sOr "machine" p = mid && sOr "state" p = s && sOr "event" p = e) with
                  | [ p ] ->
                      if List.sort (strs "guards" p) <> guards then yield sprintf "%s (%s, %s): guards [%s] differ from the reviewed partition [%s]" mid s e (String.concat "; " guards) (String.concat "; " (strs "guards" p))
                      if String.IsNullOrWhiteSpace (sOr "exclusiveBecause" p) then yield sprintf "%s (%s, %s): the partition does not say why its guards are exclusive" mid s e
                  | [] -> yield sprintf "%s (%s, %s): %d transitions with no reviewed guard partition" mid s e ts.Length
                  | _ -> yield sprintf "%s (%s, %s): more than one guard partition" mid s e
              for p in partitions |> List.filter (fun p -> sOr "machine" p = mid) do
                  if not (groups |> List.exists (fun ((s, e), _) -> s = sOr "state" p && e = sOr "event" p)) then
                      yield sprintf "%s (%s, %s): a guard partition names a pair without several transitions" mid (sOr "state" p) (sOr "event" p)
              let reachable =
                  let rec go (seen: Set<string>) (frontier: string list) =
                      match frontier with
                      | [] -> seen
                      | s :: rest ->
                          let next = transitions |> List.filter (fun t -> str "from" t = s) |> List.map (str "to") |> List.filter (seen.Contains >> not) |> List.distinct
                          go (Set.union seen (Set.ofList next)) (rest @ next)
                  go (Set.singleton (str "initial" m)) [ str "initial" m ]
              for s in states do
                  if not (reachable.Contains s) then yield sprintf "%s: state %s is not reachable from %s" mid s (str "initial" m)
              // v4's W05 rule holds for the amended machine too: Saved is entered only by a confirmation (PERSIST-007,
              // PERSIST-023).
              let confirmations = strs "entersSavedOnlyBy" m
              if not confirmations.IsEmpty then
                  for t in transitions do
                      if str "to" t = "Saved" && str "from" t <> "Saved" && not (List.contains (str "event" t) confirmations) then
                          yield sprintf "%s %s: Saved is entered by %s, which is not a confirmation (PERSIST-007, PERSIST-023)" mid (str "id" t) (str "event" t)
              // An event no transition accepts is a gap in the machine; it must be listed with an owner decision.
              let listed = arr "unusedEvents" i.GuardPartitions |> List.filter (fun u -> sOr "machine" u = mid)
              for e in events do
                  let used = transitions |> List.exists (fun t -> str "event" t = e)
                  match used, listed |> List.tryFind (fun u -> sOr "event" u = e) with
                  | false, None -> yield sprintf "%s: event %s is accepted by no transition and is not listed as unused" mid e
                  | false, Some u when String.IsNullOrWhiteSpace (sOr "decision" u) -> yield sprintf "%s: unused event %s names no decision" mid e
                  | true, Some _ -> yield sprintf "%s: event %s is listed as unused but a transition accepts it" mid e
                  | _ -> () ]
        let pairs =
            [ for s in states do
                  for e in events do
                      let ts = transitions |> List.filter (fun t -> str "from" t = s && str "event" t = e)
                      if ts.IsEmpty then
                          yield JObj [ "state", JStr s; "event", JStr e; "outcome", JStr "illegal"; "fault", JStr(sprintf "%s/%s/%s" mid s e) ]
                      else
                          yield
                              JObj
                                  [ "state", JStr s
                                    "event", JStr e
                                    "outcome", JStr "legal"
                                    "transitions",
                                    JArr(
                                        ts
                                        |> List.map (fun t ->
                                            JObj(
                                                [ "id", JStr(str "id" t)
                                                  "guard", JStr(sOr "guard" t)
                                                  "next", JStr(str "to" t)
                                                  "effects", JArr(arr "effects" t)
                                                  "sources", JArr(arr "sources" t)
                                                  "test", JStr(machineTest i mid (str "id" t)) ]
                                                @ (match strOpt "decision" t with Some d -> [ "amendedBy", JStr d ] | None -> [])))
                                    ) ] ]
        let legal = pairs |> List.filter (fun p -> sOr "outcome" p = "legal") |> List.length
        JObj
            [ "id", JStr mid
              "title", JStr(sOr "title" m)
              "initial", JStr(str "initial" m)
              "states", jstrs states
              "events", jstrs events
              "counts", JObj [ "states", jint states.Length; "events", jint events.Length; "transitions", jint transitions.Length; "pairs", jint pairs.Length; "legalPairs", jint legal; "illegalPairs", jint (pairs.Length - legal) ]
              "illegalPairTest", JStr(machineTest i mid "EveryIllegalPairFailsClosed")
              "pairs", JArr pairs ],
        problems
    let built = amendedMachines i |> List.map machine
    JObj
        [ "schema", JStr "indy-init.v41-state-machine-contracts/1"
          "description", JStr "The implementation table of every v4 state machine (requirements-compiled/v4/state-machines.json, frozen), with the transitions the owner approved in analysis/state-machine-amendments.json, generated by requirements-compiler v41: each (state, event) pair is either legal, with its transitions (guard, next state, effects, test case; an amended transition names its decision in amendedBy), or illegal. No state or event is added. Unlisted pairs are illegal by the v4 rule, so every pair is decided; where a pair has several transitions, its guards are the reviewed partition of analysis/guard-partitions.json. A transition's test drives the machine through it and asserts the next state and the effects; the illegal-pair test enumerates every illegal pair. Test files and owners are in agent-file-ownership.json."
          "invalidTransition", JObj [ "rule", JStr policy; "behaviour", JStr "fail closed: return Error SaveFault { Machine; State; Input; Reason }, emit no effect, keep state, base head and unsaved changes"; "sources", JArr(field "illegalInputPolicy" i.StateMachines |> Option.map (arr "sources") |> Option.defaultValue []) ]
          "evidence", JStr "the state-machine tests' results in the CI test record of the commit (TRX), one result per transition test and one per machine for the illegal pairs"
          "machines", JArr(built |> List.map fst) ],
    (built |> List.collect snd) @ amendmentProblems i

// ---- verification execution plan --------------------------------------------------------------------------------

type FixtureScope =
    | PerSuite
    | PerClass
    | PerFile
    | PerTest

type Suite =
    { Id: string
      Runner: string
      Environment: string
      Fixture: string
      Scope: FixtureScope
      DependsOn: string list
      Producer: string
      /// Plan tests or v4.1's own (contract, state-machine, unit) tests; never both.
      Plan: bool
      Matches: string -> bool }

let suites : Suite list =
    let browser (folder: string) (engine: string) (t: string) = starts ("playwright:" + folder + "/") t && t.EndsWith(sprintf "[%s]" engine, StringComparison.Ordinal)
    let ownTests (t: string) = starts "dotnet:Causa.Contract.Tests." t || Regex.IsMatch(t, @"^dotnet:Causa\.(Domain|Persistence|App)\.Tests\.")
    [ { Id = "SU-OWN-DOTNET"; Runner = "dotnet test tests/Causa.Contract.Tests tests/Causa.Domain.Tests tests/Causa.Persistence.Tests tests/Causa.App.Tests"; Environment = "ci"; Fixture = "pure functions and the stateful fake; no network"; Scope = PerClass; DependsOn = []; Producer = "CI test job (TRX)"; Plan = false; Matches = ownTests }
      { Id = "SU-OWN-BROWSER"; Runner = "npx playwright test --project=chromium tests/browser/contract"; Environment = "ci-browser"; Fixture = "the test build served once per worker; one browser context per test"; Scope = PerTest; DependsOn = [ "SU-OWN-DOTNET" ]; Producer = "CI browser job (Playwright JSON report)"; Plan = false; Matches = starts "playwright:contract/" }
      { Id = "SU-DOTNET-DETERMINISTIC"; Runner = "dotnet test tests/Causa.Acceptance --filter FullyQualifiedName~Causa.Acceptance.Deterministic."; Environment = "ci"; Fixture = "stateful GitHub fake built from the recorded answers (PERSIST-025), created once per test class"; Scope = PerClass; DependsOn = [ "SU-OWN-DOTNET" ]; Producer = "CI test job (TRX)"; Plan = true; Matches = starts "dotnet:Causa.Acceptance.Deterministic." }
      { Id = "SU-DOTNET-INVARIANTS"; Runner = "dotnet test tests/Causa.Invariants --filter \"FullyQualifiedName~Causa.Invariants.&FullyQualifiedName!~Causa.Invariants.Live.\""; Environment = "ci"; Fixture = "property generators and the stateful fake, created once per test class; no network"; Scope = PerClass; DependsOn = [ "SU-OWN-DOTNET" ]; Producer = "CI test job (TRX)"; Plan = true; Matches = fun t -> starts "dotnet:Causa.Invariants." t && not (starts "dotnet:Causa.Invariants.Live." t) }
      { Id = "SU-BROWSER-CHROMIUM"; Runner = "npx playwright test --project=chromium tests/browser/acceptance tests/browser/invariants"; Environment = "ci-browser"; Fixture = "the test build served once per worker with the stateful fake; one browser context per test"; Scope = PerTest; DependsOn = [ "SU-DOTNET-DETERMINISTIC" ]; Producer = "CI browser job (Playwright JSON report, screenshots)"; Plan = true; Matches = fun t -> browser "acceptance" "chromium" t || browser "invariants" "chromium" t }
      { Id = "SU-BROWSER-WEBKIT"; Runner = "npx playwright test --project=webkit tests/browser/acceptance tests/browser/invariants"; Environment = "ci-browser"; Fixture = "as SU-BROWSER-CHROMIUM, in WebKit"; Scope = PerTest; DependsOn = [ "SU-DOTNET-DETERMINISTIC" ]; Producer = "CI browser job (Playwright JSON report, screenshots)"; Plan = true; Matches = fun t -> browser "acceptance" "webkit" t || browser "invariants" "webkit" t }
      { Id = "SU-DOTNET-LIVE"; Runner = "dotnet test tests/Causa.Acceptance tests/Causa.Invariants --filter \"FullyQualifiedName~Causa.Acceptance.Live.|FullyQualifiedName~Causa.Invariants.Live.\""; Environment = "live-github"; Fixture = "the authorized GitHub test repository; one fresh branch per test class"; Scope = PerClass; DependsOn = [ "SU-DOTNET-DETERMINISTIC"; "SU-DOTNET-INVARIANTS" ]; Producer = "live CI job of the candidate's own run (FP-LIVE-PER-COMMIT)"; Plan = true; Matches = fun t -> starts "dotnet:Causa.Acceptance.Live." t || starts "dotnet:Causa.Invariants.Live." t }
      { Id = "SU-BROWSER-LIVE"; Runner = "npx playwright test --project=chromium tests/browser/live"; Environment = "deployed-live"; Fixture = "the deployed candidate at the exact SHA and the GitHub test repository; one fresh investigation per spec file"; Scope = PerFile; DependsOn = [ "SU-DOTNET-LIVE"; "SU-BROWSER-CHROMIUM" ]; Producer = "live browser job against the deployment"; Plan = true; Matches = starts "playwright:live/" }
      { Id = "SU-VERIFIER"; Runner = "independent verifier job (not authored by the implementer)"; Environment = "independent"; Fixture = "the deployed candidate and its provenance record at the exact SHA"; Scope = PerSuite; DependsOn = [ "SU-BROWSER-LIVE" ]; Producer = "independent acceptance job (AG-07, AG-10)"; Plan = true; Matches = starts "verifier:" }
      { Id = "SU-REVIEW-RECORDS"; Runner = "Praxis work evidence --kind independent-review / owner-approval"; Environment = "process"; Fixture = "the run's Praxis event log"; Scope = PerSuite; DependsOn = [ "SU-VERIFIER" ]; Producer = "independent reviewer and owner records"; Plan = true; Matches = fun t -> t = "praxis:independent-review" || t = "praxis:owner-approval" }
      { Id = "SU-PROCESS-RECORDS"; Runner = "Praxis work event log, checked by verify-process"; Environment = "process"; Fixture = "the run's Praxis event log"; Scope = PerSuite; DependsOn = []; Producer = "Praxis work event log"; Plan = true; Matches = fun t -> starts "praxis:" t && t <> "praxis:independent-review" && t <> "praxis:owner-approval" } ]

let suiteOf (plan: bool) (test: string) = suites |> List.filter (fun s -> s.Plan = plan && s.Matches test)

/// The number of fixture set-ups a suite performs, by its scope: test classes (the id without its last segment),
/// files, tests, or one.
let private fixtureInstances (s: Suite) (tests: string list) =
    match s.Scope with
    | PerSuite -> if tests.IsEmpty then 0 else 1
    | PerTest -> tests.Length
    | PerFile -> tests |> List.choose testFile |> List.distinct |> List.length
    | PerClass -> tests |> List.map (fun t -> let k = t.LastIndexOf '.' in if k > 0 then t.Substring(0, k) else t) |> List.distinct |> List.length

let verificationExecutionPlan (i: Inputs) : Json * string list =
    let bound = boundTests i.Plan
    let own = namedTests i |> List.map (fun (t, _, _) -> t) |> List.distinct
    let problems =
        (bound |> List.filter (fun b -> (suiteOf true b.Test).Length <> 1) |> List.map (fun b -> sprintf "%s (%s): in %d plan suites" b.Test b.Obligation (suiteOf true b.Test).Length) |> List.distinct)
        @ (own |> List.filter (fun t -> (suiteOf false t).Length <> 1) |> List.map (fun t -> sprintf "%s: in %d suites of v4.1's own tests" t (suiteOf false t).Length))
    let scopeName = function PerSuite -> "suite" | PerClass -> "test class" | PerFile -> "file" | PerTest -> "test"
    let suiteJson (s: Suite) =
        let mine = if s.Plan then bound |> List.filter (fun b -> s.Matches b.Test) else []
        let ownMine = if s.Plan then [] else own |> List.filter s.Matches |> List.sort
        let tests = mine |> List.groupBy (fun b -> b.Test) |> List.sortBy fst
        let ids = if s.Plan then tests |> List.map fst else ownMine
        JObj
            [ "id", JStr s.Id
              "runner", JStr s.Runner
              "environment", JStr s.Environment
              "fixture", JStr s.Fixture
              "fixtureScope", JStr(scopeName s.Scope)
              "dependsOn", jstrs s.DependsOn
              "evidenceProducer", JStr s.Producer
              "planTests", JBool s.Plan
              "sha", JStr "runs on the exact candidate SHA; every result records it"
              "counts",
              JObj
                  [ "tests", jint ids.Length
                    "bindings", jint mine.Length
                    "obligations", jint (mine |> List.map (fun b -> b.Obligation) |> List.distinct |> List.length)
                    "files", jint (ids |> List.choose testFile |> List.distinct |> List.length)
                    "fixtureSetups", jint (fixtureInstances s ids) ]
              "tests",
              JArr(
                  if s.Plan then
                      tests
                      |> List.map (fun (t, bs) ->
                          JObj
                              [ "id", JStr t
                                "kind", JStr bs.Head.Kind
                                "obligations", jstrs (bs |> List.map (fun b -> b.Obligation) |> List.distinct |> List.sort)
                                "categories", jstrs (bs |> List.collect (fun b -> b.Categories) |> List.distinct |> List.sort) ])
                  else ownMine |> List.map (fun t -> JObj [ "id", JStr t ])
              ) ]
    let categoryCounts =
        bound |> List.collect (fun b -> b.Categories |> List.map (fun c -> c, b)) |> List.groupBy fst |> List.sortBy fst |> List.map (fun (c, xs) -> c, jint xs.Length)
    JObj
        [ "schema", JStr "indy-init.v41-verification-execution-plan/1"
          "description", JStr "The order and grouping in which tests run, generated by requirements-compiler v41: the v4 plan's bound tests, and v4.1's own contract, state-machine and unit tests (which are interface-conformance evidence, not plan obligations). Grouping shares an environment only: every test keeps its identity and its own assertions, every (obligation, test) binding of the plan is in exactly one suite, and no binding, category, P0, security, accessibility, WebKit, live GitHub, independent-acceptance or provenance test is removed. A suite's runner selects by namespace or folder, the same rule that assigns its tests."
          "planIdentity", JObj [ "planId", JStr(field "planIdentity" i.Plan |> Option.map (str "planId") |> Option.defaultValue ""); "fileSha256", JStr(Git.sha256Hex i.PlanBytes) ]
          "environments",
          JArr
              [ JObj [ "id", JStr "ci"; "meaning", JStr "CI runner, no network beyond package restore" ]
                JObj [ "id", JStr "ci-browser"; "meaning", JStr "CI runner with Playwright Chromium and WebKit, the test build served locally" ]
                JObj [ "id", JStr "live-github"; "meaning", JStr "CI runner with the authorized GitHub test repository credential" ]
                JObj [ "id", JStr "deployed-live"; "meaning", JStr "the deployed candidate at the exact SHA and the GitHub test repository" ]
                JObj [ "id", JStr "independent"; "meaning", JStr "a job or reviewer independent of the implementing agent" ]
                JObj [ "id", JStr "process"; "meaning", JStr "records in the run's Praxis event log" ] ]
          "counts",
          JObj
              [ "bindings", jint bound.Length
                "planTests", jint (bound |> List.map (fun b -> b.Test) |> List.distinct |> List.length)
                "obligations", jint (bound |> List.map (fun b -> b.Obligation) |> List.distinct |> List.length)
                "ownTests", jint own.Length
                "suites", jint suites.Length ]
          "categoryBindings", JObj categoryCounts
          "savings",
          JObj
              [ "estimated", JObj [ "available", JBool false; "reason", JStr "v4 defines no run order or fixture plan to compare with, so no saving is claimed. Each suite reports the fixture set-ups its scope implies (counts.fixtureSetups); those are planning counts, not savings." ]
                "measured", JObj [ "available", JBool false; "reason", JStr "no event build has run against v4.1; the CI timings of the event build and the coordination telemetry (analysis/conflict-telemetry.json) will measure it" ] ]
          "suites", JArr(suites |> List.map suiteJson) ],
    problems

// ---- schedule ----------------------------------------------------------------------------------------------------

/// The v4 tasks with v4.1's dependency changes, estimate changes, or both, applied.
let applyAdjustments (i: Inputs) (deps: bool) (estimates: bool) : WorkTask list =
    let adjustments = arr "adjustments" i.Adjustments |> List.map (fun a -> str "task" a, a) |> Map.ofList
    let timedIds = i.Tasks |> List.filter (fun x -> x.Phase = "timed") |> List.map (fun x -> x.Id) |> Set.ofList
    i.Tasks
    |> List.map (fun t ->
        match adjustments.TryFind t.Id with
        | None -> t
        | Some a ->
            // Preflight prerequisites stay: an adjustment replaces only the timed edges, and may add preflight ones.
            let deps' =
                match field "dependsOn" a with
                | Some(JArr ds) when deps ->
                    let kept = t.DependsOn |> List.filter (fun d -> not (timedIds.Contains d.Task))
                    kept @ (ds |> List.map (fun d -> { Task = str "task" d; Reason = str "reason" d }) |> List.filter (fun d -> not (kept |> List.exists (fun k -> k.Task = d.Task))))
                | _ -> t.DependsOn
            // A successor amendment may move a task into the timed window (v4.1.1: the T10 bootstrap at T0). The move is
            // structural, so it follows the dependency changes; a moved task has no v4 estimate to fall back to.
            let phase' = match strOpt "phase" a with Some p when deps -> p | _ -> t.Phase
            let moved = phase' <> t.Phase
            let prefs' =
                match field "dropPreferenceAfter" a with
                | Some(JArr ds) when deps -> t.Preferences |> List.filter (fun p -> not (ds |> List.exists (fun d -> sOr "task" d = p.Task)))
                | _ -> t.Preferences
            let raw = match intOpt "estimateMinutes" a with Some e when estimates || moved -> setIn "estimateMinutes" (JInt e) t.Raw | _ -> t.Raw
            let raw' = if moved then setIn "phase" (JStr phase') raw else raw
            { t with DependsOn = deps'; Phase = phase'; Preferences = prefs'; Raw = raw' })

let adjustedTasks (i: Inputs) = applyAdjustments i true true

let private timedOnly (tasks: WorkTask list) =
    let ids = tasks |> List.filter (fun t -> t.Phase = "timed") |> List.map (fun t -> t.Id) |> Set.ofList
    tasks |> List.filter (fun t -> ids.Contains t.Id) |> List.map (fun t -> { t with DependsOn = t.DependsOn |> List.filter (fun d -> ids.Contains d.Task) })

/// Every ordering the timed DAG implies: (before, after) for each transitive dependency.
let precedences (tasks: WorkTask list) : Set<string * string> =
    let timed = timedOnly tasks
    let deps = timed |> List.map (fun t -> t.Id, t.DependsOn |> List.map (fun d -> d.Task)) |> Map.ofList
    let rec ancestors (seen: Set<string>) (frontier: string list) =
        match frontier with
        | [] -> seen
        | x :: rest ->
            let next = deps.TryFind x |> Option.defaultValue [] |> List.filter (seen.Contains >> not)
            ancestors (Set.union seen (Set.ofList next)) (rest @ next)
    timed |> List.collect (fun t -> ancestors Set.empty [ t.Id ] |> Set.toList |> List.map (fun a -> a, t.Id)) |> Set.ofList

let private dagSlack (tasks: WorkTask list) : Map<string, int * int> =
    // Earliest and latest start of each timed task against the critical-path length, ignoring lanes.
    let timed = timedOnly tasks
    let deps (t: WorkTask) = t.DependsOn |> List.map (fun d -> d.Task)
    let dur (t: WorkTask) = TimedSchedule.estimate t |> Option.defaultValue 0
    let byId = timed |> List.map (fun t -> t.Id, t) |> Map.ofList
    match Graph.topoOrder timed with
    | Error _ -> Map.empty
    | Ok order ->
        let es = order |> List.fold (fun (m: Map<string, int>) id -> m.Add(id, deps byId.[id] |> List.map (fun d -> m.[d] + dur byId.[d]) |> List.fold max 0)) Map.empty
        let length = timed |> List.map (fun t -> es.[t.Id] + dur t) |> List.fold max 0
        let succ id = timed |> List.filter (fun t -> List.contains id (deps t))
        let ls = List.rev order |> List.fold (fun (m: Map<string, int>) id -> m.Add(id, (succ id |> List.map (fun s -> m.[s.Id]) |> List.fold min length) - dur byId.[id])) Map.empty
        timed |> List.map (fun t -> t.Id, (es.[t.Id], ls.[t.Id])) |> Map.ofList

let private topologyJson (policy: Json) (tasks: WorkTask list) (id: string) : Json * int * bool =
    let window = field "clock" policy |> Option.bind (intOpt "timedMinutes") |> Option.map int |> Option.defaultValue 420
    let reserves = arr "reserves" policy |> List.sumBy (fun r -> intOpt "minutes" r |> Option.map int |> Option.defaultValue 0)
    let spec = arr "topologies" policy |> List.tryFind (fun t -> str "id" t = id) |> Option.defaultValue (JObj [])
    let baseId = strOpt "base" spec |> Option.defaultValue id
    let overrides = match field "laneOverrides" spec with Some(JObj ps) -> ps |> List.map (fun (k, v) -> k, asStr v) |> Map.ofList | _ -> Map.empty
    let laneFor (t: WorkTask) = overrides.TryFind t.Id |> Option.defaultValue (TimedSchedule.laneOf baseId t)
    let slots = TimedSchedule.schedule laneFor tasks
    let makespan = slots |> List.map (fun s -> s.Finish) |> List.fold max 0
    let byId = tasks |> List.map (fun t -> t.Id, t) |> Map.ofList
    let finish = slots |> List.map (fun s -> s.Task, s.Finish) |> Map.ofList
    // Contention: minutes a task waited for its lane after its prerequisites had finished.
    let waits =
        slots |> List.map (fun s ->
            let ready = byId.[s.Task].DependsOn |> List.choose (fun d -> finish.TryFind d.Task) |> List.fold max 0
            s.Task, s.Start - ready) |> Map.ofList
    let integrationMinutes = slots |> List.filter (fun s -> s.Lane = "integration" || byId.[s.Task].Owner = "integration") |> List.sumBy (fun s -> s.Finish - s.Start)
    let lanes = slots |> List.groupBy (fun s -> s.Lane) |> List.sortBy fst
    let total = makespan + reserves
    // The binding chain: from the last task back, each step is the prerequisite or the earlier task on the same lane
    // that the task waited for (whichever finished exactly when it started).
    let bySlot = slots |> List.map (fun s -> s.Task, s) |> Map.ofList
    let rec chain (s: TimedSchedule.Slot) (acc: (string * string) list) =
        let viaDependency = byId.[s.Task].DependsOn |> List.choose (fun d -> bySlot.TryFind d.Task) |> List.filter (fun p -> p.Finish = s.Start) |> List.sortBy (fun p -> p.Task) |> List.tryHead
        let viaLane = slots |> List.filter (fun p -> p.Lane = s.Lane && p.Finish = s.Start && p.Task <> s.Task) |> List.sortBy (fun p -> p.Task) |> List.tryHead
        match viaDependency, viaLane with
        | Some p, _ when s.Start > 0 -> chain p ((s.Task, "dependency on " + p.Task) :: acc)
        | None, Some p when s.Start > 0 -> chain p ((s.Task, "lane " + s.Lane + " after " + p.Task) :: acc)
        | _ -> (s.Task, "start") :: acc
    let binding = slots |> List.sortBy (fun s -> -s.Finish, s.Task) |> List.tryHead |> Option.map (fun last -> chain last []) |> Option.defaultValue []
    JObj
        [ "id", JStr id
          "bindingChain", JArr(binding |> List.map (fun (t, why) -> JObj [ "task", JStr t; "startsAfter", JStr why ]))
          "makespanMinutes", jint makespan
          "reservedMinutes", jint reserves
          "totalMinutes", jint total
          "windowMinutes", jint window
          "fits", JBool(total <= window)
          "slackMinutes", jint (window - total)
          "contentionMinutes", jint (waits |> Map.toList |> List.sumBy snd)
          "integrationMinutes", JObj [ "lane", jint integrationMinutes; "reserve", jint (arr "reserves" policy |> List.filter (fun r -> str "id" r = "RES-INTEGRATION") |> List.sumBy (fun r -> intOpt "minutes" r |> Option.map int |> Option.defaultValue 0)) ]
          "lanes",
          JArr(
              lanes
              |> List.map (fun (lane, ss) ->
                  let busy = ss |> List.sumBy (fun s -> s.Finish - s.Start)
                  JObj
                      [ "lane", JStr lane
                        "busyMinutes", jint busy
                        "utilization", JStr(if makespan = 0 then "0" else sprintf "%.2f" (float busy / float makespan))
                        "slots", JArr(ss |> List.sortBy (fun s -> s.Start, s.Task) |> List.map (fun s -> JObj [ "task", JStr s.Task; "start", jint s.Start; "finish", jint s.Finish; "waitedForLane", jint waits.[s.Task] ])) ])
          ) ],
    total,
    total <= window

/// Sensitivity scenarios of a successor amendment: each scales the estimates of named tasks, or of every task an owner
/// holds, by a whole percentage (rounded up), and reschedules every topology with the same scheduler. Nothing is shortened.
let private sensitivity (i: Inputs) (tasks: WorkTask list) : Json list =
    let window = field "clock" i.Policy |> Option.bind (intOpt "timedMinutes") |> Option.map int |> Option.defaultValue 420
    let topologies = arr "topologies" i.Policy |> List.map (str "id")
    amendmentList "sensitivity" i
    |> List.map (fun s ->
        let rules = arr "scale" s
        let percentFor (t: WorkTask) =
            let pct (r: Json) = intOpt "percent" r |> Option.map int |> Option.defaultValue 100
            let named = rules |> List.filter (fun r -> List.contains t.Id (strs "tasks" r))
            let byOwner = rules |> List.filter (fun r -> (strs "tasks" r).IsEmpty && sOr "owner" r <> "" && sOr "owner" r = t.Owner)
            // A rule that names the task replaces the owner rule; it never compounds with it.
            (if named.IsEmpty then byOwner else named) |> List.fold (fun p r -> p * pct r / 100) 100
        let scaled =
            tasks |> List.map (fun t ->
                match TimedSchedule.estimate t with
                | Some e when t.Phase = "timed" -> { t with Raw = setIn "estimateMinutes" (JInt(int64 ((e * percentFor t + 99) / 100))) t.Raw }
                | _ -> t)
        let _, cp = TimedSchedule.criticalPath scaled
        JObj
            [ "id", JStr(str "id" s)
              "description", JStr(sOr "description" s)
              "scale", JArr rules
              "criticalPathMinutes", jint cp
              "topologies",
              JArr(topologies |> List.map (fun id ->
                  let _, total, fits = topologyJson i.Policy scaled id
                  JObj [ "id", JStr id; "totalMinutes", jint total; "windowMinutes", jint window; "marginMinutes", jint (window - total); "fits", JBool fits ])) ])

let schedule (i: Inputs) : Json * Json * string list =
    let tasks = adjustedTasks i
    let timed = tasks |> List.filter (fun t -> t.Phase = "timed")
    let timedIds = timed |> List.map (fun t -> t.Id) |> Set.ofList
    let taskIds = i.Tasks |> List.map (fun t -> t.Id) |> Set.ofList
    let adjustmentIds = arr "adjustments" i.Adjustments |> List.map (str "task")
    let acyclic (ts: WorkTask list) = Graph.topoOrder (timedOnly ts) |> Result.isOk
    let lost = if acyclic tasks then Set.difference (precedences i.Tasks) (precedences tasks) else Set.empty
    let waivers = arr "waivedPrecedences" i.Adjustments |> List.map (fun w -> (sOr "before" w, sOr "after" w), w)
    let problems =
        [ for a in arr "adjustments" i.Adjustments do
              let id = str "task" a
              if not (taskIds.Contains id) then yield sprintf "adjustment names %s, which is not a v4 task" id
              for d in arr "dependsOn" a do
                  if not (taskIds.Contains (str "task" d)) then yield sprintf "adjustment of %s depends on %s, which is not a v4 task" id (str "task" d)
                  if String.IsNullOrWhiteSpace (sOr "reason" d) then yield sprintf "adjustment of %s: dependency on %s has no reason" id (str "task" d)
              if String.IsNullOrWhiteSpace (sOr "rationale" a) then yield sprintf "adjustment of %s has no rationale" id
              match sOr "evidence" a with
              | "estimated" -> ()
              | "measured+estimated" when i.Amendment.IsSome ->
                  if not (i.AuthoredText |> List.exists (fst >> (=) (sOr "measurement" a))) then yield sprintf "adjustment of %s is labelled partly measured but names no measurement record of the package" id
                  match intOpt "measuredMinutes" a, intOpt "estimatedMinutes" a, intOpt "estimateMinutes" a with
                  | Some m, Some e, Some t when m >= 0L && e >= 0L && m + e = t -> ()
                  | _ -> yield sprintf "adjustment of %s is partly measured but its measuredMinutes and estimatedMinutes do not add up to its estimate" id
              | _ -> yield sprintf "adjustment of %s must be labelled estimated" id
              if (field "phase" a).IsSome || (field "dropPreferenceAfter" a).IsSome then
                  if i.Amendment.IsNone then yield sprintf "adjustment of %s moves a phase or drops a preference, which only a successor amendment may do" id
                  match strOpt "phase" a with
                  | Some p when p <> "timed" -> yield sprintf "adjustment of %s moves it to phase %s; only a move into the timed window is allowed" id p
                  | _ -> ()
                  for d in arr "dropPreferenceAfter" a do
                      if String.IsNullOrWhiteSpace (sOr "reason" d) || String.IsNullOrWhiteSpace (sOr "decision" d) then yield sprintf "adjustment of %s drops its preference after %s without a reason and a decision" id (sOr "task" d)
              // No estimate falls below half of v4's: a larger cut is a different plan, not an adjustment.
              match intOpt "estimateMinutes" a, i.Tasks |> List.tryFind (fun t -> t.Id = id) |> Option.bind TimedSchedule.estimate with
              | Some e, Some v4 when int e * 2 < v4 -> yield sprintf "adjustment of %s cuts its estimate from %d to %d, below half of v4's" id v4 e
              | Some e, _ when e <= 0L -> yield sprintf "adjustment of %s gives a non-positive estimate" id
              | _ -> ()
          for d in duplicates adjustmentIds do
              yield sprintf "task %s is adjusted twice" d
          match Graph.topoOrder (timedOnly tasks) with
          | Error cycle -> yield sprintf "the adjusted DAG has a cycle through %s" (String.concat ", " cycle)
          | Ok _ -> ()
          // Every ordering v4 implied is kept, unless a waiver names it with a reason and the owner decision.
          for (a, b) in lost do
              match waivers |> List.tryFind (fst >> (=) (a, b)) with
              | None -> yield sprintf "v4 ordered %s before %s; the adjusted DAG drops it without a waiver" a b
              | Some(_, w) ->
                  if String.IsNullOrWhiteSpace (sOr "reason" w) || String.IsNullOrWhiteSpace (sOr "decision" w) then yield sprintf "the waiver of %s before %s needs a reason and a decision" a b
          let requests = requestIds i
          for ((a, b), w) in waivers do
              if not (lost.Contains(a, b)) then yield sprintf "the waiver of %s before %s waives an ordering the adjusted DAG keeps (or v4 never had)" a b
              if not (List.contains (sOr "decision" w) requests) then yield sprintf "the waiver of %s before %s names decision %s, which is not an owner decision request" a b (sOr "decision" w)
          // Work is preserved: the same tasks, each covering the same requirements, each with an estimate.
          if (tasks |> List.map (fun t -> t.Id)) <> (i.Tasks |> List.map (fun t -> t.Id)) then yield "the adjusted task list differs from v4's"
          for (a, b) in List.zip tasks i.Tasks do
              if a.Covers <> b.Covers then yield sprintf "task %s covers different requirements than in v4" a.Id
          for t in timed do
              if (TimedSchedule.estimate t).IsNone then yield sprintf "timed task %s has no estimate" t.Id ]
    let path, pathMinutes = TimedSchedule.criticalPath tasks
    let basePath, baseMinutes = TimedSchedule.criticalPath i.Tasks
    let slack = dagSlack tasks
    let topologies = arr "topologies" i.Policy |> List.map (str "id")
    // A cyclic DAG cannot be scheduled (the scheduler would not terminate); X13 reports the cycle.
    let scenario (name: string) (deps: bool) (est: bool) =
        let ts = applyAdjustments i deps est
        if not (acyclic ts) then JObj [ "scenario", JStr name; "schedulable", JBool false ]
        else
            let _, cp = TimedSchedule.criticalPath ts
            JObj
                [ "scenario", JStr name
                  "dependencies", JStr(if deps then (match i.Amendment with Some _ -> "v" + packageVersion i | None -> "v4.1") else "v4")
                  "estimates", JStr(if est then (match i.Amendment with Some _ -> "v" + packageVersion i | None -> "v4.1") else "v4")
                  "criticalPathMinutes", jint cp
                  "topologies", JArr(topologies |> List.map (fun id -> let _, total, fits = topologyJson i.Policy ts id in JObj [ "id", JStr id; "totalMinutes", jint total; "fits", JBool fits ])) ]
    let newTopologies = if acyclic tasks then topologies |> List.map (topologyJson i.Policy tasks) else []
    let dag =
        JObj
            [ "schema", JStr "indy-init.v41-execution-dag/1"
              "description",
              JStr(
                  match i.Amendment with
                  | None -> "The timed work items of v4 with v4.1's estimate and dependency changes (analysis/task-adjustments.json) applied, generated by requirements-compiler v41. Every v4 task is kept with the requirements it covers; every ordering v4 implied is kept except the waived ones listed here. Estimates are the package author's; none is measured."
                  | Some _ -> sprintf "The timed work items of v4 with the estimate, dependency and phase changes of package %s (analysis/task-adjustments.json) applied, generated by requirements-compiler v41. Every v4 task is kept with the requirements it covers; every ordering v4 implied is kept except the waived ones listed here. An adjustment labelled measured names its measurement record; every other estimate is the package author's." (packageVersion i)
              )
              "criticalPath", JObj [ "tasks", jstrs path; "minutes", jint pathMinutes ]
              "waivedPrecedences", JArr(lost |> Set.toList |> List.map (fun (a, b) -> JObj [ "before", JStr a; "after", JStr b; "reason", JStr(waivers |> List.tryFind (fst >> (=) (a, b)) |> Option.map (snd >> sOr "reason") |> Option.defaultValue "") ]))
              "tasks",
              JArr(
                  timed
                  |> List.map (fun t ->
                      let es, ls = slack.TryFind t.Id |> Option.defaultValue (0, 0)
                      JObj
                          [ "id", JStr t.Id
                            "title", JStr t.Title
                            "owner", JStr t.Owner
                            "estimateMinutes", jint (TimedSchedule.estimate t |> Option.defaultValue 0)
                            "v4EstimateMinutes", jint (i.Tasks |> List.tryFind (fun x -> x.Id = t.Id) |> Option.bind TimedSchedule.estimate |> Option.defaultValue 0)
                            "dependsOn", jstrs (t.DependsOn |> List.map (fun d -> d.Task) |> List.filter timedIds.Contains)
                            "earliestStart", jint es
                            "latestStart", jint ls
                            "slackMinutes", jint (ls - es)
                            "adjusted", JBool(List.contains t.Id adjustmentIds)
                            "covers", jstrs t.Covers ])
              ) ]
    let sched =
        JObj
            [ "schema", JStr "indy-init.v41-schedule/1"
              "description",
              JStr(
                  match i.Amendment with
                  | None -> "The v4.1 timed schedule for one lead agent (B-single-lead), role lanes with one UI agent (A-role-lanes) and role lanes with two UI agents (C-role-lanes-two-ui), computed by the same scheduler as v4 (TimedSchedule) over execution-dag.json, with v4's reserves and window. All minutes are estimates. 'scenarios' separates the effect of the dependency changes (ODR-V41-03) from the estimate changes (ODR-V41-04)."
                  | Some _ -> sprintf "The v%s timed schedule for one lead agent (B-single-lead), role lanes with one UI agent (A-role-lanes) and role lanes with two UI agents (C-role-lanes-two-ui), computed by the same scheduler as v4 (TimedSchedule) over execution-dag.json, with v4's reserves and window. Every estimate is the package author's except T10's, which is partly measured (analysis/bootstrap-measurement.json). 'scenarios' separates the dependency changes from the estimate changes; a scenario with v4's dependencies keeps T10 before T0, outside the window, as v4 has it. 'sensitivity' rescales named tasks or an owner's tasks; a rule naming a task replaces the owner rule for it." (packageVersion i)
              )
              "evidence", JStr "estimated"
              "experimentalTarget", JObj [ "criticalPathMinutes", jint 300; "meets", JBool(pathMinutes < 300); "note", JStr "an experimental goal of the v4.1 brief, not an acceptance condition. It is measured on the lane-free dependency critical path; each topology's makespan, which adds waiting for a busy lane, is in topologies (makespanMinutes, bindingChain)." ]
              "criticalPath", JObj [ "v4", JObj [ "tasks", jstrs basePath; "minutes", jint baseMinutes ]; "v41", JObj [ "tasks", jstrs path; "minutes", jint pathMinutes ] ]
              "scenarios",
              (let v = match i.Amendment with Some _ -> "v" + packageVersion i | None -> "v4.1"
               JArr [ scenario "v4" false false; scenario (v + " dependencies only") true false; scenario (v + " estimates only") false true; scenario v true true ])
              "riskiestDependencies",
              JArr(
                  List.pairwise path
                  |> List.map (fun (a, b) ->
                      let reason = tasks |> List.find (fun t -> t.Id = b) |> fun t -> t.DependsOn |> List.tryFind (fun d -> d.Task = a) |> Option.map (fun d -> d.Reason) |> Option.defaultValue ""
                      JObj [ "from", JStr a; "to", JStr b; "slackMinutes", jint 0; "reason", JStr reason ])
              )
              "topologies", JArr(newTopologies |> List.map (fun (j, _, _) -> j)) ]
    // A successor amendment adds its version and the sensitivity of each topology to overruns.
    let sched' =
        match i.Amendment, sched with
        | Some _, JObj ps when acyclic tasks -> JObj(ps @ [ "packageVersion", JStr(packageVersion i); "sensitivity", JArr(sensitivity i tasks) ])
        | _ -> sched
    dag, sched', problems

// ---- checks ------------------------------------------------------------------------------------------------------

/// Files only integration writes (the brief: project files, solution, composition, shared configuration, dependency
/// manifests, CI and deployment), whatever the authored structure says.
let integrationExclusive (path: string) =
    let name = path.Substring(path.LastIndexOf '/' + 1)
    [ ".fsproj"; ".csproj"; ".slnx"; ".sln"; ".runsettings" ] |> List.exists (fun s -> path.EndsWith(s, StringComparison.Ordinal))
    || (starts "Directory." name && (name.EndsWith(".props", StringComparison.Ordinal) || name.EndsWith(".targets", StringComparison.Ordinal)))
    || String.Equals(name, "nuget.config", StringComparison.OrdinalIgnoreCase)
    || [ ".github/"; ".echelon/"; "src/Causa.Host/"; "tests/fixtures/"; "tests/browser/support/" ] |> List.exists (fun p -> starts p path)
    || List.contains path
        [ ".gitignore"; "global.json"; "package.json"; "package-lock.json"; "playwright.config.ts"; "build.fsx"; "README.md"; "web/boundary.js"
          "src/Causa.App/View.fs"; "src/Causa.App/Program.fs" ]

let ownershipCheck (i: Inputs) (files: OwnedFile list) : Check list =
    let roles = arr "roles" i.Structure |> List.map (str "id") |> Set.ofList
    let allProjects = (arr "projects" i.Structure @ arr "testProjects" i.Structure)
    let declared = allProjects |> List.map (str "id") |> Set.ofList
    [ check "X01" "Every file has exactly one owner, and the owner is a declared role"
          [ for p in duplicates (files |> List.map (fun f -> f.Path)) do
                yield sprintf "%s is listed more than once (two owners or two entries)" p
            for f in files do
                if not (roles.Contains f.Owner) then yield sprintf "%s: owner '%s' is not a declared role" f.Path f.Owner
                for o in f.Conflicts do
                    yield sprintf "%s holds tests of %s and of %s" f.Path f.Owner o ]
      check "X02" "Project files, the solution, composition, shared configuration, dependency manifests, CI and deployment are owned by integration"
          [ let exclusive (p: string) = integrationExclusive p || List.contains p (strs "files" (amendmentField "sharedEntrypoints" i))
            for f in files do
                if exclusive f.Path && f.Owner <> "integration" then yield sprintf "%s is integration's alone but owned by %s" f.Path f.Owner
                if exclusive f.Path && not f.Contributors.IsEmpty then yield sprintf "%s is integration's alone but lists contributors %s" f.Path (String.concat ", " f.Contributors)
            // The shared files the event build cannot do without must be in the manifest, owned by integration.
            for required in [ ".gitignore"; "Causa.slnx"; "global.json"; "Directory.Build.props"; "Directory.Packages.props"; "package.json"; "package-lock.json"; "playwright.config.ts"; ".github/workflows/ci.yml"; "web/boundary.js"; "tests/browser/support/layout-assertions.mjs" ] do
                if not (files |> List.exists (fun f -> f.Path = required)) then yield sprintf "%s is missing from the manifest" required ]
      check "X03" "Compile order is predefined: each file depends only on earlier files of its project or on referenced projects, project references (source and test) are acyclic, and every test file is in a declared test project"
          [ let refs = allProjects |> List.map (fun p -> str "id" p, strs "references" p) |> Map.ofList
            let rec closure (seen: Set<string>) (p: string) =
                refs.TryFind p |> Option.defaultValue [] |> List.fold (fun (s: Set<string>) r -> if s.Contains r then s else closure (s.Add r) r) seen
            for d in duplicates (allProjects |> List.map (str "id")) do
                yield sprintf "project %s is declared twice" d
            for p in allProjects do
                let pid = str "id" p
                if (closure Set.empty pid).Contains pid then yield sprintf "project %s references itself through its references" pid
                for r in strs "references" p do
                    if not (refs.ContainsKey r) then yield sprintf "project %s references unknown project %s" pid r
            let projects = arr "projects" i.Structure
            for p in projects do
                let pid = str "id" p
                let ordered = arr "files" p |> List.map (str "path")
                let visible = closure Set.empty pid |> Set.toList |> List.collect (fun r -> projects |> List.filter (fun q -> str "id" q = r) |> List.collect (arr "files") |> List.map (str "path"))
                for (n, f) in List.indexed (arr "files" p) do
                    for d in strs "dependsOn" f do
                        if not (List.contains d (List.take n ordered) || List.contains d visible) then
                            yield sprintf "%s depends on %s, which is neither earlier in %s nor in a referenced project" (str "path" f) d pid
            for f in files do
                match f.Project with
                | Some p when (f.Kind = "test" || f.Kind = "test-helper") && not (starts "verifier/" f.Path) && not (declared.Contains p) ->
                    yield sprintf "%s is in test project %s, which is not declared" f.Path p
                | _ -> ()
            // A test helper depends only on earlier helpers of its project or on files of projects it references.
            let helpers = files |> List.filter (fun f -> f.Kind = "test-helper")
            let filesOf (p: string) = files |> List.filter (fun f -> f.Project = Some p && f.Kind <> "test") |> List.map (fun f -> f.Path)
            for (n, h) in List.indexed helpers do
                let earlier = helpers |> List.take n |> List.filter (fun x -> x.Project = h.Project) |> List.map (fun x -> x.Path)
                let visible = match h.Project with Some p -> closure Set.empty p |> Set.toList |> List.collect filesOf | None -> []
                for d in h.DependsOn do
                    if not (List.contains d earlier || List.contains d visible) then
                        yield sprintf "test helper %s depends on %s, which is neither an earlier helper of its project nor in a referenced project" h.Path d ] ]

let interfaceCheck (i: Inputs) (files: OwnedFile list) : Check list =
    let interfaces = arr "interfaces" i.Interfaces
    let ids = interfaces |> List.map (str "id")
    let byPath = files |> List.map (fun f -> f.Path, f) |> Map.ofList
    let planTests = allTestIds i.Plan
    let sourceOrder =
        arr "projects" i.Structure |> List.collect (fun p -> arr "files" p |> List.map (str "path")) |> List.indexed |> List.map (fun (n, p) -> p, n) |> Map.ofList
    let roles = arr "roles" i.Structure |> List.map (str "id") |> Set.ofList
    let ownerOfTest (t: string) = testFile t |> Option.bind byPath.TryFind |> Option.map (fun f -> f.Owner)
    let machineIds = amendedMachines i |> List.map (str "id")
    let machineTypes = interfaces |> List.collect (fun x -> arr "machineTypes" x |> List.map (fun mt -> x, mt))
    [ check "X04" "Every interface has a complete contract implemented by one file its producer owns, in file order; its producer tests are in files the producer owns and every consumer has a contract test in a file it owns"
          [ if strs "compileOrder" i.Interfaces |> List.filter ((<>) "DOMAIN-STUBS") <> ids then yield "compileOrder does not list the interfaces exactly, in order"
            for d in duplicates ids do
                yield sprintf "interface %s is defined twice" d
            for x in interfaces do
                let id = str "id" x
                let producer = sOr "producer" x
                for k in [ "producer"; "file"; "fsharp" ] do
                    if String.IsNullOrWhiteSpace (sOr k x) then yield sprintf "%s: %s is missing" id k
                for k in [ "concurrency"; "versioning" ] do
                    if (sOr k x).Trim().Length < 30 then yield sprintf "%s: %s is missing or too short to state a rule" id k
                if (arr "operations" x).IsEmpty then yield sprintf "%s: no operations" id
                for o in arr "operations" x do
                    for k in [ "name"; "signature" ] do
                        if String.IsNullOrWhiteSpace (sOr k o) then yield sprintf "%s: operation %s has no %s" id (sOr "name" o) k
                    for k in [ "success"; "failure" ] do
                        if (sOr k o).Trim().Length < 5 then yield sprintf "%s: operation %s does not state its %s" id (sOr "name" o) k
                    let name = (sOr "name" o).Split('.') |> Array.last
                    if name <> "" && not (Regex.IsMatch(str "fsharp" x, sprintf @"\b%s\b" (Regex.Escape name))) then yield sprintf "%s: operation %s is not in the contract" id (sOr "name" o)
                for t in strs "producerTests" x do
                    if not (Regex.IsMatch(t, @"^dotnet:Causa\.Contract\.Tests\.[A-Za-z0-9]+\.Producer\.")) then yield sprintf "%s: producer test %s is not a contract test identity" id t
                for t in strs "consumerTests" x do
                    if not (Regex.IsMatch(t, @"^(dotnet:Causa\.Contract\.Tests\.[A-Za-z0-9]+\.Consumer[A-Za-z0-9]+\.|playwright:contract/)")) then yield sprintf "%s: consumer test %s is not a contract test identity" id t
                for k in [ "consumers"; "producerTests"; "consumerTests"; "requirements" ] do
                    if (arr k x).IsEmpty then yield sprintf "%s: %s is empty" id k
                for t in strs "producerTests" x do
                    match ownerOfTest t with
                    | None -> yield sprintf "%s: producer test %s is in no owned file" id t
                    | Some o when o <> producer -> yield sprintf "%s: producer test %s is in a file owned by %s, not the producer %s" id t o producer
                    | _ -> ()
                for c in strs "consumers" x do
                    if not (roles.Contains c) then yield sprintf "%s: consumer %s is not a role" id c
                    let theirs = strs "consumerTests" x |> List.filter (fun t -> consumerRole (strs "consumers" x) t = Some c)
                    if theirs.IsEmpty then yield sprintf "%s: consumer %s has no contract test" id c
                    for t in theirs do
                        if ownerOfTest t <> Some c then yield sprintf "%s: consumer test %s is not in a file %s owns" id t c
                for t in strs "consumerTests" x do
                    if (consumerRole (strs "consumers" x) t).IsNone then yield sprintf "%s: consumer test %s names no consumer" id t
                for t in strs "relatedPlanTests" x do
                    if not (planTests.Contains t) then yield sprintf "%s: related plan test %s is not bound in the plan" id t
                match byPath.TryFind (sOr "file" x) with
                | None -> yield sprintf "%s: file %s is not an owned file" id (sOr "file" x)
                | Some f ->
                    if f.Owner <> producer then yield sprintf "%s: file %s is owned by %s, not the producer %s" id f.Path f.Owner producer
                    if not (List.contains id f.Implements) then yield sprintf "%s: file %s does not declare that it implements it" id f.Path
                let implementers = files |> List.filter (fun f -> List.contains id f.Implements)
                if implementers.Length <> 1 then yield sprintf "%s: implemented by %d files" id implementers.Length
            for f in files do
                for impl in f.Implements do
                    if not (List.contains impl ids) then yield sprintf "%s implements unknown interface %s" f.Path impl
            // Case names shared by two unions that do not require qualified access are ambiguous in any file that opens
            // both namespaces (review RV41-4-6, RV41-5-2).
            let stubs = field "domainStubs" i.Interfaces |> Option.map (sOr "fsharp") |> Option.defaultValue ""
            let unions =
                stubs :: (interfaces |> List.map (sOr "fsharp"))
                |> List.collect (fun src ->
                    Regex.Matches(src, @"(\[<RequireQualifiedAccess>\]\s*\n)?[ \t]*type (\w+) =\s*\n((?:[ \t]*\|[^\n]*(?:\n|$))+)")
                    |> Seq.filter (fun m -> not m.Groups.[1].Success)
                    |> Seq.collect (fun m -> Regex.Matches(m.Groups.[3].Value, @"\|\s*([A-Z][A-Za-z0-9]*)") |> Seq.map (fun c -> c.Groups.[1].Value, m.Groups.[2].Value))
                    |> List.ofSeq)
            for (case, owners) in unions |> List.groupBy fst |> List.map (fun (c, xs) -> c, xs |> List.map snd |> List.distinct) do
                if owners.Length > 1 then yield sprintf "union case %s is declared by %s without qualified access" case (String.concat " and " owners)
            let positions = interfaces |> List.map (fun x -> sourceOrder.TryFind (sOr "file" x) |> Option.defaultValue -1)
            if positions <> List.sort positions then yield "the interface compile order disagrees with the project file order" ]
      check "X05" "Each state machine's states and events are exactly the cases of the state and input unions of one interface (no new state, no unmapped event)"
          [ for m in machineIds do
                match machineTypes |> List.filter (fun (_, mt) -> sOr "machine" mt = m) with
                | [ _ ] -> ()
                | [] -> yield sprintf "%s is mapped to no interface type" m
                | _ -> yield sprintf "%s is mapped to more than one interface type" m
            for (x, mt) in machineTypes do
                let mid = sOr "machine" mt
                match amendedMachines i |> List.tryFind (fun m -> str "id" m = mid) with
                | None -> yield sprintf "%s maps unknown machine %s" (str "id" x) mid
                | Some m ->
                    for (kind, typeKey) in [ "states", "states"; "events", "events" ] do
                        let expected = arr kind m |> List.map (str "id")
                        let cases = unionCases (sOr typeKey mt) (str "fsharp" x)
                        if List.sort cases <> List.sort expected then
                            yield sprintf "%s %s [%s] differ from %s %s [%s]" (str "id" x) (sOr typeKey mt) (String.concat ", " cases) mid kind (String.concat ", " expected) ] ]

/// The cases of IF41-ERRORS ProviderError.
let private providerErrorCases (i: Inputs) : string list =
    arr "interfaces" i.Interfaces |> List.tryFind (fun x -> str "id" x = "IF41-ERRORS") |> Option.map (str "fsharp" >> unionCases "ProviderError") |> Option.defaultValue []

let private requiredErrorClasses =
    [ "GH-AUTH"; "GH-PERMISSION"; "GH-RATE-PRIMARY"; "GH-RATE-SECONDARY"; "GH-STALE-HEAD"; "GH-VALIDATION"; "GH-NOT-FOUND"; "GH-HISTORY-REWRITTEN"
      "GH-NETWORK-BEFORE"; "GH-NETWORK-AFTER"; "GH-UNKNOWN-OUTCOME"; "GH-MALFORMED"; "GH-READBACK-MISMATCH" ]

let errorMatrixCheck (i: Inputs) : Check =
    let classes = arr "classes" i.ErrorMatrix
    let bounds = field "bounds" i.ErrorMatrix |> Option.defaultValue JNull
    let tests = allTestIds i.Plan
    let testRequirements = boundTests i.Plan |> List.groupBy (fun b -> b.Test) |> List.map (fun (t, bs) -> t, bs |> List.map (fun b -> b.Requirement) |> Set.ofList) |> Map.ofList
    let errorCases = providerErrorCases i
    // Evidence that is only a status (with or without "HTTP", "status" or its reason phrase) is not other evidence.
    let reasonPhrases = Regex(@"(?i)\b(http|status|code|forbidden|not found|unauthorized|too many requests|unprocessable( entity)?|internal server error|bad gateway|service unavailable|gateway timeout|ok|[0-9x]{3})\b")
    let isStatusOnly (e: string) = Regex.Replace(reasonPhrases.Replace(e, ""), @"[^A-Za-z]", "").Length < 4
    let shared =
        classes |> List.collect (fun c -> arr "httpStatuses" c |> List.choose (function JInt n -> Some(n, str "id" c) | _ -> None))
        |> List.groupBy fst |> List.filter (fun (_, xs) -> xs.Length > 1) |> List.collect (snd >> List.map snd) |> Set.ofList
    let evidence (c: Json) = strs "nonStatusEvidence" c |> List.filter (isStatusOnly >> not)
    check "X06" "The GitHub error matrix covers every required class and timeouts; a shared HTTP status is never the only evidence and no two such classes share evidence; each class retries within its own bound; every test exists in the plan and belongs to the class's requirements"
        [ let ids = classes |> List.map (str "id")
          for r in requiredErrorClasses do
              if not (List.contains r ids) then yield sprintf "required error class %s is missing" r
          if not (classes |> List.exists (fun c -> strs "nonStatusEvidence" c |> List.exists (fun e -> starts "transport:" e && e.Contains "TimedOut"))) then yield "no class detects a request timeout (transport: TimedOut)"
          if String.IsNullOrWhiteSpace (sOr "precedence" i.ErrorMatrix) then yield "no precedence rule for an answer matching several classes"
          for d in duplicates ids do
              yield sprintf "error class %s is defined twice" d
          for c in classes do
              let id = str "id" c
              for k in [ "class"; "detection"; "typedError"; "transition"; "userVisible"; "retry"; "reconciliation"; "recovery" ] do
                  if String.IsNullOrWhiteSpace (sOr k c) then yield sprintf "%s: %s is missing" id k
              for k in [ "operations"; "tests"; "requirements" ] do
                  if (arr k c).IsEmpty then yield sprintf "%s: %s is empty" id k
              // Each bound belongs to one kind of retry: rebases to the stale-head class, re-reads of a large file to the
              // large-file class, reconciliation reads to classes that reconcile. A class that does not retry has none.
              let reconciles = not (starts "none" ((sOr "reconciliation" c).Trim().ToLowerInvariant()))
              let noRetry = starts "none" ((sOr "retry" c).Trim().ToLowerInvariant())
              match intOpt "maxAttempts" c, strOpt "boundedBy" c with
              | None, _ -> yield sprintf "%s: maxAttempts is missing" id
              | Some n, None when n <> 0L -> yield sprintf "%s: %d attempts with no bound named (boundedBy)" id n
              | Some n, Some _ when noRetry && n <> 0L -> yield sprintf "%s: retry is none but maxAttempts is %d" id n
              | Some n, Some b ->
                  match intOpt b bounds with
                  | None -> yield sprintf "%s: bound %s is not defined" id b
                  | Some limit when n < 0L || n > limit -> yield sprintf "%s: maxAttempts %d exceeds its bound %s (%d)" id n b limit
                  | _ -> ()
                  match b with
                  | "automaticRebases" when not (starts "StaleHead" (sOr "typedError" c)) -> yield sprintf "%s: only the stale-head class is bounded by automaticRebases" id
                  | "largeFileRereads" when id <> "GH-LARGE-FILE" -> yield sprintf "%s: only the large-file class is bounded by largeFileRereads" id
                  | "automaticReconciliationReads" when not reconciles -> yield sprintf "%s: bounded by reconciliation reads but does not reconcile" id
                  | _ -> ()
              | _ -> ()
              let head = (sOr "typedError" c).Split(' ').[0]
              if head <> "none" && not (List.contains head errorCases) then yield sprintf "%s: typed error %s is not a case of IF41-ERRORS ProviderError" id (sOr "typedError" c)
              if shared.Contains id && (evidence c).IsEmpty then yield sprintf "%s shares an HTTP status with another class but names no non-status evidence" id
              let reqs = strs "requirements" c |> Set.ofList
              for t in strs "tests" c do
                  match testRequirements.TryFind t with
                  | None -> yield sprintf "%s: test %s is not bound in the v4 plan" id t
                  | Some rs when Set.intersect rs reqs |> Set.isEmpty -> yield sprintf "%s: test %s verifies none of the class's requirements" id t
                  | _ -> ()
          let used = classes |> List.map (fun c -> (sOr "typedError" c).Split(' ').[0]) |> Set.ofList
          for case in errorCases do
              if not (used.Contains case) then yield sprintf "ProviderError.%s is produced by no error class" case
          // Each class sharing a status names evidence that every other class with that status lacks.
          let sharing = classes |> List.filter (fun c -> shared.Contains (str "id" c))
          for a in sharing do
              let rivals = sharing |> List.filter (fun b -> str "id" b <> str "id" a && not (Set.intersect (arr "httpStatuses" a |> Set.ofList) (arr "httpStatuses" b |> Set.ofList) |> Set.isEmpty))
              let others = rivals |> List.collect evidence |> Set.ofList
              if not rivals.IsEmpty && evidence a |> List.forall others.Contains then
                  yield sprintf "%s names no evidence that distinguishes it from %s" (str "id" a) (rivals |> List.map (str "id") |> String.concat ", ") ]

let formaCheck (i: Inputs) : Check =
    let inv = i.FormaInventory
    let has (key: string) (x: string) = List.contains x (strs key inv)
    let forma = field "forma" i.Forma |> Option.defaultValue JNull
    let tests = allTestIds i.Plan
    let text (r: Json) = sOr "rule" r + " " + sOr "assertion" r
    let found (rx: string) (r: Json) = Regex.Matches(text r, rx) |> Seq.map (fun m -> m.Value) |> List.ofSeq
    check "X07" "The Forma layout contract uses only custom properties, classes, patterns and breakpoints of the published Forma release, names no colour literal without a requirement source, and the committed inventory equals the one extracted from the package"
        [ if sOr "tarballSha256" inv <> sOr "tarballSha256" forma || sOr "version" inv <> sOr "version" forma then yield "the inventory is for another Forma package than the layout contract names"
          match i.FormaExtract with
          | Some x when serialize x <> serialize inv -> yield "the committed Forma inventory differs from the one extracted from the published package"
          | _ -> ()
          for t in field "tokens" i.Forma |> Option.map (strs "required") |> Option.defaultValue [] do
              if not (has "customProperties" t) then yield sprintf "token %s is not defined by Forma %s" t (sOr "version" forma)
          for r in arr "rules" i.Forma do
              let id = sOr "id" r
              // A name followed by '*' is a family: only the --ef- family is Forma's.
              let named = Regex.Matches(text r, @"--[a-z][a-z0-9-]*") |> Seq.map (fun m -> m.Value, (text r).Length > m.Index + m.Length && (text r).[m.Index + m.Length] = '*') |> Seq.distinct |> List.ofSeq
              for (p, family) in named |> List.filter (fun (p, family) -> family && p.TrimEnd('-') <> "--ef") do
                  yield sprintf "%s names custom property family %s*, which is not Forma's" id p
              for (p, _) in named |> List.filter (fun (p, family) -> not family && not (has "customProperties" (p.TrimEnd('-')))) do
                  yield sprintf "%s names custom property %s, which Forma does not define" id p
              for c in found @"(?<![\w-])\.?ef-[a-z0-9_-]*[a-z0-9]" r |> List.map (fun c -> c.TrimStart('.')) |> List.distinct do
                  if not (has "classes" c) then yield sprintf "%s names class %s, which Forma does not style" id c
              for c in found @"(?<![\w/.])\.[a-z][a-z0-9_-]*[a-z0-9]" r |> List.distinct do
                  if not (starts ".ef-" c) then yield sprintf "%s names class %s, which is not a Forma class" id c
              // A colour literal is allowed only when its source names a requirement document that contains it.
              let sourced (l: Json) =
                  let m = Regex.Match(sOr "source" l, @"docs/[A-Za-z0-9_./-]+\.md")
                  m.Success && (i.ReadSource m.Value |> Option.map (fun t -> t.Contains(sOr "value" l)) |> Option.defaultValue false)
              let literals = arr "literals" r |> List.filter sourced |> List.map (sOr "value")
              for l in arr "literals" r do
                  if not (sourced l) then yield sprintf "%s: literal %s does not appear in the document its source names" id (sOr "value" l)
              for h in found @"#[0-9A-Fa-f]{3,8}\b|\b(?:rgb|rgba|hsl|hsla)\([^)]*\)" r |> List.distinct do
                  if not (List.contains h literals) then yield sprintf "%s names colour %s with no requirement source" id h
              if (arr "tests" r).IsEmpty then yield sprintf "%s has no test" id
              for t in strs "tests" r do
                  if not (tests.Contains t) then yield sprintf "%s: test %s is not bound in the v4 plan" id t
          for r in arr "reuse" i.Forma do
              for p in strs "patterns" r do
                  if not (has "patterns" p) then yield sprintf "reuse '%s': pattern %s is not a Forma pattern" (sOr "need" r) p
          for b in field "breakpoints" i.Forma |> Option.map (strs "values") |> Option.defaultValue [] do
              if not (has "breakpoints" b) then yield sprintf "breakpoint %s is not used by Forma's stylesheet" b
          for v in arr "viewports" i.Forma do
              if (intOpt "width" v |> Option.defaultValue 0L) <= 0L || (intOpt "height" v |> Option.defaultValue 0L) <= 0L then yield sprintf "viewport %s has no size" (sOr "id" v) ]

let private requiredMetrics =
    [ "merge-conflicts"; "overlapping-files"; "conflict-line-ranges"; "prs-needing-resolution"; "resolution-time"; "contract-test-failures"
      "integration-test-failures"; "reverted-integrations"; "multi-agent-files"; "shared-file-modifications"; "ownership-violations"; "waiting-time" ]

let telemetryCheck (i: Inputs) : Check =
    let metrics = arr "metrics" i.Telemetry
    let kinds = metrics |> List.map (sOr "kind")
    let policy = field "unavailablePolicy" i.Telemetry |> Option.defaultValue JNull
    check "X08" "Coordination telemetry defines every required metric with a source, keeps textual conflicts apart from semantic failures, and records an unavailable metric as null with a reason, never inferred"
        [ for m in requiredMetrics do
              if not (metrics |> List.exists (fun x -> sOr "id" x = m)) then yield sprintf "metric %s is not defined" m
          for d in duplicates (metrics |> List.map (sOr "id")) do
              yield sprintf "metric %s is defined twice" d
          for m in metrics do
              for k in [ "name"; "definition"; "source" ] do
                  if String.IsNullOrWhiteSpace (sOr k m) then yield sprintf "metric %s: %s is missing" (sOr "id" m) k
              if not (List.contains (sOr "kind" m) [ "textual"; "semantic"; "coordination" ]) then yield sprintf "metric %s: kind '%s' is not textual, semantic or coordination" (sOr "id" m) (sOr "kind" m)
          if not (List.contains "textual" kinds && List.contains "semantic" kinds) then yield "textual conflicts and semantic integration failures are not both measured"
          if field "value" policy <> Some JNull || boolOr "unavailable" false policy <> true || boolOr "reasonRequired" false policy <> true || boolOr "inferenceAllowed" true policy <> false then
              yield "the unavailable-metric policy must be {value: null, unavailable: true, reasonRequired: true, inferenceAllowed: false}" ]

let preservationCheck (i: Inputs) : Check * Json =
    let vp = field "verificationPlan" i.BaseManifest |> Option.defaultValue JNull
    let counts = field "counts" i.BaseManifest |> Option.defaultValue JNull
    let reqs = arr "requirements" i.Baseline
    let p0 = reqs |> List.filter (fun r -> sOr "priority" r = "P0")
    let actual =
        [ "requirements", reqs.Length
          "p0Requirements", p0.Length
          "obligations", reqs |> List.sumBy (fun r -> (arr "obligations" r).Length)
          "p0Obligations", p0 |> List.sumBy (fun r -> (arr "obligations" r).Length) ]
    let planReqs = arr "requirements" i.Plan |> List.map (str "requirement") |> Set.ofList
    let planObligations = arr "requirements" i.Plan |> List.collect (arr "obligations") |> List.map (str "sourceObligation") |> Set.ofList
    let unbound = arr "requirements" i.Plan |> List.collect (arr "obligations") |> List.filter (fun o -> (field "testIdentity" o |> Option.map (sOr "status")) <> Some "bound") |> List.map (str "sourceObligation")
    let planSha = Git.sha256Hex i.PlanBytes
    let problems =
        [ if planSha <> sOr "fileSha256" vp then yield sprintf "the verification plan (sha256 %s) is not v4's (%s)" planSha (sOr "fileSha256" vp)
          for (k, n) in actual do
              if intOpt k counts <> Some(int64 n) then yield sprintf "%s: the baseline has %d, the v4 manifest records %A" k n (intOpt k counts)
          for r in p0 do
              if not (planReqs.Contains (str "id" r)) then yield sprintf "P0 requirement %s is not in the plan" (str "id" r)
              for o in arr "obligations" r do
                  let oid = match o with JStr s -> s | o -> sOr "id" o
                  if not (planObligations.Contains oid) then yield sprintf "P0 obligation %s is not in the plan" oid
          for o in unbound do
              yield sprintf "plan obligation %s is not bound to a test" o ]
    let report =
        JObj
            [ "schema", JStr "indy-init.v41-preservation-report/1"
              "description", JStr "Proof that v4.1 changes no requirement, obligation or test binding of v4, generated by requirements-compiler v41: v4.1 uses v4's verification plan byte for byte, and the baseline counts equal v4's manifest."
              "verificationPlan", JObj [ "planId", JStr(sOr "planId" vp); "fileSha256", JStr planSha; "v4FileSha256", JStr(sOr "fileSha256" vp); "unchanged", JBool(planSha = sOr "fileSha256" vp) ]
              "counts", JObj(actual |> List.map (fun (k, n) -> k, JObj [ "v41", jint n; "v4", (intOpt k counts |> Option.map JInt |> Option.defaultValue JNull) ]))
              "boundObligations", jint (planObligations.Count - (List.distinct unbound).Length)
              "boundTests", jint (allTestIds i.Plan).Count
              "removed", JObj [ "requirements", JArr []; "obligations", JArr []; "testBindings", JArr [] ]
              "result", JStr(if problems.IsEmpty then "PRESERVED" else "NOT-PRESERVED") ]
    check "X10" "Every requirement, P0 obligation and test binding of v4 is preserved unchanged" problems, report

let provenanceCheck (i: Inputs) : Check =
    let forbidden = Regex(@"(?i)indy[-_ .]?init[-_ .]?rehearsal[-_ .]?0*[1-6](?![0-9])|rehearsal[-_ .]?0*[1-6]/(src|app|web|lib|tests?)/")
    check "X11" "No v4.1 input or document imports or names application source from an earlier rehearsal (Rehearsals 1-6)"
        [ for (name, text) in i.AuthoredText do
              for m in forbidden.Matches(text) do
                  yield sprintf "%s names %s" name m.Value ]

let testIdentityCheck (i: Inputs) (files: OwnedFile list) : Check =
    let owned = files |> List.map (fun f -> f.Path) |> Set.ofList
    let all = (boundTests i.Plan |> List.map (fun b -> b.Test)) @ (namedTests i |> List.map (fun (t, _, _) -> t)) |> List.distinct
    check "X14" "Every test identity (plan, contract, state-machine, unit) names an owned test file, or is a Praxis record that is not code"
        [ for t in all do
              match testFile t with
              | Some f when not (owned.Contains f) -> yield sprintf "%s: file %s has no owner" t f
              | None when not (starts "praxis:" t) -> yield sprintf "%s names no test file" t
              | _ -> () ]

/// The executable layout kit: every critical Forma rule names assertions the kit exports, each with a broken fixture,
/// run against Forma patterns at contract viewports; every Forma gap names an executable pair and a remedy using only
/// Forma tokens. CI runs the kit (layout-tests/run.mjs) in Chromium and WebKit.
let layoutKitCheck (i: Inputs) : Check =
    let text name = i.AuthoredText |> List.tryFind (fst >> (=) name) |> Option.map snd |> Option.defaultValue ""
    let exported = Regex.Matches(text "layout-tests/assertions.mjs", @"(?m)^export const ([A-Za-z0-9]+)\s*=") |> Seq.map (fun m -> m.Groups.[1].Value) |> Set.ofSeq
    let section (src: string) (start: string) (stop: string) =
        let a = src.IndexOf start
        if a < 0 then "" else let b = src.IndexOf(stop, a + start.Length) in if b < 0 then src.Substring a else src.Substring(a, b - a)
    let keys (src: string) = Regex.Matches(src, @"(?m)^\s+([A-Za-z0-9]+):\s*['\[]") |> Seq.map (fun m -> m.Groups.[1].Value) |> Set.ofSeq
    let fixturesText = text "layout-tests/fixtures.mjs"
    let fixtures = keys (section fixturesText "export const brokenFixtures" "export const conformingFixtures")
    let conforming = keys (section fixturesText "export const conformingFixtures" "\n};")
    let patterns = strs "patterns" i.FormaInventory |> Set.ofList
    let properties = strs "customProperties" i.FormaInventory |> Set.ofList
    let viewports = arr "viewports" i.Forma |> List.map (sOr "id") |> Set.ofList
    let pairs = arr "rules" i.Forma |> List.collect (fun r -> arr "executable" r |> List.collect (fun x -> strs "patterns" x |> List.map (fun p -> sOr "assertion" x, p)))
    check "X15" "Every critical Forma layout rule has executable browser assertions, each exported by the layout kit with a broken fixture that must fail, run against published Forma patterns at contract viewports; every Forma gap has a remedy that uses only Forma tokens"
        [ if String.IsNullOrWhiteSpace (text "layout-tests/run.mjs") then yield "the layout kit runner is missing"
          let copy = field "executableTests" i.Forma |> Option.map (sOr "eventBuildCopy") |> Option.defaultValue ""
          if copy = "" || not (arr "testHelpers" i.Structure |> List.exists (fun h -> sOr "path" h = copy && sOr "owner" h = "integration")) then
              yield sprintf "the event-build copy of the layout assertions (%s) is not an integration-owned test helper" copy
          // The coverage the optimization report states is the coverage the contract and the kit have.
          let rules = arr "rules" i.Forma
          let invariantCount = rules |> List.sumBy (arr "invariants" >> List.length)
          let assertionCount = rules |> List.collect (arr "invariants" >> List.collect (strs "assertions")) |> List.distinct |> List.length
          let brokenPages = Regex.Matches(section fixturesText "export const brokenFixtures" "export const conformingFixtures", @"(?m)^    '").Count
          let report = text "optimization-report.md"
          let stated = Regex.Match(report, @"(\d+) of (\d+) rules, (\d+) listed invariants, (\d+) assertions, (\d+) broken pages")
          let actual = [ rules.Length; rules.Length; invariantCount; assertionCount; brokenPages ]
          if not stated.Success then yield "optimization-report.md does not state the layout kit's coverage (N of N rules, N listed invariants, N assertions, N broken pages)"
          elif [ 1..5 ] |> List.map (fun k -> int stated.Groups.[k].Value) <> actual then
              yield sprintf "optimization-report.md states '%s'; the contract and kit have %d of %d rules, %d listed invariants, %d assertions, %d broken pages" stated.Value rules.Length rules.Length invariantCount assertionCount brokenPages
          for m in Regex.Matches(report, @"must fail in CI \((\d+)\)") do
              if int m.Groups.[1].Value <> brokenPages then yield sprintf "optimization-report.md states %s broken pages; the kit has %d" m.Groups.[1].Value brokenPages
          for r in arr "rules" i.Forma do
              let id = sOr "id" r
              // Every layout rule is critical: none may opt out of executable assertions.
              if not (boolOr "critical" false r) then yield sprintf "%s is not marked critical; every layout rule is" id
              if (arr "executable" r).IsEmpty then yield sprintf "%s names no executable assertion" id
              // Every stated invariant names assertions the rule runs, and every assertion it runs tests an invariant.
              let runs = arr "executable" r |> List.map (sOr "assertion") |> Set.ofList
              let invariants = arr "invariants" r
              if invariants.IsEmpty then yield sprintf "%s lists no invariants" id
              for v in invariants do
                  if (strs "assertions" v).IsEmpty then yield sprintf "%s: invariant %s names no assertion" id (sOr "id" v)
                  for a in strs "assertions" v do
                      if not (runs.Contains a) then yield sprintf "%s: invariant %s needs assertion %s, which the rule does not run" id (sOr "id" v) a
              let covering = invariants |> List.collect (strs "assertions") |> Set.ofList
              for a in runs do
                  if not (covering.Contains a) then yield sprintf "%s: assertion %s tests none of the rule's invariants" id a
              for x in arr "executable" r do
                  let a = sOr "assertion" x
                  if not (exported.Contains a) then yield sprintf "%s: assertion %s is not exported by layout-tests/assertions.mjs" id a
                  if not (fixtures.Contains a) then yield sprintf "%s: assertion %s has no broken fixture" id a
                  if (strs "patterns" x).IsEmpty && (arr "composed" x).IsEmpty && (strs "fixtures" x).IsEmpty then yield sprintf "%s: assertion %s runs against no page" id a
                  for f in strs "fixtures" x do
                      if not (conforming.Contains f) then yield sprintf "%s: conforming fixture %s does not exist" id f
                  for p in arr "composed" x |> List.collect (fun c -> match c with JArr ps -> ps |> List.map asStr | _ -> []) do
                      if not (patterns.Contains p) then yield sprintf "%s: composed pattern %s is not a Forma pattern" id p
                  for p in strs "patterns" x do
                      if not (patterns.Contains p) then yield sprintf "%s: %s is not a Forma pattern" id p
                  match field "viewports" x with
                  | Some(JStr "all") -> ()
                  | Some(JArr vs) -> for v in vs |> List.map asStr do if not (viewports.Contains v) then yield sprintf "%s: viewport %s is not in the contract" id v
                  | _ -> yield sprintf "%s: assertion %s names no viewports" id a
                  for c in strs "colorSchemes" x do
                      if c <> "light" && c <> "dark" then yield sprintf "%s: colour scheme %s is not light or dark" id c
          for g in arr "formaGaps" i.Forma do
              let key = sOr "assertion" g, sOr "pattern" g
              if not (List.contains key pairs) then yield sprintf "Forma gap %s on %s names no executable run" (fst key) (snd key)
              if String.IsNullOrWhiteSpace (sOr "finding" g) || String.IsNullOrWhiteSpace (sOr "remedyCss" g) then yield sprintf "Forma gap %s on %s needs a finding and a remedy" (fst key) (snd key)
              if Regex.IsMatch(sOr "remedyCss" g, @"--ef-[a-z0-9-]+\s*:") then yield sprintf "Forma gap %s on %s: the remedy declares an --ef-* property" (fst key) (snd key)
              for m in Regex.Matches(sOr "remedyCss" g, @"--[a-z][a-z0-9-]*") do
                  if not (properties.Contains m.Value) then yield sprintf "Forma gap %s on %s: the remedy uses %s, which Forma does not define" (fst key) (snd key) m.Value
              for v in strs "viewports" g do
                  if not (viewports.Contains v) then yield sprintf "Forma gap %s on %s: viewport %s is not in the contract" (fst key) (snd key) v ]

// ---- bootstrap ---------------------------------------------------------------------------------------------------

let private projectNamespace (path: string) =
    // src/Causa.App/Views/Shell.fs -> Causa.App.Views; tests/Causa.Acceptance/Live/X.fs -> Causa.Acceptance.Live
    let parts = path.Split('/')
    parts.[1 .. parts.Length - 2] |> Array.map (fun p -> p.Replace("-", "")) |> String.concat "."

/// The test tooling the owner locked: analysis/project-structure.json testTooling, one lock per dependency-lock decision
/// (ODR-V41-07, and any later one), as (id, version) pairs of NuGet packages for the test projects and npm packages for
/// package.json.
let private lockEntry (entry: string) =
    match entry.LastIndexOf '@' with
    | n when n > 0 -> Some(entry.Substring(0, n), entry.Substring(n + 1))
    | _ -> None

let testTooling (i: Inputs) =
    let tooling = field "testTooling" i.Structure |> Option.defaultValue JNull
    let entries key = arr "locks" tooling |> List.collect (strs key) |> List.choose lockEntry
    entries "nuget", entries "npm", strs "testProjects" tooling

/// The test projects that host .NET tests (one of their files holds a dotnet: test): exactly these take the NuGet test
/// tooling; a helper library such as Causa.TestSupport takes none.
let testHostProjects (i: Inputs) (files: OwnedFile list) : string list =
    let testProjects = arr "testProjects" i.Structure |> List.map (str "id") |> Set.ofList
    files
    |> List.filter (fun f -> f.Tests |> List.exists (starts "dotnet:"))
    |> List.choose (fun f -> f.Project)
    |> List.filter testProjects.Contains
    |> List.distinct
    |> List.sort

let testToolingCheck (i: Inputs) (files: OwnedFile list) : Check =
    let tooling = field "testTooling" i.Structure |> Option.defaultValue JNull
    let locks = arr "locks" tooling
    let all key = locks |> List.collect (strs key)
    let hosts = testHostProjects i files
    check "X16" "The test tooling is a well-formed owner lock: each lock names a dependency-lock decision and its approval, each entry is an exact id@version, no scoped npm name is in the NuGet list, no package is locked twice, and the NuGet packages go to exactly the test projects that host .NET tests (the offline suite compares each lock, and each package's manager, with the owner's record)"
        [ if locks.IsEmpty then yield "testTooling holds no lock"
          for l in locks do
              let d = sOr "decision" l
              if not (requestOfKind i "dependency-lock" d) then yield sprintf "a testTooling lock names decision '%s', which is not a dependency-lock request" d
              if String.IsNullOrWhiteSpace (sOr "approval" l) then yield sprintf "the testTooling lock of %s names no operator approval" d
              for key in [ "nuget"; "npm" ] do
                  for e in strs key l do
                      if not (Regex.IsMatch(e, @"^(@[^@\s/]+/)?[^@\s/]+@[0-9]+(\.[0-9]+)+$")) then yield sprintf "testTooling %s entry '%s' is not an exact id@version" key e
              // A scoped @x/y name is npm's: NuGet ids have no '@'. Which manager each recorded package belongs to is
              // compared with the owner's record by the offline suite.
              for e in strs "nuget" l do
                  if e.StartsWith("@", StringComparison.Ordinal) then yield sprintf "testTooling nuget entry '%s' is an npm package" e
          for key in [ "nuget"; "npm" ] do
              for d in duplicates (all key |> List.choose lockEntry |> List.map fst) do
                  yield sprintf "testTooling locks %s package %s twice" key d
          if (all "nuget").IsEmpty then yield "testTooling locks no NuGet test package"
          if List.sort (strs "testProjects" tooling) <> hosts then
              yield sprintf "testTooling applies to [%s]; the test projects that host .NET tests are [%s]" (String.concat ", " (List.sort (strs "testProjects" tooling))) (String.concat ", " hosts) ]

/// The empty application repository as integration writes it at bootstrap (T10): the solution, every source and test
/// project with its complete Compile list and references, every planned file as an empty namespace, the domain stubs
/// and each interface contract in the file that implements it. No behaviour, and no third-party package except the test
/// tooling the owner locked (testTooling): its NuGet versions in Directory.Packages.props (central package management),
/// a version-less reference in each test project that hosts .NET tests, and its npm packages in package.json. Every
/// module is written by its owner. Building it proves the project graph, the compile order, the contract placement and
/// the test tooling restore.
let bootstrapFiles (i: Inputs) (files: OwnedFile list) (globalJson: string) : (string * string) list =
    let lists = compileLists i files |> Map.ofList
    let interfaces = arr "interfaces" i.Interfaces |> List.map (fun x -> sOr "file" x, x) |> Map.ofList
    let stubs = field "domainStubs" i.Interfaces |> Option.map (sOr "fsharp") |> Option.defaultValue ""
    let projects = (arr "projects" i.Structure |> List.map (fun p -> p, false)) @ (arr "testProjects" i.Structure |> List.map (fun p -> p, true))
    let pathOf id = projects |> List.tryFind (fun (p, _) -> str "id" p = id) |> Option.map (fst >> str "path") |> Option.defaultValue ""
    let dirOf (path: string) = path.Substring(0, path.LastIndexOf '/')
    let relative (fromDir: string) (path: string) =
        let a = fromDir.Split('/') |> List.ofArray
        let b = path.Split('/') |> List.ofArray
        let common = List.zip (List.truncate (min a.Length b.Length) a) (List.truncate (min a.Length b.Length) b) |> List.takeWhile (fun (x, y) -> x = y) |> List.length
        (List.replicate (a.Length - common) ".." @ List.skip common b) |> String.concat "/"
    let nuget, npm, toolingProjects = testTooling i
    let fsproj (p: Json) =
        let dir = dirOf (str "path" p)
        let compiles = lists.TryFind (str "id" p) |> Option.defaultValue [] |> List.map (fun f -> sprintf "    <Compile Include=\"%s\" />" (relative dir f))
        let refs = strs "references" p |> List.map (fun r -> sprintf "    <ProjectReference Include=\"%s\" />" (relative dir (pathOf r)))
        let packages =
            if List.contains (str "id" p) toolingProjects then nuget |> List.map (fun (id, _) -> sprintf "    <PackageReference Include=\"%s\" />" id) else []
        String.concat "\n"
            ([ "<Project Sdk=\"Microsoft.NET.Sdk\">"; "  <PropertyGroup>"; "    <TargetFramework>net10.0</TargetFramework>"; "  </PropertyGroup>"; "  <ItemGroup>" ]
             @ compiles @ [ "  </ItemGroup>" ]
             @ (if refs.IsEmpty then [] else [ "  <ItemGroup>" ] @ refs @ [ "  </ItemGroup>" ])
             @ (if packages.IsEmpty then [] else [ "  <ItemGroup>" ] @ packages @ [ "  </ItemGroup>" ])
             @ [ "</Project>"; "" ])
    let csproj (p: Json) =
        let dir = dirOf (str "path" p)
        let refs = strs "references" p |> List.map (fun r -> sprintf "    <ProjectReference Include=\"%s\" />" (relative dir (pathOf r)))
        String.concat "\n" ([ "<Project Sdk=\"Microsoft.NET.Sdk\">"; "  <PropertyGroup>"; "    <TargetFramework>net10.0</TargetFramework>"; "  </PropertyGroup>"; "  <ItemGroup>" ] @ refs @ [ "  </ItemGroup>"; "</Project>"; "" ])
    let firstDomainFile = lists.TryFind "Causa.Domain" |> Option.bind List.tryHead
    let source (f: OwnedFile) =
        let header = sprintf "// Owner: %s. Written at bootstrap by requirements-compiler v41; the owner replaces this content." f.Owner
        match interfaces.TryFind f.Path with
        | Some x -> f.Path, header + "\n" + (str "fsharp" x).TrimEnd() + "\n"
        | None when Some f.Path = firstDomainFile -> f.Path, header + "\n// The domain stubs of the interface contracts; the domain owner implements the full shapes.\n" + stubs.TrimEnd() + "\n"
        | None -> f.Path, sprintf "%s\nnamespace %s\n" header (projectNamespace f.Path)
    let fsFiles = files |> List.filter (fun f -> f.Path.EndsWith(".fs", StringComparison.Ordinal) && not (starts "verifier/" f.Path) && f.Project.IsSome) |> List.map source
    let host = files |> List.filter (fun f -> f.Path.EndsWith(".cs", StringComparison.Ordinal)) |> List.map (fun f -> f.Path, sprintf "// Owner: %s. Written at bootstrap by requirements-compiler v41.\nnamespace Causa.Host;\n\ninternal static class HostShim\n{\n}\n" f.Owner)
    let projectFiles = projects |> List.map (fun (p, _) -> str "path" p, (if (str "path" p).EndsWith(".csproj", StringComparison.Ordinal) then csproj p else fsproj p))
    let slnx =
        String.concat "\n" ([ "<Solution>" ] @ (projects |> List.map (fun (p, _) -> sprintf "  <Project Path=\"%s\" />" (str "path" p))) @ [ "</Solution>"; "" ])
    let props = String.concat "\n" [ "<Project>"; "  <PropertyGroup>"; "    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>"; "    <Nullable>enable</Nullable>"; "  </PropertyGroup>"; "</Project>"; "" ]
    let layoutCopy =
        match field "executableTests" i.Forma |> Option.map (sOr "eventBuildCopy"), i.AuthoredText |> List.tryFind (fst >> (=) "layout-tests/assertions.mjs") with
        | Some path, Some(_, text) when path <> "" -> [ path, text ]
        | _ -> []
    let gitignore = String.concat "\n" [ "bin/"; "obj/"; "node_modules/"; "test-results/"; "playwright-report/"; "*.trx"; "" ]
    let packageJson =
        serialize (JObj [ "name", JStr "causa"; "private", JBool true; "devDependencies", JObj(npm |> List.map (fun (id, v) -> id, JStr v)) ])
    // v4.1.1 layers onto Conditor's repository, whose own projects (App.slnx) pin their package versions inline: central
    // package management then applies to the Causa projects only, so both solutions build (measured: an unconditional
    // switch fails App.slnx with NU1008).
    let layered = i.Amendment |> Option.bind (field "bootstrap") |> Option.bind (strOpt "layersOnto") = Some "conditor"
    let cpm =
        if layered then "    <ManagePackageVersionsCentrally Condition=\"$(MSBuildProjectName.StartsWith('Causa.'))\">true</ManagePackageVersionsCentrally>"
        else "    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>"
    let centralVersions =
        String.concat "\n"
            ([ "<Project>"; "  <PropertyGroup>"; cpm; "  </PropertyGroup>"; "  <ItemGroup>" ]
             @ (nuget |> List.map (fun (id, v) -> sprintf "    <PackageVersion Include=\"%s\" Version=\"%s\" />" id v))
             @ [ "  </ItemGroup>"; "</Project>"; "" ])
    [ ".gitignore", gitignore; "Causa.slnx", slnx; "Directory.Build.props", props; "global.json", globalJson ]
    @ (if nuget.IsEmpty then [] else [ "Directory.Packages.props", centralVersions ])
    @ (if npm.IsEmpty then [] else [ "package.json", packageJson ])
    @ projectFiles @ fsFiles @ host @ layoutCopy

// ---- contracts ---------------------------------------------------------------------------------------------------

/// The frozen interface contracts as an F# library, one file per block in compile order, so CI can typecheck them
/// and producers can commit them at the interface freeze.
let contractFiles (i: Inputs) : (string * string) list =
    let stubs = field "domainStubs" i.Interfaces |> Option.defaultValue JNull
    let blocks = (sOr "id" stubs, sOr "fsharp" stubs) :: (arr "interfaces" i.Interfaces |> List.map (fun x -> str "id" x, str "fsharp" x))
    // A positive control after the contracts: names that occur in several unions resolve without ambiguity, because
    // the machines' input unions and NetworkState require qualified access (review RV41-4-6).
    let usage =
        "99-Usage.fs",
        String.concat "\n"
            [ "// Generated by requirements-compiler v41. Positive control: these uses must compile unambiguously."
              "namespace Causa.App"
              ""
              "module ContractUsage ="
              "    open Causa.Persistence"
              ""
              "    let lostBeforeSending (outcome: HttpOutcome) ="
              "        match outcome with"
              "        | NotAnswered(_, FailedBeforeSending) -> true"
              "        | _ -> false"
              ""
              "    let saveInput = SaveInput.FailedBeforeSending"
              "    let networkOffline = NetworkState.Offline"
              "    let saveOffline = SaveState.Offline"
              "    let saveConflict (view: ConflictView) = SaveState.Conflict view"
              ""
              "    let versionClash (decision: Causa.Domain.Decision) ="
              "        match decision with"
              "        | Causa.Domain.Conflict(expected, actual) -> Some(expected, actual)"
              "        | _ -> None"
              "    let connection = ConnectionState.CredentialRefused"
              "" ]
    let named = (blocks |> List.mapi (fun n (id, src) -> sprintf "%02d-%s.fs" n id, src.TrimEnd() + "\n")) @ [ usage ]
    let fsproj =
        let compiles = named |> List.map (fun (f, _) -> sprintf "    <Compile Include=\"%s\" />" f) |> String.concat "\n"
        String.concat "\n"
            [ "<!-- Generated by requirements-compiler v41 from analysis/interface-specifications.json. Typechecks the frozen interface contracts; not part of the application. -->"
              "<Project Sdk=\"Microsoft.NET.Sdk\">"
              "  <PropertyGroup>"
              "    <TargetFramework>net10.0</TargetFramework>"
              "    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>"
              "    <Nullable>enable</Nullable>"
              "    <GenerateDocumentationFile>false</GenerateDocumentationFile>"
              "  </PropertyGroup>"
              "  <ItemGroup>"
              compiles
              "  </ItemGroup>"
              "</Project>"
              "" ]
    // A negative control CI compiles after the contracts: it must be refused, because Confirmation's representation is
    // private to its module (PERSIST-023).
    let forge =
        String.concat "\n"
            [ "// Generated by requirements-compiler v41. Negative control: this file must NOT compile (FS1093). CI adds it to"
              "// ContractCheck.fsproj and expects the build to fail, proving that no file can forge a Confirmation."
              "namespace Causa.Persistence"
              ""
              "module ForgeConfirmation ="
              "    let forged : Confirmation.T ="
              "        { Investigation = Causa.Domain.InvestigationId System.Guid.Empty"
              "          Commit = CommitOid \"forged\""
              "          CommittedAt = System.DateTimeOffset.MinValue }"
              "" ]
    let readCredential =
        String.concat "\n"
            [ "// Generated by requirements-compiler v41. Negative control: this file must NOT compile (FS1093). CI adds it to"
              "// ContractCheck.fsproj and expects the build to fail, proving that no file can read a credential's token."
              "namespace Causa.App"
              ""
              "module ReadCredential ="
              "    let token (c: Causa.Persistence.Credential.T) ="
              "        match c with"
              "        | Causa.Persistence.Credential.Token t -> t"
              "" ]
    (named |> List.map (fun (f, t) -> "contracts/" + f, t))
    @ [ "contracts/ContractCheck.fsproj", fsproj; "contracts/negative/ForgeConfirmation.fs", forge; "contracts/negative/ReadCredential.fs", readCredential ]

// ---- successor amendment (v4.1.1) ------------------------------------------------------------------------------

/// A member of a contract that is code rather than declaration: its enclosing module and type path, its symbol, and
/// its full text (the declaring line and every deeper-indented line under it), which the amendment pins by digest.
type ContractMember = { Scope: string; Symbol: string; Text: string }

/// Comments, string literals and character literals blanked, line structure kept, following the F# lexer's order:
/// a string or character literal is read before a comment can start, so `"(*"` and `'"'` hide nothing; triple-quoted
/// and verbatim strings and nested block comments are handled. `(*)` is the multiplication operator, not a comment.
let private stripCode (text: string) =
    let s = text.Replace("\r\n", "\n")
    let n = s.Length
    let at (i: int) (p: string) = i + p.Length <= n && String.CompareOrdinal(s, i, p, 0, p.Length) = 0
    let blank (c: char) = if c = '\n' then '\n' else ' '
    // Returns the index just past the construct that starts at i, or i when none starts there.
    let rec skipBlock (i: int) (depth: int) =
        if i >= n then n
        elif at i "(*" && not (at i "(*)") then skipBlock (i + 2) (depth + 1)
        elif at i "*)" then (if depth = 1 then i + 2 else skipBlock (i + 2) (depth - 1))
        else skipBlock (i + 1) depth
    let rec skipString (i: int) =
        if i >= n then n
        elif s.[i] = '\\' then skipString (i + 2)
        elif s.[i] = '"' then i + 1
        else skipString (i + 1)
    let rec skipVerbatim (i: int) =
        if i >= n then n
        elif at i "\"\"" then skipVerbatim (i + 2)
        elif s.[i] = '"' then i + 1
        else skipVerbatim (i + 1)
    let skipTriple (i: int) = let e = s.IndexOf("\"\"\"", i, StringComparison.Ordinal) in if e < 0 then n else e + 3
    let construct (i: int) =
        if at i "\"\"\"" then Some(skipTriple (i + 3), "\"\"")
        elif at i "@\"" then Some(skipVerbatim (i + 2), "\"\"")
        elif s.[i] = '"' then Some(skipString (i + 1), "\"\"")
        elif s.[i] = '\'' && i + 2 < n && s.[i + 1] <> '\\' && s.[i + 2] = '\'' then Some(i + 3, "' '")
        elif s.[i] = '\'' && i + 3 < n && s.[i + 1] = '\\' then (let e = s.IndexOf('\'', i + 2) in if e > 0 && e - i <= 8 then Some(e + 1, "' '") else None)
        elif at i "(*" && not (at i "(*)") then Some(skipBlock i 0, "")
        elif at i "//" then (let e = s.IndexOf('\n', i) in Some((if e < 0 then n else e), ""))
        else None
    let rec go (i: int) (acc: Text.StringBuilder) =
        if i >= n then acc.ToString()
        else
            match construct i with
            | Some(j, marker) ->
                // Keep the newlines the construct spanned so line numbers and indentation stay true.
                let span = s.Substring(i, j - i)
                let kept = String(span |> Seq.map blank |> Array.ofSeq)
                go j (acc.Append(if marker = "" then kept else marker + kept.Substring(min kept.Length marker.Length)))
            | None -> go (i + 1) (acc.Append(s.[i]))
    go 0 (Text.StringBuilder())

/// The code members of a contract file. A line is declaration only when it begins as a declaration does (namespace,
/// module, open, type, a union case, a record field or signature `name:`, an attribute, a brace, a type continuation)
/// and contains no code keyword. Every other line is code: a let, member, override, default, static, do, use, new or
/// inherit binding; an active pattern; a primary constructor; an expression; or any line that begins otherwise. This is a
/// heuristic over lines, not the F# syntax tree: the amendment also requires every contract to equal its reviewed
/// baseline byte for byte, and pins each allowed member's text by digest (X17).
let contractImplementations (text: string) : ContractMember list =
    let scopeRx = Regex(@"^\s*(?:module|type)\s+(?:private\s+|internal\s+|rec\s+)*([A-Za-z_][A-Za-z0-9_']*)")
    let declStart =
        Regex(@"^(?:(?:namespace|module|open|type|exception|abstract|interface|with|private|internal)\b|and\s+[A-Z]|\||\{|\}|\[<|->|\*|\)|[A-Za-z_][A-Za-z0-9_']*\s*:(?!:)|[A-Z][A-Za-z0-9_']*\s+of\b|[A-Z][A-Za-z0-9_']*\s*$)")
    let keyword = Regex(@"\b(let|member|override|default|static|do|use|new|inherit|val|fun|function|match|if|then|else|while|for|yield|lazy|async|task)\b")
    let primaryCtor = Regex(@"^\s*type\s+[A-Za-z_][A-Za-z0-9_']*\s*(?:<[^>]*>)?\s*\(")
    let named = Regex(@"\b(?:let|use)\s+(?:(?:rec|inline|private|internal|mutable)\s+)*(\(\|[^)]*\|\)|``[^`]*``|\([^)]*\)|[A-Za-z_][A-Za-z0-9_']*)|\b(?:member|override|default)\s+(?:(?:private|internal|inline)\s+)*(?:[A-Za-z_][A-Za-z0-9_]*\.)?(``[^`]*``|[A-Za-z_][A-Za-z0-9_']*)")
    let indent (l: string) = l.Length - l.TrimStart().Length
    let raw = text.Replace("\r\n", "\n").Split('\n')
    let code = (stripCode text).Split('\n')
    let rows = Array.zip raw code |> List.ofArray
    let rec go (scopes: (int * string) list) (rows: (string * string) list) (found: ContractMember list) =
        match rows with
        | [] -> List.rev found
        | (_, c) :: rest when c.Trim() = "" -> go scopes rest found
        | (r, c) :: rest ->
            let ind = indent c
            let open' = scopes |> List.filter (fun (n, _) -> n < ind)
            let trimmed = c.Trim()
            let signature = trimmed.StartsWith("abstract", StringComparison.Ordinal)
            let k = if signature then None else (let m = keyword.Match trimmed in if m.Success then Some m.Groups.[1].Value else None)
            let recordExpression = trimmed.StartsWith("{", StringComparison.Ordinal) && Regex.IsMatch(trimmed, @"[^<>=:]=[^>]")
            let isCode = k.IsSome || recordExpression || primaryCtor.IsMatch c || not (declStart.IsMatch trimmed)
            let s = scopeRx.Match c
            let scopes' = if s.Success then (ind, s.Groups.[1].Value) :: open' else open'
            if not isCode then go scopes' rest found
            else
                let body = rest |> List.takeWhile (fun (_, bc) -> bc.Trim() = "" || indent bc > ind)
                let text = (r :: (body |> List.map fst)) |> List.map (fun l -> l.TrimEnd()) |> String.concat "\n" |> fun t -> t.TrimEnd()
                let n = named.Match trimmed
                let symbol =
                    if n.Success then (if n.Groups.[1].Success then n.Groups.[1].Value else n.Groups.[2].Value)
                    elif primaryCtor.IsMatch c then "new"
                    else k |> Option.defaultValue "expression"
                let scope = (if s.Success then scopes' else open') |> List.rev |> List.map snd |> String.concat "."
                go scopes' (List.skip body.Length rest) ({ Scope = scope; Symbol = symbol; Text = text } :: found)
    go [] rows []

/// Each generated contract file with its classification: a specification (types and signatures only), a specification
/// with reviewed boundary code (the amendment's allowlist names each member and why), the generated compile control, the
/// typecheck project, or a negative control that must fail to compile.
let contractClassification (i: Inputs) (contracts: (string * string) list) : Json * string list =
    let allowed = amendmentList "contractImplementations" i
    let controls = amendmentList "contractControls" i |> List.map (fun c -> sOr "file" c, c)
    let key (a: Json) = sOr "file" a, sOr "scope" a, sOr "symbol" a
    let interfaces = arr "interfaces" i.Interfaces
    let entries =
        contracts
        |> List.map (fun (path, text) ->
            let name = path.Substring("contracts/".Length)
            let found = if name.EndsWith(".fs", StringComparison.Ordinal) then contractImplementations text |> List.map (fun m -> name, m) else []
            let iface = interfaces |> List.tryFind (fun x -> name.EndsWith("-" + str "id" x + ".fs", StringComparison.Ordinal))
            let kind =
                match controls |> List.tryFind (fst >> (=) name) with
                | Some(_, c) -> sOr "classification" c
                | None when starts "negative/" name -> "negative control: must fail to compile"
                | None when name.EndsWith(".fsproj", StringComparison.Ordinal) -> "typecheck project: builds the contracts and the controls"
                | None when found.IsEmpty -> "specification: types and signatures only"
                | None -> "specification with reviewed boundary code"
            name, found, iface, kind)
    let isControl (name: string) = controls |> List.exists (fst >> (=) name) || starts "negative/" name
    let implemented = entries |> List.filter (fun (n, _, _, _) -> not (isControl n)) |> List.collect (fun (_, f, _, _) -> f)
    let digest (t: string) = Git.sha256Hex (Text.Encoding.UTF8.GetBytes t)
    let baseline = amendmentField "contractBaseline" i |> sOr "package"
    let amends = amendmentField "amends" i
    let problems =
        [ for (n, m) in implemented do
              match allowed |> List.tryFind (key >> (=) (n, m.Scope, m.Symbol)) with
              | None -> yield sprintf "contracts/%s: %s.%s is implementation code in a specification and is not on the reviewed allowlist" n m.Scope m.Symbol
              | Some a when sOr "sha256" a <> digest m.Text -> yield sprintf "contracts/%s: the text of %s.%s has sha256 %s, the allowlist pins %s" n m.Scope m.Symbol (digest m.Text) (sOr "sha256" a)
              | Some _ -> ()
          for a in allowed do
              let (n, s, m) = key a
              if not (implemented |> List.exists (fun (f, x) -> (f, x.Scope, x.Symbol) = (n, s, m))) then yield sprintf "the allowlist names %s %s.%s, which the contracts do not implement (stale entry)" n s m
              if String.IsNullOrWhiteSpace (sOr "rationale" a) || (strs "requirements" a).IsEmpty then
                  yield sprintf "the allowlist entry %s %s.%s needs a rationale and the requirements it enforces" n s m
          // The line scan is a heuristic; the guarantee is that every contract equals its reviewed, approved baseline.
          if baseline = "" then yield "the amendment names no reviewed contract baseline"
          elif baseline <> sOr "package" amends || sOr "approval" (amendmentField "contractBaseline" i) <> sOr "approval" amends then
              yield sprintf "the contract baseline %s is not the package the amendment amends (%s, %s)" baseline (sOr "package" amends) (sOr "approval" amends)
          else
              for (path, text) in contracts do
                  match i.ReadSource (baseline + "/" + path) with
                  | Some b when b = text -> ()
                  | Some _ -> yield sprintf "%s differs from the reviewed baseline %s/%s; a changed contract needs its own review" path baseline path
                  | None -> yield sprintf "%s is not in the reviewed baseline %s" path baseline
          for (c, j) in controls do
              if not (entries |> List.exists (fun (n, _, _, _) -> n = c)) then yield sprintf "the control %s is not a generated contract file" c
              if String.IsNullOrWhiteSpace (sOr "classification" j) then yield sprintf "the control %s has no classification" c
          if allowed.IsEmpty && controls.IsEmpty then yield "the amendment classifies no contract file" ]
    let json =
        JObj
            [ "schema", JStr "indy-init.v411-contract-classification/1"
              "description", JStr "Every generated contract file, classified. The contracts are specifications: frozen types and signatures the implementing roles compile against. The only code they carry is the reviewed boundary code listed here, which enforces a requirement at the type boundary (a credential that never prints its token; a confirmation that exists only from a read-back equal to what was written); each producer replaces nothing in it and implements everything else. Generated by requirements-compiler v41 from the contracts it generates and analysis/amendment.json (X17)."
              "typecheck", JStr "CI builds contracts/ContractCheck.fsproj; each negative control must fail to compile"
              "stateMachines", JStr "X05 proves each machine's states and events are exactly the cases of one interface's unions"
              "materialization", JStr "the bootstrap writes each contract into its producer's file, byte for byte, from the same text; two builds are identical (offline suite)"
              "files",
              JArr(entries |> List.map (fun (n, found, iface, kind) ->
                  JObj
                      [ "file", JStr("contracts/" + n)
                        "classification", JStr kind
                        "interface", (match iface with Some x -> JStr(str "id" x) | None -> JNull)
                        "producerFile", (match iface with Some x -> JStr(sOr "file" x) | None -> JNull)
                        "implementation",
                        JArr(found |> List.map (fun (fn, x) ->
                            let a = allowed |> List.tryFind (key >> (=) (fn, x.Scope, x.Symbol))
                            JObj [ "scope", JStr x.Scope; "symbol", JStr x.Symbol; "sha256", JStr(digest x.Text); "rationale", JStr(a |> Option.map (sOr "rationale") |> Option.defaultValue ""); "requirements", (a |> Option.map (fun x -> jstrs (strs "requirements" x)) |> Option.defaultValue (JArr [])) ])) ])) ]
    json, problems

/// X18 (E): the shared web entrypoints are integration's; a UI role changes one only through a change request
/// integration owns and applies; the ownership manifest and the agent rules say the same.
let entrypointCheck (i: Inputs) (files: OwnedFile list) : Check =
    let spec = amendmentField "sharedEntrypoints" i
    let paths = strs "files" spec
    let cr = field "changeRequests" spec |> Option.defaultValue (JObj [])
    let roles = arr "roles" i.Structure |> List.map (str "id") |> Set.ofList
    let rules = i.AuthoredText |> List.tryFind (fst >> (=) "AGENT-RULES.md") |> Option.map snd |> Option.defaultValue ""
    // The roles table of AGENT-RULES: (role, what it owns). Prose and manifest agree when integration's row lists each
    // entrypoint and no other role's row claims one except to say it asks for changes by change request.
    let rows =
        rules.Split('\n')
        |> List.ofArray
        |> List.filter (fun l -> l.StartsWith("| ", StringComparison.Ordinal))
        |> List.map (fun l -> l.Split('|') |> Array.map (fun c -> c.Trim()))
        |> List.filter (fun cells -> cells.Length > 3)
        |> List.map (fun cells -> cells.[1], cells.[2])
    check "X18" "The shared web entrypoints are integration's alone; a UI role changes one only through a change request that integration owns and applies; the ownership manifest and the agent rules agree"
        [ if paths.IsEmpty then yield "the amendment names no shared web entrypoint"
          for p in paths do
              match files |> List.tryFind (fun f -> f.Path = p) with
              | None -> yield sprintf "%s is not in the ownership manifest" p
              | Some f ->
                  if f.Owner <> "integration" then yield sprintf "%s is a shared entrypoint but owned by %s" p f.Owner
                  if not f.Contributors.IsEmpty then yield sprintf "%s is a shared entrypoint but lists contributors %s" p (String.concat ", " f.Contributors)
              if not (rows |> List.exists (fun (role, owns) -> role = "integration" && owns.Contains p)) then yield sprintf "AGENT-RULES.md's roles table does not list %s under integration" p
              let sentence = sOr "ruleSentence" cr
              for (role, owns) in rows do
                  if role <> "integration" && (if sentence = "" then owns else owns.Replace(sentence, "")).Contains p then yield sprintf "AGENT-RULES.md's roles table names %s under %s outside the change-request sentence" p role
              let reviewed = strs "ruleLinesSha256" spec |> Set.ofList
              for l in rules.Split('\n') do
                  if l.Contains p && not (reviewed.Contains(Git.sha256Hex (Text.Encoding.UTF8.GetBytes(l.TrimEnd())))) then
                      yield sprintf "AGENT-RULES.md names %s in a line that is not one of the reviewed lines (sharedEntrypoints.ruleLinesSha256): %s" p (l.Trim().Substring(0, min 120 (l.Trim().Length)))
          if (sOr "ruleSentence" cr) = "" || not (rules.Contains(sOr "ruleSentence" cr)) then yield "AGENT-RULES.md does not carry the change-request sentence the amendment fixes (sharedEntrypoints.changeRequests.ruleSentence)"
          if sOr "owner" cr <> "integration" then yield "change requests to the shared entrypoints must be owned by integration"
          let requesters = strs "requesters" cr
          if requesters.IsEmpty then yield "the change-request protocol names no requesting role"
          for r in requesters do
              if not (roles.Contains r) then yield sprintf "change-request requester %s is not a declared role" r
              if r = "integration" then yield "integration does not request changes to its own files"
          if String.IsNullOrWhiteSpace (sOr "protocol" cr) || String.IsNullOrWhiteSpace (sOr "record" cr) then yield "the change-request protocol needs its steps and where each request is recorded"
          if not (rules.Contains "change request") then yield "AGENT-RULES.md does not describe the change-request protocol" ]

/// X19 (A, C): the bootstrap runs at T0 inside the timed window with a measured duration, every other timed task
/// follows it, and no task waits on organizer permission; the starting-state policy records the published rules as
/// external constraints, keeps the historical records, and states the residual interpretation risk.
let bootstrapCheck (i: Inputs) : Check =
    let spec = amendmentField "bootstrap" i
    let task = sOr "task" spec
    let tasks = adjustedTasks i
    let timed = tasks |> List.filter (fun t -> t.Phase = "timed")
    let prec = precedences tasks
    let policy = amendmentField "startingState" i
    let measurement = i.AuthoredText |> List.tryFind (fst >> (=) (sOr "measurement" spec)) |> Option.map (snd >> fun t -> parseBytes (Text.Encoding.UTF8.GetBytes t))
    let removed = arr "removedGates" policy
    // Every organizer gate in the frozen v4 inputs and the internal rule documents must have a recorded disposition
    // under an owner decision (RV411-1-1): preflight checks, tasks and requirement obligations that mention organizer
    // approval or confirmation, DAG waivers resting on ODR-V41-06, and each matching line of the scanned documents.
    let refs = arr "organizerReferences" policy
    let decisions = requestIds i @ [ "OD-INDY-18" ]
    let organizerRx = Regex(@"organi[sz]er", RegexOptions.IgnoreCase)
    let gateRx = Regex(@"confirm|approv|verif|permission|permit|interpretation|sign[- ]?off|signs? off|signed|wait|until|hold|obtain|\bask", RegexOptions.IgnoreCase)
    let mentions (j: Json) = match j with JObj ps -> ps |> List.exists (fun (_, v) -> match v with JStr s -> organizerRx.IsMatch s && gateRx.IsMatch s | _ -> false) | _ -> false
    let rec walk (j: Json) : Json list = match j with JObj ps -> j :: (ps |> List.collect (snd >> walk)) | JArr xs -> xs |> List.collect walk | _ -> []
    let fromJson (path: string) (select: Json -> string list) =
        match i.ReadSource path with
        | Some t -> Ok(select (parseBytes (Text.Encoding.UTF8.GetBytes t)))
        | None -> Error(sprintf "%s is not readable for the organizer-gate scan" path)
    let jsonGates =
        [ fromJson "requirements-compiled/v4/preflight-manifest.json" (fun j -> arr "checks" j |> List.filter mentions |> List.map (str "id"))
          fromJson "requirements-compiled/v4/curation/tasks.json" (fun j -> arr "tasks" j |> List.filter (fun t -> organizerRx.IsMatch(sOr "title" t)) |> List.map (str "id"))
          fromJson "requirements-compiled/v4/requirements-inventory.json" (fun j -> walk j |> List.filter (fun x -> (strOpt "id" x).IsSome && mentions x) |> List.map (str "id") |> List.distinct)
          fromJson "requirements-compiled/v4/risk-register.json" (fun j -> arr "risks" j |> List.filter mentions |> List.map (str "id")) ]
    let waiverGates = arr "waivedPrecedences" i.Adjustments |> List.filter (fun w -> (sOr "reason" w).Contains "ODR-V41-06") |> List.map (fun w -> sprintf "waiver %s>%s" (sOr "before" w) (sOr "after" w))
    let docLines =
        strs "organizerDocuments" policy
        |> List.collect (fun d ->
            // A package document is read from the package inputs (so a changed AGENT-RULES is seen); others from the source.
            match i.AuthoredText |> List.tryFind (fst >> (=) d) |> Option.map snd |> Option.orElse (i.ReadSource d) with
            | None -> [ d, 0, "<unreadable>" ]
            | Some t -> t.Split('\n') |> Array.mapi (fun n l -> d, n + 1, l) |> Array.filter (fun (_, _, l) -> organizerRx.IsMatch l && gateRx.IsMatch l) |> List.ofArray)
    check "X19" "The bootstrap is the first timed task (T0), its duration partly measured and labelled so, every other timed task follows it, every organizer gate the scan finds has a recorded disposition under an owner decision, and the starting-state policy quotes the published rules verbatim, keeps the historical records unchanged and states the residual risk"
        [ match timed |> List.tryFind (fun t -> t.Id = task) with
          | None -> yield sprintf "the bootstrap task %s is not in the timed window" task
          | Some t ->
              if t.Owner <> "integration" then yield sprintf "the bootstrap task %s is owned by %s, not integration" task t.Owner
              if t.DependsOn |> List.exists (fun d -> timed |> List.exists (fun x -> x.Id = d.Task)) then yield sprintf "the bootstrap task %s waits on another timed task" task
              for x in timed do
                  if x.Id <> task && not (prec.Contains(task, x.Id)) then yield sprintf "timed task %s can start before the bootstrap %s" x.Id task
              let adj = arr "adjustments" i.Adjustments |> List.tryFind (fun a -> str "task" a = task)
              match adj with
              | Some a when sOr "evidence" a = "measured+estimated" && sOr "measurement" a = sOr "measurement" spec ->
                  let t10 = measurement |> Option.bind (field "t10") |> Option.defaultValue (JObj [])
                  if intOpt "measuredMinutes" a <> intOpt "measuredMinutes" t10 || intOpt "estimatedMinutes" a <> intOpt "estimatedMinutes" t10 then
                      yield sprintf "the measured and estimated minutes of %s differ from the bootstrap measurement record" task
              | _ -> yield sprintf "the estimate of %s is not labelled measured+estimated with the bootstrap measurement record" task
          match measurement with
          | None -> yield "the bootstrap measurement record is missing"
          | Some m ->
              if (arr "runs" m).IsEmpty then yield "the bootstrap measurement records no run"
              if (arr "steps" m).IsEmpty then yield "the bootstrap measurement names no step"
              for s in arr "steps" m do
                  if String.IsNullOrWhiteSpace (sOr "command" s) then yield sprintf "measurement step %s has no command" (sOr "name" s)
          for g in removed do
              let gate = sOr "task" g
              for t in timed do
                  if t.DependsOn @ t.Preferences |> List.exists (fun d -> d.Task = gate) then yield sprintf "timed task %s still waits on %s (%s)" t.Id gate (sOr "was" g)
              if tasks |> List.exists (fun t -> t.Id = task && (t.DependsOn @ t.Preferences |> List.exists (fun d -> d.Task = gate))) then yield sprintf "the bootstrap still waits on %s" gate
          for r in jsonGates do
              match r with
              | Error e -> yield e
              | Ok ids -> for id in ids do if not (refs |> List.exists (fun x -> sOr "id" x = id)) then yield sprintf "organizer gate %s has no recorded disposition" id
          for w in waiverGates do
              if not (refs |> List.exists (fun x -> sOr "id" x = w)) then yield sprintf "%s rests on ODR-V41-06 and has no recorded disposition" w
          for (d, n, l) in docLines do
              if not (refs |> List.exists (fun x -> sOr "source" x = d && sOr "quote" x <> "" && l.Contains(sOr "quote" x))) then
                  yield sprintf "organizer gate at %s:%d has no recorded disposition: %s" d n (l.Trim().Substring(0, min 100 (l.Trim().Length)))
          for x in refs do
              if String.IsNullOrWhiteSpace (sOr "disposition" x) then yield sprintf "organizer reference %s has no disposition" (sOr "id" x)
              if not (List.contains (sOr "decision" x) decisions) then yield sprintf "organizer reference %s names decision %s, which is neither an owner decision request nor OD-INDY-18" (sOr "id" x) (sOr "decision" x)
          if (strs "externalConstraints" policy).IsEmpty then yield "the starting-state policy quotes no published rule as an external constraint"
          if String.IsNullOrWhiteSpace (sOr "residualRisk" policy) then yield "the starting-state policy does not state the residual interpretation risk"
          let rulesDoc = sOr "externalConstraintsSource" policy
          match i.ReadSource rulesDoc with
          | None -> yield sprintf "the published rules' source %s is not readable" rulesDoc
          | Some t -> for c in strs "externalConstraints" policy do if not (t.Contains c) then yield sprintf "the external constraint '%s' is not quoted verbatim in %s" c rulesDoc
          for h in arr "historicalRecords" policy do
              if sOr "disposition" h <> "kept unchanged" then yield sprintf "historical record %s must be kept unchanged" (sOr "id" h)
              match i.ReadSource (sOr "source" h) with
              | Some t when sOr "quote" h <> "" && t.Contains(sOr "quote" h) -> ()
              | _ -> yield sprintf "historical record %s no longer reads as recorded in %s" (sOr "id" h) (sOr "source" h)
          if (arr "historicalRecords" policy).IsEmpty then yield "the starting-state policy lists no historical record"
          let docs = sOr "document" policy
          if not (i.AuthoredText |> List.exists (fst >> (=) docs)) then yield sprintf "the starting-state policy document %s is not part of the package" docs
          for (n, t) in i.AuthoredText do
              if n = "AGENT-RULES.md" && t.Contains "relies on ODR-V41-06" then yield "AGENT-RULES.md still makes the bootstrap rely on organizer confirmation (ODR-V41-06)" ]

/// X20: the amendment names the package it amends by its approved manifest digest and its approval, and its version is a
/// successor of 4.1.0.
let amendmentRecordCheck (i: Inputs) : Check =
    let a = i.Amendment |> Option.defaultValue (JObj [])
    let amends = field "amends" a |> Option.defaultValue (JObj [])
    let target = sOr "package" amends
    let actual = i.ReadSource (target + "/package-manifest.json") |> Option.map (fun t -> Git.sha256Hex (Text.Encoding.UTF8.GetBytes t))
    check "X20" "The amendment names the package it amends by the approved manifest digest and approval, its version succeeds 4.1.0, and every change is classified"
        [ if not (Regex.IsMatch(packageVersion i, @"^4\.1\.[1-9][0-9]*$")) then yield sprintf "version %s is not a successor of 4.1.0" (packageVersion i)
          if target <> "requirements-compiled/v4.1" then yield sprintf "the amendment amends %s, not requirements-compiled/v4.1" target
          match actual with
          | None -> yield sprintf "%s/package-manifest.json is not readable" target
          | Some d when d <> sOr "manifestSha256" amends -> yield sprintf "the amended manifest has sha256 %s, the amendment names %s" d (sOr "manifestSha256" amends)
          | Some _ -> ()
          if String.IsNullOrWhiteSpace (sOr "approval" amends) then yield "the amendment names no approval of the package it amends"
          let changes = arr "changes" a
          if changes.IsEmpty then yield "the amendment lists no change"
          for c in changes do
              if String.IsNullOrWhiteSpace (sOr "id" c) || String.IsNullOrWhiteSpace (sOr "summary" c) || String.IsNullOrWhiteSpace (sOr "area" c) then yield "every change needs an id, an area and a summary" ]

/// Layers one generated bootstrap file onto a repository Conditor has initialized (v4.1.1: Conditor's `init` runs first
/// on the empty repository, then the generator). Conditor's scaffold files belong to the project once its lock records
/// the scaffold as established, so three of them are merged deterministically and every other existing file must be
/// identical: package.json takes the owner's locked devDependencies (ODR-V41-07, ODR-V41-09) over Conditor's pins;
/// Directory.Build.props is kept when it already sets every property the generator sets, to the same value; .gitignore
/// gains the generator's missing lines. Ok None: nothing to write.
let layerFile (name: string) (existing: string) (generated: string) : Result<string option, string> =
    let norm (s: string) = s.Replace("\r\n", "\n").TrimEnd()
    if norm existing = norm generated then Ok None
    else
        match name with
        | "package.json" ->
            let locks = match field "devDependencies" (parseBytes (Text.Encoding.UTF8.GetBytes generated)) with Some(JObj ps) -> ps | _ -> []
            match parseBytes (Text.Encoding.UTF8.GetBytes existing) with
            | JObj ps ->
                let section (name: string) = match List.tryFind (fst >> (=) name) ps with Some(_, JObj ds) -> ds | _ -> []
                let runtime = section "dependencies"
                // A locked package Conditor already lists under dependencies takes the lock's version there; every other
                // lock goes to devDependencies. No package ends up in both sections.
                let inRuntime (k: string) = runtime |> List.exists (fst >> (=) k)
                let runtime' = runtime |> List.map (fun (k, v) -> match locks |> List.tryFind (fst >> (=) k) with Some(_, l) -> k, l | None -> k, v)
                let devLocks = locks |> List.filter (fst >> inRuntime >> not)
                let merged = (section "devDependencies" |> List.filter (fun (k, _) -> not (locks |> List.exists (fst >> (=) k)))) @ devLocks |> List.sortBy fst
                let withRuntime = ps |> List.map (fun (k, v) -> if k = "dependencies" then k, JObj runtime' else k, v)
                let ps' = if withRuntime |> List.exists (fst >> (=) "devDependencies") then withRuntime |> List.map (fun (k, v) -> if k = "devDependencies" then k, JObj merged else k, v) else withRuntime @ [ "devDependencies", JObj merged ]
                Ok(Some(serialize (JObj ps')))
            | _ -> Error "package.json exists but is not a JSON object"
        | "Directory.Build.props" ->
            let props (s: string) = Regex.Matches(s, @"<([A-Za-z]+)>([^<]*)</\1>") |> Seq.map (fun m -> m.Groups.[1].Value, m.Groups.[2].Value.Trim()) |> List.ofSeq
            let have = props existing
            match props generated |> List.filter (fun p -> not (List.contains p have)) with
            | [] -> Ok None
            | missing -> Error(sprintf "Directory.Build.props exists and does not set %s" (missing |> List.map (fun (k, v) -> k + "=" + v) |> String.concat ", "))
        | ".gitignore" ->
            let have = existing.Replace("\r\n", "\n").Split('\n') |> Array.map (fun l -> l.Trim()) |> Set.ofArray
            match generated.Replace("\r\n", "\n").Split('\n') |> Array.filter (fun l -> l.Trim() <> "" && not (have.Contains(l.Trim()))) |> List.ofArray with
            | [] -> Ok None
            | add -> Ok(Some(existing.TrimEnd('\n') + "\n# requirements-compiler v41 (v4.1.1 bootstrap)\n" + String.concat "\n" add + "\n"))
        | _ -> Error(sprintf "%s already exists with different content; the bootstrap never overwrites it" name)

// ---- build -------------------------------------------------------------------------------------------------------

type Built = { Files: (string * string) list; Checks: Check list }

let build (i: Inputs) : Built =
    let files = ownedFiles i
    let smJson, smProblems = stateMachineContracts i
    let vepJson, vepProblems = verificationExecutionPlan i
    let dag, sched, schedProblems = schedule i
    let preservation, preservationReport = preservationCheck i
    let checks =
        ownershipCheck i files
        @ interfaceCheck i files
        @ [ errorMatrixCheck i
            formaCheck i
            telemetryCheck i
            check "X09" "The verification execution plan places every (obligation, test) binding of the plan, and every contract, state-machine and unit test, in exactly one suite, keeping each test and its assertions" vepProblems
            preservation
            provenanceCheck i
            check "X12" "Every state machine is deterministic by reviewed guard partitions, every pair is decided (listed or fail-closed), every transition is sourced, every state is reachable, no state is added, and every transition v4.1 adds to v4 names an owner decision and approval" smProblems
            check "X13" "The adjusted DAG is acyclic, keeps every v4 task with its requirements and every v4 ordering not explicitly waived, keeps every estimate at least half of v4's, and labels every change an estimate with a rationale" schedProblems
            testIdentityCheck i files
            layoutKitCheck i
            testToolingCheck i files ]
    let contracts = contractFiles i
    let classification, classificationProblems = contractClassification i contracts
    let amended =
        match i.Amendment with
        | None -> []
        | Some _ ->
            [ check "X17" "The interface contracts are specifications: types and signatures only, except reviewed boundary code each allowlist entry names with its rationale and requirements; the compile control and negative controls are classified" classificationProblems
              entrypointCheck i files
              bootstrapCheck i
              amendmentRecordCheck i ]
    let json (n: string) (j: Json) = n, serialize j
    { Files =
        [ json "agent-file-ownership.json" (ownershipJson i files)
          json "state-machine-contracts.json" smJson
          json "verification-execution-plan.json" vepJson
          json "execution-dag.json" dag
          json "schedule.json" sched
          json "preservation-report.json" preservationReport ]
        @ contracts
        @ (if i.Amendment.IsSome then [ json "contract-classification.json" classification ] else [])
      Checks = checks @ amended |> List.sortBy (fun c -> c.Id) }

let validationReport (checks: Check list) : Json =
    JObj
        [ "schema", JStr "indy-init.v41-validation-report/1"
          "verdict", JStr(if checks |> List.forall (fun c -> c.Problems.IsEmpty) then "PASS" else "FAIL")
          "checks", JArr(checks |> List.map (fun c -> JObj [ "id", JStr c.Id; "title", JStr c.Title; "result", JStr(if c.Problems.IsEmpty then "PASS" else "FAIL"); "problems", jstrs c.Problems ])) ]

/// The package manifest: status CANDIDATE always. v4.1 is frozen only by the owner (FREEZE.md); the compiler never
/// computes a frozen state.
let packageManifest (i: Inputs) (digests: (string * string) list) (verdict: string) : Json =
    let vp = field "verificationPlan" i.BaseManifest |> Option.defaultValue JNull
    JObj
        [ "schema", JStr "indy-init.v41-package-manifest/1"
          "packageVersion", JStr(packageVersion i)
          "status", JStr "CANDIDATE"
          "statusMeaning",
          JStr(
              match i.Amendment with
              | None -> "Compiled and validated by requirements-compiler v41. Not approved and not frozen: only the owner's approval of ODR-V41-01 and the freeze tag make it frozen (FREEZE.md). The compiler never reports frozen."
              | Some a -> sprintf "Compiled and validated by requirements-compiler v41. Not approved and not frozen: only the owner's approval of %s and the freeze tag make it frozen (FREEZE.md). The compiler never reports frozen." (sOr "approvalDecision" a)
          )
          yield! (match i.Amendment with Some a -> [ "amends", field "amends" a |> Option.defaultValue JNull ] | None -> [])
          "base", JObj [ "package", JStr "requirements-compiled/v4"; "verificationPlan", JObj [ "planId", JStr(sOr "planId" vp); "fileSha256", JStr(sOr "fileSha256" vp); "contentDigest", JStr(sOr "contentDigest" vp) ] ]
          "validation", JStr verdict
          "files", JObj(digests |> List.sortBy fst |> List.map (fun (n, d) -> n, JStr d)) ]
