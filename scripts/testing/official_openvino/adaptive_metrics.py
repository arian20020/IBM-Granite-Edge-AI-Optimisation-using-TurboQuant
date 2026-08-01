"""Lossless formal runtime samples for adaptive OpenVINO comparison."""

from __future__ import annotations

import hashlib
import json
import math
import statistics
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any

from .runtime_process import measurement_sample


TIMING_FIELDS = (
    "load_ms",
    "ttft_ms",
    "prompt_tps",
    "tpot_ms",
    "decode_tps",
    "generation_duration_ms",
)
MEMORY_FIELDS = (
    "peak_working_set_mib",
    "peak_private_mib",
    "available_ram_mib",
    "kv_mib",
    "gpu_memory_peak_mib",
)
IDENTITY_HASH_FIELDS = (
    "artifact_manifest_sha256",
    "prompt_sha256",
    "matrix_sha256",
    "build_provenance_sha256",
    "command_sha256",
    "evidence_sha256",
)
RAW_MIB_FIELDS = {
    "peak_working_set_mb": "peak_working_set_mib",
    "peak_private_mb": "peak_private_mib",
    "available_ram_min_mb": "available_ram_mib",
    "kv_mb": "kv_mib",
    "gpu_memory_peak_mb": "gpu_memory_peak_mib",
}
_SHA256_LENGTH = 64
_MEMORY_RECEIPT_SCHEMA = "official-openvino-memory-unit-receipt/v2"
_MIB_PROJECTION_PROVENANCE = {
    "peak_working_set_mb": (
        "owned_process_memory.working_set_bytes",
        "owned-process memory sampler",
    ),
    "peak_private_mb": (
        "owned_process_memory.private_bytes",
        "owned-process memory sampler",
    ),
    "available_ram_min_mb": (
        "available_ram_bytes.minimum",
        "available-RAM sampler",
    ),
    "kv_mb": (
        "activation.actual_bytes",
        "OpenVINO activation telemetry",
    ),
    "gpu_memory_peak_mb": (
        "GPUProcessMemory.DedicatedUsage+SharedUsage",
        "Windows GPUProcessMemory sampler",
    ),
}


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _sha256_json(value: Any) -> str:
    return hashlib.sha256(
        json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        ).encode("utf-8")
    ).hexdigest()


def _finite_nonnegative(value: Any, field: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be numeric")
    number = float(value)
    if not math.isfinite(number) or number < 0:
        raise ValueError(f"{field} must be finite and non-negative")
    return number


def _positive_integer(value: Any, field: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value <= 0:
        raise ValueError(f"{field} must be a positive integer")
    return value


def _zero_integer(value: Any, field: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value != 0:
        raise ValueError(f"{field} must be zero")
    return value


def _require_binary_mib_receipt(
    record: Mapping[str, Any], sample: Mapping[str, Any]
) -> None:
    receipt = record.get("memory_unit_receipt")
    if not isinstance(receipt, Mapping):
        raise ValueError("memory unit receipt is required")
    if receipt.get("schema") != _MEMORY_RECEIPT_SCHEMA:
        raise ValueError(
            "legacy memory unit receipt has no verifiable binary MiB provenance"
        )
    if receipt.get("binary_mib_bytes") != 1024**2:
        raise ValueError("memory unit receipt does not prove binary MiB")
    projections = receipt.get("projections")
    if not isinstance(projections, Mapping) or set(projections) != set(RAW_MIB_FIELDS):
        raise ValueError("memory unit receipt provenance is incomplete")
    for field, (source_field, collector) in _MIB_PROJECTION_PROVENANCE.items():
        projection = projections.get(field)
        if not isinstance(projection, Mapping) or projection != {
            "collector": collector,
            "source_field": source_field,
            "source_unit": "bytes",
            "conversion": "divide-by-binary-mib",
            "conversion_divisor_bytes": 1024**2,
            "projected_unit": "MiB",
        }:
            raise ValueError(
                f"{field} memory unit provenance is not a binary MiB conversion"
            )
    activation = record.get("activation")
    raw_values = {
        "peak_working_set_mb": record.get("peak_working_set_bytes"),
        "peak_private_mb": record.get("peak_private_bytes"),
        "available_ram_min_mb": (
            record.get("available_ram_bytes", {}).get("minimum")
            if isinstance(record.get("available_ram_bytes"), Mapping)
            else None
        ),
        "kv_mb": activation.get("actual_bytes") if isinstance(activation, Mapping) else None,
    }
    for field, raw_value in raw_values.items():
        raw_bytes = _finite_nonnegative(raw_value, f"{field} source bytes")
        projected = _finite_nonnegative(sample.get(field), field)
        if not math.isclose(
            projected * (1024**2), raw_bytes, rel_tol=1e-9, abs_tol=1e-6
        ):
            raise ValueError(f"{field} does not match binary MiB source conversion")


def _identity_hashes(record: Mapping[str, Any], source: Path) -> dict[str, str]:
    supplied = record.get("identity_hashes")
    if not isinstance(supplied, Mapping):
        raise ValueError("identity hashes are required")
    values = dict(supplied)
    command = record.get("command")
    if (
        not isinstance(command, list)
        or not command
        or not all(isinstance(item, str) and item for item in command)
    ):
        raise ValueError("command payload is required")
    expected_command_hash = _sha256_json(command)
    if values.get("command_sha256") != expected_command_hash:
        raise ValueError("command_sha256 does not match the canonical command payload")
    values.setdefault("evidence_sha256", _sha256_file(source))
    if set(values) != set(IDENTITY_HASH_FIELDS):
        raise ValueError("identity hashes are incomplete")
    normalized: dict[str, str] = {}
    for field in IDENTITY_HASH_FIELDS:
        value = values[field]
        if (
            not isinstance(value, str)
            or len(value) != _SHA256_LENGTH
            or any(character not in "0123456789abcdef" for character in value)
        ):
            raise ValueError(f"{field} must be a lowercase SHA-256")
        normalized[field] = value
    return normalized


def _utilisation(sample: Mapping[str, Any], field: str) -> dict[str, Any]:
    value = sample.get(field)
    if not isinstance(value, Mapping) or value.get("query_succeeded") is not True:
        raise ValueError(f"{field} sampler observations are required")
    raw = value.get("values")
    if not isinstance(raw, list) or not raw:
        raise ValueError(f"{field} sampler observations are required")
    values = [_finite_nonnegative(item, f"{field} observation") for item in raw]
    if value.get("count") != len(values):
        raise ValueError(f"{field} sampler count does not match observations")
    return {
        "values": values,
        "count": len(values),
        "query_succeeded": True,
    }


def _validate_and_enrich_sample(
    sample: Mapping[str, Any],
    record: Mapping[str, Any],
    source: Path,
) -> dict[str, Any]:
    _require_binary_mib_receipt(record, sample)
    worker = record.get("worker")
    activation = sample.get("activation")
    if not isinstance(worker, Mapping) or not isinstance(activation, Mapping):
        raise ValueError("worker and activation evidence are required")
    values = dict(sample)
    for raw, target in RAW_MIB_FIELDS.items():
        values[target] = _finite_nonnegative(values.get(raw), target)
    values["num_input_tokens"] = _positive_integer(
        worker.get("num_input_tokens"), "num_input_tokens"
    )
    values["num_generated_tokens"] = _positive_integer(
        worker.get("num_generated_tokens"), "num_generated_tokens"
    )
    values["exit_code"] = _zero_integer(record.get("exit_code"), "exit_code")
    values["fallback_count"] = _zero_integer(
        record.get("fallback_count"), "fallback_count"
    )
    values["residual_owned_process_count"] = _zero_integer(
        record.get("residual_owned_process_count"),
        "residual_owned_process_count",
    )
    if record.get("cleanup_process_count") != 0:
        raise ValueError("cleanup_process_count must be zero")
    if activation.get("fallback") is not False:
        raise ValueError("activation fallback must be false")
    values["device"] = {
        "requested": activation.get("requested_device"),
        "actual": activation.get("actual_device"),
    }
    values["gpu_sampler_supported"] = record.get("gpu_sampler_supported") is True
    values["cpu_percent"] = _utilisation(values, "cpu_percent")
    values["gpu_percent"] = _utilisation(values, "gpu_percent")
    if not values["gpu_sampler_supported"]:
        raise ValueError("gpu sampler support receipt is required")
    values["identity_hashes"] = _identity_hashes(record, source)
    property_hash = record.get("runtime_property_sha256")
    if property_hash is not None:
        if (
            not isinstance(property_hash, str)
            or len(property_hash) != _SHA256_LENGTH
            or any(character not in "0123456789abcdef" for character in property_hash)
        ):
            raise ValueError("runtime_property_sha256 must be a lowercase SHA-256")
        values["runtime_property_sha256"] = property_hash
    return values


def build_adaptive_runtime_sample(
    record: Mapping[str, Any],
    source_path: Path,
) -> dict[str, Any]:
    """Project one governed record without discarding formal runtime evidence."""

    sample = measurement_sample(dict(record), Path(source_path))
    return _validate_and_enrich_sample(sample, record, Path(source_path))


def _summarize_run_scalars(
    samples: Sequence[Mapping[str, Any]], fields: Sequence[str]
) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for field in fields:
        values = [_finite_nonnegative(sample.get(field), field) for sample in samples]
        result[field] = {
            "values": values,
            "mean": statistics.fmean(values),
            "median": statistics.median(values),
            "min": min(values),
            "max": max(values),
            "count": len(values),
        }
    return result


def _summarize_memory(samples: Sequence[Mapping[str, Any]]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for field in MEMORY_FIELDS:
        values = [_finite_nonnegative(sample.get(field), field) for sample in samples]
        if field == "available_ram_mib":
            result[field] = {"values": values, "global_min": min(values), "count": len(values)}
        else:
            result[field] = {
                "values": values,
                "median": statistics.median(values),
                "worst_max": max(values),
                "count": len(values),
            }
    return result


def _summarize_pooled_utilisation(
    samples: Sequence[Mapping[str, Any]],
) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for field in ("cpu_percent", "gpu_percent"):
        values = [
            value
            for sample in samples
            for value in _utilisation(sample, field)["values"]
        ]
        if field == "gpu_percent" and not any(values):
            if not all(sample.get("gpu_sampler_supported") is True for sample in samples):
                raise ValueError("zero GPU utilization requires sampler support")
        result[field] = {
            "values": values,
            "mean": statistics.fmean(values),
            "median": statistics.median(values),
            "peak": max(values),
            "count": len(values),
        }
    return result


def _require_consistent_hashes(samples: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    hashes = [sample.get("identity_hashes") for sample in samples]
    if not all(isinstance(value, Mapping) for value in hashes):
        raise ValueError("identity_hashes are required")
    first = dict(hashes[0])
    constant_fields = tuple(
        field for field in IDENTITY_HASH_FIELDS if field != "command_sha256"
    )
    if any(
        any(dict(value).get(field) != first[field] for field in constant_fields)
        for value in hashes[1:]
    ):
        raise ValueError("identity hashes differ between formal samples")
    if set(first) != set(IDENTITY_HASH_FIELDS):
        raise ValueError("identity hashes are incomplete")
    commands = [dict(value)["command_sha256"] for value in hashes]
    result: dict[str, Any] = {
        field: first[field] for field in constant_fields
    }
    result["command_sha256"] = (
        commands[0] if len(set(commands)) == 1 else commands
    )
    return result


def _summarize_activation(samples: Sequence[Mapping[str, Any]]) -> dict[str, Any]:
    activation = [sample.get("activation") for sample in samples]
    if not all(isinstance(value, Mapping) for value in activation):
        raise ValueError("activation is required")
    first = dict(activation[0])
    if any(dict(value) != first for value in activation[1:]):
        raise ValueError("activation differs between formal samples")
    devices = [sample.get("device") for sample in samples]
    if any(device != devices[0] for device in devices[1:]):
        raise ValueError("device differs between formal samples")
    return {"sample_count": len(samples), "telemetry": first, "device": devices[0], "fallback": False}


def _require_consistent_formal_identity(samples: Sequence[Mapping[str, Any]]) -> None:
    tokens = {
        (sample.get("num_input_tokens"), sample.get("num_generated_tokens"))
        for sample in samples
    }
    if len(tokens) != 1:
        raise ValueError("num_generated_tokens differ between formal samples")
    for sample in samples:
        _positive_integer(sample.get("num_input_tokens"), "num_input_tokens")
        _positive_integer(sample.get("num_generated_tokens"), "num_generated_tokens")
        _zero_integer(sample.get("exit_code"), "exit_code")
        _zero_integer(sample.get("fallback_count"), "fallback_count")
        _zero_integer(
            sample.get("residual_owned_process_count"),
            "residual_owned_process_count",
        )


def summarize_adaptive_runtime_samples(
    samples: Sequence[Mapping[str, Any]],
) -> dict[str, Any]:
    """Aggregate exactly three lossless governed runtime samples."""

    if len(samples) != 3:
        raise ValueError("adaptive runtime summary requires exactly three samples")
    _require_consistent_formal_identity(samples)
    return {
        "schema": "official-openvino-adaptive-runtime-summary-v1",
        "samples": [dict(sample) for sample in samples],
        "timing": _summarize_run_scalars(samples, TIMING_FIELDS),
        "memory": _summarize_memory(samples),
        "utilisation": _summarize_pooled_utilisation(samples),
        "activation": _summarize_activation(samples),
        "identity_hashes": _require_consistent_hashes(samples),
    }


__all__ = [
    "IDENTITY_HASH_FIELDS",
    "MEMORY_FIELDS",
    "RAW_MIB_FIELDS",
    "TIMING_FIELDS",
    "build_adaptive_runtime_sample",
    "summarize_adaptive_runtime_samples",
]
