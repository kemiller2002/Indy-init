// conventions.fsx: Implementation Convention Catalog tooling (PROPOSED; not part of any governed gate).
//
//   dotnet fsi tools/conventions/conventions.fsx -- validate [--dir D]          catalog + failure-data integrity
//   dotnet fsi tools/conventions/conventions.fsx -- forecast [--dir D] [--json]  ranked net savings, cumulative, 80% set
//   dotnet fsi tools/conventions/conventions.fsx -- render [--dir D]             rewrite the generated blocks in the docs
//   dotnet fsi tools/conventions/conventions.fsx -- check --workspace W [--forma-min 0.13.0]
//                                                                                workspace convention checks CK-01..CK-04
//   dotnet fsi tools/conventions/conventions.fsx -- test                         self-tests with negative controls
//
// D defaults to docs/optimization/implementation-conventions. Exit codes: 0 pass, 3 findings, 2 bad invocation.
// Every function below except the command dispatch at the bottom is pure over its inputs (file reads are explicit).

open System
open System.IO
open System.Text.Json.Nodes
open System.Text.RegularExpressions

// ---------------------------------------------------------------- arguments

let argv = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let opt name = argv |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let flag name = argv |> List.contains name
let defaultDir = "docs/optimization/implementation-conventions"

// ---------------------------------------------------------------- json helpers

let str (n: JsonNode) (k: string) = match n with null -> "" | n -> match n[k] with null -> "" | v -> v.ToString()
let arr (n: JsonNode) (k: string) = match n with null -> [] | n -> match n[k] with :? JsonArray as a -> a |> Seq.toList | _ -> []
let strs n k = arr n k |> List.map (fun x -> x.ToString())
let num (n: JsonNode) (k: string) : float option =
    match n with
    | null -> None
    | n -> match n[k] with null -> None | v -> (try Some(v.GetValue<float>()) with _ -> None)

// ---------------------------------------------------------------- model

type Scenario = Conservative | Central | Optimistic
let scenarios = [ Conservative; Central; Optimistic ]
let scenarioKey = function Conservative -> "conservative" | Central -> "central" | Optimistic -> "optimistic"
type Triple = Map<Scenario, float>
let triple (n: JsonNode) : Triple option =
    match n with
    | null -> None
    | n ->
        let vs = scenarios |> List.map (fun s -> s, num n (scenarioKey s))
        if vs |> List.forall (snd >> Option.isSome) then Some(vs |> List.map (fun (s, v) -> s, v.Value) |> Map.ofList) else None

type FailureMode = { Id: string; Title: string; Findings: string list; Minutes: Triple; Probability: Triple; Overlap: Triple }
type Prevention = { Mode: string; Q: Triple }
type Convention =
    { Id: string; Title: string; Level: string; Tier: int; Evidence: string list; Prevents: Prevention list
      Overhead: Triple; EnfKind: string; Check: string; Review: string; Tests: string list; Status: string; Safety: bool; Approval: string
      Mechanism: string; Confidence: string; Assumptions: string }
type Finding = { FId: string; Minutes: float option; Kind: string; Duplicate: string; Category: string; Rehearsals: string list }

let zero = scenarios |> List.map (fun s -> s, 0.0) |> Map.ofList
let parseMode (n: JsonNode) : FailureMode =
    { Id = str n "id"; Title = str n "title"; Findings = strs n "findings"
      Minutes = triple n["criticalPathMinutesIfOccurs"] |> Option.defaultValue zero
      Probability = triple n["probabilityOfOccurrence"] |> Option.defaultValue zero
      Overlap = triple n["expIndy002Overlap"] |> Option.defaultValue zero }
let parseConvention (n: JsonNode) : Convention =
    let e = n["enforcement"]
    { Id = str n "id"; Title = str n "title"; Level = str n "level"
      Tier = num n "tier" |> Option.map int |> Option.defaultValue 0
      Evidence = strs n "evidence"
      Prevents = arr n "prevents" |> List.map (fun p -> { Mode = str p "failureMode"; Q = triple p["prevention"] |> Option.defaultValue zero })
      Overhead = triple n["competitionOverheadMinutes"] |> Option.defaultValue zero
      EnfKind = str e "kind"; Check = str e "check"; Review = str e "reviewObligation"
      Tests = strs n "requiredTests"; Status = str n "adoptionStatus"
      Safety = (match n["correctnessOrSafety"] with null -> false | v -> v.GetValue<bool>())
      Approval = str n "ownerApprovalRequired"
      Mechanism = [ str e "mechanism"; str e "check"; str e "reviewObligation" ] |> List.filter ((<>) "") |> String.concat "; "
      Confidence = str n "confidence"; Assumptions = str n "keyAssumptions" }
let rehearsalIds (texts: string list) =
    texts |> List.collect (fun t -> Regex.Matches(t, @"R010-readiness|R0\d\d|post-R10|Forma") |> Seq.map (fun m -> m.Value) |> List.ofSeq)
let parseFinding (n: JsonNode) : Finding =
    { FId = str n "id"; Minutes = num n "measuredMinutes"; Kind = str n "measuredMinutesKind"; Duplicate = str n "duplicateOf"; Category = str n "category"
      Rehearsals = rehearsalIds (str n "rehearsal" :: strs n "recurredIn") |> List.distinct }

type Data = { Modes: FailureMode list; Conventions: Convention list; Findings: Finding list; RawCatalog: JsonNode; RawFindings: JsonNode }
let load (dir: string) : Data =
    let cat = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "CONVENTION-CATALOG.json")))
    let fd = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "FAILURE-DATA.json")))
    { Modes = arr cat "failureModes" |> List.map parseMode
      Conventions = arr cat "conventions" |> List.map parseConvention
      Findings = arr fd "findings" |> List.map parseFinding
      RawCatalog = cat; RawFindings = fd }

// ---------------------------------------------------------------- validation (pure)

let validCategories =
    set [ "requirements-ambiguity"; "contract-instability"; "fsharp-inconsistency"; "ui-implementation"; "persistence-serialization"
          "agent-coordination"; "testing-verification"; "ci-tooling"; "architecture"; "governance" ]
let criticalKinds = set [ "delay"; "unused-window" ]
let validKinds = set [ "delay"; "unused-window"; "cycle-time"; "elapsed-pre-competition" ]
let levels = set [ "MANDATORY"; "PREFERRED"; "DISCRETIONARY" ]

let ascending (t: Triple) = t[Conservative] <= t[Central] && t[Central] <= t[Optimistic]
let descending (t: Triple) = t[Conservative] >= t[Central] && t[Central] >= t[Optimistic]
let inUnit (t: Triple) = t |> Map.forall (fun _ v -> v >= 0.0 && v <= 1.0)
let duplicates xs = xs |> List.countBy id |> List.filter (snd >> (<) 1) |> List.map fst

let validate (d: Data) : string list =
    let fids = d.Findings |> List.map (fun f -> f.FId) |> Set.ofList
    let mids = d.Modes |> List.map (fun m -> m.Id) |> Set.ofList
    let findingRules =
        [ for id in duplicates (d.Findings |> List.map (fun f -> f.FId)) -> $"V01 duplicate finding id {id}"
          for f in d.Findings do
            if not (Regex.IsMatch(f.FId, @"^HF-\d{3}$")) then yield $"V01 finding id {f.FId} is not HF-nnn"
            if not (validCategories.Contains f.Category) then yield $"V02 {f.FId}: unknown category {f.Category}"
            match f.Minutes, f.Kind with
            | Some _, k when not (validKinds.Contains k) -> yield $"V03 {f.FId}: measured minutes without a valid measuredMinutesKind"
            | None, k when k <> "" -> yield $"V03 {f.FId}: measuredMinutesKind without measured minutes"
            | _ -> ()
            if f.Duplicate <> "" then
                if not (fids.Contains f.Duplicate) then yield $"V04 {f.FId}: duplicateOf {f.Duplicate} does not exist"
                if f.Minutes.IsSome then yield $"V04 {f.FId}: a duplicate carries measured minutes (double count)" ]
    let modeRules =
        [ for id in duplicates (d.Modes |> List.map (fun m -> m.Id)) -> $"V01 duplicate failure mode {id}"
          for m in d.Modes do
            for f in m.Findings do if not (fids.Contains f) then yield $"V05 {m.Id}: finding {f} does not exist"
            if not (ascending m.Minutes) then yield $"V07 {m.Id}: criticalPathMinutesIfOccurs must rise conservative<=central<=optimistic"
            if not (ascending m.Probability && inUnit m.Probability) then yield $"V07 {m.Id}: probability must be in [0,1] and rise across scenarios"
            if not (descending m.Overlap && inUnit m.Overlap) then yield $"V07 {m.Id}: EXP-INDY-002 overlap must be in [0,1] and fall across scenarios"
          // a finding's measured cost may sit in only one failure mode
          let owners = d.Modes |> List.collect (fun m -> m.Findings |> List.map (fun f -> f, m.Id))
          for (f, ms) in owners |> List.groupBy fst do
            let costed = d.Findings |> List.exists (fun x -> x.FId = f && x.Minutes.IsSome)
            if costed && ms.Length > 1 then yield $"V08 {f}: measured minutes counted in {ms.Length} failure modes" ]
    let conventionRules =
        [ for id in duplicates (d.Conventions |> List.map (fun c -> c.Id)) -> $"V01 duplicate convention {id}"
          for c in d.Conventions do
            if not (Regex.IsMatch(c.Id, @"^CV-\d{2}$")) then yield $"V01 convention id {c.Id} is not CV-nn"
            if not (levels.Contains c.Level) then yield $"V09 {c.Id}: level {c.Level} is not MANDATORY/PREFERRED/DISCRETIONARY"
            if c.Tier < 1 || c.Tier > 3 then yield $"V09 {c.Id}: tier must be 1, 2 or 3"
            if c.Evidence.IsEmpty then yield $"V10 {c.Id}: no historical evidence"
            for e in c.Evidence do if not (fids.Contains e) then yield $"V10 {c.Id}: evidence {e} does not exist"
            for p in c.Prevents do
                if not (mids.Contains p.Mode) then yield $"V11 {c.Id}: failure mode {p.Mode} does not exist"
                if not (ascending p.Q && inUnit p.Q) then yield $"V07 {c.Id}/{p.Mode}: prevention must be in [0,1] and rise across scenarios"
            if not (descending c.Overhead) then yield $"V07 {c.Id}: overhead must fall conservative>=central>=optimistic"
            if c.Level = "MANDATORY" then
                if c.EnfKind = "" || c.EnfKind = "none" then yield $"V12 {c.Id}: MANDATORY without an enforcement method"
                if c.EnfKind = "review" && c.Review = "" then yield $"V12 {c.Id}: review enforcement without a precise review obligation"
                if c.Tests.IsEmpty then yield $"V12 {c.Id}: MANDATORY without required tests"
            if c.Tier = 1 && c.EnfKind <> "review" && c.Check = "" && c.EnfKind <> "existing" then
                yield $"V13 {c.Id}: tier 1 without an exact check"
            if c.Tier = 1 && c.EnfKind = "none" then yield $"V13 {c.Id}: tier 1 without an enforcement mechanism"
            if c.Status.StartsWith "ADOPTED" then yield $"V14 {c.Id}: adoption is the owner's to record, not the catalog's" ]
    findingRules @ modeRules @ conventionRules

// ---------------------------------------------------------------- forecast (pure)

/// Residual critical-path minutes a failure mode is expected to cost in one 420-minute build, net of EXP-INDY-002.
let residual (s: Scenario) (m: FailureMode) = m.Probability[s] * m.Minutes[s] * (1.0 - m.Overlap[s])

/// Net minutes saved by adopting a set of conventions together: per failure mode, prevention combines as
/// 1 - prod(1 - q), so overlapping conventions never prevent the same minute twice.
let netSavings (modes: FailureMode list) (s: Scenario) (set: Convention list) : float =
    let prevented =
        modes
        |> List.sumBy (fun m ->
            let miss = set |> List.collect (fun c -> c.Prevents |> List.filter (fun p -> p.Mode = m.Id)) |> List.fold (fun acc p -> acc * (1.0 - p.Q[s])) 1.0
            residual s m * (1.0 - miss))
    prevented - (set |> List.sumBy (fun c -> c.Overhead[s]))

type Ranked = { Rank: int; Convention: Convention; Standalone: Map<Scenario, float>; Cumulative: Map<Scenario, float> }
type Forecast =
    { Ranked: Ranked list
      /// highest cumulative net along the ranked order, and the rank where it occurs
      Peak: Map<Scenario, float * int>
      EightyPct: Map<Scenario, int option>
      Pool: Map<Scenario, float>
      /// net secured by conventions an existing check already enforces
      Secured: Map<Scenario, float>
      /// added net of the not-yet-enforced conventions with positive central net, given the secured set
      Incremental: Map<Scenario, float> }

let forecast (d: Data) : Forecast =
    let stand c = scenarios |> List.map (fun s -> s, netSavings d.Modes s [ c ]) |> Map.ofList
    let order =
        d.Conventions
        |> List.map (fun c -> c, stand c)
        |> List.sortBy (fun (c, st) -> -st[Central], c.Id)
    let prefixes = order |> List.mapi (fun i _ -> order |> List.take (i + 1) |> List.map fst)
    let ranked =
        List.zip order prefixes
        |> List.mapi (fun i ((c, st), pre) ->
            { Rank = i + 1; Convention = c; Standalone = st
              Cumulative = scenarios |> List.map (fun s -> s, netSavings d.Modes s pre) |> Map.ofList })
    let pool = scenarios |> List.map (fun s -> s, d.Modes |> List.sumBy (residual s)) |> Map.ofList
    let peak =
        scenarios
        |> List.map (fun s -> s, ranked |> List.fold (fun (best, at) r -> if r.Cumulative[s] > best then r.Cumulative[s], r.Rank else best, at) (0.0, 0))
        |> Map.ofList
    let eighty =
        scenarios
        |> List.map (fun s ->
            let best, _ = peak[s]
            s, (if best <= 0.0 then None else ranked |> List.tryFind (fun r -> r.Cumulative[s] >= 0.8 * best) |> Option.map (fun r -> r.Rank)))
        |> Map.ofList
    let existing = d.Conventions |> List.filter (fun c -> c.Status.StartsWith "ENFORCED-EXISTING")
    let proposed = order |> List.filter (fun (c, st) -> st[Central] > 0.0 && not (c.Status.StartsWith "ENFORCED-EXISTING")) |> List.map fst
    let secured = scenarios |> List.map (fun s -> s, netSavings d.Modes s existing) |> Map.ofList
    let incremental = scenarios |> List.map (fun s -> s, netSavings d.Modes s (existing @ proposed) - secured[s]) |> Map.ofList
    { Ranked = ranked; Peak = peak; EightyPct = eighty; Pool = pool; Secured = secured; Incremental = incremental }

let f1 (v: float) = v.ToString("0.0", Globalization.CultureInfo.InvariantCulture)
let trip (m: Map<Scenario, float>) = $"{f1 m[Conservative]} / {f1 m[Central]} / {f1 m[Optimistic]}"
let f2 (v: float) = v.ToString("0.00", Globalization.CultureInfo.InvariantCulture)
let tripP (m: Map<Scenario, float>) = $"{f2 m[Conservative]} / {f2 m[Central]} / {f2 m[Optimistic]}"

let topTable (d: Data) (fc: Forecast) =
    let measured c =
        let costed =
            d.Modes |> List.filter (fun m -> c.Prevents |> List.exists (fun p -> p.Mode = m.Id))
            |> List.collect (fun m -> m.Findings) |> List.distinct
            |> List.choose (fun f -> d.Findings |> List.tryFind (fun x -> x.FId = f && criticalKinds.Contains x.Kind))
        match costed with
        | [] -> "none stated"
        | xs -> xs |> List.map (fun x -> $"{x.FId} {f1 x.Minutes.Value}") |> String.concat "; "
    [ yield "| Rank | Convention | Level | Measured historical critical-path minutes (findings) | Standalone net, cons / central / opt | Overhead, cons / central / opt | Cumulative net in rank order, cons / central / opt | Safety rule |"
      yield "|---:|---|---|---|---|---|---|---|"
      for r in fc.Ranked |> List.truncate 10 ->
          let safety = if r.Convention.Safety then "yes" else ""
          $"| {r.Rank} | {r.Convention.Id} {r.Convention.Title} | {r.Convention.Level} | {measured r.Convention} | {trip r.Standalone} | {trip r.Convention.Overhead} | {trip r.Cumulative} | {safety} |" ]
    |> String.concat "\n"

let fullTable (fc: Forecast) =
    [ yield "| Rank | Convention | Tier | Status | Standalone net, cons / central / opt | Cumulative net, cons / central / opt |"
      yield "|---:|---|---:|---|---|---|"
      for r in fc.Ranked ->
          $"| {r.Rank} | {r.Convention.Id} {r.Convention.Title} | {r.Convention.Tier} | {r.Convention.Status} | {trip r.Standalone} | {trip r.Cumulative} |" ]
    |> String.concat "\n"

let topDetail (d: Data) (fc: Forecast) =
    let findingsOf (c: Convention) =
        d.Modes |> List.filter (fun m -> c.Prevents |> List.exists (fun p -> p.Mode = m.Id)) |> List.collect (fun m -> m.Findings) |> List.distinct
        |> List.choose (fun f -> d.Findings |> List.tryFind (fun x -> x.FId = f && x.Duplicate = ""))
    let gross (r: Ranked) = r.Standalone |> Map.map (fun s v -> v + r.Convention.Overhead[s])
    [ for r in fc.Ranked |> List.truncate 10 do
        let c = r.Convention
        let fs = findingsOf c
        let rehearsals = fs |> List.collect (fun f -> f.Rehearsals) |> List.distinct |> List.sort
        let measured =
            match fs |> List.filter (fun f -> criticalKinds.Contains f.Kind) with
            | [] -> "none stated in any source (unknown)"
            | xs -> xs |> List.map (fun f -> $"{f.FId}: {f1 f.Minutes.Value} min ({f.Kind})") |> String.concat "; "
        let other =
            fs |> List.filter (fun f -> f.Minutes.IsSome && not (criticalKinds.Contains f.Kind))
            |> List.map (fun f -> $"{f.FId}: {f1 f.Minutes.Value} min ({f.Kind}, not a competition loss)") |> String.concat "; "
        let freq = c.Prevents |> List.map (fun p -> d.Modes |> List.find (fun m -> m.Id = p.Mode)) |> List.map (fun m -> $"{m.Id} {tripP m.Probability}") |> String.concat "; "
        let pre = if c.Status.StartsWith "ENFORCED-EXISTING" then "yes (already enforced; keep it on)" elif c.Approval <> "" then $"yes, after owner approval ({c.Approval})" else "yes"
        let extra = if other = "" then "" else $" Other measured: {other}."
        let rehearsalText = String.concat ", " rehearsals
        let evidenceText = fs |> List.map (fun f -> f.FId) |> String.concat ", "
        let conventionEvidence = String.concat ", " c.Evidence
        yield $"### {r.Rank}. {c.Id} {c.Title}"
        yield ""
        yield $"- Level: {c.Level}; tier {c.Tier}; status {c.Status}" + (if c.Safety then "; safety/correctness rule (enforced regardless of time value)" else "")
        yield $"- Rehearsals where the problem occurred: {rehearsalText}"
        yield $"- Evidence: {evidenceText} (FAILURE-DATA.json); convention evidence {conventionEvidence}"
        yield $"- Observed repair or delay minutes (measured): {measured}.{extra}"
        yield $"- Estimated recurrence probability per build, cons / central / opt (modeled): {freq}"
        yield $"- Estimated minutes prevented per build, standalone, cons / central / opt (modeled): {trip (gross r)}"
        yield $"- Enforcement and implementation overhead on the critical path, cons / central / opt: {trip c.Overhead}"
        yield $"- Net critical-path minutes saved, standalone, cons / central / opt: {trip r.Standalone}; cumulative in rank order: {trip r.Cumulative}"
        yield $"- Confidence: {c.Confidence}. Key assumptions: {c.Assumptions}"
        yield $"- Enforcement mechanism: {c.Mechanism}"
        yield $"- Implementable before the competition: {pre}"
        yield "" ]
    |> String.concat "\n"

let poolTable (d: Data) =
    let r s m = residual s m
    [ yield "| Failure mode | Occurs, cons / central / opt | Minutes if it occurs | EXP-INDY-002 overlap | Residual expected minutes per build | Share of central pool |"
      yield "|---|---|---|---|---|---:|"
      let total = d.Modes |> List.sumBy (r Central)
      for m in d.Modes |> List.sortBy (fun m -> -(r Central m), m.Id) ->
          let res = scenarios |> List.map (fun s -> s, r s m) |> Map.ofList
          let share = if total = 0.0 then 0.0 else 100.0 * res[Central] / total
          $"| {m.Id} {m.Title} | {tripP m.Probability} | {trip m.Minutes} | {tripP m.Overlap} | {trip res} | {f1 share}%% |" ]
    |> String.concat "\n"

/// Central standalone net when every occurrence probability is scaled (capped at 1).
let sensitivityTable (d: Data) =
    let scaled k = { d with Modes = d.Modes |> List.map (fun m -> { m with Probability = m.Probability |> Map.map (fun _ p -> min 1.0 (p * k)) }) }
    let factors = [ 0.5; 1.0; 1.5 ]
    let nets = factors |> List.map (fun k -> let dd = scaled k in k, dd.Conventions |> List.map (fun c -> c.Id, netSavings dd.Modes Central [ c ]) |> Map.ofList)
    let order = d.Conventions |> List.sortBy (fun c -> -(netSavings d.Modes Central [ c ]), c.Id)
    [ yield "| Convention | Tier | Central net, probabilities x0.5 | x1.0 | x1.5 |"
      yield "|---|---:|---:|---:|---:|"
      for c in order ->
          let v k = nets |> List.find (fst >> (=) k) |> snd |> Map.find c.Id |> f1
          $"| {c.Id} {c.Title} | {c.Tier} | {v 0.5} | {v 1.0} | {v 1.5} |" ]
    |> String.concat "\n"

let summary (fc: Forecast) =
    let k s = match fc.EightyPct[s] with Some n -> $"top {n}" | None -> "not reached (no positive net)"
    let set s = match fc.EightyPct[s] with Some n -> fc.Ranked |> List.take n |> List.map (fun r -> r.Convention.Id) |> String.concat ", " | None -> "-"
    let pk s = let v, r = fc.Peak[s] in $"{f1 v} at rank {r}"
    [ $"- Residual preventable pool (expected critical-path minutes per build without these conventions, net of EXP-INDY-002 overlap), cons / central / opt: {trip fc.Pool}"
      $"- Peak cumulative net along the ranked order (overlap-adjusted, overhead deducted): conservative {pk Conservative}; central {pk Central}; optimistic {pk Optimistic}"
      $"- Smallest ranked prefix reaching 80%% of the peak: conservative {k Conservative} ({set Conservative}); central {k Central} ({set Central}); optimistic {k Optimistic} ({set Optimistic})"
      $"- Secured by conventions an existing check already enforces (kept only while those checks stay on), cons / central / opt: {trip fc.Secured}"
      $"- Incremental from not-yet-enforced conventions with positive central net, given the secured set, cons / central / opt: {trip fc.Incremental}" ]
    |> String.concat "\n"

let forecastJson (fc: Forecast) =
    let tri (m: Map<Scenario, float>) = JsonObject(dict [ for s in scenarios -> scenarioKey s, JsonValue.Create(Math.Round(m[s], 2)) :> JsonNode ])
    let o = JsonObject()
    o["schema"] <- JsonValue.Create "indy-init.implementation-conventions.forecast/1"
    o["generatedBy"] <- JsonValue.Create "tools/conventions/conventions.fsx forecast"
    o["pool"] <- tri fc.Pool
    o["peakCumulativeNet"] <- tri (fc.Peak |> Map.map (fun _ (v, _) -> v))
    o["peakRank"] <- JsonObject(dict [ for s in scenarios -> scenarioKey s, JsonValue.Create(snd fc.Peak[s]) :> JsonNode ])
    o["securedByExistingChecks"] <- tri fc.Secured
    o["incrementalFromNewConventions"] <- tri fc.Incremental
    o["eightyPercentPrefix"] <- JsonObject(dict [ for s in scenarios -> scenarioKey s, (match fc.EightyPct[s] with Some n -> JsonValue.Create n :> JsonNode | None -> null) ])
    o["ranked"] <- JsonArray([| for r in fc.Ranked ->
                                  let x = JsonObject()
                                  x["rank"] <- JsonValue.Create r.Rank
                                  x["id"] <- JsonValue.Create r.Convention.Id
                                  x["standaloneNet"] <- tri r.Standalone
                                  x["cumulativeNet"] <- tri r.Cumulative
                                  x :> JsonNode |])
    o.ToJsonString(Text.Json.JsonSerializerOptions(WriteIndented = true))

// ---------------------------------------------------------------- generated doc blocks

let blockPattern name = Regex($@"(<!-- GENERATED:{name} BEGIN[^>]*-->\n)(.*?)(\n<!-- GENERATED:{name} END -->)", RegexOptions.Singleline)
let blocks (d: Data) (fc: Forecast) = [ "top10", topTable d fc; "summary", summary fc; "ranking", fullTable fc; "top10detail", topDetail d fc; "pool", poolTable d; "sensitivity", sensitivityTable d ]
let renderText (generated: (string * string) list) (text: string) =
    generated |> List.fold (fun (t: string) (name, body) -> (blockPattern name).Replace(t, (fun (m: Match) -> m.Groups[1].Value + body + m.Groups[3].Value))) text
let staleBlocks (generated: (string * string) list) (file: string) (text: string) =
    [ for (name, body) in generated do
        for m in (blockPattern name).Matches(text) do
            if m.Groups[2].Value <> body then yield $"V15 {file}: generated block '{name}' is stale (run: conventions.fsx render)" ]

// ---------------------------------------------------------------- workspace checks (pure over a file map)

type Workspace = Map<string, string> // relative path (forward slashes) -> content
let readWorkspace (root: string) : Workspace =
    let skip (p: string) = [ "/.git/"; "/node_modules/"; "/bin/"; "/obj/" ] |> List.exists (fun s -> ("/" + p).Contains s)
    Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
    |> Seq.map (fun f -> Path.GetRelativePath(root, f).Replace('\\', '/'))
    |> Seq.filter (fun p -> not (skip p))
    |> Seq.filter (fun p -> Regex.IsMatch(p, @"\.(fsproj|props|targets|json|ya?ml|js|mjs|ts|fs|fsx|css|html)$"))
    |> Seq.map (fun p -> p, File.ReadAllText(Path.Combine(root, p)))
    |> Map.ofSeq

/// CK-01 (CV-10): warnings stay errors and the drift warnings are never suppressed.
let ck01 (w: Workspace) =
    let projects = w |> Map.filter (fun p _ -> p.EndsWith ".fsproj" || p.EndsWith "Directory.Build.props")
    let on (t: string) = Regex.IsMatch(t, @"<TreatWarningsAsErrors>\s*true\s*</TreatWarningsAsErrors>", RegexOptions.IgnoreCase)
    let off (t: string) = Regex.IsMatch(t, @"<TreatWarningsAsErrors>\s*false\s*</TreatWarningsAsErrors>", RegexOptions.IgnoreCase)
    let suppressed (t: string) =
        Regex.Matches(t, @"<NoWarn>([^<]*)</NoWarn>") |> Seq.collect (fun m -> m.Groups[1].Value.Split([| ';'; ','; ' ' |], StringSplitOptions.RemoveEmptyEntries))
        |> Seq.map (fun c -> c.Trim().ToUpperInvariant().Replace("FS", "").TrimStart('0')) |> Seq.filter (fun c -> List.contains c [ "25"; "26"; "49" ]) |> List.ofSeq
    let rootOn = w |> Map.tryFind "Directory.Build.props" |> Option.map on |> Option.defaultValue false
    [ for KeyValue(p, t) in projects do
        if off t then yield $"CK-01 {p}: TreatWarningsAsErrors is false"
        for c in suppressed t do
            let code = c.PadLeft(4, '0')
            yield $"CK-01 {p}: NoWarn suppresses FS{code}"
        if p.EndsWith ".fsproj" && not rootOn && not (on t) then yield $"CK-01 {p}: TreatWarningsAsErrors is not set (no root Directory.Build.props sets it)" ]

/// CK-02 (CV-05): a localhost/127.0.0.1 port literal may appear in only one file.
let ck02 (w: Workspace) =
    let lit = Regex(@"(?:localhost|127\.0\.0\.1):(\d{2,5})")
    w
    |> Map.toList
    |> List.collect (fun (p, t) -> lit.Matches(t) |> Seq.map (fun m -> m.Groups[1].Value, p) |> List.ofSeq)
    |> List.distinct
    |> List.groupBy fst
    |> List.filter (fun (_, ps) -> ps.Length > 1)
    |> List.map (fun (port, ps) ->
        let files = ps |> List.map snd |> String.concat ", "
        $"CK-02 port {port} is written in {ps.Length} files ({files}); keep it in one configuration file")

/// CK-03 (CV-07): run steps execute under an explicit shell (bash => -eo pipefail).
let ck03 (w: Workspace) =
    let wf = w |> Map.filter (fun p _ -> p.StartsWith ".github/workflows/" && (p.EndsWith ".yml" || p.EndsWith ".yaml"))
    [ for KeyValue(p, t) in wf do
        let lines = t.Split('\n')
        let hasDefault = Regex.IsMatch(t, @"(?m)^defaults:\s*\n\s+run:\s*\n(\s+[a-z-]+:.*\n)*?\s+shell:\s*\S+")
        if not hasDefault then
            // each "- run:" or "run:" step must have a sibling "shell:" within the same step block
            let steps =
                lines |> Array.mapi (fun i l -> i, l) |> Array.filter (fun (_, l) -> Regex.IsMatch(l, @"^\s*-?\s*run:\s*"))
            for (i, l) in steps do
                let indent = l.Length - l.TrimStart([| ' '; '-' |]).Length
                let block =
                    Array.append (lines |> Array.skip (i + 1) |> Array.takeWhile (fun x -> x.Trim() = "" || (x.Length - x.TrimStart().Length) >= indent && not (x.TrimStart().StartsWith "- ")))
                                 (lines |> Array.take i |> Array.rev |> Array.takeWhile (fun x -> not (x.TrimStart().StartsWith "- ") && (x.Length - x.TrimStart().Length) >= indent))
                let stepStart = lines |> Array.take (i + 1) |> Array.rev |> Array.tryFind (fun x -> x.TrimStart().StartsWith "- ") |> Option.defaultValue ""
                let all = Array.concat [ block; [| stepStart; l |] ]
                let declared = all |> Array.exists (fun x -> Regex.IsMatch(x, @"^\s*-?\s*shell:\s*\S+"))
                let inlinePipefail = all |> Array.exists (fun x -> Regex.IsMatch(x, @"^\s*(run:\s*)?set\s+-[a-z]*o\s+pipefail\b"))
                if not (declared || inlinePipefail) then
                    yield $"CK-03 {p}:{i + 1}: run step without a declared shell and no defaults.run.shell (implicit bash -e has no pipefail)" ]

/// CK-04 (CV-17): Forma (@echelon-foundry/design-system) is at or above the minimum version.
let ck04 (minimum: Version) (w: Workspace) =
    let ver = Regex(@"""@echelon-foundry/design-system""\s*:\s*""[^""]*?(\d+\.\d+\.\d+)[^""]*""")
    [ for KeyValue(p, t) in w |> Map.filter (fun p _ -> p.EndsWith "package.json") do
        for m in ver.Matches(t) do
            let v = Version(m.Groups[1].Value)
            if v < minimum then yield $"CK-04 {p}: Forma {v} is below {minimum} (containment defaults ship in 0.13.0)" ]

let check (forma: Version) (w: Workspace) = ck01 w @ ck02 w @ ck03 w @ ck04 forma w

// ---------------------------------------------------------------- self-tests

let selfTest () =
    let good: Workspace =
        Map.ofList
            [ "Directory.Build.props", "<Project><PropertyGroup><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>"
              "src/A/A.fsproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><NoWarn>FS3370</NoWarn></PropertyGroup></Project>"
              "tests/config.json", "{ \"site\": \"http://localhost:4821\" }"
              "tests/a.spec.js", "import cfg from './config.json'"
              ".github/workflows/ci.yml", "on: push\ndefaults:\n  run:\n    shell: bash\njobs:\n  b:\n    steps:\n      - run: dotnet test | tee out.txt\n"
              ".github/workflows/x.yml", "on: push\njobs:\n  b:\n    steps:\n      - name: t\n        shell: bash\n        run: |\n          dotnet test\n"
              "web/package.json", "{ \"dependencies\": { \"@echelon-foundry/design-system\": \"https://x/releases/download/v0.13.0/echelon-foundry-design-system-0.13.0.tgz\" } }" ]
    let m013 = Version "0.13.0"
    let neg name (mutate: Workspace -> Workspace) (rule: string) =
        let f = check m013 (mutate good)
        name, (f |> List.exists (fun x -> x.StartsWith rule)), f
    let cases =
        [ "positive control passes", (check m013 good |> List.isEmpty), check m013 good
          neg "TreatWarningsAsErrors false fails" (Map.add "src/A/A.fsproj" "<Project><PropertyGroup><TreatWarningsAsErrors>false</TreatWarningsAsErrors></PropertyGroup></Project>") "CK-01"
          neg "NoWarn FS0025 fails" (Map.add "src/A/A.fsproj" "<Project><PropertyGroup><NoWarn>FS0025;3370</NoWarn></PropertyGroup></Project>") "CK-01"
          neg "NoWarn 49 fails" (Map.add "src/A/A.fsproj" "<Project><PropertyGroup><NoWarn>49</NoWarn></PropertyGroup></Project>") "CK-01"
          neg "no root setting and project unset fails" (Map.remove "Directory.Build.props") "CK-01"
          neg "port literal in two files fails" (Map.add "tests/b.spec.js" "await page.goto('http://localhost:4821/')") "CK-02"
          neg "run step without shell fails" (Map.add ".github/workflows/y.yml" "on: push\njobs:\n  b:\n    steps:\n      - name: t\n        run: dotnet test | tee o\n") "CK-03"
          "a step that sets -euo pipefail itself passes",
            (check m013 (Map.add ".github/workflows/z.yml" "on: push\njobs:\n  b:\n    steps:\n      - name: t\n        run: |\n          set -euo pipefail\n          dotnet test | tee o\n" good) |> List.isEmpty), []
          neg "Forma 0.12.0 fails" (Map.add "web/package.json" "{ \"dependencies\": { \"@echelon-foundry/design-system\": \"0.12.0\" } }") "CK-04" ]
    // forecast invariants on a synthetic catalog: overlap never exceeds the pool; overhead is deducted
    let mode id mins p = { Id = id; Title = id; Findings = []; Minutes = scenarios |> List.map (fun s -> s, mins) |> Map.ofList; Probability = scenarios |> List.map (fun s -> s, p) |> Map.ofList; Overlap = zero }
    let conv id q oh modes = { Id = id; Title = id; Level = "PREFERRED"; Tier = 2; Evidence = []; Prevents = modes |> List.map (fun m -> { Mode = m; Q = scenarios |> List.map (fun s -> s, q) |> Map.ofList }); Overhead = scenarios |> List.map (fun s -> s, oh) |> Map.ofList; EnfKind = "review"; Check = ""; Review = "r"; Tests = []; Status = "PROPOSED"; Safety = false; Approval = ""; Mechanism = ""; Confidence = ""; Assumptions = "" }
    let ms = [ mode "FM-A" 100.0 1.0 ]
    let a, b = conv "CV-A" 0.6 0.0 [ "FM-A" ], conv "CV-B" 0.5 2.0 [ "FM-A" ]
    let combined = netSavings ms Central [ a; b ]
    let math =
        [ "two overlapping conventions combine as 1-(1-q1)(1-q2), minus overhead", abs (combined - (100.0 * (1.0 - 0.4 * 0.5) - 2.0)) < 1e-9, [ f1 combined ]
          "combined never exceeds the sum of standalone", combined <= netSavings ms Central [ a ] + netSavings ms Central [ b ], [] ]
    // catalog validation controls: each mutation of a valid synthetic catalog must be reported
    let tri a b c = Map.ofList [ Conservative, a; Central, b; Optimistic, c ]
    let okMode = { Id = "FM-01"; Title = "m"; Findings = [ "HF-001" ]; Minutes = tri 1.0 2.0 3.0; Probability = tri 0.1 0.2 0.3; Overlap = tri 0.2 0.1 0.0 }
    let okConv =
        { conv "CV-01" 0.5 1.0 [ "FM-01" ] with
            Level = "MANDATORY"; Tier = 1; Evidence = [ "HF-001" ]; EnfKind = "new"; Check = "c"; Tests = [ "t" ]
            Prevents = [ { Mode = "FM-01"; Q = tri 0.3 0.5 0.7 } ]; Overhead = tri 2.0 1.0 0.5 }
    let okFinding = { FId = "HF-001"; Minutes = Some 10.0; Kind = "delay"; Duplicate = ""; Category = "ci-tooling"; Rehearsals = [ "R005" ] }
    let okData = { Modes = [ okMode ]; Conventions = [ okConv ]; Findings = [ okFinding ]; RawCatalog = null; RawFindings = null }
    let vneg name (dd: Data) (rule: string) = let f = validate dd in name, (f |> List.exists (fun x -> x.StartsWith rule)), f
    let validation =
        [ "valid synthetic catalog passes", (validate okData |> List.isEmpty), validate okData
          vneg "evidence that does not exist fails" { okData with Conventions = [ { okConv with Evidence = [ "HF-999" ] } ] } "V10"
          vneg "MANDATORY without enforcement fails" { okData with Conventions = [ { okConv with EnfKind = "none" } ] } "V12"
          vneg "review enforcement without an obligation fails" { okData with Conventions = [ { okConv with EnfKind = "review"; Review = "" } ] } "V12"
          vneg "a duplicate carrying minutes fails" { okData with Findings = [ okFinding; { okFinding with FId = "HF-002"; Duplicate = "HF-001" } ] } "V04"
          vneg "measured minutes in two failure modes fails" { okData with Modes = [ okMode; { okMode with Id = "FM-02" } ] } "V08"
          vneg "unordered scenarios fail" { okData with Modes = [ { okMode with Probability = tri 0.5 0.2 0.3 } ] } "V07"
          vneg "a catalog claiming adoption fails" { okData with Conventions = [ { okConv with Status = "ADOPTED" } ] } "V14" ]
    let results = cases @ math @ validation
    results |> List.iter (fun (n, ok, f) -> printfn "%s %s%s" (if ok then "PASS" else "FAIL") n (if ok then "" else "\n    " + String.concat "\n    " f))
    if results |> List.forall (fun (_, ok, _) -> ok) then 0 else 3

// ---------------------------------------------------------------- dispatch (the only effects)

let report (findings: string list) =
    findings |> List.iter (printfn "%s")
    if findings.IsEmpty then (printfn "PASS"; 0) else (printfn "FAIL (%d)" findings.Length; 3)

let docsWithBlocks = [ "TOP-10-TIME-SAVINGS.md"; "IMPACT-FORECAST.md"; "FINAL-REPORT.md" ]

let exitCode =
    let dir = opt "--dir" |> Option.defaultValue defaultDir
    match argv |> List.tryHead with
    | Some "validate" ->
        let d = load dir
        let fc = forecast d
        let stale =
            docsWithBlocks |> List.filter (fun f -> File.Exists(Path.Combine(dir, f)))
            |> List.collect (fun f -> staleBlocks (blocks d fc) f (File.ReadAllText(Path.Combine(dir, f))))
        // the human-readable catalog must list every convention with its JSON level, tier and status
        let catalogMd = Path.Combine(dir, "CONVENTION-CATALOG.md")
        let mdDrift =
            if not (File.Exists catalogMd) then [ "V16 CONVENTION-CATALOG.md is missing" ] else
            let md = File.ReadAllText catalogMd
            d.Conventions |> List.choose (fun c ->
                let row = $"| {c.Id} | {c.Title} | {c.Level} | {c.Tier} | {c.Status} |"
                if md.Contains row then None else Some $"V16 CONVENTION-CATALOG.md: the row for {c.Id} does not match the JSON (id, title, level, tier, status)")
        report (validate d @ stale @ mdDrift)
    | Some "forecast" ->
        let d = load dir
        let fc = forecast d
        if flag "--json" then printfn "%s" (forecastJson fc) else printfn "%s\n\n%s\n\n%s" (topTable d fc) (summary fc) (fullTable fc)
        0
    | Some "render" ->
        let d = load dir
        let fc = forecast d
        docsWithBlocks
        |> List.map (fun f -> Path.Combine(dir, f))
        |> List.filter File.Exists
        |> List.iter (fun p -> File.WriteAllText(p, renderText (blocks d fc) (File.ReadAllText p)))
        File.WriteAllText(Path.Combine(dir, "FORECAST.json"), forecastJson fc + "\n")
        0
    | Some "check" ->
        match opt "--workspace" with
        | None -> eprintfn "check needs --workspace PATH"; 2
        | Some w -> report (check (opt "--forma-min" |> Option.defaultValue "0.13.0" |> Version) (readWorkspace w))
    | Some "test" -> selfTest ()
    | _ -> eprintfn "usage: conventions.fsx -- validate|forecast|render|check|test (see header)"; 2

exit exitCode
