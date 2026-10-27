/// Phase Eight: verifies a candidate's acceptance ledger and evidence bundle against the verification plan.
///
/// Pure: every input is passed in (the evidence bundle as a read function). It complements, and never
/// replaces, tools/compute-acceptance-verdict.py, which computes the authoritative gate and ledger verdict.
/// This check adds what only the plan knows: per-obligation test identity, freshness policy and the
/// plan's own exclusion record. Nothing is upgraded: a FAIL stays FAIL, absent evidence stays MISSING,
/// and a PASS whose obligations have no planned test identity is UNBOUND, not PASS.
module PlanVerification

open System
open System.Text
open Json

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
      TrustedOperators: string list }

type RequirementResult =
    { Id: string
      LedgerStatus: string
      Status: string
      Policy: string
      Reasons: string list
      Obligations: (string * string) list }

let private sha (b: byte[]) = Git.sha256Hex b

let private tryParse (b: byte[]) =
    try Some(parseBytes b) with _ -> None

let private ledgerString key (j: Json) = strOpt key j |> Option.defaultValue ""

/// Severity order for the verdict: the first status present decides.
let private verdictFor (statuses: string list) =
    if List.contains "INVALID" statuses || List.contains "STALE" statuses then "INVALID"
    elif List.contains "FAIL" statuses then "FAILED"
    elif List.contains "BLOCKED" statuses then "BLOCKED"
    elif statuses |> List.exists (fun s -> s = "NOT_RUN" || s = "MISSING" || s = "UNBOUND") then "INCOMPLETE"
    else "ACCEPTED"

/// Evidence for one PASS claim: present in the pinned manifest, digest-identical, bound to this subject,
/// and fresh under the requirement's policy. Returns (status, reasons) where status is PASS, MISSING,
/// STALE or INVALID.
let private assessEvidence (t: Trust) (entries: Map<string, string>) (runId: string) (rid: string) (policy: string) (paths: string list) =
    if paths.IsEmpty then "MISSING", [ "PASS claimed without evidence_paths" ]
    else
        let one (path: string) =
            match entries.TryFind path with
            | None -> "INVALID", [ sprintf "evidence %s is absent from the pinned manifest" path ]
            | Some expected ->
                match t.ReadEvidence path with
                | None -> "MISSING", [ sprintf "evidence %s cannot be retrieved" path ]
                | Some raw when sha raw <> expected -> "INVALID", [ sprintf "evidence %s digest mismatch: manifest %s, actual %s" path expected (sha raw) ]
                | Some raw ->
                    match tryParse raw with
                    | None -> "INVALID", [ sprintf "evidence %s is not JSON" path ]
                    | Some ev ->
                        let subject =
                            [ if strOpt "schema" ev <> Some "causa.acceptance-evidence/1" then yield sprintf "evidence %s has an unsupported schema" path
                              if strOpt "subject_type" ev <> Some "requirement" || strOpt "subject_id" ev <> Some rid then yield sprintf "evidence %s is for a different subject" path
                              if strOpt "status" ev <> Some "PASS" || intOpt "exit_code" ev <> Some 0L then yield sprintf "evidence %s does not record a passing exit" path ]
                        let fresh = strOpt "source_sha" ev = Some t.TrustedSourceSha && strOpt "run_id" ev = Some runId
                        if not subject.IsEmpty then "INVALID", subject
                        elif fresh then "PASS", []
                        elif policy = "FP-DETERMINISTIC-INPUT-DIGEST" && boolOr "reusable" false ev && strOpt "input_digest" ev <> None then
                            // Reuse needs a Dokimos Reusable:true attestation; the bundle format has no verified carrier for it yet.
                            "STALE", [ sprintf "evidence %s is from another run or commit; reuse is claimed but not attested by Dokimos" path ]
                        else "STALE", [ sprintf "evidence %s is bound to source %s run %s, not the candidate %s run %s (%s forbids reuse)" path (ledgerString "source_sha" ev) (ledgerString "run_id" ev) t.TrustedSourceSha runId policy ]
        let results = paths |> List.map one
        let statuses = results |> List.map fst
        let worst =
            [ "INVALID"; "STALE"; "MISSING" ] |> List.tryFind (fun s -> List.contains s statuses) |> Option.defaultValue "PASS"
        worst, results |> List.collect snd

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
        VerificationPlan.authorizedExclusions
            { Repository = ""; Commit = ""; Path = ""; Sha256 = sha t.ApprovalsRaw; TrustedOperators = t.TrustedOperators; Document = approvals }

    let planExcluded =
        arr "requirements" plan
        |> List.choose (fun e ->
            let ex = field "exclusion" e |> Option.defaultValue JNull
            if strOpt "status" ex = Some "excluded" then Some(str "requirement" e, ledgerString "approval" ex) else None)
        |> Map.ofList

    // Integrity of the inputs: any problem here makes the verdict INVALID.
    let integrity =
        [ yield! VerificationPlan.preservationProblems t.Baseline plan |> List.map (sprintf "plan: %s")
          if strOpt "baselineSha256" planSource <> Some t.BaselineSha256 then yield "plan is bound to a different requirements baseline"
          if strOpt "sha256" ledgerBaseline <> Some t.BaselineSha256 || strOpt "id" ledgerBaseline <> strOpt "baselineId" planSource then
              yield "ledger requirements baseline differs from the plan's baseline"
          if sha t.ApprovalsRaw <> ledgerString "sha256" approvalsPin then yield "approvals file differs from the one the plan pins"
          if sha t.ManifestRaw <> t.ExpectedManifestSha256 then yield "evidence manifest SHA-256 differs from the independently pinned value"
          if strOpt "schema" manifest <> Some "causa.acceptance-evidence-manifest/1" then yield "evidence manifest has an unsupported schema"
          if ledgerString "source_commit_sha" ledger <> t.TrustedSourceSha then yield "ledger source_commit_sha differs from the trusted source SHA"
          if ledgerString "run_id" manifest <> runId then yield "evidence manifest belongs to a different run (stale)"
          if ledgerString "source_sha" manifest <> t.TrustedSourceSha then yield "evidence manifest is bound to a different source revision (stale)"
          for c in arr "scope_changes" ledger do
              let rid, action, approval = ledgerString "requirement_id" c, ledgerString "action" c, ledgerString "approval_id" c
              if action = "exclude" then
                  if not (authorized.Contains(approval, rid)) then yield sprintf "unauthorized exclusion of %s (approval '%s' is not an authenticated operator exclusion)" rid approval
                  elif planExcluded.TryFind rid <> Some approval then yield sprintf "exclusion of %s is not recorded in the verification plan" rid
              else yield sprintf "scope change %s %s is not planned; the plan admits only operator-approved exclusions" rid action ]

    let entries =
        arr "entries" manifest |> List.choose (fun e -> match strOpt "path" e, strOpt "sha256" e with Some p, Some h -> Some(p, h) | _ -> None) |> Map.ofList

    let rows = arr "requirements" ledger |> List.groupBy (ledgerString "id") |> Map.ofList
    let ledgerExcluded = arr "scope_changes" ledger |> List.filter (fun c -> ledgerString "action" c = "exclude") |> List.map (ledgerString "requirement_id") |> Set.ofList

    let results =
        arr "requirements" plan
        |> List.map (fun e ->
            let rid = str "requirement" e
            let policy = field "freshness" e |> Option.map (str "policy") |> Option.defaultValue ""
            let obligations = arr "obligations" e
            let tiOf (o: Json) = field "testIdentity" o |> Option.map (str "status") |> Option.defaultValue "missing"
            let allBound = not obligations.IsEmpty && obligations |> List.forall (fun o -> tiOf o = "bound")

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
                            let es, why = assessEvidence t entries runId rid policy (strs "evidence_paths" row)
                            if es <> "PASS" then s, es, why
                            elif not allBound then s, "UNBOUND", [ "evidence passes, but the plan has no test identity for every obligation, so the obligations are not shown to be verified" ]
                            else s, "PASS", []
                        | "NOT_RUN" ->
                            s, "NOT_RUN", [ (if (strs "evidence_paths" row).IsEmpty then "no evidence" else "evidence present but not run to PASS"); ledgerString "reason" row ]
                        | other -> s, "INVALID", [ sprintf "unknown ledger status '%s'" other ]
                | _, Some [] -> "-", "MISSING", []

            let obligationStatus (o: Json) =
                match status with
                | "PASS" | "UNBOUND" -> if tiOf o = "bound" then "PASS" else "UNBOUND"
                | s -> s

            { Id = rid
              LedgerStatus = ledgerStatus
              Status = status
              Policy = policy
              Reasons = reasons |> List.filter (String.IsNullOrWhiteSpace >> not)
              Obligations = obligations |> List.map (fun o -> str "id" o, obligationStatus o) })

    let statuses = results |> List.map (fun r -> r.Status)
    let verdict = if integrity.IsEmpty then verdictFor statuses else "INVALID"
    let count s = statuses |> List.filter ((=) s) |> List.length
    let obligationStatuses = results |> List.collect (fun r -> r.Obligations |> List.map snd)

    JObj
        [ "schema", JStr "indy-init.verification-plan-assessment/1"
          "plan", JObj [ "planId", JStr(field "planIdentity" plan |> Option.map (str "planId") |> Option.defaultValue ""); "digest", JStr(field "planIdentity" plan |> Option.map (str "digest") |> Option.defaultValue "") ]
          "candidate", JObj [ "sourceSha", JStr t.TrustedSourceSha; "runId", JStr runId; "evidenceManifestSha256", JStr(sha t.ManifestRaw) ]
          "verdict", JStr verdict
          "accepted", JBool(verdict = "ACCEPTED")
          "integrityProblems", jstrs integrity
          "summary",
          JObj
              [ "requirements", jint results.Length
                "byStatus", [ "PASS"; "FAIL"; "BLOCKED"; "NOT_RUN"; "MISSING"; "UNBOUND"; "STALE"; "INVALID"; "EXCLUDED" ] |> List.map (fun s -> s, jint (count s)) |> JObj
                "obligations", jint obligationStatuses.Length
                "obligationsByStatus",
                obligationStatuses |> List.countBy id |> List.sortBy fst |> List.map (fun (s, n) -> s, jint n) |> JObj ]
          "requirements",
          results
          |> List.map (fun r ->
              JObj
                  [ "requirement", JStr r.Id
                    "ledgerStatus", JStr r.LedgerStatus
                    "status", JStr r.Status
                    "freshnessPolicy", JStr r.Policy
                    "reasons", jstrs r.Reasons
                    "obligations", r.Obligations |> List.map (fun (o, s) -> JObj [ "id", JStr o; "status", JStr s ]) |> JArr ])
          |> JArr ]
