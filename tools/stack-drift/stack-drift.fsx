// stack-drift.fsx: Echelon stack version-drift preflight (PROPOSED; not part of any governed gate until ODR-ESU-04).
//
//   dotnet fsi tools/stack-drift/stack-drift.fsx -- lock  --source SRC.json --registry DIR [--registry-commit SHA] [--out FILE]
//        Generates an indy-init.v4-dependency-lock/1 lock from a lock source and echelon-registry at a commit, with the
//        algorithm of requirements-compiler's V4Package.dependencyLock (the frozen compiler is not edited; the test
//        command proves this generator reproduces the frozen v4 lock from the v4 source).
//
//   dotnet fsi tools/stack-drift/stack-drift.fsx -- check --lock LOCK.json [--registry DIR] [--toolchain-lock FILE]
//        [--workspace DIR]... [--sdk-root DIR] [--bin DIR] [--conditor FILE] [--artifacts DIR | --fetch DIR] [--json FILE]
//        Compares the lock with the registry, the toolchain lock, every workspace's manifests and workflows, the pinned
//        and installed .NET SDK, the installed Echelon executables, the Conditor binary and the downloaded artifacts.
//
//   dotnet fsi tools/stack-drift/stack-drift.fsx -- test [--registry DIR]
//        Self-tests with negative controls.
//
// Statuses: PASS (matches the lock), DRIFT (a version differs from the lock), BLOCKED (missing or incompatible),
// UNVERIFIED (version or provenance cannot be established). A required system that is not PASS makes the stack not
// ready (exit 3). Nothing is substituted: the tool only reports. Exit 0 ready, 3 not ready, 2 bad invocation.
// Every function except the IO gatherers and the command dispatch at the bottom is pure over its inputs.

open System
open System.IO
open System.Diagnostics
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.RegularExpressions

// ---------------------------------------------------------------- arguments

let argv = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")
let opt name = argv |> List.pairwise |> List.tryFind (fst >> (=) name) |> Option.map snd
let opts name = argv |> List.pairwise |> List.filter (fst >> (=) name) |> List.map snd
let flag name = argv |> List.contains name

// ---------------------------------------------------------------- json helpers

let str (n: JsonNode) (k: string) = match n with null -> "" | n -> match n[k] with null -> "" | v -> v.ToString()
let strOpt (n: JsonNode) (k: string) = match str n k with "" -> None | s -> Some s
let field (n: JsonNode) (k: string) : JsonNode option = match n with null -> None | n -> Option.ofObj n[k]
let arr (n: JsonNode) (k: string) = match n with null -> [] | n -> match n[k] with :? JsonArray as a -> a |> Seq.toList | _ -> []
let boolOr (d: bool) (n: JsonNode) (k: string) =
    match n with
    | null -> d
    | n -> match n[k] with null -> d | v -> (try v.GetValue<bool>() with _ -> d)
let parse (bytes: byte[]) = JsonNode.Parse(Encoding.UTF8.GetString bytes)
let parseText (text: string) = JsonNode.Parse text
let clone (n: JsonNode) = match n with null -> null | n -> n.DeepClone()
let jobj (pairs: (string * JsonNode) list) =
    let o = JsonObject()
    pairs |> List.iter (fun (k, v) -> o[k] <- v)
    o :> JsonNode
let jstr (s: string) = JsonValue.Create s :> JsonNode
let jbool (b: bool) = JsonValue.Create b :> JsonNode
let jarr (xs: JsonNode list) = JsonArray(xs |> List.toArray) :> JsonNode
let render (n: JsonNode) =
    n.ToJsonString(JsonSerializerOptions(WriteIndented = true, Encoder = Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping))
let sameJson (a: JsonNode) (b: JsonNode) = JsonNode.DeepEquals(a, b)

let sha256Hex (bytes: byte[]) = SHA256.HashData bytes |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()

// ---------------------------------------------------------------- versions

let semver = Regex(@"(?<![0-9.])(\d+\.\d+\.\d+)(?![0-9])")
let floatingSpec = Regex(@"^\s*([\^~<>=*]|latest|next|x$)|\.x\b|\*")

/// The exact version a dependency spec pins, or why it pins none: "0.13.0", or a release-asset URL ending
/// "-0.13.0.tgz", pins; "^0.13.0", "0.13.x", "latest" float.
let pinnedVersion (spec: string) : Result<string, string> =
    if floatingSpec.IsMatch spec then Error(sprintf "floating specifier '%s'" spec)
    else
        let tgz = Regex.Match(spec, @"-(\d+\.\d+\.\d+)\.tgz$")
        if tgz.Success then Ok tgz.Groups[1].Value
        else
            let m = Regex.Match(spec.Trim(), @"^v?(\d+\.\d+\.\d+)$")
            if m.Success then Ok m.Groups[1].Value else Error(sprintf "unrecognized specifier '%s'" spec)

/// The first semantic version in a tool's --version output (praxis "praxis 3.25.0", ordo "1.20.0",
/// percepta "percepta 0.4.0+sha", dokimos JSON with "ReleaseVersion").
let versionFromOutput (output: string) : string option =
    let json =
        try
            match parseText output with
            | null -> None
            | n -> strOpt n "ReleaseVersion"
        with _ -> None
    match json with
    | Some v -> Some v
    | None ->
        let m = semver.Match output
        if m.Success then Some m.Groups[1].Value else None

// ---------------------------------------------------------------- lock generation (mirror of V4Package.dependencyLock)

/// read: a file of echelon-registry at the pinned commit. Returns the lock and the problems found.
let buildLock (read: string -> byte[] option) (registryCommit: string) (source: JsonNode) : JsonNode * string list =
    let set = field source "releaseSet" |> Option.toObj
    let setPath = str set "path"
    let setBytes = read setPath
    let setDoc = setBytes |> Option.map parse |> Option.toObj
    let components = arr setDoc "components" |> List.map (fun c -> str c "systemId", c) |> Map.ofList
    let extensions (s: JsonNode) =
        [ "classification"; "required"; "executable"; "npmPackage"; "nugetPackage" ]
        |> List.choose (fun k -> field s k |> Option.map (fun v -> k, clone v))
    let entry (s: JsonNode) : JsonNode * string list =
        let id = str s "id"
        match str s "kind" with
        | "registry-component" ->
            let sid = str s "systemId"
            match components.TryFind sid with
            | None -> jobj [ "id", jstr id; "status", jstr "not-in-release-set" ], [ sprintf "dependency lock: %s is not a component of %s" sid setPath ]
            | Some c ->
                let version = str c "version"
                let manifestPath = sprintf "releases/%s/%s.release.json" sid version
                match read manifestPath with
                | None -> jobj [ "id", jstr id; "status", jstr "release-manifest-missing" ], [ sprintf "dependency lock: %s is unreadable at %s" manifestPath registryCommit ]
                | Some bytes ->
                    let m = parse bytes
                    let sha = sha256Hex bytes
                    let problems =
                        [ if sha <> str c "releaseManifestSha256" then sprintf "dependency lock: %s hashes to %s, the set records %s" manifestPath sha (str c "releaseManifestSha256")
                          if str m "version" <> version then sprintf "dependency lock: %s declares version %s" manifestPath (str m "version")
                          if str m "releaseStage" <> "stable" then sprintf "dependency lock: %s %s is %s, not stable (VER-005)" sid version (str m "releaseStage") ]
                    let package = arr m "artifacts" |> List.tryFind (fun a -> str a "purpose" = "package")
                    jobj (
                        [ "id", jstr id
                          "systemId", jstr sid
                          "version", jstr version
                          "kind", jstr "registry-component"
                          "use", jstr (str s "use")
                          "repository", jstr (str m "repository")
                          "tag", jstr (str m "tag")
                          "commit", jstr (str m "commit")
                          "releaseStage", jstr (str m "releaseStage")
                          "distributionClass", jstr (str m "distributionClass")
                          "distributions", jarr (arr m "distributions" |> List.map clone)
                          "packageDigest", (package |> Option.map (fun a -> jobj [ "artifact", jstr (str a "name"); "sha256", jstr (str a "sha256") ]) |> Option.toObj)
                          "releaseManifest", jobj [ "path", jstr manifestPath; "sha256", jstr sha ]
                          "runtimeCompatibility", jstr (str s "runtimeCompatibility")
                          "availability", jstr (if problems.IsEmpty then "released-stable-in-approved-set" else "unverified") ]
                        @ extensions s),
                    problems
        | kind ->
            jobj (
                [ "id", jstr id; "kind", jstr kind; "version", jstr (str s "version"); "use", jstr (str s "use"); "runtimeCompatibility", jstr (str s "runtimeCompatibility") ]
                @ ([ "source"; "repo"; "tag"; "commit"; "path"; "sha256" ] |> List.choose (fun k -> strOpt s k |> Option.map (fun v -> k, jstr v)))
                @ [ ("availability",
                    jstr (
                        match kind with
                        | "sdk" -> "pinned by global.json"
                        | "external-api" -> "external service; verified live by the provider-conformance task"
                        | _ -> "pinned by tag or commit; identity checked where a checkout is supplied")) ]
                @ extensions s),
            []
    let built = arr source "systems" |> List.map entry
    let setProblems =
        [ if setBytes.IsNone then sprintf "dependency lock: %s is unreadable at %s" setPath registryCommit
          if setBytes.IsSome && str setDoc "status" <> "approved" then sprintf "dependency lock: %s is %s, not approved" setPath (str setDoc "status") ]
    let forbidden = arr source "forbiddenFrameworks" |> List.map (fun s -> s.ToString().ToLowerInvariant()) |> Set.ofList
    let frameworkProblems =
        arr source "systems" |> List.map (fun s -> str s "id") |> List.filter (fun s -> forbidden.Contains(s.ToLowerInvariant())) |> List.map (sprintf "dependency lock: %s is a forbidden framework")
    let lock =
        jobj (
            [ "schema", jstr "indy-init.v4-dependency-lock/1"
              "description", jstr (str source "description")
              "releaseSet",
              jobj
                  [ "channel", jstr (str set "channel")
                    "version", jstr (str set "version")
                    "registry", jstr "kemiller2002/echelon-registry"
                    "registryCommit", jstr registryCommit
                    "path", jstr setPath
                    "sha256", (setBytes |> Option.map (sha256Hex >> jstr) |> Option.toObj)
                    "status", jstr (str setDoc "status")
                    "decision", jstr (str set "decision") ]
              "approvedStack", jarr (arr source "approvedStack" |> List.map clone)
              "forbiddenFrameworks", jarr (arr source "forbiddenFrameworks" |> List.map clone)
              "interopBoundaries", jarr (arr source "interopBoundaries" |> List.map clone)
              "systems", jarr (built |> List.map fst) ]
            @ (field source "excluded" |> Option.map (fun e -> [ ("excluded", clone e) ]) |> Option.defaultValue []))
    lock, setProblems @ frameworkProblems @ (built |> List.collect snd)

// ---------------------------------------------------------------- findings model

type Status =
    | Pass
    | Drift
    | Blocked
    | Unverified

let statusText = function Pass -> "PASS" | Drift -> "DRIFT" | Blocked -> "BLOCKED" | Unverified -> "UNVERIFIED"

type Finding =
    { System: string
      Check: string
      Expected: string
      Actual: string
      Status: Status
      Required: bool
      Correction: string }

let finding system check expected actual status required correction =
    { System = system; Check = check; Expected = expected; Actual = actual; Status = status; Required = required; Correction = correction }

/// One system of the lock, as the checks need it.
type LockedSystem =
    { Id: string
      Kind: string
      Version: string
      Required: bool
      Executable: string option
      NpmPackage: string option
      NugetPackage: string option
      Repository: string
      Tag: string
      ReleaseManifestPath: string
      ReleaseManifestSha256: string
      Artifact: (string * string) option
      Distributions: (string * string) list }

/// Locks without explicit package fields (the frozen v4 lock) name the npm package in their distributions.
let npmPackageOf (s: JsonNode) =
    arr s "distributions" |> List.map (fun d -> str d "package") |> List.tryFind (fun p -> p.StartsWith "@echelon-foundry/")

/// ... and the NuGet package in a nuget distribution or the .nupkg artifact name.
let nugetPackageOf (s: JsonNode) =
    let fromDistribution = arr s "distributions" |> List.tryFind (fun d -> str d "mechanism" = "nuget") |> Option.bind (fun d -> strOpt d "package")
    let fromArtifact =
        field s "packageDigest"
        |> Option.map (fun d -> Regex.Match(str d "artifact", @"^(.+?)\.\d+\.\d+\.\d+\.nupkg$"))
        |> Option.filter (fun m -> m.Success)
        |> Option.map (fun m -> m.Groups[1].Value)
    fromDistribution |> Option.orElse fromArtifact

let systemsOf (lock: JsonNode) : LockedSystem list =
    arr lock "systems"
    |> List.map (fun s ->
        { Id = str s "id"
          Kind = str s "kind"
          Version = str s "version"
          Required = boolOr true s "required"
          Executable = strOpt s "executable"
          NpmPackage = strOpt s "npmPackage" |> Option.orElse (npmPackageOf s)
          NugetPackage = strOpt s "nugetPackage" |> Option.orElse (nugetPackageOf s)
          Repository = str s "repository"
          Tag = str s "tag"
          ReleaseManifestPath = field s "releaseManifest" |> Option.map (fun m -> str m "path") |> Option.defaultValue ""
          ReleaseManifestSha256 = field s "releaseManifest" |> Option.map (fun m -> str m "sha256") |> Option.defaultValue ""
          Artifact = field s "packageDigest" |> Option.map (fun d -> str d "artifact", str d "sha256")
          Distributions = arr s "distributions" |> List.map (fun d -> str d "mechanism", str d "url") })

// ---------------------------------------------------------------- observations (gathered by IO, judged purely)

type SetupDotnet =
    | FloatingVersion of string
    | ExactVersion of string
    | GlobalJsonFile

type WorkspaceFacts =
    { Root: string
      GlobalJson: (string * string) option // version, rollForward
      NpmDeclared: (string * string * string) list // file, package, spec
      NpmInstalled: (string * string * string) list // file, package, version
      ConditorComponents: (string * string * string) list // file, id, version
      NugetReferences: (string * string * string) list // file, package, version
      WorkflowPins: (string * int * string * string) list // file, line, package, version
      SetupDotnetSteps: (string * int * SetupDotnet) list } // file, line, how

type Observations =
    { Registry: (string -> byte[] option) option
      ToolchainLock: JsonNode option
      Workspaces: WorkspaceFacts list
      PinnedSdk: (string * string) option // the repository's global.json
      InstalledSdk: Result<string, string>
      Tools: Map<string, Result<string, string>> // executable -> version or why none
      ConditorSha256: Result<string, string> option
      Platform: string
      Artifacts: Map<string, Result<string, string>> } // artifact -> sha256 or why none

// ---------------------------------------------------------------- pure checks

let checkRegistry (lock: JsonNode) (obs: Observations) : Finding list =
    let set = field lock "releaseSet" |> Option.toObj
    let systems = systemsOf lock |> List.filter (fun s -> s.Kind = "registry-component")
    match obs.Registry with
    | None ->
        systems
        |> List.map (fun s ->
            finding s.Id "release manifest in echelon-registry" (sprintf "%s %s" s.ReleaseManifestPath s.ReleaseManifestSha256) "no registry checkout supplied" Unverified s.Required
                "pass --registry <echelon-registry checkout at the lock's registryCommit>")
    | Some read ->
        let setBytes = read (str set "path")
        let setDoc = setBytes |> Option.map parse |> Option.toObj
        let setVersions = arr setDoc "components" |> List.map (fun c -> str c "systemId", str c "version") |> Map.ofList
        let setFinding =
            match setBytes with
            | None -> finding "echelon-current" "release set" (str set "path") "unreadable" Blocked true "check out echelon-registry at the lock's registryCommit"
            | Some b when sha256Hex b <> str set "sha256" ->
                finding "echelon-current" "release set digest" (str set "sha256") (sha256Hex b) Drift true "regenerate the lock from the approved set (stack-drift lock)"
            | Some _ when str setDoc "status" <> "approved" ->
                finding "echelon-current" "release set status" "approved" (str setDoc "status") Blocked true "use an approved set"
            | Some _ -> finding "echelon-current" "release set" (sprintf "%s %s" (str set "version") (str set "sha256")) "match" Pass true ""
        let systemFinding (s: LockedSystem) =
            let setVersion = setVersions.TryFind s.Id |> Option.defaultValue "absent"
            match read s.ReleaseManifestPath with
            | _ when setVersion <> s.Version ->
                finding s.Id "lock version equals the set version" s.Version setVersion Drift s.Required "regenerate the lock from the approved set (stack-drift lock)"
            | None -> finding s.Id "release manifest" s.ReleaseManifestPath "unreadable" Unverified s.Required "check out echelon-registry at the lock's registryCommit"
            | Some b when sha256Hex b <> s.ReleaseManifestSha256 ->
                finding s.Id "release manifest digest" s.ReleaseManifestSha256 (sha256Hex b) Drift s.Required "regenerate the lock; a release manifest is immutable, so investigate the registry change"
            | Some b when str (parse b) "releaseStage" <> "stable" ->
                finding s.Id "release stage" "stable" (str (parse b) "releaseStage") Blocked s.Required "select a stable release"
            | Some _ -> finding s.Id "release manifest" (sprintf "%s %s" s.Version s.ReleaseManifestSha256) "match" Pass s.Required ""
        setFinding :: (systems |> List.map systemFinding)

let checkToolchainLock (lock: JsonNode) (obs: Observations) : Finding list =
    match obs.ToolchainLock with
    | None -> []
    | Some t ->
        let set = field lock "releaseSet" |> Option.toObj
        let rs = field t "resolvedSet" |> Option.toObj
        let byId = systemsOf lock |> List.map (fun s -> s.Id, s) |> Map.ofList
        let agree system what expected actual required =
            if expected = actual then finding system what expected actual Pass required ""
            else finding system what expected actual Drift required "make the toolchain lock and the dependency lock name the same release (owner decision process)"
        let expectedTools =
            match field rs "expectedTools" with
            | Some (:? JsonObject as o) -> o |> Seq.map (fun kv -> kv.Key, kv.Value.ToString()) |> List.ofSeq
            | _ -> []
        [ agree "echelon-current" "toolchain lock profile version" (str set "version") (str rs "profileVersion") true
          agree "echelon-current" "toolchain lock registry commit" (str set "registryCommit") (str rs "commit") true
          yield!
              expectedTools
              |> List.map (fun (id, v) ->
                  match byId.TryFind id with
                  | Some s -> agree id "toolchain lock expected tool" s.Version v s.Required
                  | None -> finding id "toolchain lock expected tool" "a system of the dependency lock" v Blocked true "add the system to the dependency lock source or remove it from the toolchain lock")
          match byId.TryFind "conditor", field t "conditor" with
          | Some c, Some tc -> agree "conditor" "toolchain lock Conditor" c.Version (str tc "version") true
          | _ -> () ]

let checkSdk (lock: JsonNode) (obs: Observations) : Finding list =
    match systemsOf lock |> List.tryFind (fun s -> s.Kind = "sdk") with
    | None -> []
    | Some sdk ->
        let pinned =
            match obs.PinnedSdk with
            | None -> finding "dotnet-sdk" "global.json" sdk.Version "no global.json" Blocked true "add global.json with the locked SDK and rollForward disable"
            | Some (v, _) when v <> sdk.Version -> finding "dotnet-sdk" "global.json" sdk.Version v Drift true "pin global.json to the locked SDK"
            | Some (_, roll) when roll <> "disable" -> finding "dotnet-sdk" "global.json rollForward" "disable" roll Drift true "set rollForward to disable"
            | Some (v, _) -> finding "dotnet-sdk" "global.json" sdk.Version v Pass true ""
        let installed =
            match obs.InstalledSdk with
            | Error why -> finding "dotnet-sdk" "installed SDK (dotnet --version)" sdk.Version why Blocked true (sprintf "install .NET SDK %s (dotnet-install.sh --version %s)" sdk.Version sdk.Version)
            | Ok v when v <> sdk.Version -> finding "dotnet-sdk" "installed SDK (dotnet --version)" sdk.Version v Drift true (sprintf "install .NET SDK %s; global.json refuses to roll forward" sdk.Version)
            | Ok v -> finding "dotnet-sdk" "installed SDK (dotnet --version)" sdk.Version v Pass true ""
        [ pinned; installed ]

let checkTools (lock: JsonNode) (obs: Observations) : Finding list =
    systemsOf lock
    |> List.choose (fun s -> s.Executable |> Option.map (fun e -> s, e))
    |> List.map (fun (s, exe) ->
        match obs.Tools.TryFind exe with
        | None -> finding s.Id (sprintf "installed %s" exe) s.Version "not examined" Unverified s.Required "pass --bin <directory of the installed Echelon executables>"
        | Some (Error why) when why.StartsWith "not found" -> finding s.Id (sprintf "installed %s" exe) s.Version why Blocked s.Required "install the locked set (r9.fsx workstation --lock <toolchain lock>)"
        | Some (Error why) -> finding s.Id (sprintf "installed %s" exe) s.Version why Unverified s.Required "the executable did not report a version; reinstall the locked release"
        | Some (Ok v) when v <> s.Version -> finding s.Id (sprintf "installed %s" exe) s.Version v Drift s.Required "install the locked set; do not use another version"
        | Some (Ok v) -> finding s.Id (sprintf "installed %s" exe) s.Version v Pass s.Required "")

let checkConditorBinary (obs: Observations) : Finding list =
    match obs.ConditorSha256, obs.ToolchainLock with
    | None, _ -> []
    | Some _, None -> [ finding "conditor" "binary digest" "toolchain lock artifact digest" "no toolchain lock supplied" Unverified true "pass --toolchain-lock" ]
    | Some actual, Some t ->
        let expected = field t "conditor" |> Option.bind (fun c -> field c "artifacts") |> Option.bind (fun a -> field a obs.Platform) |> Option.map (fun a -> str a "sha256")
        match expected, actual with
        | None, _ -> [ finding "conditor" "binary digest" (sprintf "an artifact for %s" obs.Platform) "the toolchain lock has none" Unverified true "pin this platform's Conditor artifact" ]
        | Some _, Error why -> [ finding "conditor" "binary digest" "installed Conditor" why Blocked true "r9.fsx install-conditor --lock <toolchain lock>" ]
        | Some e, Ok a when e <> a -> [ finding "conditor" "binary digest" e a Drift true "reinstall Conditor with r9.fsx install-conditor --lock <toolchain lock>" ]
        | Some e, Ok a -> [ finding "conditor" "binary digest" e a Pass true "" ]

let checkArtifacts (lock: JsonNode) (obs: Observations) : Finding list =
    if obs.Artifacts.IsEmpty then []
    else
        systemsOf lock
        |> List.choose (fun s -> s.Artifact |> Option.map (fun a -> s, a))
        |> List.map (fun (s, (name, expected)) ->
            match obs.Artifacts.TryFind name with
            | None -> finding s.Id (sprintf "artifact %s" name) expected "not obtained" Unverified s.Required "pass --artifacts DIR holding it, or --fetch DIR"
            | Some (Error why) -> finding s.Id (sprintf "artifact %s" name) expected why Unverified s.Required "the published artifact could not be obtained; provenance is not established"
            | Some (Ok actual) when actual <> expected -> finding s.Id (sprintf "artifact %s" name) expected actual Blocked s.Required "the artifact differs from the registry's digest: do not use it"
            | Some (Ok actual) -> finding s.Id (sprintf "artifact %s" name) expected actual Pass s.Required "")

let checkWorkspace (lock: JsonNode) (w: WorkspaceFacts) : Finding list =
    let systems = systemsOf lock
    let byNpm = systems |> List.choose (fun s -> s.NpmPackage |> Option.map (fun p -> p, s)) |> Map.ofList
    let byId = systems |> List.map (fun s -> s.Id, s) |> Map.ofList
    let nugetOwner (package: string) =
        systems
        |> List.tryFind (fun s ->
            match s.NugetPackage with
            | Some p when p = package -> true
            | Some p -> p.StartsWith "EchelonFoundry.Limen." && package.StartsWith "EchelonFoundry.Limen."
            | None -> false)
    let rel (f: string) = Path.GetRelativePath(w.Root, f).Replace('\\', '/')
    let where (f: string) = sprintf "%s: %s" (Path.GetFileName w.Root) (rel f)
    let versionFinding (s: LockedSystem) check actual =
        if actual = s.Version then finding s.Id check s.Version actual Pass s.Required ""
        else finding s.Id check s.Version actual Drift s.Required (sprintf "pin %s to %s, the locked version" s.Id s.Version)
    let sdk = systems |> List.tryFind (fun s -> s.Kind = "sdk")
    [ yield!
          w.NpmDeclared
          |> List.choose (fun (f, package, spec) ->
              byNpm.TryFind package
              |> Option.map (fun s ->
                  match pinnedVersion spec with
                  | Ok v -> versionFinding s (sprintf "declared in %s" (where f)) v
                  | Error why -> finding s.Id (sprintf "declared in %s" (where f)) s.Version why Drift s.Required (sprintf "pin exactly %s" s.Version)))
      yield!
          w.NpmDeclared
          |> List.filter (fun (_, package, _) -> package.StartsWith "@echelon-foundry/" && not (byNpm.ContainsKey package))
          |> List.map (fun (f, package, spec) ->
              finding package (sprintf "declared in %s" (where f)) "a package of the lock" spec Blocked true "an Echelon package outside the approved lock: add it through the owner decision process or remove it")
      yield!
          w.NpmInstalled
          |> List.choose (fun (f, package, v) -> byNpm.TryFind package |> Option.map (fun s -> versionFinding s (sprintf "installed in %s" (where f)) v))
      yield!
          w.ConditorComponents
          |> List.choose (fun (f, id, v) -> byId.TryFind id |> Option.map (fun s -> versionFinding s (sprintf "component in %s" (where f)) v))
      yield!
          w.NugetReferences
          |> List.map (fun (f, package, v) ->
              match nugetOwner package with
              | Some s -> versionFinding s (sprintf "%s in %s" package (where f)) v
              | None -> finding package (sprintf "referenced in %s" (where f)) "a package of the lock" v Blocked true "an Echelon package outside the approved lock: add it through the owner decision process or remove it")
      yield!
          w.WorkflowPins
          |> List.choose (fun (f, line, package, v) -> byNpm.TryFind package |> Option.map (fun s -> versionFinding s (sprintf "pinned in %s:%d" (where f) line) v))
      match w.GlobalJson, sdk with
      | Some (v, _), Some s when v <> s.Version -> finding "dotnet-sdk" (sprintf "global.json in %s" (Path.GetFileName w.Root)) s.Version v Drift true "pin the locked SDK"
      | _ -> ()
      yield!
          w.SetupDotnetSteps
          |> List.map (fun (f, line, how) ->
              let check = sprintf "setup-dotnet in %s:%d" (where f) line
              match how, w.GlobalJson, sdk with
              | GlobalJsonFile, Some _, _ -> finding "dotnet-sdk" check "global-json-file" "global-json-file" Pass true ""
              | GlobalJsonFile, None, _ -> finding "dotnet-sdk" check "a global.json to read" "no global.json" Blocked true "add global.json"
              | ExactVersion v, _, Some s when v = s.Version -> finding "dotnet-sdk" check s.Version v Pass true ""
              | (ExactVersion v | FloatingVersion v), Some (pinned, "disable"), _ ->
                  finding "dotnet-sdk" check (sprintf "the SDK global.json pins (%s, rollForward disable)" pinned) v Drift true
                      "use `global-json-file: global.json` (HF-029: R8 and R9 CI failed on the runner's newer 10.0.x)"
              | FloatingVersion v, _, _ -> finding "dotnet-sdk" check "an exact SDK" v Drift true "pin the SDK (global.json and global-json-file)"
              | ExactVersion v, _, Some s -> finding "dotnet-sdk" check s.Version v Drift true "install the locked SDK"
              | ExactVersion v, _, None -> finding "dotnet-sdk" check "the locked SDK" v Unverified true "lock the SDK") ]

let evaluate (lock: JsonNode) (obs: Observations) : Finding list =
    checkRegistry lock obs
    @ checkToolchainLock lock obs
    @ checkSdk lock obs
    @ checkTools lock obs
    @ checkConditorBinary obs
    @ checkArtifacts lock obs
    @ (obs.Workspaces |> List.collect (checkWorkspace lock))

let ready (findings: Finding list) = findings |> List.forall (fun f -> not f.Required || f.Status = Pass)

let summary (findings: Finding list) =
    [ Pass; Drift; Blocked; Unverified ] |> List.map (fun s -> s, findings |> List.filter (fun f -> f.Status = s) |> List.length)

let reportText (findings: Finding list) =
    let line f =
        sprintf "%-10s %-18s %-58s expected %s; actual %s%s%s" (statusText f.Status) f.System f.Check f.Expected f.Actual
            (if f.Required then "" else " (optional)")
            (if f.Correction = "" then "" else sprintf "\n           -> %s" f.Correction)
    let counts = summary findings |> List.map (fun (s, n) -> sprintf "%s %d" (statusText s) n) |> String.concat ", "
    (findings |> List.map line) @ [ sprintf "stack-drift: %s; %s" counts (if ready findings then "READY" else "NOT READY (a required system is not PASS)") ]
    |> String.concat "\n"

let reportJson (findings: Finding list) : JsonNode =
    jobj
        [ "schema", jstr "indy-init.stack-drift-report/1"
          "ready", jbool (ready findings)
          "summary", jobj (summary findings |> List.map (fun (s, n) -> statusText s, (JsonValue.Create n :> JsonNode)))
          "findings",
          jarr (
              findings
              |> List.map (fun f ->
                  jobj
                      [ "system", jstr f.System
                        "check", jstr f.Check
                        "expected", jstr f.Expected
                        "actual", jstr f.Actual
                        "status", jstr (statusText f.Status)
                        "required", jbool f.Required
                        "correction", jstr f.Correction ])) ]

// ---------------------------------------------------------------- IO gatherers

let run (exe: string) (args: string) (cwd: string) : Result<string, string> =
    try
        let psi = ProcessStartInfo(exe, args, WorkingDirectory = cwd, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        use p = Process.Start psi
        let out = p.StandardOutput.ReadToEndAsync()
        let err = p.StandardError.ReadToEnd()
        p.WaitForExit()
        if p.ExitCode = 0 then Ok(out.Result + err) else Error(sprintf "exit %d: %s" p.ExitCode ((out.Result + err).Trim().Split('\n') |> Array.head |> fun s -> s.Substring(0, min 160 s.Length)))
    with e -> Error(sprintf "not found: %s" exe)

let gitShow (dir: string) (commit: string) (path: string) : byte[] option =
    try
        let psi = ProcessStartInfo("git", sprintf "-C \"%s\" show %s:%s" dir commit path, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        use p = Process.Start psi
        use ms = new MemoryStream()
        p.StandardOutput.BaseStream.CopyTo ms
        p.WaitForExit()
        if p.ExitCode = 0 then Some(ms.ToArray()) else None
    with _ -> None

let readGlobalJson (file: string) =
    if File.Exists file then
        let n = parseText (File.ReadAllText file)
        let sdk = field n "sdk" |> Option.toObj
        Some(str sdk "version", (match str sdk "rollForward" with "" -> "latestPatch" | r -> r))
    else None

let skipDir (d: string) =
    let name = Path.GetFileName d
    name = "node_modules" || name = ".git" || name = "bin" || name = "obj" || name = "dist" || name = ".conditor"

let rec filesUnder (root: string) (pattern: string) : string list =
    if not (Directory.Exists root) then []
    else
        (Directory.GetFiles(root, pattern) |> List.ofArray)
        @ (Directory.GetDirectories root |> List.ofArray |> List.filter (skipDir >> not) |> List.collect (fun d -> filesUnder d pattern))

let npmDeclared (file: string) =
    let n = parseText (File.ReadAllText file)
    [ "dependencies"; "devDependencies"; "peerDependencies"; "optionalDependencies" ]
    |> List.collect (fun k ->
        match field n k with
        | Some (:? JsonObject as o) -> o |> Seq.map (fun kv -> file, kv.Key, kv.Value.ToString()) |> List.ofSeq
        | _ -> [])
    |> List.filter (fun (_, p, _) -> p.StartsWith "@echelon-foundry/")

let npmInstalled (dir: string) =
    let scope = Path.Combine(dir, "node_modules", "@echelon-foundry")
    if Directory.Exists scope then
        Directory.GetDirectories scope
        |> List.ofArray
        |> List.map (fun d -> Path.Combine(d, "package.json"))
        |> List.filter File.Exists
        |> List.map (fun f -> let n = parseText (File.ReadAllText f) in f, str n "name", str n "version")
    else []

let nugetRefs (file: string) =
    Regex.Matches(File.ReadAllText file, "<PackageReference\\s+Include=\"(EchelonFoundry\\.[^\"]+)\"\\s+Version=\"([^\"]+)\"")
    |> Seq.map (fun m -> file, m.Groups[1].Value, m.Groups[2].Value)
    |> List.ofSeq

let workflowFacts (file: string) =
    let lines = File.ReadAllLines file |> Array.toList |> List.indexed
    let pins =
        lines
        |> List.collect (fun (i, l) ->
            Regex.Matches(l, @"(@echelon-foundry/)?(design-system|limen|print-components)(?:@|/-/[a-z-]+-|-)(\d+\.\d+\.\d+)")
            |> Seq.map (fun m -> file, i + 1, "@echelon-foundry/" + m.Groups[2].Value, m.Groups[3].Value)
            |> List.ofSeq)
    let setups =
        lines
        |> List.choose (fun (i, l) ->
            let v = Regex.Match(l, @"^\s*dotnet-version:\s*[""']?([^""'\s]+)")
            if v.Success then
                let spec = v.Groups[1].Value
                Some(file, i + 1, (if spec.Contains "x" || spec.Contains "*" then FloatingVersion spec else ExactVersion spec))
            elif Regex.IsMatch(l, @"^\s*global-json-file:") then Some(file, i + 1, GlobalJsonFile)
            else None)
    pins, setups

let gatherWorkspace (root: string) : WorkspaceFacts =
    let packageJsons = filesUnder root "package.json"
    let workflows =
        let d = Path.Combine(root, ".github", "workflows")
        if Directory.Exists d then Directory.GetFiles(d, "*.y*ml") |> List.ofArray else []
    let wf = workflows |> List.map workflowFacts
    { Root = root
      GlobalJson = readGlobalJson (Path.Combine(root, "global.json"))
      NpmDeclared = packageJsons |> List.collect npmDeclared
      NpmInstalled = packageJsons |> List.map Path.GetDirectoryName |> List.collect npmInstalled
      ConditorComponents =
        [ Path.Combine(root, "conditor.json") ]
        |> List.filter File.Exists
        |> List.collect (fun f -> arr (parseText (File.ReadAllText f)) "components" |> List.map (fun c -> f, str c "id", str c "version"))
      NugetReferences = (filesUnder root "*.fsproj" @ filesUnder root "*.csproj" @ filesUnder root "Directory.Packages.props") |> List.collect nugetRefs
      WorkflowPins = wf |> List.collect fst
      SetupDotnetSteps = wf |> List.collect snd }

let platform () =
    let arch = match Runtime.InteropServices.RuntimeInformation.OSArchitecture with Runtime.InteropServices.Architecture.Arm64 -> "arm64" | _ -> "x64"
    if OperatingSystem.IsWindows() then "win-" + arch elif OperatingSystem.IsMacOS() then "osx-" + arch else "linux-" + arch

let fileSha (f: string) = if File.Exists f then Ok(sha256Hex (File.ReadAllBytes f)) else Error(sprintf "not found: %s" f)

/// Where a locked artifact is published: a distribution that names the file itself (npm tarball, NuGet flat
/// container), else the GitHub release asset of the locked tag.
let artifactUrl (s: LockedSystem) (name: string) =
    s.Distributions
    |> List.map snd
    |> List.tryFind (fun u -> u.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) || u.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
    |> Option.defaultValue (sprintf "https://github.com/%s/releases/download/%s/%s" s.Repository s.Tag name)

let fetchArtifacts (lock: JsonNode) (dir: string) : Map<string, Result<string, string>> =
    Directory.CreateDirectory dir |> ignore
    use http = new Net.Http.HttpClient()
    systemsOf lock
    |> List.choose (fun s -> s.Artifact |> Option.map (fun (name, _) -> s, name))
    |> List.map (fun (s, name) ->
        let target = Path.Combine(dir, name)
        let url = artifactUrl s name
        let result =
            if File.Exists target then fileSha target
            else
                try
                    let bytes = http.GetByteArrayAsync(url).Result
                    File.WriteAllBytes(target, bytes)
                    Ok(sha256Hex bytes)
                with e -> Error(sprintf "download failed: %s (%s)" url (e.GetBaseException().Message))
        name, result)
    |> Map.ofList

let gather (lock: JsonNode) : Observations =
    let registryCommit = field lock "releaseSet" |> Option.map (fun s -> str s "registryCommit") |> Option.defaultValue ""
    let sdkRoot = opt "--sdk-root" |> Option.defaultValue "."
    let tools =
        systemsOf lock
        |> List.choose (fun s -> s.Executable)
        |> List.map (fun exe ->
            let path = opt "--bin" |> Option.map (fun b -> Path.Combine(b, exe)) |> Option.defaultValue exe
            let result =
                match run path "--version" "." with
                | Ok output -> (match versionFromOutput output with Some v -> Ok v | None -> Error "no version in its --version output")
                | Error why -> Error why
            exe, result)
        |> Map.ofList
    { Registry = opt "--registry" |> Option.map (fun dir -> gitShow dir registryCommit)
      ToolchainLock = opt "--toolchain-lock" |> Option.map (File.ReadAllText >> parseText)
      Workspaces = opts "--workspace" |> List.map (Path.GetFullPath >> gatherWorkspace)
      PinnedSdk = readGlobalJson (Path.Combine(sdkRoot, "global.json"))
      InstalledSdk = run "dotnet" "--version" sdkRoot |> Result.map (fun s -> s.Trim())
      Tools = tools
      ConditorSha256 = opt "--conditor" |> Option.map fileSha
      Platform = platform ()
      Artifacts =
        match opt "--artifacts", opt "--fetch" with
        | Some dir, _ -> systemsOf lock |> List.choose (fun s -> s.Artifact |> Option.map (fun (n, _) -> n, fileSha (Path.Combine(dir, n)))) |> Map.ofList
        | None, Some dir -> fetchArtifacts lock dir
        | None, None -> Map.empty }

// ---------------------------------------------------------------- self-tests

let tests (registry: string option) : (string * Result<unit, string>) list =
    let expect name cond detail = name, (if cond then Ok() else Error detail)
    let lockText =
        """{"releaseSet":{"channel":"echelon-current","version":"1.37.0","registryCommit":"c0ffee","path":"sets/s.json","sha256":"SETSHA"},
            "systems":[
             {"id":"forma","kind":"registry-component","version":"0.13.0","required":true,"npmPackage":"@echelon-foundry/design-system","repository":"kemiller2002/forma","tag":"v0.13.0",
              "releaseManifest":{"path":"releases/forma/0.13.0.release.json","sha256":"MSHA"},"packageDigest":{"artifact":"forma.tgz","sha256":"aaa"}},
             {"id":"limen-fsharp","kind":"registry-component","version":"0.9.0","required":true,"nugetPackage":"EchelonFoundry.Limen.Contract","releaseManifest":{"path":"","sha256":""}},
             {"id":"praxis","kind":"registry-component","version":"3.25.0","required":true,"executable":"praxis","releaseManifest":{"path":"","sha256":""}},
             {"id":"percepta","kind":"registry-component","version":"0.1.0","required":false,"executable":"percepta-repo","releaseManifest":{"path":"","sha256":""}},
             {"id":"dotnet-sdk","kind":"sdk","version":"10.0.402"},
             {"id":"conditor","kind":"tag-pinned","version":"0.10.20"}]}"""
    let lock = parseText lockText
    let emptyWs root =
        { Root = root; GlobalJson = Some("10.0.402", "disable"); NpmDeclared = []; NpmInstalled = []; ConditorComponents = []
          NugetReferences = []; WorkflowPins = []; SetupDotnetSteps = [] }
    let good =
        { Registry = None
          ToolchainLock = None
          Workspaces = [ emptyWs "/w" ]
          PinnedSdk = Some("10.0.402", "disable")
          InstalledSdk = Ok "10.0.402"
          Tools = Map.ofList [ "praxis", Ok "3.25.0"; "percepta-repo", Ok "0.1.0" ]
          ConditorSha256 = None
          Platform = "linux-x64"
          Artifacts = Map.empty }
    let statusOf system check (fs: Finding list) =
        fs |> List.filter (fun f -> f.System = system && (f.Check: string).Contains(check: string)) |> List.map (fun f -> f.Status)
    let withWs f = { good with Workspaces = [ f (emptyWs "/w") ] }
    let noRegistry = evaluate lock good
    [ expect "pinnedVersion: exact, v-prefixed and release-asset URL pin" (pinnedVersion "0.13.0" = Ok "0.13.0" && pinnedVersion "v0.9.1" = Ok "0.9.1" && pinnedVersion "https://github.com/kemiller2002/folio/releases/download/v0.4.1/echelon-foundry-print-components-0.4.1.tgz" = Ok "0.4.1") "pinnedVersion"
      expect "pinnedVersion: ^, ~, x-range, * and latest float (negative control)" ([ "^0.13.0"; "~0.13.0"; "0.13.x"; "*"; "latest"; ">=0.12.0" ] |> List.forall (pinnedVersion >> Result.isError)) "a floating spec was accepted"
      expect "versionFromOutput: praxis, ordo, percepta and dokimos JSON" (versionFromOutput "praxis 3.25.0" = Some "3.25.0" && versionFromOutput "1.20.0\n" = Some "1.20.0" && versionFromOutput "percepta 0.4.0+4a53688" = Some "0.4.0" && versionFromOutput """{"ReleaseVersion":"0.11.1","SchemaVersion":"1.1.0"}""" = Some "0.11.1") "versionFromOutput"
      expect "versionFromOutput: no version is None (UNVERIFIED, not PASS)" (versionFromOutput "usage: tool" = None) "invented a version"
      ( let fs = evaluate lock good |> List.filter (fun f -> f.Check <> "release manifest in echelon-registry")
        expect "matching tools and SDK are PASS" (fs |> List.forall (fun f -> f.Status = Pass)) (reportText fs))
      expect "no registry checkout makes every registry component UNVERIFIED, so not ready" (noRegistry |> List.exists (fun f -> f.Status = Unverified) && not (ready noRegistry)) "UNVERIFIED did not block"
      ( let fs = evaluate lock { good with Tools = good.Tools.Add("praxis", Ok "3.24.0") }
        expect "negative control: installed praxis 3.24.0 against 3.25.0 is DRIFT and not ready" (statusOf "praxis" "installed" fs = [ Drift ] && not (ready fs)) (reportText fs))
      ( let fs = evaluate lock { good with Tools = good.Tools.Add("praxis", Error "not found: praxis") }
        expect "negative control: a missing required tool is BLOCKED" (statusOf "praxis" "installed" fs = [ Blocked ]) (reportText fs))
      ( let fs = evaluate lock { good with Tools = good.Tools.Add("praxis", Error "no version in its --version output") }
        expect "negative control: an executable without a version is UNVERIFIED" (statusOf "praxis" "installed" fs = [ Unverified ]) (reportText fs))
      ( let fs = evaluate lock { good with Tools = good.Tools.Add("percepta-repo", Ok "0.0.9") } |> List.filter (fun f -> f.Check <> "release manifest in echelon-registry")
        expect "an optional system's drift is reported but does not block" (statusOf "percepta" "installed" fs = [ Drift ] && ready fs) (reportText fs))
      ( let fs = evaluate lock { good with InstalledSdk = Ok "10.0.403" }
        expect "negative control: SDK 10.0.403 installed against 10.0.402 is DRIFT" (statusOf "dotnet-sdk" "installed SDK" fs = [ Drift ]) (reportText fs))
      ( let fs = evaluate lock { good with InstalledSdk = Error "not found: dotnet" }
        expect "negative control: no dotnet is BLOCKED" (statusOf "dotnet-sdk" "installed SDK" fs = [ Blocked ]) (reportText fs))
      ( let fs = evaluate lock { good with PinnedSdk = Some("10.0.402", "latestFeature") }
        expect "negative control: global.json that rolls forward is DRIFT" (statusOf "dotnet-sdk" "rollForward" fs = [ Drift ]) (reportText fs))
      ( let fs = evaluate lock (withWs (fun w -> { w with NpmDeclared = [ ("/w/package.json", "@echelon-foundry/design-system", "0.12.0") ] }))
        expect "negative control: Forma 0.12.0 declared against 0.13.0 is DRIFT" (statusOf "forma" "declared" fs = [ Drift ]) (reportText fs))
      ( let fs = evaluate lock (withWs (fun w -> { w with NpmDeclared = [ ("/w/package.json", "@echelon-foundry/design-system", "^0.13.0") ] }))
        expect "negative control: a floating Forma range is DRIFT even when it would resolve to 0.13.0" (statusOf "forma" "declared" fs = [ Drift ]) (reportText fs))
      ( let fs = evaluate lock (withWs (fun w -> { w with NpmInstalled = [ ("/w/node_modules/x/package.json", "@echelon-foundry/design-system", "0.13.1") ] }))
        expect "negative control: an installed node_modules version that differs is DRIFT" (statusOf "forma" "installed" fs = [ Drift ]) (reportText fs))
      ( let fs = evaluate lock (withWs (fun w -> { w with NpmDeclared = [ ("/w/package.json", "@echelon-foundry/forma-workflow", "1.2.0") ] }))
        expect "negative control: an Echelon npm package outside the lock is BLOCKED" (statusOf "@echelon-foundry/forma-workflow" "declared" fs = [ Blocked ]) (reportText fs))
      ( let fs = evaluate lock (withWs (fun w -> { w with NugetReferences = [ "/w/a.fsproj", "EchelonFoundry.Limen.Guest", "0.9.0"; "/w/b.fsproj", "EchelonFoundry.Arca.Core", "0.7.0" ] }))
        expect "Limen F# packages map to limen-fsharp; Arca outside the lock is BLOCKED" (statusOf "limen-fsharp" "Limen.Guest" fs = [ Pass ] && statusOf "EchelonFoundry.Arca.Core" "referenced" fs = [ Blocked ]) (reportText fs))
      ( let fs = evaluate lock (withWs (fun w -> { w with WorkflowPins = [ ("/w/.github/workflows/ci.yml", 120, "@echelon-foundry/design-system", "0.12.0") ] }))
        expect "negative control: a workflow downloading Forma 0.12.0 is DRIFT" (statusOf "forma" "pinned in" fs = [ Drift ]) (reportText fs))
      ( let fs = evaluate lock (withWs (fun w -> { w with SetupDotnetSteps = [ "/w/.github/workflows/ci.yml", 29, FloatingVersion "10.0.x"; "/w/.github/workflows/ok.yml", 9, GlobalJsonFile ] }))
        expect "negative control: setup-dotnet 10.0.x with global.json rollForward disable is DRIFT (HF-029); global-json-file is PASS"
            (statusOf "dotnet-sdk" "ci.yml" fs = [ Drift ] && statusOf "dotnet-sdk" "ok.yml" fs = [ Pass ]) (reportText fs))
      ( let fs = evaluate lock (withWs (fun w -> { w with ConditorComponents = [ ("/w/conditor.json", "forma", "0.12.0") ] }))
        expect "negative control: a Conditor manifest naming Forma 0.12.0 is DRIFT" (statusOf "forma" "component" fs = [ Drift ]) (reportText fs))
      ( let fs = evaluate lock { good with Artifacts = Map.ofList [ ("forma.tgz", Ok "bbb") ] }
        expect "negative control: an artifact whose digest differs is BLOCKED" (statusOf "forma" "artifact" fs = [ Blocked ]) (reportText fs))
      ( let fs = evaluate lock { good with Artifacts = Map.ofList [ ("forma.tgz", Error "download failed") ] }
        expect "negative control: an unobtainable artifact is UNVERIFIED" (statusOf "forma" "artifact" fs = [ Unverified ]) (reportText fs))
      ( let v4 =
            systemsOf (parseText """{"systems":[
              {"id":"forma","distributions":[{"mechanism":"github-release","package":"@echelon-foundry/design-system"}]},
              {"id":"limen-fsharp","distributions":[{"mechanism":"github-release"}],"packageDigest":{"artifact":"EchelonFoundry.Limen.Contract.0.9.0.nupkg"}},
              {"id":"aegis","distributions":[{"mechanism":"nuget","package":"EchelonFoundry.Aegis.Core"}]}]}""")
        expect "package mapping falls back to a lock's distributions and artifact names (the frozen v4 lock has no package fields)"
            (v4[0].NpmPackage = Some "@echelon-foundry/design-system" && v4[1].NugetPackage = Some "EchelonFoundry.Limen.Contract" && v4[2].NugetPackage = Some "EchelonFoundry.Aegis.Core")
            (sprintf "%A" v4))
      ( let s =
            systemsOf (parseText """{"systems":[
              {"id":"limen","repository":"kemiller2002/limen","tag":"v0.9.1","distributions":[{"mechanism":"npm","url":"https://registry.npmjs.org/@echelon-foundry/limen/-/limen-0.9.1.tgz"}]},
              {"id":"forma","repository":"kemiller2002/forma","tag":"v0.13.0","distributions":[{"mechanism":"github-release","url":"https://github.com/kemiller2002/forma/releases/tag/v0.13.0"}]}]}""")
        expect "artifactUrl: a file distribution (npm) is used as is; a release page falls back to the tag's asset"
            (artifactUrl s[0] "x.tgz" = "https://registry.npmjs.org/@echelon-foundry/limen/-/limen-0.9.1.tgz"
             && artifactUrl s[1] "f.tgz" = "https://github.com/kemiller2002/forma/releases/download/v0.13.0/f.tgz") "artifactUrl")
      ( let read p = if p = "sets/s.json" then Some(Encoding.UTF8.GetBytes """{"status":"approved","components":[{"systemId":"forma","version":"0.12.0"}]}""") else None
        let fs = evaluate lock { good with Registry = Some read }
        expect "negative control: a lock version that is not the set's version is DRIFT" (statusOf "forma" "lock version" fs = [ Drift ]) (reportText fs))
      ( let toolchain = parseText """{"conditor":{"version":"0.10.18","artifacts":{"linux-x64":{"sha256":"abc"}}},"resolvedSet":{"commit":"c0ffee","profileVersion":"1.36.0","expectedTools":{"praxis":"3.25.0"}}}"""
        let fs = evaluate lock { good with ToolchainLock = Some toolchain; ConditorSha256 = Some(Ok "def") }
        expect "negative controls: toolchain lock on 1.36.0 and Conditor 0.10.18, and a Conditor binary of another digest, are DRIFT"
            (statusOf "echelon-current" "profile version" fs = [ Drift ] && statusOf "conditor" "toolchain lock Conditor" fs = [ Drift ] && statusOf "conditor" "binary digest" fs = [ Drift ] && statusOf "praxis" "expected tool" fs = [ Pass ])
            (reportText fs))
      ( let fs = evaluate lock { good with ToolchainLock = Some(parseText """{"resolvedSet":{"expectedTools":{"tutors":"1.0.0"}}}""") }
        expect "negative control: a toolchain-lock tool outside the dependency lock is BLOCKED" (statusOf "tutors" "expected tool" fs = [ Blocked ]) (reportText fs))
      ( let read p =
            match p with
            | "sets/s.json" -> Some(Encoding.UTF8.GetBytes """{"status":"approved","components":[{"systemId":"forma","version":"0.13.0","releaseManifestSha256":"x"}]}""")
            | "releases/forma/0.13.0.release.json" -> Some(Encoding.UTF8.GetBytes """{"version":"0.13.0","releaseStage":"prerelease","artifacts":[]}""")
            | _ -> None
        let source = parseText """{"releaseSet":{"path":"sets/s.json"},"forbiddenFrameworks":["Bolero"],"systems":[{"id":"forma","kind":"registry-component","systemId":"forma"},{"id":"bolero","kind":"tag-pinned"}]}"""
        let _, problems = buildLock read "c0ffee" source
        expect "negative controls (lock): a prerelease, a digest the set does not record and a forbidden framework are problems"
            (problems |> List.exists (fun p -> p.Contains "not stable") && problems |> List.exists (fun p -> p.Contains "the set records") && problems |> List.exists (fun p -> p.Contains "forbidden framework"))
            (String.concat "; " problems))
      match registry with
      | None -> ("lock: reproduces the frozen v4 lock from the v4 source (needs --registry)", Error "NOT RUN: no --registry supplied")
      | Some dir ->
          let frozen = parseText (File.ReadAllText "requirements-compiled/v4/dependency-lock.json")
          let commit = str (field frozen "releaseSet" |> Option.toObj) "registryCommit"
          let source = parseText (File.ReadAllText "requirements-compiled/v4/analysis/dependency-lock.source.json")
          let built, problems = buildLock (gitShow dir commit) commit source
          expect "lock: reproduces the frozen v4 lock from the v4 source (deep JSON equality, no problems)" (sameJson built frozen && problems.IsEmpty)
              (sprintf "differs or problems: %s" (String.concat "; " problems)) ]

// ---------------------------------------------------------------- dispatch

let exitWith (code: int) = exit code

match argv with
| "lock" :: _ ->
    match opt "--source", opt "--registry" with
    | Some source, Some registry ->
        let commit = opt "--registry-commit" |> Option.defaultWith (fun () -> match run "git" (sprintf "-C \"%s\" rev-parse HEAD" registry) "." with Ok s -> s.Trim() | Error e -> failwith e)
        let lock, problems = buildLock (gitShow registry commit) commit (parseText (File.ReadAllText source))
        let text = render lock + "\n"
        match opt "--out" with
        | Some out -> File.WriteAllText(out, text); printfn "stack-drift lock: wrote %s (registry %s)" out commit
        | None -> printf "%s" text
        problems |> List.iter (eprintfn "  problem: %s")
        exitWith (if problems.IsEmpty then 0 else 3)
    | _ -> eprintfn "usage: lock --source SRC --registry DIR [--registry-commit SHA] [--out FILE]"; exitWith 2
| "check" :: _ ->
    match opt "--lock" with
    | None -> eprintfn "usage: check --lock LOCK [--registry DIR] [--toolchain-lock F] [--workspace DIR]... [--sdk-root DIR] [--bin DIR] [--conditor F] [--artifacts DIR | --fetch DIR] [--json F]"; exitWith 2
    | Some lockFile ->
        let lock = parseText (File.ReadAllText lockFile)
        let findings = evaluate lock (gather lock)
        printfn "%s" (reportText findings)
        opt "--json" |> Option.iter (fun f -> File.WriteAllText(f, render (reportJson findings) + "\n"))
        exitWith (if ready findings then 0 else 3)
| "test" :: _ ->
    let results = tests (opt "--registry")
    results |> List.iter (fun (name, r) -> match r with Ok () -> printfn "PASS %s" name | Error e -> printfn "FAIL %s\n     %s" name e)
    let failed = results |> List.filter (snd >> Result.isError) |> List.length
    printfn "stack-drift test: %d/%d passed" (results.Length - failed) results.Length
    exitWith (if failed = 0 then 0 else 3)
| _ -> eprintfn "usage: stack-drift.fsx -- lock|check|test ..."; exitWith 2
