// Builds the frozen identity record of a rehearsal kickoff from its sources, so no release identity is typed by
// hand: every version, commit and digest below is read from a Registry file, a git object or a file's bytes.
//
// Usage:
//   dotnet fsi tools/rehearsal-freeze/freeze.fsx \
//     --indy-init DIR --registry DIR --registry-ref REF --set-version VER --platform RID \
//     --conditor-version VER --lock FILE --rehearsal DIR --rehearsal-ref REF \
//     --decisions FILE --out FILE
//
// Exit 0 when the record is written and every identity resolved; 1 when an identity is missing or inconsistent
// (the record is still written, with the problems listed, so a reviewer sees what failed).

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Nodes

let args =
    fsi.CommandLineArgs
    |> Array.toList
    |> List.tail
    |> List.chunkBySize 2
    |> List.choose (function
        | [ k: string; v ] when k.StartsWith "--" -> Some(k.Substring 2, v)
        | _ -> None)
    |> Map.ofList

let arg name =
    args |> Map.tryFind name |> Option.defaultWith (fun () -> failwith $"missing --{name}")

let sha256Hex (bytes: byte[]) =
    (SHA256.HashData bytes |> Convert.ToHexString).ToLowerInvariant()

/// Runs git and returns its stdout bytes, or the error text labelled with what was asked for.
let git (dir: string) (gitArgs: string list) : Result<byte[], string> =
    let info = ProcessStartInfo("git", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
    [ "-C"; dir ] @ gitArgs |> List.iter info.ArgumentList.Add
    use p = Process.Start info
    use out = new MemoryStream()
    p.StandardOutput.BaseStream.CopyTo out
    let err = p.StandardError.ReadToEnd()
    p.WaitForExit()
    if p.ExitCode = 0 then Ok(out.ToArray()) else Error $"git {String.Join(' ', gitArgs)}: {err.Trim()}"

let gitText dir gitArgs =
    git dir gitArgs |> Result.map (fun b -> Encoding.UTF8.GetString(b).Trim())

let showAt dir rev (path: string) = git dir [ "show"; $"{rev}:{path}" ]
let commitOf dir rev = gitText dir [ "rev-parse"; $"{rev}^{{commit}}" ]

let parse (bytes: byte[]) = JsonNode.Parse(Encoding.UTF8.GetString bytes)

let str (node: JsonNode) (name: string) =
    Option.ofObj node |> Option.bind (fun n -> Option.ofObj n[name]) |> Option.map string

let errorOf = function Ok _ -> None | Error e -> Some e

let jstr (s: string) = JsonValue.Create s :> JsonNode
let jopt = Option.map jstr >> Option.toObj
let jobj (pairs: (string * JsonNode) list) =
    pairs |> List.fold (fun (o: JsonObject) (k, v) -> o[k] <- v; o) (JsonObject()) :> JsonNode
let jarr (items: JsonNode list) =
    items |> List.fold (fun (a: JsonArray) i -> a.Add i; a) (JsonArray()) :> JsonNode

let indy, registry, registryRef = arg "indy-init", arg "registry", arg "registry-ref"
let setVersion, platform, conditorVersion = arg "set-version", arg "platform", arg "conditor-version"
let rehearsal, rehearsalRef = arg "rehearsal", arg "rehearsal-ref"

let indyCommit = commitOf indy "HEAD"
let registryCommit = commitOf registry registryRef
let rehearsalCommit = commitOf rehearsal rehearsalRef

// Requirements baseline: identity from the committed file, and each source document re-hashed at HEAD.
let baselineBytes = Path.Combine(indy, "requirements", "causa-requirements-baseline.json") |> File.ReadAllBytes
let baseline = parse baselineBytes
let documents =
    (baseline["source"]["documents"]).AsArray()
    |> Seq.map (fun d -> string (d["path"]), string (d["sha256"]))
    |> List.ofSeq
let changedDocuments =
    documents
    |> List.filter (fun (path, pinned) -> (Path.Combine(indy, path) |> File.ReadAllBytes |> sha256Hex) <> pinned)
    |> List.map fst

let manifestSha =
    Path.Combine(indy, "requirements-compiled", "v1", "package-manifest.json") |> File.ReadAllBytes |> sha256Hex

// Release set and its resolved channel for the platform, read at the Registry commit.
let setPath = $"sets/echelon-current/{setVersion}.json"
let channelPath = $"channels/echelon-current/{platform}.json"
let setRead = showAt registry registryRef setPath
let channelRead = showAt registry registryRef channelPath
let set = setRead |> Result.toOption |> Option.map parse
let channel = channelRead |> Result.toOption |> Option.map parse
let channelSet = channel |> Option.bind (fun c -> str (c["profile"]) "version")
let setStatus = set |> Option.bind (fun s -> str s "status")

let components =
    channel
    |> Option.map (fun c ->
        (c["components"]).AsArray()
        |> Seq.map (fun x ->
            jobj [ "systemId", jstr (string (x["systemId"]))
                   "version", jstr (string (x["version"]))
                   "role", jopt (str x "role")
                   "tag", jopt (str x "tag")
                   "commit", jopt (str x "commit")
                   "releaseManifestSha256", jopt (str (x["releaseManifest"]) "sha256") ])
        |> List.ofSeq)
    |> Option.defaultValue []

// Conditor: the Registry release record for the version used to bootstrap.
let conditorPath = $"releases/conditor/{conditorVersion}.release.json"
let conditorRead = showAt registry registryRef conditorPath
let conditorBinary =
    conditorRead
    |> Result.toOption
    |> Option.bind (fun b ->
        ((parse b)["artifacts"]).AsArray()
        |> Seq.tryFind (fun a -> str a "platform" = Some platform)
        |> Option.bind (fun a -> str a "sha256"))

// Repository lock produced by the bootstrap.
let lockBytes = File.ReadAllBytes(arg "lock")
let lockComponents =
    ((parse lockBytes)["components"]).AsArray()
    |> Seq.map (fun c -> jobj [ "id", jopt (str c "id" |> Option.orElse (str c "systemId")); "version", jopt (str c "version") ])
    |> List.ofSeq

// Tooling and CI identities from the rehearsal repository at the given ref.
let listing = gitText rehearsal [ "ls-tree"; "-r"; "--name-only"; rehearsalRef ]
let filesAt (prefixes: string list) =
    listing
    |> Result.map (fun t -> t.Split '\n' |> Array.filter (fun p -> prefixes |> List.exists p.StartsWith) |> Array.sort |> List.ofArray)
    |> Result.defaultValue []
    |> List.map (fun p -> p, showAt rehearsal rehearsalRef p |> Result.map sha256Hex)
let pipelineFiles = filesAt [ "tools/acceptance/"; "tools/acceptance-plan/"; "acceptance/" ]
let workflowFiles = filesAt [ ".github/workflows/" ]
let fileNodes = List.map (fun (p, h) -> jobj [ "path", jstr p; "sha256", jopt (Result.toOption h) ])

let verifierPath = "tools/compute-acceptance-verdict.py"
let verifierSha = Path.Combine(indy, verifierPath) |> File.ReadAllBytes |> sha256Hex

let problems =
    [ yield! [ indyCommit; registryCommit; rehearsalCommit; listing ] |> List.choose errorOf
      yield! [ setRead; channelRead; conditorRead ] |> List.choose errorOf
      yield! (pipelineFiles @ workflowFiles) |> List.choose (snd >> errorOf)
      yield! changedDocuments |> List.map (sprintf "baseline document changed since the pin: %s")
      if channelSet <> Some setVersion then yield $"{channelPath} resolves profile {channelSet}, not {setVersion}"
      if setStatus <> Some "approved" then yield $"{setPath} status is {setStatus}, not approved"
      if conditorBinary.IsNone then yield $"{conditorPath} has no {platform} executable digest" ]

let record =
    jobj [
        "schema", jstr "indy-init.rehearsal-freeze/1"
        "sources", jobj [
            "indyInit", jopt (Result.toOption indyCommit)
            "registry", jopt (Result.toOption registryCommit)
            "rehearsalTooling", jobj [ "ref", jstr rehearsalRef; "commit", jopt (Result.toOption rehearsalCommit) ] ]
        "requirementsBaseline", jobj [
            "id", jstr (string (baseline["id"]))
            "sourceCommit", jstr (string (baseline["source"]["commit"]))
            "sha256", jstr (sha256Hex baselineBytes)
            "documentsChecked", JsonValue.Create documents.Length
            "documentsChangedSincePin", jarr (changedDocuments |> List.map jstr) ]
        "compiledPackage", jobj [ "path", jstr "requirements-compiled/v1"; "packageManifestSha256", jstr manifestSha ]
        "releaseSet", jobj [
            "channel", jstr "echelon-current"
            "version", jstr setVersion
            "status", jopt setStatus
            "approvedBy", jopt (set |> Option.bind (fun s -> str (s["approval"]) "by"))
            "approvedAt", jopt (set |> Option.bind (fun s -> str (s["approval"]) "at"))
            "platform", jstr platform
            "channelFileSha256", jopt (channelRead |> Result.toOption |> Option.map sha256Hex)
            "components", jarr components ]
        "conditor", jobj [
            "version", jstr conditorVersion
            "registryRecord", jstr conditorPath
            "registryRecordSha256", jopt (conditorRead |> Result.toOption |> Option.map sha256Hex)
            "executableSha256", jopt conditorBinary ]
        "repositoryLock", jobj [ "sha256", jstr (sha256Hex lockBytes); "components", jarr lockComponents ]
        "acceptanceTooling", jobj [
            "verifier", jobj [ "repository", jstr "kemiller2002/indy-init"; "path", jstr verifierPath; "sha256", jstr verifierSha ]
            "rehearsalPipeline", jarr (fileNodes pipelineFiles) ]
        "ciWorkflows", jarr (fileNodes workflowFiles)
        "decisions", (File.ReadAllBytes(arg "decisions") |> parse)
        "problems", jarr (problems |> List.map jstr) ]

File.WriteAllText(arg "out", record.ToJsonString(JsonSerializerOptions(WriteIndented = true)) + "\n")
printfn "wrote %s (%d problem(s))" (arg "out") problems.Length
problems |> List.iter (printfn "  %s")
exit (if List.isEmpty problems then 0 else 1)
