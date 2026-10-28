/// Required evidence categories per plan obligation, from the declared table `curation/evidence-categories.json`
/// (schema indy-init.evidence-categories/1).
///
/// Pure functions only. A rule applies to an obligation when every condition it states holds; the obligation's
/// required set is the union of the categories of every applicable rule. Nothing is assigned by default: an
/// obligation that no rule reaches is `unclassified`, which the plan reports as missing coverage.
module EvidenceCategories

open System
open System.Text.RegularExpressions
open Json

let schemaId = "indy-init.evidence-categories/1"

/// The closed vocabulary, in the order the plan lists categories.
let vocabulary =
    [ "unit"; "integration"; "browser"; "live-provider"; "security"; "accessibility"; "independent-acceptance"; "process-record"; "owner-decision" ]

let unclassified = "unclassified"

/// Test kinds of the verification plan (a binding's `kind`).
let testKinds = [ "live"; "deterministic"; "review"; "process"; "owner" ]

/// Who produces a category's evidence. Application tests prove product behaviour; reviewer, process and owner
/// evidence is about how the work was done and is never satisfied by an application test (and the reverse).
let evidenceClasses = [ "application-test"; "reviewer"; "process"; "owner" ]

/// The test kinds a category of each evidence class may name.
let private kindsOfClass =
    function
    | "process" -> [ "process" ]
    | "owner" -> [ "owner" ]
    | _ -> [ "live"; "deterministic"; "review" ]

/// What a rule can see about one obligation: existing requirement data only.
type Subject =
    { Requirement: string
      Title: string
      Document: string
      Gates: string list
      Rules: string list
      Policy: string
      Obligation: string
      ObligationKind: string }

let private conditionKeys =
    [ "rules"; "gates"; "documents"; "requirements"; "titlePattern"; "obligationKinds"; "obligations"; "freshnessPolicies" ]

let private conditionsOf (rule: Json) =
    match field "when" rule with
    | Some(JObj cs) -> cs
    | _ -> []

let private asSubjectRequirement (s: Subject) : Model.Requirement =
    { Id = s.Requirement
      Title = s.Title
      Priority = ""
      Document = s.Document
      Heading = ""
      SectionSha256 = ""
      Repository = ""
      Commit = ""
      Origin = "baseline"
      Obligations = [] }

let private holds (s: Subject) (key: string, value: Json) =
    let values = match value with JArr xs -> xs |> List.choose (function JStr x -> Some x | _ -> None) | _ -> []
    let anyIn (xs: string list) = values |> List.exists (fun v -> List.contains v xs)
    match key, value with
    | "rules", _ -> anyIn s.Rules
    | "gates", _ -> anyIn s.Gates
    | "documents", _ -> anyIn [ s.Document ]
    | "requirements", _ -> values |> List.exists (fun sel -> Model.matches sel (asSubjectRequirement s))
    | "obligationKinds", _ -> anyIn [ s.ObligationKind ]
    | "obligations", _ -> anyIn [ s.Obligation ]
    | "freshnessPolicies", _ -> anyIn [ s.Policy ]
    | "titlePattern", JStr p -> Regex.IsMatch(s.Title, p, RegexOptions.CultureInvariant)
    | _ -> false

/// A rule applies when it states at least one condition and every stated condition holds.
let applies (rule: Json) (s: Subject) =
    let cs = conditionsOf rule
    not cs.IsEmpty && cs |> List.forall (holds s)

/// The required categories of one obligation, in vocabulary order, each with the ids of the rules that require it.
let classify (table: Json) (s: Subject) : (string * string list) list =
    let hits = arr "rules" table |> List.filter (fun r -> applies r s) |> List.map (fun r -> str "category" r, str "id" r)
    vocabulary
    |> List.choose (fun c ->
        match hits |> List.filter (fst >> (=) c) |> List.map snd with
        | [] -> None
        | ids -> Some(c, List.sort ids))

let private categoryEntry (table: Json) (c: string) =
    arr "categories" table |> List.tryFind (fun x -> strOpt "id" x = Some c)

/// The test kinds that may provide evidence of a category.
let kindsFor (table: Json) (c: string) =
    categoryEntry table c |> Option.map (strs "testKinds") |> Option.defaultValue []

/// The evidence class of a category (application-test, reviewer, process or owner).
let classFor (table: Json) (c: string) =
    categoryEntry table c |> Option.bind (strOpt "evidenceClass") |> Option.defaultValue ""

/// The producer a category's evidence is expected to come from.
let producerFor (table: Json) (c: string) =
    categoryEntry table c |> Option.bind (strOpt "producer") |> Option.defaultValue ""

/// The freshness policy a category imposes on its evidence, if any.
let policyFor (table: Json) (c: string) =
    categoryEntry table c |> Option.bind (strOpt "freshnessPolicy")

/// Problems with the table itself: anything that would let a category be assigned by something other than a
/// declared, checkable rule.
let problems (table: Json) (knownRules: Set<string>) (knownPolicies: Set<string>) : string list =
    let cats = arr "categories" table
    let catIds = cats |> List.map (fun c -> strOpt "id" c |> Option.defaultValue "")
    let rules = arr "rules" table

    [ if strOpt "schema" table <> Some schemaId then yield sprintf "evidence-categories table has schema %A, expected %s" (strOpt "schema" table) schemaId
      if catIds <> vocabulary then yield sprintf "evidence-categories table must declare exactly the categories %s, in that order" (String.concat ", " vocabulary)
      for c in cats do
          let id = strOpt "id" c |> Option.defaultValue "?"
          if String.IsNullOrWhiteSpace(strOpt "definition" c |> Option.defaultValue "") then yield sprintf "category %s has no definition" id
          let ks = strs "testKinds" c
          if ks.IsEmpty then yield sprintf "category %s names no test kind" id
          for k in ks do
              if not (List.contains k testKinds) then yield sprintf "category %s names unknown test kind '%s'" id k
          match strOpt "evidenceClass" c with
          | Some k when List.contains k evidenceClasses ->
              for kind in ks do
                  if List.contains kind testKinds && not (List.contains kind (kindsOfClass k)) then
                      yield sprintf "category %s of evidence class %s cannot be proven by test kind '%s'" id k kind
          | k -> yield sprintf "category %s has evidence class %A, expected one of %s" id k (String.concat ", " evidenceClasses)
          if String.IsNullOrWhiteSpace(strOpt "producer" c |> Option.defaultValue "") then yield sprintf "category %s names no evidence producer" id
          match strOpt "freshnessPolicy" c with
          | Some p when not (knownPolicies.Contains p) -> yield sprintf "category %s names unknown freshness policy %s" id p
          | _ -> ()
      for (dup, n) in rules |> List.countBy (fun r -> strOpt "id" r |> Option.defaultValue "") do
          if n > 1 then yield sprintf "category rule id %s is declared more than once" dup
      for r in rules do
          let id = strOpt "id" r |> Option.defaultValue "?"
          match strOpt "category" r with
          | Some c when List.contains c vocabulary -> ()
          | c -> yield sprintf "category rule %s assigns %A, which is not a declared category" id c
          if String.IsNullOrWhiteSpace(strOpt "source" r |> Option.defaultValue "") then yield sprintf "category rule %s cites no source" id
          let cs = conditionsOf r
          if cs.IsEmpty then yield sprintf "category rule %s states no condition (a rule must not apply to everything by default)" id
          for (k, v) in cs do
              if not (List.contains k conditionKeys) then yield sprintf "category rule %s has unknown condition '%s'" id k
              match k, v with
              | "titlePattern", JStr p ->
                  if (try Regex(p) |> ignore; false with _ -> true) then yield sprintf "category rule %s has an invalid titlePattern" id
              | "titlePattern", _ -> yield sprintf "category rule %s titlePattern must be a string" id
              | _, JArr [] -> yield sprintf "category rule %s condition '%s' lists nothing" id k
              | _, JArr xs when xs |> List.forall (function JStr _ -> true | _ -> false) -> ()
              | _ -> yield sprintf "category rule %s condition '%s' must be a list of strings" id k
              match k, v with
              | "rules", JArr xs ->
                  for x in xs do
                      match x with
                      | JStr rid when not (knownRules.Contains rid) -> yield sprintf "category rule %s names unknown verification rule %s" id rid
                      | _ -> ()
              | "freshnessPolicies", JArr xs ->
                  for x in xs do
                      match x with
                      | JStr p when not (knownPolicies.Contains p) -> yield sprintf "category rule %s names unknown freshness policy %s" id p
                      | _ -> ()
              | _ -> () ]

/// The table as the plan embeds it, so a consumer can re-derive every obligation's categories from the plan alone.
let embedded (path: string) (sha256: string) (table: Json) =
    JObj
        [ "schema", JStr schemaId
          "table", JObj [ "path", JStr path; "sha256", JStr sha256 ]
          "categories",
          arr "categories" table
          |> List.map (fun c ->
              JObj
                  [ "id", JStr(str "id" c)
                    "definition", JStr(str "definition" c)
                    "testKinds", jstrs (strs "testKinds" c)
                    "freshnessPolicy", jopt JStr (strOpt "freshnessPolicy" c)
                    "evidenceClass", JStr(str "evidenceClass" c)
                    "producer", JStr(str "producer" c) ])
          |> JArr
          "rules",
          arr "rules" table
          |> List.map (fun r ->
              JObj
                  [ "id", JStr(str "id" r)
                    "category", JStr(str "category" r)
                    "when", JObj(conditionsOf r)
                    "source", JStr(str "source" r) ])
          |> JArr
          "rule",
          JStr
              "An obligation requires the union of the categories of every rule whose conditions all hold for it. No rule: unclassified (missing coverage), never a default." ]
