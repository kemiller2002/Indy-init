"""Domain admissibility (CONTRACT §11.3, stage 15) implemented from SEMANTICS.md alone.

Section map:
  §3   value semantics, blank/trim/lowerInvariant            -> is_blank (canon), norm
  §4.4 derived predicates                                    -> is_open, open_unknown, ..., all_satisfied
  §5   evolve                                                -> apply_event
  §6   command catalog / §8 decide / §9 lifecycle / §10 content / §11 cause gate / §12 verification /
       §13 conclusion / §14 resolution gate                  -> decide and helpers
  §7   transition tables                                     -> *_TABLE
  §10.4.1-§10.4.2 artifact URL procedure (+ §17 E-1, CAUSA-URL-W) -> artifact_url_verdict, writer_url_check
  §15  History.validate                                      -> validate_history, command_of, code_for

The state is an immutable-by-convention dict: every function returns a new value and never mutates its inputs.
"""
from __future__ import annotations

import ipaddress
import re
import unicodedata
from dataclasses import dataclass, field
from functools import reduce
from typing import Any

from canon import ENGINE_WHITESPACE, is_blank

POLICY_ID = "core-default/1"                                     # §3.5
VERIFICATION_FAILED_REASON = "A verification failed; work reopens (A21)."   # §12.3

COLLECTIONS = ("observations", "hypotheses", "evidence", "relationships", "unknowns", "assumptions",
               "contradictions", "predictions", "actions", "findings", "negativeKnowledge", "causes",
               "correctiveActions", "criteria", "verifications", "conclusions", "proposals")


# ====================================================================================================================
# Errors and decisions (§1.3, §1.5)
# ====================================================================================================================

@dataclass(frozen=True)
class DomainError:
    code: str
    rule: str
    message: str = ""


@dataclass(frozen=True)
class Refused:
    errors: tuple


@dataclass(frozen=True)
class Emitted:
    bodies: tuple            # of Body


@dataclass(frozen=True)
class Body:
    event_type: str
    payload: dict = field(hash=False)
    implicit: tuple = ()     # HypothesisTransitioned: (at, aggregateVersion), §2.2 / §15.6


@dataclass(frozen=True)
class Unresolved:
    """A platform-defined case (SEMANTICS §17) this implementation cannot decide from the text alone."""
    reason: str


def err(code: str, rule: str, message: str = "") -> tuple:
    return (DomainError(code, rule, message),)


def collect(*checks: tuple) -> tuple:
    return tuple(e for c in checks for e in c)


def emit(*bodies: Body) -> Emitted:
    return Emitted(tuple(bodies))


def body(event_type: str, **payload) -> Body:
    return Body(event_type, dict(payload))


# ====================================================================================================================
# §3 value helpers
# ====================================================================================================================

def text(s) -> tuple:
    return () if not is_blank(s) else err("VALIDATION.EMPTY", "schema", "text is blank")


def opt(s):
    return None if is_blank(s) else s


def _simple_lower(ch: str) -> str:
    if ch == "İ":
        return ch
    low = ch.lower()
    return low if len(low) == 1 else ch


UNICODE_PIN = "15.1.0"                                            # §3.3 pins Unicode 15.1.0 simple lowercase


def norm(s: str) -> str:
    """§3.3: lowerInvariant(trim(s)); simple one-to-one lowercase per code point, U+0130 unchanged."""
    return "".join(_simple_lower(c) for c in s.strip("".join(ENGINE_WHITESPACE)))


def vals(S: dict, coll: str) -> list:
    """Entities of a collection in id order (§3.4)."""
    return [S[coll][k] for k in sorted(S[coll])]


def absent(S, coll, id_) -> tuple:
    return () if id_ not in S[coll] else err("DUPLICATE_ID", "schema", f"{coll} {id_} already exists")


def found(S, coll, id_) -> tuple:
    return () if id_ in S[coll] else err("NOT_FOUND", "schema", f"{coll} {id_} not found")


def all_exist(S, coll, ids) -> tuple:
    return () if all(i in S[coll] for i in ids) else err("NOT_FOUND", "schema", f"some {coll} not found")


def transition(table: dict, frm: str, cmd: str):
    target = table.get((frm, cmd))
    if target is None:
        return err("TRANSITION.ILLEGAL", "STATE-TRANSITION-TABLES", f"{cmd} not allowed from {frm}"), None
    return (), target


# ====================================================================================================================
# §7 transition tables
# ====================================================================================================================

def _table(rows: dict) -> dict:
    return {(frm, cmd): to for frm, m in rows.items() for cmd, to in m.items()}


HYPOTHESIS_TABLE = _table({
    "proposed": {"markPlausible": "plausible", "falsify": "falsified", "supersede": "superseded"},
    "plausible": {"markSupported": "supported", "markWeakened": "weakened", "falsify": "falsified",
                  "supersede": "superseded"},
    "supported": {"markWeakened": "weakened", "falsify": "falsified", "supersede": "superseded"},
    "weakened": {"markPlausible": "plausible", "markSupported": "supported", "falsify": "falsified",
                 "supersede": "superseded"},
    "falsified": {"reopen": "plausible"},
})
EVIDENCE_REQUIRING = frozenset({"markSupported", "markWeakened", "falsify", "reopen"})

UNKNOWN_TABLE = _table({
    "identified": {"StartInvestigatingUnknown": "investigating", "ResolveUnknown": "resolved",
                   "AcceptUnknownAsResidual": "acceptedResidual"},
    "investigating": {"ResolveUnknown": "resolved", "AcceptUnknownAsResidual": "acceptedResidual"},
})
CONTRADICTION_TABLE = _table({
    "detected": {"TriageContradiction": "triaged", "ResolveContradiction": "resolved",
                 "AcceptContradictionAsResidual": "acceptedResidual"},
    "triaged": {"ResolveContradiction": "resolved", "AcceptContradictionAsResidual": "acceptedResidual"},
    "investigating": {"ResolveContradiction": "resolved", "AcceptContradictionAsResidual": "acceptedResidual"},
})
ASSUMPTION_TABLE = _table({
    "explicitUnverified": {"ProvisionallyAcceptAssumption": "provisionallyAccepted",
                           "VerifyAssumption": "verified", "RejectAssumption": "rejected"},
    "provisionallyAccepted": {"VerifyAssumption": "verified", "RejectAssumption": "rejected"},
})
ACTION_TABLE = _table({
    "proposed": {"ApproveAction": "approved", "RejectAction": "rejected"},
    "approved": {"StartAction": "inProgress", "CancelAction": "cancelled"},
    "inProgress": {"CancelAction": "cancelled"},
})
CORRECTION_TABLE = _table({
    "proposed": {"ApproveCorrection": "approved", "CancelCorrection": "cancelled"},
    "approved": {"CancelCorrection": "cancelled", "StartCorrection": "inProgress"},
    "inProgress": {"MarkImplemented": "implemented"},
    "implemented": {"StartVerificationOfEffect": "awaitingVerificationOfEffect"},
    "verificationFailed": {"RequireRework": "reworkRequired"},
    "reworkRequired": {"StartCorrection": "inProgress"},
})
LIFECYCLE_TABLE = _table({
    "draft": {"activateInvestigation": "active"},
    "active": {"markAwaitingEvidence": "awaitingEvidence", "markAwaitingVerification": "awaitingVerification",
               "resolveInvestigation": "resolved"},
    "awaitingEvidence": {"activateInvestigation": "active"},
    "awaitingVerification": {"activateInvestigation": "active", "resolveInvestigation": "resolved"},
    "resolved": {"closeInvestigation": "closed", "reopenInvestigation": "active"},
    "closed": {"reopenInvestigation": "active"},
    "archived": {"activateInvestigation": "active"},
})
LIFECYCLE_COMMANDS = frozenset({"activateInvestigation", "markAwaitingEvidence", "markAwaitingVerification",
                                "resolveInvestigation", "closeInvestigation", "reopenInvestigation"})


# ====================================================================================================================
# §4.4 derived predicates
# ====================================================================================================================

def is_open(lifecycle: str) -> bool:
    return lifecycle in ("draft", "active", "awaitingEvidence", "awaitingVerification")


def open_unknown(u) -> bool:
    return u["state"] in ("identified", "investigating")


def open_contradiction(c) -> bool:
    return c["state"] in ("detected", "triaged", "investigating")


def material(m) -> bool:
    return m in ("blocking", "material")


def active(S, e) -> bool:
    return e in S["evidence"] and S["evidence"][e]["supersededBy"] is None


def round_of(S, ca) -> int:
    return S["correctiveActions"][ca]["awaitingSinceVersion"] if ca in S["correctiveActions"] else 0


def fresh(S, r, e) -> bool:
    return active(S, e) and S["evidence"][e]["admittedAtVersion"] > r


def criteria_of(S, ca) -> list:
    return [c for c in vals(S, "criteria") if c["correctiveActionId"] == ca]


def satisfied(S, c) -> bool:
    r = round_of(S, c["correctiveActionId"])
    return any(v["criterionId"] == c["id"] and v["outcome"] == "satisfied" and v["recordedAtVersion"] > r
               and v["evidenceIds"] and all(fresh(S, r, e) for e in v["evidenceIds"])
               for v in S["verifications"].values())


def all_satisfied(S, ca) -> bool:
    cs = criteria_of(S, ca)
    return bool(cs) and all(satisfied(S, c) for c in cs)


def latest_conclusion(S):
    cs = list(S["conclusions"].values())
    return max(cs, key=lambda c: c["recordedAtVersion"]) if cs else None


# ====================================================================================================================
# §5 evolve (also the CONTRACT §8.3 projection fold, before the final sort)
# ====================================================================================================================

def _provenance(e) -> dict:
    return {"actor": e["actor"], "recordedAt": e["occurredAt"]}


def _new(S, coll, id_, e, **fields) -> dict:
    return {**S, coll: {**S[coll], id_: {"id": id_, **fields, "provenance": _provenance(e)}}}


def _update(S, coll, id_, **fields) -> dict:
    if id_ not in S[coll]:
        return S
    return {**S, coll: {**S[coll], id_: {**S[coll][id_], **fields}}}


def _apply_body(S: dict, e: dict) -> dict:
    t, p, v = e["eventType"], e["payload"], e["aggregateVersion"]
    if t == "InvestigationStarted":
        return S
    if t == "LifecycleChanged":
        return {**S, "lifecycle": p["to"]}
    if t == "ObservationRecorded":
        return _new(S, "observations", p["observationId"], e, original=p["original"], observedAt=p["observedAt"])
    if t == "HypothesisRecorded":
        return _new(S, "hypotheses", p["hypothesisId"], e, statement=p["statement"], mechanism=p["mechanism"],
                    state="proposed", history=[])
    if t == "HypothesisTransitioned":
        h = S["hypotheses"].get(p["hypothesisId"])
        if h is None:
            return S
        change = {"from": p["from"], "to": p["to"], "command": p["command"], "reason": p["reason"],
                  "evidenceIds": p["evidenceIds"], "at": e["occurredAt"], "aggregateVersion": v}
        return _update(S, "hypotheses", h["id"], state=p["to"], history=[*h["history"], change])
    if t == "EvidenceAdmitted":
        return _new(S, "evidence", p["evidenceId"], e, contentKind=p["contentKind"], content=p["content"],
                    category=p["category"], source=p["source"], capturedAt=p["capturedAt"],
                    verification="unverified", supersededBy=None, admittedAtVersion=v)
    if t == "EvidenceSuperseded":
        return _update(S, "evidence", p["evidenceId"], supersededBy=p["replacementEvidenceId"])
    if t == "EvidenceVerificationAssessed":
        return _update(S, "evidence", p["evidenceId"], verification=p["verification"])
    if t == "EvidenceRelated":
        return _new(S, "relationships", p["relationshipId"], e, evidenceId=p["evidenceId"],
                    hypothesisId=p["hypothesisId"], kind=p["kind"], rationale=p["rationale"])
    if t == "UnknownIdentified":
        return _new(S, "unknowns", p["unknownId"], e, question=p["question"], materiality=p["materiality"],
                    state="identified", basis=None)
    if t == "UnknownTransitioned":
        return _update(S, "unknowns", p["unknownId"], state=p["state"], basis=p["basis"])
    if t == "AssumptionRecorded":
        return _new(S, "assumptions", p["assumptionId"], e, proposition=p["proposition"],
                    materiality=p["materiality"], state="explicitUnverified")
    if t == "AssumptionTransitioned":
        return _update(S, "assumptions", p["assumptionId"], state=p["state"])
    if t == "ContradictionRecorded":
        return _new(S, "contradictions", p["contradictionId"], e, statement=p["statement"],
                    evidenceIds=p["evidenceIds"], materiality=p["materiality"], state="detected", basis=None)
    if t == "ContradictionTransitioned":
        return _update(S, "contradictions", p["contradictionId"], state=p["state"], basis=p["basis"])
    if t == "PredictionRecorded":
        return _new(S, "predictions", p["predictionId"], e, hypothesisId=p["hypothesisId"],
                    statement=p["statement"], strength=p["strength"], timing=p["timing"])
    if t == "ActionRecorded":
        return _new(S, "actions", p["actionId"], e, kind=p["kind"], purpose=p["purpose"],
                    targetHypothesisIds=p["targetHypothesisIds"], state="proposed")
    if t == "ActionTransitioned":
        return _update(S, "actions", p["actionId"], state=p["state"])
    if t == "FindingRecorded":
        return _new(S, "findings", p["findingId"], e, actionId=p["actionId"], originalResult=p["originalResult"],
                    interpretation=p["interpretation"])
    if t == "NegativeKnowledgeEstablished":
        return _new(S, "negativeKnowledge", p["negativeKnowledgeId"], e, statement=p["statement"],
                    scope=p["scope"], basisEvidenceIds=p["basisEvidenceIds"])
    if t == "CausalAssertionRecorded":
        return _new(S, "causes", p["causeId"], e, outcome=p["outcome"], members=p["members"],
                    mechanism=p["mechanism"], state="proposed")
    if t == "CausalAssertionConfirmed":
        return _update(S, "causes", p["causeId"], state="confirmed")
    if t == "CausalAssertionReopened":
        return _update(S, "causes", p["causeId"], state="reopened")
    if t == "CorrectiveActionRecorded":
        return _new(S, "correctiveActions", p["correctiveActionId"], e, kind=p["kind"],
                    description=p["description"], targetCauseIds=p["targetCauseIds"], state="proposed",
                    awaitingSinceVersion=0)
    if t == "CorrectiveActionTransitioned":
        extra = {"awaitingSinceVersion": v} if p["state"] == "awaitingVerificationOfEffect" else {}
        return _update(S, "correctiveActions", p["correctiveActionId"], state=p["state"], **extra)
    if t == "CriterionRecorded":
        return _new(S, "criteria", p["criterionId"], e, correctiveActionId=p["correctiveActionId"],
                    expectedOutcome=p["expectedOutcome"], method=p["method"])
    if t == "VerificationRecorded":
        return _new(S, "verifications", p["verificationId"], e, criterionId=p["criterionId"],
                    outcome=p["outcome"], evidenceIds=p["evidenceIds"], recordedAtVersion=v)
    if t == "ConclusionRecorded":
        return _new(S, "conclusions", p["conclusionId"], e, statement=p["statement"], strength=p["strength"],
                    causeIds=p["causeIds"], residualUncertainty=p["residualUncertainty"], recordedAtVersion=v)
    if t == "ProposalRecorded":
        return _new(S, "proposals", p["proposalId"], e, kind=p["kind"], text=p["text"],
                    rationale=p["rationale"], state="active", decision=None)
    if t == "ProposalWasAccepted":
        return _update(S, "proposals", p["proposalId"], state="accepted")
    if t == "ProposalWasRejected":
        return _update(S, "proposals", p["proposalId"], state="rejected", decision=p["reason"])
    return S


def apply_event(S: dict | None, e: dict) -> dict | None:
    """§5.1-§5.3: ⊥ ignores everything but InvestigationStarted; otherwise bump version, then apply the row."""
    if S is None:
        if e["eventType"] != "InvestigationStarted":
            return None
        p = e["payload"]
        empty = {c: {} for c in COLLECTIONS}
        start = {"investigationId": e["investigationId"], "title": p["title"], "lifecycle": "draft",
                 "version": e["aggregateVersion"], "createdAt": e["occurredAt"], **empty}
        return _new(start, "observations", p["observationId"], e, original=p["original"], observedAt=None)
    return _apply_body({**S, "version": e["aggregateVersion"]}, e)


def replay(events) -> dict | None:
    return reduce(apply_event, events, None)


def project_state(S: dict) -> dict:
    """CONTRACT §8.2/§8.3: entity maps become arrays sorted by ordinal id text."""
    return {"investigationId": S["investigationId"], "title": S["title"], "lifecycle": S["lifecycle"],
            "version": S["version"], "createdAt": S["createdAt"],
            **{c: vals(S, c) for c in COLLECTIONS}}


# ====================================================================================================================
# §10.4.1 artifact URL well-formedness: normative decision procedure; §10.4.2 profile; §17 E-1
# ====================================================================================================================

_ASCII_WS = "\t\n\x0b\x0c\r "
_LABEL = r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?"
# §10.4.2 profile pattern, ECMA '$' rendered as '\Z' (CONTRACT §2.13).  Groups: host, port, rest.
_PROFILE = re.compile(r"[Hh][Tt][Tt][Pp][Ss]?://((?:" + _LABEL + r"(?:\." + _LABEL + r")*)|\[[0-9A-Fa-f:.]+\])"
                      r"(?::([0-9]{1,5}))?([/?#][\x21-\x7E]*)?\Z")
_PCHAR = frozenset("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~"   # unreserved
                   "!$&'()*+,;="                                                          # sub-delims
                   ":@/")
_QCHAR = _PCHAR | {"?"}
_HEXDIGITS = frozenset("0123456789abcdefABCDEF")


def _chars_ok(s: str, allowed: frozenset) -> bool:
    """Every character in `allowed`, except '%' which must start a %HH escape."""
    return all((c in allowed) if c != "%" else (s[i + 1:i + 2] in _HEXDIGITS and s[i + 2:i + 3] in _HEXDIGITS
                                                 and len(s) >= i + 3)
               for i, c in enumerate(s))


def in_profile(u: str):
    """§10.4.2 membership: the pattern matches the whole string and a bracketed host is RFC 4291 IPv6 text."""
    m = _PROFILE.match(u)
    if not m:
        return None
    host = m.group(1)
    if host.startswith("["):
        try:
            ipaddress.IPv6Address(host[1:-1])
        except ValueError:
            return None
    return m


def well_formed_profile(m) -> bool:
    port, rest = m.group(2), m.group(3) or ""
    hash_at = rest.find("#")
    before, fragment = (rest, None) if hash_at < 0 else (rest[:hash_at], rest[hash_at + 1:])
    q_at = before.find("?")
    path, query = (before, None) if q_at < 0 else (before[:q_at], before[q_at + 1:])
    return ((port is None or int(port) <= 65535)
            and (fragment is None or "#" not in fragment)
            and _chars_ok(path, _PCHAR)
            and (query is None or _chars_ok(query, _QCHAR))
            and (fragment is None or _chars_ok(fragment, _QCHAR)))


def artifact_url_verdict(u: str):
    """§10.4.1 decision procedure. True = well-formed, False = ill-formed (EVIDENCE.URL),
    Unresolved = step 4, platform-defined (§17 E-1): the reader stops with 'admissibility not evaluated'."""
    if not re.match(r"[Hh][Tt][Tt][Pp][Ss]?://", u):                                     # step 1: prefix
        return False
    if any(c in _ASCII_WS for c in u.strip(_ASCII_WS)):                                   # step 2: interior ws
        return False
    m = in_profile(u)                                                                     # step 3: profile
    if m is not None:
        return well_formed_profile(m)
    return Unresolved("artifact URL outside the §10.4.2 profile (platform-defined, SEMANTICS §17 E-1)")  # step 4


def writer_url_check(u: str):
    """CAUSA-URL-W: None when a writer may persist `u`; otherwise (CAUSA code or None, domain refusal code or None)."""
    if not re.match(r"[Hh][Tt][Tt][Pp][Ss]?://", u) or any(c in _ASCII_WS for c in u.strip(_ASCII_WS)):
        return (None, "EVIDENCE.URL")
    m = in_profile(u)
    if m is None:
        return ("CAUSA.UNREPRESENTABLE", None)
    return None if well_formed_profile(m) else (None, "EVIDENCE.URL")


# ====================================================================================================================
# §11 cause gate, §14 resolution gate
# ====================================================================================================================

def cause_support(S, ca) -> tuple:
    def member_findings(m):
        h = S["hypotheses"].get(m["hypothesisId"])
        if h is None:
            return err("CAUSE.MEMBER_MISSING", "I-19")
        rels = vals(S, "relationships")
        addressed = {e for c in S["contradictions"].values() if c["state"] in ("resolved", "acceptedResidual")
                     for e in c["evidenceIds"]}
        return collect(
            err("CAUSE.MEMBER_NOT_SUPPORTED", "§15.3") if h["state"] != "supported" else (),
            () if any(r["hypothesisId"] == h["id"] and r["kind"] == "supports" and active(S, r["evidenceId"])
                      for r in rels) else err("CAUSE.NO_SUPPORTING_EVIDENCE", "§15.3"),
            tuple(DomainError("CAUSE.CONTRADICTING_EVIDENCE", "§15.4") for r in rels
                  if r["hypothesisId"] == h["id"] and r["kind"] == "contradicts" and active(S, r["evidenceId"])
                  and r["evidenceId"] not in addressed))

    member_ids = {m["hypothesisId"] for m in ca["members"]}
    return collect(
        () if any(m["role"] == "rootCause" for m in ca["members"]) else err("CAUSE.NO_ROOT", "R6.3"),
        *(member_findings(m) for m in ca["members"]),
        tuple(DomainError("CAUSE.OPEN_CONTRADICTION", "§15.4") for c in vals(S, "contradictions")
              if open_contradiction(c) and material(c["materiality"])),
        tuple(DomainError("CAUSE.OPEN_UNKNOWN", "§15.5") for u in vals(S, "unknowns")
              if open_unknown(u) and material(u["materiality"])),
        tuple(DomainError("CAUSE.UNVERIFIED_ASSUMPTION", "§15.6") for a in vals(S, "assumptions")
              if a["state"] == "explicitUnverified" and material(a["materiality"])),
        tuple(DomainError("CAUSE.ALTERNATIVE_NOT_ADDRESSED", "§15.7") for h in vals(S, "hypotheses")
              if h["id"] not in member_ids and h["state"] in ("proposed", "plausible", "supported")),
        err("CAUSE.NO_MECHANISM", "§15.8") if ca["mechanism"] is None else ())


def cause_blockers(S, cid) -> tuple:
    if cid not in S["causes"]:
        return err("CAUSE.NOT_FOUND", "I-19")
    ca = S["causes"][cid]
    return collect(
        err("CAUSE.STATE", "STATE-TRANSITION-TABLES §10") if ca["state"] not in ("proposed", "reopened") else (),
        err("INVESTIGATION.NOT_OPEN", "D-005")
        if S["lifecycle"] not in ("active", "awaitingEvidence", "awaitingVerification") else (),
        cause_support(S, ca))


def resolution_blockers(S) -> tuple:
    confirmed = [c for c in vals(S, "causes") if c["state"] == "confirmed"]
    C = {c["id"] for c in confirmed}
    corrections = [ca for ca in vals(S, "correctiveActions")
                   if ca["kind"] == "correction" and any(t in C for t in ca["targetCauseIds"])]
    k = latest_conclusion(S)
    if not corrections:
        correction_check = err("RESOLVE.NO_CORRECTION", "R7.2")
    elif not any(ca["state"] == "verifiedEffective" for ca in corrections):
        correction_check = err("RESOLVE.CORRECTION_NOT_VERIFIED", "R7.4")
    else:
        correction_check = ()
    conclusion_checks = () if k is None else collect(
        err("RESOLVE.CONCLUSION_STALE", "R6.1")
        if k["strength"] == "confirmedCause" and (not k["causeIds"] or any(c not in C for c in k["causeIds"]))
        else (),
        err("RESOLVE.RESIDUAL_NOT_STATED", "R6.4")
        if k["residualUncertainty"] is None and (
            any(u["state"] == "acceptedResidual" for u in S["unknowns"].values())
            or any(c["state"] == "acceptedResidual" for c in S["contradictions"].values())) else ())
    return collect(
        err("RESOLVE.NO_CONFIRMED_CAUSE", "R6.1") if not confirmed else (),
        tuple(DomainError("RESOLVE.CAUSE_NO_LONGER_HOLDS", f.rule, f.code)
              for c in confirmed for f in cause_support(S, c)),
        correction_check,
        tuple(DomainError("RESOLVE.CRITERION_UNSATISFIED", "R7.3") for ca in corrections
              if ca["state"] != "cancelled" for crit in criteria_of(S, ca["id"]) if not satisfied(S, crit)),
        tuple(DomainError("RESOLVE.BLOCKING_UNKNOWN", "R5.2") for u in vals(S, "unknowns")
              if open_unknown(u) and u["materiality"] == "blocking"),
        err("RESOLVE.NO_CONCLUSION", "R6.4") if k is None else (),
        conclusion_checks)


# ====================================================================================================================
# §8 decide
# ====================================================================================================================

def _refuse_or(errors: tuple, then):
    return Refused(errors) if errors else then()


def decide(S: dict | None, env: dict, cmd: dict, expected_version: int | None = None):
    """§8. env = {investigationId, actor, at}; cmd = {"name": <command>, ...inputs}. Returns Refused | Emitted |
    Unresolved."""
    name = cmd["name"]
    if env["actor"].get("kind") == "agent" and name != "RecordProposal":                       # step 1
        return Refused(err("AGENT.PROPOSAL_ONLY", "D-004"))
    if S is None:                                                                              # step 2
        if name != "StartInvestigation":
            return Refused(err("INVESTIGATION.NOT_FOUND", "schema"))
        if cmd["investigationId"] != env["investigationId"]:
            return Refused(err("ENVELOPE.MISMATCH", "schema"))
        t = cmd["original"] if is_blank(cmd["title"]) else cmd["title"]
        return _refuse_or(collect(text(t), text(cmd["original"])), lambda: emit(body(
            "InvestigationStarted", title=t, observationId=cmd["observationId"], original=cmd["original"])))
    if env["investigationId"] != S["investigationId"]:                                         # step 3
        return Refused(err("ENVELOPE.MISMATCH", "schema"))
    if expected_version is not None and expected_version != S["version"]:                      # step 4
        return Refused(err("VERSION.STALE", "STATE-TRANSITION-TABLES §16.3"))
    if name in LIFECYCLE_COMMANDS:                                                             # step 5
        return decide_lifecycle(S, cmd)
    if not is_open(S["lifecycle"]):                                                            # step 6
        return Refused(err("INVESTIGATION.NOT_OPEN", "D-005"))
    return decide_content(S, env, cmd)                                                         # step 7


def decide_lifecycle(S, cmd):
    """§9."""
    name = cmd["name"]
    errs, target = transition(LIFECYCLE_TABLE, S["lifecycle"], name)
    if errs:
        return Refused(errs)
    lc = lambda reason: emit(body("LifecycleChanged", **{"from": S["lifecycle"], "to": target,  # noqa: E731
                                                          "command": name, "reason": reason}))
    if name in ("markAwaitingEvidence", "reopenInvestigation"):
        return _refuse_or(text(cmd["reason"]), lambda: lc(cmd["reason"]))
    if name == "markAwaitingVerification":
        work = any(ca["state"] in ("implemented", "awaitingVerificationOfEffect")
                   for ca in S["correctiveActions"].values())
        return lc(None) if work else Refused(err("LIFECYCLE.NO_VERIFICATION_WORK", "STATE-TRANSITION-TABLES §1"))
    if name == "resolveInvestigation":
        return _refuse_or(resolution_blockers(S), lambda: lc(None))
    return lc(None)


def _record_hypothesis(S, hid, statement, mechanism):
    return _refuse_or(collect(absent(S, "hypotheses", hid), text(statement)), lambda: emit(body(
        "HypothesisRecorded", hypothesisId=hid, statement=statement, mechanism=opt(mechanism))))


def _identify_unknown(S, uid, question, materiality):
    return _refuse_or(collect(absent(S, "unknowns", uid), text(question)), lambda: emit(body(
        "UnknownIdentified", unknownId=uid, question=question, materiality=materiality)))


def _record_action(S, aid, kind, purpose, targets):
    return _refuse_or(collect(absent(S, "actions", aid), text(purpose), all_exist(S, "hypotheses", targets)),
                      lambda: emit(body("ActionRecorded", actionId=aid, kind=kind, purpose=purpose,
                                        targetHypothesisIds=targets)))


def _transition_hypothesis(S, env, c):
    missing = found(S, "hypotheses", c["hypothesisId"])
    if missing:
        return Refused(missing)
    h = S["hypotheses"][c["hypothesisId"]]
    c1, target = transition(HYPOTHESIS_TABLE, h["state"], c["command"])
    if c["command"] in EVIDENCE_REQUIRING and not c["evidenceIds"]:
        c3 = err("HYPOTHESIS.EVIDENCE_REQUIRED", "R2.3")
    else:
        c3 = all_exist(S, "evidence", c["evidenceIds"])
    c4 = ()
    if c["command"] == "reopen":
        fa = next((ch["aggregateVersion"] for ch in reversed(h["history"]) if ch["to"] == "falsified"), 0)
        if not any(e in S["evidence"] and S["evidence"][e]["admittedAtVersion"] > fa for e in c["evidenceIds"]):
            c4 = err("HYPOTHESIS.REOPEN_NEEDS_NEW_EVIDENCE", "R2.5")
    return _refuse_or(collect(c1, text(c["reason"]), c3, c4), lambda: emit(Body(
        "HypothesisTransitioned",
        {"hypothesisId": h["id"], "from": h["state"], "to": target, "command": c["command"],
         "reason": c["reason"], "evidenceIds": c["evidenceIds"]},
        (env["at"], S["version"] + 1))))


def _admit_evidence(S, c):
    if c["contentKind"] == "artifact":
        verdict = artifact_url_verdict(c["content"])
        if isinstance(verdict, Unresolved):
            return verdict
        content_check = () if verdict is True else err("EVIDENCE.URL", "R3.5")
    else:
        content_check = text(c["content"])
    return _refuse_or(collect(absent(S, "evidence", c["evidenceId"]), content_check, text(c["source"])),
                      lambda: emit(body("EvidenceAdmitted", **{k: c[k] for k in (
                          "evidenceId", "contentKind", "content", "category", "source", "capturedAt")})))


def _supersede_evidence(S, c):
    errs = collect(found(S, "evidence", c["evidenceId"]), found(S, "evidence", c["replacementEvidenceId"]),
                   text(c["reason"]))
    if errs:
        return Refused(errs)
    if S["evidence"][c["evidenceId"]]["supersededBy"] is not None:
        return Refused(err("EVIDENCE.ALREADY_SUPERSEDED", "STATE-TRANSITION-TABLES §4"))
    if c["evidenceId"] == c["replacementEvidenceId"]:
        return Refused(err("EVIDENCE.SELF", "schema"))
    return emit(body("EvidenceSuperseded", evidenceId=c["evidenceId"],
                     replacementEvidenceId=c["replacementEvidenceId"], reason=c["reason"]))


def _found_then(S, coll, id_, then):
    missing = found(S, coll, id_)
    return Refused(missing) if missing else then(S[coll][id_])


def _record_prediction(S, c):
    def decided_timing():
        hit = any(f["actionId"] in S["actions"] and (
            not S["actions"][f["actionId"]]["targetHypothesisIds"]
            or c["hypothesisId"] in S["actions"][f["actionId"]]["targetHypothesisIds"])
            for f in S["findings"].values())
        return "retrospective" if hit else c["timing"]
    return _refuse_or(collect(absent(S, "predictions", c["predictionId"]), found(S, "hypotheses", c["hypothesisId"]),
                              text(c["statement"])),
                      lambda: emit(body("PredictionRecorded", predictionId=c["predictionId"],
                                        hypothesisId=c["hypothesisId"], statement=c["statement"],
                                        strength=c["strength"], timing=decided_timing())))


def _record_finding(S, c):
    errs = collect(absent(S, "findings", c["findingId"]), found(S, "actions", c["actionId"]),
                   text(c["originalResult"]))
    if errs:
        return Refused(errs)
    state = S["actions"][c["actionId"]]["state"]
    finding = body("FindingRecorded", findingId=c["findingId"], actionId=c["actionId"],
                   originalResult=c["originalResult"], interpretation=opt(c["interpretation"]))
    at = lambda s: body("ActionTransitioned", actionId=c["actionId"], state=s)  # noqa: E731
    if state == "inProgress":
        return emit(finding, at("completed"))
    if state == "approved":
        return emit(at("inProgress"), finding, at("completed"))
    if state == "completed":
        return emit(finding)
    return Refused(err("ACTION.NOT_EXECUTABLE", "STATE-TRANSITION-TABLES §5"))


def _transition_correction(S, c):
    def go(ca):
        errs, target = transition(CORRECTION_TABLE, ca["state"], c["command"])
        if errs:
            return Refused(errs)
        if target == "awaitingVerificationOfEffect" and not criteria_of(S, ca["id"]):
            return Refused(err("CORRECTION.NO_CRITERIA", "R7.3"))
        return emit(body("CorrectiveActionTransitioned", correctiveActionId=ca["id"], state=target))
    return _found_then(S, "correctiveActions", c["correctiveActionId"], go)


def _record_verification(S, env, c):
    """§12.2, §12.3."""
    errs = collect(absent(S, "verifications", c["verificationId"]), found(S, "criteria", c["criterionId"]),
                   all_exist(S, "evidence", c["evidenceIds"]))
    if errs:
        return Refused(errs)
    crit = S["criteria"][c["criterionId"]]
    ca = S["correctiveActions"].get(crit["correctiveActionId"])
    if ca is None:
        # §17 E-2: the oracle throws here; reachable only after an earlier inadmissible group.
        return Refused(err("ORACLE.EXCEPTION", "E-2", "criterion names a missing corrective action"))
    if ca["state"] != "awaitingVerificationOfEffect":
        return Refused(err("VERIFICATION.NOT_AWAITING", "R7.4"))
    if c["outcome"] == "satisfied" and not c["evidenceIds"]:
        return Refused(err("VERIFICATION.EVIDENCE_REQUIRED", "R7.4"))
    if c["outcome"] == "satisfied" and not all(fresh(S, ca["awaitingSinceVersion"], e) for e in c["evidenceIds"]):
        return Refused(err("VERIFICATION.EVIDENCE_PREDATES_ROUND", "R7.4"))
    recorded = body("VerificationRecorded", verificationId=c["verificationId"], criterionId=c["criterionId"],
                    outcome=c["outcome"], evidenceIds=c["evidenceIds"])
    if c["outcome"] == "failed":
        follow = (body("CorrectiveActionTransitioned", correctiveActionId=ca["id"], state="verificationFailed"),)
        if S["lifecycle"] == "awaitingVerification":
            follow += (body("LifecycleChanged", **{"from": "awaitingVerification", "to": "active",
                                                    "command": "verificationFailed",
                                                    "reason": VERIFICATION_FAILED_REASON}),)
        return emit(recorded, *follow)
    if c["outcome"] == "satisfied":
        s_after = apply_event(S, {"eventType": "VerificationRecorded", "payload": recorded.payload,
                                  "aggregateVersion": S["version"] + 1, "actor": env["actor"],
                                  "occurredAt": env["at"], "investigationId": S["investigationId"]})
        if all_satisfied(s_after, ca["id"]):
            return emit(recorded, body("CorrectiveActionTransitioned", correctiveActionId=ca["id"],
                                       state="verifiedEffective"))
    return emit(recorded)


def _record_conclusion(S, c):
    """§13."""
    errs = collect(absent(S, "conclusions", c["conclusionId"]), text(c["statement"]),
                   all_exist(S, "causes", c["causeIds"]))
    if errs:
        return Refused(errs)
    if c["strength"] == "confirmedCause" and not any(S["causes"][x]["state"] == "confirmed" for x in c["causeIds"]):
        return Refused(err("CONCLUSION.UNSUPPORTED", "R6.1"))
    if is_blank(c["residualUncertainty"]) and (
            any(open_unknown(u) and material(u["materiality"]) for u in S["unknowns"].values())
            or any(u["state"] == "acceptedResidual" for u in S["unknowns"].values())):
        return Refused(err("CONCLUSION.RESIDUAL_REQUIRED", "R6.4"))
    return emit(body("ConclusionRecorded", conclusionId=c["conclusionId"], statement=c["statement"],
                     strength=c["strength"], causeIds=c["causeIds"],
                     residualUncertainty=opt(c["residualUncertainty"])))


def _record_proposal(S, env, c):
    """§10.27."""
    errs = collect(absent(S, "proposals", c["proposalId"]), text(c["text"]))
    if errs:
        return Refused(errs)
    if env["actor"].get("kind") == "agent" and c["kind"] == "hypothesis":
        if unicodedata.unidata_version != UNICODE_PIN:
            # str.lower() per code point equals the §3.3 simple mapping (U+0130 excepted) only on the pinned version.
            return Unresolved(f"runtime Unicode {unicodedata.unidata_version} != pinned {UNICODE_PIN} (§3.3, E-5)")
        s = norm(c["text"])
        if any(h["state"] == "falsified" and norm(h["statement"]) == s for h in S["hypotheses"].values()) or any(
                norm(n["statement"]) == s for n in S["negativeKnowledge"].values()):
            return Refused(err("PROPOSAL.REPEATS_NEGATIVE_KNOWLEDGE", "R5.4"))
    return emit(body("ProposalRecorded", proposalId=c["proposalId"], kind=c["kind"], text=c["text"],
                     rationale=opt(c["rationale"])))


def _accept_proposal(S, env, c):
    """§10.29."""
    def go(p):
        if p["state"] != "active":
            return Refused(err("PROPOSAL.NOT_ACTIVE", "STATE-TRANSITION-TABLES §12"))
        if env["actor"].get("kind") == "agent":
            return Refused(err("PROPOSAL.AGENT_CANNOT_ACCEPT", "D-004"))
        tid = c["targetId"]
        target = {"hypothesis": lambda: _record_hypothesis(S, tid, p["text"], None),
                  "unknown": lambda: _identify_unknown(S, tid, p["text"], "material"),
                  "action": lambda: _record_action(S, tid, "test", p["text"], [])}[p["kind"]]()
        if not isinstance(target, Emitted):
            return target
        return Emitted(target.bodies + (body("ProposalWasAccepted", proposalId=p["id"], targetId=tid),))
    return _found_then(S, "proposals", c["proposalId"], go)


def _reject_proposal(S, c):
    errs = collect(found(S, "proposals", c["proposalId"]), text(c["reason"]))
    if errs:
        return Refused(errs)
    if S["proposals"][c["proposalId"]]["state"] != "active":
        return Refused(err("PROPOSAL.NOT_ACTIVE", "STATE-TRANSITION-TABLES §12"))
    return emit(body("ProposalWasRejected", proposalId=c["proposalId"], reason=c["reason"]))


def _simple_transition(S, coll, id_key, table, c, event_type, with_basis):
    def go(entity):
        errs, target = transition(table, entity["state"], c["command"])
        checks = collect(errs, text(c["basis"])) if with_basis else errs
        if checks:
            return Refused(checks)
        extra = {"basis": c["basis"]} if with_basis else {}
        return emit(body(event_type, **{id_key: entity["id"], "state": target, **extra}))
    return _found_then(S, coll, c[id_key], go)


def decide_content(S, env, c):
    """§10 (content commands, run only when the investigation is open)."""
    n = c["name"]
    if n == "RecordObservation":
        return _refuse_or(collect(absent(S, "observations", c["observationId"]), text(c["original"])),
                          lambda: emit(body("ObservationRecorded", observationId=c["observationId"],
                                            original=c["original"], observedAt=c["observedAt"])))
    if n == "RecordHypothesis":
        return _record_hypothesis(S, c["hypothesisId"], c["statement"], c["mechanism"])
    if n == "TransitionHypothesis":
        return _transition_hypothesis(S, env, c)
    if n == "AdmitEvidence":
        return _admit_evidence(S, c)
    if n == "SupersedeEvidence":
        return _supersede_evidence(S, c)
    if n == "VerifyEvidence":
        return _found_then(S, "evidence", c["evidenceId"], lambda e: emit(body(
            "EvidenceVerificationAssessed", evidenceId=e["id"], verification=c["verification"])))
    if n == "RelateEvidence":
        return _refuse_or(collect(absent(S, "relationships", c["relationshipId"]),
                                  found(S, "evidence", c["evidenceId"]), found(S, "hypotheses", c["hypothesisId"])),
                          lambda: emit(body("EvidenceRelated", relationshipId=c["relationshipId"],
                                            evidenceId=c["evidenceId"], hypothesisId=c["hypothesisId"],
                                            kind=c["kind"], rationale=opt(c["rationale"]))))
    if n == "IdentifyUnknown":
        return _identify_unknown(S, c["unknownId"], c["question"], c["materiality"])
    if n == "TransitionUnknown":
        return _simple_transition(S, "unknowns", "unknownId", UNKNOWN_TABLE, c, "UnknownTransitioned", True)
    if n == "RecordAssumption":
        return _refuse_or(collect(absent(S, "assumptions", c["assumptionId"]), text(c["proposition"])),
                          lambda: emit(body("AssumptionRecorded", assumptionId=c["assumptionId"],
                                            proposition=c["proposition"], materiality=c["materiality"])))
    if n == "TransitionAssumption":
        return _simple_transition(S, "assumptions", "assumptionId", ASSUMPTION_TABLE, c, "AssumptionTransitioned",
                                  False)
    if n == "RecordContradiction":
        return _refuse_or(collect(absent(S, "contradictions", c["contradictionId"]), text(c["statement"]),
                                  all_exist(S, "evidence", c["evidenceIds"])),
                          lambda: emit(body("ContradictionRecorded", contradictionId=c["contradictionId"],
                                            statement=c["statement"], evidenceIds=c["evidenceIds"],
                                            materiality=c["materiality"])))
    if n == "TransitionContradiction":
        return _simple_transition(S, "contradictions", "contradictionId", CONTRADICTION_TABLE, c,
                                  "ContradictionTransitioned", True)
    if n == "RecordPrediction":
        return _record_prediction(S, c)
    if n == "RecordAction":
        return _record_action(S, c["actionId"], c["kind"], c["purpose"], c["targetHypothesisIds"])
    if n == "TransitionAction":
        return _simple_transition(S, "actions", "actionId", ACTION_TABLE, c, "ActionTransitioned", False)
    if n == "RecordFinding":
        return _record_finding(S, c)
    if n == "EstablishNegativeKnowledge":
        errs = collect(absent(S, "negativeKnowledge", c["negativeKnowledgeId"]), text(c["statement"]),
                       text(c["scope"]), all_exist(S, "evidence", c["basisEvidenceIds"]))
        if errs:
            return Refused(errs)
        if not c["basisEvidenceIds"]:
            return Refused(err("NEGATIVE.BASIS_REQUIRED", "R5.3"))
        return emit(body("NegativeKnowledgeEstablished", negativeKnowledgeId=c["negativeKnowledgeId"],
                         statement=c["statement"], scope=c["scope"], basisEvidenceIds=c["basisEvidenceIds"]))
    if n == "RecordCausalAssertion":
        errs = collect(absent(S, "causes", c["causeId"]), text(c["outcome"]),
                       all_exist(S, "hypotheses", [m["hypothesisId"] for m in c["members"]]))
        if errs:
            return Refused(errs)
        if not c["members"]:
            return Refused(err("CAUSE.NO_MEMBERS", "R6.3"))
        return emit(body("CausalAssertionRecorded", causeId=c["causeId"], outcome=c["outcome"],
                         members=c["members"], mechanism=opt(c["mechanism"])))
    if n == "ConfirmCause":
        return _refuse_or(cause_blockers(S, c["causeId"]),
                          lambda: emit(body("CausalAssertionConfirmed", causeId=c["causeId"], policy=POLICY_ID)))
    if n == "ReopenCause":
        errs = collect(found(S, "causes", c["causeId"]), text(c["reason"]))
        if errs:
            return Refused(errs)
        if S["causes"][c["causeId"]]["state"] != "confirmed":
            return Refused(err("TRANSITION.ILLEGAL", "STATE-TRANSITION-TABLES §10"))
        return emit(body("CausalAssertionReopened", causeId=c["causeId"], reason=c["reason"]))
    if n == "RecordCorrectiveAction":
        return _refuse_or(collect(absent(S, "correctiveActions", c["correctiveActionId"]), text(c["description"]),
                                  all_exist(S, "causes", c["targetCauseIds"])),
                          lambda: emit(body("CorrectiveActionRecorded", correctiveActionId=c["correctiveActionId"],
                                            kind=c["kind"], description=c["description"],
                                            targetCauseIds=c["targetCauseIds"])))
    if n == "TransitionCorrection":
        return _transition_correction(S, c)
    if n == "RecordCriterion":
        return _refuse_or(collect(absent(S, "criteria", c["criterionId"]),
                                  found(S, "correctiveActions", c["correctiveActionId"]), text(c["expectedOutcome"])),
                          lambda: emit(body("CriterionRecorded", criterionId=c["criterionId"],
                                            correctiveActionId=c["correctiveActionId"],
                                            expectedOutcome=c["expectedOutcome"], method=opt(c["method"]))))
    if n == "RecordVerification":
        return _record_verification(S, env, c)
    if n == "RecordConclusion":
        return _record_conclusion(S, c)
    if n == "RecordProposal":
        return _record_proposal(S, env, c)
    if n == "AcceptProposal":
        return _accept_proposal(S, env, c)
    if n == "RejectProposal":
        return _reject_proposal(S, c)
    if n == "StartInvestigation":
        return Refused(err("INVESTIGATION.EXISTS", "schema"))                                 # §10.30
    return Refused(err("INTERNAL", "decide", f"unknown command {n}"))


# ====================================================================================================================
# §15.3 command reconstruction
# ====================================================================================================================

_UNKNOWN_CMD = {"investigating": "StartInvestigatingUnknown", "resolved": "ResolveUnknown",
                "acceptedResidual": "AcceptUnknownAsResidual"}
_ASSUMPTION_CMD = {"provisionallyAccepted": "ProvisionallyAcceptAssumption", "verified": "VerifyAssumption",
                   "rejected": "RejectAssumption"}
_CONTRADICTION_CMD = {"triaged": "TriageContradiction", "resolved": "ResolveContradiction",
                      "acceptedResidual": "AcceptContradictionAsResidual"}
_ACTION_CMD = {"approved": "ApproveAction", "rejected": "RejectAction", "inProgress": "StartAction",
               "cancelled": "CancelAction"}
_CORRECTION_CMD = {"approved": "ApproveCorrection", "inProgress": "StartCorrection",
                   "implemented": "MarkImplemented", "awaitingVerificationOfEffect": "StartVerificationOfEffect",
                   "reworkRequired": "RequireRework", "cancelled": "CancelCorrection"}

_SIMPLE = {   # eventType -> (command name, payload fields copied)
    "ObservationRecorded": ("RecordObservation", ("observationId", "original", "observedAt")),
    "HypothesisRecorded": ("RecordHypothesis", ("hypothesisId", "statement", "mechanism")),
    "HypothesisTransitioned": ("TransitionHypothesis", ("hypothesisId", "command", "reason", "evidenceIds")),
    "EvidenceAdmitted": ("AdmitEvidence", ("evidenceId", "contentKind", "content", "category", "source",
                                           "capturedAt")),
    "EvidenceSuperseded": ("SupersedeEvidence", ("evidenceId", "replacementEvidenceId", "reason")),
    "EvidenceVerificationAssessed": ("VerifyEvidence", ("evidenceId", "verification")),
    "EvidenceRelated": ("RelateEvidence", ("relationshipId", "evidenceId", "hypothesisId", "kind", "rationale")),
    "UnknownIdentified": ("IdentifyUnknown", ("unknownId", "question", "materiality")),
    "AssumptionRecorded": ("RecordAssumption", ("assumptionId", "proposition", "materiality")),
    "ContradictionRecorded": ("RecordContradiction", ("contradictionId", "statement", "evidenceIds",
                                                      "materiality")),
    "PredictionRecorded": ("RecordPrediction", ("predictionId", "hypothesisId", "statement", "strength", "timing")),
    "ActionRecorded": ("RecordAction", ("actionId", "kind", "purpose", "targetHypothesisIds")),
    "NegativeKnowledgeEstablished": ("EstablishNegativeKnowledge", ("negativeKnowledgeId", "statement", "scope",
                                                                    "basisEvidenceIds")),
    "CausalAssertionRecorded": ("RecordCausalAssertion", ("causeId", "outcome", "members", "mechanism")),
    "CausalAssertionConfirmed": ("ConfirmCause", ("causeId",)),
    "CausalAssertionReopened": ("ReopenCause", ("causeId", "reason")),
    "CorrectiveActionRecorded": ("RecordCorrectiveAction", ("correctiveActionId", "kind", "description",
                                                            "targetCauseIds")),
    "CriterionRecorded": ("RecordCriterion", ("criterionId", "correctiveActionId", "expectedOutcome", "method")),
    "ConclusionRecorded": ("RecordConclusion", ("conclusionId", "statement", "strength", "causeIds",
                                                "residualUncertainty")),
    "ProposalRecorded": ("RecordProposal", ("proposalId", "kind", "text", "rationale")),
    "ProposalWasRejected": ("RejectProposal", ("proposalId", "reason")),
    "FindingRecorded": ("RecordFinding", ("findingId", "actionId", "originalResult", "interpretation")),
    "VerificationRecorded": ("RecordVerification", ("verificationId", "criterionId", "outcome", "evidenceIds")),
    "ProposalWasAccepted": ("AcceptProposal", ("proposalId", "targetId")),
}


def _from_event(e) -> dict:
    name, fields = _SIMPLE[e["eventType"]]
    return {"name": name, **{f: e["payload"][f] for f in fields}}


def command_of(group: list) -> dict | None:
    for t in ("FindingRecorded", "VerificationRecorded", "ProposalWasAccepted"):         # precedence §15.3.1
        first = next((e for e in group if e["eventType"] == t), None)
        if first is not None:
            return _from_event(first)
    e1 = group[0]
    t, p = e1["eventType"], e1["payload"]
    if t == "InvestigationStarted":
        return {"name": "StartInvestigation", "investigationId": e1["investigationId"], "title": p["title"],
                "observationId": p["observationId"], "original": p["original"]}
    if t == "LifecycleChanged":
        cmd = p["command"]
        if cmd in ("markAwaitingEvidence", "reopenInvestigation"):
            return {"name": cmd, "reason": p["reason"]} if p["reason"] is not None else None
        return {"name": cmd} if cmd in LIFECYCLE_COMMANDS else None
    by_state = {"UnknownTransitioned": (_UNKNOWN_CMD, "unknownId", True),
                "AssumptionTransitioned": (_ASSUMPTION_CMD, "assumptionId", False),
                "ContradictionTransitioned": (_CONTRADICTION_CMD, "contradictionId", True),
                "ActionTransitioned": (_ACTION_CMD, "actionId", False),
                "CorrectiveActionTransitioned": (_CORRECTION_CMD, "correctiveActionId", False)}
    if t in by_state:
        table, key, with_basis = by_state[t]
        command = table.get(p["state"])
        if command is None:
            return None
        name = {"UnknownTransitioned": "TransitionUnknown", "AssumptionTransitioned": "TransitionAssumption",
                "ContradictionTransitioned": "TransitionContradiction", "ActionTransitioned": "TransitionAction",
                "CorrectiveActionTransitioned": "TransitionCorrection"}[t]
        return {"name": name, key: p[key], "command": command, **({"basis": p["basis"]} if with_basis else {})}
    if t in _SIMPLE:
        return _from_event(e1)
    return None


# ====================================================================================================================
# §15 History.validate
# ====================================================================================================================

def groups_of(events: list) -> list:
    """§15.1: maximal runs of consecutive events with equal correlationId."""
    def step(acc, e):
        if acc and acc[-1][0]["correlationId"] == e["correlationId"]:
            return acc[:-1] + [acc[-1] + [e]]
        return acc + [[e]]
    return reduce(step, events, [])


def code_for(group: list) -> str:
    e1 = group[0]
    if e1["eventType"] == "CausalAssertionConfirmed":
        return "HISTORY.UNGATED_CONFIRMATION"
    if e1["eventType"] == "LifecycleChanged" and e1["payload"].get("to") == "resolved":
        return "HISTORY.UNGATED_RESOLUTION"
    return "HISTORY.INADMISSIBLE_EVENT"


def _actor_key(a: dict) -> tuple:
    return (a.get("actorId"), a.get("kind"), a.get("provider") if a.get("kind") == "agent" else None,
            a.get("model") if a.get("kind") == "agent" else None)


def _recorded_bodies(group: list) -> tuple:
    return tuple(Body(e["eventType"], e["payload"],
                      (e["occurredAt"], e["aggregateVersion"]) if e["eventType"] == "HypothesisTransitioned" else ())
                 for e in group)


def _bodies_equal(a: tuple, b: tuple) -> bool:
    return len(a) == len(b) and all(x.event_type == y.event_type and x.payload == y.payload
                                    and x.implicit == y.implicit for x, y in zip(a, b))


@dataclass(frozen=True)
class HistoryError:
    code: str
    rule: str
    version: int
    message: str


@dataclass(frozen=True)
class Validation:
    errors: tuple            # of HistoryError, in §15.7 order
    unresolved: tuple        # of (version, reason): platform-defined cases (§17) that were not decided
    state: Any               # final state (replayed regardless of verdict, §15.2 step 4)


def _verdict(S, group: list):
    """§15.2 step 2. Returns (tuple of HistoryError, unresolved reason or None)."""
    e1 = group[0]
    v1 = e1["aggregateVersion"]
    if S is None and e1["eventType"] != "InvestigationStarted":
        return (HistoryError("HISTORY.NO_START", "D-005", v1, "no InvestigationStarted before this group"),), None
    cmd = command_of(group)
    if cmd is None:
        return (HistoryError("HISTORY.INADMISSIBLE_EVENT", "DEC-04", v1, "no command reconstructs this group"),), None
    if any(_actor_key(e["actor"]) != _actor_key(e1["actor"]) or e["occurredAt"] != e1["occurredAt"]
           or e["investigationId"] != e1["investigationId"] for e in group):
        return (HistoryError("HISTORY.INADMISSIBLE_EVENT", "DEC-04", v1, "inconsistent command group"),), None
    env = {"investigationId": e1["investigationId"], "actor": e1["actor"], "at": e1["occurredAt"]}
    r = decide(S, env, cmd)
    if isinstance(r, Unresolved):
        return (), r.reason
    if isinstance(r, Refused):
        code = code_for(group)
        return tuple(HistoryError(code, f.rule, v1, f"Event {v1} would be refused now ({f.code}): {f.message}")
                     for f in r.errors), None
    if not _bodies_equal(r.bodies, _recorded_bodies(group)):
        return (HistoryError("HISTORY.INADMISSIBLE_EVENT", "DEC-04", v1,
                             f"Event {v1}: decided events differ from the recorded events"),), None
    return (), None


def _fold_group(acc: Validation, group: list) -> Validation:
    S = acc.state
    x = 1 if S is None else S["version"] + 1
    seq = tuple(HistoryError("HISTORY.SEQUENCE", "D-005", e["aggregateVersion"],
                             f"expected aggregateVersion {x + i}, found {e['aggregateVersion']}")
                for i, e in enumerate(group) if e["aggregateVersion"] != x + i)
    verdict, unresolved = _verdict(S, group)
    return Validation(acc.errors + seq + verdict,
                      acc.unresolved + (((group[0]["aggregateVersion"], unresolved),) if unresolved else ()),
                      reduce(apply_event, group, S))


def validate_history(events: list) -> Validation:
    """§15: fold over command groups; never stops early. Admissible iff errors == () (and nothing unresolved)."""
    return reduce(_fold_group, groups_of(events), Validation((), (), None))
