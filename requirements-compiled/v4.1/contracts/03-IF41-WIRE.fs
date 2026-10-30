namespace Causa.Persistence

/// IF41-WIRE: HTTP as data. Persistence builds requests and decodes answers; the App runtime only executes them
/// through Limen and returns the raw result (ODR-V4-06 proposes that decoding stays inside persistence).
type RequestId = RequestId of int64

type HttpRequest =
    { Id: RequestId
      Method: string
      Url: string
      Headers: (string * string) list
      Body: string option }

type TransportFailure =
    | FailedBeforeSending
    | LostAfterSending
    | TimedOut

type HttpOutcome =
    | Answered of RequestId * status: int * headers: (string * string) list * body: string
    | NotAnswered of RequestId * TransportFailure
