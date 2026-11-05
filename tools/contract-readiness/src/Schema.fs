namespace Causa.ContractReadiness

open System
open System.IO

/// The value kinds the causa-storage v1.0.0 schemas use. Primitive kinds carry the CONTRACT §2 rules; the codec
/// implements them, the registry records them by name.
type Kind =
    | KUuid
    | KTimestamp
    | KText
    | KHttpUrl
    | KSha256
    | KInteger of min: int64 * max: int64
    | KConst of J
    | KEnum of name: string * values: string list
    | KNullable of Kind
    | KList of item: Kind * unique: bool * minItems: int * maxItems: int option
    | KObject of objectType: string
    /// An object whose shape is chosen by a rule (the envelope `payload`, `{"type":"object"}` until eventType binds it).
    | KAnyObject

/// When a field is present. causa-storage v1.0.0 has no optional keys (§2.5), so `Always` is the rule; `When` is only
/// produced by a `oneOf` of object shapes (the actor: provider and model exist only for agents).
type Presence =
    | Always
    | When of path: string * values: J list

type FieldDef =
    { FieldId: string
      ObjectType: string
      Name: string
      Kind: Kind
      Presence: Presence
      SchemaFile: string }

/// `if <path> = <value> then <field> must be <constraint>` (the schemas' allOf/if/then blocks).
type RuleConstraint =
    | MustEqual of J
    | MustBeKind of Kind

type Rule =
    { ObjectType: string
      WhenPath: string
      WhenValue: J
      Field: string
      Constraint: RuleConstraint }

type ObjectTypeDef =
    { TypeId: string
      Name: string
      SchemaFile: string
      SchemaSha256: string
      Description: string }

type CompiledSchemas =
    { ObjectTypes: ObjectTypeDef list
      Fields: FieldDef list
      Rules: Rule list
      Enums: (string * string list) list
      BundleSha256: string }

module Schema =
    let maxSafe = 9007199254740991L

    /// Common definitions that map to a primitive kind with fixed CONTRACT §2 semantics.
    let private primitive (defName: string) : Kind option =
        match defName with
        | "uuid" -> Some KUuid
        | "timestamp" -> Some KTimestamp
        | "text" -> Some KText
        | "httpUrl" -> Some KHttpUrl
        | "sha256" -> Some KSha256
        | "nullableText" -> Some(KNullable KText)
        | "nullableTimestamp" -> Some(KNullable KTimestamp)
        | "uuidList" -> Some(KList(KUuid, true, 0, None))
        | "nonEmptyUuidList" -> Some(KList(KUuid, true, 1, None))
        | _ -> None

    type private Ctx =
        { Root: string
          Docs: Map<string, J> }

    let private relPath (root: string) (full: string) = Path.GetRelativePath(root, full).Replace('\\', '/')

    let private loadDoc (ctx: Ctx) (file: string) = ctx.Docs |> Map.find file

    /// Resolves a `$ref` against the referring file: returns the target file (relative to the schema root), the JSON
    /// pointer's last segment (the definition name, if any) and the target node.
    let private resolve (ctx: Ctx) (fromFile: string) (reference: string) : string * string option * J =
        let filePart, frag =
            match reference.IndexOf '#' with
            | -1 -> reference, ""
            | k -> reference.Substring(0, k), reference.Substring(k + 1)
        let targetFile =
            if filePart = "" then fromFile
            else relPath ctx.Root (Path.GetFullPath(Path.Combine(ctx.Root, Path.GetDirectoryName(fromFile), filePart)))
        let doc = loadDoc ctx targetFile
        match frag with
        | "" -> targetFile, None, doc
        | f ->
            let segs = f.TrimStart('/').Split('/') |> List.ofArray
            let node = segs |> List.fold (fun j seg -> Json.field seg j) doc
            targetFile, Some(List.last segs), node

    let private objectTypeId (file: string) (defName: string option) =
        let stem = Path.GetFileName(file).Replace(".schema.json", "")
        match file, defName with
        | f, None when f.StartsWith "payloads/" -> "Payload." + stem
        | f, None when f.StartsWith "entities/" -> "Entity." + stem
        | "event-envelope.schema.json", None -> "Envelope"
        | "investigation-manifest.schema.json", None -> "Manifest"
        | "snapshot.schema.json", None -> "Snapshot"
        | "state.schema.json", None -> "State"
        | _, Some d -> "Def." + d
        | f, None -> "Root." + stem

    type private Acc =
        { Types: Map<string, ObjectTypeDef>
          Fields: FieldDef list
          Rules: Rule list
          Enums: Map<string, string list> }

    let private constValues (node: J) =
        match Json.tryField "const" node, Json.tryField "enum" node with
        | Some c, _ -> [ c ]
        | None, Some(JArr xs) -> xs
        | _ -> []

    /// Compiles one schema node to a kind, registering the object types it reaches.
    let rec private kindOf (ctx: Ctx) (file: string) (nameHint: string option) (node: J) (acc: Acc) : Kind * Acc =
        match Json.tryField "$ref" node with
        | Some(JStr r) ->
            let tf, defName, target = resolve ctx file r
            match defName |> Option.bind primitive with
            | Some k -> k, acc
            // An object reached by $ref is named after its file (defName None) or its definition.
            | None -> kindOf ctx tf (defName |> Option.orElse (Some "#root")) target acc
        | _ ->
            match Json.tryField "enum" node, Json.tryField "const" node with
            | Some(JArr vs), _ ->
                let name = nameHint |> Option.defaultValue "inline"
                let values = vs |> List.map Json.str
                KEnum(name, values), { acc with Enums = acc.Enums |> Map.add name values }
            | _, Some c -> KConst c, acc
            | _ ->
                match Json.tryField "oneOf" node, Json.tryField "allOf" node, Json.tryField "type" node with
                | Some(JArr alts), _, _ when alts |> List.exists (fun a -> Json.tryField "type" a = Some(JStr "null")) ->
                    let other = alts |> List.find (fun a -> Json.tryField "type" a <> Some(JStr "null"))
                    let k, a = kindOf ctx file nameHint other acc
                    KNullable k, a
                | Some(JArr alts), _, _ when alts |> List.forall (fun a -> Json.tryField "type" a = Some(JStr "object")) ->
                    objectKind ctx file nameHint node acc
                | None, Some(JArr [ baseRef; extra ]), None when Json.tryField "minItems" extra |> Option.isSome ->
                    let k, a = kindOf ctx file nameHint baseRef acc
                    match k, Json.tryField "minItems" extra with
                    | KList(item, u, _, mx), Some(JInt n) -> KList(item, u, int n, mx), a
                    | other, _ -> other, a
                | _, _, Some(JStr "array") ->
                    let item, a =
                        match Json.tryField "items" node with
                        | Some items -> kindOf ctx file nameHint items acc
                        | None -> KAnyObject, acc
                    let unique = Json.boolOf "uniqueItems" node
                    let minItems = Json.intOf "minItems" node |> int
                    let maxItems = Json.tryField "maxItems" node |> Option.map (function JInt n -> int n | _ -> 0)
                    KList(item, unique, minItems, maxItems), a
                | _, _, Some(JStr "integer") ->
                    let mn = Json.tryField "minimum" node |> Option.map (function JInt n -> n | _ -> 0L) |> Option.defaultValue -maxSafe
                    let mx = Json.tryField "maximum" node |> Option.map (function JInt n -> n | _ -> 0L) |> Option.defaultValue maxSafe
                    KInteger(mn, mx), acc
                | _, _, Some(JStr "object") when Json.tryField "properties" node |> Option.isNone -> KAnyObject, acc
                | _, _, Some(JStr "object") -> objectKind ctx file nameHint node acc
                | _ -> failwithf "unsupported schema node in %s: %s" file (Json.canonical node |> fun t -> t.Substring(0, min 200 t.Length))

    and private objectKind (ctx: Ctx) (file: string) (defName: string option) (node: J) (acc: Acc) : Kind * Acc =
        let typeId = objectTypeId file (if defName = Some "#root" then None else defName)
        if acc.Types.ContainsKey typeId then KObject typeId, acc
        else
            let tdef =
                { TypeId = typeId
                  Name = typeId
                  SchemaFile = file
                  SchemaSha256 = Json.sha256File (Path.Combine(ctx.Root, file))
                  Description = Json.strOpt "description" node |> Option.defaultValue "" }
            let acc = { acc with Types = acc.Types |> Map.add typeId tdef }
            // A oneOf of object shapes (the actor) merges into one closed object; keys not required by every shape are
            // present only when the shapes' discriminating const holds.
            let shapes =
                match Json.tryField "oneOf" node with
                | Some(JArr alts) -> alts
                | _ -> [ node ]
            let allProps =
                shapes
                |> List.collect (fun sh -> Json.tryField "properties" sh |> Option.map (function JObj ms -> ms | _ -> []) |> Option.defaultValue [])
                |> List.fold (fun (seen: (string * J) list) (k, v) ->
                    match seen |> List.tryFind (fst >> (=) k) with
                    | None -> seen @ [ k, v ]
                    | Some(_, prev) ->
                        // Merge enum/const alternatives of a discriminator (kind: human|system and kind: agent).
                        let merged = constValues prev @ constValues v
                        if List.isEmpty merged then seen
                        else seen |> List.map (fun (k2, v2) -> if k2 = k then k2, JObj [ "enum", JArr(List.distinct merged) ] else k2, v2)) []
            let requiredIn sh = Json.strs "required" sh
            let discriminator =
                if List.length shapes < 2 then None
                else
                    allProps
                    |> List.filter (fun (k, _) -> shapes |> List.forall (fun sh -> requiredIn sh |> List.contains k))
                    |> List.tryPick (fun (k, _) ->
                        let perShape = shapes |> List.map (fun sh -> Json.tryField "properties" sh |> Option.bind (Json.tryField k) |> Option.map constValues |> Option.defaultValue [])
                        if perShape |> List.forall (List.isEmpty >> not) then Some(k, perShape) else None)
            let folder (acc: Acc) (name: string, propNode: J) =
                let k, acc = kindOf ctx file (Some name) propNode acc
                let k =
                    match k with
                    | KEnum("inline", vs) -> KEnum(name, vs)
                    | other -> other
                let presence =
                    if shapes |> List.forall (fun sh -> requiredIn sh |> List.contains name) then Always
                    else
                        match discriminator with
                        | Some(dk, perShape) ->
                            let values = List.zip shapes perShape |> List.filter (fun (sh, _) -> requiredIn sh |> List.contains name) |> List.collect snd
                            When(dk, values)
                        | None -> failwithf "%s.%s is not required in every shape and no discriminator exists" typeId name
                let f =
                    { FieldId = sprintf "F.%s.%s" typeId name
                      ObjectType = typeId
                      Name = name
                      Kind = k
                      Presence = presence
                      SchemaFile = file }
                { acc with Fields = acc.Fields @ [ f ] }
            let acc = allProps |> List.fold folder acc
            // allOf / if / then rules.
            let rules, acc =
                Json.arrOf "allOf" node
                |> List.fold (fun (rs, acc) clause ->
                    match Json.tryField "if" clause, Json.tryField "then" clause with
                    | Some ifNode, Some thenNode ->
                        let rec whenOf (prefix: string) (n: J) =
                            match Json.tryField "properties" n with
                            | Some(JObj [ k, v ]) ->
                                match Json.tryField "const" v with
                                | Some c -> prefix + k, c
                                | None -> whenOf (prefix + k + ".") v
                            | _ -> failwithf "unsupported if clause in %s" file
                        let wp, wv = whenOf "" ifNode
                        let thenProps = match Json.tryField "properties" thenNode with Some(JObj ms) -> ms | _ -> []
                        thenProps
                        |> List.fold (fun (rs, acc) (fname, fnode) ->
                            match Json.tryField "const" fnode, Json.tryField "type" fnode with
                            | Some c, _ -> rs @ [ { ObjectType = typeId; WhenPath = wp; WhenValue = wv; Field = fname; Constraint = MustEqual c } ], acc
                            | None, Some(JStr "null") -> rs @ [ { ObjectType = typeId; WhenPath = wp; WhenValue = wv; Field = fname; Constraint = MustEqual JNull } ], acc
                            | _ ->
                                let k, acc = kindOf ctx file (Some fname) fnode acc
                                rs @ [ { ObjectType = typeId; WhenPath = wp; WhenValue = wv; Field = fname; Constraint = MustBeKind k } ], acc) (rs, acc)
                    | _ -> rs, acc) ([], acc)
            KObject typeId, { acc with Rules = acc.Rules @ rules }

    /// Compiles every schema in `contract/v1.0.0/schemas` reachable from the envelope, the manifest and the snapshot.
    let compile (schemaRoot: string) : CompiledSchemas =
        let files =
            Directory.EnumerateFiles(schemaRoot, "*.schema.json", SearchOption.AllDirectories)
            |> Seq.map (relPath schemaRoot)
            |> Seq.sort
            |> List.ofSeq
        let ctx =
            { Root = schemaRoot
              Docs = files |> List.map (fun f -> f, Json.parseFile (Path.Combine(schemaRoot, f))) |> Map.ofList }
        let empty: Acc = { Types = Map.empty; Fields = []; Rules = []; Enums = Map.empty }
        // Seed with common enumerations so unused ones are still registered.
        let common = loadDoc ctx "common.schema.json" |> Json.field "$defs"
        let acc =
            match common with
            | JObj defs ->
                defs |> List.fold (fun (acc: Acc) (name, node) ->
                    match Json.tryField "enum" node with
                    | Some(JArr vs) -> { acc with Enums = acc.Enums |> Map.add name (vs |> List.map Json.str) }
                    | _ -> acc) empty
            | _ -> empty
        let roots = [ "event-envelope.schema.json"; "investigation-manifest.schema.json"; "snapshot.schema.json" ]
        let acc = roots |> List.fold (fun acc f -> kindOf ctx f (Some "#root") (loadDoc ctx f) acc |> snd) acc
        // Payload and entity files are reached through rules and arrays; make sure each one is registered.
        let acc =
            files
            |> List.filter (fun f -> f.StartsWith "payloads/" || f.StartsWith "entities/")
            |> List.fold (fun acc f -> kindOf ctx f (Some "#root") (loadDoc ctx f) acc |> snd) acc
        { ObjectTypes = acc.Types |> Map.toList |> List.map snd |> List.sortBy (fun t -> t.TypeId)
          Fields = acc.Fields
          Rules = acc.Rules
          Enums = acc.Enums |> Map.toList
          BundleSha256 = Json.sha256File (Path.Combine(schemaRoot, "bundle.json")) }

    // ---- Kind <-> JSON (the registry's machine-readable form) -------------------------------------------------------

    let rec kindToJson (k: Kind) : J =
        match k with
        | KUuid -> Json.o [ "type", Json.s "uuid" ]
        | KTimestamp -> Json.o [ "type", Json.s "timestamp" ]
        | KText -> Json.o [ "type", Json.s "text" ]
        | KHttpUrl -> Json.o [ "type", Json.s "httpUrl" ]
        | KSha256 -> Json.o [ "type", Json.s "sha256" ]
        | KInteger(a, b) -> Json.o [ "type", Json.s "integer"; "min", Json.l a; "max", Json.l b ]
        | KConst c -> Json.o [ "type", Json.s "const"; "value", c ]
        | KEnum(n, vs) -> Json.o [ "type", Json.s "enum"; "enum", Json.s n; "values", Json.ss vs ]
        | KNullable inner -> Json.o [ "type", Json.s "nullable"; "of", kindToJson inner ]
        | KList(item, u, mn, mx) ->
            Json.o ([ "type", Json.s "list"; "of", kindToJson item; "unique", Json.b u; "minItems", Json.i mn ]
                    @ (mx |> Option.map (fun m -> [ "maxItems", Json.i m ]) |> Option.defaultValue []))
        | KObject t -> Json.o [ "type", Json.s "object"; "objectType", Json.s t ]
        | KAnyObject -> Json.o [ "type", Json.s "object-bound-by-rule" ]

    let rec kindOfJson (j: J) : Kind =
        match Json.fieldStr "type" j with
        | "uuid" -> KUuid
        | "timestamp" -> KTimestamp
        | "text" -> KText
        | "httpUrl" -> KHttpUrl
        | "sha256" -> KSha256
        | "integer" -> KInteger(Json.intOf "min" j, Json.intOf "max" j)
        | "const" -> KConst(Json.field "value" j)
        | "enum" -> KEnum(Json.fieldStr "enum" j, Json.strs "values" j)
        | "nullable" -> KNullable(kindOfJson (Json.field "of" j))
        | "list" ->
            KList(kindOfJson (Json.field "of" j), Json.boolOf "unique" j, int (Json.intOf "minItems" j),
                  Json.tryField "maxItems" j |> Option.map (function JInt n -> int n | _ -> 0))
        | "object" -> KObject(Json.fieldStr "objectType" j)
        | "object-bound-by-rule" -> KAnyObject
        | other -> failwithf "unknown kind %s" other

    let rec kindLabel (k: Kind) =
        match k with
        | KUuid -> "uuid"
        | KTimestamp -> "timestamp"
        | KText -> "text"
        | KHttpUrl -> "httpUrl"
        | KSha256 -> "sha256"
        | KInteger _ -> "integer"
        | KConst c -> "const " + Json.canonical c
        | KEnum(n, _) -> "enum " + n
        | KNullable i -> kindLabel i + "?"
        | KList(i, _, _, _) -> kindLabel i + "[]"
        | KObject t -> t
        | KAnyObject -> "object"
