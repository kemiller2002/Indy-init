#!/usr/bin/env python3
"""Draft the Causa requirements baseline (praxis.requirements-baseline/1).

stdlib only; deterministic; fail closed. Reads the requirement documents
listed in DOCUMENTS from a checkout of this repository and writes the
baseline JSON to stdout. The manifest in requirements/ is the authority; this
script only drafts it mechanically, so that it can be regenerated and
reviewed without hand-copied text:

  python3 -I tools/build-requirements-baseline.py . COMMIT > requirements/causa-requirements-baseline.json

Mechanical rules (see requirements/README.md):
- A requirement is a line the document's requirementPattern captures an id
  from (a heading, bold paragraph or list item), plus every other heading
  section of level 1-3 that has a body of its own, except the sections named
  in EXCLUDED_SECTIONS (goal statements, open items, history, references).
- Section boundaries and hashes follow Praxis's requirements-preservation
  contract (docs/requirements-preservation.md in kemiller2002/praxis).
- Obligations are the verbatim units of the section body: each table data
  row, and each sentence of every other line (document "Status:" lines are
  metadata, not obligations). Nothing is paraphrased; the
  source's own modality (MUST, should, may) stays in the text.
- Tokens are the concrete values an obligation contains (colours, ratios,
  percentages, sizes, timings, versions, HTTP status codes, commit SHAs).
- kind: acceptance when the unit names acceptance, tests, screenshots or
  verify/verified (or its section is an acceptance section); else value when it
  has tokens; else constraint.
"""
import hashlib
import json
import re
import sys
from pathlib import Path

DOCUMENTS = [
    ("docs/CAUSA-UI-EXPERIENCE-REQUIREMENTS.md", "CAUSA-UI-EXPERIENCE", r"^## (UI-\d{3}):"),
    ("docs/WASM-LOADING-EXPERIENCE-REQUIREMENTS.md", "WASM-LOADING-EXPERIENCE", r"^## (WASM-LOAD-\d{3}):"),
    ("docs/PROVIDER-FIXTURE-AND-INTEGRATION-REQUIREMENTS.md", "PROVIDER-FIXTURE-AND-INTEGRATION", r"^\*\*(PF-\d{3}) "),
    ("docs/REHEARSAL-EXTERNAL-DATA-BOUNDARY.md", "REHEARSAL-EXTERNAL-DATA-BOUNDARY", r"^- (EXT-DATA-\d{3}) "),
    ("docs/FROZEN-DEMO-STORAGE-REQUIREMENTS.md", "FROZEN-DEMO-STORAGE", r"^## (STORAGE-\d{3}):"),
    ("docs/DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md", "DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES", r"^## (DCC-\d{3}):"),
    ("docs/AGENT-DECISION-REDUCTION-PROFILE.md", "AGENT-DECISION-REDUCTION-PROFILE", r"^## (ADRP-\d{3}):"),
    ("docs/REHEARSAL-DEPENDENCY-LOCK.md", "REHEARSAL-DEPENDENCY-LOCK", r"^## (DEP-CURRENT-\d{3}):"),
    ("docs/REQUIREMENTS.md", "REQUIREMENTS", r"^### (R\d+\.\d+) "),
    ("docs/SCREEN-CONTRACTS.md", "SCREEN-CONTRACTS", r"^## (S\d{2}) "),
    # Declared normative for Causa rehearsals by AGENT-DECISION-REDUCTION-PROFILE.md
    # ("Supporting normative documents"); the remaining ones it names are above.
    ("docs/DESIGN-DECISION-BOUNDARIES.md", "DESIGN-DECISION-BOUNDARIES", r"^### (D-\d{3}) "),
    ("docs/STATE-TRANSITION-TABLES.md", "STATE-TRANSITION-TABLES", None),
    ("docs/KICKOFF-EXECUTION-PACKET.md", "KICKOFF-EXECUTION-PACKET", None),
    ("docs/CAUSA-STORAGE-RELEASE-v1.0.0.md", "CAUSA-STORAGE-RELEASE", None),
    # Added 2026-10-24 (owner decision OD-INDY-5, docs/OWNER-DECISIONS-2026-10-24.md):
    # normative for future rehearsals and referenced from docs/REQUIREMENTS.md.
    ("docs/WORKFLOW-DIAGRAM-INTEGRATION-REQUIREMENTS.md", "WORKFLOW-DIAGRAM-INTEGRATION", r"^\*\*(WDL-\d{3}) "),
    ("docs/SYSTEM-VERSION-AND-COMPATIBILITY-REQUIREMENTS.md", "SYSTEM-VERSION-AND-COMPATIBILITY", r"^\*\*(VER-\d{3}) "),
    ("docs/AGENT-RELIABILITY-REQUIREMENTS.md", "AGENT-RELIABILITY", r"^\*\*(AR-\d{3}) "),
    # Added after Rehearsal 4 (defect D-HIST-WASM, docs/decisions/): normative for the
    # baseline that follows BL-CAUSA-INDY-INIT-aab8f80c7743; it does not apply to Rehearsal 4.
    ("docs/WASM-DIAGNOSTICS-REQUIREMENTS.md", "WASM-DIAGNOSTICS", r"^## (WASM-DIAG-\d{3}):"),
    # Normative rehearsal contracts without requirement ids.
    ("docs/REHEARSAL-AGENT-INTERFACE-FREEZE.md", "REHEARSAL-AGENT-INTERFACE-FREEZE", None),
    ("docs/REHEARSAL-ACCEPTANCE-LEDGER.md", "REHEARSAL-ACCEPTANCE-LEDGER", None),
]

# Un-identified sections that pass the normative-language test but state no
# obligation for an implementation; excluded by name so the choice is reviewable.
EXCLUDED_SECTIONS = {
    ("docs/REHEARSAL-DEPENDENCY-LOCK.md", "## Current observed historical baseline (NOT a recommended target)"),
    ("docs/DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md", "## Objective"),
    ("docs/AGENT-DECISION-REDUCTION-PROFILE.md", "## Purpose"),
    ("docs/AGENT-DECISION-REDUCTION-PROFILE.md", "## Still deliberately open"),
    ("docs/AGENT-DECISION-REDUCTION-PROFILE.md", "## Supporting normative documents"),
    ("docs/SCREEN-CONTRACTS.md", "## Percepta integration"),
    ("docs/CAUSA-STORAGE-RELEASE-v1.0.0.md", "## How the tag was created"),
}

ACCEPTANCE = re.compile(r"\b(acceptance|accepted|tests?|tested|testing|screenshots?|verify|verified)\b", re.IGNORECASE)
TOKEN_PATTERNS = [
    r"#[0-9A-Fa-f]{6}\b",
    r"\b[0-9a-f]{40}\b",
    r"\b\d+(?:\.\d+)?:1\b",
    r"\b\d+(?:\.\d+)?%",
    r"\b\d+(?:\.\d+)?\s?(?:px|ms|seconds?|sec|minutes?|min|hours?|KB|MB|GB|fps|rem|em)\b",
    r"\bv?\d+\.\d+\.\d+\b",
    r"(?<![\w.-])[45]\d\d(?![\w.-])",
    r"\b\d{1,2}:\d{2}\b",
    r"\b(?:[Tt]wo|[Tt]hree|[Ff]our|[Ff]ive|[Ss]ix|[Ss]even|[Ee]ight|[Nn]ine|[Tt]en|[Ee]leven|[Tt]welve)\b",
]
ACCEPTANCE_HEADING = re.compile(r"acceptance|required evidence|completion evidence", re.IGNORECASE)
SENTENCE = re.compile(r"(?<=[.!?])\s+(?=[A-Z0-9*`\"(\[])")
PRIORITY = re.compile(r"\[(P\d)\]")


def normalize(text):
    return text.replace("\r\n", "\n").replace("\r", "\n")


def sha256(text):
    return hashlib.sha256(normalize(text).encode("utf-8")).hexdigest()


def heading_level(line):
    level = len(line) - len(line.lstrip("#"))
    return level if 0 < level < len(line) and line[level] == " " else None


def section(lines, start, regex):
    anchor_level = heading_level(lines[start])
    finish = len(lines)
    for index in range(start + 1, len(lines)):
        line = lines[index]
        level = heading_level(line)
        if regex.match(line) or (level is not None and (anchor_level is None or level <= anchor_level)):
            finish = index
            break
    body = lines[start:finish]
    while body and not body[-1].strip():
        body.pop()
    return body


def tokens_of(text):
    found = []
    for pattern in TOKEN_PATTERNS:
        for match in re.finditer(pattern, text):
            token = match.group(0)
            if token not in found:
                found.append(token)
    return found


def units(body_lines, anchor_is_heading, anchor_prefix):
    """Verbatim obligation units of a section body."""
    lines = body_lines[1:] if anchor_is_heading else [body_lines[0][len(anchor_prefix):]] + body_lines[1:]
    in_code = False
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("```"):
            in_code = not in_code
            continue
        if in_code or not stripped or heading_level(stripped) or stripped.startswith("Status:"):
            continue
        if stripped.startswith("|"):
            if re.fullmatch(r"[|:\- ]+", stripped):
                continue
            yield stripped
            continue
        content = re.sub(r"^(?:[-*]|\d+\.)\s+", "", stripped)
        for sentence in SENTENCE.split(content):
            if sentence.strip():
                yield sentence.strip()


def slug(text):
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")


def build(root, commit):
    documents, requirements, seen_ids = [], [], set()
    for path, key, pattern in DOCUMENTS:
        text = normalize((root / path).read_text(encoding="utf-8"))
        lines = [line.rstrip() for line in text.split("\n")]
        regex = re.compile(pattern if pattern else r"(?!x)x")
        document = {"path": path, "sha256": sha256(text)}
        if pattern:
            document["requirementPattern"] = pattern
        documents.append(document)
        header_rows = set()
        for index, line in enumerate(lines):
            match = regex.match(line)
            level = heading_level(line)
            if match:
                rid = match.group(1)
            elif level in (1, 2, 3) and (path, line) not in EXCLUDED_SECTIONS:
                rid = None
            else:
                continue
            body = section(lines, index, regex)
            section_text = "\n".join(body)
            if rid is None:
                own = [l for l in body[1:] if l.strip()]
                if not own:
                    continue
                rid = f"{key}§preamble" if level == 1 else f"{key}§{slug(line.lstrip('#').strip())}"
            if rid in seen_ids:
                raise SystemExit(f"duplicate requirement id {rid} in {path}")
            seen_ids.add(rid)
            if level:
                title = re.sub(r"^#+\s+", "", line)
                prefix = ""
            else:
                bold = re.match(r"^(\*\*[^*]+\*\*\s*)", line)
                bullet = re.match(r"^(- \S+ (?:\[P\d\] )?)", line)
                prefix = bold.group(1) if bold else (bullet.group(1) if bullet else "")
                title = prefix.strip().strip("*").strip("- ").strip()
            title = re.sub(r"^(\S+?)[:\s]+", "", title, count=1) if match else title
            title = re.sub(r"\s*\[P\d\]\s*", " ", title).strip().rstrip(".")
            priority = PRIORITY.search(line)
            acceptance_section = bool(ACCEPTANCE_HEADING.search(line))
            obligations = []
            for unit in units(body, level is not None, prefix):
                if unit in header_rows:
                    continue
                if unit.startswith("|") and re.match(r"^\|\s*(ID|Token role|Gate|Class|Requirement)\s*\|", unit):
                    continue
                tokens = tokens_of(unit)
                if acceptance_section or ACCEPTANCE.search(unit):
                    kind = "acceptance"
                elif tokens:
                    kind = "value"
                else:
                    kind = "constraint"
                if unit not in section_text:
                    raise SystemExit(f"internal: unit not verbatim in {rid}: {unit!r}")
                obligation = {"id": f"{rid}-O{len(obligations) + 1}", "kind": kind, "text": unit}
                if tokens:
                    obligation["tokens"] = tokens
                obligations.append(obligation)
            if not obligations:
                if match:
                    raise SystemExit(f"requirement {rid} in {path} has no obligation text")
                continue  # a section whose only body is document metadata
            requirements.append(
                {
                    "id": rid,
                    "title": title or rid,
                    "priority": priority.group(1) if priority else "unstated",
                    "document": path,
                    "heading": line,
                    "sectionSha256": sha256(section_text),
                    "obligations": obligations,
                }
            )
    return {
        "schema": "praxis.requirements-baseline/1",
        "id": f"BL-CAUSA-INDY-INIT-{commit[:12]}",
        "source": {"repository": "kemiller2002/indy-init", "commit": commit, "documents": documents},
        "requirements": requirements,
        "conflicts": [],
    }


def main(argv):
    if len(argv) != 3 or not re.fullmatch(r"[0-9a-f]{40}", argv[2]):
        print("usage: build-requirements-baseline.py ROOT COMMIT(40 hex)", file=sys.stderr)
        return 2
    json.dump(build(Path(argv[1]), argv[2]), sys.stdout, indent=2, ensure_ascii=False)
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
