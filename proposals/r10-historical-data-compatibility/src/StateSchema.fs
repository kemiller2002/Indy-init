namespace Causa.HistoricalCompatibility

/// Stage 7 for snapshot `state` (state.schema.json + entities/*.schema.json, CONTRACT §8.2-8.3): a declarative,
/// closed-key shape table. Content equality with the replay is stage 17; this only checks shape.
module StateSchema =
    type Kind =
        | KUuid
        | KUuidN
        | KText
        | KTextN
        | KTs
        | KTsN
        | KEnum of WireEnumValues: string list
        | KInt of minimum: int64
        | KUuids
        | KUuidsNonEmpty
        | KProvenance
        | KMembers
        | KHistory

    let private wires (e: WireEnum<'T>) = KEnum(e.Table |> List.map fst)
    let private targets (e: WireEnum<'T>) (allowed: 'T list) = KEnum(allowed |> List.map (WireEnum.toWire e))

    let private prov = "provenance", KProvenance

    let entities : (string * (string * Kind) list) list =
        [ "observations", [ "id", KUuid; "original", KText; "observedAt", KTsN; prov ]
          "hypotheses", [ "id", KUuid; "statement", KText; "mechanism", KTextN; "state", wires Enums.hypothesisState; "history", KHistory; prov ]
          "evidence",
          [ "id", KUuid; "contentKind", wires Enums.contentKind; "content", KText; "category", wires Enums.evidenceCategory; "source", KText; "capturedAt", KTsN
            "verification", wires Enums.verificationStatus; "supersededBy", KUuidN; "admittedAtVersion", KInt 1L; prov ]
          "relationships", [ "id", KUuid; "evidenceId", KUuid; "hypothesisId", KUuid; "kind", wires Enums.relationshipKind; "rationale", KTextN; prov ]
          "unknowns", [ "id", KUuid; "question", KText; "materiality", wires Enums.materiality; "state", wires Enums.unknownState; "basis", KTextN; prov ]
          "assumptions", [ "id", KUuid; "proposition", KText; "materiality", wires Enums.materiality; "state", wires Enums.assumptionState; prov ]
          "contradictions", [ "id", KUuid; "statement", KText; "evidenceIds", KUuids; "materiality", wires Enums.materiality; "state", wires Enums.contradictionState; "basis", KTextN; prov ]
          "predictions", [ "id", KUuid; "hypothesisId", KUuid; "statement", KText; "strength", wires Enums.predictionStrength; "timing", wires Enums.predictionTiming; prov ]
          "actions", [ "id", KUuid; "kind", wires Enums.actionKind; "purpose", KText; "targetHypothesisIds", KUuids; "state", wires Enums.actionState; prov ]
          "findings", [ "id", KUuid; "actionId", KUuid; "originalResult", KText; "interpretation", KTextN; prov ]
          "negativeKnowledge", [ "id", KUuid; "statement", KText; "scope", KText; "basisEvidenceIds", KUuidsNonEmpty; prov ]
          "causes", [ "id", KUuid; "outcome", KText; "members", KMembers; "mechanism", KTextN; "state", wires Enums.causeState; prov ]
          "correctiveActions",
          [ "id", KUuid; "kind", wires Enums.correctiveActionKind; "description", KText; "targetCauseIds", KUuids; "state", wires Enums.correctiveActionState
            "awaitingSinceVersion", KInt 0L; prov ]
          "criteria", [ "id", KUuid; "correctiveActionId", KUuid; "expectedOutcome", KText; "method", KTextN; prov ]
          "verifications", [ "id", KUuid; "criterionId", KUuid; "outcome", wires Enums.verificationOutcome; "evidenceIds", KUuids; "recordedAtVersion", KInt 1L; prov ]
          "conclusions",
          [ "id", KUuid; "statement", KText; "strength", wires Enums.conclusionStrength; "causeIds", KUuids; "residualUncertainty", KTextN; "recordedAtVersion", KInt 1L; prov ]
          "proposals", [ "id", KUuid; "kind", wires Enums.proposalKind; "text", KText; "rationale", KTextN; "state", wires Enums.proposalState; "decision", KTextN; prov ] ]

    let private historyEntry =
        [ "from", wires Enums.hypothesisState; "to", targets Enums.hypothesisState Enums.hypothesisTarget; "command", wires Enums.hypothesisCommand
          "reason", KText; "evidenceIds", KUuids; "at", KTs; "aggregateVersion", KInt 1L ]

    let rec private checkValue (path: string) (kind: Kind) (v: JValue) : Result<unit, string> =
        let o = [ "x", v ]
        let asUnit (r: Result<'T, string>) = r |> Result.map ignore |> Result.mapError (fun e -> path + ": " + e)
        match kind, v with
        | KUuidN, JNull | KTextN, JNull | KTsN, JNull -> Ok()
        | (KUuid | KUuidN), _ -> Field.uuid o "x" |> asUnit
        | (KText | KTextN), _ -> Field.text o "x" |> asUnit
        | (KTs | KTsN), _ -> Field.timestamp o "x" |> asUnit
        | KEnum values, JStr s when List.contains s values -> Ok()
        | KEnum _, _ -> Error(path + ": value outside its enumeration")
        | KInt m, JInt n when n >= m -> Ok()
        | KInt _, _ -> Error(path + ": integer out of range or wrong type")
        | KUuids, _ -> Field.uuids o "x" |> asUnit
        | KUuidsNonEmpty, _ -> Field.nonEmptyUuids o "x" |> asUnit
        | KProvenance, JObj p ->
            result {
                do! Field.exactKeys path [ "actor"; "recordedAt" ] p
                let! a = Field.obj p "actor"
                let! _ = Envelope.decodeActor a |> Result.mapError (fun e -> path + ": " + e)
                do! checkValue (path + ".recordedAt") KTs (Json.tryField "recordedAt" p |> Option.defaultValue JNull)
            }
        | KMembers, JArr ms when not ms.IsEmpty && List.length (List.distinct ms) = List.length ms ->
            ms |> traverse (checkObject (path + "[]") [ "hypothesisId", KUuid; "role", wires Enums.causeRole ]) |> Result.map ignore
        | KMembers, _ -> Error(path + ": members must be a non-empty array of distinct members")
        | KHistory, JArr hs -> hs |> traverse (checkObject (path + "[]") historyEntry) |> Result.map ignore
        | (KProvenance | KHistory), _ -> Error(path + ": wrong type")

    and private checkObject (path: string) (spec: (string * Kind) list) (v: JValue) : Result<unit, string> =
        match v with
        | JObj o ->
            result {
                do! Field.exactKeys path (spec |> List.map fst) o
                let! _ = spec |> traverse (fun (k, kind) -> checkValue (path + "." + k) kind (Json.tryField k o |> Option.defaultValue JNull))
                return ()
            }
        | _ -> Error(path + ": must be an object")

    let private top =
        [ "investigationId", KUuid; "title", KText; "lifecycle", wires Enums.lifecycle; "version", KInt 1L; "createdAt", KTs ]

    /// Closed keys, required members, types and enumerations at every level of `state`.
    let check (state: JValue) : Result<unit, string> =
        match state with
        | JObj o ->
            result {
                do! Field.exactKeys "state" ((top |> List.map fst) @ (entities |> List.map fst)) o
                let! _ = top |> traverse (fun (k, kind) -> checkValue ("state." + k) kind (Json.tryField k o |> Option.defaultValue JNull))
                let! _ =
                    entities
                    |> traverse (fun (name, spec) ->
                        match Json.tryField name o with
                        | Some(JArr items) -> items |> traverse (checkObject ("state." + name + "[]") spec) |> Result.map ignore
                        | _ -> Error("state." + name + " must be an array"))
                return ()
            }
        | _ -> Error "state must be an object"
