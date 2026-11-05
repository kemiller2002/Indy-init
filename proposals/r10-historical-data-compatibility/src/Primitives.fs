namespace Causa.HistoricalCompatibility

open System
open System.Globalization
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions

/// A canonical lowercase, hyphenated, non-nil UUID (CONTRACT §2.8). The text is kept exactly as stored.
[<Struct; CustomEquality; CustomComparison>]
type Uuid =
    private
    | Uuid of string

    member this.Text = let (Uuid s) = this in s
    override this.ToString() = this.Text
    override this.Equals(o) = match o with :? Uuid as u -> String.Equals(this.Text, u.Text, StringComparison.Ordinal) | _ -> false
    override this.GetHashCode() = StringComparer.Ordinal.GetHashCode this.Text

    interface IComparable with
        member this.CompareTo(o) =
            match o with
            | :? Uuid as u -> String.CompareOrdinal(this.Text, u.Text)
            | _ -> invalidArg "o" "not a Uuid"

/// An RFC 3339 UTC millisecond timestamp (CONTRACT §2.9). The stored text is authoritative; the instant is derived.
type Timestamp = { Text: string; Instant: DateTimeOffset }

/// Domain text: stored verbatim, never trimmed (CONTRACT §2.6), with at least one non-whitespace code unit.
[<Struct>]
type Text =
    private
    | Text of string

    member this.Value = let (Text s) = this in s

module Uuid =
    let private pattern = Regex(@"\A[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z", RegexOptions.CultureInvariant)
    let nilText = "00000000-0000-0000-0000-000000000000"

    let tryParse (s: string) : Uuid option =
        if pattern.IsMatch s && s <> nilText then Some(Uuid s) else None

    let text (u: Uuid) = u.Text

    let private toBytes (u: Uuid) : byte[] = Convert.FromHexString(u.Text.Replace("-", ""))

    let private ofBytes (b: byte[]) : Uuid =
        let h = Convert.ToHexString(b).ToLowerInvariant()
        Uuid(String.Join("-", [| h.Substring(0, 8); h.Substring(8, 4); h.Substring(12, 4); h.Substring(16, 4); h.Substring(20, 12) |]))

    /// RFC 9562 name-based UUID version 5 (SHA-1). Used to verify the derived event ids (CONTRACT §5.2).
    let v5 (ns: Uuid) (name: string) : Uuid =
        let hash = SHA1.HashData(Array.append (toBytes ns) (Encoding.UTF8.GetBytes name))
        let bytes =
            hash
            |> Array.take 16
            |> Array.mapi (fun i b ->
                match i with
                | 6 -> (b &&& 0x0Fuy) ||| 0x50uy
                | 8 -> (b &&& 0x3Fuy) ||| 0x80uy
                | _ -> b)
        ofBytes bytes

    /// RFC 9562 NAMESPACE_URL.
    let namespaceUrl = Uuid "6ba7b811-9dad-11d1-80b4-00c04fd430c8"

    /// CONTRACT §5.2: N = UUIDv5(NAMESPACE_URL, "https://github.com/kemiller2002/causa-data/contract/v1/event-id").
    let eventNamespace = v5 namespaceUrl "https://github.com/kemiller2002/causa-data/contract/v1/event-id"

    let deriveEventId (investigationId: Uuid) (aggregateVersion: int64) (correlationId: Uuid) =
        v5 eventNamespace (investigationId.Text + "/" + aggregateVersion.ToString(CultureInfo.InvariantCulture) + "/" + correlationId.Text)

module Timestamp =
    let private pattern =
        Regex(@"\A[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]\.[0-9]{3}Z\z", RegexOptions.CultureInvariant)

    /// Pattern and calendar validity (no 02-30, years 0001-9999) — reader stage 7.
    let tryParse (s: string) : Timestamp option =
        if not (pattern.IsMatch s) then None
        else
            match DateTimeOffset.TryParseExact(s, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal) with
            | true, d when d.Year >= 1 -> Some { Text = s; Instant = d }
            | _ -> None

module Text =
    /// The engine whitespace set (CONTRACT §2.6).
    let private isEngineWhitespace (c: char) =
        (c >= '\u0009' && c <= '\u000D') || c = ' ' || c = '\u0085' || c = ' ' || c = ' '
        || (c >= ' ' && c <= ' ') || c = ' ' || c = ' ' || c = ' ' || c = ' ' || c = '　'

    let tryCreate (s: string) : Text option =
        if s |> Seq.exists (isEngineWhitespace >> not) then Some(Text s) else None

    let value (t: Text) = t.Value

    /// `httpUrl` of common.schema.json: an absolute http(s) prefix followed by a non-whitespace code unit.
    let isHttpUrl (s: string) =
        s.Length > 0
        && (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        && (let rest = s.Substring(s.IndexOf("://", StringComparison.Ordinal) + 3)
            rest.Length > 0 && not (isEngineWhitespace rest[0]))

module Sha256 =
    let ofBytes (b: byte[]) = Convert.ToHexString(SHA256.HashData b).ToLowerInvariant()
    let ofText (s: string) = ofBytes (Encoding.UTF8.GetBytes s)

/// A closed wire enumeration: DU case <-> lowerCamelCase wire value (CONTRACT §6). No fallback value exists.
type WireEnum<'T when 'T: equality> =
    { Name: string
      Table: (string * 'T) list }

module WireEnum =
    let create name table : WireEnum<'T> = { Name = name; Table = table }
    let tryParse (e: WireEnum<'T>) (wire: string) = e.Table |> List.tryFind (fst >> (=) wire) |> Option.map snd
    let toWire (e: WireEnum<'T>) (v: 'T) = e.Table |> List.find (snd >> (=) v) |> fst
    let values (e: WireEnum<'T>) = e.Table |> List.map snd

type Lifecycle = Draft | Active | AwaitingEvidence | AwaitingVerification | Resolved | Closed | Archived
type LifecycleCommand = ActivateInvestigation | MarkAwaitingEvidence | MarkAwaitingVerification | ResolveInvestigation | CloseInvestigation | ReopenInvestigation | LifecycleVerificationFailed
type HypothesisState = HProposed | HPlausible | HSupported | HWeakened | HFalsified | HSuperseded
type HypothesisCommand = MarkPlausible | MarkSupported | MarkWeakened | Falsify | Supersede | Reopen
type UnknownState = UIdentified | UInvestigating | UResolved | UAcceptedResidual | USuperseded
type Materiality = Blocking | Material | NonMaterial | Informational
type AssumptionState = ExplicitUnverified | ProvisionallyAccepted | AssumptionVerified | AssumptionRejected | Expired
type ContradictionState = CDetected | CTriaged | CInvestigating | CResolved | CAcceptedResidual | CSuperseded
type ActionState = AProposed | AApproved | AInProgress | ACompleted | AInterpreted | ARejected | ACancelled
type ActionKind = Test | Inspection | Query | Comparison | Experiment
type CauseRole = RootCause | ContributingFactor | Trigger | EnablingCondition
type CauseState = CauseProposed | CauseConfirmed | CauseReopened | CauseSuperseded
type CorrectiveActionState = CaProposed | CaApproved | CaInProgress | CaImplemented | CaAwaitingVerificationOfEffect | CaVerifiedEffective | CaVerificationFailed | CaReworkRequired | CaCancelled
type CorrectiveActionKind = Mitigation | Workaround | Correction | Prevention
type EvidenceCategory = DirectObservation | Measurement | Log | Document | ExperimentResult | Testimony | ExternalReference
type VerificationStatus = Unverified | PartiallyVerified | Verified | Invalidated
type RelationshipKind = Supports | Contradicts | Discriminates | Insufficient
type PredictionStrength = Necessary | Expected | Discriminating | NonDiscriminating
type PredictionTiming = Prospective | Retrospective
type VerificationOutcome = Satisfied | Failed | Inconclusive
type ConclusionStrength = PlausibleExplanation | LeadingExplanation | SupportedCause | ConfirmedCause | AcceptedWithResidualUncertainty
type ProposalKind = ProposedHypothesis | ProposedUnknown | ProposedAction
type ProposalState = ProposalActive | ProposalAccepted | ProposalRejected
type ContentKind = TextContent | ArtifactContent

/// The enumerations of CONTRACT §6, value for value. `*Target` sets are checked as subsets of the state enumerations.
module Enums =
    let lifecycle = WireEnum.create "lifecycle" [ "draft", Draft; "active", Active; "awaitingEvidence", AwaitingEvidence; "awaitingVerification", AwaitingVerification; "resolved", Resolved; "closed", Closed; "archived", Archived ]
    let lifecycleTarget = [ Active; AwaitingEvidence; AwaitingVerification; Resolved; Closed ]
    let lifecycleCommand =
        WireEnum.create "lifecycleCommand"
            [ "activateInvestigation", ActivateInvestigation; "markAwaitingEvidence", MarkAwaitingEvidence; "markAwaitingVerification", MarkAwaitingVerification
              "resolveInvestigation", ResolveInvestigation; "closeInvestigation", CloseInvestigation; "reopenInvestigation", ReopenInvestigation
              "verificationFailed", LifecycleVerificationFailed ]
    let hypothesisState = WireEnum.create "hypothesisState" [ "proposed", HProposed; "plausible", HPlausible; "supported", HSupported; "weakened", HWeakened; "falsified", HFalsified; "superseded", HSuperseded ]
    let hypothesisTarget = [ HPlausible; HSupported; HWeakened; HFalsified; HSuperseded ]
    let hypothesisCommand = WireEnum.create "hypothesisCommand" [ "markPlausible", MarkPlausible; "markSupported", MarkSupported; "markWeakened", MarkWeakened; "falsify", Falsify; "supersede", Supersede; "reopen", Reopen ]
    let unknownState = WireEnum.create "unknownState" [ "identified", UIdentified; "investigating", UInvestigating; "resolved", UResolved; "acceptedResidual", UAcceptedResidual; "superseded", USuperseded ]
    let unknownTarget = [ UInvestigating; UResolved; UAcceptedResidual ]
    let materiality = WireEnum.create "materiality" [ "blocking", Blocking; "material", Material; "nonMaterial", NonMaterial; "informational", Informational ]
    let assumptionState = WireEnum.create "assumptionState" [ "explicitUnverified", ExplicitUnverified; "provisionallyAccepted", ProvisionallyAccepted; "verified", AssumptionVerified; "rejected", AssumptionRejected; "expired", Expired ]
    let assumptionTarget = [ ProvisionallyAccepted; AssumptionVerified; AssumptionRejected ]
    let contradictionState = WireEnum.create "contradictionState" [ "detected", CDetected; "triaged", CTriaged; "investigating", CInvestigating; "resolved", CResolved; "acceptedResidual", CAcceptedResidual; "superseded", CSuperseded ]
    let contradictionTarget = [ CTriaged; CResolved; CAcceptedResidual ]
    let actionState = WireEnum.create "actionState" [ "proposed", AProposed; "approved", AApproved; "inProgress", AInProgress; "completed", ACompleted; "interpreted", AInterpreted; "rejected", ARejected; "cancelled", ACancelled ]
    let actionTarget = [ AApproved; ARejected; AInProgress; ACompleted; ACancelled ]
    let actionKind = WireEnum.create "actionKind" [ "test", Test; "inspection", Inspection; "query", Query; "comparison", Comparison; "experiment", Experiment ]
    let causeRole = WireEnum.create "causeRole" [ "rootCause", RootCause; "contributingFactor", ContributingFactor; "trigger", Trigger; "enablingCondition", EnablingCondition ]
    let causeState = WireEnum.create "causeState" [ "proposed", CauseProposed; "confirmed", CauseConfirmed; "reopened", CauseReopened; "superseded", CauseSuperseded ]
    let correctiveActionState =
        WireEnum.create "correctiveActionState"
            [ "proposed", CaProposed; "approved", CaApproved; "inProgress", CaInProgress; "implemented", CaImplemented; "awaitingVerificationOfEffect", CaAwaitingVerificationOfEffect
              "verifiedEffective", CaVerifiedEffective; "verificationFailed", CaVerificationFailed; "reworkRequired", CaReworkRequired; "cancelled", CaCancelled ]
    let correctiveActionTarget = [ CaApproved; CaCancelled; CaInProgress; CaImplemented; CaAwaitingVerificationOfEffect; CaVerificationFailed; CaVerifiedEffective; CaReworkRequired ]
    let correctiveActionKind = WireEnum.create "correctiveActionKind" [ "mitigation", Mitigation; "workaround", Workaround; "correction", Correction; "prevention", Prevention ]
    let evidenceCategory =
        WireEnum.create "evidenceCategory"
            [ "directObservation", DirectObservation; "measurement", Measurement; "log", Log; "document", Document; "experimentResult", ExperimentResult; "testimony", Testimony; "externalReference", ExternalReference ]
    let verificationStatus = WireEnum.create "verificationStatus" [ "unverified", Unverified; "partiallyVerified", PartiallyVerified; "verified", Verified; "invalidated", Invalidated ]
    let relationshipKind = WireEnum.create "relationshipKind" [ "supports", Supports; "contradicts", Contradicts; "discriminates", Discriminates; "insufficient", Insufficient ]
    let predictionStrength = WireEnum.create "predictionStrength" [ "necessary", Necessary; "expected", Expected; "discriminating", Discriminating; "nonDiscriminating", NonDiscriminating ]
    let predictionTiming = WireEnum.create "predictionTiming" [ "prospective", Prospective; "retrospective", Retrospective ]
    let verificationOutcome = WireEnum.create "verificationOutcome" [ "satisfied", Satisfied; "failed", Failed; "inconclusive", Inconclusive ]
    let conclusionStrength =
        WireEnum.create "conclusionStrength"
            [ "plausibleExplanation", PlausibleExplanation; "leadingExplanation", LeadingExplanation; "supportedCause", SupportedCause; "confirmedCause", ConfirmedCause
              "acceptedWithResidualUncertainty", AcceptedWithResidualUncertainty ]
    let proposalKind = WireEnum.create "proposalKind" [ "hypothesis", ProposedHypothesis; "unknown", ProposedUnknown; "action", ProposedAction ]
    let proposalState = WireEnum.create "proposalState" [ "active", ProposalActive; "accepted", ProposalAccepted; "rejected", ProposalRejected ]
    let contentKind = WireEnum.create "contentKind" [ "text", TextContent; "artifact", ArtifactContent ]
