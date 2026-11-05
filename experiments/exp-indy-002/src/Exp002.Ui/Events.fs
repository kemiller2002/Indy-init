/// Pure decoding of Limen SemanticEvent names (the data-event values in web/index.html) to contract UiActions.
module Exp002.Ui.Events

open Exp002.Contracts

/// name: the data-event value; key: the enclosing data-each item key; value: the control's value.
let decode (name: string) (key: string option) (value: string option) : UiAction option =
    let v = value |> Option.defaultValue ""
    match name, key with
    | "show-list", _ -> Some ShowList
    | "show-start", _ -> Some ShowStart
    | "open-investigation", Some id -> Some(OpenInvestigation(InvestigationId id))
    | "cancel-start", _ -> Some CancelStart
    | "submit-start", _ -> Some SubmitStart
    | "edit-details", _ -> Some(OpenEditor DetailsEditor)
    | "new-observation", _ -> Some(OpenEditor NewObservationEditor)
    | "edit-observation", Some id -> Some(OpenEditor(ObservationEditor(ObservationId id)))
    | "new-hypothesis", _ -> Some(OpenEditor NewHypothesisEditor)
    | "edit-hypothesis", Some id -> Some(OpenEditor(HypothesisEditor(HypothesisId id)))
    | "new-evidence", _ -> Some(OpenEditor NewEvidenceEditor)
    | "edit-evidence", Some id -> Some(OpenEditor(EvidenceEditor(EvidenceId id)))
    | "new-relationship", _ -> Some(OpenEditor NewRelationshipEditor)
    | "reassess-relationship", Some id -> Some(OpenEditor(RelationshipEditor(RelationshipId id)))
    | "withdraw-relationship", Some id -> Some(WithdrawSelected(RelationshipId id))
    | "cancel-editor", _ -> Some CancelEditor
    | "submit-editor", _ -> Some SubmitEditor
    | "save", _ -> Some SaveInvestigation
    | n, _ when n.StartsWith "field:" -> Some(SetField(n.Substring 6, v))
    | _ -> None
