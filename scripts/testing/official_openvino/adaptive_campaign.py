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

from .conversion import validate_artifact_manifest
from .matrix import OpenVINOCase, load_adaptive_comparison_matrix
from .owned_process_guard import KillOnCloseJob, available_ram_bytes
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
JOB_PROBE_SCHEMA = "official-openvino-adaptive-job-probe/v1"
ARTIFACT_TERMINAL_RECEIPT_SCHEMA = (
    "official-openvino-adaptive-controller-terminal-receipt/v1"
)
TASK_THREE_CAMPAIGN_SCHEMA = "official-openvino-wb04-campaign-identity/v1"
TASK_THREE_RECEIPT_SCHEMA = "official-openvino-wb04-sequence-receipt/v1"
TASK_THREE_SPEC_SCHEMA = "official-openvino-wb04-worker-spec/v1"
TASK_THREE_RUN_SCHEMA = "official-openvino-wb04-governed-run/v1"
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


def _campaign_job_name(campaign_root: Path) -> str:
    identity = hashlib.sha256(
        str(Path(campaign_root).resolve()).encode("utf-8")
    ).hexdigest()[:24]
    return f"WB04-adaptive-campaign-{identity}"


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
    halt = state.get("campaign_halt")
    if halt is not None and (
        not isinstance(halt, Mapping)
        or not isinstance(halt.get("reason"), str)
        or not halt["reason"]
    ):
        raise ValueError("adaptive campaign halt record is invalid")
    probes = state.get("safety_probes", [])
    if not isinstance(probes, list) or any(
        not isinstance(probe, Mapping) for probe in probes
    ):
        raise ValueError("adaptive campaign safety probe chain is invalid")
    validated["campaign_halt"] = dict(halt) if halt is not None else None
    validated["safety_probes"] = [dict(probe) for probe in probes]
    return validated


def _eligible_breadth_first_steps(
    state: Mapping[str, Any],
) -> tuple[tuple[str, int], ...]:
    if state.get("campaign_halt") is not None:
        return ()
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


def _task_three_directory_identity(
    path: Path,
    *,
    allow_missing: bool = False,
) -> dict[str, Any] | None:
    root = Path(path).resolve()
    if not root.is_dir():
        if not allow_missing:
            return None
        files: list[dict[str, Any]] = []
    else:
        files = [
            {
                "path": source.relative_to(root).as_posix(),
                "bytes": source.stat().st_size,
                "sha256": _sha256_file(source),
            }
            for source in sorted(
                root.rglob("*"),
                key=lambda candidate: candidate.relative_to(root).as_posix(),
            )
            if source.is_file()
            and "__pycache__" not in source.relative_to(root).parts
            and source.suffix.lower() not in {".pyc", ".pyo"}
        ]
    return {
        "path": str(root),
        "files": files,
        "content_sha256": _sha256_json(files),
    }


def _file_identity_matches(
    value: object,
    *,
    expected_path: Path | None = None,
) -> bool:
    if not isinstance(value, Mapping) or set(value) != {"path", "sha256"}:
        return False
    path_value = value.get("path")
    if not isinstance(path_value, str) or not path_value:
        return False
    path = Path(path_value).resolve()
    if expected_path is not None and path != Path(expected_path).resolve():
        return False
    return (
        path.is_file()
        and isinstance(value.get("sha256"), str)
        and value["sha256"] == _sha256_file(path)
    )


def _directory_identity_matches(
    value: object,
    *,
    expected_path: Path | None = None,
    allow_missing: bool = False,
) -> bool:
    if not isinstance(value, Mapping):
        return False
    path_value = value.get("path")
    if not isinstance(path_value, str) or not path_value:
        return False
    path = Path(path_value).resolve()
    if expected_path is not None and path != Path(expected_path).resolve():
        return False
    expected = _task_three_directory_identity(
        path,
        allow_missing=allow_missing,
    )
    return expected is not None and dict(value) == expected


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


def _paths_are_equal(first: object, second: object) -> bool:
    if not isinstance(first, str) or not first:
        return False
    try:
        return Path(first).resolve() == Path(str(second)).resolve()
    except (OSError, RuntimeError, ValueError):
        return False


def _native_task_three_failure_evidence(
    *,
    attempt_path: Path,
    scan_root: Path,
    matrix_path: Path,
    matrix_payload: Mapping[str, Any],
    case_payload: Mapping[str, Any],
    adaptive_spec: Mapping[str, Any],
    task_two_build_root: Path,
    expected_build_provenance_path: Path | None = None,
    expected_python_executable: Path | None = None,
    expected_python_site_packages: Path | None = None,
    expected_openvino_libraries: Path | None = None,
) -> dict[str, Any] | None:
    """Reopen one native Task 3 failure and normalize only hash-bound facts."""

    attempt = Path(attempt_path).resolve()
    scope = Path(scan_root).resolve()
    if scope != attempt and scope not in attempt.parents:
        raise ValueError("native Task 3 attempt escapes the caller-provided root")
    if (
        attempt.name != "attempt.json"
        or attempt.parent.name != "run"
        or re.fullmatch(r"attempt-(\d{3})", attempt.parents[1].name) is None
        or attempt.parents[3].name != "attempts"
    ):
        return None
    attempt_dir = attempt.parents[1]
    role = attempt.parents[2].name
    attempt_number = int(attempt_dir.name.removeprefix("attempt-"))
    campaign_root = attempt.parents[4].resolve()
    if scope != campaign_root and scope not in campaign_root.parents:
        return None

    receipt_path = attempt_dir / "sequence-receipt.json"
    spec_path = attempt_dir / "spec.json"
    identity_path = campaign_root / "campaign-identity.json"
    receipt = _load_json(receipt_path, "native Task 3 sequence receipt")
    role_spec = _load_json(spec_path, "native Task 3 role spec")
    campaign_identity = _load_json(
        identity_path, "native Task 3 campaign identity"
    )
    record = _load_json(attempt, "native Task 3 runtime attempt")

    receipt_spec = _resolved_child(
        campaign_root, receipt.get("spec_path"), "native Task 3 receipt spec"
    )
    receipt_record = _resolved_child(
        campaign_root,
        receipt.get("runtime_record_path"),
        "native Task 3 receipt runtime record",
    )
    if receipt_spec != spec_path.resolve() or receipt_record != attempt:
        raise ValueError("native Task 3 receipt path binding is invalid")
    if (
        _require_hash(
            receipt.get("spec_file_sha256"),
            "native Task 3 spec file hash",
        )
        != _sha256_file(spec_path)
        or _require_hash(
            receipt.get("runtime_record_sha256"),
            "native Task 3 runtime record hash",
        )
        != _sha256_file(attempt)
        or _require_hash(
            receipt.get("spec_sha256"),
            "native Task 3 canonical spec hash",
        )
        != _sha256_json(role_spec)
    ):
        raise ValueError("native Task 3 attempt hash validation failed")

    identity = campaign_identity.get("identity")
    identity_hash = campaign_identity.get("campaign_identity_sha256")
    if (
        set(campaign_identity)
        != {"schema", "identity", "campaign_identity_sha256"}
        or campaign_identity.get("schema") != TASK_THREE_CAMPAIGN_SCHEMA
        or not isinstance(identity, Mapping)
        or set(identity)
        != {"config", "context", "matrix", "build", "model", "prompt", "runtime"}
        or _require_hash(identity_hash, "native Task 3 campaign identity hash")
        != _sha256_json(identity)
        or receipt.get("schema") != TASK_THREE_RECEIPT_SCHEMA
        or receipt.get("role") != role
        or receipt.get("attempt_number") != attempt_number
        or receipt.get("campaign_identity_sha256") != identity_hash
        or receipt.get("accepted") is not False
        or role_spec.get("schema") != TASK_THREE_SPEC_SCHEMA
        or role_spec.get("role") != role
        or role_spec.get("campaign_identity_sha256") != identity_hash
        or record.get("schema") != TASK_THREE_RUN_SCHEMA
        or record.get("role") != role
    ):
        return None

    test_id = role_spec.get("controlled_test_id")
    context = role_spec.get("context")
    if (
        test_id != case_payload.get("test_id")
        or context != adaptive_spec.get("context_tokens")
        or role_spec.get("expected_input_tokens") != context
        or role_spec.get("device") != adaptive_spec.get("device")
        or role_spec.get("properties") != adaptive_spec.get("properties")
        or role_spec.get("max_new_tokens") != adaptive_spec.get("max_new_tokens")
        or role_spec.get("ignore_eos") != adaptive_spec.get("ignore_eos")
        or role_spec.get("seed") != adaptive_spec.get("seed")
        or role_spec.get("apply_chat_template")
        != adaptive_spec.get("apply_chat_template")
        or not _paths_are_equal(
            role_spec.get("model_path"), adaptive_spec.get("model_path")
        )
    ):
        return None
    workload = adaptive_spec.get("workload")
    if (
        not isinstance(workload, Mapping)
        or role_spec.get("prompt") != workload.get("prompt")
        or workload.get("actual_input_tokens") != context
    ):
        return None

    build_identity = identity.get("build")
    prompt_identity = identity.get("prompt")
    runtime_identity = identity.get("runtime")
    task_two_build = Path(task_two_build_root).resolve()
    package = task_two_build / "openvino_genai"
    modules = sorted(package.glob("py_openvino_genai*.pyd"))
    runtime_dll = package / "openvino_genai.dll"
    if (
        not isinstance(build_identity, Mapping)
        or set(build_identity)
        != {
            "root",
            "provenance_path",
            "provenance_sha256",
            "python_module",
            "runtime_dll",
        }
        or not _paths_are_equal(build_identity.get("root"), task_two_build)
        or len(modules) != 1
        or not _file_identity_matches(
            build_identity.get("python_module"), expected_path=modules[0]
        )
        or not _file_identity_matches(
            build_identity.get("runtime_dll"), expected_path=runtime_dll
        )
    ):
        return None
    provenance_value = build_identity.get("provenance_path")
    if not isinstance(provenance_value, str) or not provenance_value:
        return None
    provenance_path = Path(provenance_value).resolve()
    if (
        expected_build_provenance_path is not None
        and provenance_path != Path(expected_build_provenance_path).resolve()
    ):
        return None
    if (
        not provenance_path.is_file()
        or build_identity.get("provenance_sha256")
        != _sha256_file(provenance_path)
        or _load_json(provenance_path, "native Task 3 build provenance").get(
            "status"
        )
        != "passed"
    ):
        return None

    prompt = role_spec.get("prompt")
    if not isinstance(prompt, str):
        return None
    if (
        not isinstance(prompt_identity, Mapping)
        or set(prompt_identity) != {"utf8_bytes", "sha256"}
        or prompt_identity.get("utf8_bytes") != len(prompt.encode("utf-8"))
        or prompt_identity.get("sha256")
        != hashlib.sha256(prompt.encode("utf-8")).hexdigest()
    ):
        return None

    repository = _ROOT.resolve()
    runtime_sources = repository / "scripts" / "testing" / "official_openvino"
    controller_source = repository / "scripts" / "testing" / "measure_official_openvino.py"
    if (
        not isinstance(runtime_identity, Mapping)
        or set(runtime_identity)
        != {
            "repository_root",
            "repository_runtime_sources",
            "controller_source",
            "python_executable",
            "python_openvino_package",
            "openvino_libraries",
        }
        or not _paths_are_equal(runtime_identity.get("repository_root"), repository)
        or not _directory_identity_matches(
            runtime_identity.get("repository_runtime_sources"),
            expected_path=runtime_sources,
        )
        or not _file_identity_matches(
            runtime_identity.get("controller_source"),
            expected_path=controller_source,
        )
        or not _file_identity_matches(
            runtime_identity.get("python_executable"),
            expected_path=expected_python_executable,
        )
    ):
        return None
    package_identity = runtime_identity.get("python_openvino_package")
    package_path = (
        Path(expected_python_site_packages).resolve() / "openvino"
        if expected_python_site_packages is not None
        else (
            Path(str(package_identity.get("path"))).resolve()
            if isinstance(package_identity, Mapping)
            else Path()
        )
    )
    libraries_identity = runtime_identity.get("openvino_libraries")
    libraries_path = (
        Path(expected_openvino_libraries).resolve()
        if expected_openvino_libraries is not None
        else (
            Path(str(libraries_identity.get("path"))).resolve()
            if isinstance(libraries_identity, Mapping)
            else Path()
        )
    )
    if not _directory_identity_matches(
        package_identity,
        expected_path=package_path,
        allow_missing=True,
    ) or not _directory_identity_matches(
        libraries_identity,
        expected_path=libraries_path,
    ):
        return None

    expected_config = {
        field: role_spec.get(field)
        for field in (
            "device",
            "max_new_tokens",
            "expected_input_tokens",
            "ignore_eos",
            "seed",
            "apply_chat_template",
            "properties",
        )
    }
    matrix_identity = identity.get("matrix")
    model_identity = identity.get("model")
    validated_artifact = (
        model_identity.get("validated_artifact")
        if isinstance(model_identity, Mapping)
        else None
    )
    expected_matrix = Path(matrix_path).resolve()
    manifest_path = Path(str(adaptive_spec.get("artifact_manifest_path"))).resolve()
    expected_precision = case_payload.get("weight_precision")
    if not isinstance(expected_precision, str):
        return None
    try:
        task_three_validated_artifact = validate_artifact_manifest(
            manifest_path,
            expected_precision=expected_precision,
        )
    except ValueError:
        return None
    if (
        identity.get("context") != context
        or identity.get("config") != expected_config
        or not isinstance(matrix_identity, Mapping)
        or set(matrix_identity)
        != {
            "path",
            "file_sha256",
            "schema_version",
            "source_identity",
            "build_identity",
            "case",
        }
        or not _paths_are_equal(matrix_identity.get("path"), expected_matrix)
        or matrix_identity.get("file_sha256") != _sha256_file(expected_matrix)
        or matrix_identity.get("schema_version")
        != matrix_payload.get("schema_version")
        or matrix_identity.get("source_identity")
        != matrix_payload.get("source_identity")
        or matrix_identity.get("build_identity")
        != matrix_payload.get("build_identity")
        or matrix_identity.get("case") != dict(case_payload)
        or not isinstance(model_identity, Mapping)
        or set(model_identity)
        != {
            "artifact_manifest_path",
            "artifact_manifest_sha256",
            "validated_artifact",
        }
        or not _paths_are_equal(
            model_identity.get("artifact_manifest_path"),
            adaptive_spec.get("artifact_manifest_path"),
        )
        or model_identity.get("artifact_manifest_sha256")
        != adaptive_spec.get("artifact_manifest_sha256")
        or model_identity.get("artifact_manifest_sha256")
        != _sha256_file(manifest_path)
        or not isinstance(validated_artifact, Mapping)
        or dict(validated_artifact) != task_three_validated_artifact
        or validated_artifact.get("artifact_id") != adaptive_spec.get("artifact_id")
        or not _paths_are_equal(
            validated_artifact.get("artifact_root"),
            adaptive_spec.get("model_path"),
        )
        or validated_artifact.get("precision") != expected_precision
    ):
        return None

    launch_bytes = record.get("launch_minimum_available_ram_bytes")
    emergency_bytes = record.get("emergency_minimum_available_ram_bytes")
    if (
        type(launch_bytes) is not int
        or launch_bytes != START_RESERVE_MIB * MIB
        or type(emergency_bytes) is not int
        or emergency_bytes != RUNTIME_FLOOR_MIB * MIB
    ):
        return None
    worker = record.get("worker")
    if worker is not None:
        if not isinstance(worker, Mapping):
            return None
        expected_worker = {
            "role": role,
            "controlled_test_id": test_id,
            "context": context,
            "expected_input_tokens": context,
            "device": role_spec.get("device"),
        }
        if any(worker.get(field) != value for field, value in expected_worker.items()):
            return None
        if not _paths_are_equal(
            worker.get("model_path"), role_spec.get("model_path")
        ):
            return None

    normalized = dict(record)
    native_case = matrix_identity["case"]
    normalized.update(
        {
            "controlled_test_id": test_id,
            "context_tokens": context,
            "stage": role,
            "artifact_id": validated_artifact["artifact_id"],
            "artifact_manifest_sha256": model_identity[
                "artifact_manifest_sha256"
            ],
            "model": native_case.get("model"),
            "device": native_case.get("device"),
            "execution_route": native_case.get("execution_route"),
            "launch_minimum_available_ram_mib": launch_bytes // MIB,
            "emergency_minimum_available_ram_mib": emergency_bytes // MIB,
        }
    )
    boundary_identity = {
        "artifact_id": normalized["artifact_id"],
        "artifact_manifest_sha256": normalized[
            "artifact_manifest_sha256"
        ],
        "model": normalized["model"],
        "device": normalized["device"],
        "execution_route": normalized["execution_route"],
        "context_tokens": context,
        "launch_reserve_mib": launch_bytes // MIB,
        "emergency_floor_mib": emergency_bytes // MIB,
    }
    return {
        "test_id": test_id,
        "context_tokens": context,
        "role": role,
        "record": normalized,
        "record_path": attempt,
        "record_sha256": _sha256_file(attempt),
        "receipt_path": receipt_path.resolve(),
        "receipt_sha256": _sha256_file(receipt_path),
        "campaign_identity_sha256": identity_hash,
        "boundary_identity": boundary_identity,
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
    inventory_hash = _require_hash(
        index.get("artifact_inventory_sha256"), "artifact inventory hash"
    )
    inventory_value = index.get("artifact_inventory_path")
    if not isinstance(inventory_value, str) or not inventory_value:
        raise ValueError("adaptive spec index artifact inventory path is missing")
    inventory = _require_file(Path(inventory_value), "artifact inventory")
    if _sha256_file(inventory) != inventory_hash:
        raise ValueError("artifact inventory hash drift")
    build_root_value = index.get("build_root_path")
    if not isinstance(build_root_value, str) or not build_root_value:
        raise ValueError("adaptive spec index build root path is missing")
    indexed_build_root = _require_directory(
        Path(build_root_value), "adaptive spec index build root"
    )
    indexed_build_hash = _require_hash(
        index.get("build_root_sha256"), "adaptive spec index build root hash"
    )
    if _directory_sha256(indexed_build_root) != indexed_build_hash:
        raise ValueError("adaptive spec index build root hash drift")
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
    if (
        build_root != Path(str(index["build_root_path"])).resolve()
        or _directory_sha256(build_root) != index["build_root_sha256"]
    ):
        raise ValueError("adaptive spec index build binding drift")
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
        "artifact_inventory_path": str(
            Path(str(index["artifact_inventory_path"])).resolve()
        ),
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
        native_identity_sha256 = raw.get("native_campaign_identity_sha256")
        if (
            not isinstance(native_identity_sha256, str)
            or _SHA256.fullmatch(native_identity_sha256) is None
        ):
            raise ValueError(
                "reference boundary native campaign identity is not an "
                "equivalent boundary"
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
        "campaign_halt": None,
        "safety_probes": [],
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


def _task_three_cleanup_is_proven(record: Mapping[str, Any]) -> bool:
    expected_fields = {
        "setup_ok",
        "query_ok",
        "terminate_job_called",
        "queried_active_process_count_after_cleanup",
        "survivor_pids_after_cleanup",
    }
    for field in ("workload_job", "sampler_job"):
        evidence = record.get(field)
        if (
            not isinstance(evidence, Mapping)
            or set(evidence) != expected_fields
            or evidence.get("setup_ok") is not True
            or evidence.get("query_ok") is not True
            or type(evidence.get("terminate_job_called")) is not bool
            or evidence.get("queried_active_process_count_after_cleanup") != 0
            or evidence.get("survivor_pids_after_cleanup") != []
        ):
            return False
    return True


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
        and _task_three_cleanup_is_proven(record)
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
        if step.get("boundary_source") == "explicit-reference-index":
            continue
        attempts = step.get("attempts", [])
        if not isinstance(attempts, list):
            raise ValueError("adaptive controller attempt chain is invalid")
        for attempt in attempts:
            if not isinstance(attempt, Mapping) or type(
                attempt.get("residual_owned_process_count")
            ) is not int:
                raise RuntimeError("owned survivor history proof is missing")
            if attempt["residual_owned_process_count"] != 0:
                raise RuntimeError("campaign Job Object reports owned survivors")


def _set_campaign_halt(
    state: dict[str, Any],
    *,
    reason: str,
    detail: Mapping[str, Any],
) -> None:
    if state.get("campaign_halt") is None:
        state["campaign_halt"] = {
            "reason": reason,
            "detail": dict(detail),
        }


def _record_job_query_receipt(
    *,
    config: AdaptiveCampaignConfig,
    state: dict[str, Any],
    stage: str,
    query_ok: bool,
    active_pids: list[int] | None,
    error: str | None,
) -> bool:
    """Serialize one already-completed campaign Job query into state."""

    root = Path(config.campaign_root).resolve()
    normalized_active_pids = (
        sorted(active_pids)
        if isinstance(active_pids, list)
        and all(type(pid) is int and pid > 0 for pid in active_pids)
        else None
    )
    missing = (
        type(query_ok) is not bool
        or (query_ok is True and normalized_active_pids is None)
    )
    if missing:
        failure_reason = "owned-survivor-proof-missing"
    elif query_ok is not True:
        failure_reason = "owned-survivor-proof-query-failed"
    elif normalized_active_pids:
        failure_reason = "owned-survivor-proof-survivors-present"
    else:
        failure_reason = None

    probes = state.setdefault("safety_probes", [])
    if not isinstance(probes, list):
        raise ValueError("adaptive campaign safety probe chain is invalid")
    number = len(probes) + 1
    relative = Path("safety-probes") / f"probe-{number:03d}.json"
    destination = root / relative
    if destination.exists():
        raise RuntimeError("safety probe receipt is immutable")
    receipt = {
        "schema": JOB_PROBE_SCHEMA,
        "probe_number": number,
        "stage": stage,
        "job_name": _campaign_job_name(root),
        "query_ok": query_ok if type(query_ok) is bool else False,
        "active_pids": normalized_active_pids,
        "error": error if isinstance(error, str) else None,
    }
    destination.parent.mkdir(parents=True, exist_ok=True)
    atomic_write_json(destination, receipt)
    entry = {
        "probe_number": number,
        "stage": stage,
        "query_ok": receipt["query_ok"],
        "active_pids": normalized_active_pids,
        "receipt_path": relative.as_posix(),
        "receipt_sha256": _sha256_file(destination),
    }
    probes.append(entry)
    if failure_reason is not None:
        _set_campaign_halt(
            state,
            reason=failure_reason,
            detail={"stage": stage, "probe_receipt": entry},
        )
    save_state_atomically(root / "adaptive-campaign-state.json", state)
    return failure_reason is None


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


def _record_artifact_preparation_terminal(
    *,
    config: AdaptiveCampaignConfig,
    state: dict[str, Any],
    terminal: Mapping[str, Any],
) -> dict[str, Any]:
    test_id = terminal.get("test_id")
    if test_id != "OV-13" or terminal.get("stage") != "artifact-preparation":
        raise ValueError("adaptive artifact terminal identity is invalid")
    context = CONTEXTS[0]
    source = _require_file(
        Path(str(terminal.get("receipt_path"))),
        "artifact preparation terminal source receipt",
    )
    source_hash = _require_hash(
        terminal.get("receipt_sha256"),
        "artifact preparation terminal source receipt hash",
    )
    if _sha256_file(source) != source_hash:
        raise ValueError("adaptive artifact terminal source receipt hash drift")

    root = Path(config.campaign_root).resolve()
    relative = (
        Path("controller-receipts")
        / test_id
        / "artifact-preparation-terminal.json"
    )
    destination = root / relative
    receipt = {
        "schema": ARTIFACT_TERMINAL_RECEIPT_SCHEMA,
        "test_id": test_id,
        "context_tokens": context,
        "status": "artifact-preparation-terminal",
        "stage": "artifact-preparation",
        "source_receipt_path": str(source),
        "source_receipt_sha256": source_hash,
        "matrix_sha256": state["bindings"]["matrix_sha256"],
        "spec_index_sha256": state["bindings"]["spec_index_sha256"],
    }
    if destination.exists():
        if _load_json(destination, "artifact terminal controller receipt") != receipt:
            raise ValueError("artifact terminal controller receipt is non-identical")
    else:
        destination.parent.mkdir(parents=True, exist_ok=True)
        atomic_write_json(destination, receipt)
    step = {
        "test_id": test_id,
        "context_tokens": context,
        "runtime_status": "artifact-preparation-terminal",
        "quality_status": "not-run",
        "attempt_count": 0,
        "failure_fingerprint": None,
        "evidence_path": str(source),
        "evidence_sha256": source_hash,
        "attempts": [],
        "terminal_receipt_path": relative.as_posix(),
        "terminal_receipt_sha256": _sha256_file(destination),
    }
    state["steps"][f"{test_id}:{context}"] = step
    return state


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


def _native_runtime_failures_for_step(
    *,
    config: AdaptiveCampaignConfig,
    test_id: str,
    context: int,
    adaptive_spec: Mapping[str, Any],
) -> list[dict[str, Any]]:
    runtime_root = (
        Path(config.campaign_root).resolve()
        / "runtime"
        / test_id
        / f"context-{context}"
    )
    attempts_root = runtime_root / "attempts"
    if not attempts_root.exists():
        return []
    if not attempts_root.is_dir():
        raise ValueError("native Task 3 attempts root is invalid")
    matrix_path = Path(config.matrix_path).resolve()
    matrix_payload = _load_json(matrix_path, "adaptive comparison matrix")
    raw_cases = matrix_payload.get("cases")
    if not isinstance(raw_cases, list):
        raise ValueError("adaptive comparison matrix cases are invalid")
    matches = [
        case
        for case in raw_cases
        if isinstance(case, Mapping) and case.get("test_id") == test_id
    ]
    if len(matches) != 1:
        raise ValueError("adaptive comparison matrix case identity is invalid")

    role_order = {
        role: index
        for index, role in enumerate(
            ("pilot", "warmup", "sample-1", "sample-2", "sample-3")
        )
    }
    discovered: list[tuple[int, int, Path]] = []
    for receipt_path in attempts_root.glob("*/attempt-*/sequence-receipt.json"):
        receipt = _load_json(receipt_path, "native Task 3 sequence receipt")
        if receipt.get("accepted") is True:
            continue
        if receipt.get("accepted") is not False:
            raise ValueError("native Task 3 failed receipt status is invalid")
        role = receipt_path.parents[1].name
        match = re.fullmatch(r"attempt-(\d{3})", receipt_path.parent.name)
        relative_record = receipt.get("runtime_record_path")
        if role not in role_order or match is None or not isinstance(
            relative_record, str
        ):
            raise ValueError("native Task 3 failed receipt identity is invalid")
        record_path = _resolved_child(
            runtime_root,
            relative_record,
            "native Task 3 failed runtime record",
        )
        discovered.append((role_order[role], int(match.group(1)), record_path))

    failures: list[dict[str, Any]] = []
    seen_paths: set[Path] = set()
    for _, _, record_path in sorted(discovered):
        if record_path in seen_paths:
            raise ValueError("native Task 3 failed receipt is duplicated")
        seen_paths.add(record_path)
        native = _native_task_three_failure_evidence(
            attempt_path=record_path,
            scan_root=runtime_root,
            matrix_path=matrix_path,
            matrix_payload=matrix_payload,
            case_payload=matches[0],
            adaptive_spec=adaptive_spec,
            task_two_build_root=config.build_root,
            expected_build_provenance_path=config.build_provenance_path,
            expected_python_executable=config.python_executable,
            expected_python_site_packages=config.python_site_packages,
            expected_openvino_libraries=config.openvino_libraries,
        )
        if native is None:
            raise ValueError("native Task 3 failed attempt lacks exact identity proof")
        failures.append(native)
    return failures


def _reconcile_native_runtime_failures(
    *,
    config: AdaptiveCampaignConfig,
    state: dict[str, Any],
    test_id: str,
    context: int,
    adaptive_spec: Mapping[str, Any],
    controller_attempts: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    try:
        native_failures = _native_runtime_failures_for_step(
            config=config,
            test_id=test_id,
            context=context,
            adaptive_spec=adaptive_spec,
        )
    except ValueError as error:
        _set_campaign_halt(
            state,
            reason="native-runtime-failure-proof-invalid",
            detail={
                "test_id": test_id,
                "context_tokens": context,
                "error": str(error),
            },
        )
        save_state_atomically(
            Path(config.campaign_root).resolve()
            / "adaptive-campaign-state.json",
            state,
        )
        return controller_attempts

    represented: dict[tuple[str, str], dict[str, Any]] = {
        (
            str(Path(attempt["evidence_path"]).resolve()),
            str(attempt["evidence_sha256"]),
        ): attempt
        for attempt in controller_attempts
        if attempt.get("status") in {"retryable-failure", "safety-boundary"}
    }
    missing: list[tuple[dict[str, Any], StepOutcome]] = []
    for native in native_failures:
        failure = MeasurementFailureRecord(
            native["role"],
            native["record_path"],
            native["record"],
            native["campaign_identity_sha256"],
        )
        outcome = classify_runtime_failure(failure)
        identity = (str(native["record_path"]), native["record_sha256"])
        existing = represented.get(identity)
        residual_value = native["record"].get("residual_owned_process_count")
        residual = residual_value if type(residual_value) is int else -1
        if existing is not None:
            if (
                existing.get("status") != outcome.runtime_status
                or existing.get("failure_fingerprint")
                != outcome.failure_fingerprint
                or existing.get("residual_owned_process_count") != residual
            ):
                _set_campaign_halt(
                    state,
                    reason="native-runtime-controller-receipt-mismatch",
                    detail={
                        "test_id": test_id,
                        "context_tokens": context,
                        "evidence_path": str(native["record_path"]),
                    },
                )
                save_state_atomically(
                    Path(config.campaign_root).resolve()
                    / "adaptive-campaign-state.json",
                    state,
                )
                return controller_attempts
            continue
        missing.append((native, outcome))

    capacity = MAX_GUARDED_ATTEMPTS - len(controller_attempts)
    for native, outcome in missing[: max(capacity, 0)]:
        residual_value = native["record"].get("residual_owned_process_count")
        residual = residual_value if type(residual_value) is int else -1
        controller_attempts.append(
            _write_controller_receipt(
                config=config,
                test_id=test_id,
                context=context,
                attempt_number=len(controller_attempts) + 1,
                status=outcome.runtime_status,
                evidence_path=native["record_path"],
                evidence_sha256=native["record_sha256"],
                failure_fingerprint=outcome.failure_fingerprint,
                residual_owned_process_count=residual,
            )
        )
        if outcome.runtime_status == "safety-boundary":
            _set_campaign_halt(
                state,
                reason="unsafe-reconciled-runtime-failure",
                detail={
                    "test_id": test_id,
                    "context_tokens": context,
                    "evidence_path": str(native["record_path"]),
                    "evidence_sha256": native["record_sha256"],
                },
            )

    if len(missing) > max(capacity, 0):
        _set_campaign_halt(
            state,
            reason="native-runtime-attempt-limit-exceeded",
            detail={
                "test_id": test_id,
                "context_tokens": context,
                "native_failed_attempt_count": len(native_failures),
                "maximum_guarded_attempts": MAX_GUARDED_ATTEMPTS,
            },
        )
    if state.get("campaign_halt") is not None:
        save_state_atomically(
            Path(config.campaign_root).resolve()
            / "adaptive-campaign-state.json",
            state,
        )
    return controller_attempts


def _validate_state_receipts(
    config: AdaptiveCampaignConfig,
    state: Mapping[str, Any],
) -> None:
    root = Path(config.campaign_root).resolve()
    for expected_number, entry in enumerate(state.get("safety_probes", []), 1):
        if entry.get("probe_number") != expected_number:
            raise ValueError("safety probe receipt numbering is not monotonic")
        receipt_path = _resolved_child(
            root, entry.get("receipt_path"), "safety probe receipt"
        )
        if _sha256_file(receipt_path) != entry.get("receipt_sha256"):
            raise ValueError("safety probe receipt hash drift")
        receipt = _load_json(receipt_path, "safety probe receipt")
        if (
            receipt.get("schema") != JOB_PROBE_SCHEMA
            or receipt.get("probe_number") != expected_number
            or receipt.get("stage") != entry.get("stage")
            or receipt.get("query_ok") != entry.get("query_ok")
            or receipt.get("active_pids") != entry.get("active_pids")
        ):
            raise ValueError("safety probe receipt identity drift")
    for key, step in state["steps"].items():
        if step.get("runtime_status") == "artifact-preparation-terminal":
            receipt_path = _resolved_child(
                root,
                step.get("terminal_receipt_path"),
                "artifact terminal controller receipt",
            )
            if _sha256_file(receipt_path) != step.get("terminal_receipt_sha256"):
                raise ValueError("artifact terminal controller receipt hash drift")
            receipt = _load_json(
                receipt_path, "artifact terminal controller receipt"
            )
            evidence = _require_file(
                Path(str(step.get("evidence_path"))),
                "artifact terminal source receipt",
            )
            evidence_hash = _require_hash(
                step.get("evidence_sha256"),
                "artifact terminal source receipt hash",
            )
            test_id, context_text = key.split(":", 1)
            if (
                _sha256_file(evidence) != evidence_hash
                or step.get("attempt_count") != 0
                or step.get("attempts") != []
                or receipt.get("schema") != ARTIFACT_TERMINAL_RECEIPT_SCHEMA
                or receipt.get("test_id") != test_id
                or receipt.get("context_tokens") != int(context_text)
                or receipt.get("status") != "artifact-preparation-terminal"
                or receipt.get("stage") != "artifact-preparation"
                or receipt.get("source_receipt_path") != str(evidence)
                or receipt.get("source_receipt_sha256") != evidence_hash
                or receipt.get("matrix_sha256")
                != state["bindings"]["matrix_sha256"]
                or receipt.get("spec_index_sha256")
                != state["bindings"]["spec_index_sha256"]
            ):
                raise ValueError("artifact terminal controller receipt identity drift")
            continue
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
    campaign_job = KillOnCloseJob(_campaign_job_name(campaign_root))
    try:
        def require_owned_job_open() -> KillOnCloseJob:
            if type(campaign_job) is not KillOnCloseJob:
                raise RuntimeError("real campaign Job Object is required")
            handle = getattr(campaign_job, "_handle", None)
            if type(handle) is not int or handle <= 0:
                raise RuntimeError("real campaign Job Object handle is not open")
            return campaign_job

        def record_live_survivors(
            state: dict[str, Any],
            stage: str,
        ) -> bool:
            try:
                owned_job = require_owned_job_open()
                active_pids = KillOnCloseJob.active_pids(owned_job)
                query_ok = True
                query_error = None
            except (OSError, RuntimeError) as error:
                active_pids = None
                query_ok = False
                query_error = f"{type(error).__name__}: {error}"
            return _record_job_query_receipt(
                config=config,
                state=state,
                stage=stage,
                query_ok=query_ok,
                active_pids=active_pids,
                error=query_error,
            )

        def execute_runtime_step(
            state: dict[str, Any],
            *,
            test_id: str,
            context: int,
            spec_path: Path,
            spec: Mapping[str, Any],
        ) -> dict[str, Any]:
            attempts = _controller_attempts(config, test_id, context)
            attempts = _reconcile_native_runtime_failures(
                config=config,
                state=state,
                test_id=test_id,
                context=context,
                adaptive_spec=spec,
                controller_attempts=attempts,
            )
            failure_fingerprints = [
                attempt["failure_fingerprint"]
                for attempt in attempts
                if attempt["status"] == "retryable-failure"
            ]
            runtime_root = (
                campaign_root / "runtime" / test_id / f"context-{context}"
            )
            if state.get("campaign_halt") is not None and not attempts:
                return state
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
                if state.get("campaign_halt") is not None:
                    status = "safety-boundary"
                elif last["status"] == "safety-boundary":
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

            for attempt_number in range(
                len(attempts) + 1,
                MAX_GUARDED_ATTEMPTS + 1,
            ):
                try:
                    _require_zero_recorded_survivors(state)
                    if not record_live_survivors(
                        state,
                        f"before-launch:{test_id}:{context}:{attempt_number}",
                    ):
                        return state
                    _require_start_reserve(available_ram)
                    require_owned_job_open()
                    arguments = {
                        "spec_path": spec_path,
                        "campaign_root": runtime_root,
                        "matrix_path": Path(config.matrix_path).resolve(),
                        "artifact_manifest_path": Path(
                            str(spec["artifact_manifest_path"])
                        ).resolve(),
                        "build_provenance_path": Path(
                            config.build_provenance_path
                        ).resolve(),
                        "build_root": Path(config.build_root).resolve(),
                        "repo_root": _ROOT,
                        "python_executable": Path(
                            config.python_executable
                        ).resolve(),
                        "python_site_packages": Path(
                            config.python_site_packages
                        ).resolve(),
                        "openvino_libraries": Path(
                            config.openvino_libraries
                        ).resolve(),
                        "sampler_script": Path(
                            config.sampler_script
                        ).resolve(),
                        "launch_minimum_available_ram_mib": (
                            START_RESERVE_MIB
                        ),
                        "emergency_minimum_available_ram_mib": (
                            RUNTIME_FLOOR_MIB
                        ),
                        "campaign_job": campaign_job,
                    }
                except RuntimeError as error:
                    _set_campaign_halt(
                        state,
                        reason="unsafe-ram-or-survivor-admission",
                        detail={
                            "stage": "before-launch",
                            "error": str(error),
                        },
                    )
                    save_state_atomically(
                        campaign_root / "adaptive-campaign-state.json",
                        state,
                    )
                    return state
                try:
                    result = dict(run_runtime(**arguments))
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
                        for item in load_adaptive_comparison_matrix(
                            config.matrix_path
                        )
                        if item.test_id == test_id
                    )
                    raw_record.setdefault("model", case.model)
                    raw_record.setdefault("device", case.device)
                    raw_record.setdefault(
                        "execution_route", case.execution_route
                    )
                    raw_record.setdefault(
                        "launch_minimum_available_ram_mib",
                        START_RESERVE_MIB,
                    )
                    raw_record.setdefault(
                        "emergency_minimum_available_ram_mib",
                        RUNTIME_FLOOR_MIB,
                    )
                    failure = MeasurementFailureRecord(
                        role=raw_failure.role,
                        record_path=raw_failure.record_path,
                        record=raw_record,
                        fingerprint=raw_failure.fingerprint,
                    )
                    outcome = classify_runtime_failure(failure)
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
                            raw_record.get("residual_owned_process_count", -1)
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
                            if failure_fingerprints[0]
                            == failure_fingerprints[1]
                            else "inconclusive-safety-boundary"
                        )
                        break
                    try:
                        _require_zero_recorded_survivors(
                            {"steps": {"current": {"attempts": attempts}}}
                        )
                        if not record_live_survivors(
                            state,
                            (
                                f"before-retry:{test_id}:{context}:"
                                f"{attempt_number + 1}"
                            ),
                        ):
                            status = "safety-boundary"
                            break
                        _require_start_reserve(available_ram)
                        require_owned_job_open()
                    except RuntimeError as error:
                        _set_campaign_halt(
                            state,
                            reason="unsafe-retry-admission",
                            detail={
                                "stage": "before-retry",
                                "error": str(error),
                            },
                        )
                        status = "safety-boundary"
                        break
                    continue
                evidence_path = runtime_root / "attempt-sequence.json"
                if not evidence_path.is_file() or _load_json(
                    evidence_path,
                    "measurement sequence evidence",
                ) != result:
                    raise RuntimeError(
                        "runtime sequence result is not immutable evidence"
                    )
                if (
                    result.get("accepted_sample_count") != 3
                    or result.get("cleanup_process_count") != 0
                ):
                    raise RuntimeError(
                        "runtime sequence did not pass formal validation"
                    )
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
            else:  # pragma: no cover - fixed loop returns or breaks
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
            elif status == "safety-boundary":
                _set_campaign_halt(
                    state,
                    reason="unsafe-runtime-failure",
                    detail={
                        "test_id": test_id,
                        "context_tokens": context,
                        "evidence_path": str(last_evidence_path.resolve()),
                        "evidence_sha256": last_evidence_sha256,
                    },
                )
            return state

        def execute_quality_step(
            state: dict[str, Any],
            *,
            test_id: str,
            context: int,
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
                "campaign_root": str(campaign_root),
            }
            result = dict(run_quality(quality_input, resume=False))
            status = result.get("status")
            if status not in {"passed", "quality-blocked"}:
                raise RuntimeError("quality callback returned an invalid status")
            step["quality_status"] = status
            step["quality_result"] = result
            return state

        with CampaignLock(campaign_root):
            state = load_or_create_state(config)
            if state.get("campaign_halt") is not None:
                return state
            _cases, index, specs, _bindings = _validate_config(config)
            terminals = {
                terminal["test_id"]: terminal
                for terminal in index["terminals"]
            }
            state_path = campaign_root / "adaptive-campaign-state.json"
            for test_id, context in build_ladder(config.matrix_path):
                if context > config.max_context or not step_is_eligible(
                    state, test_id, context
                ):
                    continue
                spec_entry = specs.get((test_id, context))
                if spec_entry is None:
                    terminal = terminals.get(test_id)
                    if terminal is not None and context == CONTEXTS[0]:
                        state = _record_artifact_preparation_terminal(
                            config=config,
                            state=state,
                            terminal=terminal,
                        )
                        save_state_atomically(state_path, state)
                        if publish_checkpoint is not None:
                            publish_checkpoint(state_path, False)
                    continue
                if not record_live_survivors(
                    state,
                    f"before-next-step:{test_id}:{context}",
                ):
                    if publish_checkpoint is not None:
                        publish_checkpoint(state_path, False)
                    return state
                state = execute_runtime_step(
                    state,
                    test_id=test_id,
                    context=context,
                    spec_path=spec_entry[0],
                    spec=spec_entry[1],
                )
                if state.get("campaign_halt") is not None and (
                    f"{test_id}:{context}" not in state["steps"]
                ):
                    if publish_checkpoint is not None:
                        publish_checkpoint(state_path, False)
                    return state
                if (
                    state["steps"][f"{test_id}:{context}"]["runtime_status"]
                    == "passed"
                ):
                    state = execute_quality_step(
                        state,
                        test_id=test_id,
                        context=context,
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
    finally:
        campaign_job.close()


def preflight_adaptive_campaign(
    config: AdaptiveCampaignConfig,
    *,
    available_ram: Callable[[], int | None] = available_ram_bytes,
) -> dict[str, Any]:
    """Validate all bindings and safety gates without creating an attempt."""

    root = Path(config.campaign_root).resolve()
    campaign_job = KillOnCloseJob(_campaign_job_name(root))
    try:
        def require_owned_job_open() -> KillOnCloseJob:
            if type(campaign_job) is not KillOnCloseJob:
                raise RuntimeError("real campaign Job Object is required")
            handle = getattr(campaign_job, "_handle", None)
            if type(handle) is not int or handle <= 0:
                raise RuntimeError("real campaign Job Object handle is not open")
            return campaign_job

        def record_live_survivors(state: dict[str, Any]) -> bool:
            try:
                owned_job = require_owned_job_open()
                active_pids = KillOnCloseJob.active_pids(owned_job)
                query_ok = True
                query_error = None
            except (OSError, RuntimeError) as error:
                active_pids = None
                query_ok = False
                query_error = f"{type(error).__name__}: {error}"
            return _record_job_query_receipt(
                config=config,
                state=state,
                stage="preflight",
                query_ok=query_ok,
                active_pids=active_pids,
                error=query_error,
            )

        with CampaignLock(root):
            state = load_or_create_state(config)
            if state.get("campaign_halt") is not None:
                raise RuntimeError(
                    f"adaptive campaign is halted: {state['campaign_halt']['reason']}"
                )
            try:
                _require_zero_recorded_survivors(state)
                if not record_live_survivors(state):
                    raise RuntimeError(
                        "adaptive campaign is halted: "
                        f"{state['campaign_halt']['reason']}"
                    )
                _require_start_reserve(available_ram)
            except RuntimeError as error:
                _set_campaign_halt(
                    state,
                    reason="unsafe-preflight-admission",
                    detail={"error": str(error)},
                )
                save_state_atomically(
                    root / "adaptive-campaign-state.json", state
                )
                raise
            return state
    finally:
        campaign_job.close()


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
        "campaign_halt": validated.get("campaign_halt"),
        "next_eligible_step": list(candidates[0]) if candidates else None,
    }


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
    cases, index, specs = _load_matrix_and_specs(matrix, root)
    by_case = {case.test_id: case for case in cases}
    matrix_payload = _load_json(matrix, "adaptive comparison matrix")
    raw_cases = matrix_payload.get("cases")
    if not isinstance(raw_cases, list) or any(
        not isinstance(case, Mapping) for case in raw_cases
    ):
        raise ValueError("adaptive comparison matrix cases are invalid")
    case_payloads = {
        str(case["test_id"]): case
        for case in raw_cases
        if case.get("test_id") in by_case
    }
    groups: defaultdict[
        tuple[str, int, str, str], list[dict[str, Any]]
    ] = defaultdict(list)
    for discovered in sorted(historical.rglob("attempt.json")):
        attempt = discovered.resolve()
        if historical != attempt and historical not in attempt.parents:
            raise ValueError("historical attempt escapes caller-provided root")
        if (
            attempt.parent.name != "run"
            or re.fullmatch(r"attempt-(\d{3})", attempt.parents[1].name)
            is None
            or attempt.parents[3].name != "attempts"
        ):
            continue
        worker_spec_path = attempt.parents[1] / "spec.json"
        worker_spec = _load_json(worker_spec_path, "historical Task 3 role spec")
        test_id = worker_spec.get("controlled_test_id")
        context = worker_spec.get("context")
        key = (test_id, context)
        if (
            key not in specs
            or test_id not in by_case
            or test_id not in case_payloads
        ):
            continue
        native = _native_task_three_failure_evidence(
            attempt_path=attempt,
            scan_root=historical,
            matrix_path=matrix,
            matrix_payload=matrix_payload,
            case_payload=case_payloads[test_id],
            adaptive_spec=specs[key][1],
            task_two_build_root=Path(str(index["build_root_path"])),
        )
        if native is None:
            continue
        record = native["record"]
        if not _retryable_failure(record):
            continue
        failure = MeasurementFailureRecord(
            native["role"],
            native["record_path"],
            record,
            native["campaign_identity_sha256"],
        )
        _fingerprint_identity, fingerprint = _failure_identity(failure)
        identity = native["boundary_identity"]
        expected = _spec_identity(by_case[test_id], context)
        if identity != expected:
            continue
        groups[
            (
                test_id,
                context,
                fingerprint,
                native["campaign_identity_sha256"],
            )
        ].append(
            {
                "path": str(attempt),
                "sha256": native["record_sha256"],
                "failure_fingerprint": fingerprint,
            }
        )
    boundaries = []
    for (
        test_id,
        context,
        fingerprint,
        native_identity_sha256,
    ), attempts in sorted(groups.items()):
        if len(attempts) < 2:
            continue
        boundaries.append(
            {
                "test_id": test_id,
                "context_tokens": context,
                "failure_fingerprint": fingerprint,
                "native_campaign_identity_sha256": native_identity_sha256,
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
