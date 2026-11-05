/// Hash routes (D-203: `index.html#/...`, relative, so a reloaded deep link never 404s on static hosting). The route is
/// a projection of the model; a browser navigation (Back, Forward, a pasted link) is a message the engine interprets.
module Causa.Preview.Routing

open Causa.HistoricalCompatibility
open Causa.Preview.Model

type Route =
    | RConnect
    | RList
    | RInvestigation of Source * dir: string
    | RNew

let toHash =
    function
    | RConnect -> "#/"
    | RList -> "#/investigations"
    | RInvestigation(Catalog, d) -> $"#/investigations/catalog/{d}"
    | RInvestigation(WorkingCopy, d) -> $"#/investigations/working/{d}"
    | RNew -> "#/new"

let parse (hash: string) : Route =
    match hash.TrimStart('#').Trim('/').Split('/') with
    | [| "investigations" |] -> RList
    | [| "investigations"; "catalog"; d |] when (Uuid.tryParse d).IsSome -> RInvestigation(Catalog, d)
    | [| "investigations"; "working"; d |] when (Uuid.tryParse d).IsSome -> RInvestigation(WorkingCopy, d)
    | [| "new" |] -> RNew
    | _ -> RConnect

/// The route the current model stands for.
let ofModel (m: Model) : Route =
    match m.Screen, m.Opened with
    | ListScreen, _ -> RList
    | DetailScreen, Some o -> RInvestigation(o.Source, o.DirName)
    | CreateScreen, _ -> RNew
    | _ -> RConnect
