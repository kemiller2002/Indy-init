namespace Causa.ContractReadiness

open System
open System.Text
open System.Text.Json

/// An immutable JSON tree. Object members keep their source order (duplicates are refused at parse time), so the
/// canonical writer can prove that a stored line was already in RFC 8785 order rather than re-sorting it silently.
type J =
    | JNull
    | JBool of bool
    | JInt of int64
    | JStr of string
    | JArr of J list
    | JObj of (string * J) list

module Json =
    exception JsonError of string

    let private options =
        JsonDocumentOptions(AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 128)

    let rec private ofElement (e: JsonElement) : J =
        match e.ValueKind with
        | JsonValueKind.Null -> JNull
        | JsonValueKind.True -> JBool true
        | JsonValueKind.False -> JBool false
        | JsonValueKind.String -> JStr(e.GetString())
        | JsonValueKind.Number ->
            match e.TryGetInt64() with
            | true, v when not (e.GetRawText().Contains '.' || e.GetRawText().Contains 'e' || e.GetRawText().Contains 'E') -> JInt v
            | _ -> raise (JsonError(sprintf "non-integer number %s" (e.GetRawText())))
        | JsonValueKind.Array -> JArr [ for x in e.EnumerateArray() -> ofElement x ]
        | JsonValueKind.Object ->
            let members = [ for p in e.EnumerateObject() -> p.Name, ofElement p.Value ]
            let names = members |> List.map fst
            if List.length (List.distinct names) <> List.length names then raise (JsonError "duplicate object key")
            JObj members
        | k -> raise (JsonError(sprintf "unsupported JSON value kind %A" k))

    /// Parses one JSON text. Integers only (CONTRACT §2.3); duplicate keys are refused.
    let tryParse (text: string) : Result<J, string> =
        try
            use d = JsonDocument.Parse(text, options)
            Ok(ofElement d.RootElement)
        with
        | JsonError m -> Error m
        | ex -> Error ex.Message

    let parse (text: string) : J =
        match tryParse text with
        | Ok j -> j
        | Error m -> failwithf "JSON parse error: %s" m

    let parseFile (path: string) : J = parse (IO.File.ReadAllText path)

    // ---- RFC 8785 canonical text (CONTRACT §2.2) ----------------------------------------------------------------

    let private escapeCanonical (s: string) =
        let sb = StringBuilder(s.Length + 2)
        sb.Append('"') |> ignore
        s
        |> Seq.iter (fun c ->
            match c with
            | '"' -> sb.Append("\\\"") |> ignore
            | '\\' -> sb.Append("\\\\") |> ignore
            | '\b' -> sb.Append("\\b") |> ignore
            | '\f' -> sb.Append("\\f") |> ignore
            | '\n' -> sb.Append("\\n") |> ignore
            | '\r' -> sb.Append("\\r") |> ignore
            | '\t' -> sb.Append("\\t") |> ignore
            | c when c < ' ' -> sb.AppendFormat("\\u{0:x4}", int c) |> ignore
            | c -> sb.Append(c) |> ignore)
        sb.Append('"').ToString()

    /// Canonical text: members sorted by UTF-16 code units, no insignificant whitespace.
    let rec canonical (j: J) : string =
        match j with
        | JNull -> "null"
        | JBool true -> "true"
        | JBool false -> "false"
        | JInt i -> string i
        | JStr s -> escapeCanonical s
        | JArr xs -> "[" + (xs |> List.map canonical |> String.concat ",") + "]"
        | JObj ms ->
            let sorted = ms |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))
            "{" + (sorted |> List.map (fun (k, v) -> escapeCanonical k + ":" + canonical v) |> String.concat ",") + "}"

    // ---- Deterministic pretty output for generated package files --------------------------------------------------

    let private escapePretty (s: string) = escapeCanonical s

    let rec private pretty (indent: int) (j: J) : string =
        let pad n = String(' ', n)
        match j with
        | JArr [] -> "[]"
        | JObj [] -> "{}"
        | JArr xs when xs |> List.forall (function JStr _ | JInt _ | JBool _ | JNull -> true | _ -> false) && List.length xs <= 12 ->
            "[" + (xs |> List.map canonical |> String.concat ", ") + "]"
        | JArr xs ->
            "[\n" + (xs |> List.map (fun x -> pad (indent + 2) + pretty (indent + 2) x) |> String.concat ",\n") + "\n" + pad indent + "]"
        | JObj ms ->
            "{\n"
            + (ms |> List.map (fun (k, v) -> pad (indent + 2) + escapePretty k + ": " + pretty (indent + 2) v) |> String.concat ",\n")
            + "\n" + pad indent + "}"
        | other -> canonical other

    let render (j: J) = pretty 0 j + "\n"

    // ---- Accessors ----------------------------------------------------------------------------------------------

    let tryField (name: string) (j: J) =
        match j with
        | JObj ms -> ms |> List.tryFind (fst >> (=) name) |> Option.map snd
        | _ -> None

    let field name j =
        match tryField name j with
        | Some v -> v
        | None -> failwithf "missing field '%s'" name

    let str j =
        match j with
        | JStr s -> s
        | other -> failwithf "expected string, got %s" (canonical other)

    let strOpt name j =
        match tryField name j with
        | Some(JStr s) -> Some s
        | _ -> None

    let arr j =
        match j with
        | JArr xs -> xs
        | JNull -> []
        | other -> failwithf "expected array, got %s" (canonical other)

    let arrOf name j = tryField name j |> Option.map arr |> Option.defaultValue []
    let strs name j = arrOf name j |> List.map str
    let fieldStr name j = field name j |> str

    let boolOf name j =
        match tryField name j with
        | Some(JBool b) -> b
        | _ -> false

    let intOf name j =
        match tryField name j with
        | Some(JInt i) -> i
        | _ -> 0L

    // ---- Builders -----------------------------------------------------------------------------------------------

    let s (x: string) = JStr x
    let i (x: int) = JInt(int64 x)
    let l (x: int64) = JInt x
    let b (x: bool) = JBool x
    let ss (xs: string seq) = JArr(xs |> Seq.map JStr |> List.ofSeq)
    let o (ms: (string * J) list) = JObj ms
    let opt (f: 'a -> J) (x: 'a option) = x |> Option.map f |> Option.defaultValue JNull

    let sha256Hex (bytes: byte[]) =
        Security.Cryptography.SHA256.HashData(bytes) |> Convert.ToHexString |> fun h -> h.ToLowerInvariant()

    let sha256File (path: string) = sha256Hex (IO.File.ReadAllBytes path)
    let sha256Text (text: string) = sha256Hex (Encoding.UTF8.GetBytes text)
