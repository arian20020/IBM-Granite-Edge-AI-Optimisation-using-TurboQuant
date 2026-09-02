"""Closed contracts and entry-gate validation for EXP-TV-COMP-001."""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
import json
from pathlib import Path
import re
from typing import Any, Mapping


EXPECTED_WHEEL_SHA256 = "CD855E0B318A57DC57C733F9A62AE98DE5192F4F6C2C760E305523E8CEB1B090"
SHA256 = re.compile(r"^[0-9a-f]{64}$")
RUN_ID = re.compile(r"^EXP-TV-COMP-001-\d{8}T\d{6}Z-\d{3}$")


class Decision(str, Enum):
    INTEGRATE = "INTEGRATE"
    DEMONSTRATOR_ONLY = "DEMONSTRATOR_ONLY"
    EXCLUDE = "EXCLUDE"
    BLOCKED = "BLOCKED"


@dataclass(frozen=True)
class DependencyIdentity:
    repository: str
    version: str
    commit: str
    tree: str
    licence: str
    wheel_filename: str = ""
    wheel_bytes: int = 0
    wheel_sha256: str = ""


@dataclass(frozen=True)
class EmbeddingIdentity:
    repository: str
    revision: str
    dimension: int
    licence: str
    status: str


@dataclass(frozen=True)
class Thresholds:
    recall_at_10: float = 0.90
    relative_ndcg_at_10: float = 0.95
    minimum_storage_ratio: float = 2.0
    maximum_p95_slowdown: float = 1.10


@dataclass(frozen=True)
class ControlManifest:
    experiment_id: str
    turbovec: DependencyIdentity
    pdfpig: DependencyIdentity
    embedding: EmbeddingIdentity
    openvino_version: str
    warmup_batches: int
    measured_batches: int
    top_k: int
    thresholds: Thresholds


def _closed(document: Mapping[str, Any], allowed: set[str], label: str) -> None:
    unknown = set(document) - allowed
    if unknown:
        raise ValueError(f"{label} contains unknown properties: {sorted(unknown)}")


def load_control_manifest(path: Path) -> ControlManifest:
    return load_control_manifest_dict(json.loads(Path(path).read_text(encoding="utf-8-sig")))


def load_control_manifest_dict(document: Mapping[str, Any]) -> ControlManifest:
    _closed(document, {"schema_version", "experiment_id", "dependencies", "execution", "thresholds"}, "manifest")
    if document.get("experiment_id") != "EXP-TV-COMP-001":
        raise ValueError("wrong experiment ID")
    dependencies = document["dependencies"]
    _closed(dependencies, {"turbovec", "pdfpig", "embedding", "openvino"}, "dependencies")
    tv = dependencies["turbovec"]
    if str(tv.get("wheel_sha256", "")).upper() != EXPECTED_WHEEL_SHA256:
        raise ValueError("turbovec wheel SHA-256 mismatch")
    turbovec = DependencyIdentity(**tv)
    pdfpig = DependencyIdentity(**dependencies["pdfpig"])
    embedding = EmbeddingIdentity(**dependencies["embedding"])
    execution = document["execution"]
    thresholds = Thresholds(**document["thresholds"])
    if execution != {"warmup_batches": 5, "measured_batches": 30, "top_k": 10}:
        raise ValueError("execution controls differ from the frozen protocol")
    return ControlManifest(
        experiment_id=document["experiment_id"], turbovec=turbovec, pdfpig=pdfpig,
        embedding=embedding, openvino_version=dependencies["openvino"]["version"],
        thresholds=thresholds, **execution,
    )


def validate_command_arithmetic(row: Mapping[str, Any]) -> None:
    _closed(row, {"discovered", "executed", "passed", "failed", "skipped"}, "command arithmetic")
    values = {key: row.get(key) for key in ("discovered", "executed", "passed", "failed", "skipped")}
    if any(type(value) is not int or value < 0 for value in values.values()):
        raise ValueError("command arithmetic values must be non-negative integers")
    if values["discovered"] != values["executed"] + values["skipped"]:
        raise ValueError("discovered must equal executed plus skipped")
    if values["executed"] != values["passed"] + values["failed"]:
        raise ValueError("executed must equal passed plus failed")


def validate_evidence(document: Mapping[str, Any]) -> None:
    _closed(document, {"schema_version", "run_id", "command"}, "evidence")
    if document.get("schema_version") != "1.0" or not RUN_ID.fullmatch(str(document.get("run_id", ""))):
        raise ValueError("invalid evidence identity")
    validate_command_arithmetic(document["command"])
