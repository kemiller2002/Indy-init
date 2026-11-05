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

[<RequireQualifiedAccess>]
module Connection =
    /// Labels satisfy the governed journey's patterns (support.mjs connect: owner, repository, branch, token|credential;
    /// ODR-R10-07). No requirement fixes the exact words.
    let fields : FieldSpec list =
        [ { Field = Fields.repositoryOwner; Label = "Repository owner"; Component = TextInput; Rules = [ Required ] }
          { Field = Fields.repositoryName; Label = "Repository"; Component = TextInput; Rules = [ Required ] }
          { Field = Fields.branch; Label = "Branch"; Component = TextInput; Rules = [ Required ] }
          { Field = Fields.token; Label = "Access token"; Component = PasswordInput; Rules = [ Required ] } ]

    let emptyForm = { Owner = ""; Repository = ""; Branch = ""; CredentialEntered = false }

    let validate (form: ConnectForm) : ValidationError list =
        let values =
            Map.ofList
                [ Fields.repositoryOwner, form.Owner
                  Fields.repositoryName, form.Repository
                  Fields.branch, form.Branch
                  Fields.token, (if form.CredentialEntered then "entered" else "") ]
        Validation.checkAll fields values

    let withField (field: FieldId) (value: string) (form: ConnectForm) =
        if field = Fields.repositoryOwner then { form with Owner = value }
        elif field = Fields.repositoryName then { form with Repository = value }
        elif field = Fields.branch then { form with Branch = value }
        else form

    /// The state word shown to the user (UI-024; never a raw state code).
    let describe (state: ConnectionState) =
        match state with
        | Disconnected -> "Not connected"
        | Connecting _ -> "Connecting"
        | Connected true -> "Connected"
        | Connected false -> "Connected (read only)"
        | ConnectionState.CredentialRefused -> "Credential not accepted"
        | Reauthenticating -> "Reconnecting"
