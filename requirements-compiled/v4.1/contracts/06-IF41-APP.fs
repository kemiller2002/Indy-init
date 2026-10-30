namespace Causa.App

open Causa.Domain
open Causa.Persistence

/// IF41-APP: the Limen engine boundary (App <-> browser host). The host relays only what the engine asks for.
/// The credential is opaque: it is created once from the Connect form and never rendered, logged or stored.
type Credential =
    private
        | Credential of string
    static member Create(token: string) = Credential token
    override _.ToString() = "<credential>"

/// SM-CONNECTION states.
type ConnectionState =
    | Disconnected
    | Connecting
    | Connected of canWrite: bool
    | CredentialRefused
    | Reauthenticating

/// SM-CONNECTION events.
type ConnectionInput =
    | ConnectSubmitted of repository: string * Credential
    | ConnectSucceeded of canWrite: bool
    | ConnectRefused of ProviderError
    | AuthFailed
    | ReconnectSubmitted of Credential
    | DisconnectConfirmed

/// SM-NETWORK states.
type NetworkState =
    | Online
    | Offline

/// SM-NETWORK events.
type NetworkInput =
    | OfflineObserved
    | OnlineObserved

type BrowserSignal =
    | ConnectivityChanged of online: bool
    | TimerFired of RequestId
    | HttpCompleted of HttpOutcome
    | LeaveAttempted

type HostEffect =
    | Http of HttpRequest
    | Timer of RequestId * milliseconds: int
    | SetLeaveGuard of bool
    | Download of fileName: string * content: string
    | LiveAnnounce of Politeness * text: string

type Route = Route of string

type Msg =
    | Navigate of Route
    | Browser of BrowserSignal
    | Connection of ConnectionInput
    | Network of NetworkInput
    | SessionInput of InvestigationId * SaveInput
    | ConflictStep of InvestigationId * ConflictInput
    | LoadStep of InvestigationId * LoadInput
    | DomainCommand of CommandEnvelope

type Model =
    { Connection: ConnectionState
      Network: NetworkState
      Route: Route
      Loads: Map<InvestigationId, LoadState>
      Sessions: Map<InvestigationId, InvestigationSession> }

type Engine =
    { Init: Route -> Model * HostEffect list
      Update: Msg -> Model -> Model * HostEffect list }
