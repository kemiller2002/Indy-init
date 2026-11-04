// r9-replay.fsx: Rehearsal 009 as a read-only negative control for the Rehearsal 010 process
// (docs/rehearsal-010-readiness/R9-ADVERSARIAL-REPLAY.md). F#, as OD-INDY-14 requires.
//
//   dotnet fsi tools/r10/r9-replay.fsx -- --indy-init SRC --r9 R9-CLONE --work NEW-DIR --evidence DIR [--suites true]
//
// Works on a copy of the Rehearsal 009 clone; the evidence repository is never written. Each check records the
// command, its exit code and the decision, and whether that decision is the expected one. Rehearsal 009's
// historical outcome is not re-judged or repaired: the replay asks only what the corrected process would decide.

open System
open System.Diagnostics
open System.IO
open System.Text.Json

let argv = fsi.CommandLineArgs |> Array.toList |> List.tail
let opt name = argv |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let req name = opt name |> Option.defaultWith (fun () -> failwithf "missing %s" name)
let src = Path.GetFullPath(req "--indy-init")
let work = Directory.CreateDirectory(Path.GetFullPath(req "--work")).FullName
let evidence = Directory.CreateDirectory(Path.GetFullPath(req "--evidence")).FullName
let runSuites = opt "--suites" = Some "true"

let run (exe: string) (args: string list) (cwd: string) =
    let info = ProcessStartInfo(exe, WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true)
    args |> List.iter info.ArgumentList.Add
    use p = Process.Start info
    let o, e = p.StandardOutput.ReadToEndAsync(), p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    p.ExitCode, o.Result + e.Result

// A copy of the clone, history included, so builds and checks never touch the evidence repository.
let r9 = Path.Combine(work, "r9")
let code, out = run "git" [ "clone"; "-q"; Path.GetFullPath(req "--r9"); r9 ] work
if code <> 0 then failwithf "clone: %s" out
let r9Head = (snd (run "git" [ "-C"; r9; "rev-parse"; "HEAD" ] r9)).Trim()

let build = run "dotnet" [ "build"; Path.Combine(src, "tools", "requirements-compiler", "src", "RequirementsCompiler.fsproj"); "-c"; "Release"; "-o"; Path.Combine(work, "rc"); "-v"; "q"; "-nologo" ] work
if fst build <> 0 then failwithf "build: %s" (snd build)
let rc args = run "dotnet" (Path.Combine(work, "rc", "requirements-compiler.dll") :: args) work
let r9fsx args cwd = run "dotnet" ([ "fsi"; Path.Combine(src, "tools", "r9", "r9.fsx"); "--" ] @ args) cwd
let write (name: string) (text: string) = let p = Path.Combine(work, name) in Directory.CreateDirectory(Path.GetDirectoryName p) |> ignore; File.WriteAllText(p, text); p
let json (o: obj) = JsonSerializer.Serialize(o, JsonSerializerOptions(WriteIndented = true))
let at (rel: string) = Path.Combine(r9, rel)

let manifest = Path.Combine(src, "docs", "rehearsal-010-readiness", "scope-manifest.json")
let scopeSha = (JsonDocument.Parse(File.ReadAllText manifest)).RootElement.GetProperty("scopeSha256").GetString()
let events = Path.Combine(src, "docs", "rehearsal-010-readiness", "evidence", "stop-decision", "r9-events.json")
let checkpoint = write "checkpoint.json" (json (dict [ "schema", box "indy-init.scope-checkpoint/1"; "scopeSha256", box scopeSha ]))
// Rehearsal 009 recorded T0 in evidence/t0/T0.md (00:30:01Z); the T0 record format is new in Rehearsal 010.
let t0Record = write "t0.json" (json (dict [ "schema", box "indy-init.t0/1"; "t0", box "2026-11-04T00:30:01Z"; "deadline", box "2026-11-04T07:30:01Z"; "windowMinutes", box 420 ]))
let policy = Path.Combine(src, "kickoff", "rehearsal-010", "deadline-policy.json")
let releaseCandidate = "39dc037990c8c049ee997fae8384c5bcc9290e2b"

type Check = { Id: string; Finding: string; Kind: string; Expected: string; Command: string; Exit: int; Output: string; AsExpected: bool }
let check id finding kind expected (accept: int * string -> bool) (command: string, (exit: int, output: string)) =
    File.WriteAllText(Path.Combine(evidence, id + ".log"), $"$ {command}\nexit {exit}\n{output}")
    { Id = id; Finding = finding; Kind = kind; Expected = expected; Command = command; Exit = exit; Output = output; AsExpected = accept (exit, output) }
let refused (codes: string list) (exit: int, output: string) = exit = 4 && codes |> List.forall output.Contains
let cmd (name: string) (args: string list) = (name + " " + String.Join(" ", args |> List.map (fun a -> a.Replace(work, "$WORK").Replace(src, "$SRC")))), args

// Design contract (static): shell, Administration, Settings, sections, Forma save status, tokens, theme boundary.
let design =
    let c, a = cmd "requirements-compiler design-contract" [ "design-contract"; "--contract"; Path.Combine(src, "kickoff", "rehearsal-010", "design-contract.json"); "--forma-inventory"; Path.Combine(src, "verification", "design-contract", "forma-0.13.0-inventory.json"); "--root"; r9; "--candidate"; r9Head; "--out"; Path.Combine(evidence, "design-contract.json") ]
    c, rc a

// The governed catalog acceptance of Rehearsal 009's own catalog verdict.
let facts =
    let dir = at "evidence/requirement-verification/live/requirement-observations"
    let merged =
        Directory.EnumerateFiles(dir, "*.json")
        |> Seq.collect (fun f -> match (JsonDocument.Parse(File.ReadAllText f)).RootElement.TryGetProperty "facts" with | true, v when v.ValueKind = JsonValueKind.Object -> v.EnumerateObject() |> Seq.map (fun p -> p.Name, p.Value.GetRawText()) | _ -> Seq.empty)
        |> Seq.distinctBy fst |> Seq.map (fun (k, v) -> $"\"{k}\": {v}") |> String.concat ", "
    write "declared-facts.json" ("{" + merged + "}")
let contract = Path.Combine(src, "kickoff", "rehearsal-010", "catalog-verification-contract.json")
let contractSha = Security.Cryptography.SHA256.HashData(File.ReadAllBytes contract) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
let runId = (JsonDocument.Parse(File.ReadAllText(at "evidence/requirement-verification/requirement-verification.json"))).RootElement.GetProperty("runId").GetString()
let producer = write "producer.json" (json (dict [ "kind", box "implementer-run"; "runId", box runId; "workflow", box ".github/workflows/requirement-verification.yml (written by Rehearsal 009)" ]))
let catalogOut = Path.Combine(evidence, "catalog-acceptance.json")
let catalog =
    cmd "requirements-compiler catalog-acceptance"
        [ "catalog-acceptance"; "--contract"; contract; "--approved-contract-sha256"; contractSha; "--catalog"; Path.Combine(src, "requirements", "catalogs", "causa-investigation.requirement-catalog.json")
          "--candidate"; releaseCandidate; "--manifest"; at "evidence/requirement-verification/verification-manifest.json"; "--verification"; at "evidence/requirement-verification/requirement-verification.json"
          "--producer"; producer; "--probe-results"; at "evidence/requirement-verification/fault-probe-results.json"; "--probe-spec"; at ".echelon/fault-probes.json"; "--declared-facts"; facts; "--out"; catalogOut ]
    |> fun (c, a) -> c, rc a

// The full-scope completion gate with Rehearsal 009's own release-gate verdict.
let completion =
    cmd "requirements-compiler completion-gate"
        [ "completion-gate"; "--manifest"; manifest; "--events"; events; "--candidate"; releaseCandidate; "--release-gate"; at "evidence/release-gate/local-gate-release-39dc037.json"
          "--catalog"; catalogOut; "--out"; Path.Combine(evidence, "completion-gate.json") ]
    |> fun (c, a) -> c, rc a

// The stop requests Rehearsal 009 made at 02:25Z (115 minutes), judged with the clock bound to T0.
let stop (reason: string) =
    let request = write $"request-{reason}.json" (json (dict [ "at", box "2026-11-04T02:25:00Z"; "reason", box reason; "scopeSha256", box scopeSha ]))
    cmd "requirements-compiler stop-decision"
        [ "stop-decision"; "--manifest"; manifest; "--checkpoint"; checkpoint; "--events"; events; "--request"; request; "--t0-record"; t0Record; "--deadline-policy"; policy; "--out"; Path.Combine(evidence, $"stop-decision-{reason}.json") ]
    |> fun (c, a) -> c, rc a

// The ledger and scope report from the replayed events.
let ledger = cmd "requirements-compiler ledger" [ "ledger"; "--manifest"; manifest; "--events"; events; "--out"; Path.Combine(evidence, "scope-report.json") ] |> fun (c, a) -> c, rc a

// Praxis sequencing: the scope checkpoint (#102) and the initialization checkpoint (#101).
let order = cmd "requirements-compiler scope order" [ "scope"; "order"; "--repo"; r9 ] |> fun (c, a) -> c, rc a
let sequencing = ("r9.fsx check-sequencing --target R9", r9fsx [ "check-sequencing"; "--target"; r9; "--out"; Path.Combine(evidence, "check-sequencing.json") ] r9)

// Suite execution: the frozen v4.1.2 commands against the Rehearsal 009 tree (needs a .NET build of its tests).
let suites =
    if not runSuites then None
    else
        let obligations = Path.Combine(work, "obligations-v4.1.2.json")
        run "dokimos" [ "verification"; "compile"; "--indy-execution-plan"; Path.Combine(src, "requirements-compiled", "v4.1.2", "verification-execution-plan.json"); "--indy-verification-plan"; Path.Combine(src, "requirements-compiled", "v4", "verification-plan.json"); "--out"; obligations ] work |> ignore
        Some("r9.fsx suite-preflight (v4.1.2 plan) --root R9", r9fsx [ "suite-preflight"; "--obligations"; obligations; "--root"; r9; "--out"; Path.Combine(evidence, "suite-preflight-v4.1.2.json") ] r9)

// The stop requests are judged before the observer reads their decisions.
let stopConserve, stopComplete, stopGate = stop "conserve-compute", stop "complete", stop "release-gate-failed"

// The observer, over the evidence above and Rehearsal 009's final report claim.
let observerEvidence = Directory.CreateDirectory(Path.Combine(work, "observer-evidence")).FullName
let () =
    [ "design-contract.json"; "catalog-acceptance.json"; "stop-decision-conserve-compute.json" ] |> List.iter (fun f -> let p = Path.Combine(evidence, f) in if File.Exists p then File.Copy(p, Path.Combine(observerEvidence, f), true))
    let report = File.ReadAllText(at "evidence/FINAL-REPORT.md")
    let claim = report.Split('\n') |> Array.filter (fun l -> l.Contains("verified", StringComparison.OrdinalIgnoreCase) || l.Contains("accepted", StringComparison.OrdinalIgnoreCase)) |> Array.truncate 3 |> String.concat " "
    File.WriteAllText(Path.Combine(observerEvidence, "claim.json"), json (dict [ "schema", box "indy-init.builder-claim/1"; "source", box "evidence/FINAL-REPORT.md (Rehearsal 009)"; "text", box claim ]))
let t0InRepo = write "r9/evidence/t0/t0.json" (File.ReadAllText t0Record)
run "git" [ "-C"; r9; "-c"; "user.name=replay"; "-c"; "user.email=replay@example.invalid"; "add"; "evidence/t0/t0.json" ] r9 |> ignore
run "git" [ "-C"; r9; "-c"; "user.name=replay"; "-c"; "user.email=replay@example.invalid"; "commit"; "-qm"; "replay only: the T0 record in the Rehearsal 010 format (not part of Rehearsal 009)" ] r9 |> ignore
let observe =
    cmd "requirements-compiler observe"
        [ "observe"; "--manifest"; manifest; "--events"; events; "--repo"; r9; "--t0-record"; "evidence/t0/t0.json"; "--deadline-policy"; policy; "--now"; "2026-11-04T03:00:00Z"
          "--evidence"; observerEvidence; "--out"; Path.Combine(evidence, "observer-findings.json") ]
    |> fun (c, a) -> c, rc a

let checks =
    [ check "RP-01" "Missing full application shell (Forma navigation shell)" "observed" "design-contract REFUSED: FORMA_ROLE_MISSING navigation-shell" (refused [ "FORMA_ROLE_MISSING navigation-shell" ]) design
      check "RP-02" "Missing Administration and Settings" "observed" "DESTINATION_MISSING Administration and Settings" (refused [ "DESTINATION_MISSING primary destination Administration"; "DESTINATION_MISSING primary destination Settings" ]) design
      check "RP-03" "Missing causal analysis views (Overview, Causal Analysis, Timeline)" "observed" "SECTION_MISSING Causal Analysis" (refused [ "SECTION_MISSING investigation section Causal Analysis"; "SECTION_MISSING investigation section Timeline" ]) design
      check "RP-04" "Incomplete Forma integration (save status, theme boundary)" "observed" "FORMA_ROLE_MISSING save-status and LITERAL_COLOUR_OUTSIDE_THEME" (refused [ "FORMA_ROLE_MISSING save-status"; "LITERAL_COLOUR_OUTSIDE_THEME" ]) design
      check "RP-05" "Missing design tokens (UI-016 palette)" "observed" "PALETTE_TOKEN_MISSING for the palette, THEME_MISSING" (refused [ "PALETTE_TOKEN_MISSING"; "THEME_MISSING" ]) design
      check "RP-06" "Missing independent acceptance evidence (builder-run catalog verdict)" "observed" "catalog-acceptance not governed: PRODUCER_NOT_GOVERNED, and CATALOG_WRONG_COMMIT (33d072d is not the release candidate 39dc037)" (refused [ "PRODUCER_NOT_GOVERNED"; "CATALOG_WRONG_COMMIT" ]) catalog
      check "RP-07" "Incomplete scope presented as the approved scope" "observed" "completion-gate INCOMPLETE: SCOPE_NOT_INTEGRATED, RELEASE_GATE_REFUSED, CATALOG_NOT_GOVERNED_ACCEPTED, JOURNEYS_MISSING, DESIGN_CONTRACT_MISSING" (refused [ "SCOPE_NOT_INTEGRATED"; "RELEASE_GATE_REFUSED"; "CATALOG_NOT_GOVERNED_ACCEPTED"; "JOURNEYS_MISSING" ]) completion
      check "RP-08" "Premature stopping (115 of 420 minutes, compute)" "observed" "stop-decision CONTINUE_REQUIRED: NOT_A_STOP_CONDITION" (refused [ "NOT_A_STOP_CONDITION"; "CONTINUE_REQUIRED" ]) stopConserve
      check "RP-09" "Premature stopping claimed as complete (catalog only)" "observed" "stop-decision CONTINUE_REQUIRED: CATALOG_IS_NOT_SCOPE, SCOPE_NOT_INTEGRATED" (refused [ "CATALOG_IS_NOT_SCOPE"; "SCOPE_NOT_INTEGRATED" ]) stopComplete
      check "RP-10" "Premature stopping because the release gate failed" "observed" "stop-decision CONTINUE_REQUIRED: NOT_A_STOP_CONDITION (a failed gate is a result, not a stop condition)" (refused [ "NOT_A_STOP_CONDITION" ]) stopGate
      check "RP-11" "Incorrect Praxis sequencing (scope checkpoint)" "observed" "scope order REFUSED: IMPLEMENTATION_BEFORE_CHECKPOINT" (refused [ "IMPLEMENTATION_BEFORE_CHECKPOINT" ]) order
      check "RP-12" "Incorrect Praxis sequencing (initialization checkpoint)" "observed" "check-sequencing reports implementation commits before authorization" (fun (e, o) -> e <> 0 && o.Contains "edde38c") sequencing
      check "RP-13" "Incomplete scope, by requirement group" "observed" "ledger CONTINUE_REQUIRED with the P0 groups unstarted" (fun (e, o) -> e = 0 && o.Contains "CONTINUE_REQUIRED") ledger
      check "RP-14" "The observer reports the run's defects with evidence" "observed" "observe: blocking findings (IMPLEMENTATION_WITHOUT_WORK_ITEM, DESIGN_CONTRACT_FAILED, SELF_VERIFIED_SUCCESS, PREMATURE_STOP)" (refused [ "IMPLEMENTATION_WITHOUT_WORK_ITEM"; "DESIGN_CONTRACT_FAILED"; "SELF_VERIFIED_SUCCESS"; "PREMATURE_STOP" ]) observe ]
    @ (match suites with
       | Some s -> [ check "RP-15" "Invalid suite execution (v4.1.2 runners)" "observed" "suite-preflight REFUSED: MSB1008" (fun (e, o) -> e = 4 && o.Contains "MSB1008") s ]
       | None -> [])

let tsv = Path.Combine(evidence, "replay.tsv")
File.WriteAllText(tsv, "check\tfinding\tkind\texpected\texit\tas expected\tcommand\n")
checks |> List.iter (fun c ->
    File.AppendAllText(tsv, String.Join("\t", [ c.Id; c.Finding; c.Kind; c.Expected; string c.Exit; string c.AsExpected; c.Command ]) + "\n")
    printfn "%-6s %-11s %s" c.Id (if c.AsExpected then "AS-EXPECTED" else "UNEXPECTED") c.Finding)
File.WriteAllText(Path.Combine(evidence, "replay.json"), json (dict [ "schema", box "indy-init.r9-replay/1"; "r9Head", box r9Head; "releaseCandidate", box releaseCandidate; "checks", box (checks |> List.map (fun c -> dict [ "id", box c.Id; "finding", box c.Finding; "kind", box c.Kind; "expected", box c.Expected; "exit", box c.Exit; "asExpected", box c.AsExpected; "command", box c.Command ])) ]))
let unexpected = checks |> List.filter (fun c -> not c.AsExpected) |> List.length
printfn "\n%d check(s), %d as expected, %d unexpected" checks.Length (checks.Length - unexpected) unexpected
exit (if unexpected = 0 then 0 else 1)
