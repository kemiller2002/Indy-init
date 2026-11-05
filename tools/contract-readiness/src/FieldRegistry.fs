namespace Causa.ContractReadiness

open System
open System.IO

/// One registered field. Wire fields come from the causa-storage v1.0.0 schemas; their F# element, F# type and
/// meaning come from contract/v1.0.0/FIELD-MAPPING.md. Proposed fields come from DOMAIN-CONTRACT.json's
/// `proposedStorageExtension` and are never counted as available until that extension is released.
type RegistryField =
    { Def: FieldDef
      Status: string
      FsharpElement: string option
      FsharpType: string option
      Meaning: string option }

type Registry =
    { ObjectTypes: ObjectTypeDef list
      Fields: RegistryField list
      Rules: Rule list
      Enums: (string * string list) list
      BundleSha256: string
      FieldMappingSha256: string }

module FieldRegistry =
    let statusWire = "wire-v1.0.0"
    let statusProposed = "proposed-causa-storage-1.1.0"

    /// FIELD-MAPPING.md: `### \`EventType\`` sections with `| wire field | F# element | F# type | ... | meaning |` rows,
    /// and the `## Envelope` table. Returns (objectTypeId, wireField) -> (element, fsharpType, meaning).
    let parseFieldMapping (path: string) : Map<string * string, string * string * string> =
        let lines = File.ReadAllLines path |> List.ofArray
        let cell (s: string) = s.Trim().Trim('`').Trim()
        let folder (section: string option, acc: Map<_, _>) (line: string) =
            if line.StartsWith "## Envelope" then Some "Envelope", acc
            elif line.StartsWith "### `" then Some("Payload." + line.Substring(5).TrimEnd('`').Trim()), acc
            elif line.StartsWith "## " then None, acc
            else
                match section with
                | Some owner when line.StartsWith "| `" ->
                    let cells = line.Split('|') |> Array.map cell |> Array.filter (fun _ -> true)
                    // cells.[0] is empty (leading pipe).
                    let wire = cells.[1]
                    if owner = "Envelope" then
                        section, acc |> Map.add (owner, wire) (cells.[2], cells.[3], cells.[4])
                    else
                        let meaning = if cells.Length > 7 then cells.[7] else ""
                        section, acc |> Map.add (owner, wire) (cells.[2], cells.[3], meaning)
                | _ -> section, acc
        lines |> List.fold folder (None, Map.empty) |> snd

    let fromSchemas (schemas: CompiledSchemas) (fieldMappingPath: string) (proposed: RegistryField list) (proposedTypes: ObjectTypeDef list) : Registry =
        let mapping = parseFieldMapping fieldMappingPath
        let wire =
            schemas.Fields
            |> List.map (fun f ->
                let m = mapping |> Map.tryFind (f.ObjectType, f.Name)
                { Def = f
                  Status = statusWire
                  FsharpElement = m |> Option.map (fun (e, _, _) -> e)
                  FsharpType = m |> Option.map (fun (_, t, _) -> t)
                  Meaning = m |> Option.map (fun (_, _, mn) -> mn) |> Option.filter (String.IsNullOrWhiteSpace >> not) })
        { ObjectTypes = schemas.ObjectTypes @ proposedTypes
          Fields = wire @ proposed
          Rules = schemas.Rules
          Enums = schemas.Enums
          BundleSha256 = schemas.BundleSha256
          FieldMappingSha256 = Json.sha256File fieldMappingPath }

    let presenceToJson =
        function
        | Always -> Json.s "always"
        | When(p, vs) -> Json.o [ "when", Json.s p; "in", JArr vs ]

    let presenceOfJson (j: J) =
        match j with
        | JStr "always" -> Always
        | other -> When(Json.fieldStr "when" other, Json.arrOf "in" other)

    let ruleToJson (r: Rule) =
        Json.o
            [ "objectType", Json.s r.ObjectType
              "when", Json.o [ "path", Json.s r.WhenPath; "equals", r.WhenValue ]
              "field", Json.s r.Field
              (match r.Constraint with
               | MustEqual v -> "mustEqual", v
               | MustBeKind k -> "mustBeKind", Schema.kindToJson k) ]

    let ruleOfJson (j: J) =
        let w = Json.field "when" j
        { ObjectType = Json.fieldStr "objectType" j
          WhenPath = Json.fieldStr "path" w
          WhenValue = Json.field "equals" w
          Field = Json.fieldStr "field" j
          Constraint =
            match Json.tryField "mustEqual" j with
            | Some v -> MustEqual v
            | None -> MustBeKind(Schema.kindOfJson (Json.field "mustBeKind" j)) }

    let fieldToJson (f: RegistryField) =
        Json.o
            [ "fieldId", Json.s f.Def.FieldId
              "objectType", Json.s f.Def.ObjectType
              "name", Json.s f.Def.Name
              "kind", Schema.kindToJson f.Def.Kind
              "kindLabel", Json.s (Schema.kindLabel f.Def.Kind)
              "presence", presenceToJson f.Def.Presence
              "nullable", Json.b (match f.Def.Kind with KNullable _ -> true | _ -> false)
              "status", Json.s f.Status
              "fsharpElement", Json.opt Json.s f.FsharpElement
              "fsharpType", Json.opt Json.s f.FsharpType
              "meaning", Json.opt Json.s f.Meaning
              "schemaFile", Json.s f.Def.SchemaFile ]

    let fieldOfJson (j: J) =
        { Def =
            { FieldId = Json.fieldStr "fieldId" j
              ObjectType = Json.fieldStr "objectType" j
              Name = Json.fieldStr "name" j
              Kind = Schema.kindOfJson (Json.field "kind" j)
              Presence = presenceOfJson (Json.field "presence" j)
              SchemaFile = Json.fieldStr "schemaFile" j }
          Status = Json.fieldStr "status" j
          FsharpElement = Json.strOpt "fsharpElement" j
          FsharpType = Json.strOpt "fsharpType" j
          Meaning = Json.strOpt "meaning" j }

    let typeToJson (t: ObjectTypeDef) =
        Json.o
            [ "typeId", Json.s t.TypeId
              "schemaFile", Json.s t.SchemaFile
              "schemaSha256", Json.s t.SchemaSha256
              "description", Json.s t.Description ]

    let typeOfJson (j: J) =
        { TypeId = Json.fieldStr "typeId" j
          Name = Json.fieldStr "typeId" j
          SchemaFile = Json.fieldStr "schemaFile" j
          SchemaSha256 = Json.fieldStr "schemaSha256" j
          Description = Json.fieldStr "description" j }

    let toJson (pin: J) (r: Registry) =
        let wireCount = r.Fields |> List.filter (fun f -> f.Status = statusWire) |> List.length
        Json.o
            [ "schema", Json.s "causa.contract-readiness.field-registry/1"
              "generatedBy", Json.s "tools/contract-readiness (dotnet run -- generate); do not edit by hand"
              "source", pin
              "schemaBundleSha256", Json.s r.BundleSha256
              "fieldMappingSha256", Json.s r.FieldMappingSha256
              "fieldIdConvention", Json.s "F.<objectTypeId>.<wireName>. objectTypeId: Envelope | Manifest | Snapshot | State | Payload.<eventType> | Entity.<entity> | Def.<definition>; proposed causa-storage 1.1.0 types use the same scheme with status proposed-causa-storage-1.1.0."
              "counts",
              Json.o
                  [ "objectTypes", Json.i (List.length r.ObjectTypes)
                    "fields", Json.i (List.length r.Fields)
                    "wireFields", Json.i wireCount
                    "proposedFields", Json.i (List.length r.Fields - wireCount)
                    "rules", Json.i (List.length r.Rules)
                    "enumerations", Json.i (List.length r.Enums) ]
              "enumerations", Json.o (r.Enums |> List.map (fun (n, vs) -> n, Json.ss vs))
              "objectTypes", JArr(r.ObjectTypes |> List.map typeToJson)
              "fields", JArr(r.Fields |> List.map fieldToJson)
              "rules", JArr(r.Rules |> List.map ruleToJson) ]

    let ofJson (j: J) : Registry =
        { ObjectTypes = Json.arrOf "objectTypes" j |> List.map typeOfJson
          Fields = Json.arrOf "fields" j |> List.map fieldOfJson
          Rules = Json.arrOf "rules" j |> List.map ruleOfJson
          Enums =
            match Json.field "enumerations" j with
            | JObj ms -> ms |> List.map (fun (k, v) -> k, Json.arr v |> List.map Json.str)
            | _ -> []
          BundleSha256 = Json.fieldStr "schemaBundleSha256" j
          FieldMappingSha256 = Json.fieldStr "fieldMappingSha256" j }

    let wireFields (r: Registry) = r.Fields |> List.filter (fun f -> f.Status = statusWire)
    let childrenOf (r: Registry) (objectType: string) = wireFields r |> List.filter (fun f -> f.Def.ObjectType = objectType)
    let byId (r: Registry) = r.Fields |> List.map (fun f -> f.Def.FieldId, f) |> Map.ofList
