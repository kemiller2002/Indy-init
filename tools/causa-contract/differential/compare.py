#!/usr/bin/env python3
"""Compare a consumer's verdicts with the engine oracle on a mutant corpus produced by mutate.py.

Usage: python3 -I compare.py <mutants dir> <oracle verdicts .jsonl> --consumer "<cmd>" --causa-data <root> [--json out]

Agreement rule (stage 15 is the only stage a mutant can fail before 17, see mutate.py):
  oracle admissible   -> consumer primaryCode in {null, CAUSA.SNAPSHOT_HASH_MISMATCH, CAUSA.SNAPSHOT_MISMATCH}
  oracle inadmissible -> consumer primaryCode = CAUSA.HISTORY_INADMISSIBLE and domainCode = oracle's first code
A consumer answer "admissibility": "not-evaluated" is never agreement.
"""
from __future__ import annotations

import json
import shlex
import subprocess
import sys
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from typing import Any, Mapping

AFTER_15 = {None, "CAUSA.SNAPSHOT_HASH_MISMATCH", "CAUSA.SNAPSHOT_MISMATCH"}


def verdict(consumer: str, dataset: Path, inv_dir: Path) -> Mapping[str, Any]:
    p = subprocess.run([*shlex.split(consumer), "verify", "--dataset", str(dataset), "--investigation", str(inv_dir)], capture_output=True, text=True, timeout=300)
    try:
        return json.loads(p.stdout.strip().splitlines()[-1])
    except (ValueError, IndexError):
        return {"protocolError": p.stderr[-300:]}


def agree(oracle_codes: list[str], got: Mapping[str, Any]) -> bool:
    if got.get("admissibility") != "evaluated" and got.get("primaryCode") in AFTER_15 | {"CAUSA.HISTORY_INADMISSIBLE"}:
        return False
    if not oracle_codes:
        return got.get("primaryCode") in AFTER_15
    return got.get("primaryCode") == "CAUSA.HISTORY_INADMISSIBLE" and got.get("domainCode") == oracle_codes[0]


def main(argv: list[str]) -> int:
    mutants, verdict_file = Path(argv[1]), Path(argv[2])
    consumer = argv[argv.index("--consumer") + 1]
    dataset = Path(argv[argv.index("--causa-data") + 1])
    oracle = {Path(d["file"]).parent.name: d["codes"] for d in map(json.loads, verdict_file.read_text().splitlines())}
    labels = sorted(oracle)
    dirs = {lab: next(p for p in (mutants / lab).iterdir() if p.is_dir()) for lab in labels}
    with ThreadPoolExecutor(max_workers=8) as pool:
        got = dict(zip(labels, pool.map(lambda lab: verdict(consumer, dataset, dirs[lab]), labels)))
    rows = [{"label": lab, "oracle": oracle[lab][:1], "consumer": [got[lab].get("primaryCode"), got[lab].get("domainCode")], "agree": agree(oracle[lab], got[lab])} for lab in labels]
    bad = [r for r in rows if not r["agree"]]
    by_class = Counter((r["oracle"][0] if r["oracle"] else "ADMISSIBLE", r["agree"]) for r in rows)
    for r in bad:
        print(f"DISAGREE {r['label']} oracle={r['oracle']} consumer={r['consumer']}")
    for (cls, ok), n in sorted(by_class.items()):
        print(f"INFO {cls} {'agree' if ok else 'disagree'} {n}")
    print(("PASS" if not bad else "FAIL") + f" differential {len(rows) - len(bad)}/{len(rows)} agree")
    if "--json" in argv:
        Path(argv[argv.index("--json") + 1]).write_text(json.dumps({"total": len(rows), "agree": len(rows) - len(bad), "disagreements": bad}, indent=1) + "\n")
    return 0 if not bad else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
