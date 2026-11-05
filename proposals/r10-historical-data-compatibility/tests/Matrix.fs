/// Generates EVENT-COMPATIBILITY-MATRIX.json from the actual data, so no count in it is typed by hand.
module Matrix

open Causa.HistoricalCompatibility
open Checks

let private jstr (s: string) = JStr s
let private jint (n: int) = JInt(int64 n)
let private jlist f xs = JArr(xs |> List.map f)

/// Nullable payload fields per discriminator (CONTRACT §7 tables, column "nullable = yes"). Every field is required.
let nullable =
    Map.ofList
        [ "LifecycleChanged", [ "reason" ]; "ObservationRecorded", [ "observedAt" ]; "HypothesisRecorded", [ "mechanism" ]
          "EvidenceAdmitted", [ "capturedAt" ]; "EvidenceRelated", [ "rationale" ]; "FindingRecorded", [ "interpretation" ]
          "CausalAssertionRecorded", [ "mechanism" ]; "CriterionRecorded", [ "method" ]; "ConclusionRecorded", [ "residualUncertainty" ]
          "ProposalRecorded", [ "rationale" ] ]

/// Payload fields per discriminator, in CONTRACT §7 order (from EventEncoder, which mirrors the decoder).
let private fieldsOf (d: DecodedEvent) = match EventEncoder.payload d.Event with JObj o -> o |> List.map fst | _ -> []

let private enumFields = set [ "from"; "to"; "command"; "kind"; "state"; "verification"; "category"; "contentKind"; "materiality"; "strength"; "timing"; "outcome" ]

let build (historical: Loaded list) (fixtures: Loaded list) : JValue =
    let decodedBy (ls: Loaded list) = ls |> List.map (fun l -> l.Case.CaseKey, decodedOf l.Files)
    let hist = decodedBy historical
    let fix = fixtures |> List.collect (fun l -> decodedOf l.Files)
    let allHist = hist |> List.collect snd
    let verifiedCases = historical |> List.filter (fun l -> (verified l).IsSome) |> List.map (fun l -> l.Case.CaseKey) |> Set.ofList
    let countsFor (pred: DecodedEvent -> bool) =
        JObj(
            (hist |> List.map (fun (k, ds) -> k, jint (ds |> List.filter pred |> List.length)))
            @ [ "historicalTotal", jint (allHist |> List.filter pred |> List.length); "syntheticFixtures", jint (fix |> List.filter pred |> List.length) ]
        )
    let frozenJson =
        function
        | FrozenCase c -> JObj [ "representation", jstr "frozen-case"; "case", jstr c; "droppedFields", JArr [] ]
        | FrozenCaseLossy(c, dropped) -> JObj [ "representation", jstr "frozen-case-lossy"; "case", jstr c; "droppedFields", jlist jstr dropped ]
        | NoFrozenCase reason -> JObj [ "representation", jstr "no-frozen-case"; "case", JNull; "reason", jstr reason ]
    let successorJson =
        function
        | KeepCase c -> JObj [ "change", jstr "keep"; "case", jstr c ]
        | WidenCase(c, w) -> JObj [ "change", jstr "widen"; "case", jstr c; "detail", jstr w ]
        | AddCase c -> JObj [ "change", jstr "add"; "case", jstr c ]
    let entries =
        EventDecoder.eventTypes
        |> List.mapi (fun i t ->
            let ofType = allHist |> List.filter (fun d -> d.Envelope.EventType = t)
            let sample = (allHist @ fix) |> List.tryFind (fun d -> d.Envelope.EventType = t)
            let fields = sample |> Option.map fieldsOf |> Option.defaultValue []
            let keySets = ofType |> List.map (fun d -> d.Envelope.Payload |> List.map fst |> List.sort |> String.concat ",") |> List.distinct
            let nulls =
                fields
                |> List.choose (fun f ->
                    let n = ofType |> List.filter (fun d -> Json.tryField f d.Envelope.Payload = Some JNull) |> List.length
                    if n > 0 then Some(f, jint n) else None)
            let enums =
                fields
                |> List.filter enumFields.Contains
                |> List.map (fun f ->
                    f,
                    JObj(
                        ofType
                        |> List.choose (fun d -> match Json.tryField f d.Envelope.Payload with Some(JStr s) -> Some s | _ -> None)
                        |> List.countBy id
                        |> List.sortBy fst
                        |> List.map (fun (v, c) -> v, jint c)
                    ))
            let variants =
                EventCompatibility.table
                |> List.filter (fun e -> e.Variant = t || e.Variant.StartsWith(t + "/"))
                |> List.map (fun e ->
                    JObj
                        [ "variant", jstr e.Variant
                          "counts", countsFor (fun d -> EventCompatibility.variant d.Event = e.Variant)
                          "frozenV414", frozenJson e.Frozen
                          "proposedSuccessor", successorJson e.Successor ])
            let inCases = hist |> List.filter (fun (_, ds) -> ds |> List.exists (fun d -> d.Envelope.EventType = t)) |> List.map fst
            let projectionVerifiedIn = inCases |> List.filter verifiedCases.Contains
            let status =
                if not ofType.IsEmpty then
                    $"decoded and byte-for-byte round-tripped on all {List.length ofType} historical events; projection reproduced snapshot.json in {List.length projectionVerifiedIn}/{List.length inCases} cases; admissibility not evaluated"
                elif fix |> List.exists (fun d -> d.Envelope.EventType = t) then
                    "absent from the historical cases; decoded, round-tripped and projected against the golden states on the frozen synthetic fixtures only"
                else "never observed"
            JObj
                [ "discriminator", jstr t
                  "registryIndex", jint (i + 1)
                  "counts", countsFor (fun d -> d.Envelope.EventType = t)
                  "decoderSupport", jstr ("typed: WireEvent." + t + " (src/EventDecoder.fs); no catch-all case")
                  "requiredFields", jlist jstr fields
                  "nullableFields", jlist jstr (nullable.TryFind t |> Option.defaultValue [])
                  "historicalVariations", JObj [ "payloadKeySets", jlist jstr keySets; "nullValues", JObj nulls; "enumValues", JObj enums ]
                  "variants", JArr variants
                  "replay",
                  JObj
                      [ "projection", jstr ("CONTRACT §8.3 row " + t + " (src/ReplayAdapter.fs)")
                        "projectionVerifiedInCases", jlist jstr projectionVerifiedIn
                        "authoritativeReplay", jstr "not established: admissibility (CONTRACT §11.3, engine History.validate) is not evaluated by this adapter" ]
                  "verificationStatus", jstr status ])
    let frozenTotals =
        let count pred = allHist |> List.filter (fun d -> pred (EventCompatibility.entry d.Event).Frozen) |> List.length
        JObj
            [ "frozenCaseNoFieldDropped", jint (count (function FrozenCase _ -> true | _ -> false))
              "frozenCaseFieldsDropped", jint (count (function FrozenCaseLossy _ -> true | _ -> false))
              "noFrozenCase", jint (count (function NoFrozenCase _ -> true | _ -> false)) ]
    JObj
        [ "schema", jstr "r10-historical-data-compatibility/event-compatibility-matrix/1"
          "generatedBy", jstr "dotnet run --project proposals/r10-historical-data-compatibility/tests -- --causa-data <checkout> --write"
          "source", jstr "kemiller2002/causa-data@e8259df74add723765c57a5b1a5888d9c3f707af catalog/historical-v1 (workspaces/demo/historical-v1/investigations)"
          "frozenContract", jstr "Indy-init requirements-compiled/v4.1.4/contracts/00-DOMAIN-STUBS.fs (CoreEvent :92-120, EventEnvelope :126)"
          "registeredDiscriminators", jint (List.length EventDecoder.eventTypes)
          "discriminatorsInHistoricalData", jint (allHist |> List.map (fun d -> d.Envelope.EventType) |> List.distinct |> List.length)
          "historicalEvents", jint (List.length allHist)
          "frozenEnvelopeDroppedFields", jlist jstr EventCompatibility.frozenEnvelopeDroppedFields
          "historicalEventsByFrozenRepresentation", frozenTotals
          "eventTypes", JArr entries ]
