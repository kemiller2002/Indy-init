/// investigation-list (catalog screens[0], route #/): the Investigations region (INV-039), Open investigation (INV-040),
/// and its loading, empty and failure states (UI-010, UI-028). Pure: AppView -> Bindings. It reads only the UI
/// contracts; no domain or persistence implementation type is reachable from here.
module Causa.Ui.Prototype.InvestigationListScreen

open System
open Causa.Ui.Contracts

let private countText c =
    match c with
    | Known n -> string n
    // Unknown is never zero (UI-022).
    | UnknownCount -> "Not available"
    | NotSupported -> "Not recorded"

let private copyText c =
    match c with
    | FrozenDemonstration -> "Demonstration (read only)"
    | WorkingCopy -> "Your working copy"
    | CopyKindUnknown -> ""

let private row (s: InvestigationSummary) =
    let counts = s.Counts |> Option.map Shown.value
    // The row key is the canonical route, so "Open" carries the workspace with the id (v4.1.5 W1).
    [ "id", BindingPrimitive.Text s.Href
      "title", BindingPrimitive.Text s.Title
      "status", BindingPrimitive.Text(s.Status |> Option.map Shown.value |> Option.defaultValue "Not available")
      "lastActivity", BindingPrimitive.Text(s.LastActivity |> Option.map (fun d -> d.ToString("d MMMM yyyy", Globalization.CultureInfo.InvariantCulture)) |> Option.defaultValue "Not available")
      "hypotheses", BindingPrimitive.Text(counts |> Option.map (fun c -> countText c.Hypotheses) |> Option.defaultValue "Not available")
      "evidence", BindingPrimitive.Text(counts |> Option.map (fun c -> countText c.Evidence) |> Option.defaultValue "Not available")
      "contradictions", BindingPrimitive.Text(counts |> Option.map (fun c -> countText c.UnresolvedContradictions) |> Option.defaultValue "Not available")
      "copy", BindingPrimitive.Text(copyText s.Copy)
      "href", BindingPrimitive.Text s.Href ]

let listBindings (v: InvestigationListView) : (string * BindingValue) list =
    let region = RegionId.value Regions.investigations
    let failure = match v.Investigations with Failed f -> Some(PresentationLanguage.failure f) | _ -> None
    [ yield Bindings.flag (region + "-visible") (match v.Investigations with Ready _ -> true | _ -> false)
      yield Bindings.flag "list-loading" (match v.Investigations with Waiting _ -> true | _ -> false)
      yield Bindings.text "list-loading-text" (match v.Investigations with Waiting t -> t | _ -> "")
      yield Bindings.flag "list-empty" (v.State = ScreenEmpty)
      yield Bindings.flag "list-failed" failure.IsSome
      yield Bindings.text "list-failure-what" (failure |> Option.map (fun c -> c.What) |> Option.defaultValue "")
      yield Bindings.text "list-failure-preserved" (failure |> Option.map (fun c -> c.Preserved) |> Option.defaultValue "")
      yield Bindings.text "list-failure-next" (failure |> Option.map (fun c -> c.NextStep) |> Option.defaultValue "")
      yield Bindings.flag (ActionId.value Actions.retryLoad + "-offered") (failure |> Option.exists (fun c -> c.Retry))
      yield Bindings.flag (ActionId.value Actions.newInvestigation + "-offered") (v.NewInvestigation = Available)
      yield Bindings.items "investigations" (match v.Investigations with Ready items -> Visible.value items |> List.map row | _ -> [])
      yield! ConnectionScreen.bindings v.Connection ]

/// The whole page's bindings for the list route; other routes show only the shell.
let bindings (view: AppView) : Bindings =
    Map.ofList (
        Shell.bindings view
        @ (match view.Screen with
           | ListScreen v -> Bindings.flag "screen-investigation-list" true :: listBindings v
           | _ -> [ Bindings.flag "screen-investigation-list" false ])
    )
