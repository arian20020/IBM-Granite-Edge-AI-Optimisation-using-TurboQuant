"""Closed JSON-line protocol for the converter process."""

from __future__ import annotations

import json
import os
import re
import sys
import uuid
from dataclasses import dataclass
from typing import NoReturn

from . import PROTOCOL, PROTOCOL_VERSION

MAX_REQUEST_BYTES = 16 * 1024
MAX_EVENT_BYTES = 16 * 1024
_DIGEST = re.compile(r"^[0-9a-f]{64}$")
_REQUEST_FIELDS = frozenset(
    {
        "protocol",
        "version",
        "operationId",
        "sourcePath",
        "destinationPath",
        "sourceManifestSha256",
        "operation",
        "weightPrecision",
    }
)


class ProtocolError(Exception):
    """A deliberately detail-free protocol rejection."""


@dataclass(frozen=True)
class ConvertRequest:
    operation_id: str
    source_path: str
    destination_path: str
    source_manifest_sha256: str
    operation: str
    weight_precision: str


def reject_unknown_fields(value: dict[str, object], allowed: frozenset[str]) -> None:
    if frozenset(value) != allowed:
        raise ProtocolError()


def _fail() -> NoReturn:
    raise ProtocolError()


def read_request() -> ConvertRequest:
    raw = sys.stdin.buffer.readline(MAX_REQUEST_BYTES + 1)
    if not raw or len(raw) > MAX_REQUEST_BYTES or not raw.endswith(b"\n"):
        _fail()
    try:
        text = raw.decode("utf-8", errors="strict")
        value = json.loads(
            text,
            object_pairs_hook=_unique_object,
            parse_constant=lambda _: _fail(),
        )
    except (UnicodeDecodeError, json.JSONDecodeError, TypeError, ValueError):
        _fail()
    if not isinstance(value, dict):
        _fail()
    reject_unknown_fields(value, _REQUEST_FIELDS)
    if value["protocol"] != PROTOCOL or value["version"] != PROTOCOL_VERSION:
        _fail()
    operation_id = _canonical_uuid(value["operationId"])
    source = _absolute_path(value["sourcePath"])
    destination = _absolute_path(value["destinationPath"])
    digest = value["sourceManifestSha256"]
    operation = value["operation"]
    weight_precision = value["weightPrecision"]
    if os.path.normcase(source) == os.path.normcase(destination):
        _fail()
    if not isinstance(digest, str) or _DIGEST.fullmatch(digest) is None:
        _fail()
    if operation not in {"convert", "optimize"} or weight_precision not in {
        "fp16", "int8", "int4"
    }:
        _fail()
    if operation == "convert" and weight_precision != "fp16":
        _fail()
    return ConvertRequest(
        operation_id, source, destination, digest, operation, weight_precision
    )


def write_event(event: dict[str, object]) -> None:
    encoded = json.dumps(
        event, ensure_ascii=False, allow_nan=False, separators=(",", ":"), sort_keys=True
    ).encode("utf-8")
    if len(encoded) > MAX_EVENT_BYTES:
        raise ProtocolError()
    sys.stdout.buffer.write(encoded + b"\n")
    sys.stdout.buffer.flush()


def _unique_object(pairs: list[tuple[str, object]]) -> dict[str, object]:
    value: dict[str, object] = {}
    for name, item in pairs:
        if name in value:
            _fail()
        value[name] = item
    return value


def _canonical_uuid(value: object) -> str:
    if not isinstance(value, str):
        _fail()
    try:
        parsed = uuid.UUID(value)
    except (ValueError, AttributeError):
        _fail()
    if str(parsed) != value:
        _fail()
    return value


def _absolute_path(value: object) -> str:
    if not isinstance(value, str) or not value or "\x00" in value:
        _fail()
    path = os.path.abspath(value)
    if path != value:
        _fail()
    return path
