"""Typed loader for the frozen official OpenVINO WB-04 matrix."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path


PHASES = frozenset({"build", "conversion", "baseline", "capability", "formal"})
DEVICES = frozenset({"host", "cpu", "gpu"})
ALGORITHMS = frozenset({"standard", "scalar", "tbq3", "tbq4", "qjl", "polar", "frozen"})
PRECISIONS = frozenset({"dynamic", "f16", "bf16", "u8", "u4", "u3", "probe", "frozen"})
GUARDS = frozenset({"none", "ram-2048-mib"})
FORMAL_METRICS = frozenset({
    "load_ms", "ttft_ms", "prompt_tps", "tpot_ms", "decode_tps",
    "generation_duration_ms", "peak_working_set_mb", "peak_private_mb",
    "available_ram_min_mb", "kv_mb", "gpu_memory_peak_mb",
    "cpu_percent", "gpu_percent",
})


@dataclass(frozen=True)
class OpenVINOCase:
    test_id: str
    phase: str
    description: str
    model: str
    weight_precision: str
    k_algorithm: str
    v_algorithm: str
    k_precision: str
    v_precision: str
    device: str
    contexts: tuple[int, ...]
    guard: str
    quality_required: bool
    required_metrics: frozenset[str]


def load_matrix(path: Path) -> list[OpenVINOCase]:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    cases: list[OpenVINOCase] = []
    seen: set[str] = set()
    for raw in payload.get("cases", []):
        test_id = raw["test_id"]
        if test_id in seen:
            raise ValueError(f"duplicate test id: {test_id}")
        seen.add(test_id)
        for field, allowed in (("phase", PHASES), ("device", DEVICES),
                               ("k_algorithm", ALGORITHMS), ("v_algorithm", ALGORITHMS),
                               ("k_precision", PRECISIONS), ("v_precision", PRECISIONS),
                               ("guard", GUARDS)):
            value = raw.get(field)
            if value not in allowed:
                raise ValueError(f"unknown {field}: {value}")
        metrics = frozenset(raw.get("required_metrics", ()))
        if raw["phase"] in {"baseline", "formal"} and metrics != FORMAL_METRICS:
            raise ValueError(f"{test_id} must require the complete formal metric set")
        cases.append(OpenVINOCase(
            test_id=test_id, phase=raw["phase"], description=raw["description"],
            model=raw.get("model", "diagnostic"),
            weight_precision=raw.get("weight_precision", "dynamic"),
            k_algorithm=raw["k_algorithm"], v_algorithm=raw["v_algorithm"],
            k_precision=raw["k_precision"], v_precision=raw["v_precision"],
            device=raw["device"], contexts=tuple(raw.get("contexts", ())),
            guard=raw["guard"], quality_required=bool(raw.get("quality_required", False)),
            required_metrics=metrics,
        ))
    return cases
