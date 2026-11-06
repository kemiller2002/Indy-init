/// CG-2-HEADER, CG-3-CONTENT, CG-4-RELATIONSHIP tests.
module Exp002.Tests.DomainTests

open Exp002.Contracts
open Exp002.Engine
open Exp002.Tests.Runner
open Exp002.Tests.StartTests

let t1 = Timestamp 1_700_000_001_000L
let t2 = Timestamp 1_700_000_002_000L

/// Runs commands in order from no investigation, folding events; returns the state and the full event log.
let runAll (commands: (Timestamp * Command) list) : Result<Investigation option * Event list, DomainError> =
    commands
    |> List.fold
        (fun acc (at, cmd) ->
            acc
            |> Result.bind (fun (state, log) ->
                Domain.decide state at cmd
                |> Result.map (fun events -> List.fold Domain.evolve state events, log @ events)))
        (Ok(None, []))

let stateOf commands =
    runAll commands
    |> Result.mapError (sprintf "%A")
    |> Result.bind (fun (s, _) -> match s with Some i -> Ok i | None -> Error "no investigation")

let withInv commands (f: Investigation -> Result<unit, string>) =
    match stateOf commands with
    | Ok inv -> f inv
    | Error e -> Error e

let start = (t0, StartInvestigation startInput)
let obsInput: ObservationInput = { ObservationText = "p99 went from 200ms to 450ms"; SourceReference = "Grafana board 12" }
let hypInput: HypothesisInput = { Statement = "New ORM version adds N+1 queries"; Explanation = "Latency scales with cart size" }

let evInput: EvidenceInput =
    { Label = "Query log excerpt"
      OriginalContent = "SELECT * FROM items WHERE cart_id = ... (x37)"
      SourceReference = "db-replica-2 slow log"
      SourceUrl = "https://logs.example.com/q/123" }

let linkInput: RelationshipInput =
    { EvidenceId = "E-1"
      HypothesisId = "H-1"
      Kind = Some Supports
      Rationale = "37 identical queries per checkout" }

let linked =
    [ start
      (t1, RecordHypothesis hypInput)
      (t1, AdmitEvidence evInput)
      (t2, LinkEvidence linkInput) ]

let detailsInput: InvestigationDetailsInput =
    { ProblemStatement = "Checkout p99 latency doubled"
      Title = ""
      PrimaryQuestion = "Which dependency changed?" }

let header =
    [ test "UT-INV-007" "CreatedAt is the start timestamp and is unchanged by later edits" (fun () ->
          withInv [ start; (t2, EditInvestigationDetails detailsInput) ] (fun inv -> equal t0 inv.CreatedAt))
      test "UT-INV-008" "problem statement shown from state reflects start then edit" (fun () ->
          all
              [ withInv [ start ] (fun inv -> equal startInput.ProblemStatement inv.Details.ProblemStatement)
                withInv [ start; (t1, EditInvestigationDetails detailsInput) ] (fun inv ->
                    equal "Checkout p99 latency doubled" inv.Details.ProblemStatement) ])
      test "UT-INV-018" "investigation state is Open after start and stays Open after edits" (fun () ->
          withInv [ start; (t1, EditInvestigationDetails detailsInput); (t1, RecordObservation obsInput) ] (fun inv ->
              equal Open inv.State))
      test "UT-INV-034" "edit details replaces details, keeps id, blank title -> None" (fun () ->
          withInv [ start; (t1, EditInvestigationDetails detailsInput) ] (fun inv ->
              all
                  [ equal (InvestigationId uuid) inv.Id
                    equal
                        { ProblemStatement = "Checkout p99 latency doubled"
                          Title = None
                          PrimaryQuestion = Some "Which dependency changed?" }
                        inv.Details ]))
      test "UT-INV-034" "edit details validates like start (INV-001/002/023 accumulated)" (fun () ->
          withInv [ start ] (fun inv ->
              Domain.decide
                  (Some inv)
                  t1
                  (EditInvestigationDetails
                      { ProblemStatement = " "
                        Title = String.replicate 201 "x"
                        PrimaryQuestion = String.replicate 10001 "q" })
              |> invalidFields [ "INV-001"; "INV-002"; "INV-023" ]))
      test "UT-INV-034" "edit details before start refused with NotStarted" (fun () ->
          equal (Error NotStarted) (Domain.decide None t1 (EditInvestigationDetails detailsInput)))
      test "UT-INV-035" "edits keep entity IDs and append to history with sequence numbers" (fun () ->
          let cmds =
              [ start
                (t1, RecordObservation obsInput)
                (t1, EditObservation(ObservationId "O-1", { obsInput with ObservationText = "p99 450ms, p50 unchanged" }))
                (t2, EditInvestigationDetails detailsInput) ]
          match runAll cmds with
          | Error e -> Error(sprintf "%A" e)
          | Ok(None, _) -> Error "no investigation"
          | Ok(Some inv, log) ->
              all
                  [ equal [ ObservationId "O-1" ] (inv.Observations |> List.map (fun o -> o.Id))
                    equal "p99 450ms, p50 unchanged" inv.Observations.Head.Text
                    equal [ 1; 2; 3; 4 ] (inv.History |> List.map (fun h -> h.Sequence))
                    equal log (inv.History |> List.map (fun h -> h.Event))
                    check
                        (match inv.History.[1].Event with
                         | ObservationRecorded(o, _) -> o.Text = obsInput.ObservationText
                         | _ -> false)
                        "original observation text kept in history" ])
      test "UT-INV-035" "editing an unknown entity is refused" (fun () ->
          withInv [ start ] (fun inv ->
              all
                  [ equal (Error(UnknownObservation(ObservationId "O-9"))) (Domain.decide (Some inv) t1 (EditObservation(ObservationId "O-9", obsInput)))
                    equal (Error(UnknownHypothesis(HypothesisId "H-9"))) (Domain.decide (Some inv) t1 (EditHypothesis(HypothesisId "H-9", hypInput)))
                    equal (Error(UnknownEvidence(EvidenceId "E-9"))) (Domain.decide (Some inv) t1 (EditEvidence(EvidenceId "E-9", evInput))) ])) ]

let content =
    [ test "UT-INV-014" "record observation emits ObservationRecorded with O-1, O-2" (fun () ->
          withInv [ start; (t1, RecordObservation obsInput); (t1, RecordObservation { obsInput with SourceReference = "" }) ] (fun inv ->
              equal
                  [ { Id = ObservationId "O-1"; Text = obsInput.ObservationText; SourceReference = Some "Grafana board 12" }
                    { Id = ObservationId "O-2"; Text = obsInput.ObservationText; SourceReference = None } ]
                  inv.Observations))
      test "UT-INV-014" "record observation before start refused with NotStarted" (fun () ->
          equal (Error NotStarted) (Domain.decide None t1 (RecordObservation obsInput)))
      test "UT-INV-024" "observation text required, not blank, <= 10000" (fun () ->
          all
              [ Validation.validateObservation { obsInput with ObservationText = "  " } |> fieldErrors [ "INV-024" ]
                Validation.validateObservation { obsInput with ObservationText = String.replicate 10000 "o" } |> isOk
                Validation.validateObservation { obsInput with ObservationText = String.replicate 10001 "o" } |> fieldErrors [ "INV-024" ] ])
      test "UT-INV-025" "observation source reference optional, blank -> None, <= 10000" (fun () ->
          all
              [ Validation.validateObservation { obsInput with SourceReference = " " } |> bind (fun (_, s) -> equal None s)
                Validation.validateObservation { obsInput with SourceReference = String.replicate 10001 "s" } |> fieldErrors [ "INV-025" ]
                Validation.validateObservation { ObservationText = ""; SourceReference = String.replicate 10001 "s" } |> fieldErrors [ "INV-024"; "INV-025" ] ])
      test "UT-INV-009" "record hypothesis emits HypothesisRecorded with H-1" (fun () ->
          withInv [ start ] (fun inv ->
              equal
                  (Ok [ HypothesisRecorded({ Id = HypothesisId "H-1"; Statement = hypInput.Statement; Explanation = hypInput.Explanation }, t1) ])
                  (Domain.decide (Some inv) t1 (RecordHypothesis hypInput))))
      test "UT-INV-009" "invalid hypothesis refused; no event" (fun () ->
          withInv [ start ] (fun inv -> Domain.decide (Some inv) t1 (RecordHypothesis { Statement = ""; Explanation = "" }) |> invalidFields [ "INV-026"; "INV-011" ]))
      test "UT-INV-010" "hypotheses listed in recording order; edit keeps position" (fun () ->
          withInv
              [ start
                (t1, RecordHypothesis hypInput)
                (t1, RecordHypothesis { hypInput with Statement = "Cache eviction storm" })
                (t2, EditHypothesis(HypothesisId "H-1", { hypInput with Statement = "ORM N+1 (confirmed shape)" })) ]
              (fun inv ->
                  equal
                      [ HypothesisId "H-1", "ORM N+1 (confirmed shape)"; HypothesisId "H-2", "Cache eviction storm" ]
                      (inv.Hypotheses |> List.map (fun h -> h.Id, h.Statement))))
      test "UT-INV-026" "hypothesis statement required, <= 10000" (fun () ->
          all
              [ Validation.validateHypothesis { hypInput with Statement = "\t" } |> fieldErrors [ "INV-026" ]
                Validation.validateHypothesis { hypInput with Statement = String.replicate 10000 "h" } |> isOk
                Validation.validateHypothesis { hypInput with Statement = String.replicate 10001 "h" } |> fieldErrors [ "INV-026" ] ])
      test "UT-INV-011" "hypothesis explanation required, <= 10000" (fun () ->
          all
              [ Validation.validateHypothesis { hypInput with Explanation = "" } |> fieldErrors [ "INV-011" ]
                Validation.validateHypothesis { hypInput with Explanation = String.replicate 10001 "e" } |> fieldErrors [ "INV-011" ]
                Validation.validateHypothesis hypInput |> bind (fun (_, e) -> equal hypInput.Explanation e) ])
      test "UT-INV-013" "admit evidence emits EvidenceAdmitted E-1 with all fields; evidence listed" (fun () ->
          withInv [ start; (t1, AdmitEvidence evInput); (t1, AdmitEvidence { evInput with SourceReference = ""; SourceUrl = " " }) ] (fun inv ->
              equal
                  [ { Id = EvidenceId "E-1"
                      Label = evInput.Label
                      OriginalContent = evInput.OriginalContent
                      SourceReference = Some evInput.SourceReference
                      SourceUrl = Some evInput.SourceUrl }
                    { Id = EvidenceId "E-2"
                      Label = evInput.Label
                      OriginalContent = evInput.OriginalContent
                      SourceReference = None
                      SourceUrl = None } ]
                  inv.Evidence))
      test "UT-INV-013" "admit evidence before start refused; invalid evidence refused" (fun () ->
          all
              [ equal (Error NotStarted) (Domain.decide None t1 (AdmitEvidence evInput))
                withInv [ start ] (fun inv -> Domain.decide (Some inv) t1 (AdmitEvidence { evInput with Label = "" }) |> invalidFields [ "INV-027" ]) ])
      test "UT-INV-027" "evidence label required, <= 200" (fun () ->
          all
              [ Validation.validateEvidence { evInput with Label = "  " } |> fieldErrors [ "INV-027" ]
                Validation.validateEvidence { evInput with Label = String.replicate 200 "l" } |> isOk
                Validation.validateEvidence { evInput with Label = String.replicate 201 "l" } |> fieldErrors [ "INV-027" ] ])
      test "UT-INV-012" "evidence original content required, <= 10000, kept verbatim" (fun () ->
          all
              [ Validation.validateEvidence { evInput with OriginalContent = "" } |> fieldErrors [ "INV-012" ]
                Validation.validateEvidence { evInput with OriginalContent = String.replicate 10001 "c" } |> fieldErrors [ "INV-012" ]
                Validation.validateEvidence { evInput with OriginalContent = "  raw\n\tcontent  " } |> bind (fun e -> equal "  raw\n\tcontent  " e.OriginalContent) ])
      test "UT-INV-028" "evidence source reference optional, <= 10000" (fun () ->
          all
              [ Validation.validateEvidence { evInput with SourceReference = "" } |> bind (fun e -> equal "" e.SourceReference)
                Validation.validateEvidence { evInput with SourceReference = String.replicate 10000 "r" } |> isOk
                Validation.validateEvidence { evInput with SourceReference = String.replicate 10001 "r" } |> fieldErrors [ "INV-028" ] ])
      test "UT-INV-029" "source URL optional; http/https accepted; bad scheme, spaces, >2000 rejected" (fun () ->
          all
              [ Validation.validateEvidence { evInput with SourceUrl = "" } |> isOk
                Validation.validateEvidence { evInput with SourceUrl = "http://a.b/c?d=e" } |> isOk
                Validation.validateEvidence { evInput with SourceUrl = "https://x.y/" + String.replicate 1988 "a" } |> isOk
                Validation.validateEvidence { evInput with SourceUrl = "https://x.y/" + String.replicate 1989 "a" } |> fieldErrors [ "INV-029" ]
                Validation.validateEvidence { evInput with SourceUrl = "ftp://x.y/z" } |> fieldErrors [ "INV-029" ]
                Validation.validateEvidence { evInput with SourceUrl = "https://x.y/a b" } |> fieldErrors [ "INV-029" ]
                Validation.validateEvidence { evInput with SourceUrl = "https://" } |> fieldErrors [ "INV-029" ]
                Validation.validateEvidence { evInput with SourceUrl = "example.com" } |> fieldErrors [ "INV-029" ] ])
      test "UT-INV-029" "all evidence field errors accumulated" (fun () ->
          Validation.validateEvidence
              { Label = ""
                OriginalContent = ""
                SourceReference = String.replicate 10001 "r"
                SourceUrl = "nope" }
          |> fieldErrors [ "INV-027"; "INV-012"; "INV-028"; "INV-029" ]) ]

let relationship =
    [ test "UT-INV-030" "link evidence records a Current relationship R-1" (fun () ->
          withInv linked (fun inv ->
              equal
                  [ { Id = RelationshipId "R-1"
                      EvidenceId = EvidenceId "E-1"
                      HypothesisId = HypothesisId "H-1"
                      Kind = Supports
                      Rationale = linkInput.Rationale
                      Status = Current } ]
                  inv.Relationships))
      test "UT-INV-030" "unknown evidence or hypothesis refused" (fun () ->
          withInv (List.take 3 linked) (fun inv ->
              all
                  [ equal (Error(UnknownEvidence(EvidenceId "E-7"))) (Domain.decide (Some inv) t2 (LinkEvidence { linkInput with EvidenceId = "E-7" }))
                    equal (Error(UnknownHypothesis(HypothesisId "H-7"))) (Domain.decide (Some inv) t2 (LinkEvidence { linkInput with HypothesisId = "H-7" })) ]))
      test "UT-INV-030" "duplicate current link for the same pair refused with DuplicateRelationship" (fun () ->
          withInv linked (fun inv ->
              equal
                  (Error(DuplicateRelationship(RelationshipId "R-1")))
                  (Domain.decide (Some inv) t2 (LinkEvidence { linkInput with Kind = Some Contradicts }))))
      test "UT-INV-030" "revise: old becomes Revised(R-2), new Current R-2 recorded; history keeps both" (fun () ->
          withInv (linked @ [ (t2, ReviseRelationship(RelationshipId "R-1", { Kind = Some Inconclusive; Rationale = "Queries are cached" })) ]) (fun inv ->
              all
                  [ equal
                        [ RelationshipId "R-1", Supports, Revised(RelationshipId "R-2")
                          RelationshipId "R-2", Inconclusive, Current ]
                        (inv.Relationships |> List.map (fun r -> r.Id, r.Kind, r.Status))
                    equal "Queries are cached" inv.Relationships.[1].Rationale
                    equal 5 inv.History.Length ]))
      test "UT-INV-030" "revise a non-current or unknown relationship refused" (fun () ->
          withInv (linked @ [ (t2, ReviseRelationship(RelationshipId "R-1", { Kind = Some Contradicts; Rationale = "r" })) ]) (fun inv ->
              all
                  [ equal (Error(RelationshipNotCurrent(RelationshipId "R-1"))) (Domain.decide (Some inv) t2 (ReviseRelationship(RelationshipId "R-1", { Kind = Some Supports; Rationale = "r" })))
                    equal (Error(UnknownRelationship(RelationshipId "R-9"))) (Domain.decide (Some inv) t2 (ReviseRelationship(RelationshipId "R-9", { Kind = Some Supports; Rationale = "r" })))
                    equal (Error(RelationshipNotCurrent(RelationshipId "R-1"))) (Domain.decide (Some inv) t2 (WithdrawRelationship(RelationshipId "R-1", "x"))) ]))
      test "UT-INV-030" "withdraw: Current -> Withdrawn(reason); pair can then be linked again" (fun () ->
          withInv (linked @ [ (t2, WithdrawRelationship(RelationshipId "R-1", "Log was from staging")); (t2, LinkEvidence { linkInput with Kind = Some ConsistentWith }) ]) (fun inv ->
              equal
                  [ RelationshipId "R-1", Withdrawn "Log was from staging"; RelationshipId "R-2", Current ]
                  (inv.Relationships |> List.map (fun r -> r.Id, r.Status))))
      test "UT-INV-031" "kind must be selected (None refused with INV-031); all four kinds accepted" (fun () ->
          all
              ([ Validation.validateRelationship { linkInput with Kind = None } |> fieldErrors [ "INV-031" ]
                 Validation.validateRevision { Kind = None; Rationale = "r" } |> fieldErrors [ "INV-031" ] ]
               @ ([ Supports; Contradicts; ConsistentWith; Inconclusive ]
                  |> List.map (fun k -> Validation.validateRelationship { linkInput with Kind = Some k } |> bind (fun (k2, _) -> equal k k2)))))
      test "UT-INV-031" "link with no kind refused by decide; no relationship" (fun () ->
          withInv (List.take 3 linked) (fun inv -> Domain.decide (Some inv) t2 (LinkEvidence { linkInput with Kind = None }) |> invalidFields [ "INV-031" ]))
      test "UT-INV-038" "rationale required, <= 10000, for link, revise and withdraw reason" (fun () ->
          withInv linked (fun inv ->
              all
                  [ Validation.validateRelationship { linkInput with Rationale = " " } |> fieldErrors [ "INV-038" ]
                    Validation.validateRelationship { linkInput with Rationale = String.replicate 10000 "r" } |> isOk
                    Validation.validateRelationship { linkInput with Rationale = String.replicate 10001 "r" } |> fieldErrors [ "INV-038" ]
                    Validation.validateRelationship { linkInput with Kind = None; Rationale = "" } |> fieldErrors [ "INV-031"; "INV-038" ]
                    Domain.decide (Some inv) t2 (ReviseRelationship(RelationshipId "R-1", { Kind = Some Supports; Rationale = "" })) |> invalidFields [ "INV-038" ]
                    Domain.decide (Some inv) t2 (WithdrawRelationship(RelationshipId "R-1", "  ")) |> invalidFields [ "INV-038" ] ]))
      test "UT-INV-044" "a link is visible from both its evidence and its hypothesis" (fun () ->
          withInv linked (fun inv ->
              let forEvidence = inv.Relationships |> List.filter (fun r -> r.EvidenceId = inv.Evidence.Head.Id)
              let forHypothesis = inv.Relationships |> List.filter (fun r -> r.HypothesisId = inv.Hypotheses.Head.Id)
              all [ equal 1 forEvidence.Length; equal forEvidence forHypothesis ])) ]
