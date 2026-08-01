"""Resumable breadth-first policy for the adaptive OpenVINO comparison."""

from __future__ import annotations

import hashlib
import json
import math
import re
from collections import defaultdict
from collections.abc import Callable, Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from scripts.testing.measure_official_openvino import (
    CampaignLock,
    MeasurementFailureRecord,
    MeasurementSequenceFailure,
    run_measurement_sequence,
)

from .matrix import OpenVINOCase, load_adaptive_comparison_matrix
from .owned_process_guard import available_ram_bytes
from .runtime_measurement import atomic_write_json, build_runtime_property_spec


CONTEXTS = (512, 1024, 2048, 4096, 8192)
CANDIDATE_ORDER = ("OV-11", "OV-TQ-22", "OV-TQ-21", "OV-12", "OV-13")
START_RESERVE_MIB = 4096
RUNTIME_FLOOR_MIB = 2048
MAX_GUARDED_ATTEMPTS = 2

STATE_SCHEMA = "official-openvino-adaptive-campaign-state/v1"
CONTROLLER_RECEIPT_SCHEMA = "official-openvino-adaptive-controller-receipt/v1"
BOUNDARY_INDEX_SCHEMA = "official-openvino-adaptive-boundary-index/v1"
SPEC_INDEX_SCHEMA = "official-openvino-adaptive-comparison-spec-index/v1"
MIB = 1024**2
_ROOT = Path(__file__).resolve().parents[3]
_SHA256 = re.compile(r"^[0-9a-f]{64}$")

_PASSING_RUNTIME_STATUS = "passed"
_TERMINAL_RUNTIME_STATUSES = frozenset(
    {
        "boundary-confirmed",
        "inconclusive-safety-boundary",
        "safety-boundary",
        "artifact-preparation-terminal",
    }
)
_RUNTIME_STATUSES = _TERMINAL_RUNTIME_STATUSES | {_PASSING_RUNTIME_STATUS}


@dataclass(frozen=True)
class AdaptiveCampaignConfig:
    matrix_path: Path
    spec_root: Path
    campaign_root: Path
    build_root: Path
    build_provenance_path: Path
    python_executable: Path
    python_site_packages: Path
    openvino_libraries: Path
    sampler_script: Path
    reference_boundary_index: Path | None = None
    max_context: int = 8192


@dataclass(frozen=True)
class StepOutcome:
    test_id: str
    context_tokens: int
    runtime_status: str
    quality_status: str
    attempt_count: int
    failure_fingerprint: str | None
    evidence_path: Path
    evidence_sha256: str


def build_ladder(matrix_path: Path) -> tuple[tuple[str, int], ...]:
    """Validate the matrix and return the fixed context-major run order."""

    load_adaptive_comparison_matrix(Path(matrix_path))
    return tuple(
        (test_id, context)
        for context in CONTEXTS
        for test_id in CANDIDATE_ORDER
    )


def _validate_state(state: Mapping[str, Any]) -> dict[str, Any]:
    if not isinstance(state, Mapping):
        raise ValueError("adaptive campaign state must be an object")
    steps = state.get("steps")
    if not isinstance(steps, Mapping):
        raise ValueError("adaptive campaign state steps must be an object")
    validated_steps: dict[str, dict[str, Any]] = {}
    for key, raw in steps.items():
        if not isinstance(key, str) or not isinstance(raw, Mapping):
            raise ValueError("adaptive campaign step is invalid")
        try:
            test_id, context_text = key.split(":", 1)
            context = int(context_text)
        except (TypeError, ValueError) as error:
            raise ValueError(f"adaptive campaign step key is invalid: {key}") from error
        if test_id not in CANDIDATE_ORDER or context not in CONTEXTS:
            raise ValueError(f"adaptive campaign step identity is invalid: {key}")
        status = raw.get("runtime_status")
        if status not in _RUNTIME_STATUSES:
            raise ValueError(f"adaptive campaign runtime status is invalid: {key}")
        attempt_count = raw.get("attempt_count")
        if (
            isinstance(attempt_count, bool)
            or not isinstance(attempt_count, int)
            or not 0 <= attempt_count <= MAX_GUARDED_ATTEMPTS
        ):
            raise ValueError(f"adaptive campaign attempt count is invalid: {key}")
        validated_steps[key] = dict(raw)
    validated = dict(state)
    validated["steps"] = validated_steps
    return validated


def _eligible_breadth_first_steps(
    state: Mapping[str, Any],
) -> tuple[tuple[str, int], ...]:
    steps = state["steps"]
    eligible: list[tuple[str, int]] = []
    for test_id in CANDIDATE_ORDER:
        for index, context in enumerate(CONTEXTS):
            key = f"{test_id}:{context}"
            current = steps.get(key)
            if current is not None:
                if current["runtime_status"] != _PASSING_RUNTIME_STATUS:
                    break
                continue
            if index == 0:
                eligible.append((test_id, context))
            else:
                previous = steps.get(f"{test_id}:{CONTEXTS[index - 1]}")
                if (
                    previous is not None
                    and previous["runtime_status"] == _PASSING_RUNTIME_STATUS
                ):
                    eligible.append((test_id, context))
            break
    order = {
        pair: position
        for position, pair in enumerate(
            (candidate, context)
            for context in CONTEXTS
            for candidate in CANDIDATE_ORDER
        )
    }
    return tuple(sorted(eligible, key=order.__getitem__))


def eligible_steps(state: Mapping[str, Any]) -> tuple[tuple[str, int], ...]:
    """Return each candidate's next unskipped context in ladder order."""

    return _eligible_breadth_first_steps(_validate_state(state))


def step_is_eligible(
    state: Mapping[str, Any], test_id: str, context_tokens: int
) -> bool:
    return (test_id, context_tokens) in eligible_steps(state)


def _reject_constant(value: str) -> None:
    raise ValueError(f"non-finite JSON number is forbidden: {value}")


def _object_without_duplicates(
    pairs: list[tuple[str, Any]],
) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON key is forbidden: {key}")
        result[key] = value
    return result


def _load_json(path: Path, field: str) -> dict[str, Any]:
    source = Path(path).resolve()
    if not source.is_file():
        raise ValueError(f"{field} is missing: {source}")
    try:
        value = json.loads(
            source.read_text(encoding="utf-8-sig"),
            object_pairs_hook=_object_without_duplicates,
            parse_constant=_reject_constant,
        )
    except (OSError, UnicodeError, json.JSONDecodeError, ValueError) as error:
        raise ValueError(f"{field} is not canonical valid JSON: {source}") from error
    if not isinstance(value, dict):
        raise ValueError(f"{field} must be a JSON object")
    return value


def _sha256_file(path: Path) -> str:
    source = Path(path).resolve()
    if not source.is_file():
        raise ValueError(f"hash-bound file is missing: {source}")
    return hashlib.sha256(source.read_bytes()).hexdigest()


def _json_bytes(value: object) -> bytes:
    return (
        json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        )
        + "\n"
    ).encode("utf-8")


def _sha256_json(value: object) -> str:
    return hashlib.sha256(_json_bytes(value).rstrip(b"\n")).hexdigest()


def _require_file(path: Path, field: str) -> Path:
    value = Path(path).resolve()
    if not value.is_file():
        raise ValueError(f"{field} is missing: {value}")
    return value


def _require_directory(path: Path, field: str) -> Path:
    value = Path(path).resolve()
    if not value.is_dir():
        raise ValueError(f"{field} directory is missing: {value}")
    return value


def _directory_sha256(path: Path) -> str:
    root = _require_directory(path, "build root")
    inventory = []
    for source in sorted(root.rglob("*")):
        if not source.is_file() or "__pycache__" in source.parts or source.suffix == ".pyc":
            continue
        inventory.append(
            {
                "path": source.relative_to(root).as_posix(),
                "size_bytes": source.stat().st_size,
                "sha256": _sha256_file(source),
            }
        )
    if not inventory:
        raise ValueError("build root contains no hash-bound files")
    return _sha256_json(inventory)


def _require_hash(value: object, field: str) -> str:
    if not isinstance(value, str) or _SHA256.fullmatch(value) is None:
        raise ValueError(f"{field} must be a SHA-256 hex digest")
    return value


def _resolved_child(root: Path, relative: object, field: str) -> Path:
    if not isinstance(relative, str) or not relative:
        raise ValueError(f"{field} path is invalid")
    lexical = Path(relative)
    if lexical.is_absolute() or ".." in lexical.parts:
        raise ValueError(f"{field} path escapes its declared root")
    resolved = (root / lexical).resolve()
    if root != resolved and root not in resolved.parents:
        raise ValueError(f"{field} path escapes its declared root")
    return resolved


def _spec_identity(case: OpenVINOCase, context: int) -> dict[str, Any]:
    return {
        "artifact_id": case.artifact_id,
        "artifact_manifest_sha256": case.artifact_manifest_sha256,
        "model": case.model,
        "device": case.device,
        "execution_route": case.execution_route,
        "context_tokens": context,
        "launch_reserve_mib": START_RESERVE_MIB,
        "emergency_floor_mib": RUNTIME_FLOOR_MIB,
    }


def _load_matrix_and_specs(
    matrix_path: Path,
    spec_root: Path,
) -> tuple[
    tuple[OpenVINOCase, ...],
    dict[str, Any],
    dict[tuple[str, int], tuple[Path, dict[str, Any]]],
]:
    matrix = _require_file(matrix_path, "adaptive comparison matrix")
    cases = load_adaptive_comparison_matrix(matrix)
    root = _require_directory(spec_root, "adaptive spec root")
    index_path = root / "spec-index.json"
    index = _load_json(index_path, "adaptive spec index")
    if index.get("schema") != SPEC_INDEX_SCHEMA:
        raise ValueError("adaptive spec index schema is invalid")
    if index.get("matrix_sha256") != _sha256_file(matrix):
        raise ValueError("adaptive spec index matrix hash drift")
    _require_hash(index.get("artifact_inventory_sha256"), "artifact inventory hash")
    raw_specs = index.get("runtime_specs")
    if not isinstance(raw_specs, list):
        raise ValueError("adaptive spec index runtime_specs must be a list")
    by_case = {case.test_id: case for case in cases}
    specs: dict[tuple[str, int], tuple[Path, dict[str, Any]]] = {}
    for entry in raw_specs:
        if not isinstance(entry, Mapping):
            raise ValueError("adaptive spec index entry is invalid")
        test_id = entry.get("test_id")
        context = entry.get("context_tokens")
        if test_id not in by_case or context not in CONTEXTS:
            raise ValueError("adaptive spec index identity is invalid")
        key = (test_id, context)
        if key in specs:
            raise ValueError("adaptive spec index contains a duplicate step")
        spec_path = _resolved_child(root, entry.get("path"), "runtime spec")
        expected_hash = _require_hash(entry.get("sha256"), "runtime spec hash")
        if _sha256_file(spec_path) != expected_hash:
            raise ValueError(f"adaptive runtime spec hash drift: {test_id}:{context}")
        spec = _load_json(spec_path, "adaptive runtime spec")
        case = by_case[test_id]
        properties = spec.get("properties")
        cache_dir = (
            properties.get("CACHE_DIR")
            if isinstance(properties, Mapping)
            else None
        )
        if not isinstance(cache_dir, str) or not cache_dir:
            raise ValueError(f"adaptive runtime spec property drift: {test_id}:{context}")
        expected_runtime = build_runtime_property_spec(
            device=case.device,
            key_algorithm=str(case.runtime_key_algorithm),
            value_algorithm=str(case.runtime_value_algorithm),
            key_cache_precision=case.k_precision,
            value_cache_precision=case.v_precision,
            norm_correction=bool(case.norm_correction),
            cache_dir=cache_dir,
        )
        if (
            spec.get("controlled_test_id") != test_id
            or spec.get("context_tokens") != context
            or spec.get("max_new_tokens") != 4
            or not isinstance(spec.get("workload"), Mapping)
            or spec["workload"].get("actual_input_tokens") != context
            or spec.get("artifact_id") != case.artifact_id
            or spec.get("artifact_manifest_sha256")
            != case.artifact_manifest_sha256
            or spec.get("device") != expected_runtime["device"]
            or properties != expected_runtime["properties"]
        ):
            raise ValueError(f"adaptive runtime spec property drift: {test_id}:{context}")
        manifest = _require_file(
            Path(str(spec.get("artifact_manifest_path"))),
            f"{test_id} artifact manifest",
        )
        if _sha256_file(manifest) != case.artifact_manifest_sha256:
            raise ValueError(f"{test_id} artifact manifest hash drift")
        specs[key] = (spec_path, spec)

    raw_terminals = index.get("terminals")
    if not isinstance(raw_terminals, list):
        raise ValueError("adaptive spec index terminals must be a list")
    terminal_ids: set[str] = set()
    for terminal in raw_terminals:
        if not isinstance(terminal, Mapping):
            raise ValueError("adaptive spec terminal is invalid")
        test_id = terminal.get("test_id")
        if test_id in terminal_ids or test_id != "OV-13":
            raise ValueError("adaptive spec terminal identity is invalid")
        case = by_case[test_id]
        receipt = _require_file(Path(str(terminal.get("receipt_path"))), "terminal receipt")
        if (
            terminal.get("stage") != "artifact-preparation"
            or terminal.get("receipt_sha256") != _sha256_file(receipt)
            or terminal.get("receipt_sha256") != case.artifact_terminal_sha256
        ):
            raise ValueError("adaptive artifact terminal receipt hash drift")
        terminal_ids.add(test_id)

    expected = {
        (case.test_id, context)
        for case in cases
        if case.test_id not in terminal_ids
        for context in CONTEXTS
    }
    if set(specs) != expected:
        raise ValueError("adaptive spec index is incomplete or has unexpected steps")
    return cases, index, specs


def _validate_config(
    config: AdaptiveCampaignConfig,
) -> tuple[
    tuple[OpenVINOCase, ...],
    dict[str, Any],
    dict[tuple[str, int], tuple[Path, dict[str, Any]]],
    dict[str, Any],
]:
    if config.max_context not in CONTEXTS:
        raise ValueError("max context must be one of 512, 1024, 2048, 4096, 8192")
    matrix = _require_file(config.matrix_path, "adaptive comparison matrix")
    spec_root = _require_directory(config.spec_root, "adaptive spec root")
    cases, index, specs = _load_matrix_and_specs(matrix, spec_root)
    build_root = _require_directory(config.build_root, "build root")
    provenance = _require_file(config.build_provenance_path, "build provenance")
    _load_json(provenance, "build provenance")
    _require_file(config.python_executable, "Python executable")
    _require_directory(config.python_site_packages, "Python site-packages")
    _require_directory(config.openvino_libraries, "OpenVINO libraries")
    _require_file(config.sampler_script, "sampler script")
    reference = (
        _require_file(config.reference_boundary_index, "reference boundary index")
        if config.reference_boundary_index is not None
        else None
    )
    bindings = {
        "matrix_path": str(matrix),
        "matrix_sha256": _sha256_file(matrix),
        "spec_index_path": str((spec_root / "spec-index.json").resolve()),
        "spec_index_sha256": _sha256_file(spec_root / "spec-index.json"),
        "artifact_inventory_sha256": index["artifact_inventory_sha256"],
        "build_root": str(build_root),
        "build_root_sha256": _directory_sha256(build_root),
        "build_provenance_path": str(provenance),
        "build_provenance_sha256": _sha256_file(provenance),
        "reference_boundary_index_path": str(reference) if reference else None,
        "reference_boundary_index_sha256": (
            _sha256_file(reference) if reference else None
        ),
    }
    return cases, index, specs, bindings


def _boundary_entries(
    boundary_index_path: Path,
    *,
    bindings: Mapping[str, Any],
    cases: tuple[OpenVINOCase, ...],
    specs: Mapping[tuple[str, int], tuple[Path, dict[str, Any]]],
) -> list[dict[str, Any]]:
    value = _load_json(boundary_index_path, "reference boundary index")
    if value.get("schema") != BOUNDARY_INDEX_SCHEMA:
        raise ValueError("reference boundary index schema is invalid")
    if (
        value.get("matrix_sha256") != bindings["matrix_sha256"]
        or value.get("spec_index_sha256") != bindings["spec_index_sha256"]
    ):
        raise ValueError("reference boundary index input hash drift")
    entries = value.get("boundaries")
    if not isinstance(entries, list):
        raise ValueError("reference boundary index boundaries must be a list")
    by_case = {case.test_id: case for case in cases}
    validated: list[dict[str, Any]] = []
    seen: set[tuple[str, int]] = set()
    for raw in entries:
        if not isinstance(raw, Mapping):
            raise ValueError("reference boundary is not an equivalent boundary")
        test_id = raw.get("test_id")
        context = raw.get("context_tokens")
        key = (test_id, context)
        if key in seen or key not in specs or test_id not in by_case:
            raise ValueError("reference boundary is not an equivalent boundary")
        fingerprint = _require_hash(
            raw.get("failure_fingerprint"), "boundary failure fingerprint"
        )
        attempts = raw.get("attempts")
        count = raw.get("matching_attempt_count")
        if (
            isinstance(count, bool)
            or not isinstance(count, int)
            or count < 2
            or not isinstance(attempts, list)
            or len(attempts) < 2
            or count != len(attempts)
            or any(
                not isinstance(attempt, Mapping)
                or attempt.get("failure_fingerprint") != fingerprint
                or not isinstance(attempt.get("path"), str)
                or _SHA256.fullmatch(str(attempt.get("sha256"))) is None
                for attempt in attempts
            )
            or raw.get("identity") != _spec_identity(by_case[test_id], context)
        ):
            raise ValueError("reference boundary is not an equivalent boundary")
        validated.append(dict(raw))
        seen.add(key)
    return validated


def _initial_state(
    bindings: Mapping[str, Any],
    *,
    cases: tuple[OpenVINOCase, ...],
    specs: Mapping[tuple[str, int], tuple[Path, dict[str, Any]]],
    reference_boundary_index: Path | None,
) -> dict[str, Any]:
    state: dict[str, Any] = {
        "schema": STATE_SCHEMA,
        "policy": {
            "contexts": list(CONTEXTS),
            "candidate_order": list(CANDIDATE_ORDER),
            "start_reserve_mib": START_RESERVE_MIB,
            "runtime_floor_mib": RUNTIME_FLOOR_MIB,
            "max_guarded_attempts": MAX_GUARDED_ATTEMPTS,
        },
        "bindings": dict(bindings),
        "steps": {},
        "boundaries": {},
    }
    if reference_boundary_index is not None:
        for boundary in _boundary_entries(
            reference_boundary_index,
            bindings=bindings,
            cases=cases,
            specs=specs,
        ):
            test_id = boundary["test_id"]
            context = boundary["context_tokens"]
            key = f"{test_id}:{context}"
            state["steps"][key] = {
                "test_id": test_id,
                "context_tokens": context,
                "runtime_status": "boundary-confirmed",
                "quality_status": "not-run",
                "attempt_count": min(
                    boundary["matching_attempt_count"], MAX_GUARDED_ATTEMPTS
                ),
                "failure_fingerprint": boundary["failure_fingerprint"],
                "evidence_path": str(Path(reference_boundary_index).resolve()),
                "evidence_sha256": bindings[
                    "reference_boundary_index_sha256"
                ],
                "attempts": [dict(item) for item in boundary["attempts"]],
                "boundary_source": "explicit-reference-index",
            }
            state["boundaries"][test_id] = {
                "context_tokens": context,
                "failure_fingerprint": boundary["failure_fingerprint"],
                "source": "explicit-reference-index",
            }
    return state


def save_state_atomically(path: Path, state: Mapping[str, Any]) -> None:
    _validate_state(state)
    atomic_write_json(Path(path), dict(state))


def load_or_create_state(config: AdaptiveCampaignConfig) -> dict[str, Any]:
    cases, _index, specs, bindings = _validate_config(config)
    state_path = Path(config.campaign_root).resolve() / "adaptive-campaign-state.json"
    if state_path.exists():
        state = _load_json(state_path, "adaptive campaign state")
        if state.get("schema") != STATE_SCHEMA:
            raise ValueError("adaptive campaign state schema drift")
        if state.get("bindings") != bindings:
            raise ValueError("adaptive campaign input drift prevents resume")
        expected_policy = {
            "contexts": list(CONTEXTS),
            "candidate_order": list(CANDIDATE_ORDER),
            "start_reserve_mib": START_RESERVE_MIB,
            "runtime_floor_mib": RUNTIME_FLOOR_MIB,
            "max_guarded_attempts": MAX_GUARDED_ATTEMPTS,
        }
        if state.get("policy") != expected_policy:
            raise ValueError("adaptive campaign policy drift prevents resume")
        validated = _validate_state(state)
        _validate_state_receipts(config, validated)
        return validated
    state = _initial_state(
        bindings,
        cases=cases,
        specs=specs,
        reference_boundary_index=config.reference_boundary_index,
    )
    save_state_atomically(state_path, state)
    return state


def _nested(record: Mapping[str, Any], *paths: tuple[str, ...]) -> Any:
    for path in paths:
        value: Any = record
        for field in path:
            if not isinstance(value, Mapping) or field not in value:
                break
            value = value[field]
        else:
            return value
    return None


def _failure_category(record: Mapping[str, Any]) -> str:
    value = _nested(
        record,
        ("failure_category",),
        ("category",),
        ("guard", "category"),
    )
    if isinstance(value, str):
        normalized = value.strip().lower().replace("_", "-")
    else:
        errors = record.get("validation_errors")
        messages = (
            [item.lower() for item in errors if isinstance(item, str)]
            if isinstance(errors, list)
            else []
        )
        if any("ram query failed" in item for item in messages):
            return "ram-query"
        if record.get("low_memory_stop") is True or any(
            "ram is below" in item and "floor" in item for item in messages
        ):
            return "ram-floor"
        if record.get("exit_code") not in (None, 0) or messages:
            return "functional"
        return "unknown"
    aliases = {
        "available-ram-floor": "ram-floor",
        "memory-floor": "ram-floor",
        "runtime-functional": "functional",
    }
    return aliases.get(normalized, normalized)


def _normalized_failure_code(record: Mapping[str, Any]) -> str:
    value = _nested(
        record,
        ("failure_code",),
        ("exit_code",),
        ("worker", "exit_code"),
        ("validation_code",),
    )
    if isinstance(value, bool) or value is None:
        return "UNSPECIFIED"
    if isinstance(value, (int, float)):
        if isinstance(value, float) and not math.isfinite(value):
            return "UNSPECIFIED"
        return str(value)
    if not isinstance(value, str):
        return "UNSPECIFIED"
    return re.sub(r"[^A-Z0-9]+", "_", value.strip().upper()).strip("_") or "UNSPECIFIED"


def _failure_identity(
    failure: MeasurementFailureRecord,
) -> tuple[dict[str, Any], str]:
    record = dict(failure.record) if isinstance(failure.record, Mapping) else {}
    role = failure.role
    identity = {
        "stage": _nested(record, ("stage",), ("role",)) or role,
        "category": _failure_category(record),
        "artifact_manifest_sha256": _nested(
            record,
            ("artifact_manifest_sha256",),
            ("identity_hashes", "artifact_manifest_sha256"),
        ),
        "execution_route": _nested(
            record,
            ("execution_route",),
            ("activation", "execution_route"),
            ("worker", "execution_route"),
        ),
        "context_tokens": _nested(
            record,
            ("context_tokens",),
            ("context",),
            ("worker", "context"),
        ),
        "launch_reserve_mib": _nested(
            record,
            ("launch_minimum_available_ram_mib",),
            ("launch_reserve_mib",),
        ),
        "emergency_floor_mib": _nested(
            record,
            ("emergency_minimum_available_ram_mib",),
            ("emergency_floor_mib",),
        ),
        "normalized_failure_code": _normalized_failure_code(record),
    }
    return identity, _sha256_json(identity)


def _retryable_failure(record: Mapping[str, Any]) -> bool:
    available = record.get("available_ram_bytes")
    inferred_ram_query = (
        isinstance(available, Mapping)
        and isinstance(available.get("before"), int)
        and not isinstance(available.get("before"), bool)
        and isinstance(available.get("after"), int)
        and not isinstance(available.get("after"), bool)
    )
    ram_query_succeeded = record.get(
        "ram_query_succeeded", inferred_ram_query
    )
    inferred_launch_restore = (
        inferred_ram_query
        and int(available["after"]) >= START_RESERVE_MIB * MIB
    )
    launch_reserve_restored = record.get(
        "launch_reserve_restored", inferred_launch_restore
    )
    clean = (
        record.get("cleanup_process_count") == 0
        and record.get("residual_owned_process_count") == 0
        and not record.get("emergency_actions")
        and record.get("os_instability", False) is False
        and ram_query_succeeded is True
        and launch_reserve_restored is True
    )
    return clean and _failure_category(record) in {"ram-floor", "functional"}


def classify_runtime_failure(
    failure: MeasurementFailureRecord,
) -> StepOutcome:
    if not isinstance(failure, MeasurementFailureRecord):
        raise TypeError("failure must be a MeasurementFailureRecord")
    record = dict(failure.record) if isinstance(failure.record, Mapping) else {}
    identity, fingerprint = _failure_identity(failure)
    test_id = _nested(record, ("controlled_test_id",), ("test_id",))
    context = identity["context_tokens"]
    if test_id not in CANDIDATE_ORDER:
        test_id = "OV-11"
    if context not in CONTEXTS:
        context = CONTEXTS[0]
    evidence = Path(failure.record_path).resolve() if failure.record_path else Path()
    evidence_hash = (
        _sha256_file(evidence)
        if failure.record_path is not None and evidence.is_file()
        else _sha256_json(record)
    )
    return StepOutcome(
        test_id=test_id,
        context_tokens=context,
        runtime_status=(
            "retryable-failure" if _retryable_failure(record) else "safety-boundary"
        ),
        quality_status="not-run",
        attempt_count=1,
        failure_fingerprint=fingerprint,
        evidence_path=evidence,
        evidence_sha256=evidence_hash,
    )


def _require_start_reserve(available_ram: Callable[[], int | None]) -> int:
    value = available_ram()
    minimum = START_RESERVE_MIB * MIB
    if isinstance(value, bool) or not isinstance(value, int):
        raise RuntimeError("available RAM query failed")
    if value < minimum:
        raise RuntimeError("4096 MiB launch reserve is not restored")
    return value


def _require_zero_recorded_survivors(state: Mapping[str, Any]) -> None:
    for step in state["steps"].values():
        attempts = step.get("attempts", [])
        if not isinstance(attempts, list):
            raise ValueError("adaptive controller attempt chain is invalid")
        for attempt in attempts:
            if (
                isinstance(attempt, Mapping)
                and attempt.get("residual_owned_process_count", 0) != 0
            ):
                raise RuntimeError("campaign Job Object reports owned survivors")


def _write_controller_receipt(
    *,
    config: AdaptiveCampaignConfig,
    test_id: str,
    context: int,
    attempt_number: int,
    status: str,
    evidence_path: Path,
    evidence_sha256: str,
    failure_fingerprint: str | None,
    residual_owned_process_count: int,
) -> dict[str, Any]:
    root = Path(config.campaign_root).resolve()
    relative = (
        Path("controller-receipts")
        / test_id
        / str(context)
        / f"attempt-{attempt_number:03d}.json"
    )
    destination = root / relative
    if destination.exists():
        raise RuntimeError("completed or failed controller attempt is immutable")
    receipt = {
        "schema": CONTROLLER_RECEIPT_SCHEMA,
        "test_id": test_id,
        "context_tokens": context,
        "attempt_number": attempt_number,
        "status": status,
        "evidence_path": str(Path(evidence_path).resolve()),
        "evidence_sha256": evidence_sha256,
        "failure_fingerprint": failure_fingerprint,
        "residual_owned_process_count": residual_owned_process_count,
        "launch_reserve_mib": START_RESERVE_MIB,
        "emergency_floor_mib": RUNTIME_FLOOR_MIB,
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    atomic_write_json(destination, receipt)
    return {
        "attempt_number": attempt_number,
        "status": status,
        "receipt_path": relative.as_posix(),
        "receipt_sha256": _sha256_file(destination),
        "evidence_path": receipt["evidence_path"],
        "evidence_sha256": evidence_sha256,
        "failure_fingerprint": failure_fingerprint,
        "residual_owned_process_count": residual_owned_process_count,
    }


def _controller_attempts(
    config: AdaptiveCampaignConfig,
    test_id: str,
    context: int,
) -> list[dict[str, Any]]:
    root = Path(config.campaign_root).resolve()
    receipt_root = root / "controller-receipts" / test_id / str(context)
    if not receipt_root.exists():
        return []
    if not receipt_root.is_dir():
        raise ValueError("controller receipt root is invalid")
    discovered: list[tuple[int, Path]] = []
    for path in receipt_root.iterdir():
        match = re.fullmatch(r"attempt-(\d{3})\.json", path.name)
        if path.is_file() and match is not None:
            discovered.append((int(match.group(1)), path.resolve()))
        else:
            raise ValueError("controller receipt directory contains an unexpected entry")
    discovered.sort()
    if [number for number, _ in discovered] != list(
        range(1, len(discovered) + 1)
    ):
        raise ValueError("controller receipt attempt numbering is not monotonic")
    if len(discovered) > MAX_GUARDED_ATTEMPTS:
        raise ValueError("controller receipt attempt limit was exceeded")
    attempts: list[dict[str, Any]] = []
    for number, path in discovered:
        receipt = _load_json(path, "controller receipt")
        if (
            receipt.get("schema") != CONTROLLER_RECEIPT_SCHEMA
            or receipt.get("test_id") != test_id
            or receipt.get("context_tokens") != context
            or receipt.get("attempt_number") != number
            or receipt.get("status") not in {"passed", "retryable-failure", "safety-boundary"}
            or receipt.get("launch_reserve_mib") != START_RESERVE_MIB
            or receipt.get("emergency_floor_mib") != RUNTIME_FLOOR_MIB
        ):
            raise ValueError("controller receipt identity is invalid")
        evidence = _require_file(
            Path(str(receipt.get("evidence_path"))), "controller receipt evidence"
        )
        evidence_hash = _require_hash(
            receipt.get("evidence_sha256"), "controller receipt evidence hash"
        )
        if _sha256_file(evidence) != evidence_hash:
            raise ValueError("controller receipt evidence hash drift")
        relative = path.relative_to(root).as_posix()
        attempts.append(
            {
                "attempt_number": number,
                "status": receipt["status"],
                "receipt_path": relative,
                "receipt_sha256": _sha256_file(path),
                "evidence_path": str(evidence),
                "evidence_sha256": evidence_hash,
                "failure_fingerprint": receipt.get("failure_fingerprint"),
                "residual_owned_process_count": receipt.get(
                    "residual_owned_process_count"
                ),
            }
        )
    return attempts


def _validate_state_receipts(
    config: AdaptiveCampaignConfig,
    state: Mapping[str, Any],
) -> None:
    for key, step in state["steps"].items():
        if step.get("boundary_source") == "explicit-reference-index":
            for attempt in step.get("attempts", []):
                if not isinstance(attempt, Mapping):
                    raise ValueError("reference boundary receipt is invalid")
                evidence = _require_file(
                    Path(str(attempt.get("path"))), "reference boundary evidence"
                )
                if _sha256_file(evidence) != attempt.get("sha256"):
                    raise ValueError("reference boundary receipt hash drift")
            continue
        test_id, context_text = key.split(":", 1)
        attempts = _controller_attempts(config, test_id, int(context_text))
        if attempts != step.get("attempts"):
            raise ValueError("controller receipt chain drift prevents resume")
        if len(attempts) != step.get("attempt_count"):
            raise ValueError("controller receipt attempt count drift prevents resume")


def _runtime_kwargs(
    config: AdaptiveCampaignConfig,
    *,
    spec_path: Path,
    spec: Mapping[str, Any],
    test_id: str,
    context: int,
) -> dict[str, Any]:
    return {
        "spec_path": spec_path,
        "campaign_root": (
            Path(config.campaign_root).resolve()
            / "runtime"
            / test_id
            / f"context-{context}"
        ),
        "matrix_path": Path(config.matrix_path).resolve(),
        "artifact_manifest_path": Path(str(spec["artifact_manifest_path"])).resolve(),
        "build_provenance_path": Path(config.build_provenance_path).resolve(),
        "build_root": Path(config.build_root).resolve(),
        "repo_root": _ROOT,
        "python_executable": Path(config.python_executable).resolve(),
        "python_site_packages": Path(config.python_site_packages).resolve(),
        "openvino_libraries": Path(config.openvino_libraries).resolve(),
        "sampler_script": Path(config.sampler_script).resolve(),
        "launch_minimum_available_ram_mib": START_RESERVE_MIB,
        "emergency_minimum_available_ram_mib": RUNTIME_FLOOR_MIB,
    }


def _execute_runtime_step(
    state: dict[str, Any],
    *,
    config: AdaptiveCampaignConfig,
    test_id: str,
    context: int,
    spec_path: Path,
    spec: Mapping[str, Any],
    run_runtime: Callable[..., Mapping[str, Any]],
    available_ram: Callable[[], int | None],
) -> dict[str, Any]:
    attempts = _controller_attempts(config, test_id, context)
    failure_fingerprints = [
        attempt["failure_fingerprint"]
        for attempt in attempts
        if attempt["status"] == "retryable-failure"
    ]
    runtime_root = _runtime_kwargs(
        config,
        spec_path=spec_path,
        spec=spec,
        test_id=test_id,
        context=context,
    )["campaign_root"]
    if attempts:
        last = attempts[-1]
        if last["status"] == "passed":
            state["steps"][f"{test_id}:{context}"] = {
                "test_id": test_id,
                "context_tokens": context,
                "runtime_status": "passed",
                "quality_status": "not-run",
                "attempt_count": len(attempts),
                "failure_fingerprint": None,
                "evidence_path": last["evidence_path"],
                "evidence_sha256": last["evidence_sha256"],
                "attempts": attempts,
            }
            return state
        if last["status"] == "safety-boundary":
            status = "safety-boundary"
        elif len(attempts) == MAX_GUARDED_ATTEMPTS:
            status = (
                "boundary-confirmed"
                if failure_fingerprints[0] == failure_fingerprints[1]
                else "inconclusive-safety-boundary"
            )
        else:
            status = "retryable-failure"
        if status != "retryable-failure":
            state["steps"][f"{test_id}:{context}"] = {
                "test_id": test_id,
                "context_tokens": context,
                "runtime_status": status,
                "quality_status": "not-run",
                "attempt_count": len(attempts),
                "failure_fingerprint": (
                    last["failure_fingerprint"]
                    if status == "boundary-confirmed"
                    else None
                ),
                "evidence_path": last["evidence_path"],
                "evidence_sha256": last["evidence_sha256"],
                "attempts": attempts,
            }
            return state

    for attempt_number in range(len(attempts) + 1, MAX_GUARDED_ATTEMPTS + 1):
        _require_zero_recorded_survivors(state)
        _require_start_reserve(available_ram)
        try:
            result = dict(
                run_runtime(
                    **_runtime_kwargs(
                        config,
                        spec_path=spec_path,
                        spec=spec,
                        test_id=test_id,
                        context=context,
                    )
                )
            )
        except MeasurementSequenceFailure as error:
            raw_failure = error.failure
            raw_record = (
                dict(raw_failure.record)
                if isinstance(raw_failure.record, Mapping)
                else {}
            )
            raw_record.setdefault("controlled_test_id", test_id)
            raw_record.setdefault("context_tokens", context)
            raw_record.setdefault("artifact_id", spec.get("artifact_id"))
            raw_record.setdefault(
                "artifact_manifest_sha256",
                spec.get("artifact_manifest_sha256"),
            )
            case = next(
                item
                for item in load_adaptive_comparison_matrix(config.matrix_path)
                if item.test_id == test_id
            )
            raw_record.setdefault("model", case.model)
            raw_record.setdefault("device", case.device)
            raw_record.setdefault("execution_route", case.execution_route)
            raw_record.setdefault(
                "launch_minimum_available_ram_mib", START_RESERVE_MIB
            )
            raw_record.setdefault(
                "emergency_minimum_available_ram_mib", RUNTIME_FLOOR_MIB
            )
            failure = MeasurementFailureRecord(
                role=raw_failure.role,
                record_path=raw_failure.record_path,
                record=raw_record,
                fingerprint=raw_failure.fingerprint,
            )
            outcome = classify_runtime_failure(failure)
            record = raw_record
            receipt = _write_controller_receipt(
                config=config,
                test_id=test_id,
                context=context,
                attempt_number=attempt_number,
                status=outcome.runtime_status,
                evidence_path=outcome.evidence_path,
                evidence_sha256=outcome.evidence_sha256,
                failure_fingerprint=outcome.failure_fingerprint,
                residual_owned_process_count=int(
                    record.get("residual_owned_process_count", -1)
                ),
            )
            attempts.append(receipt)
            failure_fingerprints.append(outcome.failure_fingerprint)
            if outcome.runtime_status != "retryable-failure":
                status = "safety-boundary"
                break
            if attempt_number == MAX_GUARDED_ATTEMPTS:
                status = (
                    "boundary-confirmed"
                    if failure_fingerprints[0] == failure_fingerprints[1]
                    else "inconclusive-safety-boundary"
                )
                break
            try:
                _require_zero_recorded_survivors(
                    {"steps": {"current": {"attempts": attempts}}}
                )
                _require_start_reserve(available_ram)
            except RuntimeError:
                status = "safety-boundary"
                break
            continue
        evidence_path = Path(runtime_root) / "attempt-sequence.json"
        if not evidence_path.is_file() or _load_json(
            evidence_path, "measurement sequence evidence"
        ) != result:
            raise RuntimeError("runtime sequence result is not immutable evidence")
        if (
            result.get("accepted_sample_count") != 3
            or result.get("cleanup_process_count") != 0
        ):
            raise RuntimeError("runtime sequence did not pass formal validation")
        evidence_sha256 = _sha256_file(evidence_path)
        attempts.append(
            _write_controller_receipt(
                config=config,
                test_id=test_id,
                context=context,
                attempt_number=attempt_number,
                status="passed",
                evidence_path=evidence_path,
                evidence_sha256=evidence_sha256,
                failure_fingerprint=None,
                residual_owned_process_count=0,
            )
        )
        state["steps"][f"{test_id}:{context}"] = {
            "test_id": test_id,
            "context_tokens": context,
            "runtime_status": "passed",
            "quality_status": "not-run",
            "attempt_count": attempt_number,
            "failure_fingerprint": None,
            "evidence_path": str(evidence_path.resolve()),
            "evidence_sha256": evidence_sha256,
            "attempts": attempts,
        }
        return state
    else:  # pragma: no cover - the fixed two-attempt loop always returns or breaks
        raise AssertionError("unreachable guarded attempt state")

    last_outcome_fingerprint = attempts[-1]["failure_fingerprint"]
    last_evidence_path = Path(attempts[-1]["evidence_path"])
    last_evidence_sha256 = attempts[-1]["evidence_sha256"]
    state["steps"][f"{test_id}:{context}"] = {
        "test_id": test_id,
        "context_tokens": context,
        "runtime_status": status,
        "quality_status": "not-run",
        "attempt_count": len(attempts),
        "failure_fingerprint": (
            last_outcome_fingerprint
            if status == "boundary-confirmed"
            else None
        ),
        "evidence_path": str(last_evidence_path.resolve()),
        "evidence_sha256": last_evidence_sha256,
        "attempts": attempts,
    }
    if status == "boundary-confirmed":
        state["boundaries"][test_id] = {
            "context_tokens": context,
            "failure_fingerprint": last_outcome_fingerprint,
            "source": "matching-guarded-attempts",
        }
    return state


def _execute_quality_step(
    state: dict[str, Any],
    *,
    config: AdaptiveCampaignConfig,
    test_id: str,
    context: int,
    run_quality: Callable[..., Mapping[str, Any]] | None,
) -> dict[str, Any]:
    step = state["steps"][f"{test_id}:{context}"]
    if run_quality is None:
        step["quality_status"] = "quality-blocked"
        step["quality_recovery"] = {
            "test_id": test_id,
            "context_tokens": context,
            "runtime_evidence_path": step["evidence_path"],
            "runtime_evidence_sha256": step["evidence_sha256"],
        }
        return state
    quality_input = {
        "test_id": test_id,
        "context_tokens": context,
        "runtime_evidence_path": step["evidence_path"],
        "runtime_evidence_sha256": step["evidence_sha256"],
        "campaign_root": str(Path(config.campaign_root).resolve()),
    }
    result = dict(run_quality(quality_input, resume=False))
    status = result.get("status")
    if status not in {"passed", "quality-blocked"}:
        raise RuntimeError("quality callback returned an invalid status")
    step["quality_status"] = status
    step["quality_result"] = result
    return state


def run_adaptive_campaign(
    config: AdaptiveCampaignConfig,
    *,
    run_runtime: Callable[..., Mapping[str, Any]] = run_measurement_sequence,
    run_quality: Callable[..., Mapping[str, Any]] | None = None,
    publish_checkpoint: Callable[[Path, bool], Mapping[str, Any]] | None = None,
    available_ram: Callable[[], int | None] = available_ram_bytes,
) -> dict[str, Any]:
    """Run or resume the fixed ladder while checkpointing each final step."""

    campaign_root = Path(config.campaign_root).resolve()
    with CampaignLock(campaign_root):
        state = load_or_create_state(config)
        _cases, _index, specs, _bindings = _validate_config(config)
        state_path = campaign_root / "adaptive-campaign-state.json"
        for test_id, context in build_ladder(config.matrix_path):
            if context > config.max_context or not step_is_eligible(
                state, test_id, context
            ):
                continue
            spec_entry = specs.get((test_id, context))
            if spec_entry is None:
                continue
            state = _execute_runtime_step(
                state,
                config=config,
                test_id=test_id,
                context=context,
                spec_path=spec_entry[0],
                spec=spec_entry[1],
                run_runtime=run_runtime,
                available_ram=available_ram,
            )
            if state["steps"][f"{test_id}:{context}"]["runtime_status"] == "passed":
                state = _execute_quality_step(
                    state,
                    config=config,
                    test_id=test_id,
                    context=context,
                    run_quality=run_quality,
                )
            save_state_atomically(state_path, state)
            if publish_checkpoint is not None:
                publish_checkpoint(state_path, False)
            if (
                state["steps"][f"{test_id}:{context}"]["runtime_status"]
                == "safety-boundary"
            ):
                break
        return state


def preflight_adaptive_campaign(
    config: AdaptiveCampaignConfig,
    *,
    available_ram: Callable[[], int | None] = available_ram_bytes,
) -> dict[str, Any]:
    """Validate all bindings and safety gates without creating an attempt."""

    with CampaignLock(Path(config.campaign_root).resolve()):
        state = load_or_create_state(config)
        _require_zero_recorded_survivors(state)
        _require_start_reserve(available_ram)
        return state


def campaign_status(
    config: AdaptiveCampaignConfig,
    state: Mapping[str, Any],
) -> dict[str, Any]:
    validated = _validate_state(state)
    path = Path(config.campaign_root).resolve() / "adaptive-campaign-state.json"
    candidates = [
        pair
        for pair in eligible_steps(validated)
        if pair[1] <= config.max_context
    ]
    steps = validated["steps"].values()
    return {
        "state_path": str(path),
        "state_sha256": _sha256_file(path),
        "completed_step_count": sum(
            step["runtime_status"] == "passed" for step in steps
        ),
        "terminal_step_count": sum(
            step["runtime_status"] in _TERMINAL_RUNTIME_STATUSES
            for step in steps
        ),
        "next_eligible_step": list(candidates[0]) if candidates else None,
    }


def _attempt_boundary_identity(
    record: Mapping[str, Any],
) -> tuple[str | None, int | None, dict[str, Any]]:
    test_id = _nested(record, ("controlled_test_id",), ("test_id",))
    context = _nested(record, ("context_tokens",), ("context",), ("worker", "context"))
    identity = {
        "artifact_id": _nested(record, ("artifact_id",)),
        "artifact_manifest_sha256": _nested(
            record,
            ("artifact_manifest_sha256",),
            ("identity_hashes", "artifact_manifest_sha256"),
        ),
        "model": _nested(record, ("model",), ("model_name",)),
        "device": _nested(record, ("device",), ("worker", "device")),
        "execution_route": _nested(record, ("execution_route",)),
        "context_tokens": context,
        "launch_reserve_mib": _nested(
            record,
            ("launch_minimum_available_ram_mib",),
            ("launch_reserve_mib",),
        ),
        "emergency_floor_mib": _nested(
            record,
            ("emergency_minimum_available_ram_mib",),
            ("emergency_floor_mib",),
        ),
    }
    return test_id, context, identity


def build_boundary_index(
    *,
    matrix_path: Path,
    spec_root: Path,
    historical_root: Path,
    output_path: Path,
) -> dict[str, Any]:
    """Build an explicit index from hash-validated attempts under one root."""

    matrix = _require_file(matrix_path, "adaptive comparison matrix")
    root = _require_directory(spec_root, "adaptive spec root")
    historical = _require_directory(historical_root, "historical root")
    cases, _index, specs = _load_matrix_and_specs(matrix, root)
    by_case = {case.test_id: case for case in cases}
    groups: defaultdict[
        tuple[str, int, str, str], list[dict[str, Any]]
    ] = defaultdict(list)
    for discovered in sorted(historical.rglob("attempt.json")):
        attempt = discovered.resolve()
        if historical != attempt and historical not in attempt.parents:
            raise ValueError("historical attempt escapes caller-provided root")
        record = _load_json(attempt, "historical attempt")
        if _failure_category(record) not in {"ram-floor", "functional"}:
            continue
        receipt_path = attempt.parents[1] / "sequence-receipt.json"
        receipt = _load_json(receipt_path, "historical sequence receipt")
        attempt_hash = _sha256_file(attempt)
        if receipt.get("runtime_record_sha256") != attempt_hash:
            raise ValueError("historical attempt hash validation failed")
        role = _nested(record, ("stage",), ("role",)) or "pilot"
        failure = MeasurementFailureRecord(role, attempt, record, "historical")
        if not _retryable_failure(record):
            continue
        _fingerprint_identity, fingerprint = _failure_identity(failure)
        test_id, context, identity = _attempt_boundary_identity(record)
        key = (test_id, context)
        if key not in specs or test_id not in by_case:
            continue
        expected = _spec_identity(by_case[test_id], context)
        if identity != expected:
            continue
        groups[(test_id, context, fingerprint, _sha256_json(identity))].append(
            {
                "path": str(attempt),
                "sha256": attempt_hash,
                "failure_fingerprint": fingerprint,
            }
        )
    boundaries = []
    for (test_id, context, fingerprint, _), attempts in sorted(groups.items()):
        if len(attempts) < 2:
            continue
        boundaries.append(
            {
                "test_id": test_id,
                "context_tokens": context,
                "failure_fingerprint": fingerprint,
                "matching_attempt_count": len(attempts),
                "identity": _spec_identity(by_case[test_id], context),
                "attempts": attempts,
            }
        )
    result = {
        "schema": BOUNDARY_INDEX_SCHEMA,
        "matrix_sha256": _sha256_file(matrix),
        "spec_index_sha256": _sha256_file(root / "spec-index.json"),
        "historical_root": str(historical),
        "boundaries": boundaries,
    }
    output = Path(output_path).resolve()
    if output.exists():
        if output.read_bytes() != _json_bytes(result):
            raise ValueError("existing boundary index is non-identical")
        return result
    output.parent.mkdir(parents=True, exist_ok=True)
    atomic_write_json(output, result)
    return result


__all__ = [
    "AdaptiveCampaignConfig",
    "CANDIDATE_ORDER",
    "CONTEXTS",
    "MAX_GUARDED_ATTEMPTS",
    "RUNTIME_FLOOR_MIB",
    "START_RESERVE_MIB",
    "StepOutcome",
    "build_boundary_index",
    "build_ladder",
    "campaign_status",
    "classify_runtime_failure",
    "eligible_steps",
    "load_or_create_state",
    "preflight_adaptive_campaign",
    "run_adaptive_campaign",
    "save_state_atomically",
    "step_is_eligible",
]
