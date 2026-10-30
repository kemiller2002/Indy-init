namespace Causa.Persistence

/// IF41-ERRORS: the classification of github-error-matrix.json. Every provider answer maps to exactly one case.
type RateLimitKind =
    | Primary of resetAtEpochSeconds: int64 option
    | Secondary of retryAfterSeconds: int option

type ProviderError =
    | AuthenticationFailed
    | PermissionDenied of operation: string
    | RateLimited of RateLimitKind
    | StaleHead of expected: CommitOid * actual: CommitOid option
    | ValidationFailed of messages: string list
    | NotFound of what: string
    | HistoryRewritten of expected: CommitOid
    | NetworkUnavailable of TransportFailure
    | Malformed of detail: string
    | ReadBackMismatch of commit: CommitOid
    | Unrecognised of status: int

type Classifier = HttpOutcome -> Result<string, ProviderError>
