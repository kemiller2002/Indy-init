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
    | WorkspaceRoute of RouteTarget * Section
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
