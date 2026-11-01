/// Compiled package v4.1.1, the successor amendment of v4.1: the committed package is reproduced from its inputs, v4.1
/// is untouched by it, and each check v4.1.1 adds rejects the inputs it guards against. Those checks are: X17, hidden
/// implementation in a contract; X18, an unauthorized owner of a shared web entrypoint (the negative test the amendment
/// requires); X19, a timed task that can start before the bootstrap, a gate on organizer permission, an unmeasured
/// bootstrap; X20, an amendment that names another package or digest. The bootstrap's layering onto Conditor's
/// repository merges only the files it may and refuses every other overlap.
module V411Tests

open System.IO
open Json

let private setIn (key: string) (value: Json) (j: Json) =
    match j with
    | JObj ps when ps |> List.exists (fst >> (=) key) -> JObj(ps |> List.map (fun (k, v) -> if k = key then k, value else k, v))
    | JObj ps -> JObj(ps @ [ key, value ])
    | j -> j

let private mapArr (key: string) (f: Json list -> Json list) (j: Json) = setIn key (JArr(f (arr key j))) j

let inputs (source: string) : V41Package.Inputs =
    Program.v41Inputs source (Path.Combine(source, "requirements-compiled", "v4.1.1")) (Path.Combine(source, "requirements-compiled", "v4")) None

let private failing (i: V41Package.Inputs) =
    (V41Package.build i).Checks |> List.filter (fun c -> not c.Problems.IsEmpty) |> List.map (fun c -> c.Id)

let private expectFail (name: string) (checkId: string) (i: V41Package.Inputs) : string list =
    try
        if List.contains checkId (failing i) then [] else [ sprintf "v4.1.1 control '%s': %s did not fail" name checkId ]
    with ex -> [ sprintf "v4.1.1 control '%s' threw: %s" name ex.Message ]

/// The committed v4.1.1 package equals a fresh build, every check passes, two builds and two bootstraps are identical,
/// the plan is preserved, and v4.1 without the amendment still has none of v4.1.1's checks or files.
let scope (source: string) : string list =
    let i = inputs source
    let built = V41Package.build i
    let package = Path.Combine(source, "requirements-compiled", "v4.1.1")
    let globalJson = File.ReadAllText(Path.Combine(source, "global.json"))
    [ for c in built.Checks do
          if not c.Problems.IsEmpty then yield sprintf "v4.1.1: %s fails on the committed package: %A" c.Id (List.truncate 3 c.Problems)
      for id in [ "X17"; "X18"; "X19"; "X20" ] do
          if not (built.Checks |> List.exists (fun c -> c.Id = id)) then yield sprintf "v4.1.1: check %s does not run" id
      for (n, t) in built.Files do
          let p = Path.Combine(package, n)
          if not (File.Exists p) || File.ReadAllText p <> t then yield sprintf "v4.1.1: committed %s differs from a fresh build" n
      if (V41Package.build i).Files <> built.Files then yield "v4.1.1: two builds differ"
      let files = V41Package.ownedFiles i
      if V41Package.bootstrapFiles i files globalJson <> V41Package.bootstrapFiles i files globalJson then yield "v4.1.1: two bootstraps differ"
      // Each contract is materialized into its producer's file from the same text (deterministic, never re-authored).
      let boot = V41Package.bootstrapFiles i files globalJson |> Map.ofList
      for x in arr "interfaces" i.Interfaces do
          match boot.TryFind (str "file" x) with
          | Some text when text.Contains((str "fsharp" x).TrimEnd()) -> ()
          | _ -> yield sprintf "v4.1.1: the bootstrap does not write contract %s into %s verbatim" (str "id" x) (str "file" x)
      if boot |> Map.exists (fun _ t -> t.Contains "module ContractUsage") then yield "v4.1.1: the compile control 99-Usage is materialized into the application"
      let preservation = built.Files |> List.find (fst >> (=) "preservation-report.json") |> snd |> System.Text.Encoding.UTF8.GetBytes |> parseBytes
      if strOpt "result" preservation <> Some "PRESERVED" then yield "v4.1.1: the preservation report does not read PRESERVED"
      // v4.1 stays as approved: no amendment, no v4.1.1 check, no classification file.
      let v41 = V41Package.build (V41Tests.inputs source)
      if v41.Checks |> List.exists (fun c -> List.contains c.Id [ "X17"; "X18"; "X19"; "X20" ]) then yield "v4.1: a v4.1.1 check runs on v4.1"
      if v41.Files |> List.exists (fst >> (=) "contract-classification.json") then yield "v4.1: v4.1.1's contract classification is generated for v4.1"
      // The shared web entrypoints are integration's in the generated manifest.
      for p in [ "web/index.html"; "web/causa.css" ] do
          match files |> List.tryFind (fun f -> f.Path = p) with
          | Some f when f.Owner = "integration" -> ()
          | Some f -> yield sprintf "v4.1.1: %s is owned by %s, not integration" p f.Owner
          | None -> yield sprintf "v4.1.1: %s is not in the manifest" p ]

/// The bootstrap's layering onto a Conditor-initialized repository (layerFile): what it merges, what it keeps, what it
/// refuses.
let layering : string list =
    let conditorPackage = "{\n  \"name\": \"app\",\n  \"private\": true,\n  \"devDependencies\": {\n    \"@playwright/test\": \"1.50.0\",\n    \"limen\": \"0.9.1\"\n  }\n}\n"
    let generatedPackage = "{\n  \"name\": \"causa\",\n  \"private\": true,\n  \"devDependencies\": {\n    \"@playwright/test\": \"1.63.0\",\n    \"axe-core\": \"4.14.0\"\n  }\n}\n"
    let conditorProps = "<Project>\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n    <Nullable>enable</Nullable>\n    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n  </PropertyGroup>\n</Project>\n"
    let generatedProps = "<Project>\n  <PropertyGroup>\n    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n    <Nullable>enable</Nullable>\n  </PropertyGroup>\n</Project>\n"
    [ match V41Package.layerFile "package.json" conditorPackage generatedPackage with
      | Ok(Some merged) ->
          let deps = parseBytes (System.Text.Encoding.UTF8.GetBytes merged) |> field "devDependencies"
          let dep k = deps |> Option.bind (strOpt k)
          if dep "@playwright/test" <> Some "1.63.0" then yield "layering: package.json does not take the owner's @playwright/test lock"
          if dep "axe-core" <> Some "4.14.0" then yield "layering: package.json does not add the owner's axe-core lock"
          if dep "limen" <> Some "0.9.1" then yield "layering: package.json drops Conditor's own pin"
          if (parseBytes (System.Text.Encoding.UTF8.GetBytes merged) |> strOpt "name") <> Some "app" then yield "layering: package.json loses Conditor's name"
      | other -> yield sprintf "layering: package.json merge gave %A" other
      if V41Package.layerFile "Directory.Build.props" conditorProps generatedProps <> Ok None then yield "layering: a Directory.Build.props that already sets every property is not kept"
      match V41Package.layerFile "Directory.Build.props" (conditorProps.Replace("<Nullable>enable</Nullable>", "<Nullable>disable</Nullable>")) generatedProps with
      | Error _ -> ()
      | other -> yield sprintf "layering: a Directory.Build.props with Nullable disabled is accepted (%A)" other
      match V41Package.layerFile ".gitignore" "bin/\nobj/\n" "bin/\nnode_modules/\n" with
      | Ok(Some t) when t.Contains "node_modules/" && t.StartsWith "bin/\nobj/\n" -> ()
      | other -> yield sprintf "layering: .gitignore is not extended (%A)" other
      match V41Package.layerFile "src/Causa.Domain/Causa.Domain.fsproj" "<Project />" "<Project Sdk=\"x\" />" with
      | Error _ -> ()
      | other -> yield sprintf "layering: an existing project file with other content is overwritten (%A)" other
      if V41Package.layerFile "App.slnx" "x\n" "x" <> Ok None then yield "layering: an identical file is rewritten"
      // A locked package Conditor lists under dependencies takes the lock there and is not duplicated (RV411-1-10).
      match V41Package.layerFile "package.json" "{\n  \"name\": \"app\",\n  \"dependencies\": {\n    \"axe-core\": \"4.10.0\"\n  }\n}\n" generatedPackage with
      | Ok(Some merged) ->
          let j = parseBytes (System.Text.Encoding.UTF8.GetBytes merged)
          if (field "dependencies" j |> Option.bind (strOpt "axe-core")) <> Some "4.14.0" then yield "layering: a locked package under dependencies does not take the lock"
          if (field "devDependencies" j |> Option.bind (strOpt "axe-core")).IsSome then yield "layering: a locked package is listed in both sections"
      | other -> yield sprintf "layering: package.json merge with dependencies gave %A" other ]


/// The contract scanner (X17's heuristic) finds code in every form the independent review used to bypass its first
/// version (RV411-1-2), and finds nothing in a contract that only declares.
let scanner : string list =
    let found (src: string) = V41Package.contractImplementations ("namespace N\n\n" + src)
    let clean = "type A =\n    | One of int\n    | Two\n\ntype R = { X: int; Y: string }\n\n[<RequireQualifiedAccess>]\ntype I =\n    abstract Run: int -> int\n    abstract member Name: string\n\ntype F = int -> string\n"
    let bypasses =
        [ "active pattern", "module M =\n    let (|Even|Odd|) n = if n % 2 = 0 then Even else Odd\n"
          "static let", "type T() =\n    static let cache = 1\n"
          "standalone do", "module M =\n    do\n        printfn \"x\"\n"
          "bare expression", "module M =\n    printfn \"x\"\n"
          "use binding", "module M =\n    use r = new System.IO.MemoryStream()\n"
          "attribute and let on one line", "module M =\n    [<Literal>] let X = 1\n"
          "backtick name", "module M =\n    let ``hidden work`` x = x + 1\n"
          "one-line type with member", "type T() = member _.Run x = x * 2\n"
          "block comment before let", "module M =\n    (* note *) let f x = x\n"
          "let with its name on the next line", "module M =\n    let\n        f x = x\n"
          "record with a member", "type R =\n    { A: int }\n    member r.Double = r.A * 2\n"
          "object expression", "module M =\n    let o = { new System.IDisposable with member _.Dispose() = () }\n"
          "and-chained binding", "module M =\n    let rec f x = g x\n    and g x = x\n" ]
    [ if not (found clean).IsEmpty then yield sprintf "scanner: a declaration-only contract is reported as code: %A" (found clean |> List.map (fun m -> m.Symbol))
      for (name, src) in bypasses do
          if (found src).IsEmpty then yield sprintf "scanner: %s is not detected" name
      match found "module M =\n    let f x = x\n    and g x = x\n" |> List.map (fun m -> m.Symbol) with
      | [ "f"; _ ] -> ()
      | other -> yield sprintf "scanner: an and-chained binding is not reported separately: %A" other ]

/// The bootstrap layered onto the recorded Conditor 0.10.18 scaffold (RV411-1-10): it merges package.json and
/// .gitignore, keeps Conditor's Directory.Build.props, writes the skeleton, and leaves Conditor's own files untouched.
let ontoConditor (source: string) : string list =
    let fixture = Path.Combine(source, "tools", "requirements-compiler", "tests", "fixtures", "conditor-0.10.18-scaffold")
    let target = Path.Combine(Path.GetTempPath(), "v411-layered-" + string (System.Guid.NewGuid()))
    let copy (rel: string) (dest: string) =
        let p = Path.Combine(target, dest)
        Directory.CreateDirectory(Path.GetDirectoryName p) |> ignore
        File.Copy(Path.Combine(fixture, rel), p)
    let files = Directory.GetFiles(fixture, "*", SearchOption.AllDirectories) |> Array.map (fun f -> Path.GetRelativePath(fixture, f).Replace('\\', '/')) |> List.ofArray
    try
        Directory.CreateDirectory(Path.Combine(target, ".git")) |> ignore
        files |> List.filter (fun f -> f <> "README.md") |> List.iter (fun f -> copy f (if f = "gitignore.txt" then ".gitignore" else f))
        let before = files |> List.filter (fun f -> f.StartsWith "src/engine/" || f.StartsWith "tests/App.Engine.Tests/" || f = "App.slnx" || f = "Directory.Build.props") |> List.map (fun f -> f, File.ReadAllText(Path.Combine(target, f)))
        let code =
            Program.v41Command [ "--source"; source; "--package"; Path.Combine(source, "requirements-compiled", "v4.1.1"); "--base"; Path.Combine(source, "requirements-compiled", "v4"); "--check"; "--bootstrap"; target ]
        let read (f: string) = let p = Path.Combine(target, f) in if File.Exists p then Some(File.ReadAllText p) else None
        let pkg = read "package.json" |> Option.map (System.Text.Encoding.UTF8.GetBytes >> parseBytes)
        [ if code <> 0 then yield sprintf "onto Conditor: the bootstrap exited %d" code
          for (f, t) in before do
              if read f <> Some t then yield sprintf "onto Conditor: %s was changed" f
          if (read "Causa.slnx").IsNone then yield "onto Conditor: the skeleton was not written"
          match pkg with
          | Some j ->
              if strOpt "name" j <> (parseFile (Path.Combine(fixture, "package.json")) |> strOpt "name") then yield "onto Conditor: package.json lost Conditor's name"
              if (field "devDependencies" j |> Option.bind (strOpt "axe-core")) <> Some "4.14.0" then yield "onto Conditor: package.json lacks the owner's axe-core lock"
              if (field "scripts" j).IsNone then yield "onto Conditor: package.json lost Conditor's scripts"
          | None -> yield "onto Conditor: package.json is missing"
          match read ".gitignore" with
          | Some g when g.StartsWith(File.ReadAllText(Path.Combine(fixture, "gitignore.txt")).TrimEnd('\n')) -> ()
          | _ -> yield "onto Conditor: Conditor's .gitignore was not kept ahead of the generator's lines"
          match read "Directory.Packages.props" with
          | Some p when p.Contains "StartsWith('Causa.')" -> ()
          | _ -> yield "onto Conditor: central package management is not limited to the Causa projects" ]
    finally
        if Directory.Exists target then Directory.Delete(target, true)

/// Each v4.1.1 check rejects what it guards against.
let controls (source: string) : string list =
    let i0 = inputs source
    let amend f = { i0 with Amendment = i0.Amendment |> Option.map f }
    let structure f = { i0 with Structure = f i0.Structure }
    let owner path who = structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some path then setIn "owner" (JStr who) f else f)))
    let adjust id f = { i0 with Adjustments = mapArr "adjustments" (List.map (fun a -> if strOpt "task" a = Some id then f a else a)) i0.Adjustments }
    let iface id f = { i0 with Interfaces = mapArr "interfaces" (List.map (fun x -> if strOpt "id" x = Some id then f x else x)) i0.Interfaces }
    let authored name f = { i0 with AuthoredText = i0.AuthoredText |> List.map (fun (n, t) -> if n = name then n, f t else n, t) }
    [ // X18 / X02 (E): an unauthorized ownership change is rejected.
      yield! expectFail "web/index.html given back to ui" "X18" (owner "web/index.html" "ui")
      yield! expectFail "web/index.html given back to ui" "X02" (owner "web/index.html" "ui")
      yield! expectFail "web/causa.css given to ui-2" "X18" (owner "web/causa.css" "ui-2")
      yield! expectFail "ui contributing to an entrypoint" "X18" (structure (mapArr "sharedFiles" (List.map (fun f -> if strOpt "path" f = Some "web/causa.css" then setIn "contributors" (jstrs [ "ui" ]) f else f))))
      yield! expectFail "change requests owned by ui" "X18" (amend (fun a -> setIn "sharedEntrypoints" (setIn "changeRequests" (JObj [ "owner", JStr "ui"; "requesters", jstrs [ "ui" ]; "protocol", JStr "x"; "record", JStr "x" ]) (field "sharedEntrypoints" a |> Option.defaultValue (JObj []))) a))
      yield! expectFail "unknown requester" "X18" (amend (fun a -> setIn "sharedEntrypoints" (setIn "changeRequests" (JObj [ "owner", JStr "integration"; "requesters", jstrs [ "designer" ]; "protocol", JStr "x"; "record", JStr "x" ]) (field "sharedEntrypoints" a |> Option.defaultValue (JObj []))) a))
      yield! expectFail "prose drops the entrypoints from integration" "X18" (authored "AGENT-RULES.md" (fun t -> t.Replace("the shared web entrypoints `web/index.html` and `web/causa.css` (loading shell and layout);", "")))
      yield! expectFail "prose lets ui edit an entrypoint directly" "X18" (authored "AGENT-RULES.md" (fun t -> t + "\nThe ui role edits `web/index.html` directly when it needs to.\n"))
      yield! expectFail "prose gives the entrypoints to ui" "X18" (authored "AGENT-RULES.md" (fun t -> t.Replace("It changes `web/index.html` and `web/causa.css` only through a change request to integration", "`web/index.html` and `web/causa.css` (loading shell and layout)")))
      // X17 (B): hidden implementation in a contract; a stale allowlist entry; an allowlist entry without its reason.
      yield! expectFail "hidden implementation" "X17" (iface "IF41-APP" (fun x -> setIn "fsharp" (JStr(str "fsharp" x + "\nmodule Hidden =\n    let decide (m: Model) = m\n")) x))
      yield! expectFail "hidden member" "X17" (iface "IF41-VIEW" (fun x -> setIn "fsharp" (JStr(str "fsharp" x + "\ntype Extra =\n    { A: int }\n    member e.Double = e.A * 2\n")) x))
      yield! expectFail "contract differs from the reviewed baseline" "X17" (iface "IF41-VIEW" (fun x -> setIn "fsharp" (JStr(str "fsharp" x + "\n/// a changed comment\n")) x))
      yield! expectFail "allowlisted member body changed" "X17" (amend (mapArr "contractImplementations" (List.map (fun a -> if strOpt "symbol" a = Some "ofReadBack" then setIn "sha256" (JStr(String.replicate 64 "a")) a else a))))
      yield! expectFail "no reviewed baseline" "X17" (amend (setIn "contractBaseline" (JObj [])))
      yield! expectFail "stale allowlist entry" "X17" (amend (mapArr "contractImplementations" (fun xs -> xs @ [ JObj [ "file", JStr "06-IF41-APP.fs"; "scope", JStr "Engine"; "symbol", JStr "run"; "rationale", JStr "x"; "requirements", jstrs [ "PF-015" ] ] ])))
      yield! expectFail "allowlist entry without rationale" "X17" (amend (mapArr "contractImplementations" (List.map (setIn "rationale" (JStr "")))))
      // X19 (A, C): a timed task that need not wait for the bootstrap; the organizer gate restored; the bootstrap
      // estimate unmeasured; the measurement dropped; AGENT-RULES relying on ODR-V41-06 again; no residual risk.
      yield! expectFail "T14 starts before the bootstrap" "X19" (adjust "T14" (setIn "dependsOn" (JArr [])))
      yield! expectFail "organizer gate restored" "X19" (adjust "T10" (setIn "dropPreferenceAfter" (JArr [])))
      yield! expectFail "bootstrap estimate unmeasured" "X19" (adjust "T10" (setIn "evidence" (JStr "estimated")))
      yield! expectFail "bootstrap labelled fully measured" "X13" (adjust "T10" (setIn "evidence" (JStr "measured")))
      yield! expectFail "measured split does not add up" "X13" (adjust "T10" (setIn "measuredMinutes" (JInt 5L)))
      yield! expectFail "measured split differs from the record" "X19" (adjust "T10" (setIn "measuredMinutes" (JInt 5L) >> setIn "estimatedMinutes" (JInt 24L)))
      let policy f = amend (fun a -> setIn "startingState" (f (field "startingState" a |> Option.defaultValue (JObj []))) a)
      for gate in [ "PRE-ORG-01"; "ADRP-016-O2"; "STORAGE-008-O1"; "waiver T12>T14"; "OD-INDY-12" ] do
          yield! expectFail ("organizer gate without disposition: " + gate) "X19" (policy (mapArr "organizerReferences" (List.filter (fun r -> strOpt "id" r <> Some gate))))
      yield! expectFail "organizer reference naming an unknown decision" "X19" (policy (mapArr "organizerReferences" (List.map (setIn "decision" (JStr "ODR-V411-99")))))
      yield! expectFail "new organizer gate in a rule document" "X19" (policy (setIn "organizerDocuments" (jstrs [ "docs/HACKATHON-BOUNDARIES.md"; "docs/OWNER-DECISIONS-2026-11-01.md" ])))
      yield! expectFail "bootstrap left before T0" "X19" (adjust "T10" (fun a -> match a with JObj ps -> JObj(ps |> List.filter (fst >> (<>) "phase")) | j -> j))
      yield! expectFail "measurement missing" "X19" { i0 with AuthoredText = i0.AuthoredText |> List.filter (fst >> (<>) "analysis/bootstrap-measurement.json") }
      yield! expectFail "relies on ODR-V41-06" "X19" (authored "AGENT-RULES.md" (fun t -> t + "\nThis relies on ODR-V41-06.\n"))
      yield! expectFail "no residual risk" "X19" (amend (fun a -> setIn "startingState" (setIn "residualRisk" (JStr "") (field "startingState" a |> Option.defaultValue (JObj []))) a))
      yield! expectFail "historical record rewritten" "X19" (amend (fun a -> setIn "startingState" (mapArr "historicalRecords" (List.map (setIn "disposition" (JStr "superseded"))) (field "startingState" a |> Option.defaultValue (JObj []))) a))
      // X13: only an amendment moves a phase; a measured label needs its record.
      yield! expectFail "measured without record" "X13" (adjust "T10" (setIn "measurement" (JStr "analysis/none.json")))
      yield! expectFail "phase move without an amendment" "X13" { (adjust "T10" id) with Amendment = None }
      // X20: the amended package's digest; the version.
      yield! expectFail "wrong amended digest" "X20" (amend (fun a -> setIn "amends" (setIn "manifestSha256" (JStr(String.replicate 64 "0")) (field "amends" a |> Option.defaultValue (JObj []))) a))
      yield! expectFail "not a successor version" "X20" (amend (setIn "packageVersion" (JStr "4.1.0")))
      yield! expectFail "amends another package" "X20" (amend (fun a -> setIn "amends" (setIn "package" (JStr "requirements-compiled/v4") (field "amends" a |> Option.defaultValue (JObj []))) a)) ]

let all (source: string) : string list =
    (try scope source with ex -> [ "v4.1.1 scope: " + ex.Message ])
    @ (try layering with ex -> [ "v4.1.1 layering: " + ex.Message ])
    @ (try scanner with ex -> [ "v4.1.1 scanner: " + ex.Message ])
    @ (try ontoConditor source with ex -> [ "v4.1.1 onto Conditor: " + ex.Message ])
    @ (try controls source with ex -> [ "v4.1.1 controls: " + ex.Message ])
