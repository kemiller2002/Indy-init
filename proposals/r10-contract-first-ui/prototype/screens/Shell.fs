/// Shell bindings every screen shares: the primary navigation (INV-041, UI-020) and the fixture marker. In the frozen
/// layout the shell is ui's Views/Shell.fs; here it is the prototype's, consumed read-only by the two screens.
module Causa.Ui.Prototype.Shell

open Causa.Ui.Contracts

let private slug d =
    match d with
    | Investigations -> "investigations"
    | Administration -> "administration"
    | Settings -> "settings"

let bindings (view: AppView) : (string * BindingValue) list =
    [ for link in view.Primary do
          yield Bindings.text (sprintf "nav-%s-current" (slug link.Destination)) (if link.Current then "page" else "false")
      yield
          Bindings.text
              "origin"
              (match view.Origin with
               | FixtureBackend l -> FixtureLabel.value l
               | IntegratedBackend -> "")
      yield Bindings.flag "is-fixture" (match view.Origin with FixtureBackend _ -> true | IntegratedBackend -> false) ]
