/// Model and the pure update. The domain is reached only through EnginePorts; browser work is returned as Effect data.
module Exp002.Ui.App

open Exp002.Contracts

// ---------- Effects and messages (UI-local, serialised to Limen by Dispatch) ----------

type Effect =
    | StorageGet of correlationId: string * key: string
    | StorageSet of correlationId: string * key: string * value: string
    | NavigatePush of correlationId: string * url: string

type StorageOutcome =
    | StorageValue of string option
    | StorageFailed of reason: string

type Msg =
    | Init of hash: string
    | Ui of UiAction
    | LocationChanged of hash: string
    | StorageResult of correlationId: string * StorageOutcome
    | NavigationDone of correlationId: string

// ---------- Model ----------

type IndexEntry = { Id: string; Title: string }

type Pending =
    | LoadIndex
    | LoadInvestigation of id: string
    | Write of id: string * encoded: string * version: int
    | ReadBack of id: string * encoded: string * version: int
    | WriteIndex

type Draft =
    | DetailsDraft of InvestigationDetailsInput
    | ObservationDraft of ObservationInput
    | HypothesisDraft of HypothesisInput
    | EvidenceDraft of EvidenceInput
    | RelationshipDraft of RelationshipInput
    | RevisionDraft of RelationshipRevisionInput
    | ConclusionDraft of ConclusionInput

type Workspace =
    { Id: InvestigationId
      Events: Event list
      Investigation: Investigation
      Version: int
      Save: SaveState
      Editor: (Editor * Draft) option
      FieldErrors: FieldError list }

type Model =
    { Screen: Screen
      Index: IndexEntry list
      Start: StartInvestigationInput
      StartErrors: FieldError list
      Workspace: Workspace option
      Pending: Map<string, Pending>
      NextCorrelation: int
      Alert: string option }

let indexKey = "exp002:index"
let investigationKey (id: string) = sprintf "exp002:inv:%s" id
let savedToText = "Saved to: this browser (local storage)"

// ---------- Pure helpers ----------

/// A UUID v4-formatted identifier derived deterministically from the boundary clock and a counter (no ambient RNG).
let freshUuid (Timestamp ms) (counter: int) =
    let mix (z0: uint64) =
        let z1 = (z0 ^^^ (z0 >>> 30)) * 0xbf58476d1ce4e5b9UL
        let z2 = (z1 ^^^ (z1 >>> 27)) * 0x94d049bb133111ebUL
        z2 ^^^ (z2 >>> 31)
    let a = mix (uint64 ms + 0x9e3779b97f4a7c15UL * uint64 (counter + 1))
    let b = mix (a + 0x9e3779b97f4a7c15UL)
    let hex = sprintf "%016x%016x" a b
    let c = hex.ToCharArray()
    c[12] <- '4'
    c[16] <- "89ab"[int (System.Convert.ToInt32(string c[16], 16) % 4)]
    let s = System.String c
    sprintf "%s-%s-%s-%s-%s" s[0..7] s[8..11] s[12..15] s[16..19] s[20..31]

let private escape (s: string) = s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n")

let private unescape (s: string) =
    let rec go (acc: char list) (cs: char list) =
        match cs with
        | '\\' :: 't' :: rest -> go ('\t' :: acc) rest
        | '\\' :: 'n' :: rest -> go ('\n' :: acc) rest
        | '\\' :: '\\' :: rest -> go ('\\' :: acc) rest
        | c :: rest -> go (c :: acc) rest
        | [] -> System.String(acc |> List.rev |> List.toArray)
    go [] (List.ofSeq s)

/// The list-screen index: one line per investigation, "id<TAB>title", with \\, \t and \n escaped.
let encodeIndex (entries: IndexEntry list) =
    "exp002-index/1\n" + (entries |> List.map (fun e -> escape e.Id + "\t" + escape e.Title) |> String.concat "\n")

let decodeIndex (text: string) : IndexEntry list =
    match text.Split('\n') |> List.ofArray with
    | "exp002-index/1" :: lines ->
        lines
        |> List.choose (fun l ->
            match l.Split('\t') with
            | [| id; title |] -> Some { Id = unescape id; Title = unescape title }
            | _ -> None)
    | _ -> []

let displayTitle (d: InvestigationDetails) =
    match d.Title with
    | Some t when t.Trim() <> "" -> t
    | _ -> d.ProblemStatement

let emptyStart id = { InvestigationId = id; ProblemStatement = ""; Title = ""; PrimaryQuestion = "" }

let init: Model =
    { Screen = ListScreen
      Index = []
      Start = emptyStart ""
      StartErrors = []
      Workspace = None
      Pending = Map.empty
      NextCorrelation = 1
      Alert = None }

type Route =
    | ListRoute
    | StartRoute
    | WorkspaceRoute of string

let parseRoute (hash: string) =
    match hash.TrimStart('#').Trim('/').Split('/') |> List.ofArray with
    | [ "investigations"; "new" ] -> StartRoute
    | [ "investigations"; id ] when id <> "" -> WorkspaceRoute(System.Uri.UnescapeDataString id)
    | _ -> ListRoute

let routeUrl =
    function
    | ListRoute -> "#/"
    | StartRoute -> "#/investigations/new"
    | WorkspaceRoute id -> sprintf "#/investigations/%s" (System.Uri.EscapeDataString id)

let describeDomainError =
    function
    | Invalid errors -> errors |> List.map (fun e -> e.Message) |> String.concat " "
    | NotStarted -> "The investigation has not been started."
    | AlreadyStarted -> "An investigation with this identifier is already started."
    | UnknownObservation(ObservationId id) -> sprintf "Unknown observation %s." id
    | UnknownHypothesis(HypothesisId id) -> sprintf "Unknown hypothesis %s." id
    | UnknownEvidence(EvidenceId id) -> sprintf "Unknown evidence %s." id
    | UnknownRelationship(RelationshipId id) -> sprintf "Unknown relationship %s." id
    | DuplicateRelationship(RelationshipId id) -> sprintf "This evidence is already linked to this hypothesis (%s)." id
    | RelationshipNotCurrent(RelationshipId id) -> sprintf "Relationship %s is no longer current." id

let describePersistenceError =
    function
    | NotFound(InvestigationId id) -> sprintf "Investigation %s was not found in this browser." id
    | VersionConflict(e, a) -> sprintf "Version conflict: expected %d, found %d." e a
    | Corrupt d -> sprintf "The saved investigation could not be read: %s" d
    | StorageUnavailable d -> sprintf "Browser storage is unavailable: %s" d

let kindOfText =
    function
    | "supports" -> Some Supports
    | "contradicts" -> Some Contradicts
    | "consistent-with" -> Some ConsistentWith
    | "inconclusive" -> Some Inconclusive
    | _ -> None

// ---------- Effects with correlation ----------

let private request (m: Model) (pending: Pending) (make: string -> Effect) =
    let corr = sprintf "c%d" m.NextCorrelation
    { m with NextCorrelation = m.NextCorrelation + 1; Pending = m.Pending.Add(corr, pending) }, make corr

let private navigate (m: Model) (route: Route) =
    let corr = sprintf "n%d" m.NextCorrelation
    { m with NextCorrelation = m.NextCorrelation + 1 }, NavigatePush(corr, routeUrl route)

let private showRoute (now: Timestamp) (m: Model) (route: Route) : Model * Effect list =
    match route with
    | ListRoute ->
        let m1, e = request { m with Screen = ListScreen } LoadIndex (fun c -> StorageGet(c, indexKey))
        m1, [ e ]
    | StartRoute ->
        { m with Screen = StartScreen; Start = emptyStart (freshUuid now m.NextCorrelation); StartErrors = []; NextCorrelation = m.NextCorrelation + 1 }, []
    | WorkspaceRoute id ->
        match m.Workspace with
        | Some ws when ws.Id = InvestigationId id -> { m with Screen = WorkspaceScreen ws.Id }, []
        | _ ->
            let m1, e = request { m with Screen = WorkspaceScreen(InvestigationId id); Workspace = None } (LoadInvestigation id) (fun c -> StorageGet(c, investigationKey id))
            m1, [ e ]

let private onRoute (now: Timestamp) (m: Model) (route: Route) =
    let current =
        match m.Screen with
        | ListScreen -> ListRoute
        | StartScreen -> StartRoute
        | WorkspaceScreen(InvestigationId id) -> WorkspaceRoute id
    if current = route && route <> ListRoute then m, [] else showRoute now { m with Alert = None } route

let private go now (m: Model) route =
    let m1, effects = showRoute now { m with Alert = None } route
    let m2, nav = navigate m1 route
    m2, effects @ [ nav ]

// ---------- Save pipeline ----------

let private startSave (ports: EnginePorts) (m: Model) : Model * Effect list =
    match m.Workspace with
    | Some ws when ws.Save <> Saving ->
        let (InvestigationId id) = ws.Id
        let version = ws.Version + 1
        let encoded = ports.Encode { Id = ws.Id; Version = version; Events = ws.Events }
        let ws1 = { ws with Save = ports.SaveTransition ws.Save SaveRequested }
        let m1, e = request { m with Workspace = Some ws1 } (Write(id, encoded, version)) (fun c -> StorageSet(c, investigationKey id, encoded))
        m1, [ e ]
    | _ -> m, []

let private withSave (ports: EnginePorts) (m: Model) (events: SaveEvent list) =
    match m.Workspace with
    | Some ws -> { m with Workspace = Some { ws with Save = events |> List.fold ports.SaveTransition ws.Save } }
    | None -> m

let private onStorage (ports: EnginePorts) (now: Timestamp) (m0: Model) (corr: string) (outcome: StorageOutcome) =
    match m0.Pending.TryFind corr with
    | None -> m0, []
    | Some pending ->
        let m = { m0 with Pending = m0.Pending.Remove corr }
        let wsIs id = m.Workspace |> Option.exists (fun ws -> ws.Id = InvestigationId id)
        match pending, outcome with
        | LoadIndex, StorageValue v -> { m with Index = v |> Option.map decodeIndex |> Option.defaultValue [] }, []
        | LoadIndex, StorageFailed r
        | WriteIndex, StorageFailed r -> { m with Alert = Some(describePersistenceError (StorageUnavailable r)) }, []
        | WriteIndex, StorageValue _ -> m, []
        | LoadInvestigation id, _ when m.Screen <> WorkspaceScreen(InvestigationId id) -> m, []
        | LoadInvestigation id, StorageFailed r -> { m with Alert = Some(describePersistenceError (StorageUnavailable r)) }, []
        | LoadInvestigation id, StorageValue None -> { m with Alert = Some(describePersistenceError (NotFound(InvestigationId id))) }, []
        | LoadInvestigation id, StorageValue(Some text) ->
            match ports.Decode text with
            | Error e -> { m with Alert = Some(describePersistenceError e) }, []
            | Ok stored ->
                match ports.Replay stored.Events with
                | None -> { m with Alert = Some(describePersistenceError (Corrupt "the event log does not start an investigation")) }, []
                | Some inv ->
                    let ws =
                        { Id = stored.Id; Events = stored.Events; Investigation = inv; Version = stored.Version
                          Save = Saved(stored.Version, now); Editor = None; FieldErrors = [] }
                    { m with Workspace = Some ws; Screen = WorkspaceScreen stored.Id }, []
        | Write(id, _, _), _ when not (wsIs id) -> m, []
        | ReadBack(id, _, _), _ when not (wsIs id) -> m, []
        | Write(id, encoded, version), StorageValue _ ->
            // CR-3: the write is confirmed (state stays Saving); Saved is entered only after the read-back matches.
            let m1, e = request (withSave ports m [ WriteConfirmed(version, now) ]) (ReadBack(id, encoded, version)) (fun c -> StorageGet(c, investigationKey id))
            m1, [ e ]
        | Write(_, _, _), StorageFailed r ->
            { withSave ports m [ WriteRejected(StorageUnavailable r) ] with Alert = Some(describePersistenceError (StorageUnavailable r)) }, []
        | ReadBack(id, encoded, version), StorageValue(Some back) when back = encoded ->
            let m1 = withSave ports m [ ReadBackMatched(version, now) ]
            let title = m1.Workspace |> Option.map (fun ws -> displayTitle ws.Investigation.Details) |> Option.defaultValue id
            let index = (m1.Index |> List.filter (fun e -> e.Id <> id)) @ [ { Id = id; Title = title } ]
            let m2 = { m1 with Workspace = m1.Workspace |> Option.map (fun ws -> { ws with Version = version }); Index = index }
            let m3, e = request m2 WriteIndex (fun c -> StorageSet(c, indexKey, encodeIndex index))
            m3, [ e ]
        | ReadBack _, StorageValue _ -> { withSave ports m [ ReadBackMismatched ] with Alert = Some "The saved copy did not match what was written." }, []
        | ReadBack _, StorageFailed _ -> withSave ports m [ WriteOutcomeUnknown ], []

// ---------- Editors ----------

let private findBy pick id xs = xs |> List.tryFind (fun x -> pick x = id)

let openDraft (inv: Investigation) (editor: Editor) : Draft option =
    let orEmpty = Option.defaultValue ""
    match editor with
    | DetailsEditor ->
        Some(DetailsDraft { ProblemStatement = inv.Details.ProblemStatement; Title = orEmpty inv.Details.Title; PrimaryQuestion = orEmpty inv.Details.PrimaryQuestion })
    | NewObservationEditor -> Some(ObservationDraft { ObservationText = ""; SourceReference = "" })
    | ObservationEditor id ->
        findBy (fun (o: Observation) -> o.Id) id inv.Observations
        |> Option.map (fun o -> ObservationDraft { ObservationText = o.Text; SourceReference = orEmpty o.SourceReference })
    | NewHypothesisEditor -> Some(HypothesisDraft { Statement = ""; Explanation = "" })
    | HypothesisEditor id ->
        findBy (fun (h: Hypothesis) -> h.Id) id inv.Hypotheses
        |> Option.map (fun h -> HypothesisDraft { Statement = h.Statement; Explanation = h.Explanation })
    | NewEvidenceEditor -> Some(EvidenceDraft { Label = ""; OriginalContent = ""; SourceReference = ""; SourceUrl = "" })
    | EvidenceEditor id ->
        findBy (fun (e: Evidence) -> e.Id) id inv.Evidence
        |> Option.map (fun e -> EvidenceDraft { Label = e.Label; OriginalContent = e.OriginalContent; SourceReference = orEmpty e.SourceReference; SourceUrl = orEmpty e.SourceUrl })
    | NewRelationshipEditor ->
        // CR-2: no evidence or hypothesis preselected; the pickers start on an empty placeholder.
        Some(RelationshipDraft { EvidenceId = ""; HypothesisId = ""; Kind = None; Rationale = "" })
    | NewConclusionEditor -> Some(ConclusionDraft { Statement = "" })
    | RelationshipEditor id ->
        findBy (fun (r: Relationship) -> r.Id) id inv.Relationships
        |> Option.map (fun r -> RevisionDraft { Kind = None; Rationale = "" })

let setDraftField (fieldId: string) (v: string) (d: Draft) : Draft =
    match d, fieldId with
    | DetailsDraft x, "INV-001" -> DetailsDraft { x with ProblemStatement = v }
    | DetailsDraft x, "INV-002" -> DetailsDraft { x with Title = v }
    | DetailsDraft x, "INV-023" -> DetailsDraft { x with PrimaryQuestion = v }
    | ObservationDraft x, "INV-024" -> ObservationDraft { x with ObservationText = v }
    | ObservationDraft x, "INV-025" -> ObservationDraft { x with SourceReference = v }
    | HypothesisDraft x, "INV-026" -> HypothesisDraft { x with Statement = v }
    | HypothesisDraft x, "INV-011" -> HypothesisDraft { x with Explanation = v }
    | EvidenceDraft x, "INV-027" -> EvidenceDraft { x with Label = v }
    | EvidenceDraft x, "INV-012" -> EvidenceDraft { x with OriginalContent = v }
    | EvidenceDraft x, "INV-028" -> EvidenceDraft { x with SourceReference = v }
    | EvidenceDraft x, "INV-029" -> EvidenceDraft { x with SourceUrl = v }
    | RelationshipDraft x, "INV-030-evidence" -> RelationshipDraft { x with EvidenceId = v }
    | RelationshipDraft x, "INV-030-hypothesis" -> RelationshipDraft { x with HypothesisId = v }
    | RelationshipDraft x, "INV-031" -> RelationshipDraft { x with Kind = kindOfText v }
    | RelationshipDraft x, "INV-038" -> RelationshipDraft { x with Rationale = v }
    | RevisionDraft x, "INV-031" -> RevisionDraft { x with Kind = kindOfText v }
    | RevisionDraft x, "INV-038" -> RevisionDraft { x with Rationale = v }
    | ConclusionDraft _, "INV-033" -> ConclusionDraft { Statement = v }
    | other, _ -> other

let setStartField (fieldId: string) (v: string) (s: StartInvestigationInput) =
    match fieldId with
    | "INV-006" -> { s with InvestigationId = v }
    | "INV-001" -> { s with ProblemStatement = v }
    | "INV-002" -> { s with Title = v }
    | "INV-023" -> { s with PrimaryQuestion = v }
    | _ -> s

let commandOf (editor: Editor) (draft: Draft) : Command option =
    match editor, draft with
    | DetailsEditor, DetailsDraft d -> Some(EditInvestigationDetails d)
    | NewObservationEditor, ObservationDraft o -> Some(RecordObservation o)
    | ObservationEditor id, ObservationDraft o -> Some(EditObservation(id, o))
    | NewHypothesisEditor, HypothesisDraft h -> Some(RecordHypothesis h)
    | HypothesisEditor id, HypothesisDraft h -> Some(EditHypothesis(id, h))
    | NewEvidenceEditor, EvidenceDraft e -> Some(AdmitEvidence e)
    | EvidenceEditor id, EvidenceDraft e -> Some(EditEvidence(id, e))
    | NewRelationshipEditor, RelationshipDraft r -> Some(LinkEvidence r)
    | RelationshipEditor id, RevisionDraft r -> Some(ReviseRelationship(id, r))
    | NewConclusionEditor, ConclusionDraft c -> Some(RecordConclusion c)
    | _ -> None

/// Runs one command through the domain. Accepted events are appended and replayed; the save state moves by Edited.
let private applyCommand (ports: EnginePorts) (now: Timestamp) (m: Model) (ws: Workspace) (cmd: Command) =
    match ports.Decide (Some ws.Investigation) now cmd with
    | Ok events ->
        let all = ws.Events @ events
        match ports.Replay all with
        | Some inv ->
            { m with Alert = None
                     Workspace = Some { ws with Events = all; Investigation = inv; Editor = None; FieldErrors = []
                                                Save = ports.SaveTransition ws.Save Edited } }, []
        | None -> { m with Alert = Some "The domain could not replay the investigation." }, []
    | Error(Invalid errors) -> { m with Workspace = Some { ws with FieldErrors = errors } }, []
    | Error e -> { m with Alert = Some(describeDomainError e) }, []

// ---------- Update ----------

let private onAction (ports: EnginePorts) (now: Timestamp) (m: Model) (action: UiAction) : Model * Effect list =
    match action, m.Workspace with
    | ShowList, _
    | CancelStart, _ -> go now m ListRoute
    | ShowStart, _ -> go now m StartRoute
    // The open link is an <a href>: the browser moves the hash itself, so no Navigation effect is requested here.
    | OpenInvestigation(InvestigationId id), _ -> onRoute now m (WorkspaceRoute id)
    | SetField(f, v), _ when m.Screen = StartScreen -> { m with Start = setStartField f v m.Start }, []
    | SetField(f, v), Some ws ->
        match ws.Editor with
        | Some(e, d) -> { m with Workspace = Some { ws with Editor = Some(e, setDraftField f v d) } }, []
        | None -> m, []
    | SubmitStart, _ ->
        match ports.ValidateStart m.Start with
        | Error errors -> { m with StartErrors = errors }, []
        | Ok(id, _) ->
            match ports.Decide None now (StartInvestigation m.Start) with
            | Error(Invalid errors) -> { m with StartErrors = errors }, []
            | Error e -> { m with Alert = Some(describeDomainError e) }, []
            | Ok events ->
                match ports.Replay events with
                | None -> { m with Alert = Some "The domain could not replay the new investigation." }, []
                | Some inv ->
                    let (InvestigationId raw) = id
                    let ws = { Id = inv.Id; Events = events; Investigation = inv; Version = 0; Save = Unsaved; Editor = None; FieldErrors = [] }
                    let m1 = { m with Workspace = Some ws; Screen = WorkspaceScreen inv.Id; StartErrors = []; Alert = None }
                    let m2, nav = navigate m1 (WorkspaceRoute raw)
                    // INV-004: starting writes nothing; the save state is Unsaved until the user presses Save (INV-015).
                    m2, [ nav ]
    | OpenEditor editor, Some ws ->
        match openDraft ws.Investigation editor with
        | Some d -> { m with Workspace = Some { ws with Editor = Some(editor, d); FieldErrors = [] } }, []
        | None -> { m with Alert = Some "That item no longer exists." }, []
    | CancelEditor, Some ws -> { m with Workspace = Some { ws with Editor = None; FieldErrors = [] } }, []
    | SubmitEditor, Some ws ->
        match ws.Editor |> Option.bind (fun (e, d) -> commandOf e d) with
        | Some cmd -> applyCommand ports now m ws cmd
        | None -> m, []
    | WithdrawSelected rid, Some ws ->
        let reason =
            match ws.Editor with
            | Some(RelationshipEditor r, RevisionDraft d) when r = rid -> d.Rationale
            | _ -> ""
        applyCommand ports now m ws (WithdrawRelationship(rid, reason))
    | SaveInvestigation, Some _ -> startSave ports m
    | _, None -> m, []

let update (ports: EnginePorts) (now: Timestamp) (m: Model) (msg: Msg) : Model * Effect list =
    match msg with
    | Init hash ->
        // The list index is always loaded at start so the list is ready when the user returns to it.
        let m1, e1 = showRoute now m (parseRoute hash)
        if parseRoute hash = ListRoute then m1, e1
        else
            let m2, e2 = request m1 LoadIndex (fun c -> StorageGet(c, indexKey))
            m2, e1 @ [ e2 ]
    | LocationChanged hash -> onRoute now m (parseRoute hash)
    | Ui action -> onAction ports now m action
    | StorageResult(corr, outcome) -> onStorage ports now m corr outcome
    | NavigationDone _ -> m, []

/// True while a Storage effect is in flight (the loading state).
let isBusy (m: Model) = not m.Pending.IsEmpty
