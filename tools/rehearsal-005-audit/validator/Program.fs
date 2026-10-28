/// Integrity checks for the Rehearsal 5 implementation audit (docs/rehearsal-005-implementation-audit, section 17).
///
/// Inputs: the audit directory (requirement-matrix.json, summary.json, independent-review.md) and an unmodified
/// checkout of kemiller2002/indy-init-rehearsal-005 at the final-results commit b972cb4, whose tree holds the
/// candidate source (identical to 1b9fb56 outside evidence/, .ros/ and .praxis/) and the frozen run evidence.
/// Every check is a pure function from the loaded inputs to a list of problems; any problem fails the audit.
module Rehearsal005Audit.Validate

open System
open System.IO
open System.Security.Cryptography
open System.Text.Json

let private runDir = "evidence/acceptance/run-40620099844-1b9fb56"
let private traceabilityPath = runDir + "/bundle/traceability.json"
let private verdictPath = runDir + "/verdict.json"
let private finalPath = "evidence/rehearsal/final.json"

let private classifications =
    set [ "IMPLEMENTED_VERIFICATION_MISSING"; "PARTIALLY_IMPLEMENTED"; "NOT_IMPLEMENTED"; "INDETERMINATE"; "PROCESS_EVIDENCE_MISSING" ]

let private processStatuses = set [ "PERFORMED_EVIDENCE_INADEQUATE"; "NOT_PERFORMED"; "UNKNOWN" ]
let private refKinds = set [ "historical"; "post-rehearsal" ]
let private efforts = set [ "none"; "small"; "medium"; "large"; "unknown" ]
let private workKinds = [ "applicationCode"; "tests"; "evidence"; "planCorrections"; "ownerDecisions"; "independentReview" ]

// ---- JSON access ------------------------------------------------------------------------------------------------

let private prop (name: string) (e: JsonElement) =
    match e.ValueKind with
    | JsonValueKind.Object ->
        match e.TryGetProperty name with
        | true, v when v.ValueKind <> JsonValueKind.Null -> Some v
        | _ -> None
    | _ -> None

let private str name e = prop name e |> Option.bind (fun v -> if v.ValueKind = JsonValueKind.String then Some(v.GetString()) else None)
let private num name e = prop name e |> Option.bind (fun v -> if v.ValueKind = JsonValueKind.Number then Some(v.GetInt32()) else None)
let private boolean name e = prop name e |> Option.bind (fun v -> match v.ValueKind with JsonValueKind.True -> Some true | JsonValueKind.False -> Some false | _ -> None)
let private arr name e = prop name e |> Option.map (fun v -> if v.ValueKind = JsonValueKind.Array then v.EnumerateArray() |> List.ofSeq else []) |> Option.defaultValue []

let private load (path: string) = JsonDocument.Parse(File.ReadAllText path).RootElement.Clone()
let private sha256 (path: string) = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes path)).ToLowerInvariant()

// ---- The frozen facts ---------------------------------------------------------------------------------------------

type private FrozenObligation = { Vpo: string; Requirement: string; Source: string; Verification: string }

type private Frozen =
    { Obligations: FrozenObligation list
      Verdict: JsonElement
      Final: JsonElement
      Hashes: Map<string, string> }

let private loadFrozen (r5: string) =
    let path p = Path.Combine(r5, p)
    let trace = load (path traceabilityPath)
    { Obligations =
        arr "obligations" trace
        |> List.map (fun o ->
            { Vpo = str "id" o |> Option.defaultValue ""
              Requirement = str "requirement" o |> Option.defaultValue ""
              Source = str "sourceObligation" o |> Option.defaultValue ""
              Verification = str "verification" o |> Option.defaultValue "" })
      Verdict = load (path verdictPath)
      Final = load (path finalPath)
      Hashes = [ traceabilityPath; verdictPath; finalPath ] |> List.map (fun p -> p, sha256 (path p)) |> Map.ofList }

let private notRunRequirements (f: Frozen) =
    f.Obligations |> List.filter (fun o -> o.Verification = "NOT_RUN") |> List.map (fun o -> o.Requirement) |> Set.ofList

let private passRequirements (f: Frozen) =
    let all = f.Obligations |> List.map (fun o -> o.Requirement) |> Set.ofList
    Set.difference all (notRunRequirements f)

let private verdictNotRun (f: Frozen) =
    arr "reasons" f.Verdict
    |> List.choose (fun r -> if r.ValueKind = JsonValueKind.String then Some(r.GetString()) else None)
    |> List.choose (fun r ->
        let m = Text.RegularExpressions.Regex.Match(r, @"^P0 requirement (\S+): NOT_RUN$")
        if m.Success then Some m.Groups[1].Value else None)
    |> Set.ofList

// ---- Checks (section 17) ------------------------------------------------------------------------------------------

type private Inputs =
    { Frozen: Frozen
      Matrix: JsonElement
      Summary: JsonElement
      AuditDir: string
      R5: string
      IndyInit: string }

let private requirements (i: Inputs) = arr "requirements" i.Matrix
let private rid (r: JsonElement) = str "id" r |> Option.defaultValue "<no id>"
let private cls (r: JsonElement) = str "classification" r |> Option.defaultValue ""
let private fail cond msg = if cond then [] else [ msg ]

let private c1Count (i: Inputs) =
    fail ((requirements i).Length = 31) (sprintf "1: %d requirements analysed, expected exactly 31" (requirements i).Length)

let private c2Identity (i: Inputs) =
    let ids = requirements i |> List.map rid
    let frozen = notRunRequirements i.Frozen
    [ yield! fail (ids.Length = (List.distinct ids).Length) "2: a requirement id appears more than once"
      yield! fail (Set.ofList ids = frozen) (sprintf "2: ids differ from the frozen traceability NOT_RUN set: extra %A, missing %A" (Set.difference (Set.ofList ids) frozen) (Set.difference frozen (Set.ofList ids)))
      yield! fail (frozen = verdictNotRun i.Frozen) "2: the frozen traceability and verdict.json disagree on the NOT_RUN set" ]

let private c3OneClassification (i: Inputs) =
    requirements i
    |> List.collect (fun r ->
        let c = cls r
        let ps = str "processStatus" r
        [ yield! fail (classifications.Contains c) (sprintf "3: %s has classification '%s', not one of the five" (rid r) c)
          yield! fail ((c = "PROCESS_EVIDENCE_MISSING") = ps.IsSome) (sprintf "3: %s: processStatus must be given exactly for PROCESS_EVIDENCE_MISSING" (rid r))
          yield! fail (ps |> Option.forall processStatuses.Contains) (sprintf "3: %s: unknown processStatus" (rid r)) ])

let private c4Sums (i: Inputs) =
    let counts = requirements i |> List.countBy cls |> Map.ofList
    let count c = counts |> Map.tryFind c |> Option.defaultValue 0
    let totals = prop "classification" i.Summary
    let stated c = totals |> Option.bind (num c) |> Option.defaultValue -1
    let verified = totals |> Option.bind (num "VERIFIED_DURING_REHEARSAL") |> Option.defaultValue -1
    let total = totals |> Option.bind (num "TOTAL") |> Option.defaultValue -1
    let frozenPass = (passRequirements i.Frozen).Count
    [ yield! classifications |> Seq.collect (fun c -> fail (stated c = count c) (sprintf "4: summary states %d %s, matrix has %d" (stated c) c (count c))) |> List.ofSeq
      yield! fail ((classifications |> Seq.sumBy count) = 31) "4: the five classifications do not sum to 31"
      yield! fail (verified = frozenPass) (sprintf "4: summary states %d verified during the rehearsal, the frozen evidence has %d" verified frozenPass)
      yield! fail (total = frozenPass + 31 && total = 108) (sprintf "4: summary total %d is not 77 + 31 = 108" total) ]

let private existsRef (i: Inputs) (path: string) =
    if path.StartsWith("indy-init:", StringComparison.Ordinal) then File.Exists(Path.Combine(i.IndyInit, path.Substring 10)) || Directory.Exists(Path.Combine(i.IndyInit, path.Substring 10))
    else File.Exists(Path.Combine(i.R5, path)) || Directory.Exists(Path.Combine(i.R5, path))

let private refs name r = arr name r |> List.map (fun x -> str "kind" x |> Option.defaultValue "", str "path" x |> Option.defaultValue "", str "result" x |> Option.defaultValue "")

let private c5ImplementedHasEvidence (i: Inputs) =
    requirements i
    |> List.filter (fun r -> cls r = "IMPLEMENTED_VERIFICATION_MISSING")
    |> List.collect (fun r ->
        let sources = arr "sourceRefs" r |> List.choose (str "path")
        let post = refs "testRefs" r |> List.filter (fun (k, _, res) -> k = "post-rehearsal" && res.StartsWith("passed", StringComparison.Ordinal))
        [ yield! fail (not sources.IsEmpty) (sprintf "5: %s is IMPLEMENTED with no source reference" (rid r))
          yield! sources |> List.collect (fun p -> fail (existsRef i p) (sprintf "5: %s: source %s does not exist in the checkout" (rid r) p))
          yield! fail (not post.IsEmpty) (sprintf "5: %s is IMPLEMENTED without a passing post-rehearsal execution" (rid r)) ])

let private c6MissingHasInvestigation (i: Inputs) =
    requirements i
    |> List.filter (fun r -> [ "PARTIALLY_IMPLEMENTED"; "NOT_IMPLEMENTED"; "INDETERMINATE" ] |> List.contains (cls r))
    |> List.collect (fun r ->
        let investigation = str "investigation" r |> Option.defaultValue ""
        fail (investigation.Trim().Length >= 80) (sprintf "6: %s (%s) has no documented investigation" (rid r) (cls r)))

/// Files this audit wrote; a historical reference may never point at them.
let private auditOutput (path: string) =
    [ "indy-init:docs/rehearsal-005-implementation-audit"; "indy-init:tools/rehearsal-005-audit" ]
    |> List.exists (fun p -> path.StartsWith(p, StringComparison.Ordinal))

let private c7HistoricalVsPost (i: Inputs) =
    let frozenByVpo = i.Frozen.Obligations |> List.map (fun o -> o.Vpo, o) |> Map.ofList
    requirements i
    |> List.collect (fun r ->
        [ yield!
              (refs "testRefs" r @ refs "evidenceRefs" r)
              |> List.collect (fun (kind, path, _) ->
                  [ yield! fail (refKinds.Contains kind) (sprintf "7: %s: reference %s has kind '%s'" (rid r) path kind)
                    yield! fail (kind <> "post-rehearsal" || path.StartsWith("indy-init:", StringComparison.Ordinal)) (sprintf "7: %s: post-rehearsal reference %s is not held in Indy-init" (rid r) path)
                    yield! fail (kind <> "historical" || not (auditOutput path)) (sprintf "7: %s: historical reference %s points into the audit's own output" (rid r) path)
                    yield! fail (existsRef i path) (sprintf "7: %s: reference %s does not exist" (rid r) path) ])
          yield!
              arr "obligations" r
              |> List.collect (fun o ->
                  let vpo = str "vpo" o |> Option.defaultValue ""
                  match Map.tryFind vpo frozenByVpo with
                  | None -> [ sprintf "7: %s: %s is not a frozen obligation" (rid r) vpo ]
                  | Some f -> fail (str "frozenVerification" o = Some f.Verification && str "id" o = Some f.Source) (sprintf "7: %s: %s misstates the frozen obligation (%s %s)" (rid r) vpo f.Source f.Verification)) ])

let private c8NoVerdictRewrite (i: Inputs) =
    let frozen = prop "frozen" i.Summary
    let stated p = frozen |> Option.bind (prop "sha256") |> Option.bind (str p)
    [ yield! fail (str "verdict" i.Frozen.Verdict = Some "INCOMPLETE") "8: the frozen verdict.json no longer says INCOMPLETE"
      yield! fail (prop "verdict" i.Frozen.Final |> Option.bind (boolean "accepted") = Some false) "8: the frozen final.json no longer says accepted false"
      yield! fail (frozen |> Option.bind (str "verdict") = Some "INCOMPLETE") "8: summary.json restates the frozen verdict"
      yield! fail (frozen |> Option.bind (str "outcome") = Some "NOT ACCEPTED") "8: summary.json restates the frozen outcome"
      yield! fail (frozen |> Option.bind (boolean "accepted") = Some false) "8: summary.json claims acceptance"
      yield! i.Frozen.Hashes |> Map.toList |> List.collect (fun (p, h) -> fail (stated p = Some h) (sprintf "8: %s does not match the sha256 the audit recorded (frozen evidence changed or misrecorded)" p)) ]

let private c9NoSilentExclusion (i: Inputs) =
    let byReq = i.Frozen.Obligations |> List.groupBy (fun o -> o.Requirement) |> Map.ofList
    let missing = i.Frozen.Obligations |> List.filter (fun o -> o.Verification = "NOT_RUN")
    let analysedMissing = requirements i |> List.collect (arr "obligations") |> List.filter (fun o -> str "frozenVerification" o = Some "NOT_RUN")
    [ yield!
          requirements i
          |> List.collect (fun r ->
              let stated = arr "obligations" r |> List.choose (str "vpo") |> Set.ofList
              let expected = Map.tryFind (rid r) byReq |> Option.defaultValue [] |> List.map (fun o -> o.Vpo) |> Set.ofList
              [ yield! fail (stated = expected) (sprintf "9: %s does not list exactly its frozen obligations" (rid r))
                yield! fail (str "scope" r |> Option.exists (fun s -> [ "application"; "process"; "tooling" ] |> List.contains s)) (sprintf "9: %s has no scope" (rid r))
                yield!
                    prop "remainingWork" r
                    |> Option.map (fun w -> workKinds |> List.collect (fun k -> fail (str k w |> Option.exists efforts.Contains) (sprintf "9: %s: remainingWork.%s missing or not an effort category" (rid r) k)))
                    |> Option.defaultValue [ sprintf "9: %s has no remainingWork" (rid r) ] ])
      yield! fail (analysedMissing.Length = missing.Length && missing.Length = 49) (sprintf "9: %d NOT_RUN obligations analysed, frozen evidence has %d" analysedMissing.Length missing.Length)
      yield!
          analysedMissing
          |> List.collect (fun o -> fail (num "primaryCause" o |> Option.exists (fun c -> c >= 1 && c <= 12)) (sprintf "9: %s has no primary cause" (str "id" o |> Option.defaultValue "?"))) ]

let private c10ReviewIncluded (i: Inputs) =
    let review = prop "independentReview" i.Summary
    let changed = requirements i |> List.filter (fun r -> prop "review" r |> Option.bind (str "initialClassification") <> Some(cls r))
    [ yield! fail (File.Exists(Path.Combine(i.AuditDir, "independent-review.md"))) "10: independent-review.md is missing"
      yield! fail (review |> Option.bind (boolean "performed") = Some true) "10: summary.json does not record the independent review"
      yield! fail (review |> Option.bind (num "classificationChanges") = Some changed.Length) (sprintf "10: summary.json states a different number of review changes than the matrix (%d)" changed.Length)
      yield!
          requirements i
          |> List.collect (fun r ->
              let rv = prop "review" r
              [ yield! fail (rv |> Option.bind (boolean "challenged") = Some true) (sprintf "10: %s was not challenged by the independent review" (rid r))
                yield! fail (rv |> Option.bind (str "finalClassification") = Some(cls r)) (sprintf "10: %s: review.finalClassification differs from the classification" (rid r)) ]) ]

let private checks =
    [ "1 exactly 31 analysed", c1Count
      "2 ids match the frozen ledger", c2Identity
      "3 one primary classification", c3OneClassification
      "4 classifications sum correctly", c4Sums
      "5 implemented claims have evidence", c5ImplementedHasEvidence
      "6 missing implementation is investigated", c6MissingHasInvestigation
      "7 historical and post-rehearsal evidence distinguishable", c7HistoricalVsPost
      "8 no historical verdict rewritten", c8NoVerdictRewrite
      "9 no requirement or obligation silently excluded", c9NoSilentExclusion
      "10 independent review included", c10ReviewIncluded ]

[<EntryPoint>]
let main argv =
    match argv with
    | [| auditDir; r5 |] ->
        let indyInit = Path.GetFullPath(Path.Combine(auditDir, "..", ".."))
        let inputs =
            { Frozen = loadFrozen r5
              Matrix = load (Path.Combine(auditDir, "requirement-matrix.json"))
              Summary = load (Path.Combine(auditDir, "summary.json"))
              AuditDir = auditDir
              R5 = r5
              IndyInit = indyInit }
        let results = checks |> List.map (fun (name, check) -> name, check inputs)
        results |> List.iter (fun (name, problems) -> printfn "%s %s" (if problems.IsEmpty then "PASS" else "FAIL") name; problems |> List.iter (printfn "     %s"))
        if results |> List.forall (snd >> List.isEmpty) then
            printfn "AUDIT INTEGRITY: PASS"
            0
        else
            printfn "AUDIT INTEGRITY: FAIL"
            1
    | _ ->
        eprintfn "usage: r5-audit-validate <audit-directory> <indy-init-rehearsal-005 checkout at b972cb4>"
        2
