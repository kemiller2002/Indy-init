namespace Causa.ContractReadiness

open System
open System.Globalization
open System.Text.RegularExpressions

/// A value decoded into its registered kind. Encoding goes from these typed values, not from the parsed text, so a
/// round trip proves the registry's kinds represent the stored value (a UUID is re-rendered from a Guid, a timestamp
/// from a DateTime), not merely that the text was echoed.
type TV =
    | TUuid of Guid
    | TTime of DateTime
    | TText of string
    | TUrl of string
    | TSha of string
    | TInt of int64
    | TEnum of string
    | TConst of J
    | TNull
    | TList of TV list
    | TObj of objectType: string * members: (string * TV) list

/// Why a value is not representable by the registry. `UnknownField` keeps the offending value so nothing is dropped
/// silently: the problem itself preserves it.
type Problem =
    | NotCanonical of detail: string
    | Malformed of detail: string
    | UnknownField of path: string * value: J
    | MissingField of path: string
    | UnexpectedField of path: string
    | WrongKind of path: string * expected: string * actual: string
    | RuleViolated of path: string * rule: string
    | RoundTripMismatch of detail: string

module Codec =
    let private uuidRe = Regex(@"\A[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z", RegexOptions.CultureInvariant)
    let private tsRe = Regex(@"\A[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]\.[0-9]{3}Z\z", RegexOptions.CultureInvariant)
    let private urlRe = Regex(@"\A[Hh][Tt][Tt][Pp][Ss]?://[^\t\n\u000b\f\r \u0085   -     　]", RegexOptions.CultureInvariant)
    let private shaRe = Regex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant)
    let private tsFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"

    /// CONTRACT §2.6 engine whitespace set; a text must contain one code unit outside it.
    let private isEngineWhitespace (c: char) =
        let n = int c
        (n >= 0x09 && n <= 0x0D) || n = 0x20 || n = 0x85 || n = 0xA0 || n = 0x1680 || (n >= 0x2000 && n <= 0x200A)
        || n = 0x2028 || n = 0x2029 || n = 0x202F || n = 0x205F || n = 0x3000

    let isNonBlankText (s: string) = s.Length > 0 && s |> Seq.exists (isEngineWhitespace >> not) && not (s.Contains '﻿')

    let private describe (j: J) =
        match j with
        | JNull -> "null"
        | JBool _ -> "boolean"
        | JInt _ -> "integer"
        | JStr _ -> "string"
        | JArr _ -> "array"
        | JObj _ -> "object"

    let private join (prefix: string) (name: string) = if prefix = "" then name else prefix + "." + name

    let rec private lookup (path: string) (members: (string * J) list) : J option =
        match path.IndexOf '.' with
        | -1 -> members |> List.tryFind (fst >> (=) path) |> Option.map snd
        | k ->
            match members |> List.tryFind (fst >> (=) (path.Substring(0, k))) with
            | Some(_, JObj inner) -> lookup (path.Substring(k + 1)) inner
            | _ -> None

    /// Decodes `j` as `kind`, collecting every problem (it does not stop at the first one).
    let rec decode (reg: Registry) (path: string) (kind: Kind) (j: J) : TV * Problem list =
        let wrong expected = TNull, [ WrongKind(path, expected, describe j) ]
        match kind, j with
        | KNullable _, JNull -> TNull, []
        | KNullable inner, other -> decode reg path inner other
        | KUuid, JStr s when uuidRe.IsMatch s && s <> "00000000-0000-0000-0000-000000000000" -> TUuid(Guid.ParseExact(s, "D")), []
        | KUuid, _ -> wrong "uuid"
        | KTimestamp, JStr s when tsRe.IsMatch s ->
            match DateTime.TryParseExact(s, tsFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal ||| DateTimeStyles.AssumeUniversal) with
            | true, dt -> TTime dt, []
            | _ -> TNull, [ WrongKind(path, "calendar-valid timestamp", s) ]
        | KTimestamp, _ -> wrong "timestamp"
        | KText, JStr s when isNonBlankText s -> TText s, []
        | KText, _ -> wrong "non-blank text"
        | KHttpUrl, JStr s when urlRe.IsMatch s && isNonBlankText s -> TUrl s, []
        | KHttpUrl, _ -> wrong "httpUrl"
        | KSha256, JStr s when shaRe.IsMatch s -> TSha s, []
        | KSha256, _ -> wrong "sha256"
        | KInteger(mn, mx), JInt n when n >= mn && n <= mx -> TInt n, []
        | KInteger _, _ -> wrong "integer in range"
        | KConst c, v when v = c -> TConst c, []
        | KConst c, _ -> wrong ("const " + Json.canonical c)
        | KEnum(_, values), JStr s when List.contains s values -> TEnum s, []
        | KEnum(n, _), _ -> wrong ("enum " + n)
        | KList(item, unique, minItems, maxItems), JArr xs ->
            let decoded = xs |> List.mapi (fun i x -> decode reg (sprintf "%s[%d]" path i) item x)
            let values = decoded |> List.map fst
            let problems = decoded |> List.collect snd
            let extra =
                [ if unique && List.length (List.distinct xs) <> List.length xs then RuleViolated(path, "uniqueItems")
                  if List.length xs < minItems then RuleViolated(path, sprintf "minItems %d" minItems)
                  match maxItems with
                  | Some m when List.length xs > m -> RuleViolated(path, sprintf "maxItems %d" m)
                  | _ -> () ]
            TList values, problems @ extra
        | KList _, _ -> wrong "array"
        | KObject t, JObj members -> decodeObject reg path t members
        | KObject _, _ -> wrong "object"
        | KAnyObject, JObj _ -> TNull, [ RuleViolated(path, "object shape not bound by any rule") ]
        | KAnyObject, _ -> wrong "object"

    and decodeObject (reg: Registry) (path: string) (objectType: string) (members: (string * J) list) : TV * Problem list =
        let fields = FieldRegistry.childrenOf reg objectType
        let rules = reg.Rules |> List.filter (fun r -> r.ObjectType = objectType)
        let active = rules |> List.filter (fun r -> lookup r.WhenPath members = Some r.WhenValue)
        let unknown =
            members
            |> List.filter (fun (k, _) -> fields |> List.exists (fun f -> f.Def.Name = k) |> not)
            |> List.map (fun (k, v) -> UnknownField(join path k, v))
        let decodeField (f: RegistryField) =
            let p = join path f.Def.Name
            let expected =
                match f.Def.Presence with
                | Always -> true
                | When(dp, vs) -> lookup dp members |> Option.map (fun v -> List.contains v vs) |> Option.defaultValue false
            match members |> List.tryFind (fst >> (=) f.Def.Name), expected with
            | None, true -> None, [ MissingField p ]
            | None, false -> None, []
            | Some _, false -> None, [ UnexpectedField p ]
            | Some(_, v), true ->
                let kind =
                    active
                    |> List.tryPick (fun r -> match r.Field, r.Constraint with
                                              | n, MustBeKind k when n = f.Def.Name -> Some k
                                              | _ -> None)
                    |> Option.defaultValue f.Def.Kind
                let consts =
                    active
                    |> List.choose (fun r -> match r.Field, r.Constraint with
                                             | n, MustEqual c when n = f.Def.Name && c <> v -> Some(RuleViolated(p, sprintf "if %s = %s then %s" r.WhenPath (Json.canonical r.WhenValue) (Json.canonical c)))
                                             | _ -> None)
                let tv, ps = decode reg p kind v
                Some(f.Def.Name, tv), ps @ consts
        let decoded = fields |> List.map decodeField
        TObj(objectType, decoded |> List.choose fst), unknown @ (decoded |> List.collect snd)

    // ---- Encoding from typed values -------------------------------------------------------------------------------

    let rec encode (tv: TV) : J =
        match tv with
        | TUuid g -> JStr(g.ToString("D"))
        | TTime t -> JStr(t.ToString(tsFormat, CultureInfo.InvariantCulture))
        | TText s
        | TUrl s
        | TSha s
        | TEnum s -> JStr s
        | TInt n -> JInt n
        | TConst c -> c
        | TNull -> JNull
        | TList xs -> JArr(xs |> List.map encode)
        | TObj(_, ms) -> JObj(ms |> List.map (fun (k, v) -> k, encode v))

    type Decoded =
        { Value: TV
          Raw: J
          Problems: Problem list }

    /// Decodes one canonical JSON text (an events.ndjson line, or a JSON file without its final LF) as `objectType`,
    /// and proves the typed value re-encodes to exactly the stored bytes.
    let decodeText (reg: Registry) (objectType: string) (text: string) : Decoded =
        match Json.tryParse text with
        | Error m -> { Value = TNull; Raw = JNull; Problems = [ Malformed m ] }
        | Ok raw ->
            let canonicalProblems = if Json.canonical raw = text then [] else [ NotCanonical "stored text is not its RFC 8785 canonical form" ]
            match raw with
            | JObj ms ->
                let tv, problems = decodeObject reg "" objectType ms
                let roundTrip =
                    if List.isEmpty problems then
                        let back = Json.canonical (encode tv)
                        if back = text then [] else [ RoundTripMismatch(sprintf "re-encoded %d bytes differ from stored %d bytes" back.Length text.Length) ]
                    else []
                { Value = tv; Raw = raw; Problems = canonicalProblems @ problems @ roundTrip }
            | _ -> { Value = TNull; Raw = raw; Problems = canonicalProblems @ [ WrongKind("", "object", describe raw) ] }

    let problemText (p: Problem) =
        match p with
        | NotCanonical d -> "NOT_CANONICAL: " + d
        | Malformed d -> "MALFORMED: " + d
        | UnknownField(path, v) -> sprintf "UNKNOWN_FIELD %s = %s (preserved here, not dropped)" path (let t = Json.canonical v in t.Substring(0, min 120 t.Length))
        | MissingField p -> "MISSING_FIELD " + p
        | UnexpectedField p -> "UNEXPECTED_FIELD " + p
        | WrongKind(p, e, a) -> sprintf "WRONG_KIND %s expected %s got %s" p e a
        | RuleViolated(p, r) -> sprintf "RULE %s: %s" p r
        | RoundTripMismatch d -> "ROUND_TRIP: " + d

    /// Typed member access on a decoded object.
    let rec at (path: string) (tv: TV) : TV option =
        match tv with
        | TObj(_, ms) ->
            match path.IndexOf '.' with
            | -1 -> ms |> List.tryFind (fst >> (=) path) |> Option.map snd
            | k -> ms |> List.tryFind (fst >> (=) (path.Substring(0, k))) |> Option.bind (snd >> at (path.Substring(k + 1)))
        | _ -> None

    let asString (tv: TV option) =
        match tv with
        | Some(TUuid g) -> Some(g.ToString("D"))
        | Some(TText s | TUrl s | TSha s | TEnum s) -> Some s
        | Some(TTime t) -> Some(t.ToString(tsFormat, CultureInfo.InvariantCulture))
        | Some(TInt n) -> Some(string n)
        | Some(TConst c) -> Some(Json.canonical c)
        | _ -> None

    /// Every (objectType.field, value-kind) pair present in a decoded value, for the inventory.
    let rec observedFields (tv: TV) : (string * string) list =
        match tv with
        | TObj(t, ms) ->
            ms
            |> List.collect (fun (k, v) ->
                let kindName =
                    match v with
                    | TUuid _ -> "uuid"
                    | TTime _ -> "timestamp"
                    | TText _ -> "text"
                    | TUrl _ -> "httpUrl"
                    | TSha _ -> "sha256"
                    | TInt _ -> "integer"
                    | TEnum _ -> "enum"
                    | TConst _ -> "const"
                    | TNull -> "null"
                    | TList [] -> "emptyList"
                    | TList _ -> "list"
                    | TObj _ -> "object"
                (sprintf "F.%s.%s" t k, kindName) :: observedFields v)
        | TList xs -> xs |> List.collect observedFields
        | _ -> []
