"""Dataset verification (CAUSA-CLAR-10; CONTRACT §4, §10, §11.1 dataset-level codes, §12).

Order (CLAR-10 §1):
  1. framing, canonical form, versions and schema of the root manifest, bundle.json, golden files and negative
     fixtures, with the stage codes;
  2. entry checks with CAUSA.FIXTURE_DRIFT (root-manifest entries vs investigations, bundle, negative listing);
  3. golden/scenarios.json and golden states with CAUSA.GOLDEN_MISMATCH.
`verify_dataset` is pure over a `DatasetView` (bytes keyed by relative path); `load_view` does the reading.
"""
from __future__ import annotations

import os
from dataclasses import dataclass

from canon import Failure, digest_json, is_failure, sha256_hex
from reader import FILES, framing, noncanonical, parse_doc, schema_violation, unsupported_version, verify
from semantics import groups_of

FIX, CON = "fixtures/v1.0.0", "contract/v1.0.0"


@dataclass(frozen=True)
class DatasetView:
    files: dict            # relative path -> bytes, for every file the checks need
    investigation_dirs: tuple
    negative_files: tuple  # names under fixtures/v1.0.0/negative/
    schema_files: tuple    # relative paths of *.schema.json under contract/v1.0.0/schemas/


def load_view(root: str) -> DatasetView:
    """Impure: read the dataset files that dataset verification inspects."""
    def rd(rel):
        p = os.path.join(root, rel)
        return open(p, "rb").read() if os.path.isfile(p) else None
    inv_root = os.path.join(root, FIX, "investigations")
    dirs = tuple(sorted(os.listdir(inv_root))) if os.path.isdir(inv_root) else ()
    neg_root = os.path.join(root, FIX, "negative")
    negs = tuple(sorted(os.listdir(neg_root))) if os.path.isdir(neg_root) else ()
    sroot = os.path.join(root, CON, "schemas")
    schema_files = tuple(sorted(os.path.relpath(os.path.join(r, f), sroot).replace(os.sep, "/")
                                for r, _, fs in os.walk(sroot) for f in fs if f.endswith(".schema.json")))
    golden_root = os.path.join(root, CON, "golden")
    golden = sorted(os.listdir(golden_root)) if os.path.isdir(golden_root) else []
    rels = ([f"{FIX}/manifest.json", f"{CON}/schemas/bundle.json"]
            + [f"{CON}/golden/{g}" for g in golden]
            + [f"{FIX}/negative/{n}" for n in negs]
            + [f"{CON}/schemas/{s}" for s in schema_files]
            + [f"{FIX}/investigations/{d}/{f}" for d in dirs for f in
               (sorted(os.listdir(os.path.join(inv_root, d))) if os.path.isdir(os.path.join(inv_root, d)) else [])])
    return DatasetView({r: rd(r) for r in rels}, dirs, negs, schema_files)


def _json_file(rel: str, data, validator, version_keys=("formatVersion",)):
    """Stage codes for one dataset JSON file: framing, JSON, canonical, version, schema. Returns doc or Failure."""
    if data is None:
        return Failure("CAUSA.PATH_INVALID", f"{rel}: missing")
    f = framing(rel, data, False)
    if f:
        return f
    text = data.decode("utf-8")[:-1]
    doc = parse_doc(rel, text)
    if is_failure(doc):
        return doc
    return (noncanonical(rel, doc, text) or unsupported_version(rel, doc, version_keys)
            or (schema_violation(rel, validator, doc) if validator else None) or doc)


def _drift(msg: str) -> Failure:
    return Failure("CAUSA.FIXTURE_DRIFT", msg)


def _first_failure(results):
    return next((r for r in results if is_failure(r)), None)


def verify_dataset(view: DatasetView, schemas) -> Failure | str:
    """Returns a Failure (first failing check) or a summary string."""
    v = schemas.by_schema
    root_rel, bundle_rel = f"{FIX}/manifest.json", f"{CON}/schemas/bundle.json"
    # ---- 1. stage codes --------------------------------------------------------------------------------------
    root = _json_file(root_rel, view.files.get(root_rel), v["root-manifest.schema.json"])
    if is_failure(root):
        return root
    bundle = _json_file(bundle_rel, view.files.get(bundle_rel), v["schema-bundle.schema.json"])
    if is_failure(bundle):
        return bundle
    keys = [i["scenarioKey"] for i in root["investigations"]]
    golden_rels = {k: f"{CON}/golden/{k}.state.json" for k in keys}
    goldens = {k: _json_file(rel, view.files.get(rel), v["state.schema.json"], ()) for k, rel in golden_rels.items()}
    scen_rel = f"{CON}/golden/scenarios.json"
    scenarios = _json_file(scen_rel, view.files.get(scen_rel), v["golden-scenarios.schema.json"])
    negs = {n["path"]: _json_file(f"{FIX}/{n['path']}", view.files.get(f"{FIX}/{n['path']}"),
                                  v["negative-fixture.schema.json"]) for n in root["negativeFixtures"]}
    bad = _first_failure([*goldens.values(), scenarios, *negs.values()])
    if bad:
        return bad
    # ---- 2. entry checks (FIXTURE_DRIFT) -----------------------------------------------------------------------
    if sha256_hex(view.files[bundle_rel]) != root["schemaSha256"]:
        return _drift("root manifest schemaSha256 != SHA-256 of contract/v1.0.0/schemas/bundle.json")
    if root["schemaSha256"] != schemas.identity:
        return _drift("dataset schema identity != requirements schema identity")
    listed = [e["path"] for e in bundle["schemas"]]
    if listed != sorted(listed) or sorted(listed) != list(view.schema_files):
        return _drift("bundle.json does not list exactly the schema files, sorted")
    wrong = [e["path"] for e in bundle["schemas"]
             if sha256_hex(view.files.get(f"{CON}/schemas/{e['path']}") or b"") != e["sha256"]]
    if wrong:
        return _drift(f"schema file digests differ from bundle.json: {wrong[:3]}")
    ids = [i["investigationId"] for i in root["investigations"]]
    if ids != sorted(ids) or len(set(ids)) != len(ids) or len(set(keys)) != len(keys):
        return _drift("root manifest investigations are not ordered by id with unique ids and keys")
    neg_paths = [n["path"] for n in root["negativeFixtures"]]
    if any(p.startswith("investigations/") for p in neg_paths) or len(set(neg_paths)) != len(neg_paths):
        return _drift("a negativeFixtures path is also an investigation, or is listed twice")
    for n in root["negativeFixtures"]:          # entry field vs file (CONTRACT §11.1 "any root-manifest entry field")
        if negs[n["path"]]["expectedErrorCode"] != n["expectedErrorCode"]:
            return _drift(f"{n['path']}: expectedErrorCode differs from the root manifest")
    reports = {}
    for e in root["investigations"]:
        iid = e["investigationId"]
        prefix = f"{FIX}/investigations/{iid}/"
        files = {r[len(prefix):]: b for r, b in view.files.items() if r.startswith(prefix)}
        r = verify(iid, files, schemas)
        if r.failure:                           # a reader-stage failure keeps its own code (CLAR-10 §2)
            return r.failure
        if r.admissibility != "evaluated":
            return Failure("CAUSA.HISTORY_INADMISSIBLE", f"{iid}: admissibility not evaluated: {r.detail}")
        got = {"investigationId": iid, "scenarioKey": e["scenarioKey"], "eventsSha256": r.digests["events"], "manifestSha256": r.digests["manifest"],
               "snapshotSha256": r.digests["snapshot"], "expectedStateSha256": digest_json(r.state),
               "expectedVersion": r.state["version"], "expectedLifecycle": r.state["lifecycle"],
               "title": r.state["title"]}
        diff = [k for k, val in got.items() if e[k] != val]
        if diff:
            return _drift(f"{iid}: root manifest entry differs in {diff}")
        reports[e["scenarioKey"]] = r
    # ---- 3. golden (GOLDEN_MISMATCH) ----------------------------------------------------------------------------
    for k, r in reports.items():
        if digest_json(goldens[k]) != digest_json(r.state):
            return Failure("CAUSA.GOLDEN_MISMATCH", f"golden/{k}.state.json differs from the replayed state")
    by_key = {s["scenarioKey"]: s for s in scenarios["scenarios"]}
    if sorted(by_key) != sorted(keys):
        return Failure("CAUSA.GOLDEN_MISMATCH", "scenarios.json scenario keys differ from the investigations")
    for k, r in reports.items():
        s = by_key[k]
        events = list(r.events)
        expect = {"investigationId": r.state["investigationId"], "events": len(events),
                  "eventTypes": sorted({e["eventType"] for e in events}),
                  "commands": len(groups_of(events)) + len(s["refusals"])}
        diff = [f for f, val in expect.items() if s[f] != val]
        if diff:
            return Failure("CAUSA.GOLDEN_MISMATCH", f"scenarios.json {k}: {diff} contradict the investigation")
    return (f"dataset valid: {len(ids)} investigations, {len(neg_paths)} negative fixtures, "
            f"{len(listed)} schemas, golden files agree")
