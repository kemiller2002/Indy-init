"""The 17 reader stages of CONTRACT §11.1, derived documents (§8.1, §9.1), append (§9.2) and successor (§9.3).

Every function is pure over in-memory file sets: {file name: bytes}.  The only I/O lives in causa_clean.py.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

from canon import (Failure, canonical, digest_file_as_read, digest_json, event_id, file_bytes,
                   is_canonical_uuid, is_failure, parse_json, sha256_hex)
from schemas import first_violation
from semantics import project_state, replay, validate_history

FILES = ("manifest.json", "events.ndjson", "snapshot.json")
VERSION = "1.0.0"


@dataclass(frozen=True)
class Report:
    failure: Failure | None
    admissibility: str                     # "evaluated" | "not-evaluated"
    stage15: str                           # "run" | "not-reached" | "unresolved"
    digests: dict = field(default_factory=dict)
    state: Any = None                      # projected state object (CONTRACT §8.2) when replay happened
    events: tuple = ()
    detail: str = ""

    def as_json(self) -> dict:
        st = self.state
        return {"primaryCode": self.failure.code if self.failure else None,
                "domainCode": self.failure.domain if self.failure else None,
                "admissibility": self.admissibility,
                "stateSha256": digest_json(st) if st is not None else None,
                "eventsSha256": self.digests.get("events"),
                "manifestSha256": self.digests.get("manifest"),
                "snapshotSha256": self.digests.get("snapshot"),
                "version": st["version"] if st is not None else None,
                "lifecycle": st["lifecycle"] if st is not None else None,
                "detail": self.detail or (self.failure.detail if self.failure else "valid")}


# ====================================================================================================================
# Derived documents
# ====================================================================================================================

def build_snapshot(events: list, state: dict) -> dict:
    """CONTRACT §8.1."""
    return {"formatVersion": VERSION, "investigationId": state["investigationId"],
            "aggregateVersion": len(events) and events[-1]["aggregateVersion"], "projectionVersion": 1,
            "semanticVersion": VERSION, "generatedAt": events[-1]["occurredAt"], "state": state}


def build_manifest(investigation_id: str, events: list, events_bytes: bytes, snapshot: dict) -> dict:
    """CONTRACT §9.1."""
    return {"formatVersion": VERSION, "investigationId": investigation_id, "schemaVersion": VERSION,
            "semanticVersion": VERSION, "aggregateVersion": len(events), "eventCount": len(events),
            "eventsSha256": sha256_hex(events_bytes), "snapshotVersion": 1, "snapshotSha256": digest_json(snapshot),
            "domainPacks": [], "createdAt": events[0]["occurredAt"], "updatedAt": events[-1]["occurredAt"]}


def project(events: list) -> dict | None:
    s = replay(events)
    return project_state(s) if s is not None else None


# ====================================================================================================================
# Stage helpers.  Each returns None (pass) or a Failure.
# ====================================================================================================================

def _first(*thunks):
    for t in thunks:
        r = t()
        if r is not None:
            return r
    return None


def stage1_paths(dir_name: str, files: dict):
    if not is_canonical_uuid(dir_name):
        return Failure("CAUSA.PATH_INVALID", f"directory name {dir_name!r} is not a canonical UUID")
    if set(files) != set(FILES):
        return Failure("CAUSA.PATH_INVALID", f"directory must contain exactly {list(FILES)}, has {sorted(files)}")
    bad = [n for n, b in files.items() if b is None]
    return Failure("CAUSA.PATH_INVALID", f"not regular files: {bad}") if bad else None


def framing(name: str, data: bytes, ndjson: bool):
    """Stage 2 (CONTRACT §2.1-§2.2 framing)."""
    enc = lambda d: Failure("CAUSA.ENCODING", f"{name}: {d}")  # noqa: E731
    if not data:
        return enc("empty file")
    if data.startswith(b"\xef\xbb\xbf"):
        return enc("byte order mark")
    if b"\r" in data:
        return enc("CR character")
    try:
        data.decode("utf-8", "strict")
    except UnicodeDecodeError as exc:
        return enc(f"invalid UTF-8 ({exc.reason})")
    if not data.endswith(b"\n"):
        return enc("missing final LF")
    if ndjson and (data.startswith(b"\n") or b"\n\n" in data):
        return enc("blank line")
    return None


def ndjson_lines(data: bytes) -> list[str]:
    return data.decode("utf-8")[:-1].split("\n")


def parse_doc(name: str, text: str):
    v = parse_json(text)
    return Failure(v.code, f"{name}: {v.detail}") if is_failure(v) else v


def noncanonical(name: str, value: Any, text: str):
    return None if canonical(value) == text else Failure("CAUSA.NON_CANONICAL", f"{name}: not RFC 8785 canonical")


def unsupported_version(name: str, doc: Any, keys: tuple):
    if not isinstance(doc, dict):
        return None
    bad = [k for k in keys if isinstance(doc.get(k), str) and doc[k] != VERSION]
    return Failure("CAUSA.UNSUPPORTED_VERSION", f"{name}: {bad[0]}={doc[bad[0]]!r}") if bad else None


def unknown_type(name: str, ev: Any, registry: tuple):
    if isinstance(ev, dict) and isinstance(ev.get("eventType"), str) and ev["eventType"] not in registry:
        return Failure("CAUSA.UNKNOWN_EVENT_TYPE", f"{name}: unregistered eventType {ev['eventType']!r}")
    return None


def schema_violation(name: str, validator, doc: Any):
    msg = first_violation(validator, doc)
    return Failure("CAUSA.SCHEMA_VIOLATION", f"{name}: {msg}") if msg else None


def check_lines_2_to_7(lines: list[str], first_version: int, schemas):
    """Stages 3-7 over event lines (stage 2 is file framing). Returns (events, None) or (None, Failure)."""
    label = lambda i: f"events.ndjson line {first_version + i}"  # noqa: E731
    parsed = [parse_doc(label(i), ln) for i, ln in enumerate(lines)]
    bad = next((p for p in parsed if is_failure(p)), None)
    if bad:
        return None, bad
    fail = _first(
        lambda: next((f for i, (v, t) in enumerate(zip(parsed, lines)) if (f := noncanonical(label(i), v, t))), None),
        lambda: next((f for i, v in enumerate(parsed)
                      if (f := unsupported_version(label(i), v, ("formatVersion", "semanticVersion")))), None),
        lambda: next((f for i, v in enumerate(parsed) if (f := unknown_type(label(i), v, schemas.event_types))), None),
        lambda: next((f for i, v in enumerate(parsed) if (f := schema_violation(label(i), schemas.envelope, v))), None))
    return (None, fail) if fail else (parsed, None)


def correlation_reused(events: list):
    seen, prev = set(), None
    for e in events:
        c = e["correlationId"]
        if c != prev:
            if c in seen:
                return Failure("CAUSA.CORRELATION_REUSED",
                               f"correlationId {c} names a second, non-adjacent group at v{e['aggregateVersion']}")
            seen = seen | {c}
        prev = c
    return None


# ====================================================================================================================
# verify (CONTRACT §11.1)
# ====================================================================================================================

def _fail(failure: Failure, digests: dict, stage15: str = "not-reached", state=None, events=()) -> Report:
    # A failure before stage 15 is a complete verdict: no later stage can change the primary code, so the answer
    # is not conditional on admissibility (CONTRACT §11.3 last sentence).
    return Report(failure, "evaluated", stage15, digests, state, tuple(events),
                  f"{failure.detail} [stage 15 {stage15}]")


def file_digests(files: dict) -> dict:
    """CAUSA-CLAR-07: events = SHA-256 of bytes as read; manifest/snapshot = bytes as read minus one trailing LF;
    null for a missing (or non-regular) file."""
    get = lambda n: files.get(n)  # noqa: E731
    return {"events": sha256_hex(get("events.ndjson")) if get("events.ndjson") is not None else None,
            "manifest": digest_file_as_read(get("manifest.json")) if get("manifest.json") is not None else None,
            "snapshot": digest_file_as_read(get("snapshot.json")) if get("snapshot.json") is not None else None}


def verify(dir_name: str, files: dict, schemas) -> Report:
    digests = file_digests(files)                                                               # CLAR-07
    f = stage1_paths(dir_name, files)                                                           # 1
    if f:
        return _fail(f, digests)
    m_bytes, e_bytes, s_bytes = files["manifest.json"], files["events.ndjson"], files["snapshot.json"]
    f = _first(lambda: framing("manifest.json", m_bytes, False),                                # 2
               lambda: framing("events.ndjson", e_bytes, True),
               lambda: framing("snapshot.json", s_bytes, False))
    if f:
        return _fail(f, digests)
    m_text, s_text = m_bytes.decode("utf-8")[:-1], s_bytes.decode("utf-8")[:-1]
    lines = ndjson_lines(e_bytes)
    manifest = parse_doc("manifest.json", m_text)                                               # 3
    if is_failure(manifest):
        return _fail(manifest, digests)
    parsed = [parse_doc(f"events.ndjson line {i + 1}", ln) for i, ln in enumerate(lines)]
    bad = next((p for p in parsed if is_failure(p)), None)
    if bad:
        return _fail(bad, digests)
    snapshot = parse_doc("snapshot.json", s_text)
    if is_failure(snapshot):
        return _fail(snapshot, digests)
    lbl = lambda i: f"events.ndjson line {i + 1}"  # noqa: E731
    f = _first(
        lambda: noncanonical("manifest.json", manifest, m_text),                                # 4
        lambda: next((f for i, (v, t) in enumerate(zip(parsed, lines)) if (f := noncanonical(lbl(i), v, t))), None),
        lambda: noncanonical("snapshot.json", snapshot, s_text),
        lambda: unsupported_version("manifest.json", manifest,                                  # 5
                                    ("formatVersion", "schemaVersion", "semanticVersion")),
        lambda: next((f for i, v in enumerate(parsed)
                      if (f := unsupported_version(lbl(i), v, ("formatVersion", "semanticVersion")))), None),
        lambda: unsupported_version("snapshot.json", snapshot, ("formatVersion", "semanticVersion")),
        lambda: next((f for i, v in enumerate(parsed) if (f := unknown_type(lbl(i), v, schemas.event_types))), None),
        lambda: schema_violation("manifest.json", schemas.manifest, manifest),                  # 7
        lambda: next((f for i, v in enumerate(parsed) if (f := schema_violation(lbl(i), schemas.envelope, v))), None),
        lambda: schema_violation("snapshot.json", schemas.snapshot, snapshot))
    if f:
        return _fail(f, digests)
    events = parsed
    n = len(events)
    iid = manifest["investigationId"]
    versions = [e["aggregateVersion"] for e in events]
    ids = [e["eventId"] for e in events]
    f = _first(
        lambda: None if iid == dir_name else Failure(                                           # 8
            "CAUSA.MANIFEST_MISMATCH", f"manifest investigationId {iid} != directory {dir_name}"),
        lambda: None if manifest["aggregateVersion"] == n and manifest["eventCount"] == n else Failure(
            "CAUSA.MANIFEST_MISMATCH", f"manifest aggregateVersion/eventCount != {n} lines"),
        lambda: None if manifest["createdAt"] == events[0]["occurredAt"] else Failure(
            "CAUSA.MANIFEST_MISMATCH", "manifest createdAt != occurredAt of the first event"),
        lambda: None if manifest["updatedAt"] == events[-1]["occurredAt"] else Failure(
            "CAUSA.MANIFEST_MISMATCH", "manifest updatedAt != occurredAt of the last event"),
        lambda: None if manifest["eventsSha256"] == sha256_hex(e_bytes) else Failure(           # 9
            "CAUSA.EVENTS_HASH_MISMATCH", "eventsSha256 does not match events.ndjson bytes"),
        lambda: next((Failure("CAUSA.INVESTIGATION_MISMATCH",                                    # 10
                              f"line {i + 1} names investigation {e['investigationId']}")
                      for i, e in enumerate(events) if e["investigationId"] != iid), None),
        lambda: None if len(set(ids)) == len(ids) else Failure(                                 # 11
            "CAUSA.EVENT_ID_DUPLICATE", "duplicate eventId"),
        lambda: None if versions == list(range(1, n + 1)) else Failure(                         # 12
            "CAUSA.SEQUENCE_INVALID", f"aggregateVersion values are not exactly 1..{n} in file order"),
        lambda: next((Failure("CAUSA.EVENT_ID_MISMATCH", f"line {i + 1}: eventId is not the §5.2 derivation")  # 13
                      for i, e in enumerate(events)
                      if e["eventId"] != event_id(e["investigationId"], e["aggregateVersion"], e["correlationId"])),
                     None),
        lambda: correlation_reused(events))                                                     # 14
    if f:
        return _fail(f, digests)
    validation = validate_history(events)                                                       # 15
    final = validation.state
    state = project_state(final) if final is not None else None
    first_err_v = validation.errors[0].version if validation.errors else None
    blocking_unresolved = [u for u in validation.unresolved if first_err_v is None or u[0] <= first_err_v]
    def stages_16_17():
        return _first(
            lambda: None if manifest["snapshotSha256"] == sha256_hex(s_text.encode("utf-8")) else Failure(  # 16
                "CAUSA.SNAPSHOT_HASH_MISMATCH", "snapshotSha256 does not match the snapshot"),
            lambda: None if file_bytes(build_snapshot(events, state)) == s_bytes else Failure(            # 17
                "CAUSA.SNAPSHOT_MISMATCH", "snapshot is not the projection of the replayed history"))
    if blocking_unresolved:
        f = stages_16_17()
        # CAUSA-CLAR-02: primaryCode stays null; a stage 16-17 code is reported only in detail, as conditional.
        v, reason = blocking_unresolved[0]
        later = (f"; conditional on admissibility: {f.code} ({f.detail})" if f
                 else "; stages 16-17 pass, conditional on admissibility")
        return Report(None, "not-evaluated", "unresolved", digests, state, tuple(events),
                      f"structurally valid; admissibility not evaluated: group v{v}: {reason} (SEMANTICS §17)"
                      + later)
    if validation.errors:
        e0 = validation.errors[0]
        return _fail(Failure("CAUSA.HISTORY_INADMISSIBLE", f"{e0.message} [rule {e0.rule}; "
                             f"{len(validation.errors)} error(s)]", e0.code), digests, "run", state, events)
    f = stages_16_17()
    if f:
        return _fail(f, digests, "run", state, events)
    return Report(None, "evaluated", "run", digests, state, tuple(events), "valid; all 17 stages passed")


# ====================================================================================================================
# append (§9.2) and successor (§9.3)
# ====================================================================================================================

def append_lines(dir_name: str, base_files: dict, expected_version: int, appended: bytes, schemas) -> Report:
    base = verify(dir_name, base_files, schemas)
    if base.failure or base.admissibility != "evaluated":
        return base
    current = base.state["version"]
    if expected_version != current:                                                             # (1)
        return _fail(Failure("CAUSA.VERSION_STALE", f"expected version {expected_version} != current {current}"),
                     base.digests)
    f = framing("appended events", appended, True)                                              # (2) stage 2
    if f:
        return _fail(f, base.digests)
    new_events, f = check_lines_2_to_7(ndjson_lines(appended), current + 1, schemas)            # (2) stages 3-7
    if f:
        return _fail(f, base.digests)
    return verify(dir_name, rebuild(dir_name, list(base.events) + new_events,
                                    base_files["events.ndjson"] + appended), schemas)           # (3)


def rebuild(dir_name: str, events: list, events_bytes: bytes) -> dict:
    """Manifest and snapshot rebuilt by the evaluator from the events (§9.2 step 3)."""
    state = project(events)
    snapshot = build_snapshot(events, state)
    manifest = build_manifest(dir_name, events, events_bytes, snapshot)
    return {"events.ndjson": events_bytes, "manifest.json": file_bytes(manifest), "snapshot.json": file_bytes(snapshot)}


def successor(dir_name: str, base_files: dict, candidate: dict, schemas) -> Report:
    r = verify(dir_name, candidate, schemas)
    if r.failure or r.admissibility != "evaluated":
        return r
    if not candidate["events.ndjson"].startswith(base_files["events.ndjson"]):
        return _fail(Failure("CAUSA.HISTORY_REWRITTEN", "candidate events.ndjson does not start with the accepted "
                             "events.ndjson bytes"), r.digests, "run", r.state, r.events)
    return r
