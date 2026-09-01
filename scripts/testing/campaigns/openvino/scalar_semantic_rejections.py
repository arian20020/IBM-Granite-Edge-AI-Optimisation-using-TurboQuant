"""Fail-closed evidence for post-activation scalar cache rejection rows."""

from __future__ import annotations

import hashlib
import json
import math
import os
import tempfile
from collections.abc import Mapping
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

from .matrix import SEMANTIC_SCALAR_REJECTION_IDS, execution_contract, load_matrix
from .runtime_measurement import RESULT_MARKER, validate_activation_telemetry


SCHEMA = "official-openvino-wb04-scalar-semantic-rejection-evidence/v1"
_WORKER_SCHEMA = "official-openvino-wb04-worker/v1"
_SPEC_SCHEMA = "official-openvino-wb04-worker-spec/v1"
_ATTEMPT_SCHEMA = "official-openvino-wb04-governed-run/v1"
_CONTROLLER_PATH = Path(__file__).resolve()
_REPO_ROOT = _CONTROLLER_PATH.parents[4]
_HISTORICAL_CONTROLLER_PATH = (
    "scripts/testing/official_openvino/scalar_semantic_rejections.py"
)
_HISTORICAL_CONTROLLER_SHA256 = (
    "474284d7e31ff54813c661654ecfa1d999ae731f6ecf018a27856b4abbd167bd"
)
_PYTHON_CONFIG_PATH = (
    _REPO_ROOT / ".venv-official-openvino-turboquant-py313" / "pyvenv.cfg"
)
_PYTHON_CONFIG_ENV = "OPENVINO_WB04_PYTHON_CONFIG"
_FORMAL_METRICS = frozenset(
    {
        "load_ms", "ttft_ms", "prompt_tps", "tpot_ms", "decode_tps",
        "generation_duration_ms", "peak_working_set_mb", "peak_private_mb",
        "available_ram_min_mb", "kv_mb", "gpu_memory_peak_mb",
        "cpu_percent", "gpu_percent",
    }
)
_PAIR_CONFIG = {
    "u8": {
        "precision": "u8",
        "diagnostic_id": "WB04-DIAGNOSTIC-U8-STANDARD",
        "test_ids": ("OV-04", "OV-TQ-01"),
    },
    "u4": {
        "precision": "u4",
        "diagnostic_id": "WB04-DIAGNOSTIC-U4-STANDARD",
        "test_ids": ("OV-05", "OV-TQ-02"),
    },
}
_EXPECTED_SCALAR_ROWS = {
    "OV-04": {
        "phase": "baseline",
        "precision": "u8",
        "description": "Granite 3B U8 scalar",
    },
    "OV-05": {
        "phase": "baseline",
        "precision": "u4",
        "description": "Granite 3B U4 scalar",
    },
    "OV-TQ-01": {
        "phase": "formal",
        "precision": "u8",
        "description": "U8 control",
    },
    "OV-TQ-02": {
        "phase": "formal",
        "precision": "u4",
        "description": "U4 control",
    },
}
_TOP_LEVEL_KEYS = frozenset(
    {
        "schema", "matrix_path", "matrix_sha256", "controller_path",
        "controller_sha256", "probe_count", "controlled_test_ids",
        "u8_spec_path", "u8_spec_sha256", "u8_attempt_path",
        "u8_attempt_sha256", "u4_spec_path", "u4_spec_sha256",
        "u4_attempt_path", "u4_attempt_sha256", "cleanup_process_count",
        "probes", "aggregate_sha256",
    }
)
_PROBE_KEYS = frozenset(
    {
        "probe_id", "controlled_test_id", "status", "expected_outcome",
        "matrix_case", "execution_contract", "rejection_kind",
        "requested_cache_precision", "reported_cache_precision",
        "observed_key_state_precision", "observed_value_state_precision",
        "actual_persistent_standard_bytes", "actual_persistent_payload_bytes",
        "actual_persistent_norm_bytes", "actual_persistent_metadata_bytes",
        "actual_persistent_total_bytes", "diagnostic_id", "spec_path",
        "spec_sha256", "attempt_path", "attempt_sha256", "stdout_sha256",
        "stderr_sha256", "output_sha256", "telemetry_sha256",
        "generation_launched", "numeric_generation_metrics_accepted",
        "metric_outcome", "cleanup_process_count", "probe_sha256",
    }
)
_MATRIX_CASE_KEYS = frozenset(
    {
        "test_id", "phase", "description", "model", "weight_precision",
        "k_algorithm", "v_algorithm", "k_precision", "v_precision", "device",
        "contexts", "guard", "quality_required", "required_metrics",
        "key_cache_precision", "value_cache_precision", "requested_device",
        "runtime_key_algorithm", "runtime_value_algorithm", "norm_correction",
        "attention_path", "execution_route", "expected_outcome",
        "suitable_host_required", "numeric_generation_metrics_expected",
    }
)
_CONTRACT_KEYS = frozenset(
    {
        "controlled_test_id", "execution_route", "expected_outcome",
        "runtime_key_algorithm", "runtime_value_algorithm", "norm_correction",
        "attention_path", "suitable_host_required",
        "requires_actual_cache_precision_proof",
        "numeric_generation_metrics_expected",
    }
)


@dataclass(frozen=True)
class _JsonArtifact:
    path: Path
    raw: bytes
    value: dict[str, Any]
    sha256: str


def _canonical_bytes(value: object) -> bytes:
    return (json.dumps(value, allow_nan=False, ensure_ascii=False,
                       separators=(",", ":"), sort_keys=True) + "\n").encode("utf-8")


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _sha256_json(value: object) -> str:
    return hashlib.sha256(json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")).hexdigest()


def _reject_constant(value: str) -> Any:
    raise ValueError(f"non-finite JSON number is forbidden: {value}")


def _reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            raise ValueError(f"duplicate JSON key: {key}")
        value[key] = item
    return value


def _strict_json_bytes(raw: bytes, label: str) -> dict[str, Any]:
    if raw.startswith(b"\xef\xbb\xbf"):
        raise ValueError(f"{label} must not contain a UTF-8 BOM")
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ValueError(f"{label} is not UTF-8") from error
    try:
        value = json.loads(text, object_pairs_hook=_reject_duplicate_keys,
                           parse_constant=_reject_constant)
    except (TypeError, json.JSONDecodeError, UnicodeDecodeError) as error:
        raise ValueError(f"{label} is not strict JSON") from error
    _reject_nonfinite(value, label)
    if type(value) is not dict:
        raise ValueError(f"{label} root must be an object")
    return value


def _reject_nonfinite(value: object, label: str) -> None:
    if isinstance(value, float) and not math.isfinite(value):
        raise ValueError(f"{label} contains a non-finite number")
    if isinstance(value, dict):
        for child in value.values():
            _reject_nonfinite(child, label)
    elif isinstance(value, list):
        for child in value:
            _reject_nonfinite(child, label)


def _read_bytes(path: Path, label: str) -> tuple[Path, bytes]:
    resolved = Path(path).resolve()
    if not resolved.is_file():
        raise ValueError(f"{label} file is missing: {resolved}")
    try:
        raw = resolved.read_bytes()
    except OSError as error:
        raise ValueError(f"{label} file could not be read: {resolved}") from error
    return resolved, raw


def _read_json(path: Path, label: str) -> _JsonArtifact:
    resolved, raw = _read_bytes(path, label)
    return _JsonArtifact(
        path=resolved,
        raw=raw,
        value=_strict_json_bytes(raw, label),
        sha256=_sha256_bytes(raw),
    )


def _configured_python_executable() -> Path:
    configured_path = os.environ.get(_PYTHON_CONFIG_ENV)
    config_source = (
        Path(configured_path) if configured_path is not None else _PYTHON_CONFIG_PATH
    )
    config_path, raw = _read_bytes(
        config_source,
        "configured Python identity",
    )
    try:
        text = raw.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ValueError(
            f"configured Python identity is not UTF-8: {config_path}"
        ) from error
    values: dict[str, list[str]] = {}
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line:
            continue
        key, separator, value = line.partition("=")
        if not separator:
            raise ValueError("configured Python identity contains an invalid line")
        values.setdefault(key.strip(), []).append(value.strip())
    if len(values.get("executable", [])) != 1:
        raise ValueError(
            "configured Python identity must declare one executable"
        )
    if len(values.get("version", [])) != 1 or not values["version"][0].startswith(
        "3.13."
    ):
        raise ValueError("configured Python identity is not Python 3.13")
    configured_input = Path(values["executable"][0])
    if not configured_input.is_absolute():
        raise ValueError("configured Python executable must be absolute")
    try:
        configured = configured_input.resolve(strict=True)
    except OSError as error:
        raise ValueError("configured Python executable does not exist") from error
    if (
        not configured.is_file()
        or configured.name.casefold() != "python.exe"
        or str(configured) != values["executable"][0]
    ):
        raise ValueError(
            "configured Python executable identity is not canonical"
        )
    return configured


def _load_matrix_snapshot(matrix: _JsonArtifact) -> list[Any]:
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=".wb04-scalar-matrix-",
        suffix=".json",
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(matrix.raw)
            handle.flush()
            os.fsync(handle.fileno())
        return load_matrix(temporary)
    finally:
        temporary.unlink(missing_ok=True)


def _display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(_REPO_ROOT).as_posix()
    except ValueError:
        return str(path.resolve())


def _repository_local_evidence_path(path: Path) -> Path:
    root = _REPO_ROOT.resolve()
    resolved = path.resolve()
    try:
        resolved.relative_to(root)
        return resolved
    except ValueError:
        parts = resolved.parts
        lowered = [part.casefold() for part in parts]
        matches = [
            index
            for index in range(len(parts) - 1)
            if lowered[index : index + 2] == ["experiments", "raw-results"]
        ]
        if len(matches) != 1:
            raise ValueError("historical evidence path is not repository-relative")
        candidate = (root / Path(*parts[matches[0] :])).resolve()
        try:
            candidate.relative_to(root)
        except ValueError as error:
            raise ValueError("historical evidence path escapes repository root") from error
        return candidate


def _require_exact_keys(value: object, expected: frozenset[str], label: str) -> dict[str, Any]:
    if type(value) is not dict:
        raise ValueError(f"{label} must be an object")
    if set(value) != expected:
        raise ValueError(f"{label} fields do not match the controlled schema")
    return value


def _require_bool(value: object, label: str, expected: bool | None = None) -> bool:
    if type(value) is not bool:
        raise ValueError(f"{label} must be a boolean")
    if expected is not None and value is not expected:
        raise ValueError(f"{label} has an invalid value")
    return value


def _require_int(value: object, label: str, *, positive: bool = False, zero: bool = False) -> int:
    if type(value) is not int:
        raise ValueError(f"{label} must be an integer")
    if positive and value <= 0:
        raise ValueError(f"{label} must be positive")
    if zero and value != 0:
        raise ValueError(f"{label} must be zero")
    if value < 0:
        raise ValueError(f"{label} must be non-negative")
    return value


def _require_text(value: object, label: str, expected: str | None = None) -> str:
    if type(value) is not str or not value:
        raise ValueError(f"{label} must be non-empty text")
    if expected is not None and value != expected:
        raise ValueError(f"{label} has an invalid value")
    return value


def _require_sha256(value: object, label: str) -> str:
    digest = _require_text(value, label)
    if len(digest) != 64 or any(
        character not in "0123456789abcdef" for character in digest
    ):
        raise ValueError(f"{label} must be a lowercase SHA-256")
    return digest


def _require_text_list(value: object, label: str) -> list[str]:
    if type(value) is not list or any(type(item) is not str for item in value):
        raise ValueError(f"{label} must be a list of text values")
    return value


def _require_integer_list(value: object, label: str) -> list[int]:
    if type(value) is not list or any(type(item) is not int for item in value):
        raise ValueError(f"{label} must be a list of integers")
    return value


def _validate_scalar_case(case: Any, test_id: str) -> None:
    expected = _EXPECTED_SCALAR_ROWS[test_id]
    precision = expected["precision"]
    scalar_fields = {
        "test_id": test_id,
        "phase": expected["phase"],
        "description": expected["description"],
        "model": "granite-3b",
        "weight_precision": precision,
        "k_algorithm": "scalar",
        "v_algorithm": "scalar",
        "k_precision": precision,
        "v_precision": precision,
        "device": "cpu",
        "contexts": (4096,),
        "guard": "none",
        "quality_required": True,
        "key_cache_precision": precision,
        "value_cache_precision": precision,
        "requested_device": "CPU",
        "runtime_key_algorithm": "STANDARD",
        "runtime_value_algorithm": "STANDARD",
        "norm_correction": False,
        "attention_path": "not-produced-by-expected-rejection",
        "execution_route": "expected-rejection",
        "expected_outcome": "expected-rejection",
        "suitable_host_required": False,
        "numeric_generation_metrics_expected": False,
    }
    for field, expected_value in scalar_fields.items():
        actual = getattr(case, field)
        if type(expected_value) is bool:
            matches = type(actual) is bool and actual is expected_value
        else:
            matches = actual == expected_value
        if not matches:
            raise ValueError(
                f"{test_id} {field} does not match scalar rejection semantics"
            )
    if case.required_metrics != _FORMAL_METRICS:
        raise ValueError(
            f"{test_id} required_metrics do not match scalar rejection semantics"
        )

    contract = execution_contract(case)
    contract_fields = {
        "controlled_test_id": test_id,
        "execution_route": "expected-rejection",
        "expected_outcome": "expected-rejection",
        "runtime_key_algorithm": "STANDARD",
        "runtime_value_algorithm": "STANDARD",
        "norm_correction": False,
        "attention_path": "not-produced-by-expected-rejection",
        "suitable_host_required": False,
        "requires_actual_cache_precision_proof": False,
        "numeric_generation_metrics_expected": False,
    }
    for field, expected_value in contract_fields.items():
        actual = getattr(contract, field)
        if type(expected_value) is bool:
            matches = type(actual) is bool and actual is expected_value
        else:
            matches = actual == expected_value
        if not matches:
            raise ValueError(
                f"{test_id} contract {field} does not match scalar rejection semantics"
            )


def _matrix_case_record(case: Any) -> dict[str, Any]:
    record = asdict(case)
    record["contexts"] = list(case.contexts)
    record["required_metrics"] = sorted(case.required_metrics)
    return record


def _parse_json_line(raw: bytes, label: str) -> dict[str, Any]:
    return _strict_json_bytes(raw, label)


def _parse_worker_streams(
    stdout_raw: bytes,
    stderr_raw: bytes,
) -> tuple[dict[str, Any], dict[str, Any]]:
    for raw, label in ((stdout_raw, "stdout"), (stderr_raw, "stderr")):
        if raw.startswith(b"\xef\xbb\xbf"):
            raise ValueError(f"{label} must not contain a UTF-8 BOM")
        try:
            raw.decode("utf-8")
        except UnicodeDecodeError as error:
            raise ValueError(f"{label} is not UTF-8") from error
    markers: list[dict[str, Any]] = []
    activations: list[dict[str, Any]] = []
    for raw, label in ((stdout_raw, "stdout"), (stderr_raw, "stderr")):
        for line in raw.splitlines():
            if line.startswith(RESULT_MARKER.encode("utf-8")):
                markers.append(_parse_json_line(line[len(RESULT_MARKER):], f"{label} worker marker"))
            elif line.lstrip().startswith(b"{"):
                candidate = _parse_json_line(line.strip(), f"{label} JSON line")
                if "status" in candidate:
                    activations.append(candidate)
    if len(markers) != 1:
        raise ValueError("worker output must contain exactly one result marker")
    if len(activations) != 1:
        raise ValueError("worker output must contain exactly one activation object")
    return markers[0], activations[0]


def _validate_job(value: object, label: str) -> None:
    if type(value) is not dict:
        raise ValueError(f"{label} proof must be an object")
    _require_bool(value.get("query_ok"), f"{label} query_ok", True)
    _require_bool(value.get("setup_ok"), f"{label} setup_ok", True)
    _require_int(value.get("queried_active_process_count_after_cleanup"), f"{label} survivors", zero=True)
    if value.get("survivor_pids_after_cleanup") != []:
        raise ValueError(f"{label} survivor PID proof is non-empty")


def _validate_spec(spec: dict[str, Any], precision: str, diagnostic_id: str) -> str:
    _require_text(spec.get("schema"), "spec schema", _SPEC_SCHEMA)
    _require_text(spec.get("role"), "spec role", "pilot")
    _require_text(spec.get("device"), "spec device", "CPU")
    _require_text(spec.get("controlled_test_id"), "spec diagnostic ID", diagnostic_id)
    model_path = _require_text(spec.get("model_path"), "spec model path")
    properties = spec.get("properties")
    if type(properties) is not dict:
        raise ValueError("spec properties must be an object")
    _require_text(properties.get("KEY_CACHE_PRECISION"), "spec key precision", precision)
    _require_text(properties.get("VALUE_CACHE_PRECISION"), "spec value precision", precision)
    if any(key.startswith("TURBOQUANT_") for key in properties):
        raise ValueError("spec must not request TurboQuant runtime properties")
    return model_path


def _validate_attempt(
    attempt_artifact: _JsonArtifact,
    spec_artifact: _JsonArtifact,
    precision: str,
    diagnostic_id: str,
    model_path: str,
    configured_executable: Path,
) -> dict[str, Any]:
    attempt = attempt_artifact.value
    attempt_path = attempt_artifact.path
    spec_path = spec_artifact.path
    if attempt_path.parent.name != "run":
        raise ValueError("attempt must be beneath a run directory")
    _require_text(attempt.get("schema"), "attempt schema", _ATTEMPT_SCHEMA)
    _require_bool(attempt.get("valid"), "attempt valid", True)
    _require_text(attempt.get("role"), "attempt role", "pilot")
    _require_int(attempt.get("exit_code"), "attempt exit code", zero=True)
    _require_int(attempt.get("sampler_exit_code"), "sampler exit code", zero=True)
    _require_bool(attempt.get("timed_out"), "attempt timeout", False)
    _require_bool(attempt.get("low_memory_stop"), "attempt low-memory stop", False)
    if attempt.get("emergency_actions") != [] or attempt.get("validation_errors") != []:
        raise ValueError("attempt contains an emergency action or validation error")
    _require_int(attempt.get("cleanup_process_count"), "attempt cleanup", zero=True)
    _validate_job(attempt.get("workload_job"), "workload Job Object")
    _validate_job(attempt.get("sampler_job"), "sampler Job Object")

    command = attempt.get("command")
    if (
        type(command) is not list
        or len(command) != 5
        or not all(type(item) is str and item for item in command)
    ):
        raise ValueError("attempt command is invalid")
    executable_input = Path(command[0])
    if not executable_input.is_absolute():
        raise ValueError("attempt command executable must be absolute")
    try:
        executable = executable_input.resolve(strict=True)
    except OSError as error:
        raise ValueError("attempt command executable does not exist") from error
    if (
        not executable.is_file()
        or executable.name.casefold() != "python.exe"
        or command[0] != str(executable)
        or executable != configured_executable
    ):
        raise ValueError(
            "attempt command executable is not the configured governed Python executable"
        )
    expected_prefix = [str(executable), "-m"]
    allowed_worker_modules = {
        "scripts.testing.campaigns.openvino.measurement_worker",
        # Frozen pre-migration receipts retain this exact historical identity.
        "scripts.testing.official_openvino.measurement_worker",
    }
    command_spec = Path(command[4])
    command_spec_matches = False
    if command_spec.is_absolute():
        command_spec_resolved = command_spec.resolve()
        command_spec_matches = command_spec_resolved == spec_path
        if not command_spec_matches:
            try:
                command_spec_matches = (
                    _repository_local_evidence_path(command_spec_resolved) == spec_path
                )
            except ValueError:
                command_spec_matches = False
    if (
        command[:2] != expected_prefix
        or command[2] not in allowed_worker_modules
        or command[3] != "--spec"
        or not command_spec_matches
    ):
        raise ValueError(
            "attempt command is not exactly bound to the measurement worker and supplied spec"
        )

    stdout = attempt_path.parent / "stdout.txt"
    stderr = attempt_path.parent / "stderr.txt"
    _, stdout_raw = _read_bytes(stdout, "attempt stdout")
    _, stderr_raw = _read_bytes(stderr, "attempt stderr")
    for field, raw in (
        ("stdout", stdout_raw),
        ("stderr", stderr_raw),
    ):
        expected_hash = _require_sha256(
            attempt.get(f"{field}_sha256"),
            f"attempt {field} SHA-256",
        )
        if _sha256_bytes(raw) != expected_hash:
            raise ValueError(f"attempt {field} bytes do not match SHA-256")
    streamed_worker, streamed_activation = _parse_worker_streams(
        stdout_raw,
        stderr_raw,
    )
    worker = attempt.get("worker")
    activation = attempt.get("activation")
    if type(worker) is not dict or type(activation) is not dict:
        raise ValueError("attempt worker and activation must be objects")
    if worker != streamed_worker or activation != streamed_activation:
        raise ValueError("attempt worker or activation is not bound to captured output")
    _require_text(worker.get("schema"), "worker schema", _WORKER_SCHEMA)
    _require_text(worker.get("controlled_test_id"), "worker diagnostic ID", diagnostic_id)
    _require_text(worker.get("role"), "worker role", "pilot")
    _require_text(worker.get("device"), "worker device", "CPU")
    _require_text(worker.get("model_path"), "worker model path", model_path)
    _require_bool(worker.get("output_valid"), "worker output validity", True)
    _require_text(worker.get("output"), "worker output")
    _require_int(worker.get("num_generated_tokens"), "worker generated tokens", positive=True)
    if _sha256_bytes(worker["output"].encode("utf-8")) != _require_sha256(attempt.get("output_sha256"), "attempt output SHA-256"):
        raise ValueError("attempt output SHA-256 does not match worker output")
    if _sha256_json(activation) != _require_sha256(attempt.get("telemetry_sha256"), "attempt telemetry SHA-256"):
        raise ValueError("attempt telemetry SHA-256 does not match activation")

    validate_activation_telemetry(activation)
    for field in ("requested_key_algorithm", "requested_value_algorithm", "activated_key_algorithm", "activated_value_algorithm"):
        _require_text(activation.get(field), f"activation {field}", "STANDARD")
    for field in ("requested_key_cache_precision", "requested_value_cache_precision", "activated_key_cache_precision", "activated_value_cache_precision"):
        _require_text(activation.get(field), f"activation {field}", precision)
    for field in ("observed_key_state_precision", "observed_value_state_precision"):
        _require_text(activation.get(field), f"activation {field}", "f32")
    for field in ("status", "operation_type", "runtime_layer_type", "transformed_model_hash"):
        _require_text(activation.get(field), f"activation {field}", "not_requested")
    _require_text(activation.get("attention_path"), "activation attention path", "stateful_sdpa_standard")
    _require_bool(activation.get("norm_correction"), "activation norm correction", False)
    _require_bool(activation.get("fallback"), "activation fallback", False)
    _require_text(activation.get("device"), "activation requested device", "CPU")
    _require_text(activation.get("actual_device"), "activation actual device", "CPU")
    for field in ("actual_bytes", "expected_bytes", "full_precision_equivalent_bytes", "actual_persistent_standard_bytes"):
        _require_int(activation.get(field), f"activation {field}", positive=True)
    if activation["actual_bytes"] != activation["expected_bytes"] or activation["actual_bytes"] != activation["full_precision_equivalent_bytes"] or activation["actual_bytes"] != activation["actual_persistent_standard_bytes"]:
        raise ValueError("activation standard byte allocation does not reconcile")
    for field in ("actual_persistent_payload_bytes", "actual_persistent_norm_bytes", "actual_persistent_metadata_bytes", "expected_persistent_payload_bytes", "expected_persistent_norm_bytes", "expected_persistent_metadata_bytes"):
        _require_int(activation.get(field), f"activation {field}", zero=True)
    return {"activation": activation, "stdout_sha256": attempt["stdout_sha256"], "stderr_sha256": attempt["stderr_sha256"], "output_sha256": attempt["output_sha256"], "telemetry_sha256": attempt["telemetry_sha256"]}


def _evidence(
    spec_path: Path,
    attempt_path: Path,
    pair: str,
    configured_executable: Path,
) -> dict[str, Any]:
    config = _PAIR_CONFIG[pair]
    spec = _read_json(spec_path, f"{pair} spec")
    attempt = _read_json(attempt_path, f"{pair} attempt")
    model_path = _validate_spec(
        spec.value,
        config["precision"],
        config["diagnostic_id"],
    )
    result = _validate_attempt(
        attempt,
        spec,
        config["precision"],
        config["diagnostic_id"],
        model_path,
        configured_executable,
    )
    return {
        "spec_path": _display_path(spec.path),
        "spec_sha256": spec.sha256,
        "attempt_path": _display_path(attempt.path),
        "attempt_sha256": attempt.sha256,
        "diagnostic_id": config["diagnostic_id"],
        "precision": config["precision"],
        **result,
    }


def _build_probe(case: Any, evidence: dict[str, Any]) -> dict[str, Any]:
    activation = evidence["activation"]
    probe = {
        "probe_id": case.test_id,
        "controlled_test_id": case.test_id,
        "status": "passed: expected-rejection",
        "expected_outcome": "expected-rejection",
        "matrix_case": _matrix_case_record(case),
        "execution_contract": asdict(execution_contract(case)),
        "rejection_kind": "post-activation-concrete-state-mismatch",
        "requested_cache_precision": evidence["precision"],
        "reported_cache_precision": evidence["precision"],
        "observed_key_state_precision": activation["observed_key_state_precision"],
        "observed_value_state_precision": activation["observed_value_state_precision"],
        "actual_persistent_standard_bytes": activation["actual_persistent_standard_bytes"],
        "actual_persistent_payload_bytes": activation["actual_persistent_payload_bytes"],
        "actual_persistent_norm_bytes": activation["actual_persistent_norm_bytes"],
        "actual_persistent_metadata_bytes": activation["actual_persistent_metadata_bytes"],
        "actual_persistent_total_bytes": activation["actual_bytes"],
        "diagnostic_id": evidence["diagnostic_id"],
        "spec_path": evidence["spec_path"], "spec_sha256": evidence["spec_sha256"],
        "attempt_path": evidence["attempt_path"], "attempt_sha256": evidence["attempt_sha256"],
        "stdout_sha256": evidence["stdout_sha256"], "stderr_sha256": evidence["stderr_sha256"],
        "output_sha256": evidence["output_sha256"], "telemetry_sha256": evidence["telemetry_sha256"],
        "generation_launched": True,
        "numeric_generation_metrics_accepted": False,
        "metric_outcome": "not-produced-by-expected-rejection",
        "cleanup_process_count": 0,
    }
    probe["probe_sha256"] = _sha256_bytes(_canonical_bytes(probe))
    return probe


def generate_scalar_semantic_rejection_evidence(matrix_path: Path, *, u8_spec_path: Path, u8_attempt_path: Path, u4_spec_path: Path, u4_attempt_path: Path) -> dict[str, Any]:
    """Generate evidence only from explicitly bound, retained diagnostics."""
    matrix = _read_json(matrix_path, "matrix")
    if SEMANTIC_SCALAR_REJECTION_IDS != frozenset({"OV-04", "OV-05", "OV-TQ-01", "OV-TQ-02"}):
        raise ValueError("semantic scalar rejection ID contract changed")
    cases = {case.test_id: case for case in _load_matrix_snapshot(matrix)}
    if set(cases).intersection(SEMANTIC_SCALAR_REJECTION_IDS) != SEMANTIC_SCALAR_REJECTION_IDS:
        raise ValueError("matrix semantic scalar rejection rows are missing")
    for test_id in SEMANTIC_SCALAR_REJECTION_IDS:
        _validate_scalar_case(cases[test_id], test_id)
    configured_executable = _configured_python_executable()
    evidence = {
        "u8": _evidence(
            u8_spec_path,
            u8_attempt_path,
            "u8",
            configured_executable,
        ),
        "u4": _evidence(
            u4_spec_path,
            u4_attempt_path,
            "u4",
            configured_executable,
        ),
    }
    probes = [_build_probe(cases[test_id], evidence[pair]) for pair in ("u8", "u4") for test_id in _PAIR_CONFIG[pair]["test_ids"]]
    probes.sort(key=lambda probe: probe["probe_id"])
    payload = {
        "schema": SCHEMA, "matrix_path": _display_path(matrix.path), "matrix_sha256": matrix.sha256,
        "controller_path": _display_path(_CONTROLLER_PATH), "controller_sha256": _sha256_file(_CONTROLLER_PATH),
        "probe_count": len(probes), "controlled_test_ids": sorted(SEMANTIC_SCALAR_REJECTION_IDS),
        "u8_spec_path": evidence["u8"]["spec_path"], "u8_spec_sha256": evidence["u8"]["spec_sha256"],
        "u8_attempt_path": evidence["u8"]["attempt_path"], "u8_attempt_sha256": evidence["u8"]["attempt_sha256"],
        "u4_spec_path": evidence["u4"]["spec_path"], "u4_spec_sha256": evidence["u4"]["spec_sha256"],
        "u4_attempt_path": evidence["u4"]["attempt_path"], "u4_attempt_sha256": evidence["u4"]["attempt_sha256"],
        "cleanup_process_count": 0, "probes": probes,
    }
    payload["aggregate_sha256"] = _sha256_bytes(_canonical_bytes(payload))
    return payload


def _validate_matrix_case_output(value: object, test_id: str) -> None:
    case = _require_exact_keys(value, _MATRIX_CASE_KEYS, "matrix case")
    expected = _EXPECTED_SCALAR_ROWS[test_id]
    precision = expected["precision"]
    expected_text = {
        "test_id": test_id,
        "phase": expected["phase"],
        "description": expected["description"],
        "model": "granite-3b",
        "weight_precision": precision,
        "k_algorithm": "scalar",
        "v_algorithm": "scalar",
        "k_precision": precision,
        "v_precision": precision,
        "device": "cpu",
        "guard": "none",
        "key_cache_precision": precision,
        "value_cache_precision": precision,
        "requested_device": "CPU",
        "runtime_key_algorithm": "STANDARD",
        "runtime_value_algorithm": "STANDARD",
        "attention_path": "not-produced-by-expected-rejection",
        "execution_route": "expected-rejection",
        "expected_outcome": "expected-rejection",
    }
    for field, expected_value in expected_text.items():
        _require_text(case.get(field), f"matrix case {field}", expected_value)
    if _require_integer_list(case.get("contexts"), "matrix case contexts") != [4096]:
        raise ValueError("matrix case contexts are invalid")
    _require_bool(case.get("quality_required"), "matrix case quality", True)
    if _require_text_list(
        case.get("required_metrics"),
        "matrix case required metrics",
    ) != sorted(_FORMAL_METRICS):
        raise ValueError("matrix case required metrics are invalid")
    _require_bool(
        case.get("norm_correction"),
        "matrix case norm correction",
        False,
    )
    _require_bool(
        case.get("suitable_host_required"),
        "matrix case suitable-host requirement",
        False,
    )
    _require_bool(
        case.get("numeric_generation_metrics_expected"),
        "matrix case numeric metric expectation",
        False,
    )


def _validate_contract_output(value: object, test_id: str) -> None:
    contract = _require_exact_keys(value, _CONTRACT_KEYS, "execution contract")
    expected_text = {
        "controlled_test_id": test_id,
        "execution_route": "expected-rejection",
        "expected_outcome": "expected-rejection",
        "runtime_key_algorithm": "STANDARD",
        "runtime_value_algorithm": "STANDARD",
        "attention_path": "not-produced-by-expected-rejection",
    }
    for field, expected_value in expected_text.items():
        _require_text(
            contract.get(field),
            f"execution contract {field}",
            expected_value,
        )
    _require_bool(
        contract.get("norm_correction"),
        "execution contract norm correction",
        False,
    )
    _require_bool(
        contract.get("suitable_host_required"),
        "execution contract suitable-host requirement",
        False,
    )
    _require_bool(
        contract.get("requires_actual_cache_precision_proof"),
        "execution contract cache precision proof",
        False,
    )
    _require_bool(
        contract.get("numeric_generation_metrics_expected"),
        "execution contract numeric metric expectation",
        False,
    )


def _validate_probe_output(value: object) -> str:
    probe = _require_exact_keys(value, _PROBE_KEYS, "probe")
    probe_id = _require_text(probe.get("probe_id"), "probe ID")
    if probe_id not in _EXPECTED_SCALAR_ROWS:
        raise ValueError("probe ID is not controlled")
    _require_text(
        probe.get("controlled_test_id"),
        "controlled test ID",
        probe_id,
    )
    _require_text(
        probe.get("status"),
        "probe status",
        "passed: expected-rejection",
    )
    _require_text(
        probe.get("expected_outcome"),
        "probe expected outcome",
        "expected-rejection",
    )
    _require_text(
        probe.get("rejection_kind"),
        "probe rejection kind",
        "post-activation-concrete-state-mismatch",
    )
    _validate_matrix_case_output(probe.get("matrix_case"), probe_id)
    _validate_contract_output(probe.get("execution_contract"), probe_id)

    precision = _EXPECTED_SCALAR_ROWS[probe_id]["precision"]
    _require_text(
        probe.get("requested_cache_precision"),
        "probe requested precision",
        precision,
    )
    _require_text(
        probe.get("reported_cache_precision"),
        "probe reported precision",
        precision,
    )
    _require_text(
        probe.get("observed_key_state_precision"),
        "probe observed key precision",
        "f32",
    )
    _require_text(
        probe.get("observed_value_state_precision"),
        "probe observed value precision",
        "f32",
    )
    standard_bytes = _require_int(
        probe.get("actual_persistent_standard_bytes"),
        "probe standard bytes",
        positive=True,
    )
    for field in (
        "actual_persistent_payload_bytes",
        "actual_persistent_norm_bytes",
        "actual_persistent_metadata_bytes",
    ):
        _require_int(probe.get(field), f"probe {field}", zero=True)
    total_bytes = _require_int(
        probe.get("actual_persistent_total_bytes"),
        "probe total bytes",
        positive=True,
    )
    if total_bytes != standard_bytes:
        raise ValueError("probe total and standard bytes differ")

    pair = precision
    _require_text(
        probe.get("diagnostic_id"),
        "probe diagnostic ID",
        _PAIR_CONFIG[pair]["diagnostic_id"],
    )
    for field in ("spec_path", "attempt_path"):
        _require_text(probe.get(field), f"probe {field}")
    for field in (
        "spec_sha256", "attempt_sha256", "stdout_sha256", "stderr_sha256",
        "output_sha256", "telemetry_sha256",
    ):
        _require_sha256(probe.get(field), f"probe {field}")
    _require_bool(
        probe.get("generation_launched"),
        "probe generation launched",
        True,
    )
    _require_bool(
        probe.get("numeric_generation_metrics_accepted"),
        "probe numeric metric acceptance",
        False,
    )
    _require_text(
        probe.get("metric_outcome"),
        "probe metric outcome",
        "not-produced-by-expected-rejection",
    )
    _require_int(probe.get("cleanup_process_count"), "probe cleanup", zero=True)
    claimed_probe_hash = _require_sha256(
        probe.get("probe_sha256"),
        "probe SHA-256",
    )
    unhashed_probe = dict(probe)
    unhashed_probe.pop("probe_sha256")
    if claimed_probe_hash != _sha256_bytes(_canonical_bytes(unhashed_probe)):
        raise ValueError("probe SHA-256 is invalid")
    return probe_id


def _validate_payload_shape(payload: Mapping[str, Any]) -> dict[str, Any]:
    aggregate = _require_exact_keys(payload, _TOP_LEVEL_KEYS, "aggregate")
    _require_text(aggregate.get("schema"), "aggregate schema", SCHEMA)
    for field in (
        "matrix_path", "controller_path", "u8_spec_path", "u8_attempt_path",
        "u4_spec_path", "u4_attempt_path",
    ):
        _require_text(aggregate.get(field), f"aggregate {field}")
    for field in (
        "matrix_sha256", "controller_sha256", "u8_spec_sha256",
        "u8_attempt_sha256", "u4_spec_sha256", "u4_attempt_sha256",
    ):
        _require_sha256(aggregate.get(field), f"aggregate {field}")
    if _require_int(
        aggregate.get("probe_count"),
        "aggregate probe count",
        positive=True,
    ) != 4:
        raise ValueError("aggregate probe count is not four")
    controlled_ids = _require_text_list(
        aggregate.get("controlled_test_ids"),
        "aggregate controlled IDs",
    )
    if controlled_ids != sorted(SEMANTIC_SCALAR_REJECTION_IDS):
        raise ValueError("aggregate controlled IDs are invalid")
    _require_int(
        aggregate.get("cleanup_process_count"),
        "aggregate cleanup",
        zero=True,
    )
    probes = aggregate.get("probes")
    if type(probes) is not list or len(probes) != 4:
        raise ValueError("aggregate probes are invalid")
    probe_ids = [_validate_probe_output(probe) for probe in probes]
    if probe_ids != sorted(SEMANTIC_SCALAR_REJECTION_IDS):
        raise ValueError("aggregate probe IDs are invalid or unsorted")
    claimed_aggregate_hash = _require_sha256(
        aggregate.get("aggregate_sha256"),
        "aggregate SHA-256",
    )
    unhashed_aggregate = dict(aggregate)
    unhashed_aggregate.pop("aggregate_sha256")
    if claimed_aggregate_hash != _sha256_bytes(
        _canonical_bytes(unhashed_aggregate)
    ):
        raise ValueError("aggregate SHA-256 is invalid")
    return aggregate


def validate_scalar_semantic_rejection_evidence(payload: Mapping[str, Any], matrix_path: Path, *, u8_spec_path: Path, u8_attempt_path: Path, u4_spec_path: Path, u4_attempt_path: Path) -> dict[str, Any]:
    """Accept only byte-for-byte current evidence from the supplied paths."""
    aggregate = _validate_payload_shape(payload)
    expected = generate_scalar_semantic_rejection_evidence(matrix_path, u8_spec_path=u8_spec_path, u8_attempt_path=u8_attempt_path, u4_spec_path=u4_spec_path, u4_attempt_path=u4_attempt_path)
    try:
        supplied_bytes = _canonical_bytes(aggregate)
    except (TypeError, ValueError) as error:
        raise ValueError(
            "scalar semantic rejection evidence is not canonical JSON data"
        ) from error
    historical_expected = dict(expected)
    historical_expected["controller_path"] = _HISTORICAL_CONTROLLER_PATH
    historical_expected["controller_sha256"] = _HISTORICAL_CONTROLLER_SHA256
    historical_expected.pop("aggregate_sha256")
    historical_expected["aggregate_sha256"] = _sha256_bytes(
        _canonical_bytes(historical_expected)
    )
    if supplied_bytes not in {
        _canonical_bytes(expected),
        _canonical_bytes(historical_expected),
    }:
        raise ValueError("scalar semantic rejection evidence does not match current controlled inputs")
    return {"accepted": True, "schema": SCHEMA, "probe_count": 4, "matrix_sha256": aggregate["matrix_sha256"], "controller_sha256": aggregate["controller_sha256"], "aggregate_sha256": aggregate["aggregate_sha256"]}


def write_scalar_semantic_rejection_evidence(path: Path, payload: Mapping[str, Any], *, matrix_path: Path, u8_spec_path: Path, u8_attempt_path: Path, u4_spec_path: Path, u4_attempt_path: Path) -> None:
    """Create canonical evidence once, refusing to replace a destination."""
    validate_scalar_semantic_rejection_evidence(payload, matrix_path, u8_spec_path=u8_spec_path, u8_attempt_path=u8_attempt_path, u4_spec_path=u4_spec_path, u4_attempt_path=u4_attempt_path)
    destination = Path(path).resolve()
    destination.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(dir=destination.parent, prefix=f".{destination.name}.", suffix=".tmp")
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(_canonical_bytes(payload)); handle.flush(); os.fsync(handle.fileno())
        try:
            os.link(temporary, destination)
        except FileExistsError as error:
            raise FileExistsError(f"refusing to replace existing scalar semantic rejection evidence: {destination}") from error
    except BaseException:
        temporary.unlink(missing_ok=True)
        raise
    temporary.unlink()


__all__ = ["generate_scalar_semantic_rejection_evidence", "validate_scalar_semantic_rejection_evidence", "write_scalar_semantic_rejection_evidence"]
