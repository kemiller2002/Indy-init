/// F# port of tools/build-requirements-baseline.py: drafts the Causa requirements baseline
/// (praxis.requirements-baseline/1). The rules, document list, exclusions and output bytes are
/// those of the Python generator; the equivalence tests compare outputs byte for byte.
/// `read` supplies a document's text, so the same rules run on a working tree or at a git commit.
module Baseline

open System
open System.Text.RegularExpressions
open Json

let documents: (string * string * string option) list =
    [ "docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md", "CAUSA-UI-EXPERIENCE", Some @"^## (UI-\d{3}):"
      "docs/WASM-LOADING-EXPERIENCE-REQUIREMENTS.md", "WASM-LOADING-EXPERIENCE", Some @"^## (WASM-LOAD-\d{3}):"
      "docs/PROVIDER-FIXTURE-AND-INTEGRATION-REQUIREMENTS.md", "PROVIDER-FIXTURE-AND-INTEGRATION", Some @"^\*\*(PF-\d{3}) "
      "docs/REHEARSAL-EXTERNAL-DATA-BOUNDARY.md", "REHEARSAL-EXTERNAL-DATA-BOUNDARY", Some @"^- (EXT-DATA-\d{3}) "
      "docs/FROZEN-DEMO-STORAGE-REQUIREMENTS.md", "FROZEN-DEMO-STORAGE", Some @"^## (STORAGE-\d{3}):"
      "docs/DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md", "DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES", Some @"^## (DCC-\d{3}):"
      "docs/AGENT-DECISION-REDUCTION-PROFILE.md", "AGENT-DECISION-REDUCTION-PROFILE", Some @"^## (ADRP-\d{3}):"
      "docs/REHEARSAL-DEPENDENCY-LOCK.md", "REHEARSAL-DEPENDENCY-LOCK", Some @"^## (DEP-CURRENT-\d{3}):"
      "docs/REQUIREMENTS.md", "REQUIREMENTS", Some @"^### (R\d+\.\d+) "
      "docs/SCREEN-CONTRACTS.md", "SCREEN-CONTRACTS", Some @"^## (S\d{2}) "
      "docs/DESIGN-DECISION-BOUNDARIES.md", "DESIGN-DECISION-BOUNDARIES", Some @"^### (D-\d{3}) "
      "docs/STATE-TRANSITION-TABLES.md", "STATE-TRANSITION-TABLES", None
      "docs/KICKOFF-EXECUTION-PACKET.md", "KICKOFF-EXECUTION-PACKET", None
      "docs/CAUSA-STORAGE-RELEASE-v1.0.0.md", "CAUSA-STORAGE-RELEASE", None
      "docs/WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md", "WORKFLOW-DIAGRAM-INTEGRATION", Some @"^\*\*(WDL-\d{3}) "
      "docs/SYSTEM-VERSION-AND-COMPATIBILITY-REQUIREMENTS.md", "SYSTEM-VERSION-AND-COMPATIBILITY", Some @"^\*\*(VER-\d{3}) "
      "docs/AGENT-RELIABILITY-REQUIREMENTS.md", "AGENT-RELIABILITY", Some @"^\*\*(AR-\d{3}) "
      "docs/WASM-DIAGNOSTICS-REQUIREMENTS.md", "WASM-DIAGNOSTICS", Some @"^## (WASM-DIAG-\d{3}):"
      "docs/REHEARSAL-AGENT-INTERFACE-FREEZE.md", "REHEARSAL-AGENT-INTERFACE-FREEZE", None
      "docs/REHEARSAL-ACCEPTANCE-LEDGER.md", "REHEARSAL-ACCEPTANCE-LEDGER", None ]

/// Documents added after the Rehearsal 4 pin (BL-CAUSA-INDY-INIT-aab8f80c7743). At a source commit that predates
/// one of them it is skipped, so the baseline and package of that commit still rebuild byte for byte with these
/// tools; at any later commit it is required like every other document.
let introducedAfterRehearsal4 = set [ "docs/WASM-DIAGNOSTICS-REQUIREMENTS.md" ]

let private excludedSections =
    set
        [ "docs/REHEARSAL-DEPENDENCY-LOCK.md", "## Current observed historical baseline (NOT a recommended target)"
          "docs/DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md", "## Objective"
          "docs/AGENT-DECISION-REDUCTION-PROFILE.md", "## Purpose"
          "docs/AGENT-DECISION-REDUCTION-PROFILE.md", "## Still deliberately open"
          "docs/AGENT-DECISION-REDUCTION-PROFILE.md", "## Supporting normative documents"
          "docs/SCREEN-CONTRACTS.md", "## Percepta integration"
          "docs/CAUSA-STORAGE-RELEASE-v1.0.0.md", "## How the tag was created" ]

let private acceptance =
    Regex(@"\b(acceptance|accepted|tests?|tested|testing|screenshots?|verify|verified)\b", RegexOptions.IgnoreCase)

let private tokenPatterns =
    [ @"#[0-9A-Fa-f]{6}\b"
      @"\b[0-9a-f]{40}\b"
      @"\b\d+(?:\.\d+)?:1\b"
      @"\b\d+(?:\.\d+)?%"
      @"\b\d+(?:\.\d+)?\s?(?:px|ms|seconds?|sec|minutes?|min|hours?|KB|MB|GB|fps|rem|em)\b"
      @"\bv?\d+\.\d+\.\d+\b"
      @"(?<![\w.-])[45]\d\d(?![\w.-])"
      @"\b\d{1,2}:\d{2}\b"
      @"\b(?:[Tt]wo|[Tt]hree|[Ff]our|[Ff]ive|[Ss]ix|[Ss]even|[Ee]ight|[Nn]ine|[Tt]en|[Ee]leven|[Tt]welve)\b" ]
    |> List.map Regex

let private acceptanceHeading = Regex(@"acceptance|required evidence|completion evidence", RegexOptions.IgnoreCase)
let private sentence = Regex(@"(?<=[.!?])\s+(?=[A-Z0-9*`""(\[])")
let private priority = Regex(@"\[(P\d)\]")
let private separatorRow = Regex(@"\A[|:\- ]+\z")
let private listMarker = Regex(@"^(?:[-*]|\d+\.)\s+")
let private headerRow = Regex(@"^\|\s*(ID|Token role|Gate|Class|Requirement)\s*\|")
let private boldPrefix = Regex(@"^(\*\*[^*]+\*\*\s*)")
let private bulletPrefix = Regex(@"^(- \S+ (?:\[P\d\] )?)")
let private idPrefix = Regex(@"^(\S+?)[:\s]+")
let private priorityMarker = Regex(@"\s*\[P\d\]\s*")
let private never = Regex("(?!x)x")

let normalize (text: string) = text.Replace("\r\n", "\n").Replace("\r", "\n")

let sha256 (text: string) = normalize text |> Text.Encoding.UTF8.GetBytes |> Git.sha256Hex

let headingLevel (line: string) =
    let level = line.Length - line.TrimStart('#').Length
    if level > 0 && level < line.Length && line.[level] = ' ' then Some level else None

/// Section boundaries per the Praxis preservation contract: to the next requirement line, or the next
/// heading at the same or a higher level (any heading, for a non-heading anchor); trailing blanks removed.
let section (lines: string[]) (start: int) (regex: Regex) =
    let anchorLevel = headingLevel lines.[start]

    let finish =
        seq { start + 1 .. lines.Length - 1 }
        |> Seq.tryFind (fun i ->
            regex.IsMatch lines.[i]
            || (match headingLevel lines.[i], anchorLevel with
                | Some level, Some anchor -> level <= anchor
                | Some _, None -> true
                | None, _ -> false))
        |> Option.defaultValue lines.Length

    lines.[start .. finish - 1] |> List.ofArray |> List.rev |> List.skipWhile (fun l -> l.Trim() = "") |> List.rev

let tokensOf (text: string) =
    tokenPatterns
    |> List.collect (fun p -> p.Matches text |> Seq.map (fun m -> m.Value) |> List.ofSeq)
    |> List.distinct

/// Verbatim obligation units of a section body.
let units (body: string list) (anchorIsHeading: bool) (anchorPrefix: string) =
    let lines =
        if anchorIsHeading then List.tail body
        else (body.Head.Substring anchorPrefix.Length) :: List.tail body

    lines
    |> List.fold
        (fun (inCode, acc) line ->
            let stripped = line.Trim()
            if stripped.StartsWith "```" then (not inCode, acc)
            elif inCode || stripped = "" || (headingLevel stripped).IsSome || stripped.StartsWith "Status:" then (inCode, acc)
            elif stripped.StartsWith "|" then (inCode, (if separatorRow.IsMatch stripped then acc else stripped :: acc))
            else
                let content = listMarker.Replace(stripped, "", 1)
                let parts = sentence.Split content |> Array.map (fun s -> s.Trim()) |> Array.filter (fun s -> s <> "")
                (inCode, (List.rev (List.ofArray parts)) @ acc))
        (false, [])
    |> snd
    |> List.rev

let private slug (text: string) =
    Regex.Replace(text.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-')

exception BaselineError of string

/// Builds the baseline. `read path` returns a document's text; failures raise BaselineError,
/// matching the Python generator's SystemExit (exit code 1).
let build (read: string -> string) (commit: string) : Json =
    let folder (docs, reqs, seen: Set<string>) (path: string, key: string, pattern: string option) =
        let text = normalize (read path)
        let lines = text.Split('\n') |> Array.map (fun l -> l.TrimEnd())
        let regex = pattern |> Option.map Regex |> Option.defaultValue never

        let document =
            JObj(
                [ "path", JStr path; "sha256", JStr(sha256 text) ]
                @ (pattern |> Option.map (fun p -> [ "requirementPattern", JStr p ]) |> Option.defaultValue [])
            )

        let docReqs, seen' =
            lines
            |> Array.indexed
            |> Array.fold
                (fun (acc: Json list, seen: Set<string>) (index, line) ->
                    let m = regex.Match line
                    let level = headingLevel line

                    let candidate =
                        if m.Success then Some(Some m.Groups.[1].Value)
                        elif (level = Some 1 || level = Some 2 || level = Some 3) && not (excludedSections.Contains(path, line)) then Some None
                        else None

                    match candidate with
                    | None -> acc, seen
                    | Some explicitId ->
                        let body = section lines index regex
                        let sectionText = String.Join("\n", body)

                        let ridOpt =
                            match explicitId with
                            | Some rid -> Some rid
                            | None when (List.tail body |> List.exists (fun l -> l.Trim() <> "")) ->
                                Some(if level = Some 1 then key + "§preamble" else sprintf "%s§%s" key (slug (line.TrimStart('#').Trim())))
                            | None -> None

                        match ridOpt with
                        | None -> acc, seen
                        | Some rid ->
                            if seen.Contains rid then raise (BaselineError(sprintf "duplicate requirement id %s in %s" rid path))

                            let title0, prefix =
                                match level with
                                | Some _ -> Regex.Replace(line, @"^#+\s+", ""), ""
                                | None ->
                                    let b = boldPrefix.Match line
                                    let u = bulletPrefix.Match line
                                    let p = if b.Success then b.Groups.[1].Value elif u.Success then u.Groups.[1].Value else ""
                                    p.Trim().Trim('*').Trim('-', ' ').Trim(), p

                            let title1 = if m.Success then idPrefix.Replace(title0, "", 1) else title0
                            let title = priorityMarker.Replace(title1, " ").Trim().TrimEnd('.')
                            let pr = priority.Match line
                            let acceptanceSection = acceptanceHeading.IsMatch line

                            let obligations =
                                units body level.IsSome prefix
                                |> List.filter (fun u -> not (u.StartsWith "|" && headerRow.IsMatch u))
                                |> List.mapi (fun i u ->
                                    let tokens = tokensOf u
                                    let kind =
                                        if acceptanceSection || acceptance.IsMatch u then "acceptance"
                                        elif not tokens.IsEmpty then "value"
                                        else "constraint"
                                    if not (sectionText.Contains(u, StringComparison.Ordinal)) then
                                        raise (BaselineError(sprintf "internal: unit not verbatim in %s: %s" rid u))
                                    JObj(
                                        [ "id", JStr(sprintf "%s-O%d" rid (i + 1)); "kind", JStr kind; "text", JStr u ]
                                        @ (if tokens.IsEmpty then [] else [ "tokens", jstrs tokens ])
                                    ))

                            if obligations.IsEmpty then
                                if m.Success then raise (BaselineError(sprintf "requirement %s in %s has no obligation text" rid path))
                                acc, seen.Add rid
                            else
                                let req =
                                    JObj
                                        [ "id", JStr rid
                                          "title", JStr(if title = "" then rid else title)
                                          "priority", JStr(if pr.Success then pr.Groups.[1].Value else "unstated")
                                          "document", JStr path
                                          "heading", JStr line
                                          "sectionSha256", JStr(sha256 sectionText)
                                          "obligations", JArr obligations ]

                                req :: acc, seen.Add rid)
                (reqs, seen)

        document :: docs, docReqs, seen'

    let present (path: string, _, _) =
        not (introducedAfterRehearsal4.Contains path) || (try read path |> ignore; true with _ -> false)

    let docs, reqs, _ = documents |> List.filter present |> List.fold folder ([], [], Set.empty)

    JObj
        [ "schema", JStr "praxis.requirements-baseline/1"
          "id", JStr("BL-CAUSA-INDY-INIT-" + commit.Substring(0, 12))
          "source",
          JObj [ "repository", JStr "kemiller2002/indy-init"; "commit", JStr commit; "documents", JArr(List.rev docs) ]
          "requirements", JArr(List.rev reqs)
          "conflicts", JArr [] ]

let isCommit (s: string) = Regex.IsMatch(s, @"\A[0-9a-f]{40}\z")

/// The baseline without its identity (id and source.commit): two baselines with equal content describe the same
/// requirements, whatever commit each was pinned at. Used to decide drift (RDP-001).
let content (baseline: Json) : Json =
    match baseline with
    | JObj props ->
        props
        |> List.filter (fun (k, _) -> k <> "id")
        |> List.map (fun (k, v) ->
            match k, v with
            | "source", JObj sp -> k, JObj(sp |> List.filter (fun (n, _) -> n <> "commit"))
            | _ -> k, v)
        |> JObj
    | j -> j
