/// Phase 1: the requirements inventory. Baseline requirements are taken verbatim from the
/// `praxis.requirements-baseline/1` document; external requirement documents are sectioned
/// mechanically from the commit recorded in the curation sources.
module Inventory

open System.Text.RegularExpressions
open Json
open Model
open Text

let fromBaseline (baseline: Json) : Requirement list =
    let source = field "source" baseline |> Option.defaultValue JNull
    let repository = str "repository" source
    let commit = str "commit" source

    arr "requirements" baseline
    |> List.map (fun r ->
        { Id = str "id" r
          Title = str "title" r
          Priority = str "priority" r
          Document = str "document" r
          Heading = str "heading" r
          SectionSha256 = str "sectionSha256" r
          Repository = repository
          Commit = commit
          Origin = "baseline"
          Obligations =
            arr "obligations" r
            |> List.map (fun o ->
                { Id = str "id" o
                  Kind = str "kind" o
                  Text = str "text" o
                  Tokens = strs "tokens" o }) })

let private heading = Regex(@"^(#{1,3})\s+(.+?)\s*$")
let private listMarker = Regex(@"^\s*(?:[-*]|\d+\.)\s+")
let private tableSeparator = Regex(@"^\|[\s\-:|]+\|$")

let private unitsOf (body: string list) =
    body
    |> List.filter (fun l -> l.Trim() <> "" && not (l.StartsWith "Status:") && not (tableSeparator.IsMatch(l.Trim())))
    |> List.collect (fun l ->
        let line = listMarker.Replace(l, "").Trim()
        if line.StartsWith "|" then [ line ] else sentences line)

let private kindOf (sectionSlug: string) (text: string) =
    if sectionSlug.Contains "gate" || sectionSlug.Contains "acceptance"
       || Regex.IsMatch(text, @"(?i)\b(accept\w*|verif\w*|tests?|evidence)\b") then
        "acceptance"
    else
        "constraint"

/// Sections a markdown document by level 1-3 headings. Each section runs to the next heading of any
/// level, so no obligation is counted twice. Obligations are the verbatim sentences of the body.
let sectionDocument (key: string) (repo: string) (commit: string) (path: string) (text: string) : Requirement list =
    let lines = (normalizeNewlines text).Split('\n') |> List.ofArray
    let headingIndexes = lines |> List.indexed |> List.filter (fun (_, l) -> heading.IsMatch l) |> List.map fst

    headingIndexes
    |> List.mapi (fun n start ->
        let stop = if n + 1 < headingIndexes.Length then headingIndexes.[n + 1] else lines.Length
        let section = lines.[start .. stop - 1] |> List.rev |> List.skipWhile (fun l -> l.Trim() = "") |> List.rev
        let m = heading.Match lines.[start]
        let title = m.Groups.[2].Value
        let sectionSlug = if m.Groups.[1].Value = "#" then "preamble" else slug title
        let id = sprintf "%s§%s" key sectionSlug

        { Id = id
          Title = title
          Priority = "unstated"
          Document = path
          Heading = lines.[start]
          SectionSha256 = sha256OfText (String.concat "\n" section)
          Repository = repo
          Commit = commit
          Origin = "external"
          Obligations =
            unitsOf (List.tail section)
            |> List.mapi (fun i u ->
                { Id = sprintf "%s-O%d" id (i + 1)
                  Kind = kindOf sectionSlug u
                  Text = u
                  Tokens = [] }) })
    |> List.filter (fun r -> not r.Obligations.IsEmpty)

// ---- classification ----------------------------------------------------------

/// Category assignment comes from the curated `categories` table: the first matching selector wins.
let categoryOf (rules: (string * string) list) (r: Requirement) =
    rules |> List.tryFind (fun (sel, _) -> matches sel r) |> Option.map snd |> Option.defaultValue "uncategorized"

let tagsOf (r: Requirement) =
    let text = r.Title + " " + (r.Obligations |> List.map (fun o -> o.Text) |> String.concat " ")

    [ "security", mentionsSecurity text
      "accessibility", mentionsAccessibility text
      "error-handling", mentionsErrorHandling text
      "timing", mentionsTiming text ]
    |> List.filter snd
    |> List.map fst

// ---- explicit references -------------------------------------------------------

let private idPattern =
    @"(?:UI|WASM-LOAD|PF|EXT-DATA|STORAGE|DCC|ADRP|DEP-CURRENT|WDL|VER|AR|PERSIST|D)-\d{3}|R\d{1,2}\.\d{1,2}|S\d{2}"

let private rangePattern =
    Regex(@"\b((?:UI|WASM-LOAD|PF|EXT-DATA|STORAGE|DCC|ADRP|DEP-CURRENT|WDL|VER|AR|PERSIST)-)(\d{3})\s*(?:\.\.|–)\s*(?:\1)?(\d{3})\b")

let private singlePattern = Regex(sprintf @"(?<![A-Za-z0-9\-])(%s)(?![0-9])" idPattern)

/// Requirement ids a requirement's own text names, including `X-001..X-005` ranges, restricted to known ids.
let referencesOf (known: Set<string>) (r: Requirement) =
    let text = r.Obligations |> List.map (fun o -> o.Text) |> String.concat "\n"

    let ranged =
        rangePattern.Matches text
        |> Seq.collect (fun m ->
            let prefix = m.Groups.[1].Value
            let a = int m.Groups.[2].Value
            let b = int m.Groups.[3].Value
            if b >= a && b - a < 100 then seq { for i in a..b -> sprintf "%s%03d" prefix i } else Seq.empty)

    let single = singlePattern.Matches text |> Seq.map (fun m -> m.Groups.[1].Value)

    Seq.append ranged single
    |> Seq.filter (fun id -> known.Contains id && id <> r.Id)
    |> Set.ofSeq
    |> Set.toList
