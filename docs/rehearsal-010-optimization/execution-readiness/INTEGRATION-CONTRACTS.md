# Rehearsal 010: integration contracts

These are the frozen IF41 contracts at each module boundary. All are in `requirements-compiled/v4.1.4/contracts/`, and **none is changed here**.

| Boundary | Contract | Producer → consumer |
|---|---|---|
| Domain ↔ everything | IF41-DOMAIN (`Decide`, `Evolve`, `Replay`, `Project`; `CoreEvent` in 00-DOMAIN-STUBS) | domain → persistence, ui |
| Storage codec | IF41-STORAGE (`Verify`, `PathOf`, `Encode`) | persistence → persistence and app |
| GitHub wire | IF41-WIRE (`Repository`, `ConnectionContext`, `Credential.T`, `HttpRequest`, `HttpOutcome`, `Execute`) | integration host executes; persistence builds and interprets |
| Errors and Connect | IF41-ERRORS (`Classifier`, `ConnectResult`, `Connector`) | persistence → ui |
| Save and load | IF41-SAVE (SM-SAVE, SM-LOAD, SM-CONFLICT) | persistence → ui |
| Application | IF41-APP (`Msg`, `Model`, `Update`, SM-CONNECTION, SM-NETWORK) | ui ↔ integration |
| Views | IF41-VIEW, IF41-REPORT | ui, ui-2 |

## Substitution demonstration (scratchpad; not committed, being a throwaway)

The frozen contract files, plus one file, were built with the frozen `ContractCheck.fsproj` settings:

```fsharp
module Screen =
    let connect (c: Connector) (execute: Execute) (id: RequestId) (ctx: ConnectionContext) : string =
        match c.InterpretConnect id (execute (c.ConnectRequest id ctx)) with
        | ConnectAccepted canWrite -> if canWrite then "connected (read/write)" else "connected (read-only)"
        | ConnectRejected _ -> "rejected"
        | ConnectSuperseded -> "superseded"
module Fixture = let connector : Connector = { ConnectRequest = …; InterpretConnect = fun _ _ -> ConnectAccepted false }
module GitHub  = let connector : Connector = { ConnectRequest = … api.github.com …; InterpretConnect = … }
module Swap    = let bothCompile = [ Screen.connect Fixture.connector Fixture.execute; Screen.connect GitHub.connector Fixture.execute ]
```

| Case | Result |
|---|---|
| Fixture and GitHub connectors behind the same screen code | build exit 0, 6 s |
| Adapter drift: `InterpretConnect` returns `bool` | exit 1, `FS0001 … expected to have type 'ConnectResult' but here has type 'bool'` |
| Screen drift: matches a case `ConnectReadOnly` the contract lacks | exit 1, `FS0049` and `FS0026` (warnings promoted by `TreatWarningsAsErrors`) |
| The same screen drift with `TreatWarningsAsErrors=false` | **exit 0**, 4 warnings: silent wrong behaviour |

## Rules for contract changes during the run (frozen, restated)

- A change to a frozen interface goes through the coordinator and is versioned. Consumers are notified before merge (`agent-file-ownership.json` `rules[1]`).
- A change to any file under `requirements-compiled/`, or to a project or props file, invalidates every test result (CHANGE-IMPACT-MATRIX.json `CI-ALL-GOVERNANCE`).
