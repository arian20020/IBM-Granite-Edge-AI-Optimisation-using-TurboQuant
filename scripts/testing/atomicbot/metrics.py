"""AtomicBot runtime metric parsing and aggregation."""

from __future__ import annotations

import re
import statistics
from dataclasses import dataclass

from scripts.testing.parse_llama_measurement import LLAMA_KV_GROUP_RE, KV_SIZE_RE


MIB = 1024 * 1024
COMPACT_TIMING_RE = re.compile(
    r"\[\s*Prompt:\s*(?P<prompt>\d+(?:\.\d+)?)\s*t/s\s*\|\s*"
    r"Generation:\s*(?P<decode>\d+(?:\.\d+)?)\s*t/s\s*\]",
    re.IGNORECASE,
)


@dataclass(frozen=True)
class Availability:
    value: float | None
    status: str
    reason: str


@dataclass(frozen=True)
class RuntimeMetrics:
    peak_ram_mb: float | None
    peak_private_mb: float | None
    minimum_available_ram_mb: float | None
    kv_mb: float | None
    ttft_ms: float | None
    prompt_tps: float | None
    decode_tps: float | None
    gpu_dedicated_mb: Availability | None
    gpu_shared_mb: Availability | None
    valid: bool
    errors: tuple[str, ...]


def parse_runtime_metrics(events: list[dict]) -> RuntimeMetrics:
    working_sets = [event["working_set_bytes"] for event in events
                    if event.get("kind") == "memory" and "working_set_bytes" in event]
    private_values = [event["private_bytes"] for event in events
                      if event.get("kind") == "memory" and "private_bytes" in event]
    available_values = [event["available_ram_bytes"] for event in events
                        if event.get("kind") == "memory" and "available_ram_bytes" in event]
    timing_matches = []
    kv_values: list[float] = []
    kv_groups: set[tuple[float, int]] = set()
    for event in events:
        if event.get("kind") != "stderr":
            continue
        text = event.get("text", "")
        timing_matches.extend(COMPACT_TIMING_RE.finditer(text))
        group = LLAMA_KV_GROUP_RE.search(text)
        if group:
            kv_groups.add((float(group.group("size")), int(group.group("layers"))))
        match = KV_SIZE_RE.search(text)
        if match:
            kv_values.append(float(match.group("size")))
    ttft_values = [event["elapsed_ms"] for event in events
                   if event.get("kind") == "first_response_byte"]
    exit_codes = [event.get("exit_code") for event in events if event.get("kind") == "exit"]
    errors: list[str] = []
    if len(timing_matches) > 1:
        errors.append("ambiguous_timing")
    timing = timing_matches[0] if len(timing_matches) == 1 else None
    values = {
        "peak_ram_mb": max(working_sets) / MIB if working_sets else None,
        "peak_private_mb": max(private_values) / MIB if private_values else None,
        "minimum_available_ram_mb": min(available_values) / MIB if available_values else None,
        "kv_mb": sum(size for size, _ in kv_groups) if kv_groups else (sum(kv_values) if kv_values else None),
        "ttft_ms": min(ttft_values) if ttft_values else None,
        "prompt_tps": float(timing.group("prompt")) if timing else None,
        "decode_tps": float(timing.group("decode")) if timing else None,
    }
    for field, value in values.items():
        if value is None:
            errors.append(f"missing_{field}")
    if not exit_codes or exit_codes[-1] != 0:
        errors.append("nonzero_or_missing_exit")
    unavailable = Availability(None, "N/A", "collector-unavailable")
    return RuntimeMetrics(**values, gpu_dedicated_mb=unavailable,
                          gpu_shared_mb=unavailable, valid=not errors, errors=tuple(errors))


def aggregate_samples(samples: list[RuntimeMetrics]) -> dict[str, dict[str, float]]:
    if len(samples) != 3 or not all(sample.valid for sample in samples):
        raise ValueError("exactly three valid samples are required")
    fields = ("peak_ram_mb", "peak_private_mb", "minimum_available_ram_mb", "kv_mb",
              "ttft_ms", "prompt_tps", "decode_tps")
    result = {}
    for field in fields:
        values = [getattr(sample, field) for sample in samples]
        result[field] = {"median": statistics.median(values), "min": min(values), "max": max(values)}
    return result
