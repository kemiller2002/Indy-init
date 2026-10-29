/// Review records: what `verify-defect-closures` accepts as review evidence and `assertion-ratchet` accepts as the
/// review behind a tag change. Rehearsal 6's reviews (docs/r6/reviews/final-review.json, review-1.json) are
/// `indy-init.independent-review/1` documents naming the reviewer, the role, the candidate, the findings and a verdict;
/// the final review also records each defect class it checked (`defectClasses[]` with `defectClass`). A review record is
/// such a document, read from the repository at a commit, that is not shaped like the record it reviews: it carries no
/// closure fields (`reviewerEvidence`, `status`, `fixSha` beside `closedAt`) and no tag-change list (`changes`), so a
/// copy of the closures or tag-change record, however lightly edited, is not a review.
///
/// Inherent limit: the structure shows a document is shaped like a review, not who wrote it. A review the implementing
/// agent wrote itself in this shape is accepted; only reviewer identity (a signed review, a pull-request review fetched
/// by trusted CI) tells them apart.
module ReviewRecord

open System
open System.Text
open Json

let schema = "indy-init.independent-review/1"

/// A path as the repository names it: `.` segments and repeated slashes dropped, `..` collapsed. None for an absolute
/// path or one that leaves the repository root, which names no file of the repository.
let repositoryPath (path: string) : string option =
    if path.StartsWith("/", StringComparison.Ordinal) then
        None
    else
        path.Split('/')
        |> Array.fold
            (fun (acc: string list option) segment ->
                match acc, segment with
                | None, _ -> None
                | Some acc, ("" | ".") -> Some acc
                | Some [], ".." -> None
                | Some(_ :: rest), ".." -> Some rest
                | Some acc, segment -> Some(segment :: acc))
            (Some [])
        |> Option.filter (List.isEmpty >> not)
        |> Option.map (List.rev >> String.concat "/")

/// The repository path a reference (`path#anchor`) names.
let pathOf (reference: string) = repositoryPath (reference.Split('#').[0])

let private nonEmpty key (j: Json) =
    strOpt key j |> Option.exists (fun s -> s.Trim() <> "")

/// Whether any object in the document carries one of the closure record's own fields.
let rec private closureShaped (j: Json) =
    match j with
    | JObj props ->
        let keys = props |> List.map fst |> Set.ofList
        keys.Contains "reviewerEvidence" || (keys.Contains "status" && keys.Contains "closedAt") || (props |> List.exists (snd >> closureShaped))
    | JArr items -> items |> List.exists closureShaped
    | _ -> false

/// Why a document is not a review record (empty when it is one).
let problems (doc: Json) : string list =
    [ if strOpt "schema" doc <> Some schema then
          yield sprintf "its schema is %s, not %s" (strOpt "schema" doc |> Option.defaultValue "(none)") schema
      if not (nonEmpty "reviewer" doc) then
          yield "it names no reviewer"
      if not (nonEmpty "verdict" doc) then
          yield "it records no verdict"
      if closureShaped doc then
          yield "it is shaped like a defect-closures record (reviewerEvidence, or status with closedAt)"
      if (field "changes" doc).IsSome then
          yield "it is shaped like a review tag-change record (changes)" ]

/// The review record a reference names at `commit`: its repository path and document, or why it is not one. `self`
/// is the path of the record under review, which cannot be its own review.
let read (repo: string) (commit: string) (self: string list) (reference: string) : Result<string * Json, string> =
    match pathOf reference with
    | None -> Error(sprintf "%s does not name a path inside the repository" reference)
    | Some path when self |> List.choose repositoryPath |> List.contains path -> Error(sprintf "%s is the record under review itself" reference)
    | Some path ->
        match Git.showFile repo commit path with
        | Error _ -> Error(sprintf "%s does not exist at %s" reference commit)
        | Ok bytes ->
            match (try Ok(parseBytes bytes) with ex -> Error ex.Message) with
            | Error _ -> Error(sprintf "%s is not a review record: it is not JSON" reference)
            | Ok doc ->
                match problems doc with
                | [] -> Ok(path, doc)
                | ps -> Error(sprintf "%s is not a review record: %s" reference (String.Join("; ", ps)))
