/// Process and owner evidence for one release candidate (Echelon development speed optimization, Priority One).
///
/// Pure: every input is passed in. The evidence is the rehearsal repository's own Praxis event log
/// (`.ros/events/events.jsonl`): `work.started` and `work.checkpointed` events, which Praxis writes when work begins and
/// when a pushed commit is verified, and `work.evidence.recorded` events (`praxis work evidence`), which keep what the
/// caller asserted apart from what Praxis observed. There is no second evidence store.
///
/// Every record keeps its provenance class (tool-observed, agent-assertion, reviewer-finding, owner-decision); the
/// class never changes because a record exists. An obligation category is `verified` (the verified-acceptance class)
/// only when a binding in the frozen plan names the record kind that proves it and a record of that kind qualifies
/// for this candidate. Owner decisions qualify only with an attestation that CI observed in the trusted run; reviews
/// only from a reviewer who implemented nothing; everything only for this candidate and from a known identity.
module ProcessVerification

open System
open Json

let schemaId = "indy-init.process-evidence-assessment/1"
let rulesSchemaId = "indy-init.process-evidence-rules/1"
let attestationsSchemaId = "indy-init.owner-attestations/1"

/// The record kinds a plan binding may name as `praxis:<kind>`.
let recordKinds =
    [ "execution-start"
      "checkpoint"
      "assignment"
      "dependency-decision"
      "scope-change"
      "owner-approval"
      "independent-review"
      "release-selection"
      "test-result"
      "acceptance-reference"
      "final-disposition" ]

/// Categories this verifier decides. Application-test categories belong to verify-plan's evidence bundle.
let decidedCategories = [ "process-record"; "owner-decision"; "independent-acceptance" ]

let bindingPrefix = "praxis:"

type Inputs =
    { Plan: Json
      Events: Json list
      Executions: Map<string, Json>
      Rules: Json
      RulesSha256: string
      Attestations: Json option
      TrustedRunId: string option
      Candidate: string
      IsAncestor: string -> string -> bool }

type Record =
    { EventId: string
      Kind: string
      WorkItem: string
      Class: string
      ActorId: string
      ActorKind: string
      Execution: string option
      ExecutionRole: string option
      Commit: string option
      Candidate: string option
      Outcome: string option
      Subjects: string list
      Decision: string option
      Attestation: string option }

type Diagnostic = { Code: string; Subject: string; Detail: string }

let private d code subject detail = { Code = code; Subject = subject; Detail = detail }

let diagnosticJson (x: Diagnostic) = JObj [ "code", JStr x.Code; "subject", JStr x.Subject; "detail", JStr x.Detail ]

let private opt key j = strOpt key j |> Option.filter (String.IsNullOrWhiteSpace >> not)
let private obj key j = field key j |> Option.defaultValue (JObj [])

// ---- projection of the Praxis log ---------------------------------------------------------------------------------

let private executionOf (e: Json) =
    match opt "execution" e, opt "execution" (obj "observed" e) with
    | Some x, _ -> Some x
    | None, Some x -> Some x
    | None, None -> strs "telemetryExecutions" e |> List.tryHead

/// One event as a record, or None when it is not process evidence.
let recordOf (executions: Map<string, Json>) (e: Json) : Record option =
    let actor = obj "actor" e
    let execution = executionOf e
    let role =
        match opt "executionRole" (obj "observed" e) with
        | Some r -> Some r
        | None -> execution |> Option.bind executions.TryFind |> Option.bind (opt "role")
    let basis kind cls commit statement =
        Some
            { EventId = opt "eventId" e |> Option.defaultValue ""
              Kind = kind
              WorkItem = opt "workItem" e |> Option.defaultValue ""
              Class = cls
              ActorId = opt "id" actor |> Option.defaultValue ""
              ActorKind = opt "kind" actor |> Option.defaultValue "unknown"
              Execution = execution
              ExecutionRole = role
              Commit = commit
              Candidate = opt "candidate" statement
              Outcome = opt "outcome" statement
              Subjects = strs "subjects" statement
              Decision = opt "decision" statement
              Attestation = opt "attestation" statement }

    match opt "type" e with
    | Some "work.started" -> basis "execution-start" "tool-observed" None (JObj [])
    | Some "work.checkpointed" ->
        let checkpoint = obj "checkpoint" e
        // A checkpoint Praxis did not verify against the remote is only the agent's statement.
        // Praxis verified the checkpoint against the remote (git-remote-observation); anything else is the agent's word.
        let cls = if opt "status" (obj "verification" checkpoint) = Some "verified" then "tool-observed" else "agent-assertion"
        basis "checkpoint" cls (opt "commit" checkpoint) (JObj [])
    | Some "work.evidence.recorded" ->
        let cls = opt "class" (obj "provenance" e) |> Option.defaultValue "agent-assertion"
        // A log line claiming verified-acceptance was not written by Praxis; it is demoted, never trusted.
        let cls = if cls = "verified-acceptance" then "agent-assertion" else cls
        basis (opt "kind" e |> Option.defaultValue "") cls (opt "commit" (obj "observed" e)) (obj "statement" e)
    | _ -> None

// ---- frozen rules and attestations --------------------------------------------------------------------------------

let private owners (rules: Json) = arr "owners" rules
let private ownerActorIds (rules: Json) = owners rules |> List.collect (strs "actorIds") |> Set.ofList
let private ownerLogins (rules: Json) = owners rules |> List.choose (opt "login") |> Set.ofList
let private roster (rules: Json) = Set.unionMany [ Set.ofList (strs "agents" rules); Set.ofList (strs "automation" rules); ownerActorIds rules ]
let private reviewRoles (rules: Json) = strs "reviewRoles" rules |> Set.ofList

/// Problems with the rules or the attestations file that make the whole assessment unusable.
let inputProblems (i: Inputs) : string list =
    [ if strOpt "schema" i.Rules <> Some rulesSchemaId then yield sprintf "process rules have schema %A, expected %s" (strOpt "schema" i.Rules) rulesSchemaId
      if owners i.Rules |> List.isEmpty then yield "process rules name no owner"
      for o in owners i.Rules do
          if opt "login" o |> Option.isNone then yield "an owner in the process rules has no login"
      if (reviewRoles i.Rules).IsEmpty then yield "process rules name no review role"
      if i.Candidate.Length <> 40 || not (i.Candidate |> Seq.forall Uri.IsHexDigit) then yield "the candidate must be a full 40-character commit SHA"
      match i.Attestations with
      | Some a when strOpt "schema" a <> Some attestationsSchemaId -> yield sprintf "owner attestations have schema %A, expected %s" (strOpt "schema" a) attestationsSchemaId
      | Some _ when i.TrustedRunId.IsNone -> yield "owner attestations were supplied without the trusted CI run id that observed them"
      | _ -> () ]

/// Attestations observed by CI in the trusted run: (reference, login) of every approving review by an owner.
let private observedAttestations (i: Inputs) : Set<string * string> =
    match i.Attestations, i.TrustedRunId with
    | Some a, Some run when opt "runId" a = Some run && opt "candidate" a = Some i.Candidate ->
        arr "attestations" a
        |> List.filter (fun x -> opt "state" x = Some "APPROVED" && (ownerLogins i.Rules).Contains(opt "login" x |> Option.defaultValue ""))
        |> List.choose (fun x -> match opt "type" x, opt "reference" x, opt "login" x with
                                 | Some t, Some r, Some l -> Some(t + ":" + r, l)
                                 | _ -> None)
        |> Set.ofList
    | _ -> Set.empty

// ---- per-record qualification -------------------------------------------------------------------------------------

let private candidateKinds = set [ "independent-review"; "release-selection"; "test-result"; "acceptance-reference"; "final-disposition" ]

/// Every reason this record cannot serve as evidence for the candidate. Empty means it qualifies as a record; a
/// kind-specific rule (below) may still find it insufficient.
let recordDiagnostics (i: Inputs) (implementers: Set<string>) (attested: Set<string * string>) (r: Record) : Diagnostic list =
    let subject = sprintf "%s %s" r.Kind (if r.EventId = "" then "(no event id)" else r.EventId)
    [ if not ((roster i.Rules).Contains r.ActorId) then
          yield d "INCORRECT-AGENT-IDENTITY" subject (sprintf "actor '%s' is not an identity the frozen rules allow" r.ActorId)
      match r.Execution |> Option.bind i.Executions.TryFind with
      | Some envelope ->
          let executionActor = opt "id" (obj "actor" envelope) |> Option.defaultValue ""
          if executionActor <> "" && executionActor <> r.ActorId then
              yield d "INCORRECT-AGENT-IDENTITY" subject (sprintf "recorded by '%s' under execution %s, which belongs to '%s'" r.ActorId r.Execution.Value executionActor)
      | None when r.Kind = "execution-start" || r.Kind = "checkpoint" ->
          yield d "INCORRECT-AGENT-IDENTITY" subject "names no execution envelope present in the log; its actor cannot be checked"
      | None -> ()
      match r.Commit with
      | Some c when not (i.IsAncestor c i.Candidate) ->
          yield d "INCORRECT-COMMIT-IDENTITY" subject (sprintf "observed commit %s is not the candidate or one of its ancestors" c)
      | None when r.Kind = "checkpoint" -> yield d "INCORRECT-COMMIT-IDENTITY" subject "checkpoint names no commit"
      | _ -> ()
      if candidateKinds.Contains r.Kind then
          match r.Candidate with
          | Some c when c = i.Candidate -> ()
          | Some c when r.Kind = "independent-review" ->
              yield d "STALE-REVIEW" subject (sprintf "the review is of %s, not the candidate %s" c i.Candidate)
          | Some c -> yield d "EVIDENCE-OTHER-CANDIDATE" subject (sprintf "states something about candidate %s, not %s" c i.Candidate)
          | None -> yield d "EVIDENCE-OTHER-CANDIDATE" subject "names no candidate"
      match r.Kind with
      | "independent-review" ->
          if r.Class <> "reviewer-finding" then yield d "SELF-ACCEPTANCE" subject (sprintf "a review recorded as %s is not a reviewer finding" r.Class)
          if implementers.Contains r.ActorId then
              yield d "SELF-ACCEPTANCE" subject (sprintf "reviewer '%s' also implemented work in this run" r.ActorId)
          match r.ExecutionRole with
          | Some role when (reviewRoles i.Rules).Contains role -> ()
          | role -> yield d "SELF-ACCEPTANCE" subject (sprintf "the review ran in execution role %A, not a review role of the frozen rules" role)
          if r.Outcome <> Some "approved" then yield d "REVIEW-NOT-APPROVED" subject (sprintf "review outcome is %A" r.Outcome)
      | "owner-approval" ->
          match r.Class, r.Attestation with
          | "owner-decision", Some a when attested |> Set.exists (fst >> (=) a) ->
              if not ((ownerActorIds i.Rules).Contains r.ActorId) then
                  yield d "MISSING-APPROVAL" subject (sprintf "recorded by '%s', who is not an owner identity of the frozen rules" r.ActorId)
              if r.Outcome <> Some "approved" then yield d "MISSING-APPROVAL" subject (sprintf "owner decision outcome is %A" r.Outcome)
          | "owner-decision", Some a ->
              yield d "UNATTESTED-OWNER-DECISION" subject (sprintf "attestation %s was not observed by CI in the trusted run for this candidate" a)
          | "owner-decision", None -> yield d "UNATTESTED-OWNER-DECISION" subject "the owner decision references no attestation"
          | cls, _ -> yield d "TRANSCRIBED-DECISION" subject (sprintf "an owner approval recorded as %s is not the owner's decision" cls)
      | _ -> () ]

// ---- kind-specific satisfaction -----------------------------------------------------------------------------------

/// Whether the qualifying records of `kind` prove it for this run, with the reasons when they do not.
let private kindSatisfied (reviewRoles: Set<string>) (kind: string) (qualifying: Record list) (all: Record list) (verifiedDecisions: Set<string>) : Diagnostic list =
    let ofKind k = qualifying |> List.filter (fun r -> r.Kind = k)
    let none code detail = if (ofKind kind).IsEmpty then [ d code kind detail ] else []
    match kind with
    | "checkpoint" ->
        // Every implementing work item needs a durable checkpoint; a review that commits nothing does not.
        let started =
            all
            |> List.filter (fun r -> r.Kind = "execution-start" && not (reviewRoles.Contains(r.ExecutionRole |> Option.defaultValue "")))
            |> List.map (fun r -> r.WorkItem)
            |> List.distinct
        let checkpointed = ofKind "checkpoint" |> List.map (fun r -> r.WorkItem) |> Set.ofList
        [ yield! none "MISSING-CHECKPOINT" "no qualifying checkpoint in the run"
          for w in started do
              if not (checkpointed.Contains w) then yield d "MISSING-CHECKPOINT" w (sprintf "work item %s started but has no qualifying checkpoint" w) ]
    | "scope-change"
    | "assignment" ->
        [ if kind = "assignment" then yield! none "MISSING-ASSIGNMENT" "no qualifying assignment record"
          for r in all |> List.filter (fun r -> r.Kind = "scope-change") do
              match r.Decision with
              | Some dec when verifiedDecisions.Contains dec -> ()
              | dec -> yield d "UNAPPROVED-SCOPE-CHANGE" r.EventId (sprintf "scope change approved by %A, which is not an attested owner decision" dec) ]
    | "owner-approval" -> none "MISSING-APPROVAL" "no attested owner approval for this candidate"
    | "independent-review" -> none "MISSING-INDEPENDENT-REVIEW" "no qualifying independent review of this candidate"
    | "release-selection" -> none "MISSING-RELEASE-SELECTION" "no release selection names this candidate"
    | k -> none "MISSING-PROCESS-RECORD" (sprintf "no qualifying %s record" k)

// ---- assessment ---------------------------------------------------------------------------------------------------

let private boundTests (o: Json) =
    field "acceptanceContract" o |> Option.bind (field "testIdentity") |> Option.map (arr "tests") |> Option.defaultValue []

let private required (o: Json) =
    field "acceptanceContract" o |> Option.map (strs "requiredEvidenceCategories") |> Option.defaultValue []

let assess (i: Inputs) : Json =
    let problems = inputProblems i
    let records = i.Events |> List.choose (recordOf i.Executions)
    let implementers =
        records
        |> List.filter (fun r -> (r.Kind = "execution-start" || r.Kind = "checkpoint") && not ((reviewRoles i.Rules).Contains(r.ExecutionRole |> Option.defaultValue "")))
        |> List.map (fun r -> r.ActorId)
        |> Set.ofList
    let attested = observedAttestations i
    let assessed = records |> List.map (fun r -> r, recordDiagnostics i implementers attested r)
    let qualifying = assessed |> List.filter (snd >> List.isEmpty) |> List.map fst
    let verifiedDecisions =
        qualifying |> List.filter (fun r -> r.Kind = "owner-approval") |> List.collect (fun r -> r.Subjects @ Option.toList r.Decision) |> Set.ofList

    let categoryResult (o: Json) (category: string) =
        let bindings =
            boundTests o
            |> List.filter (fun t -> List.contains category (strs "categories" t))
            |> List.choose (fun t -> opt "id" t |> Option.filter (fun x -> x.StartsWith(bindingPrefix, StringComparison.Ordinal)))
            |> List.map (fun x -> x.Substring bindingPrefix.Length)
        let perKind =
            bindings |> List.map (fun k -> k, kindSatisfied (reviewRoles i.Rules) k qualifying records verifiedDecisions, qualifying |> List.filter (fun r -> r.Kind = k))
        // Every producer the plan binds to the category must deliver; one satisfied producer does not excuse another.
        let satisfied = perKind |> List.filter (fun (_, ds, rs) -> ds.IsEmpty && not rs.IsEmpty)
        let allSatisfied = not bindings.IsEmpty && satisfied.Length = perKind.Length
        let diagnostics =
            if bindings.IsEmpty then [ d "UNBOUND-PROCESS-EVIDENCE" category "the plan binds no praxis:<kind> record to this category" ]
            elif not allSatisfied then
                perKind
                |> List.filter (fun (_, ds, rs) -> not ds.IsEmpty || rs.IsEmpty)
                |> List.collect (fun (k, ds, _) ->
                    ds @ (assessed |> List.filter (fun (r, _) -> r.Kind = k) |> List.collect snd))
                |> List.distinct
            else []
        JObj
            [ "category", JStr category
              "status", JStr(if problems.IsEmpty && allSatisfied then "verified" else "unverified")
              "class", (if problems.IsEmpty && allSatisfied then JStr "verified-acceptance" else JNull)
              "bindings", jstrs (bindings |> List.map ((+) bindingPrefix))
              "records", satisfied |> List.collect (fun (_, _, rs) -> rs |> List.map (fun r -> r.EventId)) |> List.distinct |> jstrs
              "diagnostics", diagnostics |> List.map diagnosticJson |> JArr ]

    let obligations =
        arr "requirements" i.Plan
        |> List.collect (fun e ->
            arr "obligations" e
            |> List.choose (fun o ->
                match required o |> List.filter (fun c -> List.contains c decidedCategories) with
                | [] -> None
                | cats ->
                    // independent-acceptance is decided here only where the plan binds a Praxis review record to it.
                    let decided =
                        cats
                        |> List.filter (fun c ->
                            c <> "independent-acceptance"
                            || boundTests o |> List.exists (fun t -> opt "id" t = Some(bindingPrefix + "independent-review") && List.contains c (strs "categories" t)))
                    if decided.IsEmpty then None
                    else
                        Some(
                            JObj
                                [ "requirement", JStr(str "requirement" e)
                                  "obligation", JStr(str "sourceObligation" o)
                                  "obligationId", JStr(str "id" o)
                                  "categories", decided |> List.map (categoryResult o) |> JArr ]
                        )))

    let statuses = obligations |> List.collect (arr "categories") |> List.map (str "status")
    let verified = statuses |> List.filter ((=) "verified") |> List.length
    let identity = field "planIdentity" i.Plan |> Option.defaultValue JNull

    JObj
        [ "schema", JStr schemaId
          "plan", JObj [ "planId", jopt JStr (strOpt "planId" identity); "digest", jopt JStr (strOpt "digest" identity) ]
          "candidate", JStr i.Candidate
          "rules", JObj [ "sha256", JStr i.RulesSha256 ]
          "attestationRun", jopt JStr i.TrustedRunId
          "verdict", JStr(if not problems.IsEmpty then "INVALID" elif statuses |> List.forall ((=) "verified") then "VERIFIED" else "INCOMPLETE")
          "problems", jstrs problems
          "summary",
          JObj
              [ "records", jint records.Length
                "qualifyingRecords", jint qualifying.Length
                "recordsByClass", records |> List.countBy (fun r -> r.Class) |> List.sort |> List.map (fun (c, n) -> c, jint n) |> JObj
                "obligations", jint obligations.Length
                "categoryChecks", jint statuses.Length
                "verified", jint verified
                "unverified", jint (statuses.Length - verified) ]
          "records",
          assessed
          |> List.map (fun (r, ds) ->
              JObj
                  [ "eventId", JStr r.EventId
                    "kind", JStr r.Kind
                    "workItem", JStr r.WorkItem
                    "class", JStr r.Class
                    "actor", JStr r.ActorId
                    "qualifies", JBool ds.IsEmpty
                    "diagnostics", ds |> List.map diagnosticJson |> JArr ])
          |> JArr
          "obligations", JArr obligations ]

/// The categories `verify-plan` may treat as covered for each obligation id: verified, decided by this assessment,
/// and only when the assessment is for the same plan and candidate.
let coveredBy (plan: Json) (candidate: string) (assessment: Json) : Map<string, Set<string>> =
    let identity = field "planIdentity" plan |> Option.defaultValue JNull
    let sameDigest = field "plan" assessment |> Option.bind (strOpt "digest") = strOpt "digest" identity
    if strOpt "schema" assessment <> Some schemaId || not sameDigest || strOpt "candidate" assessment <> Some candidate || strOpt "verdict" assessment = Some "INVALID" then
        Map.empty
    else
        arr "obligations" assessment
        |> List.map (fun o ->
            str "obligationId" o,
            arr "categories" o
            |> List.filter (fun c -> strOpt "status" c = Some "verified")
            |> List.map (str "category")
            |> List.filter (fun c -> List.contains c decidedCategories)
            |> Set.ofList)
        |> Map.ofList
