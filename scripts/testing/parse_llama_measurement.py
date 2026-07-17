"""Validate and summarize raw llama measurement events."""

from __future__ import annotations

import re
import statistics


MIB = 1024 * 1024
KV_SIZE_RE = re.compile(
    r"(?:\bK cache size|\bV cache size|\bKV(?: buffer| cache)?(?: size)?|llama_kv_cache:.*?size)\s*=\s*"
    r"(?P<size>\d+(?:\.\d+)?)\s*MiB",
    re.IGNORECASE,
)
LLAMA_KV_GROUP_RE = re.compile(
    r"llama_kv_cache:\s*size\s*=\s*(?P<size>\d+(?:\.\d+)?)\s*MiB"
    r".*?(?P<layers>\d+)\s+layers",
    re.IGNORECASE,
)


def summarize_measurement(events: list[dict]) -> dict:
    """Return validated peak RAM, allocated KV memory, and TTFT."""
    memory_values = [
        event["private_bytes"]
        for event in events
        if event.get("kind") == "memory" and "private_bytes" in event
    ]
    kv_values: list[float] = []
    kv_groups: set[tuple[float, int]] = set()
    for event in events:
        if event.get("kind") != "stderr":
            continue
        text = event.get("text", "")
        group_match = LLAMA_KV_GROUP_RE.search(text)
        if group_match:
            kv_groups.add(
                (float(group_match.group("size")), int(group_match.group("layers")))
            )
        match = KV_SIZE_RE.search(text)
        if match:
            kv_values.append(float(match.group("size")))

    ttft_values = [
        event["elapsed_ms"]
        for event in events
        if event.get("kind") == "first_response_byte"
    ]
    exit_codes = [
        event.get("exit_code") for event in events if event.get("kind") == "exit"
    ]

    summary = {
        "peak_ram_mb": max(memory_values) / MIB if memory_values else None,
        "kv_mb": (
            sum(size for size, _ in kv_groups)
            if kv_groups
            else (sum(kv_values) if kv_values else None)
        ),
        "ttft_ms": min(ttft_values) if ttft_values else None,
        "exit_code": exit_codes[-1] if exit_codes else None,
    }
    summary["missing"] = [
        field
        for field in ("peak_ram_mb", "kv_mb", "ttft_ms")
        if summary[field] is None
    ]
    summary["valid"] = summary["exit_code"] == 0 and not summary["missing"]
    return summary


def median_valid(samples: list[dict]) -> dict:
    """Compute medians only when exactly three complete samples are supplied."""
    if len(samples) != 3 or not all(sample.get("valid") for sample in samples):
        raise ValueError("exactly three valid samples are required")
    return {
        field: statistics.median(sample[field] for sample in samples)
        for field in ("peak_ram_mb", "kv_mb", "ttft_ms")
    }
