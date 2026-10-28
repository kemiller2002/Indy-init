/// Process and owner evidence for one release candidate (Echelon development speed optimization, Priority One).
///
/// Pure: every input is passed in. The evidence is the rehearsal repository's own Praxis event log
/// (`.ros/events/events.jsonl`): `work.started` and `work.checkpointed` events, which Praxis writes when work begins and
/// when a pushed commit is verified, and `work.evidence.recorded` events (`praxis work evidence`), which keep what the
/// caller asserted apart from what Praxis observed. There is no second evidence store.
///
/// Praxis identities are self-reported, so nothing here trusts what a record says about itself:
/// - the plan and the rules are pinned by the caller (the frozen kickoff record), and recomputed;
/// - a CI record counts as tool-observed only when it names the CI run the caller trusts;
/// - an owner decision counts only with an attestation CI observed in that run, and only for the decisions the
///   attestation names;
/// - a reviewer must be on the frozen reviewer roster and must not have implemented anything in the run;
/// - for each owner decision and for the candidate's review, the latest record wins.
/// An obligation category is `verified` (the verified-acceptance class) only when every producer the plan binds to it
/// delivers a qualifying record for this candidate.
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
      /// The plan digest frozen at kickoff; the plan must recompute to it.
      ExpectedPlanDigest: string
      Events: Json list
      Executions: Map<string, Json>
      Rules: Json
      RulesSha256: string
      /// The rules hash frozen at kickoff.
      ExpectedRulesSha256: string
      Attestations: Json option
      TrustedRunId: string option
      Candidate: string
      IsAncestor: string -> string -> bool }

type Record =
    { Sequence: int
      EventId: string
      Kind: string
      WorkItem: string
      Repository: string option
      /// What the record claims to be (its Praxis provenance class).
      ClaimedClass: string
      /// What this verifier accepts it as: a tool-observed claim from any run but the trusted one is an assertion.
      Class: string
      ActorId: string
      Execution: string option
      ExecutionRole: string option
      RunId: string option
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
let recordOf (executions: Map<string, Json>) (trustedRun: string option) (sequence: int) (e: Json) : Record option =
    let actor = obj "actor" e
    let execution = executionOf e
    let observed = obj "observed" e
    // The role comes from the execution envelope Praxis wrote, never from the event's own copy of it.
    let role = execution |> Option.bind executions.TryFind |> Option.bind (opt "role")
    let runId = opt "runId" (obj "identity" observed)
    let basis kind claimed commit statement =
        let accepted =
            match kind, claimed with
            | ("checkpoint" | "execution-start"), c -> c
            | _, "tool-observed" when runId.IsSome && runId = trustedRun -> "tool-observed"
            | _, "tool-observed"
            | _, "verified-acceptance" -> "agent-assertion"
            | _, c -> c
        Some
            { Sequence = sequence
              EventId = opt "eventId" e |> Option.defaultValue ""
              Kind = kind
              WorkItem = opt "workItem" e |> Option.defaultValue ""
              Repository = opt "repository" e
              ClaimedClass = claimed
              Class = accepted
              ActorId = opt "id" actor |> Option.defaultValue ""
              Execution = execution
              ExecutionRole = role
              RunId = runId
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
        // Praxis verified the checkpoint against the remote (git-remote-observation); anything else is the agent's word.
        let cls = if opt "status" (obj "verification" checkpoint) = Some "verified" then "tool-observed" else "agent-assertion"
        basis "checkpoint" cls (opt "commit" checkpoint) (JObj [])
    | Some "work.evidence.recorded" ->
        basis (opt "kind" e |> Option.defaultValue "") (opt "class" (obj "provenance" e) |> Option.defaultValue "agent-assertion") (opt "commit" observed) (obj "statement" e)
    | _ -> None

// ---- frozen rules and attestations --------------------------------------------------------------------------------

let private owners (rules: Json) = arr "owners" rules
let private ownerActorIds (rules: Json) = owners rules |> List.collect (strs "actorIds") |> Set.ofList
let private ownerLogins (rules: Json) = owners rules |> List.choose (opt "login") |> Set.ofList
/// The frozen independent reviewers: each Praxis actor id with the GitHub login whose review attests its findings.
let private reviewerLogins (rules: Json) =
    arr "reviewers" rules |> List.choose (fun r -> match opt "actorId" r, opt "login" r with
                                                   | Some a, Some l -> Some(a, l)
                                                   | _ -> None) |> Map.ofList
let private reviewers (rules: Json) = reviewerLogins rules |> Map.keys |> Set.ofSeq
let private roster (rules: Json) = Set.unionMany [ Set.ofList (strs "agents" rules); Set.ofList (strs "automation" rules); ownerActorIds rules; reviewers rules ]
let private reviewRoles (rules: Json) = strs "reviewRoles" rules |> Set.ofList

let private isHex40 (s: string) = s.Length = 40 && s |> Seq.forall (fun c -> Uri.IsHexDigit c && not (Char.IsUpper c))

/// Problems that make the whole assessment unusable (verdict INVALID).
let inputProblems (i: Inputs) : string list =
    let identity = field "planIdentity" i.Plan |> Option.defaultValue JNull
    let recomputed = VerificationPlan.digestOf i.Plan
    [ if recomputed <> i.ExpectedPlanDigest then
          yield sprintf "the plan recomputes to digest %s, not the frozen %s" recomputed i.ExpectedPlanDigest
      if strOpt "digest" identity <> Some recomputed then yield "the plan's planIdentity does not match its content"
      if i.RulesSha256 <> i.ExpectedRulesSha256 then yield sprintf "the process rules hash to %s, not the frozen %s" i.RulesSha256 i.ExpectedRulesSha256
      if strOpt "schema" i.Rules <> Some rulesSchemaId then yield sprintf "process rules have schema %A, expected %s" (strOpt "schema" i.Rules) rulesSchemaId
      if owners i.Rules |> List.isEmpty then yield "process rules name no owner"
      for o in owners i.Rules do
          if opt "login" o |> Option.isNone then yield "an owner in the process rules has no login"
      if (reviewRoles i.Rules).IsEmpty then yield "process rules name no review role"
      if (reviewers i.Rules).IsEmpty then yield "process rules name no independent reviewer (reviewers: [{actorId, login}])"
      if not (Set.intersect (reviewers i.Rules) (Set.ofList (strs "agents" i.Rules))).IsEmpty then
          yield "an independent reviewer is also listed as an implementing agent"
      if not (isHex40 i.Candidate) then yield "the candidate must be a full 40-character lowercase commit SHA"
      match i.Attestations with
      | Some a when strOpt "schema" a <> Some attestationsSchemaId -> yield sprintf "owner attestations have schema %A, expected %s" (strOpt "schema" a) attestationsSchemaId
      | Some _ when i.TrustedRunId.IsNone -> yield "owner attestations were supplied without the trusted CI run id that observed them"
      | _ -> () ]

/// What CI observed, in the trusted run, that withdraws something: the latest review state of an owner or frozen
/// reviewer login on some pull request is not APPROVED. It withdraws whatever that review names, on any pull request.
type CiWithdrawal =
    { Login: string
      Decisions: Set<string>
      Commit: string option }

/// What CI observed about one approving pull-request review in the trusted run.
type ObservedAttestation =
    { Login: string
      Decisions: Set<string>
      Commit: string option }

/// The latest CI-observed review state per (login, pull request) of the owners and frozen reviewers, in the trusted run
/// for this candidate: (reference, entry).
let private latestReviews (i: Inputs) : (string * Json) list =
    let logins = Set.union (ownerLogins i.Rules) (reviewerLogins i.Rules |> Map.values |> Set.ofSeq)
    let pullRequest (reference: string) = match reference.IndexOf '@' with
                                          | n when n > 0 -> reference.Substring(0, n)
                                          | _ -> reference
    match i.Attestations, i.TrustedRunId with
    | Some a, Some run when opt "runId" a = Some run && opt "candidate" a = Some i.Candidate ->
        arr "attestations" a
        |> List.filter (fun x -> logins.Contains(opt "login" x |> Option.defaultValue ""))
        |> List.choose (fun x ->
            match opt "type" x, opt "reference" x, opt "login" x with
            | Some t, Some r, Some l -> Some((l, t + ":" + pullRequest r), (t + ":" + r, x))
            | _ -> None)
        |> List.groupBy fst
        |> List.map (fun (_, entries) -> entries |> List.map snd |> List.maxBy (fun (_, x) -> opt "submittedAt" x |> Option.defaultValue ""))
    | _ -> []

/// Every non-approving latest review CI observed: a withdrawal, whichever pull request it is on.
let private ciWithdrawals (i: Inputs) : CiWithdrawal list =
    latestReviews i
    |> List.filter (fun (_, x) -> opt "state" x <> Some "APPROVED")
    |> List.map (fun (_, x) -> { Login = str "login" x; Decisions = Set.ofList (strs "decisions" x); Commit = opt "commit" x })

/// Approving reviews CI observed in the trusted run for this candidate, by reference ("TYPE:REF"). Only an owner's or
/// a frozen reviewer's login counts, and only that login's **latest** review state on that pull request: a later
/// CHANGES_REQUESTED or DISMISSED review withdraws the earlier approval, so a log record citing it no longer counts.
let private observedAttestations (i: Inputs) : Map<string, ObservedAttestation> =
    latestReviews i
    |> List.filter (fun (_, x) -> opt "state" x = Some "APPROVED")
    |> List.map (fun (reference, x) -> reference, { Login = str "login" x; Decisions = Set.ofList (strs "decisions" x); Commit = opt "commit" x })
    |> Map.ofList

// ---- per-record qualification -------------------------------------------------------------------------------------

let private candidateKinds = set [ "independent-review"; "release-selection"; "test-result"; "acceptance-reference"; "final-disposition" ]

/// What an owner decision record decides: its subjects and its decision id.
let private decided (r: Record) = r.Subjects @ Option.toList r.Decision |> Set.ofList

/// Every reason this record cannot serve as evidence for the candidate. Empty means it qualifies as a record; a
/// kind-specific rule (below) may still find it insufficient.
let recordDiagnostics (i: Inputs) (implementers: Set<string>) (attested: Map<string, ObservedAttestation>) (r: Record) : Diagnostic list =
    let subject = sprintf "%s %s" r.Kind (if r.EventId = "" then "(no event id)" else r.EventId)
    [ match opt "repository" i.Rules, r.Repository with
      | Some expected, actual when actual <> Some expected ->
          yield d "INCORRECT-REPOSITORY" subject (sprintf "recorded in repository %A, not %s" actual expected)
      | _ -> ()
      if not ((roster i.Rules).Contains r.ActorId) then
          yield d "INCORRECT-AGENT-IDENTITY" subject (sprintf "actor '%s' is not an identity the frozen rules allow" r.ActorId)
      match r.Execution |> Option.bind i.Executions.TryFind with
      | Some envelope ->
          let executionActor = opt "id" (obj "actor" envelope) |> Option.defaultValue ""
          if executionActor <> r.ActorId then
              yield d "INCORRECT-AGENT-IDENTITY" subject (sprintf "recorded by '%s' under execution %s, which belongs to '%s'" r.ActorId r.Execution.Value executionActor)
      | None when r.Execution.IsSome -> yield d "INCORRECT-AGENT-IDENTITY" subject (sprintf "names execution %s, whose envelope is not in the log" r.Execution.Value)
      | None when r.Kind = "execution-start" || r.Kind = "checkpoint" -> yield d "INCORRECT-AGENT-IDENTITY" subject "names no execution; its actor cannot be checked"
      | None -> ()
      match r.Commit with
      | Some c when not (i.IsAncestor c i.Candidate) ->
          yield d "INCORRECT-COMMIT-IDENTITY" subject (sprintf "observed commit %s is not the candidate or one of its ancestors" c)
      | None when r.Kind <> "execution-start" -> yield d "INCORRECT-COMMIT-IDENTITY" subject "no observed commit"
      | _ -> ()
      if candidateKinds.Contains r.Kind then
          match r.Candidate with
          | Some c when c = i.Candidate -> ()
          | Some c when r.Kind = "independent-review" ->
              yield d "STALE-REVIEW" subject (sprintf "the review is of %s, not the candidate %s" c i.Candidate)
          | Some c -> yield d "EVIDENCE-OTHER-CANDIDATE" subject (sprintf "states something about candidate %s, not %s" c i.Candidate)
          | None -> yield d "EVIDENCE-OTHER-CANDIDATE" subject "names no candidate"
      match r.Kind with
      | "checkpoint" when r.Class <> "tool-observed" -> yield d "UNVERIFIED-CHECKPOINT" subject "Praxis did not verify this checkpoint against the remote"
      | "test-result" ->
          if r.Class <> "tool-observed" then
              yield d "UNTRUSTED-TEST-RESULT" subject (sprintf "a test result must be recorded by CI in the trusted run; this one is %s (run %A)" r.Class r.RunId)
          if r.Outcome <> Some "pass" then yield d "TEST-NOT-PASSED" subject (sprintf "test outcome is %A" r.Outcome)
      | "acceptance-reference" when r.Class <> "tool-observed" ->
          yield d "UNTRUSTED-ACCEPTANCE-REFERENCE" subject (sprintf "an acceptance reference must be recorded by CI in the trusted run; this one is %s (run %A)" r.Class r.RunId)
      | "independent-review" ->
          if r.Class <> "reviewer-finding" then yield d "SELF-ACCEPTANCE" subject (sprintf "a review recorded as %s is not a reviewer finding" r.Class)
          if not ((reviewers i.Rules).Contains r.ActorId) then
              yield d "SELF-ACCEPTANCE" subject (sprintf "'%s' is not an independent reviewer of the frozen rules" r.ActorId)
          if implementers.Contains r.ActorId then
              yield d "SELF-ACCEPTANCE" subject (sprintf "reviewer '%s' also implemented work in this run" r.ActorId)
          match r.ExecutionRole with
          | Some role when (reviewRoles i.Rules).Contains role -> ()
          | role -> yield d "SELF-ACCEPTANCE" subject (sprintf "the review ran in execution role %A, not a review role of the frozen rules" role)
          // A finding written in the log is the reviewer's word; the reviewer's own approving review of this exact
          // candidate commit, observed by CI in the trusted run, is what anchors it.
          match r.Attestation |> Option.bind attested.TryFind with
          | None -> yield d "UNATTESTED-REVIEW" subject (sprintf "attestation %A was not observed by CI in the trusted run for this candidate" r.Attestation)
          | Some a ->
              if (reviewerLogins i.Rules).TryFind r.ActorId <> Some a.Login then
                  yield d "SELF-ACCEPTANCE" subject (sprintf "the attesting review is by '%s', not the frozen login of reviewer '%s'" a.Login r.ActorId)
              if a.Commit <> Some i.Candidate then
                  yield d "STALE-REVIEW" subject (sprintf "the attesting review approved commit %A, not the candidate %s" a.Commit i.Candidate)
      | "final-disposition" ->
          match r.Class, r.Attestation |> Option.bind attested.TryFind with
          | "owner-decision", Some a when
              (ownerActorIds i.Rules).Contains r.ActorId
              && (ownerLogins i.Rules).Contains a.Login
              && a.Commit = Some i.Candidate
              && a.Decisions.Contains "final-disposition" -> ()
          | "owner-decision", _ ->
              yield d "UNATTESTED-DISPOSITION" subject "the disposition is not the owner's, attested by CI as the final disposition of this candidate commit"
          | cls, _ -> yield d "TRANSCRIBED-DECISION" subject (sprintf "a final disposition recorded as %s is not the owner's" cls)
      | "owner-approval" ->
          match r.Class, r.Attestation |> Option.bind attested.TryFind with
          | "owner-decision", Some a when (ownerLogins i.Rules).Contains a.Login ->
              let approves = a.Decisions
              if not ((ownerActorIds i.Rules).Contains r.ActorId) then
                  yield d "MISSING-APPROVAL" subject (sprintf "recorded by '%s', who is not an owner identity of the frozen rules" r.ActorId)
              if r.Decision.IsNone then yield d "MISSING-APPROVAL" subject "the owner decision names no decision id (--decision)"
              let unbound = Set.difference (decided r) approves
              if not unbound.IsEmpty then
                  yield
                      d
                          "UNATTESTED-OWNER-DECISION"
                          subject
                          (sprintf "the attestation does not name %s; an attestation proves only the decisions it names" (String.Join(", ", unbound)))
          | "owner-decision", _ ->
              yield d "UNATTESTED-OWNER-DECISION" subject (sprintf "attestation %A is not an owner review observed by CI in the trusted run for this candidate" r.Attestation)
          | cls, _ -> yield d "TRANSCRIBED-DECISION" subject (sprintf "an owner approval recorded as %s is not the owner's decision" cls)
      | _ -> () ]

// ---- kind-specific satisfaction -----------------------------------------------------------------------------------

/// Owner decisions that stand, as decision id → the paths and requirements it approves. An owner approval names one
/// decision id (`decision`) and what it covers (`subjects`), all of it in its attestation. A rejection, by any owner
/// record or by a non-approving review CI observed, withdraws the decisions it names wherever it sits: it can only
/// withdraw, so a replayed approval cannot outrank it, and a rejected decision needs a new id.
let private standingApprovals (i: Inputs) (qualifyingOwner: Record list) (all: Record list) : Map<string, Set<string>> =
    let owners = ownerActorIds i.Rules
    let rejected =
        Set.union
            (all
             |> List.filter (fun r -> r.Kind = "owner-approval" && r.Outcome <> Some "approved" && owners.Contains r.ActorId)
             |> List.collect (decided >> Set.toList)
             |> Set.ofList)
            (ciWithdrawals i |> List.filter (fun w -> (ownerLogins i.Rules).Contains w.Login) |> List.collect (fun w -> Set.toList w.Decisions) |> Set.ofList)
    qualifyingOwner
    |> List.filter (fun r -> r.Outcome = Some "approved")
    |> List.choose (fun r -> r.Decision |> Option.map (fun dec -> dec, Set.ofList r.Subjects))
    |> List.filter (fun (dec, _) -> not (rejected.Contains dec))
    |> List.groupBy fst
    |> List.map (fun (dec, xs) -> dec, xs |> List.map snd |> Set.unionMany)
    |> Map.ofList

/// Whether the qualifying records of `kind` prove it for this run, with the reasons when they do not.
let private kindSatisfied (i: Inputs) (obligation: string) (kind: string) (qualifying: Record list) (all: Record list) (approved: Map<string, Set<string>>) : Diagnostic list =
    let ofKind k = qualifying |> List.filter (fun r -> r.Kind = k)
    let none code detail = if (ofKind kind).IsEmpty then [ d code kind detail ] else []
    match kind with
    | "checkpoint" ->
        // Every work item an implementing agent of the frozen rules started needs a verified checkpoint, whatever role
        // its envelope claims; a frozen reviewer's review that commits nothing does not.
        let agents = Set.ofList (strs "agents" i.Rules)
        let started =
            all
            |> List.filter (fun r -> r.Kind = "execution-start" && (agents.Contains r.ActorId || not ((reviewers i.Rules).Contains r.ActorId)))
            |> List.map (fun r -> r.WorkItem)
            |> List.distinct
        let checkpointed = ofKind "checkpoint" |> List.map (fun r -> r.WorkItem) |> Set.ofList
        [ yield! none "MISSING-CHECKPOINT" "no qualifying checkpoint in the run"
          for w in started do
              if not (checkpointed.Contains w) then yield d "MISSING-CHECKPOINT" w (sprintf "work item %s started but has no qualifying checkpoint" w) ]
    | "scope-change"
    | "assignment" ->
        [ if kind = "assignment" then yield! none "MISSING-ASSIGNMENT" "no qualifying assignment record"
          // A scope change stands only when the owner's standing, attested decision names the decision id and every
          // path or requirement the change adds; one approval does not cover another change citing the same id.
          for r in all |> List.filter (fun r -> r.Kind = "scope-change") do
              match r.Decision with
              | Some dec when approved.ContainsKey dec && r.Subjects |> List.forall approved.[dec].Contains -> ()
              | dec ->
                  yield
                      d
                          "UNAPPROVED-SCOPE-CHANGE"
                          r.EventId
                          (sprintf "scope change %s citing %A is not covered by a standing attested owner decision" (String.Join(", ", r.Subjects)) dec) ]
    | "owner-approval" ->
        // An owner obligation is proven by the decisions the frozen rules name for it, not by any approval at all.
        match field "ownerDecisions" i.Rules |> Option.map (strs obligation) with
        | None
        | Some [] -> [ d "MISSING-APPROVAL" obligation (sprintf "the frozen rules name no owner decision for %s" obligation) ]
        | Some decisions ->
            [ for dec in decisions do
                  if not (approved.ContainsKey dec) then
                      yield d "MISSING-APPROVAL" obligation (sprintf "owner decision %s for %s is not a standing attested approval" dec obligation) ]
    | "independent-review" ->
        // Any rejection by a frozen reviewer of this candidate withdraws the review, wherever it sits in the log.
        let rejections =
            all |> List.filter (fun r -> r.Kind = "independent-review" && r.Outcome <> Some "approved" && (reviewers i.Rules).Contains r.ActorId && r.Candidate = Some i.Candidate)
        let reviewerLoginSet = reviewerLogins i.Rules |> Map.values |> Set.ofSeq
        [ if (ofKind kind |> List.filter (fun r -> r.Outcome = Some "approved")).IsEmpty then
              yield d "MISSING-INDEPENDENT-REVIEW" kind "no qualifying approving independent review of this candidate"
          for r in rejections do
              yield d "REVIEW-NOT-APPROVED" r.EventId (sprintf "reviewer '%s' recorded %A for this candidate" r.ActorId r.Outcome)
          for w in ciWithdrawals i do
              if reviewerLoginSet.Contains w.Login && (w.Commit.IsNone || w.Commit = Some i.Candidate) then
                  yield d "REVIEW-NOT-APPROVED" w.Login (sprintf "CI observed a non-approving review of the candidate by '%s'" w.Login) ]
    | "release-selection" ->
        // Selecting another candidate after this one was first selected supersedes it; re-selecting it later does not
        // undo that.
        let selections = all |> List.filter (fun r -> r.Kind = "release-selection" && (roster i.Rules).Contains r.ActorId)
        let first = selections |> List.filter (fun r -> r.Candidate = Some i.Candidate) |> List.sortBy (fun r -> r.Sequence) |> List.tryHead
        [ yield! none "MISSING-RELEASE-SELECTION" "no release selection names this candidate"
          match first with
          | Some f ->
              for r in selections do
                  if r.Sequence > f.Sequence && r.Candidate <> Some i.Candidate then
                      yield d "RELEASE-SUPERSEDED" r.EventId (sprintf "a later release selection chose %A" r.Candidate)
          | None -> () ]
    | "test-result" ->
        // Any failing trusted CI result for the candidate withdraws every pass.
        [ yield! none "MISSING-PROCESS-RECORD" "no qualifying test-result record"
          for r in all do
              if r.Kind = "test-result" && r.Class = "tool-observed" && r.Candidate = Some i.Candidate && r.Outcome <> Some "pass" then
                  yield d "TEST-NOT-PASSED" r.EventId (sprintf "a trusted test result is %A" r.Outcome) ]
    | "final-disposition" ->
        // Any disposition by the owner other than accepted withdraws the acceptance.
        [ if (ofKind kind |> List.filter (fun r -> r.Outcome = Some "accepted")).IsEmpty then
              yield d "MISSING-PROCESS-RECORD" kind "no qualifying accepted final disposition"
          for r in all do
              if r.Kind = "final-disposition" && r.Outcome <> Some "accepted" && (ownerActorIds i.Rules).Contains r.ActorId && r.Candidate = Some i.Candidate then
                  yield d "DISPOSITION-NOT-ACCEPTED" r.EventId (sprintf "the owner recorded %A" r.Outcome)
          for w in ciWithdrawals i do
              if (ownerLogins i.Rules).Contains w.Login && w.Decisions.Contains "final-disposition" then
                  yield d "DISPOSITION-NOT-ACCEPTED" w.Login "CI observed a non-approving owner review of the final disposition" ]
    | k -> none "MISSING-PROCESS-RECORD" (sprintf "no qualifying %s record" k)

// ---- assessment ---------------------------------------------------------------------------------------------------

let private boundTests (o: Json) =
    field "acceptanceContract" o |> Option.bind (field "testIdentity") |> Option.map (arr "tests") |> Option.defaultValue []

/// Whether a binding may provide a category: its kind is one the plan's embedded categories table allows for that
/// category, and a Praxis producer's kind fits its record kind. Compilation enforces the same; a frozen plan built
/// without that check must not get further here.
let admissible (plan: Json) (category: string) (t: Json) =
    let kinds =
        field "evidenceCategories" plan
        |> Option.map (arr "categories")
        |> Option.defaultValue []
        |> List.tryFind (fun c -> strOpt "id" c = Some category)
        |> Option.map (strs "testKinds")
        |> Option.defaultValue []
    let kind = opt "kind" t |> Option.defaultValue ""
    let id = opt "id" t |> Option.defaultValue ""
    let record = if id.StartsWith(bindingPrefix, StringComparison.Ordinal) then Some(id.Substring bindingPrefix.Length) else None
    List.contains kind kinds
    && (match kind, record with
        | "process", Some k -> List.contains k recordKinds
        | "owner", Some k -> k = "owner-approval"
        | "review", Some k -> k = "independent-review"
        | ("process" | "owner"), None -> false
        | _, Some _ -> false
        | _, None -> true)

let private required (o: Json) =
    field "acceptanceContract" o |> Option.map (strs "requiredEvidenceCategories") |> Option.defaultValue []

let assess (i: Inputs) : Json =
    let problems = inputProblems i
    let records = i.Events |> List.mapi (recordOf i.Executions i.TrustedRunId) |> List.choose id
    // The frozen implementing agents, and anyone who checkpointed commits or started work outside a review role,
    // implemented something in this run, whatever role they later chose.
    let implementers =
        records
        |> List.filter (fun r -> r.Kind = "checkpoint" || (r.Kind = "execution-start" && not ((reviewRoles i.Rules).Contains(r.ExecutionRole |> Option.defaultValue ""))))
        |> List.map (fun r -> r.ActorId)
        |> Set.ofList
        |> Set.union (Set.ofList (strs "agents" i.Rules))
    let attested = observedAttestations i
    let assessed = records |> List.map (fun r -> r, recordDiagnostics i implementers attested r)
    let qualifying = assessed |> List.filter (snd >> List.isEmpty) |> List.map fst
    // Rejections are authentic records too: the latest authentic owner record for a subject decides.
    let approved = standingApprovals i (qualifying |> List.filter (fun r -> r.Kind = "owner-approval")) records

    let categoryResult (o: Json) (category: string) =
        let obligation = str "sourceObligation" o
        let bindings =
            boundTests o
            |> List.filter (fun t -> List.contains category (strs "categories" t) && admissible i.Plan category t)
            |> List.choose (fun t -> opt "id" t |> Option.filter (fun x -> x.StartsWith(bindingPrefix, StringComparison.Ordinal)))
            |> List.map (fun x -> x.Substring bindingPrefix.Length)
        let perKind =
            bindings |> List.map (fun k -> k, kindSatisfied i obligation k qualifying records approved, qualifying |> List.filter (fun r -> r.Kind = k))
        // Every producer the plan binds to the category must deliver; one satisfied producer does not excuse another.
        let satisfied = perKind |> List.filter (fun (_, ds, rs) -> ds.IsEmpty && not rs.IsEmpty)
        let allSatisfied = not bindings.IsEmpty && satisfied.Length = perKind.Length
        let diagnostics =
            if bindings.IsEmpty then [ d "UNBOUND-PROCESS-EVIDENCE" category "the plan binds no praxis:<kind> record to this category" ]
            elif not allSatisfied then
                perKind
                |> List.filter (fun (_, ds, rs) -> not ds.IsEmpty || rs.IsEmpty)
                |> List.collect (fun (k, ds, _) -> ds @ (assessed |> List.filter (fun (r, _) -> r.Kind = k) |> List.collect snd))
                |> List.distinct
            else []
        let verified = problems.IsEmpty && allSatisfied
        JObj
            [ "category", JStr category
              "status", JStr(if verified then "verified" else "unverified")
              "class", (if verified then JStr "verified-acceptance" else JNull)
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
          "plan", JObj [ "planId", jopt JStr (strOpt "planId" identity); "digest", JStr(VerificationPlan.digestOf i.Plan) ]
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
                    "claimedClass", JStr r.ClaimedClass
                    "class", JStr r.Class
                    "actor", JStr r.ActorId
                    "qualifies", JBool ds.IsEmpty
                    "diagnostics", ds |> List.map diagnosticJson |> JArr ])
          |> JArr
          "obligations", JArr obligations ]

/// The categories `verify-plan` may treat as covered for each obligation id, from an assessment `verify-plan` has
/// just computed itself for its own plan and candidate (never from a file it was handed).
let coveredBy (assessment: Json) : Map<string, Set<string>> =
    if strOpt "verdict" assessment = Some "INVALID" then Map.empty
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
