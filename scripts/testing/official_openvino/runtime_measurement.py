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
import statistics
import tempfile
from collections.abc import Callable, Mapping, Sequence
from pathlib import Path
from typing import Any


RESULT_MARKER = "OPENVINO_WB04_RESULT_JSON="
WORKER_SCHEMA = "official-openvino-wb04-worker/v1"
RUNTIME_ALGORITHMS = frozenset({"STANDARD", "TBQ3", "TBQ4"})
CACHE_PRECISIONS = frozenset({"f16", "bf16", "f32", "u8", "u4", "u3"})


def _finite_number(value: Any, field: str, *, maximum: float | None = None) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be numeric")
    result = float(value)
    if not math.isfinite(result) or result < 0:
        raise ValueError(f"{field} must be finite and non-negative")
    if maximum is not None and result > maximum:
        raise ValueError(f"{field} exceeds {maximum}")
    return result


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

    if key_algorithm not in RUNTIME_ALGORITHMS or value_algorithm not in RUNTIME_ALGORITHMS:
        raise ValueError("runtime algorithm labels must use exact uppercase enums")
    normalized_device = device.upper()
    if normalized_device not in {"CPU", "GPU"}:
        raise ValueError("runtime device must be CPU or GPU")
    if (
        normalized_device != "CPU"
        and (key_algorithm != "STANDARD" or value_algorithm != "STANDARD")
    ):
        raise ValueError("project TurboQuant is supported only on CPU")
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
        if value.get("status") == "activated"
    ]
    if len(activations) != 1:
        raise ValueError(
            f"expected exactly one activated telemetry record, found {len(activations)}"
        )
    activation = activations[0]
    if activation.get("fallback") is not False:
        raise ValueError("activated telemetry reports fallback")
    expected = _finite_number(activation.get("expected_bytes"), "expected bytes")
    actual = _finite_number(activation.get("actual_bytes"), "actual bytes")
    if expected != actual:
        raise ValueError("activated telemetry persistent bytes do not reconcile")
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
            rows = list(reader)
    except OSError as error:
        raise ValueError(f"GPU CSV could not be read: {error}") from error
    if len(rows) < 2:
        raise ValueError("GPU evidence requires at least two observations")

    gpu: list[float] = []
    dedicated: list[float] = []
    shared: list[float] = []
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
    return {
        "gpu_percent": _summarize(gpu),
        "gpu_engine_count": _summarize(engines),
        "gpu_dedicated_memory_peak_mb": max(dedicated),
        "gpu_shared_memory_peak_mb": max(shared),
        "gpu_memory_peak_mb": max(dedicated) + max(shared),
        "observation_count": len(rows),
    }


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
]
