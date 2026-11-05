/// Field-level validation of user-entered investigation content, keyed by the ratified catalog IDs
/// (requirements/catalogs/causa-investigation.requirement-catalog.json). Pure; no I/O.
/// Interface frozen at CI-CADENCE-001 T+0 (docs/experiments/ci-cadence-001/PLAN.md).
module Causa.Preview.Validation

type FieldError = { FieldId: string; RuleId: string; Message: string }

/// Length in UTF-16 code units: what the HTML maxlength attribute counts.
let length (s: string) : int = s.Length

/// INV-019, INV-020, INV-021 (and the storage contract's non-blank title).
let startForm (title: string) (problemStatement: string) : FieldError list = []

/// INV-024: observation text, 1..10000.
let observationText (text: string) : FieldError list = []

/// INV-026: hypothesis statement, 1..10000.
let hypothesisStatement (statement: string) : FieldError list = []
