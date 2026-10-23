"""Command/write path: append-observation.

Implements CONTRACT §9.2 (optimistic append, verify before success), §2.9 / CAUSA-CLAR-09 (command-boundary
timestamps), CAUSA-CLAR-03 (write-path codes and their precedence), CAUSA-CLAR-04 (local atomic publish through
sibling directories, plus recovery) and CONTRACT §3.4 (frozen trees).

`plan_*` functions are pure.  `publish`, `recover` and `read_dir` are the only functions that touch the filesystem.
"""
from __future__ import annotations

import datetime
import os
import re

from canon import Failure, canonical, event_id, is_blank, is_canonical_uuid, is_failure
from reader import FILES, rebuild, verify
from semantics import Refused, Unresolved, decide, replay

RUN_ID = re.compile(r"[a-z0-9][a-z0-9-]{0,63}\Z")              # CONTRACT §3.2
FROZEN_TREES = ("fixtures", "contract", "compatibility")       # CAUSA-CLAR-03 / CONTRACT §3.4
RFC3339 = re.compile(r"([0-9]{4})-([0-9]{2})-([0-9]{2})[Tt]([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]+))?"
                     r"([Zz]|[+-][0-9]{2}:[0-9]{2})\Z")


# ====================================================================================================================
# Pure checks (CLAR-03 precedence: path, version, representability, decide, verification, confirmation)
# ====================================================================================================================

def path_check(real_path: str):
    """CAUSA.PATH_FROZEN unless the target is <...>/workspaces/demo/<runId>/investigations/<dir>, outside frozen trees."""
    parts = real_path.split(os.sep)
    frozen = any(parts[i] in FROZEN_TREES and parts[i + 1] == "v1.0.0" for i in range(len(parts) - 1))
    in_workspace = (len(parts) >= 5 and parts[-5] == "workspaces" and parts[-4] == "demo"
                    and bool(RUN_ID.match(parts[-3])) and parts[-2] == "investigations")
    if frozen or not in_workspace:
        return Failure("CAUSA.PATH_FROZEN", "writes go only to workspaces/demo/<runId>/investigations/<id>, never "
                                            "under a frozen v1.0.0 tree (CAUSA-CLAR-03, CONTRACT §3.4)")
    return None


def normalize_instant(text: str):
    """CAUSA-CLAR-09: RFC 3339 with Z or numeric offset -> UTC YYYY-MM-DDTHH:MM:SS.mmmZ, else UNREPRESENTABLE."""
    unrep = lambda why: Failure("CAUSA.UNREPRESENTABLE", f"--occurred-at {text!r}: {why}")  # noqa: E731
    m = RFC3339.match(text)
    if not m:
        return unrep("not an RFC 3339 instant with Z or a numeric offset")
    y, mo, d, h, mi, s, frac, off = m.groups()
    frac = frac or ""
    if frac[3:].strip("0"):
        return unrep("sub-millisecond precision (writers refuse rather than truncate)")
    ms = int((frac + "000")[:3])
    sign, oh, om = (1, 0, 0) if off in ("Z", "z") else (1 if off[0] == "+" else -1, int(off[1:3]), int(off[4:6]))
    if oh > 23 or om > 59:
        return unrep("invalid offset")
    try:
        local = datetime.datetime(int(y), int(mo), int(d), int(h), int(mi), int(s), ms * 1000,
                                  tzinfo=datetime.timezone(sign * datetime.timedelta(hours=oh, minutes=om)))
        utc = local.astimezone(datetime.timezone.utc)
    except (ValueError, OverflowError):
        return unrep("not a calendar-valid instant in years 0001-9999")
    return (f"{utc.year:04d}-{utc.month:02d}-{utc.day:02d}T{utc.hour:02d}:{utc.minute:02d}:{utc.second:02d}."
            f"{utc.microsecond // 1000:03d}Z")


def _unwritable_text(t: str) -> bool:
    return "﻿" in t or any(0xD800 <= ord(c) <= 0xDFFF for c in t)


def representability(args):
    """CLAR-03 'a value has no v1 wire form'. Returns (occurredAt, actor) or a Failure."""
    at = normalize_instant(args.occurred_at)
    if is_failure(at):
        return at
    if _unwritable_text(args.original) or _unwritable_text(args.actor_id):
        return Failure("CAUSA.UNREPRESENTABLE", "text contains U+FEFF or a lone surrogate (CONTRACT §2.3)")
    if not (is_canonical_uuid(args.observation_id) and is_canonical_uuid(args.correlation_id)):
        return Failure("CAUSA.UNREPRESENTABLE", "ids must be lowercase canonical non-nil UUIDs (CONTRACT §2.8)")
    if args.actor_kind not in ("human", "system"):
        return Failure("CAUSA.UNREPRESENTABLE", "actor kind must be human or system (an agent needs provider/model "
                                                "and may author only ProposalRecorded)")
    if is_blank(args.actor_id):
        return Failure("CAUSA.UNREPRESENTABLE", "actorId must be non-blank text (CONTRACT §5.3)")
    return at, {"actorId": args.actor_id, "kind": args.actor_kind}


def plan_observation_append(dir_name: str, files: dict, args, schemas):
    """Pure. Returns (new_files, new_version) or a Failure, applying CLAR-03 precedence after the path check."""
    current = verify(dir_name, files, schemas)                         # base must verify (CLAR-05 §1 analogue)
    if current.failure:
        return current.failure
    if current.admissibility != "evaluated":
        return Failure("CAUSA.HISTORY_INADMISSIBLE", f"base history not evaluable: {current.detail}")
    version = current.state["version"]
    if args.expected_version != version:                               # version
        return Failure("CAUSA.VERSION_STALE", f"expected version {args.expected_version} != current {version}")
    rep = representability(args)                                       # representability
    if is_failure(rep):
        return rep
    at, actor = rep
    events = list(current.events)
    env = {"investigationId": dir_name, "actor": actor, "at": at}
    cmd = {"name": "RecordObservation", "observationId": args.observation_id, "original": args.original,
           "observedAt": None}
    decision = decide(replay(events), env, cmd, expected_version=args.expected_version)   # decide
    if isinstance(decision, Refused):
        first = decision.errors[0]
        return Failure("CAUSA.COMMAND_REFUSED", "; ".join(f"{e.code} ({e.rule})" for e in decision.errors),
                       first.code)
    if isinstance(decision, Unresolved):
        return Failure("CAUSA.COMMAND_REFUSED", f"decision not evaluable: {decision.reason}")
    new_events = [{"formatVersion": "1.0.0", "eventId": event_id(dir_name, version + 1 + i, args.correlation_id),
                   "investigationId": dir_name, "aggregateVersion": version + 1 + i, "eventType": b.event_type,
                   "occurredAt": at, "actor": actor, "semanticVersion": "1.0.0",
                   "correlationId": args.correlation_id, "payload": b.payload}
                  for i, b in enumerate(decision.bodies)]
    appended = "".join(canonical(e) + "\n" for e in new_events).encode("utf-8")
    new_files = rebuild(dir_name, events + new_events, files["events.ndjson"] + appended)
    check = verify(dir_name, new_files, schemas)                        # verification (§9.2 step 3)
    if check.failure:
        return check.failure
    if check.admissibility != "evaluated":
        return Failure("CAUSA.HISTORY_INADMISSIBLE", check.detail)
    return new_files, check.state["version"]


# ====================================================================================================================
# Filesystem edge (CAUSA-CLAR-04)
# ====================================================================================================================

def read_dir(path: str) -> dict:
    """Every directory entry counts (CONTRACT §3.3); non-regular entries map to None."""
    full = lambda n: os.path.join(path, n)  # noqa: E731
    return {n: (open(full(n), "rb").read() if os.path.isfile(full(n)) and not os.path.islink(full(n)) else None)
            for n in sorted(os.listdir(path))}


def _fsync_dir(path: str) -> None:
    fd = os.open(path, os.O_RDONLY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def _siblings(parent: str, name: str, kind: str) -> list:
    pat = re.compile(re.escape(name) + r"\." + kind + r"-([1-9][0-9]*)\Z")
    return sorted(((int(m.group(1)), e) for e in os.listdir(parent) if (m := pat.match(e))), reverse=True)


def _free_n(parent: str, name: str) -> int:
    taken = {n for kind in ("next", "prev", "invalid") for n, _ in _siblings(parent, name, kind)}
    return next(n for n in range(1, len(taken) + 2) if n not in taken)


def recover(path: str, schemas):
    """CLAR-04 recovery: if <id> is missing or invalid and a .prev-<n>/.next-<n> sibling verifies fully, restore it
    (preferring .next only when it extends .prev, CONTRACT §9.3). Returns a note string or None."""
    parent, name = os.path.dirname(path), os.path.basename(path)
    if os.path.isdir(path) and verify(name, read_dir(path), schemas).failure is None:
        return None
    ok = lambda p: os.path.isdir(p) and verify(name, read_dir(p), schemas).failure is None  # noqa: E731
    best = lambda kind: next((os.path.join(parent, e) for _, e in _siblings(parent, name, kind)  # noqa: E731
                              if ok(os.path.join(parent, e))), None)
    prev, nxt = best("prev"), best("next")
    if nxt and prev:
        chosen = nxt if read_dir(nxt)["events.ndjson"].startswith(read_dir(prev)["events.ndjson"]) else prev
    else:
        chosen = nxt or prev
    if chosen is None:
        return None
    if os.path.lexists(path):          # keep the invalid set, untouched, under a non-candidate name
        os.rename(path, os.path.join(parent, f"{name}.invalid-{_free_n(parent, name)}"))
    os.rename(chosen, path)
    _fsync_dir(parent)
    return f"recovered from {os.path.basename(chosen)}"


def publish(path: str, new_files: dict):
    """CLAR-04: write <id>.next-<n>, fsync; rename <id> -> <id>.prev-<n>, .next-<n> -> <id>; fsync parent;
    re-read; remove .prev-<n> only when the re-read returns the written bytes. Returns None or a Failure."""
    parent, name = os.path.dirname(path), os.path.basename(path)
    n = _free_n(parent, name)
    nxt, prev = os.path.join(parent, f"{name}.next-{n}"), os.path.join(parent, f"{name}.prev-{n}")
    os.mkdir(nxt)
    for fname in FILES:
        with open(os.path.join(nxt, fname), "wb") as fh:
            fh.write(new_files[fname])
            fh.flush()
            os.fsync(fh.fileno())
    _fsync_dir(nxt)
    os.rename(path, prev)
    os.rename(nxt, path)
    _fsync_dir(parent)
    if read_dir(path) != {f: new_files[f] for f in FILES}:
        return Failure("CAUSA.WRITE_UNCONFIRMED", f"re-read of {name} did not return the written bytes; "
                                                  f"{os.path.basename(prev)} kept")
    for fname in os.listdir(prev):
        os.remove(os.path.join(prev, fname))
    os.rmdir(prev)
    _fsync_dir(parent)
    return None
