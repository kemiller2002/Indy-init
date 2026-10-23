"""Pure model of the imported Causa storage contract: schema loading, reference resolution and field expansion.

Everything here is a pure function over immutable values (tuples, frozen NamedTuples, MappingProxyType).  The only I/O is
`load_contract`, which reads a contract directory into an immutable `Contract` value once.
"""
from __future__ import annotations

import hashlib
import json
import posixpath
import re
from functools import reduce
from pathlib import Path
from types import MappingProxyType
from typing import Any, Callable, Iterable, Mapping, NamedTuple

COMMON = "common.schema.json"
ACTOR = "actor.schema.json"
ENVELOPE = "event-envelope.schema.json"


class Contract(NamedTuple):
    root: Path
    schemas: Mapping[str, Mapping[str, Any]]  # relative path under schemas/ -> parsed schema
    schema_bytes: Mapping[str, bytes]
    bundle_bytes: bytes
    field_mapping: Mapping[str, Mapping[str, Mapping[str, str]]]  # eventType -> wire field -> row
    semantics: Mapping[str, Mapping[str, str]]


class Field(NamedTuple):
    document: str  # persisted document kind, e.g. "events.ndjson line (eventType = EvidenceAdmitted)"
    path: str  # JSON path, arrays as [], map keys as {name}
    name: str
    json_type: str
    required: bool
    nullable: bool
    allowed: tuple[str, ...]
    fmt: str
    bounds: str
    schema: str  # schema file that declares the property
    sem_key: str  # key into field-semantics.json / FIELD-MAPPING
    variant: str  # oneOf variant label, "" if none
    conditions: tuple[str, ...]  # conditional (if/then) rules that constrain this field


def freeze(value: Any) -> Any:
    if isinstance(value, dict):
        return MappingProxyType({k: freeze(v) for k, v in value.items()})
    if isinstance(value, list):
        return tuple(freeze(v) for v in value)
    return value


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


# ------------------------------------------------------------------------------------------------- loading (I/O edge)


def parse_field_mapping(text: str) -> Mapping[str, Mapping[str, Mapping[str, str]]]:
    """FIELD-MAPPING.md payload tables -> {eventType: {wireField: {fsharp, meaning, ...}}}."""
    sections = re.split(r"^### `([A-Za-z]+)`\s*$", text, flags=re.M)
    pairs = zip(sections[1::2], sections[2::2])

    def rows(body: str) -> Mapping[str, Mapping[str, str]]:
        cells = (
            [c.strip() for c in line.strip().strip("|").split(" | ")]
            for line in body.splitlines()
            if line.startswith("| `")
        )
        return MappingProxyType(
            {
                c[0].strip("`"): MappingProxyType({"element": c[1].strip("`"), "fsharp": c[2], "meaning": c[6]})
                for c in cells
                if len(c) >= 7
            }
        )

    return MappingProxyType({name: rows(body) for name, body in pairs})


def load_contract(root: Path) -> Contract:
    schema_dir = root / "schemas"
    paths = sorted(p.relative_to(schema_dir).as_posix() for p in schema_dir.rglob("*.schema.json"))
    raw = {p: (schema_dir / p).read_bytes() for p in paths}
    semantics_file = root / "catalog" / "field-semantics.json"
    semantics = json.loads(semantics_file.read_text("utf-8"))["fields"] if semantics_file.exists() else {}
    return Contract(
        root=root,
        schemas=MappingProxyType({p: freeze(json.loads(b)) for p, b in raw.items()}),
        schema_bytes=MappingProxyType(raw),
        bundle_bytes=(schema_dir / "bundle.json").read_bytes(),
        field_mapping=parse_field_mapping((root / "FIELD-MAPPING.md").read_text("utf-8")),
        semantics=freeze(semantics),
    )


# ------------------------------------------------------------------------------------------------- reference resolution


def resolve(contract: Contract, current: str, ref: str) -> tuple[str, str, Mapping[str, Any]]:
    """Resolve a relative $ref from schema file `current` -> (file, fragment, node)."""
    file_part, _, fragment = ref.partition("#")
    target = current if not file_part else posixpath.normpath(posixpath.join(posixpath.dirname(current), file_part))
    if target not in contract.schemas:
        raise KeyError(f"unresolvable $ref {ref!r} from {current}")
    node = reduce(lambda n, part: n[part], [p for p in fragment.split("/") if p], contract.schemas[target])
    return target, fragment, node


def all_refs(contract: Contract) -> tuple[tuple[str, str], ...]:
    def walk(node: Any) -> Iterable[str]:
        if isinstance(node, Mapping):
            yield from ([node["$ref"]] if "$ref" in node else [])
            for v in node.values():
                yield from walk(v)
        elif isinstance(node, tuple):
            for v in node:
                yield from walk(v)

    return tuple((f, r) for f, s in contract.schemas.items() for r in walk(s))


# ------------------------------------------------------------------------------------------------- scalar descriptions


class Shape(NamedTuple):
    json_type: str
    nullable: bool
    allowed: tuple[str, ...]
    fmt: str
    bounds: str
    child: tuple[str, Mapping[str, Any]] | None  # (file, object node) to expand further
    item_child: bool  # child is the item of an array
    scope: str = ""  # semantics scope of the child when reached through $ref ("" = inline, scoped by parent)


SCALAR_FORMATS = {
    "uuid": "lowercase canonical UUID `^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$`, not nil",
    "timestamp": "RFC 3339 UTC `YYYY-MM-DDTHH:MM:SS.mmmZ`, exactly 3 fractional digits, calendar-valid, years 0001-9999",
    "text": "non-blank verbatim text: minLength 1 and at least one char outside the engine whitespace set; never trimmed/normalized",
    "httpUrl": "absolute http(s) URL prefix `^[Hh][Tt][Tt][Pp][Ss]?://` + non-whitespace",
    "sha256": "lowercase hex `^[0-9a-f]{64}$`",
    "slug": "`^[a-z0-9][a-z0-9-]{0,63}$`",
    "errorCode": "`^CAUSA\\.[A-Z_]+$`",
}


def describe_def(contract: Contract, name: str) -> Shape:
    node = contract.schemas[COMMON]["$defs"][name]
    enum = tuple(node.get("enum", ()))
    simple = {
        "uuid": Shape("string", False, (), SCALAR_FORMATS["uuid"], "", None, False),
        "timestamp": Shape("string", False, (), SCALAR_FORMATS["timestamp"], "", None, False),
        "nullableTimestamp": Shape("string", True, (), SCALAR_FORMATS["timestamp"], "", None, False),
        "text": Shape("string", False, (), SCALAR_FORMATS["text"], "minLength 1", None, False),
        "nullableText": Shape("string", True, (), SCALAR_FORMATS["text"], "minLength 1", None, False),
        "httpUrl": Shape("string", False, (), SCALAR_FORMATS["httpUrl"], "", None, False),
        "sha256": Shape("string", False, (), SCALAR_FORMATS["sha256"], "", None, False),
        "slug": Shape("string", False, (), SCALAR_FORMATS["slug"], "", None, False),
        "errorCode": Shape("string", False, (), SCALAR_FORMATS["errorCode"], "", None, False),
        "uuidList": Shape("array<uuid>", False, (), "items: " + SCALAR_FORMATS["uuid"] + "; ordered (citation order), distinct (uniqueItems), not sorted", "", None, False),
        "nonEmptyUuidList": Shape("array<uuid>", False, (), "items: " + SCALAR_FORMATS["uuid"] + "; ordered, distinct", "minItems 1", None, False),
        "aggregateVersion": Shape("integer", False, (), "JSON integer", "1 .. 9007199254740991", None, False),
        "versionOrZero": Shape("integer", False, (), "JSON integer", "0 .. 9007199254740991", None, False),
        "formatVersion": Shape("string", False, ("1.0.0",), "const", "", None, False),
        "schemaVersion": Shape("string", False, ("1.0.0",), "const", "", None, False),
        "semanticVersion": Shape("string", False, ("1.0.0",), "const", "", None, False),
    }
    return simple.get(name) or Shape(f"string enum `{name}`", False, enum, "closed lowerCamelCase set", "", None, False)


def bounds_of(node: Mapping[str, Any]) -> str:
    keys = (("minimum", "min"), ("maximum", "max"), ("minItems", "minItems"), ("maxItems", "maxItems"), ("minLength", "minLength"))
    return ", ".join(f"{label} {node[k]}" for k, label in keys if k in node)


def describe(contract: Contract, file: str, node: Mapping[str, Any]) -> Shape:
    if "$ref" in node:
        target, fragment, target_node = resolve(contract, file, node["$ref"])
        if target == COMMON:
            return describe_def(contract, fragment.split("/")[-1])
        label = f"object ({target}{'#' + fragment if fragment else ''})"
        return Shape(label, False, (), "closed object (additionalProperties false)", "", (target, target_node), False, target)
    if "oneOf" in node:
        branches = node["oneOf"]
        non_null = tuple(b for b in branches if b.get("type") != "null")
        if len(non_null) == 1 and len(branches) == 2:
            inner = describe(contract, file, non_null[0])
            return inner._replace(nullable=True)
        return Shape("object (oneOf variants)", False, (), "exactly one variant", "", (file, node), False)
    if "const" in node:
        value = node["const"]
        return Shape("integer" if isinstance(value, int) else "string", False, (json.dumps(value),), "const", "", None, False)
    if "enum" in node:
        return Shape("string enum (inline)", False, tuple(node["enum"]), "closed set", "", None, False)
    kind = node.get("type")
    if kind == "array":
        items = node.get("items", {})
        inner = describe(contract, file, items) if items else Shape("any", False, (), "", "", None, False)
        unique = "distinct (uniqueItems)" if node.get("uniqueItems") else ""
        return Shape(
            f"array<{inner.json_type}>",
            False,
            inner.allowed,
            "; ".join(x for x in (inner.fmt, unique) if x),
            ", ".join(x for x in (bounds_of(node), inner.bounds) if x),
            inner.child,
            inner.child is not None,
            inner.scope,
        )
    if kind == "object":
        has_children = "properties" in node or isinstance(node.get("additionalProperties"), Mapping)
        label = "object (map: file name -> object)" if isinstance(node.get("additionalProperties"), Mapping) else "object"
        return Shape(label, False, (), "closed" if node.get("additionalProperties") is False else "", bounds_of(node), (file, node) if has_children else None, False)
    if kind == "integer":
        return Shape("integer", False, (), "JSON integer", bounds_of(node), None, False)
    if kind == "string":
        fmt = f"pattern `{node['pattern']}`" if "pattern" in node else ""
        return Shape("string", False, (), fmt, bounds_of(node), None, False)
    if kind == "null":
        return Shape("null", True, (), "", "", None, False)
    return Shape(str(kind), False, (), "", "", None, False)


# ------------------------------------------------------------------------------------------------- conditional rules


def render_value(node: Mapping[str, Any]) -> str:
    if "const" in node:
        return f"= {json.dumps(node['const'])}"
    if node.get("type") == "null":
        return "is null"
    if "$ref" in node:
        return f"must be {node['$ref'].split('/')[-1]}"
    return json.dumps(dict(node))


def conditions_of(schema: Mapping[str, Any]) -> tuple[tuple[str, str], ...]:
    """allOf if/then rules -> ((constrained field, rule text), ...); payload-dispatch rules excluded."""

    def one(rule: Mapping[str, Any]) -> tuple[tuple[str, str], ...]:
        cond = rule.get("if", {}).get("properties", {})
        then = rule.get("then", {}).get("properties", {})
        if "payload" in then:
            return ()
        premise = " and ".join(
            f"`{k}` " + (render_value(v) if "properties" not in v else f"has {', '.join(f'`{kk}` {render_value(vv)}' for kk, vv in v['properties'].items())}")
            for k, v in cond.items()
        )
        return tuple((field, f"if {premise} then `{field}` {render_value(v)}") for field, v in then.items())

    return tuple(pair for rule in schema.get("allOf", ()) for pair in one(rule))


# ------------------------------------------------------------------------------------------------- expansion


def expand(contract: Contract, document: str, file: str, node: Mapping[str, Any], path: str, sem_scope: str, variant: str = "") -> tuple[Field, ...]:
    if "oneOf" in node and "properties" not in node:
        variants = tuple(b for b in node["oneOf"] if "properties" in b)
        return tuple(
            f
            for i, b in enumerate(variants)
            for f in expand(contract, document, file, b, path, sem_scope, variant_label(b, i))
        )
    if isinstance(node.get("additionalProperties"), Mapping) and "properties" not in node:
        child = node["additionalProperties"]
        entry = Field(document, f"{path}.{{fileName}}", "{fileName}", "object (one directory entry)", False, False, (), "closed", "",
                      file, f"{sem_scope}#{{fileName}}", variant, ())
        return (entry,) + expand(contract, document, file, child, f"{path}.{{fileName}}", sem_scope, variant)
    required = frozenset(node.get("required", ()))
    conds = conditions_of(node)

    def field_rows(name: str, prop: Mapping[str, Any]) -> tuple[Field, ...]:
        shape = describe(contract, file, prop)
        own = Field(
            document=document,
            path=f"{path}.{name}",
            name=name,
            json_type=shape.json_type,
            required=name in required,
            nullable=shape.nullable,
            allowed=shape.allowed,
            fmt=shape.fmt,
            bounds=shape.bounds,
            schema=file,
            sem_key=f"{sem_scope}#{name}",
            variant=variant,
            conditions=tuple(text for f, text in conds if f == name),
        )
        if shape.child is None:
            return (own,)
        child_file, child_node = shape.child
        child_path = f"{path}.{name}" + ("[]" if shape.item_child else "")
        child_scope = shape.scope or f"{sem_scope}/{name}"
        return (own,) + expand(contract, document, child_file, child_node, child_path, child_scope, variant)

    return tuple(row for name, prop in node.get("properties", {}).items() for row in field_rows(name, prop))


def variant_label(branch: Mapping[str, Any], index: int) -> str:
    props = branch.get("properties", {})
    for key in ("kind",):
        if key in props:
            spec = props[key]
            if "const" in spec:
                return f"{key}={spec['const']}"
            if "enum" in spec:
                return f"{key}={'|'.join(spec['enum'])}"
    return f"variant {index + 1}"


PAYLOAD_DOC = "events.ndjson line, eventType = {}"


def event_types(contract: Contract) -> tuple[str, ...]:
    return tuple(contract.schemas[ENVELOPE]["properties"]["eventType"]["enum"])


def documents(contract: Contract) -> tuple[tuple[str, str, str], ...]:
    """(document label, schema file, root JSON path prefix) for every persisted or published JSON document."""
    return (
        ("events.ndjson line (envelope)", ENVELOPE, "$"),
        *((PAYLOAD_DOC.format(t), f"payloads/{t}.schema.json", "$.payload") for t in event_types(contract)),
        ("manifest.json (investigation manifest)", "investigation-manifest.schema.json", "$"),
        ("snapshot.json", "snapshot.schema.json", "$"),
        ("golden/<scenarioKey>.state.json", "state.schema.json", "$"),
        ("fixtures manifest.json (root fixture manifest)", "root-manifest.schema.json", "$"),
        ("golden/scenarios.json", "golden-scenarios.schema.json", "$"),
        ("negative/<name>.json (negative fixture)", "negative-fixture.schema.json", "$"),
        ("schemas/bundle.json (schema bundle)", "schema-bundle.schema.json", "$"),
    )


def all_fields(contract: Contract) -> tuple[Field, ...]:
    return tuple(
        f
        for label, file, prefix in documents(contract)
        for f in expand(contract, label, file, contract.schemas[file], prefix, file)
    )


# ------------------------------------------------------------------------------------------------- semantics join


class Semantic(NamedTuple):
    fsharp: str
    meaning: str
    default: str
    rule: str


def semantic_for(contract: Contract, f: Field) -> Semantic | None:
    if f.schema.startswith("payloads/"):
        event = Path(f.schema).name.split(".")[0]
        row = contract.field_mapping.get(event, {}).get(f.name)
        if row is not None and f.sem_key.startswith(f.schema + "#"):
            return Semantic(row["fsharp"], row["meaning"], "", "")
    entry = contract.semantics.get(f.sem_key)
    if entry is None:
        return None
    return Semantic(entry["fsharp"], entry["meaning"], entry.get("default", ""), entry.get("rule", ""))


def missing_semantics(contract: Contract, fields: Iterable[Field]) -> tuple[str, ...]:
    return tuple(sorted({f.sem_key for f in fields if semantic_for(contract, f) is None}))


def unique_by(key: Callable[[Any], Any], items: Iterable[Any]) -> tuple[Any, ...]:
    return tuple({key(i): i for i in items}.values())
