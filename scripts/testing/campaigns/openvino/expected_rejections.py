"""Deterministic fail-closed evidence for WB-04 expected rejections."""

from __future__ import annotations

import hashlib
import json
import os
import tempfile
from collections.abc import Mapping
from dataclasses import asdict
from pathlib import Path
from typing import Any

from .matrix import (
    EXPECTED_REJECTION_IDS,
    PROPERTY_EXPECTED_REJECTION_IDS,
    SEMANTIC_SCALAR_REJECTION_IDS,
    execution_contract,
    load_matrix,
)
from .runtime_measurement import build_runtime_property_spec


SCHEMA = "official-openvino-wb04-expected-rejection-evidence/v1"
EXPECTED_PROBE_IDS = frozenset(
    {"OV-B11-QJL", "OV-B11-POLAR"} | PROPERTY_EXPECTED_REJECTION_IDS
)
_CONTROLLER_PATH = Path(__file__).resolve()
_REPO_ROOT = _CONTROLLER_PATH.parents[4]
_HISTORICAL_CONTROLLER_PATH = "scripts/testing/official_openvino/expected_rejections.py"
_HISTORICAL_CONTROLLER_SHA256 = frozenset(
    {
        "de957a2d9624f9cd4ca1bd07c4beebb4eb1d8c7cf3029178ee4ba65ebef8696e",
        "4d2652bb2d9283bf45658e99904e45dc6f31f5412411497eee29e46331d5cf05",
    }
)
_TOP_LEVEL_KEYS = frozenset(
    {
        "schema",
        "matrix_path",
        "matrix_sha256",
        "controller_path",
        "controller_sha256",
        "probe_count",
        "controlled_test_ids",
        "generation_not_launched",
        "cleanup_process_count",
        "probes",
        "aggregate_sha256",
    }
)
_PROBE_KEYS = frozenset(
    {
        "probe_id",
        "controlled_test_id",
        "status",
        "expected_outcome",
        "matrix_case",
        "execution_contract",
        "rejection_boundary",
        "runtime_property_input",
        "expected_exception",
        "observed_exception",
        "generation_not_launched",
        "cleanup_process_count",
        "probe_sha256",
    }
)
_BOUNDARIES: dict[str, tuple[str, str]] = {
    "OV-TQS-05": ("value_algorithm", "SCALAR"),
    "OV-TQS-06": ("key_algorithm", "SCALAR"),
    "OV-TQS-07": ("value_algorithm", "SCALAR"),
    "OV-TQS-08": ("key_algorithm", "SCALAR"),
    "OV-TQS-09": ("value_algorithm", "SCALAR"),
    "OV-TQS-10": ("key_algorithm", "SCALAR"),
    "OV-TQS-11": ("value_algorithm", "SCALAR"),
    "OV-TQS-12": ("key_algorithm", "SCALAR"),
    "OV-TQ-18": ("device", "GPU"),
    "OV-TQ-19": ("key_algorithm", "QJL"),
    "OV-TQ-20": ("key_algorithm", "POLAR"),
    "OV-B11-QJL": ("key_algorithm", "QJL"),
    "OV-B11-POLAR": ("value_algorithm", "POLAR"),
}


def _canonical_bytes(value: object) -> bytes:
    return (
        json.dumps(
            value,
            allow_nan=False,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        )
        + "\n"
    ).encode("utf-8")


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _display_path(path: Path) -> str:
    resolved = path.resolve()
    try:
        return resolved.relative_to(_REPO_ROOT).as_posix()
    except ValueError:
        return str(resolved)


def _matrix_case_record(case: Any) -> dict[str, Any]:
    record = asdict(case)
    record["contexts"] = list(case.contexts)
    record["required_metrics"] = sorted(case.required_metrics)
    return record


def _unsupported_algorithm_message(field: str, value: str) -> str:
    return (
        f"unsupported runtime {field} algorithm: {value}; "
        "expected exact uppercase STANDARD, TBQ3, or TBQ4"
    )


def _expected_message(probe_id: str) -> str:
    field, value = _BOUNDARIES[probe_id]
    if field == "device":
        return "project TurboQuant is supported only on CPU"
    return _unsupported_algorithm_message(field.removesuffix("_algorithm"), value)


def _runtime_input(case: Any, contract: Any, probe_id: str) -> dict[str, Any]:
    key_algorithm = contract.runtime_key_algorithm
    value_algorithm = contract.runtime_value_algorithm
    if probe_id == "OV-B11-QJL":
        key_algorithm, value_algorithm = "QJL", "STANDARD"
    elif probe_id == "OV-B11-POLAR":
        key_algorithm, value_algorithm = "STANDARD", "POLAR"
    return {
        "device": case.device,
        "key_algorithm": key_algorithm,
        "value_algorithm": value_algorithm,
        "key_cache_precision": case.k_precision,
        "value_cache_precision": case.v_precision,
        "norm_correction": contract.norm_correction,
        "cache_dir": f"not-created-by-expected-rejection/{probe_id}",
    }


def _build_probe(case: Any, probe_id: str) -> dict[str, Any]:
    if probe_id in SEMANTIC_SCALAR_REJECTION_IDS:
        raise ValueError(
            f"{probe_id} is a semantic expected rejection, not a property probe"
        )
    contract = execution_contract(case)
    runtime_input = _runtime_input(case, contract, probe_id)
    expected_exception = {
        "type": "ValueError",
        "message": _expected_message(probe_id),
    }
    try:
        build_runtime_property_spec(**runtime_input)
    except ValueError as error:
        observed_exception = {
            "type": type(error).__name__,
            "message": str(error),
        }
    else:
        raise ValueError(
            f"{probe_id} unexpectedly produced runtime properties"
        )
    if observed_exception != expected_exception:
        raise ValueError(
            f"{probe_id} rejection mismatch: "
            f"expected {expected_exception!r}, observed {observed_exception!r}"
        )

    boundary_field, boundary_value = _BOUNDARIES[probe_id]
    probe = {
        "probe_id": probe_id,
        "controlled_test_id": case.test_id,
        "status": "passed: expected-rejection",
        "expected_outcome": "expected-rejection",
        "matrix_case": _matrix_case_record(case),
        "execution_contract": asdict(contract),
        "rejection_boundary": {
            "field": boundary_field,
            "value": boundary_value,
        },
        "runtime_property_input": runtime_input,
        "expected_exception": expected_exception,
        "observed_exception": dict(observed_exception),
        "generation_not_launched": True,
        "cleanup_process_count": 0,
    }
    probe["probe_sha256"] = _sha256_bytes(_canonical_bytes(probe))
    return probe


def generate_expected_rejection_evidence(matrix_path: Path) -> dict[str, Any]:
    """Execute only the property-validation boundary for all declared probes."""

    matrix = Path(matrix_path).resolve()
    if not matrix.is_file():
        raise ValueError(f"matrix file is missing: {matrix}")
    if EXPECTED_REJECTION_IDS != (
        PROPERTY_EXPECTED_REJECTION_IDS | SEMANTIC_SCALAR_REJECTION_IDS
    ):
        raise ValueError("matrix expected-rejection ID contract changed")
    cases = {case.test_id: case for case in load_matrix(matrix)}
    if "OV-B11" not in cases:
        raise ValueError("matrix is missing OV-B11")
    actual_expected_rejections = {
        case.test_id
        for case in cases.values()
        if execution_contract(case).expected_outcome == "expected-rejection"
    }
    if actual_expected_rejections != EXPECTED_REJECTION_IDS:
        raise ValueError("matrix expected-rejection set changed")

    probes = [
        _build_probe(
            cases["OV-B11"] if probe_id.startswith("OV-B11-") else cases[probe_id],
            probe_id,
        )
        for probe_id in sorted(EXPECTED_PROBE_IDS)
    ]
    payload = {
        "schema": SCHEMA,
        "matrix_path": _display_path(matrix),
        "matrix_sha256": _sha256_file(matrix),
        "controller_path": _display_path(_CONTROLLER_PATH),
        "controller_sha256": _sha256_file(_CONTROLLER_PATH),
        "probe_count": len(probes),
        "controlled_test_ids": sorted(
            {probe["controlled_test_id"] for probe in probes}
        ),
        "generation_not_launched": True,
        "cleanup_process_count": 0,
        "probes": probes,
    }
    payload["aggregate_sha256"] = _sha256_bytes(_canonical_bytes(payload))
    return payload


def _require_exact_keys(
    value: object,
    expected: frozenset[str],
    label: str,
) -> dict[str, Any]:
    if type(value) is not dict:
        raise ValueError(f"{label} must be an object")
    record = value
    if set(record) != expected:
        raise ValueError(f"{label} fields do not match the schema")
    return record


def validate_expected_rejection_evidence(
    payload: Mapping[str, Any],
    matrix_path: Path,
) -> dict[str, Any]:
    """Reject any evidence that is not the exact current fail-closed result."""

    aggregate = _require_exact_keys(payload, _TOP_LEVEL_KEYS, "aggregate")
    if aggregate["schema"] != SCHEMA:
        raise ValueError("expected-rejection evidence schema is unsupported")
    if aggregate["probe_count"] != len(EXPECTED_PROBE_IDS):
        raise ValueError("expected-rejection probe count mismatch")
    if aggregate["generation_not_launched"] is not True:
        raise ValueError("aggregate claims generation was launched")
    if (
        isinstance(aggregate["cleanup_process_count"], bool)
        or aggregate["cleanup_process_count"] != 0
    ):
        raise ValueError("aggregate cleanup process count is not zero")
    probes = aggregate["probes"]
    if not isinstance(probes, list):
        raise ValueError("expected-rejection probes must be a list")
    probe_ids = [
        _require_exact_keys(probe, _PROBE_KEYS, "probe").get("probe_id")
        for probe in probes
    ]
    if any(not isinstance(probe_id, str) for probe_id in probe_ids):
        raise ValueError("expected-rejection probe ID must be text")
    if len(probe_ids) != len(set(probe_ids)):
        raise ValueError("duplicate expected-rejection probe ID")
    if set(probe_ids) != EXPECTED_PROBE_IDS:
        raise ValueError("expected-rejection probe set mismatch")

    for probe in probes:
        if probe["generation_not_launched"] is not True:
            raise ValueError(f"{probe['probe_id']} claims generation was launched")
        if (
            isinstance(probe["cleanup_process_count"], bool)
            or probe["cleanup_process_count"] != 0
        ):
            raise ValueError(
                f"{probe['probe_id']} cleanup process count is not zero"
            )
        unhashed_probe = dict(probe)
        claimed_probe_hash = unhashed_probe.pop("probe_sha256")
        observed_probe_hash = _sha256_bytes(_canonical_bytes(unhashed_probe))
        if claimed_probe_hash != observed_probe_hash:
            raise ValueError(f"{probe['probe_id']} probe hash mismatch")

    unhashed_aggregate = dict(aggregate)
    claimed_aggregate_hash = unhashed_aggregate.pop("aggregate_sha256")
    observed_aggregate_hash = _sha256_bytes(
        _canonical_bytes(unhashed_aggregate)
    )
    if claimed_aggregate_hash != observed_aggregate_hash:
        raise ValueError("expected-rejection aggregate hash mismatch")

    expected = generate_expected_rejection_evidence(Path(matrix_path))
    accepted_payloads = [expected]
    for historical_sha256 in sorted(_HISTORICAL_CONTROLLER_SHA256):
        historical = dict(expected)
        historical["controller_path"] = _HISTORICAL_CONTROLLER_PATH
        historical["controller_sha256"] = historical_sha256
        historical.pop("aggregate_sha256")
        historical["aggregate_sha256"] = _sha256_bytes(
            _canonical_bytes(historical)
        )
        accepted_payloads.append(historical)
    if aggregate not in accepted_payloads:
        raise ValueError(
            "expected-rejection evidence does not match the frozen matrix "
            "and current controller"
        )
    return {
        "accepted": True,
        "schema": SCHEMA,
        "probe_count": len(probes),
        "matrix_sha256": aggregate["matrix_sha256"],
        "controller_sha256": aggregate["controller_sha256"],
        "aggregate_sha256": aggregate["aggregate_sha256"],
    }


def write_expected_rejection_evidence(
    path: Path,
    payload: Mapping[str, Any],
) -> None:
    """Validate and atomically publish new evidence without replacing a file."""

    if type(payload) is not dict:
        raise ValueError("expected-rejection publication payload must be an object")
    bound_matrix_value = payload.get("matrix_path")
    if not isinstance(bound_matrix_value, str) or not bound_matrix_value:
        raise ValueError(
            "expected-rejection publication matrix_path must be non-empty text"
        )
    bound_matrix = Path(bound_matrix_value)
    if not bound_matrix.is_absolute():
        bound_matrix = _REPO_ROOT / bound_matrix
    validate_expected_rejection_evidence(payload, bound_matrix.resolve())

    destination = Path(path).resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    file_descriptor, temporary_name = tempfile.mkstemp(
        dir=destination.parent,
        prefix=f".{destination.name}.",
        suffix=".tmp",
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(file_descriptor, "wb") as handle:
            handle.write(_canonical_bytes(payload))
            handle.flush()
            os.fsync(handle.fileno())
        try:
            os.link(temporary, destination)
        except FileExistsError as error:
            raise FileExistsError(
                f"refusing to replace existing expected-rejection evidence: "
                f"{destination}"
            ) from error
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise
    temporary.unlink()


__all__ = [
    "generate_expected_rejection_evidence",
    "validate_expected_rejection_evidence",
    "write_expected_rejection_evidence",
]
