namespace Causa.ContractReadiness

open System
open System.IO
open System.Text.RegularExpressions

type Outcome =
    | Pass
    | Fail of string list
    | Unverified of string

type CheckResult =
    { CheckId: string
      Title: string
      Category: string
      Outcome: Outcome
      Metrics: (string * J) list }

/// Everything the checks read. Histories are produced by a function of the registry and variants, so a negative
/// control that removes a field or a variant re-decodes the data against the mutated contract.
type Inputs =
    { Registry: Registry
      GeneratedRegistryText: string
      CommittedRegistryText: string
      Package: ContractPackage
      V414: FsContract
      V415: FsContract
      LoadHistories: Registry -> EventVariant list -> HistoryFile list
      Catalogs: (string * (string * string) list) list
      ActiveCatalog: string
      DataRoot: string
      IndyRoot: string
      Catalog: J
      ScreenManifest: J
      UiMap: J
      SourcePin: J
      PackageTexts: (string * string) list }

module Checks =
    let private result id title category outcome metrics =
        { CheckId = id; Title = title; Category = category; Outcome = outcome; Metrics = metrics }

    let private ofProblems (problems: string list) = if List.isEmpty problems then Pass else Fail problems

    let private dupes (xs: string list) = xs |> List.countBy id |> List.filter (snd >> (<) 1) |> List.map fst

    let private baseName (s: string) = match s.IndexOf " (" with -1 -> s.Trim() | k -> s.Substring(0, k).Trim()

    let wireVariants (p: ContractPackage) = p.Variants |> List.filter (fun v -> v.Status = FieldRegistry.statusWire)

    // ---- Representability --------------------------------------------------------------------------------------------

    type RepresentationSummary =
        { Contract: string
          Total: int
          Lossless: int
          NoCase: int
          Lossy: int
          NoVariant: int
          EnvelopeDropped: string list
          NoCaseByVariant: (string * int) list
          LossyByVariant: (string * string list * int) list }

    let summarize (inputs: Inputs) (name: string) (contract: FsContract) (events: EventRecord list) =
        let envDropped = Historical.envelopeDropped inputs.Registry contract inputs.Package.EnvelopeBinding
        let reps =
            events
            |> List.map (fun e ->
                e,
                match e.Variant with
                | None -> NoVariant
                | Some v -> Historical.representPayload inputs.Registry contract v)
        let count f = reps |> List.filter (snd >> f) |> List.length
        { Contract = name
          Total = List.length events
          Lossless = reps |> List.filter (fun (_, r) -> match r with Lossless _ -> List.isEmpty envDropped | _ -> false) |> List.length
          NoCase = count (function NoCase _ -> true | _ -> false)
          Lossy = count (function Lossy _ -> true | _ -> false)
          NoVariant = count (function NoVariant -> true | _ -> false)
          EnvelopeDropped = envDropped
          NoCaseByVariant =
            reps |> List.choose (fun (e, r) -> match r with NoCase _ -> e.Variant |> Option.map (fun v -> v.Name) | _ -> None)
            |> List.countBy id |> List.sortBy fst
          LossyByVariant =
            reps |> List.choose (fun (e, r) -> match r, e.Variant with Lossy(_, d), Some v -> Some(v.Name, d) | _ -> None)
            |> List.countBy id |> List.map (fun ((n, d), c) -> n, d, c) |> List.sortBy (fun (n, _, _) -> n) }

    let summaryJson (s: RepresentationSummary) =
        Json.o
            [ "contract", Json.s s.Contract
              "eventsExamined", Json.i s.Total
              "losslessIncludingEnvelope", Json.i s.Lossless
              "payloadCaseMissing", Json.i s.NoCase
              "payloadCaseLossy", Json.i s.Lossy
              "noWireVariant", Json.i s.NoVariant
              "envelopeFieldsDropped", Json.ss s.EnvelopeDropped
              "eventsLosingEnvelopeFields", Json.i (if List.isEmpty s.EnvelopeDropped then 0 else s.Total)
              "missingCaseByVariant", Json.o (s.NoCaseByVariant |> List.map (fun (n, c) -> n, Json.i c))
              "lossyByVariant", JArr(s.LossyByVariant |> List.map (fun (n, d, c) -> Json.o [ "variant", Json.s n; "droppedFields", Json.ss d; "events", Json.i c ])) ]

    // ---- Helpers over the data -----------------------------------------------------------------------------------------

    let isHistorical (h: HistoryFile) = h.Catalogs |> List.contains "historical-v1" && h.Dir.StartsWith "workspaces/demo/historical-v1/"
    let isActiveCatalog (inputs: Inputs) (h: HistoryFile) =
        let cases = inputs.Catalogs |> List.tryFind (fst >> (=) inputs.ActiveCatalog) |> Option.map snd |> Option.defaultValue []
        cases |> List.exists (fst >> (=) h.Dir)

    let distinctHistories (hs: HistoryFile list) = hs |> List.distinctBy (fun h -> h.EventsSha256)

    let catalogRequirements (inputs: Inputs) = Json.arrOf "requirements" inputs.Catalog

    /// The per-requirement verification ids of SCREEN-MANIFEST.json (an object keyed INV-0nn somewhere in it).
    let screenManifestTests (sm: J) : Map<string, string list> =
        let rec find (j: J) : (string * string list) list =
            match j with
            | JObj ms when ms |> List.exists (fun (k, _) -> Regex.IsMatch(k, @"\AINV-\d{3}\z")) ->
                ms |> List.choose (fun (k, v) -> match v with JArr xs when Regex.IsMatch(k, @"\AINV-\d{3}\z") -> Some(k, xs |> List.choose (function JStr s -> Some s | _ -> None)) | _ -> None)
            | JObj ms -> ms |> List.collect (snd >> find)
            | JArr xs -> xs |> List.collect find
            | _ -> []
        find sm |> List.filter (fun (_, ts) -> ts |> List.exists (fun t -> t.Contains "/V")) |> Map.ofList

    // ---- The checks --------------------------------------------------------------------------------------------------

    let run (inputs: Inputs) : CheckResult list * Map<string, J> =
        let reg = inputs.Registry
        let pkg = inputs.Package
        let fieldIds = reg.Fields |> List.map (fun f -> f.Def.FieldId)
        let fieldSet = Set.ofList fieldIds
        let byId = FieldRegistry.byId reg
        let histories = inputs.LoadHistories reg pkg.Variants
        let distinct = distinctHistories histories
        let allEvents = distinct |> List.collect (fun h -> h.Events)
        let historical = histories |> List.filter isHistorical
        let historicalEvents = historical |> List.collect (fun h -> h.Events)
        let active = histories |> List.filter (isActiveCatalog inputs)
        let activeEvents = active |> List.collect (fun h -> h.Events)
        let variantNames = pkg.Variants |> List.map (fun v -> v.Name) |> Set.ofList
        let decisionIds = pkg.Decisions |> List.map (fun d -> d.DecisionId) |> Set.ofList
        let enumValues name = reg.Enums |> List.tryFind (fst >> (=) name) |> Option.map snd

        // Sources and reproducibility
        let pinChecks =
            let cases = Json.arrOf "cases" inputs.SourcePin
            let problems =
                cases
                |> List.choose (fun c ->
                    let id = Json.fieldStr "investigationId" c
                    let expected = Json.fieldStr "eventsSha256" c
                    match historical |> List.tryFind (fun h -> h.InvestigationId = id) with
                    | Some h when h.EventsSha256 = expected -> None
                    | Some h -> Some(sprintf "%s events sha256 %s, pinned %s" id h.EventsSha256 expected)
                    | None -> Some(sprintf "pinned case %s not found in historical-v1" id))
            result "CR-SRC-01" "Pinned historical data matches the source pin (causa-data e8259df, PR #126/#141 source-pin.json)" "sources" (ofProblems problems)
                [ "pinnedCases", Json.i (List.length cases) ]
        let reproducible =
            result "CR-REG-01" "FIELD-REGISTRY.json equals a fresh generation from the pinned schemas, FIELD-MAPPING.md and DOMAIN-CONTRACT.json" "reproducibility"
                (if inputs.GeneratedRegistryText = inputs.CommittedRegistryText then Pass
                 else Fail [ "committed FIELD-REGISTRY.json differs from the generated registry; run `generate`" ]) []

        // Registry
        let uniqueFields =
            result "CR-REG-02" "Field IDs are unique" "schema" (ofProblems (dupes fieldIds |> List.map (sprintf "duplicate field id %s")))
                [ "fields", Json.i (List.length fieldIds) ]
        let completeness =
            let typeIds = reg.ObjectTypes |> List.map (fun t -> t.TypeId) |> Set.ofList
            let rec kindProblems (fid: string) (k: Kind) =
                match k with
                | KObject t when not (typeIds.Contains t) -> [ sprintf "%s refers to unknown object type %s" fid t ]
                | KEnum(n, vs) when List.isEmpty vs -> [ sprintf "%s enum %s has no values" fid n ]
                | KNullable i -> kindProblems fid i
                | KList(i, _, _, _) -> kindProblems fid i
                | _ -> []
            let eventTypes = (FieldRegistry.childrenOf reg "Envelope" |> List.tryFind (fun f -> f.Def.Name = "eventType")) |> Option.map (fun f -> match f.Def.Kind with KEnum(_, vs) -> vs | _ -> []) |> Option.defaultValue []
            let missingPayload = eventTypes |> List.filter (fun et -> not (typeIds.Contains("Payload." + et))) |> List.map (sprintf "event type %s has no payload type")
            let emptyTypes = reg.ObjectTypes |> List.filter (fun t -> reg.Fields |> List.exists (fun f -> f.Def.ObjectType = t.TypeId) |> not) |> List.map (fun t -> sprintf "object type %s has no fields" t.TypeId)
            let ruleTargets = reg.Rules |> List.filter (fun r -> FieldRegistry.childrenOf reg r.ObjectType |> List.exists (fun f -> f.Def.Name = r.Field) |> not) |> List.map (fun r -> sprintf "rule on %s targets unknown field %s" r.ObjectType r.Field)
            result "CR-REG-03" "Schema completeness: every event type has a payload type, every object type has fields, every reference and rule resolves" "schema"
                (ofProblems ((reg.Fields |> List.collect (fun f -> kindProblems f.Def.FieldId f.Def.Kind)) @ missingPayload @ emptyTypes @ ruleTargets))
                [ "eventTypes", Json.i (List.length eventTypes); "objectTypes", Json.i (List.length reg.ObjectTypes); "rules", Json.i (List.length reg.Rules) ]

        // Command / event registry
        let uniqueIds =
            let p =
                (dupes (pkg.Variants |> List.map (fun v -> v.VariantId)) |> List.map (sprintf "duplicate variant id %s"))
                @ (dupes (pkg.Variants |> List.map (fun v -> v.Name)) |> List.map (sprintf "duplicate variant name %s"))
                @ (dupes (pkg.Commands |> List.map (fun c -> c.CommandId)) |> List.map (sprintf "duplicate command id %s"))
                @ (dupes (pkg.Machines |> List.collect (fun m -> m.Transitions |> List.map (fun t -> t.TransitionId))) |> List.map (sprintf "duplicate transition id %s"))
                @ (dupes (pkg.Bindings |> List.map (fun b -> b.BindingId)) |> List.map (sprintf "duplicate binding id %s"))
            result "CR-CER-01" "Command, event-variant, transition and binding IDs are unique" "registry" (ofProblems p)
                [ "variants", Json.i (List.length pkg.Variants); "commands", Json.i (List.length pkg.Commands) ]
        let variantPartition =
            let eventTypes =
                FieldRegistry.childrenOf reg "Envelope" |> List.tryFind (fun f -> f.Def.Name = "eventType")
                |> Option.map (fun f -> match f.Def.Kind with KEnum(_, vs) -> vs | _ -> []) |> Option.defaultValue []
            let wv = wireVariants pkg
            let problems =
                eventTypes
                |> List.collect (fun et ->
                    let vs = wv |> List.filter (fun v -> v.EventType = et)
                    match vs with
                    | [] -> [ sprintf "event type %s has no variant" et ]
                    | [ v ] when v.Discriminator.IsNone -> []
                    | vs when vs |> List.forall (fun v -> v.Discriminator.IsSome) ->
                        let path = vs |> List.map (fun v -> fst v.Discriminator.Value) |> List.distinct
                        match path with
                        | [ p ] ->
                            let f = FieldRegistry.childrenOf reg ("Payload." + et) |> List.tryFind (fun x -> x.Def.Name = p)
                            let values = match f |> Option.map (fun x -> x.Def.Kind) with Some(KEnum(_, xs)) -> xs | _ -> []
                            let bound = vs |> List.map (fun v -> match snd v.Discriminator.Value with JStr s -> s | other -> Json.canonical other)
                            (values |> List.filter (fun x -> List.contains x bound |> not) |> List.map (sprintf "%s value %s has no variant" et))
                            @ (bound |> List.filter (fun x -> List.contains x values |> not) |> List.map (sprintf "%s variant value %s is not a wire value" et))
                            @ (dupes bound |> List.map (sprintf "%s value %s has two variants" et))
                        | _ -> [ sprintf "%s variants use several discriminator paths" et ]
                    | _ -> [ sprintf "%s mixes discriminated and undiscriminated variants" et ])
            let stray = wv |> List.filter (fun v -> List.contains v.EventType eventTypes |> not) |> List.map (fun v -> sprintf "variant %s names unknown event type %s" v.Name v.EventType)
            result "CR-CER-02" "Wire variants partition every registered event type and discriminator value (no silent default)" "registry" (ofProblems (problems @ stray))
                [ "wireEventTypes", Json.i (List.length eventTypes); "wireVariants", Json.i (List.length wv) ]
        let eventCaseCoverage =
            let cases = FsContract.caseNames inputs.V415 "CoreEvent"
            let bound = pkg.Variants |> List.choose (fun v -> v.CoreEventCase)
            let problems =
                (cases |> List.filter (fun c -> List.contains c bound |> not) |> List.map (sprintf "v4.1.5 CoreEvent.%s has no variant"))
                @ (pkg.Variants |> List.filter (fun v -> v.Status <> FieldRegistry.statusProposed) |> List.choose (fun v -> v.CoreEventCase |> Option.filter (fun c -> List.contains c cases |> not) |> Option.map (sprintf "variant %s names CoreEvent.%s, absent from v4.1.5" v.Name)))
                @ (pkg.Variants |> List.filter (fun v -> v.Status = FieldRegistry.statusProposed) |> List.choose (fun v -> v.CoreEventCase |> Option.filter (fun c -> List.contains c cases) |> Option.map (sprintf "proposed variant %s claims CoreEvent.%s, which v4.1.5 already has" v.Name)))
                @ (dupes (pkg.Variants |> List.choose (fun v -> v.CoreEventCase)) |> List.map (sprintf "CoreEvent.%s bound by two variants"))
            result "CR-CER-03" "Every v4.1.5 CoreEvent case is bound to exactly one variant; proposed cases are absent from v4.1.5" "registry" (ofProblems problems)
                [ "coreEventCases", Json.i (List.length cases) ]
        let commandCoverage =
            let unions = [ "CoreCommand"; "Msg"; "SaveInput"; "LoadInput"; "ConflictInput" ]
            let coreCases = FsContract.caseNames inputs.V415 "CoreCommand"
            let named = pkg.Commands |> List.collect (fun c -> c.ContractCase |> Option.map (fun s -> s.Split('|') |> List.ofArray) |> Option.defaultValue [] |> List.map (fun x -> c, x))
            let problems =
                (coreCases |> List.filter (fun c -> named |> List.exists (fun (cmd, n) -> n = c && cmd.ContractUnion = Some "CoreCommand") |> not) |> List.map (sprintf "v4.1.5 CoreCommand.%s has no command entry"))
                @ (named |> List.choose (fun (cmd, n) ->
                    match cmd.ContractUnion with
                    | Some u when List.contains u unions ->
                        let present = FsContract.caseNames inputs.V415 u |> List.contains n
                        match cmd.Status, present with
                        | s, false when s.StartsWith "defined" || s.StartsWith "refused" -> Some(sprintf "%s claims %s.%s, absent from v4.1.5" cmd.CommandId u n)
                        | "proposed-successor", true -> Some(sprintf "%s is proposed but %s.%s already exists" cmd.CommandId u n)
                        | _ -> None
                    | Some u -> Some(sprintf "%s names unknown union %s" cmd.CommandId u)
                    | None -> None))
            result "CR-CER-04" "Every v4.1.5 CoreCommand case has a command entry; defined commands exist in the parsed contracts and proposed ones do not" "registry" (ofProblems problems)
                [ "coreCommandCases", Json.i (List.length coreCases) ]
        let commandRefs =
            let transitionIds = pkg.Machines |> List.collect (fun m -> m.Transitions |> List.map (fun t -> t.TransitionId)) |> Set.ofList
            let problems =
                pkg.Commands
                |> List.collect (fun c ->
                    (c.ResultEvents |> List.map baseName |> List.filter (fun n -> not (variantNames.Contains n)) |> List.map (sprintf "%s result event %s is not a registered variant" c.CommandId))
                    @ (c.Inputs |> List.filter (fun i -> not (fieldSet.Contains i.FieldId)) |> List.map (fun i -> sprintf "%s input %s is not in FIELD-REGISTRY" c.CommandId i.FieldId))
                    @ (c.Transitions |> List.filter (fun t -> not (t.StartsWith "SM-") && not (transitionIds.Contains t) && t <> "DT-INV-00" && t <> "DT-HYP-00") |> List.map (sprintf "%s names unknown transition %s" c.CommandId))
                    @ (c.BlockedBy |> List.filter (decisionIds.Contains >> not) |> List.map (sprintf "%s blocked by unknown decision %s" c.CommandId)))
            result "CR-CER-05" "Command result events, input fields, transitions and blocking decisions all resolve" "registry" (ofProblems problems) []
        let inputPaths =
            // Every field of a command's primary result event needs a documented input path; a field with "NO INPUT
            // PATH" must be tied to an open owner decision.
            let problems, gaps =
                pkg.Commands
                |> List.filter (fun c -> c.Layer.StartsWith "domain" && not (List.isEmpty c.Inputs))
                |> List.fold (fun (ps, gs) c ->
                    let primary = c.ResultEvents |> List.tryHead |> Option.map baseName
                    let variant = primary |> Option.bind (fun n -> pkg.Variants |> List.tryFind (fun v -> v.Name = n))
                    let required =
                        variant
                        |> Option.map (fun v ->
                            let implied = match v.Discriminator with Some(p, _) -> [ p ] | None -> []
                            reg.Fields |> List.filter (fun f -> f.Def.ObjectType = "Payload." + v.EventType && not (List.contains f.Def.Name implied) && not (List.contains f.Def.Name [ "from"; "to" ])))
                        |> Option.defaultValue []
                    let listed = c.Inputs |> List.map (fun i -> i.FieldId) |> Set.ofList
                    let missing = required |> List.filter (fun f -> not (listed.Contains f.Def.FieldId)) |> List.map (fun f -> sprintf "%s: %s has no documented input path" c.CommandId f.Def.FieldId)
                    let noPath = c.Inputs |> List.filter (fun i -> i.Source.StartsWith "NO INPUT PATH")
                    let untied = if List.isEmpty noPath || not (List.isEmpty c.BlockedBy) then [] else [ sprintf "%s has inputs with no path and no blocking decision" c.CommandId ]
                    ps @ missing @ untied, gs @ (noPath |> List.map (fun i -> c.CommandId + " " + i.FieldId))) ([], [])
            result "CR-CER-06" "Every required domain input has a documented input path (UI field, derivation or session), or is a named gap tied to an open owner decision" "registry" (ofProblems problems)
                [ "inputGapsTiedToDecisions", Json.ss gaps ]

        // Historical replay
        let decodeCheck =
            let bad = allEvents |> List.filter (fun e -> not (List.isEmpty e.Decoded.Problems))
            result "CR-HIST-01" "Every stored event decodes strictly into registered typed fields and re-encodes to its exact stored bytes" "historical"
                (ofProblems (bad |> List.truncate 20 |> List.map (fun e -> sprintf "%s:%d %s" e.File e.Line (e.Decoded.Problems |> List.map Codec.problemText |> String.concat "; "))))
                [ "distinctHistories", Json.i (List.length distinct); "eventsExamined", Json.i (List.length allEvents); "eventsLossless", Json.i (List.length allEvents - List.length bad)
                  "unknownFieldsPreserved", Json.i (bad |> List.sumBy (fun e -> e.Decoded.Problems |> List.filter (function UnknownField _ -> true | _ -> false) |> List.length)) ]
        let variantCheck =
            let none = allEvents |> List.filter (fun e -> e.Variant.IsNone)
            result "CR-HIST-02" "Every stored event maps to exactly one registered wire variant" "historical"
                (ofProblems (none |> List.truncate 20 |> List.map (fun e -> sprintf "%s:%d %s has no variant" e.File e.Line e.EventType))) [ "unmapped", Json.i (List.length none) ]
        let integrityCheck =
            let p = distinct |> List.collect (fun h -> h.IntegrityProblems)
            result "CR-HIST-03" "Envelope integrity: contiguous aggregateVersion, UUIDv5 eventId derivation, one correlation group per id, InvestigationStarted first" "historical" (ofProblems (p |> List.truncate 20)) [ "problems", Json.i (List.length p) ]
        let filesCheck =
            let p =
                histories
                |> List.collect (fun h ->
                    let manifestSha =
                        let m = Json.parseFile (Path.Combine(inputs.DataRoot, h.Dir, "manifest.json"))
                        if Json.fieldStr "eventsSha256" m = h.EventsSha256 then [] else [ sprintf "%s manifest eventsSha256 differs from events.ndjson" h.Dir ]
                    (h.ManifestProblems |> List.map (fun x -> sprintf "%s/manifest.json %s" h.Dir (Codec.problemText x)))
                    @ (h.SnapshotProblems |> List.map (fun x -> sprintf "%s/snapshot.json %s" h.Dir (Codec.problemText x)))
                    @ manifestSha)
            result "CR-HIST-04" "Every manifest.json and snapshot.json decodes against the registry, round-trips, and the manifest pins the events bytes" "historical" (ofProblems (p |> List.truncate 20)) [ "investigationDirectories", Json.i (List.length histories) ]
        let catalogCheck =
            let p =
                inputs.Catalogs
                |> List.collect (fun (cat, cases) ->
                    let manifest = Json.parseFile (Path.Combine(inputs.DataRoot, "catalog", cat, "manifest.json"))
                    Json.arrOf "cases" manifest
                    |> List.choose (fun c ->
                        let path = Json.fieldStr "path" c
                        match histories |> List.tryFind (fun h -> h.Dir = path) with
                        | None -> Some(sprintf "%s case %s missing" cat path)
                        | Some h when Some h.EventsSha256 <> Json.strOpt "eventsSha256" c -> Some(sprintf "%s case %s events sha differs" cat path)
                        | Some h when Json.tryField "expectedVersion" c <> Some(JInt(int64 h.Events.Length)) -> Some(sprintf "%s case %s version differs" cat path)
                        | _ -> None))
            result "CR-HIST-05" "Every catalog case exists with its pinned events digest and expected version" "historical" (ofProblems p)
                [ "catalogs", Json.ss (inputs.Catalogs |> List.map fst); "activeCatalog", Json.s inputs.ActiveCatalog ]
        let transitions, transitionsChecked =
            distinct |> List.fold (fun (fs, n) h -> let f, c = Historical.replayTransitions pkg.Machines h in fs @ f, n + c) ([], 0)
        let transitionCheck =
            result "CR-HIST-06" "Every stored state transition is legal under the domain transition tables and its stored 'from' equals the replayed state" "historical"
                (ofProblems (transitions |> List.truncate 30 |> List.map (fun f -> sprintf "%s:%d %s %s: %s" f.File f.Line f.Machine f.EntityId f.Detail)))
                [ "transitionsChecked", Json.i transitionsChecked; "violations", Json.i (List.length transitions) ]
        let prov = Historical.provenance inputs.DataRoot histories
        let provCheck =
            let p =
                prov |> List.collect (fun c ->
                    (c.MissingEvidence |> List.map (sprintf "%s provenance evidence %s not admitted" c.CaseKey))
                    @ (c.EvidenceWithoutProvenance |> List.map (sprintf "%s evidence %s has no provenance record" c.CaseKey))
                    @ (c.UrlNotInSource |> List.map (sprintf "%s evidence %s source text lacks its provenance URL" c.CaseKey)))
            result "CR-HIST-07" "Historical provenance records and admitted evidence correspond one to one, and each source text carries its URL" "historical" (ofProblems (p |> List.truncate 20))
                [ "cases", Json.i (List.length prov); "provenanceRecords", Json.i (prov |> List.sumBy (fun c -> c.Records)) ]

        // Representability
        let rep415Active = summarize inputs "v4.1.5 (candidate)" inputs.V415 activeEvents
        let rep415All = summarize inputs "v4.1.5 (candidate)" inputs.V415 allEvents
        let rep414Hist = summarize inputs "v4.1.4 (frozen)" inputs.V414 historicalEvents
        let rep415Hist = summarize inputs "v4.1.5 (candidate)" inputs.V415 historicalEvents
        let rep414Active = summarize inputs "v4.1.4 (frozen)" inputs.V414 activeEvents
        let repCheck =
            let p s = if s.Lossless = s.Total then [] else [ sprintf "%s represents %d of %d events losslessly (missing case %d, lossy %d, envelope drops %A)" s.Contract s.Lossless s.Total s.NoCase s.Lossy s.EnvelopeDropped ]
            result "CR-REP-01" "The successor candidate represents every pinned catalog event and every stored history losslessly (payload case and envelope)" "historical" (ofProblems (p rep415Active @ p rep415All))
                [ "activeCatalog", summaryJson rep415Active; "allDistinctHistories", summaryJson rep415All ]
        let frozenRepro =
            let expectedNoCase, expectedLossy, expectedTotal = 487, 63, 1315
            let p =
                [ if rep414Hist.Total <> expectedTotal then sprintf "historical events %d, PR #141 reported %d" rep414Hist.Total expectedTotal
                  if rep414Hist.NoCase <> expectedNoCase then sprintf "frozen missing-case events %d, PR #141 reported %d" rep414Hist.NoCase expectedNoCase
                  if rep414Hist.Lossy <> expectedLossy then sprintf "frozen lossy events %d, PR #141 reported %d" rep414Hist.Lossy expectedLossy
                  if List.isEmpty rep414Hist.EnvelopeDropped then "frozen envelope drops nothing, PR #141 reported provenance loss" ]
            result "CR-REP-02" "Reproduce PR #141 against the frozen v4.1.4 contract: 487 of 1,315 historical events have no case, 63 more are lossy, and every event loses envelope provenance" "historical" (ofProblems p)
                [ "historicalCases", summaryJson rep414Hist; "historicalCasesSuccessor", summaryJson rep415Hist; "activeCatalogFrozen", summaryJson rep414Active ]

        // UI bindings
        let catReqs = catalogRequirements inputs
        let catById = catReqs |> List.map (fun r -> Json.fieldStr "id" r, r) |> Map.ofList
        let uiCoverage =
            let bound = pkg.Bindings |> List.map (fun b -> b.RequirementId) |> Set.ofList
            let problems =
                (catById |> Map.toList |> List.map fst |> List.filter (bound.Contains >> not) |> List.map (sprintf "%s has no binding"))
                @ (pkg.Bindings |> List.collect (fun b ->
                    match catById.TryFind b.RequirementId with
                    | None -> [ sprintf "%s binds unknown requirement %s" b.BindingId b.RequirementId ]
                    | Some r ->
                        let name = Json.fieldStr "name" r
                        let comp = Json.strOpt "component" r
                        let required = Json.boolOf "required" r
                        let screen = Json.fieldStr "screen" r
                        let labelOk = b.Label = name
                        let catValidation = match Json.tryField "validation" r with Some(JObj ms) -> ms | _ -> []
                        [ if not labelOk then sprintf "%s label '%s' differs from catalog '%s'" b.BindingId b.Label name
                          if b.InputType <> comp then sprintf "%s input type %A differs from catalog %A" b.BindingId b.InputType comp
                          if b.Required <> required then sprintf "%s required %b differs from catalog %b" b.BindingId b.Required required
                          if b.ScreenId <> screen then sprintf "%s screen %s differs from catalog %s" b.BindingId b.ScreenId screen
                          yield! catValidation |> List.filter (fun (k, v) -> Json.tryField k b.Validation <> Some v) |> List.map (fun (k, _) -> sprintf "%s validation %s differs from catalog" b.BindingId k) ]))
            result "CR-UI-01" "Every catalog requirement has a binding whose label, accessible name source, input type, required flag, screen and validation equal the catalog" "ui" (ofProblems problems)
                [ "catalogRequirements", Json.i (Map.count catById); "bindings", Json.i (List.length pkg.Bindings) ]
        let smTests = screenManifestTests inputs.ScreenManifest
        let uiRefs =
            let commandIds = pkg.Commands |> List.map (fun c -> c.CommandId) |> Set.ofList
            let screens = Json.arrOf "screens" inputs.UiMap
            let smScreens = Json.arrOf "screens" inputs.ScreenManifest
            let screenProblems =
                screens |> List.collect (fun s ->
                    let id = Json.fieldStr "screenId" s
                    match smScreens |> List.tryFind (fun x -> Json.fieldStr "id" x = id) with
                    | None -> [ sprintf "screen %s is not in SCREEN-MANIFEST" id ]
                    | Some sm ->
                        [ if Json.field "states" sm <> Json.field "states" s then sprintf "screen %s states differ from SCREEN-MANIFEST" id
                          if Json.field "responsive" sm <> Json.field "mobile" s then sprintf "screen %s mobile obligations differ from SCREEN-MANIFEST" id ])
            let screenIds = screens |> List.map (Json.fieldStr "screenId") |> Set.ofList
            let problems =
                screenProblems
                @ (pkg.Bindings |> List.collect (fun b ->
                    [ if not (screenIds.Contains b.ScreenId) then sprintf "%s screen %s has no screen entry" b.BindingId b.ScreenId
                      match b.CommandId with
                      | Some c when not (commandIds.Contains c) -> sprintf "%s command %s is not registered" b.BindingId c
                      | _ -> ()
                      if b.Kind = "ui-field" && (b.CommandId.IsNone || List.isEmpty b.WriteFields) && b.Status <> "not-applicable-v1.0.0" then sprintf "%s is an editable field with no command or write field" b.BindingId
                      if b.Kind = "action" && b.CommandId.IsNone then sprintf "%s is an action with no command" b.BindingId
                      yield! (b.WriteFields @ b.ReadBackFields) |> List.filter (fieldSet.Contains >> not) |> List.map (sprintf "%s field %s is not in FIELD-REGISTRY" b.BindingId)
                      if not (List.contains ("CATALOG:" + b.RequirementId) b.Tests) then sprintf "%s names no catalog verification test" b.BindingId
                      // The independent test ids are the catalog's verification kinds, compiled by Praxis into
                      // INV-0nn/Vn/<kind> and judged by Dokimos (catalog acceptance).
                      match catById.TryFind b.RequirementId with
                      | Some r when List.isEmpty (Json.arrOf "verification" r) -> sprintf "%s: catalog %s has no verification kinds" b.BindingId b.RequirementId
                      | _ -> ()
                      yield! b.States |> List.filter (fun s -> screens |> List.exists (fun sc -> Json.fieldStr "screenId" sc = b.ScreenId && List.contains (JStr s) (Json.arrOf "states" sc)) |> not) |> List.map (sprintf "%s state '%s' is not a state of its screen" b.BindingId) ]))
            result "CR-UI-02" "Binding references resolve: screens and states (equal to SCREEN-MANIFEST), commands, write and read-back field IDs, verification ids" "ui" (ofProblems problems)
                [ "screens", Json.i (List.length screens)
                  "catalogVerificationKinds", Json.i (catReqs |> List.sumBy (fun r -> Json.arrOf "verification" r |> List.length))
                  "screenManifestMandatoryIds", Json.i (smTests |> Map.toList |> List.sumBy (snd >> List.length)) ]
        let uiHonesty =
            let commandById = pkg.Commands |> List.map (fun c -> c.CommandId, c) |> Map.ofList
            let problems =
                pkg.Bindings |> List.collect (fun b ->
                    let proposedFields = (b.WriteFields @ b.ReadBackFields) |> List.filter (fun f -> byId.TryFind f |> Option.map (fun x -> x.Status = FieldRegistry.statusProposed) |> Option.defaultValue false)
                    let cmd = b.CommandId |> Option.bind commandById.TryFind
                    let cmdProposed = cmd |> Option.map (fun c -> c.Status = "proposed-successor") |> Option.defaultValue false
                    // A command lacks a required input path when one of its inputs is a named gap (OD-CR-*).
                    let cmdBlocked = cmd |> Option.map (fun c -> c.Inputs |> List.exists (fun i -> i.Source.StartsWith "NO INPUT PATH")) |> Option.defaultValue false
                    [ if (not (List.isEmpty proposedFields) || cmdProposed) && b.Status <> "blocked" then sprintf "%s uses proposed fields or commands but is %s" b.BindingId b.Status
                      if b.Status = "blocked" && List.isEmpty b.BlockedBy then sprintf "%s is blocked without a decision" b.BindingId
                      if b.Status.StartsWith "covered" && cmdBlocked && b.Kind = "ui-field" then sprintf "%s is covered but its command has a required input with no path" b.BindingId
                      yield! b.BlockedBy |> List.filter (decisionIds.Contains >> not) |> List.map (sprintf "%s blocked by unknown decision %s" b.BindingId) ])
            result "CR-UI-03" "Coverage is not claimed for a binding that needs a proposed field or command or a blocked command" "ui" (ofProblems problems)
                [ "covered", Json.i (pkg.Bindings |> List.filter (fun b -> b.Status.StartsWith "covered") |> List.length)
                  "blocked", Json.i (pkg.Bindings |> List.filter (fun b -> b.Status = "blocked") |> List.length)
                  "notApplicable", Json.i (pkg.Bindings |> List.filter (fun b -> b.Status = "not-applicable-v1.0.0") |> List.length) ]
        let uiReverse =
            let boundReqs = pkg.Bindings |> List.map (fun b -> b.RequirementId) |> Set.ofList
            let boundCommands = pkg.Bindings |> List.choose (fun b -> b.CommandId) |> Set.ofList
            let uiInputReq (s: string) = Regex.Match(s, @"ui:(INV-\d{3})") |> fun m -> if m.Success then Some m.Groups.[1].Value else None
            let problems =
                pkg.Commands |> List.collect (fun c ->
                    let reqs = c.Inputs |> List.choose (fun i -> uiInputReq i.Source)
                    (reqs |> List.filter (boundReqs.Contains >> not) |> List.map (sprintf "%s takes input from %s, which has no binding" c.CommandId))
                    @ (if c.Layer <> "ui-only" && not (List.isEmpty reqs) && not (boundCommands.Contains c.CommandId) && not (pkg.Bindings |> List.exists (fun b -> b.Note |> Option.map (fun n -> n.Contains c.CommandId) |> Option.defaultValue false))
                       then [ sprintf "%s has UI inputs but no binding names it" c.CommandId ] else [])
                    @ (if (boundCommands.Contains c.CommandId) && c.Layer <> "ui-only" && List.isEmpty c.ResultEvents && List.isEmpty c.Transitions && c.Layer.StartsWith "domain"
                       then [ sprintf "%s is a bound domain command with no result event or transition" c.CommandId ] else []))
            result "CR-UI-04" "Reverse coverage: every command fed by a UI field is reachable from a binding, and every bound domain action has a state transition or result event" "ui" (ofProblems problems) []

        // State transitions and persistence
        let domainMachines =
            let problems =
                pkg.Machines |> List.collect (fun m ->
                    let states = enumValues m.StateEnum
                    let check (s: string) = match states with Some vs when not (List.contains s vs) -> [ sprintf "%s state %s is not in enum %s" m.MachineId s m.StateEnum ] | _ -> []
                    (m.Creation |> List.collect (snd >> check))
                    @ (m.Transitions |> List.collect (fun t -> (t.From |> List.collect check) @ check t.To))
                    @ (m.Transitions |> List.choose (fun t ->
                        // A discriminated transition's target must equal the value the schema pins for that discriminator.
                        match t.When with
                        | Some(p, v) ->
                            reg.Rules
                            |> List.tryFind (fun r -> r.ObjectType = "Payload." + t.EventType && r.WhenPath = p && r.WhenValue = v && r.Field = "to")
                            |> Option.bind (fun r -> match r.Constraint with MustEqual(JStr s) when s <> t.To -> Some(sprintf "%s to=%s but the schema pins %s" t.TransitionId t.To s) | _ -> None)
                        | None -> None)))
            let stateChanging = wireVariants pkg |> List.filter (fun v -> v.EventType.EndsWith "Transitioned" || v.EventType = "LifecycleChanged" || v.EventType = "CausalAssertionConfirmed" || v.EventType = "CausalAssertionReopened" || v.EventType = "ProposalWasAccepted" || v.EventType = "ProposalWasRejected")
            let uncovered =
                stateChanging |> List.filter (fun v ->
                    pkg.Machines |> List.exists (fun m -> m.Transitions |> List.exists (fun t -> t.EventType = v.EventType && (t.When.IsNone || t.When = v.Discriminator))) |> not)
                |> List.filter (fun v -> v.EventType <> "EvidenceVerificationAssessed")
                |> List.map (fun v -> sprintf "state-changing variant %s has no transition" v.Name)
            result "CR-ST-01" "Domain transition tables use only wire states, agree with schema-pinned targets, and cover every state-changing variant" "transitions" (ofProblems (problems @ uncovered))
                [ "machines", Json.i (List.length pkg.Machines); "transitions", Json.i (pkg.Machines |> List.sumBy (fun m -> List.length m.Transitions)) ]
        let smPath = Path.Combine(inputs.IndyRoot, fst pkg.PersistenceSource)
        let smJson = Json.parseFile smPath
        let persistenceMachines =
            let sha = Json.sha256File smPath
            let machines = Json.arrOf "machines" smJson
            let binding = Json.arrOf "unionBinding" (Json.field "persistenceMachines" (Json.parseFile (Path.Combine(pkg.Dir, "STATE-TRANSITIONS.json"))))
            let problems =
                [ if sha <> snd pkg.PersistenceSource then sprintf "state-machine-contracts.json sha256 %s, indexed %s" sha (snd pkg.PersistenceSource)
                  yield! pkg.PersistenceMachines |> List.filter (fun id -> machines |> List.exists (fun m -> Json.fieldStr "id" m = id) |> not) |> List.map (sprintf "machine %s not in state-machine-contracts.json")
                  yield! binding |> List.collect (fun b ->
                    let id = Json.fieldStr "machine" b
                    match machines |> List.tryFind (fun m -> Json.fieldStr "id" m = id) with
                    | None -> [ sprintf "%s missing" id ]
                    | Some m ->
                        let st = Json.strs "states" m |> Set.ofList
                        let ev = Json.strs "events" m |> Set.ofList
                        let fsSt = FsContract.caseNames inputs.V415 (Json.fieldStr "stateUnion" b) |> Set.ofList
                        let fsEv = FsContract.caseNames inputs.V415 (Json.fieldStr "inputUnion" b) |> Set.ofList
                        [ if st <> fsSt then sprintf "%s states %A differ from F# %s %A" id (Set.toList st) (Json.fieldStr "stateUnion" b) (Set.toList fsSt)
                          if ev <> fsEv then sprintf "%s events differ from F# %s (missing in F#: %A; extra in F#: %A)" id (Json.fieldStr "inputUnion" b) (Set.difference ev fsEv |> Set.toList) (Set.difference fsEv ev |> Set.toList) ]) ]
            result "CR-ST-02" "Persistence machines are indexed by digest and every machine's states and inputs equal the parsed v4.1.5 F# unions (no silent default case)" "persistence" (ofProblems problems)
                [ "machines", Json.i (List.length machines)
                  "pairsDecided", Json.i (machines |> List.sumBy (fun m -> Json.arrOf "pairs" m |> List.length)) ]
        let durability =
            let saveStates = FsContract.caseNames inputs.V415 "SaveState"
            let required = [ "Local changes"; "Save pending"; "Provider write acknowledged"; "Provider read-back verified"; "Save conflict"; "Save failed"; "Offline / unverified" ]
            let problems =
                (required |> List.filter (fun n -> pkg.Durability |> List.exists (fun d -> d.Name = n) |> not) |> List.map (sprintf "durability state '%s' is not defined"))
                @ (saveStates |> List.filter (fun s -> pkg.Durability |> List.exists (fun d -> List.contains s d.SaveStates) |> not) |> List.map (sprintf "SaveState.%s has no durability state"))
                @ (pkg.Durability |> List.collect (fun d -> d.SaveStates |> List.filter (fun s -> List.contains s saveStates |> not) |> List.map (sprintf "%s names SaveState.%s, absent from v4.1.5" d.StateId)))
                @ (pkg.Durability |> List.filter (fun d -> d.MayDisplaySaved && not d.Durable) |> List.map (fun d -> sprintf "%s may display saved without durability" d.StateId))
                @ (pkg.Durability |> List.filter (fun d -> d.Durable && d.SaveStates <> [ "Saved" ]) |> List.map (fun d -> sprintf "%s is durable but maps to %A" d.StateId d.SaveStates))
                @ (pkg.Durability |> List.filter (fun d -> d.Durable && not (d.Evidence.Contains "read")) |> List.map (fun d -> sprintf "%s is durable without a read-back basis" d.StateId))
                @ (if FsContract.caseNames inputs.V415 "SavedBasis" |> List.sort <> [ "Confirmed"; "VerifiedAt" ] then [ "SavedBasis is not {Confirmed; VerifiedAt}" ] else [])
            result "CR-ST-03" "Save durability: seven distinct states; only provider read-back verified (SaveState.Saved) is durable or may be displayed as saved" "persistence" (ofProblems problems)
                [ "saveStates", Json.ss saveStates ]

        // Domain contract and decisions
        let domain =
            let required = [ "Workspace"; "Investigation"; "Observation"; "Hypothesis"; "Evidence"; "Evidence source"; "Hypothesis-evidence relationship"; "Assessment"; "Conclusion"; "Decision"; "Investigation history"; "Provenance"; "Persistence metadata" ]
            let names = pkg.Entities |> List.map (fun e -> e.Name)
            let problems =
                (required |> List.filter (fun r -> names |> List.exists (fun n -> n.StartsWith r) |> not) |> List.map (sprintf "domain concept '%s' is not defined"))
                @ (pkg.Entities |> List.collect (fun e -> e.Fields |> List.filter (fieldSet.Contains >> not) |> List.map (sprintf "%s field %s is not in FIELD-REGISTRY" e.TypeId)))
                @ (pkg.Entities |> List.collect (fun e -> e.BlockedBy |> List.filter (decisionIds.Contains >> not) |> List.map (sprintf "%s blocked by unknown decision %s" e.TypeId)))
                @ (dupes (pkg.Entities |> List.map (fun e -> e.TypeId)) |> List.map (sprintf "duplicate type id %s"))
            result "CR-DOM-01" "Every required domain concept is defined with a stable type ID and registered fields" "domain" (ofProblems problems) [ "entities", Json.i (List.length pkg.Entities) ]
        let extension =
            let wireTypes = reg.ObjectTypes |> List.filter (fun t -> not (t.SchemaFile.StartsWith "(proposed)")) |> List.map (fun t -> t.TypeId) |> Set.ofList
            let proposedVariants = pkg.Variants |> List.filter (fun v -> v.Status = FieldRegistry.statusProposed)
            let problems =
                (pkg.ProposedTypes |> List.filter (fun t -> wireTypes.Contains t.TypeId) |> List.map (fun t -> sprintf "proposed type %s would change a v1.0.0 type" t.TypeId))
                @ (proposedVariants |> List.filter (fun v -> pkg.ProposedTypes |> List.exists (fun t -> t.TypeId = "Payload." + v.EventType) |> not) |> List.map (fun v -> sprintf "proposed variant %s has no proposed payload type" v.Name))
                @ (pkg.ProposedFields |> List.filter (fun f -> f.Def.Kind = KAnyObject) |> List.map (fun f -> sprintf "%s has no closed kind" f.Def.FieldId))
            result "CR-DOM-02" "The proposed causa-storage 1.1.0 extension is purely additive: no v1.0.0 type changes, every proposed event has a closed payload" "domain" (ofProblems problems)
                [ "proposedEvents", Json.i (List.length proposedVariants); "proposedFields", Json.i (List.length pkg.ProposedFields) ]
        let decisions =
            let problems = dupes (pkg.Decisions |> List.map (fun d -> d.DecisionId)) |> List.map (sprintf "duplicate decision %s")
            result "CR-DEC-01" "Owner decision requests are unique and every blocker names one" "governance" (ofProblems problems)
                [ "open", Json.i (pkg.Decisions |> List.filter (fun d -> d.Status.StartsWith "open") |> List.length)
                  "openBlocking", Json.ss (pkg.Decisions |> List.filter (fun d -> d.Blocking && d.Status.StartsWith "open") |> List.map (fun d -> d.DecisionId)) ]
        let secrets =
            let patterns = [ @"gh[pousr]_[A-Za-z0-9]{20,}"; @"github_pat_[A-Za-z0-9_]{20,}"; @"(?i)authorization:\s*(bearer|token)\s+[A-Za-z0-9._-]{12,}"; @"AKIA[0-9A-Z]{16}"; @"-----BEGIN [A-Z ]*PRIVATE KEY-----" ]
            let problems =
                inputs.PackageTexts |> List.collect (fun (name, text) ->
                    patterns |> List.filter (fun p -> Regex.IsMatch(text, p)) |> List.map (sprintf "%s matches credential pattern %s" name))
            result "CR-SEC-01" "No credential or access token appears in any contract package file or fixture" "security" (ofProblems problems) [ "filesScanned", Json.i (List.length inputs.PackageTexts) ]
        let live =
            result "CR-LIVE-01" "Real-provider durability: a save shown as saved is read back from GitHub (PERSIST-023, J3-05, INV-037)" "persistence"
                (Unverified "Needs a GitHub credential and a writable repository; none is available to this validator. A provider double tests protocol behaviour only and is not substituted. Run the live-provider preflight (.github/workflows/live-provider-preflight.yml).") []

        let checks =
            [ pinChecks; reproducible; uniqueFields; completeness; uniqueIds; variantPartition; eventCaseCoverage; commandCoverage; commandRefs; inputPaths
              decodeCheck; variantCheck; integrityCheck; filesCheck; catalogCheck; transitionCheck; provCheck; repCheck; frozenRepro
              uiCoverage; uiRefs; uiHonesty; uiReverse; domainMachines; persistenceMachines; durability; domain; extension; decisions; secrets; live ]
        let facts =
            Map.ofList
                [ "historiesAll", JInt(int64 (List.length histories))
                  "historiesDistinct", JInt(int64 (List.length distinct))
                  "eventsDistinct", JInt(int64 (List.length allEvents))
                  "eventsHistorical", JInt(int64 (List.length historicalEvents))
                  "eventsActiveCatalog", JInt(int64 (List.length activeEvents))
                  "eventsAllCopies", JInt(int64 (histories |> List.sumBy (fun h -> h.Events.Length)))
                  "rep414Historical", summaryJson rep414Hist
                  "rep415Historical", summaryJson rep415Hist
                  "rep414Active", summaryJson rep414Active
                  "rep415Active", summaryJson rep415Active
                  "rep415All", summaryJson rep415All ]
        checks, facts
