#!/usr/bin/env python3
"""Offline tests for tools/build-requirements-baseline.py. stdlib only."""
import importlib.util
import sys

sys.dont_write_bytecode = True
from pathlib import Path

spec = importlib.util.spec_from_file_location("baseline", Path(__file__).with_name("build-requirements-baseline.py"))
baseline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(baseline)

failures = []


def check(condition, message):
    if not condition:
        failures.append(message)


import re

lines = [
    "## Architectural contract",
    "",
    "**PF-001 [P0] One.** Define a port. It must not leak.",
    "",
    "**PF-002 [P0] Two.** Levels:",
    "1. Domain.",
    "",
    "Each level must have a command.",
    "",
    "## Fixture catalog",
]
regex = re.compile(r"^\*\*(PF-\d{3}) ")
check(baseline.section(lines, 2, regex) == [lines[2]], "a paragraph anchor ends at the next requirement line")
check(baseline.section(lines, 4, regex) == lines[4:8], "a paragraph anchor runs to the next heading, blank tail removed")
units = list(baseline.units(baseline.section(lines, 2, regex), False, "**PF-001 [P0] One.** "))
check(units == ["Define a port.", "It must not leak."], f"sentences are verbatim units: {units}")

table = ["## UI-016: Colors [P0]", "| Token role | Hex |", "| --- | --- |", "| Navy | `#14283F` |"]
units = list(baseline.units(table, True, ""))
check("| Navy | `#14283F` |" in units and "| --- | --- |" not in units, f"table data rows are units, separators are not: {units}")
check(baseline.tokens_of("contrast 4.5:1 at 200% within 60 minutes, HTTP 422, #14283F, v1.0.0, five frozen") ==
      ["#14283F", "4.5:1", "200%", "60 minutes", "v1.0.0", "422", "five"], "concrete values become tokens")
check(baseline.sha256("a\r\nb") == baseline.sha256("a\nb"), "hashes normalize line endings")

if failures:
    for failure in failures:
        print("FAIL", failure)
    sys.exit(1)
print("requirements baseline tooling tests passed")
