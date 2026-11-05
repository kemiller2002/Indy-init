/// Field-level validation of user-entered investigation content, keyed by the ratified catalog IDs
/// (requirements/catalogs/causa-investigation.requirement-catalog.json). Pure; no I/O.
/// Interface frozen at CI-CADENCE-001 T+0 (docs/experiments/ci-cadence-001/PLAN.md).
module Causa.Preview.Validation

open Causa.HistoricalCompatibility

type FieldError = { FieldId: string; RuleId: string; Message: string }

/// Length in UTF-16 code units: what the HTML maxlength attribute counts.
let length (s: string) : int = s.Length

let private isBlank (s: string) = (Text.tryCreate s).IsNone

/// A rule: the error it produces when its predicate refuses the value.
let private rule fieldId ruleId message (refuses: string -> bool) (value: string) : FieldError option =
    if refuses value then Some { FieldId = fieldId; RuleId = ruleId; Message = message } else None

let private apply (rules: (string -> FieldError option) list) (value: string) : FieldError list =
    rules |> List.choose (fun r -> r value)

let private nonBlank fieldId ruleId (name: string) =
    rule fieldId ruleId $"The {name} must contain at least one non-whitespace character." isBlank

let private maxLength fieldId ruleId (name: string) (limit: int) =
    rule fieldId ruleId $"The {name} must be at most {limit} characters long." (fun s -> length s > limit)

let private titleRules =
    [ nonBlank "INV-002" "STORAGE-TITLE" "title"
      maxLength "INV-002" "INV-020" "title" 200 ]

let private problemStatementRules =
    [ nonBlank "INV-001" "INV-019" "problem statement"
      maxLength "INV-001" "INV-021" "problem statement" 10000 ]

let private textRules fieldId (name: string) =
    [ nonBlank fieldId $"{fieldId}.minLength" name
      maxLength fieldId $"{fieldId}.maxLength" name 10000 ]

/// INV-019, INV-020, INV-021 (and the storage contract's non-blank title).
let startForm (title: string) (problemStatement: string) : FieldError list =
    apply titleRules title @ apply problemStatementRules problemStatement

/// INV-024: observation text, 1..10000.
let observationText (text: string) : FieldError list = apply (textRules "INV-024" "observation text") text

/// INV-026: hypothesis statement, 1..10000.
let hypothesisStatement (statement: string) : FieldError list =
    apply (textRules "INV-026" "hypothesis statement") statement
