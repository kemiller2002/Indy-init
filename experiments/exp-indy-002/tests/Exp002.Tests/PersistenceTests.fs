/// CG-5-PERSISTENCE: INV-015, INV-016, INV-017.
module Exp002.Tests.PersistenceTests

open Exp002.Contracts
open Exp002.Engine
open Exp002.Engine.Persistence
open Exp002.Tests.Runner
open Exp002.Tests.StartTests
open Exp002.Tests.DomainTests

/// A session touching every command and therefore every event case, with unicode and None/Some optionals.
let fullSession =
    linked
    @ [ (t1, RecordObservation { ObservationText = "Zeichen: ü, 日本語, emoji 🚀, \"quotes\" \\ back\nslash"; SourceReference = "" })
        (t1, RecordObservation obsInput)
        (t2, EditObservation(ObservationId "O-1", { ObservationText = "edited ✓"; SourceReference = "ref ±" }))
        (t2, EditHypothesis(HypothesisId "H-1", { Statement = "Hyp é"; Explanation = "Explains ⌚" }))
        (t2, EditEvidence(EvidenceId "E-1", { evInput with SourceUrl = ""; SourceReference = "" }))
        (t2, AdmitEvidence { evInput with Label = "Second" })
        (t2, RecordHypothesis { Statement = "H2"; Explanation = "E2" })
        (t2, LinkEvidence { linkInput with EvidenceId = "E-2"; HypothesisId = "H-2"; Kind = Some Contradicts })
        (t2, LinkEvidence { linkInput with EvidenceId = "E-1"; HypothesisId = "H-2"; Kind = Some ConsistentWith })
        (t2, ReviseRelationship(RelationshipId "R-1", { Kind = Some Inconclusive; Rationale = "revised ⇒" }))
        (t2, WithdrawRelationship(RelationshipId "R-2", "withdrawn ∅"))
        (t2, RecordConclusion { Statement = "Conclusion ✔ — ORM N+1 likely" })
        (Timestamp 9_007_199_254_740_993L, EditInvestigationDetails { ProblemStatement = "  edited  problem  "; Title = "T"; PrimaryQuestion = "" }) ]

let fullLog () =
    runAll fullSession |> Result.mapError (sprintf "%A") |> Result.map snd

let tests =
    [ test "UT-INV-017" "encode -> decode round-trips every event case and field exactly" (fun () ->
          fullLog ()
          |> Result.bind (fun events ->
              let doc = { Id = InvestigationId uuid; Version = 3; Events = events }
              let cases =
                  events |> List.map (fun e -> (Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(e, typeof<Event>) |> fst).Name) |> List.distinct
              all [ equal 12 cases.Length; equal (Ok doc) (decode (encode doc)) ]))
      test "UT-INV-017" "save -> load -> replay restores every entered value" (fun () ->
          match runAll fullSession with
          | Error e -> Error(sprintf "%A" e)
          | Ok(state, events) ->
              let doc = { Id = InvestigationId uuid; Version = 0; Events = events }
              save 0 doc emptyStore
              |> Result.bind (fun (store, v) -> load (InvestigationId uuid) store |> Result.map (fun d -> v, d))
              |> Result.mapError (sprintf "%A")
              |> Result.bind (fun (v, loaded) ->
                  let restored = Domain.replay loaded.Events
                  all
                      [ equal 1 v
                        equal 1 loaded.Version
                        equal state restored
                        restored
                        |> Option.map (fun i -> equal (Some "edited ✓") (i.Observations |> List.tryHead |> Option.map (fun o -> o.Text)))
                        |> Option.defaultValue (Error "none")
                        restored
                        |> Option.map (fun i -> equal None i.Details.PrimaryQuestion)
                        |> Option.defaultValue (Error "none") ]))
      test "UT-INV-017" "decode failures are Corrupt, never exceptions" (fun () ->
          let isCorrupt r = match r with Error(Corrupt _) -> Ok() | other -> Error(sprintf "expected Corrupt, got %A" other)
          all
              [ isCorrupt (decode "")
                isCorrupt (decode "{not json")
                isCorrupt (decode "[]")
                isCorrupt (decode "null")
                isCorrupt (decode null)
                isCorrupt (decode """{"schema":"exp002.investigation/1","id":"x","version":1}""")
                isCorrupt (decode """{"schema":"other/9","id":"x","version":1,"events":[]}""")
                isCorrupt (decode """{"schema":"exp002.investigation/1","id":"x","version":1,"events":[{"type":"Nope","at":1}]}""")
                isCorrupt (decode """{"schema":"exp002.investigation/1","id":"x","version":"1","events":[]}""") ])
      test "UT-INV-017" "load of an unknown id is NotFound; corrupt stored text is Corrupt" (fun () ->
          let id = InvestigationId uuid
          all
              [ equal (Error(NotFound id)) (load id emptyStore)
                (match load id (putRaw id "garbage" emptyStore) with
                 | Error(Corrupt _) -> Ok()
                 | other -> Error(sprintf "%A" other)) ])
      test "UT-INV-015" "save with the expected version writes version+1; stale expected version is VersionConflict" (fun () ->
          let doc = { Id = InvestigationId uuid; Version = 0; Events = [] }
          match save 0 doc emptyStore with
          | Error e -> Error(sprintf "%A" e)
          | Ok(s1, v1) ->
              all
                  [ equal 1 v1
                    (match save 1 doc s1 with Ok(_, v2) -> equal 2 v2 | Error e -> Error(sprintf "%A" e))
                    equal (Error(VersionConflict(0, 1))) (save 0 doc s1 |> Result.map snd)
                    equal (Ok 0) (currentVersion emptyStore doc.Id) ])
      test "UT-INV-015" "happy save path: Unsaved -> Saving -> (write confirmed) Saving -> (read-back) Saved(v,t)" (fun () ->
          let steps = [ SaveRequested; WriteConfirmed(4, t2); ReadBackMatched(4, t2) ]
          let states = List.scan saveTransition Unsaved steps
          equal [ Unsaved; Saving; Saving; Saved(4, t2) ] states)
      test "UT-INV-015" "Saved is never entered before the read-back" (fun () ->
          let states = List.scan saveTransition Unsaved [ SaveRequested; WriteConfirmed(4, t2) ]
          check (states |> List.forall (function Saved _ -> false | _ -> true)) (sprintf "Saved reached early: %A" states))
      test "UT-INV-015" "failure paths: conflict, rejection, unknown outcome, read-back mismatch" (fun () ->
          all
              [ equal (Conflict 7) (saveTransition Saving (WriteRejected(VersionConflict(6, 7))))
                equal (SaveFailed(describe (StorageUnavailable "quota"))) (saveTransition Saving (WriteRejected(StorageUnavailable "quota")))
                equal UnknownOutcome (saveTransition Saving WriteOutcomeUnknown)
                check
                    (match List.fold saveTransition Saving [ WriteConfirmed(4, t2); ReadBackMismatched ] with SaveFailed _ -> true | _ -> false)
                    "write confirmed then mismatched read-back -> SaveFailed"
                check (match saveTransition Saving ReadBackMismatched with SaveFailed _ -> true | _ -> false) "mismatch while saving -> SaveFailed" ])
      test "UT-INV-016" "Edited marks Unsaved from Saved/Saving/SaveFailed/UnknownOutcome; Conflict and Offline kept" (fun () ->
          all
              [ equal Unsaved (saveTransition (Saved(1, t0)) Edited)
                equal Unsaved (saveTransition Saving Edited)
                equal Unsaved (saveTransition (SaveFailed "x") Edited)
                equal Unsaved (saveTransition UnknownOutcome Edited)
                equal Unsaved (saveTransition Unsaved Edited)
                equal (Conflict 3) (saveTransition (Conflict 3) Edited)
                equal Offline (saveTransition Offline Edited) ])
      test "UT-INV-016" "retry allowed from SaveFailed and UnknownOutcome; inapplicable events leave state unchanged" (fun () ->
          all
              [ equal Saving (saveTransition (SaveFailed "x") SaveRequested)
                equal Saving (saveTransition UnknownOutcome SaveRequested)
                equal (Saved(1, t0)) (saveTransition (Saved(1, t0)) SaveRequested)
                equal Saving (saveTransition Saving SaveRequested)
                equal Unsaved (saveTransition Unsaved (WriteConfirmed(2, t1)))
                equal (Conflict 3) (saveTransition (Conflict 3) SaveRequested)
                equal (Saved(1, t0)) (saveTransition (Saved(1, t0)) (ReadBackMatched(2, t1)))
                equal Unsaved (saveTransition Unsaved (ReadBackMatched(2, t1))) ])
      test "UT-INV-016" "Engine.ports wires the real implementations" (fun () ->
          let p = Engine.ports
          all
              [ equal (Validation.validateStart startInput) (p.ValidateStart startInput)
                equal Saving (p.SaveTransition Unsaved SaveRequested)
                (match p.Decide None t0 (StartInvestigation startInput) with
                 | Ok events ->
                     let doc = { Id = InvestigationId uuid; Version = 1; Events = events }
                     equal (Domain.replay events) (p.Decode(p.Encode doc) |> Result.toOption |> Option.bind (fun d -> p.Replay d.Events))
                 | Error e -> Error(sprintf "%A" e)) ]) ]
