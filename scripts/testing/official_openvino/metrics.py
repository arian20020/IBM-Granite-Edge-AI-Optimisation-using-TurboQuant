"""Complete measurement aggregation for official OpenVINO runs."""

from __future__ import annotations

import statistics
from collections.abc import Mapping, Sequence
from typing import Any


SCALARS = (
    "load_ms", "ttft_ms", "prompt_tps", "tpot_ms", "decode_tps",
    "generation_duration_ms", "peak_working_set_mb", "peak_private_mb",
    "available_ram_min_mb", "kv_mb", "gpu_memory_peak_mb",
)
UTILIZATION = ("cpu_percent", "gpu_percent")
UTIL_STATS = ("mean", "median", "peak", "count")


def summarize_samples(samples: Sequence[Mapping[str, Any]], *,
                      terminal: Mapping[str, str] | None = None) -> dict[str, Any]:
    if terminal is not None:
        if samples or not terminal.get("status") or not terminal.get("evidence"):
            raise ValueError("terminal measurement requires no samples, status, and evidence")
        return dict(terminal)
    if len(samples) != 3:
        raise ValueError("exactly three measured samples are required")
    for index, row in enumerate(samples):
        for field in SCALARS:
            if not isinstance(row.get(field), (int, float)):
                raise ValueError(f"sample {index} missing {field}")
        for family in UTILIZATION:
            values = row.get(family)
            if not isinstance(values, Mapping):
                raise ValueError(f"sample {index} missing {family}")
            for statistic in UTIL_STATS:
                if not isinstance(values.get(statistic), (int, float)):
                    raise ValueError(f"{family}.{statistic} is required")
    result: dict[str, Any] = {"status": "measured", "sample_count": 3}
    for field in SCALARS:
        values = [float(row[field]) for row in samples]
        result[field] = {"mean": statistics.fmean(values),
                         "median": statistics.median(values),
                         "min": min(values), "max": max(values), "count": 3}
    for family in UTILIZATION:
        result[family] = {
            "mean": statistics.fmean(float(row[family]["mean"]) for row in samples),
            "median": statistics.median(float(row[family]["median"]) for row in samples),
            "peak": max(float(row[family]["peak"]) for row in samples),
            "count": int(sum(int(row[family]["count"]) for row in samples)),
        }
    return result
