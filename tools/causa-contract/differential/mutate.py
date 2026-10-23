#!/usr/bin/env python3
"""Generate structurally valid, semantically mutated Causa v1.0.0 investigations for differential testing.

Every mutant passes reader stages 1-14 by construction (schema-valid events, canonical bytes, contiguous versions,
re-derived event ids, adjacent correlation groups, consistent manifest), so any verdict difference between two
implementations can only come from stage 15 (SEMANTICS.md) or later.  The stale snapshot is kept on purpose: an
admissible mutant therefore stops at stage 17 (CAUSA.SNAPSHOT_MISMATCH), an inadmissible one at stage 15.

Usage: python3 -I mutate.py <causa-data checkout> <out dir> [--seed N] [--per-op K]
"""
from __future__ import annotations

import hashlib
import json
import random
import sys
import uuid
from pathlib import Path
from typing import Any, Callable, Iterable

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from acceptance import jcs  # noqa: E402

EVENT_NS = uuid.UUID("241fefd4-c810-5a3a-9cfa-4bbd968ae1cb")
REQ = Path(__file__).resolve().parents[3] / "requirements" / "causa" / "data" / "v1.0.0"
COMMON = json.loads((REQ / "schemas" / "common.schema.json").read_text("utf-8"))["$defs"]

Events = tuple[dict, ...]
Mutation = Callable[[Events, random.Random], "Events | None"]


def renumber(events: Iterable[dict]) -> Events:
    def one(i: int, e: dict) -> dict:
        v = i + 1
        return {**e, "aggregateVersion": v, "eventId": str(uuid.uuid5(EVENT_NS, f"{e['investigationId']}/{v}/{e['correlationId']}"))}

    return tuple(one(i, e) for i, e in enumerate(events))


def groups(events: Events) -> tuple[tuple[dict, ...], ...]:
    out: list[list[dict]] = []
    for e in events:
        if out and out[-1][-1]["correlationId"] == e["correlationId"]:
            out[-1].append(e)
        else:
            out.append([e])
    return tuple(tuple(g) for g in out)


def flat(gs: Iterable[Iterable[dict]]) -> Events:
    return tuple(e for g in gs for e in g)


def fresh(rng: random.Random) -> str:
    return str(uuid.UUID(int=rng.getrandbits(128), version=4))


def with_payload(e: dict, **changes: Any) -> dict:
    return {**e, "payload": {**e["payload"], **changes}}


# ------------------------------------------------------------------------------------------------- mutation operators


def drop_group(ev: Events, rng: random.Random) -> Events | None:
    gs = groups(ev)
    if len(gs) < 3:
        return None
    k = rng.randrange(1, len(gs))
    return flat(gs[:k] + gs[k + 1:])


def drop_event(ev: Events, rng: random.Random) -> Events | None:
    k = rng.randrange(1, len(ev))
    return ev[:k] + ev[k + 1:]


def truncate(ev: Events, rng: random.Random) -> Events | None:
    return ev[: rng.randrange(1, len(ev))]


def swap_groups(ev: Events, rng: random.Random) -> Events | None:
    gs = list(groups(ev))
    if len(gs) < 4:
        return None
    k = rng.randrange(1, len(gs) - 1)
    gs[k], gs[k + 1] = gs[k + 1], gs[k]
    return flat(gs)


def move_group_later(ev: Events, rng: random.Random) -> Events | None:
    gs = list(groups(ev))
    if len(gs) < 4:
        return None
    k = rng.randrange(1, len(gs) - 1)
    g = gs.pop(k)
    gs.insert(rng.randrange(k + 1, len(gs) + 1), g)
    return flat(gs)


def duplicate_group(ev: Events, rng: random.Random) -> Events | None:
    gs = list(groups(ev))
    k = rng.randrange(1, len(gs))
    cid = fresh(rng)
    copy = tuple({**e, "correlationId": cid} for e in gs[k])
    gs.insert(rng.randrange(k + 1, len(gs) + 1), copy)
    return flat(gs)


ENUM_FIELDS = {
    "kind": None, "state": None, "strength": None, "timing": None, "materiality": None, "outcome": None,
    "category": None, "verification": None, "to": None, "from": None, "command": None, "contentKind": None,
}


def enum_values(event_type: str, field: str) -> tuple[str, ...]:
    schema = json.loads((REQ / "schemas" / "payloads" / f"{event_type}.schema.json").read_text("utf-8"))
    prop = schema["properties"].get(field, {})
    ref = prop.get("$ref", "")
    name = ref.split("/")[-1]
    return tuple(COMMON.get(name, {}).get("enum", ()))


def change_enum(ev: Events, rng: random.Random) -> Events | None:
    candidates = [(i, f) for i, e in enumerate(ev) for f in e["payload"] if f in ENUM_FIELDS and enum_values(e["eventType"], f)]
    if not candidates:
        return None
    i, f = rng.choice(candidates)
    e = ev[i]
    values = [v for v in enum_values(e["eventType"], f) if v != e["payload"][f]]
    changed = with_payload(e, **{f: rng.choice(values)})
    if e["eventType"] == "HypothesisTransitioned" and f == "command":
        target = {"markPlausible": "plausible", "markSupported": "supported", "markWeakened": "weakened", "falsify": "falsified", "supersede": "superseded", "reopen": "plausible"}
        changed = with_payload(changed, to=target[changed["payload"]["command"]])
    return ev[:i] + (changed,) + ev[i + 1:]


ID_FIELDS = ("hypothesisId", "evidenceId", "replacementEvidenceId", "actionId", "causeId", "criterionId", "correctiveActionId", "proposalId", "targetId", "unknownId", "assumptionId", "contradictionId")


def all_ids(ev: Events) -> tuple[str, ...]:
    return tuple(sorted({v for e in ev for k, v in e["payload"].items() if k.endswith("Id") and isinstance(v, str)}))


def retarget_id(ev: Events, rng: random.Random) -> Events | None:
    candidates = [(i, f) for i, e in enumerate(ev) for f in e["payload"] if f in ID_FIELDS]
    if not candidates:
        return None
    i, f = rng.choice(candidates)
    pool = [x for x in all_ids(ev) if x != ev[i]["payload"][f]] + [fresh(rng)]
    return ev[:i] + (with_payload(ev[i], **{f: rng.choice(pool)}),) + ev[i + 1:]


def edit_id_list(ev: Events, rng: random.Random) -> Events | None:
    lists = [(i, f) for i, e in enumerate(ev) for f, v in e["payload"].items() if f.endswith("Ids") and isinstance(v, list)]
    if not lists:
        return None
    i, f = rng.choice(lists)
    current = list(ev[i]["payload"][f])
    op = rng.choice(("empty", "drop", "add", "reverse"))
    new = (
        [] if op == "empty"
        else current[1:] if op == "drop" and current
        else list(reversed(current)) if op == "reverse"
        else current + [x for x in [rng.choice(all_ids(ev) + (fresh(rng),))] if x not in current]
    )
    return ev[:i] + (with_payload(ev[i], **{f: new}),) + ev[i + 1:]


def change_text(ev: Events, rng: random.Random) -> Events | None:
    fixed = [(i, f) for i, e in enumerate(ev) for f in ("policy", "reason", "statement", "text", "purpose", "question", "title") if isinstance(e["payload"].get(f), str)]
    if not fixed:
        return None
    i, f = rng.choice(fixed)
    return ev[:i] + (with_payload(ev[i], **{f: ev[i]["payload"][f] + " (edited)"}),) + ev[i + 1:]


def null_optional(ev: Events, rng: random.Random) -> Events | None:
    nullable = ("mechanism", "residualUncertainty", "rationale", "interpretation", "method", "capturedAt", "observedAt")
    cands = [(i, f) for i, e in enumerate(ev) for f in nullable if e["payload"].get(f) is not None]
    if not cands:
        return None
    i, f = rng.choice(cands)
    return ev[:i] + (with_payload(ev[i], **{f: None}),) + ev[i + 1:]


def change_actor(ev: Events, rng: random.Random) -> Events | None:
    gs = list(groups(ev))
    k = rng.randrange(1, len(gs))
    actor = rng.choice(({"actorId": "mutant-human", "kind": "human"}, {"actorId": "mutant-system", "kind": "system"}))
    if all(e["eventType"] == "ProposalRecorded" for e in gs[k]):
        actor = rng.choice((actor, {"actorId": "mutant-agent", "kind": "agent", "provider": "p", "model": "m"}))
    gs[k] = tuple({**e, "actor": actor} for e in gs[k])
    return flat(gs)


def split_group_time(ev: Events, rng: random.Random) -> Events | None:
    multi = [g for g in groups(ev) if len(g) > 1]
    if not multi:
        return None
    g = rng.choice(multi)
    last = g[-1]
    shifted = {**last, "occurredAt": last["occurredAt"][:-5] + ("999Z" if last["occurredAt"][-4:-1] != "999" else "998Z")}
    return tuple(shifted if e is last else e for e in ev)


OPERATORS: dict[str, Mutation] = {
    "drop_group": drop_group, "drop_event": drop_event, "truncate": truncate, "swap_groups": swap_groups,
    "move_group_later": move_group_later, "duplicate_group": duplicate_group, "change_enum": change_enum,
    "retarget_id": retarget_id, "edit_id_list": edit_id_list, "change_text": change_text,
    "null_optional": null_optional, "change_actor": change_actor, "split_group_time": split_group_time,
}


# ------------------------------------------------------------------------------------------------- materialization


def structurally_valid(ev: Events, validator: Any) -> bool:
    cids = [g[0]["correlationId"] for g in groups(ev)]
    return (
        bool(ev)
        and ev[0]["eventType"] == "InvestigationStarted"
        and len(cids) == len(set(cids))
        and all(not list(validator.iter_errors(e)) for e in ev)
    )


def write_investigation(out: Path, ev: Events, snapshot_bytes: bytes, manifest: dict) -> None:
    out.mkdir(parents=True, exist_ok=True)
    events_bytes = "".join(jcs(e) + "\n" for e in ev).encode()
    snapshot = json.loads(snapshot_bytes)
    new_manifest = {
        **manifest,
        "aggregateVersion": len(ev),
        "eventCount": len(ev),
        "eventsSha256": hashlib.sha256(events_bytes).hexdigest(),
        "snapshotSha256": hashlib.sha256(jcs(snapshot).encode()).hexdigest(),
        "createdAt": ev[0]["occurredAt"],
        "updatedAt": ev[-1]["occurredAt"],
    }
    (out / "events.ndjson").write_bytes(events_bytes)
    (out / "snapshot.json").write_bytes(snapshot_bytes)
    (out / "manifest.json").write_bytes((jcs(new_manifest) + "\n").encode())


def main(argv: list[str]) -> int:
    from jsonschema import Draft202012Validator
    from referencing import Registry, Resource

    dataset, out = Path(argv[1]), Path(argv[2])
    seed = int(argv[argv.index("--seed") + 1]) if "--seed" in argv else 20261023
    per_op = int(argv[argv.index("--per-op") + 1]) if "--per-op" in argv else 12
    schemas = [json.loads(p.read_text("utf-8")) for p in (REQ / "schemas").rglob("*.schema.json")]
    registry = Registry().with_resources((s["$id"], Resource.from_contents(s)) for s in schemas)
    envelope = next(s for s in schemas if s["$id"].endswith("/event-envelope.schema.json"))
    validator = Draft202012Validator(envelope, registry=registry)
    rng = random.Random(seed)
    index = []
    for inv in sorted((dataset / "fixtures" / "v1.0.0" / "investigations").iterdir()):
        base = tuple(json.loads(line) for line in (inv / "events.ndjson").read_text("utf-8").splitlines())
        manifest = json.loads((inv / "manifest.json").read_text("utf-8"))
        snapshot_bytes = (inv / "snapshot.json").read_bytes()
        for name, op in OPERATORS.items():
            made = 0
            for attempt in range(per_op * 8):
                if made >= per_op:
                    break
                mutated = op(base, rng)
                if mutated is None:
                    break
                ev = renumber(mutated)
                if ev == base or not structurally_valid(ev, validator):
                    continue
                label = f"{inv.name[:8]}-{name}-{made:02d}"
                write_investigation(out / label / inv.name, ev, snapshot_bytes, manifest)
                (out / label / "events.ndjson").write_bytes((out / label / inv.name / "events.ndjson").read_bytes())
                index.append({"label": label, "base": inv.name, "operator": name, "events": len(ev)})
                made += 1
    (out / "index.json").write_text(json.dumps(index, indent=1) + "\n", "utf-8")
    print(f"generated {len(index)} mutants")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
