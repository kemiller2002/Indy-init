/// Pure field validation. Every check returns the FieldError list for its field; validators accumulate all of them.
module Exp002.Engine.Validation

open System.Text.RegularExpressions
open Exp002.Contracts

let private err fieldId message : FieldError = { FieldId = fieldId; Message = message }

let private isBlank (s: string) = isNull s || s.Trim().Length = 0
let private len (s: string) = if isNull s then 0 else s.Length

/// Required text: not blank, at most maxLen characters. Returns the text unchanged.
let requiredText (fieldId: string) (maxLen: int) (value: string) : Result<string, FieldError list> =
    if isBlank value then Error [ err fieldId "Required; must not be empty or whitespace." ]
    elif len value > maxLen then Error [ err fieldId (sprintf "Must be at most %d characters." maxLen) ]
    else Ok value

/// Optional text: blank -> None, else at most maxLen characters.
let optionalText (fieldId: string) (maxLen: int) (value: string) : Result<string option, FieldError list> =
    if isBlank value then Ok None
    elif len value > maxLen then Error [ err fieldId (sprintf "Must be at most %d characters." maxLen) ]
    else Ok(Some value)

let private uuidPattern =
    Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.CultureInvariant)

/// INV-006: required UUID.
let investigationId (value: string) : Result<InvestigationId, FieldError list> =
    if isBlank value then Error [ err "INV-006" "Required." ]
    elif uuidPattern.IsMatch value then Ok(InvestigationId value)
    else Error [ err "INV-006" "Must be a UUID (8-4-4-4-12 hexadecimal digits)." ]

/// Applicative combination: both results' errors are kept.
let both (a: Result<'a, FieldError list>) (b: Result<'b, FieldError list>) : Result<'a * 'b, FieldError list> =
    match a, b with
    | Ok x, Ok y -> Ok(x, y)
    | Error e1, Error e2 -> Error(e1 @ e2)
    | Error e, _
    | _, Error e -> Error e

let private details (problem: string) (title: string) (question: string) : Result<InvestigationDetails, FieldError list> =
    both (requiredText "INV-001" 10000 problem) (both (optionalText "INV-002" 200 title) (optionalText "INV-023" 10000 question))
    |> Result.map (fun (p, (t, q)) -> { ProblemStatement = p; Title = t; PrimaryQuestion = q })

/// INV-006, INV-001 (INV-019, INV-021), INV-002 (INV-020), INV-023.
let validateStart: ValidateStart =
    fun input ->
        both (investigationId input.InvestigationId) (details input.ProblemStatement input.Title input.PrimaryQuestion)

/// INV-034: the same rules as start, without the identifier.
let validateDetails: ValidateDetails =
    fun input -> details input.ProblemStatement input.Title input.PrimaryQuestion

/// INV-024 (required, <= 10000), INV-025 (optional, <= 10000).
let validateObservation: ValidateObservation =
    fun input -> both (requiredText "INV-024" 10000 input.ObservationText) (optionalText "INV-025" 10000 input.SourceReference)

/// INV-026, INV-011: both required, <= 10000.
let validateHypothesis: ValidateHypothesis =
    fun input -> both (requiredText "INV-026" 10000 input.Statement) (requiredText "INV-011" 10000 input.Explanation)

let private urlPattern = Regex(@"^https?://[^\s]+$", RegexOptions.CultureInvariant)

/// INV-029: optional; blank -> None; <= 2000; ^https?://[^\s]+$.
let sourceUrl (value: string) : Result<string option, FieldError list> =
    optionalText "INV-029" 2000 value
    |> Result.bind (function
        | Some u when not (urlPattern.IsMatch u) -> Error [ err "INV-029" "Must be an http:// or https:// URL without spaces." ]
        | v -> Ok v)

/// The validated evidence fields, with optional fields normalised to options.
let evidenceFields (input: EvidenceInput) : Result<string * string * string option * string option, FieldError list> =
    both
        (both (requiredText "INV-027" 200 input.Label) (requiredText "INV-012" 10000 input.OriginalContent))
        (both (optionalText "INV-028" 10000 input.SourceReference) (sourceUrl input.SourceUrl))
    |> Result.map (fun ((l, c), (r, u)) -> l, c, r, u)

/// INV-027 (required, <= 200), INV-012 (required, <= 10000), INV-028 (optional, <= 10000), INV-029.
/// On success returns the input with blank optional fields normalised to "".
let validateEvidence: ValidateEvidence =
    fun input ->
        evidenceFields input
        |> Result.map (fun (l, c, r, u) ->
            { Label = l
              OriginalContent = c
              SourceReference = Option.defaultValue "" r
              SourceUrl = Option.defaultValue "" u })

/// INV-031: a kind must be selected (no default).
let kind (value: RelationshipKind option) : Result<RelationshipKind, FieldError list> =
    match value with
    | Some k -> Ok k
    | None -> Error [ err "INV-031" "Select a relationship." ]

/// INV-038: rationale (and withdrawal reason) required, <= 10000.
let rationale (value: string) : Result<string, FieldError list> = requiredText "INV-038" 10000 value

/// INV-031, INV-038. Evidence/hypothesis existence is checked by Domain.decide against state.
let validateRelationship: ValidateRelationship =
    fun input -> both (kind input.Kind) (rationale input.Rationale)

/// Reassessment of a relationship: the same rules as a new link (INV-031, INV-038).
let validateRevision (input: RelationshipRevisionInput) : Result<RelationshipKind * string, FieldError list> =
    both (kind input.Kind) (rationale input.Rationale)
