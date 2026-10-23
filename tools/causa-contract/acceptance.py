#!/usr/bin/env python3
"""Causa v1.0.0 acceptance suite runner (requirements/causa/data/v1.0.0/acceptance/ACCEPTANCE.md).

Drives any consumer that implements the consumer protocol (ACCEPTANCE.md §3) through every row of TEST-MATRIX.json.

Usage:
  python3 -I tools/causa-contract/acceptance.py run --consumer "<command prefix>" --causa-data <checkout at the tag>
                                                    [--only <row id prefix>] [--json <report path>]

A row passes only when the consumer reports exactly the expected values.  A consumer answer of
`"admissibility": "not-evaluated"` is NOT_EVALUATED, never PASS, and the suite verdict is then not PASS.
"""
from __future__ import annotations

import hashlib
import json
import shlex
import shutil
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path
from typing import Any, Callable, Mapping, NamedTuple

TOOLS = Path(__file__).resolve().parent
REQ_ROOT = TOOLS.parents[1] / "requirements" / "causa" / "data" / "v1.0.0"
EVENT_NS = uuid.UUID("241fefd4-c810-5a3a-9cfa-4bbd968ae1cb")
APPEND_BASE = "api-latency"


class Outcome(NamedTuple):
    row: str
    status: str  # PASS | FAIL | NOT_EVALUATED
    detail: str


# ------------------------------------------------------------------------------------------------- independent checks


def jcs(value: Any) -> str:
    """RFC 8785 canonical text for the JSON subset Causa v1 allows (no floats)."""
    if value is None or isinstance(value, bool):
        return json.dumps(value)
    if isinstance(value, int):
        return str(value)
    if isinstance(value, str):
        return jcs_string(value)
    if isinstance(value, list):
        return "[" + ",".join(jcs(v) for v in value) + "]"
    if isinstance(value, dict):
        keys = sorted(value, key=lambda k: k.encode("utf-16-be"))
        return "{" + ",".join(jcs_string(k) + ":" + jcs(value[k]) for k in keys) + "}"
    raise TypeError(f"not representable in Causa v1 JSON: {type(value).__name__}")


def jcs_string(s: str) -> str:
    escapes = {'"': '\\"', "\\": "\\\\", "\b": "\\b", "\f": "\\f", "\n": "\\n", "\r": "\\r", "\t": "\\t"}
    return '"' + "".join(escapes.get(c) or (f"\\u{ord(c):04x}" if ord(c) < 0x20 else c) for c in s) + '"'


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def structural_check(directory: Path, schema_root: Path) -> tuple[str, ...]:
    """Independent structural reader (stages 1-4, 7-14, 16) used to cross-check a consumer's writes."""
    from jsonschema import Draft202012Validator
    from referencing import Registry, Resource

    names = sorted(p.name for p in directory.iterdir())
    if names != ["events.ndjson", "manifest.json", "snapshot.json"]:
        return (f"directory entries {names}",)
    raw = {n: (directory / n).read_bytes() for n in names}
    lines = raw["events.ndjson"].decode("utf-8").split("\n")
    problems: list[str] = []
    if lines[-1] != "" or any(not ln for ln in lines[:-1]):
        problems.append("events.ndjson framing")
    events = [json.loads(ln) for ln in lines[:-1]]
    problems += [f"event {i + 1} not canonical" for i, (ln, e) in enumerate(zip(lines, events)) if jcs(e) != ln]
    manifest = json.loads(raw["manifest.json"])
    snapshot = json.loads(raw["snapshot.json"])
    problems += [f"{n} not canonical" for n, v in (("manifest.json", manifest), ("snapshot.json", snapshot)) if raw[n] != (jcs(v) + "\n").encode()]
    schemas = {p.relative_to(schema_root).as_posix(): json.loads(p.read_text("utf-8")) for p in schema_root.rglob("*.schema.json")}
    registry = Registry().with_resources((s["$id"], Resource.from_contents(s)) for s in schemas.values())
    validate = lambda name, doc: [f"{name}: {e.message}" for e in Draft202012Validator(schemas[name], registry=registry).iter_errors(doc)]
    problems += validate("investigation-manifest.schema.json", manifest) + validate("snapshot.schema.json", snapshot)
    problems += [p for e in events for p in validate("event-envelope.schema.json", e)]
    n = len(events)
    expected_ids = [str(uuid.uuid5(EVENT_NS, f"{e['investigationId']}/{e['aggregateVersion']}/{e['correlationId']}")) for e in events]
    checks = (
        (manifest.get("aggregateVersion") == n and manifest.get("eventCount") == n, "manifest counts"),
        (manifest.get("investigationId") == directory.name, "manifest id vs directory"),
        (manifest.get("createdAt") == events[0]["occurredAt"] and manifest.get("updatedAt") == events[-1]["occurredAt"], "manifest times"),
        (manifest.get("eventsSha256") == sha256(raw["events.ndjson"]), "eventsSha256"),
        (all(e["investigationId"] == directory.name for e in events), "event investigation ids"),
        (len(set(e["eventId"] for e in events)) == n, "eventId uniqueness"),
        ([e["aggregateVersion"] for e in events] == list(range(1, n + 1)), "aggregate versions 1..N"),
        ([e["eventId"] for e in events] == expected_ids, "eventId derivation"),
        (manifest.get("snapshotSha256") == sha256(jcs(snapshot).encode()), "snapshotSha256"),
        (snapshot.get("aggregateVersion") == n and snapshot.get("generatedAt") == events[-1]["occurredAt"], "snapshot version/generatedAt"),
    )
    return tuple(problems) + tuple(label for ok, label in checks if not ok)


# ------------------------------------------------------------------------------------------------- consumer protocol


def call(consumer: str, *args: str) -> Mapping[str, Any]:
    proc = subprocess.run([*shlex.split(consumer), *args], capture_output=True, text=True, timeout=600)
    try:
        return json.loads(proc.stdout.strip().splitlines()[-1])
    except (ValueError, IndexError):
        return {"protocolError": f"exit {proc.returncode}; stdout={proc.stdout[-400:]!r}; stderr={proc.stderr[-400:]!r}"}


def compare(row: str, expected: Mapping[str, Any], actual: Mapping[str, Any]) -> Outcome:
    if "protocolError" in actual:
        return Outcome(row, "FAIL", actual["protocolError"])
    diffs = [f"{k}: expected {v!r}, got {actual.get(k)!r}" for k, v in expected.items() if actual.get(k) != v]
    if actual.get("admissibility") == "not-evaluated" and expected.get("admissibility") == "evaluated":
        return Outcome(row, "NOT_EVALUATED", "; ".join(diffs) or "admissibility not evaluated")
    return Outcome(row, "FAIL" if diffs else "PASS", "; ".join(diffs) or actual.get("detail", ""))


def tool(row: str, script: str, *args: str) -> Outcome:
    proc = subprocess.run([sys.executable, "-I", str(TOOLS / script), *args], capture_output=True, text=True)
    last = (proc.stdout.strip().splitlines() or [""])[-1]
    return Outcome(row, "PASS" if proc.returncode == 0 else "FAIL", last)


def append_rows(consumer: str, dataset: Path, base_dir: Path, base_version: int) -> tuple[Outcome, Outcome]:
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp) / "workspaces" / "demo" / "acceptance" / "investigations" / base_dir.name
        shutil.copytree(base_dir, work)
        before = {p.name: p.read_bytes() for p in work.iterdir()}
        last = json.loads(before["events.ndjson"].decode().splitlines()[-1])
        stale = call(
            consumer, "append-observation", "--dir", str(work), "--expected-version", str(base_version - 1),
            "--observation-id", "9b1c2d6e-0000-4000-8000-0000000000a1", "--original", "Acceptance stale write",
            "--occurred-at", "2026-10-23T12:00:00.000Z", "--actor-id", "acceptance-suite", "--actor-kind", "human",
            "--correlation-id", "9b1c2d6e-0000-4000-8000-0000000000c1",
        )
        unchanged = {p.name: p.read_bytes() for p in work.iterdir()} == before
        stale_out = Outcome(
            "AC-APPEND-02",
            "PASS" if stale.get("primaryCode") == "CAUSA.VERSION_STALE" and unchanged else "FAIL",
            f"primaryCode={stale.get('primaryCode')!r} bytesUnchanged={unchanged}",
        )
        fresh = call(
            consumer, "append-observation", "--dir", str(work), "--expected-version", str(base_version),
            "--observation-id", "9b1c2d6e-0000-4000-8000-0000000000a2", "--original", "Acceptance write: latency normal after deploy",
            "--occurred-at", "2026-10-23T12:00:00.000Z", "--actor-id", last["actor"]["actorId"], "--actor-kind", last["actor"]["kind"],
            "--correlation-id", "9b1c2d6e-0000-4000-8000-0000000000c2",
        )
        after = {p.name: p.read_bytes() for p in work.iterdir()}
        prefix = after.get("events.ndjson", b"").startswith(before["events.ndjson"])
        added = after.get("events.ndjson", b"")[len(before["events.ndjson"]):].decode("utf-8", "replace").splitlines()
        reverify = call(consumer, "verify", "--dataset", str(dataset), "--investigation", str(work))
        structural = structural_check(work, REQ_ROOT / "schemas") if "events.ndjson" in after else ("missing files",)
        new_event = json.loads(added[0]) if added else {}
        problems = [
            *([] if fresh.get("primaryCode") is None and "protocolError" not in fresh else [f"append answered {fresh}"]),
            *([] if prefix else ["old events.ndjson bytes are not a prefix"]),
            *([] if len(added) == 1 else [f"{len(added)} lines appended"]),
            *([] if new_event.get("eventType") == "ObservationRecorded" and new_event.get("aggregateVersion") == base_version + 1 else ["unexpected appended event"]),
            *([] if reverify.get("primaryCode") is None and reverify.get("admissibility") == "evaluated" else [f"consumer re-verify {reverify.get('primaryCode')} {reverify.get('admissibility')}"]),
            *(f"independent reader: {p}" for p in structural),
        ]
        return Outcome("AC-APPEND-01", "FAIL" if problems else "PASS", "; ".join(problems) or f"version {base_version} -> {base_version + 1}, prefix preserved, independent reader accepts"), stale_out


# ------------------------------------------------------------------------------------------------- runner


def run(consumer: str, dataset: Path, only: str) -> tuple[Outcome, ...]:
    matrix = json.loads((REQ_ROOT / "acceptance" / "TEST-MATRIX.json").read_text("utf-8"))
    manifest = json.loads((dataset / "fixtures" / "v1.0.0" / "manifest.json").read_text("utf-8"))
    base = next(i for i in manifest["investigations"] if i["scenarioKey"] == APPEND_BASE)
    base_dir = dataset / "fixtures" / "v1.0.0" / "investigations" / base["investigationId"]
    info: Callable[[], Mapping[str, Any]] = lambda: call(consumer, "info", "--dataset", str(dataset))
    append_cache: dict[str, Outcome] = {}

    def append_outcome(row_id: str) -> Outcome:
        if not append_cache:
            a, b = append_rows(consumer, dataset, base_dir, base["expectedVersion"])
            append_cache.update({a.row: a, b.row: b})
        return append_cache[row_id]

    def evaluate(row: Mapping[str, Any]) -> Outcome:
        rid, exp = row["id"], row["expected"]
        if rid == "AC-SCHEMA-01":
            return compare(rid, exp, info())
        if rid == "AC-REG-01":
            return compare(rid, exp, info())
        if rid.startswith("AC-INV-"):
            return compare(rid, exp, call(consumer, "verify", "--dataset", str(dataset), "--investigation", str(dataset / row["input"])))
        if rid.startswith("AC-NEG-"):
            return compare(rid, exp, call(consumer, "negative", "--dataset", str(dataset), "--fixture", str(dataset / row["input"])))
        if rid.startswith("AC-APPEND-"):
            return append_outcome(rid)
        if rid == "AC-PARITY-01":
            return tool(rid, "parity_gate.py", "check", "--causa-data", str(dataset))
        if rid == "AC-PARITY-02":
            return tool(rid, "parity_gate.py", "selftest", "--causa-data", str(dataset))
        if rid == "AC-CATALOG-01":
            return tool(rid, "generate_catalogs.py", "check", str(REQ_ROOT), "--dataset", str(dataset))
        return Outcome(rid, "FAIL", "unknown matrix row")

    return tuple(evaluate(r) for r in matrix["rows"] if r["id"].startswith(only))


def main(argv: list[str]) -> int:
    if len(argv) < 2 or argv[1] != "run" or "--consumer" not in argv or "--causa-data" not in argv:
        print(__doc__)
        return 2
    consumer = argv[argv.index("--consumer") + 1]
    dataset = Path(argv[argv.index("--causa-data") + 1])
    only = argv[argv.index("--only") + 1] if "--only" in argv else ""
    outcomes = run(consumer, dataset, only)
    for o in outcomes:
        print(f"{o.status} {o.row} {o.detail}".rstrip())
    counts = {s: sum(1 for o in outcomes if o.status == s) for s in ("PASS", "FAIL", "NOT_EVALUATED")}
    verdict = "PASS" if counts["PASS"] == len(outcomes) else "FAIL"
    print(f"{verdict} acceptance {counts['PASS']}/{len(outcomes)} passed, {counts['FAIL']} failed, {counts['NOT_EVALUATED']} not evaluated")
    if "--json" in argv:
        Path(argv[argv.index("--json") + 1]).write_text(json.dumps({"verdict": verdict, "counts": counts, "rows": [o._asdict() for o in outcomes]}, indent=1) + "\n", "utf-8")
    return 0 if verdict == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
