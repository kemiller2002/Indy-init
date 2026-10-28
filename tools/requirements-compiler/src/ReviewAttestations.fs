/// Owner and reviewer attestations from GitHub pull-request reviews (Rehearsal 6 readiness, ESO-R6-05).
///
/// Pure. The acceptance job fetches every pull request's reviews from the GitHub API in its own run and passes them
/// here; this module turns them into `indy-init.owner-attestations/1` for `verify-process`. It keeps every review that
/// changes approval state (APPROVED, CHANGES_REQUESTED, DISMISSED); a COMMENTED or PENDING review changes nothing on
/// GitHub and is dropped. Decisions are read from the review body, one per line:
///
///     Decision: DEC-R6-KICKOFF
///     Decision: DEC-R6-007 paths: src/engine/Replay.fs, AR-024
///     Decision: final-disposition
///
/// A decision without `paths:` covers no path. Nothing here decides what a review proves; the verifier does that
/// against its frozen rules, and trusts this file only inside the run that produced it.
module ReviewAttestations

open System
open System.Text.RegularExpressions
open Json

let schemaId = "indy-init.owner-attestations/1"

/// The review states that change a pull request's approval.
let attestedStates = set [ "APPROVED"; "CHANGES_REQUESTED"; "DISMISSED" ]

let private decisionLine =
    Regex(@"^\s*Decision:\s*(?<id>[A-Za-z0-9][A-Za-z0-9._-]*)(?:\s+paths?:\s*(?<paths>.+?))?\s*$", RegexOptions.IgnoreCase ||| RegexOptions.Multiline)

/// The decisions a review body declares, in order: a bare id, or `{id, paths}`.
let decisionsOf (body: string) : Json list =
    decisionLine.Matches(body)
    |> Seq.map (fun m ->
        let id = m.Groups.["id"].Value
        match m.Groups.["paths"] with
        | g when g.Success ->
            let paths = g.Value.Split(',') |> Array.map (fun p -> p.Trim()) |> Array.filter (String.IsNullOrEmpty >> not) |> List.ofArray
            JObj [ "id", JStr id; "paths", jstrs paths ]
        | _ -> JStr id)
    |> List.ofSeq

let private userLogin (review: Json) = field "user" review |> Option.bind (strOpt "login")

/// One attestation per state-changing review. `repository` is OWNER/REPO; a review's reference is
/// `OWNER/REPO#PULL@REVIEW-ID`.
let attestationOf (repository: string) (pull: int64) (review: Json) : Json option =
    match strOpt "state" review, userLogin review, field "id" review with
    | Some state, Some login, Some(JInt id) when attestedStates.Contains state ->
        Some(
            JObj
                [ "type", JStr "github-review"
                  "reference", JStr(sprintf "%s#%d@%d" repository pull id)
                  "login", JStr login
                  "state", JStr state
                  "decisions", JArr(decisionsOf (strOpt "body" review |> Option.defaultValue ""))
                  "commit", jopt JStr (strOpt "commit_id" review)
                  "submittedAt", jopt JStr (strOpt "submitted_at" review) ]
        )
    | _ -> None

/// `pulls` is `[{number, reviews: [GitHub review objects]}]`, as the acceptance job writes it.
let build (repository: string) (runId: string) (candidate: string) (pulls: Json) : Json =
    let items = match pulls with JArr xs -> xs | _ -> []
    let attestations =
        items
        |> List.collect (fun p ->
            match field "number" p with
            | Some(JInt n) -> arr "reviews" p |> List.choose (attestationOf repository n)
            | _ -> [])
        |> List.sortBy (fun a -> strOpt "submittedAt" a |> Option.defaultValue "", str "reference" a)
    JObj
        [ "schema", JStr schemaId
          "repository", JStr repository
          "runId", JStr runId
          "candidate", JStr candidate
          "attestations", JArr attestations ]
