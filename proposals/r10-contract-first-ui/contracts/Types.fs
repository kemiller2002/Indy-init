namespace Causa.Ui.Contracts

open System
open System.Text.RegularExpressions
open Causa.Domain

/// A fixture's label. Every fixture value carries one, so a fixture-backed screen can never be reported as an
/// integrated one (VERIFICATION-STRATEGY.md, Layer A/B versus Layer C). Construct only through FixtureLabel.create.
type FixtureLabel = private FixtureLabel of string

[<RequireQualifiedAccess>]
module FixtureLabel =
    let create (name: string) : FixtureLabel = FixtureLabel("FIXTURE: " + name)
    let value (FixtureLabel s) = s

/// Where a stored record was read: the repository commit when the provider supplied one.
type StoredRef = { Path: string; Commit: string option }

// ---- The three capability boundaries (README.md section 3) ----------------------------------------------------------

/// What the UI is entitled to claim about a value.
type Capability =
    /// Repository status, investigation summaries, metadata and stored records exactly as read. No replay is claimed.
    | DataVisible
    /// Validated projections the domain engine supplied (hypotheses, evidence, relationships, timeline, conclusions,
    /// state). The UI never replays events itself.
    | DataUnderstood
    /// Inputs, commands, validation results and save progress. Persistence and the domain engine decide correctness.
    | DataEditable

/// A value shown as stored (Data Visible). It can never be turned into an Understood value: there is no conversion,
/// so stored data cannot be presented as the kernel's interpretation.
type Visible<'a> =
    private
    | StoredValue of 'a * StoredRef
    | VisibleFixture of 'a * FixtureLabel

/// A value the domain engine projected at a version (Data Understood).
type Understood<'a> =
    private
    | Projected of 'a * AggregateVersion
    | UnderstoodFixture of 'a * FixtureLabel

[<RequireQualifiedAccess>]
module Visible =
    let stored (at: StoredRef) (value: 'a) = StoredValue(value, at)
    let fixture (label: FixtureLabel) (value: 'a) = VisibleFixture(value, label)
    let value v = match v with StoredValue(x, _) | VisibleFixture(x, _) -> x
    let fixtureLabel v = match v with VisibleFixture(_, l) -> Some l | StoredValue _ -> None
    let map f v = match v with StoredValue(x, s) -> StoredValue(f x, s) | VisibleFixture(x, l) -> VisibleFixture(f x, l)

[<RequireQualifiedAccess>]
module Understood =
    /// Only an integration adapter holding a domain projection calls this.
    let projected (version: AggregateVersion) (value: 'a) = Projected(value, version)
    let fixture (label: FixtureLabel) (value: 'a) = UnderstoodFixture(value, label)
    let value u = match u with Projected(x, _) | UnderstoodFixture(x, _) -> x
    let version u = match u with Projected(_, v) -> Some v | UnderstoodFixture _ -> None
    let fixtureLabel u = match u with UnderstoodFixture(_, l) -> Some l | Projected _ -> None
    let map f u = match u with Projected(x, v) -> Projected(f x, v) | UnderstoodFixture(x, l) -> UnderstoodFixture(f x, l)

/// A displayed fact, either as stored or as projected; the boundary stays visible to the view.
type Shown<'a> =
    | AsStored of Visible<'a>
    | AsProjected of Understood<'a>

[<RequireQualifiedAccess>]
module Shown =
    let value s = match s with AsStored v -> Visible.value v | AsProjected u -> Understood.value u
    let capability s = match s with AsStored _ -> DataVisible | AsProjected _ -> DataUnderstood
    let isFixture s =
        match s with
        | AsStored v -> (Visible.fixtureLabel v).IsSome
        | AsProjected u -> (Understood.fixtureLabel u).IsSome

/// A count that may be unknown. Unknown is never rendered as zero (UI-022, UI-001 "where supported").
type Count =
    | Known of int
    | UnknownCount
    | NotSupported

/// Whether an action is offered. The domain engine decides (UI-004); the UI renders only what it is given.
type Availability =
    | Available
    /// Shown disabled with the engine's reason, only when the engine supplies one.
    | Unavailable of reason: string
    /// Not rendered at all.
    | NotOffered

// ---- Validation (catalog validation blocks; the shape follows Limen.Forms' FieldSpec so it maps 1:1) -----------------

type Rule =
    | Required
    | MinLength of int
    | MaxLength of int
    | Pattern of string
    | OneOf of string list

type ComponentKind =
    | TextInput
    | TextArea
    | UrlInput
    | PasswordInput
    | RadioGroup
    | Select
    | Display
    | Button
    | Link
    | Region

type FieldSpec =
    { Field: FieldId
      /// The accessible name the governed journeys look the field up by (catalog `name`).
      Label: string
      Component: ComponentKind
      Rules: Rule list }

type ValidationError = { Field: FieldId; Rule: Rule; Message: string }

[<RequireQualifiedAccess>]
module Validation =
    let private message (label: string) rule =
        match rule with
        | Required -> sprintf "%s is required." label
        | MinLength n -> sprintf "%s must have at least %d character%s." label n (if n = 1 then "" else "s")
        | MaxLength n -> sprintf "%s must have at most %d characters." label n
        | Pattern _ -> sprintf "%s is not in the expected form." label
        | OneOf options -> sprintf "Choose one of: %s." (String.Join(", ", options))

    let private holds (value: string) rule =
        match rule with
        | Required -> not (String.IsNullOrWhiteSpace value)
        // Catalog INV-019 (OD-INDY-21): whitespace-only counts as empty for a minimum length.
        | MinLength n -> value.Trim().Length >= n
        | MaxLength n -> value.Length <= n
        // An optional field that is empty is not checked against its pattern; Required/MinLength judge emptiness.
        | Pattern p -> value = "" || Regex.IsMatch(value, p)
        | OneOf options -> value = "" || List.contains value options

    /// Pure: the field's errors for this value, in rule order.
    let check (spec: FieldSpec) (value: string) : ValidationError list =
        spec.Rules
        |> List.filter (holds value >> not)
        |> List.map (fun r -> { Field = spec.Field; Rule = r; Message = message spec.Label r })

    let checkAll (specs: FieldSpec list) (values: Map<FieldId, string>) : ValidationError list =
        specs |> List.collect (fun s -> check s (values |> Map.tryFind s.Field |> Option.defaultValue ""))

/// An absolute http(s) URL, the only form INV-029 allows to be rendered as a link (UI-005 sanitising).
type SafeUrl = private SafeUrl of string

[<RequireQualifiedAccess>]
module SafeUrl =
    let pattern = @"^https?://[^\s]+$"
    let tryCreate (s: string) =
        let schemeOk = s.StartsWith("http://", StringComparison.Ordinal) || s.StartsWith("https://", StringComparison.Ordinal)
        if schemeOk && Uri.IsWellFormedUriString(s, UriKind.Absolute) && Regex.IsMatch(s, pattern) && s.Length <= 2000 then Some(SafeUrl s)
        else None
    let value (SafeUrl s) = s

// ---- Failure and loading ---------------------------------------------------------------------------------------------

/// Why data is not shown. Each case has its own plain-language copy (UiStates.describe, UI-010, UI-028).
type UiFailure =
    | InvestigationNotFound of InvestigationId
    /// The stored files did not verify (schema, digest, replay refusal). Fails closed: nothing partial is shown.
    | InvalidData of detail: string
    | NotAuthorized
    | CredentialRefused
    | ProviderUnreachable
    | ProviderRateLimited
    /// The provider answered something no case classifies.
    | ProviderError of detail: string
    /// The capability the screen needs is not provided by the current backend or by the frozen contracts.
    | CapabilityAbsent of needed: Capability * because: string

/// Data a screen is waiting for.
type Remote<'a> =
    | NotRequested
    /// The copy describes the user's task (UI-024 "Opening investigation"), never a fabricated progress figure.
    | Waiting of task: string
    | Ready of 'a
    | Failed of UiFailure

[<RequireQualifiedAccess>]
module Remote =
    let map f r =
        match r with
        | Ready x -> Ready(f x)
        | NotRequested -> NotRequested
        | Waiting t -> Waiting t
        | Failed e -> Failed e
