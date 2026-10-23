#!/usr/bin/env python3
"""Generate the supplementary Causa v1.0.0 admissibility fixtures (closes review finding F-09).

Every fixture is built deliberately from a frozen valid history of causa-data@causa-storage-v1.0.0: a prefix of it
(always cut at a command-group boundary, so the prefix stays admissible) followed by new command groups, almost
always appended at the end.  Files are canonical so that CONTRACT stages 1-14 pass (RFC 8785 lines, versions 1..N,
re-derived eventIds, a fresh correlationId per new group, consistent manifest).

* negative fixtures keep the frozen snapshot (stale on purpose): an inadmissible history stops at stage 15.
* valid histories get the snapshot and manifest of the clean-room consumer's projection, so they pass all 17 stages.

Every spec states the complete ordered stage-15 error list it must produce, as (history code, inner rule code) pairs.
`check` runs the pinned engine oracle and the clean-room consumer and fails on any difference.

Usage:
  python3 -I supplementary.py generate --causa-data <checkout at causa-storage-v1.0.0>
  python3 -I supplementary.py check    --causa-data <root> --oracle <oracle binary> [--json <report>]

`generate` is deterministic: it rewrites requirements/causa/data/v1.0.0/acceptance/supplementary/{negative,valid,
MATRIX.json} byte-identically from the frozen dataset.  README.md in that directory is maintained by hand.
"""
from __future__ import annotations

import base64
import datetime as dt
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path
from typing import Any, Callable, Iterable, NamedTuple

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE.parent / "clean-room-consumer"))

from acceptance import jcs  # noqa: E402
from mutate import groups, renumber  # noqa: E402

REQ = HERE.parents[2] / "requirements" / "causa" / "data" / "v1.0.0"
OUT = REQ / "acceptance" / "supplementary"
CONSUMER = HERE.parent / "clean-room-consumer" / "causa_clean.py"
GENERATOR = "tools/causa-contract/differential/supplementary.py"

SUP_NS = uuid.uuid5(uuid.NAMESPACE_URL, "https://github.com/kemiller2002/indy-init/causa/v1.0.0/supplementary-fixtures")
HUMAN = {"actorId": "investigator-sup", "kind": "human"}
HUMAN_B = {"actorId": "investigator-sup-b", "kind": "human"}
SYSTEM = {"actorId": "pipeline-sup", "kind": "system"}
AGENT = {"actorId": "assistant-sup", "kind": "agent", "provider": "synthetic-provider", "model": "synthetic-model-1"}
A21 = "A verification failed; work reopens (A21)."
POLICY = "core-default/1"

Events = tuple[dict, ...]

# frozen investigations (directory = investigationId)
PAYMENT = "6fd2ddc9-bedd-5fff-9093-5506c4d68cb3"      # payment-failure, active, v26
AUTH = "a6747bd3-2794-5ac7-83b8-5ea2a0c50f5e"         # authentication-outage, resolved, v59
METRICS = "abd5f9b5-e335-5245-b951-802f01d5a2e5"      # conflicting-metrics, active, v28
OUTAGE = "c57e4407-dd43-5d63-8dbe-9d6ceac6c31a"       # intermittent-outage, awaitingEvidence, v37
LATENCY = "d588748e-8df9-5f27-859e-5d36d9a2b53d"      # api-latency, awaitingVerification, v34

# entity ids of the frozen histories used below
P_ACE, P_240, P_99A, P_F87 = ("ace16a94-6c8c-558f-b07e-e5265ea67f24", "2406873b-ee64-5b7e-809a-577d6a37f9f9",
                              "99a15892-43af-54ac-a983-7873549055f6", "f870176d-9773-59c9-a89c-cf6195f16092")
P_EV_A12, P_EV_457, P_EV_DD2 = ("a12c7a52-10b8-54a6-924a-0da043b751f6", "457a14a6-8e60-5878-a712-69a75f39aeb7",
                                "dd2998ed-9b82-5a0e-bdee-91bb509ed5fa")
P_UNK, P_ASM, P_ACT = ("871e3d69-66d8-50d4-981e-0771c2c8c677", "97035465-4115-56ff-903f-15ea58263805",
                       "a608465a-d93d-5823-84ca-8768159d7310")
P_PROP_REJECTED = "b77aef24-e1ad-5a96-9bd0-7cc18170a8f4"
A_HYP, A_CAUSE = "0e23038c-c01f-5d20-8963-9d47c612544b", "78673ccb-fd64-52be-ada3-b9be627bf83c"
M_CONTRA, M_ASM_REJ, M_CAUSE = ("3ed5b475-47e4-527f-bcb3-8afea96aacd5", "3314fef9-7236-5f5e-8d38-0df225c2d47c",
                                "ea6f3f1c-49cc-58e5-a286-21dcd33de2e2")
O_HYP_FALSIFIED, O_HYP_PLAUSIBLE = "76d72740-174a-50a0-834f-7fbda9ef5230", "21e1fd03-0373-5deb-aac8-6616b93f3118"
O_UNK_INV, O_ACT_DONE, O_ACT_REJ = ("77f4b2ed-5eee-5e32-b925-9475d8c959be", "bbe734e0-1652-5214-9392-dee51ac2ed30",
                                    "99e3f809-8c7c-55db-bf88-12d22fb3b49b")
O_NK_STATEMENT = "DNS resolution failure is not the cause of the 502 bursts."
O_FALSIFIED_STATEMENT = "DNS resolution for the upstream inventory service intermittently fails."
L_ROOT, L_CONTRIB, L_SUPERSEDED = ("0ac287b7-5cc2-530a-a6b5-4c3e04176619", "8ba9eb14-1af3-5d26-ad46-a7bed8dd27f0",
                                   "e970897d-afec-53bb-878b-d14b44ed97e8")
L_EV_ROOT, L_EV_CONTRIB = "6f8b636d-a646-5e67-8514-a1473a1d9f95", "7fdcc246-7e9c-5f34-98f1-f67b570808a4"
L_CAUSE, L_CA, L_CA_MITIGATION = ("d2c375a6-5fcc-5d43-baa7-bee0b60ba90b", "c1320907-89f7-5bd4-921b-e2eba812356e",
                                  "4585500d-4441-540f-8e54-2bf57c65f052")
L_CRIT_1, L_CRIT_2 = "438ec188-ec4b-5215-bc89-2574925f4e51", "5094710b-f862-5bd6-9cf3-d94ce5415d94"


# ====================================================================================================================
# Building blocks (pure)
# ====================================================================================================================

class Ev(NamedTuple):
    """An event of a new command group, before envelope, version and id are assigned."""
    eventType: str
    payload: dict
    actor: dict = HUMAN


def uid(tag: str, label: str) -> str:
    return str(uuid.uuid5(SUP_NS, f"{tag}/{label}"))


def load_events(dataset: Path, inv: str) -> Events:
    path = dataset / "fixtures" / "v1.0.0" / "investigations" / inv / "events.ndjson"
    data = path.read_text("utf-8")
    assert data.endswith("\n")
    return tuple(json.loads(line) for line in data[:-1].split("\n"))   # LF only: text may hold raw U+0085/U+2028/U+2029


def prefix(events: Events, n: int) -> Events:
    """The first n events; n must end a command group so the prefix is the persisted state after that group."""
    assert n == len(events) or events[n]["correlationId"] != events[n - 1]["correlationId"], f"v{n} splits a group"
    return events[:n]


def without_group_at(events: Events, version: int) -> Events:
    cid = events[version - 1]["correlationId"]
    return tuple(e for e in events if e["correlationId"] != cid)


def at(text: str, minutes: int) -> str:
    t = dt.datetime.strptime(text, "%Y-%m-%dT%H:%M:%S.%fZ") + dt.timedelta(minutes=minutes)
    return t.strftime("%Y-%m-%dT%H:%M:%S.") + f"{t.microsecond // 1000:03d}Z"


def envelope(e: Ev, investigation_id: str, correlation_id: str, occurred_at: str) -> dict:
    return {"actor": e.actor, "correlationId": correlation_id, "eventId": "", "eventType": e.eventType,
            "formatVersion": "1.0.0", "investigationId": investigation_id, "aggregateVersion": 0,
            "occurredAt": occurred_at, "payload": e.payload, "semanticVersion": "1.0.0"}


def extend(base: Events, tag: str, *new_groups: Iterable[Ev]) -> Events:
    """Append command groups: fresh correlationId per group, occurredAt 2 minutes apart, contiguous versions."""
    iid, last = base[0]["investigationId"], base[-1]["occurredAt"]
    new = tuple(envelope(e, iid, uid(tag, f"group-{i}"), at(last, 2 * (i + 1)))
                for i, g in enumerate(new_groups) for e in g)
    return renumber(base + new)


def into_group(base: Events, version: int, e: Ev) -> Events:
    """Add an event at the end of the command group that contains `version` (same correlationId, time and actor)."""
    cid = base[version - 1]["correlationId"]
    end = max(i for i, x in enumerate(base) if x["correlationId"] == cid)
    host = base[end]
    extra = {**envelope(e, host["investigationId"], cid, host["occurredAt"]), "actor": host["actor"]}
    return renumber(base[: end + 1] + (extra,) + base[end + 1:])


# --- event constructors (wire payloads, CONTRACT §7) ---------------------------------------------------------------

def g(*events: Ev) -> tuple[Ev, ...]:
    return events


def lifecycle(command: str, frm: str, to: str, reason: str | None = None, actor: dict = HUMAN) -> Ev:
    return Ev("LifecycleChanged", {"command": command, "from": frm, "reason": reason, "to": to}, actor)


def started(observation_id: str, original: str, title: str) -> Ev:
    return Ev("InvestigationStarted", {"observationId": observation_id, "original": original, "title": title})


def observation(oid: str, original: str, observed_at: str | None = None) -> Ev:
    return Ev("ObservationRecorded", {"observationId": oid, "observedAt": observed_at, "original": original})


def hypothesis(hid: str, statement: str, mechanism: str | None = None) -> Ev:
    return Ev("HypothesisRecorded", {"hypothesisId": hid, "mechanism": mechanism, "statement": statement})


HYP_TARGET = {"markPlausible": "plausible", "markSupported": "supported", "markWeakened": "weakened",
              "falsify": "falsified", "supersede": "superseded", "reopen": "plausible"}


def hyp_transition(hid: str, command: str, frm: str, evidence: list[str], reason: str, to: str | None = None) -> Ev:
    return Ev("HypothesisTransitioned", {"command": command, "evidenceIds": evidence, "from": frm,
                                         "hypothesisId": hid, "reason": reason, "to": to or HYP_TARGET[command]})


def evidence(eid: str, content: str, kind: str = "text", category: str = "measurement",
             source: str = "supplementary fixture (synthetic)", captured_at: str | None = None) -> Ev:
    return Ev("EvidenceAdmitted", {"capturedAt": captured_at, "category": category, "content": content,
                                   "contentKind": kind, "evidenceId": eid, "source": source})


def superseded(old: str, new: str, reason: str) -> Ev:
    return Ev("EvidenceSuperseded", {"evidenceId": old, "reason": reason, "replacementEvidenceId": new})


def assessed(eid: str, verification: str) -> Ev:
    return Ev("EvidenceVerificationAssessed", {"evidenceId": eid, "verification": verification})


def related(rid: str, eid: str, hid: str, kind: str, rationale: str | None = None) -> Ev:
    return Ev("EvidenceRelated", {"evidenceId": eid, "hypothesisId": hid, "kind": kind, "rationale": rationale,
                                  "relationshipId": rid})


def unknown(uid_: str, question: str, materiality: str) -> Ev:
    return Ev("UnknownIdentified", {"materiality": materiality, "question": question, "unknownId": uid_})


def unknown_to(uid_: str, state: str, basis: str) -> Ev:
    return Ev("UnknownTransitioned", {"basis": basis, "state": state, "unknownId": uid_})


def assumption(aid: str, proposition: str, materiality: str) -> Ev:
    return Ev("AssumptionRecorded", {"assumptionId": aid, "materiality": materiality, "proposition": proposition})


def assumption_to(aid: str, state: str) -> Ev:
    return Ev("AssumptionTransitioned", {"assumptionId": aid, "state": state})


def contradiction(cid: str, statement: str, evidence_ids: list[str], materiality: str) -> Ev:
    return Ev("ContradictionRecorded", {"contradictionId": cid, "evidenceIds": evidence_ids,
                                        "materiality": materiality, "statement": statement})


def contradiction_to(cid: str, state: str, basis: str) -> Ev:
    return Ev("ContradictionTransitioned", {"basis": basis, "contradictionId": cid, "state": state})


def prediction(pid: str, hid: str, statement: str, timing: str, strength: str = "expected") -> Ev:
    return Ev("PredictionRecorded", {"hypothesisId": hid, "predictionId": pid, "statement": statement,
                                     "strength": strength, "timing": timing})


def action(aid: str, purpose: str, targets: list[str], kind: str = "test") -> Ev:
    return Ev("ActionRecorded", {"actionId": aid, "kind": kind, "purpose": purpose, "targetHypothesisIds": targets})


def action_to(aid: str, state: str, actor: dict = HUMAN) -> Ev:
    return Ev("ActionTransitioned", {"actionId": aid, "state": state}, actor)


def finding(fid: str, aid: str, result: str, interpretation: str | None = None) -> Ev:
    return Ev("FindingRecorded", {"actionId": aid, "findingId": fid, "interpretation": interpretation,
                                  "originalResult": result})


def negative_knowledge(nid: str, statement: str, scope: str, basis: list[str]) -> Ev:
    return Ev("NegativeKnowledgeEstablished", {"basisEvidenceIds": basis, "negativeKnowledgeId": nid,
                                               "scope": scope, "statement": statement})


def cause(cid: str, outcome: str, members: list[tuple[str, str]], mechanism: str | None) -> Ev:
    return Ev("CausalAssertionRecorded", {"causeId": cid, "mechanism": mechanism, "outcome": outcome,
                                          "members": [{"hypothesisId": h, "role": r} for h, r in members]})


def confirmed(cid: str, policy: str = POLICY) -> Ev:
    return Ev("CausalAssertionConfirmed", {"causeId": cid, "policy": policy})


def cause_reopened(cid: str, reason: str) -> Ev:
    return Ev("CausalAssertionReopened", {"causeId": cid, "reason": reason})


def corrective(cid: str, kind: str, description: str, targets: list[str]) -> Ev:
    return Ev("CorrectiveActionRecorded", {"correctiveActionId": cid, "description": description, "kind": kind,
                                           "targetCauseIds": targets})


def corrective_to(cid: str, state: str) -> Ev:
    return Ev("CorrectiveActionTransitioned", {"correctiveActionId": cid, "state": state})


def criterion(cid: str, ca: str, expected: str, method: str | None = None) -> Ev:
    return Ev("CriterionRecorded", {"correctiveActionId": ca, "criterionId": cid, "expectedOutcome": expected,
                                    "method": method})


def verification(vid: str, crit: str, outcome: str, evidence_ids: list[str]) -> Ev:
    return Ev("VerificationRecorded", {"criterionId": crit, "evidenceIds": evidence_ids, "outcome": outcome,
                                       "verificationId": vid})


def conclusion(cid: str, statement: str, strength: str, causes: list[str], residual: str | None) -> Ev:
    return Ev("ConclusionRecorded", {"causeIds": causes, "conclusionId": cid, "residualUncertainty": residual,
                                     "statement": statement, "strength": strength})


def proposal(pid: str, kind: str, text: str, actor: dict = AGENT, rationale: str | None = None) -> Ev:
    return Ev("ProposalRecorded", {"kind": kind, "proposalId": pid, "rationale": rationale, "text": text}, actor)


def accepted(pid: str, target: str) -> Ev:
    return Ev("ProposalWasAccepted", {"proposalId": pid, "targetId": target})


def rejected(pid: str, reason: str) -> Ev:
    return Ev("ProposalWasRejected", {"proposalId": pid, "reason": reason})


# ====================================================================================================================
# Specifications
# ====================================================================================================================

I, C, R = "HISTORY.INADMISSIBLE_EVENT", "HISTORY.UNGATED_CONFIRMATION", "HISTORY.UNGATED_RESOLUTION"


class Spec(NamedTuple):
    name: str
    description: str
    covers: tuple[str, ...]                 # SEMANTICS references / rule codes this fixture isolates
    build: Callable[[Callable[[str], Events]], Events]
    errors: tuple[tuple[str, str | None], ...] = ()   # complete stage-15 list: (history code, inner rule code)


def neg(name, description, covers, build, *errors) -> Spec:
    return Spec(name, description, tuple(covers), build, tuple(errors))


def on(inv: str, upto: int | None = None) -> Callable[[Callable[[str], Events]], Events]:
    return lambda base: base(inv) if upto is None else prefix(base(inv), upto)


def appended(inv: str, upto: int | None, tag: str, *new_groups) -> Callable[[Callable[[str], Events]], Events]:
    return lambda base: extend(on(inv, upto)(base), tag, *new_groups)


def text_proposal_accepting(tag: str, kind: str, text: str, *target_group: Ev):
    """An admissible agent ProposalRecorded group followed by a (forged) acceptance group."""
    return (g(proposal(uid(tag, "proposal"), kind, text)), g(*target_group))


def negatives() -> tuple[Spec, ...]:
    t = "sup"   # namespace for ids; each fixture adds its own name

    def ids(name: str) -> Callable[[str], str]:
        return lambda label: uid(f"{t}/{name}", label)

    specs: list[Spec] = []
    add = specs.append

    # ---------------------------------------------------------------------------------------- §8 decision procedure
    n = "sup-start-group-reconstructs-finding"
    add(neg(n, "Group 1 is InvestigationStarted plus a FindingRecorded in the same command group: by the §15.3 "
               "precedence it reconstructs as RecordFinding, decided with no state (§8 step 2: INVESTIGATION.NOT_FOUND).",
            ("INVESTIGATION.NOT_FOUND", "§8.2", "§15.3.1"),
            lambda base, i=ids(n): into_group(base(LATENCY), 1,
                                              finding(i("finding"), i("action"), "A finding recorded before any state.")),
            (I, "INVESTIGATION.NOT_FOUND")))

    n = "sup-no-start-single-group"
    add(neg(n, "A one-group history whose only event is ObservationRecorded: the state is still empty, so the "
               "group has no start (§15.2 2.1: HISTORY.NO_START) and, being the last group, it is the only error.",
            ("HISTORY.NO_START", "§15.2 2.1"),
            lambda base, n=n: renumber((envelope(observation(uid(n, "observation"), "An observation without a start."),
                                                 PAYMENT, uid(n, "group-0"), base(PAYMENT)[0]["occurredAt"]),)),
            ("HISTORY.NO_START", None)))

    n = "sup-content-after-close"
    add(neg(n, "The investigation is closed (resolved -> closed is admissible); a later content command is refused "
               "(§8 step 6: INVESTIGATION.NOT_OPEN).",
            ("INVESTIGATION.NOT_OPEN", "§8.6", "§7.9"),
            appended(AUTH, None, n, g(lifecycle("closeInvestigation", "resolved", "closed")),
                     g(observation(ids(n)("observation"), "An observation recorded after closing."))),
            (I, "INVESTIGATION.NOT_OPEN")))

    n = "sup-confirm-after-resolution"
    add(neg(n, "A CausalAssertionConfirmed group on a resolved investigation: §8 step 6 refuses it, and the group "
               "code is the confirmation code (INVESTIGATION.NOT_OPEN -> HISTORY.UNGATED_CONFIRMATION).",
            ("INVESTIGATION.NOT_OPEN", "§8.6", "§15.4"),
            appended(AUTH, None, n, g(confirmed(A_CAUSE))),
            (C, "INVESTIGATION.NOT_OPEN")))

    n = "sup-confirm-in-draft"
    add(neg(n, "The investigation was never activated (lifecycle draft); every gate finding of the cause is clear, "
               "so the only blocker is the gate's lifecycle check (§11.3: INVESTIGATION.NOT_OPEN).",
            ("INVESTIGATION.NOT_OPEN", "§11.3"),
            lambda base, n=n: extend(prefix(without_group_at(base(LATENCY), 2), 19), n, g(confirmed(L_CAUSE))),
            (C, "INVESTIGATION.NOT_OPEN")))

    # ------------------------------------------------------------------------------------------- §1.6 generic checks
    n = "sup-accept-proposal-duplicate-target"
    i = ids(n)
    add(neg(n, "AcceptProposal of a hypothesis proposal whose targetId names an existing hypothesis: the target "
               "command RecordHypothesis is refused (§10.29.5: DUPLICATE_ID).",
            ("DUPLICATE_ID", "§10.29.5"),
            appended(PAYMENT, None, n, *text_proposal_accepting(
                n, "hypothesis", "Gateway connection pool exhaustion causes the declines.",
                hypothesis(P_ACE, "Gateway connection pool exhaustion causes the declines."),
                accepted(uid(n, "proposal"), P_ACE))),
            (I, "DUPLICATE_ID")))

    n = "sup-contradiction-dangling-evidence"
    add(neg(n, "RecordContradiction citing an evidence id that does not exist (§10.12 allExist: NOT_FOUND).",
            ("NOT_FOUND", "§10.12"),
            appended(PAYMENT, None, n, g(contradiction(ids(n)("contradiction"), "Two logs disagree.",
                                                       [P_EV_A12, ids(n)("missing-evidence")], "material"))),
            (I, "NOT_FOUND")))

    # ------------------------------------------------------------------------------------- §7 transition tables
    n = "sup-hypothesis-reopen-superseded"
    add(neg(n, "Reopen of a superseded hypothesis with evidence admitted after the last falsification (none): only "
               "the §7.1 table refuses (TRANSITION.ILLEGAL).",
            ("TRANSITION.ILLEGAL", "§7.1"),
            appended(LATENCY, None, n, g(hyp_transition(L_SUPERSEDED, "reopen", "superseded", [L_EV_ROOT],
                                                        "Reconsider the noisy-neighbour hypothesis."))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-unknown-start-investigating-twice"
    add(neg(n, "StartInvestigatingUnknown on an unknown already investigating (§7.2: TRANSITION.ILLEGAL).",
            ("TRANSITION.ILLEGAL", "§7.2"),
            appended(OUTAGE, None, n, g(unknown_to(O_UNK_INV, "investigating", "Still watching the rotation window."))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-contradiction-triage-twice"
    add(neg(n, "TriageContradiction on a triaged contradiction (§7.3: TRANSITION.ILLEGAL).",
            ("TRANSITION.ILLEGAL", "§7.3"),
            appended(METRICS, None, n, g(contradiction_to(M_CONTRA, "triaged", "Triaged again."))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-assumption-accept-rejected"
    add(neg(n, "ProvisionallyAcceptAssumption on a rejected assumption (§7.4: TRANSITION.ILLEGAL).",
            ("TRANSITION.ILLEGAL", "§7.4"),
            appended(METRICS, None, n, g(assumption_to(M_ASM_REJ, "provisionallyAccepted"))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-action-approve-rejected"
    add(neg(n, "ApproveAction on a rejected action (§7.5: TRANSITION.ILLEGAL).",
            ("TRANSITION.ILLEGAL", "§7.5"),
            appended(OUTAGE, None, n, g(action_to(O_ACT_REJ, "approved"))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-cause-reopen-proposed"
    add(neg(n, "ReopenCause on a cause that was never confirmed (§7.6, §10.23: TRANSITION.ILLEGAL, rule "
               "STATE-TRANSITION-TABLES §10).",
            ("TRANSITION.ILLEGAL", "§7.6", "§10.23"),
            appended(METRICS, None, n, g(cause_reopened(M_CAUSE, "Reassess the sampling cause."))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-correction-approve-cancelled"
    add(neg(n, "ApproveCorrection on a cancelled corrective action (§7.7: TRANSITION.ILLEGAL).",
            ("TRANSITION.ILLEGAL", "§7.7"),
            appended(LATENCY, None, n, g(corrective_to(L_CA_MITIGATION, "approved"))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-accept-rejected-proposal"
    add(neg(n, "AcceptProposal of a rejected proposal, with exactly the target events a decision would emit "
               "(§7.8, §10.29.2: PROPOSAL.NOT_ACTIVE).",
            ("PROPOSAL.NOT_ACTIVE", "§7.8", "§10.29.2"),
            appended(PAYMENT, None, n, g(action(ids(n)("action"), "Roll back fraud rule FR-118 immediately.", []),
                                         accepted(P_PROP_REJECTED, ids(n)("action")))),
            (I, "PROPOSAL.NOT_ACTIVE")))

    n = "sup-lifecycle-activate-active"
    add(neg(n, "ActivateInvestigation while active (§7.9, §9.1: TRANSITION.ILLEGAL).",
            ("TRANSITION.ILLEGAL", "§7.9", "§9.1"),
            appended(PAYMENT, None, n, g(lifecycle("activateInvestigation", "active", "active"))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-lifecycle-close-active"
    add(neg(n, "CloseInvestigation while active (§7.9: TRANSITION.ILLEGAL).",
            ("TRANSITION.ILLEGAL", "§7.9"),
            appended(PAYMENT, None, n, g(lifecycle("closeInvestigation", "active", "closed"))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-lifecycle-reopen-active"
    add(neg(n, "ReopenInvestigation while active (§7.9: TRANSITION.ILLEGAL).",
            ("TRANSITION.ILLEGAL", "§7.9"),
            appended(PAYMENT, None, n, g(lifecycle("reopenInvestigation", "active", "active",
                                                   "Reopen requested while still active."))),
            (I, "TRANSITION.ILLEGAL")))

    n = "sup-resolve-from-awaiting-evidence"
    add(neg(n, "ResolveInvestigation from awaitingEvidence: the §7.9 table refuses before any resolution blocker is "
               "evaluated; the group code is the resolution code (TRANSITION.ILLEGAL -> HISTORY.UNGATED_RESOLUTION).",
            ("TRANSITION.ILLEGAL", "§7.9", "§9.1", "§15.4"),
            appended(OUTAGE, None, n, g(lifecycle("resolveInvestigation", "awaitingEvidence", "resolved"))),
            (R, "TRANSITION.ILLEGAL")))

    n = "sup-resolve-from-resolved"
    add(neg(n, "ResolveInvestigation on a resolved investigation (§7.9: TRANSITION.ILLEGAL -> "
               "HISTORY.UNGATED_RESOLUTION; lifecycle commands skip the open check).",
            ("TRANSITION.ILLEGAL", "§7.9", "§8.5"),
            appended(AUTH, None, n, g(lifecycle("resolveInvestigation", "resolved", "resolved"))),
            (R, "TRANSITION.ILLEGAL")))

    # ----------------------------------------------------------------------------------------------- §10.4 evidence
    n = "sup-artifact-url-port-out-of-range"
    add(neg(n, "Artifact URL inside the §10.4.2 profile with port 70000 > 65535: ill-formed (EVIDENCE.URL).",
            ("EVIDENCE.URL", "§10.4.2"),
            appended(PAYMENT, None, n, g(evidence(ids(n)("evidence"), "https://example.invalid:70000/runbook",
                                                  kind="artifact", category="document"))),
            (I, "EVIDENCE.URL")))

    n = "sup-artifact-url-bad-percent-escape"
    add(neg(n, "Artifact URL inside the §10.4.2 profile whose path has '%' not followed by two hex digits: "
               "ill-formed (EVIDENCE.URL).",
            ("EVIDENCE.URL", "§10.4.2"),
            appended(PAYMENT, None, n, g(evidence(ids(n)("evidence"), "https://example.invalid/report%zz.html",
                                                  kind="artifact", category="document"))),
            (I, "EVIDENCE.URL")))

    # ------------------------------------------------------------------------------------------------ §10.20 finding
    n = "sup-finding-for-proposed-action"
    add(neg(n, "RecordFinding for an action that is still proposed (§10.20: ACTION.NOT_EXECUTABLE).",
            ("ACTION.NOT_EXECUTABLE", "§10.20"),
            appended(PAYMENT, None, n, g(action(ids(n)("action"), "Inspect the gateway pool metrics.", [P_ACE])),
                     g(finding(ids(n)("finding"), ids(n)("action"), "Pool saturated at 09:41."))),
            (I, "ACTION.NOT_EXECUTABLE")))

    # ------------------------------------------------------------------------------------------- §12 verification
    n = "sup-verification-not-awaiting"
    i = ids(n)
    add(neg(n, "A criterion for a cancelled corrective action is recorded (admissible), then a verification of it "
               "(§12.2.3: VERIFICATION.NOT_AWAITING).",
            ("VERIFICATION.NOT_AWAITING", "§12.2.3"),
            appended(LATENCY, None, n,
                     g(criterion(i("criterion"), L_CA_MITIGATION, "Cache hit rate above 90%.")),
                     g(verification(i("verification"), i("criterion"), "failed", []))),
            (I, "VERIFICATION.NOT_AWAITING")))

    n = "sup-verification-evidence-superseded"
    i = ids(n)
    add(neg(n, "A satisfied verification cites evidence admitted after the round started but superseded before the "
               "verification (§12.2.5 freshness: VERIFICATION.EVIDENCE_PREDATES_ROUND).",
            ("VERIFICATION.EVIDENCE_PREDATES_ROUND", "§12.2.5", "§4.4 fresh"),
            appended(LATENCY, None, n,
                     g(evidence(i("e1"), "p95 158 ms over 24 hours (first export).")),
                     g(evidence(i("e2"), "p95 161 ms over 24 hours (corrected export).")),
                     g(superseded(i("e1"), i("e2"), "The first export missed one region.")),
                     g(verification(i("verification"), L_CRIT_1, "satisfied", [i("e1")]))),
            (I, "VERIFICATION.EVIDENCE_PREDATES_ROUND")))

    # ---------------------------------------------------------------------------------------------- §11 cause gate
    n = "sup-cause-not-found"
    add(neg(n, "CausalAssertionConfirmed naming a cause that does not exist (§11.1: CAUSE.NOT_FOUND).",
            ("CAUSE.NOT_FOUND", "§11.1"),
            appended(LATENCY, None, n, g(confirmed(ids(n)("cause")))),
            (C, "CAUSE.NOT_FOUND")))

    n = "sup-cause-member-not-supported"
    i = ids(n)
    add(neg(n, "A member hypothesis is weakened (it keeps its active supporting evidence) before the cause is "
               "confirmed (§11.4.2: CAUSE.MEMBER_NOT_SUPPORTED).",
            ("CAUSE.MEMBER_NOT_SUPPORTED", "§11.4.2"),
            appended(LATENCY, 20, n,
                     g(evidence(i("evidence"), "Cache hit rate unchanged after the TTL change.")),
                     g(hyp_transition(L_CONTRIB, "markWeakened", "supported", [i("evidence")],
                                      "Hit rate did not move with the TTL.")),
                     g(confirmed(L_CAUSE))),
            (C, "CAUSE.MEMBER_NOT_SUPPORTED")))

    n = "sup-cause-no-supporting-evidence"
    i = ids(n)
    add(neg(n, "The only supporting evidence of a supported member is superseded, so no active supporting evidence "
               "remains (§11.4.2: CAUSE.NO_SUPPORTING_EVIDENCE).",
            ("CAUSE.NO_SUPPORTING_EVIDENCE", "§11.4.2", "§4.4 active"),
            appended(LATENCY, 20, n,
                     g(evidence(i("evidence"), "Configuration diff re-exported with full context.", category="document")),
                     g(superseded(L_EV_CONTRIB, i("evidence"), "The first diff export was truncated.")),
                     g(confirmed(L_CAUSE))),
            (C, "CAUSE.NO_SUPPORTING_EVIDENCE")))

    n = "sup-cause-contradicting-evidence"
    i = ids(n)
    add(neg(n, "Active evidence contradicts a member hypothesis and is not addressed by a resolved or accepted "
               "contradiction (§11.4.2: CAUSE.CONTRADICTING_EVIDENCE).",
            ("CAUSE.CONTRADICTING_EVIDENCE", "§11.4.2"),
            appended(LATENCY, 20, n,
                     g(evidence(i("evidence"), "Latency rose on hosts whose cache TTL was not changed.")),
                     g(related(i("relationship"), i("evidence"), L_CONTRIB, "contradicts")),
                     g(confirmed(L_CAUSE))),
            (C, "CAUSE.CONTRADICTING_EVIDENCE")))

    n = "sup-cause-member-missing"
    i = ids(n)
    add(neg(n, "Not reachable alone (§15.9.4): a cause naming a missing hypothesis is first refused at recording "
               "(NOT_FOUND), and its later confirmation reports CAUSE.MEMBER_MISSING (§11.4.2) as the only gate "
               "finding. Minimal co-firing set.",
            ("CAUSE.MEMBER_MISSING", "§11.4.2", "§15.9.4"),
            appended(LATENCY, 19, n,
                     g(cause(i("cause"), "Search API p95 latency regression",
                             [(L_ROOT, "rootCause"), (L_CONTRIB, "contributingFactor"),
                              (i("missing-hypothesis"), "contributingFactor")], "N+1 queries.")),
                     g(confirmed(i("cause")))),
            (I, "NOT_FOUND"), (C, "CAUSE.MEMBER_MISSING")))

    # --------------------------------------------------------------------------------------- §14 resolution gate
    n = "sup-resolve-conclusion-stale"
    i = ids(n)
    add(neg(n, "The latest conclusion (confirmedCause) cites a confirmed cause and a second, unconfirmed cause; "
               "every other resolution condition holds (§14.7: RESOLVE.CONCLUSION_STALE).",
            ("RESOLVE.CONCLUSION_STALE", "§14.7", "§13.2"),
            appended(AUTH, 55, n,
                     g(cause(i("cause"), "Single sign-on outage (redirect handling)",
                             [(A_HYP, "rootCause")], "The refresh job ignored HTTP 308.")),
                     g(conclusion(i("conclusion"), "The outage was caused by the stale key cache and the redirect "
                                  "handling of the refresh job.", "confirmedCause", [A_CAUSE, i("cause")],
                                  "Redirect handling is asserted but not separately confirmed.")),
                     g(lifecycle("resolveInvestigation", "awaitingVerification", "resolved"))),
            (R, "RESOLVE.CONCLUSION_STALE")))

    n = "sup-resolve-no-confirmed-cause"
    add(neg(n, "Not reachable alone (§14 note): with no confirmed cause there is no correction either. A conclusion "
               "exists, so the minimal set is RESOLVE.NO_CONFIRMED_CAUSE + RESOLVE.NO_CORRECTION.",
            ("RESOLVE.NO_CONFIRMED_CAUSE", "§14.1", "§14.3"),
            appended(PAYMENT, None, n,
                     g(conclusion(ids(n)("conclusion"), "Gateway timeouts are the leading explanation.",
                                  "leadingExplanation", [], "Client retry behaviour is still open.")),
                     g(lifecycle("resolveInvestigation", "active", "resolved"))),
            (R, "RESOLVE.NO_CONFIRMED_CAUSE"), (R, "RESOLVE.NO_CORRECTION")))

    n = "sup-resolve-blocking-unknown"
    add(neg(n, "Not reachable alone (§14 note): a blocking open unknown is also a material open unknown, so the "
               "confirmed cause no longer holds. Minimal set: RESOLVE.CAUSE_NO_LONGER_HOLDS (§15.5) + "
               "RESOLVE.BLOCKING_UNKNOWN.",
            ("RESOLVE.BLOCKING_UNKNOWN", "§14.5", "§14.2"),
            appended(AUTH, 55, n,
                     g(unknown(ids(n)("unknown"), "Did any tenant keep a pinned copy of the old key?", "blocking")),
                     g(lifecycle("resolveInvestigation", "awaitingVerification", "resolved"))),
            (R, "RESOLVE.CAUSE_NO_LONGER_HOLDS"), (R, "RESOLVE.BLOCKING_UNKNOWN")))

    # ------------------------------------------------------------------------------- §10.27 repetition refusal
    n = "sup-agent-repeats-negative-knowledge"
    add(neg(n, "An agent hypothesis proposal whose text equals a negative-knowledge statement after trim and "
               "invariant lower-casing (ASCII only) (§10.27, §3.3: PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE).",
            ("PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE", "§10.27", "§3.3"),
            appended(OUTAGE, None, n, g(proposal(ids(n)("proposal"), "hypothesis",
                                                 "  \t" + O_NK_STATEMENT.upper() + " \t "))),
            (I, "PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE")))

    n = "sup-agent-repeats-falsified-normalised"
    add(neg(n, "An agent hypothesis proposal whose text equals a falsified hypothesis statement after trim and "
               "invariant lower-casing (mixed case, surrounding spaces; ASCII only) (§10.27).",
            ("PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE", "§10.27", "§3.3"),
            appended(OUTAGE, None, n, g(proposal(ids(n)("proposal"), "hypothesis",
                                                 " " + O_FALSIFIED_STATEMENT.swapcase() + "   "))),
            (I, "PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE")))

    # ----------------------------------------------------------------- §15.6 decided fields (accepted, but forged)
    n = "sup-forged-lifecycle-from"
    add(neg(n, "ActivateInvestigation from awaitingEvidence recorded with from = active: the decision accepts but "
               "emits from = awaitingEvidence (§15.6 decided LifecycleChanged.from).",
            ("§15.6 LifecycleChanged.from",),
            appended(OUTAGE, None, n, g(lifecycle("activateInvestigation", "active", "active"))),
            (I, None)))

    n = "sup-forged-resolution-from"
    add(neg(n, "An otherwise admissible resolution recorded with from = active (state awaitingVerification): an "
               "accepted-but-different resolution is HISTORY.INADMISSIBLE_EVENT, not UNGATED_RESOLUTION (§15.4, §15.6).",
            ("§15.6 LifecycleChanged.from", "§15.4"),
            appended(AUTH, 55, n, g(lifecycle("resolveInvestigation", "active", "resolved"))),
            (I, None)))

    n = "sup-forged-hypothesis-from"
    add(neg(n, "markWeakened of a plausible hypothesis recorded with from = supported (§15.6 decided "
               "HypothesisTransitioned.from).",
            ("§15.6 HypothesisTransitioned.from",),
            appended(PAYMENT, None, n, g(hyp_transition(P_ACE, "markWeakened", "supported", [P_EV_A12],
                                                        "Timeouts do not explain all declines."))),
            (I, None)))

    n = "sup-forged-confirmation-policy"
    add(neg(n, "An otherwise admissible confirmation recorded with policy core-default/2 (§3.5, §15.6): accepted "
               "but different, so HISTORY.INADMISSIBLE_EVENT rather than UNGATED_CONFIRMATION (§15.4).",
            ("§15.6 CausalAssertionConfirmed.policy", "§3.5", "§15.4"),
            appended(LATENCY, 20, n, g(confirmed(L_CAUSE, "core-default/2"))),
            (I, None)))

    n = "sup-forged-a21-reason"
    i = ids(n)
    add(neg(n, "A failed verification while awaitingVerification whose LifecycleChanged follow-on carries a reason "
               "other than the exact A21 text (§12.3).",
            ("§12.3 A21 reason",),
            appended(LATENCY, None, n, g(verification(i("verification"), L_CRIT_1, "failed", []),
                                         corrective_to(L_CA, "verificationFailed"),
                                         lifecycle("verificationFailed", "awaitingVerification", "active",
                                                   "A verification failed; work reopens."))),
            (I, None)))

    n = "sup-forged-verification-lifecycle-missing"
    i = ids(n)
    add(neg(n, "A failed verification while awaitingVerification recorded without the LifecycleChanged follow-on "
               "(§12.3).",
            ("§12.3 follow-ons",),
            appended(LATENCY, None, n, g(verification(i("verification"), L_CRIT_1, "failed", []),
                                         corrective_to(L_CA, "verificationFailed"))),
            (I, None)))

    n = "sup-forged-verification-lifecycle-extra"
    i = ids(n)
    add(neg(n, "A failed verification while the investigation is active, recorded with a LifecycleChanged "
               "follow-on that only applies in awaitingVerification (§12.3).",
            ("§12.3 follow-ons",),
            appended(LATENCY, None, n, g(lifecycle("activateInvestigation", "awaitingVerification", "active")),
                     g(verification(i("verification"), L_CRIT_1, "failed", []),
                       corrective_to(L_CA, "verificationFailed"),
                       lifecycle("verificationFailed", "awaitingVerification", "active", A21))),
            (I, None)))

    n = "sup-forged-verification-correction-missing"
    i = ids(n)
    add(neg(n, "A failed verification recorded with the LifecycleChanged follow-on but without the "
               "CorrectiveActionTransitioned verificationFailed follow-on (§12.3).",
            ("§12.3 follow-ons",),
            appended(LATENCY, None, n, g(verification(i("verification"), L_CRIT_1, "failed", []),
                                         lifecycle("verificationFailed", "awaitingVerification", "active", A21))),
            (I, None)))

    n = "sup-forged-verified-effective-premature"
    i = ids(n)
    add(neg(n, "A satisfied verification of one of two criteria recorded with a verifiedEffective follow-on; the "
               "other criterion is not satisfied in this round (§12.3).",
            ("§12.3 follow-ons", "§4.4 allSatisfied"),
            appended(LATENCY, None, n, g(evidence(i("evidence"), "p95 171 ms over 24 hours after deployment.")),
                     g(verification(i("verification"), L_CRIT_1, "satisfied", [i("evidence")]),
                       corrective_to(L_CA, "verifiedEffective"))),
            (I, None)))

    n = "sup-forged-verified-effective-missing"
    i = ids(n)
    add(neg(n, "Both criteria satisfied with fresh evidence, but the second verification is recorded without its "
               "verifiedEffective follow-on (§12.3).",
            ("§12.3 follow-ons",),
            appended(LATENCY, None, n, g(evidence(i("evidence"), "p95 171 ms and 2 queries per request.")),
                     g(verification(i("v1"), L_CRIT_1, "satisfied", [i("evidence")])),
                     g(verification(i("v2"), L_CRIT_2, "satisfied", [i("evidence")]))),
            (I, None)))

    n = "sup-forged-inconclusive-follow-on"
    i = ids(n)
    add(neg(n, "An inconclusive verification recorded with a verificationFailed follow-on (§12.3: inconclusive has "
               "none).",
            ("§12.3 follow-ons",),
            appended(LATENCY, None, n, g(verification(i("verification"), L_CRIT_1, "inconclusive", []),
                                         corrective_to(L_CA, "verificationFailed"))),
            (I, None)))

    n = "sup-forged-finding-completion-missing"
    add(neg(n, "RecordFinding for an inProgress action recorded without its ActionTransitioned completed follow-on "
               "(§10.20, §15.6).",
            ("§10.20 RecordFinding shape",),
            appended(PAYMENT, None, n, g(finding(ids(n)("finding"), P_ACT, "Declines stopped on the test node."))),
            (I, None)))

    n = "sup-forged-finding-start-missing"
    i = ids(n)
    add(neg(n, "RecordFinding for an approved action recorded without the leading ActionTransitioned inProgress "
               "(§10.20, §15.6).",
            ("§10.20 RecordFinding shape",),
            appended(PAYMENT, None, n, g(action(i("action"), "Inspect retry counters on the client.", [P_F87])),
                     g(action_to(i("action"), "approved")),
                     g(finding(i("finding"), i("action"), "Retries tripled after 09:40."),
                       action_to(i("action"), "completed"))),
            (I, None)))

    n = "sup-forged-finding-extra-completion"
    add(neg(n, "A second finding for a completed action recorded with a redundant ActionTransitioned completed "
               "(§10.20: completed actions emit the finding only).",
            ("§10.20 RecordFinding shape",),
            appended(OUTAGE, None, n, g(finding(ids(n)("finding"), O_ACT_DONE, "Drain timeout is 0 s on edge-2."),
                                        action_to(O_ACT_DONE, "completed"))),
            (I, None)))

    n = "sup-forged-accept-hypothesis-mechanism"
    add(neg(n, "AcceptProposal (hypothesis) whose target HypothesisRecorded carries a mechanism; the decided target "
               "has mechanism null (§10.29.4, §15.6).",
            ("§10.29 AcceptProposal target shape",),
            appended(PAYMENT, None, n, *text_proposal_accepting(
                n, "hypothesis", "Gateway connection pool exhaustion causes the declines.",
                hypothesis(uid(n, "target"), "Gateway connection pool exhaustion causes the declines.",
                           "The pool is capped at 64 connections."),
                accepted(uid(n, "proposal"), uid(n, "target")))),
            (I, None)))

    n = "sup-forged-accept-hypothesis-statement"
    add(neg(n, "AcceptProposal (hypothesis) whose target statement differs from the proposal text (§10.29.4).",
            ("§10.29 AcceptProposal target shape",),
            appended(PAYMENT, None, n, *text_proposal_accepting(
                n, "hypothesis", "Gateway connection pool exhaustion causes the declines.",
                hypothesis(uid(n, "target"), "Gateway connection pool exhaustion causes most declines."),
                accepted(uid(n, "proposal"), uid(n, "target")))),
            (I, None)))

    n = "sup-forged-accept-unknown-materiality"
    add(neg(n, "AcceptProposal (unknown) whose target UnknownIdentified has materiality blocking; the decided "
               "target is material (§10.29.4).",
            ("§10.29 AcceptProposal target shape",),
            appended(PAYMENT, None, n, *text_proposal_accepting(
                n, "unknown", "Do other card networks show the same decline pattern?",
                unknown(uid(n, "target"), "Do other card networks show the same decline pattern?", "blocking"),
                accepted(uid(n, "proposal"), uid(n, "target")))),
            (I, None)))

    n = "sup-forged-accept-action-kind"
    add(neg(n, "AcceptProposal (action) whose target ActionRecorded has kind inspection; the decided kind is test "
               "(§10.29.4).",
            ("§10.29 AcceptProposal target shape",),
            appended(PAYMENT, None, n, *text_proposal_accepting(
                n, "action", "Compare decline rates per card network.",
                action(uid(n, "target"), "Compare decline rates per card network.", [], kind="inspection"),
                accepted(uid(n, "proposal"), uid(n, "target")))),
            (I, None)))

    n = "sup-forged-accept-action-targets"
    add(neg(n, "AcceptProposal (action) whose target ActionRecorded names target hypotheses; the decided target "
               "has targetHypothesisIds [] (§10.29.4).",
            ("§10.29 AcceptProposal target shape",),
            appended(PAYMENT, None, n, *text_proposal_accepting(
                n, "action", "Compare decline rates per card network.",
                action(uid(n, "target"), "Compare decline rates per card network.", [P_ACE]),
                accepted(uid(n, "proposal"), uid(n, "target")))),
            (I, None)))

    n = "sup-forged-accept-target-missing"
    add(neg(n, "AcceptProposal recorded as a lone ProposalWasAccepted, without the target command's event "
               "(§10.29.5, §15.6 group shape).",
            ("§10.29 AcceptProposal target shape",),
            appended(PAYMENT, None, n, *text_proposal_accepting(
                n, "hypothesis", "Gateway connection pool exhaustion causes the declines.",
                accepted(uid(n, "proposal"), uid(n, "target")))),
            (I, None)))

    n = "sup-forged-accept-target-id-mismatch"
    add(neg(n, "AcceptProposal whose target HypothesisRecorded id differs from ProposalWasAccepted.targetId "
               "(§10.29.4: the target command uses targetId).",
            ("§10.29 AcceptProposal target shape",),
            appended(PAYMENT, None, n, *text_proposal_accepting(
                n, "hypothesis", "Gateway connection pool exhaustion causes the declines.",
                hypothesis(uid(n, "other"), "Gateway connection pool exhaustion causes the declines."),
                accepted(uid(n, "proposal"), uid(n, "target")))),
            (I, None)))

    n = "sup-forged-accept-order-reversed"
    add(neg(n, "AcceptProposal recorded with ProposalWasAccepted before the target event (§15.6: order is "
               "compared).",
            ("§10.29 AcceptProposal target shape",),
            appended(PAYMENT, None, n, *text_proposal_accepting(
                n, "hypothesis", "Gateway connection pool exhaustion causes the declines.",
                accepted(uid(n, "proposal"), uid(n, "target")),
                hypothesis(uid(n, "target"), "Gateway connection pool exhaustion causes the declines."))),
            (I, None)))

    # ----------------------------------------------------------------------- §15.2 / §15.3 non-refusal branches
    n = "sup-unreconstructable-verification-failed"
    add(neg(n, "A lone LifecycleChanged verificationFailed group: no command emits a group starting with it "
               "(§15.3 'none').",
            ("§15.3 none",),
            appended(LATENCY, None, n, g(lifecycle("verificationFailed", "awaitingVerification", "active", A21))),
            (I, None)))

    n = "sup-unreconstructable-verified-effective"
    add(neg(n, "A lone CorrectiveActionTransitioned verifiedEffective group (§15.3 'none').",
            ("§15.3 none",),
            appended(LATENCY, None, n, g(corrective_to(L_CA, "verifiedEffective"))),
            (I, None)))

    n = "sup-unreconstructable-action-completed"
    add(neg(n, "A lone ActionTransitioned completed group (§15.3 'none'; there is no CompleteAction).",
            ("§15.3 none",),
            appended(PAYMENT, None, n, g(action_to(P_ACT, "completed"))),
            (I, None)))

    n = "sup-group-actor-disagreement"
    add(neg(n, "A RecordFinding group whose events share correlationId and occurredAt but not the actor "
               "(§15.2 2.3 inconsistent group).",
            ("§15.2 2.3",),
            appended(PAYMENT, None, n, g(finding(ids(n)("finding"), P_ACT, "Declines stopped on the test node."),
                                         action_to(P_ACT, "completed", HUMAN_B))),
            (I, None)))

    return tuple(specs)


# ------------------------------------------------------------------------------------------------- valid histories

class Valid(NamedTuple):
    name: str
    description: str
    covers: tuple[str, ...]
    build: Callable[[Callable[[str], Events]], Events]


def valids() -> tuple[Valid, ...]:
    n = "sup-valid-entity-transitions"
    i = lambda label: uid(n, label)  # noqa: E731
    entity = Valid(n, "payment-failure extended with hypothesis, unknown, assumption, contradiction, action, "
                      "finding, proposal and evidence transitions that the frozen histories do not contain.",
                   ("§7.1", "§7.2", "§7.3", "§7.4", "§7.5", "§7.8", "§7.9", "§7.10", "§10.17", "§10.20", "§10.29"),
                   appended(PAYMENT, None, n,
                            g(prediction(i("p-prospective"), P_99A, "A three-decimal amount fails every time.",
                                         "prospective", "discriminating")),
                            g(finding(i("f1"), P_ACT, "Declines on the test node fell from 18% to 2%."),
                              action_to(P_ACT, "completed")),
                            g(prediction(i("p-forced"), P_ACE, "Raising timeouts fleet-wide removes declines.",
                                         "retrospective")),
                            g(hyp_transition(P_ACE, "markWeakened", "plausible", [P_EV_A12], "2% remain.")),
                            g(hyp_transition(P_ACE, "markPlausible", "weakened", [], "Residual declines explained.")),
                            g(hyp_transition(P_ACE, "markWeakened", "plausible", [P_EV_A12], "Second look.")),
                            g(hyp_transition(P_ACE, "markSupported", "weakened", [P_EV_A12], "Test node result.")),
                            g(evidence(i("e1"), "Gateway p99 is 1.2 s, well under the 3 s client timeout.")),
                            g(hyp_transition(P_ACE, "falsify", "supported", [i("e1")], "Latency is too low.")),
                            g(evidence(i("e2"), "The 1.2 s figure excluded TLS handshakes; true p99 is 3.4 s.")),
                            g(hyp_transition(P_ACE, "reopen", "falsified", [i("e2")], "New latency evidence.")),
                            g(hyp_transition(P_ACE, "supersede", "plausible", [], "Folded into a broader one.")),
                            g(hyp_transition(P_240, "markSupported", "plausible", [P_EV_DD2], "Support reports.")),
                            g(hyp_transition(P_240, "supersede", "supported", [], "Folded into a broader one.")),
                            g(unknown_to(P_UNK, "acceptedResidual", "Retry behaviour is out of scope.")),
                            g(unknown(i("u2"), "Was FR-118 enabled in all regions?", "blocking")),
                            g(unknown_to(i("u2"), "resolved", "Rule rollout log shows all regions.")),
                            g(unknown(i("u3"), "Are wallet payments affected?", "material")),
                            g(unknown_to(i("u3"), "investigating", "Querying wallet declines.")),
                            g(unknown_to(i("u3"), "acceptedResidual", "Wallet volume too low to tell.")),
                            g(assumption_to(P_ASM, "rejected")),
                            g(assumption(i("a2"), "Gateway clocks are synchronised.", "nonMaterial")),
                            g(assumption_to(i("a2"), "verified")),
                            g(contradiction(i("c1"), "Two dashboards disagree on decline counts.", [], "material")),
                            g(contradiction_to(i("c1"), "resolved", "One dashboard double-counted retries.")),
                            g(contradiction(i("c2"), "Fraud rule rejections exceed declines.", [P_EV_457],
                                            "informational")),
                            g(contradiction_to(i("c2"), "triaged", "Low priority.")),
                            g(contradiction_to(i("c2"), "acceptedResidual", "Counting windows differ.")),
                            g(action(i("x1"), "Replay a declined payment in staging.", [])),
                            g(action_to(i("x1"), "approved")),
                            g(action_to(i("x1"), "inProgress"), finding(i("f2"), i("x1"), "Replay declined."),
                              action_to(i("x1"), "completed")),
                            g(finding(i("f3"), i("x1"), "Second replay also declined.", "Deterministic failure.")),
                            g(prediction(i("p-empty-targets"), P_99A, "Three-decimal amounts fail in staging.",
                                         "retrospective", "necessary")),
                            g(action(i("x2"), "Query the processor for decline codes.", [P_F87], kind="query")),
                            g(action_to(i("x2"), "rejected")),
                            g(action(i("x3"), "Disable FR-118 on one node.", [P_240], kind="experiment")),
                            g(action_to(i("x3"), "approved")),
                            g(action_to(i("x3"), "inProgress")),
                            g(action_to(i("x3"), "cancelled")),
                            g(proposal(i("pr1"), "hypothesis", "Amount rounding differs between client and gateway.")),
                            g(hypothesis(i("h-accepted"), "Amount rounding differs between client and gateway."),
                              accepted(i("pr1"), i("h-accepted"))),
                            g(proposal(i("pr2"), "action", "Diff the rounding code of client and gateway.",
                                       rationale="Cheap to check.")),
                            g(action(i("x-accepted"), "Diff the rounding code of client and gateway.", []),
                              accepted(i("pr2"), i("x-accepted"))),
                            g(proposal(i("pr3"), "unknown", "Does the mobile app round differently?")),
                            g(rejected(i("pr3"), "Mobile traffic is not affected.")),
                            g(evidence(i("e3"), "https://example.invalid/runbook/checkout?step=3#retry",
                                       kind="artifact", category="document")),
                            g(assessed(i("e3"), "verified")),
                            g(assessed(i("e3"), "verified")),
                            g(superseded(i("e3"), P_EV_A12, "The runbook duplicates the gateway log.")),
                            g(related(i("r1"), i("e2"), i("h-accepted"), "insufficient")),
                            g(negative_knowledge(i("nk1"), "Client timeouts alone do not explain the declines.",
                                                 "Checkout, 2026-09-08", [i("e1")])),
                            g(lifecycle("markAwaitingEvidence", "active", "awaitingEvidence",
                                        "Waiting for the processor's decline export.")),
                            g(lifecycle("activateInvestigation", "awaitingEvidence", "active")),
                            g(observation(i("o1"), "Declines continued at 12:00 UTC."))))

    n = "sup-valid-verification-rounds"
    i = lambda label: uid(n, label)  # noqa: E731
    rounds = Valid(n, "api-latency extended through a failed verification while active, rework, a new round, "
                      "both criteria satisfied, cause reopened and re-confirmed with an addressed contradiction, "
                      "resolution, close, reopen and a second resolution.",
                   ("§7.6", "§7.7", "§7.9", "§11.4.2 addressed", "§12.1", "§12.3", "§13", "§14"),
                   appended(LATENCY, None, n,
                            g(lifecycle("activateInvestigation", "awaitingVerification", "active")),
                            g(evidence(i("e1"), "p95 240 ms on the first canary hour.")),
                            g(verification(i("v-inconclusive"), L_CRIT_1, "inconclusive", [])),
                            g(verification(i("v-failed"), L_CRIT_2, "failed", [i("e1")]),
                              corrective_to(L_CA, "verificationFailed")),
                            g(corrective_to(L_CA, "reworkRequired")),
                            g(corrective_to(L_CA, "inProgress")),
                            g(corrective_to(L_CA, "implemented")),
                            g(corrective_to(L_CA, "awaitingVerificationOfEffect")),
                            g(lifecycle("markAwaitingVerification", "active", "awaitingVerification")),
                            g(evidence(i("e2"), "p95 171 ms over 24 hours; 2 queries per request.")),
                            g(verification(i("v-sat-1"), L_CRIT_1, "satisfied", [i("e2")])),
                            g(verification(i("v-sat-2"), L_CRIT_2, "satisfied", [i("e2")]),
                              corrective_to(L_CA, "verifiedEffective")),
                            g(cause_reopened(L_CAUSE, "Check the cache contribution against new evidence.")),
                            g(evidence(i("e3"), "Hosts with the old TTL also regressed briefly.")),
                            g(related(i("r1"), i("e3"), L_CONTRIB, "contradicts", "Same symptom without the TTL.")),
                            g(contradiction(i("c1"), "Regression seen on hosts with the old TTL.", [i("e3")],
                                            "material")),
                            g(contradiction_to(i("c1"), "resolved", "Those hosts shared the regressed database.")),
                            g(assumption(i("a1"), "Canary traffic is representative.", "material")),
                            g(assumption_to(i("a1"), "provisionallyAccepted")),
                            g(unknown(i("u1"), "Will traffic growth reintroduce the regression?", "informational")),
                            g(confirmed(L_CAUSE)),
                            g(corrective(i("ca2"), "prevention", "Add a query-count budget to CI.", [L_CAUSE])),
                            g(corrective_to(i("ca2"), "approved")),
                            g(corrective_to(i("ca2"), "cancelled")),
                            g(conclusion(i("k1"), "The regression was caused by per-result queries unmasked by the "
                                         "shorter cache TTL.", "confirmedCause", [L_CAUSE],
                                         "The TTL question was accepted as residual.")),
                            g(lifecycle("resolveInvestigation", "awaitingVerification", "resolved")),
                            g(lifecycle("closeInvestigation", "resolved", "closed")),
                            g(lifecycle("reopenInvestigation", "closed", "active", "Audit asked for the CI budget.")),
                            g(lifecycle("markAwaitingEvidence", "active", "awaitingEvidence", "Waiting for audit.")),
                            g(lifecycle("activateInvestigation", "awaitingEvidence", "active")),
                            g(lifecycle("resolveInvestigation", "active", "resolved")),
                            g(lifecycle("reopenInvestigation", "resolved", "active", "Audit follow-up."))))

    n = "sup-valid-repetition-not-refused"
    i = lambda label: uid(n, label)  # noqa: E731
    repeat = Valid(n, "intermittent-outage extended with proposals that resemble negative knowledge or a falsified "
                      "hypothesis but are not refused: human/system actors, non-hypothesis kinds, interior "
                      "whitespace and punctuation differences, and a non-falsified hypothesis statement.",
                   ("§10.27", "§3.3", "§7.2", "§7.9"),
                   appended(OUTAGE, None, n,
                            g(proposal(i("p-human"), "hypothesis", O_NK_STATEMENT, HUMAN)),
                            g(proposal(i("p-system"), "hypothesis", O_FALSIFIED_STATEMENT.upper(), SYSTEM)),
                            g(proposal(i("p-unknown"), "unknown", O_NK_STATEMENT)),
                            g(proposal(i("p-action"), "action", " " + O_FALSIFIED_STATEMENT.lower())),
                            g(proposal(i("p-interior"), "hypothesis", O_NK_STATEMENT.replace("not the", "not  the"))),
                            g(proposal(i("p-punct"), "hypothesis", O_NK_STATEMENT.rstrip("."))),
                            g(proposal(i("p-live"), "hypothesis",
                                       "  the LOAD balancer drops connections during scheduled certificate rotation.")),
                            g(unknown_to(O_UNK_INV, "resolved", "The 03:28 rotation reproduced the burst.")),
                            g(lifecycle("activateInvestigation", "awaitingEvidence", "active"))))
    n = "sup-valid-unicode-line-separators"
    i = lambda label: uid(n, label)  # noqa: E731
    separators = Valid(n, "payment-failure extended with verbatim text containing raw U+0085 (NEL), U+2028 (LINE "
                          "SEPARATOR) and U+2029 (PARAGRAPH SEPARATOR). RFC 8785 does not escape them, so they appear "
                          "raw inside events.ndjson lines; a reader must split lines on LF only (CONTRACT §2).",
                       ("CONTRACT §2 LF framing", "RFC 8785 escaping", "§3.1 verbatim text"),
                       appended(PAYMENT, None, n,
                                g(observation(i("o1"), "Operator note line 1\u0085line 2\u2028line 3\u2029line 4.")),
                                g(evidence(i("e1"), "Pasted log:\u2028gateway timeout\u2029retry\u0085declined",
                                           category="log")),
                                g(related(i("r1"), i("e1"), P_ACE, "supports",
                                          "Rationale with\u2028a line separator.")),
                                g(hypothesis(i("h1"), "Separators\u0085inside\u2028a\u2029statement.",
                                             "Mechanism\u2029text."))))
    return entity, rounds, repeat, separators


# ====================================================================================================================
# Materialization (deterministic)
# ====================================================================================================================

def events_bytes(ev: Events) -> bytes:
    return "".join(jcs(e) + "\n" for e in ev).encode("utf-8")


def stale_files(dataset: Path, ev: Events) -> dict[str, bytes]:
    """Negative fixtures: frozen snapshot of the same investigation (stale), manifest consistent with the events."""
    import hashlib
    inv = ev[0]["investigationId"]
    root = dataset / "fixtures" / "v1.0.0" / "investigations" / inv
    snapshot_bytes = (root / "snapshot.json").read_bytes()
    manifest = json.loads((root / "manifest.json").read_text("utf-8"))
    body = events_bytes(ev)
    new_manifest = {**manifest, "aggregateVersion": len(ev), "eventCount": len(ev),
                    "eventsSha256": hashlib.sha256(body).hexdigest(),
                    "snapshotSha256": hashlib.sha256(jcs(json.loads(snapshot_bytes)).encode()).hexdigest(),
                    "createdAt": ev[0]["occurredAt"], "updatedAt": ev[-1]["occurredAt"]}
    return {"events.ndjson": body, "manifest.json": (jcs(new_manifest) + "\n").encode(), "snapshot.json": snapshot_bytes}


def projected_files(ev: Events) -> tuple[dict[str, bytes], dict]:
    """Valid histories: snapshot and manifest from the clean-room consumer's projection (CONTRACT §8.1, §9.1)."""
    from canon import digest_json, file_bytes
    from reader import build_manifest, build_snapshot, project
    body = events_bytes(ev)
    state = project(list(ev))
    snapshot = build_snapshot(list(ev), state)
    manifest = build_manifest(ev[0]["investigationId"], list(ev), body, snapshot)
    files = {"events.ndjson": body, "manifest.json": file_bytes(manifest), "snapshot.json": file_bytes(snapshot)}
    return files, {"stateSha256": digest_json(state), "version": state["version"], "lifecycle": state["lifecycle"]}


def negative_doc(spec: Spec, files: dict[str, bytes]) -> bytes:
    doc = {"formatVersion": "1.0.0", "name": spec.name, "description": spec.description,
           "expectedErrorCode": "CAUSA.HISTORY_INADMISSIBLE", "expectedDomainCode": spec.errors[0][0],
           "kind": "investigation", "directoryId": json.loads(files["manifest.json"])["investigationId"],
           "files": {k: {"base64": base64.b64encode(v).decode("ascii")} for k, v in files.items()}}
    return (jcs(doc) + "\n").encode("utf-8")


def plan(dataset: Path) -> tuple[dict[str, bytes], list[dict]]:
    """All output files {path relative to OUT: bytes} and the matrix rows."""
    cache: dict[str, Events] = {}
    base = lambda inv: cache.setdefault(inv, load_events(dataset, inv))  # noqa: E731
    out: dict[str, bytes] = {}
    rows: list[dict] = []
    for spec in negatives():
        ev = spec.build(base)
        files = stale_files(dataset, ev)
        out[f"negative/{spec.name}.json"] = negative_doc(spec, files)
        rows.append({"id": f"AC-SUP-{spec.name}", "kind": "negative",
                     "input": f"acceptance/supplementary/negative/{spec.name}.json",
                     "covers": list(spec.covers),
                     "errors": [{"code": c, "inner": r} for c, r in spec.errors],
                     "expected": {"primaryCode": "CAUSA.HISTORY_INADMISSIBLE", "domainCode": spec.errors[0][0],
                                  "admissibility": "evaluated"}})
    for v in valids():
        ev = v.build(base)
        files, facts = projected_files(ev)
        inv = ev[0]["investigationId"]
        for k, b in files.items():
            out[f"valid/{v.name}/{inv}/{k}"] = b
        import hashlib
        rows.append({"id": f"AC-SUP-{v.name}", "kind": "valid", "input": f"acceptance/supplementary/valid/{v.name}/{inv}",
                     "covers": list(v.covers), "errors": [],
                     "expected": {"primaryCode": None, "domainCode": None, "admissibility": "evaluated",
                                  "stateSha256": facts["stateSha256"],
                                  "eventsSha256": hashlib.sha256(files["events.ndjson"]).hexdigest(),
                                  "version": facts["version"], "lifecycle": facts["lifecycle"]}})
    matrix = {"contractVersion": "1.0.0", "dataset": "causa-demo-v1", "generator": GENERATOR,
              "description": "Supplementary stage-15 rows (SEMANTICS §16, §7). 'errors' is the complete ordered "
                             "History.validate list of the reference oracle: history code and inner rule code "
                             "(null when the group is not a refusal).", "rows": rows}
    out["MATRIX.json"] = (json.dumps(matrix, indent=1, ensure_ascii=False) + "\n").encode("utf-8")
    return out, rows


def generate(dataset: Path) -> int:
    out, rows = plan(dataset)
    for sub in ("negative", "valid"):
        shutil.rmtree(OUT / sub, ignore_errors=True)
    for rel, data in sorted(out.items()):
        path = OUT / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    print(f"wrote {sum(r['kind'] == 'negative' for r in rows)} negative fixtures, "
          f"{sum(r['kind'] == 'valid' for r in rows)} valid histories, MATRIX.json under {OUT}")
    return 0


# ====================================================================================================================
# Check against the oracle and the clean-room consumer (impure)
# ====================================================================================================================

INNER = re.compile(r"would be refused now \(([A-Z_.]+)\)")


def oracle_lists(oracle: str, paths: list[Path]) -> dict[str, list[tuple[str, str | None]]]:
    env = {**os.environ, "DOTNET_ROOT": os.environ.get("DOTNET_ROOT", "/root/.dotnet")}
    proc = subprocess.run([oracle, *map(str, paths)], capture_output=True, text=True, env=env, check=True)
    result = {}
    decoder, text, pos = json.JSONDecoder(strict=False), proc.stdout, 0
    docs = []
    while text[pos:].strip():                      # messages may contain raw newlines (F# %A formatting)
        d, end = decoder.raw_decode(text, len(text) - len(text[pos:].lstrip()))
        docs.append(d)
        pos = end
    for d in docs:
        inner = [(m.group(1) if (m := INNER.search(msg)) else None) for msg in d.get("messages", [])]
        result[d["file"]] = [(c, r) for c, r in zip(d["codes"], inner)] if d["decode"] is None else [("DECODE", d["decode"])]
    return result


def consumer(*args: str) -> dict:
    proc = subprocess.run([sys.executable, "-I", str(CONSUMER), *args], capture_output=True, text=True)
    return json.loads(proc.stdout.strip().splitlines()[-1])


def check(dataset: Path, oracle: str, report: Path | None) -> int:
    out, rows = plan(dataset)
    stale = [rel for rel, data in out.items() if not (OUT / rel).exists() or (OUT / rel).read_bytes() != data]
    results = []
    with tempfile.TemporaryDirectory() as tmp:
        paths = {}
        for row in rows:
            if row["kind"] == "negative":
                doc = json.loads(out[row["input"].removeprefix("acceptance/supplementary/")])
                data = base64.b64decode(doc["files"]["events.ndjson"]["base64"])
            else:
                data = out[row["input"].removeprefix("acceptance/supplementary/") + "/events.ndjson"]
            p = Path(tmp) / f"{row['id']}.ndjson"
            p.write_bytes(data)
            paths[row["id"]] = p
        verdicts = oracle_lists(oracle, list(paths.values()))
    for row in rows:
        got = [list(x) for x in verdicts[str(paths[row["id"]])]]
        want = [[e["code"], e["inner"]] for e in row["errors"]]
        target = str(REQ / row["input"])
        answer = (consumer("negative", "--dataset", str(dataset), "--fixture", target) if row["kind"] == "negative"
                  else consumer("verify", "--dataset", str(dataset), "--investigation", target))
        mismatched = {k: (v, answer.get(k)) for k, v in row["expected"].items() if answer.get(k) != v}
        results.append({"id": row["id"], "oracle": got, "oracleAgrees": got == want, "consumer": {
            k: answer.get(k) for k in ("primaryCode", "domainCode", "admissibility", "stateSha256", "detail")},
            "consumerAgrees": not mismatched, "consumerMismatch": mismatched})
    bad = [r for r in results if not (r["oracleAgrees"] and r["consumerAgrees"])]
    for r in results:
        flag = "OK  " if r in [x for x in results if x not in bad] else "FAIL"
        print(f"{flag} {r['id']} oracle={r['oracle']} consumer={r['consumer']['primaryCode']},{r['consumer']['domainCode']}"
              + (f" mismatch={r['consumerMismatch']}" if r["consumerMismatch"] else ""))
    for rel in stale:
        print(f"STALE {rel} (run generate)")
    if report:
        report.write_text(json.dumps({"rows": results, "stale": stale}, indent=1) + "\n", "utf-8")
    print(f"{'PASS' if not bad and not stale else 'FAIL'} {len(results) - len(bad)}/{len(results)} rows agree with "
          f"the oracle and the clean-room consumer; {len(stale)} stale file(s)")
    return 0 if not bad and not stale else 1


def main(argv: list[str]) -> int:
    if len(argv) < 2 or argv[1] not in ("generate", "check") or "--causa-data" not in argv:
        print(__doc__)
        return 2
    dataset = Path(argv[argv.index("--causa-data") + 1])
    if argv[1] == "generate":
        return generate(dataset)
    if "--oracle" not in argv:
        print(__doc__)
        return 2
    report = Path(argv[argv.index("--json") + 1]) if "--json" in argv else None
    return check(dataset, argv[argv.index("--oracle") + 1], report)


if __name__ == "__main__":
    sys.exit(main(sys.argv))
