/// Compiled package v4 (Indy Init event): the scope test (K3f) and the v4 stage's negative controls.
///
/// Scope: the v4 plan is rebuilt from committed files only and must equal the committed plan, be READY with no permit,
/// keep every P0 of the v4 baseline, bind exactly the v3 bindings extended by the regenerated r7 proposal, apply only
/// amendments a trusted operator's record authorizes (the pinned approvals file is never last changed by an agent),
/// and leave v3, the R6/R7 baselines and the frozen baseline untouched. Controls: each check of V4Package must reject a package broken in the way it guards against, and the
/// freeze gate must reach READY-FOR-OWNER-FREEZE only when every recorded condition holds, and never declare FROZEN.
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

/// RV3-1, end to end: writing the owner's records changes the approvals file, its pin and the plan digest, but not
/// the content digest the package approval names, so the approval can name the plan that pins it.
let approvalDigestControl (source: string) : string list =
    let i = planInputs source
    // The owner appends the package approval to the pinned file, after any records already there.
    let packageApproval =
        JObj [ "id", JStr "APR-CONTROL"
               "authority", JObj [ "kind", JStr "operator"; "id", JStr(List.head i.Approvals.TrustedOperators) ]
               "changes", JArr [ JObj [ "decision_id", JStr "ODR-V4-01"; "action", JStr "approve-package" ] ] ]
    let withRecord = setIn "approvals" (JArr(arr "approvals" i.Approvals.Document @ [ packageApproval ])) i.Approvals.Document
    let bytes = Text.Encoding.UTF8.GetBytes(serialize withRecord)
    let approved = { i with Approvals = { i.Approvals with Commit = String('a', 40); Sha256 = Git.sha256Hex bytes; Document = withRecord } }
    let before, after = VerificationPlan.build i, VerificationPlan.build approved
    [ if VerificationPlan.digestOf before = VerificationPlan.digestOf after then yield "approval digest control: the plan digest did not change with the approvals pin (control is vacuous)"
      if V4Package.planContentDigest before <> V4Package.planContentDigest after then yield "approval digest control: writing an approval record changes the content digest the record must name (RV3-1)" ]

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
      // Bindings: v3's, extended by the proposal regenerated from the plan built with v3's bindings alone (K3e), then by the
      // v4 strengthened proposal (invariant, lesson and WebKit tests; check W16).
      if serialize (TestBindingProposal.propose v3BindingsOnly) <> serialize r7Proposal then
          yield "v4: the committed r7 proposal differs from the proposal regenerated from the plan built with v3's bindings"
      let strengthened = parseFile (Path.Combine(v4 source, "proposals", "v4-strengthened-bindings.proposed.json"))
      if serialize (JArr(arr "testBindings" i.Spec)) <> serialize (JArr(TestBindingProposal.applyTo (TestBindingProposal.applyTo (arr "testBindings" v3Spec) r7Proposal) strengthened)) then
          yield "v4: the curation's testBindings are not exactly v3's bindings extended by the r7 proposal and then the v4 strengthened proposal"
      // RDP-003: the pinned approvals file holds only trusted-operator records, last changed in a commit no agent
      // wrote, and the curation amends only what such a record authorizes; nothing is excluded.
      for ap in arr "approvals" i.Approvals.Document do
          let auth = field "authority" ap |> Option.defaultValue JNull
          if strOpt "kind" auth <> Some "operator" || not (List.contains (str "id" auth) i.Approvals.TrustedOperators) then
              yield sprintf "v4: approval %s in the pinned approvals file is not by a trusted operator (RDP-003)" (str "id" ap)
      if not (arr "approvals" i.Approvals.Document).IsEmpty then
          match Git.run source [ "log"; "-1"; "--format=%B"; i.Approvals.Commit; "--"; i.Approvals.Path ] with
          | Ok b ->
              let message = Text.Encoding.UTF8.GetString b
              if message.Contains("Claude-Session:", StringComparison.Ordinal) || message.Contains("Co-Authored-By: Claude", StringComparison.Ordinal) then
                  yield "v4: the pinned approvals file was last changed in an agent commit (RDP-003)"
          | Error e -> yield sprintf "v4: the pinned approvals commit is unreadable: %s" e
      let authorizedAmend = VerificationPlan.authorizedAmendments i.Approvals
      for a in arr "obligationAmendments" i.Spec do
          if not (authorizedAmend.ContainsKey(str "approval" a, str "obligation" a)) then
              yield sprintf "v4: the curation amends %s without an operator record" (str "obligation" a)
      if not (arr "exclusions" i.Spec).IsEmpty then yield "v4: the curation excludes an obligation without an operator record"
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
    // Rehearsal 7's five evidence files: CAUSA_R7_EVIDENCE (CI checks them out) or the local reviewer checkout.
    let evidence =
        [ Environment.GetEnvironmentVariable "CAUSA_R7_EVIDENCE"; "/home/user/kemiller2002/indy-init-rehearsal-007" ]
        |> List.tryFind (fun d -> not (String.IsNullOrEmpty d) && Directory.Exists(Path.Combine(d, "evidence")))
    let base0 = Program.v4Inputs source (v4 source) None evidence
    let i0 = { base0 with Registry = Some(syntheticRegistry base0.LockSource "approved") }
    let built0 = V4Package.build i0
    let planDigest = V4Package.planContentDigest i0.Plan
    let expectFail label (id: string) (i: V4Package.Inputs) =
        let b = V4Package.build i
        [ if (problemsOf b id).IsEmpty then yield sprintf "control %s: %s accepted a package it must reject" label id ]
    let baseline f = { i0 with Baseline = f i0.Baseline }
    let machines f = { i0 with StateMachines = mapWhere "machines" "SM-SAVE" f i0.StateMachines }
    let saveTransition (t: Json) = mapArr "transitions" (fun ts -> ts @ [ t ])
    let transition from' ev to' =
        JObj [ "id", JStr "T-X"; "from", JStr from'; "event", JStr ev; "to", JStr to'; "guard", JStr "x"; "effects", JArr []; "sources", jstrs [ "PERSIST-031" ] ]
    let operator = i0.TrustedOperators |> List.tryHead |> Option.defaultValue "kemiller2002"
    let testSha = String('f', 64)
    // Operator decisions in the approvals schema verify-plan authorizes: one approval with an operator authority and
    // its changes. Amendments are compiled into the plan through obligationAmendments, exactly as the owner's would be.
    let amendChanges (pick: Json -> bool) =
        arr "requests" i0.Proposals
        |> List.filter (fun p -> str "kind" p = "obligation-amendment" && pick p)
        |> List.collect (fun p ->
            match field "approvalRecord" p with
            | Some(JArr rs) ->
                rs |> List.map (fun r ->
                    JObj [ "decision_id", JStr(str "id" p); "obligation_id", JStr(str "obligation_id" r); "action", JStr "amend"
                           "criterion", JStr(strOpt "combinedCriterion" r |> Option.defaultValue (str "criterion" r)); "rationale", JStr(str "rationale" r) ])
            | _ -> [])
        |> List.distinctBy (str "obligation_id")
    let decisionChanges =
        arr "requests" i0.Proposals
        |> List.collect (fun p ->
            match str "kind" p with
            | "scope-or-topology" -> [ JObj [ "decision_id", JStr(str "id" p); "action", JStr "select-topology"; "topology", JStr "C-role-lanes-two-ui" ] ]
            | "scope" -> [ JObj [ "decision_id", JStr(str "id" p); "action", JStr "decide"; "option", JStr "A" ] ]
            | _ -> [])
    let approvalDoc (authority: string) (changes: Json list) =
        JObj [ "schema", JStr "causa.requirements-approvals/1"
               "approvals", JArr [ JObj [ "id", JStr "APR-V4-OWNER"; "authority", JObj [ "kind", JStr "operator"; "id", JStr authority ]; "date", JStr "2026-11-01"; "changes", JArr changes ] ] ]
    let pi = planInputs source
    let compiledWithBindings (doc: Json) (amends: Json list) (bindings: Json list) =
        let bytes = Text.Encoding.UTF8.GetBytes(serialize doc)
        let commit = String('a', 40)
        let spec =
            pi.Spec
            |> setIn "obligationAmendments" (JArr(amends |> List.map (fun c -> JObj [ "obligation", JStr(str "obligation_id" c); "approval", JStr "APR-V4-OWNER"; "decision", JStr(str "decision_id" c) ])))
            |> setIn "testBindings" (JArr bindings)
        let plan = VerificationPlan.build { pi with Spec = spec; Approvals = { pi.Approvals with Commit = commit; Sha256 = Git.sha256Hex bytes; Document = doc } }
        plan, (commit, pi.Approvals.Path, Git.sha256Hex bytes)
    let compiledWith (doc: Json) (amends: Json list) = compiledWithBindings doc amends i0.CurationBindings
    let allAmends = amendChanges (fun _ -> true)

    // Two passes, as the real workflow: compile the amendments, then bind the clauses the approvals made normative
    // (v4 --apply-bindings) and compile again.
    let scenario (authority: string) (changes: Json list) (amends: Json list) (i: V4Package.Inputs) =
        let doc = approvalDoc authority changes
        let plan1, pin = compiledWith doc amends
        let pass1 = { i with Approvals = doc; Plan = plan1; ApprovalsPin = pin }
        let bindings = TestBindingProposal.applyTo i0.BaseBindings (V4Package.strengthenedProposal pass1)
        let plan2, _ = compiledWithBindings doc amends bindings
        { pass1 with Plan = plan2; CurationBindings = bindings }
    // The owner approves the plan as finally compiled (amendments applied, approved clauses bound).
    let trustedBase = { i0 with ApprovalsAgentAuthored = false; ApprovalsOnMain = true }
    let contentDigest = V4Package.planContentDigest (scenario operator (allAmends @ decisionChanges) allAmends trustedBase).Plan
    // The review record names the plan content digest its last round reviewed (RV7-3).
    let reviewOf (digest: string) =
        JObj [ "reviewer", JStr "independent reviewer"; "reviewerIsImplementer", JBool false; "rounds", JInt 1L
               "roundVerdicts", JArr [ JObj [ "round", JInt 1L; "verdict", JStr "PASS"; "planContentDigest", JStr digest ] ]; "findings", JArr [] ]
    let review = reviewOf contentDigest
    let reviewSha = Git.sha256Hex (Text.Encoding.UTF8.GetBytes(serialize review))
    let packageChange = JObj [ "decision_id", JStr "ODR-V4-01"; "action", JStr "approve-package"; "plan_content_digest", JStr contentDigest; "review_sha256", JStr reviewSha; "test_record_sha256", JStr testSha ]
    let allChanges = allAmends @ decisionChanges @ [ packageChange ]
    let allRecorded =
        scenario operator allChanges allAmends
            { i0 with
                ApprovalsAgentAuthored = false
                ApprovalsOnMain = true
                CompilerTests = JObj [ "result", JStr "PASS"; "compilerTreeSha256", JStr i0.CompilerTreeSha256 ]
                TestRecordSha256 = testSha
                Review = review
                ReviewSha256 = reviewSha
                FreezeCommitVerified = true }
    let statusOf (i: V4Package.Inputs) =
        let b = V4Package.build i
        let _, status = V4Package.freezeStatus b (V4Package.gates i b (V4Package.planContentDigest i.Plan)) true
        status
    let mapChange (pred: Json -> bool) (f: Json -> Json) (cs: Json list) = cs |> List.map (fun c -> if pred c then f c else c)
    let byDecision id (c: Json) = strOpt "decision_id" c = Some id
    let freezeControls =
        [ if statusOf i0 <> "CANDIDATE" then yield "freeze: the committed package is not a CANDIDATE"
          if statusOf allRecorded <> "READY-FOR-OWNER-FREEZE" then
              let b = V4Package.build allRecorded
              let failing = V4Package.gates allRecorded b (V4Package.planContentDigest allRecorded.Plan) |> List.filter (fun g -> not g.Pass) |> List.map (fun g -> g.Id + ": " + g.Detail)
              let checks = b.Checks |> List.filter (fun c -> not c.Problems.IsEmpty) |> List.map (fun c -> c.Id + ": " + List.head c.Problems)
              let pre = (scenario operator (allAmends @ decisionChanges) allAmends trustedBase).Plan
              let diff =
                  match pre, allRecorded.Plan with
                  | JObj a, JObj b -> a |> List.filter (fun (k, v) -> b |> List.tryFind (fst >> (=) k) |> Option.map (snd >> serialize) <> Some(serialize v)) |> List.map fst
                  | _ -> []
              yield sprintf "freeze: every condition recorded, but the package is not READY-FOR-OWNER-FREEZE (positive control): %A %A; plan keys that differ: %A" failing checks diff
          // No combination of repository inputs makes the compiler declare FROZEN (RV2-1).
          let frozenFlag (i: V4Package.Inputs) = let b = V4Package.build i in V4Package.freezeStatus b (V4Package.gates i b (V4Package.planContentDigest i.Plan)) true |> fst |> List.contains ("frozen", true)
          if frozenFlag allRecorded then yield "freeze: the compiler declared frozen from repository inputs"
          let blocked label (i: V4Package.Inputs) = [ if statusOf i <> "CANDIDATE" then yield sprintf "freeze: ready although %s" label ]
          yield! blocked "the committed approvals hold no package approval" { allRecorded with Approvals = i0.Approvals; Plan = i0.Plan; ApprovalsPin = i0.ApprovalsPin }
          yield! blocked "the approvals file holds no record at all" { allRecorded with Approvals = setIn "approvals" (JArr []) i0.Approvals; Plan = i0.Plan; ApprovalsPin = i0.ApprovalsPin }
          yield! blocked "approved amendments were never applied to the plan (RV4-1)" (scenario operator allChanges [] allRecorded)
          let without08 = allAmends |> List.filter (fun c -> not (List.contains (strOpt "obligation_id" c) [ Some "PERSIST-023-O2"; Some "PERSIST-007-O2"; Some "PERSIST-007-O4" ]))
          let declined08 = without08 @ decisionChanges @ [ packageChange; JObj [ "decision_id", JStr "ODR-V4-08"; "action", JStr "decline" ] ]
          yield! blocked "the combined criterion stands although its partner was declined (RV4-2)" (scenario operator declined08 without08 allRecorded)
          yield! blocked "the approvals file was last changed by an agent" { allRecorded with ApprovalsAgentAuthored = true }
          yield! blocked "the approvals commit is not on origin/main" { allRecorded with ApprovalsOnMain = false }
          yield! blocked "the authority is not a trusted operator" (scenario "someone-else" allChanges allAmends allRecorded)
          let weaker = allChanges |> mapChange (fun c -> strOpt "obligation_id" c = Some "PERSIST-031-O22") (setIn "criterion" (JStr "The action is Retry."))
          yield! blocked "an amendment approval carries a weaker criterion" (scenario operator weaker (weaker |> List.filter (fun c -> strOpt "action" c = Some "amend")) allRecorded)
          yield! blocked "the package approval names another plan" (scenario operator (allChanges |> mapChange (byDecision "ODR-V4-01") (setIn "plan_content_digest" (JStr(String('e', 64))))) allAmends allRecorded)
          yield! blocked "the package approval names another review record" { allRecorded with ReviewSha256 = String('1', 64) }
          yield! blocked "the selected topology does not fit" (scenario operator (allChanges |> mapChange (byDecision "ODR-V4-07") (setIn "topology" (JStr "B-single-lead"))) allAmends allRecorded)
          yield! blocked "no topology was selected" (scenario operator (allChanges |> List.filter (byDecision "ODR-V4-07" >> not)) allAmends allRecorded)
          yield! blocked "a scope decision names an option the request does not list" (scenario operator (allChanges |> mapChange (byDecision "ODR-V4-10") (setIn "option" (JStr "Z"))) allAmends allRecorded)
          yield! blocked "the test record is for another compiler tree" { allRecorded with CompilerTests = JObj [ "result", JStr "PASS"; "compilerTreeSha256", JStr(String('d', 64)) ] }
          yield! blocked "the review has an open major finding" { allRecorded with Review = setIn "findings" (JArr [ JObj [ "id", JStr "F1"; "severity", JStr "major"; "status", JStr "open" ] ]) review }
          yield! blocked "the reviewer is the implementer" { allRecorded with Review = setIn "reviewerIsImplementer" (JBool true) review }
          yield! blocked "the review record only claims PASS" { allRecorded with Review = JObj [ "verdict", JStr "PASS" ] }
          yield! blocked "the reviewer's last round failed although the author marked findings fixed" { allRecorded with Review = setIn "roundVerdicts" (JArr [ JObj [ "round", JInt 1L; "verdict", JStr "FAIL"; "planContentDigest", JStr contentDigest ] ]) review }
          // RV7-3: a PASS for an earlier plan does not count; the review record keeps its digest, so only FG-09 can fail.
          yield! blocked "the last review round reviewed another plan (RV7-3)" { allRecorded with Review = reviewOf (String('e', 64)) }
          yield! blocked "the last review round names no plan (RV7-3)" { allRecorded with Review = setIn "roundVerdicts" (JArr [ JObj [ "round", JInt 1L; "verdict", JStr "PASS" ] ]) review }
          yield! blocked "the freeze commit is not verified" { allRecorded with FreezeCommitVerified = false }
          yield! blocked "the window is too small for the selected topology" { allRecorded with Policy = setIn "clock" (JObj [ "timedMinutes", JInt 100L ]) allRecorded.Policy }
          // RV5-2: clauses the owner approved are bound in the plan, so verify-plan enforces them.
          let unboundNormative =
              (V4Package.build allRecorded).Files |> List.find (fst >> (=) "acceptance/acceptance-obligations.json") |> snd |> arr "obligations"
              |> List.filter (fun o -> strOpt "origin" o <> Some "verification-plan" && boolOr "normative" false o && not (boolOr "boundInPlan" false o)
                                       && (arr "requiredTests" o |> List.exists (fun t -> strs "categories" t |> List.exists (fun c -> [ "unit"; "integration"; "browser"; "live-provider"; "security"; "accessibility" ] |> List.contains c))))
              |> List.map (str "id")
          if not unboundNormative.IsEmpty then yield sprintf "freeze: approved clauses are normative but not bound in the plan: %A" unboundNormative
          // RV5-1: FG-10 reads the package approval in the same schema as every other record.
          if not (V4Package.freezeCommitHoldsApproval [ operator ] allRecorded.Approvals) then yield "freeze: the freeze-commit check does not find a package approval written in the approvals schema"
          if V4Package.freezeCommitHoldsApproval [ operator ] (approvalDoc "someone-else" allChanges) then yield "freeze: the freeze-commit check accepts a package approval by an untrusted authority"
          if V4Package.freezeCommitHoldsApproval [ operator ] (approvalDoc operator allAmends) then yield "freeze: the freeze-commit check accepts approvals without the package approval"
          let proposedCount (i: V4Package.Inputs) =
              (V4Package.build i).Files |> List.find (fst >> (=) "acceptance/acceptance-obligations.json") |> snd |> field "summary" |> Option.bind (intOpt "proposed")
          if proposedCount allRecorded <> Some 0L then yield "freeze: approved and applied amendments did not make their acceptance obligations normative (RV4-1)"
          let unapproved = { i0 with Approvals = setIn "approvals" (JArr []) i0.Approvals }
          if proposedCount unapproved = Some 0L then yield "freeze: proposed acceptance obligations are normative without the owner's approval"
          yield! expectFail "untrusted authority" "W13" (scenario "someone-else" allChanges allAmends i0)
          yield! expectFail "agent-authored approvals with records" "W13" { allRecorded with ApprovalsAgentAuthored = true }
          // RV7-4: an applied amendment labelled with a decision that does not propose it.
          let mislabelled = allAmends |> mapChange (fun c -> strOpt "obligation_id" c = Some "PERSIST-026-O2") (setIn "decision_id" (JStr "ODR-V4-05"))
          yield! expectFail "an amendment names a decision that does not propose it (RV7-4)" "W13" (scenario operator allChanges mislabelled allRecorded) ]
    [ // The committed inputs pass every check with a valid registry.
      for c in built0.Checks do
          if not c.Problems.IsEmpty && not (c.Id = "W07" && evidence.IsNone) then yield sprintf "v4 stage: %s fails on the committed package: %A" c.Id (List.truncate 3 c.Problems)
      yield! expectFail "no Rehearsal 7 evidence" "W07" { i0 with R7Evidence = None }
      yield! expectFail "strengthened binding missing from the curation" "W16" { i0 with CurationBindings = i0.BaseBindings }
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
      yield! freezeControls ]

/// verify-plan: a category is covered only when every bound ledger test declaring it passed (RV2-2).
let verifyPlanControls () : string list =
    let test id = JObj [ "id", JStr id; "kind", JStr "deterministic"; "categories", jstrs [ "browser" ] ]
    let o =
        JObj
            [ "id", JStr "VPO-control"
              "acceptanceContract", JObj [ "requiredEvidenceCategories", jstrs [ "browser" ]; "testIdentity", JObj [ "tests", JArr [ test "t [chromium]"; test "t [webkit]" ] ] ] ]
    let evidence outcomes =
        [ JObj [ "obligations", JArr [ JObj [ "id", JStr "VPO-control"; "verification", JStr "PASS"; "tests", JArr(outcomes |> List.map (fun (t, r) -> JObj [ "testId", JStr t; "outcome", JStr r ])) ] ] ] ]
    let covered ev = let _, c, _ = PlanVerification.coverage Map.empty None "PASS" ev o in c
    [ if covered (evidence [ "t [chromium]", "passed"; "t [webkit]", "failed" ]) <> [] then yield "verify-plan: a failed WebKit twin was outvoted by a passing Chromium test"
      if covered (evidence [ "t [chromium]", "passed" ]) <> [] then yield "verify-plan: a missing WebKit result was outvoted by a passing Chromium test"
      if covered (evidence [ "t [chromium]", "passed"; "t [webkit]", "passed" ]) <> [ "browser" ] then yield "verify-plan: both twins passed but the category is not covered (positive control)" ]

let all (source: string) : string list =
    (try scope source with ex -> [ "v4 scope: " + ex.Message ]) @ (try approvalDigestControl source with ex -> [ "v4 approval digest: " + ex.Message ]) @ (try controls source with ex -> [ "v4 controls: " + ex.Message ]) @ verifyPlanControls ()
