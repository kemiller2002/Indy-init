namespace Causa.Ui.Contracts

open Causa.Domain
open Causa.Persistence

/// The screen a route shows.
type ScreenView =
    | ListScreen of InvestigationListView
    | StartScreen of StartView
    | WorkspaceScreen of WorkspaceView
    | AdministrationScreen of AdministrationView
    | SettingsScreen of SettingsView
    | NotFoundScreen of NotFoundView

/// Who produced a view. A fixture-backed view can never be reported as an integrated feature: acceptance evidence
/// requires Integrated, and only an integration adapter over the real engine says so (VERIFICATION-STRATEGY.md).
type BackendOrigin =
    | FixtureBackend of FixtureLabel
    | IntegratedBackend

type AppView =
    { Route: UiRoute
      Primary: NavLink<PrimaryDestination> list
      Screen: ScreenView
      /// Polite/assertive announcements for the live region (frozen Politeness).
      Announcements: (Politeness * string) list
      Origin: BackendOrigin }

/// What the user did, in Limen's SemanticEvent shape: an element id, an optional row key, an optional value. Views emit
/// only these; they never call a backend function, decide legality or build a domain command. Qualified access: its
/// Navigate would otherwise collide with IF41-APP Msg.Navigate (X04).
[<RequireQualifiedAccess>]
type UiIntent =
    | Navigate of UiRoute
    /// A field's value changed (data-on="input").
    | Input of field: FieldId * value: string * key: string option
    /// The credential field: turned into the opaque frozen Credential.T before it reaches any backend.
    | Secret of field: FieldId * Credential.T
    /// A button or link was activated.
    | Activate of action: ActionId * key: string option

/// The one substitutable interface. The fixture backend (fixtures/) and the integration adapter (real-adapter-sketch/)
/// both implement it; screens depend only on AppView and UiIntent. Pure: effects are data the host performs.
type UiBackend<'state, 'effect> =
    { /// The boundaries this backend actually provides; a screen needing more shows CapabilityAbsent.
      Provides: Capability list
      Init: UiRoute -> 'state * 'effect list
      Dispatch: UiIntent -> 'state -> 'state * 'effect list
      Project: 'state -> AppView }

/// An action's contract, as data (SCREEN-MANIFEST.json `actions` is checked against these).
type ActionContract =
    { Action: ActionId
      Screen: ScreenId
      Label: string
      Component: ComponentKind
      Needs: Capability
      /// The fields validated before the action is sent.
      Validates: FieldId list
      Outcome: string }
