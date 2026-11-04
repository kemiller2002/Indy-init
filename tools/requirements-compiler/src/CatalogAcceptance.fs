/// Governed catalog acceptance (adversarial audit R2; SA-02, SA-07, SA-08, SA-13).
///
/// Rehearsal 009 headlined "Dokimos accepted… 199 of 199" from a workflow, probes and fault probes it wrote itself; no
/// owner gate consumed the result. Here a catalog verdict counts as governed acceptance only when it binds, at once:
/// - the candidate commit (the exact SHA under release);
/// - the approved catalog digest (the sha256 of the ratified catalog file, which the compiled manifest records);
/// - the approved verification contract (its sha256, pinned by the lock);
/// - the evidence producer (the governed gate run, never a builder-authored workflow);
/// - the test result and its failure details;
/// and the fault-sensitivity categories and applicability facts come from the contract, not from probe output.
/// A builder-run verdict is kept as supporting evidence and is never governed acceptance.
module CatalogAcceptance

open System
open Json
open Scope

/// The owner-approved verification contract (`indy-init.catalog-verification-contract/1`).
type Contract =
    { CatalogId: string
      /// sha256 of the ratified catalog file (requirements/catalogs/…), recorded by the manifest as source.catalogSha256.
      CatalogSha256: string
      /// Producer kinds whose verdict is governed (the governed gate, `verification-gate.yml`, run by `r9.fsx`).
      GovernedProducers: string list
      /// category -> the catalog checks whose mandatory obligations must be fault-sensitive
      FaultSensitivity: (string * string list) list
      /// fact -> the governed value; a requirement is not applicable only where these facts say so
      Facts: Map<string, Json>
      RequireProbeSpec: bool }

let contractOf (j: Json) : Contract =
    let catalog = field "catalog" j |> Option.defaultValue (JObj [])
    { CatalogId = strOpt "catalogId" catalog |> Option.defaultValue ""
      CatalogSha256 = strOpt "sha256" catalog |> Option.defaultValue ""
      GovernedProducers = strs "governedProducers" j
      FaultSensitivity = arr "faultSensitivity" j |> List.map (fun c -> strOpt "category" c |> Option.defaultValue "", strs "checks" c)
      Facts = match field "facts" j with Some(JObj ps) -> Map.ofList ps | _ -> Map.empty
      RequireProbeSpec = boolOr "requireProbeSpec" true j }

/// Who produced a verdict: the governed gate writes `{kind: "governed-gate", runId, workflow}` itself; anything else
/// (a workflow in the candidate repository, a local run) is "implementer-run".
type Producer = { Kind: string; RunId: string }

let producerOf (j: Json option) : Producer =
    match j with
    | Some p -> { Kind = strOpt "kind" p |> Option.defaultValue "implementer-run"; RunId = strOpt "runId" p |> Option.defaultValue "" }
    | None -> { Kind = "implementer-run"; RunId = "" }

type Inputs =
    { Contract: Contract
      ContractSha256: string
      /// The contract digest the owner approved (the lock's `catalogVerification.contractSha256`).
      ApprovedContractSha256: string
      Candidate: string
      /// The Praxis manifest the governed gate compiled from the approved catalog and the candidate's mappings.
      Manifest: Json option
      /// Dokimos `dokimos.requirement-verification/1`, or None when no catalog verification was produced.
      Verification: Json option
      Producer: Producer
      /// Dokimos fault-probe results (`probes[]: id, obligations, outcome`), or None.
      ProbeResults: Json option
      ProbeSpecPresent: bool
      /// Facts the candidate's own observations or probes declare.
      DeclaredFacts: Map<string, Json>
      /// The approved catalog (its `appliesWhen` conditions decide which requirements may be not applicable).
      Catalog: Json }

type Verdict =
    { Level: string // GOVERNED-ACCEPTED | SUPPORTING-ONLY | REFUSED
      Refusals: Refusal list
      Failures: (string * string) list }

let private r code detail = { Code = code; Detail = detail }

let private factText (j: Json) = match j with JBool b -> (if b then "true" else "false") | JStr s -> s | JInt i -> string i | j -> serialize j

/// The obligations of the verification, with their requirement's status, as (requirementId, obligation).
let private obligations (v: Json) = arr "requirements" v |> List.collect (fun req -> arr "obligations" req |> List.map (fun o -> req, o))

let judge (i: Inputs) : Verdict =
    match i.Verification with
    | None -> { Level = "REFUSED"; Refusals = [ r "CATALOG_VERIFICATION_MISSING" "no catalog verification was produced for the candidate; the release gate requires one" ]; Failures = [] }
    | Some v ->
        let manifest = field "manifest" v |> Option.defaultValue (JObj [])
        let obs = obligations v
        let failures =
            obs
            |> List.filter (fun (_, o) -> boolOr "mandatory" false o && not (List.contains (strOpt "status" o |> Option.defaultValue "") [ "PASS"; "NOT_APPLICABLE" ]))
            |> List.map (fun (_, o) -> str "id" o, strOpt "reason" o |> Option.defaultValue (strOpt "status" o |> Option.defaultValue "no status"))
        let detected =
            match i.ProbeResults with
            | Some p -> arr "probes" p |> List.filter (fun x -> strOpt "outcome" x = Some "detected") |> List.collect (strs "obligations") |> Set.ofList
            | None -> Set.empty
        let bindingRefusals =
            [ if i.ContractSha256 <> i.ApprovedContractSha256 || i.ApprovedContractSha256 = "" then
                  yield r "CONTRACT_UNAPPROVED" (sprintf "the verification contract has sha256 %s; the approved contract is %s" i.ContractSha256 (if i.ApprovedContractSha256 = "" then "(none pinned)" else i.ApprovedContractSha256))
              let candidate = strOpt "candidate" v |> Option.defaultValue ""
              if candidate <> i.Candidate then yield r "CATALOG_WRONG_COMMIT" (sprintf "the catalog verification is for %s, not the candidate %s" (if candidate = "" then "(no candidate)" else candidate) i.Candidate)
              if strOpt "catalogId" manifest <> Some i.Contract.CatalogId then
                  yield r "CATALOG_UNAPPROVED" (sprintf "the verification judged catalog %s, not the approved %s" (strOpt "catalogId" manifest |> Option.defaultValue "(none)") i.Contract.CatalogId)
              match i.Manifest with
              | None -> yield r "CATALOG_MANIFEST_MISSING" "no compiled catalog manifest accompanies the verification"
              | Some m ->
                  let catalogSha = field "source" m |> Option.bind (strOpt "catalogSha256") |> Option.defaultValue ""
                  if catalogSha <> i.Contract.CatalogSha256 || i.Contract.CatalogSha256 = "" then
                      yield r "CATALOG_DIGEST_UNAPPROVED" (sprintf "the manifest was compiled from catalog sha256 %s; the approved catalog is %s" (if catalogSha = "" then "(none)" else catalogSha) i.Contract.CatalogSha256)
                  if strOpt "contentHash" manifest <> strOpt "contentHash" m then
                      yield r "CATALOG_MANIFEST_MISMATCH" (sprintf "the verification judged manifest %s, not the governed manifest %s" (strOpt "contentHash" manifest |> Option.defaultValue "(none)") (strOpt "contentHash" m |> Option.defaultValue "(none)"))
              if not (boolOr "intact" false manifest) then yield r "CATALOG_DIGEST_UNAPPROVED" "the verification reports the catalog manifest as not intact" ]
        let sensitivityRefusals =
            [ for category, checks in i.Contract.FaultSensitivity do
                  let members = obs |> List.filter (fun (_, o) -> boolOr "mandatory" false o && List.contains (strOpt "check" o |> Option.defaultValue "") checks)
                  if members.IsEmpty then
                      yield r "FAULT_CATEGORY_EMPTY" (sprintf "fault-sensitivity category %s matches no mandatory obligation of the catalog (checks %s); an empty category enforces nothing" category (String.concat ", " checks))
                  else
                      if i.Contract.RequireProbeSpec && not i.ProbeSpecPresent then
                          yield r "FAULT_PROBE_SPEC_MISSING" (sprintf "category %s requires fault probes, and the candidate has no .echelon/fault-probes.json" category)
                      for (_, o) in members do
                          if strOpt "status" o = Some "PASS" && not (detected.Contains(str "id" o)) then
                              yield r "FAULT_INSENSITIVE" (sprintf "%s (%s) passed, but no detected fault probe at the candidate names it" (str "id" o) category) ]
        let factRefusals =
            [ for KeyValue(fact, value) in i.DeclaredFacts do
                  match i.Contract.Facts.TryFind fact with
                  | None -> yield r "FACT_UNGOVERNED" (sprintf "the candidate declares fact %s=%s, which the approved contract does not govern" fact (factText value))
                  | Some governed when factText governed <> factText value ->
                      yield r "FACT_CONFLICT" (sprintf "the candidate declares %s=%s; the governed value is %s" fact (factText value) (factText governed))
                  | Some _ -> ()
              // A requirement may be reported not applicable only where its catalog condition is false under the governed
              // facts; a requirement the governed facts make applicable is never excused by the candidate's own facts.
              let conditionsOf id =
                  arr "requirements" i.Catalog |> List.tryFind (fun c -> strOpt "id" c = Some id) |> Option.map (arr "appliesWhen") |> Option.defaultValue []
              let holds (c: Json) =
                  match strOpt "fact" c, field "equals" c with
                  | Some f, Some expected -> i.Contract.Facts.TryFind f |> Option.map (fun g -> factText g = factText expected)
                  | _ -> None
              for req in arr "requirements" v do
                  let id = str "id" req
                  let conditions = conditionsOf id
                  let applicable = conditions |> List.forall (fun c -> holds c = Some true)
                  let undecided = conditions |> List.exists (fun c -> holds c = None)
                  if strOpt "status" req = Some "NOT_APPLICABLE" then
                      if conditions.IsEmpty then yield r "FACT_CONFLICT" (sprintf "%s is reported not applicable, but the approved catalog states no applicability condition for it" id)
                      elif undecided then yield r "FACT_CONFLICT" (sprintf "%s is reported not applicable, but the contract does not govern every fact its condition names" id)
                      elif applicable then yield r "FACT_CONFLICT" (sprintf "%s is reported not applicable, but the governed facts make it applicable" id) ]
        let resultRefusals =
            [ if not (field "acceptance" v |> Option.exists (boolOr "accepted" false)) then
                  yield r "CATALOG_NOT_ACCEPTED" "Dokimos did not accept the catalog verification"
              for (id, why) in failures do
                  yield r "CATALOG_OBLIGATION_FAILED" (sprintf "%s: %s" id why) ]
        let governed = i.Contract.GovernedProducers |> List.contains i.Producer.Kind && i.Producer.RunId <> "" && i.Producer.RunId = (strOpt "runId" v |> Option.defaultValue "")
        let producerRefusals =
            if governed then []
            else [ r "PRODUCER_NOT_GOVERNED" (sprintf "the verdict was produced by %s (run %s); only the governed gate establishes acceptance. It is kept as supporting evidence" i.Producer.Kind (if i.Producer.RunId = "" then "unknown" else i.Producer.RunId)) ]
        let refusals = bindingRefusals @ sensitivityRefusals @ factRefusals @ resultRefusals @ producerRefusals
        let level =
            if refusals.IsEmpty then "GOVERNED-ACCEPTED"
            elif producerRefusals.Length = refusals.Length then "SUPPORTING-ONLY"
            else "REFUSED"
        { Level = level; Refusals = refusals; Failures = failures }

let verdictJson (i: Inputs) (v: Verdict) : Json =
    JObj
        [ "schema", JStr "indy-init.catalog-acceptance/1"
          "level", JStr v.Level
          "governed", JBool(v.Level = "GOVERNED-ACCEPTED")
          "candidate", JStr i.Candidate
          "catalogId", JStr i.Contract.CatalogId
          "catalogSha256", JStr i.Contract.CatalogSha256
          "manifestContentHash", JStr(i.Manifest |> Option.bind (strOpt "contentHash") |> Option.defaultValue "")
          "contractSha256", JStr i.ContractSha256
          "producer", JObj [ "kind", JStr i.Producer.Kind; "runId", JStr i.Producer.RunId ]
          "failures", JArr(v.Failures |> List.map (fun (o, why) -> JObj [ "obligation", JStr o; "reason", JStr why ]))
          "refusals", JArr(v.Refusals |> List.map refusalJson) ]
