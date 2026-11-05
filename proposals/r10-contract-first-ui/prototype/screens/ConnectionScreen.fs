/// The connection region of investigation-list (UI-001, EXT-DATA-002/003/007). Pure: ConnectionView -> bindings.
/// Binding names are SCREEN-MANIFEST.json ids; the credential never appears in any binding (PF-015).
module Causa.Ui.Prototype.ConnectionScreen

open Causa.App
open Causa.Ui.Contracts

let private offered a = match a with Available -> true | Unavailable _ | NotOffered -> false

let bindings (v: ConnectionView) : (string * BindingValue) list =
    let idOf = RegionId.value Regions.connection
    [ Bindings.flag (idOf + "-visible") (match v.State with Connected _ -> false | _ -> true)
      Bindings.text "connection-state" (Connection.describe v.State)
      Bindings.flag "connecting" (match v.State with Connecting _ -> true | _ -> false)
      Bindings.text (FieldId.value Fields.repositoryOwner) v.Form.Owner
      Bindings.text (FieldId.value Fields.repositoryName) v.Form.Repository
      Bindings.text (FieldId.value Fields.branch) v.Form.Branch
      Bindings.flag (ActionId.value Actions.connect + "-offered") (offered v.Connect)
      Bindings.flag (ActionId.value Actions.disconnect + "-offered") (offered v.Disconnect)
      Bindings.text "connected-repository" (v.Connected |> Option.defaultValue "")
      Bindings.flag "connection-errors-present" (not v.Errors.IsEmpty)
      Bindings.items
          "connection-errors"
          [ for e in v.Errors -> [ "field", BindingPrimitive.Text(FieldId.value e.Field); "message", BindingPrimitive.Text e.Message ] ] ]
