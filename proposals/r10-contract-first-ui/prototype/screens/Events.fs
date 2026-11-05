/// Limen SemanticEvent (name, key, value) -> UiIntent. Mechanical: the element id IS the field or action id. The token
/// field becomes the opaque frozen Credential.T here, before any backend sees it.
module Causa.Ui.Prototype.Events

open Causa.Persistence
open Causa.Ui.Contracts

let toIntent (name: string) (key: string option) (value: string option) : UiIntent option =
    let v = value |> Option.defaultValue ""
    if name = "navigate" then Some(Navigate(Navigation.parse v))
    elif name = FieldId.value Fields.token then Some(Secret(Fields.token, Credential.create v))
    elif Connection.fields @ StartForm.fields @ DetailsForm.fields @ ContentForms.all |> List.exists (fun f -> FieldId.value f.Field = name) then
        Some(Input(FieldId name, v, key))
    elif ActionContracts.all |> List.exists (fun a -> ActionId.value a.Action = name) then Some(Activate(ActionId name, key))
    else None

/// A type to find this assembly by in the boundary tests.
type Marker = class end
