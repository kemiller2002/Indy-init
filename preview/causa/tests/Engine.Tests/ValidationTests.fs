/// Pure tests of Causa.Preview.Validation (owner: domain agent). Returns the number of failures.
module ValidationTests

open Causa.HistoricalCompatibility
open Causa.Preview
open Causa.Preview.Validation

type private Case = { Name: string; Ok: bool; Detail: string }

let private case name ok detail = { Name = name; Ok = ok; Detail = detail }

let private show (errors: FieldError list) =
    match errors with
    | [] -> "[]"
    | es -> es |> List.map (fun e -> $"{e.FieldId}/{e.RuleId}") |> String.concat ", "

/// The (FieldId, RuleId) pairs of an error list, in order.
let private ids (errors: FieldError list) = errors |> List.map (fun e -> e.FieldId, e.RuleId)

let private expect name (expected: (string * string) list) (errors: FieldError list) =
    case name (ids errors = expected) $"expected {expected}, got {show errors}"

let private chars n = String.replicate n "a"

let private textRuleCases (fieldId: string) (label: string) (validate: string -> FieldError list) =
    [ expect $"{label}: 1 character is accepted" [] (validate "x")
      expect $"{label}: 10000 characters are accepted" [] (validate (chars 10000))
      expect $"{label}: 10001 characters are refused" [ fieldId, $"{fieldId}.maxLength" ] (validate (chars 10001))
      expect $"{label}: empty is refused" [ fieldId, $"{fieldId}.minLength" ] (validate "")
      expect $"{label}: whitespace-only \" \\t\\n\" is refused" [ fieldId, $"{fieldId}.minLength" ] (validate " \t\n")
      expect $"{label}: 10001 spaces are refused as blank and over-long"
          [ fieldId, $"{fieldId}.minLength"; fieldId, $"{fieldId}.maxLength" ] (validate (String.replicate 10001 " ")) ]

/// A surrogate pair: one rune, two UTF-16 code units.
let private grin = "\U0001F600"

let private surrogateCases =
    let fiveThousand = String.replicate 5000 grin // 5000 runes, 10000 code units
    let overByOne = fiveThousand + "a" // 5001 runes, 10001 code units
    let titleOver = String.replicate 100 grin + "a" // 101 runes, 201 code units
    [ case "length counts UTF-16 code units, not runes"
          (length grin = 2 && grin.EnumerateRunes() |> Seq.length = 1) $"length = {length grin}"
      expect "5000 surrogate pairs (10000 code units) are accepted" [] (observationText fiveThousand)
      expect "5000 surrogate pairs + 1 char (10001 code units, 5001 runes) are refused"
          [ "INV-024", "INV-024.maxLength" ] (observationText overByOne)
      expect "a title of 101 runes but 201 code units is refused" [ "INV-002", "INV-020" ] (startForm titleOver "p") ]

let private startFormCases =
    [ expect "start: a valid title and problem statement are accepted" [] (startForm "Title" "Problem")
      expect "start: a 200-character title is accepted" [] (startForm (chars 200) "p")
      expect "start: a 201-character title is refused (INV-020)" [ "INV-002", "INV-020" ] (startForm (chars 201) "p")
      expect "start: a 10000-character problem statement is accepted" [] (startForm "t" (chars 10000))
      expect "start: a 10001-character problem statement is refused (INV-021)" [ "INV-001", "INV-021" ] (startForm "t" (chars 10001))
      expect "start: an empty problem statement is refused (INV-019)" [ "INV-001", "INV-019" ] (startForm "t" "")
      expect "start: a \" \\t\\n\" problem statement is refused (INV-019)" [ "INV-001", "INV-019" ] (startForm "t" " \t\n")
      expect "start: a blank title is refused (STORAGE-TITLE)" [ "INV-002", "STORAGE-TITLE" ] (startForm " \t\n" "p")
      case "start: the blank-title message names the title"
          (startForm "" "p" |> List.forall (fun e -> e.Message.Contains "title")) (show (startForm "" "p"))
      expect "start: all errors are returned, title first"
          [ "INV-002", "INV-020"; "INV-001", "INV-019" ] (startForm (chars 201) "  ")
      expect "start: blank title and over-long problem statement both reported"
          [ "INV-002", "STORAGE-TITLE"; "INV-001", "INV-021" ] (startForm "" (chars 10001))
      case "start: messages name the field and the limit"
          (match startForm (chars 201) (chars 10001) with
           | [ t; p ] -> t.Message.Contains "title" && t.Message.Contains "200" && p.Message.Contains "problem statement" && p.Message.Contains "10000"
           | _ -> false)
          (startForm (chars 201) (chars 10001) |> List.map (fun e -> e.Message) |> String.concat " | ") ]

let private writerCases =
    let investigationId = Uuid.v5 Uuid.namespaceUrl "https://example.invalid/validation-tests"
    let now = (Timestamp.tryParse "2026-11-05T15:00:00.000Z").Value
    let events = Writer.eventsOf investigationId 1L now
    let refused name (command: Writer.Command) (needle: string) =
        match events command with
        | Error message -> case name (message.Contains needle) message
        | Ok _ -> case name false "the command was accepted"
    let accepted name (command: Writer.Command) =
        match events command with
        | Ok [ _ ] -> case name true ""
        | Ok lines -> case name false $"{List.length lines} lines"
        | Error e -> case name false e
    [ refused "writer: an over-long observation is refused" (Writer.RecordObservation(chars 10001)) "10000"
      refused "writer: an over-long hypothesis is refused" (Writer.RecordHypothesis(chars 10001)) "10000"
      refused "writer: an over-long title is refused" (Writer.Start(chars 201, "p")) "200"
      refused "writer: a blank title is refused with a message naming the title" (Writer.Start("   ", "p")) "title"
      refused "writer: all start errors are joined"
          (Writer.Start(chars 201, "")) "200 characters long. The problem statement must contain"
      accepted "writer: a boundary-length observation is accepted" (Writer.RecordObservation(chars 10000))
      accepted "writer: a boundary-length hypothesis is accepted" (Writer.RecordHypothesis(chars 10000))
      accepted "writer: a boundary-length start is accepted" (Writer.Start(chars 200, chars 10000)) ]

let private cases =
    List.concat
        [ startFormCases
          textRuleCases "INV-024" "observation" observationText
          textRuleCases "INV-026" "hypothesis" hypothesisStatement
          surrogateCases
          writerCases ]

let run () : int =
    cases
    |> List.iter (fun c -> printfn "%s %s%s" (if c.Ok then "PASS" else "FAIL") c.Name (if c.Ok then "" else " — " + c.Detail))
    cases |> List.filter (fun c -> not c.Ok) |> List.length
