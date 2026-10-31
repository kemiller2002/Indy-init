/// Compiled package v4.1: the committed package is reproduced from its inputs, and each check of V41Package rejects
/// inputs broken in the way it guards against (ownership violations, interface incompatibility, verification coverage
/// drop, nondeterministic transitions, missing error classes, unverified Forma tokens, cyclic compile order, amendments
/// and test tooling without the owner's decision), including every bypass independent review round 1 found (RV41-1-4,
/// RV41-1-5, RV41-1-7). The owner's decisions v4.1 applies are checked against the committed operator record.
module V41Tests

open System.IO
open System.Text.RegularExpressions
open Json

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let private mapArr (key: string) (f: Json list -> Json list) (j: Json) = setIn key (JArr(f (arr key j))) j

let private mapWhere (key: string) (idKey: string) (id: string) (f: Json -> Json) (j: Json) =
    mapArr key (List.map (fun x -> if strOpt idKey x = Some id then f x else x)) j

let inputs (source: string) : V41Package.Inputs =
    Program.v41Inputs source (Path.Combine(source, "requirements-compiled", "v4.1")) (Path.Combine(source, "requirements-compiled", "v4")) None

let private failing (i: V41Package.Inputs) =
    (V41Package.build i).Checks |> List.filter (fun c -> not c.Problems.IsEmpty) |> List.map (fun c -> c.Id)

let private expectFail (name: string) (checkId: string) (i: V41Package.Inputs) : string list =
    try
        if List.contains checkId (failing i) then [] else [ sprintf "v4.1 control '%s': %s did not fail" name checkId ]
    with ex -> [ sprintf "v4.1 control '%s' threw: %s" name ex.Message ]

/// The transitions each option of a state-machine-amendment request adds, as its option text states them (machine,
/// from, event, to). ODR-V41-08 A: "add a Saving + MutationSent transition ... and stays Saving".
let private optionTransitions =
    Map [ ("ODR-V41-08", "A"), Set.ofList [ "SM-SAVE", "Saving", "MutationSent", "Saving" ] ]

/// The machines each option accepts unchanged, as its option text states them. ODR-V41-08 A: "accept LoadRejected
/// carrying ProviderFailed for transport failures" (SM-LOAD).
let private optionAccepted =
    Map [ ("ODR-V41-08", "A"), Set.ofList [ "SM-LOAD" ] ]

/// The package manager of each package a dependency-lock option names, as its option text states it (the owner's record
/// lists packages without managers). ODR-V41-07 A: "xUnit with Microsoft.NET.Test.Sdk, and Playwright 1.63.0";
/// ODR-V41-09 A: "axe-core 4.14.0".
let private optionManagers =
    Map [ ("ODR-V41-07", "A"), Map [ "xunit", "nuget"; "xunit.runner.visualstudio", "nuget"; "Microsoft.NET.Test.Sdk", "nuget"; "@playwright/test", "npm" ]
          ("ODR-V41-09", "A"), Map [ "axe-core", "npm" ] ]

/// The owner's decisions v4.1 applies match the committed operator record (RDP-003): each test-tooling lock is exactly
/// the packages its decision records; the amendments citing a decision add exactly the transitions of the option its
/// operator approval selects, and every selected option that adds transitions is applied; each accepted entry is an
/// operator's record of its decision, and the accepted machines are exactly those of the selected option; every
/// recorded dependency-lock decision has its lock; and the approvals file was last changed in a commit with no agent
/// trailers.
let ownerRecords (source: string) (i: V41Package.Inputs) : string list =
    let path = "kickoff/rehearsal-007/approvals.causa.json"
    let approvals = parseFile (Path.Combine(source, path)) |> arr "approvals"
    let operator (a: Json) = (field "authority" a |> Option.bind (strOpt "kind")) = Some "operator"
    let approval id = approvals |> List.tryFind (fun a -> strOpt "id" a = Some id) |> Option.filter operator
    let change (a: Json) (decision: string) = arr "changes" a |> List.tryFind (fun c -> strOpt "decision_id" c = Some decision)
    let recorded (approvalId: string) (decision: string) = approval approvalId |> Option.bind (fun a -> change a decision)
    let tooling = field "testTooling" i.Structure |> Option.defaultValue JNull
    let amendments = arr "transitions" i.MachineAmendments
    let accepted = arr "accepted" i.MachineAmendments
    let requestKinds =
        parseFile (Path.Combine(source, "requirements-compiled", "v4.1", "proposals", "owner-decision-requests.json"))
        |> arr "requests" |> List.map (fun r -> str "id" r, str "kind" r) |> Map.ofList
    let lastChange = match Git.run source [ "log"; "-1"; "--format=%B"; "--"; path ] with Ok m -> System.Text.Encoding.UTF8.GetString m | Error e -> e
    [ if lastChange.Contains "Claude-Session:" || lastChange.Contains "Co-Authored-By: Claude" then yield "v4.1: the approvals file was last changed by an agent commit"
      for l in arr "locks" tooling do
          match recorded (str "approval" l) (str "decision" l) with
          | None -> yield sprintf "v4.1: no operator approval %s records %s" (str "approval" l) (str "decision" l)
          | Some c ->
              let locked = strs "nuget" l @ strs "npm" l
              if List.sort (strs "packages" c) <> List.sort locked then yield sprintf "v4.1: the %s lock holds %A; the owner recorded %A" (str "decision" l) locked (strs "packages" c)
              // Each package is in its manager's list, as the selected option names it (RV41-13-1).
              let idOf (e: string) = match e.LastIndexOf '@' with n when n > 0 -> e.Substring(0, n) | _ -> e
              let option = strOpt "option" c |> Option.defaultValue ""
              match optionManagers.TryFind((str "decision" l, option)) with
              | None -> yield sprintf "v4.1: no package managers are pinned for option %s of %s" option (str "decision" l)
              | Some managers ->
                  for (manager, entries) in [ "nuget", strs "nuget" l; "npm", strs "npm" l ] do
                      for e in entries do
                          match managers.TryFind(idOf e) with
                          | Some m when m = manager -> ()
                          | Some m -> yield sprintf "v4.1: %s is in the %s list; option %s of %s names it a %s package" e manager option (str "decision" l) m
                          | None -> yield sprintf "v4.1: option %s of %s names no package %s" option (str "decision" l) (idOf e)
      for (decision, ts) in amendments |> List.groupBy (str "decision") do
          match ts |> List.map (fun t -> recorded (str "approval" t) decision |> Option.bind (strOpt "option")) |> List.distinct with
          | [ Some option ] ->
              let added = ts |> List.map (fun t -> str "machine" t, str "from" t, str "event" t, str "to" t)
              match optionTransitions.TryFind((decision, option)) with
              | Some expected when Set.ofList added = expected && added.Length = expected.Count -> ()
              | Some expected -> yield sprintf "v4.1: the amendments citing %s add %A; option %s adds exactly %A" decision added option (Set.toList expected)
              | None -> yield sprintf "v4.1: option %s of %s adds no transition, yet amendments cite it" option decision
          | _ -> yield sprintf "v4.1: the amendments citing %s are not all selected by one operator approval of it" decision
      for a in approvals |> List.filter operator do
          for c in arr "changes" a do
              match strOpt "decision_id" c, strOpt "option" c with
              | Some d, Some o when optionTransitions.ContainsKey((d, o)) && not (amendments |> List.exists (fun t -> str "decision" t = d)) ->
                  yield sprintf "v4.1: %s selects option %s of %s, which adds transitions, but no amendment applies it" (str "id" a) o d
              | _ -> ()
      for x in accepted do
          if (recorded (str "approval" x) (str "decision" x)).IsNone then
              yield sprintf "v4.1: the accepted %s entry cites %s, which operator approval %s does not record" (str "machine" x) (str "decision" x) (str "approval" x)
      // The accepted machines of each decision are exactly those of the option its approvals select (RV41-12-3).
      for a in approvals |> List.filter operator do
          for c in arr "changes" a do
              match strOpt "decision_id" c, strOpt "option" c with
              | Some d, Some o when optionAccepted.ContainsKey((d, o)) ->
                  let listed = accepted |> List.filter (fun x -> str "decision" x = d) |> List.map (str "machine")
                  if Set.ofList listed <> optionAccepted.[(d, o)] || listed.Length <> optionAccepted.[(d, o)].Count then
                      yield sprintf "v4.1: the accepted entries of %s list %A; option %s accepts exactly %A" d listed o (Set.toList optionAccepted.[(d, o)])
              | _ -> ()
      // Every operator-recorded dependency-lock decision has its lock (RV41-12-4).
      for a in approvals |> List.filter operator do
          for c in arr "changes" a do
              match strOpt "decision_id" c with
              | Some d when requestKinds.TryFind d = Some "dependency-lock" && not (arr "locks" tooling |> List.exists (fun l -> str "decision" l = d)) ->
                  yield sprintf "v4.1: %s records %s, a dependency lock, but testTooling holds no lock for it" (str "id" a) d
              | _ -> () ]

/// The committed package equals a fresh build, and two builds are identical.
let scope (source: string) : string list =
    let i = inputs source
    let built = V41Package.build i
    let package = Path.Combine(source, "requirements-compiled", "v4.1")
    [ for c in built.Checks do
          if not c.Problems.IsEmpty then yield sprintf "v4.1: %s fails on the committed package: %A" c.Id (List.truncate 3 c.Problems)
      for (n, t) in built.Files do
          let p = Path.Combine(package, n)
          if not (File.Exists p) || File.ReadAllText p <> t then yield sprintf "v4.1: committed %s differs from a fresh build" n
      if (V41Package.build i).Files <> built.Files then yield "v4.1: two builds differ"
      let preservation = built.Files |> List.find (fst >> (=) "preservation-report.json") |> snd |> System.Text.Encoding.UTF8.GetBytes |> parseBytes
      if strOpt "result" preservation <> Some "PRESERVED" then yield "v4.1: the preservation report does not read PRESERVED"
      let counts = field "counts" preservation |> Option.defaultValue JNull
      for (k, n) in [ "requirements", 520L; "p0Requirements", 161L; "obligations", 2599L; "p0Obligations", 613L ] do
          if (field k counts |> Option.bind (intOpt "v41")) <> Some n then yield sprintf "v4.1: %s is not %d" k n
      // RV41-1-1: the save machine (first write code) follows the invariant tests, and checkpoint 1 follows the kernel.
      let order = V41Package.precedences (V41Package.adjustedTasks i)
      for (a, b) in [ "T12", "T19"; "T13", "T17"; "T13", "T21"; "T12", "T35" ] do
          if not (order.Contains(a, b)) then yield sprintf "v4.1: %s no longer precedes %s" a b
      // The bootstrap writes exactly the locked test tooling: versions in Directory.Packages.props, a version-less
      // reference in each test project that hosts .NET tests and none elsewhere, the npm packages in package.json.
      let nuget, npm, toolingProjects = V41Package.testTooling i
      let files = V41Package.ownedFiles i
      if List.sort toolingProjects <> V41Package.testHostProjects i files then yield "v4.1: testTooling does not apply to exactly the test projects that host .NET tests"
      let boot = V41Package.bootstrapFiles i files "{}" |> Map.ofList
      let versions = Regex.Matches(boot.TryFind "Directory.Packages.props" |> Option.defaultValue "", "<PackageVersion Include=\"([^\"]+)\" Version=\"([^\"]+)\" />") |> Seq.map (fun m -> m.Groups[1].Value, m.Groups[2].Value) |> List.ofSeq
      if versions <> nuget then yield sprintf "v4.1: Directory.Packages.props pins %A, not %A" versions nuget
      for tp in arr "projects" i.Structure @ arr "testProjects" i.Structure do
          let refs = Regex.Matches(boot.TryFind (str "path" tp) |> Option.defaultValue "", "<PackageReference Include=\"([^\"]+)\"( Version=\"[^\"]*\")? />") |> Seq.map (fun m -> m.Groups[1].Value, m.Groups[2].Value) |> List.ofSeq
          let expected = if List.contains (str "id" tp) toolingProjects then nuget |> List.map (fun (id, _) -> id, "") else []
          if refs <> expected then yield sprintf "v4.1: bootstrap writes %A into %s, not %A" refs (str "id" tp) expected
      match boot.TryFind "package.json" |> Option.map (System.Text.Encoding.UTF8.GetBytes >> parseBytes) with
      | Some pj -> if (field "devDependencies" pj |> Option.map (function JObj ps -> ps |> List.map (fun (k, v) -> k, asStr v) | _ -> []) |> Option.defaultValue []) <> npm then yield "v4.1: package.json does not hold exactly the locked npm packages"
      | None -> yield "v4.1: the bootstrap writes no package.json"
      yield! ownerRecords source i ]

let controls (source: string) : string list =
    let i0 = inputs source
    let structure f = { i0 with Structure = f i0.Structure }
    let project id f = structure (mapWhere "projects" "id" id f)
    let fileIn id path f = project id (mapArr "files" (List.map (fun x -> if strOpt "path" x = Some path then f x else x)))
    let machine id f = { i0 with StateMachines = mapWhere "machines" "id" id f i0.StateMachines }
    let iface id f = { i0 with Interfaces = mapWhere "interfaces" "id" id f i0.Interfaces }
    let editSource id (from: string) (into: string) = iface id (fun x -> setIn "fsharp" (JStr((str "fsharp" x).Replace(from, into))) x)
    let errClass id f = { i0 with ErrorMatrix = mapWhere "classes" "id" id f i0.ErrorMatrix }
    let formaRule id f = { i0 with Forma = mapWhere "rules" "id" id f i0.Forma }
    let adjust id f = { i0 with Adjustments = mapWhere "adjustments" "task" id f i0.Adjustments }
    let firstSave = arr "transitions" (arr "machines" i0.StateMachines |> List.find (fun m -> strOpt "id" m = Some "SM-SAVE")) |> List.head
    let addTest (t: string) =
        mapArr "requirements" (List.mapi (fun n r ->
            if n > 0 then r
            else mapArr "obligations" (List.mapi (fun k o ->
                if k > 0 then o
                else
                    let c = field "acceptanceContract" o |> Option.defaultValue JNull
                    let ti = field "testIdentity" c |> Option.defaultValue JNull
                    setIn "acceptanceContract" (setIn "testIdentity" (mapArr "tests" (fun ts -> ts @ [ JObj [ "id", JStr t; "kind", JStr "deterministic"; "categories", jstrs [ "unit" ] ] ]) ti) c) o)) r))
    [ // X01 ownership: a file listed twice; an unknown role; one file holding tests of two owners.
      yield! expectFail "file with two owners" "X01" (structure (mapArr "sharedFiles" (fun fs -> fs @ [ JObj [ "path", JStr "web/causa.css"; "owner", JStr "ui" ] ])))
      yield! expectFail "unknown owner role" "X01" (structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some "README.md" then setIn "owner" (JStr "intern") f else f))))
      yield! expectFail "one file, two owners' tests" "X01" (iface "IF41-SAVE" (setIn "producerTests" (jstrs [ "dotnet:Causa.Contract.Tests.Save.ConsumerUi.Hijacked" ])))
      // X02: project file, CI, composition and stylesheet owned by someone other than integration (RV41-1-7).
      yield! expectFail "project file owned by ui" "X02" (project "Causa.App" (setIn "owner" (JStr "ui")))
      yield! expectFail "CI workflow owned by persistence" "X02" (structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some ".github/workflows/ci.yml" then setIn "owner" (JStr "persistence") f else f))))
      yield! expectFail "View.fs owned by ui" "X02" (fileIn "Causa.App" "src/Causa.App/View.fs" (setIn "owner" (JStr "ui")))
      yield! expectFail "boundary script owned by ui" "X02" (structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some "web/boundary.js" then setIn "owner" (JStr "ui") f else f))))
      // X02 round 2 (RV41-2-5): shared build configuration and browser fixtures declared as helpers of other roles.
      let helper path owner = structure (mapArr "testHelpers" (fun hs -> hs @ [ JObj [ "path", JStr path; "owner", JStr owner ] ]))
      yield! expectFail "NuGet.config owned by ui" "X02" (helper "NuGet.config" "ui")
      yield! expectFail "Directory.Build.targets owned by persistence" "X02" (helper "Directory.Build.targets" "persistence")
      yield! expectFail "browser fixtures owned by ui" "X02" (structure (mapArr "testHelpers" (List.map (fun h -> if strOpt "path" h = Some "tests/browser/support/fixtures.js" then setIn "owner" (JStr "ui") h else h))))
      // X03 compile order: a source reference cycle; a test project cycle; a file depending on a later file.
      yield! expectFail "cyclic project references" "X03" (project "Causa.Domain" (setIn "references" (jstrs [ "Causa.App" ])))
      yield! expectFail "cyclic test project references" "X03" (project "Causa.Domain" (setIn "references" (jstrs [ "Causa.Domain.Tests" ])))
      yield! expectFail "file depends on a later file" "X03" (fileIn "Causa.Domain" "src/Causa.Domain/Identity.fs" (setIn "dependsOn" (jstrs [ "src/Causa.Domain/Kernel.fs" ])))
      // X03 round 2 (RV41-2-4): a helper depending on a later helper, or on a project its test project does not reference.
      let helperDeps deps = structure (mapArr "testHelpers" (List.map (fun h -> if strOpt "path" h = Some "tests/Causa.TestSupport/Recordings.fs" then setIn "dependsOn" (jstrs deps) h else h)))
      yield! expectFail "helper depends on a later helper" "X03" (helperDeps [ "tests/Causa.TestSupport/StatefulFake.fs" ])
      yield! expectFail "helper depends on an unreferenced project" "X03" (helperDeps [ "src/Causa.App/Engine.fs" ])
      // X04 interfaces: wrong implementing file; contract tests that resolve nowhere; a consumer without a test.
      yield! expectFail "interface in a file the producer does not own" "X04" (iface "IF41-SAVE" (setIn "file" (JStr "src/Causa.App/Engine.fs")))
      yield! expectFail "producer tests that resolve nowhere" "X04" (iface "IF41-SAVE" (setIn "producerTests" (jstrs [ "nonexistent:x" ])))
      yield! expectFail "consumer without a contract test" "X04" (iface "IF41-SAVE" (mapArr "consumerTests" (List.filter (fun t -> not ((asStr t).Contains "ConsumerUi2")))))
      // X04 round 2 (RV41-2-5, RV41-2-11): a non-contract test passed off as a producer test; empty rules; operations.
      yield! expectFail "non-contract producer test" "X04" (iface "IF41-SAVE" (setIn "producerTests" (jstrs [ "dotnet:Causa.Persistence.Tests.Anything.Trivial" ])))
      yield! expectFail "empty concurrency rule" "X04" (iface "IF41-SAVE" (setIn "concurrency" (JStr ".")))
      yield! expectFail "no operations" "X04" (iface "IF41-WIRE" (setIn "operations" (JArr [])))
      yield! expectFail "operation not in the contract" "X04" (iface "IF41-WIRE" (setIn "operations" (JArr [ JObj [ "name", JStr "Teleport"; "signature", JStr "a -> b"; "success", JStr "s"; "failure", JStr "f" ] ])))
      // X05 machine types: a new conflict state; a save event the input union lacks.
      yield! expectFail "new conflict state" "X05" (editSource "IF41-SAVE" "    | RemoteUnavailable\n" "    | RemoteUnavailable\n    | AwaitingOwner\n")
      yield! expectFail "new persistence state" "X05" (editSource "IF41-SAVE" "    | Saving\n" "    | Saving\n    | Syncing\n")
      yield! expectFail "unmapped save event" "X05" (editSource "IF41-SAVE" "    | ReadBackReadFailed\n" "\n")
      // X06 error matrix (RV41-1-7): missing class; attempts without a bound; a status passed off as evidence; two
      // non-adjacent classes with the same evidence; an unrelated test; no timeout detection.
      yield! expectFail "stale-head class missing" "X06" { i0 with ErrorMatrix = mapArr "classes" (List.filter (fun c -> strOpt "id" c <> Some "GH-STALE-HEAD")) i0.ErrorMatrix }
      yield! expectFail "attempts without a bound" "X06" (errClass "GH-AUTH" (setIn "maxAttempts" (JInt 4L)))
      yield! expectFail "retries beyond the class bound" "X06" (errClass "GH-STALE-HEAD" (setIn "maxAttempts" (JInt 4L)))
      yield! expectFail "status passed off as evidence" "X06" (errClass "GH-PERMISSION" (setIn "nonStatusEvidence" (jstrs [ "HTTP 403" ])))
      let permissionEvidence = arr "classes" i0.ErrorMatrix |> List.find (fun c -> strOpt "id" c = Some "GH-PERMISSION") |> strs "nonStatusEvidence"
      yield! expectFail "same evidence, non-adjacent classes" "X06" (errClass "GH-NOT-FOUND" (setIn "nonStatusEvidence" (jstrs permissionEvidence)))
      yield! expectFail "unrelated test" "X06" (errClass "GH-STALE-HEAD" (setIn "tests" (jstrs [ "dotnet:Causa.Acceptance.Deterministic.PERSIST051Tests.PERSIST-051-O1" ])))
      yield! expectFail "no timeout detection" "X06" (errClass "GH-NETWORK-AFTER" (setIn "nonStatusEvidence" (jstrs [ "transport: LostAfterSending" ])))
      // X06 round 2 (RV41-2-5): a reason phrase passed off as evidence; evidence padded with junk; a bound of another kind.
      yield! expectFail "reason phrase as evidence" "X06" (errClass "GH-PERMISSION" (setIn "nonStatusEvidence" (jstrs [ "HTTP 403 Forbidden" ])))
      yield! expectFail "evidence padded with junk" "X06" (errClass "GH-NOT-FOUND" (setIn "nonStatusEvidence" (jstrs permissionEvidence)))
      yield! expectFail "bound of another kind" "X06" (errClass "GH-AUTH" (setIn "maxAttempts" (JInt 3L) >> setIn "boundedBy" (JStr "automaticRebases")))
      // X07 Forma (RV41-1-7): an invented token, a foreign custom property, a foreign class, an unsourced colour.
      yield! expectFail "invented token" "X07" { i0 with Forma = setIn "tokens" (JObj [ "required", jstrs [ "--ef-causa-invented" ] ]) i0.Forma }
      yield! expectFail "foreign custom property" "X07" (formaRule "FL-01" (fun r -> setIn "rule" (JStr(str "rule" r + " Gaps use var(--causa-gap).")) r))
      yield! expectFail "foreign class" "X07" (formaRule "FL-01" (fun r -> setIn "rule" (JStr(str "rule" r + " Lay out with .my-grid.")) r))
      yield! expectFail "unsourced colour" "X07" (formaRule "FL-01" (fun r -> setIn "rule" (JStr(str "rule" r + " Borders are #123456.")) r))
      yield! expectFail "unknown pattern" "X07" { i0 with Forma = mapArr "reuse" (fun rs -> rs @ [ JObj [ "need", JStr "x"; "patterns", jstrs [ "causa-card" ] ] ]) i0.Forma }
      yield! expectFail "unknown breakpoint" "X07" { i0 with Forma = setIn "breakpoints" (JObj [ "values", jstrs [ "52rem" ] ]) i0.Forma }
      // X07 round 2 (RV41-2-5): rgb() colours, unhyphenated classes, foreign property families, invented sources.
      yield! expectFail "rgb colour" "X07" (formaRule "FL-01" (fun r -> setIn "rule" (JStr(str "rule" r + " Use rgb(233,162,59).")) r))
      yield! expectFail "unhyphenated class" "X07" (formaRule "FL-01" (fun r -> setIn "rule" (JStr(str "rule" r + " Lay out with .mygrid here.")) r))
      yield! expectFail "foreign property family" "X07" (formaRule "FL-01" (fun r -> setIn "rule" (JStr(str "rule" r + " Use var(--causa-*) tokens.")) r))
      yield! expectFail "invented literal source" "X07" (formaRule "FL-06" (setIn "literals" (JArr [ JObj [ "value", JStr "#E9A23B"; "source", JStr "made up" ] ])))
      // X08 telemetry: a metric dropped; a policy that allows estimating.
      yield! expectFail "metric missing" "X08" { i0 with Telemetry = mapArr "metrics" (List.filter (fun m -> strOpt "id" m <> Some "ownership-violations")) i0.Telemetry }
      yield! expectFail "estimate-it policy" "X08" { i0 with Telemetry = setIn "unavailablePolicy" (JStr "estimate it") i0.Telemetry }
      // X09 / X14 verification coverage: a bound test no suite runs and no file holds.
      yield! expectFail "test no suite runs" "X09" { i0 with Plan = addTest "junit:Causa.Orphan" i0.Plan }
      yield! expectFail "test with no file" "X14" { i0 with Plan = addTest "junit:Causa.Orphan" i0.Plan }
      // X10 preservation: a changed plan; a P0 requirement the plan lost.
      yield! expectFail "plan bytes changed" "X10" { i0 with PlanBytes = Array.append i0.PlanBytes [| 32uy |] }
      yield! expectFail "P0 requirement dropped from the plan" "X10" { i0 with Plan = mapArr "requirements" (List.filter (fun r -> strOpt "requirement" r <> Some "PERSIST-007")) i0.Plan }
      // X11 provenance (RV41-1-7): spellings of earlier rehearsals' repositories and source paths.
      for text in [ "copied from kemiller2002/indy-init-rehearsal-003"; "see indy_init_rehearsal_003"; "port rehearsal-005/lib/save.fs" ] do
          yield! expectFail ("rehearsal source named: " + text) "X11" { i0 with AuthoredText = i0.AuthoredText @ [ "README.md", text ] }
      // X12 state machines (RV41-1-7): two transitions one guard selects; an overlapping guard; a dropped branch; an
      // unknown next state.
      yield! expectFail "nondeterministic transition" "X12" (machine "SM-SAVE" (mapArr "transitions" (fun ts -> ts @ [ setIn "id" (JStr "T-S99") firstSave |> setIn "to" (JStr "Offline") ])))
      yield! expectFail "overlapping guard" "X12" (machine "SM-SAVE" (mapArr "transitions" (fun ts -> ts @ [ setIn "id" (JStr "T-S99") firstSave |> setIn "guard" (JStr "online and anything") ])))
      yield! expectFail "offline branch dropped" "X12" (machine "SM-SAVE" (mapArr "transitions" (List.filter (fun t -> strOpt "id" t <> Some "T-S05"))))
      yield! expectFail "unknown next state" "X12" (machine "SM-SAVE" (mapArr "transitions" (fun ts -> ts @ [ setIn "id" (JStr "T-S99") firstSave |> setIn "guard" (JStr "never") |> setIn "event" (JStr "CheckAgain") |> setIn "to" (JStr "Limbo") ])))
      // X12 round 2 (RV41-2-2): an event no transition accepts, not listed with a decision (MutationSent without the
      // owner's amendment).
      yield! expectFail "unlisted unused event" "X12" { i0 with GuardPartitions = setIn "unusedEvents" (JArr []) i0.GuardPartitions; MachineAmendments = setIn "transitions" (JArr []) i0.MachineAmendments }
      // X12 (ODR-V41-08): an amendment without an owner decision or approval, one that adds a state, one on a machine v4
      // does not have, and one that makes a pair nondeterministic.
      let amendment f = { i0 with MachineAmendments = mapArr "transitions" (List.map f) i0.MachineAmendments }
      let added = arr "transitions" i0.MachineAmendments |> List.head
      yield! expectFail "amendment without a decision" "X12" (amendment (setIn "decision" (JStr "")))
      yield! expectFail "amendment naming no request" "X12" (amendment (setIn "decision" (JStr "ODR-V41-99")))
      yield! expectFail "amendment without an approval" "X12" (amendment (setIn "approval" (JStr "")))
      yield! expectFail "amendment adding a state" "X12" (amendment (setIn "to" (JStr "Sending")))
      yield! expectFail "amendment on an unknown machine" "X12" (amendment (setIn "machine" (JStr "SM-SYNC")))
      yield! expectFail "amendment making a pair nondeterministic" "X12" { i0 with MachineAmendments = mapArr "transitions" (fun ts -> ts @ [ setIn "id" (JStr "T-S62") added |> setIn "to" (JStr "Offline") ]) i0.MachineAmendments }
      // X12 round 11 (RV41-11-1): Saved entered without a confirmation, a source that is not a requirement, a decision of
      // another kind, and an accepted entry naming no request.
      yield! expectFail "amendment entering Saved without a confirmation" "X12" (amendment (setIn "to" (JStr "Saved")))
      yield! expectFail "second amendment into Saved" "X12" { i0 with MachineAmendments = mapArr "transitions" (fun ts -> ts @ [ setIn "id" (JStr "T-S62") added |> setIn "from" (JStr "Unsaved") |> setIn "event" (JStr "CheckAgain") |> setIn "to" (JStr "Saved") ]) i0.MachineAmendments }
      yield! expectFail "amendment source not a requirement" "X12" (amendment (setIn "sources" (jstrs [ "NOPE-1" ])))
      yield! expectFail "amendment citing a dependency-lock decision" "X12" (amendment (setIn "decision" (JStr "ODR-V41-07")))
      yield! expectFail "accepted entry naming no request" "X12" { i0 with MachineAmendments = mapArr "accepted" (List.map (setIn "decision" (JStr "ODR-V41-99"))) i0.MachineAmendments }
      // X16 (ODR-V41-07, RV41-11-5): a lock of another kind of decision or without approval, a version range, an
      // unversioned package, a package locked twice (in one lock or across two), no NuGet entry, no lock, and test
      // projects that are not exactly the ones hosting .NET tests.
      let tooling f = structure (fun st -> setIn "testTooling" (f (field "testTooling" st |> Option.defaultValue JNull)) st)
      let lock0 f = tooling (mapArr "locks" (List.mapi (fun n l -> if n = 0 then f l else l)))
      yield! expectFail "lock of a schedule decision" "X16" (lock0 (setIn "decision" (JStr "ODR-V41-03")))
      yield! expectFail "lock without an approval" "X16" (lock0 (setIn "approval" (JStr "")))
      yield! expectFail "version range" "X16" (lock0 (setIn "nuget" (jstrs [ "xunit@^2.9.2" ])))
      yield! expectFail "unversioned npm package" "X16" (lock0 (setIn "npm" (jstrs [ "@playwright/test" ])))
      yield! expectFail "package locked twice" "X16" (lock0 (mapArr "nuget" (fun ps -> ps @ [ JStr "xunit@2.9.3" ])))
      yield! expectFail "package locked by two locks" "X16" (tooling (mapArr "locks" (fun ls -> ls @ [ JObj [ "decision", JStr "ODR-V41-09"; "approval", JStr "APR-X"; "nuget", jstrs [ "xunit@2.9.3" ] ] ])))
      yield! expectFail "no NuGet test package" "X16" (lock0 (setIn "nuget" (JArr [])))
      yield! expectFail "no lock" "X16" (tooling (setIn "locks" (JArr [])))
      yield! expectFail "test SDK on a source project" "X16" (tooling (mapArr "testProjects" (fun ps -> ps @ [ JStr "Causa.Domain" ])))
      yield! expectFail "test SDK on the helper library" "X16" (tooling (mapArr "testProjects" (fun ps -> ps @ [ JStr "Causa.TestSupport" ])))
      yield! expectFail "a test host left out" "X16" (tooling (mapArr "testProjects" (List.filter (fun p -> asStr p <> "Causa.Contract.Tests"))))
      yield! expectFail "no test project" "X16" (tooling (setIn "testProjects" (JArr [])))
      // The owner-record comparison rejects a lock or an amendment the owner did not record (RV41-11-1, RV41-11-5).
      let recordFails name (i: V41Package.Inputs) = [ if (try (ownerRecords source i).IsEmpty with _ -> true) then yield sprintf "v4.1 owner-record control '%s' passed" name ]
      yield! recordFails "lock with another version" (lock0 (setIn "nuget" (jstrs [ "xunit@2.9.3"; "xunit.runner.visualstudio@2.8.2"; "Microsoft.NET.Test.Sdk@17.11.1" ])))
      yield! recordFails "lock with an extra package" (lock0 (mapArr "npm" (fun ps -> ps @ [ JStr "axe-core@4.14.0" ])))
      yield! recordFails "lock with a missing package" (lock0 (setIn "npm" (JArr [])))
      yield! recordFails "lock citing an unrecorded approval" (lock0 (setIn "approval" (JStr "APR-V41-OWNER-9")))
      yield! recordFails "lock no approval records" (tooling (mapArr "locks" (fun ls -> ls @ [ JObj [ "decision", JStr "ODR-V41-09"; "approval", JStr "APR-V41-OWNER-2"; "npm", jstrs [ "axe-core@4.14.0" ] ] ])))
      yield! recordFails "amendment to another state" (amendment (setIn "to" (JStr "Offline")))
      yield! recordFails "amendment on another event" (amendment (setIn "event" (JStr "CheckAgain")))
      yield! recordFails "second amendment the option does not add" { i0 with MachineAmendments = mapArr "transitions" (fun ts -> ts @ [ setIn "id" (JStr "T-S62") added |> setIn "from" (JStr "Unsaved") ]) i0.MachineAmendments }
      yield! recordFails "selected option not applied" { i0 with MachineAmendments = setIn "transitions" (JArr []) i0.MachineAmendments }
      yield! recordFails "amendment citing another approval" (amendment (setIn "approval" (JStr "APR-V41-OWNER-1")))
      yield! recordFails "accepted entry citing an unrecorded approval" { i0 with MachineAmendments = mapArr "accepted" (List.map (setIn "approval" (JStr "FAKE"))) i0.MachineAmendments }
      // Round 12 (RV41-12-3, RV41-12-4): accepted machines not the option's; a recorded lock decision left unapplied; a
      // package in the other manager's list.
      yield! recordFails "accepted machine replaced" { i0 with MachineAmendments = mapArr "accepted" (List.map (setIn "machine" (JStr "SM-SAVE"))) i0.MachineAmendments }
      yield! recordFails "extra accepted machine" { i0 with MachineAmendments = mapArr "accepted" (fun xs -> xs @ (xs |> List.map (setIn "machine" (JStr "SM-CONFLICT")))) i0.MachineAmendments }
      yield! recordFails "accepted machine dropped" { i0 with MachineAmendments = setIn "accepted" (JArr []) i0.MachineAmendments }
      yield! recordFails "recorded lock decision without its lock" (tooling (mapArr "locks" (List.filter (fun l -> strOpt "decision" l <> Some "ODR-V41-09"))))
      yield! expectFail "npm package in the NuGet list" "X16" (lock0 (mapArr "nuget" (fun ps -> ps @ [ JStr "@playwright/test@1.63.0" ])))
      // Round 13 (RV41-13-1): a package moved to the other manager's list, which no name rule can tell.
      yield! recordFails "xunit moved to npm" (lock0 (mapArr "nuget" (List.filter (fun e -> asStr e <> "xunit@2.9.2")) >> mapArr "npm" (fun ps -> ps @ [ JStr "xunit@2.9.2" ])))
      let lockOf d f = tooling (mapArr "locks" (List.map (fun l -> if strOpt "decision" l = Some d then f l else l)))
      yield! recordFails "axe-core moved to NuGet" (lockOf "ODR-V41-09" (setIn "npm" (JArr []) >> setIn "nuget" (jstrs [ "axe-core@4.14.0" ])))
      // X13 round 2 (RV41-2-5): a waiver naming no owner decision request.
      yield! expectFail "waiver without a real decision" "X13" { i0 with Adjustments = mapArr "waivedPrecedences" (List.map (setIn "decision" (JStr "x"))) i0.Adjustments }
      // X15 executable layout kit (RV41-3-1, RV41-3-7): a rule without assertions or opted out; an assertion the kit
      // does not export; a missing conforming fixture; an unknown composed pattern; the event-build copy unowned.
      yield! expectFail "rule without assertions" "X15" (formaRule "FL-04" (setIn "executable" (JArr [])))
      yield! expectFail "rule opted out of critical" "X15" (formaRule "FL-04" (setIn "critical" (JBool false)))
      yield! expectFail "assertion not exported" "X15" (formaRule "FL-04" (setIn "executable" (JArr [ JObj [ "assertion", JStr "focusTrapMagic"; "patterns", jstrs [ "dialog" ]; "viewports", JStr "all" ] ])))
      yield! expectFail "missing conforming fixture" "X15" (formaRule "FL-08" (setIn "executable" (JArr [ JObj [ "assertion", JStr "faultStatesPreserved"; "fixtures", jstrs [ "noSuchFixture" ]; "viewports", JStr "all" ] ])))
      yield! expectFail "unknown composed pattern" "X15" (formaRule "FL-07" (setIn "executable" (JArr [ JObj [ "assertion", JStr "indicatorComposed"; "composed", JArr [ jstrs [ "record-header"; "causa-banner" ] ]; "viewports", JStr "all" ] ])))
      yield! expectFail "event-build copy unowned" "X15" (structure (mapArr "testHelpers" (List.filter (fun h -> strOpt "path" h <> Some "tests/browser/support/layout-assertions.mjs"))))
      // X15 round 7 (RV41-7-7): the report's stated coverage must equal the contract's and the kit's.
      let report f = { i0 with AuthoredText = i0.AuthoredText |> List.map (fun (n, t) -> if n = "optimization-report.md" then n, f t else n, t) }
      yield! expectFail "stale stated coverage" "X15" (report (fun t -> Regex.Replace(t, @"(\d+) broken pages", "1 broken pages")))
      yield! expectFail "stated coverage missing" "X15" (report (fun t -> Regex.Replace(t, @"\d+ of \d+ rules, ", "")))
      yield! expectFail "kit without broken fixtures" "X15" { i0 with AuthoredText = i0.AuthoredText |> List.map (fun (n, t) -> if n = "layout-tests/fixtures.mjs" then n, "export const conformingFixtures = {\n};" else n, t) }
      // X04 round 5 (RV41-5-2): a case name shared by two unqualified unions.
      yield! expectFail "ambiguous union case" "X04" (editSource "IF41-SAVE" "[<RequireQualifiedAccess>]\ntype SaveState =" "type SaveState =")
      // X15 round 4 (RV41-4-2): an assertion dropped from a rule whose invariant needs it; an assertion that tests no
      // invariant.
      yield! expectFail "invariant's assertion dropped" "X15" (formaRule "FL-04" (mapArr "executable" (List.filter (fun x -> strOpt "assertion" x <> Some "dialogFocus"))))
      yield! expectFail "assertion testing no invariant" "X15" (formaRule "FL-01" (mapArr "executable" (fun xs -> xs @ [ JObj [ "assertion", JStr "lineLength"; "patterns", jstrs [ "prose" ]; "viewports", JStr "all" ] ])))
      // X02 round 4 (RV41-4-7): .gitignore is integration's.
      yield! expectFail ".gitignore owned by ui" "X02" (structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some ".gitignore" then setIn "owner" (JStr "ui") f else f))))
      // X02 / X04 round 3 (RV41-3-7): contributors on an integration file; a required shared file dropped; trivial
      // operation outcomes.
      yield! expectFail "contributors on package.json" "X02" (structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some "package.json" then setIn "contributors" (jstrs [ "ui"; "persistence" ]) f else f))))
      yield! expectFail "boundary script dropped" "X02" (structure (mapArr "sharedFiles" (List.filter (fun f -> strOpt "path" f <> Some "web/boundary.js"))))
      yield! expectFail "trivial operation outcome" "X04" (iface "IF41-WIRE" (mapArr "operations" (List.map (setIn "success" (JStr "ok") >> setIn "failure" (JStr "-")))))
      // X13 DAG (RV41-1-1, RV41-1-7): a cycle; a measured label; a dropped v4 ordering; a gutted estimate; a stale waiver.
      yield! expectFail "cyclic DAG" "X13" (adjust "T12" (setIn "dependsOn" (JArr [ JObj [ "task", JStr "T21"; "reason", JStr "x" ] ])))
      yield! expectFail "estimate labelled measured" "X13" (adjust "T16" (setIn "evidence" (JStr "measured")))
      yield! expectFail "test-first order dropped" "X13" (adjust "T19" (setIn "dependsOn" (JArr [ JObj [ "task", JStr "T14"; "reason", JStr "x" ]; JObj [ "task", JStr "T18"; "reason", JStr "x" ] ])))
      yield! expectFail "v4 ordering dropped" "X13" { i0 with Adjustments = mapArr "adjustments" (fun xs -> xs @ [ JObj [ "task", JStr "T20"; "dependsOn", JArr [ JObj [ "task", JStr "T18"; "reason", JStr "x" ] ]; "rationale", JStr "x"; "evidence", JStr "estimated" ] ]) i0.Adjustments }
      yield! expectFail "estimate gutted" "X13" (adjust "T19" (setIn "estimateMinutes" (JInt 1L)))
      yield! expectFail "stale waiver" "X13" { i0 with Adjustments = mapArr "waivedPrecedences" (fun ws -> ws @ [ JObj [ "before", JStr "T18"; "after", JStr "T19"; "reason", JStr "x"; "decision", JStr "ODR-V41-03" ] ]) i0.Adjustments } ]

let all (source: string) : string list =
    (try scope source with ex -> [ "v4.1 scope: " + ex.Message ]) @ (try controls source with ex -> [ "v4.1 controls: " + ex.Message ])
