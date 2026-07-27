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
RUNTIME_ALGORITHMS = frozenset({"STANDARD", "TBQ3", "TBQ4"})
FORMAL_METRICS = frozenset({
    "load_ms", "ttft_ms", "prompt_tps", "tpot_ms", "decode_tps",
    "generation_duration_ms", "peak_working_set_mb", "peak_private_mb",
    "available_ram_min_mb", "kv_mb", "gpu_memory_peak_mb",
    "cpu_percent", "gpu_percent",
})
EXPECTED_REJECTION_IDS = frozenset(
    {f"OV-TQS-{index:02d}" for index in range(5, 13)}
    | {"OV-TQ-18", "OV-TQ-19", "OV-TQ-20"}
)
NORM_DISABLED_ABLATION_IDS = frozenset({"OV-TQ-11", "OV-TQ-12"})


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


@dataclass(frozen=True)
class OpenVINOExecutionContract:
    """One unambiguous translation from a controlled row to runtime behavior."""

    controlled_test_id: str
    execution_route: str
    expected_outcome: str
    runtime_key_algorithm: str
    runtime_value_algorithm: str
    norm_correction: bool
    suitable_host_required: bool
    requires_actual_cache_precision_proof: bool
    numeric_generation_metrics_expected: bool


def _runtime_algorithm(label: str, *, scalar_is_upstream: bool) -> str:
    if label == "standard" or label == "frozen":
        return "STANDARD"
    if label == "scalar":
        return "STANDARD" if scalar_is_upstream else "SCALAR"
    return label.upper()


def execution_contract(case: OpenVINOCase) -> OpenVINOExecutionContract:
    """Translate a frozen WB-04 row without conflating precision dimensions."""

    expected_outcome = (
        "expected-rejection"
        if case.test_id in EXPECTED_REJECTION_IDS
        else "pass"
    )
    algorithms = {case.k_algorithm, case.v_algorithm}
    if case.phase in {"build", "conversion"}:
        route = "non-runtime"
    elif expected_outcome == "expected-rejection":
        route = "expected-rejection"
    elif algorithms == {"scalar"}:
        route = "upstream-scalar"
    elif algorithms == {"frozen"}:
        route = "device-standard"
    elif algorithms & {"tbq3", "tbq4"}:
        route = "patched-stateful"
    else:
        route = "stateful-standard"

    scalar_is_upstream = route == "upstream-scalar"
    runtime_key = _runtime_algorithm(
        case.k_algorithm, scalar_is_upstream=scalar_is_upstream
    )
    runtime_value = _runtime_algorithm(
        case.v_algorithm, scalar_is_upstream=scalar_is_upstream
    )
    if expected_outcome == "pass" and route == "patched-stateful":
        if runtime_key not in RUNTIME_ALGORITHMS or runtime_value not in RUNTIME_ALGORITHMS:
            raise ValueError(
                f"{case.test_id} cannot silently map scalar state to STANDARD"
            )

    turboquant_requested = bool(algorithms & {"tbq3", "tbq4"})
    norm_correction = (
        turboquant_requested
        and case.test_id not in NORM_DISABLED_ABLATION_IDS
        and case.test_id not in {"OV-B08", "OV-B09", "OV-B10", "OV-B12"}
    )
    return OpenVINOExecutionContract(
        controlled_test_id=case.test_id,
        execution_route=route,
        expected_outcome=expected_outcome,
        runtime_key_algorithm=runtime_key,
        runtime_value_algorithm=runtime_value,
        norm_correction=norm_correction,
        suitable_host_required=case.model == "granite-8b",
        requires_actual_cache_precision_proof=route
        in {"upstream-scalar", "stateful-standard", "device-standard"},
        numeric_generation_metrics_expected=(
            case.phase in {"baseline", "formal"}
            and expected_outcome == "pass"
        ),
    )


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
        model = raw.get("model", "diagnostic")
        quality_required = (
            model.startswith("granite-")
            and raw["phase"] in {"baseline", "formal"}
        )
        if bool(raw.get("quality_required", False)) != quality_required:
            raise ValueError(
                f"{test_id} quality_required must cover every Granite runtime candidate"
            )
        case = OpenVINOCase(
            test_id=test_id, phase=raw["phase"], description=raw["description"],
            model=model,
            weight_precision=raw.get("weight_precision", "dynamic"),
            k_algorithm=raw["k_algorithm"], v_algorithm=raw["v_algorithm"],
            k_precision=raw["k_precision"], v_precision=raw["v_precision"],
            device=raw["device"], contexts=tuple(raw.get("contexts", ())),
            guard=raw["guard"], quality_required=quality_required,
            required_metrics=metrics,
        )
        execution_contract(case)
        cases.append(case)
    return cases
