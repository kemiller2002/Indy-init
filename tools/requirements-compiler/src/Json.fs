/// A small immutable JSON model with a deterministic writer.
/// Objects keep the key order they are built with; callers sort when order is not semantic.
module Json

open System
open System.Globalization
open System.IO
open System.Text
open System.Text.Json

type Json =
    | JNull
    | JBool of bool
    | JInt of int64
    | JNum of decimal
    | JStr of string
    | JArr of Json list
    | JObj of (string * Json) list

let rec private ofElement (e: JsonElement) : Json =
    match e.ValueKind with
    | JsonValueKind.Null | JsonValueKind.Undefined -> JNull
    | JsonValueKind.True -> JBool true
    | JsonValueKind.False -> JBool false
    | JsonValueKind.String -> JStr(e.GetString())
    | JsonValueKind.Number ->
        match e.TryGetInt64() with
        | true, i -> JInt i
        | _ -> JNum(e.GetDecimal())
    | JsonValueKind.Array -> e.EnumerateArray() |> Seq.map ofElement |> List.ofSeq |> JArr
    | JsonValueKind.Object -> e.EnumerateObject() |> Seq.map (fun p -> p.Name, ofElement p.Value) |> List.ofSeq |> JObj
    | k -> failwithf "unsupported JSON value kind %A" k

let parseBytes (bytes: byte[]) : Json =
    use doc = JsonDocument.Parse(ReadOnlyMemory bytes)
    ofElement doc.RootElement

let parseFile (path: string) : Json = File.ReadAllBytes path |> parseBytes

// ---- accessors -------------------------------------------------------------

let field (key: string) (j: Json) : Json option =
    match j with
    | JObj props -> props |> List.tryFind (fun (k, _) -> k = key) |> Option.map snd
    | _ -> None

let strOpt key j =
    match field key j with
    | Some(JStr s) -> Some s
    | _ -> None

let str key j =
    match strOpt key j with
    | Some s -> s
    | None -> failwithf "missing string field '%s'" key

let arr key j =
    match field key j with
    | Some(JArr xs) -> xs
    | _ -> []

let strs key j =
    arr key j |> List.choose (function JStr s -> Some s | _ -> None)

let boolOr key dflt j =
    match field key j with
    | Some(JBool b) -> b
    | _ -> dflt

let intOpt key j =
    match field key j with
    | Some(JInt i) -> Some i
    | _ -> None

let asStr =
    function
    | JStr s -> s
    | j -> failwithf "expected string, got %A" j

// ---- builders --------------------------------------------------------------

let jstrs (xs: string seq) = xs |> Seq.map JStr |> List.ofSeq |> JArr
let jint (i: int) = JInt(int64 i)
let jopt f = function Some x -> f x | None -> JNull

// ---- deterministic writer --------------------------------------------------

/// asciiOnly: escape every character outside printable ASCII (the compiler's own artifacts).
/// Otherwise follow Python's json.dump(ensure_ascii=False): escape only quote, backslash and C0 controls,
/// which is the format of the existing baseline generator.
let private escape (asciiOnly: bool) (s: string) =
    let sb = StringBuilder(s.Length + 2)
    sb.Append('"') |> ignore
    s
    |> Seq.iter (fun c ->
        match c with
        | '"' -> sb.Append("\\\"") |> ignore
        | '\\' -> sb.Append("\\\\") |> ignore
        | '\n' -> sb.Append("\\n") |> ignore
        | '\r' -> sb.Append("\\r") |> ignore
        | '\t' -> sb.Append("\\t") |> ignore
        | '\b' -> sb.Append("\\b") |> ignore
        | '\f' -> sb.Append("\\f") |> ignore
        | c when c < ' ' || (asciiOnly && c > '~') -> sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:x4}", int c) |> ignore
        | c -> sb.Append(c) |> ignore)
    sb.Append('"').ToString()

let rec private write (asciiOnly: bool) (sb: StringBuilder) (indent: int) (j: Json) : unit =
    let pad n = String(' ', n)
    match j with
    | JNull -> sb.Append("null") |> ignore
    | JBool b -> sb.Append(if b then "true" else "false") |> ignore
    | JInt i -> sb.Append(i.ToString(CultureInfo.InvariantCulture)) |> ignore
    | JNum d -> sb.Append(d.ToString("0.###", CultureInfo.InvariantCulture)) |> ignore
    | JStr s -> sb.Append(escape asciiOnly s) |> ignore
    | JArr [] -> sb.Append("[]") |> ignore
    | JObj [] -> sb.Append("{}") |> ignore
    | JArr xs ->
        sb.Append("[\n") |> ignore
        xs
        |> List.iteri (fun i x ->
            sb.Append(pad (indent + 2)) |> ignore
            write asciiOnly sb (indent + 2) x
            sb.Append(if i < xs.Length - 1 then ",\n" else "\n") |> ignore)
        sb.Append(pad indent).Append("]") |> ignore
    | JObj props ->
        sb.Append("{\n") |> ignore
        props
        |> List.iteri (fun i (k, v) ->
            sb.Append(pad (indent + 2)).Append(escape asciiOnly k).Append(": ") |> ignore
            write asciiOnly sb (indent + 2) v
            sb.Append(if i < props.Length - 1 then ",\n" else "\n") |> ignore)
        sb.Append(pad indent).Append("}") |> ignore

let private render (asciiOnly: bool) (j: Json) =
    let sb = StringBuilder()
    write asciiOnly sb 0 j
    sb.Append('\n').ToString()

/// Serializes with two-space indentation, ASCII-only output and a trailing newline.
let serialize (j: Json) : string = render true j

/// Byte-compatible with Python's json.dump(indent=2, ensure_ascii=False) plus a trailing newline.
let serializePythonStyle (j: Json) : string = render false j
