namespace Causa.HistoricalCompatibility

open System
open System.Text

/// A JSON value as causa-storage v1.0.0 allows it (CONTRACT §2.3): no floats, integers within ±(2^53-1).
/// Object members keep their source order, so nothing about the input is lost before the canonical check.
type JValue =
    | JNull
    | JBool of bool
    | JInt of int64
    | JStr of string
    | JArr of JValue list
    | JObj of (string * JValue) list

/// Why a text is not one acceptable JSON value (reader stage 3, CAUSA.JSON_MALFORMED).
type JsonError = { Offset: int; Detail: string }

/// Strict recursive-descent parser for the JSON subset of CONTRACT §2.3. Pure: no mutable state; every step returns
/// the value and the next offset.
module Json =
    [<Literal>]
    let MaxDepth = 128

    let maxSafe = 9007199254740991L

    let private fail offset detail : Result<'T, JsonError> = Error { Offset = offset; Detail = detail }

    let private isWs (c: char) = c = ' ' || c = '\t' || c = '\n' || c = '\r'

    let rec private skipWs (s: string) i = if i < s.Length && isWs s[i] then skipWs s (i + 1) else i

    let private hexValue (c: char) =
        if c >= '0' && c <= '9' then Some(int c - int '0')
        elif c >= 'a' && c <= 'f' then Some(int c - int 'a' + 10)
        elif c >= 'A' && c <= 'F' then Some(int c - int 'A' + 10)
        else None

    let private hex4 (s: string) i : Result<int, JsonError> =
        if i + 4 > s.Length then fail i "truncated \\u escape"
        else
            [ 0..3 ]
            |> List.fold
                (fun acc k ->
                    acc
                    |> Result.bind (fun v ->
                        match hexValue s[i + k] with
                        | Some h -> Ok(v * 16 + h)
                        | None -> fail (i + k) "invalid hex digit in \\u escape"))
                (Ok 0)

    let private isHigh (u: int) = u >= 0xD800 && u <= 0xDBFF
    let private isLow (u: int) = u >= 0xDC00 && u <= 0xDFFF

    /// Parses the string body after the opening quote. Returns the decoded value and the offset after the closing quote.
    let private parseString (s: string) (start: int) : Result<string * int, JsonError> =
        let rec go i (acc: char list) =
            if i >= s.Length then fail start "unterminated string"
            else
                let c = s[i]
                if c = '"' then Ok(String(acc |> List.rev |> Array.ofList), i + 1)
                elif c = '\\' then
                    if i + 1 >= s.Length then fail i "unterminated escape"
                    else
                        match s[i + 1] with
                        | '"' -> go (i + 2) ('"' :: acc)
                        | '\\' -> go (i + 2) ('\\' :: acc)
                        | '/' -> go (i + 2) ('/' :: acc)
                        | 'b' -> go (i + 2) ('\b' :: acc)
                        | 'f' -> go (i + 2) ('\012' :: acc)
                        | 'n' -> go (i + 2) ('\n' :: acc)
                        | 'r' -> go (i + 2) ('\r' :: acc)
                        | 't' -> go (i + 2) ('\t' :: acc)
                        | 'u' ->
                            hex4 s (i + 2)
                            |> Result.bind (fun u ->
                                if u = 0xFEFF then fail i "U+FEFF is forbidden (CONTRACT §2.3)"
                                elif isLow u then fail i "unpaired low surrogate"
                                elif isHigh u then
                                    if i + 7 < s.Length && s[i + 6] = '\\' && s[i + 7] = 'u' then
                                        hex4 s (i + 8)
                                        |> Result.bind (fun lo ->
                                            if isLow lo then go (i + 12) (char lo :: char u :: acc)
                                            else fail i "unpaired high surrogate")
                                    else fail i "unpaired high surrogate"
                                else go (i + 6) (char u :: acc))
                        | other -> fail i ("invalid escape \\" + string other)
                elif int c < 0x20 then fail i "unescaped control character in string"
                elif c = '﻿' then fail i "U+FEFF is forbidden (CONTRACT §2.3)"
                elif Char.IsHighSurrogate c then
                    if i + 1 < s.Length && Char.IsLowSurrogate s[i + 1] then go (i + 2) (s[i + 1] :: c :: acc)
                    else fail i "unpaired high surrogate"
                elif Char.IsLowSurrogate c then fail i "unpaired low surrogate"
                else go (i + 1) (c :: acc)

        go start []

    let private isDigit (c: char) = c >= '0' && c <= '9'

    let rec private digitsEnd (s: string) i = if i < s.Length && isDigit s[i] then digitsEnd s (i + 1) else i

    /// JSON number grammar; anything but an in-range integer is rejected (CONTRACT §2.3).
    let private parseNumber (s: string) (start: int) : Result<JValue * int, JsonError> =
        let afterSign = if s[start] = '-' then start + 1 else start
        if afterSign >= s.Length || not (isDigit s[afterSign]) then fail start "invalid number"
        else
            let intEnd =
                if s[afterSign] = '0' then afterSign + 1 else digitsEnd s afterSign
            let hasFraction = intEnd < s.Length && s[intEnd] = '.'
            let hasExponent = intEnd < s.Length && (s[intEnd] = 'e' || s[intEnd] = 'E')
            if hasFraction || hasExponent then fail start "non-integer number (floats and exponents are not allowed)"
            elif intEnd < s.Length && isDigit s[intEnd] then fail start "leading zero in number"
            else
                let text = s.Substring(start, intEnd - start)
                match Int64.TryParse(text, Globalization.NumberStyles.AllowLeadingSign, Globalization.CultureInfo.InvariantCulture) with
                | true, v when v >= -maxSafe && v <= maxSafe -> Ok(JInt v, intEnd)
                | _ -> fail start "integer outside [-(2^53-1), 2^53-1]"

    let private literal (s: string) i (word: string) (value: JValue) =
        if i + word.Length <= s.Length && String.CompareOrdinal(s, i, word, 0, word.Length) = 0 then Ok(value, i + word.Length)
        else fail i "invalid literal"

    /// depth = number of enclosing objects/arrays including this one (top-level container = 1).
    let rec private parseValue (s: string) (i: int) (depth: int) : Result<JValue * int, JsonError> =
        let i = skipWs s i
        if i >= s.Length then fail i "unexpected end of input"
        else
            match s[i] with
            | '{' -> if depth + 1 > MaxDepth then fail i "nesting deeper than 128" else parseObject s (i + 1) (depth + 1)
            | '[' -> if depth + 1 > MaxDepth then fail i "nesting deeper than 128" else parseArray s (i + 1) (depth + 1)
            | '"' -> parseString s (i + 1) |> Result.map (fun (v, j) -> JStr v, j)
            | 't' -> literal s i "true" (JBool true)
            | 'f' -> literal s i "false" (JBool false)
            | 'n' -> literal s i "null" JNull
            | c when c = '-' || isDigit c -> parseNumber s i
            | _ -> fail i "unexpected character"

    and private parseArray s i depth =
        let i0 = skipWs s i
        if i0 < s.Length && s[i0] = ']' then Ok(JArr [], i0 + 1)
        else
            let rec items j acc =
                parseValue s j depth
                |> Result.bind (fun (v, k) ->
                    let k = skipWs s k
                    if k >= s.Length then fail k "unterminated array"
                    elif s[k] = ',' then items (k + 1) (v :: acc)
                    elif s[k] = ']' then Ok(JArr(List.rev (v :: acc)), k + 1)
                    else fail k "expected ',' or ']'")
            items i0 []

    and private parseObject s i depth =
        let i0 = skipWs s i
        if i0 < s.Length && s[i0] = '}' then Ok(JObj [], i0 + 1)
        else
            let rec members j (acc: (string * JValue) list) (seen: Set<string>) =
                let j = skipWs s j
                if j >= s.Length || s[j] <> '"' then fail j "expected a member name"
                else
                    parseString s (j + 1)
                    |> Result.bind (fun (key, k) ->
                        if seen.Contains key then fail j ("duplicate key '" + key + "'")
                        else
                            let k = skipWs s k
                            if k >= s.Length || s[k] <> ':' then fail k "expected ':'"
                            else
                                parseValue s (k + 1) depth
                                |> Result.bind (fun (v, m) ->
                                    let m = skipWs s m
                                    if m >= s.Length then fail m "unterminated object"
                                    elif s[m] = ',' then members (m + 1) ((key, v) :: acc) (seen.Add key)
                                    elif s[m] = '}' then Ok(JObj(List.rev ((key, v) :: acc)), m + 1)
                                    else fail m "expected ',' or '}'"))
            members i0 [] Set.empty

    /// Parses exactly one JSON text (surrounding JSON whitespace allowed; the canonical check rejects it later).
    let parse (s: string) : Result<JValue, JsonError> =
        parseValue s 0 0
        |> Result.bind (fun (v, i) ->
            let j = skipWs s i
            if j = s.Length then Ok v else fail j "trailing content after the JSON value")

    // ---------------------------------------------------------------- RFC 8785 (JCS) serialization

    let private escape (sb: StringBuilder) (s: string) =
        s
        |> Seq.fold
            (fun (b: StringBuilder) c ->
                match c with
                | '"' -> b.Append("\\\"")
                | '\\' -> b.Append("\\\\")
                | '\b' -> b.Append("\\b")
                | '\012' -> b.Append("\\f")
                | '\n' -> b.Append("\\n")
                | '\r' -> b.Append("\\r")
                | '\t' -> b.Append("\\t")
                | c when int c < 0x20 -> b.Append("\\u").Append((int c).ToString("x4"))
                | c -> b.Append(c))
            (sb.Append('"'))
        |> fun b -> b.Append('"')

    let rec private write (sb: StringBuilder) (v: JValue) : StringBuilder =
        match v with
        | JNull -> sb.Append("null")
        | JBool true -> sb.Append("true")
        | JBool false -> sb.Append("false")
        | JInt n -> sb.Append(n.ToString(Globalization.CultureInfo.InvariantCulture))
        | JStr s -> escape sb s
        | JArr items ->
            items
            |> List.fold (fun (b: StringBuilder, first) x -> (write (if first then b else b.Append(',')) x, false)) (sb.Append('['), true)
            |> fst
            |> fun b -> b.Append(']')
        | JObj members ->
            members
            |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))
            |> List.fold
                (fun (b: StringBuilder, first) (k, x) -> (write ((escape (if first then b else b.Append(',')) k).Append(':')) x, false))
                (sb.Append('{'), true)
            |> fst
            |> fun b -> b.Append('}')

    /// The RFC 8785 canonical text of a value (keys sorted by UTF-16 code units, CONTRACT §2.2).
    let canonical (v: JValue) : string = (write (StringBuilder()) v).ToString()

    // ---------------------------------------------------------------- object access helpers

    let tryField (key: string) (members: (string * JValue) list) = members |> List.tryFind (fst >> (=) key) |> Option.map snd

    let keys (members: (string * JValue) list) = members |> List.map fst |> Set.ofList

    /// A readable, deterministic rendering for reports: keys sorted as in `canonical`, two-space indentation.
    /// Reports only; storage files are always `canonical`.
    let pretty (v: JValue) : string =
        let rec go (indent: string) (v: JValue) : string =
            let inner = indent + "  "
            match v with
            | JArr [] -> "[]"
            | JObj [] -> "{}"
            | JArr items -> "[\n" + String.Join(",\n", items |> List.map (fun x -> inner + go inner x)) + "\n" + indent + "]"
            | JObj members ->
                let sorted = members |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))
                "{\n" + String.Join(",\n", sorted |> List.map (fun (k, x) -> inner + canonical (JStr k) + ": " + go inner x)) + "\n" + indent + "}"
            | scalar -> canonical scalar
        go "" v
