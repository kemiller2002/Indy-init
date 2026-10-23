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
VERSION_DIR = re.compile(r"v?[0-9]+\.[0-9]+\.[0-9]+\Z")         # "<version>" segment, e.g. v1.0.0 (NEW-13)
RFC3339 = re.compile(r"([0-9]{4})-([0-9]{2})-([0-9]{2})[Tt]([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.([0-9]+))?"
                     r"([Zz]|[+-][0-9]{2}:[0-9]{2})\Z")


# ====================================================================================================================
# Pure checks (CLAR-03 precedence: path, version, representability, decide, verification, confirmation)
# ====================================================================================================================

def path_check(real_path: str):
    """CAUSA-CLAR-03 "Paths", on the path with symlinks and ./.. resolved:
    PATH_FROZEN at any depth below fixtures/<version>/, contract/<version>/ or compatibility/<version>/;
    PATH_INVALID unless exactly workspaces/demo/<runId>/investigations/<canonical UUID>."""
    parts = real_path.split(os.sep)
    if any(parts[i] in FROZEN_TREES and VERSION_DIR.match(parts[i + 1]) for i in range(len(parts) - 2)):
        return Failure("CAUSA.PATH_FROZEN", "target is below a frozen fixtures|contract|compatibility/<version>/ tree "
                                            "(CAUSA-CLAR-03, CONTRACT §3.4)")
    exact = (len(parts) >= 5 and parts[-5] == "workspaces" and parts[-4] == "demo" and bool(RUN_ID.match(parts[-3]))
             and parts[-2] == "investigations" and is_canonical_uuid(parts[-1]))
    if not exact:
        return Failure("CAUSA.PATH_INVALID", "target is not exactly workspaces/demo/<runId>/investigations/"
                                             "<investigationId> (CAUSA-CLAR-03, CONTRACT §3.2)")
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


def _staging(path: str) -> str:
    """workspaces/demo/<runId>/.staging/, sibling of investigations/ (same filesystem)."""
    return os.path.join(os.path.dirname(os.path.dirname(path)), ".staging")


def _staged(staging: str, iid: str, kinds=("next", "prev", "invalid")) -> list:
    """[(n, kind, name)] of staging siblings for `iid`."""
    if not os.path.isdir(staging):
        return []
    pat = re.compile(re.escape(iid) + r"\.(" + "|".join(kinds) + r")-([1-9][0-9]*)\Z")
    return [(int(m.group(2)), m.group(1), e) for e in os.listdir(staging) if (m := pat.match(e))]


def _next_n(staging: str, iid: str) -> int:
    """1 + the largest <n> already present in .staging/ for that id, or 1."""
    return 1 + max((n for n, _, _ in _staged(staging, iid)), default=0)


def _remove_set(path: str) -> None:
    for fname in os.listdir(path):
        full = os.path.join(path, fname)
        os.rmdir(full) if os.path.isdir(full) and not os.path.islink(full) else os.remove(full)
    os.rmdir(path)


def _verifies(iid: str, path: str, schemas) -> bool:
    return os.path.isdir(path) and verify(iid, read_dir(path), schemas).failure is None


def recover(path: str, schemas):
    """CAUSA-CLAR-04 recovery, run before the first read or write of an investigation. Returns a note or None."""
    iid, investigations, staging = os.path.basename(path), os.path.dirname(path), _staging(path)
    transient = lambda: [e for e in _staged(staging, iid, ("next", "prev"))]  # noqa: E731
    if _verifies(iid, path, schemas):                                                        # 1
        for _, _, name in transient():
            _remove_set(os.path.join(staging, name))
        return None
    note = []
    if os.path.lexists(path):                                                                # 2
        os.makedirs(staging, exist_ok=True)
        moved = f"{iid}.invalid-{_next_n(staging, iid)}"
        os.rename(path, os.path.join(staging, moved))
        note.append(f"invalid set kept as .staging/{moved}")
    candidates = [(n, kind, name) for n, kind, name in transient()                         # 3
                  if _verifies(iid, os.path.join(staging, name), schemas)]
    if not candidates:                                                                       # 5
        _fsync_dir(investigations)
        return "; ".join(note + ["no verified staging candidate: investigation missing"])
    top = max(n for n, _, _ in candidates)
    at_top = {kind: name for n, kind, name in candidates if n == top}
    if "next" in at_top and "prev" in at_top:
        nxt, prv = (read_dir(os.path.join(staging, at_top[k]))["events.ndjson"] for k in ("next", "prev"))
        winner = at_top["next"] if nxt.startswith(prv) else at_top["prev"]
    else:
        winner = next(iter(at_top.values()))
    os.rename(os.path.join(staging, winner), path)                                           # 4
    for _, _, name in transient():
        _remove_set(os.path.join(staging, name))
    _fsync_dir(investigations)
    _fsync_dir(staging)
    return "; ".join(note + [f"recovered from .staging/{winner}"])


def publish(path: str, new_files: dict, schemas):
    """CAUSA-CLAR-04 steps 2-4. Returns None or a Failure (WRITE_UNCONFIRMED: no rollback, recovery runs)."""
    iid, investigations, staging = os.path.basename(path), os.path.dirname(path), _staging(path)
    os.makedirs(staging, exist_ok=True)
    n = _next_n(staging, iid)
    nxt, prev = os.path.join(staging, f"{iid}.next-{n}"), os.path.join(staging, f"{iid}.prev-{n}")
    os.mkdir(nxt)                                                                            # 2
    for fname in FILES:
        with open(os.path.join(nxt, fname), "wb") as fh:
            fh.write(new_files[fname])
            fh.flush()
            os.fsync(fh.fileno())
    _fsync_dir(nxt)
    os.rename(path, prev)                                                                    # 3
    os.rename(nxt, path)
    _fsync_dir(investigations)
    _fsync_dir(staging)
    if read_dir(path) == {f: new_files[f] for f in FILES}:                                   # 4
        _remove_set(prev)
        _fsync_dir(staging)
        return None
    note = recover(path, schemas)
    return Failure("CAUSA.WRITE_UNCONFIRMED", f"re-read of {iid} did not return the written bytes; no rollback; "
                                              f"recovery: {note or 'investigation verifies'}")
