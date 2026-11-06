namespace Causa.ContractReadiness

open System
open System.IO

type Verdict =
    | VerdictPass
    | VerdictBlocked
    | VerdictUnverified

module Gate =
    let verdictText =
        function
        | VerdictPass -> "PASS"
        | VerdictBlocked -> "BLOCKED"
        | VerdictUnverified -> "UNVERIFIED"

    let outcomeText =
        function
        | Pass -> "PASS"
        | Fail _ -> "FAIL"
        | Unverified _ -> "UNVERIFIED"

    let checkJson (c: CheckResult) =
        Json.o
            [ "checkId", Json.s c.CheckId
              "title", Json.s c.Title
              "category", Json.s c.Category
              "outcome", Json.s (outcomeText c.Outcome)
              "details",
              (match c.Outcome with
               | Fail ps -> Json.ss ps
               | Unverified r -> Json.ss [ r ]
               | Pass -> JArr [])
              "metrics", Json.o c.Metrics ]

    let controlJson (r: ControlResult) =
        Json.o
            [ "controlId", Json.s r.Control.ControlId
              "defect", Json.s r.Control.Description
              "mustFail", Json.ss r.Control.MustFail
              "failedAsExpected", Json.ss r.FailedAsExpected
              "didNotFail", Json.ss r.DidNotFail
              "outcome", Json.s (if List.isEmpty r.DidNotFail then "FAILED-AS-EXPECTED" else "CONTROL-NOT-DETECTED") ]

    /// PASS needs every mandatory obligation, compatible history, covered fields and commands, working negative
    /// controls, no open blocking owner decision and a reproducible package. BLOCKED: a contract is missing or
    /// incompatible, data would be lost, a command or transition is undefined, or a blocking decision is open.
    /// UNVERIFIED: otherwise clean, but required evidence could not be executed. Nothing here can waive a reason.
    let decide (pkg: ContractPackage) (checks: CheckResult list) (controls: ControlResult list) (reproducible: bool) =
        let failed = checks |> List.filter (fun c -> match c.Outcome with Fail _ -> true | _ -> false)
        let unverified = checks |> List.filter (fun c -> match c.Outcome with Unverified _ -> true | _ -> false)
        let controlsBad = controls |> List.filter (fun r -> not (List.isEmpty r.DidNotFail))
        let openBlocking = pkg.Decisions |> List.filter (fun d -> d.Blocking && d.Status.StartsWith "open")
        let blockedBindings = pkg.Bindings |> List.filter (fun b -> b.Status = "blocked")
        let proposedCommands = pkg.Commands |> List.filter (fun c -> c.Status = "proposed-successor")
        let blocked =
            (failed |> List.map (fun c -> sprintf "%s failed: %s" c.CheckId c.Title))
            @ (controlsBad |> List.map (fun r -> sprintf "%s not detected by %A" r.Control.ControlId r.DidNotFail))
            @ (if reproducible then [] else [ "generated package files differ from the committed ones" ])
            @ (openBlocking |> List.map (fun d -> sprintf "owner decision %s is open: %s [%s]" d.DecisionId d.Title d.Status))
            @ (if List.isEmpty blockedBindings then [] else [ sprintf "%d UI bindings have no defined write path: %s" blockedBindings.Length (String.Join(", ", blockedBindings |> List.map (fun b -> b.BindingId + " " + b.RequirementId))) ])
            @ (if List.isEmpty proposedCommands then [] else [ sprintf "%d required commands are undefined in the successor candidate: %s" proposedCommands.Length (String.Join(", ", proposedCommands |> List.map (fun c -> c.CommandId))) ])
        let verdict =
            if not (List.isEmpty blocked) then VerdictBlocked
            elif not (List.isEmpty unverified) then VerdictUnverified
            else VerdictPass
        verdict, blocked, unverified |> List.map (fun c -> sprintf "%s: %s" c.CheckId (match c.Outcome with Unverified r -> r | _ -> ""))

    // ---- Inventory --------------------------------------------------------------------------------------------------

    let inventory (inputs: Inputs) (histories: HistoryFile list) (negatives: NegativeFixture list) (prov: ProvenanceCheck list) (pin: J) =
        let reg = inputs.Registry
        let distinct = histories |> List.distinctBy (fun h -> h.EventsSha256)
        let observed (hs: HistoryFile list) =
            hs |> List.collect (fun h -> h.Events |> List.collect (fun e -> Codec.observedFields e.Decoded.Value))
        let fieldTable (obs: (string * string) list) =
            obs |> List.groupBy fst |> List.sortBy fst
            |> List.map (fun (fid, xs) ->
                fid, Json.o [ "occurrences", Json.i xs.Length; "valueKinds", Json.o (xs |> List.countBy snd |> List.sortBy fst |> List.map (fun (k, c) -> k, Json.i c)) ])
        let counts (xs: string list) = Json.o (xs |> List.countBy id |> List.sortBy fst |> List.map (fun (k, c) -> k, Json.i c))
        let payloadStr path (e: EventRecord) = Codec.at ("payload." + path) e.Decoded.Value |> Codec.asString
        let caseEntry (h: HistoryFile) =
            let copies = histories |> List.filter (fun x -> x.EventsSha256 = h.EventsSha256) |> List.map (fun x -> x.Dir)
            let occurred = h.Events |> List.choose (fun e -> Codec.at "occurredAt" e.Decoded.Value |> Codec.asString)
            let relationshipIds = h.Events |> List.filter (fun e -> e.EventType = "EvidenceRelated")
            Json.o
                [ "path", Json.s h.Dir
                  "datasetKind", Json.s h.DatasetKind
                  "datasetId", Json.s h.DatasetId
                  "catalogs", Json.ss h.Catalogs
                  "caseKey", Json.opt Json.s h.CaseKey
                  "investigationId", Json.s h.InvestigationId
                  "eventsSha256", Json.s h.EventsSha256
                  "identicalCopies", Json.ss copies
                  "events", Json.i h.Events.Length
                  "formatVersions", Json.ss (h.Events |> List.choose (fun e -> Codec.at "formatVersion" e.Decoded.Value |> Codec.asString) |> List.distinct)
                  "semanticVersions", Json.ss (h.Events |> List.choose (fun e -> Codec.at "semanticVersion" e.Decoded.Value |> Codec.asString) |> List.distinct)
                  "eventTypes", counts (h.Events |> List.map (fun e -> e.EventType))
                  "variants", counts (h.Events |> List.map (fun e -> e.Variant |> Option.map (fun v -> v.Name) |> Option.defaultValue "(none)"))
                  "relationshipKinds", counts (relationshipIds |> List.choose (payloadStr "kind"))
                  "relationshipsWithRationale", Json.i (relationshipIds |> List.filter (fun e -> payloadStr "rationale" e |> Option.isSome) |> List.length)
                  "actorKinds", counts (h.Events |> List.choose (fun e -> Codec.at "actor.kind" e.Decoded.Value |> Codec.asString))
                  "actorIds", Json.ss (h.Events |> List.choose (fun e -> Codec.at "actor.actorId" e.Decoded.Value |> Codec.asString) |> List.distinct)
                  "timestampFormat", Json.s "RFC 3339 UTC, exactly three fractional digits, Z (every value decoded as KTimestamp)"
                  "occurredAtRange", Json.ss (if List.isEmpty occurred then [] else [ List.min occurred; List.max occurred ])
                  "occurredAtNonDecreasing", Json.b (occurred |> List.pairwise |> List.forall (fun (a, b) -> a <= b))
                  "correlationGroups", Json.i (h.Events |> List.choose (fun e -> Codec.at "correlationId" e.Decoded.Value |> Codec.asString) |> List.distinct |> List.length)
                  "unknownOrUnsupported", Json.ss (h.Events |> List.collect (fun e -> e.Decoded.Problems |> List.map (fun p -> sprintf "%s:%d %s" e.File e.Line (Codec.problemText p))))
                  "manifestProblems", Json.ss (h.ManifestProblems |> List.map Codec.problemText)
                  "snapshotProblems", Json.ss (h.SnapshotProblems |> List.map Codec.problemText)
                  "integrityProblems", Json.ss h.IntegrityProblems
                  "requiredMigration", Json.s "none: every file is formatVersion/semanticVersion 1.0.0; the proposed 1.1.0 extension is additive and never rewrites v1.0.0 files (CONTRACT §13.2)" ]
        let allObserved = observed distinct |> List.map fst |> Set.ofList
        let neverObserved =
            // Only fields an event line can carry: manifests and snapshots are checked by CR-HIST-04, not observed here.
            FieldRegistry.wireFields reg
            |> List.filter (fun f -> f.Def.ObjectType = "Envelope" || f.Def.ObjectType.StartsWith "Payload." || f.Def.ObjectType = "Def.actor" || f.Def.ObjectType = "Entity.causal-member")
            |> List.map (fun f -> f.Def.FieldId) |> List.filter (allObserved.Contains >> not)
        let nullable =
            observed distinct |> List.filter (fun (_, k) -> k = "null") |> List.map fst |> List.countBy id |> List.sortBy fst
        Json.o
            [ "schema", Json.s "causa.contract-readiness.historical-data-inventory/1"
              "generatedBy", Json.s "tools/contract-readiness (dotnet run -- generate); do not edit by hand"
              "source", pin
              "totals",
              Json.o
                  [ "investigationDirectories", Json.i histories.Length
                    "distinctHistories", Json.i distinct.Length
                    "eventsAllCopies", Json.i (histories |> List.sumBy (fun h -> h.Events.Length))
                    "eventsDistinct", Json.i (distinct |> List.sumBy (fun h -> h.Events.Length))
                    "eventsHistoricalCases", Json.i (histories |> List.filter Checks.isHistorical |> List.sumBy (fun h -> h.Events.Length))
                    "eventsActiveCatalog", Json.i (histories |> List.filter (Checks.isActiveCatalog inputs) |> List.sumBy (fun h -> h.Events.Length))
                    "eventTypesObserved", Json.i (distinct |> List.collect (fun h -> h.Events |> List.map (fun e -> e.EventType)) |> List.distinct |> List.length)
                    "variantsObserved", Json.i (distinct |> List.collect (fun h -> h.Events |> List.choose (fun e -> e.Variant |> Option.map (fun v -> v.Name))) |> List.distinct |> List.length)
                    "eventTypesObservedHistoricalCases", Json.i (histories |> List.filter Checks.isHistorical |> List.collect (fun h -> h.Events |> List.map (fun e -> e.EventType)) |> List.distinct |> List.length)
                    "variantsObservedHistoricalCases", Json.i (histories |> List.filter Checks.isHistorical |> List.collect (fun h -> h.Events |> List.choose (fun e -> e.Variant |> Option.map (fun v -> v.Name))) |> List.distinct |> List.length)
                    "eventTypesRegistered", Json.i 31 ]
              "storageLocations",
              Json.o
                  [ "events", Json.s "<dataset>/investigations/<investigationId>/events.ndjson"
                    "manifest", Json.s "<dataset>/investigations/<investigationId>/manifest.json"
                    "snapshot", Json.s "<dataset>/investigations/<investigationId>/snapshot.json"
                    "activeCatalog", Json.s "catalog/active.json"
                    "catalogManifests", Json.s "catalog/<catalogId>/manifest.json"
                    "historicalProvenance", Json.s "catalog/historical-v1/provenance/<caseKey>.json"
                    "researchSources", Json.s "research/historical/<caseKey>/{claims,sources}.json" ]
              "catalogs", Json.o (inputs.Catalogs |> List.map (fun (c, cases) -> c, Json.o [ "active", Json.b (c = inputs.ActiveCatalog); "cases", JArr(cases |> List.map (fun (p, k) -> Json.o [ "caseKey", Json.s k; "path", Json.s p ])) ]))
              "datasets", JArr(histories |> List.map caseEntry)
              "fieldsObserved", Json.o (fieldTable (observed distinct))
              "nullValuesObserved", Json.o (nullable |> List.map (fun (k, c) -> k, Json.i c))
              "eventFieldsNeverObserved", Json.ss neverObserved
              "relationshipTypes",
              Json.o
                  [ "evidenceToHypothesis", Json.s "EvidenceRelated (kind, rationale)"
                    "hypothesisCitesEvidence", Json.s "HypothesisTransitioned.evidenceIds"
                    "causalMembers", Json.s "CausalAssertionRecorded.members[] {hypothesisId, role}"
                    "evidenceSupersession", Json.s "EvidenceSuperseded.replacementEvidenceId"
                    "contradictionEvidence", Json.s "ContradictionRecorded.evidenceIds"
                    "actionTargets", Json.s "ActionRecorded.targetHypothesisIds; FindingRecorded.actionId"
                    "correctiveTargets", Json.s "CorrectiveActionRecorded.targetCauseIds; CriterionRecorded.correctiveActionId; VerificationRecorded.criterionId/evidenceIds"
                    "conclusionCauses", Json.s "ConclusionRecorded.causeIds"
                    "negativeKnowledgeBasis", Json.s "NegativeKnowledgeEstablished.basisEvidenceIds"
                    "proposalTarget", Json.s "ProposalWasAccepted.targetId" ]
              "provenance",
              JArr(prov |> List.map (fun p ->
                  Json.o
                      [ "caseKey", Json.s p.CaseKey
                        "records", Json.i p.Records
                        "evidenceAdmitted", Json.i p.EvidenceAdmitted
                        "recordsWithoutEvidence", Json.ss p.MissingEvidence
                        "evidenceWithoutRecord", Json.ss p.EvidenceWithoutProvenance
                        "urlNotInSourceText", Json.ss p.UrlNotInSource ]))
              "negativeFixtures",
              Json.o
                  [ "total", Json.i negatives.Length
                    "withEventsFile", Json.i (negatives |> List.filter (fun n -> n.HasEvents) |> List.length)
                    "refusedByRegistryDecode", Json.i (negatives |> List.filter (fun n -> n.RefusedByRegistry) |> List.length)
                    "note", Json.s "Informative only. A fixture not refused here is refused by later reader stages (paths, digests, manifest, snapshot) or by admissibility (CONTRACT §11.3; OD-HC-03 A assigns that to the competition kernel)."
                    "fixtures", JArr(negatives |> List.map (fun n -> Json.o [ "name", Json.s n.Name; "expectedErrorCode", Json.s n.ExpectedCode; "expectedDomainCode", Json.opt Json.s n.ExpectedDomainCode; "refusedByRegistryDecode", Json.b n.RefusedByRegistry; "firstProblem", Json.opt Json.s n.FirstProblem ])) ] ]

    // ---- Verification matrix -----------------------------------------------------------------------------------------

    let matrix (pkg: ContractPackage) (checks: CheckResult list) (controls: ControlResult list) =
        let checkRows =
            checks |> List.map (fun c ->
                Json.o [ "obligationId", Json.s c.CheckId; "kind", Json.s "contract-validation"; "category", Json.s c.Category; "statement", Json.s c.Title; "executedBy", Json.s "tools/contract-readiness check"; "status", Json.s (outcomeText c.Outcome) ])
        let controlRows =
            controls |> List.map (fun r ->
                Json.o [ "obligationId", Json.s r.Control.ControlId; "kind", Json.s "negative-control"; "category", Json.s "negative-control"; "statement", Json.s (sprintf "%s must make %s fail" r.Control.Description (String.Join(", ", r.Control.MustFail))); "executedBy", Json.s "tools/contract-readiness check"; "status", Json.s (if List.isEmpty r.DidNotFail then "PASS" else "FAIL") ])
        let implRows =
            pkg.Commands
            |> List.collect (fun c -> c.Tests |> List.map (fun t -> c, t))
            |> List.filter (fun (_, t) -> t.StartsWith "T-IMPL")
            |> List.map (fun (c, t) ->
                Json.o [ "obligationId", Json.s (t.Split(' ').[0]); "kind", Json.s "implementation"; "category", Json.s "command"; "statement", Json.s t; "subject", Json.s c.CommandId; "executedBy", Json.s "competition implementation (Dokimos / domain tests)"; "status", Json.s (if List.isEmpty c.BlockedBy then "PENDING-IMPLEMENTATION" else "BLOCKED") ])
        let bindingRows =
            pkg.Bindings |> List.map (fun b ->
                Json.o [ "obligationId", Json.s ("UI-" + b.BindingId); "kind", Json.s "implementation"; "category", Json.s "ui-binding"; "statement", Json.s (sprintf "%s %s: %s -> %s; verified by %s" b.RequirementId b.Label (defaultArg b.CommandId "(display)") (String.Join(", ", b.WriteFields @ b.ReadBackFields)) (String.Join(", ", b.Tests))); "subject", Json.s b.RequirementId; "executedBy", Json.s "competition implementation; Praxis manifest + Dokimos (catalog acceptance)"; "status", Json.s (if b.Status = "blocked" then "BLOCKED" elif b.Status = "not-applicable-v1.0.0" then "NOT-APPLICABLE" else "PENDING-IMPLEMENTATION") ])
        let all = checkRows @ controlRows @ implRows @ bindingRows
        let byStatus = all |> List.map (Json.fieldStr "status") |> List.countBy id |> List.sortBy fst
        Json.o
            [ "schema", Json.s "causa.contract-readiness.verification-matrix/1"
              "generatedBy", Json.s "tools/contract-readiness (dotnet run -- generate); do not edit by hand"
              "note", Json.s "contract-validation and negative-control rows were executed by this validator. implementation rows are obligations for the competition build: they are not results."
              "counts", Json.o ([ "obligations", Json.i all.Length ] @ (byStatus |> List.map (fun (k, c) -> k, Json.i c)))
              "obligations", JArr all ]
