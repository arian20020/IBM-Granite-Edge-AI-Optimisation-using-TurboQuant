"""Fail-closed evidence for post-activation scalar cache rejection rows."""

from __future__ import annotations

import hashlib
import json
import math
import os
import tempfile
from collections.abc import Mapping
from dataclasses import asdict
from pathlib import Path
from typing import Any

from .matrix import SEMANTIC_SCALAR_REJECTION_IDS, execution_contract, load_matrix
from .runtime_measurement import RESULT_MARKER, validate_activation_telemetry


SCHEMA = "official-openvino-wb04-scalar-semantic-rejection-evidence/v1"
_WORKER_SCHEMA = "official-openvino-wb04-worker/v1"
_SPEC_SCHEMA = "official-openvino-wb04-worker-spec/v1"
_ATTEMPT_SCHEMA = "official-openvino-wb04-governed-run/v1"
_CONTROLLER_PATH = Path(__file__).resolve()
_REPO_ROOT = _CONTROLLER_PATH.parents[3]
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
        value = json.loads(raw, object_pairs_hook=_reject_duplicate_keys,
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


def _read_json(path: Path, label: str) -> tuple[Path, dict[str, Any]]:
    resolved = Path(path).resolve()
    if not resolved.is_file():
        raise ValueError(f"{label} file is missing: {resolved}")
    return resolved, _strict_json_bytes(resolved.read_bytes(), label)


def _display_path(path: Path) -> str:
    try:
        return path.resolve().relative_to(_REPO_ROOT).as_posix()
    except ValueError:
        return str(path.resolve())


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
    if not isinstance(value, str) or not value:
        raise ValueError(f"{label} must be non-empty text")
    if expected is not None and value != expected:
        raise ValueError(f"{label} has an invalid value")
    return value


def _matrix_case_record(case: Any) -> dict[str, Any]:
    record = asdict(case)
    record["contexts"] = list(case.contexts)
    record["required_metrics"] = sorted(case.required_metrics)
    return record


def _parse_json_line(raw: bytes, label: str) -> dict[str, Any]:
    return _strict_json_bytes(raw, label)


def _parse_worker_streams(stdout: Path, stderr: Path) -> tuple[dict[str, Any], dict[str, Any]]:
    stdout_raw = stdout.read_bytes()
    stderr_raw = stderr.read_bytes()
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
    attempt: dict[str, Any], attempt_path: Path, spec_path: Path,
    precision: str, diagnostic_id: str, model_path: str,
) -> dict[str, Any]:
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
    if type(command) is not list or len(command) != 5 or not all(isinstance(item, str) and item for item in command):
        raise ValueError("attempt command is invalid")
    if command[1:4] != ["-m", "scripts.testing.official_openvino.measurement_worker", "--spec"]:
        raise ValueError("attempt command does not execute only the measurement worker")
    if Path(command[4]).resolve() != spec_path:
        raise ValueError("attempt command is not bound to the supplied spec")

    stdout = attempt_path.parent / "stdout.txt"
    stderr = attempt_path.parent / "stderr.txt"
    if not stdout.is_file() or not stderr.is_file():
        raise ValueError("attempt stdout/stderr artifacts are missing")
    for field, path in (("stdout", stdout), ("stderr", stderr)):
        expected_hash = _require_text(attempt.get(f"{field}_sha256"), f"attempt {field} SHA-256")
        if _sha256_file(path) != expected_hash:
            raise ValueError(f"attempt {field} bytes do not match SHA-256")
    streamed_worker, streamed_activation = _parse_worker_streams(stdout, stderr)
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
    if _sha256_bytes(worker["output"].encode("utf-8")) != _require_text(attempt.get("output_sha256"), "attempt output SHA-256"):
        raise ValueError("attempt output SHA-256 does not match worker output")
    if _sha256_json(activation) != _require_text(attempt.get("telemetry_sha256"), "attempt telemetry SHA-256"):
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


def _evidence(spec_path: Path, attempt_path: Path, pair: str) -> dict[str, Any]:
    config = _PAIR_CONFIG[pair]
    spec_file, spec = _read_json(spec_path, f"{pair} spec")
    attempt_file, attempt = _read_json(attempt_path, f"{pair} attempt")
    model_path = _validate_spec(spec, config["precision"], config["diagnostic_id"])
    result = _validate_attempt(attempt, attempt_file, spec_file, config["precision"], config["diagnostic_id"], model_path)
    return {"spec_path": _display_path(spec_file), "spec_sha256": _sha256_file(spec_file), "attempt_path": _display_path(attempt_file), "attempt_sha256": _sha256_file(attempt_file), "diagnostic_id": config["diagnostic_id"], "precision": config["precision"], **result}


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
    matrix, _ = _read_json(matrix_path, "matrix")
    if SEMANTIC_SCALAR_REJECTION_IDS != frozenset({"OV-04", "OV-05", "OV-TQ-01", "OV-TQ-02"}):
        raise ValueError("semantic scalar rejection ID contract changed")
    cases = {case.test_id: case for case in load_matrix(matrix)}
    if set(cases).intersection(SEMANTIC_SCALAR_REJECTION_IDS) != SEMANTIC_SCALAR_REJECTION_IDS:
        raise ValueError("matrix semantic scalar rejection rows are missing")
    for test_id in SEMANTIC_SCALAR_REJECTION_IDS:
        contract = execution_contract(cases[test_id])
        if contract.expected_outcome != "expected-rejection" or contract.execution_route != "expected-rejection" or contract.numeric_generation_metrics_expected is not False:
            raise ValueError(f"{test_id} no longer has the semantic rejection contract")
    evidence = {
        "u8": _evidence(u8_spec_path, u8_attempt_path, "u8"),
        "u4": _evidence(u4_spec_path, u4_attempt_path, "u4"),
    }
    probes = [_build_probe(cases[test_id], evidence[pair]) for pair in ("u8", "u4") for test_id in _PAIR_CONFIG[pair]["test_ids"]]
    probes.sort(key=lambda probe: probe["probe_id"])
    payload = {
        "schema": SCHEMA, "matrix_path": _display_path(matrix), "matrix_sha256": _sha256_file(matrix),
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


def _validate_payload_shape(payload: Mapping[str, Any]) -> dict[str, Any]:
    aggregate = _require_exact_keys(payload, _TOP_LEVEL_KEYS, "aggregate")
    _require_text(aggregate.get("schema"), "aggregate schema", SCHEMA)
    _require_int(aggregate.get("probe_count"), "aggregate probe count", positive=True)
    _require_int(aggregate.get("cleanup_process_count"), "aggregate cleanup", zero=True)
    if type(aggregate.get("controlled_test_ids")) is not list or aggregate["controlled_test_ids"] != sorted(SEMANTIC_SCALAR_REJECTION_IDS):
        raise ValueError("aggregate controlled IDs are invalid")
    probes = aggregate.get("probes")
    if type(probes) is not list or len(probes) != 4:
        raise ValueError("aggregate probes are invalid")
    probe_ids = []
    for probe in probes:
        item = _require_exact_keys(probe, _PROBE_KEYS, "probe")
        probe_ids.append(_require_text(item.get("probe_id"), "probe ID"))
        _require_bool(item.get("generation_launched"), "probe generation launched", True)
        _require_bool(item.get("numeric_generation_metrics_accepted"), "probe numeric metric acceptance", False)
        _require_int(item.get("cleanup_process_count"), "probe cleanup", zero=True)
        unhashed = dict(item); claimed = unhashed.pop("probe_sha256")
        if not isinstance(claimed, str) or claimed != _sha256_bytes(_canonical_bytes(unhashed)):
            raise ValueError("probe SHA-256 is invalid")
    if probe_ids != sorted(SEMANTIC_SCALAR_REJECTION_IDS):
        raise ValueError("aggregate probe IDs are invalid or unsorted")
    unhashed = dict(aggregate); claimed = unhashed.pop("aggregate_sha256")
    if not isinstance(claimed, str) or claimed != _sha256_bytes(_canonical_bytes(unhashed)):
        raise ValueError("aggregate SHA-256 is invalid")
    return aggregate


def validate_scalar_semantic_rejection_evidence(payload: Mapping[str, Any], matrix_path: Path, *, u8_spec_path: Path, u8_attempt_path: Path, u4_spec_path: Path, u4_attempt_path: Path) -> dict[str, Any]:
    """Accept only byte-for-byte current evidence from the supplied paths."""
    aggregate = _validate_payload_shape(payload)
    expected = generate_scalar_semantic_rejection_evidence(matrix_path, u8_spec_path=u8_spec_path, u8_attempt_path=u8_attempt_path, u4_spec_path=u4_spec_path, u4_attempt_path=u4_attempt_path)
    if aggregate != expected:
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
