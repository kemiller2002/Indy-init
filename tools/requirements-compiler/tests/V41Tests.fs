/// Compiled package v4.1: the committed package is reproduced from its inputs, and each check of V41Package rejects
/// inputs broken in the way it guards against (ownership violations, interface incompatibility, verification coverage
/// drop, nondeterministic transitions, missing error classes, unverified Forma tokens, cyclic compile order).
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
          if (field k counts |> Option.bind (intOpt "v41")) <> Some n then yield sprintf "v4.1: %s is not %d" k n ]

let controls (source: string) : string list =
    let i0 = inputs source
    let structure f = { i0 with Structure = f i0.Structure }
    let project id f = structure (mapWhere "projects" "id" id f)
    let machines f = { i0 with StateMachines = mapWhere "machines" "id" "SM-SAVE" f i0.StateMachines }
    let saveSpec f = { i0 with Interfaces = mapWhere "interfaces" "id" "IF41-SAVE" f i0.Interfaces }
    [ // Ownership: a file listed twice with two owners; a project file not owned by integration; an unknown role.
      yield! expectFail "file with two owners" "X01" (structure (mapArr "sharedFiles" (fun fs -> fs @ [ JObj [ "path", JStr "web/causa.css"; "owner", JStr "ui" ] ])))
      yield! expectFail "unknown owner role" "X01" (structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some "README.md" then setIn "owner" (JStr "intern") f else f))))
      yield! expectFail "project file owned by ui" "X02" (project "Causa.App" (setIn "owner" (JStr "ui")))
      yield! expectFail "CI workflow owned by persistence" "X02" (structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some ".github/workflows/ci.yml" then setIn "owner" (JStr "persistence") f else f))))
      // Compile order: a project reference cycle; a file depending on a later file.
      yield! expectFail "cyclic project references" "X03" (project "Causa.Domain" (setIn "references" (jstrs [ "Causa.App" ])))
      yield! expectFail "file depends on a later file" "X03"
          (project "Causa.Domain" (mapArr "files" (List.map (fun f -> if strOpt "path" f = Some "src/Causa.Domain/Identity.fs" then setIn "dependsOn" (jstrs [ "src/Causa.Domain/Kernel.fs" ]) f else f))))
      // Interface compatibility: implemented by a file the producer does not own; a contract without consumer tests;
      // a save state the machine does not have.
      yield! expectFail "interface in a file the producer does not own" "X04" (saveSpec (setIn "file" (JStr "src/Causa.App/Engine.fs")))
      yield! expectFail "contract without consumer tests" "X04" (saveSpec (setIn "consumerTests" (JArr [])))
      yield! expectFail "new persistence state" "X05" (saveSpec (fun x -> setIn "fsharp" (JStr((str "fsharp" x).Replace("    | Saving\n", "    | Saving\n    | Syncing\n"))) x))
      // Error matrix: a required class missing; a shared status with no other evidence; retries beyond the bound; a
      // test the plan does not bind.
      yield! expectFail "stale-head class missing" "X06" { i0 with ErrorMatrix = mapArr "classes" (List.filter (fun c -> strOpt "id" c <> Some "GH-STALE-HEAD")) i0.ErrorMatrix }
      yield! expectFail "classified by status alone" "X06" { i0 with ErrorMatrix = mapWhere "classes" "id" "GH-PERMISSION" (setIn "nonStatusEvidence" (jstrs [ "status:403" ])) i0.ErrorMatrix }
      yield! expectFail "retries beyond the bound" "X06" { i0 with ErrorMatrix = mapWhere "classes" "id" "GH-STALE-HEAD" (setIn "maxAttempts" (JInt 9L)) i0.ErrorMatrix }
      yield! expectFail "unbound test" "X06" { i0 with ErrorMatrix = mapWhere "classes" "id" "GH-AUTH" (setIn "tests" (jstrs [ "dotnet:Causa.Acceptance.Deterministic.NOPE.X" ])) i0.ErrorMatrix }
      // Forma: an invented token; a pattern Forma does not have; a breakpoint Forma does not use.
      yield! expectFail "invented token" "X07" { i0 with Forma = setIn "tokens" (JObj [ "required", jstrs [ "--ef-causa-invented" ] ]) i0.Forma }
      yield! expectFail "unknown pattern" "X07" { i0 with Forma = mapArr "reuse" (fun rs -> rs @ [ JObj [ "need", JStr "x"; "patterns", jstrs [ "causa-card" ] ] ]) i0.Forma }
      yield! expectFail "unknown breakpoint" "X07" { i0 with Forma = setIn "breakpoints" (JObj [ "values", jstrs [ "52rem" ] ]) i0.Forma }
      // Telemetry: a required metric dropped.
      yield! expectFail "metric missing" "X08" { i0 with Telemetry = mapArr "metrics" (List.filter (fun m -> strOpt "id" m <> Some "ownership-violations")) i0.Telemetry }
      // Verification coverage: a bound test no suite runs.
      let addTest (t: string) =
          mapArr "requirements" (List.mapi (fun n r ->
              if n > 0 then r
              else mapArr "obligations" (List.mapi (fun k o ->
                  if k > 0 then o
                  else
                      let c = field "acceptanceContract" o |> Option.defaultValue JNull
                      let ti = field "testIdentity" c |> Option.defaultValue JNull
                      setIn "acceptanceContract" (setIn "testIdentity" (mapArr "tests" (fun ts -> ts @ [ JObj [ "id", JStr t; "kind", JStr "deterministic"; "categories", jstrs [ "unit" ] ] ]) ti) c) o)) r))
      yield! expectFail "test no suite runs" "X09" { i0 with Plan = addTest "junit:Causa.Orphan" i0.Plan }
      yield! expectFail "test with no file" "X14" { i0 with Plan = addTest "junit:Causa.Orphan" i0.Plan }
      // Preservation: a changed plan; a P0 obligation the plan lost.
      yield! expectFail "plan bytes changed" "X10" { i0 with PlanBytes = Array.append i0.PlanBytes [| 32uy |] }
      yield! expectFail "P0 requirement dropped from the plan" "X10" { i0 with Plan = mapArr "requirements" (List.filter (fun r -> strOpt "requirement" r <> Some "PERSIST-007")) i0.Plan }
      // Provenance: an input naming an earlier rehearsal's application.
      yield! expectFail "rehearsal source named" "X11" { i0 with AuthoredText = i0.AuthoredText @ [ "analysis/x.json", "copied from kemiller2002/indy-init-rehearsal-003" ] }
      // State machines: two transitions the same guard selects; a target state the machine does not have.
      let dup = arr "transitions" (arr "machines" i0.StateMachines |> List.find (fun m -> strOpt "id" m = Some "SM-SAVE")) |> List.head
      yield! expectFail "nondeterministic transition" "X12" (machines (mapArr "transitions" (fun ts -> ts @ [ setIn "id" (JStr "T-S99") dup |> setIn "to" (JStr "Offline") ])))
      yield! expectFail "unknown next state" "X12" (machines (mapArr "transitions" (fun ts -> ts @ [ setIn "id" (JStr "T-S99") dup |> setIn "guard" (JStr "never") |> setIn "to" (JStr "Limbo") ])))
      // DAG: a cycle; an unlabelled estimate.
      yield! expectFail "cyclic DAG" "X13" { i0 with Adjustments = mapWhere "adjustments" "task" "T12" (setIn "dependsOn" (JArr [ JObj [ "task", JStr "T21"; "reason", JStr "x" ] ])) i0.Adjustments }
      yield! expectFail "estimate labelled measured" "X13" { i0 with Adjustments = mapWhere "adjustments" "task" "T16" (setIn "evidence" (JStr "measured")) i0.Adjustments } ]

let all (source: string) : string list =
    (try scope source with ex -> [ "v4.1 scope: " + ex.Message ]) @ (try controls source with ex -> [ "v4.1 controls: " + ex.Message ])
