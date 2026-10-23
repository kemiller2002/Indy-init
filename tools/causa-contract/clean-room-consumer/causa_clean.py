#!/usr/bin/env python3
"""Clean-room Causa storage v1.0.0 consumer (ACCEPTANCE.md §3 consumer protocol, CLARIFICATIONS.md).

  causa_clean.py info               --dataset <root>
  causa_clean.py verify             --dataset <root> --investigation <dir>
  causa_clean.py negative           --dataset <root> --fixture <file>
  causa_clean.py url-verdict        --vectors <artifact-url-vectors.jsonl>
  causa_clean.py append-observation --dir <dir> --expected-version N --observation-id <uuid> --original <text>
                                    --occurred-at <timestamp> --actor-id <text> --actor-kind human|system
                                    --correlation-id <uuid>

The last stdout line is always one JSON object.  I/O lives here and in writer.py's filesystem edge.
"""
from __future__ import annotations

import argparse
import base64
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))   # -I drops the script directory from sys.path

from canon import Failure, canonical, is_failure, parse_json  # noqa: E402
from reader import append_lines, successor, verify  # noqa: E402
from schemas import first_violation, load_schemas  # noqa: E402
from semantics import Unresolved, artifact_url_verdict  # noqa: E402
from writer import path_check, plan_observation_append, publish, read_dir, recover  # noqa: E402


def emit(obj: dict) -> int:
    print(json.dumps(obj, ensure_ascii=True))
    return 0


def failure_json(f: Failure, **extra) -> dict:
    return {"primaryCode": f.code, "domainCode": f.domain, "admissibility": "evaluated", "stateSha256": None,
            "eventsSha256": None, "manifestSha256": None, "snapshotSha256": None, "version": None,
            "lifecycle": None, "detail": f.detail, **extra}


# --------------------------------------------------------------------------------------------------------------------
# Dataset lookups (root manifest, scenario directories)
# --------------------------------------------------------------------------------------------------------------------

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
    """CAUSA-CLAR-06: the base is found through the root manifest by scenarioKey; directoryId is never a path."""
    entry = next((i for i in root["investigations"] if i["scenarioKey"] == key), None)
    if entry is None:
        return Failure("CAUSA.FIXTURE_DRIFT", f"unknown scenario {key!r}")
    return entry["investigationId"], os.path.join(dataset, "fixtures", "v1.0.0", "investigations",
                                                  entry["investigationId"])


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
    if kind == "investigation":                       # in memory only: directoryId is data (CAUSA-CLAR-06)
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


def url_label(url: str) -> str:
    v = artifact_url_verdict(url)
    return "platformDefined" if isinstance(v, Unresolved) else ("wellFormed" if v else "illFormed")


def cmd_url_verdict(args, schemas) -> int:
    """ACCEPTANCE §3: one verdict per input line, in order (SEMANTICS §10.4.1-§10.4.2)."""
    lines = open(args.vectors, encoding="utf-8").read().split("\n")
    return emit({"verdicts": [url_label(json.loads(line)["url"]) for line in lines if line.strip()]})


def cmd_append_observation(args, schemas) -> int:
    out = lambda code, domain, version, detail: emit(  # noqa: E731
        {"primaryCode": code, "domainCode": domain, "version": version, "detail": detail})
    path = os.path.normpath(os.path.abspath(args.dir))
    frozen = path_check(os.path.realpath(path))                                   # CLAR-03: path first
    if frozen:
        return out(frozen.code, None, None, frozen.detail)
    note = recover(path, schemas)                                                 # CLAR-04 recovery
    if not os.path.isdir(path):
        return out("CAUSA.PATH_INVALID", None, None, "investigation directory does not exist")
    files = read_dir(path)
    plan = plan_observation_append(os.path.basename(path), files, args, schemas)
    if is_failure(plan):
        return out(plan.code, plan.domain, None, plan.detail)
    new_files, new_version = plan
    unconfirmed = publish(path, new_files)                                        # CLAR-04 publish + confirm
    if unconfirmed:
        return out(unconfirmed.code, None, None, unconfirmed.detail)
    return out(None, None, new_version, "appended" + (f" ({note})" if note else ""))


# --------------------------------------------------------------------------------------------------------------------

class JsonArgumentParser(argparse.ArgumentParser):
    """Keep the protocol promise (last stdout line is one JSON object) even for usage errors."""

    def error(self, message):
        emit({"primaryCode": None, "error": "usage", "detail": message})
        sys.exit(2)


def parser() -> argparse.ArgumentParser:
    p = JsonArgumentParser(prog="causa_clean")
    sub = p.add_subparsers(dest="command", required=True, parser_class=JsonArgumentParser)
    sub.add_parser("info").add_argument("--dataset")
    v = sub.add_parser("verify")
    v.add_argument("--dataset")
    v.add_argument("--investigation", required=True)
    n = sub.add_parser("negative")
    n.add_argument("--dataset", required=True)
    n.add_argument("--fixture", required=True)
    u = sub.add_parser("url-verdict")
    u.add_argument("--vectors", required=True)
    a = sub.add_parser("append-observation")
    a.add_argument("--dir", required=True)
    a.add_argument("--expected-version", type=int, required=True)
    for opt in ("--observation-id", "--original", "--occurred-at", "--actor-id", "--actor-kind", "--correlation-id"):
        a.add_argument(opt, required=True)
    return p


def main(argv: list[str]) -> int:
    args = parser().parse_args(argv)
    if args.command == "url-verdict":
        return cmd_url_verdict(args, None)
    schemas = load_schemas()
    if is_failure(schemas):
        return emit(failure_json(schemas))
    handler = {"info": cmd_info, "verify": cmd_verify, "negative": cmd_negative,
               "append-observation": cmd_append_observation}[args.command]
    return handler(args, schemas)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
