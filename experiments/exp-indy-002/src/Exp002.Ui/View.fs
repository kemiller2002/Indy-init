/// Pure projection of the Model to Limen view values (flat scalars and lists of flat items).
module Exp002.Ui.View

open Exp002.Contracts
open Exp002.Ui.App

type Prim =
    | S of string
    | B of bool
    | N of int64

type ViewValue =
    | Scalar of Prim
    | Items of (string * Prim) list list

let private s k (v: string) = k, Scalar(S v)
let private b k v = k, Scalar(B v)
let private items k xs = k, Items xs

/// "INV-001" -> "INV001": the view-key form of a catalog field ID.
let keyOf (fieldId: string) = fieldId.Replace("-", "")

let formatTimestamp (Timestamp ms) =
    System.DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture)

let saveStateText =
    function
    | Saved(v, at) -> sprintf "Saved (version %d, %s)" v (formatTimestamp at)
    | Unsaved -> "Unsaved changes"
    | Saving -> "Saving…"
    | Offline -> "Offline"
    | Conflict v -> sprintf "Conflict (stored version %d)" v
    | UnknownOutcome -> "Save outcome unknown"
    | SaveFailed r -> sprintf "Save failed: %s" r

/// Forma status-lozenge data-state for each save state.
let saveStateTone =
    function
    | Saved _ -> "ok"
    | Unsaved -> "attention"
    | Saving -> "current"
    | Offline
    | UnknownOutcome -> "unknown"
    | Conflict _
    | SaveFailed _ -> "blocked"

let isValidHttpUrl (u: string) =
    match System.Uri.TryCreate(u, System.UriKind.Absolute) with
    | true, uri -> uri.Scheme = "http" || uri.Scheme = "https"
    | _ -> false

let kindText =
    function
    | Supports -> "Supports"
    | Contradicts -> "Contradicts"
    | ConsistentWith -> "Consistent with"
    | Inconclusive -> "Inconclusive"

let kindCase =
    function
    | Supports -> "Supports"
    | Contradicts -> "Contradicts"
    | ConsistentWith -> "ConsistentWith"
    | Inconclusive -> "Inconclusive"

let private allKinds = [ Supports; Contradicts; ConsistentWith; Inconclusive ]

let describeEvent =
    function
    | InvestigationStarted(_, _, at) -> sprintf "%s · Investigation started" (formatTimestamp at)
    | InvestigationDetailsEdited(_, at) -> sprintf "%s · Details edited" (formatTimestamp at)
    | ObservationRecorded(o, at) -> sprintf "%s · Observation recorded: %s" (formatTimestamp at) o.Text
    | ObservationEdited(o, at) -> sprintf "%s · Observation edited: %s" (formatTimestamp at) o.Text
    | HypothesisRecorded(h, at) -> sprintf "%s · Hypothesis recorded: %s" (formatTimestamp at) h.Statement
    | HypothesisEdited(h, at) -> sprintf "%s · Hypothesis edited: %s" (formatTimestamp at) h.Statement
    | EvidenceAdmitted(e, at) -> sprintf "%s · Evidence admitted: %s" (formatTimestamp at) e.Label
    | EvidenceEdited(e, at) -> sprintf "%s · Evidence edited: %s" (formatTimestamp at) e.Label
    | RelationshipRecorded(r, at) -> sprintf "%s · Relationship recorded: %s" (formatTimestamp at) (kindText r.Kind)
    | RelationshipRevised(_, r, at) -> sprintf "%s · Relationship reassessed: %s" (formatTimestamp at) (kindText r.Kind)
    | RelationshipWithdrawn(_, reason, at) -> sprintf "%s · Relationship withdrawn: %s" (formatTimestamp at) reason

let private startFields = [ "INV-006"; "INV-001"; "INV-002"; "INV-023" ]

let editorFields =
    [ "INV-001"; "INV-002"; "INV-023"; "INV-024"; "INV-025"; "INV-026"; "INV-011"; "INV-027"; "INV-012"; "INV-028"
      "INV-029"; "INV-030-evidence"; "INV-030-hypothesis"; "INV-031"; "INV-038" ]

let private errorEntries prefix (fields: string list) (errors: FieldError list) =
    fields
    |> List.collect (fun f ->
        let msg = errors |> List.filter (fun e -> e.FieldId = f) |> List.map (fun e -> e.Message) |> String.concat " "
        [ s (prefix + "Err" + keyOf f) msg
          b (prefix + "HasErr" + keyOf f) (msg <> "")
          s (prefix + "Invalid" + keyOf f) (if msg = "" then "false" else "true") ])

let private draftValue (fieldId: string) (d: Draft) =
    match d, fieldId with
    | DetailsDraft x, "INV-001" -> x.ProblemStatement
    | DetailsDraft x, "INV-002" -> x.Title
    | DetailsDraft x, "INV-023" -> x.PrimaryQuestion
    | ObservationDraft x, "INV-024" -> x.ObservationText
    | ObservationDraft x, "INV-025" -> x.SourceReference
    | HypothesisDraft x, "INV-026" -> x.Statement
    | HypothesisDraft x, "INV-011" -> x.Explanation
    | EvidenceDraft x, "INV-027" -> x.Label
    | EvidenceDraft x, "INV-012" -> x.OriginalContent
    | EvidenceDraft x, "INV-028" -> x.SourceReference
    | EvidenceDraft x, "INV-029" -> x.SourceUrl
    | RelationshipDraft x, "INV-030-evidence" -> x.EvidenceId
    | RelationshipDraft x, "INV-030-hypothesis" -> x.HypothesisId
    | RelationshipDraft x, "INV-038" -> x.Rationale
    | RevisionDraft x, "INV-038" -> x.Rationale
    | _ -> ""

let private draftKind =
    function
    | Some(_, RelationshipDraft x) -> x.Kind
    | Some(_, RevisionDraft x) -> x.Kind
    | _ -> None

let private idText =
    {| ev = (fun (EvidenceId s) -> s); hy = (fun (HypothesisId s) -> s); ob = (fun (ObservationId s) -> s); rel = (fun (RelationshipId s) -> s) |}

let private workspaceEntries (ws: Workspace option) =
    let inv = ws |> Option.map (fun w -> w.Investigation)
    let editor = ws |> Option.bind (fun w -> w.Editor)
    let errors = ws |> Option.map (fun w -> w.FieldErrors) |> Option.defaultValue []
    let isEditor pred = editor |> Option.exists (fst >> pred)
    let current (i: Investigation) = i.Relationships |> List.filter (fun r -> r.Status = Current)
    let evLabel (i: Investigation) id = i.Evidence |> List.tryFind (fun e -> e.Id = id) |> Option.map (fun e -> e.Label) |> Option.defaultValue "?"
    let hyStatement (i: Investigation) id = i.Hypotheses |> List.tryFind (fun h -> h.Id = id) |> Option.map (fun h -> h.Statement) |> Option.defaultValue "?"
    let linksOf (i: Investigation) pick other =
        current i |> List.filter pick |> List.map (fun r -> sprintf "%s: %s" (kindText r.Kind) (other r)) |> String.concat "; "
    let orEmpty = Option.defaultValue ""
    let selectedEv = match editor with | Some(_, RelationshipDraft d) -> d.EvidenceId | _ -> ""
    let selectedHy = match editor with | Some(_, RelationshipDraft d) -> d.HypothesisId | _ -> ""
    let kind = draftKind editor
    let revising =
        match editor, inv with
        | Some(RelationshipEditor rid, _), Some i ->
            i.Relationships |> List.tryFind (fun r -> r.Id = rid)
            |> Option.map (fun r -> sprintf "%s → %s (currently %s)" (evLabel i r.EvidenceId) (hyStatement i r.HypothesisId) (kindText r.Kind))
            |> Option.defaultValue ""
        | _ -> ""
    let editorTitle =
        match editor |> Option.map fst with
        | Some DetailsEditor -> "Edit details"
        | Some NewObservationEditor -> "Record an observation"
        | Some(ObservationEditor _) -> "Edit observation"
        | Some NewHypothesisEditor -> "Record a hypothesis"
        | Some(HypothesisEditor _) -> "Edit hypothesis"
        | Some NewEvidenceEditor -> "Add evidence"
        | Some(EvidenceEditor _) -> "Edit evidence"
        | Some NewRelationshipEditor -> "Link evidence to a hypothesis"
        | Some(RelationshipEditor _) -> "Reassess relationship"
        | None -> ""
    [ b "wsLoaded" inv.IsSome
      b "wsLoading" inv.IsNone
      s "wsId" (inv |> Option.map (fun i -> let (InvestigationId x) = i.Id in x) |> orEmpty)
      s "wsCreated" (inv |> Option.map (fun i -> formatTimestamp i.CreatedAt) |> orEmpty)
      s "wsProblem" (inv |> Option.map (fun i -> i.Details.ProblemStatement) |> orEmpty)
      s "wsTitle" (inv |> Option.map (fun i -> displayTitle i.Details) |> orEmpty)
      s "wsQuestion" (inv |> Option.bind (fun i -> i.Details.PrimaryQuestion) |> orEmpty)
      b "wsHasQuestion" (inv |> Option.bind (fun i -> i.Details.PrimaryQuestion) |> Option.isSome)
      s "wsState" (inv |> Option.map (fun i -> match i.State with Open -> "Open") |> orEmpty)
      s "saveStateText" (ws |> Option.map (fun w -> saveStateText w.Save) |> orEmpty)
      s "saveStateTone" (ws |> Option.map (fun w -> saveStateTone w.Save) |> Option.defaultValue "unknown")
      b "saveDisabled" (ws |> Option.forall (fun w -> w.Save = Saving))
      b "editorOpen" editor.IsSome
      b "noEditor" editor.IsNone
      s "editorTitle" editorTitle
      b "editorDetails" (isEditor ((=) DetailsEditor))
      b "editorObservation" (isEditor (function NewObservationEditor | ObservationEditor _ -> true | _ -> false))
      b "editorHypothesis" (isEditor (function NewHypothesisEditor | HypothesisEditor _ -> true | _ -> false))
      b "editorEvidence" (isEditor (function NewEvidenceEditor | EvidenceEditor _ -> true | _ -> false))
      b "editorLink" (isEditor ((=) NewRelationshipEditor))
      b "editorReassess" (isEditor (function RelationshipEditor _ -> true | _ -> false))
      s "reassessTarget" revising
      items "reassessing" (match editor with
                           | Some(RelationshipEditor rid, _) -> [ [ ("id", S(idText.rel rid)) ] ]
                           | _ -> [])
      b "canLink" (inv |> Option.exists (fun i -> not i.Evidence.IsEmpty && not i.Hypotheses.IsEmpty))
      b "cannotLink" (inv |> Option.forall (fun i -> i.Evidence.IsEmpty || i.Hypotheses.IsEmpty))
      yield! allKinds |> List.map (fun k -> b ("kind" + kindCase k) (kind = Some k))
      yield! editorFields |> List.map (fun f -> s ("draft" + keyOf f) (editor |> Option.map (fun (_, d) -> draftValue f d) |> orEmpty))
      yield! errorEntries "" editorFields errors
      match inv with
      | None ->
          for k in [ "observations"; "hypotheses"; "evidence"; "relationships"; "history"; "evidenceOptions"; "hypothesisOptions" ] do
              items k []
          b "noObservations" true
          b "noHypotheses" true
          b "noEvidence" true
          b "noRelationships" true
      | Some i ->
          items "observations" (i.Observations |> List.map (fun o ->
              [ "id", S(idText.ob o.Id); "text", S o.Text; "source", S(orEmpty o.SourceReference); "hasSource", B o.SourceReference.IsSome ]))
          items "hypotheses" (i.Hypotheses |> List.map (fun h ->
              let links = linksOf i (fun r -> r.HypothesisId = h.Id) (fun r -> evLabel i r.EvidenceId)
              [ "id", S(idText.hy h.Id); "statement", S h.Statement; "explanation", S h.Explanation; "links", S links; "hasLinks", B(links <> "") ]))
          items "evidence" (i.Evidence |> List.map (fun e ->
              let url = orEmpty e.SourceUrl
              let links = linksOf i (fun r -> r.EvidenceId = e.Id) (fun r -> hyStatement i r.HypothesisId)
              [ "id", S(idText.ev e.Id); "label", S e.Label; "content", S e.OriginalContent; "source", S(orEmpty e.SourceReference)
                "hasSource", B e.SourceReference.IsSome; "url", S url; "hasLinkUrl", B(isValidHttpUrl url)
                "hasPlainUrl", B(url <> "" && not (isValidHttpUrl url)); "links", S links; "hasLinks", B(links <> "") ]))
          items "relationships" (i.Relationships |> List.map (fun r ->
              let status = match r.Status with Current -> "Current" | Revised _ -> "Superseded by a reassessment" | Withdrawn reason -> sprintf "Withdrawn: %s" reason
              [ "id", S(idText.rel r.Id); "evidenceLabel", S(evLabel i r.EvidenceId); "hypothesisStatement", S(hyStatement i r.HypothesisId)
                "kind", S(kindText r.Kind); "rationale", S r.Rationale; "status", S status; "isCurrent", B(r.Status = Current) ]))
          items "history" (i.History |> List.map (fun h -> [ "seq", N(int64 h.Sequence); "text", S(describeEvent h.Event) ]))
          items "evidenceOptions" (i.Evidence |> List.map (fun e -> [ "id", S(idText.ev e.Id); "label", S e.Label; "selected", B(idText.ev e.Id = selectedEv) ]))
          items "hypothesisOptions" (i.Hypotheses |> List.map (fun h -> [ "id", S(idText.hy h.Id); "label", S h.Statement; "selected", B(idText.hy h.Id = selectedHy) ]))
          b "noObservations" i.Observations.IsEmpty
          b "noHypotheses" i.Hypotheses.IsEmpty
          b "noEvidence" i.Evidence.IsEmpty
          b "noRelationships" i.Relationships.IsEmpty ]

let project (m: Model) : (string * ViewValue) list =
    [ b "onList" (m.Screen = ListScreen)
      b "onStart" (m.Screen = StartScreen)
      b "onWorkspace" (match m.Screen with WorkspaceScreen _ -> true | _ -> false)
      b "busy" (isBusy m)
      s "busyText" (if isBusy m then "Loading…" else "")
      b "hasAlert" m.Alert.IsSome
      s "alertText" (m.Alert |> Option.defaultValue "")
      items "investigations" (m.Index |> List.map (fun e ->
          [ "id", S e.Id; "title", S e.Title; "href", S(routeUrl (WorkspaceRoute e.Id)) ]))
      b "noInvestigations" m.Index.IsEmpty
      s "savedTo" savedToText
      s "startINV006" m.Start.InvestigationId
      s "startINV001" m.Start.ProblemStatement
      s "startINV002" m.Start.Title
      s "startINV023" m.Start.PrimaryQuestion
      yield! errorEntries "start" startFields m.StartErrors
      yield! workspaceEntries m.Workspace ]
