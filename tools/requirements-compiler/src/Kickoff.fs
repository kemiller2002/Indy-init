/// The kickoff record binds a rehearsal's base commit (shared by `verify-defect-closures` and `assertion-ratchet`).
/// The base is read from the repository, never asserted: the record, only ever `kickoff/indy-init.kickoff.json` (a
/// caller-chosen path, such as an ROS execution envelope, would bind whatever commit added it), is located in the
/// candidate's first-parent history, and the commit that first added it is the base, unless the record as added there
/// names an earlier commit (`base`, `baseCommit`, `ratchetBase`, or a receipt's `initialRepository.head`) that is an
/// ancestor of that commit. Later edits of the record are not read.
///
/// Inherent limit: history rewritten before the run moves the base, unless the kickoff commit is pinned outside the
/// candidate (an owner record or tag).
module Kickoff

open System
open System.Text
open Json

/// The only kickoff record that binds the base. Any other path is refused: a caller could otherwise name any file
/// the history happens to add (an ROS envelope) and bind whatever base it was added at.
let defaultPath = "kickoff/indy-init.kickoff.json"

/// The base commit a kickoff record names, and the field it is in: `base`, `baseCommit`, `ratchetBase`, or a
/// kickoff receipt's `initialRepository.head`.
let kickoffBase (kickoff: Json) : (string * string) option =
    [ "base"; "baseCommit"; "ratchetBase" ]
    |> List.tryPick (fun k -> strOpt k kickoff |> Option.map (fun v -> k, v))
    |> Option.orElse (field "initialRepository" kickoff |> Option.bind (strOpt "head") |> Option.map (fun v -> "initialRepository.head", v))

let resolve (repo: string) (rev: string) =
    match Git.run repo [ "rev-parse"; "--verify"; "--quiet"; rev + "^{commit}" ] with
    | Ok bytes -> Some(Encoding.UTF8.GetString(bytes).Trim())
    | Error _ -> None

let isAncestor (repo: string) (ancestor: string) (descendant: string) =
    Git.run repo [ "merge-base"; "--is-ancestor"; ancestor; descendant ] |> Result.isOk

/// The kickoff record as the candidate's history holds it: the commit that first added `path` in the candidate's
/// first-parent history, the record as added, and the base it binds (that commit, or an ancestor the record names).
/// Error when the history holds no such record, or the record names a base that is not an ancestor of it.
type Record =
    { Path: string
      AddedAt: string
      Field: string option
      RecordedBase: string option
      BoundBase: string }

let private kickoffAt (repo: string) (candidate: string) (path: string) : Result<Record, string> =
    let added =
        match Git.run repo [ "log"; "--first-parent"; "--diff-filter=A"; "--format=%H"; candidate; "--"; path ] with
        | Ok bytes -> Encoding.UTF8.GetString(bytes).Split('\n', StringSplitOptions.RemoveEmptyEntries) |> Array.tryLast
        | Error _ -> None

    match added with
    | None -> Error(sprintf "no commit in %s's first-parent history adds the kickoff record %s" candidate path)
    | Some addedAt ->
        let record =
            Git.showFile repo addedAt path |> Result.toOption |> Option.bind (fun bytes -> try Some(parseBytes bytes) with _ -> None)

        match record with
        | None -> Error(sprintf "the kickoff record %s as added at %s is not JSON" path addedAt)
        | Some record ->
            match kickoffBase record with
            | None -> Ok { Path = path; AddedAt = addedAt; Field = None; RecordedBase = None; BoundBase = addedAt }
            | Some(key, value) ->
                match resolve repo value with
                | Some named when isAncestor repo named addedAt ->
                    Ok { Path = path; AddedAt = addedAt; Field = Some key; RecordedBase = Some value; BoundBase = named }
                | _ -> Error(sprintf "the kickoff record %s as added at %s names %s `%s`, which is not an ancestor of that commit" path addedAt key value)

/// The kickoff record of the candidate's history (see kickoffAt). Only defaultPath is read.
let find (repo: string) (candidate: string) (path: string) : Result<Record, string> =
    if path <> defaultPath then
        Error(sprintf "only %s binds the base; %s is not the kickoff record" defaultPath path)
    else
        kickoffAt repo candidate path


/// The base a run uses and whether the kickoff record binds it: the caller's `requested` base, else the bound one.
/// (base, the record or why there is none, baseVerified, the mismatch if any). The base falls back to `fallback` when
/// nothing names one, so a refused run can still report.
let bind (repo: string) (candidate: string) (path: string) (requested: string option) (fallback: string) =
    let record = find repo candidate path
    let bound = record |> Result.toOption |> Option.map _.BoundBase

    let baseCommit =
        match requested |> Option.map (fun r -> resolve repo r |> Option.defaultValue r), bound with
        | Some r, _ -> r
        | None, Some b -> b
        | None, None -> fallback

    let problem =
        match record with
        | Error e -> Some e
        | Ok k when k.BoundBase <> baseCommit -> Some(sprintf "--base %s is not the base %s the kickoff record binds" baseCommit k.BoundBase)
        | Ok _ -> None

    baseCommit, record, bound = Some baseCommit, problem

/// The kickoff record as a report shows it.
let toJson (path: string) (record: Result<Record, string>) =
    match record with
    | Ok k ->
        JObj
            [ "path", JStr path
              "addedAt", JStr k.AddedAt
              "field", jopt JStr k.Field
              "recordedBase", jopt JStr k.RecordedBase
              "base", JStr k.BoundBase ]
    | Error e -> JObj [ "path", JStr path; "problem", JStr e ]
