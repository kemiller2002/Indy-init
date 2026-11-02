/// Package v4.1.2, the successor amendment of v4.1.1: its committed inputs validate once each state-machine amendment
/// names the operator approval that records it, and stay invalid until then; each compiler rule v4.1.2 relies on
/// rejects the input it guards against. Those rules are: X12, a `replaces` that names no v4 transition and a v4 id
/// reused without `replaces`; X17, a contract change that is undeclared, pinned to another digest, stale, or authorized
/// by a decision of the wrong kind; X20, a successor whose version does not follow the package it amends.
module V412Tests

open System.IO
open Json

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let private mapArr (key: string) (f: Json list -> Json list) (j: Json) = setIn key (JArr(f (arr key j))) j

let inputs (source: string) : V41Package.Inputs =
    Program.v41Inputs source (Path.Combine(source, "requirements-compiled", "v4.1.2")) (Path.Combine(source, "requirements-compiled", "v4")) None

/// The inputs as they will be once the owner records ODR-V412-01..03: every amendment names an approval. The id is a
/// test value; the owner's real record is compared by ownerRecords once it exists.
let private approved (i: V41Package.Inputs) =
    { i with MachineAmendments = i.MachineAmendments |> mapArr "transitions" (List.map (fun t -> if strOpt "approval" t = Some "" then setIn "approval" (JStr "APR-TEST-OWNER") t else t)) }

let private failing (i: V41Package.Inputs) =
    (V41Package.build i).Checks |> List.filter (fun c -> not c.Problems.IsEmpty) |> List.map (fun c -> c.Id)

let private problemsOf (id: string) (i: V41Package.Inputs) =
    (V41Package.build i).Checks |> List.filter (fun c -> c.Id = id) |> List.collect (fun c -> c.Problems)

let private expectFail (name: string) (checkId: string) (i: V41Package.Inputs) : string list =
    try
        if List.contains checkId (failing i) then [] else [ sprintf "v4.1.2 control '%s': %s did not fail" name checkId ]
    with ex -> [ sprintf "v4.1.2 control '%s' threw: %s" name ex.Message ]

let private mapAmendment (f: Json -> Json) (i: V41Package.Inputs) = { i with Amendment = i.Amendment |> Option.map f }

let private mapContractChanges (f: Json list -> Json list) = mapAmendment (mapArr "contractChanges" f)

/// The inputs validate with approvals named and fail only for the missing approvals without them; the amended machine
/// holds exactly the decided transitions.
let scope (source: string) : string list =
    let committed = inputs source
    let i = approved committed
    let built = V41Package.build i
    let saveMachine = V41Package.amendedMachines i |> List.find (fun m -> str "id" m = "SM-SAVE")
    let transition id = arr "transitions" saveMachine |> List.tryFind (fun t -> str "id" t = id)
    [ for c in built.Checks do
          if not c.Problems.IsEmpty then yield sprintf "v4.1.2: %s fails with every amendment approved: %A" c.Id (List.truncate 3 c.Problems)
      // As committed (before the owner records the decisions) only the missing approvals fail.
      match failing committed with
      | [ "X12" ] when problemsOf "X12" committed |> List.forall (fun p -> p.EndsWith "names no operator approval") -> ()
      | other -> yield sprintf "v4.1.2: the committed inputs should fail only X12 for the missing approvals, not %A" other
      // ODR-V412-01 and -03 as decided: T-S31/T-S32 narrowed in place, T-S62..T-S64 added, nothing else.
      for id, guardPart in [ "T-S31", "fewer than four reconciliation reads sent"; "T-S32", "fewer than four reconciliation reads sent"
                             "T-S62", "four reconciliation reads already sent"; "T-S63", "four reconciliation reads already sent"
                             "T-S64", "three automatic reads already made" ] do
          match transition id with
          | Some t when (str "guard" t).Contains guardPart -> ()
          | Some t -> yield sprintf "v4.1.2: %s has guard '%s'" id (str "guard" t)
          | None -> yield sprintf "v4.1.2: %s is missing from SM-SAVE" id
      let v4Count = arr "machines" i.StateMachines |> List.find (fun m -> str "id" m = "SM-SAVE") |> arr "transitions" |> List.length
      if (arr "transitions" saveMachine).Length <> v4Count + 4 then
          yield sprintf "v4.1.2: SM-SAVE has %d transitions; v4's %d plus T-S61..T-S64 is %d" (arr "transitions" saveMachine).Length v4Count (v4Count + 4)
      if arr "transitions" saveMachine |> List.exists (fun t -> str "guard" t = "automatic reads spent") then yield "v4.1.2: v4's unbounded T-S31 guard survived" ]

let controls (source: string) : string list =
    let i = approved (inputs source)
    let replacing id = i.MachineAmendments |> arr "transitions" |> List.exists (fun t -> str "id" t = id && field "replaces" t = Some(JBool true))
    [ if not (replacing "T-S31" && replacing "T-S32") then yield "v4.1.2: T-S31 and T-S32 must be declared replacements"
      // X12: a replacement must name a v4 transition; a v4 id must not be reused without replacing.
      yield!
          expectFail "replaces names no v4 transition" "X12"
              { i with MachineAmendments = i.MachineAmendments |> mapArr "transitions" (List.map (fun t -> if str "id" t = "T-S31" then setIn "id" (JStr "T-S99") t else t)) }
      yield!
          expectFail "a v4 id reused without replaces" "X12"
              { i with MachineAmendments = i.MachineAmendments |> mapArr "transitions" (List.map (fun t -> if str "id" t = "T-S32" then setIn "replaces" (JBool false) t else t)) }
      // X17: every changed contract is declared with its exact digest and an authorizing decision.
      yield! expectFail "an undeclared contract change" "X17" (mapContractChanges (List.filter (fun c -> str "file" c <> "contracts/03-IF41-WIRE.fs")) i)
      yield!
          expectFail "a declared change pinned to another digest" "X17"
              (mapContractChanges (List.map (fun c -> if str "file" c = "contracts/00-DOMAIN-STUBS.fs" then setIn "sha256" (JStr(String.replicate 64 "0")) c else c)) i)
      yield!
          expectFail "a stale declaration of an unchanged contract" "X17"
              (mapContractChanges (fun cs -> cs @ [ JObj [ "file", JStr "contracts/01-IF41-DOMAIN.fs"; "sha256", JStr(String.replicate 64 "0"); "decision", JStr "ODR-V412-02"; "summary", JStr "x" ] ]) i)
      yield!
          expectFail "a contract change authorized by a package approval" "X17"
              (mapContractChanges (List.map (fun c -> setIn "decision" (JStr "ODR-V412-04") c)) i)
      // X20: the version follows the package it amends, and names it consistently.
      yield! expectFail "a version that does not follow the amended one" "X20" (mapAmendment (setIn "packageVersion" (JStr "4.1.1")) i)
      yield!
          expectFail "an amended version that disagrees with its package" "X20"
              (mapAmendment (fun a -> setIn "amends" (field "amends" a |> Option.defaultValue (JObj []) |> setIn "packageVersion" (JStr "4.1.0")) a) i) ]

let all (source: string) : string list = scope source @ controls source
