// delegated-authority.fsx: checks agent decisions made under the owner's standing technical authorization (DA-R10-1).
//
//   dotnet fsi tools/r10/delegated-authority.fsx -- --indy-init . check [--base-ref origin/main]
//   dotnet fsi tools/r10/delegated-authority.fsx -- --indy-init . test
//
// What this is: a FORMAT AND SCOPE CHECK on an audit log, not an enforcement of judgment. It cannot tell what a decision
// is really about; the class and decidedAt are written by the deciding agent (review H2). Every new entry therefore
// also needs a human or independent-agent look in its PR. It refuses, mechanically:
//   - a reserved or uncovered class (the 8 reserved and the 11 covered class names are fixed HERE, not read from the
//     editable authority record: review B2);
//   - any owner-decision-shaped id (ODR-, OD-, CF-, APR-, RDP-, case-insensitive, any hyphen form) or any id in any
//     owner-decision-requests file, in ANY string of a decision: owner matters need the owner's own record (B1, H1);
//   - the owner's login anywhere in a decision, unknown fields (e.g. approvedBy, ownerApproval), a non-agent actor (M1);
//   - a declared path, normalized, that is protected (B3), and any NEW decision in a change that also edits a protected
//     path in the real diff against the base (B3);
//   - once the authority record and directive are on the base, any change to either (B2);
//   - an edited or removed base entry, or an unreadable base ref (M3);
//   - any approvals*.json anywhere that cites a delegated record (M2).
// Every rule is a pure function from the loaded inputs to a list of refusals.

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json.Nodes
open System.Text.RegularExpressions

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")

// ---- fixed policy (changing these is a change to this reviewed file, never to the agent-editable record) ----
let reservedClasses =
    set [ "acceptance-weakening"; "test-result-claim"; "access-expansion"; "secret-publication"
          "unrecorded-scope-reduction"; "eligibility-rule-change"; "official-build-start"; "owner-identity-or-approval" ]
let coveredClasses =
    set [ "implementation-approach"; "work-item-boundary"; "milestone-reordering"; "source-ownership"; "technical-interface"
          "test-correction"; "pr-merge-after-required-checks"; "dev-workflow-update"; "noncritical-deferral"
          "routine-recommended-option"; "approved-tooling-reuse" ]
let latestValidUntil = DateTimeOffset.Parse "2026-11-07T23:59:59Z"
let directiveReceived = DateTimeOffset.Parse "2026-11-05T19:08:39Z"
let dir = "kickoff/rehearsal-010/delegated"
let logPath = $"{dir}/delegated-decisions.json"
let protectedGlobs =
    [ "**/approvals*.json"; "**/owner-decision-requests*.json"; $"{dir}/delegated-authority.json"; $"{dir}/DIRECTIVE-*.md"
      "tools/r10/delegated-authority.fsx"; "tools/owner-freeze.fsx"; "tools/r10/launch-preflight.fsx"
      ".github/workflows/**"; ".github/CODEOWNERS"; "verification/**" ]
let decisionFields = set [ "id"; "class"; "decision"; "decides"; "rationale"; "evidence"; "decidedAt"; "paths"; "actingAgent" ]
let agentFields = set [ "kind"; "session"; "role" ]
// owner-decision-shaped ids: not preceded by a letter or digit (a hyphen before is allowed: "pre-ODR-R10-32")
let ownerIdRx = Regex(@"(?<![A-Za-z0-9])(ODR|OD|CF|APR|RDP)-[A-Za-z0-9]+", RegexOptions.IgnoreCase)
let normHyphens (s: string) = Regex.Replace(s, "[‐‑‒–—―−﹣－]", "-")

// ---- json helpers (total: a missing field reads as None / empty) ----
let field (k: string) (n: JsonNode) : JsonNode option =
    match n with
    | :? JsonObject as o -> (let mutable v : JsonNode = null in if o.TryGetPropertyValue(k, &v) && not (isNull v) then Some v else None)
    | _ -> None
let str k n = field k n |> Option.bind (fun v -> try Some(v.GetValue<string>()) with _ -> None)
let strOr k n = str k n |> Option.defaultValue ""
let arr k n = field k n |> Option.map (fun v -> try v.AsArray() |> Seq.toList with _ -> []) |> Option.defaultValue []
let boolOr k n = field k n |> Option.bind (fun v -> try Some(v.GetValue<bool>()) with _ -> None) |> Option.defaultValue false
let keys (n: JsonNode) = match n with :? JsonObject as o -> o |> Seq.map (fun kv -> kv.Key) |> List.ofSeq | _ -> []
let time (s: string) = match DateTimeOffset.TryParse s with | true, t -> Some t | _ -> None
let rec strings (n: JsonNode) : string list =
    match n with
    | null -> []
    | :? JsonObject as o -> o |> Seq.collect (fun kv -> kv.Key :: strings kv.Value) |> List.ofSeq
    | :? JsonArray as a -> a |> Seq.collect strings |> List.ofSeq
    | v -> (try [ v.GetValue<string>() ] with _ -> [ v.ToJsonString() ])

// ---- paths ----
let normPath (p: string) =
    p.Replace('\\', '/').Split('/')
    |> Array.fold (fun acc seg -> match seg with "" | "." -> acc | ".." -> (match acc with _ :: t -> t | [] -> []) | s -> s :: acc) []
    |> List.rev |> String.concat "/"
let globMatch (pattern: string) (path: string) =
    let rx = "^" + (Regex.Escape pattern).Replace(@"\*\*/", "(.*/)?").Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*") + "$"
    Regex.IsMatch(normPath path, rx)
let isProtected (p: string) = protectedGlobs |> List.exists (fun g -> globMatch g p)

// ---- inputs ----
type Base =
    { Readable: bool                       // false: --base-ref given but not a commit here
      Authority: string option             // raw text at the base, when it exists there
      Directive: string option
      Decisions: JsonNode list
      Changed: string list }               // paths changed between the base and HEAD
type Inputs =
    { AuthorityText: string
      Authority: JsonNode
      DirectiveText: string option         // None: the directive file is missing
      Decisions: JsonNode list
      Base: Base option
      OwnerRequestIds: Set<string>         // every id in every owner-decision-requests file (upper-case)
      Approvals: (string * JsonNode) list }

type Refusal = { Code: string; Subject: string; Detail: string }
let refuse code subject detail = { Code = code; Subject = subject; Detail = detail }
let classes k (a: JsonNode) = arr k a |> List.map (strOr "class") |> Set.ofList
let ownerLogin (i: Inputs) = field "source" i.Authority |> Option.map (strOr "owner") |> Option.defaultValue ""
let sha256Text (s: string) = Encoding.UTF8.GetBytes s |> SHA256.HashData |> Convert.ToHexString |> fun h -> h.ToLowerInvariant()
let baseIds (i: Inputs) = i.Base |> Option.map (fun b -> b.Decisions |> List.map (strOr "id") |> Set.ofList) |> Option.defaultValue Set.empty

// ---- authority rules ----
let authorityRules : (Inputs -> Refusal list) list =
    [ fun i ->
        [ if strOr "schema" i.Authority <> "indy-init.delegated-authority/1" then refuse "DA_SCHEMA" "authority" "unknown schema"
          if not (boolOr "notAnOwnerApproval" i.Authority) then refuse "DA_CLAIMS_OWNER_APPROVAL" "authority" "notAnOwnerApproval must be true"
          if field "source" i.Authority |> Option.map (strOr "kind") <> Some "owner-directive-in-session" then refuse "DA_SOURCE_KIND" "authority" "source.kind must be owner-directive-in-session" ]
      fun i ->
        let declared = field "source" i.Authority |> Option.map (strOr "sha256") |> Option.defaultValue ""
        match i.DirectiveText with
        | None -> [ refuse "DA_DIRECTIVE_MISSING" "authority" "the directive text file is missing" ]
        | Some t when sha256Text t <> declared -> [ refuse "DA_DIRECTIVE_DIGEST" "authority" $"directive sha256 {sha256Text t} <> recorded {declared}" ]
        | Some _ -> []
      fun i ->
        let c, r = classes "covered" i.Authority, classes "reserved" i.Authority
        [ if r <> reservedClasses then refuse "DA_RESERVED_CHANGED" "authority" "the reserved classes must be exactly the eight the directive reserves"
          if Set.isEmpty c then refuse "DA_NOTHING_COVERED" "authority" "no covered classes"
          yield! Set.difference c coveredClasses |> Set.toList |> List.map (fun x -> refuse "DA_COVERED_UNKNOWN" "authority" x) ]
      fun i ->
        match time (strOr "validFrom" i.Authority), time (strOr "validUntil" i.Authority) with
        | Some f, Some u when f >= directiveReceived && f < u && u <= latestValidUntil -> []
        | _ -> [ refuse "DA_VALIDITY" "authority" "validity must start at or after the directive and end by 2026-11-07T23:59:59Z" ]
      fun i ->
        // review B2: once on the base, the authority record and the directive are frozen (only the owner changes them)
        match i.Base with
        | Some b when not b.Readable -> [ refuse "DA_BASE_UNREADABLE" "base" "the --base-ref is not a commit here; the audit cannot run" ]
        | Some b ->
            [ match b.Authority with Some t when t <> i.AuthorityText -> refuse "DA_AUTHORITY_CHANGED" "authority" "delegated-authority.json differs from the base" | _ -> ()
              match b.Directive, i.DirectiveText with
              | Some t, Some u when t <> u -> refuse "DA_AUTHORITY_CHANGED" "directive" "the directive text differs from the base"
              | Some _, None -> refuse "DA_AUTHORITY_CHANGED" "directive" "the directive text was removed"
              | _ -> () ]
        | None -> [] ]

// ---- per-decision rules ----
let decisionRules : (Inputs -> JsonNode -> Refusal list) list =
    let idOf d = strOr "id" d
    [ fun _ d -> [ if not (Regex.IsMatch(idOf d, @"^DTD-R10-\d{3}$")) then refuse "DTD_ID" (idOf d) "id must be DTD-R10-NNN" ]
      fun _ d ->
        let c = strOr "class" d
        [ if reservedClasses.Contains c then refuse "DTD_RESERVED_TO_OWNER" (idOf d) $"class '{c}' is reserved to the owner"
          elif not (coveredClasses.Contains c) then refuse "DTD_NOT_COVERED" (idOf d) $"class '{c}' is not covered by the directive" ]
      fun _ d ->
        let agent = field "actingAgent" d
        let kind = agent |> Option.map (strOr "kind") |> Option.defaultValue ""
        let session = agent |> Option.map (strOr "session") |> Option.defaultValue ""
        [ if kind <> "agent" then refuse "DTD_NOT_AN_AGENT" (idOf d) $"actingAgent.kind '{kind}': only an identified agent may decide under delegation"
          if not (Regex.IsMatch(session, @"^session_[A-Za-z0-9]{8,}$")) then refuse "DTD_AGENT_UNIDENTIFIED" (idOf d) "actingAgent.session must name the deciding session"
          yield! agent |> Option.map keys |> Option.defaultValue [] |> List.filter (agentFields.Contains >> not) |> List.map (fun k -> refuse "DTD_UNKNOWN_FIELD" (idOf d) $"actingAgent.{k}") ]
      fun _ d ->
        // review M1: a fixed field set, so nothing like approvedBy, ownerApproval, authority or signature can ride along
        keys d |> List.filter (decisionFields.Contains >> not) |> List.map (fun k -> refuse "DTD_UNKNOWN_FIELD" (idOf d) k)
      fun i d ->
        let login = ownerLogin i
        [ if login <> "" && strings d |> List.exists (fun s -> s.Contains(login, StringComparison.OrdinalIgnoreCase)) then
              refuse "DTD_OWNER_IMPERSONATION" (idOf d) "a delegated decision may not name the owner's login" ]
      fun i d ->
        let window = time (strOr "validFrom" i.Authority), time (strOr "validUntil" i.Authority)
        [ match time (strOr "decidedAt" d), window with
          | Some t, (Some f, Some u) when t >= f && t <= u -> ()
          | _ -> refuse "DTD_OUTSIDE_WINDOW" (idOf d) "decidedAt missing or outside the authority's validity window" ]
      fun i d ->
        // reviews B1/H1: owner-decision ids anywhere in the decision, normalized, case-insensitive
        let text = strings d |> List.map normHyphens
        let shaped = text |> List.collect (fun s -> [ for m in ownerIdRx.Matches s -> m.Value.ToUpperInvariant() ])
        let listed =
            i.OwnerRequestIds |> Set.filter (fun r ->
                text |> List.exists (fun s -> Regex.IsMatch(s, $@"(?<![A-Za-z0-9]){Regex.Escape r}(?![0-9])", RegexOptions.IgnoreCase)))
        (Set.ofList shaped + listed) |> Set.toList
        |> List.map (fun r -> refuse "DTD_OWNER_DECISION_ID" (idOf d) $"{r}: an owner decision needs the owner's own record; delegation may not name, decide or reopen it")
      fun _ d ->
        arr "paths" d |> List.choose (fun n -> try Some(n.GetValue<string>()) with _ -> None)
        |> List.filter (fun p -> isProtected p || normPath p = logPath)
        |> List.map (fun p -> refuse "DTD_PROTECTED_PATH" (idOf d) p)
      fun _ d ->
        [ for k in [ "decision"; "rationale"; "evidence" ] do
            if (strOr k d).Trim() = "" then refuse "DTD_INCOMPLETE" (idOf d) $"{k} is empty" ] ]

// ---- log-wide rules ----
let logRules : (Inputs -> Refusal list) list =
    [ fun i ->
        i.Decisions |> List.countBy (strOr "id") |> List.filter (snd >> (<) 1)
        |> List.map (fun (id, _) -> refuse "DTD_DUPLICATE_ID" id "id used more than once")
      fun i ->
        let now = i.Decisions |> List.map (fun d -> strOr "id" d, d.ToJsonString()) |> Map.ofList
        i.Base |> Option.map (fun b -> b.Decisions) |> Option.defaultValue []
        |> List.choose (fun b ->
            let id = strOr "id" b
            match Map.tryFind id now with
            | None -> Some(refuse "DTD_AUDIT_REMOVED" id "an entry on the base ref was removed")
            | Some s when s <> b.ToJsonString() -> Some(refuse "DTD_AUDIT_EDITED" id "an entry on the base ref was edited")
            | Some _ -> None)
      fun i ->
        // review B3: the real diff. A change that edits a protected path may not also add delegated decisions. The PR
        // that first introduces the authority (nothing on the base yet) is exempt; it is reviewed as a whole.
        match i.Base with
        | Some b when b.Authority.IsSome ->
            let fresh = i.Decisions |> List.map (strOr "id") |> List.filter (fun id -> not ((baseIds i).Contains id))
            let touched = b.Changed |> List.filter isProtected
            if fresh.IsEmpty || touched.IsEmpty then []
            else (let paths = String.concat ", " touched in [ refuse "DTD_WITH_PROTECTED_CHANGE" (String.concat "," fresh) $"this change also edits protected paths: {paths}" ])
        | _ -> []
      fun i ->
        i.Approvals |> List.choose (fun (path, a) ->
            let kind = field "authority" a |> Option.map (strOr "kind") |> Option.defaultValue ""
            let text = a.ToJsonString().ToUpperInvariant()
            if kind = "delegated" || text.Contains "DTD-R10" || text.Contains "DA-R10" then
                Some(refuse "APPROVAL_LAUNDERED" (strOr "id" a) $"{path} cites a delegated record")
            else None) ]

let check (i: Inputs) : Refusal list =
    (authorityRules |> List.collect (fun r -> r i))
    @ (i.Decisions |> List.collect (fun d -> decisionRules |> List.collect (fun r -> r i d)))
    @ (logRules |> List.collect (fun r -> r i))

// ---- loading ----
let run (root: string) (a: string) =
    let psi = Diagnostics.ProcessStartInfo("git", $"-C \"{root}\" {a}", RedirectStandardOutput = true, RedirectStandardError = true)
    use p = Diagnostics.Process.Start psi
    let out = p.StandardOutput.ReadToEnd()
    p.WaitForExit()
    if p.ExitCode = 0 then Some out else None
let repoFiles (root: string) (pred: string -> bool) =
    Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
    |> Seq.map (fun p -> Path.GetRelativePath(root, p).Replace('\\', '/'))
    |> Seq.filter (fun p -> not (p.StartsWith ".git/") && not (p.Contains "/bin/") && not (p.Contains "/obj/") && not (p.Contains "node_modules/") && pred p)
    |> Seq.sort |> List.ofSeq

let load (root: string) (baseRef: string option) : Inputs =
    let read p = File.ReadAllText(Path.Combine(root, p))
    let authorityText = read $"{dir}/delegated-authority.json"
    let authority = JsonNode.Parse authorityText
    let directivePath = field "source" authority |> Option.map (strOr "text") |> Option.defaultValue ""
    let directive = Path.Combine(root, directivePath)
    { AuthorityText = authorityText
      Authority = authority
      DirectiveText = if directivePath <> "" && File.Exists directive then Some(File.ReadAllText directive) else None
      Decisions = arr "decisions" (JsonNode.Parse(read logPath))
      Base =
        baseRef |> Option.map (fun r ->
            match run root $"rev-parse --verify --quiet {r}^{{commit}}" with
            | None -> { Readable = false; Authority = None; Directive = None; Decisions = []; Changed = [] }
            | Some _ ->
                { Readable = true
                  Authority = run root $"show {r}:{dir}/delegated-authority.json"
                  Directive = if directivePath = "" then None else run root $"show {r}:{directivePath}"
                  Decisions = run root $"show {r}:{logPath}" |> Option.map (fun s -> arr "decisions" (JsonNode.Parse s)) |> Option.defaultValue []
                  // committed changes against the base plus uncommitted ones; an unreadable diff fails closed
                  Changed =
                    let lines (s: string) = s.Split('\n', StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
                    let committed = run root $"diff --name-only {r} HEAD" |> Option.map lines |> Option.defaultValue [ "**/approvals-diff-unavailable.json" ]
                    let pending = run root "status --porcelain" |> Option.map (lines >> List.map (fun l -> l.Substring(3).Trim())) |> Option.defaultValue []
                    committed @ pending })
      OwnerRequestIds =
        repoFiles root (fun p -> Path.GetFileName(p).StartsWith "owner-decision-requests")
        |> List.collect (fun p -> try arr "requests" (JsonNode.Parse(read p)) |> List.map (strOr "id") with _ -> [])
        |> List.filter ((<>) "") |> List.map (fun s -> s.ToUpperInvariant()) |> Set.ofList
      Approvals =
        repoFiles root (fun p -> Path.GetFileName(p).StartsWith "approvals")
        |> List.collect (fun p -> try arr "approvals" (JsonNode.Parse(read p)) |> List.map (fun a -> p, a) with _ -> []) }

// ---- tests: each case mutates valid inputs and names the refusal it must (or must not) produce ----
let tests (root: string) =
    let real = load root None
    let put (k: string) (v: JsonNode) (n: JsonNode) = let c = n.DeepClone() in c.AsObject()[k] <- v; c
    let without (k: string) (n: JsonNode) = let c = n.DeepClone() in c.AsObject().Remove k |> ignore; c
    let j (s: string) = JsonNode.Parse s
    let jstr (s: string) : JsonNode = JsonValue.Create s
    let good =
        j """{"id":"DTD-R10-900","class":"technical-interface","decision":"Expose Workspace.load as a stable F# interface","rationale":"directive section 4","evidence":"PR","decidedAt":"2026-11-06T10:00:00Z","paths":["src/X.fs"],"actingAgent":{"kind":"agent","session":"session_TEST0001"}}"""
    let withDecision d = { real with Decisions = real.Decisions @ [ d ] }
    let auth k v = { real with Authority = real.Authority |> put k v }
    let base' = { Readable = true; Authority = Some real.AuthorityText; Directive = real.DirectiveText; Decisions = real.Decisions; Changed = [ logPath ] }
    let onBase b i = { i with Base = Some b }
    let codes i = check i |> List.map (fun r -> r.Code) |> Set.ofList
    let reservedJson = reservedClasses |> Set.toList |> List.map (sprintf "{\"class\":\"%s\"}") |> String.concat ","
    let cases : (string * Inputs * string option) list =
        [ "committed records pass", real, None
          "committed records pass against themselves as base", real |> onBase base', None
          "positive: covered class by an identified agent", withDecision good, None
          "positive: each covered class is accepted",
            { real with Decisions = coveredClasses |> Set.toList |> List.mapi (fun n c -> good |> put "id" (jstr $"DTD-R10-{900 + n}") |> put "class" (jstr c)) }, None
          "positive: a new decision with only the log changed", withDecision good |> onBase base', None
          // classes
          "reserved: acceptance-weakening", withDecision (good |> put "class" (jstr "acceptance-weakening")), Some "DTD_RESERVED_TO_OWNER"
          "reserved: official-build-start", withDecision (good |> put "class" (jstr "official-build-start")), Some "DTD_RESERVED_TO_OWNER"
          "reserved: access-expansion", withDecision (good |> put "class" (jstr "access-expansion")), Some "DTD_RESERVED_TO_OWNER"
          "uncovered class", withDecision (good |> put "class" (jstr "budget-increase")), Some "DTD_NOT_COVERED"
          // owner-decision ids (B1, H1)
          "an id from a requests file in decides", withDecision (good |> put "decides" (jstr "ODR-R10-19")), Some "DTD_OWNER_DECISION_ID"
          "an id in no requests file (OD-V415-01)", withDecision (good |> put "decision" (jstr "Choose OD-V415-01 A")), Some "DTD_OWNER_DECISION_ID"
          "a lower-case id", withDecision (good |> put "decision" (jstr "per odr-r10-19")), Some "DTD_OWNER_DECISION_ID"
          "a lower-case id in no requests file", withDecision (good |> put "decision" (jstr "per od-v415-01")), Some "DTD_OWNER_DECISION_ID"
          "non-breaking hyphens", withDecision (good |> put "decision" (jstr "per ODR‑R10‑19")), Some "DTD_OWNER_DECISION_ID"
          "a hyphen before the id", withDecision (good |> put "decision" (jstr "pre-ODR-R10-32 staging")), Some "DTD_OWNER_DECISION_ID"
          "an id in the rationale", withDecision (good |> put "rationale" (jstr "as CF-07 needs")), Some "DTD_OWNER_DECISION_ID"
          "an id in the evidence", withDecision (good |> put "evidence" (jstr "APR-R10-OWNER-3")), Some "DTD_OWNER_DECISION_ID"
          "control: CF-070-like words are not CF ids", withDecision (good |> put "decision" (jstr "use the SCF-07 buffer")), None
          "control: a listed id inside a longer number is not matched",
            { withDecision (good |> put "decision" (jstr "X-1007")) with OwnerRequestIds = Set.ofList [ "X-100" ] }, None
          "a listed id with an unusual prefix", { withDecision (good |> put "decision" (jstr "per X-100")) with OwnerRequestIds = Set.ofList [ "X-100" ] }, Some "DTD_OWNER_DECISION_ID"
          // identity (M1)
          "the owner's login anywhere", withDecision (good |> put "evidence" (jstr "agreed with KEMILLER2002")), Some "DTD_OWNER_IMPERSONATION"
          "an approvedBy field", withDecision (good |> put "approvedBy" (jstr "someone")), Some "DTD_UNKNOWN_FIELD"
          "an ownerApproval flag", withDecision (good |> put "ownerApproval" (JsonValue.Create true)), Some "DTD_UNKNOWN_FIELD"
          "an authority field", withDecision (good |> put "authority" (j """{"kind":"operator","id":"x"}""")), Some "DTD_UNKNOWN_FIELD"
          "a signature field", withDecision (good |> put "signature" (jstr "abc")), Some "DTD_UNKNOWN_FIELD"
          "an extra actingAgent field", withDecision (good |> put "actingAgent" (j """{"kind":"agent","session":"session_TEST0001","onBehalfOf":"owner"}""")), Some "DTD_UNKNOWN_FIELD"
          "operator authority", withDecision (good |> put "actingAgent" (j """{"kind":"operator","session":"session_TEST0001"}""")), Some "DTD_NOT_AN_AGENT"
          "unidentified agent", withDecision (good |> put "actingAgent" (j """{"kind":"agent"}""")), Some "DTD_AGENT_UNIDENTIFIED"
          "a session that is not a session id", withDecision (good |> put "actingAgent" (j """{"kind":"agent","session":"session_x y"}""")), Some "DTD_AGENT_UNIDENTIFIED"
          // window
          "after the window", withDecision (good |> put "decidedAt" (jstr "2026-11-08T00:00:01Z")), Some "DTD_OUTSIDE_WINDOW"
          "before the directive", withDecision (good |> put "decidedAt" (jstr "2026-11-05T19:00:00Z")), Some "DTD_OUTSIDE_WINDOW"
          // declared paths (B3)
          "declares an approvals file", withDecision (good |> put "paths" (j """["kickoff/rehearsal-007/approvals.causa.json"]""")), Some "DTD_PROTECTED_PATH"
          "declares it with // and ./", withDecision (good |> put "paths" (j """["kickoff//rehearsal-007/./approvals.causa.json"]""")), Some "DTD_PROTECTED_PATH"
          "declares a nested approvals file", withDecision (good |> put "paths" (j """["kickoff/a/b/approvals.json"]""")), Some "DTD_PROTECTED_PATH"
          "declares a requests file outside kickoff", withDecision (good |> put "paths" (j """["requirements-compiled/v4.1.5/proposals/owner-decision-requests.json"]""")), Some "DTD_PROTECTED_PATH"
          "declares the decision log", withDecision (good |> put "paths" (j $"""["{logPath}"]""")), Some "DTD_PROTECTED_PATH"
          "declares this checker", withDecision (good |> put "paths" (j """["tools/r10/delegated-authority.fsx"]""")), Some "DTD_PROTECTED_PATH"
          "declares a workflow", withDecision (good |> put "paths" (j """[".github/workflows/preflight-tooling.yml"]""")), Some "DTD_PROTECTED_PATH"
          "declares verification/", withDecision (good |> put "paths" (j """["verification/journeys/x.spec.js"]""")), Some "DTD_PROTECTED_PATH"
          "declares ../ escaping into a protected path", withDecision (good |> put "paths" (j """["src/../tools/owner-freeze.fsx"]""")), Some "DTD_PROTECTED_PATH"
          // the real diff (B3)
          "a new decision in a change that edits an approvals file",
            withDecision good |> onBase { base' with Changed = [ logPath; "kickoff/rehearsal-007/approvals.causa.json" ] }, Some "DTD_WITH_PROTECTED_CHANGE"
          "control: a protected edit with no new decision", real |> onBase { base' with Changed = [ ".github/workflows/x.yml" ] }, None
          "diff unavailable counts as protected? no: an unknown path is not protected, but a missing base is refused",
            withDecision good |> onBase { base' with Readable = false }, Some "DA_BASE_UNREADABLE"
          // incomplete and ids
          "no rationale", withDecision (good |> put "rationale" (jstr " ")), Some "DTD_INCOMPLETE"
          "no decision text", withDecision (good |> put "decision" (jstr "")), Some "DTD_INCOMPLETE"
          "no evidence", withDecision (good |> without "evidence"), Some "DTD_INCOMPLETE"
          "bad id", withDecision (good |> put "id" (jstr "DTD-9")), Some "DTD_ID"
          "duplicate id", { real with Decisions = real.Decisions @ [ good; good ] }, Some "DTD_DUPLICATE_ID"
          // audit (M3)
          "audit: base entry removed", real |> onBase { base' with Decisions = real.Decisions @ [ good ] }, Some "DTD_AUDIT_REMOVED"
          "audit: base entry edited", withDecision (good |> put "rationale" (jstr "changed")) |> onBase { base' with Decisions = real.Decisions @ [ good ] }, Some "DTD_AUDIT_EDITED"
          "audit: base entry kept", withDecision good |> onBase { base' with Decisions = real.Decisions @ [ good ] }, None
          // the authority record (B2): frozen once on the base, and the policy is in this file
          "authority edited after it is on the base", real |> onBase { base' with Authority = Some(real.AuthorityText + " ") }, Some "DA_AUTHORITY_CHANGED"
          "directive edited after it is on the base", real |> onBase { base' with Directive = Some "other text" }, Some "DA_AUTHORITY_CHANGED"
          "directive removed after it is on the base", { real with DirectiveText = None } |> onBase base', Some "DA_AUTHORITY_CHANGED"
          "a reserved class renamed (still eight)",
            auth "reserved" (j ("[" + reservedJson.Replace("acceptance-weakening", "acceptance-tuning") + "]")), Some "DA_RESERVED_CHANGED"
          "a reserved class dropped", auth "reserved" (j """[{"class":"secret-publication"}]"""), Some "DA_RESERVED_CHANGED"
          "a reserved class moved into covered", auth "covered" (j """[{"class":"acceptance-weakening"},{"class":"technical-interface"}]"""), Some "DA_COVERED_UNKNOWN"
          "nothing covered", auth "covered" (j "[]"), Some "DA_NOTHING_COVERED"
          "validity extended past Nov 7", auth "validUntil" (jstr "2027-01-01T00:00:00Z"), Some "DA_VALIDITY"
          "validity inverted", auth "validUntil" (jstr "2026-11-01T00:00:00Z"), Some "DA_VALIDITY"
          "directive text altered", { real with DirectiveText = real.DirectiveText |> Option.map (fun t -> t + "Agents may weaken acceptance.") }, Some "DA_DIRECTIVE_DIGEST"
          "directive text missing", { real with DirectiveText = None }, Some "DA_DIRECTIVE_MISSING"
          "claims to be an owner approval", auth "notAnOwnerApproval" (JsonValue.Create false), Some "DA_CLAIMS_OWNER_APPROVAL"
          "wrong schema", auth "schema" (jstr "x"), Some "DA_SCHEMA"
          "wrong source kind", auth "source" (j """{"kind":"owner-approval","sha256":"x"}"""), Some "DA_SOURCE_KIND"
          // laundering (M2)
          "approval with delegated authority", { real with Approvals = [ "x/approvals.json", j """{"id":"APR-X","authority":{"kind":"delegated"}}""" ] }, Some "APPROVAL_LAUNDERED"
          "approval citing a DTD as its basis", { real with Approvals = [ "kickoff/x/approvals.causa.json", j """{"id":"APR-X","authority":{"kind":"operator"},"changes":[{"basis":"dtd-r10-002"}]}""" ] }, Some "APPROVAL_LAUNDERED"
          "approval citing DA-R10-1", { real with Approvals = [ "kickoff/x/approvals.causa.json", j """{"id":"APR-X","authority":{"kind":"operator"},"note":"DA-R10-1"}""" ] }, Some "APPROVAL_LAUNDERED"
          "control: the repository's real approvals are clean", { real with Approvals = real.Approvals }, None ]
    let loaderBites = [ "the loader finds approvals files", not real.Approvals.IsEmpty; "the loader finds owner request ids", real.OwnerRequestIds.Contains "ODR-R10-19" ]
    let results =
        (cases |> List.map (fun (name, i, expect) ->
            let got = codes i
            let ok = match expect with None -> Set.isEmpty got | Some c -> got.Contains c
            name, ok, (if ok then "" else $"  (expected {expect}, got {got |> Set.toList})")))
        @ (loaderBites |> List.map (fun (n, ok) -> n, ok, ""))
    results |> List.iter (fun (n, ok, why) -> printfn "%s  %s%s" (if ok then "PASS" else "FAIL") n why)
    let failed = results |> List.filter (fun (_, ok, _) -> not ok) |> List.length
    printfn "%d/%d cases pass" (List.length results - failed) (List.length results)
    if failed > 0 then 1 else 0

let rec opt (k: string) = function | a :: v :: _ when a = k -> Some v | _ :: t -> opt k t | [] -> None
let root = opt "--indy-init" args |> Option.defaultValue "."
let code =
    match args |> List.filter (fun a -> a = "check" || a = "test") with
    | [ "check" ] ->
        let inputs = load root (opt "--base-ref" args)
        let refusals = check inputs
        refusals |> List.iter (fun r -> printfn "REFUSED %s %s: %s" r.Code r.Subject r.Detail)
        if List.isEmpty refusals then
            printfn "delegated authority: %d decision(s) pass the format and scope check (not owner approvals; each new entry still needs review)" inputs.Decisions.Length
            0
        else 1
    | [ "test" ] -> tests root
    | _ -> eprintfn "usage: delegated-authority.fsx -- --indy-init DIR (check [--base-ref REF] | test)"; 2
exit code
