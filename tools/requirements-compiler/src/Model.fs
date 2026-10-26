/// Core records. Curated analysis (findings, decisions, contracts) stays as JSON and is read
/// through accessors; only the structures the compiler reasons about are typed.
module Model

open Json

type Obligation =
    { Id: string
      Kind: string
      Text: string
      Tokens: string list }

type Requirement =
    { Id: string
      Title: string
      Priority: string
      Document: string
      Heading: string
      SectionSha256: string
      Repository: string
      Commit: string
      Origin: string
      Obligations: Obligation list }

type Repo = { Name: string; Path: string; Commit: string }

type Citation = { Repo: string; Path: string; Quote: string }

type CitationResult = { Citation: Citation; Verified: bool; Detail: string }

type TaskDep = { Task: string; Reason: string }

type WorkTask =
    { Id: string
      Title: string
      Owner: string
      Kind: string
      SatisfiedBy: string
      Capability: string option
      Covers: string list
      DependsOn: TaskDep list
      Preferences: TaskDep list
      Completion: string list
      Gates: string list
      Contracts: string list
      Decisions: string list
      Phase: string
      ConditionalOn: string option
      Raw: Json }

type VerificationRule =
    { Id: string
      Match: string list
      Gate: string
      Owner: string
      Method: string
      Evidence: string list
      Freshness: string list
      InvalidatedBy: string list
      Tooling: string }

type Curation =
    { Sources: Json
      Findings: Json list
      Decisions: Json list
      Contracts: Json list
      Tasks: WorkTask list
      Rules: VerificationRule list
      Experiment: Json
      External: Json list
      PreflightChecks: Json list
      DefectTests: Json list }

let citationOf (j: Json) =
    { Repo = str "repo" j
      Path = str "path" j
      Quote = str "quote" j }

let citationsOf key j = arr key j |> List.map citationOf

let taskOf (j: Json) : WorkTask =
    let deps key =
        arr key j |> List.map (fun d -> { Task = str "task" d; Reason = str "reason" d })

    { Id = str "id" j
      Title = str "title" j
      Owner = str "owner" j
      Kind = str "kind" j
      SatisfiedBy = str "satisfiedBy" j
      Capability = strOpt "capability" j
      Covers = strs "covers" j
      DependsOn = deps "dependsOn"
      Preferences = deps "preferenceAfter"
      Completion = strs "completion" j
      Gates = strs "gates" j
      Contracts = strs "contracts" j
      Decisions = strs "decisions" j
      Phase = strOpt "phase" j |> Option.defaultValue ""
      ConditionalOn = strOpt "conditionalOn" j
      Raw = j }

let ruleOf (j: Json) : VerificationRule =
    { Id = str "id" j
      Match = strs "match" j
      Gate = str "gate" j
      Owner = str "owner" j
      Method = str "method" j
      Evidence = strs "evidence" j
      Freshness = strs "freshness" j
      InvalidatedBy = strs "invalidatedBy" j
      Tooling = str "tooling" j }

/// The document key used in requirement ids: `docs/FROZEN-DEMO-STORAGE-REQUIREMENTS.md` -> `FROZEN-DEMO-STORAGE`.
let documentKey (path: string) =
    System.IO.Path.GetFileNameWithoutExtension(path).Replace("-REQUIREMENTS", "")

/// Selector grammar used by tasks and verification rules:
/// `all`, `id:X`, `prefix:X`, `regex:RE`, `doc:PATH`, `origin:baseline|external`, or a bare requirement id.
let matches (selector: string) (r: Requirement) =
    match selector.Split(':', 2) with
    | [| "all"; _ |] -> true
    | [| "id"; v |] -> r.Id = v
    | [| "prefix"; v |] -> r.Id.StartsWith(v, System.StringComparison.Ordinal)
    | [| "doc"; v |] -> r.Document = v
    | [| "origin"; v |] -> r.Origin = v
    | [| "regex"; v |] -> System.Text.RegularExpressions.Regex.IsMatch(r.Id, v)
    | _ when selector = "all" -> true
    | _ -> r.Id = selector
