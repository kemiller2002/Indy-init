/// Compiled package v4.1: the committed package is reproduced from its inputs, and each check of V41Package rejects
/// inputs broken in the way it guards against (ownership violations, interface incompatibility, verification coverage
/// drop, nondeterministic transitions, missing error classes, unverified Forma tokens, cyclic compile order), including
/// every bypass independent review round 1 found (RV41-1-4, RV41-1-5, RV41-1-7).
module V41Tests

open System.IO
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
      for (a, b) in [ "T12", "T19"; "T13", "T17"; "T13", "T21" ] do
          if not (order.Contains(a, b)) then yield sprintf "v4.1: %s no longer precedes %s" a b ]

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
      yield! expectFail "stylesheet owned by ui" "X02" (structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some "web/causa.css" then setIn "owner" (JStr "ui") f else f))))
      // X03 compile order: a source reference cycle; a test project cycle; a file depending on a later file.
      yield! expectFail "cyclic project references" "X03" (project "Causa.Domain" (setIn "references" (jstrs [ "Causa.App" ])))
      yield! expectFail "cyclic test project references" "X03" (project "Causa.Domain" (setIn "references" (jstrs [ "Causa.Domain.Tests" ])))
      yield! expectFail "file depends on a later file" "X03" (fileIn "Causa.Domain" "src/Causa.Domain/Identity.fs" (setIn "dependsOn" (jstrs [ "src/Causa.Domain/Kernel.fs" ])))
      // X04 interfaces: wrong implementing file; contract tests that resolve nowhere; a consumer without a test.
      yield! expectFail "interface in a file the producer does not own" "X04" (iface "IF41-SAVE" (setIn "file" (JStr "src/Causa.App/Engine.fs")))
      yield! expectFail "producer tests that resolve nowhere" "X04" (iface "IF41-SAVE" (setIn "producerTests" (jstrs [ "nonexistent:x" ])))
      yield! expectFail "consumer without a contract test" "X04" (iface "IF41-SAVE" (mapArr "consumerTests" (List.filter (fun t -> not ((asStr t).Contains "ConsumerUi2")))))
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
      // X07 Forma (RV41-1-7): an invented token, a foreign custom property, a foreign class, an unsourced colour.
      yield! expectFail "invented token" "X07" { i0 with Forma = setIn "tokens" (JObj [ "required", jstrs [ "--ef-causa-invented" ] ]) i0.Forma }
      yield! expectFail "foreign custom property" "X07" (formaRule "FL-01" (fun r -> setIn "rule" (JStr(str "rule" r + " Gaps use var(--causa-gap).")) r))
      yield! expectFail "foreign class" "X07" (formaRule "FL-01" (fun r -> setIn "rule" (JStr(str "rule" r + " Lay out with .my-grid.")) r))
      yield! expectFail "unsourced colour" "X07" (formaRule "FL-01" (fun r -> setIn "rule" (JStr(str "rule" r + " Borders are #123456.")) r))
      yield! expectFail "unknown pattern" "X07" { i0 with Forma = mapArr "reuse" (fun rs -> rs @ [ JObj [ "need", JStr "x"; "patterns", jstrs [ "causa-card" ] ] ]) i0.Forma }
      yield! expectFail "unknown breakpoint" "X07" { i0 with Forma = setIn "breakpoints" (JObj [ "values", jstrs [ "52rem" ] ]) i0.Forma }
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
      // X13 DAG (RV41-1-1, RV41-1-7): a cycle; a measured label; a dropped v4 ordering; a gutted estimate; a stale waiver.
      yield! expectFail "cyclic DAG" "X13" (adjust "T12" (setIn "dependsOn" (JArr [ JObj [ "task", JStr "T21"; "reason", JStr "x" ] ])))
      yield! expectFail "estimate labelled measured" "X13" (adjust "T16" (setIn "evidence" (JStr "measured")))
      yield! expectFail "test-first order dropped" "X13" (adjust "T19" (setIn "dependsOn" (JArr [ JObj [ "task", JStr "T14"; "reason", JStr "x" ]; JObj [ "task", JStr "T18"; "reason", JStr "x" ] ])))
      yield! expectFail "v4 ordering dropped" "X13" { i0 with Adjustments = mapArr "adjustments" (fun xs -> xs @ [ JObj [ "task", JStr "T20"; "dependsOn", JArr [ JObj [ "task", JStr "T18"; "reason", JStr "x" ] ]; "rationale", JStr "x"; "evidence", JStr "estimated" ] ]) i0.Adjustments }
      yield! expectFail "estimate gutted" "X13" (adjust "T19" (setIn "estimateMinutes" (JInt 1L)))
      yield! expectFail "stale waiver" "X13" { i0 with Adjustments = mapArr "waivedPrecedences" (fun ws -> ws @ [ JObj [ "before", JStr "T18"; "after", JStr "T19"; "reason", JStr "x"; "decision", JStr "ODR-V41-03" ] ]) i0.Adjustments } ]

let all (source: string) : string list =
    (try scope source with ex -> [ "v4.1 scope: " + ex.Message ]) @ (try controls source with ex -> [ "v4.1 controls: " + ex.Message ])
