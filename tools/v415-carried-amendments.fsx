// v4.1.5 carries v4.1.4's owner-approved state-machine amendments unchanged, and its own two entries (SM-LOAD T-L03
// and the ODR-V415-01 accepted note on SM-SAVE) equal the content digests ODR-V415-01 pins
// (requirements-compiled/v4.1.5/proposals/owner-decision-requests.json, applies.A).
// Usage: dotnet fsi tools/v415-carried-amendments.fsx [repository root]. Exit 0 when everything holds, 1 otherwise.
open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json

let root = fsi.CommandLineArgs |> Array.tryItem 1 |> Option.defaultValue "."
let load (path: string) = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, path))).RootElement

/// Structural JSON equality: objects compare as key/value sets, arrays in order (as Python's == on parsed JSON).
let rec same (a: JsonElement) (b: JsonElement) : bool =
    match a.ValueKind, b.ValueKind with
    | JsonValueKind.Object, JsonValueKind.Object ->
        let props (e: JsonElement) = e.EnumerateObject() |> Seq.map (fun p -> p.Name, p.Value) |> List.ofSeq
        let pa, pb = props a, props b
        List.length pa = List.length pb
        && pa |> List.forall (fun (k, v) -> pb |> List.exists (fun (k2, v2) -> k = k2 && same v v2))
    | JsonValueKind.Array, JsonValueKind.Array ->
        let ea, eb = List.ofSeq (a.EnumerateArray()), List.ofSeq (b.EnumerateArray())
        List.length ea = List.length eb && List.forall2 same ea eb
    | JsonValueKind.String, JsonValueKind.String -> a.GetString() = b.GetString()
    | JsonValueKind.Number, JsonValueKind.Number -> a.GetRawText() = b.GetRawText()
    | ka, kb -> ka = kb && (ka = JsonValueKind.True || ka = JsonValueKind.False || ka = JsonValueKind.Null)

let sameList (xs: JsonElement list) (ys: JsonElement list) = List.length xs = List.length ys && List.forall2 same xs ys

/// Canonical text as Python's json.dumps(sort_keys=True, separators=(",", ":"), ensure_ascii=False).
let quote (s: string) =
    let escape (c: char) =
        match c with
        | '"' -> "\\\""
        | '\\' -> "\\\\"
        | '\n' -> "\\n"
        | '\r' -> "\\r"
        | '\t' -> "\\t"
        | '\b' -> "\\b"
        | '\f' -> "\\f"
        | c when c < ' ' -> sprintf "\\u%04x" (int c)
        | c -> string c
    "\"" + String.Join("", s |> Seq.map escape) + "\""

let rec canonical (e: JsonElement) : string =
    match e.ValueKind with
    | JsonValueKind.Object ->
        e.EnumerateObject()
        |> Seq.sortWith (fun p q -> String.CompareOrdinal(p.Name, q.Name))
        |> Seq.map (fun p -> quote p.Name + ":" + canonical p.Value)
        |> fun parts -> "{" + String.Join(",", parts) + "}"
    | JsonValueKind.Array -> "[" + String.Join(",", e.EnumerateArray() |> Seq.map canonical) + "]"
    | JsonValueKind.String -> quote (e.GetString())
    | JsonValueKind.True -> "true"
    | JsonValueKind.False -> "false"
    | JsonValueKind.Null -> "null"
    | _ -> e.GetRawText()

/// The pinned digest of an entry: its canonical text without the 'approval' field, which FREEZE step 2 fills in.
let digest (e: JsonElement) =
    e.EnumerateObject()
    |> Seq.filter (fun p -> p.Name <> "approval")
    |> Seq.sortWith (fun p q -> String.CompareOrdinal(p.Name, q.Name))
    |> Seq.map (fun p -> quote p.Name + ":" + canonical p.Value)
    |> fun parts -> "{" + String.Join(",", parts) + "}"
    |> Encoding.UTF8.GetBytes
    |> SHA256.HashData
    |> Convert.ToHexString
    |> fun h -> h.ToLowerInvariant()

let str (name: string) (e: JsonElement) =
    match e.TryGetProperty name with
    | true, v when v.ValueKind = JsonValueKind.String -> v.GetString()
    | _ -> ""

let items (name: string) (e: JsonElement) = e.GetProperty(name).EnumerateArray() |> List.ofSeq
let others (e: JsonElement) = e.EnumerateObject() |> Seq.filter (fun p -> p.Name <> "transitions" && p.Name <> "accepted") |> List.ofSeq

let old = load "requirements-compiled/v4.1.4/analysis/state-machine-amendments.json"
let next = load "requirements-compiled/v4.1.5/analysis/state-machine-amendments.json"
let mineT (t: JsonElement) = str "machine" t = "SM-LOAD" && str "id" t = "T-L03"
let mineA (a: JsonElement) = str "decision" a = "ODR-V415-01"

let pins =
    items "requests" (load "requirements-compiled/v4.1.5/proposals/owner-decision-requests.json")
    |> List.find (fun r -> str "id" r = "ODR-V415-01")
    |> fun r -> r.GetProperty("applies").GetProperty("A")

let pinned (kind: string) = items kind pins |> List.tryHead |> Option.map (str "sha256") |> Option.defaultValue ""
let ownT, ownA = items "transitions" next |> List.filter mineT, items "accepted" next |> List.filter mineA
let matches (own: JsonElement list) (pin: string) = match own with [ e ] -> digest e = pin | _ -> false

let problems =
    [ if not (sameList (items "transitions" next |> List.filter (mineT >> not)) (items "transitions" old)) then "a carried transition differs from v4.1.4"
      if not (sameList (items "accepted" next |> List.filter (mineA >> not)) (items "accepted" old)) then "a carried accepted entry differs from v4.1.4"
      let o, n = others old, others next
      if not (List.length o = List.length n && o |> List.forall (fun p -> n |> List.exists (fun q -> q.Name = p.Name && same q.Value p.Value))) then "another field differs from v4.1.4"
      if not (matches ownT (pinned "transitions")) then "T-L03 differs from the content ODR-V415-01 pins"
      if not (matches ownA (pinned "accepted")) then "the SM-SAVE accepted note differs from the content ODR-V415-01 pins" ]

printfn "%s" (if problems.IsEmpty then "carried amendments unchanged; T-L03 and the SM-SAVE note match their pinned digests" else String.Join("\n", problems))
exit (if problems.IsEmpty then 0 else 1)
