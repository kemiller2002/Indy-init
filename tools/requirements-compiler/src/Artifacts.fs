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
