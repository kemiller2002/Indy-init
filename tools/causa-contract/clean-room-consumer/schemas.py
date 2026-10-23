"""Schema loading, schema identity and stage-7 validation.

Implements CONTRACT.md §10.3 (schema bundle and identity), §2.13 (ECMA-262 regex semantics), §2.9 (calendar-valid
timestamps as part of the `timestamp` definition) and the stage-7 schema check of §11.1.
Library exceptions are converted to Failure values at this boundary.
"""
from __future__ import annotations

import datetime
import json
import os
import re
from dataclasses import dataclass
from functools import lru_cache
from typing import Any, Callable

from jsonschema import Draft202012Validator, ValidationError, validators
from jsonschema.exceptions import best_match
from referencing import Registry, Resource

from canon import Failure, is_failure, sha256_hex, parse_json

DEFAULT_REQUIREMENTS = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "requirements", "causa", "data", "v1.0.0"))


def requirements_dir() -> str:
    return os.environ.get("CAUSA_REQUIREMENTS_DIR", DEFAULT_REQUIREMENTS)


def attempt(fn: Callable[[], Any], code: str, what: str):
    """Boundary adapter: run a library call and turn any exception into a Failure value."""
    try:
        return fn()
    except Exception as exc:  # noqa: BLE001 - boundary conversion, by design
        return Failure(code, f"{what}: {exc}")


# --------------------------------------------------------------------------------------------------------------------
# ECMA-262 pattern semantics (CONTRACT §2.13): '$' matches only at the very end.  Python's '$' also matches before a
# final '\n', so every unescaped '$' outside a character class is translated to '\Z'.
# --------------------------------------------------------------------------------------------------------------------

def ecma_to_python(pattern: str) -> str:
    out, i, in_class = [], 0, False
    while i < len(pattern):
        c = pattern[i]
        if c == "\\":
            out.append(pattern[i:i + 2])
            i += 2
            continue
        if c == "[" and not in_class:
            in_class = True
        elif c == "]" and in_class:
            in_class = False
        elif c == "$" and not in_class:
            c = r"\Z"
        out.append(c)
        i += 1
    return "".join(out)


@lru_cache(maxsize=None)
def _compiled(pattern: str) -> re.Pattern:
    return re.compile(ecma_to_python(pattern))


def calendar_valid(ts: str) -> bool:
    """Real proleptic-Gregorian instant, years 0001-9999 (CONTRACT §2.9). Assumes the lexical pattern matched."""
    try:
        datetime.datetime(int(ts[0:4]), int(ts[5:7]), int(ts[8:10]), int(ts[11:13]), int(ts[14:16]), int(ts[17:19]))
        return True
    except ValueError:
        return False


def _make_validator_class(timestamp_pattern: str):
    def pattern_kw(validator, patrn, instance, schema):
        if not validator.is_type(instance, "string"):
            return
        if not _compiled(patrn).search(instance):
            yield ValidationError(f"{instance!r} does not match {patrn!r}")
        elif patrn == timestamp_pattern and not calendar_valid(instance):
            yield ValidationError(f"{instance!r} is not a calendar-valid instant")

    return validators.extend(Draft202012Validator, {"pattern": pattern_kw})


# --------------------------------------------------------------------------------------------------------------------
# Bundle
# --------------------------------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class SchemaSet:
    identity: str
    parsed: int
    by_path: dict
    envelope: Any
    manifest: Any
    snapshot: Any
    root_manifest: Any
    event_types: tuple
    by_schema: dict          # schema path -> validator (dataset-level documents, CAUSA-CLAR-10)


def _walk_refs(node: Any):
    if isinstance(node, dict):
        if isinstance(node.get("$ref"), str):
            yield node["$ref"]
        for v in node.values():
            yield from _walk_refs(v)
    elif isinstance(node, list):
        for v in node:
            yield from _walk_refs(v)


def _read(path: str):
    return attempt(lambda: open(path, "rb").read(), "CAUSA.PATH_INVALID", f"cannot read {path}")


def load_schemas(req_dir: str | None = None):
    """Parse every schema listed in schemas/bundle.json, check digests and $ref resolution; return a SchemaSet."""
    sdir = os.path.join(req_dir or requirements_dir(), "schemas")
    bundle_bytes = _read(os.path.join(sdir, "bundle.json"))
    if is_failure(bundle_bytes):
        return bundle_bytes
    bundle = parse_json(bundle_bytes.decode("utf-8")[:-1])
    if is_failure(bundle):
        return bundle
    listed = [e["path"] for e in bundle["schemas"]]
    on_disk = sorted(os.path.relpath(os.path.join(r, f), sdir).replace(os.sep, "/")
                     for r, _, fs in os.walk(sdir) for f in fs if f.endswith(".schema.json"))
    if sorted(listed) != on_disk or listed != sorted(listed):
        return Failure("CAUSA.FIXTURE_DRIFT", "bundle.json does not list exactly the schema files, sorted")
    raw = {e["path"]: _read(os.path.join(sdir, e["path"])) for e in bundle["schemas"]}
    bad = [p for p, b in raw.items() if is_failure(b) or sha256_hex(b) != next(
        e["sha256"] for e in bundle["schemas"] if e["path"] == p)]
    if bad:
        return Failure("CAUSA.FIXTURE_DRIFT", f"schema digest mismatch: {bad}")
    docs = {p: attempt(lambda b=b: json.loads(b), "CAUSA.JSON_MALFORMED", p) for p, b in raw.items()}
    bad = [p for p, d in docs.items() if is_failure(d)]
    if bad:
        return Failure("CAUSA.JSON_MALFORMED", f"unparsable schemas: {bad}")
    checked = [attempt(lambda d=d: Draft202012Validator.check_schema(d) or True, "CAUSA.SCHEMA_VIOLATION", p)
               for p, d in docs.items()]
    if any(is_failure(c) for c in checked):
        return next(c for c in checked if is_failure(c))
    registry = Registry().with_resources((d["$id"], Resource.from_contents(d)) for d in docs.values())
    unresolved = [(p, ref) for p, d in docs.items() for ref in _walk_refs(d)
                  if is_failure(attempt(lambda d=d, ref=ref: registry.resolver(base_uri=d["$id"]).lookup(ref),
                                        "CAUSA.SCHEMA_VIOLATION", ref))]
    if unresolved:
        return Failure("CAUSA.SCHEMA_VIOLATION", f"unresolved $ref: {unresolved}")
    ts_pattern = docs["common.schema.json"]["$defs"]["timestamp"]["pattern"]
    vcls = _make_validator_class(ts_pattern)
    mk = lambda p: vcls(docs[p], registry=registry)  # noqa: E731
    envelope = docs["event-envelope.schema.json"]
    return SchemaSet(
        identity=sha256_hex(bundle_bytes), parsed=len(docs), by_path=docs,
        envelope=mk("event-envelope.schema.json"), manifest=mk("investigation-manifest.schema.json"),
        snapshot=mk("snapshot.schema.json"), root_manifest=mk("root-manifest.schema.json"),
        event_types=tuple(envelope["properties"]["eventType"]["enum"]),
        by_schema={p: mk(p) for p in ("state.schema.json", "golden-scenarios.schema.json",
                                     "negative-fixture.schema.json", "schema-bundle.schema.json",
                                     "root-manifest.schema.json")})


def first_violation(validator, instance: Any) -> str | None:
    """Return the message of the first schema violation, or None when the instance is valid."""
    err = next(iter(validator.iter_errors(instance)), None)
    if err is None:
        return None
    best = best_match(validator.iter_errors(instance))
    return f"{'/'.join(map(str, best.absolute_path)) or '<root>'}: {best.message[:300]}"
