"""Wire primitives: strict JSON parsing, RFC 8785 canonical serialization, hashing, event-id derivation.

Implements CONTRACT.md §2.1-§2.3 (encoding, canonical form, values), §2.11 (hashes) and §5.2 (event identity).
All functions are pure. Errors are returned as `Failure` values, never raised.
"""
from __future__ import annotations

import hashlib
import re
import uuid
from dataclasses import dataclass
from typing import Any

MAX_DEPTH = 128                       # CONTRACT §2.3
MAX_INT = 2 ** 53 - 1                 # CONTRACT §2.3


@dataclass(frozen=True)
class Failure:
    """An error as a value: CAUSA primary code, human detail and (stage 15 only) the HISTORY domain code."""
    code: str
    detail: str
    domain: str | None = None


def is_failure(value: Any) -> bool:
    return isinstance(value, Failure)


# --------------------------------------------------------------------------------------------------------------------
# Strict JSON parser (CONTRACT §2.3).  Recursive descent; returns (value, next_index) or Failure.
# --------------------------------------------------------------------------------------------------------------------

_WS = " \t\n\r"
_NUMBER = re.compile(r"-?(?:0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?")
_PLAIN = re.compile(r'[^"\\\x00-\x1f﻿]*')
_ESCAPES = {'"': '"', "\\": "\\", "/": "/", "b": "\b", "f": "\f", "n": "\n", "r": "\r", "t": "\t"}
_HEX4 = re.compile(r"[0-9a-fA-F]{4}")


def _malformed(detail: str) -> Failure:
    return Failure("CAUSA.JSON_MALFORMED", detail)


def _skip_ws(text: str, i: int) -> int:
    n = len(text)
    while i < n and text[i] in _WS:
        i += 1
    return i


def _parse_string(text: str, i: int):
    # text[i] == '"'
    parts: list[str] = []
    i += 1
    n = len(text)
    while True:
        m = _PLAIN.match(text, i)
        parts.append(m.group(0))
        i = m.end()
        if i >= n:
            return _malformed("unterminated string")
        c = text[i]
        if c == '"':
            return "".join(parts), i + 1
        if c == "﻿":
            return _malformed("U+FEFF inside a string")
        if c != "\\":
            return _malformed(f"unescaped control character U+{ord(c):04X} in string")
        if i + 1 >= n:
            return _malformed("unterminated escape")
        e = text[i + 1]
        if e in _ESCAPES:
            parts.append(_ESCAPES[e])
            i += 2
            continue
        if e != "u" or not _HEX4.fullmatch(text, i + 2, i + 6):
            return _malformed("invalid escape sequence")
        cp = int(text[i + 2:i + 6], 16)
        i += 6
        if 0xD800 <= cp <= 0xDBFF:
            if text[i:i + 2] == "\\u" and _HEX4.fullmatch(text, i + 2, i + 6):
                lo = int(text[i + 2:i + 6], 16)
                if 0xDC00 <= lo <= 0xDFFF:
                    parts.append(chr(0x10000 + ((cp - 0xD800) << 10) + (lo - 0xDC00)))
                    i += 6
                    continue
            return _malformed("unpaired UTF-16 surrogate escape")
        if 0xDC00 <= cp <= 0xDFFF:
            return _malformed("unpaired UTF-16 surrogate escape")
        if cp == 0xFEFF:
            return _malformed("escaped U+FEFF inside a string")
        parts.append(chr(cp))


def _parse_number(text: str, i: int):
    m = _NUMBER.match(text, i)
    if not m or m.end() == i:
        return _malformed(f"unexpected character at offset {i}")
    if m.group(1) is not None or m.group(2) is not None:
        return _malformed("non-integer number (fraction or exponent)")
    value = int(m.group(0))
    if abs(value) > MAX_INT:
        return _malformed("integer outside [-(2^53-1), 2^53-1]")
    return value, m.end()


def _parse_value(text: str, i: int, depth: int):
    i = _skip_ws(text, i)
    if i >= len(text):
        return _malformed("unexpected end of input")
    c = text[i]
    if c == "{":
        return _parse_object(text, i, depth + 1)
    if c == "[":
        return _parse_array(text, i, depth + 1)
    if c == '"':
        return _parse_string(text, i)
    for literal, value in (("true", True), ("false", False), ("null", None)):
        if text.startswith(literal, i):
            return value, i + len(literal)
    return _parse_number(text, i)


def _parse_array(text: str, i: int, depth: int):
    if depth > MAX_DEPTH:
        return _malformed(f"nesting deeper than {MAX_DEPTH}")
    items: list[Any] = []
    i = _skip_ws(text, i + 1)
    if text[i:i + 1] == "]":
        return items, i + 1
    while True:
        r = _parse_value(text, i, depth)
        if is_failure(r):
            return r
        items.append(r[0])
        i = _skip_ws(text, r[1])
        c = text[i:i + 1]
        if c == "]":
            return items, i + 1
        if c != ",":
            return _malformed(f"expected ',' or ']' at offset {i}")
        i += 1


def _parse_object(text: str, i: int, depth: int):
    if depth > MAX_DEPTH:
        return _malformed(f"nesting deeper than {MAX_DEPTH}")
    obj: dict[str, Any] = {}
    i = _skip_ws(text, i + 1)
    if text[i:i + 1] == "}":
        return obj, i + 1
    while True:
        i = _skip_ws(text, i)
        if text[i:i + 1] != '"':
            return _malformed(f"expected object key at offset {i}")
        k = _parse_string(text, i)
        if is_failure(k):
            return k
        key, i = k
        if key in obj:
            return _malformed(f"duplicate object key {key!r}")
        i = _skip_ws(text, i)
        if text[i:i + 1] != ":":
            return _malformed(f"expected ':' at offset {i}")
        r = _parse_value(text, i + 1, depth)
        if is_failure(r):
            return r
        obj[key] = r[0]
        i = _skip_ws(text, r[1])
        c = text[i:i + 1]
        if c == "}":
            return obj, i + 1
        if c != ",":
            return _malformed(f"expected ',' or '}}' at offset {i}")
        i += 1


def parse_json(text: str):
    """Parse exactly one JSON text under CONTRACT §2.3 rules. Returns the value or a Failure (JSON_MALFORMED)."""
    r = _parse_value(text, 0, 0)
    if is_failure(r):
        return r
    end = _skip_ws(text, r[1])
    if end != len(text):
        return _malformed("not a single JSON text (trailing data)")
    return r[0]


# --------------------------------------------------------------------------------------------------------------------
# RFC 8785 canonical serialization (CONTRACT §2.2).  The v1 value domain has no floats, so numbers are integers.
# --------------------------------------------------------------------------------------------------------------------

_JCS_ESC = {'"': '\\"', "\\": "\\\\", "\b": "\\b", "\f": "\\f", "\n": "\\n", "\r": "\\r", "\t": "\\t"}


def _jcs_string(s: str) -> str:
    return '"' + "".join(_JCS_ESC.get(c) or (f"\\u{ord(c):04x}" if ord(c) < 0x20 else c) for c in s) + '"'


def _utf16_key(s: str) -> bytes:
    return s.encode("utf-16-be", "surrogatepass")


def canonical(value: Any) -> str:
    if value is None:
        return "null"
    if value is True:
        return "true"
    if value is False:
        return "false"
    if isinstance(value, int):
        return str(value)
    if isinstance(value, str):
        return _jcs_string(value)
    if isinstance(value, (list, tuple)):
        return "[" + ",".join(canonical(v) for v in value) + "]"
    if isinstance(value, dict):
        return "{" + ",".join(_jcs_string(k) + ":" + canonical(value[k])
                              for k in sorted(value, key=_utf16_key)) + "}"
    raise TypeError(f"value outside the v1 JSON domain: {type(value).__name__}")  # programming error, not data


def canonical_bytes(value: Any) -> bytes:
    return canonical(value).encode("utf-8")


def file_bytes(value: Any) -> bytes:
    """A JSON file: one canonical text followed by exactly one LF (CONTRACT §2.2)."""
    return canonical_bytes(value) + b"\n"


def sha256_hex(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def digest_json(value: Any) -> str:
    """JSON documents are hashed over their canonical text without the trailing LF (CONTRACT §2.11)."""
    return sha256_hex(canonical_bytes(value))


def digest_file_as_read(data: bytes) -> str:
    """Digest of a JSON file as read: its bytes without the single trailing LF."""
    return sha256_hex(data[:-1] if data.endswith(b"\n") else data)


# --------------------------------------------------------------------------------------------------------------------
# Event identity (CONTRACT §5.2)
# --------------------------------------------------------------------------------------------------------------------

EVENT_ID_NAMESPACE = uuid.uuid5(uuid.NAMESPACE_URL, "https://github.com/kemiller2002/causa-data/contract/v1/event-id")


def event_id(investigation_id: str, aggregate_version: int, correlation_id: str) -> str:
    return str(uuid.uuid5(EVENT_ID_NAMESPACE, f"{investigation_id}/{aggregate_version}/{correlation_id}"))


# --------------------------------------------------------------------------------------------------------------------
# Shared lexical predicates
# --------------------------------------------------------------------------------------------------------------------

UUID_RE = re.compile(r"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\Z")
NIL_UUID = "00000000-0000-0000-0000-000000000000"
ENGINE_WHITESPACE = frozenset(
    [chr(c) for c in range(0x09, 0x0E)] + [" ", "\u0085", " ", " "]
    + [chr(c) for c in range(0x2000, 0x200B)] + [" ", " ", " ", " ", "　"])


def is_canonical_uuid(s: Any) -> bool:
    return isinstance(s, str) and bool(UUID_RE.match(s)) and s != NIL_UUID


def is_blank(s: Any) -> bool:
    """SEMANTICS §3.2 / CONTRACT §2.6: null, empty, or only engine-whitespace code units."""
    return s is None or all(c in ENGINE_WHITESPACE for c in s)
