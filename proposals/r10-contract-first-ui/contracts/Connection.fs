namespace Causa.Ui.Contracts

open Causa.App

/// The Connect form's text fields as entered. The credential is not here: it reaches the backend as an opaque frozen
/// Credential.T (UiIntent.Secret) and is never part of any view model, binding or log (PF-015, INV-21, UI-019).
type ConnectForm = { Owner: string; Repository: string; Branch: string; CredentialEntered: bool }

/// What the connected repository allows, as the provider stated it (EXT-DATA-005). Frozen: ConnectResult.ConnectAccepted.
type WriteScope =
    | ReadOnlyConnection
    | Writable
    /// Not yet known (before Connect succeeds).
    | WriteScopeUnknown

/// The connection region of investigation-list (UI-001 before Connect; EXT-DATA-002/003/007).
type ConnectionView =
    { /// SM-CONNECTION's state, frozen (IF41-APP ConnectionState); the view never invents a sixth.
      State: ConnectionState
      Form: ConnectForm
      Errors: ValidationError list
      Connect: Availability
      Disconnect: Availability
      /// "owner/repository on branch", shown after Connect (UI-022: not in the global header as a commit hash).
      Connected: string option
      WriteScope: WriteScope }
