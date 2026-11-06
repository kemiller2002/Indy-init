namespace Causa.ContractReadiness

open System
open System.IO
open System.Text

type EventRecord =
    { File: string
      Line: int
      Text: string
      Decoded: Codec.Decoded
      EventType: string
      Variant: EventVariant option }

type HistoryFile =
    { Dir: string
      DatasetKind: string
      DatasetId: string
      InvestigationId: string
      EventsSha256: string
      Catalogs: string list
      CaseKey: string option
      Events: EventRecord list
      ManifestProblems: Problem list
      SnapshotProblems: Problem list
      IntegrityProblems: string list }

/// How a contract version represents one stored event.
type Representation =
    | Lossless of caseName: string
    | NoCase of reason: string
    | Lossy of caseName: string * dropped: string list
    | NoVariant

type TransitionFinding =
    { File: string
      Line: int
      Machine: string
      EntityId: string
      Detail: string }

type ProvenanceCheck =
    { CaseKey: string
      Records: int
      EvidenceAdmitted: int
      MissingEvidence: string list
      EvidenceWithoutProvenance: string list
      UrlNotInSource: string list }

type NegativeFixture =
    { Name: string
      ExpectedCode: string
      ExpectedDomainCode: string option
      HasEvents: bool
      RefusedByRegistry: bool
      FirstProblem: string option }

module Historical =
    // ---- UUIDv5 (CONTRACT §5.2) ------------------------------------------------------------------------------------

    let private guidToBytesBigEndian (g: Guid) =
        let b = g.ToByteArray()
        [| b.[3]; b.[2]; b.[1]; b.[0]; b.[5]; b.[4]; b.[7]; b.[6]; b.[8]; b.[9]; b.[10]; b.[11]; b.[12]; b.[13]; b.[14]; b.[15] |]

    let private bytesBigEndianToGuid (b: byte[]) =
        Guid([| b.[3]; b.[2]; b.[1]; b.[0]; b.[5]; b.[4]; b.[7]; b.[6]; b.[8]; b.[9]; b.[10]; b.[11]; b.[12]; b.[13]; b.[14]; b.[15] |])

    let uuidV5 (ns: Guid) (name: string) =
        let hash = Security.Cryptography.SHA1.HashData(Array.append (guidToBytesBigEndian ns) (Encoding.UTF8.GetBytes name))
        let b = Array.sub hash 0 16
        b.[6] <- (b.[6] &&& 0x0Fuy) ||| 0x50uy
        b.[8] <- (b.[8] &&& 0x3Fuy) ||| 0x80uy
        bytesBigEndianToGuid b

    let eventNamespace = Guid.Parse "241fefd4-c810-5a3a-9cfa-4bbd968ae1cb"
    let urlNamespace = Guid.Parse "6ba7b811-9dad-11d1-80b4-00c04fd430c8"

    // ---- Discovery -------------------------------------------------------------------------------------------------

    let private rel (root: string) (p: string) = Path.GetRelativePath(root, p).Replace('\\', '/')

    let private splitLines (bytes: byte[]) =
        let text = Encoding.UTF8.GetString bytes
        let lines = text.Split('\n')
        // A well-formed events.ndjson ends with LF, so the last split element is empty.
        if lines.Length > 0 && lines.[lines.Length - 1] = "" then lines |> Array.take (lines.Length - 1) else lines

    let private catalogs (root: string) =
        Directory.GetDirectories(Path.Combine(root, "catalog"))
        |> Array.sort
        |> Array.choose (fun d ->
            let m = Path.Combine(d, "manifest.json")
            if File.Exists m then
                let j = Json.parseFile m
                Some(Path.GetFileName d, Json.arrOf "cases" j |> List.map (fun c -> Json.fieldStr "path" c, Json.fieldStr "caseKey" c))
            else None)
        |> List.ofArray

    let private decodeJsonFile (reg: Registry) (objectType: string) (path: string) =
        if not (File.Exists path) then [ MissingField(Path.GetFileName path) ]
        else
            let text = File.ReadAllText path
            if not (text.EndsWith "\n") || text.EndsWith "\n\n" then [ NotCanonical "a JSON file is one canonical text followed by exactly one LF" ]
            else (Codec.decodeText reg objectType (text.Substring(0, text.Length - 1))).Problems

    let private integrity (dir: string) (investigationId: string) (events: EventRecord list) =
        let at path (e: EventRecord) = Codec.at path e.Decoded.Value |> Codec.asString
        let versionProblems =
            events
            |> List.mapi (fun i e ->
                match at "aggregateVersion" e with
                | Some v when v = string (i + 1) -> None
                | v -> Some(sprintf "%s:%d aggregateVersion %A, expected %d" dir e.Line v (i + 1)))
            |> List.choose id
        let idProblems =
            events
            |> List.choose (fun e ->
                match at "eventId" e, at "investigationId" e, at "aggregateVersion" e, at "correlationId" e with
                | Some eid, Some inv, Some v, Some corr ->
                    let expected = uuidV5 eventNamespace (inv + "/" + v + "/" + corr) |> string
                    let invOk = inv = investigationId
                    if eid <> expected then Some(sprintf "%s:%d eventId %s is not UUIDv5(N, %s/%s/%s) = %s" dir e.Line eid inv v corr expected)
                    elif not invOk then Some(sprintf "%s:%d investigationId %s differs from the directory" dir e.Line inv)
                    else None
                | _ -> None)
        let groups =
            // A correlationId names exactly one group of adjacent events (CONTRACT §5.4).
            events
            |> List.fold (fun (seen: Set<string>, prev: string option, problems) e ->
                match at "correlationId" e with
                | Some c when Some c = prev -> seen, prev, problems
                | Some c when seen.Contains c -> seen, Some c, problems @ [ sprintf "%s:%d correlationId %s reused by a non-adjacent group" dir e.Line c ]
                | Some c -> seen.Add c, Some c, problems
                | None -> seen, prev, problems) (Set.empty, None, [])
            |> fun (_, _, p) -> p
        let first =
            match events with
            | e :: _ when e.EventType <> "InvestigationStarted" -> [ sprintf "%s:1 history does not begin with InvestigationStarted" dir ]
            | _ -> []
        versionProblems @ idProblems @ groups @ first

    let loadHistory (reg: Registry) (variants: EventVariant list) (root: string) (catalogIndex: (string * (string * string) list) list) (dir: string) : HistoryFile =
        let relDir = rel root dir
        let eventsPath = Path.Combine(dir, "events.ndjson")
        let bytes = File.ReadAllBytes eventsPath
        let events =
            splitLines bytes
            |> Array.mapi (fun i line ->
                let d = Codec.decodeText reg "Envelope" line
                let et = Codec.at "eventType" d.Value |> Codec.asString |> Option.defaultValue "?"
                let payload = Codec.at "payload" d.Value |> Option.defaultValue TNull
                { File = relDir + "/events.ndjson"
                  Line = i + 1
                  Text = line
                  Decoded = d
                  EventType = et
                  Variant = Package.variantOfEvent variants et payload })
            |> List.ofArray
        let segs = relDir.Split('/')
        let kind, datasetId =
            match segs with
            | [| "workspaces"; "demo"; run; _; _ |] -> "workspace", "workspaces/demo/" + run
            | [| "templates"; t; _; _ |] -> "template", "templates/" + t
            | [| "fixtures"; v; _; _ |] -> "fixture", "fixtures/" + v
            | _ -> "other", relDir
        let invId = Path.GetFileName dir
        let memberOf = catalogIndex |> List.filter (fun (_, cases) -> cases |> List.exists (fst >> (=) relDir))
        { Dir = relDir
          DatasetKind = kind
          DatasetId = datasetId
          InvestigationId = invId
          EventsSha256 = Json.sha256Hex bytes
          Catalogs = memberOf |> List.map fst
          CaseKey = memberOf |> List.tryPick (fun (_, cases) -> cases |> List.tryFind (fst >> (=) relDir) |> Option.map snd)
          Events = events
          ManifestProblems = decodeJsonFile reg "Manifest" (Path.Combine(dir, "manifest.json"))
          SnapshotProblems = decodeJsonFile reg "Snapshot" (Path.Combine(dir, "snapshot.json"))
          IntegrityProblems = integrity relDir invId events }

    /// Every investigation directory in the data repository (workspaces, templates, valid fixtures).
    let discover (root: string) =
        let dirs =
            [ "workspaces"; "templates"; "fixtures" ]
            |> List.map (fun d -> Path.Combine(root, d))
            |> List.filter Directory.Exists
            |> List.collect (fun d ->
                Directory.EnumerateFiles(d, "events.ndjson", SearchOption.AllDirectories)
                |> Seq.map Path.GetDirectoryName
                |> List.ofSeq)
            |> List.sortBy (rel root)
        dirs, catalogs root

    let loadAll (reg: Registry) (variants: EventVariant list) (root: string) =
        let dirs, catalogIndex = discover root
        dirs |> List.map (loadHistory reg variants root catalogIndex), catalogIndex

    // ---- Representability against a parsed contract package ------------------------------------------------------

    /// Wire payload fields a case argument carries. An opaque payload type (`class end`) as the sole argument is "the domain owner's to
    /// define with exactly the wire fields", so they carry the whole record; an id type carries the payload's entity
    /// id (its first uuid field); a labelled argument carries the wire field of that name; an unlabelled NonEmptyText
    /// carries `reason`; anything else (Scope) has no wire source.
    let private carried (contract: FsContract) (payloadFields: RegistryField list) (args: CaseArg list) : Set<string> * string list =
        let entityId =
            payloadFields |> List.tryFind (fun f -> f.Def.Kind = KUuid && f.Def.Name.EndsWith "Id") |> Option.map (fun f -> f.Def.Name)
        args
        |> List.fold (fun (acc: Set<string>, unsourced) a ->
            if contract.OpaqueTypes.Contains a.Type && a.Label.IsNone && List.length args = 1 then Set.union acc (payloadFields |> List.map (fun f -> f.Def.Name) |> Set.ofList), unsourced
            else
                match a.Label with
                | Some l when payloadFields |> List.exists (fun f -> f.Def.Name = l) -> acc.Add l, unsourced
                | Some l -> acc, unsourced @ [ l ]
                | None when a.Type.EndsWith "Id" -> (match entityId with Some e -> acc.Add e | None -> acc), unsourced
                | None when a.Type = "NonEmptyText" -> acc.Add "reason", unsourced
                | None -> acc, unsourced @ [ a.Type ]) (Set.empty, [])

    /// Payload fields fixed by the variant itself: the discriminator and any field a rule pins for it.
    let private implied (reg: Registry) (v: EventVariant) : Set<string> =
        match v.Discriminator with
        | None -> Set.empty
        | Some(path, value) ->
            let pinned =
                reg.Rules
                |> List.filter (fun r -> r.ObjectType = "Payload." + v.EventType && r.WhenPath = path && r.WhenValue = value)
                |> List.choose (fun r -> match r.Constraint with MustEqual _ -> Some r.Field | _ -> None)
            Set.ofList (path :: pinned)

    let representPayload (reg: Registry) (contract: FsContract) (v: EventVariant) : Representation =
        match v.CoreEventCase with
        | None -> NoCase "the variant names no CoreEvent case"
        | Some caseName ->
            match FsContract.tryCase contract "CoreEvent" caseName with
            | None -> NoCase(sprintf "CoreEvent has no case %s" caseName)
            | Some c ->
                let payloadFields = FieldRegistry.childrenOf reg ("Payload." + v.EventType)
                let carriedSet, unsourced = carried contract payloadFields c.Args
                let all = payloadFields |> List.map (fun f -> f.Def.Name) |> Set.ofList
                let dropped = Set.difference all (Set.union carriedSet (implied reg v)) |> Set.toList
                // An argument with no wire source would have to be invented on read; it is reported with the loss.
                let invented = unsourced |> List.map (fun u -> sprintf "(argument %s has no wire source)" u)
                if List.isEmpty dropped && List.isEmpty invented then Lossless caseName else Lossy(caseName, dropped @ invented)

    /// Envelope fields the contract's EventEnvelope record cannot hold.
    let envelopeDropped (reg: Registry) (contract: FsContract) (binding: (string * string list) list) : string list =
        let record = contract.Records |> Map.tryFind "EventEnvelope" |> Option.defaultValue []
        let present = record |> List.map fst |> Set.ofList
        let covered = binding |> List.filter (fun (k, _) -> present.Contains k) |> List.collect snd |> Set.ofList
        FieldRegistry.childrenOf reg "Envelope" |> List.map (fun f -> f.Def.Name) |> List.filter (covered.Contains >> not)

    // ---- Domain transition replay -----------------------------------------------------------------------------------

    let replayTransitions (machines: DomainMachine list) (h: HistoryFile) : TransitionFinding list * int =
        let str path tv = Codec.at path tv |> Codec.asString
        let step (states: Map<string * string, string>, findings: TransitionFinding list, checkedCount: int) (e: EventRecord) =
            let payload = Codec.at "payload" e.Decoded.Value |> Option.defaultValue TNull
            machines
            |> List.fold (fun (states: Map<string * string, string>, findings, n) m ->
                let entityId = m.IdField |> Option.bind (fun f -> str f payload) |> Option.defaultValue "(investigation)"
                match m.Creation |> List.tryFind (fst >> (=) e.EventType) with
                | Some(_, initial) -> states.Add((m.MachineId, entityId), initial), findings, n
                | None ->
                    let candidates =
                        m.Transitions
                        |> List.filter (fun t -> t.EventType = e.EventType)
                        |> List.filter (fun t -> match t.When with None -> true | Some(p, v) -> Codec.at p payload |> Option.map Codec.encode = Some v)
                    if List.isEmpty candidates then states, findings, n
                    else
                        let target = m.TargetField |> Option.bind (fun f -> str f payload)
                        let byTarget = candidates |> List.filter (fun t -> target |> Option.map ((=) t.To) |> Option.defaultValue true)
                        let current = states.TryFind((m.MachineId, entityId))
                        let finding detail = { File = e.File; Line = e.Line; Machine = m.MachineId; EntityId = entityId; Detail = detail }
                        match byTarget, current with
                        | [], _ -> states, findings @ [ finding (sprintf "%s to %A has no transition in %s" e.EventType target m.MachineId) ], n + 1
                        | _, None -> states, findings @ [ finding (sprintf "%s on an entity never created" e.EventType) ], n + 1
                        | ts, Some cur ->
                            let fromStated = m.FromField |> Option.bind (fun f -> str f payload)
                            match ts |> List.tryFind (fun t -> List.contains cur t.From) with
                            | Some t ->
                                let fs =
                                    match fromStated with
                                    | Some f when f <> cur -> [ finding (sprintf "stored from=%s but replayed state is %s" f cur) ]
                                    | _ -> []
                                states.Add((m.MachineId, entityId), t.To), findings @ fs, n + 1
                            | None ->
                                let t = List.head ts
                                states.Add((m.MachineId, entityId), t.To),
                                findings @ [ finding (sprintf "%s %s -> %s is not in the transition table (allowed from: %s)" m.MachineId cur t.To (String.Join(", ", ts |> List.collect (fun x -> x.From) |> List.distinct))) ],
                                n + 1) (states, findings, checkedCount)
        let _, findings, n = h.Events |> List.fold step (Map.empty, [], 0)
        findings, n

    // ---- Provenance cross-reference (catalog/historical-v1/provenance) ----------------------------------------------

    let provenance (root: string) (histories: HistoryFile list) : ProvenanceCheck list =
        let provDir = Path.Combine(root, "catalog", "historical-v1", "provenance")
        if not (Directory.Exists provDir) then []
        else
            Directory.GetFiles(provDir, "*.json")
            |> Array.sort
            |> Array.map (fun f ->
                let j = Json.parseFile f
                let caseKey = Json.fieldStr "caseKey" j
                let records = Json.arrOf "evidence" j
                let history =
                    histories |> List.tryFind (fun h -> h.Dir.StartsWith "workspaces/demo/historical-v1/" && h.CaseKey = Some caseKey)
                    |> Option.orElse (histories |> List.tryFind (fun h -> h.CaseKey = Some caseKey))
                let admitted =
                    history
                    |> Option.map (fun h ->
                        h.Events
                        |> List.filter (fun e -> e.EventType = "EvidenceAdmitted")
                        |> List.choose (fun e ->
                            let p = Codec.at "payload" e.Decoded.Value |> Option.defaultValue TNull
                            match Codec.at "evidenceId" p |> Codec.asString, Codec.at "source" p |> Codec.asString with
                            | Some id, Some src -> Some(id, src)
                            | _ -> None))
                    |> Option.defaultValue []
                let admittedMap = Map.ofList admitted
                let recIds = records |> List.map (Json.fieldStr "evidenceId")
                { CaseKey = caseKey
                  Records = List.length records
                  EvidenceAdmitted = List.length admitted
                  MissingEvidence = recIds |> List.filter (admittedMap.ContainsKey >> not)
                  EvidenceWithoutProvenance = admitted |> List.map fst |> List.filter (fun id -> List.contains id recIds |> not)
                  UrlNotInSource =
                    records
                    |> List.choose (fun r ->
                        let id = Json.fieldStr "evidenceId" r
                        match admittedMap.TryFind id, Json.strOpt "url" r with
                        | Some src, Some url when not (src.Contains url) -> Some id
                        | _ -> None) })
            |> List.ofArray

    // ---- Negative fixtures (contract-level refusal, informative) ----------------------------------------------------

    let negativeFixtures (reg: Registry) (root: string) : NegativeFixture list =
        let dir = Path.Combine(root, "fixtures", "v1.0.0", "negative")
        if not (Directory.Exists dir) then []
        else
            Directory.GetFiles(dir, "*.json")
            |> Array.sort
            |> Array.map (fun f ->
                let j = Json.parseFile f
                let files = Json.tryField "files" j |> Option.defaultValue (JObj [])
                let events =
                    Json.tryField "events.ndjson" files
                    |> Option.bind (Json.strOpt "base64")
                    |> Option.map (fun b -> Convert.FromBase64String b)
                let problems =
                    match events with
                    | Some bytes ->
                        let text = Encoding.UTF8.GetString bytes
                        let framing =
                            if bytes.Length = 0 || text.Contains '\r' || not (text.EndsWith "\n") || text.Contains "\n\n" || (bytes.Length >= 3 && bytes.[0] = 0xEFuy && bytes.[1] = 0xBBuy && bytes.[2] = 0xBFuy) then [ "framing" ]
                            else []
                        framing @ (splitLines bytes |> Array.toList |> List.collect (fun l -> (Codec.decodeText reg "Envelope" l).Problems |> List.map Codec.problemText))
                    | None -> []
                { Name = Path.GetFileNameWithoutExtension f
                  ExpectedCode = Json.fieldStr "expectedErrorCode" j
                  ExpectedDomainCode = Json.strOpt "expectedDomainCode" j
                  HasEvents = events.IsSome
                  RefusedByRegistry = not (List.isEmpty problems)
                  FirstProblem = List.tryHead problems })
            |> List.ofArray
