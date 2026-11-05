// delegated-authority.fsx: checks agent decisions made under the owner's standing technical authorization (DA-R10-1).
//
//   dotnet fsi tools/r10/delegated-authority.fsx -- --indy-init . check [--base-ref origin/main]
//   dotnet fsi tools/r10/delegated-authority.fsx -- --indy-init . test
//
// The authority record (kickoff/rehearsal-010/delegated/delegated-authority.json) cites the owner's directive by sha256.
// It is NOT an owner approval. The checker refuses:
//   - a decision whose class is reserved to the owner, or not covered;
//   - a decision that names an owner-decision request id (those need the owner's own signed record, RDP-003);
//   - a decision made by anyone but an identified agent session (no owner identity, no "operator" authority);
//   - a decision outside the validity window, a duplicate or malformed id, a missing rationale or evidence;
//   - a decision that touches a protected path (approvals, owner requests, the authority record, the directive);
//   - an edited or removed entry that is already on the base ref (append-only audit trail);
//   - any approvals file that carries a delegated record (no laundering into owner approvals);
//   - an authority record whose directive digest does not match, or whose covered and reserved classes overlap.
// Every rule is a pure function from the loaded inputs to a list of refusals.

open System
open System.IO
open System.Security.Cryptography
open System.Text.Json.Nodes
open System.Text.RegularExpressions

let args = fsi.CommandLineArgs |> Array.skip 1 |> List.ofArray |> List.skipWhile ((=) "--")

// ---- json helpers (total: a missing field reads as None / empty) ----
let field (k: string) (n: JsonNode) : JsonNode option =
    match n with
    | :? JsonObject as o -> (let mutable v : JsonNode = null in if o.TryGetPropertyValue(k, &v) && not (isNull v) then Some v else None)
    | _ -> None
let str k n = field k n |> Option.bind (fun v -> try Some(v.GetValue<string>()) with _ -> None)
let strOr k n = str k n |> Option.defaultValue ""
let arr k n = field k n |> Option.map (fun v -> v.AsArray() |> Seq.toList) |> Option.defaultValue []
let boolOr k n = field k n |> Option.bind (fun v -> try Some(v.GetValue<bool>()) with _ -> None) |> Option.defaultValue false
let time (s: string) = match DateTimeOffset.TryParse s with | true, t -> Some t | _ -> None

// ---- inputs ----
type Inputs =
    { Authority: JsonNode
      DirectiveSha: string option          // None: the directive file is missing
      Decisions: JsonNode list
      BaseDecisions: JsonNode list         // entries on the base ref (append-only check)
      OwnerRequestIds: Set<string>         // every id in every owner-decision-requests.json
      Approvals: (string * JsonNode) list } // (path, approval record)

type Refusal = { Code: string; Subject: string; Detail: string }
let refuse code subject detail = { Code = code; Subject = subject; Detail = detail }

let classes k (a: JsonNode) = arr k a |> List.map (strOr "class") |> Set.ofList

let globMatch (pattern: string) (path: string) =
    let rx = "^" + (Regex.Escape pattern).Replace(@"\*", "[^/]*") + "$"
    Regex.IsMatch(path.Replace('\\', '/').TrimStart('.', '/'), rx)

let ownerIdOf (a: JsonNode) = field "source" a |> Option.map (strOr "owner") |> Option.defaultValue ""

// ---- authority rules ----
let authorityRules : (Inputs -> Refusal list) list =
    [ fun i ->
        [ if strOr "schema" i.Authority <> "indy-init.delegated-authority/1" then refuse "DA_SCHEMA" "authority" "unknown schema"
          if not (boolOr "notAnOwnerApproval" i.Authority) then refuse "DA_CLAIMS_OWNER_APPROVAL" "authority" "notAnOwnerApproval must be true" ]
      fun i ->
        let src = field "source" i.Authority
        let declared = src |> Option.map (strOr "sha256") |> Option.defaultValue ""
        [ match i.DirectiveSha with
          | None -> refuse "DA_DIRECTIVE_MISSING" "authority" "the directive text file is missing"
          | Some h when h <> declared -> refuse "DA_DIRECTIVE_DIGEST" "authority" $"directive sha256 {h} <> recorded {declared}"
          | Some _ -> ()
          if src |> Option.map (strOr "kind") <> Some "owner-directive-in-session" then refuse "DA_SOURCE_KIND" "authority" "source.kind must be owner-directive-in-session" ]
      fun i ->
        let c, r = classes "covered" i.Authority, classes "reserved" i.Authority
        [ if Set.isEmpty c then refuse "DA_NOTHING_COVERED" "authority" "no covered classes"
          if Set.count r < 8 then refuse "DA_RESERVED_INCOMPLETE" "authority" "the eight owner-reserved classes must all be listed"
          yield! Set.intersect c r |> Set.toList |> List.map (fun x -> refuse "DA_COVERED_RESERVED_OVERLAP" "authority" x) ]
      fun i ->
        match time (strOr "validFrom" i.Authority), time (strOr "validUntil" i.Authority) with
        | Some f, Some u when f < u -> []
        | _ -> [ refuse "DA_VALIDITY" "authority" "validFrom/validUntil missing or inverted" ] ]

// ---- per-decision rules ----
let decisionRules : (Inputs -> JsonNode -> Refusal list) list =
    let idOf d = strOr "id" d
    [ fun _ d ->
        [ if not (Regex.IsMatch(idOf d, @"^DTD-R10-\d{3}$")) then refuse "DTD_ID" (idOf d) "id must be DTD-R10-NNN" ]
      fun i d ->
        let c = strOr "class" d
        [ if (classes "reserved" i.Authority).Contains c then refuse "DTD_RESERVED_TO_OWNER" (idOf d) $"class '{c}' is reserved to the owner"
          elif not ((classes "covered" i.Authority).Contains c) then refuse "DTD_NOT_COVERED" (idOf d) $"class '{c}' is not covered by the directive" ]
      fun i d ->
        let agent = field "actingAgent" d
        let kind = agent |> Option.map (strOr "kind") |> Option.defaultValue ""
        let session = agent |> Option.map (strOr "session") |> Option.defaultValue ""
        let owner = ownerIdOf i.Authority
        [ if kind <> "agent" then refuse "DTD_NOT_AN_AGENT" (idOf d) $"actingAgent.kind '{kind}': only an identified agent may decide under delegation"
          if not (Regex.IsMatch(session, @"^session_[A-Za-z0-9]+$")) then refuse "DTD_AGENT_UNIDENTIFIED" (idOf d) "actingAgent.session must name the deciding session"
          let mentions = agent |> Option.map (fun a -> a.ToJsonString()) |> Option.defaultValue ""
          if owner <> "" && mentions.Contains(owner, StringComparison.OrdinalIgnoreCase) then refuse "DTD_OWNER_IMPERSONATION" (idOf d) "actingAgent names the owner"
          if field "authority" d |> Option.isSome || field "signature" d |> Option.isSome then refuse "DTD_OWNER_IMPERSONATION" (idOf d) "a delegated decision carries no authority or signature field" ]
      fun i d ->
        let window = time (strOr "validFrom" i.Authority), time (strOr "validUntil" i.Authority)
        [ match time (strOr "decidedAt" d), window with
          | Some t, (Some f, Some u) when t >= f && t <= u -> ()
          | _ -> refuse "DTD_OUTSIDE_WINDOW" (idOf d) "decidedAt missing or outside the authority's validity window" ]
      fun i d ->
        let named = strOr "decides" d :: [ strOr "decision" d ]
        let hits = i.OwnerRequestIds |> Set.filter (fun r -> named |> List.exists (fun s -> Regex.IsMatch(s, $@"(^|[^A-Za-z0-9-]){Regex.Escape r}($|[^0-9])")))
        hits |> Set.toList |> List.map (fun r -> refuse "DTD_OWNER_REQUEST" (idOf d) $"{r} is an owner-decision request: it needs the owner's own record")
      fun i d ->
        let protectedPaths = arr "protectedPaths" i.Authority |> List.map (fun n -> n.GetValue<string>())
        arr "paths" d |> List.map (fun n -> n.GetValue<string>())
        |> List.filter (fun p -> protectedPaths |> List.exists (fun g -> globMatch g p))
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
        i.BaseDecisions |> List.choose (fun b ->
            let id = strOr "id" b
            match Map.tryFind id now with
            | None -> Some(refuse "DTD_AUDIT_REMOVED" id "an entry on the base ref was removed")
            | Some s when s <> b.ToJsonString() -> Some(refuse "DTD_AUDIT_EDITED" id "an entry on the base ref was edited")
            | Some _ -> None)
      fun i ->
        i.Approvals |> List.choose (fun (path, a) ->
            let kind = field "authority" a |> Option.map (strOr "kind") |> Option.defaultValue ""
            if kind = "delegated" || (strOr "id" a).StartsWith "DTD-" || a.ToJsonString().Contains "DA-R10-1" then
                Some(refuse "APPROVAL_LAUNDERED" (strOr "id" a) $"{path} carries a delegated record")
            else None) ]

let check (i: Inputs) : Refusal list =
    (authorityRules |> List.collect (fun r -> r i))
    @ (i.Decisions |> List.collect (fun d -> decisionRules |> List.collect (fun r -> r i d)))
    @ (logRules |> List.collect (fun r -> r i))

// ---- loading ----
let sha256 (p: string) = File.ReadAllBytes p |> SHA256.HashData |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()
let repoFiles (root: string) (pred: string -> bool) =
    Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
    |> Seq.map (fun p -> Path.GetRelativePath(root, p).Replace('\\', '/'))
    |> Seq.filter (fun p -> not (p.StartsWith ".git/") && not (p.Contains "/bin/") && not (p.Contains "/obj/") && pred p)
    |> Seq.sort |> List.ofSeq

let gitShow (root: string) (spec: string) =
    let psi = Diagnostics.ProcessStartInfo("git", $"-C \"{root}\" show {spec}", RedirectStandardOutput = true, RedirectStandardError = true)
    use p = Diagnostics.Process.Start psi
    let out = p.StandardOutput.ReadToEnd()
    p.WaitForExit()
    if p.ExitCode = 0 then Some out else None

let dir = "kickoff/rehearsal-010/delegated"
let load (root: string) (baseRef: string option) : Inputs =
    let parse p = JsonNode.Parse(File.ReadAllText(Path.Combine(root, p)))
    let authority = parse $"{dir}/delegated-authority.json"
    let directive = Path.Combine(root, field "source" authority |> Option.map (strOr "text") |> Option.defaultValue "")
    { Authority = authority
      DirectiveSha = if File.Exists directive then Some(sha256 directive) else None
      Decisions = arr "decisions" (parse $"{dir}/delegated-decisions.json")
      BaseDecisions =
        baseRef |> Option.bind (fun r -> gitShow root $"{r}:{dir}/delegated-decisions.json")
        |> Option.map (fun s -> arr "decisions" (JsonNode.Parse s)) |> Option.defaultValue []
      OwnerRequestIds =
        repoFiles root (fun p -> Path.GetFileName(p).StartsWith "owner-decision-requests")
        |> List.collect (fun p -> try arr "requests" (parse p) |> List.map (strOr "id") with _ -> [])
        |> List.filter ((<>) "") |> Set.ofList
      Approvals =
        repoFiles root (fun p -> p.StartsWith "kickoff/" && Path.GetFileName(p).StartsWith "approvals")
        |> List.collect (fun p -> try arr "approvals" (parse p) |> List.map (fun a -> p, a) with _ -> []) }

// ---- tests: each case mutates valid inputs and names the refusal it must (or must not) produce ----
let tests (root: string) =
    let real = load root None
    let clone (n: JsonNode) = n.DeepClone()
    let set (k: string) (v: JsonNode) (n: JsonNode) = let c = clone n in c.AsObject()[k] <- v; c
    let jstr (s: string) : JsonNode = JsonValue.Create s
    let good =
        JsonNode.Parse """{"id":"DTD-R10-900","class":"technical-interface","decision":"Expose Workspace.load as a stable F# interface","rationale":"directive 4","evidence":"PR","decidedAt":"2026-11-06T10:00:00Z","paths":["src/X.fs"],"actingAgent":{"kind":"agent","session":"session_TEST1"}}"""
    let withDecision d = { real with Decisions = real.Decisions @ [ d ] }
    let codes i = check i |> List.map (fun r -> r.Code) |> Set.ofList
    let ownerReq = real.OwnerRequestIds |> Seq.tryFind (fun s -> s.StartsWith "ODR-R10") |> Option.defaultValue "ODR-R10-01"
    let cases : (string * Inputs * string option) list =
        [ "committed records pass", real, None
          "positive: covered class by an identified agent", withDecision good, None
          "positive: each covered class is accepted",
            { real with Decisions = classes "covered" real.Authority |> Set.toList |> List.mapi (fun n c -> good |> set "id" (jstr $"DTD-R10-{900 + n}") |> set "class" (jstr c)) }, None
          "reserved: acceptance-weakening", withDecision (good |> set "class" (jstr "acceptance-weakening")), Some "DTD_RESERVED_TO_OWNER"
          "reserved: official-build-start", withDecision (good |> set "class" (jstr "official-build-start")), Some "DTD_RESERVED_TO_OWNER"
          "reserved: access-expansion", withDecision (good |> set "class" (jstr "access-expansion")), Some "DTD_RESERVED_TO_OWNER"
          "reserved: test-result-claim", withDecision (good |> set "class" (jstr "test-result-claim")), Some "DTD_RESERVED_TO_OWNER"
          "uncovered class", withDecision (good |> set "class" (jstr "budget-increase")), Some "DTD_NOT_COVERED"
          "owner-decision request named", withDecision (good |> set "decides" (jstr ownerReq)), Some "DTD_OWNER_REQUEST"
          "owner-decision request in the decision text", withDecision (good |> set "decision" (jstr $"Choose {ownerReq} A")), Some "DTD_OWNER_REQUEST"
          "acting as the owner", withDecision (good |> set "actingAgent" (JsonNode.Parse """{"kind":"agent","session":"session_X","id":"kemiller2002"}""")), Some "DTD_OWNER_IMPERSONATION"
          "operator authority", withDecision (good |> set "actingAgent" (JsonNode.Parse """{"kind":"operator","session":"session_X"}""")), Some "DTD_NOT_AN_AGENT"
          "authority field on a decision", withDecision (good |> set "authority" (JsonNode.Parse """{"kind":"operator","id":"x"}""")), Some "DTD_OWNER_IMPERSONATION"
          "unidentified agent", withDecision (good |> set "actingAgent" (JsonNode.Parse """{"kind":"agent"}""")), Some "DTD_AGENT_UNIDENTIFIED"
          "after the window", withDecision (good |> set "decidedAt" (jstr "2026-11-08T00:00:01Z")), Some "DTD_OUTSIDE_WINDOW"
          "before the directive", withDecision (good |> set "decidedAt" (jstr "2026-11-05T19:00:00Z")), Some "DTD_OUTSIDE_WINDOW"
          "touches an approvals file", withDecision (good |> set "paths" (JsonNode.Parse """["kickoff/rehearsal-007/approvals.causa.json"]""")), Some "DTD_PROTECTED_PATH"
          "touches the authority record", withDecision (good |> set "paths" (JsonNode.Parse """["./kickoff/rehearsal-010/delegated/delegated-authority.json"]""")), Some "DTD_PROTECTED_PATH"
          "no rationale", withDecision (good |> set "rationale" (jstr " ")), Some "DTD_INCOMPLETE"
          "bad id", withDecision (good |> set "id" (jstr "APR-R10-OWNER-9")), Some "DTD_ID"
          "duplicate id", { real with Decisions = real.Decisions @ [ good; good ] }, Some "DTD_DUPLICATE_ID"
          "audit: base entry removed", { real with BaseDecisions = [ good ] }, Some "DTD_AUDIT_REMOVED"
          "audit: base entry edited", { withDecision (good |> set "rationale" (jstr "changed")) with BaseDecisions = [ good ] }, Some "DTD_AUDIT_EDITED"
          "audit: base entry kept unchanged", { withDecision good with BaseDecisions = [ good ] }, None
          "laundered into approvals", { real with Approvals = real.Approvals @ [ "kickoff/x/approvals.causa.json", JsonNode.Parse """{"id":"APR-X","authority":{"kind":"delegated","id":"DA-R10-1"}}""" ] }, Some "APPROVAL_LAUNDERED"
          "directive text altered", { real with DirectiveSha = Some "00" }, Some "DA_DIRECTIVE_DIGEST"
          "directive text missing", { real with DirectiveSha = None }, Some "DA_DIRECTIVE_MISSING"
          "authority claims to be an owner approval", { real with Authority = real.Authority |> set "notAnOwnerApproval" (JsonValue.Create false) }, Some "DA_CLAIMS_OWNER_APPROVAL"
          "reserved class moved into covered",
            { real with Authority = real.Authority |> set "covered" (JsonNode.Parse """[{"class":"official-build-start"},{"class":"technical-interface"}]""") }, Some "DA_COVERED_RESERVED_OVERLAP"
          "a reserved class dropped",
            { real with Authority = real.Authority |> set "reserved" (JsonNode.Parse """[{"class":"secret-publication"}]""") }, Some "DA_RESERVED_INCOMPLETE"
          "validity extended past inversion", { real with Authority = real.Authority |> set "validUntil" (jstr "2026-11-01T00:00:00Z") }, Some "DA_VALIDITY" ]
    let results =
        cases |> List.map (fun (name, i, expect) ->
            let got = codes i
            let ok = match expect with None -> Set.isEmpty got | Some c -> got.Contains c
            printfn "%s  %s%s" (if ok then "PASS" else "FAIL") name (if ok then "" else $"  (expected {expect}, got {got |> Set.toList})")
            ok)
    let failed = results |> List.filter not |> List.length
    printfn "%d/%d cases pass" (List.length results - failed) (List.length results)
    if failed > 0 then 1 else 0

let rec opt (k: string) = function | a :: v :: _ when a = k -> Some v | _ :: t -> opt k t | [] -> None
let root = opt "--indy-init" args |> Option.defaultValue "."
let code =
    match args |> List.filter (fun a -> a = "check" || a = "test") with
    | [ "check" ] ->
        let refusals = check (load root (opt "--base-ref" args))
        refusals |> List.iter (fun r -> printfn "REFUSED %s %s: %s" r.Code r.Subject r.Detail)
        if List.isEmpty refusals then printfn "delegated authority: %d decision(s) within scope; not owner approvals" (List.length (load root None).Decisions); 0 else 1
    | [ "test" ] -> tests root
    | _ -> eprintfn "usage: delegated-authority.fsx -- --indy-init DIR (check [--base-ref REF] | test)"; 2
exit code
