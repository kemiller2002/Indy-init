namespace Causa.Ui.Contracts

open System
open Causa.Domain
open Causa.Persistence

/// The investigation sections (INV-042, UI-029). Corrective Actions only "where supported".
type Section =
    | Overview
    | CausalAnalysis
    | EvidenceSection
    | HypothesesSection
    | TimelineSection
    | CorrectiveActions

/// Every route the in-scope requirements define. Hash routes (SCREEN-CONTRACTS URL contract, D-203).
type UiRoute =
    /// `#/` investigation-list (catalog screens[0]); before Connect it holds the connection region (UI-001, EXT-DATA-002).
    | InvestigationsRoute
    /// `#/investigations/new` (catalog screens[1]).
    | StartRoute
    /// `#/investigations/{id}?ws=<workspace>&role=<catalog|working-copy>[&tab=<section>]`. The workspace and its role
    /// are part of the identity (v4.1.5 W1: the same case can be stored in two workspaces); `tab` (S02, default
    /// overview) selects the section.
    | WorkspaceRoute of InvestigationRef * Section
    /// `#/administration`: UI-018 requires the page; no document fixes its path (OWNER-DECISIONS CF-05).
    | AdministrationRoute
    /// `#/settings` (S24).
    | SettingsRoute
    | NotFoundRoute of requested: string

type PrimaryDestination =
    | Investigations
    | Administration
    | Settings

/// One navigation link as the view renders it; `Current` becomes aria-current="page" (J2-08, J3-01).
type NavLink<'d> = { Destination: 'd; Label: string; Href: string; Current: bool }

[<RequireQualifiedAccess>]
module Navigation =
    let sectionSlug s =
        match s with
        | Overview -> "overview"
        | CausalAnalysis -> "causal-analysis"
        | EvidenceSection -> "evidence"
        | HypothesesSection -> "hypotheses"
        | TimelineSection -> "timeline"
        | CorrectiveActions -> "corrective-actions"

    /// The accessible names J2-08 and the design contract (investigationSections) look the links up by.
    let sectionLabel s =
        match s with
        | Overview -> "Overview"
        | CausalAnalysis -> "Causal Analysis"
        | EvidenceSection -> "Evidence"
        | HypothesesSection -> "Hypotheses"
        | TimelineSection -> "Timeline"
        | CorrectiveActions -> "Corrective Actions"

    let sections = [ Overview; CausalAnalysis; EvidenceSection; HypothesesSection; TimelineSection; CorrectiveActions ]

    let destinationLabel d =
        match d with
        | Investigations -> "Investigations"
        | Administration -> "Administration"
        | Settings -> "Settings"

    let destinations = [ Investigations; Administration; Settings ]

    let private guidText (InvestigationId g) = g.ToString("D")

    let roleSlug r =
        match r with
        | WorkspaceRole.Catalog -> "catalog"
        | WorkspaceRole.WorkingCopy -> "working-copy"

    let private refQuery (r: InvestigationRef) =
        let (Workspace ws) = r.Address.Workspace
        sprintf "ws=%s&role=%s" (Uri.EscapeDataString ws) (roleSlug r.Address.Role)

    /// Canonical hash route. Defaults are omitted (URL contract "canonical form"): the overview tab has no `tab`.
    let format route =
        match route with
        | InvestigationsRoute -> "#/"
        | StartRoute -> "#/investigations/new"
        | WorkspaceRoute(r, Overview) -> sprintf "#/investigations/%s?%s" (guidText r.Id) (refQuery r)
        | WorkspaceRoute(r, s) -> sprintf "#/investigations/%s?%s&tab=%s" (guidText r.Id) (refQuery r) (sectionSlug s)
        | AdministrationRoute -> "#/administration"
        | SettingsRoute -> "#/settings"
        | NotFoundRoute r -> "#/" + r.TrimStart('#', '/')

    let parse (hash: string) : UiRoute =
        let raw = hash.TrimStart('#')
        let path, query =
            match raw.IndexOf('?') with
            | -1 -> raw, ""
            | i -> raw.Substring(0, i), raw.Substring(i + 1)
        let param name =
            query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            |> Array.tryPick (fun kv -> match kv.Split('=') with [| k; v |] when k = name -> Some(Uri.UnescapeDataString v) | _ -> None)
        let tab = param "tab"
        let address =
            match param "ws", param "role" with
            | Some ws, Some "catalog" when ws <> "" -> Some { Workspace = Workspace ws; Role = WorkspaceRole.Catalog }
            | Some ws, Some "working-copy" when ws <> "" -> Some { Workspace = Workspace ws; Role = WorkspaceRole.WorkingCopy }
            | _ -> None
        let section =
            match tab with
            | None -> Some Overview
            | Some t -> sections |> List.tryFind (fun s -> sectionSlug s = t)
        match path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
        | [] -> InvestigationsRoute
        | [ "investigations"; "new" ] -> StartRoute
        | [ "investigations"; id ] ->
            match Guid.TryParse id, address, section with
            | (true, g), Some a, Some s -> WorkspaceRoute({ Address = a; Id = InvestigationId g }, s)
            | _ -> NotFoundRoute raw
        | [ "administration" ] -> AdministrationRoute
        | [ "settings" ] -> SettingsRoute
        | _ -> NotFoundRoute raw

    let destinationOf route =
        match route with
        | InvestigationsRoute | StartRoute | WorkspaceRoute _ | NotFoundRoute _ -> Investigations
        | AdministrationRoute -> Administration
        | SettingsRoute -> Settings

    /// The primary navigation for a route: present on every screen (INV-041), exactly one current.
    let primary route : NavLink<PrimaryDestination> list =
        let hrefOf d =
            match d with
            | Investigations -> format InvestigationsRoute
            | Administration -> format AdministrationRoute
            | Settings -> format SettingsRoute
        destinations
        |> List.map (fun d -> { Destination = d; Label = destinationLabel d; Href = hrefOf d; Current = destinationOf route = d })

    /// The section navigation of a workspace; Corrective Actions appears only when the projection supports it.
    let sectionLinks (id: InvestigationRef) (current: Section) (correctiveActionsSupported: bool) : NavLink<Section> list =
        sections
        |> List.filter (fun s -> s <> CorrectiveActions || correctiveActionsSupported)
        |> List.map (fun s -> { Destination = s; Label = sectionLabel s; Href = format (WorkspaceRoute(id, s)); Current = s = current })
