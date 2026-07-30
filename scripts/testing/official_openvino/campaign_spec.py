"""Generate fail-closed WB-04 worker specs from the frozen formal matrix."""

from __future__ import annotations

import hashlib
import re
import shutil
import tempfile
from dataclasses import asdict
from pathlib import Path
from typing import Any

from .matrix import OpenVINOCase, execution_contract, load_matrix
from .runtime_measurement import atomic_write_json, build_runtime_property_spec
from .workload import build_context_workload


SPEC_SCHEMA = "official-openvino-wb04-worker-spec/v1"
EXPECTED_REJECTIONS_SCHEMA = (
    "official-openvino-wb04-expected-rejections/v1"
)
_SAFE_TEST_ID = re.compile(r"^OV-(?:TQ-)?[0-9]{2}$")
_REJECTION_REASON = (
    "matrix execution contract declares expected-rejection; "
    "worker spec generation is prohibited"
)


def _directory(path: Path, field: str) -> Path:
    resolved = Path(path).resolve()
    if not resolved.is_dir():
        raise ValueError(f"{field} directory is missing: {resolved}")
    return resolved


def _build_root(path: Path) -> Path:
    root = _directory(path, "build root")
    package = _directory(root / "openvino_genai", "OpenVINO GenAI package")
    initializer = package / "__init__.py"
    if not initializer.is_file():
        raise ValueError(
            f"OpenVINO GenAI package initializer is missing: {initializer}"
        )
    modules = sorted(package.glob("py_openvino_genai*.pyd"))
    if len(modules) != 1 or modules[0].stat().st_size <= 0:
        raise ValueError(
            "build root must contain exactly one non-empty "
            "py_openvino_genai*.pyd"
        )
    runtime = package / "openvino_genai.dll"
    if not runtime.is_file():
        raise ValueError(f"openvino_genai.dll is missing: {runtime}")
    return root


def _paths_overlap(first: Path, second: Path) -> bool:
    return (
        first == second
        or first in second.parents
        or second in first.parents
    )


def _fresh_output_root(
    path: Path,
    *,
    protected_paths: tuple[tuple[str, Path], ...],
) -> Path:
    output = Path(path).resolve()
    for field, protected in protected_paths:
        if _paths_overlap(output, protected.resolve()):
            raise ValueError(
                f"output root must not overlap {field}: {protected}"
            )
    if output.exists():
        raise ValueError(
            "output root must not exist; empty-directory reuse is "
            f"prohibited: {output}"
        )
    output.parent.mkdir(parents=True, exist_ok=True)
    return output


def _matrix_case_record(case: OpenVINOCase) -> dict[str, Any]:
    value = asdict(case)
    value["contexts"] = list(case.contexts)
    value["required_metrics"] = sorted(case.required_metrics)
    return value


def _selected_cases(
    matrix_path: Path,
    *,
    model_paths_by_weight_precision: dict[str, Path],
    include_baselines: bool,
) -> tuple[Path, list[OpenVINOCase]]:
    source = Path(matrix_path).resolve()
    if not source.is_file():
        raise ValueError(f"retest matrix file is missing: {source}")
    selected = [
        case
        for case in load_matrix(source)
        if (
            case.phase == "formal"
            or (include_baselines and case.phase == "baseline")
        )
        and case.model == "granite-3b"
        and case.weight_precision in model_paths_by_weight_precision
    ]
    if not selected:
        raise ValueError(
            "retest matrix has no formal Granite 3B campaign rows for the "
            "supplied model precision bindings"
        )
    return source, selected


def generate_formal_u8_granite3b_specs(
    *,
    matrix_path: Path,
    build_root: Path,
    model_path: Path,
    u4_model_path: Path | None = None,
    cache_root: Path,
    output_root: Path,
    include_baselines: bool = False,
) -> dict[str, object]:
    """Write one sequence template per runnable formal row/context.

    Expected-rejection rows are recorded separately and can never reach the
    measurement worker. Supplying ``u4_model_path`` opts in to U4 rows and
    binds them only to that directory. ``include_baselines`` additionally
    selects runnable Granite 3B baseline rows while preserving the formal-only
    default.
    """

    if type(include_baselines) is not bool:
        raise ValueError("include_baselines must be a boolean")
    build = _build_root(build_root)
    model_paths_by_weight_precision = {
        "u8": _directory(model_path, "model"),
    }
    if u4_model_path is not None:
        u4_model = _directory(u4_model_path, "U4 model")
        if u4_model == model_paths_by_weight_precision["u8"]:
            raise ValueError(
                "U4 model directory must differ from the U8 model directory"
            )
        model_paths_by_weight_precision["u4"] = u4_model
    cache = _directory(cache_root, "cache")
    matrix, cases = _selected_cases(
        matrix_path,
        model_paths_by_weight_precision=model_paths_by_weight_precision,
        include_baselines=include_baselines,
    )
    output = _fresh_output_root(
        output_root,
        protected_paths=(
            ("matrix", matrix),
            ("build root", build),
            ("U8 model", model_paths_by_weight_precision["u8"]),
            *(
                (("U4 model", model_paths_by_weight_precision["u4"]),)
                if "u4" in model_paths_by_weight_precision
                else ()
            ),
            ("cache root", cache),
        ),
    )

    planned_specs: list[tuple[Path, dict[str, Any]]] = []
    rejections: list[dict[str, Any]] = []
    for case in cases:
        if _SAFE_TEST_ID.fullmatch(case.test_id) is None:
            raise ValueError(f"unsafe formal test id: {case.test_id}")
        contract = execution_contract(case)
        if contract.expected_outcome == "expected-rejection":
            rejections.append(
                {
                    "matrix_case": _matrix_case_record(case),
                    "execution_contract": asdict(contract),
                    "reason": _REJECTION_REASON,
                }
            )
            continue
        if (
            contract.expected_outcome != "pass"
            or not contract.numeric_generation_metrics_expected
        ):
            raise ValueError(
                f"{case.test_id} has no runnable formal execution contract"
            )

        case_model = model_paths_by_weight_precision.get(case.weight_precision)
        if case_model is None:
            raise ValueError(
                f"{case.test_id} has no model binding for weight precision "
                f"{case.weight_precision}"
            )
        for context in case.contexts:
            workload = build_context_workload(context)
            campaign_cache = (
                cache / case.test_id / f"context-{context}"
            ).resolve()
            runtime_device = (
                "GPU.0"
                if (
                    case.execution_route == "device-standard"
                    and case.requested_device == "GPU"
                )
                else case.device
            )
            runtime = build_runtime_property_spec(
                device=runtime_device,
                key_algorithm=contract.runtime_key_algorithm,
                value_algorithm=contract.runtime_value_algorithm,
                key_cache_precision=case.k_precision,
                value_cache_precision=case.v_precision,
                norm_correction=contract.norm_correction,
                cache_dir=str(campaign_cache),
            )
            spec = {
                "schema": SPEC_SCHEMA,
                "role": "pilot",
                "controlled_test_id": case.test_id,
                "context": context,
                "expected_input_tokens": workload[
                    "expected_input_tokens"
                ],
                "model_path": str(case_model),
                "device": runtime["device"],
                "prompt": workload["prompt"],
                "max_new_tokens": 4,
                "ignore_eos": True,
                "seed": 42,
                "apply_chat_template": False,
                "properties": runtime["properties"],
            }
            destination = (
                Path(case.test_id)
                / f"context-{context}"
                / "spec.json"
            )
            planned_specs.append((destination, spec))

    if not planned_specs:
        raise ValueError("selected matrix rows produced no runnable worker specs")

    staging = Path(
        tempfile.mkdtemp(
            dir=output.parent,
            prefix=f".{output.name}.",
        )
    )
    rejection_path = output / "expected-rejections.json"
    try:
        for relative_path, spec in planned_specs:
            atomic_write_json(staging / relative_path, spec)
        atomic_write_json(
            staging / rejection_path.name,
            {
                "schema": EXPECTED_REJECTIONS_SCHEMA,
                "matrix_sha256": hashlib.sha256(
                    matrix.read_bytes()
                ).hexdigest(),
                "rejections": rejections,
            },
        )
        try:
            staging.rename(output)
        except OSError as error:
            if output.exists():
                raise FileExistsError(
                    "refusing to publish specs over a concurrently created "
                    f"output root: {output}"
                ) from error
            raise
    except BaseException:
        if staging.exists():
            shutil.rmtree(staging)
        raise
    result: dict[str, object] = {
        "build_root": str(build),
        "model_path": str(model_paths_by_weight_precision["u8"]),
        "output_root": str(output),
        "spec_count": len(planned_specs),
        "expected_rejection_count": len(rejections),
        "spec_paths": [
            str(output / relative_path)
            for relative_path, _ in planned_specs
        ],
        "expected_rejections_path": str(rejection_path),
    }
    if "u4" in model_paths_by_weight_precision:
        result["u4_model_path"] = str(model_paths_by_weight_precision["u4"])
    return result


__all__ = ["generate_formal_u8_granite3b_specs"]
