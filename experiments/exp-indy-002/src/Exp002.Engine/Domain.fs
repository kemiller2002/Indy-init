/// Pure decide/evolve/replay over the investigation event log.
module Exp002.Engine.Domain

open Exp002.Contracts

let private invalid (r: Result<'a, FieldError list>) : Result<'a, DomainError> = Result.mapError Invalid r

let private appendHistory (inv: Investigation) (e: Event) : Investigation =
    { inv with History = inv.History @ [ { Sequence = List.length inv.History + 1; Event = e } ] }

/// Applies an event's effect on the entity lists (history is appended separately).
let private apply (inv: Investigation) (event: Event) : Investigation =
    match event with
    | InvestigationStarted _ -> inv
    | _ -> inv

/// Folds one event. Non-start events are ignored when there is no investigation (decide never emits them);
/// a second start is ignored.
let evolve: Evolve =
    fun state event ->
        match state, event with
        | None, InvestigationStarted(id, details, at) ->
            Some
                { Id = id
                  CreatedAt = at
                  State = Open
                  Details = details
                  Observations = []
                  Hypotheses = []
                  Evidence = []
                  Relationships = []
                  History = [ { Sequence = 1; Event = event } ] }
        | Some _, InvestigationStarted _ -> state
        | None, _ -> None
        | Some inv, _ -> Some(appendHistory (apply inv event) event)

let replay: Replay = fun events -> List.fold evolve None events

let decide: Decide =
    fun state at command ->
        match state, command with
        | None, StartInvestigation input ->
            Validation.validateStart input
            |> invalid
            |> Result.map (fun (id, details) -> [ InvestigationStarted(id, details, at) ])
        | Some _, StartInvestigation _ -> Error AlreadyStarted
        | None, _ -> Error NotStarted
        | Some _, _ -> Error NotStarted // CG-2..CG-4 commands: implemented once their groups are released
