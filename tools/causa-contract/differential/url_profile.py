#!/usr/bin/env python3
"""Reference implementation of the Causa v1.0.0 artifact-URL ASCII profile (SEMANTICS.md §10.4.2).

`in_profile(u)` decides whether `u` is inside the profile; `well_formed(u)` decides EVIDENCE.URL for in-profile strings.
Usage (conformance check against vectors produced by the .NET 10 engine predicate):
  python3 -I url_profile.py <vectors.jsonl>      # lines {"url": ..., "accepted": bool}
"""
from __future__ import annotations

import json
import re
import sys

HEX = "0123456789abcdefABCDEF"
UNRESERVED = set("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._~")
SUB_DELIMS = set("!$&'()*+,;=")
PCHAR = UNRESERVED | SUB_DELIMS | {":", "@"}


LABEL = r"[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?"
OCTET = r"(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])"
PROFILE = re.compile(
    r"(?i:https?)://"
    r"(?P<host>" + LABEL + r"(?:\." + LABEL + r")*" + r"|\[[0-9A-Fa-f:.]+\])"
    r"(?::(?P<port>[0-9]{1,5}))?"
    r"(?P<tail>[/?#][\x21-\x7e]*)?\Z"
)


def in_profile(u: str) -> bool:
    """Membership of the strict profile (SEMANTICS.md §10.4.2); purely syntactic."""
    m = PROFILE.match(u)
    return bool(m) and (not m.group("host").startswith("[") or ipv6_ok(m.group("host")[1:-1]))


def ipv6_ok(text: str) -> bool:
    import ipaddress

    try:
        ipaddress.IPv6Address(text)
        return True
    except ValueError:
        return False


def pct_ok(s: str, allowed: set[str]) -> bool:
    i = 0
    while i < len(s):
        if s[i] == "%":
            if len(s) < i + 3 or s[i + 1] not in HEX or s[i + 2] not in HEX:
                return False
            i += 3
        elif s[i] in allowed:
            i += 1
        else:
            return False
    return True


def well_formed(u: str) -> bool:
    """Exact engine verdict for an in-profile string."""
    m = PROFILE.match(u)
    port, tail = m.group("port"), m.group("tail") or ""
    if port is not None and int(port) > 65535:
        return False
    before_fragment, hash_, fragment = tail.partition("#")
    path, _, query = before_fragment.partition("?")
    return (
        "#" not in fragment
        and pct_ok(path, PCHAR | {"/"})
        and pct_ok(query, PCHAR | {"/", "?"})
        and pct_ok(fragment, PCHAR | {"/", "?"})
    )


def main(argv: list[str]) -> int:
    rows = [json.loads(line) for line in open(argv[1], encoding="utf-8")]
    scoped = [r for r in rows if in_profile(r["url"])]
    bad = [r for r in scoped if well_formed(r["url"]) != r["accepted"]]
    for r in bad[:40]:
        print(f"DISAGREE dotnet={r['accepted']} url={r['url']!r}")
    print(("PASS" if not bad else "FAIL") + f" url-profile {len(scoped) - len(bad)}/{len(scoped)} in-profile vectors agree ({len(rows) - len(scoped)} out of profile)")
    return 0 if not bad else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
