namespace Causa.Ui.Contracts

open System
open System.Text.RegularExpressions
open Causa.Domain
open Causa.Persistence

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

/// A value the domain engine projected at a version (Data Understood). The private representation keeps a stored value from
/// becoming Understood without Understood.projected; it does not prove the engine produced it, because
/// Understood.projected is public. That only the domain-engine adapter calls it is an ownership rule (integration owns the
/// adapter, AGENT-RULES) checked in review, not a type guarantee.
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

// ---- Validation (catalog validation blocks) ----------------------------------------------------------------------
// Close to Limen.Forms' FieldSpec, but NOT identical: Limen.Forms has Required: bool and no Pattern, counts runes, does
// not trim and reports one error per field; this checks UTF-16 length, trims for MinLength and lists every failed rule.
// Which semantics the product uses is CF-12.

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

/// What a route asks for, exactly as the URL states it. It is not an address: v4.1.5 addresses are derived, never
/// asserted (WorkspaceAddress.catalog / workingCopy, 02-IF41-STORAGE.fs), so whoever holds the listings resolves a target
/// against the workspaces it actually listed, and an unresolved target is not found.
type RouteTarget = { Workspace: string; Role: WorkspaceRole; Id: InvestigationId }

// ---- Failure and loading ---------------------------------------------------------------------------------------------

/// Why data is not shown. Each case has its own plain-language copy (PresentationLanguage.failure, UI-010, UI-028).
[<RequireQualifiedAccess>]
type UiFailure =
    /// Keyed by workspace and id (v4.1.5 W1): the same case can be stored in two workspaces.
    | InvestigationNotFound of RouteTarget
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
    | Waiting of activity: string
    | Ready of 'a
    | Failed of UiFailure
