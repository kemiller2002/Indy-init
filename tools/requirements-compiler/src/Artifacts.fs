/// Builds every generated artifact from the compiled state. All lists are sorted ordinally,
/// so the output depends only on the inputs.
module Artifacts

open Json
open Model
open Normalize

type Compiled =
    { SourceRepo: string
      SourceCommit: string
      BaselineId: string
      BaselineSha256: string
      Documents: Json list
      Requirements: Requirement list
      Categories: Map<string, string>
      Tags: Map<string, string list>
      References: Map<string, string list>
      Flags: Map<string, Flag list>
      Duplicates: OverlapGroup list
      Containments: OverlapGroup list
      NearDuplicates: OverlapGroup list
      FindingsByRequirement: Map<string, Json list>
      Coverage: Map<string, string list>
      RulesFor: Map<string, VerificationRule list>
      Order: Result<string list, Graph.Cycle>
      CitationResults: Map<Citation, CitationResult>
      IdentityResults: Map<string, Json list>
      MetricIds: Set<string>
      Curation: Curation }

let private sortedById (xs: Json list) = xs |> List.sortBy (str "id")

let private citationJson (c: Compiled) (cit: Citation) =
    let r = c.CitationResults.[cit]

    JObj
        [ "repo", JStr cit.Repo
          "path", JStr cit.Path
          "quote", JStr cit.Quote
          "verified", JBool r.Verified
          "detail", JStr r.Detail ]

let isOpenMaterial (f: Json) =
    boolOr "material" false f && (strOpt "status" f |> Option.defaultValue "") = "unresolved"

/// A requirement is flagged for review when a material unresolved finding names it,
/// or a mechanical flag of major severity applies.
let reviewReasons (c: Compiled) (r: Requirement) =
    let findings =
        c.FindingsByRequirement.TryFind r.Id |> Option.defaultValue [] |> List.filter isOpenMaterial |> List.map (str "id")

    let flags = c.Flags.[r.Id] |> List.filter (fun f -> f.Severity = "major") |> List.map (fun f -> f.Code)
    findings @ flags

let private summaryCounts (pairs: (string * int) list) =
    pairs |> List.sortBy fst |> List.map (fun (k, n) -> k, jint n) |> JObj

let private countBy (f: 'a -> string) (xs: 'a list) =
    xs |> List.countBy f |> summaryCounts

// ---- inventory -------------------------------------------------------------------

let inventory (c: Compiled) =
    let statusBasis = str "implementationStatusBasis" c.Curation.Sources

    let reqJson (r: Requirement) =
        let findings = c.FindingsByRequirement.TryFind r.Id |> Option.defaultValue []
        let ofType t = findings |> List.filter (fun f -> str "type" f = t) |> List.map (str "id")
        let mechanical = c.Flags.[r.Id] |> List.filter (fun f -> List.contains f.Code [ "PRIORITY-UNSTATED"; "PRIORITY-IN-TITLE-ONLY"; "VAGUE-TERMS"; "MODALITY-UNMARKED" ])

        JObj
            [ "id", JStr r.Id
              "title", JStr r.Title
              "origin", JStr r.Origin
              "priority", JStr r.Priority
              "category", JStr c.Categories.[r.Id]
              "tags", jstrs c.Tags.[r.Id]
              "source",
              JObj
                  [ "repository", JStr r.Repository
                    "commit", JStr r.Commit
                    "document", JStr r.Document
                    "heading", JStr r.Heading
                    "sectionSha256", JStr r.SectionSha256 ]
              "obligations",
              r.Obligations
              |> List.map (fun o ->
                  JObj
                      [ "id", JStr o.Id
                        "kind", JStr o.Kind
                        "modality", JStr(Text.modalityName (Text.modalityOf o.Text))
                        "text", JStr o.Text
                        "tokens", jstrs o.Tokens ])
              |> JArr
              "implementationStatus", JObj [ "status", JStr "not-verified"; "basis", JStr statusBasis ]
              "acceptanceCriteria",
              JObj [ "ownObligations", jstrs (ownAcceptance r); "linkedSections", jstrs (linkedAcceptance c.Requirements r) ]
              "dependencies",
              JObj
                  [ "referencedRequirements", jstrs c.References.[r.Id]
                    "workItems", jstrs (c.Coverage.TryFind r.Id |> Option.defaultValue []) ]
              "ambiguities",
              JObj
                  [ "findings", jstrs (ofType "ambiguity")
                    "mechanical", mechanical |> List.map (fun f -> JObj [ "code", JStr f.Code; "detail", JStr f.Detail ]) |> JArr ]
              "conflicts", jstrs (ofType "conflict")
              "verification", JStr("VM-" + r.Id) ]

    JObj
        [ "schema", JStr "indy-init.requirements-inventory/1"
          "source",
          JObj
              [ "repository", JStr c.SourceRepo
                "commit", JStr c.SourceCommit
                "baselineId", JStr c.BaselineId
                "baselineSha256", JStr c.BaselineSha256 ]
          "summary",
          JObj
              [ "requirements", jint c.Requirements.Length
                "obligations", jint (c.Requirements |> List.sumBy (fun r -> r.Obligations.Length))
                "byOrigin", countBy (fun (r: Requirement) -> r.Origin) c.Requirements
                "byPriority", countBy (fun (r: Requirement) -> r.Priority) c.Requirements
                "byCategory", countBy (fun (r: Requirement) -> c.Categories.[r.Id]) c.Requirements
                "obligationsByKind", countBy (fun (o: Obligation) -> o.Kind) (c.Requirements |> List.collect (fun r -> r.Obligations))
                "implementationStatus", JObj [ "not-verified", jint c.Requirements.Length ] ]
          "documents", JArr c.Documents
          "requirements", c.Requirements |> List.sortBy (fun r -> r.Id) |> List.map reqJson |> JArr ]

// ---- normalized ------------------------------------------------------------------

let private overlapJson (g: OverlapGroup) =
    JObj
        [ "id", JStr g.Id
          "kind", JStr g.Kind
          "requirements", jstrs g.Requirements
          "obligations", jstrs g.Obligations
          "similarity", jopt JNum g.Similarity
          "text", JStr g.Text ]

let overlapIndex (c: Compiled) =
    c.Duplicates @ c.Containments @ c.NearDuplicates
    |> List.collect (fun g -> g.Requirements |> List.map (fun r -> r, g.Id))
    |> List.groupBy fst
    |> List.map (fun (r, xs) -> r, xs |> List.map snd |> List.distinct |> List.sort)
    |> Map.ofList

let normalized (c: Compiled) =
    let overlaps = overlapIndex c

    let reqJson (r: Requirement) =
        let reasons = reviewReasons c r
        let own = ownAcceptance r
        let linked = linkedAcceptance c.Requirements r

        let acceptanceStatus =
            if isAcceptanceSection r then "is-acceptance-section"
            elif not own.IsEmpty then "present"
            elif not linked.IsEmpty then "document-level-only"
            else "missing"

        JObj
            [ "id", JStr("NR-" + r.Id)
              "source", JStr r.Id
              "origin", JStr r.Origin
              "title", JStr r.Title
              "priority", JStr r.Priority
              "modality", JStr(Text.modalityName (strongestModality r))
              "category", JStr c.Categories.[r.Id]
              "tags", jstrs c.Tags.[r.Id]
              "obligations", jstrs (r.Obligations |> List.map (fun o -> o.Id))
              "textPolicy", JStr "verbatim: obligation text is carried only in requirements-inventory.json and is not reworded"
              "acceptance", JObj [ "status", JStr acceptanceStatus; "own", jstrs own; "linkedSections", jstrs linked ]
              "dependencies", jstrs c.References.[r.Id]
              "overlaps", jstrs (overlaps.TryFind r.Id |> Option.defaultValue [])
              "flags",
              c.Flags.[r.Id]
              |> List.map (fun f -> JObj [ "code", JStr f.Code; "severity", JStr f.Severity; "detail", JStr f.Detail ])
              |> JArr
              "findings", jstrs (c.FindingsByRequirement.TryFind r.Id |> Option.defaultValue [] |> List.map (str "id"))
              "reviewStatus", JStr(if reasons.IsEmpty then "normalized" else "flagged-for-review")
              "reviewReasons", jstrs reasons ]

    let reqs = c.Requirements |> List.sortBy (fun r -> r.Id)
    let flagged = reqs |> List.filter (fun r -> not (reviewReasons c r).IsEmpty)

    JObj
        [ "schema", JStr "indy-init.normalized-requirements/1"
          "policy",
          jstrs
              [ "One normalized requirement per inventoried requirement; nothing is merged, dropped or reworded."
                "Overlaps are recorded as groups that point at both sources; a person decides any consolidation."
                "Modality is read from the text and never promoted."
                "A requirement named by a material unresolved finding, or carrying a major flag, is flagged-for-review." ]
          "summary",
          JObj
              [ "normalized", jint reqs.Length
                "flaggedForReview", jint flagged.Length
                "exactDuplicateGroups", jint c.Duplicates.Length
                "structuralContainments", jint c.Containments.Length
                "nearDuplicatePairs", jint c.NearDuplicates.Length
                "flagsByCode", countBy (fun (f: Flag) -> f.Code) (reqs |> List.collect (fun r -> c.Flags.[r.Id])) ]
          "overlapGroups", (c.Duplicates @ c.Containments @ c.NearDuplicates) |> List.map overlapJson |> JArr
          "requirements", reqs |> List.map reqJson |> JArr ]

let sourceMap (c: Compiled) =
    JObj
        [ "schema", JStr "indy-init.requirements-source-map/1"
          "description", JStr "Normalized requirement -> original requirement -> source document section, with every obligation id. Obligation text is in requirements-inventory.json."
          "entries",
          c.Requirements
          |> List.sortBy (fun r -> r.Id)
          |> List.map (fun r ->
              JObj
                  [ "normalized", JStr("NR-" + r.Id)
                    "requirement", JStr r.Id
                    "repository", JStr r.Repository
                    "commit", JStr r.Commit
                    "document", JStr r.Document
                    "heading", JStr r.Heading
                    "sectionSha256", JStr r.SectionSha256
                    "obligations", jstrs (r.Obligations |> List.map (fun o -> o.Id)) ])
          |> JArr ]

// ---- decisions and contracts -------------------------------------------------------

let decisionVerified (c: Compiled) (d: Json) =
    let cits = citationsOf "sources" d
    not cits.IsEmpty && cits |> List.forall (fun x -> c.CitationResults.[x].Verified)

let decisionRegistry (c: Compiled) =
    let decisions = sortedById c.Curation.Decisions

    let dJson (d: Json) =
        match d with
        | JObj props ->
            let kept = props |> List.filter (fun (k, _) -> k <> "sources")

            JObj(
                kept
                @ [ "sources", citationsOf "sources" d |> List.map (citationJson c) |> JArr
                    "sourcesVerified", JBool(decisionVerified c d) ]
            )
        | other -> other

    JObj
        [ "schema", JStr "indy-init.decision-registry/1"
          "classes",
          jstrs [ "fixed-architectural"; "verified-reusable"; "implementation"; "runtime-situational"; "unresolved" ]
          "rule", JStr "Agents must not reconsider a fixed-architectural decision without recorded evidence of incompatibility (DESIGN-DECISION-BOUNDARIES section 6)."
          "summary", countBy (str "classification") decisions
          "decisions", decisions |> List.map dJson |> JArr ]

let contractBindings (c: Compiled) =
    let contracts = sortedById c.Curation.Contracts

    let cJson (k: Json) =
        match k with
        | JObj props ->
            let kept = props |> List.filter (fun (n, _) -> n <> "citations")

            JObj(
                kept
                @ [ "citations", citationsOf "citations" k |> List.map (citationJson c) |> JArr
                    "identityResults", JArr(c.IdentityResults.TryFind(str "id" k) |> Option.defaultValue []) ]
            )
        | other -> other

    JObj
        [ "schema", JStr "indy-init.contract-bindings/1"
          "statusVocabulary",
          JObj
              [ "missing", JStr "no versioned contract exists yet"
                "unverified", JStr "contract identified, nothing checked"
                "identity-verified", JStr "the pinned artifacts hash to the recorded identities; compatibility not exercised"
                "blocked", JStr "cannot be verified until a named precondition is met" ]
          "summary", countBy (str "status") contracts
          "contracts", contracts |> List.map cJson |> JArr ]

// ---- dependency graph ------------------------------------------------------------------

let dependencyGraph (c: Compiled) =
    let tasks = c.Curation.Tasks |> List.sortBy (fun t -> t.Id)

    match c.Order with
    | Error cycle ->
        JObj [ "schema", JStr "indy-init.dependency-graph/1"; "acyclic", JBool false; "cycle", jstrs cycle ]
    | Ok order ->
        let depth = Graph.depthFrom order tasks
        let critical = Graph.criticalPath order tasks
        let coveredBy = c.Coverage |> Map.toList |> List.collect (fun (r, ts) -> ts |> List.map (fun t -> t, r)) |> List.groupBy fst |> Map.ofList

        let node (t: WorkTask) =
            JObj
                [ "id", JStr t.Id
                  "title", JStr t.Title
                  "owner", JStr t.Owner
                  "kind", JStr t.Kind
                  "satisfiedBy", JStr t.SatisfiedBy
                  "capability", jopt JStr t.Capability
                  "depth", jint depth.[t.Id]
                  "onCriticalPath", JBool(List.contains t.Id critical)
                  "covers", jstrs t.Covers
                  "coveredRequirementCount", jint (coveredBy.TryFind t.Id |> Option.map List.length |> Option.defaultValue 0)
                  "contracts", jstrs t.Contracts
                  "decisions", jstrs t.Decisions
                  "gates", jstrs t.Gates
                  "completion", jstrs t.Completion ]

        let levels =
            tasks
            |> List.groupBy (fun t -> depth.[t.Id])
            |> List.sortBy fst
            |> List.map (fun (d, ts) -> JObj [ "depth", jint d; "parallelizable", jstrs (ts |> List.map (fun t -> t.Id)) ])

        JObj
            [ "schema", JStr "indy-init.dependency-graph/1"
              "edgePolicy",
              JStr "dependsOn edges are genuine prerequisites (data, contract or evidence flows from the prerequisite); preferenceAfter edges are ordering preferences and are never scheduled"
              "acyclic", JBool true
              "summary",
              JObj
                  [ "tasks", jint tasks.Length
                    "genuineEdges", jint (tasks |> List.sumBy (fun t -> t.DependsOn.Length))
                    "preferenceEdges", jint (tasks |> List.sumBy (fun t -> t.Preferences.Length))
                    "criticalPathLength", jint critical.Length
                    "maxDepth", jint (depth |> Map.toList |> List.map snd |> List.fold max 0)
                    "bySatisfiedBy", countBy (fun (t: WorkTask) -> t.SatisfiedBy) tasks
                    "byOwner", countBy (fun (t: WorkTask) -> t.Owner) tasks ]
              "nodes", tasks |> List.map node |> JArr
              "edges",
              tasks
              |> List.collect (fun t -> t.DependsOn |> List.map (fun d -> JObj [ "from", JStr d.Task; "to", JStr t.Id; "reason", JStr d.Reason ]))
              |> JArr
              "preferenceEdges",
              tasks
              |> List.collect (fun t -> t.Preferences |> List.map (fun d -> JObj [ "after", JStr d.Task; "task", JStr t.Id; "reason", JStr d.Reason ]))
              |> JArr
              "topologicalOrder", jstrs order
              "criticalPath", jstrs critical
              "parallelLevels", JArr levels
              "requirementCoverage",
              c.Requirements
              |> List.sortBy (fun r -> r.Id)
              |> List.map (fun r -> JObj [ "requirement", JStr r.Id; "tasks", jstrs (c.Coverage.TryFind r.Id |> Option.defaultValue []) ])
              |> JArr ]

// ---- verification ------------------------------------------------------------------------

let verificationManifest (c: Compiled) =
    let defaults = field "verificationDefaults" c.Curation.Sources |> Option.defaultValue (JObj [])
    let contractsOf rid =
        c.Coverage.TryFind rid
        |> Option.defaultValue []
        |> List.collect (fun t -> (c.Curation.Tasks |> List.find (fun x -> x.Id = t)).Contracts)
        |> List.distinct
        |> List.sort

    let entry (r: Requirement) =
        let rules = c.RulesFor.TryFind r.Id |> Option.defaultValue []
        let union f = rules |> List.collect f |> List.distinct

        JObj
            [ "id", JStr("VM-" + r.Id)
              "requirement", JStr r.Id
              "priority", JStr r.Priority
              "rules", jstrs (rules |> List.map (fun x -> x.Id))
              "gates", jstrs (union (fun x -> [ x.Gate ]) |> List.sort)
              "owners", jstrs (union (fun x -> [ x.Owner ]) |> List.sort)
              "acceptanceTest", JObj [ "criteria", jstrs (ownAcceptance r); "linkedSections", jstrs (linkedAcceptance c.Requirements r) ]
              "dependenciesAffectingValidity", JObj [ "requirements", jstrs c.References.[r.Id]; "contracts", jstrs (contractsOf r.Id) ]
              "currentStatus", JStr "NOT_RUN" ]

    JObj
        [ "schema", JStr "indy-init.verification-manifest/1"
          "statusVocabulary", jstrs [ "NOT_RUN"; "PASS"; "FAIL"; "BLOCKED" ]
          "howToRead",
          JStr "Each entry lists the rules that apply to its requirement. The acceptance test is the entry's own criteria and linked acceptance sections, checked by each rule's method; required evidence, freshness and invalidation are the union of the defaults and of every listed rule."
          "defaults",
          JObj
              [ "freshness", jstrs (strs "freshness" defaults)
                "invalidatedBy", jstrs (strs "invalidatedBy" defaults)
                "evidenceReuse", JStr(str "evidenceReuse" defaults) ]
          "rules",
          c.Curation.Rules
          |> List.map (fun x ->
              JObj
                  [ "id", JStr x.Id
                    "match", jstrs x.Match
                    "gate", JStr x.Gate
                    "owner", JStr x.Owner
                    "method", JStr x.Method
                    "requiredEvidence", jstrs x.Evidence
                    "freshness", jstrs x.Freshness
                    "invalidatedBy", jstrs x.InvalidatedBy
                    "tooling", JStr x.Tooling ])
          |> JArr
          "summary",
          JObj
              [ "entries", jint c.Requirements.Length
                "byGate",
                c.Requirements
                |> List.collect (fun r -> c.RulesFor.TryFind r.Id |> Option.defaultValue [] |> List.map (fun x -> x.Gate) |> List.distinct)
                |> List.countBy id
                |> summaryCounts
                "byOwner",
                c.Requirements
                |> List.collect (fun r -> c.RulesFor.TryFind r.Id |> Option.defaultValue [] |> List.map (fun x -> x.Owner) |> List.distinct)
                |> List.countBy id
                |> summaryCounts ]
          "entries", c.Requirements |> List.sortBy (fun r -> r.Id) |> List.map entry |> JArr ]

// ---- execution plans -------------------------------------------------------------------------

type PlanSpec =
    { Key: string
      Name: string
      Objective: string
      LaneOf: WorkTask -> string
      Unbounded: Set<string>
      CiTriggerKinds: Set<string>
      Spec: Json }

let executionPlan (c: Compiled) (p: PlanSpec) =
    match c.Order with
    | Error _ -> JObj [ "schema", JStr "indy-init.execution-plan/1"; "plan", JStr p.Key; "error", JStr "dependency graph has a cycle" ]
    | Ok order ->
        let tasks = c.Curation.Tasks
        let byId = tasks |> List.map (fun t -> t.Id, t) |> Map.ofList
        let slots = Graph.schedule p.LaneOf p.Unbounded order tasks
        let stepOf = slots |> List.map (fun s -> s.Task, s.Step) |> Map.ofList
        let laneOf = slots |> List.map (fun s -> s.Task, s.Lane) |> Map.ofList
        let steps = slots |> List.groupBy (fun s -> s.Step) |> List.sortBy fst

        let handoffs =
            tasks |> List.sumBy (fun t -> t.DependsOn |> List.filter (fun d -> laneOf.[d.Task] <> laneOf.[t.Id]) |> List.length)

        let lanes = slots |> List.map (fun s -> s.Lane) |> List.distinct |> List.sort
        let ciRuns = tasks |> List.filter (fun t -> p.CiTriggerKinds.Contains t.Kind) |> List.length

        JObj
            [ "schema", JStr "indy-init.execution-plan/1"
              "plan", JStr p.Key
              "name", JStr p.Name
              "objective", JStr p.Objective
              "scope",
              JObj
                  [ "tasks", jint tasks.Length
                    "requirementsCovered", jint c.Coverage.Count
                    "acceptance", JStr "identical for both plans: every entry of verification-manifest.json; no requirement is dropped or weakened" ]
              "lanes", jstrs lanes
              "assumptions", JArr(arr "assumptions" p.Spec)
              "operatorDecisionsRequired", JArr(arr "operatorDecisionsRequired" p.Spec)
              "plannedStructure",
              JObj
                  [ "steps", jint steps.Length
                    "maxConcurrentTasks", jint (steps |> List.map (snd >> List.length) |> List.fold max 0)
                    "crossLaneHandoffs", jint handoffs
                    "plannedCiRunsUpperBound", jint ciRuns
                    "ciTriggerKinds", jstrs (Set.toList p.CiTriggerKinds)
                    "note", JStr "Structural counts derived from the graph. They are not measurements and not time estimates." ]
              "integrationCheckpoints",
              tasks
              |> List.filter (fun t -> t.Kind = "integration-checkpoint")
              |> List.sortBy (fun t -> stepOf.[t.Id], t.Id)
              |> List.map (fun t -> JObj [ "task", JStr t.Id; "step", jint stepOf.[t.Id]; "gates", jstrs t.Gates ])
              |> JArr
              "steps",
              steps
              |> List.map (fun (n, ss) ->
                  JObj [ "step", jint n; "tasks", ss |> List.sortBy (fun s -> s.Task) |> List.map (fun s -> JObj [ "task", JStr s.Task; "lane", JStr s.Lane ]) |> JArr ])
              |> JArr
              "tasks",
              order
              |> List.map (fun id ->
                  let t = byId.[id]

                  JObj
                      [ "id", JStr id
                        "lane", JStr laneOf.[id]
                        "step", jint stepOf.[id]
                        "dependsOn", jstrs (t.DependsOn |> List.map (fun d -> d.Task))
                        "completion", jstrs t.Completion
                        "gates", jstrs t.Gates ])
              |> JArr ]

// ---- experiment -------------------------------------------------------------------------------

let perceptaExperiment (c: Compiled) =
    let e = c.Curation.Experiment
    let metricIds = arr "metrics" e |> List.map (str "perceptaMetric") |> List.filter (fun m -> m <> "")

    match e with
    | JObj props ->
        JObj(
            props
            @ [ "metricVerification",
                metricIds
                |> List.distinct
                |> List.sort
                |> List.map (fun m -> JObj [ "metric", JStr m; "definedInPercepta", JBool(c.MetricIds.Contains m) ])
                |> JArr ]
        )
    | other -> other

// ---- readiness: package state, preflight and timed-execution manifests --------------------------

/// Deterministic evaluation of a package-state preflight condition: (status, detail).
/// Conditions that need the validation report are left to the preflight run.
let packageState (c: Compiled) (condition: string) : string * string =
    let ids (xs: Json list) = xs |> List.map (str "id") |> List.sort |> String.concat ", "

    match condition with
    | "no-unresolved-decisions" ->
        match c.Curation.Decisions |> List.filter (fun d -> str "classification" d = "unresolved") with
        | [] -> "PASS", "no unresolved decision"
        | ds -> "BLOCKED", "unresolved: " + ids ds
    | "no-open-critical-findings" ->
        match c.Curation.Findings |> List.filter (fun f -> isOpenMaterial f && str "severity" f = "critical") with
        | [] -> "PASS", "no open critical finding"
        | fs -> "BLOCKED", "open: " + ids fs
    | "no-blocked-contracts" ->
        match c.Curation.Contracts |> List.filter (fun k -> str "status" k = "blocked") with
        | [] -> "PASS", "no blocked contract"
        | ks -> "BLOCKED", "blocked: " + ids ks
    | "identities-match" ->
        let results = c.IdentityResults |> Map.toList |> List.collect snd
        if not results.IsEmpty && results |> List.forall (boolOr "match" false) then "PASS", sprintf "%d identities match" results.Length
        else "FAIL", "an identity does not match"
    | _ -> "requires-execution", "evaluated by `requirements-compiler preflight`"

let private taskById (c: Compiled) = c.Curation.Tasks |> List.map (fun t -> t.Id, t) |> Map.ofList

let private coveredBy (c: Compiled) =
    c.Coverage |> Map.toList |> List.collect (fun (r, ts) -> ts |> List.map (fun t -> t, r)) |> List.groupBy fst
    |> List.map (fun (t, xs) -> t, xs |> List.map snd) |> Map.ofList

let private priorityOf (c: Compiled) =
    c.Requirements |> List.map (fun r -> r.Id, r.Priority) |> Map.ofList

let preflightManifest (c: Compiled) =

    let check (k: Json) =
        let status, detail =
            if str "type" k = "package-state" then packageState c (field "params" k |> Option.map (str "condition") |> Option.defaultValue "")
            elif str "type" k = "manual" then "NOT_RUN", "needs an owner or organizer record; never passes automatically"
            else "requires-execution", "evaluated by `requirements-compiler preflight`"

        JObj
            [ "id", JStr(str "id" k)
              "category", JStr(str "category" k)
              "title", JStr(str "title" k)
              "type", JStr(str "type" k)
              "params", field "params" k |> Option.defaultValue (JObj [])
              "tasks", jstrs (strs "tasks" k)
              "refs", jstrs (strs "refs" k)
              "packageStatus", JStr status
              "detail", JStr detail ]

    let tasks = c.Curation.Tasks |> List.filter (fun t -> t.Phase = "preflight") |> List.sortBy (fun t -> t.Id)

    JObj
        [ "schema", JStr "indy-init.preflight-manifest/1"
          "run", JStr "Rehearsal 4 (write-path dress rehearsal)"
          "boundary", JStr "Untimed. Ends at the first green bootstrap CI, where the 420-minute timer starts (REHEARSAL-004-WRITE-PATH-PLAN P5)."
          "competitionUse", JStr "Rehearsal preflight artifacts are not automatically permissible at the event: organizer confirmation is check PRE-ORG-01 (ADRP-016)."
          "statusVocabulary",
          JObj
              [ "PASS", JStr "satisfied by the package state"
                "BLOCKED", JStr "a named owner decision, upstream release or contract is missing"
                "FAIL", JStr "a deterministic check failed"
                "NOT_RUN", JStr "needs a person's record"
                "requires-execution", JStr "only `requirements-compiler preflight` can decide it; see readiness/evidence" ]
          "summary",
          countBy (fun (k: Json) -> fst (if str "type" k = "package-state" then packageState c (field "params" k |> Option.map (str "condition") |> Option.defaultValue "") elif str "type" k = "manual" then "NOT_RUN", "" else "requires-execution", "")) c.Curation.PreflightChecks
          "checks", c.Curation.PreflightChecks |> List.sortBy (str "id") |> List.map check |> JArr
          "tasks",
          tasks
          |> List.map (fun t ->
              JObj
                  [ "id", JStr t.Id
                    "title", JStr t.Title
                    "owner", JStr t.Owner
                    "satisfiedBy", JStr t.SatisfiedBy
                    "dependsOn", jstrs (t.DependsOn |> List.map (fun d -> d.Task))
                    "decisions", jstrs t.Decisions
                    "contracts", jstrs t.Contracts
                    "completion", jstrs t.Completion
                    "gates", jstrs t.Gates ])
          |> JArr ]


/// The workstream a timed task belongs to, as named in the readiness mission.
let private workstream (t: WorkTask) =
    match t.Id, t.Owner, t.Kind with
    | ("T18" | "T21"), _, _ -> "live-provider-integration"
    | "T32", _, _ -> "defect-correction"
    | "T34", _, _ -> "final-acceptance"
    | _, "reviewer", _ -> "independent-review"
    | _, _, "integration-checkpoint" -> "integration-testing"
    | _, "persistence", _ -> "persistence-implementation"
    | _, "ui", _ -> "ui-implementation"
    | _, "domain", _ -> "application-implementation"
    | _ -> "integration-testing"

let timedExecutionManifest (c: Compiled) (plan: PlanSpec) =
    match c.Order with
    | Error _ -> JObj [ "schema", JStr "indy-init.timed-execution-manifest/1"; "error", JStr "dependency graph has a cycle" ]
    | Ok order ->
        let byId = taskById c
        let slots = Graph.schedule plan.LaneOf plan.Unbounded order c.Curation.Tasks
        let timed = slots |> List.filter (fun s -> byId.[s.Task].Phase = "timed")
        let stepRank = timed |> List.map (fun s -> s.Step) |> List.distinct |> List.sort |> List.mapi (fun i s -> s, i + 1) |> Map.ofList
        let covered = coveredBy c
        let priority = priorityOf c
        let tests = c.Curation.DefectTests |> List.groupBy (str "task") |> Map.ofList
        let p0 = c.Requirements |> List.filter (fun r -> r.Priority = "P0") |> List.map (fun r -> r.Id)

        let p0InTimed =
            p0 |> List.filter (fun r -> c.Coverage.TryFind r |> Option.defaultValue [] |> List.exists (fun t -> byId.[t].Phase = "timed"))

        let task (s: Graph.Slot) =
            let t = byId.[s.Task]
            let reqs = covered.TryFind t.Id |> Option.defaultValue []

            JObj
                [ "id", JStr t.Id
                  "title", JStr t.Title
                  "workstream", JStr(workstream t)
                  "lane", JStr s.Lane
                  "step", jint stepRank.[s.Step]
                  "kind", JStr t.Kind
                  "dependsOn", jstrs (t.DependsOn |> List.filter (fun d -> byId.[d.Task].Phase = "timed") |> List.map (fun d -> d.Task))
                  "preflightPrerequisites", jstrs (t.DependsOn |> List.filter (fun d -> byId.[d.Task].Phase <> "timed") |> List.map (fun d -> d.Task))
                  "conditionalOn", jopt JStr t.ConditionalOn
                  "excludedBy", jopt JStr t.ExcludedBy
                  "ciRun", JBool(plan.CiTriggerKinds.Contains t.Kind)
                  "requirementsCovered", jint reqs.Length
                  "p0RequirementsCovered", jint (reqs |> List.filter (fun r -> priority.[r] = "P0") |> List.length)
                  "defectClassTests", jstrs (tests.TryFind t.Id |> Option.defaultValue [] |> List.map (str "id"))
                  "contracts", jstrs t.Contracts
                  "gates", jstrs t.Gates
                  "completion", jstrs t.Completion ]

        JObj
            [ "schema", JStr "indy-init.timed-execution-manifest/1"
              "run", JStr "Rehearsal 4 (write-path dress rehearsal)"
              "plan", JStr(plan.Key + " (default; Plan A's parallel lanes need owner approval, F-007)")
              "clock",
              JObj
                  [ "startsAt", JStr "first green bootstrap CI (task T10, REHEARSAL-004-WRITE-PATH-PLAN P5)"
                    "ceilingMinutes", jint 420
                    "stopRule", JStr "stop when the computed verdict is ACCEPTED, or at 420 minutes (R4-07)" ]
              "preconditions", JStr "Every check in preflight-manifest.json is PASS, or NOT_RUN only for PRE-ORG-01 (event-only)."
              "exclusions", JStr "A task with excludedBy stays on the plan until the operator's approvals file (T09) covers the exclusion; agents do not write that file (AR-003, RDP-003)."
              "milestones",
              JArr
                  [ JObj [ "milestone", JStr "first green CI"; "task", JStr "T10" ]
                    JObj [ "milestone", JStr "first verified remote write"; "task", JStr "T21" ]
                    JObj [ "milestone", JStr "first verified conflict handling"; "task", JStr "T21" ]
                    JObj [ "milestone", JStr "first deployed candidate"; "task", JStr "T31" ]
                    JObj [ "milestone", JStr "reviewer findings per round"; "task", JStr "T30, T33" ]
                    JObj [ "milestone", JStr "computed verdict and time to it"; "task", JStr "T34" ] ]
              "measurements",
              jstrs
                  [ "CI runs created, executed and failed"
                    "tokens per agent (Praxis >= 3.17 attribution)"
                    "defect classes, recurrence against R2 and R3, fix-induced defects"
                    "unavailable values are recorded as unavailable (R4 section 5)" ]
              "coverage",
              JObj
                  [ "p0Requirements", jint p0.Length
                    "p0CoveredByTimedTasks", jint p0InTimed.Length
                    "p0CoveredOnlyByPreflight", jstrs (p0 |> List.filter (fun r -> not (List.contains r p0InTimed))) ]
              "summary",
              JObj
                  [ "tasks", jint timed.Length
                    "steps", jint stepRank.Count
                    "plannedCiRuns", jint (timed |> List.filter (fun s -> plan.CiTriggerKinds.Contains byId.[s.Task].Kind) |> List.length)
                    "byWorkstream", countBy (fun (s: Graph.Slot) -> workstream byId.[s.Task]) timed ]
              "tasks", timed |> List.sortBy (fun s -> s.Step, s.Task) |> List.map task |> JArr ]
