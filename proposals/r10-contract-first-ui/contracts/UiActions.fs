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
/// only these; they never call a backend function, decide legality or build a domain command.
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

[<RequireQualifiedAccess>]
module ActionContracts =
    let private a action screen label comp needs validates outcome =
        { Action = action; Screen = screen; Label = label; Component = comp; Needs = needs; Validates = validates; Outcome = outcome }

    let all : ActionContract list =
        [ a Actions.connect Screens.list "Connect data repository" Button DataVisible
              [ Fields.repositoryOwner; Fields.repositoryName; Fields.branch; Fields.token ] "SM-CONNECTION Connecting, then Connected or a stated refusal"
          a Actions.disconnect Screens.list "Disconnect" Button DataVisible [] "SM-CONNECTION DisconnectConfirmed"
          a Actions.openInvestigation Screens.list "Open investigation" Link DataVisible [] "route #/investigations/{id}; the workspace shows its problem statement"
          a Actions.newInvestigation Screens.list "New investigation" Link DataEditable [] "route #/investigations/new"
          a Actions.retryLoad Screens.list "Try again" Button DataVisible [] "the failed read is sent again (offered only when it performs a real action)"
          a Actions.startInvestigation Screens.start "Start investigation" Button DataEditable
              [ Fields.problemStatement; Fields.title; Fields.question; Fields.identifier ] "StartInvestigation command; the workspace opens"
          a Actions.cancelStart Screens.start "Cancel" Button DataEditable [] "leaves the start screen; nothing is created or written"
          a Actions.recordObservation Screens.workspace "Record an observation" Button DataEditable [ Fields.observationText; Fields.observationSource ] "RecordObservation; the text is listed"
          a Actions.recordHypothesis Screens.workspace "Record a hypothesis" Button DataEditable [ Fields.hypothesisStatement ] "RecordHypothesis; it is listed"
          a Actions.recordEvidence Screens.workspace "Record evidence" Button DataEditable
              [ Fields.evidenceLabel; Fields.evidenceSource; Fields.evidenceUrl; Fields.evidenceContent ] "AdmitEvidence; it is listed"
          a Actions.linkEvidence Screens.workspace "Link evidence to a hypothesis" Button DataEditable
              [ Fields.linkEvidence; Fields.linkHypothesis; Fields.relationshipKind; Fields.relationshipRationale ] "RelateEvidence; shown on the evidence and the hypothesis"
          a Actions.reassess Screens.workspace "Reassess" Button DataEditable
              [ Fields.linkEvidence; Fields.linkHypothesis; Fields.relationshipKind; Fields.relationshipRationale ] "a new current assessment; the earlier one stays in the history"
          a Actions.recordConclusion Screens.workspace "Record a conclusion" Button DataEditable [ Fields.conclusionStatement ] "a conclusion snapshot; the investigation is not resolved"
          a Actions.editDetails Screens.workspace "Edit investigation details" Button DataEditable [] "opens the details editor"
          a Actions.saveDetails Screens.workspace "Save details" Button DataEditable [ Fields.detailsTitle; Fields.detailsProblem; Fields.detailsQuestion ] "details changed; the identifier is unchanged"
          a Actions.cancelEditor Screens.workspace "Cancel" Button DataEditable [] "closes the editor; nothing entered is recorded or written"
          a Actions.save Screens.workspace "Save" Button DataEditable [] "SM-SAVE SaveRequested; Saved only after read-back confirmation"
          a Actions.saveIndicator Screens.workspace "Save-state action" Button DataEditable [] "the frozen SaveAction (Retry, Reconnect, Check again, Review, Reapply, Discard, Export)" ]
