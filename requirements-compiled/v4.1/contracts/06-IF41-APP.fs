namespace Causa.App

open Causa.Domain
open Causa.Persistence

/// IF41-APP: the Limen engine boundary (App <-> browser host). The host relays only what the engine asks for.
type Credential =
    private
        | Credential of string
    override _.ToString() = "<credential>"

type ConnectionState =
    | Disconnected
    | Connecting
    | Connected of canWrite: bool
    | CredentialRefused
    | Reauthenticating

type NetworkState =
    | Online
    | OfflineKnown

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
    | LiveAnnounce of politeness: string * text: string

type Route = Route of string

type Msg =
    | Navigate of Route
    | Browser of BrowserSignal
    | ConnectSubmitted of repository: string * credential: string
    | SessionInput of InvestigationId * SaveInput
    | DomainCommand of CommandEnvelope

type Model =
    { Connection: ConnectionState
      Network: NetworkState
      Route: Route
      Sessions: Map<InvestigationId, InvestigationSession> }

type Engine =
    { Init: Route -> Model * HostEffect list
      Update: Msg -> Model -> Model * HostEffect list }
