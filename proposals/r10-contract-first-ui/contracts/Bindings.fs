namespace Causa.Ui.Contracts

/// Limen 0.9.1's view-state shape (limen.core contract, guests/fsharp/Limen.Contract Core.fs ViewValue/ViewPrimitive),
/// mirrored case for case so this library compiles before the integration lane adds EchelonFoundry.Limen.Contract at
/// T10. Integration replaces these two types with aliases of Limen.Contract.Core.ViewValue/ViewPrimitive; no screen
/// code changes. A screen module's whole output is a Bindings map.
[<RequireQualifiedAccess>]
type BindingPrimitive =
    | Text of string
    | Number of float
    | Flag of bool

[<RequireQualifiedAccess>]
type BindingValue =
    | Text of string
    | Number of float
    | Flag of bool
    | Items of Map<string, BindingPrimitive> list

type Bindings = Map<string, BindingValue>

[<RequireQualifiedAccess>]
module Bindings =
    let text (k: string) v : string * BindingValue = k, BindingValue.Text v
    let flag (k: string) v : string * BindingValue = k, BindingValue.Flag v
    let number (k: string) (v: float) : string * BindingValue = k, BindingValue.Number v
    let items (k: string) (rows: (string * BindingPrimitive) list list) : string * BindingValue =
        k, BindingValue.Items(rows |> List.map Map.ofList)

    /// Every key of a binding map (top level and row fields), for the contract test that no binding names a credential.
    let keys (b: Bindings) =
        b
        |> Map.toList
        |> List.collect (fun (k, v) ->
            match v with
            | BindingValue.Items rows -> k :: (rows |> List.collect (Map.toList >> List.map fst))
            | _ -> [ k ])
        |> List.distinct

    /// Every text value, for the copy checks (UI-017, UI-023, credential never rendered).
    let texts (b: Bindings) =
        b
        |> Map.toList
        |> List.collect (fun (_, v) ->
            match v with
            | BindingValue.Text s -> [ s ]
            | BindingValue.Items rows ->
                rows |> List.collect (Map.toList >> List.choose (fun (_, p) -> match p with BindingPrimitive.Text s -> Some s | _ -> None))
            | _ -> [])
