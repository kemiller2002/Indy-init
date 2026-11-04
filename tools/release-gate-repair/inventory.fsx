// inventory.fsx: the machine-readable inventory of every obligation of the frozen verification plan
// (docs/release-gate-repair). Read-only on its inputs; writes one JSON file.
//
//   dotnet fsi tools/release-gate-repair/inventory.fsx -- --plan requirements-compiled/v4/verification-plan.json
//       --execution-plan requirements-compiled/v4.1.4/verification-execution-plan.json
//       --ownership requirements-compiled/v4.1.4/agent-file-ownership.json --dag requirements-compiled/v4.1.4/execution-dag.json
//       --coverage R9-coverage.json --gate R9-gate.json --suite-preflight R9-suite-preflight-v4.1.2.json --out F
//
// For each obligation it records the stable requirement and obligation IDs, the source requirement (document and
// section digest), applicability, the implementation owner, every bound test or evidence producer with its suite and
// required artifact, the status Rehearsal 009's release gate observed, whether an invalid suite command blocked its
// verification, and any approved exclusion. Nothing is classified out of scope: exclusion comes only from the plan.

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes

let args = fsi.CommandLineArgs |> Array.toList |> List.tail
let opt name = args |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let req name = opt name |> Option.defaultWith (fun () -> eprintfn "missing %s" name; exit 2)
let read path : JsonNode = JsonNode.Parse(File.ReadAllText path) |> nonNull

let str (n: JsonNode | null) (key: string) =
    match n with
    | null -> ""
    | n ->
        match n[key] with
        | null -> ""
        | v when v.GetValueKind() = JsonValueKind.String -> v.GetValue<string>()
        | v -> v.ToJsonString()

let arr (n: JsonNode | null) (key: string) : JsonNode list =
    match n with
    | null -> []
    | n ->
        match n[key] with
        | :? JsonArray as a -> a |> Seq.choose Option.ofObj |> List.ofSeq
        | _ -> []

let strs n key = arr n key |> List.map (fun v -> v.GetValue<string>())

let plan = read (req "--plan")
let execution = read (req "--execution-plan")
let ownership = read (req "--ownership")
let dag = read (req "--dag")
let coverage = opt "--coverage" |> Option.map read
let gate = opt "--gate" |> Option.map read
let preflight = opt "--suite-preflight" |> Option.map read

/// identity -> suite, from the compiled execution plan.
let suiteOfTest =
    arr execution "suites"
    |> List.collect (fun s -> arr s "tests" |> List.map (fun t -> str t "id", str s "id"))
    |> Map.ofList

let suiteProducer = arr execution "suites" |> List.map (fun s -> str s "id", str s "evidenceProducer") |> Map.ofList

/// identity -> owning role and file, from the ownership manifest.
let ownerOfTest =
    arr ownership "files"
    |> List.collect (fun f -> strs f "tests" |> List.map (fun t -> t, (str f "owner", str f "path")))
    |> Map.ofList

/// requirement -> owners of the timed tasks that cover it.
let taskOwners =
    arr dag "tasks"
    |> List.collect (fun t -> strs t "covers" |> List.map (fun r -> r, (str t "id", str t "owner")))
    |> List.groupBy fst
    |> List.map (fun (r, xs) -> r, xs |> List.map snd |> List.distinct)
    |> Map.ofList

/// identity -> status Rehearsal 009's Dokimos coverage reported at the candidate.
let coverageStatus =
    coverage
    |> Option.map (fun c -> arr c "obligations" |> List.collect (fun o -> arr o "identities" |> List.map (fun i -> str i "id", str i "status")) |> Map.ofList)
    |> Option.defaultValue Map.empty

/// Suites whose own command could not execute (suite-preflight of the plan the gate ran).
let refusedSuites =
    preflight
    |> Option.map (fun p -> arr p "suites" |> List.filter (fun s -> str s "executable" = "false") |> List.map (fun s -> str s "id", str s "cause") |> Map.ofList)
    |> Option.defaultValue Map.empty

let evidenceClass (identity: string) =
    if identity.StartsWith "dotnet:" || identity.StartsWith "playwright:" then "executable"
    elif identity.StartsWith "praxis:" then "record"
    elif identity.StartsWith "verifier:" then "independent-verifier"
    else "unclassified"

let recordOwner (identity: string) =
    match identity with
    | "praxis:independent-review" -> "reviewer"
    | "praxis:owner-approval" -> "owner"
    | i when i.StartsWith "verifier:" -> "independent verifier (not the implementer)"
    | _ -> "coordinator"

let node (o: obj) : JsonNode =
    match o with
    | :? JsonNode as n -> n
    | :? string as s -> JsonValue.Create(s: string) |> nonNull :> JsonNode
    | :? bool as b -> JsonValue.Create(b: bool) :> JsonNode
    | :? int as i -> JsonValue.Create(i: int) :> JsonNode
    | null -> null
    | other -> failwithf "unsupported %A" other

let objOf (fields: (string * obj) list) : JsonNode =
    let o = JsonObject()
    fields |> List.iter (fun (k, v) -> o[k] <- (match node v with null -> null | n when not (isNull n.Parent) -> n.DeepClone() | n -> n))
    o :> JsonNode

let arrOf (xs: JsonNode list) : JsonNode = JsonArray(xs |> List.map (fun x -> (if isNull x.Parent then x else x.DeepClone()) :> JsonNode | null) |> Array.ofList) :> JsonNode
let strArr (xs: string list) = xs |> List.map (fun s -> JsonValue.Create(s: string) |> nonNull :> JsonNode) |> arrOf

let entries =
    arr plan "requirements"
    |> List.collect (fun r ->
        let requirement = str r "requirement"
        // The plan's own exclusion record: {status: in-scope | excluded, approval}. Only an approved exclusion counts.
        let exclusion =
            match r["exclusion"] with
            | null -> null
            | e when str e "status" = "excluded" && str e "approval" <> "" -> e.DeepClone()
            | _ -> null
        let scopeStatus = match r["exclusion"] with null -> "in-scope" | e -> str e "status"
        arr r "obligations"
        |> List.map (fun o ->
            let contract = o["acceptanceContract"]
            let identities = arr (o["testIdentity"]) "tests" |> List.map (fun t -> str t "id", str t "kind", strs t "categories")
            let classes = identities |> List.map (fun (i, _, _) -> evidenceClass i) |> List.distinct
            let evidenceType =
                match classes |> List.filter ((<>) "independent-verifier") |> List.sort with
                | [ "executable" ] -> "executable"
                | [ "record" ] -> "record"
                | [ "executable"; "record" ] -> "mixed"
                | [] when classes = [ "independent-verifier" ] -> "independent-verifier"
                | other -> "unclassified: " + String.Join(",", other)
            let tests =
                identities
                |> List.map (fun (i, kind, categories) ->
                    let suite = suiteOfTest.TryFind i |> Option.defaultValue ""
                    let owner, file =
                        match ownerOfTest.TryFind i with
                        | Some(o, f) -> o, f
                        | None -> recordOwner i, ""
                    let r9 =
                        match evidenceClass i with
                        | "executable" -> coverageStatus.TryFind i |> Option.defaultValue "not-reported"
                        | "record" -> "unverified (verify-process judged 0/119 record obligations at the Rehearsal 009 candidate)"
                        | _ -> "deferred to the independent acceptance verifier"
                    objOf
                        [ "identity", box i
                          "kind", box kind
                          "evidenceClass", box (evidenceClass i)
                          "categories", box (strArr categories)
                          "suite", box suite
                          "owner", box owner
                          "file", box file
                          "requiredArtifact", box (if suite = "" then "" else suiteProducer.TryFind suite |> Option.defaultValue "")
                          "suiteCommandExecutable", box (not (refusedSuites.ContainsKey suite))
                          "rehearsal009Status", box r9 ])
            let blocked =
                identities |> List.choose (fun (i, _, _) -> suiteOfTest.TryFind i) |> List.distinct |> List.filter refusedSuites.ContainsKey
            let executableIds = identities |> List.filter (fun (i, _, _) -> evidenceClass i = "executable") |> List.map (fun (i, _, _) -> i)
            let generated = executableIds |> List.filter (fun i -> match coverageStatus.TryFind i with Some "not-generated" | None -> false | Some _ -> true)
            let implementation =
                if executableIds.IsEmpty then "record-only: no test code; the evidence is the run's records"
                elif generated.IsEmpty then "unimplemented: no bound test was written"
                elif generated.Length < executableIds.Length then "partial: some bound tests were not written"
                else "tests written"
            let owners =
                (identities |> List.map (fun (i, _, _) -> ownerOfTest.TryFind i |> Option.map fst |> Option.defaultValue (recordOwner i)))
                @ (taskOwners.TryFind requirement |> Option.defaultValue [] |> List.map snd)
                |> List.distinct
            objOf
                [ "requirement", box requirement
                  "obligation", box (str o "sourceObligation")
                  "obligationId", box (str o "id")
                  "priority", box (str r "priority")
                  "source", box (objOf [ "document", box (str r "document"); "sectionSha256", box (str r "sectionSha256"); "title", box (str r "title"); "text", box (str o "text") ])
                  "applicability", box "always: P0, in the frozen plan's scope; the plan states no applicability condition, and none is inferred"
                  "implementationOwners", box (strArr owners)
                  "coveringTasks", box (strArr (taskOwners.TryFind requirement |> Option.defaultValue [] |> List.map fst))
                  "evidenceType", box evidenceType
                  "requiredEvidenceCategories", box (strArr (strs contract "requiredEvidenceCategories"))
                  "evidenceProducers", box (arrOf (arr contract "evidenceProducers" |> List.map (fun p -> p.DeepClone())))
                  "tests", box (arrOf tests)
                  "implementation", box implementation
                  "verificationBlockedByInvalidCommand", box (not blocked.IsEmpty)
                  "blockingSuites", box (strArr blocked)
                  "rehearsal009Status",
                  box (
                      match evidenceType with
                      | "record" -> "unverified: verify-process judged 0 of the record obligations"
                      | _ when generated.IsEmpty -> "not-generated: no bound executable test exists at the candidate"
                      | _ -> "see tests" )
                  "scopeStatus", box scopeStatus
                  "approvedExclusion", box exclusion ]))

let count pred = entries |> List.filter pred |> List.length
let field (e: JsonNode) k = str e k
let blockedCount = count (fun e -> e["verificationBlockedByInvalidCommand"].GetValue<bool>())

let summary =
    objOf
        [ "requirements", box (arr plan "requirements" |> List.length)
          "obligations", box entries.Length
          "executable", box (count (fun e -> let t = field e "evidenceType" in t = "executable" || t = "mixed"))
          "record", box (count (fun e -> let t = field e "evidenceType" in t = "record" || t = "mixed"))
          "mixed", box (count (fun e -> field e "evidenceType" = "mixed"))
          "executableOnly", box (count (fun e -> field e "evidenceType" = "executable"))
          "recordOnly", box (count (fun e -> field e "evidenceType" = "record"))
          "bindingIndependentVerifierEvidence", box (count (fun e -> arr e "tests" |> List.exists (fun t -> str t "evidenceClass" = "independent-verifier")))
          "unclassified", box (count (fun e -> (field e "evidenceType").StartsWith "unclassified"))
          "unimplemented", box (count (fun e -> (field e "implementation").StartsWith "unimplemented"))
          "recordOnlyNoTestCode", box (count (fun e -> (field e "implementation").StartsWith "record-only"))
          "verificationBlockedByInvalidCommand", box blockedCount
          "unimplementedAndBlocked", box (count (fun e -> (field e "implementation").StartsWith "unimplemented" && e["verificationBlockedByInvalidCommand"].GetValue<bool>()))
          "approvedExclusions", box (count (fun e -> not (isNull e["approvedExclusion"])))
          "withoutImplementationOwner", box (count (fun e -> (arr e "implementationOwners").IsEmpty)) ]

let reconciliation =
    match gate with
    | None -> objOf [ "available", box false ]
    | Some g ->
        let reported = [ "obligations", str g "obligations"; "executable", str g "executable"; "record", str g "record"; "mixed", str g "mixed" ]
        let computed = [ "obligations", string entries.Length; "executable", str summary "executable"; "record", str summary "record"; "mixed", str summary "mixed" ]
        objOf
            [ "gateCandidate", box (str g "candidate")
              "reported", box (objOf (reported |> List.map (fun (k, v) -> k, box v)))
              "computedFromPlan", box (objOf (computed |> List.map (fun (k, v) -> k, box v)))
              "reconciles", box (reported = computed) ]

let root =
    objOf
        [ "schema", box "indy-init.obligation-inventory/1"
          "plan", box (objOf [ "planId", box (str (plan["planIdentity"]) "planId"); "file", box (req "--plan") ])
          "executionPlan", box (req "--execution-plan")
          "description", box "Every obligation of the frozen v4 verification plan (the one the Rehearsal 009 release gate judged), with its source, applicability, owner, bound tests and evidence producers, required artifacts, the status the Rehearsal 009 release gate observed, and whether an invalid suite command blocked it. Generated by tools/release-gate-repair/inventory.fsx; never edited by hand. No obligation is classified out of scope: exclusions come only from the plan's approved exclusions."
          "summary", box summary
          "reconciliation", box reconciliation
          "obligations", box (arrOf entries) ]

File.WriteAllText(req "--out", root.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
printfn "%s" (summary.ToJsonString())
printfn "reconciliation: %s" (reconciliation.ToJsonString())
