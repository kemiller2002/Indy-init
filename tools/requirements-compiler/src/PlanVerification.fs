/// Phase Eight: verifies a candidate's acceptance ledger and evidence bundle against the verification plan.
///
/// Pure: every input is passed in (the evidence bundle as a read function). It complements, and never
/// replaces, tools/compute-acceptance-verdict.py, which computes the authoritative gate and ledger verdict.
/// This check adds what only the plan knows: per-obligation test identity, required evidence categories,
/// freshness policy and the plan's own exclusion record. Nothing is upgraded: a FAIL stays FAIL, absent evidence
/// stays MISSING, and a PASS whose obligations are not each verified is UNBOUND or UNVERIFIED, not PASS.
///
/// An obligation is verified only when every required category is covered by a test that is bound to that
/// obligation in the plan, declares the category, and is reported passed for that obligation's VPO id inside
/// evidence that is in the pinned manifest, digest-identical, bound to the requirement and fresh. A related
/// passing test, a requirement-level PASS, or a test bound to another obligation verifies nothing.
module PlanVerification

open System
open Json
open VerificationPlan

type Trust =
    { Plan: Json
      Baseline: Json
      BaselineSha256: string
      Ledger: Json
      ManifestRaw: byte[]
      ExpectedManifestSha256: string
      ReadEvidence: string -> byte[] option
      TrustedSourceSha: string
      ApprovalsRaw: byte[]
      TrustedOperators: string list
      /// Process and owner categories verified per VPO id by `verify-process` for this plan and candidate
      /// (ProcessVerification.coveredBy); empty when no process assessment is supplied.
      ProcessCovered: Map<string, Set<string>> }

type ObligationResult =
    { Id: string
      Source: string
      Status: string
      Verification: string
      Required: string list
      Covered: string list
      Missing: (string * string) list }

type RequirementResult =
    { Id: string
      LedgerStatus: string
      Status: string
      Policy: string
      Reasons: string list
      Obligations: ObligationResult list }

let private sha (b: byte[]) = Git.sha256Hex b

let private tryParse (b: byte[]) =
    try Some(parseBytes b) with _ -> None

let private ledgerString key (j: Json) = strOpt key j |> Option.defaultValue ""

/// Severity order for the verdict: the first status present decides.
let private verdictFor (statuses: string list) =
    if List.contains "INVALID" statuses || List.contains "STALE" statuses then "INVALID"
    elif List.contains "FAIL" statuses then "FAILED"
    elif List.contains "BLOCKED" statuses then "BLOCKED"
    elif statuses |> List.exists (fun s -> s = "NOT_RUN" || s = "MISSING" || s = "UNBOUND" || s = "UNVERIFIED") then "INCOMPLETE"
    else "ACCEPTED"

/// Evidence for one PASS claim: present in the pinned manifest, digest-identical, bound to this subject,
/// and fresh under the requirement's policy. Returns (status, reasons, evidence documents), where status is
/// PASS, MISSING, STALE or INVALID and the documents are returned only when every file passed every check.
let private assessEvidence (t: Trust) (entries: Map<string, string>) (runId: string) (rid: string) (policy: string) (paths: string list) =
    if paths.IsEmpty then "MISSING", [ "PASS claimed without evidence_paths" ], []
    else
        let one (path: string) =
            match entries.TryFind path with
            | None -> "INVALID", [ sprintf "evidence %s is absent from the pinned manifest" path ], None
            | Some expected ->
                match t.ReadEvidence path with
                | None -> "MISSING", [ sprintf "evidence %s cannot be retrieved" path ], None
                | Some raw when sha raw <> expected -> "INVALID", [ sprintf "evidence %s digest mismatch: manifest %s, actual %s" path expected (sha raw) ], None
                | Some raw ->
                    match tryParse raw with
                    | None -> "INVALID", [ sprintf "evidence %s is not JSON" path ], None
                    | Some ev ->
                        let subject =
                            [ if strOpt "schema" ev <> Some "causa.acceptance-evidence/1" then yield sprintf "evidence %s has an unsupported schema" path
                              if strOpt "subject_type" ev <> Some "requirement" || strOpt "subject_id" ev <> Some rid then
                                  yield sprintf "evidence %s is for a different subject" path
                              if strOpt "status" ev <> Some "PASS" || intOpt "exit_code" ev <> Some 0L then
                                  yield sprintf "evidence %s does not record a passing exit" path ]
                        let fresh = strOpt "source_sha" ev = Some t.TrustedSourceSha && strOpt "run_id" ev = Some runId
                        if not subject.IsEmpty then "INVALID", subject, None
                        elif fresh then "PASS", [], Some ev
                        elif policy = "FP-DETERMINISTIC-INPUT-DIGEST" && boolOr "reusable" false ev && strOpt "input_digest" ev <> None then
                            // Reuse needs a Dokimos Reusable:true attestation; the bundle format has no verified carrier for it yet.
                            "STALE", [ sprintf "evidence %s is from another run or commit; reuse is claimed but not attested by Dokimos" path ], None
                        else
                            "STALE",
                            [ sprintf
                                  "evidence %s is bound to source %s run %s, not the candidate %s run %s (%s forbids reuse)"
                                  path
                                  (ledgerString "source_sha" ev)
                                  (ledgerString "run_id" ev)
                                  t.TrustedSourceSha
                                  runId
                                  policy ],
                            None
        let results = paths |> List.map one
        let statuses = results |> List.map (fun (s, _, _) -> s)
        let worst =
            [ "INVALID"; "STALE"; "MISSING" ] |> List.tryFind (fun s -> List.contains s statuses) |> Option.defaultValue "PASS"
        worst, results |> List.collect (fun (_, r, _) -> r), (if worst = "PASS" then results |> List.choose (fun (_, _, e) -> e) else [])

let private contractOf (o: Json) = field "acceptanceContract" o |> Option.defaultValue JNull

let private boundTests (o: Json) =
    field "testIdentity" (contractOf o) |> Option.map (arr "tests" >> List.map testOf) |> Option.defaultValue []

let private requiredOf (o: Json) = strs "requiredEvidenceCategories" (contractOf o)

/// A `praxis:<record kind>` binding names a producer of process, reviewer or owner records, not a test the ledger
/// runs; it is proven by the process assessment, never by a ledger row.
let private isProducer (t: TestRef) = t.Id.StartsWith(ProcessVerification.bindingPrefix, StringComparison.Ordinal)

let private ledgerTests (o: Json) = boundTests o |> List.filter (isProducer >> not)

let private mappingAction =
    "bind the test to the obligation it fully proves in curation/verification-plan.json testBindings (with the evidence categories it provides) and recompile the plan, or remove the claim"

/// Incorrect test mappings for one requirement: ledger claims and evidence claims that the plan does not bind,
/// and bound tests the ledger leaves out. `evidence` is every parseable evidence document the row names.
let private mappingDiagnostics (rid: string) (obligations: Json list) (row: Json option) (evidence: Json list) =
    let boundBy = obligations |> List.collect (fun o -> ledgerTests o |> List.map (fun t -> t.Id, str "sourceObligation" o)) |> List.distinct
    let boundIds = boundBy |> List.map fst |> Set.ofList
    let byVpo = obligations |> List.map (fun o -> str "id" o, o) |> Map.ofList

    [ match row with
      | None -> ()
      | Some row ->
          let claimed = strs "tests" row
          for t in claimed |> List.distinct do
              if not (boundIds.Contains t) then
                  yield diag "TEST-MAPPING" rid (sprintf "ledger test '%s' claims %s, but the plan binds it to none of its obligations" t rid) mappingAction
          if ledgerString "status" row = "PASS" || not claimed.IsEmpty then
              for (t, oid) in boundBy do
                  if not (List.contains t claimed) then
                      yield
                          diag
                              "TEST-MAPPING"
                              rid
                              (sprintf "bound test '%s' (obligation %s) is absent from the ledger row of %s" t oid rid)
                              "list every bound test in the ledger row and run it, or remove the binding through the curation"
      for ev in evidence do
          for entry in arr "obligations" ev do
              let vid = ledgerString "id" entry
              match byVpo.TryFind vid with
              | None ->
                  yield
                      diag
                          "TEST-MAPPING"
                          rid
                          (sprintf "evidence claims obligation %s, which is not an obligation of %s in the plan" vid rid)
                          "report results only under the VPO ids the plan gives this requirement"
              | Some o ->
                  let bound = ledgerTests o |> List.map (fun t -> t.Id) |> Set.ofList
                  for test in arr "tests" entry do
                      let tid = ledgerString "testId" test
                      if not (bound.Contains tid) then
                          yield
                              diag
                                  "TEST-MAPPING"
                                  (str "sourceObligation" o)
                                  (sprintf "evidence test '%s' claims obligation %s (%s), which it is not bound to in the plan" tid (str "sourceObligation" o) vid)
                                  mappingAction ]

/// Coverage of one obligation. `verifiedEvidence` holds only documents that passed every requirement-level check.
let private coverage (processCovered: Map<string, Set<string>>) (evidenceStatus: string) (verifiedEvidence: Json list) (o: Json) =
    let vid = str "id" o
    let required = requiredOf o
    let producers = boundTests o |> List.filter isProducer
    let tests = ledgerTests o
    let passed =
        verifiedEvidence
        |> List.collect (arr "obligations")
        |> List.filter (fun e -> ledgerString "id" e = vid && ledgerString "verification" e = "PASS")
        |> List.collect (arr "tests")
        |> List.filter (fun t -> ledgerString "outcome" t = "passed")
        |> List.map (ledgerString "testId")
        |> Set.ofList
    let produced = producers |> List.collect (fun t -> t.Categories) |> Set.ofList
    let byProcess = processCovered.TryFind vid |> Option.defaultValue Set.empty |> Set.intersect produced
    // A category with a bound Praxis producer is covered only by the process assessment: a passing ledger test never
    // stands in for a producer that did not deliver (every bound producer must deliver).
    let byTests = tests |> List.filter (fun t -> passed.Contains t.Id) |> List.collect (fun t -> t.Categories) |> Set.ofList
    let covered = Set.union (Set.difference byTests produced) byProcess
    let declared = tests |> List.collect (fun t -> t.Categories) |> Set.ofList
    let missing =
        if required.IsEmpty then [ EvidenceCategories.unclassified, "unclassified" ]
        else
            required
            |> List.filter (covered.Contains >> not)
            |> List.map (fun c ->
                c,
                if produced.Contains c && not (declared.Contains c) then "no-process-evidence"
                elif not (declared.Contains c) then "no-bound-test"
                else
                    match evidenceStatus with
                    | "STALE" -> "evidence-not-fresh"
                    | "INVALID" -> "evidence-invalid"
                    | "FAIL"
                    | "BLOCKED"
                    | "NOT_RUN" -> "evidence-not-pass"
                    | _ -> "no-evidence")
    required, required |> List.filter covered.Contains, missing

/// Every evidence document a row names that parses, whatever its other checks say (for mapping diagnostics only).
let private claimedEvidence (t: Trust) (rid: string) (row: Json option) =
    match row with
    | None -> []
    | Some r ->
        strs "evidence_paths" r
        |> List.choose t.ReadEvidence
        |> List.choose tryParse
        |> List.filter (fun ev -> strOpt "subject_id" ev = Some rid)

let assess (t: Trust) : Json =
    let plan = t.Plan
    let ledger = t.Ledger
    let planSource = field "source" plan |> Option.defaultValue JNull
    let approvalsPin = field "approvals" planSource |> Option.defaultValue JNull
    let manifest = tryParse t.ManifestRaw |> Option.defaultValue JNull
    let runId = ledgerString "run_id" ledger
    let ledgerBaseline = field "requirements_baseline" ledger |> Option.defaultValue JNull
    let approvals = tryParse t.ApprovalsRaw |> Option.defaultValue JNull

    let authorized =
        authorizedExclusions
            { Repository = ""; Commit = ""; Path = ""; Sha256 = sha t.ApprovalsRaw; TrustedOperators = t.TrustedOperators; Document = approvals }

    let planExcluded =
        arr "requirements" plan
        |> List.choose (fun e ->
            let ex = field "exclusion" e |> Option.defaultValue JNull
            if strOpt "status" ex = Some "excluded" then Some(str "requirement" e, ledgerString "approval" ex) else None)
        |> Map.ofList

    let integrityDiag detail = diag "EVIDENCE-INTEGRITY" "candidate" detail "re-run the acceptance job and pin the inputs it actually used"

    let staleDiag detail =
        diag "STALE-EVIDENCE" "candidate" detail "re-run the acceptance job on the candidate commit; evidence from another run or commit is never reused"

    let exclusionAction =
        "obtain an authenticated operator approval {requirement_id, action: exclude} in the pinned approvals file and record it in the plan curation, or restore the ledger row"

    // Integrity of the inputs: any problem here makes the verdict INVALID.
    let integrity =
        [ yield! preservationDiagnostics t.Baseline plan
          if strOpt "baselineSha256" planSource <> Some t.BaselineSha256 then yield integrityDiag "plan is bound to a different requirements baseline"
          if strOpt "sha256" ledgerBaseline <> Some t.BaselineSha256 || strOpt "id" ledgerBaseline <> strOpt "baselineId" planSource then
              yield integrityDiag "ledger requirements baseline differs from the plan's baseline"
          if sha t.ApprovalsRaw <> ledgerString "sha256" approvalsPin then yield integrityDiag "approvals file differs from the one the plan pins"
          if sha t.ManifestRaw <> t.ExpectedManifestSha256 then yield integrityDiag "evidence manifest SHA-256 differs from the independently pinned value"
          if strOpt "schema" manifest <> Some "causa.acceptance-evidence-manifest/1" then yield integrityDiag "evidence manifest has an unsupported schema"
          if ledgerString "source_commit_sha" ledger <> t.TrustedSourceSha then yield integrityDiag "ledger source_commit_sha differs from the trusted source SHA"
          if ledgerString "run_id" manifest <> runId then yield staleDiag "evidence manifest belongs to a different run (stale)"
          if ledgerString "source_sha" manifest <> t.TrustedSourceSha then yield staleDiag "evidence manifest is bound to a different source revision (stale)"
          for c in arr "scope_changes" ledger do
              let rid, action, approval = ledgerString "requirement_id" c, ledgerString "action" c, ledgerString "approval_id" c
              if action = "exclude" then
                  if not (authorized.Contains(approval, rid)) then
                      yield
                          diag
                              "UNAUTHORIZED-EXCLUSION"
                              rid
                              (sprintf "unauthorized exclusion of %s (approval '%s' is not an authenticated operator exclusion)" rid approval)
                              exclusionAction
                  elif planExcluded.TryFind rid <> Some approval then
                      yield diag "UNAUTHORIZED-EXCLUSION" rid (sprintf "exclusion of %s is not recorded in the verification plan" rid) exclusionAction
              else
                  yield
                      diag
                          "UNAUTHORIZED-EXCLUSION"
                          rid
                          (sprintf "scope change %s %s is not planned; the plan admits only operator-approved exclusions" rid action)
                          exclusionAction ]

    let entries =
        arr "entries" manifest
        |> List.choose (fun e -> match strOpt "path" e, strOpt "sha256" e with Some p, Some h -> Some(p, h) | _ -> None)
        |> Map.ofList

    let rows = arr "requirements" ledger |> List.groupBy (ledgerString "id") |> Map.ofList

    let ledgerExcluded =
        arr "scope_changes" ledger |> List.filter (fun c -> ledgerString "action" c = "exclude") |> List.map (ledgerString "requirement_id") |> Set.ofList

    let assessed =
        arr "requirements" plan
        |> List.map (fun e ->
            let rid = str "requirement" e
            let policy = field "freshness" e |> Option.map (str "policy") |> Option.defaultValue ""
            let obligations = arr "obligations" e
            let allBound = not obligations.IsEmpty && obligations |> List.forall (fun o -> not (boundTests o).IsEmpty)
            let row = match rows.TryFind rid with Some [ r ] -> Some r | _ -> None
            let excludedByPlan = match planExcluded.TryFind rid with Some approval -> authorized.Contains(approval, rid) | None -> false
            let rowStatus = row |> Option.map (ledgerString "status") |> Option.defaultValue ""

            // Evidence is assessed for every row that names some, so coverage reflects exactly what passed every check.
            let evidenceStatus, evidenceReasons, verifiedEvidence =
                match row with
                | Some r when not (strs "evidence_paths" r).IsEmpty -> assessEvidence t entries runId rid policy (strs "evidence_paths" r)
                | Some _ when rowStatus = "PASS" -> "MISSING", [ "PASS claimed without evidence_paths" ], []
                | _ -> "MISSING", [], []

            let coverageStatus = if rowStatus = "PASS" || evidenceStatus <> "PASS" then evidenceStatus else "NOT_RUN"
            let mapping = if excludedByPlan then [] else mappingDiagnostics rid obligations row (claimedEvidence t rid row)

            let stale =
                if evidenceStatus = "STALE" && rowStatus = "PASS" then
                    evidenceReasons |> List.map (fun r -> diag "STALE-EVIDENCE" rid r "re-run the tests on the candidate commit in this run; stale evidence is never reused")
                else []

            let obligationResults =
                obligations
                |> List.map (fun o ->
                    // A row that does not claim PASS verifies nothing, whatever its evidence says.
                    let required, covered, missing = coverage t.ProcessCovered coverageStatus (if rowStatus = "PASS" then verifiedEvidence else []) o
                    let verified = not excludedByPlan && not required.IsEmpty && missing.IsEmpty && mapping.IsEmpty
                    { Id = str "id" o
                      Source = str "sourceObligation" o
                      Status = ""
                      Verification =
                        if excludedByPlan then "excluded"
                        elif required.IsEmpty then "unclassified"
                        elif verified then "verified"
                        else "unverified"
                      Required = required
                      Covered = covered
                      Missing = if excludedByPlan then [] else missing })

            let allVerified = obligationResults |> List.forall (fun o -> o.Verification = "verified")

            let ledgerStatus, status, reasons =
                match planExcluded.TryFind rid, rows.TryFind rid with
                | Some approval, _ when authorized.Contains(approval, rid) -> "-", "EXCLUDED", [ sprintf "excluded by %s" approval ]
                | _, None when ledgerExcluded.Contains rid -> "-", "INVALID", [ "excluded by the ledger without a planned, authorized exclusion" ]
                | _, None -> "-", "MISSING", [ "no ledger row for this P0 requirement" ]
                | _, Some(_ :: _ :: _) -> "-", "INVALID", [ "more than one ledger row" ]
                | _, Some [ row ] ->
                    let s = ledgerString "status" row
                    if strOpt "priority" row <> Some "P0" then s, "INVALID", [ sprintf "ledger priority %s differs from the baseline P0" (ledgerString "priority" row) ]
                    else
                        match s with
                        | "FAIL" -> s, "FAIL", [ ledgerString "reason" row ]
                        | "BLOCKED" -> s, "BLOCKED", [ ledgerString "reason" row ]
                        | "PASS" ->
                            if evidenceStatus <> "PASS" then s, evidenceStatus, evidenceReasons
                            elif not mapping.IsEmpty then s, "INVALID", mapping |> List.map (fun d -> d.Detail)
                            elif not allBound then
                                s, "UNBOUND", [ "evidence passes, but the plan has no test identity for every obligation, so the obligations are not shown to be verified" ]
                            elif not allVerified then
                                s,
                                "UNVERIFIED",
                                [ sprintf
                                      "evidence passes, but %d of %d obligations lack fresh, integrity-checked evidence for every required category from tests bound to them"
                                      (obligationResults |> List.filter (fun o -> o.Verification <> "verified") |> List.length)
                                      obligationResults.Length ]
                            else s, "PASS", []
                        | "NOT_RUN" ->
                            s,
                            "NOT_RUN",
                            [ (if (strs "evidence_paths" row).IsEmpty then "no evidence" else "evidence present but not run to PASS")
                              ledgerString "reason" row ]
                        | other -> s, "INVALID", [ sprintf "unknown ledger status '%s'" other ]
                | _, Some [] -> "-", "MISSING", []

            let obligationStatus (o: ObligationResult) =
                match status with
                | "PASS"
                | "UNBOUND"
                | "UNVERIFIED" ->
                    if o.Verification = "verified" then "PASS"
                    elif (obligations |> List.find (fun x -> str "id" x = o.Id) |> boundTests).IsEmpty then "UNBOUND"
                    else "UNVERIFIED"
                | s -> s

            { Id = rid
              LedgerStatus = ledgerStatus
              Status = status
              Policy = policy
              Reasons = reasons |> List.filter (String.IsNullOrWhiteSpace >> not)
              Obligations = obligationResults |> List.map (fun o -> { o with Status = obligationStatus o }) },
            mapping @ stale)

    let results = assessed |> List.map fst
    let requirementDiagnostics = assessed |> List.collect snd
    let diagnostics = integrity @ requirementDiagnostics
    let statuses = results |> List.map (fun r -> r.Status)
    let mappingFound = requirementDiagnostics |> List.exists (fun d -> d.Code = "TEST-MAPPING")
    let verdict = if integrity.IsEmpty && not mappingFound then verdictFor statuses else "INVALID"
    let count s = statuses |> List.filter ((=) s) |> List.length
    let allObligations = results |> List.collect (fun r -> r.Obligations)

    let countBy (f: ObligationResult -> string) =
        allObligations |> List.map f |> List.countBy id |> List.sortBy fst |> List.map (fun (s, n) -> s, jint n) |> JObj

    JObj
        [ "schema", JStr "indy-init.verification-plan-assessment/1"
          "plan",
          JObj
              [ "planId", JStr(field "planIdentity" plan |> Option.map (str "planId") |> Option.defaultValue "")
                "digest", JStr(field "planIdentity" plan |> Option.map (str "digest") |> Option.defaultValue "") ]
          "candidate", JObj [ "sourceSha", JStr t.TrustedSourceSha; "runId", JStr runId; "evidenceManifestSha256", JStr(sha t.ManifestRaw) ]
          "verdict", JStr verdict
          "accepted", JBool(verdict = "ACCEPTED")
          "integrityProblems", jstrs (integrity |> List.map render)
          "diagnostics", diagnostics |> List.map diagnosticJson |> JArr
          "summary",
          JObj
              [ "requirements", jint results.Length
                "byStatus",
                [ "PASS"; "FAIL"; "BLOCKED"; "NOT_RUN"; "MISSING"; "UNBOUND"; "UNVERIFIED"; "STALE"; "INVALID"; "EXCLUDED" ]
                |> List.map (fun s -> s, jint (count s))
                |> JObj
                "obligations", jint allObligations.Length
                "obligationsByStatus", countBy (fun o -> o.Status)
                "obligationsByVerification", countBy (fun o -> o.Verification)
                "diagnosticsByCode", diagnostics |> List.countBy (fun d -> d.Code) |> List.sortBy fst |> List.map (fun (c, n) -> c, jint n) |> JObj ]
          "requirements",
          results
          |> List.map (fun r ->
              JObj
                  [ "requirement", JStr r.Id
                    "ledgerStatus", JStr r.LedgerStatus
                    "status", JStr r.Status
                    "freshnessPolicy", JStr r.Policy
                    "reasons", jstrs r.Reasons
                    "obligations",
                    r.Obligations
                    |> List.map (fun o ->
                        JObj
                            [ "id", JStr o.Id
                              "sourceObligation", JStr o.Source
                              "status", JStr o.Status
                              "verificationStatus", JStr o.Verification
                              "requiredEvidenceCategories", jstrs o.Required
                              "coveredEvidenceCategories", jstrs o.Covered
                              "missingCoverage", o.Missing |> List.map (fun (c, why) -> JObj [ "category", JStr c; "reason", JStr why ]) |> JArr ])
                    |> JArr ])
          |> JArr ]
