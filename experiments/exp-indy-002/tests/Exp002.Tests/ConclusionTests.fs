/// CG-6-CONCLUSION: INV-032, INV-033.
module Exp002.Tests.ConclusionTests

open Exp002.Contracts
open Exp002.Engine
open Exp002.Engine.Persistence
open Exp002.Tests.Runner
open Exp002.Tests.StartTests
open Exp002.Tests.DomainTests

let concl s = RecordConclusion { Statement = s }

/// Two current links (R-1, R-2), then a conclusion, then R-1 reassessed (-> R-3) and a second conclusion.
let session =
    linked
    @ [ (t2, RecordHypothesis { Statement = "H2"; Explanation = "E2" })
        (t2, LinkEvidence { linkInput with HypothesisId = "H-2"; Kind = Some Contradicts })
        (t2, concl "ORM N+1 is the likely cause")
        (t2, ReviseRelationship(RelationshipId "R-1", { Kind = Some Inconclusive; Rationale = "cached" }))
        (t2, concl "Inconclusive after cache check") ]

let tests =
    [ test "UT-INV-033" "conclusion statement required, not blank, <= 10000" (fun () ->
          all
              [ Validation.validateConclusion { Statement = "" } |> fieldErrors [ "INV-033" ]
                Validation.validateConclusion { Statement = " \n\t" } |> fieldErrors [ "INV-033" ]
                Validation.validateConclusion { Statement = String.replicate 10000 "c" } |> isOk
                Validation.validateConclusion { Statement = String.replicate 10001 "c" } |> fieldErrors [ "INV-033" ]
                Validation.validateConclusion { Statement = " kept verbatim ü " } |> bind (equal " kept verbatim ü ") ])
      test "UT-INV-033" "decide refuses a blank conclusion with Invalid INV-033; no event" (fun () ->
          withInv [ start ] (fun inv -> Domain.decide (Some inv) t1 (concl "  ") |> invalidFields [ "INV-033" ]))
      test "UT-INV-032" "record conclusion before start refused with NotStarted" (fun () ->
          equal (Error NotStarted) (Domain.decide None t1 (concl "x")))
      test "UT-INV-032" "conclusion C-1 with no links has empty BasedOn; history appended; State stays Open" (fun () ->
          withInv [ start; (t1, concl "Too early to say") ] (fun inv ->
              all
                  [ equal [ { Id = ConclusionId "C-1"; Statement = "Too early to say"; BasedOn = [] } ] inv.Conclusions
                    equal Open inv.State
                    equal [ 1; 2 ] (inv.History |> List.map (fun h -> h.Sequence))
                    check (match (List.last inv.History).Event with ConclusionRecorded _ -> true | _ -> false) "last history entry is ConclusionRecorded" ]))
      test "UT-INV-032" "BasedOn snapshots Current relationships in order and is unaffected by later reassessment" (fun () ->
          withInv session (fun inv ->
              all
                  [ equal
                        [ ConclusionId "C-1", [ RelationshipId "R-1"; RelationshipId "R-2" ]
                          ConclusionId "C-2", [ RelationshipId "R-2"; RelationshipId "R-3" ] ]
                        (inv.Conclusions |> List.map (fun c -> c.Id, c.BasedOn))
                    equal (Revised(RelationshipId "R-3")) inv.Relationships.Head.Status
                    equal Open inv.State ]))
      test "UT-INV-032" "withdrawn relationships are excluded from BasedOn" (fun () ->
          withInv (linked @ [ (t2, WithdrawRelationship(RelationshipId "R-1", "wrong log")); (t2, concl "No support left") ]) (fun inv ->
              equal [ [] ] (inv.Conclusions |> List.map (fun c -> c.BasedOn))))
      test "UT-INV-032" "conclusions round-trip through encode/decode/replay and the store" (fun () ->
          match runAll session with
          | Error e -> Error(sprintf "%A" e)
          | Ok(state, events) ->
              let doc = { Id = InvestigationId uuid; Version = 0; Events = events }
              save 0 doc emptyStore
              |> Result.bind (fun (store, _) -> load (InvestigationId uuid) store)
              |> Result.mapError (sprintf "%A")
              |> Result.bind (fun loaded ->
                  all
                      [ equal (Ok doc) (decode (encode doc))
                        equal state (Domain.replay loaded.Events)
                        equal
                            (state |> Option.map (fun i -> i.Conclusions))
                            (Domain.replay loaded.Events |> Option.map (fun i -> i.Conclusions)) ]))
      test "UT-INV-032" "corrupt basedOn is reported as Corrupt" (fun () ->
          let text =
              """{"schema":"exp002.investigation/1","id":"x","version":1,"events":[{"type":"ConclusionRecorded","at":1,"conclusion":{"id":"C-1","statement":"s","basedOn":[1]}}]}"""
          match decode text with
          | Error(Corrupt _) -> Ok()
          | other -> Error(sprintf "%A" other)) ]
