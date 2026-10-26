/// Phase 2: mechanical normalization. Obligation text is never rewritten; normalization adds
/// structure (modality, acceptance linkage, overlap groups, flags) and keeps every source id.
module Normalize

open System.Text.RegularExpressions
open Model
open Text

type Flag = { Code: string; Severity: string; Detail: string }

type OverlapGroup =
    { Id: string
      Kind: string
      Requirements: string list
      Obligations: string list
      Similarity: decimal option
      Text: string }

let private acceptanceSection =
    Regex(@"§.*(acceptance|required-evidence|completion-evidence|completion-gate|preflight-acceptance|adversarial-acceptance)")

let isAcceptanceSection (r: Requirement) = acceptanceSection.IsMatch r.Id

/// Acceptance sections of the same document that a requirement's own section does not contain.
let linkedAcceptance (all: Requirement list) (r: Requirement) =
    all
    |> List.filter (fun a -> a.Document = r.Document && a.Id <> r.Id && isAcceptanceSection a)
    |> List.map (fun a -> a.Id)

let ownAcceptance (r: Requirement) =
    r.Obligations |> List.filter (fun o -> o.Kind = "acceptance") |> List.map (fun o -> o.Id)

let strongestModality (r: Requirement) =
    r.Obligations
    |> List.map (fun o -> modalityOf o.Text)
    |> List.sortByDescending modalityRank
    |> List.tryHead
    |> Option.defaultValue Unmarked

// ---- overlaps ------------------------------------------------------------------

let private tableHeader = Regex(@"^\|(\s*[A-Z][A-Za-z /-]*\s*\|)+$")

/// Exact duplicates: the same canonical obligation text (at least four words) inventoried under more than one
/// requirement. Short fragments and table header rows are not treated as duplicates.
let exactDuplicates (reqs: Requirement list) : OverlapGroup list =
    reqs
    |> List.collect (fun r -> r.Obligations |> List.map (fun o -> canonical o.Text, r.Id, o.Id, o.Text))
    |> List.filter (fun (c, _, _, _) -> c.Length >= 25 && c.Split(' ').Length >= 4)
    |> List.filter (fun (_, _, _, text) -> not (tableHeader.IsMatch(text.Trim())))
    |> List.groupBy (fun (c, _, _, _) -> c)
    |> List.filter (fun (_, xs) -> (xs |> List.map (fun (_, r, _, _) -> r) |> List.distinct).Length > 1)
    |> List.sortBy fst
    |> List.mapi (fun i (_, xs) ->
        let (_, _, _, text) = xs.Head

        { Id = sprintf "DUP-%04d" (i + 1)
          Kind = "exact-duplicate-obligation"
          Requirements = xs |> List.map (fun (_, r, _, _) -> r) |> List.distinct |> List.sort
          Obligations = xs |> List.map (fun (_, _, o, _) -> o) |> List.sort
          Similarity = Some 1m
          Text = text })

/// Structural containment: every obligation of B is also an obligation of A in the same document.
/// This is how a level-1 heading section that spans its sub-sections appears in the baseline. Two sections with
/// identical obligation sets are reported once, with the ordinally greater id as the container.
let containments (reqs: Requirement list) : OverlapGroup list =
    let canon (r: Requirement) = r.Obligations |> List.map (fun o -> canonical o.Text) |> Set.ofList

    reqs
    |> List.groupBy (fun r -> r.Document)
    |> List.collect (fun (_, docReqs) ->
        docReqs
        |> List.collect (fun a ->
            let ca = canon a

            docReqs
            |> List.filter (fun b -> b.Id <> a.Id && b.Obligations.Length > 0)
            |> List.filter (fun b ->
                let cb = canon b
                Set.isSubset cb ca && (ca.Count > cb.Count || (ca = cb && System.String.CompareOrdinal(a.Id, b.Id) > 0)))
            |> List.map (fun b -> a, b)))
    |> List.sortBy (fun (a, b) -> a.Id, b.Id)
    |> List.mapi (fun i (a, b) ->
        { Id = sprintf "CNT-%04d" (i + 1)
          Kind = "structural-containment"
          Requirements = [ a.Id; b.Id ]
          Obligations = []
          Similarity = None
          Text = sprintf "%s repeats all %d obligations of %s" a.Id b.Obligations.Length b.Id })

/// Near duplicates across requirements: content-word Jaccard similarity at or above the threshold.
let nearDuplicates (threshold: float) (reqs: Requirement list) : OverlapGroup list =
    let entries =
        reqs
        |> List.collect (fun r -> r.Obligations |> List.map (fun o -> r.Id, o, contentWords o.Text))
        |> List.filter (fun (_, _, w) -> w.Count >= 6)
        |> Array.ofList

    let vocabulary =
        entries |> Seq.collect (fun (_, _, w) -> w) |> Seq.distinct |> Seq.sort |> Seq.mapi (fun i w -> w, i) |> Map.ofSeq

    let vectors = entries |> Array.map (fun (_, _, w) -> w |> Seq.map (fun x -> vocabulary.[x]) |> Seq.sort |> Array.ofSeq)

    let similarity (a: int[]) (b: int[]) =
        let rec go i j common =
            if i >= a.Length || j >= b.Length then common
            elif a.[i] = b.[j] then go (i + 1) (j + 1) (common + 1)
            elif a.[i] < b.[j] then go (i + 1) j common
            else go i (j + 1) common

        let common = go 0 0 0
        float common / float (a.Length + b.Length - common)

    [ for i in 0 .. entries.Length - 1 do
          for j in i + 1 .. entries.Length - 1 do
              let (ra, oa, _) = entries.[i]
              let (rb, ob, _) = entries.[j]

              if ra <> rb && canonical oa.Text <> canonical ob.Text then
                  let s = similarity vectors.[i] vectors.[j]
                  if s >= threshold then yield (ra, oa, rb, ob, s) ]
    |> List.sortBy (fun (ra, oa, rb, ob, _) -> oa.Id, ob.Id)
    |> List.mapi (fun i (ra, oa, rb, ob, s) ->
        { Id = sprintf "NDP-%04d" (i + 1)
          Kind = "near-duplicate-obligation"
          Requirements = [ ra; rb ] |> List.distinct
          Obligations = [ oa.Id; ob.Id ]
          Similarity = Some(System.Math.Round(decimal s, 3))
          Text = oa.Text })

// ---- flags ---------------------------------------------------------------------

let private uiCategories = set [ "ui"; "ui-screen"; "ui-loading"; "ui-visualization" ]

let private behaviourCategories =
    set [ "ui"; "ui-screen"; "ui-loading"; "persistence"; "data-boundary"; "storage-contract"; "interface-contract"; "integration-testing"; "domain" ]

let flagsOf (all: Requirement list) (category: string) (tags: string list) (r: Requirement) : Flag list =
    let hasId = not (r.Id.Contains "§")
    let decisionBoundary = r.Document.EndsWith "DESIGN-DECISION-BOUNDARIES.md"
    let allText = r.Obligations |> List.map (fun o -> o.Text) |> String.concat " "

    [ if hasId && r.Priority = "unstated" && not decisionBoundary then
          if Regex.IsMatch(r.Heading, @"\[P\d") then
              yield { Code = "PRIORITY-IN-TITLE-ONLY"; Severity = "major"; Detail = sprintf "priority marker in heading '%s' was not parsed as the requirement priority" r.Heading }
          else
              yield { Code = "PRIORITY-UNSTATED"; Severity = "minor"; Detail = "the source assigns no P0/P1 priority; cut decisions cannot rank it" }
      if ownAcceptance r |> List.isEmpty && linkedAcceptance all r |> List.isEmpty && not (isAcceptanceSection r) && not decisionBoundary then
          yield { Code = "ACCEPTANCE-CRITERIA-MISSING"; Severity = "major"; Detail = "no acceptance obligation in the section and no acceptance section in the document" }
      if r.Obligations |> List.forall (fun o -> modalityOf o.Text = Unmarked) then
          yield { Code = "MODALITY-UNMARKED"; Severity = "minor"; Detail = "no obligation states MUST/SHOULD/MAY or an imperative prohibition" }
      match vagueTermsIn allText with
      | [] -> ()
      | terms -> yield { Code = "VAGUE-TERMS"; Severity = "minor"; Detail = String.concat ", " terms }
      if behaviourCategories.Contains category && not (List.contains "error-handling" tags) && hasId then
          yield { Code = "ERROR-HANDLING-UNSTATED"; Severity = "info"; Detail = "no failure, refusal, conflict or recovery behaviour stated in this section" }
      if uiCategories.Contains category && not (List.contains "accessibility" tags) && hasId then
          yield { Code = "ACCESSIBILITY-NOT-LOCAL"; Severity = "info"; Detail = "no accessibility obligation in this section; cross-cutting UI-011, R9.3, WASM-LOAD-006 and WDL-007 apply" } ]
