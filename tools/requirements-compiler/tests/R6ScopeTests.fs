/// Rehearsal 6 scope decisions (owner decisions DEC-R6-001..010): the v3 package re-pins the baseline at 856157a
/// (AR-027..AR-035) and compiles UI-015-O2 as derived from its constituents (R5A-001-05); verify-plan honours review
/// tag removals (R5A-001-04). Obligation amendments apply only through operator-written approval records (RDP-003);
/// the pinned file holds none yet, so UI-031-O5 and AR-019-O3 stay as written.
/// Every scope decision has a negative control: an unauthorized, altered or inconsistent one is refused, never applied.
module R6ScopeTests

open System
open System.IO
open Json

let private v3 (source: string) = Path.Combine(source, "requirements-compiled", "v3")
let private v2PlanDigest = "0eb9c791dd0beba7d63f709a810c2f0d379ea71e03c74a53d8b34951a9260766"
let private frozenBaselineSha = "621e2f960038dcfb0daa2a9fe18a0c1c350faed297ba2856da1236a0360857b9"
let private r6BaselineCommit = "856157a78d47946d264ec49ee8325fcf996ae9b9"

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let private hasProblem (needle: string) (ps: string list) = ps |> List.exists (fun p -> p.Contains(needle, StringComparison.Ordinal))

let private reseal (plan: Json) =
    let d = VerificationPlan.digestOf plan
    let identity = field "planIdentity" plan |> Option.defaultValue (JObj []) |> setIn "digest" (JStr d) |> setIn "planId" (JStr("VP-" + d.Substring(0, 12)))
    setIn "planIdentity" identity plan

/// Maps one obligation (by baseline id) of a plan.
let private mapObligation (oid: string) (f: Json -> Json) (plan: Json) =
    let mapO (o: Json) = if str "sourceObligation" o = oid then f o else o
    setIn "requirements" (JArr(arr "requirements" plan |> List.map (fun e -> setIn "obligations" (JArr(arr "obligations" e |> List.map mapO)) e))) plan

/// The v3 plan inputs from committed files only: the r6 baseline, the v3 curation and manifest, and the approvals
/// file read at the commit the curation pins.
let inputs (source: string) : VerificationPlan.Inputs * byte[] =
    let spec = parseFile (Path.Combine(v3 source, "curation", "verification-plan.json"))
    let categoriesPath = Path.Combine(v3 source, "curation", "evidence-categories.json")
    let a = field "approvals" spec |> Option.defaultValue JNull
    let approvals =
        match Git.showFile source (str "commit" a) (str "path" a) with
        | Ok b -> b
        | Error e -> failwithf "v3 approvals unreadable: %s" e
    let baselineBytes = File.ReadAllBytes(Path.Combine(source, "requirements", "causa-requirements-baseline-r6.json"))
    let baseline = parseBytes baselineBytes
    let src = field "source" baseline |> Option.defaultValue JNull
    { Repository = str "repository" src
      Commit = str "commit" src
      BaselineId = str "id" baseline
      BaselineSha256 = Git.sha256Hex baselineBytes
      Baseline = baseline
      Manifest = parseFile (Path.Combine(v3 source, "verification-manifest.json"))
      Spec = spec
      Approvals =
        { Repository = "kemiller2002/indy-init"
          Commit = str "commit" a
          Path = str "path" a
          Sha256 = Git.sha256Hex approvals
          TrustedOperators = strs "trustedOperators" a
          Document = parseBytes approvals }
      Categories = { Path = "curation/evidence-categories.json"; Sha256 = Git.sha256Hex (File.ReadAllBytes categoriesPath); Table = parseFile categoriesPath } },
    approvals

let private withSpec (i: VerificationPlan.Inputs) (key: string) (value: Json) = { i with Spec = setIn key value i.Spec }

/// The committed v3 package: rebuilt byte for byte, READY with no permit, bound exactly as proposed, and v2 and the
/// frozen baseline untouched.
let package (source: string) : string list =
    let i, _ = inputs source
    let plan = VerificationPlan.build i
    let committed = File.ReadAllText(Path.Combine(v3 source, "verification-plan.json"))
    let committedPlan = parseFile (Path.Combine(v3 source, "verification-plan.json"))
    let gate = PlanReadiness.assess committedPlan (VerificationPlan.digestOf committedPlan) None
    let processProposal = parseFile (Path.Combine(v3 source, "proposals", "process-bindings.proposed.json"))
    let r6Proposal = parseFile (Path.Combine(v3 source, "proposals", "r6-test-bindings.proposed.json"))
    let processOnly = VerificationPlan.build (withSpec i "testBindings" (JArr(arr "testBindings" processProposal)))
    let v2Plan = parseFile (Path.Combine(source, "requirements-compiled", "v2", "verification-plan.json"))
    let frozenBaseline = File.ReadAllBytes(Path.Combine(source, "requirements", "causa-requirements-baseline.json"))
    let scope k = field "scope" plan |> Option.bind (intOpt k) |> Option.defaultValue -1L
    let obligation oid = arr "requirements" plan |> List.collect (arr "obligations") |> List.find (fun o -> str "sourceObligation" o = oid)
    let baselineText oid = arr "requirements" i.Baseline |> List.collect (arr "obligations") |> List.find (fun o -> str "id" o = oid) |> str "text"
    [ if serialize plan <> committed then yield "v3: the committed verification-plan.json differs from a rebuild"
      if not (VerificationPlan.problems i).IsEmpty then yield sprintf "v3: input problems %A" (VerificationPlan.problems i)
      if not (VerificationPlan.preservationProblems i.Baseline plan).IsEmpty then yield "v3: the plan fails preservation"
      if i.Commit <> r6BaselineCommit then yield sprintf "v3: the baseline is pinned at %s, not %s" i.Commit r6BaselineCommit
      if scope "requirements" <> 115L || scope "obligations" <> 355L then yield sprintf "v3: expected 115 P0 requirements and 355 obligations, got %d and %d" (scope "requirements") (scope "obligations")
      for rid in [ "AR-027"; "AR-028"; "AR-029"; "AR-030"; "AR-031"; "AR-034"; "AR-035" ] do
          if not (arr "requirements" plan |> List.exists (fun e -> str "requirement" e = rid)) then yield sprintf "v3: P0 %s is missing from the plan" rid
      // AR-032 and AR-033 are P1 in the source and stay P1 (DEC-R6-005: no silent promotion).
      for rid in [ "AR-032"; "AR-033" ] do
          if arr "requirements" i.Baseline |> List.exists (fun r -> str "id" r = rid && strOpt "priority" r <> Some "P1") then yield sprintf "v3: %s is not P1" rid
          if arr "requirements" plan |> List.exists (fun e -> str "requirement" e = rid) then yield sprintf "v3: P1 %s was promoted into the P0 plan" rid
      if str "verdict" gate <> "READY" || (field "summary" gate |> Option.bind (intOpt "permitted")) <> Some 0L then
          yield sprintf "v3: plan-readiness is %s (%A)" (str "verdict" gate) (field "summary" gate)
      if serialize (TestBindingProposal.propose processOnly) <> serialize r6Proposal then
          yield "v3: the committed r6 proposal differs from the proposal regenerated from the process-bound plan"
      if serialize (JArr(arr "testBindings" i.Spec)) <> serialize (JArr(TestBindingProposal.applyTo (arr "testBindings" processProposal) r6Proposal)) then
          yield "v3: the curation's testBindings are not exactly the process proposal extended by the r6 proposal"
      if VerificationPlan.digestOf v2Plan <> v2PlanDigest then yield "frozen: the v2 plan changed"
      if Git.sha256Hex frozenBaseline <> frozenBaselineSha then yield "frozen: requirements/causa-requirements-baseline.json changed"
      // No agent-written approval exists (RDP-003), so nothing is amended: the two owner-requested amendments wait for
      // operator-written records, and the obligations stay as written.
      if not (arr "approvals" i.Approvals.Document).IsEmpty then yield "v3: the pinned approvals file holds approval records; only the operator may write them (RDP-003)"
      for oid in [ "UI-031-O5"; "AR-019-O3" ] do
          if str "text" (obligation oid) <> baselineText oid then yield sprintf "v3: %s text is not the baseline text" oid
          if (field "amendment" (obligation oid)).IsSome then yield sprintf "v3: %s is amended without an operator approval record" oid
      // Every decision the curation applies is a recorded owner decision.
      let recorded = arr "decisions" (parseFile (Path.Combine(source, "kickoff", "rehearsal-006", "owner-decisions.json"))) |> List.map (str "id") |> Set.ofList
      for d in arr "derivedObligations" i.Spec @ arr "obligationAmendments" i.Spec do
          if not (recorded.Contains(str "decision" d)) then yield sprintf "v3: %s cites decision %s, which owner-decisions.json does not record" (str "obligation" d) (str "decision" d)
      if not (VerificationPlan.amendmentDiagnostics plan i.Approvals).IsEmpty then yield sprintf "v3: amendments refused %A" (VerificationPlan.amendmentDiagnostics plan i.Approvals)
      if not (VerificationPlan.derivationDiagnostics plan).IsEmpty then yield sprintf "v3: derivation refused %A" (VerificationPlan.derivationDiagnostics plan)
      match field "derivation" (obligation "UI-015-O2") with
      | None -> yield "v3: UI-015-O2 is not derived"
      | Some d ->
          let residual = arr "clauses" d |> List.filter (boolOr "residual" false) |> List.map (str "clause")
          if residual
             <> [ "the three active-catalog investigations (EXT-DATA-013) load from the connected repository and replay"
                  "the five frozen v1.0.0 fixtures replay as the storage contract's test corpus"
                  "timeline ordering"
                  "legal command enforcement"
                  "offline/conflict recovery"
                  "report fidelity"
                  "startup failure" ] then
              yield sprintf "v3: UI-015-O2 residual clauses %A" residual ]

/// Amendments: only an operator change in the pinned file applies; any other is refused and never applied. The
/// approvals here are test fixtures in memory: the committed file holds none, because only the operator writes them.
let amendments (source: string) : string list =
    let i0, _ = inputs source
    let change o = JObj [ "action", JStr "amend"; "obligation_id", JStr o; "criterion", JStr "fixture criterion"; "rationale", JStr "fixture" ]
    let approval id kind (o: string) =
        JObj [ "id", JStr id; "authority", JObj [ "kind", JStr kind; "id", JStr(if kind = "operator" then "kemiller2002" else "claude") ]; "changes", JArr [ change o ] ]
    let withApprovals (aps: Json list) (x: VerificationPlan.Inputs) = { x with Approvals = { x.Approvals with Document = setIn "approvals" (JArr aps) x.Approvals.Document } }
    let amend o approval = JArr [ JObj [ "obligation", JStr o; "approval", JStr approval; "decision", JStr "DEC-R6-003" ] ]
    let amended oid (p: Json) = arr "requirements" p |> List.collect (arr "obligations") |> List.exists (fun o -> str "sourceObligation" o = oid && (field "amendment" o).IsSome)
    let i = withSpec (withApprovals [ approval "APR-FIX" "operator" "UI-031-O5" ] i0) "obligationAmendments" (amend "UI-031-O5" "APR-FIX")
    let untrusted = withSpec (withApprovals [ approval "APR-FIX" "agent" "UI-031-O5" ] i0) "obligationAmendments" (amend "UI-031-O5" "APR-FIX")
    let invented = withSpec i "obligationAmendments" (amend "UI-031-O5" "APR-INVENTED")
    let otherObligation = withSpec i "obligationAmendments" (amend "UI-016-O11" "APR-FIX")
    let plan = VerificationPlan.build i
    let o = arr "requirements" plan |> List.collect (arr "obligations") |> List.find (fun o -> str "sourceObligation" o = "UI-031-O5")
    let tampered = plan |> mapObligation "UI-031-O5" (fun o -> setIn "amendment" (field "amendment" o |> Option.get |> setIn "criterion" (JStr "Never expose credentials.")) o) |> reseal
    let forged = plan |> mapObligation "UI-016-O11" (setIn "amendment" (JObj [ "approval", JStr "APR-FIX"; "decision", JStr "DEC-R6-002"; "criterion", JStr "Text contrast only."; "rationale", JStr "" ])) |> reseal
    let diagnose p = VerificationPlan.amendmentDiagnostics p i.Approvals |> List.map VerificationPlan.render
    [ if not (VerificationPlan.problems i).IsEmpty then yield sprintf "amendment: an operator amendment is refused %A" (VerificationPlan.problems i)
      if not (amended "UI-031-O5" plan) then yield "amendment: an operator amendment is not applied"
      if str "text" o <> str "text" (arr "requirements" (VerificationPlan.build i0) |> List.collect (arr "obligations") |> List.find (fun x -> str "sourceObligation" x = "UI-031-O5")) then
          yield "amendment: the amended obligation lost its verbatim text"
      if field "acceptanceContract" o |> Option.bind (field "amendment") |> Option.isNone then yield "amendment: the contract omits the amendment"
      if not (diagnose plan).IsEmpty then yield sprintf "amendment: the checker refuses an operator amendment %A" (diagnose plan)
      if not (VerificationPlan.problems invented |> hasProblem "unauthorized amendment of P0 obligation UI-031-O5") then yield "amendment: an invented approval is accepted"
      if amended "UI-031-O5" (VerificationPlan.build invented) then yield "amendment: an invented approval was applied"
      if not (VerificationPlan.problems untrusted |> hasProblem "unauthorized amendment") then yield "amendment: a non-operator approval is accepted"
      if amended "UI-031-O5" (VerificationPlan.build untrusted) then yield "amendment: a non-operator approval was applied"
      if not (VerificationPlan.problems otherObligation |> hasProblem "unauthorized amendment of P0 obligation UI-016-O11") then
          yield "amendment: an approval for one obligation amends another"
      if not (VerificationPlan.problems (withSpec i "obligationAmendments" (amend "UI-999-O1" "APR-FIX")) |> hasProblem "unknown or non-P0 obligation") then
          yield "amendment: an amendment of an unknown obligation is accepted"
      if not (diagnose tampered |> hasProblem "[UNAUTHORIZED-AMENDMENT] UI-031-O5") then yield "amendment: an edited criterion in the plan is not refused"
      if not (diagnose forged |> hasProblem "[UNAUTHORIZED-AMENDMENT] UI-016-O11") then yield "amendment: an amendment forged into the plan is not refused"
      if not (VerificationPlan.amendmentDiagnostics plan untrusted.Approvals |> List.map VerificationPlan.render |> hasProblem "UNAUTHORIZED-AMENDMENT") then
          yield "amendment: the checker accepts an amendment whose approval is not an operator's" ]

/// Derivations: clauses verbatim, nothing unaccounted for, constituents real and not derived; a plan edit is refused
/// by the checker and by the readiness gate.
let derivations (source: string) : string list =
    let i, _ = inputs source
    let spec = arr "derivedObligations" i.Spec |> List.head
    let mapClauses (f: Json list -> Json list) = withSpec i "derivedObligations" (JArr [ setIn "clauses" (JArr(f (arr "clauses" spec))) spec ])
    let mapClause (clause: string) (f: Json -> Json) = mapClauses (List.map (fun c -> if str "clause" c = clause then f c else c))
    let derived (p: Json) = arr "requirements" p |> List.collect (arr "obligations") |> List.exists (fun o -> (field "derivation" o).IsSome)
    let refused name (x: VerificationPlan.Inputs) needle =
        [ if not (VerificationPlan.problems x |> hasProblem needle) then yield sprintf "derivation %s: not refused (expected \"%s\")" name needle
          if derived (VerificationPlan.build x) then yield sprintf "derivation %s: applied although refused" name ]
    let plan = VerificationPlan.build i
    let editConstituent (o: Json) =
        let d = field "derivation" o |> Option.get
        let clauses =
            arr "clauses" d
            |> List.map (fun c ->
                if str "clause" c = "graph textual equivalent" then
                    setIn "constituents" (JArr [ JObj [ "obligation", JStr "UI-011-O5"; "obligationId", JStr "VPO-0000000000000000" ] ]) c
                else c)
        setIn "derivation" (setIn "clauses" (JArr clauses) d) o
    let dropClause (o: Json) =
        let d = field "derivation" o |> Option.get
        setIn "derivation" (setIn "clauses" (JArr(arr "clauses" d |> List.filter (fun c -> str "clause" c <> "timeline ordering"))) d) o
    let edited = plan |> mapObligation "UI-015-O2" editConstituent |> reseal
    let dropped = plan |> mapObligation "UI-015-O2" dropClause |> reseal
    let renders p = VerificationPlan.derivationDiagnostics p |> List.map VerificationPlan.render
    [ yield! refused "paraphrased clause" (mapClause "timeline ordering" (setIn "clause" (JStr "timeline order"))) "is not verbatim"
      yield! refused "omitted clause" (mapClauses (List.filter (fun c -> str "clause" c <> "report fidelity"))) "no clause accounts for: report fidelity"
      yield! refused "self constituent" (mapClause "responsive reflow" (setIn "constituents" (jstrs [ "UI-015-O2" ]))) "names the obligation itself"
      yield! refused "unknown constituent" (mapClause "responsive reflow" (setIn "constituents" (jstrs [ "UI-011-O9" ]))) "constituent UI-011-O9 is not an in-scope P0 obligation"
      yield! refused "invented connective" (withSpec i "derivedObligations" (JArr [ setIn "connectives" (jstrs [ "Verify"; "verify"; "and"; "fidelity" ]) spec |> setIn "clauses" (JArr(arr "clauses" spec |> List.filter (fun c -> str "clause" c <> "report fidelity"))) ])) "not in the fixed connective vocabulary"
      yield! refused "no decision" (withSpec i "derivedObligations" (JArr [ setIn "decision" (JStr "") spec ])) "names no decision"
      if not (renders edited |> hasProblem "[DERIVATION-INCONSISTENT] UI-015-O2") then yield "derivation: a constituent's VPO id edited in the plan is not refused"
      if not (renders dropped |> hasProblem "no clause accounts for: timeline ordering") then yield "derivation: a clause dropped from the plan is not refused"
      if str "verdict" (PlanReadiness.assess dropped (VerificationPlan.digestOf dropped) None) <> "BLOCKED" then yield "derivation: plan-readiness is READY on an inconsistent derivation" ]

/// verify-plan over a synthetic candidate of the v3 plan: a derived obligation needs its constituents, and a test a
/// review removed never counts again (R5A-001-04).
let checker (source: string) : string list =
    let i0, approvalsRaw = inputs source
    let candidateSha = String('2', 40)
    let runId = "run-r6-scope"
    let derivedId = "UI-015-O2"
    let plan0 = VerificationPlan.build i0
    let obligations = arr "requirements" plan0 |> List.collect (fun e -> arr "obligations" e |> List.map (fun o -> str "requirement" e, o))
    let constituents =
        obligations
        |> List.pick (fun (_, o) -> if str "sourceObligation" o = derivedId then field "derivation" o else None)
        |> arr "clauses"
        |> List.collect (arr "constituents")
        |> List.map (str "obligation")
        |> List.distinct
    let targets = derivedId :: constituents
    let requiredOf oid = obligations |> List.find (fun (_, o) -> str "sourceObligation" o = oid) |> snd |> field "acceptanceContract" |> Option.map (strs "requiredEvidenceCategories") |> Option.defaultValue []
    // One fixture test per required category, of the first kind the category admits (never a Praxis producer).
    let testId oid c = sprintf "fixture: %s %s" oid c
    let tests oid =
        requiredOf oid
        |> List.map (fun c -> JObj [ "id", JStr(testId oid c); "kind", JStr(EvidenceCategories.kindsFor i0.Categories.Table c |> List.head); "categories", jstrs [ c ] ])
    let i = withSpec i0 "testBindings" (JArr(targets |> List.map (fun o -> JObj [ "obligation", JStr o; "tests", JArr(tests o) ])))
    let plan = VerificationPlan.build i
    let byRequirement = obligations |> List.filter (fun (_, o) -> List.contains (str "sourceObligation" o) targets) |> List.groupBy fst
    let vpo oid = arr "requirements" plan |> List.collect (arr "obligations") |> List.find (fun o -> str "sourceObligation" o = oid) |> str "id"
    let evidence (skip: Set<string>) rid (os: (string * Json) list) =
        JObj
            [ "schema", JStr "causa.acceptance-evidence/1"
              "run_id", JStr runId
              "source_sha", JStr candidateSha
              "subject_type", JStr "requirement"
              "subject_id", JStr rid
              "status", JStr "PASS"
              "exit_code", JInt 0L
              "obligations",
              os
              |> List.map snd
              |> List.filter (fun o -> not (skip.Contains(str "sourceObligation" o)))
              |> List.map (fun o ->
                  let oid = str "sourceObligation" o
                  JObj
                      [ "id", JStr(vpo oid)
                        "verification", JStr "PASS"
                        "tests", requiredOf oid |> List.map (fun c -> JObj [ "testId", JStr(testId oid c); "outcome", JStr "passed" ]) |> JArr ])
              |> JArr ]
    let assess (skip: Set<string>) (changes: Json) (p: Json) =
        let files = byRequirement |> List.map (fun (rid, os) -> sprintf "evidence/%s.json" rid, Text.Encoding.UTF8.GetBytes(serialize (evidence skip rid os))) |> Map.ofList
        let manifest =
            JObj
                [ "schema", JStr "causa.acceptance-evidence-manifest/1"
                  "run_id", JStr runId
                  "source_sha", JStr candidateSha
                  "entries", files |> Map.toList |> List.map (fun (path, b) -> JObj [ "path", JStr path; "sha256", JStr(Git.sha256Hex b) ]) |> JArr ]
        let manifestRaw = Text.Encoding.UTF8.GetBytes(serialize manifest)
        let rows =
            arr "requirements" p
            |> List.map (fun e ->
                let rid = str "requirement" e
                match byRequirement |> List.tryFind (fst >> (=) rid) with
                | Some(_, os) ->
                    let ts = os |> List.collect (fun (_, o) -> requiredOf (str "sourceObligation" o) |> List.map (testId (str "sourceObligation" o)))
                    JObj [ "id", JStr rid; "priority", JStr "P0"; "tests", jstrs ts; "evidence_paths", jstrs [ sprintf "evidence/%s.json" rid ]; "status", JStr "PASS"; "reason", JStr "" ]
                | None -> JObj [ "id", JStr rid; "priority", JStr "P0"; "tests", JArr []; "evidence_paths", JArr []; "status", JStr "NOT_RUN"; "reason", JStr "" ])
        PlanVerification.assess
            { Plan = p
              Baseline = i.Baseline
              BaselineSha256 = i.BaselineSha256
              Ledger =
                JObj
                    [ "schema", JStr "causa.acceptance-ledger/2"
                      "run_id", JStr runId
                      "source_commit_sha", JStr candidateSha
                      "requirements_baseline", JObj [ "id", JStr i.BaselineId; "sha256", JStr i.BaselineSha256 ]
                      "scope_changes", JArr []
                      "requirements", JArr rows ]
              ManifestRaw = manifestRaw
              ExpectedManifestSha256 = Git.sha256Hex manifestRaw
              ReadEvidence = fun path -> files.TryFind path
              TrustedSourceSha = candidateSha
              ApprovalsRaw = approvalsRaw
              TrustedOperators = [ "kemiller2002" ]
              ProcessCovered = Map.empty
              ReviewTagChanges = changes }
    let resultOf oid (a: Json) = arr "requirements" a |> List.collect (arr "obligations") |> List.find (fun o -> str "sourceObligation" o = oid)
    let verification oid a = resultOf oid a |> str "verificationStatus"
    let reasons oid a = resultOf oid a |> arr "missingCoverage" |> List.map (fun m -> str "category" m, str "reason" m)
    let codes (a: Json) = arr "diagnostics" a |> List.map (str "code")
    let graph = "UI-011-O5"
    let graphTest = testId graph (requiredOf graph |> List.head)
    let removal resolution =
        JObj
            [ "schema", JStr PlanVerification.reviewTagChangesSchema
              "changes", JArr [ JObj([ "obligation", JStr graph; "test", JStr graphTest; "review", JStr "fixture review 1" ] @ resolution) ] ]
    let positive = assess Set.empty JNull plan
    let missingConstituent = assess (set [ graph ]) JNull plan
    let openRemoval = assess Set.empty (removal []) plan
    let notRebound = assess Set.empty (removal [ "resolution", JObj [ "kind", JStr "not-rebound"; "reason", JStr "the fix's tests are not bound in the frozen plan"; "review", JStr "fixture review 2" ] ]) plan
    let badRebound = assess Set.empty (removal [ "resolution", JObj [ "kind", JStr "rebound"; "tests", jstrs [ "fixture: unbound replacement" ]; "review", JStr "fixture review 2" ] ]) plan
    let tamperedPlan =
        plan
        |> mapObligation "AR-019-O3" (setIn "amendment" (JObj [ "approval", JStr "APR-R6-004"; "decision", JStr "DEC-R6-004"; "criterion", JStr "Any verdict is acceptable."; "rationale", JStr "" ]))
        |> reseal
    let tampered = assess Set.empty JNull tamperedPlan
    [ for oid in targets do
          if verification oid positive <> "verified" then yield sprintf "checker: %s is %s on the positive candidate (%A)" oid (verification oid positive) (reasons oid positive)
      if List.contains "UNAUTHORIZED-AMENDMENT" (codes positive) || List.contains "DERIVATION-INCONSISTENT" (codes positive) then yield sprintf "checker: the positive candidate has scope diagnostics %A" (codes positive)
      if verification derivedId missingConstituent <> "unverified" then yield "checker: a derived obligation is verified while a constituent is not"
      if not (List.contains (graph, "constituent-unverified") (reasons derivedId missingConstituent)) then yield sprintf "checker: the unverified constituent is not named (%A)" (reasons derivedId missingConstituent)
      if verification graph openRemoval <> "unverified" || verification derivedId openRemoval <> "unverified" then yield "checker: a test a review removed still verifies its obligation"
      if not (reasons graph openRemoval |> List.exists (snd >> (=) "unbound-after-review")) then yield sprintf "checker: an open removal is not reported unbound-after-review (%A)" (reasons graph openRemoval)
      if arr "unboundAfterReview" openRemoval |> List.map (str "resolution") <> [ "open" ] then yield "checker: the open removal is not listed"
      if verification graph notRebound <> "unverified" || not (reasons graph notRebound |> List.exists (snd >> (=) "removed-by-review")) then
          yield "checker: a removal resolved without re-binding restored the evidence or lost its reason"
      if arr "unboundAfterReview" notRebound |> List.map (str "resolution") <> [ "resolved" ] then yield "checker: a resolved removal is not listed as resolved"
      if not (List.contains "REVIEW-TAG-CHANGE" (codes badRebound)) || arr "unboundAfterReview" badRebound |> List.map (str "resolution") <> [ "open" ] then
          yield "checker: a re-binding to a test the plan does not bind is accepted"
      if str "verdict" tampered <> "INVALID" || not (List.contains "UNAUTHORIZED-AMENDMENT" (codes tampered)) then
          yield sprintf "checker: an edited amendment is not INVALID (%s %A)" (str "verdict" tampered) (codes tampered |> List.distinct) ]

let all (source: string) : string list =
    [ "package", package; "amendments", amendments; "derivations", derivations; "checker", checker ]
    |> List.collect (fun (name, test) -> try test source with ex -> [ sprintf "r6 scope %s: %s" name ex.Message ])
