/// CG-1-START: INV-001, 002, 004, 006, 019, 020, 021, 023.
module Exp002.Tests.StartTests

open Exp002.Contracts
open Exp002.Engine
open Exp002.Tests.Runner

let uuid = "3f2b8c1e-9a4d-4e6f-8b21-0c5d7e9f1a23"
let t0 = Timestamp 1_700_000_000_000L

let startInput: StartInvestigationInput =
    { InvestigationId = uuid
      ProblemStatement = "Checkout latency doubled after deploy"
      Title = "Checkout latency"
      PrimaryQuestion = "What changed in the deploy?" }

let started () =
    Domain.decide None t0 (StartInvestigation startInput) |> Result.map Domain.replay

let tests =
    [ test "UT-INV-001" "problem statement is carried into InvestigationStarted.details" (fun () ->
          match Validation.validateStart startInput with
          | Ok(_, d) -> equal "Checkout latency doubled after deploy" d.ProblemStatement
          | Error e -> Error(sprintf "%A" e))
      test "UT-INV-001" "problem statement keeps unicode and inner whitespace exactly" (fun () ->
          let p = "  Ünïcödé — 問題  \n line2 "
          Validation.validateStart { startInput with ProblemStatement = p }
          |> bind (fun (_, d) -> equal p d.ProblemStatement))
      test "UT-INV-002" "title is optional: blank -> None" (fun () ->
          all
              [ Validation.validateStart { startInput with Title = "" } |> bind (fun (_, d) -> equal None d.Title)
                Validation.validateStart { startInput with Title = "   " } |> bind (fun (_, d) -> equal None d.Title)
                Validation.validateStart startInput |> bind (fun (_, d) -> equal (Some "Checkout latency") d.Title) ])
      test "UT-INV-006" "valid UUID accepted (any case)" (fun () ->
          all
              [ Validation.validateStart startInput |> bind (fun (id, _) -> equal (InvestigationId uuid) id)
                Validation.validateStart { startInput with InvestigationId = uuid.ToUpperInvariant() } |> isOk ])
      test "UT-INV-006" "invalid or blank identifier rejected with INV-006" (fun () ->
          all
              [ Validation.validateStart { startInput with InvestigationId = "" } |> fieldErrors [ "INV-006" ]
                Validation.validateStart { startInput with InvestigationId = "not-a-uuid" } |> fieldErrors [ "INV-006" ]
                Validation.validateStart { startInput with InvestigationId = uuid + "0" } |> fieldErrors [ "INV-006" ]
                Validation.validateStart { startInput with InvestigationId = "g" + uuid.Substring 1 } |> fieldErrors [ "INV-006" ]
                Validation.validateStart { startInput with InvestigationId = " " + uuid } |> fieldErrors [ "INV-006" ] ])
      test "UT-INV-019" "empty or whitespace problem statement rejected with INV-001" (fun () ->
          all
              [ Validation.validateStart { startInput with ProblemStatement = "" } |> fieldErrors [ "INV-001" ]
                Validation.validateStart { startInput with ProblemStatement = " \t\n " } |> fieldErrors [ "INV-001" ]
                Validation.validateStart { startInput with ProblemStatement = "x" } |> isOk ])
      test "UT-INV-019" "decide refuses blank problem statement with Invalid INV-001; no event" (fun () ->
          Domain.decide None t0 (StartInvestigation { startInput with ProblemStatement = "  " }) |> invalidFields [ "INV-001" ])
      test "UT-INV-020" "title length boundary 200 accepted, 201 rejected with INV-002" (fun () ->
          all
              [ Validation.validateStart { startInput with Title = String.replicate 200 "a" } |> isOk
                Validation.validateStart { startInput with Title = String.replicate 201 "a" } |> fieldErrors [ "INV-002" ] ])
      test "UT-INV-021" "problem statement length boundary 10000 accepted, 10001 rejected with INV-001" (fun () ->
          all
              [ Validation.validateStart { startInput with ProblemStatement = String.replicate 10000 "p" } |> isOk
                Validation.validateStart { startInput with ProblemStatement = String.replicate 10001 "p" } |> fieldErrors [ "INV-001" ] ])
      test "UT-INV-023" "primary question optional, blank -> None, <= 10000" (fun () ->
          all
              [ Validation.validateStart { startInput with PrimaryQuestion = " " } |> bind (fun (_, d) -> equal None d.PrimaryQuestion)
                Validation.validateStart startInput |> bind (fun (_, d) -> equal (Some "What changed in the deploy?") d.PrimaryQuestion)
                Validation.validateStart { startInput with PrimaryQuestion = String.replicate 10000 "q" } |> isOk
                Validation.validateStart { startInput with PrimaryQuestion = String.replicate 10001 "q" } |> fieldErrors [ "INV-023" ] ])
      test "UT-INV-023" "all field errors are accumulated, not just the first" (fun () ->
          Validation.validateStart
              { InvestigationId = "bad"
                ProblemStatement = ""
                Title = String.replicate 201 "t"
                PrimaryQuestion = String.replicate 10001 "q" }
          |> fieldErrors [ "INV-006"; "INV-001"; "INV-002"; "INV-023" ])
      test "UT-INV-004" "start emits exactly one InvestigationStarted with id, details and timestamp" (fun () ->
          Domain.decide None t0 (StartInvestigation startInput)
          |> bind (fun events ->
              equal
                  [ InvestigationStarted(
                        InvestigationId uuid,
                        { ProblemStatement = startInput.ProblemStatement
                          Title = Some startInput.Title
                          PrimaryQuestion = Some startInput.PrimaryQuestion },
                        t0
                    ) ]
                  events))
      test "UT-INV-004" "replayed start is Open, CreatedAt = start time, empty content, one history entry" (fun () ->
          started ()
          |> bind (function
              | None -> Error "no investigation"
              | Some inv ->
                  all
                      [ equal (InvestigationId uuid) inv.Id
                        equal Open inv.State
                        equal t0 inv.CreatedAt
                        equal 1 inv.History.Length
                        equal 1 inv.History.Head.Sequence
                        check (List.isEmpty inv.Observations && List.isEmpty inv.Hypotheses && List.isEmpty inv.Evidence && List.isEmpty inv.Relationships) "content not empty" ]))
      test "UT-INV-004" "second start refused with AlreadyStarted" (fun () ->
          started ()
          |> bind (fun inv -> equal (Error AlreadyStarted) (Domain.decide inv t0 (StartInvestigation startInput)))) ]
