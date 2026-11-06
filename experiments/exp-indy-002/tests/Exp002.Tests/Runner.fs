/// A tiny pure test runner: a test is (id, name, body); results are values, printing happens only in Program.
module Exp002.Tests.Runner

open Exp002.Contracts

type Test = { Id: string; Name: string; Body: unit -> Result<unit, string> }

let test id name body = { Id = id; Name = name; Body = body }

let run (t: Test) : Test * Result<unit, string> =
    let outcome =
        try
            t.Body()
        with ex ->
            Error(sprintf "exception: %s" ex.Message)
    t, outcome

// ---------- assertions (all return Result) ----------

let ok = Ok()

let check (cond: bool) (msg: string) : Result<unit, string> = if cond then Ok() else Error msg

let equal (expected: 'a) (actual: 'a) : Result<unit, string> =
    if expected = actual then Ok() else Error(sprintf "expected %A but got %A" expected actual)

let all (results: Result<unit, string> list) : Result<unit, string> =
    match results |> List.choose (function Error e -> Some e | Ok() -> None) with
    | [] -> Ok()
    | errs -> Error(String.concat "; " errs)

let isOk (r: Result<'a, 'e>) : Result<unit, string> =
    match r with
    | Ok _ -> Ok()
    | Error e -> Error(sprintf "expected Ok but got Error %A" e)

/// Asserts a validation result is an error that names exactly the given field IDs (order-insensitive).
let fieldErrors (expected: string list) (r: Result<'a, FieldError list>) : Result<unit, string> =
    match r with
    | Ok v -> Error(sprintf "expected field errors %A but got Ok %A" expected v)
    | Error es ->
        let ids = es |> List.map (fun e -> e.FieldId) |> List.sort
        equal (List.sort expected) ids

let invalidFields (expected: string list) (r: Result<'a, DomainError>) : Result<unit, string> =
    match r with
    | Error(Invalid es) -> fieldErrors expected (Error es: Result<unit, FieldError list>)
    | other -> Error(sprintf "expected Invalid %A but got %A" expected other)

let bind (f: 'a -> Result<unit, string>) (r: Result<'a, 'e>) : Result<unit, string> =
    match r with
    | Ok v -> f v
    | Error e -> Error(sprintf "unexpected Error %A" e)
