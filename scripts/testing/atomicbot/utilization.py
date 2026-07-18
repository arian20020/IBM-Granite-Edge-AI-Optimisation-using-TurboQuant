"""Utilisation sample parsing and summary helpers for AtomicBot runs."""

from __future__ import annotations

import csv
import statistics
from pathlib import Path


def read_utilization_samples(path: Path) -> list[dict]:
    if not path.is_file():
        return []
    with path.open(encoding="utf-8-sig", newline="") as stream:
        rows = []
        for row in csv.DictReader(stream):
            try:
                rows.append({
                    "timestamp_utc": row["timestamp_utc"],
                    "cpu_percent": float(row["cpu_percent"]),
                    "gpu_percent": float(row["gpu_percent"]),
                    "gpu_engine_count": int(row["gpu_engine_count"]),
                })
            except (KeyError, TypeError, ValueError):
                continue
        return rows


def summarize_utilization(samples: list[dict]) -> dict:
    def stats(field: str) -> dict | None:
        values = [float(sample[field]) for sample in samples if sample.get(field) is not None]
        if not values:
            return None
        return {
            "mean": statistics.fmean(values),
            "median": statistics.median(values),
            "peak": max(values),
            "sample_count": len(values),
        }

    return {"cpu_percent": stats("cpu_percent"), "gpu_percent": stats("gpu_percent")}
