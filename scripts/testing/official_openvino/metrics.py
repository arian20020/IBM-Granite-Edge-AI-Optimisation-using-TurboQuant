"""Strict measurement reconciliation for Official OpenVINO WB-04 runs."""

from __future__ import annotations

import math
import re
import statistics
from collections.abc import Mapping, Sequence
from typing import Any


MIB = 1024**2
SCALARS = (
    "load_ms",
    "ttft_ms",
    "prompt_tps",
    "tpot_ms",
    "decode_tps",
    "generation_duration_ms",
    "peak_working_set_mb",
    "peak_private_mb",
    "available_ram_before_mb",
    "available_ram_min_mb",
    "available_ram_after_mb",
    "gpu_dedicated_memory_peak_mb",
    "gpu_shared_memory_peak_mb",
    "gpu_memory_peak_mb",
    "expected_persistent_kv_bytes",
    "actual_persistent_kv_bytes",
    "standard_kv_bytes",
    "payload_kv_bytes",
    "norm_kv_bytes",
    "metadata_kv_bytes",
    "scratch_peak_bytes",
    "kv_mb",
)
STRICTLY_POSITIVE = frozenset({
    "load_ms",
    "ttft_ms",
    "prompt_tps",
    "tpot_ms",
    "decode_tps",
    "generation_duration_ms",
    "peak_working_set_mb",
    "peak_private_mb",
    "available_ram_before_mb",
    "available_ram_min_mb",
    "available_ram_after_mb",
    "expected_persistent_kv_bytes",
    "actual_persistent_kv_bytes",
    "kv_mb",
})
UTILIZATION = ("cpu_percent", "gpu_percent")
UTIL_STATS = ("mean", "median", "peak", "count")
ACTIVATION_TEXT_FIELDS = (
    "requested_key_algorithm",
    "requested_value_algorithm",
    "activated_key_algorithm",
    "activated_value_algorithm",
    "requested_key_cache_precision",
    "requested_value_cache_precision",
    "activated_key_cache_precision",
    "activated_value_cache_precision",
    "observed_key_state_precision",
    "observed_value_state_precision",
    "attention_path",
    "requested_device",
    "actual_device",
)
_SHA256 = re.compile(r"^[0-9a-f]{64}$")


def _number(value: Any, field: str, *, positive: bool = False) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{field} must be numeric")
    result = float(value)
    if not math.isfinite(result) or result < 0 or (positive and result <= 0):
        comparison = "positive" if positive else "non-negative"
        raise ValueError(f"{field} must be finite and {comparison}")
    return result


def _integer(value: Any, field: str, *, positive: bool = False) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise ValueError(f"{field} must be an integer")
    if value < 0 or (positive and value <= 0):
        comparison = "positive" if positive else "non-negative"
        raise ValueError(f"{field} must be {comparison}")
    return value


def _sha256(value: Any, field: str) -> str:
    if not isinstance(value, str) or _SHA256.fullmatch(value) is None:
        raise ValueError(f"{field} must be a lowercase SHA256")
    return value


def _text(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} must be non-blank")
    return value


def _equal(left: float, right: float) -> bool:
    return math.isclose(left, right, rel_tol=1e-9, abs_tol=1e-9)


def _utilization(
    sample_index: int,
    family: str,
    raw: Any,
) -> list[float]:
    if not isinstance(raw, Mapping):
        raise ValueError(f"sample {sample_index} missing {family}")
    if raw.get("query_succeeded") is not True:
        raise ValueError(f"{family} query did not succeed")
    values = raw.get("values")
    if not isinstance(values, list) or not values:
        raise ValueError(f"{family}.values are required")
    normalized = [_number(value, f"{family}.values", positive=False) for value in values]
    if any(value > 100 for value in normalized):
        raise ValueError(f"{family}.values must be within 0-100")
    expected = {
        "mean": statistics.fmean(normalized),
        "median": statistics.median(normalized),
        "peak": max(normalized),
        "count": len(normalized),
    }
    for statistic in UTIL_STATS:
        if statistic not in raw:
            raise ValueError(f"{family}.{statistic} is required")
        if statistic == "count":
            actual = _integer(raw[statistic], f"{family}.count", positive=True)
            if actual != expected[statistic]:
                raise ValueError(f"{family}.count mismatch")
        else:
            actual = _number(raw[statistic], f"{family}.{statistic}")
            if not _equal(actual, float(expected[statistic])):
                raise ValueError(f"{family}.{statistic} mismatch")
    return normalized


def _activation(sample_index: int, raw: Any, sample: Mapping[str, Any]) -> dict[str, Any]:
    if not isinstance(raw, Mapping):
        raise ValueError(f"sample {sample_index} activation evidence is required")
    result = dict(raw)
    for field in ACTIVATION_TEXT_FIELDS:
        _text(raw.get(field), f"activation {field}")
    if not isinstance(raw.get("norm_correction"), bool):
        raise ValueError("activation norm correction must be boolean")
    if raw.get("fallback") is not False:
        raise ValueError("activation fallback is not permitted")
    if raw.get("output_valid") is not True:
        raise ValueError("activation output validity was not proved")
    if raw["requested_key_algorithm"] != raw["activated_key_algorithm"]:
        raise ValueError("activation key algorithm mismatch")
    if raw["requested_value_algorithm"] != raw["activated_value_algorithm"]:
        raise ValueError("activation value algorithm mismatch")
    for side in ("key", "value"):
        requested_precision = raw[f"requested_{side}_cache_precision"]
        activated_precision = raw[f"activated_{side}_cache_precision"]
        if (
            requested_precision != "plugin_default"
            and requested_precision != activated_precision
        ):
            raise ValueError(f"activation {side} cache precision mismatch")
    if raw["requested_device"] != raw["actual_device"]:
        raise ValueError("activation device fallback is not permitted")
    expected = _number(
        raw.get("expected_persistent_bytes"),
        "activation expected persistent bytes",
        positive=True,
    )
    actual = _number(
        raw.get("actual_persistent_bytes"),
        "activation actual persistent bytes",
        positive=True,
    )
    if (
        not _equal(expected, float(sample["expected_persistent_kv_bytes"]))
        or not _equal(actual, float(sample["actual_persistent_kv_bytes"]))
        or not _equal(expected, actual)
    ):
        raise ValueError("activation persistent KV byte reconciliation failed")
    expected_standard = _number(
        raw.get("expected_persistent_standard_bytes"),
        "activation expected persistent STANDARD bytes",
    )
    actual_standard = _number(
        raw.get("actual_persistent_standard_bytes"),
        "activation actual persistent STANDARD bytes",
    )
    if (
        not _equal(expected_standard, actual_standard)
        or not _equal(actual_standard, float(sample["standard_kv_bytes"]))
    ):
        raise ValueError("activation persistent STANDARD byte reconciliation failed")
    return result


def _validate_sample(index: int, raw: Mapping[str, Any]) -> dict[str, Any]:
    sample = dict(raw)
    _text(sample.get("sample_id"), "sample id")
    for field in SCALARS:
        sample[field] = _number(
            sample.get(field),
            f"sample {index} {field}",
            positive=field in STRICTLY_POSITIVE,
        )
    before = sample["available_ram_before_mb"]
    minimum = sample["available_ram_min_mb"]
    after = sample["available_ram_after_mb"]
    if minimum > before or minimum > after:
        raise ValueError("available RAM minimum exceeds a boundary measurement")

    expected_gpu_total = (
        sample["gpu_dedicated_memory_peak_mb"]
        + sample["gpu_shared_memory_peak_mb"]
    )
    if not _equal(sample["gpu_memory_peak_mb"], expected_gpu_total):
        raise ValueError("GPU memory total does not equal dedicated plus shared memory")

    expected_kv = sample["expected_persistent_kv_bytes"]
    actual_kv = sample["actual_persistent_kv_bytes"]
    if (
        not _equal(expected_kv, actual_kv)
        or not _equal(
            actual_kv,
            sample["standard_kv_bytes"]
            + sample["payload_kv_bytes"]
            + sample["norm_kv_bytes"]
            + sample["metadata_kv_bytes"],
        )
        or not _equal(sample["kv_mb"], actual_kv / MIB)
    ):
        raise ValueError("persistent KV measurements do not reconcile")

    raw_utilization: dict[str, list[float]] = {}
    for family in UTILIZATION:
        raw_utilization[family] = _utilization(index, family, sample.get(family))
    sample["_raw_utilization"] = raw_utilization
    sample["activation"] = _activation(index, sample.get("activation"), sample)
    _sha256(sample.get("output_sha256"), "output SHA256")
    _sha256(sample.get("telemetry_sha256"), "telemetry SHA256")
    _text(sample.get("source"), "sample source")
    _sha256(sample.get("source_sha256"), "sample source SHA256")
    if sample.get("cleanup_process_count") != 0:
        raise ValueError("sample cleanup process count is not zero")
    return sample


def _scalar_summary(samples: Sequence[Mapping[str, Any]], field: str) -> dict[str, Any]:
    values = [float(sample[field]) for sample in samples]
    return {
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "min": min(values),
        "max": max(values),
        "count": len(values),
    }


def summarize_samples(
    samples: Sequence[Mapping[str, Any]],
    *,
    terminal: Mapping[str, str] | None = None,
) -> dict[str, Any]:
    """Validate three formal samples and recompute every published aggregate."""

    if terminal is not None:
        if samples:
            raise ValueError("terminal measurement cannot contain numeric samples")
        status = _text(terminal.get("status"), "terminal status")
        evidence = _text(terminal.get("evidence"), "terminal evidence")
        evidence_hash = terminal.get("evidence_sha256")
        if not isinstance(evidence_hash, str) or _SHA256.fullmatch(evidence_hash) is None:
            raise ValueError("terminal evidence SHA256 is required")
        return {
            "status": status,
            "evidence": evidence,
            "evidence_sha256": evidence_hash,
            "sample_count": 0,
        }
    if len(samples) != 3:
        raise ValueError("exactly three measured samples are required")
    validated = [_validate_sample(index, row) for index, row in enumerate(samples)]

    sample_ids = [sample["sample_id"] for sample in validated]
    if len(set(sample_ids)) != len(sample_ids):
        raise ValueError("duplicate sample id")
    source_hashes = [sample["source_sha256"] for sample in validated]
    if len(set(source_hashes)) != len(source_hashes):
        raise ValueError("duplicate sample source hash")

    activation_identity_fields = (
        *ACTIVATION_TEXT_FIELDS,
        "norm_correction",
        "fallback",
    )
    identities = {
        tuple(sample["activation"][field] for field in activation_identity_fields)
        for sample in validated
    }
    if len(identities) != 1:
        raise ValueError("activation identity differs between formal samples")

    result: dict[str, Any] = {
        "schema_version": 1,
        "status": "measured",
        "sample_count": 3,
    }
    for field in SCALARS:
        result[field] = _scalar_summary(validated, field)
    for family in UTILIZATION:
        values = [
            value
            for sample in validated
            for value in sample["_raw_utilization"][family]
        ]
        result[family] = {
            "mean": statistics.fmean(values),
            "median": statistics.median(values),
            "peak": max(values),
            "count": len(values),
            "query_succeeded": True,
            "values": values,
        }

    activations = [sample["activation"] for sample in validated]
    result["activation"] = {
        "sample_count": 3,
        "requested_pairs": sorted({
            (
                activation["requested_key_algorithm"],
                activation["requested_value_algorithm"],
            )
            for activation in activations
        }),
        "activated_pairs": [
            {"key": key, "value": value}
            for key, value in sorted({
                (
                    activation["activated_key_algorithm"],
                    activation["activated_value_algorithm"],
                )
                for activation in activations
            })
        ],
        "norm_correction": sorted({
            activation["norm_correction"] for activation in activations
        }),
        "attention_paths": sorted({
            activation["attention_path"] for activation in activations
        }),
        "requested_devices": sorted({
            activation["requested_device"] for activation in activations
        }),
        "actual_devices": sorted({
            activation["actual_device"] for activation in activations
        }),
        "fallback": False,
        "telemetry_sha256": [sample["telemetry_sha256"] for sample in validated],
    }
    result["output_sha256"] = [sample["output_sha256"] for sample in validated]
    result["sources"] = [
        {
            "sample_id": sample["sample_id"],
            "path": sample["source"],
            "sha256": sample["source_sha256"],
        }
        for sample in validated
    ]
    result["cleanup_process_count"] = 0
    return result
