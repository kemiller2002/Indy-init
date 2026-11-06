namespace Causa.ContractReadiness

open System.IO

/// A wire event variant: the discriminator, refined by `command` or `state` where the domain union splits one wire type
/// into several cases, bound to the successor `CoreEvent` case that represents it.
type EventVariant =
    { VariantId: string
      Name: string
      EventType: string
      Discriminator: (string * J) option
      CoreEventCase: string option
      Status: string
      Note: string option }

type CommandInput =
    { FieldId: string
      Source: string }

type CommandDef =
    { CommandId: string
      Name: string
      Layer: string
      ContractUnion: string option
      ContractCase: string option
      Status: string
      Inputs: CommandInput list
      Preconditions: string list
      ValidationErrors: J list
      Transitions: string list
      ResultEvents: string list
      Idempotency: string
      Authorization: string
      PersistenceEffect: string
      Tests: string list
      BlockedBy: string list
      Sources: string list }

type DomainTransition =
    { TransitionId: string
      EventType: string
      When: (string * J) option
      From: string list
      To: string
      Source: string }

type DomainMachine =
    { MachineId: string
      Entity: string
      StateEnum: string
      IdField: string option
      FromField: string option
      TargetField: string option
      Creation: (string * string) list
      Transitions: DomainTransition list }

type DurabilityState =
    { StateId: string
      Name: string
      SaveStates: string list
      Durable: bool
      MayDisplaySaved: bool
      Evidence: string }

type UiBinding =
    { BindingId: string
      RequirementId: string
      ScreenId: string
      Kind: string
      Label: string
      AccessibleName: string
      InputType: string option
      Required: bool
      Validation: J
      CommandId: string option
      WriteFields: string list
      ReadBackFields: string list
      States: string list
      Mobile: string list
      Tests: string list
      Status: string
      BlockedBy: string list
      Note: string option }

type OwnerDecision =
    { DecisionId: string
      Title: string
      Blocking: bool
      Status: string
      Affects: string list }

type DomainEntity =
    { TypeId: string
      Name: string
      FsharpType: string
      Fields: string list
      Status: string
      BlockedBy: string list }

type ContractPackage =
    { Dir: string
      Version: string
      Variants: EventVariant list
      EnvelopeBinding: (string * string list) list
      Commands: CommandDef list
      Machines: DomainMachine list
      Durability: DurabilityState list
      PersistenceMachines: string list
      PersistenceSource: string * string
      Bindings: UiBinding list
      Entities: DomainEntity list
      ProposedTypes: ObjectTypeDef list
      ProposedFields: RegistryField list
      Decisions: OwnerDecision list }

module Package =
    let private strList name j = Json.strs name j

    let private variantOf (j: J) =
        { VariantId = Json.fieldStr "variantId" j
          Name = Json.fieldStr "name" j
          EventType = Json.fieldStr "eventType" j
          Discriminator =
            match Json.tryField "discriminator" j with
            | Some(JObj _ as d) -> Some(Json.fieldStr "path" d, Json.field "value" d)
            | _ -> None
          CoreEventCase = Json.strOpt "coreEventCase" j
          Status = Json.fieldStr "status" j
          Note = Json.strOpt "note" j }

    let private commandOf (j: J) =
        { CommandId = Json.fieldStr "commandId" j
          Name = Json.fieldStr "name" j
          Layer = Json.fieldStr "layer" j
          ContractUnion = Json.strOpt "contractUnion" j
          ContractCase = Json.strOpt "contractCase" j
          Status = Json.fieldStr "status" j
          Inputs = Json.arrOf "inputs" j |> List.map (fun i -> { FieldId = Json.fieldStr "fieldId" i; Source = Json.fieldStr "source" i })
          Preconditions = strList "preconditions" j
          ValidationErrors = Json.arrOf "validationErrors" j
          Transitions = strList "transitions" j
          ResultEvents = strList "resultEvents" j
          Idempotency = Json.strOpt "idempotency" j |> Option.defaultValue ""
          Authorization = Json.strOpt "authorization" j |> Option.defaultValue ""
          PersistenceEffect = Json.strOpt "persistenceEffect" j |> Option.defaultValue ""
          Tests = strList "tests" j
          BlockedBy = strList "blockedBy" j
          Sources = strList "sources" j }

    let private machineOf (j: J) =
        { MachineId = Json.fieldStr "machineId" j
          Entity = Json.fieldStr "entity" j
          StateEnum = Json.fieldStr "stateEnum" j
          IdField = Json.strOpt "idField" j
          FromField = Json.strOpt "fromField" j
          TargetField = Json.strOpt "targetField" j
          Creation = Json.arrOf "creation" j |> List.map (fun c -> Json.fieldStr "eventType" c, Json.fieldStr "state" c)
          Transitions =
            Json.arrOf "transitions" j
            |> List.map (fun t ->
                { TransitionId = Json.fieldStr "transitionId" t
                  EventType = Json.fieldStr "eventType" t
                  When =
                    match Json.tryField "when" t with
                    | Some(JObj _ as w) -> Some(Json.fieldStr "path" w, Json.field "value" w)
                    | _ -> None
                  From = strList "from" t
                  To = Json.fieldStr "to" t
                  Source = Json.fieldStr "source" t }) }

    let private bindingOf (j: J) =
        { BindingId = Json.fieldStr "bindingId" j
          RequirementId = Json.fieldStr "requirementId" j
          ScreenId = Json.fieldStr "screenId" j
          Kind = Json.fieldStr "kind" j
          Label = Json.fieldStr "label" j
          AccessibleName = Json.fieldStr "accessibleName" j
          InputType = Json.strOpt "inputType" j
          Required = Json.boolOf "required" j
          Validation = Json.tryField "validation" j |> Option.defaultValue (JObj [])
          CommandId = Json.strOpt "commandId" j
          WriteFields = strList "writeFields" j
          ReadBackFields = strList "readBackFields" j
          States = strList "states" j
          Mobile = strList "mobile" j
          Tests = strList "tests" j
          Status = Json.fieldStr "status" j
          BlockedBy = strList "blockedBy" j
          Note = Json.strOpt "note" j }

    let private proposedOf (ext: J) =
        let types =
            Json.arrOf "objectTypes" ext
            |> List.map (fun t ->
                { TypeId = Json.fieldStr "typeId" t
                  Name = Json.fieldStr "typeId" t
                  SchemaFile = "(proposed) contract/1.1.0/schemas/" + Json.fieldStr "schemaFile" t
                  SchemaSha256 = ""
                  Description = Json.fieldStr "description" t })
        let fields =
            Json.arrOf "objectTypes" ext
            |> List.collect (fun t ->
                let typeId = Json.fieldStr "typeId" t
                Json.arrOf "fields" t
                |> List.map (fun f ->
                    let name = Json.fieldStr "name" f
                    { Def =
                        { FieldId = sprintf "F.%s.%s" typeId name
                          ObjectType = typeId
                          Name = name
                          Kind = Schema.kindOfJson (Json.field "kind" f)
                          Presence = Always
                          SchemaFile = "(proposed) contract/1.1.0/schemas/" + Json.fieldStr "schemaFile" t }
                      Status = FieldRegistry.statusProposed
                      FsharpElement = Json.strOpt "fsharpElement" f
                      FsharpType = Json.strOpt "fsharpType" f
                      Meaning = Json.strOpt "meaning" f }))
        types, fields

    /// Every decision the owner has recorded in the approvals file: decision_id -> (approval id, option or action).
    let recordedDecisions (approvalsPath: string) : Map<string, string * string * (string * string) list> =
        if not (File.Exists approvalsPath) then Map.empty
        else
            Json.arrOf "approvals" (Json.parseFile approvalsPath)
            |> List.collect (fun a ->
                let id = Json.strOpt "id" a |> Option.defaultValue "?"
                Json.arrOf "changes" a
                |> List.choose (fun c ->
                    Json.strOpt "decision_id" c
                    |> Option.map (fun d ->
                        d,
                        (id,
                         Json.strOpt "option" c |> Option.defaultValue (Json.strOpt "action" c |> Option.defaultValue ""),
                         Json.arrOf "pins" c |> List.map (fun p -> Json.fieldStr "path" p, Json.fieldStr "sha256" p)))))
            |> List.fold (fun m (d, v) -> Map.add d v m) Map.empty

    /// A decision's status comes from the owner's approvals file, never from this package: a request is open until the
    /// owner records it (RDP-003).
    /// `status` gives a decision's status from the owner's records: recorded, recorded but stale (a file it pinned has
    /// changed since, so the owner must confirm it again), or None (still open as written in the request).
    let load (dir: string) (status: string -> string option) : ContractPackage =
        let read name = Json.parseFile (Path.Combine(dir, name))
        let cer = read "COMMAND-EVENT-REGISTRY.json"
        let st = read "STATE-TRANSITIONS.json"
        let ui = read "UI-FIELD-MAP.json"
        let dc = read "DOMAIN-CONTRACT.json"
        let od = read "OWNER-DECISIONS.proposed.json"
        let ptypes, pfields = proposedOf (Json.field "proposedStorageExtension" dc)
        let pm = Json.field "persistenceMachines" st
        { Dir = dir
          Version = Json.fieldStr "contractPackageVersion" dc
          Variants = Json.arrOf "eventVariants" cer |> List.map variantOf
          EnvelopeBinding =
            match Json.field "fields" (Json.field "envelopeBinding" cer) with
            | JObj ms -> ms |> List.map (fun (k, v) -> k, Json.arr v |> List.map Json.str)
            | _ -> []
          Commands = Json.arrOf "commands" cer |> List.map commandOf
          Machines = Json.arrOf "domainMachines" st |> List.map machineOf
          Durability =
            Json.arrOf "saveDurability" st
            |> List.map (fun d ->
                { StateId = Json.fieldStr "stateId" d
                  Name = Json.fieldStr "name" d
                  SaveStates = strList "saveStates" d
                  Durable = Json.boolOf "durable" d
                  MayDisplaySaved = Json.boolOf "mayDisplaySaved" d
                  Evidence = Json.fieldStr "evidence" d })
          PersistenceMachines = strList "machines" pm
          PersistenceSource = Json.fieldStr "source" pm, Json.fieldStr "sha256" pm
          Bindings = Json.arrOf "bindings" ui |> List.map bindingOf
          Entities =
            Json.arrOf "entities" dc
            |> List.map (fun e ->
                { TypeId = Json.fieldStr "typeId" e
                  Name = Json.fieldStr "name" e
                  FsharpType = Json.fieldStr "fsharpType" e
                  Fields = strList "fieldIds" e
                  Status = Json.fieldStr "status" e
                  BlockedBy = strList "blockedBy" e })
          ProposedTypes = ptypes
          ProposedFields = pfields
          Decisions =
            Json.arrOf "decisions" od
            |> List.map (fun d ->
                { DecisionId = Json.fieldStr "decisionId" d
                  Title = Json.fieldStr "title" d
                  Blocking = Json.boolOf "blocking" d
                  Status =
                    status (Json.fieldStr "decisionId" d) |> Option.defaultValue (Json.fieldStr "status" d)
                  Affects = strList "affects" d }) }

    let variantOfEvent (variants: EventVariant list) (eventType: string) (payload: TV) : EventVariant option =
        variants
        |> List.filter (fun v -> v.EventType = eventType && v.Status = FieldRegistry.statusWire)
        |> List.tryFind (fun v ->
            match v.Discriminator with
            | None -> true
            | Some(path, value) -> Codec.at path payload |> Option.map Codec.encode = Some value)
