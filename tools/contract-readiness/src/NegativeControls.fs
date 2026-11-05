namespace Causa.ContractReadiness

/// A deliberate defect. The suite is only trusted if each control makes exactly the checks it targets fail.
type NegativeControl =
    { ControlId: string
      Description: string
      Mutate: Inputs -> Inputs
      MustFail: string list }

type ControlResult =
    { Control: NegativeControl
      FailedAsExpected: string list
      DidNotFail: string list }

module NegativeControls =
    let private redecode (reg: Registry) (variants: EventVariant list) (e: EventRecord) (text: string) : EventRecord =
        let d = Codec.decodeText reg "Envelope" text
        let et = Codec.at "eventType" d.Value |> Codec.asString |> Option.defaultValue "?"
        let payload = Codec.at "payload" d.Value |> Option.defaultValue TNull
        { e with Text = text; Decoded = d; EventType = et; Variant = Package.variantOfEvent variants et payload }

    /// Rewrites line `line` of the first history whose directory ends with `dirSuffix`.
    let private mutateLine (dirSuffix: string) (line: int) (rewrite: string -> string) (inputs: Inputs) : Inputs =
        let load reg variants =
            inputs.LoadHistories reg variants
            |> List.map (fun h ->
                if h.Dir.EndsWith dirSuffix then
                    { h with Events = h.Events |> List.map (fun e -> if e.Line = line then redecode reg variants e (rewrite e.Text) else e) }
                else h)
        { inputs with LoadHistories = load }

    let private withPackage f (inputs: Inputs) = { inputs with Package = f inputs.Package }
    let private withRegistry f (inputs: Inputs) = { inputs with Registry = f inputs.Registry }

    let private removeCase (union: string) (case: string) (c: FsContract) =
        { c with Unions = c.Unions |> Map.change union (Option.map (List.filter (fun x -> x.Name <> case))) }

    let challenger = "1784da3c-7141-59ee-a495-1d5d44bf2284"

    let all : NegativeControl list =
        [ { ControlId = "NC-01"
            Description = "Remove a required field (EvidenceAdmitted.source) from the field registry"
            Mutate = withRegistry (fun r -> { r with Fields = r.Fields |> List.filter (fun f -> f.Def.FieldId <> "F.Payload.EvidenceAdmitted.source") })
            MustFail = [ "CR-HIST-01" ] }
          { ControlId = "NC-02"
            Description = "Remove an event case binding (variant EV-033 CausalAssertionConfirmed)"
            Mutate = withPackage (fun p -> { p with Variants = p.Variants |> List.filter (fun v -> v.VariantId <> "EV-033") })
            MustFail = [ "CR-HIST-02"; "CR-CER-02"; "CR-CER-03" ] }
          { ControlId = "NC-03"
            Description = "Remove CoreEvent.EvidenceVerificationAssessed from the parsed v4.1.5 contract"
            Mutate = fun i -> { i with V415 = removeCase "CoreEvent" "EvidenceVerificationAssessed" i.V415 }
            MustFail = [ "CR-REP-01"; "CR-CER-03" ] }
          { ControlId = "NC-04"
            Description = "Remove a command (CMD-REL-LINK) that UI bindings use"
            Mutate = withPackage (fun p -> { p with Commands = p.Commands |> List.filter (fun c -> c.CommandId <> "CMD-REL-LINK") })
            MustFail = [ "CR-UI-02" ] }
          { ControlId = "NC-05"
            Description = "Remove the binding of a required UI field (UB-024, INV-024 observation text)"
            Mutate = withPackage (fun p -> { p with Bindings = p.Bindings |> List.filter (fun b -> b.BindingId <> "UB-024") })
            MustFail = [ "CR-UI-01"; "CR-UI-04" ] }
          { ControlId = "NC-06"
            Description = "Duplicate a field ID"
            Mutate = withRegistry (fun r -> { r with Fields = r.Fields @ [ List.head r.Fields ] })
            MustFail = [ "CR-REG-02" ] }
          { ControlId = "NC-07"
            Description = "Make one stored Challenger event non-canonical (insert a space)"
            Mutate = mutateLine challenger 5 (fun t -> t.Replace("{\"actor\":", "{ \"actor\":"))
            MustFail = [ "CR-HIST-01" ] }
          { ControlId = "NC-08"
            Description = "Drop Provenance from the parsed v4.1.5 EventEnvelope record"
            Mutate = fun i -> { i with V415 = { i.V415 with Records = i.V415.Records |> Map.change "EventEnvelope" (Option.map (List.filter (fun (k, _) -> k <> "Provenance"))) } }
            MustFail = [ "CR-REP-01" ] }
          { ControlId = "NC-09"
            Description = "Claim durability for 'Provider write acknowledged' (no read-back)"
            Mutate = withPackage (fun p -> { p with Durability = p.Durability |> List.map (fun d -> if d.StateId = "PS-ACK" then { d with Durable = true; MayDisplaySaved = true } else d) })
            MustFail = [ "CR-ST-03" ] }
          { ControlId = "NC-10"
            Description = "Narrow a transition table entry (DT-HYP-02 markSupported only from proposed)"
            Mutate = withPackage (fun p -> { p with Machines = p.Machines |> List.map (fun m -> { m with Transitions = m.Transitions |> List.map (fun t -> if t.TransitionId = "DT-HYP-02" then { t with From = [ "proposed" ] } else t) }) })
            MustFail = [ "CR-HIST-06" ] }
          { ControlId = "NC-11"
            Description = "Mark a blocked binding (UB-023 primary question) as covered"
            Mutate = withPackage (fun p -> { p with Bindings = p.Bindings |> List.map (fun b -> if b.BindingId = "UB-023" then { b with Status = "covered-v4.1.5"; BlockedBy = [] } else b) })
            MustFail = [ "CR-UI-03" ] }
          { ControlId = "NC-12"
            Description = "Remove SaveState.Conflict from the parsed v4.1.5 contract"
            Mutate = fun i -> { i with V415 = removeCase "SaveState" "Conflict" i.V415 }
            MustFail = [ "CR-ST-02"; "CR-ST-03" ] }
          { ControlId = "NC-13"
            Description = "Add an unknown payload field to one stored event"
            Mutate = mutateLine challenger 2 (fun t -> t.Replace("\"payload\":{", "\"payload\":{\"aaUnknown\":1,"))
            MustFail = [ "CR-HIST-01" ] }
          { ControlId = "NC-14"
            Description = "Give one stored event an unregistered event type"
            Mutate = mutateLine challenger 3 (fun t -> System.Text.RegularExpressions.Regex.Replace(t, "\"eventType\":\"[A-Za-z]+\"", "\"eventType\":\"ObservationMutated\""))
            MustFail = [ "CR-HIST-01"; "CR-HIST-02" ] }
          { ControlId = "NC-15"
            Description = "Tamper with the committed FIELD-REGISTRY.json"
            Mutate = fun i -> { i with CommittedRegistryText = i.CommittedRegistryText.Replace("\"uuid\"", "\"uuid \"") }
            MustFail = [ "CR-REG-01" ] }
          { ControlId = "NC-16"
            Description = "Place an access token in a contract fixture"
            Mutate = fun i -> { i with PackageTexts = i.PackageTexts @ [ "fixture.json", "{\"token\":\"ghp_" + System.String('A', 36) + "\"}" ] }
            MustFail = [ "CR-SEC-01" ] }
          { ControlId = "NC-17"
            Description = "Remove the documented input path of a required field (ObservationRecorded.observedAt)"
            Mutate = withPackage (fun p -> { p with Commands = p.Commands |> List.map (fun c -> if c.CommandId = "CMD-OBS-ADD" then { c with Inputs = c.Inputs |> List.filter (fun x -> x.FieldId <> "F.Payload.ObservationRecorded.observedAt") } else c) })
            MustFail = [ "CR-CER-06" ] }
          { ControlId = "NC-18"
            Description = "Remove a v4.1.5 CoreCommand case's registry entries (RecordFinding)"
            Mutate = withPackage (fun p -> { p with Commands = p.Commands |> List.filter (fun c -> c.ContractCase <> Some "RecordFinding") })
            MustFail = [ "CR-CER-04" ] } ]

    let run (baseInputs: Inputs) : ControlResult list =
        all
        |> List.map (fun c ->
            let results, _ = Checks.run (c.Mutate baseInputs)
            let failed = results |> List.filter (fun r -> match r.Outcome with Fail _ -> true | _ -> false) |> List.map (fun r -> r.CheckId) |> Set.ofList
            { Control = c
              FailedAsExpected = c.MustFail |> List.filter failed.Contains
              DidNotFail = c.MustFail |> List.filter (failed.Contains >> not) })
