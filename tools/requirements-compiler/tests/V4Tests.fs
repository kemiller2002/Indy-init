/// Compiled package v4 (Indy Init event): the scope test (K3f) and the v4 stage's negative controls.
///
/// Scope: the v4 plan is rebuilt from committed files only and must equal the committed plan, be READY with no permit,
/// keep every P0 of the v4 baseline, bind exactly the v3 bindings extended by the regenerated r7 proposal, apply no
/// amendment (the pinned approvals file holds no record), and leave v3, the R6/R7 baselines and the frozen baseline
/// untouched. Controls: each check of V4Package must reject a package broken in the way it guards against, and the
/// freeze gate must reach FROZEN only when every recorded condition holds.
module V4Tests

open System
open System.IO
open Json

let private v4 (source: string) = Path.Combine(source, "requirements-compiled", "v4")
let private v3PlanDigest = "8ef1c8dca656e515f63ff287d560a9b76ffbaffc0e5fa27d81f6637b3aa46e41"
let private frozenBaselineSha = "621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9"
let private r6BaselineSha = "40599abe92724dae6796ee0d6a9bf5711c4db882fc79c77b38a43d0f0960befd"
let private v4BaselineCommit = "62df28a559c054226dd4022e9744cf1db06e954f"

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let private mapArr (key: string) (f: Json list -> Json list) (j: Json) = setIn key (JArr(f (arr key j))) j

let private mapWhere (key: string) (id: string) (f: Json -> Json) (j: Json) =
    mapArr key (List.map (fun x -> if strOpt "id" x = Some id then f x else x)) j

/// The v4 plan inputs from committed files only (the committed verification manifest stands in for the full compile).
let planInputs (source: string) : VerificationPlan.Inputs =
    let spec = parseFile (Path.Combine(v4 source, "curation", "verification-plan.json"))
    let categoriesPath = Path.Combine(v4 source, "curation", "evidence-categories.json")
    let a = field "approvals" spec |> Option.defaultValue JNull
    let approvals =
        match Git.showFile source (str "commit" a) (str "path" a) with
        | Ok b -> b
        | Error e -> failwithf "v4 approvals unreadable: %s" e
    let baselineBytes = File.ReadAllBytes(Path.Combine(source, "requirements", "causa-requirements-baseline-v4.json"))
    let baseline = parseBytes baselineBytes
    let src = field "source" baseline |> Option.defaultValue JNull
    { Repository = str "repository" src
      Commit = str "commit" src
      BaselineId = str "id" baseline
      BaselineSha256 = Git.sha256Hex baselineBytes
      Baseline = baseline
      Manifest = parseFile (Path.Combine(v4 source, "verification-manifest.json"))
      Spec = spec
      Approvals =
        { Repository = "kemiller2002/indy-init"
          Commit = str "commit" a
          Path = str "path" a
          Sha256 = Git.sha256Hex approvals
          TrustedOperators = strs "trustedOperators" a
          Document = parseBytes approvals }
      Categories = { Path = "curation/evidence-categories.json"; Sha256 = Git.sha256Hex (File.ReadAllBytes categoriesPath); Table = parseFile categoriesPath } }

let scope (source: string) : string list =
    let i = planInputs source
    let plan = VerificationPlan.build i
    let committed = File.ReadAllText(Path.Combine(v4 source, "verification-plan.json"))
    let committedPlan = parseFile (Path.Combine(v4 source, "verification-plan.json"))
    let gate = PlanReadiness.assess committedPlan (VerificationPlan.digestOf committedPlan) None
    let v3Spec = parseFile (Path.Combine(source, "requirements-compiled", "v3", "curation", "verification-plan.json"))
    let r7Proposal = parseFile (Path.Combine(v4 source, "proposals", "r7-test-bindings.proposed.json"))
    let v3BindingsOnly = VerificationPlan.build { i with Spec = setIn "testBindings" (JArr(arr "testBindings" v3Spec)) i.Spec }
    let scopeOf k = field "scope" plan |> Option.bind (intOpt k) |> Option.defaultValue -1L
    let baselineReq rid = arr "requirements" i.Baseline |> List.tryFind (fun r -> str "id" r = rid)
    let sources = parseFile (Path.Combine(v4 source, "curation", "sources.json"))
    let curationText = Directory.GetFiles(Path.Combine(v4 source, "curation"), "*.json") |> Array.map File.ReadAllText |> String.concat "\n"
    let recorded =
        Directory.GetDirectories(Path.Combine(source, "kickoff"))
        |> Array.map (fun d -> Path.Combine(d, "owner-decisions.json"))
        |> Array.filter File.Exists
        |> Array.toList
        |> List.collect (parseFile >> arr "decisions")
        |> List.map (str "id")
        |> Set.ofList
    [ if serialize plan <> committed then yield "v4: the committed verification-plan.json differs from a rebuild from committed files"
      if not (VerificationPlan.problems i).IsEmpty then yield sprintf "v4: input problems %A" (VerificationPlan.problems i)
      if not (VerificationPlan.preservationProblems i.Baseline plan).IsEmpty then yield "v4: the plan fails preservation"
      if i.Commit <> v4BaselineCommit then yield sprintf "v4: the baseline is pinned at %s, not %s" i.Commit v4BaselineCommit
      if scopeOf "requirements" <> 161L || scopeOf "obligations" <> 613L then yield sprintf "v4: expected 161 P0 requirements and 613 obligations, got %d and %d" (scopeOf "requirements") (scopeOf "obligations")
      if scopeOf "unclassifiedObligations" <> 0L || scopeOf "missingTestObligations" <> 0L then yield "v4: an obligation is unclassified or unbound"
      if str "verdict" gate <> "READY" || (field "summary" gate |> Option.bind (intOpt "permitted")) <> Some 0L then
          yield sprintf "v4: plan-readiness is %s (%A)" (str "verdict" gate) (field "summary" gate)
      // Every PERSIST P0 requirement is in the plan (K3a, K3b).
      for r in arr "requirements" i.Baseline |> List.filter (fun r -> str "priority" r = "P0" && (str "id" r).StartsWith("PERSIST-", StringComparison.Ordinal)) do
          if not (arr "requirements" plan |> List.exists (fun e -> str "requirement" e = str "id" r)) then yield sprintf "v4: P0 %s is missing from the plan" (str "id" r)
      // DCC-013..015 are written with no priority (DEC-R7-008..010 assign none): no silent promotion.
      for rid in [ "DCC-013"; "DCC-014"; "DCC-015" ] do
          match baselineReq rid with
          | None -> yield sprintf "v4: %s is missing from the v4 baseline" rid
          | Some r when strOpt "priority" r <> Some "unstated" -> yield sprintf "v4: %s was given priority %s" rid (str "priority" r)
          | Some _ -> ()
      // Bindings: v3's, extended by the proposal regenerated from the plan built with v3's bindings alone (K3e).
      if serialize (TestBindingProposal.propose v3BindingsOnly) <> serialize r7Proposal then
          yield "v4: the committed r7 proposal differs from the proposal regenerated from the plan built with v3's bindings"
      if serialize (JArr(arr "testBindings" i.Spec)) <> serialize (JArr(TestBindingProposal.applyTo (arr "testBindings" v3Spec) r7Proposal)) then
          yield "v4: the curation's testBindings are not exactly v3's bindings extended by the r7 proposal"
      // No agent-written approval (RDP-003): nothing is amended or excluded.
      if not (arr "approvals" i.Approvals.Document).IsEmpty then yield "v4: the pinned approvals file holds approval records; only the operator may write them (RDP-003)"
      if not (arr "obligationAmendments" i.Spec).IsEmpty || not (arr "exclusions" i.Spec).IsEmpty then yield "v4: the curation amends or excludes an obligation without an operator record"
      for d in arr "derivedObligations" i.Spec do
          if not (recorded.Contains(str "decision" d)) then yield sprintf "v4: %s cites decision %s, which no owner-decisions.json records" (str "obligation" d) (str "decision" d)
      if not (VerificationPlan.derivationDiagnostics plan).IsEmpty then yield sprintf "v4: derivation refused %A" (VerificationPlan.derivationDiagnostics plan)
      // Historical rehearsals are not build inputs.
      if arr "repositories" sources |> List.exists (fun r -> (str "name" r).StartsWith("indy-init-rehearsal-", StringComparison.Ordinal)) then yield "v4: sources.json pins a rehearsal repository"
      if curationText.Contains("\"repo\": \"indy-init-rehearsal-", StringComparison.Ordinal) then yield "v4: the curation cites a rehearsal repository"
      // Earlier packages and baselines are unchanged.
      if VerificationPlan.digestOf (parseFile (Path.Combine(source, "requirements-compiled", "v3", "verification-plan.json"))) <> v3PlanDigest then yield "frozen: the v3 plan changed"
      if Git.sha256Hex (File.ReadAllBytes(Path.Combine(source, "requirements", "causa-requirements-baseline.json"))) <> frozenBaselineSha then yield "frozen: requirements/causa-requirements-baseline.json changed"
      if Git.sha256Hex (File.ReadAllBytes(Path.Combine(source, "requirements", "causa-requirements-baseline-r6.json"))) <> r6BaselineSha then yield "frozen: the r6 baseline changed" ]

// ---- V4Package controls ----------------------------------------------------------------------------------------

/// A self-consistent in-memory registry for the lock source's components, so the controls do not need a checkout.
let syntheticRegistry (lockSource: Json) (status: string) : string -> byte[] option =
    let set = field "releaseSet" lockSource |> Option.defaultValue JNull
    let components =
        arr "systems" lockSource
        |> List.filter (fun s -> str "kind" s = "registry-component")
        |> List.map (fun s ->
            let sid = str "systemId" s
            let manifest =
                JObj
                    [ "systemId", JStr sid; "version", JStr "1.0.0"; "repository", JStr("kemiller2002/" + sid); "tag", JStr "v1.0.0"
                      "commit", JStr(String('a', 40)); "releaseStage", JStr "stable"; "distributionClass", JStr "web-package"
                      "distributions", JArr []; "artifacts", JArr [ JObj [ "name", JStr(sid + ".tgz"); "purpose", JStr "package"; "sha256", JStr(String('b', 64)) ] ] ]
                |> serialize |> Text.Encoding.UTF8.GetBytes
            sid, manifest)
    let setDoc =
        JObj
            [ "status", JStr status
              "components", JArr(components |> List.map (fun (sid, m) -> JObj [ "systemId", JStr sid; "version", JStr "1.0.0"; "releaseManifestSha256", JStr(Git.sha256Hex m) ])) ]
        |> serialize |> Text.Encoding.UTF8.GetBytes
    let files = (str "path" set, setDoc) :: (components |> List.map (fun (sid, m) -> sprintf "releases/%s/1.0.0.release.json" sid, m)) |> Map.ofList
    fun path -> files.TryFind path

let private problemsOf (b: V4Package.Built) id = (b.Checks |> List.find (fun c -> c.Id = id)).Problems

let controls (source: string) : string list =
    let base0 = Program.v4Inputs source (v4 source) None None
    let i0 = { base0 with Registry = Some(syntheticRegistry base0.LockSource "approved") }
    let built0 = V4Package.build i0
    let planDigest = field "planIdentity" i0.Plan |> Option.map (str "digest") |> Option.defaultValue ""
    let expectFail label (id: string) (i: V4Package.Inputs) =
        let b = V4Package.build i
        [ if (problemsOf b id).IsEmpty then yield sprintf "control %s: %s accepted a package it must reject" label id ]
    let baseline f = { i0 with Baseline = f i0.Baseline }
    let machines f = { i0 with StateMachines = mapWhere "machines" "SM-SAVE" f i0.StateMachines }
    let saveTransition (t: Json) = mapArr "transitions" (fun ts -> ts @ [ t ])
    let transition from' ev to' =
        JObj [ "id", JStr "T-X"; "from", JStr from'; "event", JStr ev; "to", JStr to'; "guard", JStr "x"; "effects", JArr []; "sources", jstrs [ "PERSIST-031" ] ]
    let approvalRecords =
        arr "requests" i0.Proposals
        |> List.collect (fun p ->
            match str "kind" p, field "approvalRecord" p with
            | "obligation-amendment", Some(JArr rs) -> rs
            | "package-approval", _ -> [ JObj [ "decision_id", JStr "ODR-V4-01"; "action", JStr "approve-package"; "plan_digest", JStr planDigest; "approved_by", JStr "kemiller2002" ] ]
            | _ -> [ JObj [ "decision_id", JStr(str "id" p); "action", JStr "decide"; "approved_by", JStr "kemiller2002" ] ])
    let allRecorded =
        { i0 with
            Approvals = setIn "approvals" (JArr approvalRecords) i0.Approvals
            CompilerTests = JObj [ "result", JStr "PASS"; "compilerTreeSha256", JStr i0.CompilerTreeSha256 ]
            Review = JObj [ "verdict", JStr "PASS"; "reviewer", JStr "independent reviewer"; "findings", JArr [] ]
            FreezeInput = setIn "freezeCommit" (JStr(String('c', 40))) i0.FreezeInput }
    let statusOf (i: V4Package.Inputs) =
        let b = V4Package.build i
        let _, status = V4Package.freezeStatus b (V4Package.gates i b planDigest) true
        status
    [ // The committed inputs pass every check with a valid registry.
      for c in built0.Checks do
          if not c.Problems.IsEmpty then yield sprintf "v4 stage: %s fails on the committed package: %A" c.Id (List.truncate 3 c.Problems)
      // Determinism.
      if (V4Package.build i0).Files |> List.map (fun (n, j) -> n, serialize j) <> (built0.Files |> List.map (fun (n, j) -> n, serialize j)) then yield "v4 stage: two builds differ"
      // W01: disappearance, P0 downgrade, unauthorized change.
      yield! expectFail "requirement removed" "W01" (baseline (mapArr "requirements" (List.filter (fun r -> str "id" r <> "PERSIST-001"))))
      yield! expectFail "P0 downgraded" "W01" (baseline (mapWhere "requirements" "UI-001" (setIn "priority" (JStr "P1"))))
      yield! expectFail "unauthorized text change" "W01" (baseline (mapWhere "requirements" "R0.1" (setIn "sectionSha256" (JStr(String('0', 64))))))
      yield! expectFail "amendment cites an unrecorded decision" "W01" { i0 with Amendments = mapArr "requirements" (List.map (setIn "decisions" (jstrs [ "DEC-NOT-RECORDED" ]))) i0.Amendments }
      // W03: a P0 requirement missing from the plan.
      yield! expectFail "P0 missing from the plan" "W03" { i0 with Plan = mapArr "requirements" (List.filter (fun e -> str "requirement" e <> "PERSIST-007")) i0.Plan }
      // W04: a definition citing a requirement that does not exist.
      yield! expectFail "definition source unknown" "W04" { i0 with Definitions = mapArr "terms" (List.mapi (fun n t -> if n = 0 then setIn "sources" (jstrs [ "NOPE-001" ]) t else t)) i0.Definitions }
      // W05: Saved reached by something other than a confirmation; an undeclared state; an unsourced transition.
      yield! expectFail "Saved entered by Edit" "W05" (machines (saveTransition (transition "Unsaved" "Edit" "Saved")))
      yield! expectFail "undeclared state" "W05" (machines (saveTransition (transition "Unsaved" "Edit" "Limbo")))
      yield! expectFail "unsourced transition" "W05" (machines (saveTransition (setIn "sources" (JArr []) (transition "Saved" "Edit" "Unsaved") |> setIn "guard" (JStr "y"))))
      yield! expectFail "ambiguous guard" "W05" (machines (saveTransition (transition "Saved" "Edit" "Unsaved" |> setIn "guard" (JStr "online"))))
      // W06: an invariant citing an unknown requirement; a missing group.
      yield! expectFail "invariant source unknown" "W06" { i0 with Invariants = mapWhere "invariants" "INV-01" (setIn "sources" (jstrs [ "NOPE-001" ])) i0.Invariants }
      yield! expectFail "invariant group missing" "W06" { i0 with Invariants = mapArr "invariants" (List.filter (fun v -> str "group" v <> "security")) i0.Invariants }
      // W07: a required class dropped; a gap without a decision request; a quote that is not in the evidence.
      yield! expectFail "lesson class dropped" "W07" { i0 with Lessons = mapArr "lessons" (List.filter (fun l -> str "class" l <> "Stale refreshes")) i0.Lessons }
      yield! expectFail "gap without proposal" "W07" { i0 with Lessons = mapWhere "lessons" "R7L-04" (setIn "proposals" (JArr [])) i0.Lessons }
      yield! expectFail "quote not in evidence" "W07" { i0 with R7Evidence = Some(fun _ -> Some "unrelated text") }
      // W08: a release set that is not approved; a component missing from the set.
      yield! expectFail "set not approved" "W08" { i0 with Registry = Some(syntheticRegistry i0.LockSource "draft") }
      yield! expectFail "component not in set" "W08" { i0 with LockSource = mapArr "systems" (fun xs -> xs @ [ JObj [ "id", JStr "react"; "kind", JStr "registry-component"; "systemId", JStr "react"; "use", JStr "runtime"; "runtimeCompatibility", JStr "" ] ]) i0.LockSource }
      yield! expectFail "no registry" "W08" base0
      // W09: an interface naming a contract that does not exist.
      yield! expectFail "unknown contract" "W09" { i0 with Interfaces = mapWhere "interfaces" "IF-STORAGE" (setIn "contracts" (jstrs [ "CT-999" ])) i0.Interfaces }
      // W10: a window too small; a timed task without an estimate.
      yield! expectFail "window too small" "W10" { i0 with Policy = setIn "clock" (JObj [ "timedMinutes", JInt 100L ]) i0.Policy }
      yield! expectFail "missing estimate" "W10" { i0 with Tasks = i0.Tasks |> List.map (fun t -> if t.Id = "T19" then { t with Raw = (match t.Raw with JObj ps -> JObj(ps |> List.filter (fst >> (<>) "estimateMinutes")) | j -> j) } else t) }
      // W11: a proposed amendment that could weaken its obligation.
      yield! expectFail "weakening amendment" "W11"
          { i0 with Proposals = mapWhere "requests" "ODR-V4-05" (fun p -> setIn "approvalRecord" (JArr [ JObj [ "obligation_id", JStr "PERSIST-012-O2"; "action", JStr "amend"; "criterion", JStr "The application may offer actions."; "rationale", JStr "x" ] ]) p) i0.Proposals }
      // W13: an approval record naming an agent.
      yield! expectFail "agent-written approval" "W13" { i0 with Approvals = setIn "approvals" (JArr [ JObj [ "decision_id", JStr "ODR-V4-01"; "approved_by", JStr "Claude agent" ] ]) i0.Approvals }
      // W14: application source in the package.
      yield! expectFail "application source" "W14" { i0 with PackageFiles = i0.PackageFiles @ [ "src/App.fs" ] }
      // W15: a plan compiled from another baseline.
      yield! expectFail "plan from another baseline" "W15" { i0 with Plan = setIn "source" (JObj [ "baselineSha256", JStr(String('9', 64)) ]) i0.Plan }
      // Freeze gate: CANDIDATE as committed; FROZEN only when every condition is recorded; each missing condition blocks it.
      if statusOf i0 <> "CANDIDATE" then yield "freeze: the committed package is not a CANDIDATE"
      if statusOf allRecorded <> "FROZEN" then yield "freeze: every condition recorded, but the package is not FROZEN (positive control)"
      if statusOf { allRecorded with Approvals = i0.Approvals } <> "CANDIDATE" then yield "freeze: FROZEN without owner approvals"
      if statusOf { allRecorded with CompilerTests = JObj [ "result", JStr "PASS"; "compilerTreeSha256", JStr(String('d', 64)) ] } <> "CANDIDATE" then yield "freeze: FROZEN with a test record for another compiler tree"
      if statusOf { allRecorded with Review = JObj [ "verdict", JStr "PASS"; "findings", JArr [ JObj [ "id", JStr "F1"; "severity", JStr "critical"; "status", JStr "open" ] ] ] } <> "CANDIDATE" then yield "freeze: FROZEN with an unresolved critical review finding"
      if statusOf { allRecorded with FreezeInput = setIn "freezeCommit" (JStr "") allRecorded.FreezeInput } <> "CANDIDATE" then yield "freeze: FROZEN without a freeze commit"
      let wrongDigest = setIn "approvals" (JArr(approvalRecords |> List.map (fun r -> if strOpt "decision_id" r = Some "ODR-V4-01" then setIn "plan_digest" (JStr(String('e', 64))) r else r))) i0.Approvals
      if statusOf { allRecorded with Approvals = wrongDigest } <> "CANDIDATE" then yield "freeze: FROZEN with a package approval for another plan" ]

let all (source: string) : string list =
    (try scope source with ex -> [ "v4 scope: " + ex.Message ]) @ (try controls source with ex -> [ "v4 controls: " + ex.Message ])
