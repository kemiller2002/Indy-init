/// Validates the Rehearsal 7 persistence requirements (ADR-R7-001) against a requirements baseline.
///
///   dotnet fsi tools/validate-r7-persistence-requirements.fsx -- --root DIR --baseline FILE
///       --previous FILE --trace FILE [--out FILE] [--self-test true]
///
/// --baseline is the praxis.requirements-baseline/1 document to check (the Rehearsal 7 baseline), --previous the
/// baseline it replaces (Rehearsal 6), --trace docs/rehearsal-007/traceability.json. Every check is mechanical: a
/// pattern list, an identifier cross-check or a text comparison. Matches that a negation permits are listed in the
/// report for human review; nothing is waived silently.
/// --self-test true runs the negative controls instead: each planted defect must fail its check.
/// Exit codes: 0 every check PASS (or every planted defect caught); 1 a check FAILED (or a defect missed); 2 unusable input.

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

// ---------------------------------------------------------------------------------------------------------------
// Input

type Obligation = { Id: string; Kind: string; Text: string }
type Requirement = { Id: string; Priority: string; Document: string; Heading: string; Obligations: Obligation list }
type Baseline = { Id: string; Documents: (string * string) list; Requirements: Requirement list }

let private str (name: string) (e: JsonElement) =
    match e.TryGetProperty name with
    | true, v when v.ValueKind = JsonValueKind.String -> v.GetString()
    | _ -> ""

let private items (name: string) (e: JsonElement) =
    match e.TryGetProperty name with
    | true, v when v.ValueKind = JsonValueKind.Array -> v.EnumerateArray() |> List.ofSeq
    | _ -> []

let private strs name e = items name e |> List.map (fun (x: JsonElement) -> x.GetString())

let private parse (path: string) = (JsonDocument.Parse(File.ReadAllText path)).RootElement

let private baselineOf (root: JsonElement) : Baseline =
    let source = root.GetProperty "source"
    { Id = str "id" root
      Documents = items "documents" source |> List.map (fun d -> str "path" d, str "sha256" d)
      Requirements =
        items "requirements" root
        |> List.map (fun r ->
            { Id = str "id" r
              Priority = str "priority" r
              Document = str "document" r
              Heading = str "heading" r
              Obligations = items "obligations" r |> List.map (fun o -> { Id = str "id" o; Kind = str "kind" o; Text = str "text" o }) }) }

// ---------------------------------------------------------------------------------------------------------------
// Results

type Finding = { Subject: string; Detail: string }
type Check = { Id: string; Title: string; Findings: Finding list; Reviewed: Finding list }

let private check id title findings reviewed = { Id = id; Title = title; Findings = findings; Reviewed = reviewed }
let private finding subject detail = { Subject = subject; Detail = detail }

// ---------------------------------------------------------------------------------------------------------------
// Shared rules

let persistenceDocument = "docs/CAUSA-PERSISTENCE-REQUIREMENTS.md"

let private isPersist (r: Requirement) = r.Id.StartsWith("PERSIST-", StringComparison.Ordinal)
let private number (id: string) = Int32.Parse(id.Substring("PERSIST-".Length))
let private isTestRequirement (r: Requirement) = isPersist r && number r.Id >= 40 && number r.Id <= 54

/// A sentence that states a prohibition or a removal, not a duty to provide the thing it names.
let private negation =
    Regex(@"\b(MUST NOT|must not|never|no|not|without|none|nothing|supersed\w*|removed|deleted|instead of)\b")

let private rx (pattern: string) = Regex(pattern, RegexOptions.IgnoreCase)

let offlineDurability =
    rx @"saved locally|saving locally|queued offline|offline (synchroni[sz]ation )?queue|local queue|local[- ]durab|durable local|syncing remotely|remotely synced|recover(able|ed)? after (application |browser )?restart|survives? (browser|application )?restart|crash recovery|pending writes"

let sharedStorage =
    rx @"localStorage|sessionStorage|IndexedDB|Cache Storage|Origin Private File System|\bOPFS\b|BroadcastChannel|shared worker|service worker|device store|shared store|browser store"

let webLocks = rx @"navigator\.locks|Web Locks|storage lease|cross-tab (lock|exclusion|guard|messag)"

/// An instruction to take implementation material from earlier builds.
let copyInstruction =
    rx @"\b(copy|copied|copying|vendor\w*|reus\w*|port(ed|ing)?|extract\w*|adapt\w*|translat\w*)\b[^.]{0,120}\b(source|code|implementation|tests?|scripts?)\b"

let earlierBuild = rx @"rehearsal|R00\d|\bR[1-6]\b|Causa|earlier|existing|previous|accepted"

/// Scans obligation texts: a match is a finding unless negated; a negated match is listed for review.
let private scan (pattern: Regex) (extra: Regex option) (requirements: Requirement list) =
    let hits =
        requirements
        |> List.collect (fun r -> r.Obligations |> List.map (fun o -> r, o))
        |> List.filter (fun (_, o) -> pattern.IsMatch o.Text && (extra |> Option.forall (fun x -> x.IsMatch o.Text)))
    let negated, positive = hits |> List.partition (fun (_, o) -> negation.IsMatch o.Text)
    positive |> List.map (fun (_, o) -> finding o.Id o.Text), negated |> List.map (fun (_, o) -> finding o.Id o.Text)

// ---------------------------------------------------------------------------------------------------------------
// Checks

let normalizeNewlines (s: string) = s.Replace("\r\n", "\n").Replace("\r", "\n")

let sha256 (text: string) =
    Security.Cryptography.SHA256.HashData(Text.Encoding.UTF8.GetBytes(normalizeNewlines text))
    |> Convert.ToHexString
    |> fun s -> s.ToLowerInvariant()

let documentPinned (root: string) (b: Baseline) =
    let file = Path.Combine(root, persistenceDocument)
    let findings =
        match b.Documents |> List.tryFind (fst >> (=) persistenceDocument) with
        | None -> [ finding persistenceDocument "not a document of the baseline" ]
        | Some _ when not (File.Exists file) -> [ finding persistenceDocument "missing from the checkout" ]
        | Some(_, sha) when sha <> sha256 (File.ReadAllText file) -> [ finding persistenceDocument "checkout differs from the pinned document" ]
        | Some _ -> []
    let count = b.Requirements |> List.filter isPersist |> List.length
    check "R7-01" "The persistence requirements are pinned in the baseline" (findings @ (if count = 0 then [ finding persistenceDocument "no PERSIST requirement" ] else [])) []

let noOfflineContradiction (b: Baseline) =
    let f, r = scan offlineDurability None b.Requirements
    check "R7-02" "No contradictory offline-persistence requirement remains" f r

let noSharedStorage (b: Baseline) =
    let f, r = scan sharedStorage None b.Requirements
    let prohibition =
        b.Requirements
        |> List.tryFind (fun r -> r.Id = "PERSIST-003")
        |> Option.filter (fun r -> r.Obligations |> List.exists (fun o -> o.Text.Contains("MUST NOT be written to localStorage")))
    check "R7-03" "No shared investigation-storage requirement remains" (f @ (if prohibition.IsNone then [ finding "PERSIST-003" "the storage prohibition is missing" ] else [])) r

let noWebLocks (b: Baseline) =
    let f, r = scan webLocks None b.Requirements
    check "R7-04" "No Web Locks dependency remains" f r

/// Each GitHub conflict-handling topic must be stated as a P0 duty in its requirement.
let conflictTopics =
    [ "atomic commit with an expected head", "PERSIST-006", [ "createCommitOnBranch"; "expectedHeadOid" ]
      "confirmed only after read-back", "PERSIST-007", [ "re-read at that commit" ]
      "stale head detected and classified", "PERSIST-008", [ "stale head"; "network failure MUST NOT be reported" ]
      "automatic rebase over unrelated changes", "PERSIST-009", [ "unchanged"; "without asking the user" ]
      "rebase attempts limited", "PERSIST-010", [ "at most three automatic rebase attempts" ]
      "same-investigation change preserves unsaved changes", "PERSIST-011", [ "keep every unsaved change unchanged in memory" ]
      "genuine conflicts resolved explicitly", "PERSIST-012", [ "until the user chooses a resolution" ]
      "never silently overwrite", "PERSIST-013", [ "MUST NOT force-push"; "remain reachable from the branch head" ]
      "unique write markers", "PERSIST-014", [ "unique across tabs, devices and page loads" ]
      "unknown outcome reconciled before retry", "PERSIST-015", [ "MUST NOT send another mutation for it until reconciliation decides the outcome" ]
      "automatic retries bounded", "PERSIST-016", [ "at most four commit mutations" ] ]

let conflictHandling (b: Baseline) =
    let byId = b.Requirements |> List.map (fun r -> r.Id, r) |> Map.ofList
    let topicFindings (topic, id, phrases: string list) =
        match byId.TryFind id with
        | None -> [ finding id (sprintf "%s: requirement missing" topic) ]
        | Some r when r.Priority <> "P0" -> [ finding id (sprintf "%s: priority %s, not P0" topic r.Priority) ]
        | Some r ->
            phrases
            |> List.filter (fun p -> not (r.Obligations |> List.exists (fun o -> o.Text.Contains(p, StringComparison.Ordinal))))
            |> List.map (fun p -> finding id (sprintf "%s: no obligation states '%s'" topic p))
    check "R7-05" "GitHub conflict handling is fully specified" (conflictTopics |> List.collect topicFindings) []

let private patRef = Regex(@"PAT-(\d{2})")
let private persistRange = Regex(@"PERSIST-(\d{3})(?: to PERSIST-(\d{3}))?")

/// Requirement ids a test requirement's "covers" sentence names, with "A to B" ranges expanded.
let coveredBy (r: Requirement) =
    r.Obligations
    |> List.filter (fun o -> o.Text.Contains("covers", StringComparison.Ordinal))
    |> List.collect (fun o ->
        persistRange.Matches(o.Text)
        |> Seq.collect (fun m ->
            let a = int m.Groups.[1].Value
            let z = if m.Groups.[2].Success then int m.Groups.[2].Value else a
            [ a..z ] |> List.map (sprintf "PERSIST-%03d"))
        |> List.ofSeq)
    |> List.distinct

let testIdOf (r: Requirement) =
    let m = patRef.Match r.Heading
    if m.Success then Some m.Value else None

let acceptanceObligations (b: Baseline) (trace: JsonElement) =
    let persist = b.Requirements |> List.filter isPersist
    let ids = persist |> List.map (fun r -> r.Id) |> Set.ofList
    let tests = persist |> List.filter isTestRequirement
    let testIds = tests |> List.choose testIdOf |> Set.ofList
    let covered = tests |> List.collect coveredBy |> Set.ofList
    let noAcceptance =
        persist
        |> List.filter (fun r -> not (r.Obligations |> List.exists (fun o -> o.Kind = "acceptance")))
        |> List.map (fun r -> finding r.Id "no acceptance obligation")
    let functional = persist |> List.filter (fun r -> not (isTestRequirement r) && number r.Id < 40)
    let unreferenced =
        functional
        |> List.filter (fun r ->
            not (r.Obligations |> List.exists (fun o -> o.Kind = "acceptance" && patRef.Matches(o.Text) |> Seq.exists (fun m -> testIds.Contains m.Value))))
        |> List.map (fun r -> finding r.Id "its acceptance names no existing PAT test")
    let uncovered = functional |> List.filter (fun r -> not (covered.Contains r.Id)) |> List.map (fun r -> finding r.Id "no PAT test covers it")
    let danglingCover =
        tests |> List.collect (fun t -> coveredBy t |> List.filter (ids.Contains >> not) |> List.map (fun c -> finding t.Id (sprintf "covers %s, which does not exist" c)))
    let testless = tests |> List.filter (fun t -> (testIdOf t).IsNone || (coveredBy t).IsEmpty) |> List.map (fun t -> finding t.Id "test requirement without a PAT id or a covers list")
    let expected = [ 1..15 ] |> List.map (sprintf "PAT-%02d") |> Set.ofList
    let gaps = Set.difference expected testIds |> Set.toList |> List.map (fun t -> finding t "PAT test missing")
    let named =
        items "tests" trace
        |> List.collect (fun t ->
            match persist |> List.tryFind (fun r -> r.Id = str "requirement" t) with
            | None -> [ finding (str "id" t) (sprintf "traced to %s, which does not exist" (str "requirement" t)) ]
            | Some r when not (r.Heading.Contains(str "id" t + " " + (str "name" t).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)) ->
                [ finding (str "id" t) (sprintf "%s heading does not name '%s %s'" r.Id (str "id" t) (str "name" t)) ]
            | Some _ -> [])
    check "R7-06" "Every new requirement has an independently verifiable acceptance obligation"
        (noAcceptance @ unreferenced @ uncovered @ danglingCover @ testless @ gaps @ named) []

/// Historical evidence and decision records: they report what happened and instruct nothing for Rehearsal 7.
let historical =
    rx @"^docs/(REHEARSAL-00\d-RESULTS|CAUSA-ACCEPTANCE-VERIFICATION|OWNER-DECISIONS-|NEXT-REHEARSAL-PREFLIGHT-STATUS|REHEARSAL-003-DEFERRED|REHEARSAL-004-WRITE-PATH-PLAN)|^docs/(rehearsal-00[3-7]|engineering-improvement|rehearsal-005-implementation-audit)/"

let private permitted (trace: JsonElement) =
    items "permittedMentions" trace |> List.map (fun p -> str "document" p, str "contains" p)

let noCopyInstruction (root: string) (b: Baseline) (trace: JsonElement) =
    let f, r = scan copyInstruction (Some earlierBuild) b.Requirements
    let allowed = permitted trace
    let docs =
        Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
        |> Array.map (fun p -> Path.GetRelativePath(root, p).Replace('\\', '/'))
        |> Array.filter (fun p -> not (historical.IsMatch p) && not (b.Documents |> List.exists (fst >> (=) p)))
        |> Array.sort
        |> List.ofArray
    let lineHits =
        docs
        |> List.collect (fun d ->
            File.ReadAllLines(Path.Combine(root, d))
            |> Array.indexed
            |> Array.filter (fun (_, l) -> copyInstruction.IsMatch l && earlierBuild.IsMatch l)
            |> Array.map (fun (i, l) -> d, i + 1, l.Trim())
            |> List.ofArray)
    let isAllowed (d, _, (l: string)) = negation.IsMatch l || allowed |> List.exists (fun (ad, c) -> ad = d && l.Contains(c, StringComparison.Ordinal))
    let docFindings, docReviewed = lineHits |> List.partition (isAllowed >> not)
    let asFinding (d, n, l) = finding (sprintf "%s:%d" d n) l
    check "R7-07" "No requirement instructs the agent to copy Causa implementation code"
        (f @ (docFindings |> List.map asFinding)) (r @ (docReviewed |> List.map asFinding))

/// Every Rehearsal 6 requirement is still present; every obligation whose text changed is traced to what replaces it.
let traceability (b: Baseline) (previous: Baseline) (trace: JsonElement) =
    let current = b.Requirements |> List.map (fun r -> r.Id, r) |> Map.ofList
    let knownIds =
        b.Requirements |> List.collect (fun r -> r.Id :: (r.Obligations |> List.map (fun o -> o.Id))) |> Set.ofList
    let superseded = items "supersededObligations" trace |> List.map (fun e -> str "previous" e, (str "previousText" e, strs "supersededBy" e)) |> Map.ofList
    let changedRequirements = items "changedRequirements" trace |> List.map (str "requirement") |> Set.ofList
    let removed = previous.Requirements |> List.filter (fun r -> not (current.ContainsKey r.Id)) |> List.map (fun r -> finding r.Id "Rehearsal 6 requirement no longer in the baseline")
    let changed =
        previous.Requirements
        |> List.choose (fun r -> current.TryFind r.Id |> Option.map (fun c -> r, c))
        |> List.collect (fun (r, c) ->
            let now = c.Obligations |> List.map (fun o -> o.Text) |> Set.ofList
            r.Obligations |> List.filter (fun o -> not (now.Contains o.Text)) |> List.map (fun o -> r, o))
    let untraced = changed |> List.filter (fun (_, o) -> not (superseded.ContainsKey o.Id)) |> List.map (fun (_, o) -> finding o.Id "changed obligation not traced in supersededObligations")
    let unlisted =
        changed |> List.map (fun (r, _) -> r.Id) |> List.distinct |> List.filter (changedRequirements.Contains >> not)
        |> List.map (fun id -> finding id "changed requirement not listed in changedRequirements")
    let changedIds = changed |> List.map (fun (_, o) -> o.Id) |> Set.ofList
    let previousTexts = previous.Requirements |> List.collect (fun r -> r.Obligations) |> List.map (fun o -> o.Id, o.Text) |> Map.ofList
    let stale =
        superseded
        |> Map.toList
        |> List.collect (fun (id, (text, by)) ->
            [ if not (changedIds.Contains id) then finding id "traced as superseded, but its text is unchanged or it never existed"
              if previousTexts.TryFind id <> Some text then finding id "previousText is not the Rehearsal 6 text"
              if by.IsEmpty then finding id "supersededBy is empty"
              yield! by |> List.filter (knownIds.Contains >> not) |> List.map (fun x -> finding id (sprintf "supersededBy %s, which does not exist" x)) ])
    check "R7-08" "All affected requirement identifiers remain traceable" (removed @ untraced @ unlisted @ stale) []

let requiredDefects = [ "R6-D-002"; "R6-D-003" ]

let defectLinks (b: Baseline) (trace: JsonElement) =
    let reqIds = b.Requirements |> List.map (fun r -> r.Id) |> Set.ofList
    let testIds = items "tests" trace |> List.map (str "id") |> Set.ofList
    let defects = items "defects" trace
    let present = defects |> List.map (str "id") |> Set.ofList
    let missing = requiredDefects |> List.filter (present.Contains >> not) |> List.map (fun d -> finding d "defect not traced")
    let rebaseFindings = defects |> List.filter (fun d -> str "group" d = "github-conflict-rebase")
    let noRebase = if rebaseFindings.IsEmpty then [ finding "github-conflict-rebase" "no Rehearsal 6 conflict or rebase finding traced" ] else []
    let links =
        defects
        |> List.collect (fun d ->
            let rs, ts = strs "requirements" d, strs "tests" d
            [ if rs.IsEmpty then finding (str "id" d) "no requirement"
              if ts.IsEmpty then finding (str "id" d) "no test"
              yield! rs |> List.filter (reqIds.Contains >> not) |> List.map (fun r -> finding (str "id" d) (sprintf "requirement %s does not exist" r))
              yield! ts |> List.filter (testIds.Contains >> not) |> List.map (fun t -> finding (str "id" d) (sprintf "test %s does not exist" t)) ])
    check "R7-09" "Historical defects link to requirements and tests" (missing @ noRebase @ links) []

/// Long-term documents outside the baseline: a persistence statement the new architecture contradicts must sit in
/// a section that records its supersession, or be negated.
let longTermDocuments (root: string) (b: Baseline) (trace: JsonElement) =
    let allowed = permitted trace
    let patterns = [ offlineDurability; sharedStorage; webLocks ]
    let sections (lines: string[]) =
        lines
        |> Array.indexed
        |> Array.fold (fun (acc: (int * string list) list) (i, l) ->
            match acc with
            | _ when l.StartsWith("#", StringComparison.Ordinal) -> (i, [ l ]) :: acc
            | (s, ls) :: rest -> (s, l :: ls) :: rest
            | [] -> [ (i, [ l ]) ]) []
        |> List.rev
        |> List.map (fun (s, ls) -> s, List.rev ls)
    let docs =
        Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
        |> Array.map (fun p -> Path.GetRelativePath(root, p).Replace('\\', '/'))
        |> Array.filter (fun p -> not (historical.IsMatch p) && not (b.Documents |> List.exists (fst >> (=) p)))
        |> Array.sort
        |> List.ofArray
    let hits =
        docs
        |> List.collect (fun d ->
            let lines = File.ReadAllLines(Path.Combine(root, d))
            sections lines
            |> List.collect (fun (start, ls) ->
                let text = String.concat "\n" ls
                let noted = text.Contains("ADR-R7-001", StringComparison.Ordinal) || text.Contains("PERSIST-0", StringComparison.Ordinal)
                ls
                |> List.indexed
                |> List.filter (fun (_, l) -> patterns |> List.exists (fun p -> p.IsMatch l))
                |> List.map (fun (k, l) -> d, start + k + 1, l.Trim(), noted || negation.IsMatch l)))
    let isAllowed (d, _, (l: string), ok) = ok || allowed |> List.exists (fun (ad, c) -> ad = d && l.Contains(c, StringComparison.Ordinal))
    let bad, ok = hits |> List.partition (isAllowed >> not)
    let asFinding (d, n, l, _) = finding (sprintf "%s:%d" d n) l
    check "R7-10" "Long-term documents mark every contradicted persistence statement as superseded"
        (bad |> List.map asFinding) (ok |> List.map asFinding)

// ---------------------------------------------------------------------------------------------------------------
// Report

let private findingJson (f: Finding) = dict [ "subject", box f.Subject; "detail", box f.Detail ]

let report (baseline: Baseline) (checks: Check list) =
    let verdict = if checks |> List.forall (fun c -> c.Findings.IsEmpty) then "PASS" else "FAIL"
    dict
        [ "schema", box "indy-init.r7-persistence-validation/1"
          "baseline", box baseline.Id
          "verdict", box verdict
          "checks",
          box (
              checks
              |> List.map (fun c ->
                  dict
                      [ "id", box c.Id
                        "title", box c.Title
                        "status", box (if c.Findings.IsEmpty then "PASS" else "FAIL")
                        "findings", box (c.Findings |> List.map findingJson)
                        "permittedForReview", box (c.Reviewed |> List.map findingJson) ])
          ) ],
    verdict

// ---------------------------------------------------------------------------------------------------------------
// Negative controls (AR-027): each planted defect must make its check fail.

let private mapRequirement (id: string) (f: Requirement -> Requirement) (b: Baseline) =
    { b with Requirements = b.Requirements |> List.map (fun r -> if r.Id = id then f r else r) }

let private plant (id: string) (text: string) (b: Baseline) =
    b |> mapRequirement id (fun r -> { r with Obligations = r.Obligations @ [ { Id = sprintf "%s-O%d" id (r.Obligations.Length + 1); Kind = "constraint"; Text = text } ] })

let private dropWhere (id: string) (contains: string) (b: Baseline) =
    b |> mapRequirement id (fun r -> { r with Obligations = r.Obligations |> List.filter (fun o -> not (o.Text.Contains(contains, StringComparison.Ordinal))) })

let private withoutRequirement (id: string) (b: Baseline) = { b with Requirements = b.Requirements |> List.filter (fun r -> r.Id <> id) }

let private traceWithout (arrayName: string) (key: string) (value: string) (trace: JsonElement) =
    let node = Text.Json.Nodes.JsonNode.Parse(trace.GetRawText())
    let a = node.[arrayName].AsArray()
    a |> Seq.filter (fun e -> e.[key].GetValue<string>() = value) |> List.ofSeq |> List.iter (a.Remove >> ignore)
    (JsonDocument.Parse(node.ToJsonString())).RootElement

let selfTest (root: string) (b: Baseline) (previous: Baseline) (trace: JsonElement) =
    let checks (b: Baseline) (t: JsonElement) =
        [ documentPinned root b; noOfflineContradiction b; noSharedStorage b; noWebLocks b; conflictHandling b
          acceptanceObligations b t; noCopyInstruction root b t; traceability b previous t; defectLinks b t ]
    let fails id (b: Baseline) t = checks b t |> List.exists (fun c -> c.Id = id && not c.Findings.IsEmpty)
    let cases =
        [ "offline durability restored", "R7-02", plant "AR-035" "Device-local durable state MUST survive several tabs sharing one store." b, trace
          "offline queue required", "R7-02", plant "R10.5" "Edits made offline are kept in a local queue and replayed on reconnect." b, trace
          "shared storage required", "R7-03", plant "D-204" "IndexedDB or another browser store may be used." b, trace
          "storage prohibition removed", "R7-03", dropWhere "PERSIST-003" "MUST NOT be written to localStorage" b, trace
          "Web Locks required", "R7-04", plant "AR-035" "Cross-tab writes MUST hold an exclusive navigator.locks section." b, trace
          "rebase limit removed", "R7-05", dropWhere "PERSIST-010" "at most three automatic rebase attempts" b, trace
          "reconcile-before-retry removed", "R7-05", dropWhere "PERSIST-015" "MUST NOT send another mutation" b, trace
          "acceptance obligation removed", "R7-06", dropWhere "PERSIST-017" "Acceptance:" b, trace
          "PAT test removed", "R7-06", withoutRequirement "PERSIST-047" b, trace
          "copy instruction planted", "R7-07", plant "REHEARSAL-AGENT-INTERFACE-FREEZE§contract-freeze-procedure" "Copy the persistence module source code from indy-init-rehearsal-006." b, trace
          "requirement removed", "R7-08", withoutRequirement "R10.7" b, trace
          "obligation changed without trace", "R7-08", plant "UI-004" "Submit commands twice when unsure." (dropWhere "UI-004" "Reject stale-version submissions" b), trace
          "supersession entry dropped", "R7-08", b, traceWithout "supersededObligations" "previous" "UI-006-O1" trace
          "R6-D-002 untraced", "R7-09", b, traceWithout "defects" "id" "R6-D-002" trace
          "R6-D-003 untraced", "R7-09", b, traceWithout "defects" "id" "R6-D-003" trace ]
    let results = cases |> List.map (fun (name, id, mb, mt) -> name, id, fails id mb mt)
    let clean = checks b trace |> List.forall (fun c -> c.Findings.IsEmpty)
    results |> List.iter (fun (name, id, caught) -> printfn "%s %s: %s" (if caught then "CAUGHT" else "MISSED") id name)
    printfn "unmutated inputs: %s" (if clean then "PASS" else "FAIL")
    if clean && results |> List.forall (fun (_, _, c) -> c) then 0 else 1

let rec private options (args: string list) (m: Map<string, string>) =
    match args with
    | [] -> Ok m
    | k :: v :: rest when k.StartsWith("--", StringComparison.Ordinal) -> options rest (m.Add(k.Substring 2, v))
    | a :: _ -> Error(sprintf "unexpected argument '%s'" a)

let run (argv: string list) =
    match options argv Map.empty with
    | Ok m when [ "root"; "baseline"; "previous"; "trace" ] |> List.forall m.ContainsKey ->
        let root = m.["root"]
        let b = baselineOf (parse m.["baseline"])
        let previous = baselineOf (parse m.["previous"])
        let trace = parse m.["trace"]
        if m.TryFind "self-test" = Some "true" then selfTest root b previous trace else
        let checks =
            [ documentPinned root b
              noOfflineContradiction b
              noSharedStorage b
              noWebLocks b
              conflictHandling b
              acceptanceObligations b trace
              noCopyInstruction root b trace
              traceability b previous trace
              defectLinks b trace
              longTermDocuments root b trace ]
        let doc, verdict = report b checks
        let json = JsonSerializer.Serialize(doc, JsonSerializerOptions(WriteIndented = true, Encoder = Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping))
        match m.TryFind "out" with
        | Some out -> File.WriteAllText(out, json + "\n")
        | None -> ()
        checks
        |> List.iter (fun c ->
            printfn "%s %s: %s (%d findings, %d permitted for review)" (if c.Findings.IsEmpty then "PASS" else "FAIL") c.Id c.Title c.Findings.Length c.Reviewed.Length
            c.Findings |> List.iter (fun f -> printfn "    %s: %s" f.Subject f.Detail))
        printfn "verdict: %s" verdict
        if verdict = "PASS" then 0 else 1
    | Ok _ ->
        eprintfn "usage: validate-r7-persistence-requirements.fsx -- --root DIR --baseline FILE --previous FILE --trace FILE [--out FILE]"
        2
    | Error e ->
        eprintfn "%s" e
        2

exit (run (fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")))
