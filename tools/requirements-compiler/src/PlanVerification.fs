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
      ProcessCovered: Map<string, Set<string>>
      /// Review tag changes (indy-init.review-tag-changes/1, R5A-001-04): tests a review removed from an obligation,
      /// and how each removal was resolved. JNull when none are supplied. A removal only ever withdraws evidence.
      ReviewTagChanges: Json }

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

let reviewTagChangesSchema = "indy-init.review-tag-changes/1"

/// What reviews removed from one obligation: the removed test ids, and whether every removal is resolved.
type Removal = { Tests: Set<string>; Open: bool }

/// The ledger tests the plan binds to each source obligation.
let boundByObligation (plan: Json) : Map<string, Set<string>> =
    arr "requirements" plan
    |> List.collect (arr "obligations")
    |> List.map (fun o -> str "sourceObligation" o, ledgerTests o |> List.map (fun t -> t.Id) |> Set.ofList)
    |> Map.ofList

/// Whether one review tag change (an entry of `entries`) is resolved: `rebound` to tests `bound` binds to the same
/// obligation that no review removed, or `not-rebound` with a stated reason, each naming the review. Shared with the
/// assertion ratchet, which honours a removed assertion only under a resolved tag change.
let tagChangeResolved (bound: Map<string, Set<string>>) (entries: Json list) (c: Json) =
    let r = field "resolution" c |> Option.defaultValue JNull
    let oid = ledgerString "obligation" c
    let removedOf oid = entries |> List.filter (fun c -> ledgerString "obligation" c = oid) |> List.map (ledgerString "test") |> Set.ofList
    match strOpt "kind" r with
    | Some "rebound" ->
        let tests = strs "tests" r
        not tests.IsEmpty
        && ledgerString "review" r <> ""
        && tests |> List.forall (fun t -> (bound.TryFind oid |> Option.defaultValue Set.empty).Contains t && not ((removedOf oid).Contains t))
    | Some "not-rebound" -> ledgerString "reason" r <> "" && ledgerString "review" r <> ""
    | _ -> false

/// Whether one review tag change stands with no REVIEW-TAG-CHANGE diagnostic of its own: it names the review that
/// removed the tag, and its resolution is admissible (tagChangeResolved). The assertion ratchet honours exactly these.
let tagChangeAdmissible (bound: Map<string, Set<string>>) (entries: Json list) (c: Json) =
    ledgerString "review" c <> "" && tagChangeResolved bound entries c

/// Removals per source obligation, with diagnostics for changes that cannot be honoured as written. A removal names a
/// test the plan binds to the obligation and the review that removed it. It is resolved by `rebound` (tests the plan
/// binds to the same obligation and no review removed) or `not-rebound` (a stated reason); anything else stays open.
/// A removed test never counts for that obligation again, whatever the resolution.
let reviewRemovals (plan: Json) (changes: Json) : Map<string, Removal> * Diagnostic list =
    let bound = boundByObligation plan
    let action = "record the review that removed the tag and resolve it: re-bind to tests the plan binds to the obligation, or state why not"
    let entries = arr "changes" changes
    let resolved = tagChangeResolved bound entries
    let diagnostics =
        [ if changes <> JNull && strOpt "schema" changes <> Some reviewTagChangesSchema then
              yield diag "REVIEW-TAG-CHANGE" "review-tag-changes" (sprintf "review tag changes have schema %A, expected %s" (strOpt "schema" changes) reviewTagChangesSchema) action
          for c in entries do
              let oid, test = ledgerString "obligation" c, ledgerString "test" c
              if not ((bound.TryFind oid |> Option.defaultValue Set.empty).Contains test) then
                  yield diag "REVIEW-TAG-CHANGE" oid (sprintf "a review removed test '%s' from %s, which the plan does not bind to it" test oid) action
              if ledgerString "review" c = "" then yield diag "REVIEW-TAG-CHANGE" oid (sprintf "the removal of '%s' from %s names no review" test oid) action
              match field "resolution" c with
              | Some r when r <> JNull && not (resolved c) ->
                  yield diag "REVIEW-TAG-CHANGE" oid (sprintf "the resolution of the removal of '%s' from %s is not admissible, so the removal stays open" test oid) action
              | _ -> () ]
    entries
    |> List.groupBy (ledgerString "obligation")
    |> List.map (fun (oid, cs) -> oid, { Tests = cs |> List.map (ledgerString "test") |> Set.ofList; Open = cs |> List.exists (resolved >> not) })
    |> Map.ofList,
    diagnostics

/// Coverage of one obligation. `verifiedEvidence` holds only documents that passed every requirement-level check.
let coverage (processCovered: Map<string, Set<string>>) (removal: Removal option) (evidenceStatus: string) (verifiedEvidence: Json list) (o: Json) =
    let vid = str "id" o
    let required = requiredOf o
    let producers = boundTests o |> List.filter isProducer
    let removed = removal |> Option.map (fun r -> r.Tests) |> Option.defaultValue Set.empty
    let tests = ledgerTests o |> List.filter (fun t -> not (removed.Contains t.Id))
    let removedDeclared = ledgerTests o |> List.filter (fun t -> removed.Contains t.Id) |> List.collect (fun t -> t.Categories) |> Set.ofList
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
    // ... and a category is covered by ledger tests only when every bound test declaring it passed: a failed, skipped
    // or missing twin (a WebKit run beside a passing Chromium run, a verifier's run beside the implementer's) is never
    // outvoted by another test of the same category (v4 independent review RV2-2).
    let byTests =
        tests
        |> List.collect (fun t -> t.Categories)
        |> List.distinct
        |> List.filter (fun c -> tests |> List.filter (fun t -> List.contains c t.Categories) |> List.forall (fun t -> passed.Contains t.Id))
        |> Set.ofList
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
                elif not (declared.Contains c) && removedDeclared.Contains c then
                    if removal |> Option.exists (fun r -> r.Open) then "unbound-after-review" else "removed-by-review"
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

    let approvalsFile: Approvals =
        { Repository = ""; Commit = ""; Path = ""; Sha256 = sha t.ApprovalsRaw; TrustedOperators = t.TrustedOperators; Document = approvals }

    let authorized = authorizedExclusions approvalsFile
    let removals, removalDiagnostics = reviewRemovals plan t.ReviewTagChanges

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
          yield! amendmentDiagnostics plan approvalsFile
          yield! derivationDiagnostics plan
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
                    let required, covered, missing =
                        coverage t.ProcessCovered (removals.TryFind(str "sourceObligation" o)) coverageStatus (if rowStatus = "PASS" then verifiedEvidence else []) o
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

    // A derived obligation is verified only when every constituent is verified too. Constituents are never derived,
    // so one pass over the per-obligation results is exact.
    let verifiedObligations =
        assessed |> List.collect (fst >> fun r -> r.Obligations) |> List.filter (fun o -> o.Verification = "verified") |> List.map (fun o -> o.Source) |> Set.ofList
    let constituentsOf =
        arr "requirements" plan
        |> List.collect (arr "obligations")
        |> List.choose (fun o ->
            field "derivation" o
            |> Option.map (fun d -> str "sourceObligation" o, arr "clauses" d |> List.collect (arr "constituents") |> List.map (str "obligation") |> List.distinct))
        |> Map.ofList
    let withConstituents (r: RequirementResult) =
        let obligations =
            r.Obligations
            |> List.map (fun o ->
                match constituentsOf.TryFind o.Source |> Option.map (List.filter (verifiedObligations.Contains >> not)) with
                | Some(_ :: _ as unverified) ->
                    { o with
                        Verification = (if o.Verification = "verified" then "unverified" else o.Verification)
                        Status = (if o.Status = "PASS" then "UNVERIFIED" else o.Status)
                        Missing = o.Missing @ (unverified |> List.map (fun k -> k, "constituent-unverified")) }
                | _ -> o)
        let downgraded = r.Status = "PASS" && obligations |> List.exists (fun o -> o.Verification <> "verified" && o.Verification <> "excluded")
        { r with
            Obligations = obligations
            Status = (if downgraded then "UNVERIFIED" else r.Status)
            Reasons = (if downgraded then r.Reasons @ [ "a derived obligation has a constituent obligation that is not verified" ] else r.Reasons) }

    let results = assessed |> List.map (fst >> withConstituents)
    let requirementDiagnostics = (assessed |> List.collect snd) @ removalDiagnostics
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
          yield!
              (if t.ReviewTagChanges = JNull then []
               else
                   [ "unboundAfterReview",
                     removals
                     |> Map.toList
                     |> List.map (fun (oid, r) ->
                         JObj [ "obligation", JStr oid; "removedTests", jstrs (Set.toList r.Tests); "resolution", JStr(if r.Open then "open" else "resolved") ])
                     |> JArr ])
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
