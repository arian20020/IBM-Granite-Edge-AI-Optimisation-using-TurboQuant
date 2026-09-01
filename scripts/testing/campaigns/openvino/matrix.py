"""Typed loader for the frozen official OpenVINO WB-04 matrix."""

from __future__ import annotations

import json
import re
from dataclasses import InitVar, dataclass
from pathlib import Path


PHASES = frozenset({"build", "conversion", "baseline", "capability", "formal"})
DEVICES = frozenset({"host", "cpu", "gpu"})
ALGORITHMS = frozenset({"standard", "scalar", "tbq3", "tbq4", "qjl", "polar", "frozen"})
PRECISIONS = frozenset({"dynamic", "f16", "bf16", "u8", "u4", "u3", "probe", "frozen"})
GUARDS = frozenset({"none", "ram-2048-mib"})
RUNTIME_ALGORITHMS = frozenset({"STANDARD", "TBQ3", "TBQ4"})
FROZEN_SOURCE_IDENTITY = {
    "path": "experiments/raw-results/openvino-turboquant/2026-07-30/provenance/source-identity-bounded.json",
    "sha256": "44f4bfc111c78258320c0788ddba288f0f54840845cefac6fdfcae9b772e6574",
    "scope": "declared campaign input; not row execution proof",
    "upstream_commit": "7dea0459b2ac7d8dfd877fd9df6737674fd8371d",
    "patch_commit": "00edae3bfd40a968c964ea4878128dceeeb22d1a",
    "derived_tree": "2e872dd4817c42d91cb7c3094954d7b56fa12b0a",
}
FROZEN_BUILD_IDENTITY = {
    "path": "experiments/raw-results/openvino-turboquant/2026-07-30/provenance/build-00edae3b-attempt-001/build-provenance.json",
    "sha256": "57fb318a55db56fb409a60f0b1d516a988f8543c0446153e63efdee3a748e262",
    "scope": "declared campaign input; not row execution proof",
    "status": "passed",
    "patch_commit": "00edae3bfd40a968c964ea4878128dceeeb22d1a",
}
FORMAL_METRICS = frozenset({
    "load_ms", "ttft_ms", "prompt_tps", "tpot_ms", "decode_tps",
    "generation_duration_ms", "peak_working_set_mb", "peak_private_mb",
    "available_ram_min_mb", "kv_mb", "gpu_memory_peak_mb",
    "cpu_percent", "gpu_percent",
})
PROPERTY_EXPECTED_REJECTION_IDS = frozenset(
    {f"OV-TQS-{index:02d}" for index in range(5, 13)}
    | {"OV-TQ-18", "OV-TQ-19", "OV-TQ-20"}
)
SEMANTIC_SCALAR_REJECTION_IDS = frozenset(
    {"OV-04", "OV-05", "OV-TQ-01", "OV-TQ-02"}
)
EXPECTED_REJECTION_IDS = (
    PROPERTY_EXPECTED_REJECTION_IDS | SEMANTIC_SCALAR_REJECTION_IDS
)
NORM_DISABLED_ABLATION_IDS = frozenset({"OV-TQ-11", "OV-TQ-12"})
COMPARISON_IDS = frozenset({"OV-11", "OV-12", "OV-13", "OV-TQ-21", "OV-TQ-22"})
COMPARISON_CONTEXTS = (512, 1024, 2048, 4096, 8192)
COMPARISON_RUN_ORDER = ("OV-11", "OV-TQ-22", "OV-TQ-21", "OV-12", "OV-13")
_COMPARISON_IDENTITIES = {
    "OV-11": ("u4", "standard", "standard", "f16", "f16", "STANDARD", "STANDARD", "stateful-standard", "stateful_sdpa_standard"),
    "OV-TQ-22": ("u8", "tbq3", "tbq3", "u3", "u3", "TBQ3", "TBQ3", "patched-stateful", "stateful_sdpa_reference_codec"),
    "OV-TQ-21": ("u8", "tbq4", "tbq4", "u4", "u4", "TBQ4", "TBQ4", "patched-stateful", "stateful_sdpa_reference_codec"),
    "OV-12": ("u8", "standard", "standard", "f16", "f16", "STANDARD", "STANDARD", "stateful-standard", "stateful_sdpa_standard"),
    "OV-13": ("f16", "standard", "standard", "f16", "f16", "STANDARD", "STANDARD", "stateful-standard", "stateful_sdpa_standard"),
}
_SHA256 = re.compile(r"^[0-9a-f]{64}$")


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
    key_cache_precision: str | None
    value_cache_precision: str | None
    requested_device: str | None
    runtime_key_algorithm: str | None
    runtime_value_algorithm: str | None
    norm_correction: bool | None
    attention_path: str | None
    execution_route: str | None
    expected_outcome: str | None
    suitable_host_required: bool | None
    numeric_generation_metrics_expected: bool | None
    artifact_id: InitVar[str | None] = None
    artifact_manifest_path: InitVar[str | None] = None
    artifact_manifest_sha256: InitVar[str | None] = None
    artifact_status: InitVar[str | None] = None
    artifact_terminal_path: InitVar[str | None] = None
    artifact_terminal_sha256: InitVar[str | None] = None

    def __post_init__(
        self,
        artifact_id: str | None,
        artifact_manifest_path: str | None,
        artifact_manifest_sha256: str | None,
        artifact_status: str | None,
        artifact_terminal_path: str | None,
        artifact_terminal_sha256: str | None,
    ) -> None:
        object.__setattr__(
            self,
            "_artifact_fields",
            {
                "artifact_id": artifact_id,
                "artifact_manifest_path": artifact_manifest_path,
                "artifact_manifest_sha256": artifact_manifest_sha256,
                "artifact_status": artifact_status,
                "artifact_terminal_path": artifact_terminal_path,
                "artifact_terminal_sha256": artifact_terminal_sha256,
            },
        )


def _artifact_field(name: str) -> property:
    return property(lambda self: self._artifact_fields[name])


for _artifact_name in (
    "artifact_id",
    "artifact_manifest_path",
    "artifact_manifest_sha256",
    "artifact_status",
    "artifact_terminal_path",
    "artifact_terminal_sha256",
):
    setattr(OpenVINOCase, _artifact_name, _artifact_field(_artifact_name))


@dataclass(frozen=True)
class OpenVINOExecutionContract:
    """One unambiguous translation from a controlled row to runtime behavior."""

    controlled_test_id: str
    execution_route: str
    expected_outcome: str
    runtime_key_algorithm: str
    runtime_value_algorithm: str
    norm_correction: bool
    attention_path: str
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

    scalar_is_upstream = (
        route == "upstream-scalar"
        or case.test_id in SEMANTIC_SCALAR_REJECTION_IDS
    )
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
    if route == "non-runtime":
        attention_path = "not-applicable-non-runtime"
    elif expected_outcome == "expected-rejection":
        attention_path = "not-produced-by-expected-rejection"
    elif route == "patched-stateful":
        attention_path = "stateful_sdpa_reference_codec"
    else:
        attention_path = "stateful_sdpa_standard"
    return OpenVINOExecutionContract(
        controlled_test_id=case.test_id,
        execution_route=route,
        expected_outcome=expected_outcome,
        runtime_key_algorithm=runtime_key,
        runtime_value_algorithm=runtime_value,
        norm_correction=norm_correction,
        attention_path=attention_path,
        suitable_host_required=case.model == "granite-8b",
        requires_actual_cache_precision_proof=route
        in {"upstream-scalar", "stateful-standard", "device-standard"},
        numeric_generation_metrics_expected=(
            case.phase in {"baseline", "formal"}
            and expected_outcome == "pass"
        ),
    )


def _load_payload(path: Path) -> dict:
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict):
        raise ValueError("matrix payload must be an object")
    if payload.get("source_identity") != FROZEN_SOURCE_IDENTITY:
        raise ValueError("source_identity does not match the frozen receipt reference")
    if payload.get("build_identity") != FROZEN_BUILD_IDENTITY:
        raise ValueError("build_identity does not match the frozen receipt reference")
    return payload


def load_matrix_metadata(path: Path) -> dict[str, dict[str, str]]:
    """Return the receipt references that bind a declared campaign input."""

    payload = _load_payload(path)
    return {
        "source_identity": dict(payload["source_identity"]),
        "build_identity": dict(payload["build_identity"]),
    }


def _validate_explicit_execution_contract(case: OpenVINOCase) -> None:
    contract = execution_contract(case)
    expected = {
        "key_cache_precision": case.k_precision,
        "value_cache_precision": case.v_precision,
        "requested_device": case.device.upper(),
        "runtime_key_algorithm": contract.runtime_key_algorithm,
        "runtime_value_algorithm": contract.runtime_value_algorithm,
        "norm_correction": contract.norm_correction,
        "attention_path": contract.attention_path,
        "execution_route": contract.execution_route,
        "expected_outcome": contract.expected_outcome,
        "suitable_host_required": contract.suitable_host_required,
        "numeric_generation_metrics_expected": contract.numeric_generation_metrics_expected,
    }
    for field, expected_value in expected.items():
        actual = getattr(case, field)
        if isinstance(expected_value, bool):
            if type(actual) is not bool or actual != expected_value:
                raise ValueError(f"{case.test_id} {field} does not match execution contract")
        elif actual != expected_value:
            raise ValueError(f"{case.test_id} {field} does not match execution contract")


def load_matrix(path: Path) -> list[OpenVINOCase]:
    payload = _load_payload(path)
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
            key_cache_precision=raw.get("key_cache_precision"),
            value_cache_precision=raw.get("value_cache_precision"),
            requested_device=raw.get("requested_device"),
            runtime_key_algorithm=raw.get("runtime_key_algorithm"),
            runtime_value_algorithm=raw.get("runtime_value_algorithm"),
            norm_correction=raw.get("norm_correction"),
            attention_path=raw.get("attention_path"),
            execution_route=raw.get("execution_route"),
            expected_outcome=raw.get("expected_outcome"),
            suitable_host_required=raw.get("suitable_host_required"),
            numeric_generation_metrics_expected=raw.get(
                "numeric_generation_metrics_expected"
            ),
            artifact_id=raw.get("artifact_id"),
            artifact_manifest_path=raw.get("artifact_manifest_path"),
            artifact_manifest_sha256=raw.get("artifact_manifest_sha256"),
            artifact_status=raw.get("artifact_status"),
            artifact_terminal_path=raw.get("artifact_terminal_path"),
            artifact_terminal_sha256=raw.get("artifact_terminal_sha256"),
        )
        _validate_explicit_execution_contract(case)
        cases.append(case)
    return cases


def _require_hash(value: str | None, field: str, test_id: str) -> None:
    if not isinstance(value, str) or _SHA256.fullmatch(value) is None:
        raise ValueError(f"{test_id} {field} must be a SHA-256 hex digest")


def _validate_comparison_artifact_bindings(
    cases: tuple[OpenVINOCase, ...],
) -> tuple[OpenVINOCase, ...]:
    for case in cases:
        if case.artifact_status == "artifact-preparation-terminal":
            if case.test_id != "OV-13":
                raise ValueError("only OV-13 may be an artifact-preparation terminal")
            if any(
                value is not None
                for value in (
                    case.artifact_id,
                    case.artifact_manifest_path,
                    case.artifact_manifest_sha256,
                )
            ):
                raise ValueError("FP16 preparation terminal cannot claim an artifact")
            if not isinstance(case.artifact_terminal_path, str) or not case.artifact_terminal_path:
                raise ValueError("OV-13 terminal receipt path is required")
            _require_hash(case.artifact_terminal_sha256, "terminal receipt hash", case.test_id)
            continue
        if case.artifact_status != "available":
            raise ValueError(f"{case.test_id} requires an available artifact binding")
        if not isinstance(case.artifact_id, str) or not case.artifact_id:
            raise ValueError(f"{case.test_id} artifact id is required")
        if not isinstance(case.artifact_manifest_path, str) or not case.artifact_manifest_path:
            raise ValueError(f"{case.test_id} artifact manifest path is required")
        _require_hash(case.artifact_manifest_sha256, "artifact manifest hash", case.test_id)
        if case.artifact_terminal_path is not None or case.artifact_terminal_sha256 is not None:
            raise ValueError(f"{case.test_id} available artifact cannot carry a terminal receipt")
    u8_bindings = {
        (
            case.artifact_id,
            case.artifact_manifest_path,
            case.artifact_manifest_sha256,
        )
        for case in cases
        if case.test_id in {"OV-12", "OV-TQ-21", "OV-TQ-22"}
    }
    if len(u8_bindings) != 1:
        raise ValueError(
            "OV-12, OV-TQ-21, and OV-TQ-22 must share the same U8 artifact binding"
        )
    return cases


def load_adaptive_comparison_matrix(path: Path) -> tuple[OpenVINOCase, ...]:
    """Load the isolated five-identity adaptive format comparison contract."""

    cases = tuple(load_matrix(path))
    if tuple(case.test_id for case in cases) != COMPARISON_RUN_ORDER:
        raise ValueError("adaptive comparison identities or run order are invalid")
    if any(case.contexts != COMPARISON_CONTEXTS for case in cases):
        raise ValueError("adaptive comparison contexts are invalid")
    if any(case.model != "granite-3b" or case.device != "cpu" for case in cases):
        raise ValueError("adaptive comparison must use Granite 3B on CPU")
    if any(case.guard != "ram-2048-mib" for case in cases):
        raise ValueError("adaptive comparison requires the 2048 MiB runtime guard")
    if any(
        case.phase != "formal"
        or not case.quality_required
        or not case.numeric_generation_metrics_expected
        or case.required_metrics != FORMAL_METRICS
        for case in cases
    ):
        raise ValueError("adaptive comparison formal quality and metrics are invalid")
    for case in cases:
        expected = _COMPARISON_IDENTITIES[case.test_id]
        actual = (
            case.weight_precision,
            case.k_algorithm,
            case.v_algorithm,
            case.k_precision,
            case.v_precision,
            case.runtime_key_algorithm,
            case.runtime_value_algorithm,
            case.execution_route,
            case.attention_path,
        )
        if actual != expected:
            raise ValueError(f"{case.test_id} adaptive comparison identity is invalid")
    return _validate_comparison_artifact_bindings(cases)
