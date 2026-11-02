/// Package v4.1.2, the successor amendment of v4.1.1. Until the owner records ODR-V412-01..03 the committed inputs
/// fail only for the approvals they cannot yet name; with them named they validate. The owner-record check ties the
/// package to exactly the content each selected option lists under 'applies' (transitions field by field, contract
/// digests), to real operator records for every approval the amendment names, and to an approvals file no agent
/// commit has touched since the v4.1.2 requests were introduced. Each compiler rule v4.1.2 relies on rejects the input
/// it guards against: X12 (`replaces`), X17 (declared contract changes), X20 (successor versions).
module V412Tests

open System.IO
open Json

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let private mapArr (key: string) (f: Json list -> Json list) (j: Json) = setIn key (JArr(f (arr key j))) j

let private without (keys: string list) (j: Json) =
    match j with
    | JObj ps -> JObj(ps |> List.filter (fun (k, _) -> not (List.contains k keys)))
    | j -> j

let inputs (source: string) : V41Package.Inputs =
    Program.v41Inputs source (Path.Combine(source, "requirements-compiled", "v4.1.2")) (Path.Combine(source, "requirements-compiled", "v4")) None

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
let private mapTransitions (f: Json list -> Json list) (i: V41Package.Inputs) = { i with MachineAmendments = i.MachineAmendments |> mapArr "transitions" f }

let private isV412 (j: Json) = (str "decision" j).StartsWith "ODR-V412"

/// The approval id a test names for the owner's not-yet-recorded decisions, and that record as it will read.
let private simulatedApproval = "APR-TEST-OWNER"

let private simulatedRecords (real: Json list) =
    real
    @ [ JObj
            [ "id", JStr simulatedApproval
              "authority", JObj [ "kind", JStr "operator"; "id", JStr "kemiller2002" ]
              "changes", JArr [ for d in [ "ODR-V412-01"; "ODR-V412-02"; "ODR-V412-03" ] -> JObj [ "decision_id", JStr d; "action", JStr "select"; "option", JStr "A" ] ] ] ]

/// Every v4.1.2 amendment and contract change names the given approval (the test stand-in before the owner records).
let private naming (approval: string) (i: V41Package.Inputs) =
    i
    |> mapTransitions (List.map (fun t -> if isV412 t && str "approval" t = "" then setIn "approval" (JStr approval) t else t))
    |> mapContractChanges (List.map (fun c -> if str "approval" c = "" then setIn "approval" (JStr approval) c else c))

let private approvalsPath = "kickoff/rehearsal-007/approvals.causa.json"
let private requestsOf (i: V41Package.Inputs) =
    i.AuthoredText |> List.tryFind (fst >> (=) "proposals/owner-decision-requests.json")
    |> Option.map (snd >> System.Text.Encoding.UTF8.GetBytes >> parseBytes >> arr "requests") |> Option.defaultValue []

/// The owner's record against the package (RDP-003), for a given approvals list.
let ownerRecordsWith (approvals: Json list) (i: V41Package.Inputs) : string list =
    let operator (a: Json) = (field "authority" a |> Option.bind (strOpt "kind")) = Some "operator"
    let byId id = approvals |> List.tryFind (fun a -> strOpt "id" a = Some id) |> Option.filter operator
    let selected (approvalId: string) (decision: string) =
        byId approvalId |> Option.bind (fun a -> arr "changes" a |> List.tryFind (fun c -> strOpt "decision_id" c = Some decision)) |> Option.bind (strOpt "option")
    let requests = requestsOf i
    let applies (decision: string) (option: string) =
        requests |> List.tryFind (fun r -> str "id" r = decision) |> Option.bind (field "applies") |> Option.bind (field option)
    let transitions = i.MachineAmendments |> arr "transitions" |> List.filter isV412
    let contractChanges = i.Amendment |> Option.map (arr "contractChanges") |> Option.defaultValue []
    let decisions = (transitions @ contractChanges) |> List.map (str "decision") |> List.distinct
    let amendment = i.Amendment |> Option.defaultValue (JObj [])
    let approvesPackage (approvalId: string) (package: string) =
        byId approvalId |> Option.exists (fun a -> arr "changes" a |> List.exists (fun c -> strOpt "action" c = Some "approve-package" && strOpt "package" c = Some package))
    [ // The approvals the amendment names for the package it amends are the owner's approvals of that package.
      for key in [ "amends"; "contractBaseline" ] do
          let x = field key amendment |> Option.defaultValue (JObj [])
          if not (approvesPackage (str "approval" x) (str "package" x)) then
              yield sprintf "v4.1.2: %s names approval %s, which is no operator approval of %s" key (str "approval" x) (str "package" x)
      for decision in decisions do
          let ts = transitions |> List.filter (fun t -> str "decision" t = decision)
          let cs = contractChanges |> List.filter (fun c -> str "decision" c = decision)
          match (ts @ cs) |> List.map (fun x -> selected (str "approval" x) decision) |> List.distinct with
          | [ Some option ] ->
              match applies decision option with
              | None -> yield sprintf "v4.1.2: option %s of %s lists nothing under 'applies', yet the package applies it" option decision
              | Some a ->
                  let expectedT = arr "transitions" a |> Set.ofList
                  let actualT = ts |> List.map (without [ "decision"; "approval" ]) |> Set.ofList
                  if expectedT <> actualT || ts.Length <> expectedT.Count then
                      yield sprintf "v4.1.2: the transitions citing %s are not exactly those option %s applies (%d applied, %d listed, %d differ)" decision option ts.Length expectedT.Count (Set.count (Set.union (Set.difference expectedT actualT) (Set.difference actualT expectedT)))
                  let expectedC = arr "contracts" a |> List.map (fun c -> str "file" c, str "sha256" c) |> Set.ofList
                  let actualC = cs |> List.map (fun c -> str "file" c, str "sha256" c) |> Set.ofList
                  if expectedC <> actualC then yield sprintf "v4.1.2: the contract changes citing %s are %A; option %s applies %A" decision (Set.toList actualC) option (Set.toList expectedC)
          | _ -> yield sprintf "v4.1.2: the changes citing %s are not all selected by one operator approval of it" decision
      // Every recorded selection whose option applies something is applied.
      for a in approvals |> List.filter operator do
          for c in arr "changes" a do
              let d, o = strOpt "decision_id" c |> Option.defaultValue "", strOpt "option" c |> Option.defaultValue ""
              match applies d o with
              | Some x when not ((arr "transitions" x).IsEmpty && (arr "contracts" x).IsEmpty) && not (List.contains d decisions) ->
                  yield sprintf "v4.1.2: %s selects option %s of %s, but the package applies none of it" (str "id" a) o d
              | _ -> () ]

/// The real record: the committed approvals file, and no agent commit on it since the v4.1.2 requests were introduced.
let ownerRecords (source: string) (i: V41Package.Inputs) : string list =
    let approvals = parseFile (Path.Combine(source, approvalsPath)) |> arr "approvals"
    let since =
        match Git.run source [ "log"; "--diff-filter=A"; "--format=%H"; "--"; "requirements-compiled/v4.1.2/proposals/owner-decision-requests.json" ] with
        | Ok b -> (System.Text.Encoding.UTF8.GetString b).Trim().Split('\n') |> Array.last
        | Error _ -> ""
    let bodies =
        if since = "" then [ "the v4.1.2 requests' first commit is not found" ]
        else
            match Git.run source [ "log"; "--format=%B%x00"; since + "..HEAD"; "--"; approvalsPath ] with
            | Ok b -> (System.Text.Encoding.UTF8.GetString b).Split('\000') |> List.ofArray
            | Error e -> [ e ]
    [ if bodies |> List.exists (fun m -> m.Contains "Claude-Session:" || m.Contains "Co-Authored-By: Claude" || m.Contains "not found") then
          yield "v4.1.2: an agent commit changed the approvals file after the v4.1.2 requests were introduced"
      yield! ownerRecordsWith approvals i ]

/// The committed inputs fail only for the approvals the owner has not yet recorded; named, they validate and apply
/// exactly what the simulated A/A/A record selects. Once the owner's record exists, the real record must pass.
let scope (source: string) : string list =
    let committed = inputs source
    let realApprovals = parseFile (Path.Combine(source, approvalsPath)) |> arr "approvals"
    let recorded = realApprovals |> List.exists (fun a -> arr "changes" a |> List.exists (fun c -> (strOpt "decision_id" c |> Option.defaultValue "").StartsWith "ODR-V412-0"))
    let pendingInputs = committed.MachineAmendments |> arr "transitions" |> List.exists (fun t -> isV412 t && str "approval" t = "")
    let named = naming simulatedApproval committed
    let saveMachine = V41Package.amendedMachines named |> List.find (fun m -> str "id" m = "SM-SAVE")
    let transition id = arr "transitions" saveMachine |> List.tryFind (fun t -> str "id" t = id)
    [ if pendingInputs then
          match failing committed with
          | [ "X12"; "X17" ] | [ "X17"; "X12" ] when (problemsOf "X12" committed @ problemsOf "X17" committed) |> List.forall (fun p -> p.EndsWith "names no operator approval") -> ()
          | other -> yield sprintf "v4.1.2: the committed inputs should fail only X12 and X17 for the approvals not yet recorded, not %A" other
          for c in (V41Package.build named).Checks do
              if not c.Problems.IsEmpty then yield sprintf "v4.1.2: %s fails with the approvals named: %A" c.Id (List.truncate 3 c.Problems)
          yield! ownerRecordsWith (simulatedRecords realApprovals) named
      else
          for c in (V41Package.build committed).Checks do
              if not c.Problems.IsEmpty then yield sprintf "v4.1.2: %s fails on the committed package: %A" c.Id (List.truncate 3 c.Problems)
          yield! ownerRecords source committed
      if recorded && pendingInputs then yield "v4.1.2: the owner has recorded ODR-V412 decisions; compile the package from them (FREEZE.md step 2)"
      // ODR-V412-01 and -03 as decided: every read path windowed, T-S47 no longer promises a read on return.
      for id, guardPart in [ "T-S31", "automatic reads spent and fewer than four reconciliation reads sent"; "T-S32", "automatic reads spent and fewer than four reconciliation reads sent"
                             "T-S62", "four reconciliation reads already sent"; "T-S63", "four reconciliation reads already sent"
                             "T-S65", "automatic reads left"; "T-S64", "three automatic reads already made" ] do
          match transition id with
          | Some t when (str "guard" t).Contains guardPart -> ()
          | Some t -> yield sprintf "v4.1.2: %s has guard '%s'" id (str "guard" t)
          | None -> yield sprintf "v4.1.2: %s is missing from SM-SAVE" id
      match transition "T-S47" with
      | Some t when strs "effects" t |> List.exists (fun e -> e.Contains "one read on return") -> yield "v4.1.2: T-S47 still promises a read on return"
      | Some _ -> ()
      | None -> yield "v4.1.2: T-S47 is missing"
      let v4Count = arr "machines" named.StateMachines |> List.find (fun m -> str "id" m = "SM-SAVE") |> arr "transitions" |> List.length
      if (arr "transitions" saveMachine).Length <> v4Count + 5 then
          yield sprintf "v4.1.2: SM-SAVE has %d transitions; v4's %d plus T-S61..T-S65 is %d" (arr "transitions" saveMachine).Length v4Count (v4Count + 5)
      // Every read path is windowed: ReconcileDue's contract states the window.
      let save = arr "interfaces" named.Interfaces |> List.find (fun x -> str "id" x = "IF41-SAVE") |> str "fsharp"
      if not (save.Contains "ReconcileDue" && save.Contains "only while fewer than four reconciliation reads were sent") then yield "v4.1.2: ReconcileDue does not state the INV-04 window" ]

let controls (source: string) : string list =
    let i = naming simulatedApproval (inputs source)
    let replacing id = i.MachineAmendments |> arr "transitions" |> List.exists (fun t -> str "id" t = id && field "replaces" t = Some(JBool true))
    [ for id in [ "T-S31"; "T-S32"; "T-S47" ] do
          if not (replacing id) then yield sprintf "v4.1.2: %s must be a declared replacement" id
      // X12: a replacement must name a v4 transition; a v4 id must not be reused without replacing.
      yield! expectFail "replaces names no v4 transition" "X12" (mapTransitions (List.map (fun t -> if str "id" t = "T-S31" then setIn "id" (JStr "T-S99") t else t)) i)
      yield! expectFail "a v4 id reused without replaces" "X12" (mapTransitions (List.map (fun t -> if str "id" t = "T-S32" then setIn "replaces" (JBool false) t else t)) i)
      // X17: every changed contract is declared once, with its exact digest, an authorizing decision and an approval.
      yield! expectFail "an undeclared contract change" "X17" (mapContractChanges (List.filter (fun c -> str "file" c <> "contracts/03-IF41-WIRE.fs")) i)
      yield! expectFail "a declared change pinned to another digest" "X17" (mapContractChanges (List.map (fun c -> if str "file" c = "contracts/00-DOMAIN-STUBS.fs" then setIn "sha256" (JStr(String.replicate 64 "0")) c else c)) i)
      yield! expectFail "a stale declaration of an unchanged contract" "X17" (mapContractChanges (fun cs -> cs @ [ JObj [ "file", JStr "contracts/01-IF41-DOMAIN.fs"; "sha256", JStr(String.replicate 64 "0"); "decision", JStr "ODR-V412-02"; "approval", JStr simulatedApproval; "summary", JStr "x" ] ]) i)
      yield! expectFail "a contract change authorized by a package approval" "X17" (mapContractChanges (List.map (fun c -> setIn "decision" (JStr "ODR-V412-04") c)) i)
      yield! expectFail "a contract change declared twice" "X17" (mapContractChanges (fun cs -> cs @ [ List.head cs ]) i)
      yield! expectFail "a contract change naming no approval" "X17" (mapContractChanges (List.map (fun c -> setIn "approval" (JStr "") c)) i)
      // X20: the version follows the package it amends, and names it consistently.
      yield! expectFail "a version that does not follow the amended one" "X20" (mapAmendment (setIn "packageVersion" (JStr "4.1.1")) i)
      yield! expectFail "an amended version that disagrees with its package" "X20" (mapAmendment (fun a -> setIn "amends" (field "amends" a |> Option.defaultValue (JObj []) |> setIn "packageVersion" (JStr "4.1.0")) a) i) ]

/// The owner-record check rejects content other than what the selected options list, and unreal approvals.
let recordControls (source: string) : string list =
    let i = naming simulatedApproval (inputs source)
    let records = simulatedRecords (parseFile (Path.Combine(source, approvalsPath)) |> arr "approvals")
    let recordFails name (j: V41Package.Inputs) = [ if (try (ownerRecordsWith records j).IsEmpty with _ -> true) then yield sprintf "v4.1.2 owner-record control '%s' passed" name ]
    let edit id f = mapTransitions (List.map (fun t -> if str "id" t = id then f t else t))
    [ yield! [ for p in ownerRecordsWith records i -> sprintf "v4.1.2: the simulated A/A/A record fails: %s" p ]
      // RV412-1-4: content, not just ids.
      yield! recordFails "T-S32 retargeted to SaveFailed" (edit "T-S32" (setIn "to" (JStr "SaveFailed")) i)
      yield! recordFails "T-S31 reading twice" (edit "T-S31" (setIn "effects" (JArr [ JStr "two reconciliation reads" ])) i)
      yield! recordFails "T-S64 resending the mutation" (edit "T-S64" (setIn "effects" (JArr [ JStr "resend the mutation" ])) i)
      yield! recordFails "an extra transition" (mapTransitions (fun ts -> ts @ [ ts |> List.find (fun t -> str "id" t = "T-S64") |> setIn "id" (JStr "T-S66") ]) i)
      yield! recordFails "a missing transition" (mapTransitions (List.filter (fun t -> str "id" t <> "T-S63")) i)
      yield! recordFails "an approval that records nothing" (edit "T-S64" (setIn "approval" (JStr "APR-NOBODY")) i)
      yield! recordFails "a contract change the option does not list" (mapContractChanges (List.map (fun c -> if str "file" c = "contracts/05-IF41-SAVE.fs" then setIn "decision" (JStr "ODR-V412-02") c else c)) i)
      yield! recordFails "a contract digest the option does not list" (mapContractChanges (List.map (fun c -> if str "file" c = "contracts/05-IF41-SAVE.fs" then setIn "sha256" (JStr(String.replicate 64 "1")) c else c)) i)
      // RV412-1-5: the approvals of the amended package are real operator records.
      yield! recordFails "amends naming an unreal approval" (mapAmendment (fun a -> setIn "amends" (field "amends" a |> Option.defaultValue (JObj []) |> setIn "approval" (JStr "APR-NOBODY")) a) i)
      yield! recordFails "a contract baseline naming an unreal approval" (mapAmendment (fun a -> setIn "contractBaseline" (field "contractBaseline" a |> Option.defaultValue (JObj []) |> setIn "approval" (JStr "APR-NOBODY")) a) i) ]

let all (source: string) : string list = scope source @ controls source @ recordControls source
