#!/usr/bin/env python3
"""Clean-room Causa storage v1.0.0 consumer (ACCEPTANCE.md §3 consumer protocol).

  causa_clean.py info               --dataset <root>
  causa_clean.py verify             --dataset <root> --investigation <dir>
  causa_clean.py negative           --dataset <root> --fixture <file>
  causa_clean.py append-observation --dir <dir> --expected-version N --observation-id <uuid> --original <text>
                                    --occurred-at <timestamp> --actor-id <text> --actor-kind human|system
                                    --correlation-id <uuid>

The last stdout line is always one JSON object.  All I/O lives in this file; reader/semantics/canon are pure.
"""
from __future__ import annotations

import argparse
import base64
import datetime
import json
import os
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # -I drops the script directory from sys.path

from canon import Failure, canonical, event_id, file_bytes, is_canonical_uuid, is_failure, parse_json  # noqa: E402
from reader import append_lines, rebuild, successor, verify  # noqa: E402
from schemas import first_violation, load_schemas  # noqa: E402
from semantics import Refused, Unresolved, decide, replay  # noqa: E402

FILES = ("manifest.json", "events.ndjson", "snapshot.json")


# --------------------------------------------------------------------------------------------------------------------
# I/O helpers (impure edge)
# --------------------------------------------------------------------------------------------------------------------

def read_dir(path: str) -> dict:
    """Every directory entry counts (CONTRACT §3.3); non-regular entries map to None."""
    entries = sorted(os.listdir(path))
    return {n: (open(os.path.join(path, n), "rb").read()
                if os.path.isfile(os.path.join(path, n)) and not os.path.islink(os.path.join(path, n)) else None)
            for n in entries}


def load_root_manifest(dataset: str, schemas):
    path = os.path.join(dataset, "fixtures", "v1.0.0", "manifest.json")
    data = open(path, "rb").read()
    doc = parse_json(data.decode("utf-8")[:-1]) if data.endswith(b"\n") else Failure("CAUSA.ENCODING", path)
    if is_failure(doc):
        return doc
    if canonical(doc) + "\n" != data.decode("utf-8"):
        return Failure("CAUSA.NON_CANONICAL", "root manifest")
    msg = first_violation(schemas.root_manifest, doc)
    if msg:
        return Failure("CAUSA.SCHEMA_VIOLATION", f"root manifest: {msg}")
    if doc["schemaSha256"] != schemas.identity:
        return Failure("CAUSA.FIXTURE_DRIFT", "root manifest schemaSha256 != schema identity")
    return doc


def scenario_dir(dataset: str, root: dict, key: str):
    entry = next((i for i in root["investigations"] if i["scenarioKey"] == key), None)
    if entry is None:
        return Failure("CAUSA.FIXTURE_DRIFT", f"unknown scenario {key!r}")
    return entry["investigationId"], os.path.join(dataset, "fixtures", "v1.0.0", "investigations",
                                                  entry["investigationId"])


def emit(obj: dict) -> int:
    print(json.dumps(obj, ensure_ascii=True))
    return 0


def failure_json(f: Failure, **extra) -> dict:
    return {"primaryCode": f.code, "domainCode": f.domain, "admissibility": "evaluated", "stateSha256": None,
            "eventsSha256": None, "manifestSha256": None, "snapshotSha256": None, "version": None,
            "lifecycle": None, "detail": f.detail, **extra}


# --------------------------------------------------------------------------------------------------------------------
# Commands
# --------------------------------------------------------------------------------------------------------------------

def cmd_info(args, schemas) -> int:
    return emit({"schemasParsed": schemas.parsed, "schemaIdentity": schemas.identity,
                 "eventTypes": list(schemas.event_types)})


def cmd_verify(args, schemas) -> int:
    path = os.path.normpath(args.investigation)
    return emit(verify(os.path.basename(path), read_dir(path), schemas).as_json())


def decode_files(files: dict) -> dict:
    return {name: base64.b64decode(entry["base64"], validate=True) for name, entry in files.items()}


def cmd_negative(args, schemas) -> int:
    raw = open(args.fixture, "rb").read()
    fx = parse_json(raw.decode("utf-8")[:-1])
    if is_failure(fx):
        return emit(failure_json(fx))
    kind = fx.get("kind")
    if kind == "investigation":
        return emit(verify(fx["directoryId"], decode_files(fx["files"]), schemas).as_json())
    root = load_root_manifest(args.dataset, schemas)
    if is_failure(root):
        return emit(failure_json(root))
    loc = scenario_dir(args.dataset, root, fx["baseScenarioKey"])
    if is_failure(loc):
        return emit(failure_json(loc))
    iid, path = loc
    base = read_dir(path)
    if kind == "append":
        appended = base64.b64decode(fx["appendedEvents"]["base64"], validate=True)
        return emit(append_lines(iid, base, fx["expectedAggregateVersion"], appended, schemas).as_json())
    if kind == "successor":
        return emit(successor(iid, base, decode_files(fx["files"]), schemas).as_json())
    return emit(failure_json(Failure("CAUSA.SCHEMA_VIOLATION", f"unknown negative fixture kind {kind!r}")))


def normalize_instant(text: str):
    """Command boundary (CONTRACT §2.9): accept RFC 3339, keep the instant, refuse sub-millisecond precision."""
    try:
        dt = datetime.datetime.fromisoformat(text.replace("z", "Z").replace("Z", "+00:00"))
    except ValueError:
        return Failure("CAUSA.UNREPRESENTABLE", f"not an RFC 3339 instant: {text!r}")
    if dt.tzinfo is None:
        return Failure("CAUSA.UNREPRESENTABLE", f"instant without offset: {text!r}")
    if dt.microsecond % 1000:
        return Failure("CAUSA.UNREPRESENTABLE", "sub-millisecond instant (writers refuse rather than truncate)")
    frac = text.split(".", 1)[1] if "." in text else ""
    digits = "".join(c for c in frac if c.isdigit())
    if digits.rstrip("0") and len(digits.rstrip("0")) > 3:
        return Failure("CAUSA.UNREPRESENTABLE", "sub-millisecond instant (writers refuse rather than truncate)")
    utc = dt.astimezone(datetime.timezone.utc)
    return utc.strftime("%Y-%m-%dT%H:%M:%S.") + f"{utc.microsecond // 1000:03d}Z"


def unrepresentable_text(*texts: str):
    bad = next((t for t in texts if "﻿" in t or any(0xD800 <= ord(c) <= 0xDFFF for c in t)), None)
    return Failure("CAUSA.UNREPRESENTABLE", "text contains U+FEFF or a lone surrogate") if bad is not None else None


def plan_observation_append(dir_name: str, files: dict, args, schemas):
    """Pure: decide RecordObservation (SEMANTICS §10.1) and produce the new file set, or a Failure."""
    current = verify(dir_name, files, schemas)
    if current.failure or current.admissibility != "evaluated":
        return current.failure or Failure("CAUSA.HISTORY_INADMISSIBLE", current.detail)
    version = current.state["version"]
    if args.expected_version != version:                                                     # §9.2 (1)
        return Failure("CAUSA.VERSION_STALE", f"expected version {args.expected_version} != current {version}")
    at = normalize_instant(args.occurred_at)
    if is_failure(at):
        return at
    bad = unrepresentable_text(args.original, args.actor_id)
    if bad:
        return bad
    if not (is_canonical_uuid(args.observation_id) and is_canonical_uuid(args.correlation_id)):
        return Failure("CAUSA.UNREPRESENTABLE", "ids must be lowercase canonical non-nil UUIDs")
    actor = {"actorId": args.actor_id, "kind": args.actor_kind}
    events = list(current.events)
    state = replay(events)
    env = {"investigationId": dir_name, "actor": actor, "at": at}
    cmd = {"name": "RecordObservation", "observationId": args.observation_id, "original": args.original,
           "observedAt": None}
    decision = decide(state, env, cmd, expected_version=args.expected_version)
    if isinstance(decision, Refused):
        codes = "; ".join(f"{e.code} ({e.rule})" for e in decision.errors)
        return Failure("CAUSA.HISTORY_INADMISSIBLE", f"command refused: {codes}", "HISTORY.INADMISSIBLE_EVENT")
    if isinstance(decision, Unresolved):
        return Failure("CAUSA.HISTORY_INADMISSIBLE", f"decision not evaluable: {decision.reason}")
    new_events = [{"formatVersion": "1.0.0",
                   "eventId": event_id(dir_name, version + 1 + i, args.correlation_id),
                   "investigationId": dir_name, "aggregateVersion": version + 1 + i, "eventType": b.event_type,
                   "occurredAt": at, "actor": actor, "semanticVersion": "1.0.0",
                   "correlationId": args.correlation_id, "payload": b.payload}
                  for i, b in enumerate(decision.bodies)]
    appended = "".join(canonical(e) + "\n" for e in new_events).encode("utf-8")
    new_files = rebuild(dir_name, events + new_events, files["events.ndjson"] + appended)
    check = verify(dir_name, new_files, schemas)                    # §9.2: verify exactly as a reader would
    if check.failure or check.admissibility != "evaluated":
        return check.failure or Failure("CAUSA.HISTORY_INADMISSIBLE", check.detail)
    return new_files, check.state["version"]


def write_unit(path: str, new_files: dict) -> None:
    """Impure: stage all three files next to the directory, fsync, then rename into place (events last-but-first:
    events, snapshot, manifest).  A local filesystem offers no multi-file atomic commit (see REQUIREMENTS-GAPS.md)."""
    parent = os.path.dirname(path)
    staged = {}
    for name in ("events.ndjson", "snapshot.json", "manifest.json"):
        fd, tmp = tempfile.mkstemp(prefix=f".{os.path.basename(path)}.{name}.", dir=parent)
        with os.fdopen(fd, "wb") as fh:
            fh.write(new_files[name])
            fh.flush()
            os.fsync(fh.fileno())
        staged[name] = tmp
    for name, tmp in staged.items():
        os.replace(tmp, os.path.join(path, name))
    dfd = os.open(path, os.O_RDONLY)
    try:
        os.fsync(dfd)
    finally:
        os.close(dfd)


def immutable_target(path: str):
    """CONTRACT §3.4: writers MUST NOT create, modify or delete anything under fixtures/v1.0.0/ or contract/v1.0.0/."""
    parts = os.path.realpath(path).split(os.sep)
    frozen = any(parts[i] in ("fixtures", "contract") and parts[i + 1] == "v1.0.0" for i in range(len(parts) - 1))
    return Failure("CAUSA.PATH_INVALID", "refusing to write under an immutable v1.0.0 tree (CONTRACT §3.4)") \
        if frozen else None


def cmd_append_observation(args, schemas) -> int:
    path = os.path.normpath(args.dir)
    dir_name = os.path.basename(path)
    files = read_dir(path)
    plan = immutable_target(path) or plan_observation_append(dir_name, files, args, schemas)
    if is_failure(plan):
        return emit({"primaryCode": plan.code, "domainCode": plan.domain, "version": None, "detail": plan.detail})
    new_files, new_version = plan
    write_unit(path, new_files)
    reread = read_dir(path)
    if any(reread.get(n) != new_files[n] for n in FILES):
        return emit({"primaryCode": "CAUSA.WRITE_UNCONFIRMED", "domainCode": None, "version": None,
                     "detail": "re-read after write did not return the written bytes"})
    return emit({"primaryCode": None, "domainCode": None, "version": new_version, "detail": "appended"})


# --------------------------------------------------------------------------------------------------------------------

def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="causa_clean")
    sub = p.add_subparsers(dest="command", required=True)
    i = sub.add_parser("info")
    i.add_argument("--dataset")
    v = sub.add_parser("verify")
    v.add_argument("--dataset")
    v.add_argument("--investigation", required=True)
    n = sub.add_parser("negative")
    n.add_argument("--dataset", required=True)
    n.add_argument("--fixture", required=True)
    a = sub.add_parser("append-observation")
    a.add_argument("--dir", required=True)
    a.add_argument("--expected-version", type=int, required=True)
    a.add_argument("--observation-id", required=True)
    a.add_argument("--original", required=True)
    a.add_argument("--occurred-at", required=True)
    a.add_argument("--actor-id", required=True)
    a.add_argument("--actor-kind", choices=("human", "system"), required=True)
    a.add_argument("--correlation-id", required=True)
    return p


def main(argv: list[str]) -> int:
    args = parser().parse_args(argv)
    schemas = load_schemas()
    if is_failure(schemas):
        return emit(failure_json(schemas))
    handler = {"info": cmd_info, "verify": cmd_verify, "negative": cmd_negative,
               "append-observation": cmd_append_observation}[args.command]
    return handler(args, schemas)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
