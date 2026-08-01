"""Crash-safe WB-04 runtime measurement primitives.

This module keeps controlled workbook labels separate from the values sent to
OpenVINO.  It also owns strict parsing of the worker and Windows counter
artifacts so a missing observation can never be replaced with an estimate.
"""

from __future__ import annotations

import csv
import json
import math
import os
import re
import statistics
import tempfile
from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import Any


RESULT_MARKER = "OPENVINO_WB04_RESULT_JSON="
WORKER_SCHEMA = "official-openvino-wb04-worker/v1"
RUNTIME_ALGORITHMS = frozenset({"STANDARD", "TBQ3", "TBQ4"})
CACHE_PRECISIONS = frozenset({"f16", "bf16", "f32", "u8", "u4", "u3"})
PERSISTENT_COMPONENTS = ("standard", "payload", "norm", "metadata")
RUNTIME_DEVICE_PATTERN = re.compile(r"(?:CPU|GPU(?:\.(?:0|[1-9][0-9]*))?)\Z")
MIB = 1024**2
GPU_MIB_DECIMAL_PLACES = 6


def _finite_number(value: Any, field: str, *, maximum: float | None = None) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be numeric")
    result = float(value)
    if not math.isfinite(result) or result < 0:
        raise ValueError(f"{field} must be finite and non-negative")
    if maximum is not None and result > maximum:
        raise ValueError(f"{field} exceeds {maximum}")
    return result


def _nonnegative_integer(
    value: Any,
    field: str,
    *,
    positive: bool = False,
) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise ValueError(f"{field} must be an integer")
    if value < 0 or (positive and value == 0):
        comparison = "positive" if positive else "non-negative"
        raise ValueError(f"{field} must be {comparison}")
    return value


def _nonblank_text(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} must be non-blank")
    return value


def validate_activation_telemetry(activation: Mapping[str, Any]) -> None:
    """Validate allocation reconciliation and immutable runtime identity."""

    if not isinstance(activation, Mapping):
        raise ValueError("activation telemetry must be an object")
    status = activation.get("status")
    if status not in {"activated", "not_requested"}:
        raise ValueError("activation telemetry status is unsupported")
    if activation.get("fallback") is not False:
        raise ValueError("activation telemetry reports fallback")

    requested_key = _nonblank_text(
        activation.get("requested_key_algorithm"),
        "requested key algorithm",
    )
    requested_value = _nonblank_text(
        activation.get("requested_value_algorithm"),
        "requested value algorithm",
    )
    activated_key = _nonblank_text(
        activation.get("activated_key_algorithm"),
        "activated key algorithm",
    )
    activated_value = _nonblank_text(
        activation.get("activated_value_algorithm"),
        "activated value algorithm",
    )
    for field, value in (
        ("requested key algorithm", requested_key),
        ("requested value algorithm", requested_value),
        ("activated key algorithm", activated_key),
        ("activated value algorithm", activated_value),
    ):
        if value not in RUNTIME_ALGORITHMS:
            raise ValueError(f"{field} is unsupported")
    if requested_key != activated_key or requested_value != activated_value:
        raise ValueError("requested and activated algorithms differ")

    expected_components: dict[str, int] = {}
    actual_components: dict[str, int] = {}
    for component in PERSISTENT_COMPONENTS:
        expected = _nonnegative_integer(
            activation.get(f"expected_persistent_{component}_bytes"),
            f"expected persistent {component} bytes",
        )
        actual = _nonnegative_integer(
            activation.get(f"actual_persistent_{component}_bytes"),
            f"actual persistent {component} bytes",
        )
        if expected != actual:
            raise ValueError(
                f"persistent {component} byte components do not reconcile"
            )
        expected_components[component] = expected
        actual_components[component] = actual

    expected_total = _nonnegative_integer(
        activation.get("expected_bytes"), "expected persistent bytes"
    )
    actual_total = _nonnegative_integer(
        activation.get("actual_bytes"), "actual persistent bytes"
    )
    if expected_total != actual_total:
        raise ValueError("persistent total bytes do not reconcile")
    if (
        expected_total != sum(expected_components.values())
        or actual_total != sum(actual_components.values())
    ):
        raise ValueError("persistent totals do not equal their byte components")

    build_commit = _nonblank_text(
        activation.get("build_commit"), "build commit"
    )
    if len(build_commit) != 40 or any(
        character not in "0123456789abcdefABCDEF" for character in build_commit
    ):
        raise ValueError("build commit must be an exact 40-character Git ID")
    _nonblank_text(activation.get("model_hash"), "model hash")
    transformed_model_hash = _nonblank_text(
        activation.get("transformed_model_hash"),
        "transformed model hash",
    )
    operation_type = _nonblank_text(
        activation.get("operation_type"), "operation type"
    )
    operation_count = _nonnegative_integer(
        activation.get("operation_count"), "operation count"
    )
    matched_state_count = _nonnegative_integer(
        activation.get("matched_state_count"), "matched state count"
    )
    device = _nonblank_text(activation.get("device"), "requested device")
    actual_device = _nonblank_text(
        activation.get("actual_device"), "actual device"
    )
    attention_path = _nonblank_text(
        activation.get("attention_path"), "attention path"
    )
    runtime_layer_type = _nonblank_text(
        activation.get("runtime_layer_type"), "runtime layer type"
    )
    decoded_scratch_bytes = _nonnegative_integer(
        activation.get("decoded_scratch_bytes"), "decoded scratch bytes"
    )
    full_precision_equivalent_bytes = _nonnegative_integer(
        activation.get("full_precision_equivalent_bytes"),
        "full-precision equivalent bytes",
    )

    if status == "not_requested":
        if (
            requested_key != "STANDARD"
            or requested_value != "STANDARD"
            or activated_key != "STANDARD"
            or activated_value != "STANDARD"
        ):
            raise ValueError(
                "not_requested telemetry must remain STANDARD/STANDARD"
            )
        if expected_components["standard"] <= 0:
            if device.upper().startswith("GPU"):
                raise ValueError(
                    "GPU STANDARD zero-byte allocation telemetry is not "
                    "instrumented"
                )
            raise ValueError(
                "CPU STANDARD telemetry requires positive measured standard bytes"
            )
        requested_family = device.split(".", 1)[0].upper()
        actual_family = actual_device.split(".", 1)[0].upper()
        if (
            requested_family not in {"CPU", "GPU"}
            or actual_family != requested_family
        ):
            raise ValueError("STANDARD requested and actual device families differ")
        if any(
            expected_components[component] != 0
            for component in ("payload", "norm", "metadata")
        ):
            raise ValueError(
                "CPU STANDARD telemetry cannot claim TurboQuant byte components"
            )
        if operation_count != 0 or matched_state_count != 0:
            raise ValueError(
                "CPU STANDARD telemetry cannot claim TurboQuant operations"
            )
        if operation_type != "not_requested":
            raise ValueError(
                "CPU STANDARD telemetry has an invalid operation type"
            )
        if transformed_model_hash != "not_requested":
            raise ValueError(
                "STANDARD telemetry has an invalid transformed model hash"
            )
        if (
            attention_path != "stateful_sdpa_standard"
            or runtime_layer_type != "not_requested"
        ):
            raise ValueError("STANDARD telemetry has an invalid runtime identity")
        if decoded_scratch_bytes != 0:
            raise ValueError("STANDARD telemetry cannot claim decoded scratch bytes")
        if full_precision_equivalent_bytes != actual_total:
            raise ValueError(
                "STANDARD full-precision bytes do not equal persistent bytes"
            )
        for field in (
            "requested_key_cache_precision",
            "requested_value_cache_precision",
            "activated_key_cache_precision",
            "activated_value_cache_precision",
            "observed_key_state_precision",
            "observed_value_state_precision",
        ):
            if _nonblank_text(activation.get(field), field) == "not_requested":
                raise ValueError(
                    "measured STANDARD telemetry requires cache precision evidence"
                )
        return

    if requested_key == "STANDARD" and requested_value == "STANDARD":
        raise ValueError(
            "activated TurboQuant telemetry must request compression"
        )
    if device != "CPU" or actual_device != "CPU":
        raise ValueError("activated TurboQuant telemetry must execute on CPU")
    if operation_type != "TurboQuantStateUpdateDecode":
        raise ValueError("activated TurboQuant operation type is invalid")
    if operation_count <= 0 or operation_count != matched_state_count:
        raise ValueError(
            "activated operation and matched-state counts do not reconcile"
        )
    if transformed_model_hash == "not_requested":
        raise ValueError(
            "activated TurboQuant telemetry lacks a transformed model hash"
        )
    if (
        attention_path != "stateful_sdpa_reference_codec"
        or runtime_layer_type != "Reference"
    ):
        raise ValueError("activated TurboQuant runtime identity is invalid")
    if decoded_scratch_bytes <= 0 or full_precision_equivalent_bytes <= 0:
        raise ValueError(
            "activated TurboQuant transient byte evidence is empty"
        )


def _summarize(values: Sequence[float]) -> dict[str, Any]:
    if not values:
        raise ValueError("sample series is empty")
    normalized = [_finite_number(value, "sample") for value in values]
    return {
        "values": normalized,
        "mean": statistics.fmean(normalized),
        "median": statistics.median(normalized),
        "peak": max(normalized),
        "count": len(normalized),
        "query_succeeded": True,
    }


def build_runtime_property_spec(
    *,
    device: str,
    key_algorithm: str,
    value_algorithm: str,
    key_cache_precision: str,
    value_cache_precision: str,
    norm_correction: bool,
    cache_dir: str,
) -> dict[str, Any]:
    """Translate an already-typed execution contract into runtime properties."""

    for field, value in (
        ("key", key_algorithm),
        ("value", value_algorithm),
    ):
        if value not in RUNTIME_ALGORITHMS:
            raise ValueError(
                f"unsupported runtime {field} algorithm: {value}; "
                "expected exact uppercase STANDARD, TBQ3, or TBQ4"
            )
    if not isinstance(device, str):
        raise ValueError("runtime device must be CPU or GPU")
    normalized_device = device.upper()
    if RUNTIME_DEVICE_PATTERN.fullmatch(normalized_device) is None:
        raise ValueError("runtime device must be CPU or GPU")
    gpu_requested = normalized_device == "GPU" or normalized_device.startswith("GPU.")
    if (
        gpu_requested
        and (key_algorithm != "STANDARD" or value_algorithm != "STANDARD")
    ):
        raise ValueError("project TurboQuant is supported only on CPU")
    if gpu_requested and (
        key_cache_precision != "frozen" or value_cache_precision != "frozen"
    ):
        raise ValueError(
            "GPU cache precision is plugin-owned; "
            "key and value cache precision must both be frozen"
        )
    if not gpu_requested:
        for field, value in (
            ("key cache precision", key_cache_precision),
            ("value cache precision", value_cache_precision),
        ):
            if value not in CACHE_PRECISIONS:
                raise ValueError(f"unsupported {field}: {value}")
    if not isinstance(norm_correction, bool):
        raise ValueError("norm correction must be boolean")
    if not isinstance(cache_dir, str) or not cache_dir.strip():
        raise ValueError("cache directory must be non-blank")

    if gpu_requested:
        return {
            "device": normalized_device,
            "properties": {
                "ATTENTION_BACKEND": "SDPA",
                "CACHE_DIR": cache_dir,
                "NUM_STREAMS": "1",
                "PERFORMANCE_HINT": "LATENCY",
            },
        }

    properties: dict[str, Any] = {
        "ATTENTION_BACKEND": "SDPA",
        "INFERENCE_NUM_THREADS": 1,
        "NUM_STREAMS": 1,
        "PERFORMANCE_HINT": "LATENCY",
        "ENABLE_CPU_PINNING": False,
        "CACHE_DIR": cache_dir,
    }
    turboquant_requested = (
        key_algorithm != "STANDARD" or value_algorithm != "STANDARD"
    )
    if turboquant_requested:
        properties.update(
            {
                "TURBOQUANT_KEY_ALGORITHM": key_algorithm,
                "TURBOQUANT_VALUE_ALGORITHM": value_algorithm,
                "TURBOQUANT_NORM_CORRECTION": norm_correction,
            }
        )
    if key_algorithm == "STANDARD":
        properties["KEY_CACHE_PRECISION"] = key_cache_precision
    if value_algorithm == "STANDARD":
        properties["VALUE_CACHE_PRECISION"] = value_cache_precision
    return {"device": normalized_device, "properties": properties}


def _json_objects(text: str) -> list[dict[str, Any]]:
    objects: list[dict[str, Any]] = []
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if not line or line.startswith(RESULT_MARKER):
            continue
        try:
            value = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict):
            objects.append(value)
    return objects


def parse_worker_output(stdout: str, stderr: str) -> dict[str, Any]:
    """Require one worker marker and one authoritative activation object."""

    markers: list[dict[str, Any]] = []
    for text in (stdout, stderr):
        for line in text.splitlines():
            if not line.startswith(RESULT_MARKER):
                continue
            try:
                value = json.loads(line[len(RESULT_MARKER) :])
            except json.JSONDecodeError as error:
                raise ValueError("worker result marker contains invalid JSON") from error
            if not isinstance(value, dict):
                raise ValueError("worker result marker must contain an object")
            markers.append(value)
    if len(markers) != 1:
        raise ValueError(
            f"expected exactly one worker result marker, found {len(markers)}"
        )
    result = markers[0]
    if result.get("schema") != WORKER_SCHEMA:
        raise ValueError("worker result schema is invalid")

    activations = [
        value
        for value in (*_json_objects(stdout), *_json_objects(stderr))
        if value.get("status") in {"activated", "not_requested"}
    ]
    if len(activations) != 1:
        raise ValueError(
            "expected exactly one activated or measured STANDARD telemetry "
            f"record, found {len(activations)}"
        )
    activation = activations[0]
    validate_activation_telemetry(activation)
    return {"result": result, "activation": activation}


def _parse_bool(value: str, field: str) -> bool:
    normalized = value.strip().lower()
    if normalized == "true":
        return True
    if normalized == "false":
        return False
    raise ValueError(f"{field} must be true or false")


def parse_cpu_samples(path: Path) -> dict[str, Any]:
    """Read repeated normalized process CPU observations."""

    try:
        with Path(path).open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            required = {
                "timestamp_utc",
                "cpu_percent",
                "cpu_sample_definition",
            }
            if reader.fieldnames is None or not required.issubset(reader.fieldnames):
                raise ValueError("CPU CSV is missing required columns")
            rows = list(reader)
    except OSError as error:
        raise ValueError(f"CPU CSV could not be read: {error}") from error
    if len(rows) < 2:
        raise ValueError("CPU evidence requires at least two observations")
    values: list[float] = []
    for index, row in enumerate(rows, start=1):
        expected_definition = (
            "lifetime_average_since_workload_resume"
            if index == 1
            else "interval_delta"
        )
        if row["cpu_sample_definition"] != expected_definition:
            raise ValueError(
                f"CPU observation {index} has an invalid sample definition"
            )
        try:
            value = float(row["cpu_percent"])
        except (TypeError, ValueError) as error:
            raise ValueError(
                f"CPU observation {index} contains a non-numeric value"
            ) from error
        values.append(_finite_number(value, "CPU percent", maximum=100.0))
    return _summarize(values)


def parse_gpu_samples(path: Path) -> dict[str, Any]:
    """Read repeated real GPU counter observations for one workload PID."""

    try:
        with Path(path).open(encoding="utf-8-sig", newline="") as handle:
            reader = csv.DictReader(handle)
            required = {
                "timestamp_utc",
                "gpu_percent",
                "gpu_engine_count",
                "gpu_dedicated_mb",
                "gpu_shared_mb",
                "gpu_engine_query_ok",
                "gpu_memory_query_ok",
            }
            if reader.fieldnames is None or not required.issubset(reader.fieldnames):
                raise ValueError("GPU CSV is missing required columns")
            component_byte_columns = {
                "gpu_dedicated_bytes",
                "gpu_shared_bytes",
            }
            combined_byte_column = "gpu_memory_bytes"
            byte_columns = component_byte_columns | {combined_byte_column}
            present_byte_columns = byte_columns.intersection(reader.fieldnames)
            if not present_byte_columns:
                byte_proof_format = None
            elif present_byte_columns == component_byte_columns and (
                "gpu_memory_mb" not in reader.fieldnames
            ):
                byte_proof_format = "component-only"
            elif (
                present_byte_columns == byte_columns
                and "gpu_memory_mb" in reader.fieldnames
            ):
                byte_proof_format = "combined"
            else:
                raise ValueError("GPU CSV has incomplete combined byte proof")
            rows = list(reader)
    except OSError as error:
        raise ValueError(f"GPU CSV could not be read: {error}") from error
    if len(rows) < 2:
        raise ValueError("GPU evidence requires at least two observations")

    gpu: list[float] = []
    dedicated: list[float] = []
    shared: list[float] = []
    combined: list[float] = []
    dedicated_bytes_observations: list[int] = []
    shared_bytes_observations: list[int] = []
    combined_bytes: list[int] = []
    engines: list[int] = []
    for index, row in enumerate(rows, start=1):
        if not _parse_bool(row["gpu_engine_query_ok"], "GPU engine query"):
            raise ValueError(f"GPU engine query failed at observation {index}")
        if not _parse_bool(row["gpu_memory_query_ok"], "GPU memory query"):
            raise ValueError(f"GPU memory query failed at observation {index}")
        try:
            gpu_value = float(row["gpu_percent"])
            engine_raw = float(row["gpu_engine_count"])
            dedicated_value = float(row["gpu_dedicated_mb"])
            shared_value = float(row["gpu_shared_mb"])
        except (TypeError, ValueError) as error:
            raise ValueError(
                f"GPU observation {index} contains a non-numeric value"
            ) from error
        gpu.append(_finite_number(gpu_value, "GPU percent", maximum=100.0))
        engine_value = _finite_number(engine_raw, "GPU engine count")
        if not engine_value.is_integer():
            raise ValueError("GPU engine count must be an integer")
        engines.append(int(engine_value))
        dedicated.append(
            _finite_number(dedicated_value, "GPU dedicated memory")
        )
        shared.append(_finite_number(shared_value, "GPU shared memory"))
        if byte_proof_format is not None:
            try:
                dedicated_bytes = int(row["gpu_dedicated_bytes"])
                shared_bytes = int(row["gpu_shared_bytes"])
                combined_bytes_value = (
                    int(row["gpu_memory_bytes"])
                    if byte_proof_format == "combined"
                    else dedicated_bytes + shared_bytes
                )
            except (TypeError, ValueError) as error:
                raise ValueError(
                    f"GPU combined byte proof {index} contains a non-integer value"
                ) from error
            if min(dedicated_bytes, shared_bytes, combined_bytes_value) < 0:
                raise ValueError(f"GPU combined byte proof {index} is negative")
            if combined_bytes_value != dedicated_bytes + shared_bytes:
                raise ValueError(
                    f"GPU combined byte proof {index} does not equal components"
                )
            expected_display = format(
                combined_bytes_value / MIB, f".{GPU_MIB_DECIMAL_PLACES}f"
            )
            if (
                byte_proof_format == "combined"
                and row["gpu_memory_mb"] != expected_display
            ):
                raise ValueError(
                    f"GPU combined MiB display {index} does not match byte proof"
                )
            combined.append(float(expected_display))
            dedicated_bytes_observations.append(dedicated_bytes)
            shared_bytes_observations.append(shared_bytes)
            combined_bytes.append(combined_bytes_value)
        else:
            combined.append(dedicated[-1] + shared[-1])
    result = {
        "gpu_percent": _summarize(gpu),
        "gpu_engine_count": _summarize(engines),
        "gpu_dedicated_memory_peak_mb": max(dedicated),
        "gpu_shared_memory_peak_mb": max(shared),
        "gpu_memory_peak_mb": max(combined),
        "observation_count": len(rows),
    }
    if byte_proof_format is not None:
        peak_index = max(range(len(combined_bytes)), key=combined_bytes.__getitem__)
        result["gpu_dedicated_memory_peak_bytes"] = max(dedicated_bytes_observations)
        result["gpu_shared_memory_peak_bytes"] = max(shared_bytes_observations)
        result["gpu_memory_peak_dedicated_bytes"] = dedicated_bytes_observations[
            peak_index
        ]
        result["gpu_memory_peak_shared_bytes"] = shared_bytes_observations[peak_index]
        result["gpu_memory_peak_bytes"] = combined_bytes[peak_index]
    return result


def atomic_write_json(path: Path, value: Mapping[str, Any]) -> None:
    """Write a valid JSON object durably before replacing the destination."""

    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="w",
            encoding="utf-8",
            newline="\n",
            prefix=".wb04-",
            dir=destination.parent,
            delete=False,
        ) as handle:
            temporary = Path(handle.name)
            json.dump(value, handle, indent=2, sort_keys=True, allow_nan=False)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, destination)
        temporary = None
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def _load_valid_attempt(path: Path, role: str) -> dict[str, Any] | None:
    if not path.is_file():
        return None
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    if (
        not isinstance(value, dict)
        or value.get("role") != role
        or value.get("valid") is not True
        or value.get("cleanup_process_count") != 0
    ):
        return None
    return value


def execute_attempt_sequence(
    root: Path,
    run_attempt: Callable[[str, Path], Mapping[str, Any]],
) -> dict[str, Any]:
    """Run or resume pilot, excluded warm-up, and three accepted samples."""

    output_root = Path(root)
    output_root.mkdir(parents=True, exist_ok=True)
    roles = ("pilot", "warmup", "sample-1", "sample-2", "sample-3")
    completed: list[dict[str, Any]] = []
    for role in roles:
        attempt_dir = output_root / role
        attempt_file = attempt_dir / "attempt.json"
        existing = _load_valid_attempt(attempt_file, role)
        if existing is not None:
            completed.append(existing)
            continue
        if attempt_dir.exists():
            raise RuntimeError(
                f"{role} attempt directory exists without a valid resumable record"
            )
        result = dict(run_attempt(role, attempt_dir))
        result.setdefault("role", role)
        if result.get("valid") is not True or result.get("cleanup_process_count") != 0:
            raise RuntimeError(f"{role} attempt did not pass validation")
        if not attempt_file.is_file():
            atomic_write_json(attempt_file, result)
        persisted = _load_valid_attempt(attempt_file, role)
        if persisted is None:
            raise RuntimeError(f"{role} attempt was not persisted atomically")
        completed.append(persisted)

    summary = {
        "pilot_passed": True,
        "warmup_excluded": True,
        "pilot": completed[0],
        "warmup": completed[1],
        "accepted_samples": completed[2:],
        "accepted_sample_count": 3,
        "cleanup_process_count": 0,
    }
    atomic_write_json(output_root / "attempt-sequence.json", summary)
    return summary


__all__ = [
    "RESULT_MARKER",
    "WORKER_SCHEMA",
    "atomic_write_json",
    "build_runtime_property_spec",
    "execute_attempt_sequence",
    "parse_cpu_samples",
    "parse_gpu_samples",
    "parse_worker_output",
    "validate_activation_telemetry",
]
