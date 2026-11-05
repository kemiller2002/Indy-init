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
