#!/usr/bin/env python3
"""Self-test: the §10.4.1 artifact-URL verdict against every normative vector in
requirements/causa/data/v1.0.0/catalog/artifact-url-vectors.jsonl (field `expected`).

    python3 -I selftest_urls.py [vectors.jsonl]

Last stdout line: {"vectors": N, "agree": A, "disagree": D, "byExpected": {...}}.  Exit status 0 iff D == 0.
`dotnetAccepted` is not read: the verdict must come from the procedure, not from the oracle's answer.
"""
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from schemas import requirements_dir  # noqa: E402
from semantics import Unresolved, artifact_url_verdict  # noqa: E402


def label(verdict) -> str:
    return "platformDefined" if isinstance(verdict, Unresolved) else ("wellFormed" if verdict else "illFormed")


def main(argv):
    path = argv[0] if argv else os.path.join(requirements_dir(), "catalog", "artifact-url-vectors.jsonl")
    vectors = [json.loads(line) for line in open(path, encoding="utf-8") if line.strip()]
    results = [(v, label(artifact_url_verdict(v["url"]))) for v in vectors]
    bad = [(v, got) for v, got in results if got != v["expected"]]
    for v, got in bad[:20]:
        print(f"DISAGREE expected={v['expected']} got={got} url={v['url']!r}", file=sys.stderr)
    by = {}
    for v, got in results:
        k = v["expected"]
        by.setdefault(k, {"total": 0, "agree": 0})
        by[k] = {"total": by[k]["total"] + 1, "agree": by[k]["agree"] + (got == k)}
    print(json.dumps({"vectors": len(vectors), "agree": len(vectors) - len(bad), "disagree": len(bad),
                      "byExpected": by}))
    return 0 if not bad else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
