#!/usr/bin/env python3
"""Bidirectional contract parity gate: Indy-init requirements <-> frozen causa-data release.

Read-only.  It never copies, rewrites or "synchronizes" either side; any unexplained difference is a failure.

Usage:
  python3 -I tools/causa-contract/parity_gate.py check    --causa-data <git checkout of kemiller2002/causa-data>
  python3 -I tools/causa-contract/parity_gate.py selftest --causa-data <git checkout>
  python3 -I tools/causa-contract/parity_gate.py digests  --causa-data <git checkout>   # print, never write, pin values

The frozen side is read from the git object store at the pinned release tag (`git show <tag>:<path>`), never from the
working tree, so local edits in the causa-data checkout cannot mask a difference.  The pin (tag name, tag object,
commit, lock/bundle/dataset digests) lives in requirements/causa/data/v1.0.0/compatibility/release-pin.json.
"""
from __future__ import annotations

import hashlib
import json
import posixpath
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path
from types import MappingProxyType
from typing import Any, Callable, Iterable, Mapping, NamedTuple

REQ_ROOT = Path(__file__).resolve().parents[2] / "requirements" / "causa" / "data" / "v1.0.0"

# Indy-init path (relative to REQ_ROOT) -> causa-data path at the release tag.  Every imported normative file.
IMPORT_MAP_FIXED = MappingProxyType(
    {
        "CONTRACT.md": "contract/v1.0.0/CONTRACT.md",
        "FIELD-MAPPING.md": "contract/v1.0.0/FIELD-MAPPING.md",
        "compatibility/lock.json": "compatibility/v1.0.0/lock.json",
        "compatibility/RELEASE": "compatibility/v1.0.0/RELEASE",
        "compatibility/engine.lock.json": "engine.lock.json",
        "compatibility/global.json": "global.json",
        "fixtures/manifest.json": "fixtures/v1.0.0/manifest.json",
    }
)
SCHEMA_DIR_REQ = "schemas"
SCHEMA_DIR_FROZEN = "contract/v1.0.0/schemas"
GOLDEN_DIR_REQ = "golden"
GOLDEN_DIR_FROZEN = "contract/v1.0.0/golden"


class Finding(NamedTuple):
    code: str
    subject: str
    detail: str


Reader = Callable[[str], "bytes | None"]  # path -> bytes, None when absent
Lister = Callable[[str], tuple[str, ...]]  # directory prefix -> relative file paths under it


class Side(NamedTuple):
    name: str
    read: Reader
    list_dir: Lister


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


# ------------------------------------------------------------------------------------------------- sides (I/O edge)


def dir_side(name: str, root: Path) -> Side:
    def read(p: str) -> bytes | None:
        f = root / p
        return f.read_bytes() if f.is_file() else None

    def list_dir(prefix: str) -> tuple[str, ...]:
        base = root / prefix
        return tuple(sorted(p.relative_to(base).as_posix() for p in base.rglob("*") if p.is_file())) if base.is_dir() else ()

    return Side(name, read, list_dir)


def git(repo: Path, *args: str) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(["git", "-C", str(repo), *args], capture_output=True, check=False)


def tag_side(repo: Path, tag: str) -> Side:
    ref = f"refs/tags/{tag}"

    def read(p: str) -> bytes | None:
        r = git(repo, "show", f"{ref}:{p}")
        return r.stdout if r.returncode == 0 else None

    def list_dir(prefix: str) -> tuple[str, ...]:
        r = git(repo, "ls-tree", "-r", "--name-only", ref, "--", prefix + "/")
        names = r.stdout.decode().splitlines() if r.returncode == 0 else []
        return tuple(sorted(posixpath.relpath(n, prefix) for n in names))

    return Side(f"causa-data@{tag}", read, list_dir)


def requirement_side(root: Path) -> Side:
    return dir_side("Indy-init requirements", root)


def frozen_paths(req: Side) -> Mapping[str, str]:
    """Indy-init path -> frozen path, including every schema and golden file present on the requirements side."""
    return MappingProxyType(
        dict(IMPORT_MAP_FIXED)
        | {f"{SCHEMA_DIR_REQ}/{p}": f"{SCHEMA_DIR_FROZEN}/{p}" for p in req.list_dir(SCHEMA_DIR_REQ)}
        | {f"{GOLDEN_DIR_REQ}/{p}": f"{GOLDEN_DIR_FROZEN}/{p}" for p in req.list_dir(GOLDEN_DIR_REQ)}
    )


# ------------------------------------------------------------------------------------------------- schema semantics


def load_json(data: bytes | None) -> Any:
    try:
        return json.loads(data) if data is not None else None
    except ValueError:
        return None


def schema_model(schemas: Mapping[str, Any]) -> Mapping[str, Mapping[str, Any]]:
    """Flatten every schema into {schemaPath: {jsonPointer-ish property path: descriptor}} for semantic diffing."""

    def describe(node: Any) -> Mapping[str, Any]:
        if not isinstance(node, dict):
            return {}
        nullable = any(isinstance(b, dict) and b.get("type") == "null" for b in node.get("oneOf", ())) or (
            "$ref" in node and node["$ref"].split("/")[-1].startswith("nullable")
        )
        return {
            "type": node.get("type") or ("ref:" + node["$ref"] if "$ref" in node else "oneOf" if "oneOf" in node else "const" if "const" in node else "enum" if "enum" in node else None),
            "enum": tuple(node["enum"]) if "enum" in node else None,
            "const": json.dumps(node["const"]) if "const" in node else None,
            "nullable": nullable,
            "items": json.dumps(node.get("items"), sort_keys=True) if "items" in node else None,
            "pattern": node.get("pattern"),
            "bounds": tuple((k, node[k]) for k in ("minimum", "maximum", "minItems", "maxItems", "minLength", "uniqueItems") if k in node),
        }

    def walk(node: Any, path: str) -> Iterable[tuple[str, Mapping[str, Any]]]:
        if isinstance(node, dict):
            required = frozenset(node.get("required", ()))
            for name, prop in node.get("properties", {}).items():
                yield f"{path}/{name}", dict(describe(prop), required=name in required)
            for key, value in node.items():
                if key != "properties":
                    yield from walk(value, f"{path}/{key}")
                else:
                    for name, prop in value.items():
                        yield from walk(prop, f"{path}/{name}")
        elif isinstance(node, list):
            for i, v in enumerate(node):
                yield from walk(v, f"{path}[{i}]")

    return MappingProxyType({f: MappingProxyType(dict(walk(s, ""))) for f, s in schemas.items() if s is not None})


def defs_model(schemas: Mapping[str, Any]) -> Mapping[str, Any]:
    common = schemas.get("common.schema.json") or {}
    return MappingProxyType({name: json.dumps(node, sort_keys=True) for name, node in common.get("$defs", {}).items()})


def enum_model(schemas: Mapping[str, Any]) -> Mapping[str, tuple[str, ...]]:
    common = schemas.get("common.schema.json") or {}
    return MappingProxyType({k: tuple(v["enum"]) for k, v in common.get("$defs", {}).items() if "enum" in v})


def event_registry(schemas: Mapping[str, Any]) -> tuple[str, ...]:
    env = schemas.get("event-envelope.schema.json") or {}
    return tuple(env.get("properties", {}).get("eventType", {}).get("enum", ()))


def leaf(path: str) -> str:
    return path.rsplit("/", 1)[-1]


def diff_schema(file: str, req: Mapping[str, Any], frz: Mapping[str, Any]) -> tuple[Finding, ...]:
    removed = sorted(set(frz) - set(req))
    added = sorted(set(req) - set(frz))
    parent = lambda p: p.rsplit("/", 1)[0]
    renames = tuple(
        (r, a) for r in removed for a in added if parent(r) == parent(a) and {k: v for k, v in frz[r].items()} == {k: v for k, v in req[a].items()}
    )
    renamed_from = {r for r, _ in renames}
    renamed_to = {a for _, a in renames}
    findings = [Finding("FIELD_RENAMED", f"{file}{r}", f"frozen `{leaf(r)}` appears renamed to `{leaf(a)}`") for r, a in renames]
    findings += [Finding("FIELD_ADDED", f"{file}{a}", "property not in the frozen contract (unapproved field)") for a in added if a not in renamed_to]
    findings += [Finding("FIELD_REMOVED", f"{file}{r}", "frozen property missing from requirements") for r in removed if r not in renamed_from]
    for p in sorted(set(req) & set(frz)):
        a, b = req[p], frz[p]
        checks = (
            ("TYPE_CHANGED", ("type", "items", "pattern", "bounds")),
            ("ENUM_CHANGED", ("enum", "const")),
            ("REQUIRED_CHANGED", ("required",)),
            ("NULLABILITY_CHANGED", ("nullable",)),
        )
        findings += [
            Finding(code, f"{file}{p}", "; ".join(f"{k}: requirements={a.get(k)!r} frozen={b.get(k)!r}" for k in keys if a.get(k) != b.get(k)))
            for code, keys in checks
            if any(a.get(k) != b.get(k) for k in keys)
        ]
    return tuple(findings)


# ------------------------------------------------------------------------------------------------- reference integrity


def broken_refs(side_name: str, schemas: Mapping[str, Any]) -> tuple[Finding, ...]:
    def refs(node: Any) -> Iterable[str]:
        if isinstance(node, dict):
            if "$ref" in node:
                yield node["$ref"]
            for v in node.values():
                yield from refs(v)
        elif isinstance(node, list):
            for v in node:
                yield from refs(v)

    def resolves(current: str, ref: str) -> bool:
        file_part, _, fragment = ref.partition("#")
        target = current if not file_part else posixpath.normpath(posixpath.join(posixpath.dirname(current), file_part))
        node = schemas.get(target)
        for part in (p for p in fragment.split("/") if p):
            if not isinstance(node, dict) or part not in node:
                return False
            node = node[part]
        return node is not None

    return tuple(
        Finding("BROKEN_REF", f"{side_name}:{f}", f"$ref {r!r} does not resolve")
        for f, s in schemas.items()
        if s is not None
        for r in refs(s)
        if not resolves(f, r)
    )


# ------------------------------------------------------------------------------------------------- side self-consistency


def side_consistency(side: Side, schema_dir: str, lock_path: str, manifest_path: str, pin: Mapping[str, Any]) -> tuple[Finding, ...]:
    bundle_bytes = side.read(f"{schema_dir}/bundle.json")
    bundle = load_json(bundle_bytes) or {}
    files = tuple(p for p in side.list_dir(schema_dir) if p.endswith(".schema.json"))
    listed = {e["path"]: e["sha256"] for e in bundle.get("schemas", ())}
    findings: list[Finding] = []
    findings += [Finding("BUNDLE_INCONSISTENT", f"{side.name}:{schema_dir}/{p}", "schema file not listed in bundle.json") for p in files if p not in listed]
    findings += [Finding("BUNDLE_INCONSISTENT", f"{side.name}:{schema_dir}/{p}", "bundle.json lists a missing schema file") for p in listed if p not in files]
    findings += [
        Finding("DIGEST_MISMATCH", f"{side.name}:{schema_dir}/{p}", f"bundle says {h}, file bytes are {sha256(side.read(f'{schema_dir}/{p}') or b'')}")
        for p, h in listed.items()
        if p in files and sha256(side.read(f"{schema_dir}/{p}") or b"") != h
    ]
    if list(listed) != sorted(listed):
        findings.append(Finding("BUNDLE_INCONSISTENT", f"{side.name}:{schema_dir}/bundle.json", "entries not sorted ordinally"))
    bundle_digest = sha256(bundle_bytes or b"")
    lock_bytes = side.read(lock_path)
    lock = load_json(lock_bytes) or {}
    manifest = load_json(side.read(manifest_path)) or {}
    schemas = {p: load_json(side.read(f"{schema_dir}/{p}")) for p in files}
    checks = (
        ("BUNDLE_DIGEST_MISMATCH", "bundle.json digest vs pin", bundle_digest, pin["schemaBundleSha256"]),
        ("BUNDLE_DIGEST_MISMATCH", "lock.schemaBundleSha256 vs bundle.json", lock.get("schemaBundleSha256"), bundle_digest),
        ("BUNDLE_DIGEST_MISMATCH", "root manifest schemaSha256 vs bundle.json", manifest.get("schemaSha256"), bundle_digest),
        ("LOCK_INCONSISTENT", "lock.json digest (release identity) vs pin", sha256(lock_bytes or b""), pin["releaseIdentitySha256"]),
        ("LOCK_INCONSISTENT", "lock.releaseTag vs pin", lock.get("releaseTag"), pin["tag"]),
        ("CONTRACT_VERSION_CHANGED", "lock.contractVersion", lock.get("contractVersion"), pin["contractVersion"]),
        ("CONTRACT_VERSION_CHANGED", "lock.formatVersion", lock.get("formatVersion"), pin["contractVersion"]),
        ("CONTRACT_VERSION_CHANGED", "bundle.contractVersion", bundle.get("contractVersion"), pin["contractVersion"]),
        ("CONTRACT_VERSION_CHANGED", "bundle.formatVersion", bundle.get("formatVersion"), pin["contractVersion"]),
        ("CONTRACT_VERSION_CHANGED", "root manifest formatVersion", manifest.get("formatVersion"), pin["contractVersion"]),
        ("CONTRACT_VERSION_CHANGED", "common formatVersion const", ((schemas.get("common.schema.json") or {}).get("$defs", {}).get("formatVersion") or {}).get("const"), pin["contractVersion"]),
        ("CONTRACT_VERSION_CHANGED", "common semanticVersion const", ((schemas.get("common.schema.json") or {}).get("$defs", {}).get("semanticVersion") or {}).get("const"), pin["contractVersion"]),
        ("DISCRIMINATOR_CHANGED", "lock.eventTypes vs envelope eventType enum", tuple(lock.get("eventTypes", ())), event_registry(schemas)),
        ("DISCRIMINATOR_CHANGED", "payload schema files vs envelope eventType enum", tuple(sorted(p[len("payloads/"):-len(".schema.json")] for p in files if p.startswith("payloads/"))), tuple(sorted(event_registry(schemas)))),
        ("DATASET_IDENTITY_MISMATCH", "root fixture manifest digest vs pin", sha256(side.read(manifest_path) or b""), pin["datasetIdentitySha256"]),
    )
    findings += [Finding(code, f"{side.name}:{what}", f"{actual!r} != {expected!r}") for code, what, actual, expected in checks if actual != expected]
    frozen_files = {e["path"]: e["sha256"] for e in lock.get("frozenFiles", ())}
    if frozen_files.get("fixtures/v1.0.0/manifest.json") not in (None, sha256(side.read(manifest_path) or b"")):
        findings.append(Finding("LOCK_INCONSISTENT", f"{side.name}:{manifest_path}", "root manifest digest differs from lock.frozenFiles"))
    findings += list(broken_refs(side.name, schemas))
    return tuple(findings)


def lock_vs_files(side: Side, lock_path: str, mapping: Mapping[str, str]) -> tuple[Finding, ...]:
    """Every file the release lock pins must hash to the lock's digest on this side (mapping: local path -> lock path)."""
    lock = load_json(side.read(lock_path)) or {}
    pinned = {e["path"]: e["sha256"] for e in lock.get("frozenFiles", ())}
    return tuple(
        Finding("LOCK_INCONSISTENT", f"{side.name}:{local}", f"lock pins {pinned[frozen]}, file is {sha256(side.read(local) or b'')}")
        for local, frozen in mapping.items()
        if frozen in pinned and frozen != lock_path and sha256(side.read(local) or b"") != pinned[frozen]
    )


# ------------------------------------------------------------------------------------------------- the gate


def tag_identity(repo: Path | None, pin: Mapping[str, Any]) -> tuple[Finding, ...]:
    if repo is None:
        return ()
    tag_obj = git(repo, "rev-parse", f"refs/tags/{pin['tag']}").stdout.decode().strip()
    commit = git(repo, "rev-parse", f"refs/tags/{pin['tag']}^{{commit}}").stdout.decode().strip()
    return tuple(
        Finding("TAG_MOVED", f"causa-data:refs/tags/{pin['tag']}", f"{what} is {actual or '(missing)'}, pinned {expected}")
        for what, actual, expected in (("tag object", tag_obj, pin["tagObject"]), ("tagged commit", commit, pin["commit"]))
        if actual != expected
    )


def run_gate(req: Side, frozen: Side, pin: Mapping[str, Any], approved: Iterable[Mapping[str, str]], repo: Path | None) -> tuple[tuple[Finding, ...], tuple[str, ...]]:
    mapping = frozen_paths(req)
    frozen_schema_files = frozen.list_dir(SCHEMA_DIR_FROZEN)
    frozen_golden_files = frozen.list_dir(GOLDEN_DIR_FROZEN)
    expected_local = (
        set(IMPORT_MAP_FIXED)
        | {f"{SCHEMA_DIR_REQ}/{p}" for p in frozen_schema_files}
        | {f"{GOLDEN_DIR_REQ}/{p}" for p in frozen_golden_files}
    )
    present_local = {p for p in mapping if req.read(p) is not None}
    inventory = tuple(Finding("MISSING_FILE", f"requirements:{p}", "frozen release file not imported") for p in sorted(expected_local - present_local)) + tuple(
        Finding("ADDED_FILE", f"requirements:{p}", "imported-contract file with no counterpart in the frozen release") for p in sorted(present_local - expected_local)
    )
    content = tuple(
        Finding("DIGEST_MISMATCH", f"requirements:{local}", f"sha256 {sha256(req.read(local) or b'')} != frozen {sha256(frozen.read(frz) or b'')}")
        for local, frz in sorted(mapping.items())
        if local in present_local & expected_local and req.read(local) != frozen.read(frz)
    )
    req_schemas = {p: load_json(req.read(f"{SCHEMA_DIR_REQ}/{p}")) for p in req.list_dir(SCHEMA_DIR_REQ) if p.endswith(".schema.json")}
    frz_schemas = {p: load_json(frozen.read(f"{SCHEMA_DIR_FROZEN}/{p}")) for p in frozen_schema_files if p.endswith(".schema.json")}
    rm, fm = schema_model(req_schemas), schema_model(frz_schemas)
    semantic = tuple(f for p in sorted(set(rm) & set(fm)) for f in diff_schema(p, rm[p], fm[p]))
    req_enums, frz_enums = enum_model(req_schemas), enum_model(frz_schemas)
    enums = tuple(
        Finding("ENUM_CHANGED", f"common.schema.json#/$defs/{n}", f"requirements={req_enums.get(n)} frozen={frz_enums.get(n)}")
        for n in sorted(set(req_enums) | set(frz_enums))
        if req_enums.get(n) != frz_enums.get(n)
    )
    rd, fd = defs_model(req_schemas), defs_model(frz_schemas)
    defs = tuple(
        Finding("TYPE_CHANGED", f"common.schema.json#/$defs/{n}", "shared definition differs")
        for n in sorted(set(rd) | set(fd))
        if rd.get(n) != fd.get(n) and n not in req_enums and n not in frz_enums
    )
    registry = (
        (Finding("DISCRIMINATOR_CHANGED", "event-envelope eventType", f"requirements={event_registry(req_schemas)} frozen={event_registry(frz_schemas)}"),)
        if event_registry(req_schemas) != event_registry(frz_schemas)
        else ()
    )
    findings = (
        inventory
        + content
        + semantic
        + enums
        + defs
        + registry
        + side_consistency(req, SCHEMA_DIR_REQ, "compatibility/lock.json", "fixtures/manifest.json", pin)
        + side_consistency(frozen, SCHEMA_DIR_FROZEN, "compatibility/v1.0.0/lock.json", "fixtures/v1.0.0/manifest.json", pin)
        + lock_vs_files(req, "compatibility/lock.json", mapping)
        + lock_vs_files(frozen, "compatibility/v1.0.0/lock.json", {v: v for v in mapping.values()})
        + tag_identity(repo, pin)
    )
    approvals = {(a["code"], a["subject"]) for a in approved}
    unexplained = tuple(f for f in dict.fromkeys(findings) if (f.code, f.subject) not in approvals)
    compared = (
        f"files compared: {len(expected_local)}",
        f"schemas compared semantically: {len(set(rm) & set(fm))} ({sum(len(v) for v in fm.values())} property descriptors)",
        f"event registry: {len(event_registry(frz_schemas))} discriminators",
        f"enumerations: {len(frz_enums)}",
        f"schema identity: {sha256(frozen.read(f'{SCHEMA_DIR_FROZEN}/bundle.json') or b'')}",
        f"release identity (lock.json): {sha256(frozen.read('compatibility/v1.0.0/lock.json') or b'')}",
        f"dataset identity (fixtures manifest): {sha256(frozen.read('fixtures/v1.0.0/manifest.json') or b'')}",
    )
    return unexplained, compared


# Non-frozen normative files whose bytes are pinned in release-pin.json "requirementsDigests" (review F-10). Generated
# catalogs are covered by generate_catalogs.py check instead. Changing one of these files requires a reviewed update of
# the pin; the gate never writes the pin.
PINNED_REQUIREMENTS = (
    "README.md", "SEMANTICS.md", "INVARIANTS.md", "CLARIFICATIONS.md", "PROVIDER-GITHUB.md",
    "compatibility/RULES.md", "acceptance/ACCEPTANCE.md", "acceptance/TEST-MATRIX.json",
    "acceptance/supplementary/MATRIX.json", "catalog/field-semantics.json", "catalog/event-semantics.json",
    "catalog/artifact-url-vectors.jsonl",
)


def current_requirement_digests(root: Path) -> Mapping[str, str | None]:
    return MappingProxyType({p: (sha256((root / p).read_bytes()) if (root / p).is_file() else None) for p in PINNED_REQUIREMENTS})


def requirement_pin_findings(root: Path, pin: Mapping[str, Any]) -> tuple[Finding, ...]:
    pinned = pin.get("requirementsDigests", {})
    current = current_requirement_digests(root)
    return tuple(
        Finding("REQUIREMENT_UNPINNED_CHANGE", f"requirements:{p}", f"sha256 {current[p]} != pinned {pinned.get(p)} (update release-pin.json in a reviewed commit)")
        for p in PINNED_REQUIREMENTS
        if current[p] != pinned.get(p)
    )


def load_pin(root: Path) -> tuple[Mapping[str, Any], tuple[Mapping[str, str], ...]]:
    pin = json.loads((root / "compatibility" / "release-pin.json").read_text("utf-8"))
    return pin, tuple(pin.get("approvedDifferences", ()))


def report(findings: tuple[Finding, ...], compared: tuple[str, ...]) -> int:
    for line in compared:
        print(f"INFO {line}")
    for f in findings:
        print(f"FAIL {f.code} {f.subject}: {f.detail}")
    print(("FAIL" if findings else "PASS") + f" parity {len(findings)} unexplained difference(s)")
    return 1 if findings else 0


# ------------------------------------------------------------------------------------------------- self-test


def mutate_json(path: Path, change: Callable[[Any], Any]) -> None:
    path.write_text(json.dumps(change(json.loads(path.read_text("utf-8"))), indent=2) + "\n", "utf-8")


def with_prop(schema: dict, name: str, value: Any) -> dict:
    return {**schema, "properties": {**schema["properties"], name: value}}


def without_prop(schema: dict, name: str) -> dict:
    return {**schema, "properties": {k: v for k, v in schema["properties"].items() if k != name}, "required": [r for r in schema.get("required", []) if r != name]}


def renamed_prop(schema: dict, old: str, new: str) -> dict:
    props = {(new if k == old else k): v for k, v in schema["properties"].items()}
    return {**schema, "properties": props, "required": [new if r == old else r for r in schema.get("required", [])]}


def set_enum(schema: dict, name: str, values: list[str]) -> dict:
    return {**schema, "$defs": {**schema["$defs"], name: {**schema["$defs"][name], "enum": values}}}


MUTATIONS: tuple[tuple[str, str, Callable[[Path], None]], ...] = (
    ("missing schema file", "MISSING_FILE", lambda r: (r / "schemas/payloads/FindingRecorded.schema.json").unlink()),
    ("added schema file", "ADDED_FILE", lambda r: shutil.copy(r / "schemas/actor.schema.json", r / "schemas/extra.schema.json")),
    ("added unapproved field", "FIELD_ADDED", lambda r: mutate_json(r / "schemas/payloads/EvidenceAdmitted.schema.json", lambda s: with_prop(s, "confidence", {"type": "integer"}))),
    ("renamed field", "FIELD_RENAMED", lambda r: mutate_json(r / "schemas/payloads/EvidenceAdmitted.schema.json", lambda s: renamed_prop(s, "source", "origin"))),
    ("removed field", "FIELD_REMOVED", lambda r: mutate_json(r / "schemas/payloads/EvidenceAdmitted.schema.json", lambda s: without_prop(s, "capturedAt"))),
    ("type change", "TYPE_CHANGED", lambda r: mutate_json(r / "schemas/snapshot.schema.json", lambda s: with_prop(s, "aggregateVersion", {"type": "string"}))),
    ("enum value change", "ENUM_CHANGED", lambda r: mutate_json(r / "schemas/common.schema.json", lambda s: set_enum(s, "materiality", ["blocking", "material", "nonMaterial", "informational", "other"]))),
    ("requiredness change", "REQUIRED_CHANGED", lambda r: mutate_json(r / "schemas/payloads/HypothesisRecorded.schema.json", lambda s: {**s, "required": [x for x in s["required"] if x != "mechanism"]})),
    ("nullability change", "NULLABILITY_CHANGED", lambda r: mutate_json(r / "schemas/payloads/HypothesisRecorded.schema.json", lambda s: with_prop(s, "mechanism", {"$ref": "../common.schema.json#/$defs/text"}))),
    ("whitespace-only schema edit", "DIGEST_MISMATCH", lambda r: (r / "schemas/actor.schema.json").write_bytes((r / "schemas/actor.schema.json").read_bytes() + b"\n")),
    ("bundle.json edited", "BUNDLE_DIGEST_MISMATCH", lambda r: (r / "schemas/bundle.json").write_bytes((r / "schemas/bundle.json").read_bytes().replace(b'"RFC8785"', b'"RFC8785" '))),
    ("contract version changed", "CONTRACT_VERSION_CHANGED", lambda r: mutate_json(r / "compatibility/lock.json", lambda s: {**s, "contractVersion": "1.1.0"})),
    ("event discriminator changed", "DISCRIMINATOR_CHANGED", lambda r: mutate_json(r / "schemas/event-envelope.schema.json", lambda s: {**s, "properties": {**s["properties"], "eventType": {"enum": [("EvidenceAccepted" if e == "EvidenceAdmitted" else e) for e in s["properties"]["eventType"]["enum"]]}}})),
    ("broken schema reference", "BROKEN_REF", lambda r: mutate_json(r / "schemas/entities/finding.schema.json", lambda s: with_prop(s, "actionId", {"$ref": "../common.schema.json#/$defs/uuidd"}))),
    ("inconsistent release lock", "LOCK_INCONSISTENT", lambda r: mutate_json(r / "compatibility/lock.json", lambda s: {**s, "releaseTag": "causa-storage-v1.0.1"})),
    ("golden projection removed", "MISSING_FILE", lambda r: (r / "golden/api-latency.state.json").unlink()),
    ("fixture manifest edited", "DATASET_IDENTITY_MISMATCH", lambda r: (r / "fixtures/manifest.json").write_bytes((r / "fixtures/manifest.json").read_bytes().replace(b"causa-demo-v1", b"causa-demo-v2"))),
)


# A file or field missing on the frozen side is, seen from the requirements, an addition (and vice versa).
MIRROR = MappingProxyType({"MISSING_FILE": "ADDED_FILE", "ADDED_FILE": "MISSING_FILE", "FIELD_ADDED": "FIELD_REMOVED", "FIELD_REMOVED": "FIELD_ADDED"})


def materialize(side: Side, paths: Iterable[str], into: Path) -> None:
    for p in paths:
        data = side.read(p)
        if data is not None:
            (into / p).parent.mkdir(parents=True, exist_ok=True)
            (into / p).write_bytes(data)


def selftest(repo: Path) -> int:
    pin, approved = load_pin(REQ_ROOT)
    frozen = tag_side(repo, pin["tag"])
    baseline, _ = run_gate(requirement_side(REQ_ROOT), frozen, pin, approved, repo)
    results = [("baseline (unmodified) passes", not baseline, ", ".join(f.code for f in baseline))]
    for side_label in ("requirements", "frozen"):
        for label, expected, mutate in MUTATIONS:
            with tempfile.TemporaryDirectory() as tmp:
                work = Path(tmp)
                if side_label == "requirements":
                    shutil.copytree(REQ_ROOT, work / "req")
                    mutate(work / "req")
                    req_side, frz_side = requirement_side(work / "req"), frozen
                else:
                    # mirror the mutation onto a materialized copy of the frozen tree (layout translated)
                    shutil.copytree(REQ_ROOT, work / "req")
                    materialize(frozen, (frz for frz in frozen_paths(requirement_side(REQ_ROOT)).values()), work / "frz")
                    shadow = work / "shadow"
                    shutil.copytree(REQ_ROOT, shadow)
                    mutate(shadow)
                    for local, frz in frozen_paths(requirement_side(REQ_ROOT)).items():
                        target = work / "frz" / frz
                        if (shadow / local).exists():
                            target.parent.mkdir(parents=True, exist_ok=True)
                            target.write_bytes((shadow / local).read_bytes())
                        elif target.exists():
                            target.unlink()
                    for p in (shadow / "schemas").rglob("*.schema.json"):
                        rel = p.relative_to(shadow / "schemas").as_posix()
                        dst = work / "frz" / SCHEMA_DIR_FROZEN / rel
                        if not dst.exists():
                            dst.parent.mkdir(parents=True, exist_ok=True)
                            dst.write_bytes(p.read_bytes())
                    req_side, frz_side = requirement_side(work / "req"), dir_side("frozen(mutated)", work / "frz")
                found, _ = run_gate(req_side, frz_side, pin, approved, None)
                codes = sorted({f.code for f in found})
                want = MIRROR.get(expected, expected) if side_label == "frozen" else expected
                results.append((f"{side_label}: {label} -> {want}", want in codes, ", ".join(codes)))
    for name, ok, codes in results:
        print(f"{'PASS' if ok else 'FAIL'} selftest {name}" + (f" [{codes}]" if codes else ""))
    failures = sum(1 for _, ok, _ in results if not ok)
    print(("FAIL" if failures else "PASS") + f" selftest {len(results) - failures}/{len(results)}")
    return 1 if failures else 0


def main(argv: list[str]) -> int:
    mode = argv[1] if len(argv) > 1 else "check"
    if "--causa-data" not in argv:
        print("usage: parity_gate.py check|selftest --causa-data <causa-data git checkout>")
        return 2
    repo = Path(argv[argv.index("--causa-data") + 1])
    if mode == "selftest":
        return selftest(repo)
    if mode == "digests":
        print(json.dumps(dict(current_requirement_digests(REQ_ROOT)), indent=2))
        return 0
    pin, approved = load_pin(REQ_ROOT)
    findings, compared = run_gate(requirement_side(REQ_ROOT), tag_side(repo, pin["tag"]), pin, approved, repo)
    return report(findings + requirement_pin_findings(REQ_ROOT, pin), compared + (f"pinned requirement files: {len(PINNED_REQUIREMENTS)}",))


if __name__ == "__main__":
    sys.exit(main(sys.argv))
