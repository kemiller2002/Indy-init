/// Verifies that a Praxis build enforces requirements preservation on the real Indy-init requirements. The positive
/// control is an identity derivation of the compiled package (requirements-compiled/v1): one derived requirement per
/// normalized requirement, carrying every baseline obligation verbatim. Each other case changes that derivation in one
/// unauthorized way, or withholds what the gate needs to establish its inputs, and expects Praxis to refuse it (cases
/// P1-P10 of the Praxis requirements-preservation release). Every verdict comes from the given Praxis executable's
/// `requirements preservation verify`; this program only builds the inputs and compares the outcome.
///
///   praxis-preservation-verification --praxis PATH --baseline FILE --previous-baseline FILE --source DIR
///                                    --package FILE --out DIR
///   exit 0 when every case gives its expected verdict and reason codes, twice and byte-identically; 1 otherwise;
///   2 usage or unusable input
module PraxisPreservationVerification.Program

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions

type Obligation = { Id: string; Kind: string; Text: string; Tokens: string list }

type Requirement = { Id: string; Priority: string; Obligations: Obligation list }

/// One derived requirement document: its id, front-matter priority, and the obligations it carries verbatim.
type Document = { Id: string; Priority: string; Carries: Obligation list }

type Mapping = { Obligation: string; Criteria: string list; Reviewer: string option }

[<RequireQualifiedAccess>]
type Disposition =
    | Preserved of Mapping list
    | Excluded of approval: string

type Entry = { Requirement: string; Disposition: Disposition }

type DispositionSet = { BaselineId: string; Commit: string; Sha256: string option; Entries: Entry list }

type Approval = { DecisionId: string; Covers: string list; ApprovedBy: string; Kind: string }

[<RequireQualifiedAccess>]
type Pin =
    | Published
    | Absent
    | Other of sha256: string

/// Everything one gate run reads. Each case is this record with one change.
type Model =
    { Baseline: string
      Documents: Document list
      Disposition: DispositionSet
      Approvals: Approval list option
      Pin: Pin
      Trusted: string list }

type Case =
    { Id: string
      Title: string
      Contract: string
      /// "pass", "fail", "blocked", or "none" when the gate must not reach a verdict (exit 2).
      Verdict: string
      Codes: string list
      Model: Model
      /// A further check on the evidence, for example that an approved exclusion stays traceable.
      Check: JsonNode -> string option }

let operator = "indy-init-fixture-operator"
let reviewer = "indy-init-fixture-reviewer"
let agent = "agent:anthropic/claude-code"
let reviewDate = "2026-10-26"

let sha256Bytes (bytes: byte array) = Convert.ToHexStringLower(SHA256.HashData bytes)
let sha256File (path: string) = sha256Bytes (File.ReadAllBytes path)
let collapse (text: string) = Regex.Replace(text, @"\s+", " ").Trim()

// ---- reading the baseline and the compiled package ----------------------------------------------------------------

let private strings (node: JsonNode) =
    match node with
    | null -> []
    | n -> n.AsArray() |> Seq.map (fun x -> x.GetValue<string>()) |> Seq.toList

let readBaseline (path: string) =
    let root = JsonNode.Parse(File.ReadAllText path)

    let requirements =
        root["requirements"].AsArray()
        |> Seq.map (fun r ->
            { Id = r["id"].GetValue<string>()
              Priority = r["priority"].GetValue<string>()
              Obligations =
                r["obligations"].AsArray()
                |> Seq.map (fun o ->
                    { Id = o["id"].GetValue<string>()
                      Kind = o["kind"].GetValue<string>()
                      Text = o["text"].GetValue<string>()
                      Tokens = strings o["tokens"] })
                |> Seq.toList })
        |> Seq.toList

    let id = root["id"].GetValue<string>()
    let commit = (root["source"]).["commit"].GetValue<string>()
    id, commit, requirements

/// The identity derivation: the compiled package's normalized requirement for every baseline requirement, carrying
/// the baseline's obligations. Refuses a package that does not account for every baseline obligation exactly.
let derive (requirements: Requirement list) (packagePath: string) =
    let normalized =
        (JsonNode.Parse(File.ReadAllText packagePath)).["requirements"].AsArray()
        |> Seq.filter (fun n -> n["origin"].GetValue<string>() = "baseline")
        |> Seq.map (fun n -> n["source"].GetValue<string>(), (n["id"].GetValue<string>(), n["priority"].GetValue<string>(), strings n["obligations"]))
        |> Map.ofSeq

    let problems =
        [ for r in requirements do
              match normalized.TryFind r.Id with
              | None -> yield $"{r.Id} has no normalized requirement"
              | Some(_, _, obligations) when Set.ofList obligations <> (r.Obligations |> List.map _.Id |> Set.ofList) ->
                  yield $"{r.Id}: the package's obligations differ from the baseline's"
              | Some _ -> ()
          if normalized.Count <> requirements.Length then
              yield $"the package normalizes {normalized.Count} baseline requirements; the baseline has {requirements.Length}" ]

    match problems with
    | [] ->
        Ok(
            requirements
            |> List.map (fun r ->
                let id, priority, _ = normalized[r.Id]
                r.Id, { Id = id; Priority = priority; Carries = r.Obligations })
        )
    | _ -> Error problems

// ---- rendering a model into the gate's input files ------------------------------------------------------------------

let private cell (text: string) = (collapse text).Replace("|", "\\|")

let render (document: Document) =
    let table header (rows: Obligation list) =
        match rows with
        | [] -> ""
        | _ ->
            [ $"| ID | {header} |"; "|---|---|" ] @ (rows |> List.map (fun o -> $"| {o.Id} | {cell o.Text} |"))
            |> String.concat "\n"
            |> fun t -> t + "\n\n"

    let acceptance, statements = document.Carries |> List.partition (fun o -> o.Kind = "acceptance")

    $"---\nid: {document.Id}\nstatus: accepted\nversion: 1.0.0\npriority: {document.Priority}\n---\n\n# {document.Id}\n\nIdentity derivation of the Indy-init compiled requirements package (verification fixture).\n\n"
    + table "Acceptance criterion" acceptance
    + table "Statement" statements

let private json (write: Utf8JsonWriter -> unit) =
    use stream = new MemoryStream()

    (use w = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))
     write w)

    Encoding.UTF8.GetString(stream.ToArray()) + "\n"

let renderDisposition (set: DispositionSet) =
    json (fun w ->
        w.WriteStartObject()
        w.WriteString("schema", "praxis.requirements-disposition/1")
        w.WriteStartObject "baseline"
        w.WriteString("id", set.BaselineId)
        w.WriteString("commit", set.Commit)
        set.Sha256 |> Option.iter (fun sha -> w.WriteString("sha256", sha))
        w.WriteEndObject()
        w.WriteString("package", "indy-init-identity-derivation")
        w.WriteStartArray "dispositions"

        for entry in set.Entries do
            w.WriteStartObject()
            w.WriteString("requirement", entry.Requirement)

            match entry.Disposition with
            | Disposition.Excluded approval ->
                w.WriteString("disposition", "excluded-with-approval")
                w.WriteString("approval", approval)
                w.WriteString("rationale", "Verification fixture: excluded under the named approval.")
            | Disposition.Preserved mappings ->
                w.WriteString("disposition", "preserved")
                w.WriteStartArray "mappings"

                for m in mappings do
                    w.WriteStartObject()
                    w.WriteString("obligation", m.Obligation)
                    w.WriteStartArray "criteria"
                    m.Criteria |> List.iter w.WriteStringValue
                    w.WriteEndArray()

                    m.Reviewer
                    |> Option.iter (fun r ->
                        w.WriteStartObject "review"
                        w.WriteString("reviewer", r)
                        w.WriteString("date", reviewDate)
                        w.WriteString("basis", "Identity derivation: the criterion is the obligation's verbatim text.")
                        w.WriteEndObject())

                    w.WriteEndObject()

                w.WriteEndArray()

            w.WriteEndObject()

        w.WriteEndArray()
        w.WriteEndObject())

let renderApprovals (approvals: Approval list) =
    json (fun w ->
        w.WriteStartObject()
        w.WriteString("schema", "praxis.requirements-approvals/1")
        w.WriteStartArray "approvals"

        for a in approvals do
            w.WriteStartObject()
            w.WriteString("decisionId", a.DecisionId)
            w.WriteString("authority", "operator (verification fixture)")
            w.WriteString("scope", "Verification fixture: exclude the named requirement from this derived package.")
            w.WriteStartArray "covers"
            a.Covers |> List.iter w.WriteStringValue
            w.WriteEndArray()
            w.WriteString("date", reviewDate)
            w.WriteStartObject "approvedBy"
            w.WriteString("id", a.ApprovedBy)
            w.WriteString("kind", a.Kind)
            w.WriteEndObject()
            w.WriteEndObject()

        w.WriteEndArray()
        w.WriteEndObject())

// ---- running the gate -----------------------------------------------------------------------------------------------

type Run = { Exit: int; Out: string }

let execute (praxis: string) (arguments: string list) =
    let start = ProcessStartInfo(RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)

    if praxis.EndsWith(".dll", StringComparison.Ordinal) then
        start.FileName <- "dotnet"
        start.ArgumentList.Add praxis
    else
        start.FileName <- praxis

    arguments |> List.iter start.ArgumentList.Add
    use proc = Process.Start start
    let err = proc.StandardError.ReadToEndAsync()
    let out = proc.StandardOutput.ReadToEnd()
    proc.WaitForExit()
    err.Wait()
    { Exit = proc.ExitCode; Out = out }

/// Writes the model's files under `directory` and returns the gate's arguments and the files that make up the package.
let materialize (source: string) (directory: string) (model: Model) =
    let derived = Path.Combine(directory, "derived")
    Directory.CreateDirectory derived |> ignore

    for document in model.Documents do
        File.WriteAllText(Path.Combine(derived, document.Id + ".md"), render document)

    let disposition = Path.Combine(directory, "disposition.json")
    File.WriteAllText(disposition, renderDisposition model.Disposition)

    let approvals =
        model.Approvals
        |> Option.map (fun a ->
            let path = Path.Combine(directory, "approvals.json")
            File.WriteAllText(path, renderApprovals a)
            path)

    let approvalArguments =
        match approvals, model.Pin with
        | None, _ -> []
        | Some path, Pin.Published -> [ "--approvals"; path; "--approvals-sha256"; sha256File path ]
        | Some path, Pin.Absent -> [ "--approvals"; path ]
        | Some path, Pin.Other sha -> [ "--approvals"; path; "--approvals-sha256"; sha ]

    let arguments =
        [ "requirements"; "preservation"; "verify"
          "--baseline"; model.Baseline
          "--disposition"; disposition
          "--derived"; derived
          "--source"; source ]
        @ approvalArguments
        @ (model.Trusted |> List.collect (fun t -> [ "--trusted-approver"; t ]))

    let packageFiles =
        [ disposition ] @ Option.toList approvals @ (Directory.GetFiles(derived, "*.md") |> Array.sort |> Array.toList)

    arguments, packageFiles

let packageDigest (directory: string) (files: string list) =
    files
    |> List.map (fun f -> $"{Path.GetRelativePath(directory, f).Replace('\\', '/')} {sha256File f}\n")
    |> String.concat ""
    |> Encoding.UTF8.GetBytes
    |> sha256Bytes

// ---- the cases -------------------------------------------------------------------------------------------------------

let cases (baselinePath: string) (previous: string) (requirements: Requirement list) (documents: (string * Document) list) =
    let baselineId, commit, _ = readBaseline baselinePath
    let previousId, previousCommit, _ = readBaseline previous
    let byRequirement = documents |> Map.ofList

    let preserved (r: Requirement) =
        let document = byRequirement[r.Id]

        { Requirement = r.Id
          Disposition =
            Disposition.Preserved(
                r.Obligations
                |> List.map (fun o ->
                    { Obligation = o.Id
                      Criteria = [ $"{document.Id}#{o.Id}" ]
                      // Only a token-less obligation under five words needs a person's review; the rest are decided
                      // by tokens or by verbatim carriage.
                      Reviewer = if o.Tokens.IsEmpty && (collapse o.Text).Split(' ').Length < 5 then Some reviewer else None })
            ) }

    let identity =
        { Baseline = baselinePath
          Documents = documents |> List.map snd
          Disposition =
            { BaselineId = baselineId
              Commit = commit
              Sha256 = Some(sha256File baselinePath)
              Entries = requirements |> List.map preserved }
          Approvals = None
          Pin = Pin.Published
          Trusted = [ operator; reviewer ] }

    // The defect that motivated the gate (DC-008) concerned UI-016; it is P0 and carries value and acceptance
    // obligations. The false-completion case needs a P0 obligation that only a review can decide.
    let target = "UI-016"
    let targetDocument = byRequirement[target]

    let reviewed =
        requirements
        |> List.filter (fun r -> r.Priority = "P0")
        |> List.pick (fun r ->
            r.Obligations
            |> List.tryFind (fun o -> o.Tokens.IsEmpty && (collapse o.Text).Split(' ').Length < 5)
            |> Option.map (fun o -> r.Id, o.Id))

    let withEntries (f: Entry list -> Entry list) (m: Model) =
        { m with Disposition = { m.Disposition with Entries = f m.Disposition.Entries } }

    let mapEntry requirement (f: Entry -> Entry) =
        withEntries (List.map (fun e -> if e.Requirement = requirement then f e else e))

    let mapMappings requirement (f: Mapping -> Mapping) =
        mapEntry requirement (fun e ->
            match e.Disposition with
            | Disposition.Preserved ms -> { e with Disposition = Disposition.Preserved(ms |> List.map f) }
            | other -> { e with Disposition = other })

    let withoutDocument id (m: Model) =
        { m with Documents = m.Documents |> List.filter (fun d -> d.Id <> id) }

    let mapDocument id (f: Document -> Document) (m: Model) =
        { m with Documents = m.Documents |> List.map (fun d -> if d.Id = id then f d else d) }

    let excluded approvedBy kind =
        identity
        |> mapEntry target (fun e -> { e with Disposition = Disposition.Excluded "APR-VERIFY-UI-016" })
        |> withoutDocument targetDocument.Id
        |> fun m ->
            { m with
                Approvals = Some [ { DecisionId = "APR-VERIFY-UI-016"; Covers = [ target ]; ApprovedBy = approvedBy; Kind = kind } ] }

    let noCheck _ = None

    let traceable (evidence: JsonNode) =
        let excludedIds = evidence["exclusions"].AsArray() |> Seq.map (fun e -> e["requirement"].GetValue<string>()) |> Set.ofSeq

        let listed =
            evidence["requirements"].AsArray()
            |> Seq.exists (fun r -> r["id"].GetValue<string>() = target && r["disposition"].GetValue<string>() = "excluded-with-approval")

        if excludedIds.Contains target && listed then None
        else Some $"{target} is not reported as an approved, traceable exclusion"

    [ { Id = "P1"; Title = "Missing P0 requirement"; Contract = "P1 missing requirements"; Verdict = "fail"; Codes = [ "disposition-missing" ]; Check = noCheck
        Model = identity |> withEntries (List.filter (fun e -> e.Requirement <> target)) |> withoutDocument targetDocument.Id }
      { Id = "P2"; Title = "P0 requirement carried at a lower priority"; Contract = "P2 downgraded requirements"; Verdict = "fail"; Codes = [ "priority-downgraded" ]; Check = noCheck
        Model = identity |> mapDocument targetDocument.Id (fun d -> { d with Priority = "P1" }) }
      { Id = "P2b"; Title = "P0 acceptance obligation weakened to a statement"; Contract = "P2 downgraded requirements"; Verdict = "fail"; Codes = [ "acceptance-criterion-missing" ]; Check = noCheck
        Model = identity |> mapDocument targetDocument.Id (fun d -> { d with Carries = d.Carries |> List.map (fun o -> if o.Kind = "acceptance" then { o with Kind = "constraint" } else o) }) }
      { Id = "P3"; Title = "Exclusion with no approval record"; Contract = "P3 unauthorized exclusions"; Verdict = "fail"; Codes = [ "approval-missing" ]; Check = noCheck
        Model = { excluded operator "human" with Approvals = Some [] } }
      { Id = "P3b"; Title = "Exclusion approved by an agent"; Contract = "P3 unauthorized exclusions"; Verdict = "fail"; Codes = [ "approval-agent-authored" ]; Check = noCheck
        Model = { excluded agent "agent" with Trusted = [ operator; reviewer; agent ] } }
      { Id = "P3c"; Title = "Exclusion approved by an identity the caller does not trust"; Contract = "P3 unauthorized exclusions"; Verdict = "fail"; Codes = [ "approval-untrusted" ]; Check = noCheck
        Model = excluded "someone-else" "human" }
      { Id = "P4"; Title = "Baseline altered after the derivation (UI-016 P0 to P1)"; Contract = "P4 altered baseline"; Verdict = "fail"; Codes = [ "baseline-digest-mismatch" ]; Check = noCheck
        Model = identity (* the altered baseline path is set when the case is materialized *) }
      { Id = "P5"; Title = "Requirement id changed without an authorized transformation"; Contract = "P5 requirement identity mismatch"; Verdict = "fail"; Codes = [ "disposition-unknown-requirement"; "disposition-missing" ]; Check = noCheck
        Model = identity |> mapEntry target (fun e -> { e with Requirement = target + "-RENAMED" }) }
      { Id = "P6"; Title = "Obligation reported complete on an agent's review"; Contract = "P6 false completion"; Verdict = "blocked"; Codes = [ "review-untrusted" ]; Check = noCheck
        Model = identity |> mapMappings (fst reviewed) (fun m -> if m.Obligation = snd reviewed then { m with Reviewer = Some agent } else m) }
      { Id = "P6b"; Title = "Obligation reported complete against a criterion that does not exist"; Contract = "P6 false completion"; Verdict = "fail"; Codes = [ "criterion-not-found" ]; Check = noCheck
        Model = identity |> mapMappings target (fun m -> { m with Criteria = [ $"{targetDocument.Id}#VERIFIED-ELSEWHERE" ] }) }
      { Id = "P7"; Title = "Disposition prepared against another baseline (002d8ba)"; Contract = "P7 cross-run evidence"; Verdict = "fail"; Codes = [ "baseline-mismatch"; "baseline-digest-mismatch" ]; Check = noCheck
        Model = { identity with Disposition = { identity.Disposition with BaselineId = previousId; Commit = previousCommit; Sha256 = Some(sha256File previous) } } }
      { Id = "P7b"; Title = "Approvals presented with another run's digest"; Contract = "P7 cross-run evidence"; Verdict = "fail"; Codes = [ "approvals-digest-mismatch" ]; Check = noCheck
        Model = { excluded operator "human" with Pin = Pin.Other(sha256Bytes (Encoding.UTF8.GetBytes(renderApprovals [ { DecisionId = "APR-OTHER-RUN"; Covers = [ target ]; ApprovedBy = operator; Kind = "human" } ]))) } }
      { Id = "P8"; Title = "Exclusion approved by the trusted operator"; Contract = "P8 approved exclusion"; Verdict = "pass"; Codes = []; Check = traceable
        Model = excluded operator "human" }
      { Id = "P9"; Title = "Unchanged identity derivation of the compiled package"; Contract = "P9 unchanged requirements"; Verdict = "pass"; Codes = []; Check = noCheck
        Model = identity }
      { Id = "P10"; Title = "Disposition records no baseline digest"; Contract = "P10 incomplete verification"; Verdict = "blocked"; Codes = [ "baseline-digest-missing" ]; Check = noCheck
        Model = { identity with Disposition = { identity.Disposition with Sha256 = None } } }
      { Id = "P10b"; Title = "Approvals without their published digest"; Contract = "P10 incomplete verification"; Verdict = "blocked"; Codes = [ "approvals-unauthenticated" ]; Check = noCheck
        Model = { excluded operator "human" with Pin = Pin.Absent } }
      { Id = "P10c"; Title = "Baseline unreadable"; Contract = "P10 incomplete verification"; Verdict = "none"; Codes = []; Check = noCheck
        Model = { identity with Baseline = baselinePath + ".missing" } } ]

/// The baseline with UI-016's priority lowered, byte for byte otherwise: an unauthorized edit that still parses.
let alterBaseline (baselinePath: string) (destination: string) =
    let text = File.ReadAllText baselinePath
    let pattern = Regex("(\"id\": \"UI-016\",\\s*\"title\": \"[^\"]*\",\\s*\"priority\": )\"P0\"")
    let altered = pattern.Replace(text, "$1\"P1\"", 1)
    if altered = text then failwith "could not alter UI-016's priority in the baseline"
    File.WriteAllText(destination, altered)
    destination

// ---- the run ---------------------------------------------------------------------------------------------------------

type Outcome =
    { Case: Case
      Exit: int
      Verdict: string
      Codes: string list
      Deterministic: bool
      Problems: string list
      PackageSha256: string
      Evidence: string option }

let runCase (praxis: string) (source: string) (out: string) (case: Case) =
    let directory = Path.Combine(out, "cases", case.Id)
    if Directory.Exists directory then Directory.Delete(directory, true)
    Directory.CreateDirectory directory |> ignore

    let model =
        if case.Id = "P4" then { case.Model with Baseline = alterBaseline case.Model.Baseline (Path.Combine(directory, "baseline-altered.json")) }
        else case.Model

    let arguments, files = materialize source directory model
    let evidencePath = Path.Combine(directory, "evidence.json")
    let first = execute praxis (arguments @ [ "--output"; evidencePath; "--json" ])
    let second = execute praxis (arguments @ [ "--json" ])

    let document = if first.Exit = 2 then None else (try Some(JsonNode.Parse first.Out) with _ -> None)

    let verdict = document |> Option.map (fun d -> d["verdict"].GetValue<string>()) |> Option.defaultValue "none"

    let codes =
        document
        |> Option.map (fun d ->
            d["findings"].AsArray()
            |> Seq.filter (fun f -> f["status"].GetValue<string>() <> "info")
            |> Seq.map (fun f -> f["code"].GetValue<string>())
            |> Seq.distinct
            |> Seq.sort
            |> Seq.toList)
        |> Option.defaultValue []

    let expectedExit =
        match case.Verdict with
        | "pass" -> 0
        | "none" -> 2
        | _ -> 3

    let problems =
        [ if verdict <> case.Verdict then $"verdict {verdict}, expected {case.Verdict}"
          if first.Exit <> expectedExit then $"exit {first.Exit}, expected {expectedExit}"
          for code in case.Codes do
              if not (List.contains code codes) then $"reason code {code} missing"
          if case.Verdict = "pass" && not codes.IsEmpty then "a passing run reported " + String.Join(", ", codes)
          match document with
          | Some d -> yield! Option.toList (case.Check d)
          | None -> ()
          if first.Out <> second.Out || first.Exit <> second.Exit then "two runs on identical inputs differ"
          match document, File.Exists evidencePath with
          | Some _, true when File.ReadAllText evidencePath <> first.Out -> "--output evidence differs from --json output"
          | Some _, false -> "no --output evidence was written"
          | _ -> () ]

    { Case = case
      Exit = first.Exit
      Verdict = verdict
      Codes = codes
      Deterministic = first.Out = second.Out && first.Exit = second.Exit
      Problems = problems
      PackageSha256 = packageDigest directory files
      Evidence = if File.Exists evidencePath then Some(Path.GetRelativePath(out, evidencePath).Replace('\\', '/')) else None }

let report (praxis: string) (version: string) (baseline: string) (previous: string) (package: string) (source: string) (out: string) (outcomes: Outcome list) =
    let baselineId, commit, _ = readBaseline baseline
    let pass = outcomes |> List.forall (fun o -> o.Problems.IsEmpty)

    let text =
      json (fun w ->
        w.WriteStartObject()
        w.WriteString("schema", "indy-init.praxis-preservation-verification/1")
        w.WriteStartObject "praxis"
        w.WriteString("path", praxis)
        w.WriteString("version", version)
        w.WriteString("sha256", sha256File praxis)
        w.WriteEndObject()
        w.WriteStartObject "baseline"
        w.WriteString("id", baselineId)
        w.WriteString("commit", commit)
        w.WriteString("sha256", sha256File baseline)
        w.WriteString("previousSha256", sha256File previous)
        w.WriteEndObject()
        w.WriteStartObject "package"
        w.WriteString("path", "requirements-compiled/v1/normalized-requirements.json")
        w.WriteString("sha256", sha256File package)
        w.WriteEndObject()
        w.WriteString("source", Path.GetFileName(Path.TrimEndingDirectorySeparator source))
        w.WriteStartObject "trust"
        w.WriteString("operator", operator)
        w.WriteString("reviewer", reviewer)
        w.WriteEndObject()
        w.WriteStartArray "cases"

        for o in outcomes do
            w.WriteStartObject()
            w.WriteString("id", o.Case.Id)
            w.WriteString("title", o.Case.Title)
            w.WriteString("contract", o.Case.Contract)
            w.WriteString("expectedVerdict", o.Case.Verdict)
            w.WriteString("actualVerdict", o.Verdict)
            w.WriteNumber("exitCode", o.Exit)
            w.WriteStartArray "expectedCodes"
            o.Case.Codes |> List.iter w.WriteStringValue
            w.WriteEndArray()
            w.WriteStartArray "reasonCodes"
            o.Codes |> List.iter w.WriteStringValue
            w.WriteEndArray()
            w.WriteString("packageSha256", o.PackageSha256)

            match o.Evidence with
            | Some e ->
                w.WriteString("evidence", e)
                w.WriteString("evidenceSha256", sha256File (Path.Combine(out, e)))
            | None -> w.WriteNull "evidence"

            w.WriteBoolean("deterministic", o.Deterministic)
            w.WriteString("result", if o.Problems.IsEmpty then "PASS" else "FAIL")
            w.WriteStartArray "problems"
            o.Problems |> List.iter w.WriteStringValue
            w.WriteEndArray()
            w.WriteEndObject()

        w.WriteEndArray()
        w.WriteString("verdict", if pass then "PASS" else "FAIL")
        w.WriteEndObject())

    text, pass

let rec private option (name: string) (arguments: string list) =
    match arguments with
    | flag :: value :: _ when flag = name -> Some value
    | _ :: rest -> option name rest
    | [] -> None

[<EntryPoint>]
let main argv =
    let arguments = List.ofArray argv

    match [ "--praxis"; "--baseline"; "--previous-baseline"; "--source"; "--package"; "--out" ] |> List.map (fun n -> option n arguments) with
    | [ Some praxis; Some baseline; Some previous; Some source; Some package; Some out ] ->
        let _, _, requirements = readBaseline baseline

        match derive requirements package with
        | Error problems ->
            problems |> List.iter (eprintfn "ERROR %s")
            2
        | Ok documents ->
            Directory.CreateDirectory out |> ignore
            let version = (execute praxis [ "version" ]).Out.Trim()
            let outcomes = cases baseline previous requirements documents |> List.map (runCase praxis source out)

            for o in outcomes do
                printfn "%-5s %-6s %-8s exit %d  %s" o.Case.Id (if o.Problems.IsEmpty then "PASS" else "FAIL") o.Verdict o.Exit (String.Join(", ", o.Codes))
                o.Problems |> List.iter (printfn "      %s")

            let text, pass = report praxis version baseline previous package source out outcomes
            File.WriteAllText(Path.Combine(out, "praxis-preservation-verification.json"), text)
            printfn "verdict: %s (praxis %s)" (if pass then "PASS" else "FAIL") version
            if pass then 0 else 1
    | _ ->
        eprintfn "usage: praxis-preservation-verification --praxis PATH --baseline FILE --previous-baseline FILE --source DIR --package FILE --out DIR"
        2
