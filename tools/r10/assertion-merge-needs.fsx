// assertion-merge-needs.fsx: the competition's assertion merge needs (CF-07, slice-gate --assertion-needs), derived
// mechanically from Rehearsal 010's (ODR-R10-33 A) by moving each needed slice to the competition slice that owns its
// requirements (PROPOSED: ODR-COMP-P16).
//
//   dotnet fsi tools/r10/assertion-merge-needs.fsx -- --indy-init . derive [--out FILE]
//        Writes the competition file (default docs/competition-readiness/assertion-merge-needs.json).
//   dotnet fsi tools/r10/assertion-merge-needs.fsx -- --indy-init . test
//        The committed file is the derivation's output; every needed slice is a slice of the competition slice manifest;
//        every requirement of every Rehearsal 010 need is owned by one of the competition slices it maps to; every rule
//        keeps its assertions and reason; negative controls.
//
// Rule (no judgement): the requirements of a Rehearsal 010 need are those of its slice in the Rehearsal 010 slice
// manifest, or, for a work-item split part (PR #121's slices, e.g. SL-WI-INV-WORKSPACE-FRAME, which Rehearsal 010's
// scope ignored as absent), those of its part in the split proposal. A need maps to itself when the competition has a
// slice of that id owning all of them; otherwise to every competition slice owning one of them. A need with no
// requirement, or with a requirement no competition slice owns, is an error (nothing is dropped silently). Needs are
// then de-duplicated in order. Only slice ids change.

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Nodes

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let opt name = args |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let root = opt "--indy-init" |> Option.defaultValue "."
let at (p: string) = Path.Combine(root, p)

let r10NeedsRel = "docs/rehearsal-010-readiness/assertion-merge-needs.json"
let r10SlicesRel = "docs/rehearsal-010-readiness/slice-execution-manifest.json"
let splitRel = "docs/rehearsal-010-optimization/inputs/work-item-split.proposed.json"
let compSlicesRel = "docs/competition-readiness/slice-execution-manifest.json"
let compScopeRel = "docs/competition-readiness/scope-manifest.json"
let outRel = "docs/competition-readiness/assertion-merge-needs.json"

let parseText (t: string) = JsonNode.Parse t
let read (rel: string) = File.ReadAllText(at rel)
let sha256 (rel: string) = SHA256.HashData(File.ReadAllBytes(at rel)) |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
let str (n: JsonNode) (k: string) = match n with null -> "" | n -> match n[k] with null -> "" | v -> v.ToString()
let arr (n: JsonNode) (k: string) = match n with null -> [] | n -> match n[k] with :? JsonArray as a -> List.ofSeq a | _ -> []
let strs (n: JsonNode) (k: string) = arr n k |> List.map string
let jstr (s: string) = JsonValue.Create s :> JsonNode
let jarr (xs: JsonNode list) = JsonArray(Array.ofList xs) :> JsonNode
let jstrs (xs: string list) = xs |> List.map jstr |> jarr
let jobj (pairs: (string * JsonNode) list) =
    let o = JsonObject()
    pairs |> List.iter (fun (k, v) -> o[k] <- v)
    o :> JsonNode
let render (n: JsonNode) =
    n.ToJsonString(JsonSerializerOptions(WriteIndented = true, Encoder = Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping)) + "\n"

/// The inputs of the derivation, as plain data.
type Inputs =
    { Rules: (string list * string * string list) list          // assertions, why, needs (Rehearsal 010)
      R10Requirements: Map<string, string list>                // Rehearsal 010 slice id -> requirements
      SplitRequirements: Map<string, string list>              // split part slice id (SL-<part>) -> requirements
      Competition: (string * string list) list }                // competition slice id -> requirements, in manifest order

let slicesOf (manifest: JsonNode) = arr manifest "slices" |> List.map (fun s -> str s "id", strs s "requirements")

let load () : Inputs =
    let needs = parseText (read r10NeedsRel)
    { Rules = arr needs "rules" |> List.map (fun r -> strs r "assertions", str r "why", strs r "needs")
      R10Requirements = slicesOf (parseText (read r10SlicesRel)) |> Map.ofList
      SplitRequirements =
        arr (parseText (read splitRel)) "splits"
        |> List.collect (fun sp -> arr sp "parts" |> List.map (fun p -> "SL-" + str p "id", strs p "requirements"))
        |> Map.ofList
      Competition = slicesOf (parseText (read compSlicesRel)) }

/// The requirements a Rehearsal 010 need stands for, and where they were read.
let requirementsOf (i: Inputs) (need: string) : Result<string list * string, string> =
    match i.R10Requirements.TryFind need, i.SplitRequirements.TryFind need with
    | Some rs, _ when not rs.IsEmpty -> Ok(rs, "rehearsal-010 slice")
    | _, Some rs when not rs.IsEmpty -> Ok(rs, "work-item split part")
    | Some _, _ -> Error(sprintf "%s owns no requirement in %s, so it has no competition owner" need r10SlicesRel)
    | None, _ -> Error(sprintf "%s is neither a slice of %s nor a part of %s" need r10SlicesRel splitRel)

/// One remapped need: the competition slices it maps to, or why it cannot be mapped.
let remap (i: Inputs) (need: string) : Result<string list * string list * string, string> =
    requirementsOf i need
    |> Result.bind (fun (rs, from) ->
        let owners (r: string) = i.Competition |> List.filter (fun (_, owned) -> List.contains r owned) |> List.map fst
        let unowned = rs |> List.filter (owners >> List.isEmpty)
        let same = i.Competition |> List.tryFind (fun (id, owned) -> id = need && rs |> List.forall (fun r -> List.contains r owned))
        if not unowned.IsEmpty then Error(sprintf "%s: no competition slice owns %s" need (String.Join(", ", unowned)))
        else
            match same with
            | Some (id, _) -> Ok([ id ], rs, from)
            | None ->
                let targets = rs |> List.collect owners |> Set.ofList
                Ok(i.Competition |> List.map fst |> List.filter targets.Contains, rs, from))

let distinctNeeds (i: Inputs) = i.Rules |> List.collect (fun (_, _, n) -> n) |> List.distinct

/// The competition file, or every need that could not be mapped.
let derive (i: Inputs) : Result<JsonNode, string list> =
    let mapped = distinctNeeds i |> List.map (fun n -> n, remap i n)
    match mapped |> List.choose (fun (_, r) -> match r with Error e -> Some e | Ok _ -> None) with
    | _ :: _ as errors -> Error errors
    | [] ->
        let table = mapped |> List.map (fun (n, r) -> match r with Ok v -> n, v | Error e -> failwith e) |> Map.ofList
        let target (n: string) = let (t, _, _) = table[n] in t
        Ok(jobj [
            "schema", jstr "indy-init.r10-contract-first-ui.merge-dependencies/1"
            "description", jstr (sprintf "PROPOSED (ODR-COMP-P16): the competition's merge-time needs of each governed journey assertion, for slice-gate --assertion-needs (Ledger.deferredAssertions, CF-07). Derived mechanically by tools/r10/assertion-merge-needs.fsx from %s (ODR-R10-33 A, PR #121's rules): every rule keeps its assertions and reason; each needed slice is moved to the competition slice (%s) that owns the requirements it stands for, and the needs are de-duplicated. Rehearsal 010's file names 11 slices the competition plan does not have; the gate ignores an absent id, so with that file the competition would judge those assertions before what they need is on main." r10NeedsRel compSlicesRel)
            "derivedFrom", jobj [
                "rehearsal010Needs", jobj [ "path", jstr r10NeedsRel; "sha256", jstr (sha256 r10NeedsRel) ]
                "rehearsal010Slices", jobj [ "path", jstr r10SlicesRel; "sha256", jstr (sha256 r10SlicesRel) ]
                "workItemSplit", jobj [ "path", jstr splitRel; "sha256", jstr (sha256 splitRel) ]
                "competitionSlices", jobj [ "path", jstr compSlicesRel; "sha256", jstr (sha256 compSlicesRel) ]
                "rule", jstr "a need's requirements are its Rehearsal 010 slice's, or its split part's; it maps to itself when the competition slice of that id owns all of them, otherwise to every competition slice owning one of them" ]
            "rules", jarr (i.Rules |> List.map (fun (assertions, why, needs) ->
                jobj [ "assertions", jstrs assertions; "why", jstr why; "needs", jstrs (needs |> List.collect target |> List.distinct) ]))
            "remap", jarr (distinctNeeds i |> List.map (fun n ->
                let (t, rs, from) = table[n]
                jobj [ "rehearsal010", jstr n; "competition", jstrs t; "requirementsFrom", jstr from; "requirements", jstrs rs ]))
            "adoptedBy", jstr "Not adopted. Proposed by owner decision request ODR-COMP-P16 (docs/v416-competition-decisions/proposals/owner-decision-requests.json), pinned by sha256; the competition lock names it as assertionMergeNeeds.path, which the governed gate reads (Rehearsal 010's file is the default). The deferral rule is ODR-R10-33 A's, unchanged." ])

// ---------------------------------------------------------------- test

let check (name: string) (ok: bool) (detail: string) = name, (if ok then Ok() else Error detail)

let tests () =
    let i = load ()
    let committed = try Some(parseText (read outRel)) with _ -> None
    let compIds = i.Competition |> List.map fst |> Set.ofList
    let scopeItems = arr (parseText (read compScopeRel)) "workItems" |> List.map (fun w -> "SL-" + str w "id") |> Set.ofList
    let derived = derive i
    [ check "the committed file is the derivation's output (byte-equal)"
          (match derived, committed with Ok d, Some _ -> render d = read outRel | _ -> false)
          (match derived with Error e -> String.Join("; ", e) | Ok _ -> sprintf "%s differs: run derive" outRel)
      ( match committed with
        | None -> check "every needed slice is a competition slice" false (sprintf "%s is missing" outRel)
        | Some c ->
            let unknown = arr c "rules" |> List.collect (fun r -> strs r "needs") |> List.distinct |> List.filter (compIds.Contains >> not)
            check "every needed slice is a slice of the competition slice manifest" unknown.IsEmpty (String.Join(", ", unknown)) )
      ( let unknown = compIds |> Set.filter (scopeItems.Contains >> not)
        check "every competition slice is a work item of the competition scope" unknown.IsEmpty (String.Join(", ", unknown)) )
      ( match committed with
        | None -> check "every requirement's owning slice is a need" false "missing"
        | Some c ->
            let owned = i.Competition |> Map.ofList
            let bad =
                List.zip i.Rules (arr c "rules")
                |> List.collect (fun ((assertions, _, needs), r) ->
                    let compNeeds = strs r "needs"
                    needs |> List.collect (fun n ->
                        match requirementsOf i n with
                        | Error e -> [ e ]
                        | Ok (rs, _) ->
                            rs |> List.filter (fun req -> not (compNeeds |> List.exists (fun cn -> owned.TryFind cn |> Option.exists (List.contains req))))
                               |> List.map (sprintf "%s: %s's requirement %s is owned by no need" (String.Join(",", assertions)) n)))
            check "every requirement of every Rehearsal 010 need is owned by one of the rule's competition needs" bad.IsEmpty (String.Join("; ", bad)) )
      ( match committed with
        | None -> check "assertions and reasons kept" false "missing"
        | Some c ->
            let kept = arr c "rules" |> List.map (fun r -> strs r "assertions", str r "why")
            check "every rule keeps its assertions and reason, in order" (kept = (i.Rules |> List.map (fun (a, w, _) -> a, w))) "a rule's assertions or reason changed" )
      ( match committed with
        | None -> check "needs only move" false "missing"
        | Some c ->
            // no need is invented: each competition need owns a requirement of one of the rule's Rehearsal 010 needs
            let owned = i.Competition |> Map.ofList
            let bad =
                List.zip i.Rules (arr c "rules")
                |> List.collect (fun ((_, _, needs), r) ->
                    let reqs = needs |> List.collect (fun n -> match requirementsOf i n with Ok (rs, _) -> rs | Error _ -> [])
                    strs r "needs" |> List.filter (fun cn -> not (owned.TryFind cn |> Option.exists (List.exists (fun x -> List.contains x reqs)))))
            check "no competition need is invented (each owns a requirement of a Rehearsal 010 need of its rule)" bad.IsEmpty (String.Join(", ", bad)) )
      // negative controls
      ( let broken = { i with Rules = i.Rules @ [ ([ "JX-01" ], "control", [ "SL-NOWHERE" ]) ] }
        check "negative control: an unknown need is refused" (match derive broken with Error e -> e |> List.exists (fun x -> x.Contains "SL-NOWHERE") | Ok _ -> false) "derived anyway" )
      ( let orphan = { i with Competition = i.Competition |> List.map (fun (id, rs) -> id, rs |> List.filter ((<>) "INV-004")) }
        check "negative control: a requirement no competition slice owns is refused" (match derive orphan with Error e -> e |> List.exists (fun x -> x.Contains "INV-004") | Ok _ -> false) "derived anyway" )
      ( let empty = { i with R10Requirements = i.R10Requirements.Add("SL-T14", []); Rules = i.Rules @ [ ([ "JX-02" ], "control", [ "SL-T14" ]) ] }
        check "negative control: a need with no requirement is refused" (match derive empty with Error e -> e |> List.exists (fun x -> x.Contains "SL-T14") | Ok _ -> false) "derived anyway" )
      ( let split = remap i "SL-WI-INV-WORKSPACE-FRAME"
        check "control: a split part maps to its parent's competition slice" (match split with Ok (t, _, f) -> t = [ "SL-WI-INV-WORKSPACE" ] && f = "work-item split part" | _ -> false) (sprintf "%A" split) )
      ( let kept = remap i "SL-T21"
        check "control: a slice the competition keeps with all its requirements maps to itself (not to T18/T20, which share some)" (match kept with Ok (t, _, _) -> t = [ "SL-T21" ] | _ -> false) (sprintf "%A" kept) ) ]

match args |> List.filter (fun a -> a <> "--indy-init" && a <> root) with
| "derive" :: _ ->
    match derive (load ()) with
    | Ok d ->
        let out = opt "--out" |> Option.defaultValue (at outRel)
        File.WriteAllText(out, render d)
        printfn "assertion-merge-needs: wrote %s" out
        exit 0
    | Error errors -> errors |> List.iter (eprintfn "  %s"); exit 3
| "test" :: _ ->
    let results = tests ()
    results |> List.iter (fun (n, r) -> match r with Ok () -> printfn "PASS %s" n | Error e -> printfn "FAIL %s\n     %s" n e)
    let failed = results |> List.filter (snd >> Result.isError) |> List.length
    printfn "assertion-merge-needs test: %d/%d passed" (results.Length - failed) results.Length
    exit (if failed = 0 then 0 else 3)
| _ -> eprintfn "usage: assertion-merge-needs.fsx -- --indy-init DIR derive [--out FILE] | test"; exit 2
