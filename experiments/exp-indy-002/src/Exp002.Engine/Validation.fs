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
