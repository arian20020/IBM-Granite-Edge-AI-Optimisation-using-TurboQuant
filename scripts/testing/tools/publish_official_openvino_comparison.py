"""Publish evidence-bound WB-04 adaptive comparison checkpoints.

The public transaction is implemented below the deterministic register projection.
No function in this module launches a model or discovers evidence directories.
"""

from __future__ import annotations

import argparse
import contextlib
import csv
import hashlib
import importlib.util
import io
import json
import math
import os
import shutil
import statistics
import sys
import tempfile
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass, replace
from pathlib import Path
from typing import Any

from scripts.testing.tools.finalize_official_openvino_comparison_workbook import (
    render_v19_workbook,
    validate_comparison_workbook_text,
)
from scripts.testing.campaigns.openvino.comparison_reconcile import (
    ComparisonKey,
    ComparisonQualityOutcome,
    ComparisonRelease,
    ComparisonRuntimeOutcome,
    load_comparison_release_input,
    reconcile_comparison_release,
    validate_closed_campaign,
    validate_complete_release,
)
from scripts.testing.campaigns.openvino.matrix import (
    COMPARISON_CONTEXTS as MATRIX_COMPARISON_CONTEXTS,
    COMPARISON_IDS as MATRIX_COMPARISON_IDS,
    COMPARISON_RUN_ORDER,
    OpenVINOCase,
    load_adaptive_comparison_matrix,
)


REGISTER_PATHS = (
    "docs/testing/OpenVINO-Codec-Traceability-Extension-v1.1.csv",
    "docs/testing/Configuration-Register.csv",
    "docs/testing/Test-Run-Register.csv",
    "docs/testing/Performance-Measurement-Register.csv",
    "docs/testing/Device-Verification-Register.csv",
    "docs/testing/Quality-Evaluation-Register.csv",
    "docs/testing/Failure-Register.csv",
    "docs/testing/Evidence-Index.csv",
    "docs/testing/Workbook-Completion-Register.csv",
    "docs/testing/Workbook-Revision-Register.csv",
    "docs/testing/workbooks/Controlled-Workbook-Manifest.csv",
)

MARKDOWN_PATH = (
    "docs/testing/workbooks/text-templates/"
    "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md"
)
DOCX_PATH = (
    "docs/testing/workbooks/generated/"
    "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx"
)
MANIFEST_PATH = REGISTER_PATHS[-1]
PUBLICATION_PATHS = (
    MARKDOWN_PATH,
    DOCX_PATH,
    *REGISTER_PATHS[:-1],
    MANIFEST_PATH,
    "publication-state.json",
)
HISTORICAL_MATRIX_SHA256 = (
    "7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42"
)
_PUBLICATION_STATE_SCHEMA = "official-openvino-comparison-publication-state/v1"

PROMPT_IDS = ("P1", "P2", "P3", "P4", "P5", "P6")
PROMPT_SET_ID = "GTQ-PROMPTS-v1"
RUBRIC_ID = "GTQ-QUALITY-RUBRIC-v1"
ROUTE = "official-openvino-turboquant"
WORKBOOK_ID = "WB-04"
MIB = 1024**2
_HEX = frozenset("0123456789abcdef")


def _sha256(value: object, label: str) -> str:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in _HEX for character in value)
    ):
        raise ValueError(f"{label} must be a lowercase SHA-256")
    return value


def _finite(value: object, label: str) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"{label} must be numeric")
    number = float(value)
    if not math.isfinite(number):
        raise ValueError(f"{label} must be finite")
    return number


def _number(value: object, label: str) -> str:
    number = _finite(value, label)
    if number == 0:
        return "0"
    return format(number, ".15g")


def _bytes_from_mib(value: object, label: str) -> str:
    number = _finite(value, label)
    byte_count = number * MIB
    rounded = round(byte_count)
    if not math.isclose(byte_count, rounded, rel_tol=0.0, abs_tol=1e-6):
        raise ValueError(f"{label} does not map to an exact byte count")
    return str(rounded)


def _path(value: object, label: str) -> str:
    path = Path(value) if isinstance(value, (str, Path)) else None
    if path is None or not path.as_posix().strip():
        raise ValueError(f"{label} is required")
    return path.as_posix()


def _case_value(case: object, name: str, *, required: bool = True) -> str:
    value = getattr(case, name, None)
    if value is None:
        if required:
            raise ValueError(f"matrix case {getattr(case, 'test_id', '?')} lacks {name}")
        return ""
    text = str(value).strip()
    if required and not text:
        raise ValueError(f"matrix case {getattr(case, 'test_id', '?')} has blank {name}")
    return text


def _validate_matrix_cases(matrix_cases: Sequence[OpenVINOCase]) -> dict[str, OpenVINOCase]:
    cases = tuple(matrix_cases)
    by_id = {str(getattr(case, "test_id", "")): case for case in cases}
    if (
        len(cases) != len(by_id)
        or set(by_id) != set(MATRIX_COMPARISON_IDS)
    ):
        raise ValueError("validated comparison matrix must declare exactly five unique identities")
    for test_id, case in by_id.items():
        contexts = tuple(getattr(case, "contexts", ()))
        if contexts != MATRIX_COMPARISON_CONTEXTS:
            raise ValueError(f"matrix case {test_id} must declare the exact adaptive contexts")
    return by_id


def _configuration_id(key: ComparisonKey) -> str:
    return f"WB04-CMP-{key.test_id}-{key.context_tokens}"


def _run_id(key: ComparisonKey) -> str:
    return f"{_configuration_id(key)}-RUN"


def _metric_value(
    outcome: ComparisonRuntimeOutcome, group: str, metric: str, statistic: str
) -> object:
    container = getattr(outcome, group)
    values = container.get(metric)
    if not isinstance(values, Mapping) or statistic not in values:
        raise ValueError(f"{outcome.key.test_id}@{outcome.key.context_tokens} lacks {metric} {statistic}")
    return values[statistic]


def _sample_metric(
    outcome: ComparisonRuntimeOutcome,
    sample: Mapping[str, Any],
    index: int,
    *,
    sample_field: str,
    group: str,
    summary_field: str,
) -> object:
    if sample_field in sample:
        return sample[sample_field]
    summary = getattr(outcome, group).get(summary_field)
    values = summary.get("values") if isinstance(summary, Mapping) else None
    if not isinstance(values, (list, tuple)) or len(values) != 3:
        raise ValueError(
            f"{outcome.key.test_id}@{outcome.key.context_tokens} lacks three {summary_field} values"
        )
    return values[index]


def _utilisation_fields(value: object, label: str) -> tuple[str, str, str, int] | None:
    if not isinstance(value, Mapping):
        return None
    raw = value.get("values")
    if not isinstance(raw, (list, tuple)) or not raw:
        raise ValueError(f"{label} observations are missing")
    values = [_finite(item, f"{label} observation") for item in raw]
    count = value.get("count")
    if count != len(values):
        raise ValueError(f"{label} sample count does not match observations")
    return (
        _number(statistics.fmean(values), f"{label} mean"),
        _number(statistics.median(values), f"{label} median"),
        _number(max(values), f"{label} peak"),
        len(values),
    )


def _activation(outcome: ComparisonRuntimeOutcome) -> Mapping[str, Any]:
    telemetry = outcome.activation.get("telemetry")
    if not isinstance(telemetry, Mapping):
        raise ValueError(f"{outcome.key.test_id}@{outcome.key.context_tokens} lacks activation telemetry")
    return telemetry


def _device(outcome: ComparisonRuntimeOutcome, field: str) -> str:
    telemetry = _activation(outcome)
    direct = telemetry.get(f"{field}_device")
    if isinstance(direct, str) and direct.strip():
        return direct.strip()
    device = outcome.activation.get("device")
    if isinstance(device, Mapping):
        value = device.get(field)
        if isinstance(value, str) and value.strip():
            return value.strip()
    raise ValueError(f"{outcome.key.test_id}@{outcome.key.context_tokens} lacks {field} device")


def _fallback(outcome: ComparisonRuntimeOutcome) -> str:
    value = outcome.activation.get("fallback", _activation(outcome).get("fallback"))
    if not isinstance(value, bool):
        raise ValueError(f"{outcome.key.test_id}@{outcome.key.context_tokens} lacks fallback proof")
    return "True" if value else "False"


def _observed_precision(outcome: ComparisonRuntimeOutcome, side: str) -> str:
    value = _activation(outcome).get(f"observed_{side}_state_precision")
    if not isinstance(value, str) or not value.strip():
        raise ValueError(
            f"{outcome.key.test_id}@{outcome.key.context_tokens} lacks observed {side} precision"
        )
    return value.strip()


def _algorithm(outcome: ComparisonRuntimeOutcome, side: str) -> str:
    value = _activation(outcome).get(f"activated_{side}_algorithm")
    if not isinstance(value, str) or not value.strip():
        raise ValueError(
            f"{outcome.key.test_id}@{outcome.key.context_tokens} lacks activated {side} algorithm"
        )
    return value.strip()


def _optional_telemetry_text(outcome: ComparisonRuntimeOutcome, field: str) -> str:
    value = _activation(outcome).get(field)
    if value is None:
        return ""
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{outcome.key.test_id}@{outcome.key.context_tokens} has invalid {field}")
    return value.strip()


def _split_gpu_memory_mib(outcome: ComparisonRuntimeOutcome) -> tuple[list[float], list[float]]:
    if len(outcome.samples) != 3:
        raise ValueError("accepted runtime must expose exactly three samples")
    dedicated: list[float] = []
    shared: list[float] = []
    for index, sample in enumerate(outcome.samples, start=1):
        for field, target in (
            ("gpu_dedicated_memory_peak_mb", dedicated),
            ("gpu_shared_memory_peak_mb", shared),
        ):
            if field not in sample:
                raise ValueError(
                    f"{outcome.key.test_id}@{outcome.key.context_tokens} sample {index} lacks {field}"
                )
            target.append(_finite(sample[field], field))
    return dedicated, shared


def _consistent_sample_integer(
    outcome: ComparisonRuntimeOutcome, field: str, label: str
) -> int:
    values = [sample.get(field) for sample in outcome.samples]
    if (
        len(values) != 3
        or any(isinstance(value, bool) or not isinstance(value, int) or value <= 0 for value in values)
        or len(set(values)) != 1
    ):
        raise ValueError(
            f"{label} must be one consistent positive integer across three formal samples"
        )
    return int(values[0])


def _runtime_note(outcome: ComparisonRuntimeOutcome) -> str:
    cpu = outcome.utilisation.get("cpu_percent")
    gpu = outcome.utilisation.get("gpu_percent")
    gpu_memory = outcome.memory.get("gpu_memory_peak_mib")
    if not all(isinstance(value, Mapping) for value in (cpu, gpu, gpu_memory)):
        raise ValueError("runtime utilisation/memory summaries are incomplete")
    dedicated, shared = _split_gpu_memory_mib(outcome)
    placements_missing = not (
        _optional_telemetry_text(outcome, "actual_model_layer_placement")
        and _optional_telemetry_text(outcome, "actual_kv_placement")
    )
    parts = [
            f"context_tokens={outcome.key.context_tokens}",
            f"runtime_evidence_sha256={_sha256(outcome.evidence_sha256, 'runtime evidence SHA-256')}",
            f"runtime_prompt_sha256={_sha256(outcome.identity_hashes.get('prompt_sha256'), 'runtime prompt SHA-256')}",
            f"cpu_median_percent={_number(cpu.get('median'), 'CPU median')}",
            f"cpu_sample_count={int(cpu.get('count'))}",
            f"gpu_median_percent={_number(gpu.get('median'), 'GPU median')}",
            f"gpu_sample_count={int(gpu.get('count'))}",
            f"gpu_dedicated_peak_mib={_number(max(dedicated), 'dedicated GPU memory peak')}",
            f"gpu_shared_peak_mib={_number(max(shared), 'shared GPU memory peak')}",
            f"gpu_memory_peak_mib={_number(gpu_memory.get('worst_max'), 'GPU memory peak')}",
    ]
    if placements_missing:
        parts.append(
            "model-layer and KV placement are not exposed by the validated activation telemetry; compatible placement cells remain empty"
        )
    return "; ".join(parts)


def _traceability_rows(cases: Mapping[str, OpenVINOCase]) -> list[dict[str, str]]:
    return [
        {
            "Test_ID": test_id,
            "Route": ROUTE,
            "Category": "Adaptive format comparison",
            "Test_Title": _case_value(cases[test_id], "description"),
            "Research_Question": "RQ1;RQ2",
            "Workbook_ID": WORKBOOK_ID,
            "Workbook_Section": test_id,
        }
        for test_id in COMPARISON_RUN_ORDER
    ]


def _configuration_rows(
    cases: Mapping[str, OpenVINOCase], matrix_path: Path, matrix_sha256: str
) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    for test_id in COMPARISON_RUN_ORDER:
        case = cases[test_id]
        for context in tuple(getattr(case, "contexts", ())):
            key = ComparisonKey(test_id, context)
            algorithm = _case_value(case, "k_algorithm").upper()
            rows.append(
                {
                    "Configuration_ID": _configuration_id(key),
                    "Test_ID": test_id,
                    "Route": ROUTE,
                    "Workbook_ID": WORKBOOK_ID,
                    "Model": _case_value(case, "model"),
                    "Model_ID": _case_value(case, "model"),
                    "Weights": _case_value(case, "weight_precision"),
                    "K_Cache": _case_value(case, "k_precision"),
                    "V_Cache": _case_value(case, "v_precision"),
                    "Requested_Device": _case_value(case, "requested_device"),
                    "Requested_Execution_or_Offload": _case_value(case, "execution_route"),
                    "Context_Target_Tokens": str(context),
                    "Purpose": "Guarded same-artifact adaptive format comparison",
                    "Prompt_Set_ID": PROMPT_SET_ID,
                    "Rubric_ID": RUBRIC_ID,
                    "Seed": "",
                    "Temperature": "",
                    "Top_P": "",
                    "Max_Output_Tokens": "",
                    "Threads": "",
                    "Flash_Attention": "",
                    "Optimisation_Name": algorithm,
                    "Optimisation_Requested": algorithm,
                    "Comparison_Group": "WB04-ADAPTIVE-V1.9",
                    "Safety_Gate": _case_value(case, "guard"),
                    "Source_Workbook_Table": "Adaptive comparison matrix",
                    "Status": "Declared",
                    "Missing_Data_Codes": "",
                    "Notes": (
                        f"matrix={Path(matrix_path).as_posix()}#sha256={matrix_sha256}; "
                        f"context_tokens={context}"
                    ),
                }
            )
    return rows


def _performance_rows(outcome: ComparisonRuntimeOutcome, evidence_commit: str) -> list[dict[str, str]]:
    if len(outcome.samples) != 3:
        raise ValueError("accepted runtime must expose exactly three samples")
    dedicated_gpu, shared_gpu = _split_gpu_memory_mib(outcome)
    rows: list[dict[str, str]] = []
    for index, raw_sample in enumerate(outcome.samples):
        sample = dict(raw_sample)
        repetition = index + 1
        cpu = _utilisation_fields(sample.get("cpu_percent"), "CPU utilisation")
        gpu = _utilisation_fields(sample.get("gpu_percent"), "GPU utilisation")
        source = sample.get("source", outcome.evidence_path)
        sample_hash = sample.get("source_sha256", outcome.evidence_sha256)
        note_parts = [
            f"context_tokens={outcome.key.context_tokens}",
            f"sample_evidence_sha256={_sha256(sample_hash, 'sample evidence SHA-256')}",
            f"gpu_memory_peak_mib={_number(_sample_metric(outcome, sample, index, sample_field='gpu_memory_peak_mib', group='memory', summary_field='gpu_memory_peak_mib'), 'GPU memory peak')}",
            f"gpu_dedicated_memory_peak_mib={_number(dedicated_gpu[index], 'dedicated GPU memory peak')}",
            f"gpu_shared_memory_peak_mib={_number(shared_gpu[index], 'shared GPU memory peak')}",
        ]
        if cpu is not None:
            note_parts.append(f"cpu_sample_count={cpu[3]}")
        else:
            note_parts.append("per-sample CPU observations are not exposed by this synthetic release")
        if gpu is not None:
            note_parts.append(f"gpu_sample_count={gpu[3]}")
        else:
            note_parts.append("per-sample GPU observations are not exposed by this synthetic release")
        rows.append(
            {
                "Measurement_ID": f"{_configuration_id(outcome.key)}-S{repetition}",
                "Run_ID": _run_id(outcome.key),
                "Test_ID": outcome.key.test_id,
                "Route": ROUTE,
                "Workbook_ID": WORKBOOK_ID,
                "Configuration_ID": _configuration_id(outcome.key),
                "Warmup_or_Measured": "Measured",
                "Repetition_Number": str(repetition),
                "Included_In_Formal_Statistics": "True",
                "Cold_or_Warm": "Warm",
                "Start_Timestamp_UTC": "",
                "End_Timestamp_UTC": "",
                "Input_Tokens": str(sample.get("num_input_tokens", "")),
                "Output_Tokens": str(sample.get("num_generated_tokens", "")),
                "Model_Load_Time_ms": _number(sample.get("load_ms"), "load_ms"),
                "TTFT_ms": _number(sample.get("ttft_ms"), "ttft_ms"),
                "Prompt_Processing_Tokens_Per_Second": _number(sample.get("prompt_tps"), "prompt_tps"),
                "TPOT_ms": _number(sample.get("tpot_ms"), "tpot_ms"),
                "Decode_Tokens_Per_Second": _number(sample.get("decode_tps"), "decode_tps"),
                "Total_Generation_Time_ms": _number(sample.get("generation_duration_ms"), "generation_duration_ms"),
                "Peak_Working_Set_Bytes": _bytes_from_mib(_sample_metric(outcome, sample, index, sample_field="peak_working_set_mib", group="memory", summary_field="peak_working_set_mib"), "peak working set"),
                "Peak_Private_Bytes": _bytes_from_mib(_sample_metric(outcome, sample, index, sample_field="peak_private_mib", group="memory", summary_field="peak_private_mib"), "peak private memory"),
                "Available_RAM_Before_Bytes": (
                    _bytes_from_mib(sample["available_ram_before_mb"], "available RAM before")
                    if "available_ram_before_mb" in sample else ""
                ),
                "Minimum_Available_RAM_During_Bytes": _bytes_from_mib(_sample_metric(outcome, sample, index, sample_field="available_ram_mib", group="memory", summary_field="available_ram_mib"), "minimum available RAM"),
                "Available_RAM_After_Bytes": (
                    _bytes_from_mib(sample["available_ram_after_mb"], "available RAM after")
                    if "available_ram_after_mb" in sample else ""
                ),
                "KV_Cache_Allocated_Bytes": _bytes_from_mib(_sample_metric(outcome, sample, index, sample_field="kv_mib", group="memory", summary_field="kv_mib"), "KV cache"),
                "GPU_Dedicated_Peak_Bytes": _bytes_from_mib(
                    dedicated_gpu[index], "dedicated GPU memory peak"
                ),
                "GPU_Shared_Peak_Bytes": _bytes_from_mib(
                    shared_gpu[index], "shared GPU memory peak"
                ),
                "CPU_Mean_Percent": cpu[0] if cpu else "",
                "CPU_Median_Percent": cpu[1] if cpu else "",
                "CPU_Peak_Percent": cpu[2] if cpu else "",
                "GPU_Engine_Mean_Percent": gpu[0] if gpu else "",
                "GPU_Engine_Median_Percent": gpu[1] if gpu else "",
                "GPU_Engine_Peak_Percent": gpu[2] if gpu else "",
                "Unload_Duration_ms": "",
                "Cleanup_Result": "No residual owned processes",
                "Raw_Metrics_Path": _path(source, "sample evidence path"),
                "Processed_Result_Path": _path(outcome.evidence_path, "runtime evidence path"),
                "Outlier_Status": "Included",
                "Outlier_Reason": "",
                "Validation_Status": "Accepted",
                "Evidence_Commit": evidence_commit,
                "Notes": "; ".join(note_parts),
            }
        )
    return rows


def _test_run_row(
    outcome: ComparisonRuntimeOutcome,
    quality: ComparisonQualityOutcome | None,
    case: OpenVINOCase,
    evidence_commit: str,
) -> dict[str, str]:
    telemetry = _activation(outcome)
    cpu = outcome.utilisation.get("cpu_percent")
    gpu = outcome.utilisation.get("gpu_percent")
    if not isinstance(cpu, Mapping) or not isinstance(gpu, Mapping):
        raise ValueError("runtime utilisation summaries are incomplete")
    dedicated_gpu, shared_gpu = _split_gpu_memory_mib(outcome)
    input_tokens = _consistent_sample_integer(outcome, "num_input_tokens", "input tokens")
    output_tokens = _consistent_sample_integer(
        outcome, "num_generated_tokens", "output tokens"
    )
    model_placement = _optional_telemetry_text(
        outcome, "actual_model_layer_placement"
    )
    kv_placement = _optional_telemetry_text(outcome, "actual_kv_placement")
    quality_complete = quality is not None and quality.status == "quality-complete"
    quality_score = (
        _number(quality.aggregates.get("mean"), "quality mean")
        if quality_complete and isinstance(quality.aggregates, Mapping)
        else ""
    )
    result_reason = "Accepted governed runtime with three formal samples"
    if quality is not None and quality.status != "quality-complete":
        result_reason += f"; quality status={quality.status}"
        if quality.status == "quality-terminal":
            result_reason += (
                f" at {quality.terminal_stage}: {quality.principal_reason}"
            )
    if quality_complete:
        next_action = "None"
    elif quality is None:
        next_action = "Capture governed P1-P6 quality"
    elif quality.status == "capture-complete-awaiting-adjudication":
        next_action = "Complete governed P1-P6 adjudication"
    elif quality.status == "quality-terminal":
        next_action = f"Respect governed quality terminal: {quality.terminal_stage}"
    else:
        next_action = f"Resolve governed quality status: {quality.status}"
    return {
        "Run_ID": _run_id(outcome.key),
        "Test_ID": outcome.key.test_id,
        "Route": ROUTE,
        "Workbook_ID": WORKBOOK_ID,
        "Run_Number": "1",
        "Run_Purpose": "Adaptive comparison aggregate",
        "Start_Timestamp_UTC": "",
        "End_Timestamp_UTC": "",
        "Start_Timestamp_Local": "",
        "End_Timestamp_Local": "",
        "Timezone": "Europe/London",
        "Operator": "Student and Codex",
        "Environment_ID": "",
        "Machine_Manifest_Path": "",
        "Repository_ID": "official-openvino-genai",
        "Repository_URL": "",
        "Repository_Branch_or_Tag": "",
        "Repository_Commit": "",
        "Repository_Clean_State": "",
        "Upstream_Base_Commit": "",
        "Build_ID": "",
        "Build_Type": "",
        "Compiler": "",
        "CMake_Version": "",
        "Build_Options": "",
        "Environment_Variables_Path": "",
        "Executable_Path": "",
        "Executable_SHA256": "",
        "Model_ID": _case_value(case, "model"),
        "Model_Source": "",
        "Model_Revision": "",
        "Model_Format": "OpenVINO IR",
        "Model_Weight_Precision": _case_value(case, "weight_precision"),
        "Model_Path_or_Folder": "",
        "Model_Size_Bytes": "",
        "Model_SHA256": "",
        "Tokenizer_ID": "",
        "Tokenizer_Revision": "",
        "Tokenizer_SHA256": "",
        "Configuration_ID": _configuration_id(outcome.key),
        "Prompt_Set_ID": "",
        "Prompt_ID": "",
        "Rubric_ID": "",
        "Seed": "",
        "Sampling_Settings_JSON": "",
        "Max_Output_Tokens": "",
        "Context_Target_Tokens": str(outcome.key.context_tokens),
        "Actual_Input_Tokens": str(input_tokens),
        "Actual_Output_Tokens": str(output_tokens),
        "Command_Path": "",
        "Working_Directory": "",
        "Requested_Backend": "OpenVINO GenAI",
        "Actual_Backend": "OpenVINO GenAI",
        "Requested_Device": _device(outcome, "requested"),
        "Actual_Device": _device(outcome, "actual"),
        "Requested_Offload": _case_value(case, "execution_route"),
        "Actual_Model_Layer_Placement": model_placement,
        "Actual_KV_Placement": kv_placement,
        "K_Cache_Type": _observed_precision(outcome, "key"),
        "V_Cache_Type": _observed_precision(outcome, "value"),
        "Optimisation_Name": f"K={_algorithm(outcome, 'key')};V={_algorithm(outcome, 'value')}",
        "Optimisation_Requested": _case_value(case, "k_algorithm").upper(),
        "Optimisation_Activated": "True",
        "Activation_Proof_Path": _path(outcome.evidence_path, "runtime evidence path"),
        "Silent_Fallback_Check": "Passed",
        "Fallback_Observed": _fallback(outcome),
        "Exit_Code": "0",
        "Model_Load_Success": "True",
        "Generation_Success": "True",
        "Output_Completion_Status": "Complete",
        "Model_Load_Time_ms": _number(_metric_value(outcome, "timing", "load_ms", "mean"), "load mean"),
        "TTFT_ms": _number(_metric_value(outcome, "timing", "ttft_ms", "mean"), "TTFT mean"),
        "Prompt_Processing_Tokens_Per_Second": _number(_metric_value(outcome, "timing", "prompt_tps", "mean"), "prompt throughput mean"),
        "TPOT_ms": _number(_metric_value(outcome, "timing", "tpot_ms", "mean"), "TPOT mean"),
        "Decode_Tokens_Per_Second": _number(_metric_value(outcome, "timing", "decode_tps", "mean"), "decode throughput mean"),
        "Total_Generation_Time_ms": _number(_metric_value(outcome, "timing", "generation_duration_ms", "mean"), "generation duration mean"),
        "Peak_Working_Set_Bytes": _bytes_from_mib(_metric_value(outcome, "memory", "peak_working_set_mib", "worst_max"), "peak working set"),
        "Peak_Private_Bytes": _bytes_from_mib(_metric_value(outcome, "memory", "peak_private_mib", "worst_max"), "peak private memory"),
        "Available_RAM_Before_Bytes": "",
        "Minimum_Available_RAM_During_Bytes": _bytes_from_mib(_metric_value(outcome, "memory", "available_ram_mib", "global_min"), "minimum available RAM"),
        "Available_RAM_After_Bytes": "",
        "KV_Cache_Allocated_Bytes": _bytes_from_mib(_metric_value(outcome, "memory", "kv_mib", "worst_max"), "KV cache"),
        "GPU_Dedicated_Peak_Bytes": _bytes_from_mib(
            max(dedicated_gpu), "dedicated GPU memory peak"
        ),
        "GPU_Shared_Peak_Bytes": _bytes_from_mib(
            max(shared_gpu), "shared GPU memory peak"
        ),
        "CPU_Mean_Percent": _number(cpu.get("mean"), "CPU mean"),
        "CPU_Peak_Percent": _number(cpu.get("peak"), "CPU peak"),
        "GPU_Engine_Mean_Percent": _number(gpu.get("mean"), "GPU mean"),
        "GPU_Engine_Peak_Percent": _number(gpu.get("peak"), "GPU peak"),
        "Unload_Duration_ms": "",
        "Cleanup_Result": "No residual owned processes",
        "Warmup_or_Measured": "Aggregate of measured samples",
        "Repetition_Number": "1-3",
        "Included_In_Formal_Statistics": "True",
        "Quality_Score_0_to_10": quality_score,
        "Quality_Criterion_Scores_Path": (
            _path(quality.evidence_path, "quality evidence path") if quality_complete else ""
        ),
        "Deterministic_Check_Result": "",
        "Schema_or_Format_Valid": "",
        "Required_Facts_Retained": "",
        "Unsupported_Statements_Count": "",
        "Repetition_Corruption_or_Truncation": "",
        "Long_Context_Result": "",
        "Multi_Turn_Result": "",
        "Perplexity": "",
        "Stability_Result": "",
        "Failure_IDs": (
            f"WB04-CMP-FAIL-{outcome.key.test_id}-{outcome.key.context_tokens}-QUALITY"
            if quality is not None and quality.status == "quality-terminal" else ""
        ),
        "Raw_Stdout_Path": "",
        "Raw_Stderr_Path": "",
        "Raw_Response_Path": "",
        "Resource_Samples_Path": _path(outcome.evidence_path, "runtime evidence path"),
        "Device_Proof_Path": _path(outcome.evidence_path, "runtime evidence path"),
        "Raw_Evidence_Path": _path(outcome.evidence_path, "runtime evidence path"),
        "Processed_Result_Path": _path(outcome.evidence_path, "runtime evidence path"),
        "Hash_Manifest_Path": "",
        "Result": (
            "Passed" if quality_complete else "Runtime-only"
        ),
        "Result_Reason": result_reason,
        "Missing_Data_Codes": "",
        "Next_Action": next_action,
        "Workbook_Update_Status": "Published candidate",
        "Workbook_Section": "Adaptive comparison",
        "Evidence_Commit": evidence_commit,
        "Reviewer": "Codex evidence reconciliation",
        "Review_Date": "2026-08-01",
        "Notes": _runtime_note(outcome),
    }


def _terminal_run_row(
    key: ComparisonKey, terminal: Mapping[str, Any], case: OpenVINOCase, evidence_commit: str
) -> dict[str, str]:
    reason = terminal.get("principal_reason")
    stage = terminal.get("stage")
    if not isinstance(reason, str) or not reason.strip():
        raise ValueError("terminal principal reason is required")
    if not isinstance(stage, str) or not stage.strip():
        raise ValueError("terminal stage is required")
    return {
        "Run_ID": _run_id(key),
        "Test_ID": key.test_id,
        "Route": ROUTE,
        "Workbook_ID": WORKBOOK_ID,
        "Run_Number": "1",
        "Run_Purpose": "Adaptive comparison terminal",
        "Model_ID": _case_value(case, "model"),
        "Model_Format": "OpenVINO IR",
        "Model_Weight_Precision": _case_value(case, "weight_precision"),
        "Configuration_ID": _configuration_id(key),
        "Prompt_Set_ID": "",
        "Prompt_ID": "",
        "Rubric_ID": "",
        "Context_Target_Tokens": str(key.context_tokens),
        "Requested_Backend": "OpenVINO GenAI",
        "Requested_Device": _case_value(case, "requested_device"),
        "Requested_Offload": _case_value(case, "execution_route"),
        "Optimisation_Name": _case_value(case, "k_algorithm").upper(),
        "Optimisation_Requested": _case_value(case, "k_algorithm").upper(),
        "Model_Load_Success": "",
        "Generation_Success": "",
        "Output_Completion_Status": "Terminal",
        "Warmup_or_Measured": "Not measured",
        "Included_In_Formal_Statistics": "False",
        "Failure_IDs": f"WB04-CMP-FAIL-{key.test_id}-{key.context_tokens}-RUNTIME",
        "Raw_Evidence_Path": _path(terminal.get("evidence_path"), "terminal evidence path"),
        "Processed_Result_Path": _path(terminal.get("evidence_path"), "terminal evidence path"),
        "Result": "Terminal",
        "Result_Reason": reason,
        "Next_Action": "",
        "Workbook_Update_Status": "Published candidate",
        "Workbook_Section": "Adaptive comparison terminal",
        "Evidence_Commit": evidence_commit,
        "Reviewer": "Codex evidence reconciliation",
        "Review_Date": "2026-08-01",
        "Notes": (
            f"context_tokens={key.context_tokens}; stage={stage}; "
            f"terminal_evidence_sha256={_sha256(terminal.get('evidence_sha256'), 'terminal evidence SHA-256')}; "
            "model-load and generation outcomes remain empty because the terminal contract does not expose attempted execution facts"
        ),
    }


def _device_row(
    outcome: ComparisonRuntimeOutcome, case: OpenVINOCase, evidence_commit: str
) -> dict[str, str]:
    cpu = outcome.utilisation["cpu_percent"]
    gpu = outcome.utilisation["gpu_percent"]
    model_placement = _optional_telemetry_text(
        outcome, "actual_model_layer_placement"
    )
    kv_placement = _optional_telemetry_text(outcome, "actual_kv_placement")
    limitation = (
        "model-layer and KV placement are not exposed by the validated activation telemetry; compatible placement cells remain empty; "
        if not model_placement or not kv_placement
        else ""
    )
    return {
        "Test_ID": outcome.key.test_id,
        "Route": ROUTE,
        "Workbook_ID": WORKBOOK_ID,
        "Latest_Run_ID": _run_id(outcome.key),
        "Requested_Device": _device(outcome, "requested"),
        "Actual_Device": _device(outcome, "actual"),
        "Backend": "OpenVINO GenAI",
        "Compiled_or_Execution_Device": _device(outcome, "actual"),
        "Requested_Offload": _case_value(case, "execution_route"),
        "Actual_Model_Layer_Placement": model_placement,
        "Actual_KV_Placement": kv_placement,
        "Optimisation_Device": _device(outcome, "actual"),
        "CPU_Fallback": _fallback(outcome),
        "Hybrid_Behaviour": "",
        "Silent_Fallback_Check": "Passed",
        "CPU_Mean_Percent": _number(cpu["mean"], "CPU mean"),
        "CPU_Median_Percent": _number(cpu["median"], "CPU median"),
        "CPU_Peak_Percent": _number(cpu["peak"], "CPU peak"),
        "GPU_Engine_Mean_Percent": _number(gpu["mean"], "GPU mean"),
        "GPU_Engine_Median_Percent": _number(gpu["median"], "GPU median"),
        "GPU_Engine_Peak_Percent": _number(gpu["peak"], "GPU peak"),
        "Device_Proof_Path": _path(outcome.evidence_path, "runtime evidence path"),
        "Utilisation_Samples_Path": _path(outcome.evidence_path, "runtime evidence path"),
        "Result": "Passed",
        "Failure_IDs": "",
        "Evidence_Commit": evidence_commit,
        "Notes": (
            f"{limitation}context_tokens={outcome.key.context_tokens}; "
            f"observed_k={_observed_precision(outcome, 'key')}; "
            f"observed_v={_observed_precision(outcome, 'value')}; "
            f"cpu_sample_count={int(cpu['count'])}; gpu_sample_count={int(gpu['count'])}; "
            f"runtime_evidence_sha256={outcome.evidence_sha256}"
        ),
    }


def _quality_rows(outcome: ComparisonQualityOutcome, evidence_commit: str) -> list[dict[str, str]]:
    if outcome.status != "quality-complete":
        return []
    if not isinstance(outcome.prompt_scores, Mapping) or tuple(outcome.prompt_scores) != PROMPT_IDS:
        raise ValueError("complete quality must expose ordered P1-P6 scores")
    path = _path(outcome.evidence_path, "quality adjudication path")
    digest = _sha256(outcome.evidence_sha256, "quality adjudication SHA-256")
    limitation = (
        "per-criterion and raw-response facts are not exposed by ComparisonRelease; "
        f"adjudication={path}#sha256={digest}; context_tokens={outcome.key.context_tokens}"
    )
    return [
        {
            "Evaluation_ID": f"{_configuration_id(outcome.key)}-{prompt_id}",
            "Route": ROUTE,
            "Workbook_ID": WORKBOOK_ID,
            "Test_ID": outcome.key.test_id,
            "Run_ID": _run_id(outcome.key),
            "Configuration_ID": _configuration_id(outcome.key),
            "Comparison_Role": "Adaptive format comparison",
            "Prompt_Set_ID": PROMPT_SET_ID,
            "Prompt_ID": prompt_id,
            "Rubric_ID": RUBRIC_ID,
            "Raw_Response_Path": "",
            "Response_SHA256": "",
            "Deterministic_Check_Result": "",
            "Format_Valid": "",
            "Required_Facts_Retained": "",
            "Unsupported_Statements_Count": "",
            "Repetition_Corruption_or_Truncation": "",
            "Correctness_and_Grounding_0_to_10": "",
            "Instruction_and_Format_0_to_10": "",
            "Completeness_and_Fact_Retention_0_to_10": "",
            "Relevance_Clarity_Coherence_0_to_10": "",
            "Stability_and_Integrity_0_to_10": "",
            "Weighted_Score_0_to_10": _number(outcome.prompt_scores[prompt_id], f"{prompt_id} weighted score"),
            "Critical_Cap_Applied": "",
            "Critical_Cap_Reason": "",
            "Blind_Label": "",
            "Pairwise_Order": "",
            "Judge_or_Reviewer": "Governed blind adjudication",
            "Manual_Adjudication_Required": "",
            "Manual_Adjudication_Result": "",
            "Result": "Adjudicated",
            "Evidence_Commit": evidence_commit,
            "Notes": limitation,
        }
        for prompt_id in PROMPT_IDS
    ]


def _failure_row(
    key: ComparisonKey,
    *,
    stage: object,
    reason: object,
    evidence_path: object,
    evidence_sha256: object,
    kind: str,
    evidence_commit: str,
) -> dict[str, str]:
    if not isinstance(stage, str) or not stage.strip():
        raise ValueError("terminal stage is required")
    if not isinstance(reason, str) or not reason.strip():
        raise ValueError("terminal principal reason is required")
    path = _path(evidence_path, "terminal evidence path")
    digest = _sha256(evidence_sha256, "terminal evidence SHA-256")
    return {
        "Failure_ID": f"WB04-CMP-FAIL-{key.test_id}-{key.context_tokens}-{kind.upper()}",
        "Date_UTC": "2026-08-01",
        "Date_Local": "2026-08-01",
        "Run_ID": _run_id(key),
        "Test_ID": key.test_id,
        "Route": ROUTE,
        "Workbook_ID": WORKBOOK_ID,
        "Failure_Code": f"ADAPTIVE_{kind.upper()}_TERMINAL",
        "Failure_Category": f"Governed {kind} terminal",
        "Failure_Stage": stage,
        "Observed_Symptom": reason,
        "Error_Message_or_Code": reason,
        "Immediate_Effect": "No numeric result published for the terminal stage",
        "Safety_Impact": "",
        "Suspected_Cause": "",
        "Diagnostic_Actions": "Validated the explicit terminal receipt",
        "Root_Cause": "",
        "Fix_or_Workaround": "",
        "Retest_Run_ID": "",
        "Final_Status": "Terminal",
        "Missing_Workbook_Fields": "Numeric fields intentionally absent because no measurement completed",
        "Next_Action": "",
        "Raw_Evidence_Path": path,
        "Evidence_Commit": evidence_commit,
        "Reviewer": "Codex evidence reconciliation",
        "Review_Date": "2026-08-01",
        "Notes": f"context_tokens={key.context_tokens}; evidence_sha256={digest}",
    }


def _evidence_rows(
    release: ComparisonRelease,
    *,
    matrix_path: Path,
    matrix_sha256: str,
    reconciliation_input_sha256: str,
    evidence_commit: str,
) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []

    def add(kind: str, key: ComparisonKey | None, path: str, digest: str, note: str) -> None:
        identity = "GLOBAL" if key is None else f"{key.test_id}-{key.context_tokens}"
        rows.append(
            {
                "Evidence_ID": f"WB04-CMP-{kind.upper()}-{identity}-{digest[:12]}",
                "Run_ID": "" if key is None else _run_id(key),
                "Test_ID": "ADAPTIVE-COMPARISON" if key is None else key.test_id,
                "Route": ROUTE,
                "Workbook_ID": WORKBOOK_ID,
                "Evidence_Type": kind,
                "Repository_Path": path,
                "File_Name": Path(path).name if path else "",
                "Size_Bytes": "",
                "SHA256": digest,
                "Created_Timestamp_UTC": "",
                "Source_or_Derivation": "Explicit reconciled comparison release",
                "Processing_Script": "scripts/testing/tools/publish_official_openvino_comparison.py",
                "Immutable_Raw_Evidence": "True",
                "Contains_Sensitive_Data": "False",
                "Redaction_Status": "Not required",
                "Evidence_Commit": evidence_commit,
                "Validation_Status": "Validated",
                "Notes": note,
            }
        )

    add("matrix", None, Path(matrix_path).as_posix(), matrix_sha256, "Validated adaptive comparison matrix")
    add(
        "reconciliation-input",
        None,
        "",
        reconciliation_input_sha256,
        "The projection API received the validated reconciliation digest; the Path API supplies its repository path during publication",
    )
    for key, outcome in sorted(release.runtime.items()):
        add("runtime", key, _path(outcome.evidence_path, "runtime evidence path"), _sha256(outcome.evidence_sha256, "runtime evidence SHA-256"), "Accepted three-sample runtime sequence")
    for key, outcome in sorted(release.quality.items()):
        add("quality", key, _path(outcome.evidence_path, "quality evidence path"), _sha256(outcome.evidence_sha256, "quality evidence SHA-256"), f"Quality status={outcome.status}")
    for key, terminal in sorted(release.terminals.items()):
        add("runtime-terminal", key, _path(terminal.get("evidence_path"), "terminal evidence path"), _sha256(terminal.get("evidence_sha256"), "terminal evidence SHA-256"), f"stage={terminal.get('stage')}; principal_reason={terminal.get('principal_reason')}")
    return rows


def _completion_rows(
    release: ComparisonRelease,
    evidence_commit: str,
    *,
    publication_final: bool,
    campaign_closed: bool,
    release_complete: bool,
) -> list[dict[str, str]]:
    if not all(
        type(value) is bool
        for value in (publication_final, campaign_closed, release_complete)
    ):
        raise ValueError("publication completion facts must be boolean")
    if publication_final and not (campaign_closed and release_complete):
        raise ValueError("final publication requires a closed complete release")
    titles = (
        "Repository, runtime and host",
        "Successful build and recovery checks",
        "Successful bounded diagnostics",
        "Successful expected-rejection controls",
        "U8 STANDARD, TBQ4 and TBQ3 shared-context comparison",
        "U4, U8 and FP16 STANDARD deployment comparison",
        "Complete timing results",
        "Complete memory and CPU/GPU results",
        "P1-P6 and aggregate quality results",
        "Laptop runtime-capable and fully-comparable boundaries",
        "Terminal attempts and hash-bound evidence",
    )
    run_ids = "; ".join(_run_id(key) for key in sorted(set(release.runtime) | set(release.terminals)))
    awaiting_quality = any(
        outcome.status == "capture-complete-awaiting-adjudication"
        for outcome in release.quality.values()
    )
    rows: list[dict[str, str]] = []
    for index, title in enumerate(titles, start=1):
        if index <= 4 or publication_final:
            completion_status = "Complete"
            missing_code = ""
            missing_reason = ""
            remaining_action = "None"
        elif not campaign_closed:
            completion_status = "Partial checkpoint"
            missing_code = "CAMPAIGN_OPEN"
            missing_reason = "The adaptive runtime ladder is not yet closed"
            remaining_action = "Continue the governed adaptive runtime ladder"
        elif index == 9 and awaiting_quality:
            completion_status = "Awaiting adjudication"
            missing_code = "QUALITY_ADJUDICATION_PENDING"
            missing_reason = "Complete P1-P6 capture awaits governed numeric adjudication"
            remaining_action = "Complete governed P1-P6 adjudication"
        else:
            completion_status = "Closed checkpoint - final publication pending"
            missing_code = "FINAL_PUBLICATION_PENDING"
            missing_reason = "The campaign is closed but this is not the validated final publication"
            remaining_action = "Publish the validated final comparison release"
        rows.append({
            "Workbook_ID": WORKBOOK_ID,
            "Workbook_File": "04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx",
            "Route": ROUTE,
            "Section_Type": "Controlled workbook section",
            "Section_or_Test_ID": str(index),
            "Section_Title": title,
            "Required_Source_Evidence": "Explicit reconciled adaptive comparison release",
            "Source_Run_IDs": run_ids,
            "Source_Result_Path": "publication-state.json",
            "Completion_Status": completion_status,
            "Missing_Data_Code": missing_code,
            "Missing_Data_Reason": missing_reason,
            "Evidence_Checked": "True",
            "Updated_Date": "2026-08-01",
            "Updated_By": "Student and Codex",
            "Evidence_Commit": evidence_commit,
            "Reviewer": "Codex evidence reconciliation",
            "Review_Date": "2026-08-01",
            "Remaining_Action": remaining_action,
            "Notes": "Unsupported optional facts remain empty and are explained in their governed register rows",
        })
    return rows


_WR036 = {
    "Record_ID": "WR-036",
    "Workbook_ID": "WB-04",
    "Version": "1.8",
    "Date": "2026-07-31",
    "Changed_By": "Student",
    "Change_Type": "Success-focused controlled presentation",
    "Change_Summary": "Presented three measured runtime rows, grouped the successful boundaries, summarized incomplete tests with compact reasons, retained the full 60-ID, 36-runtime and 33-quality evidence set, and recorded that no quality score or winner exists.",
    "Reason": "Revision 1.7 retained the governed evidence but its dense presentation obscured the verified successes and incomplete-test boundary.",
    "Affected_Test_IDs": "OV-B01-OV-B12; OV-C01-OV-C06; OV-01-OV-10; OV-TQS-01-OV-TQS-12; OV-TQ-01-OV-TQ-20; P1-P6",
    "Change_Reference": "testing/openvino-turboquant-recovery; v2 reconciliation input SHA-256 96f7355a9446092ddad6d68c75d73bbd956bd24cd3213f9e12db470a87410c65; Pending PR; Pending merge",
    "Status": "Superseded",
    "Supersedes": "1.7",
}


def _revision_rows(reconciliation_input_sha256: str) -> list[dict[str, str]]:
    return [
        dict(_WR036),
        {
            "Record_ID": "WR-037",
            "Workbook_ID": "WB-04",
            "Version": "1.9",
            "Date": "2026-08-01",
            "Changed_By": "Student and Codex",
            "Change_Type": "Adaptive format comparison release",
            "Change_Summary": "Added guarded U4, U8, FP16, TBQ4 and TBQ3 comparison ladders with complete runtime, utilisation, quality and laptop-boundary evidence.",
            "Reason": "Provide a fair same-artifact TurboQuant comparison and establish the maximum safe format/context on the test laptop.",
            "Affected_Test_IDs": "OV-11; OV-12; OV-13; OV-TQ-21; OV-TQ-22; P1-P6",
            "Change_Reference": (
                "testing/openvino-adaptive-format-comparison; adaptive comparison "
                f"reconciliation input SHA-256 {reconciliation_input_sha256}; Pending PR; Pending merge"
            ),
            "Status": "Current - pending PR",
            "Supersedes": "1.8",
        },
    ]


def register_rows_from_release(
    release: ComparisonRelease,
    *,
    matrix_cases: Sequence[OpenVINOCase],
    matrix_path: Path,
    matrix_sha256: str,
    evidence_commit: str,
    reconciliation_input_sha256: str,
    runtime_matrix_identity_sha256_by_test_id: Mapping[str, str] | None = None,
    publication_final: bool = False,
    campaign_closed: bool = False,
    release_complete: bool = False,
) -> Mapping[str, Sequence[Mapping[str, str]]]:
    """Project only facts exposed by a validated comparison release."""

    matrix_sha256 = _sha256(matrix_sha256, "matrix SHA-256")
    reconciliation_input_sha256 = _sha256(
        reconciliation_input_sha256, "reconciliation input SHA-256"
    )
    if not isinstance(evidence_commit, str) or not evidence_commit.strip():
        raise ValueError("evidence commit is required")
    cases = _validate_matrix_cases(matrix_cases)
    expected_runtime_matrix_identities = (
        {test_id: matrix_sha256 for test_id in cases}
        if runtime_matrix_identity_sha256_by_test_id is None
        else dict(runtime_matrix_identity_sha256_by_test_id)
    )
    if set(expected_runtime_matrix_identities) != set(cases):
        raise ValueError("expected runtime matrix identity map is incomplete or unexpected")
    expected_runtime_matrix_identities = {
        test_id: _sha256(value, f"{test_id} expected runtime matrix identity SHA-256")
        for test_id, value in expected_runtime_matrix_identities.items()
    }
    for key, outcome in release.runtime.items():
        if key.test_id not in cases or key.context_tokens not in tuple(cases[key.test_id].contexts):
            raise ValueError("runtime identity is outside the validated matrix")
        actual_matrix_identity = _sha256(
            outcome.identity_hashes.get("matrix_sha256"),
            "runtime matrix identity SHA-256",
        )
        if actual_matrix_identity != expected_runtime_matrix_identities[key.test_id]:
            raise ValueError(
                "runtime matrix identity hash disagrees with the exact validated matrix"
            )

    runs: list[dict[str, str]] = []
    performance: list[dict[str, str]] = []
    devices: list[dict[str, str]] = []
    quality_rows: list[dict[str, str]] = []
    failures: list[dict[str, str]] = []
    for key, outcome in sorted(release.runtime.items()):
        quality = release.quality.get(key)
        runs.append(_test_run_row(outcome, quality, cases[key.test_id], evidence_commit))
        performance.extend(_performance_rows(outcome, evidence_commit))
        devices.append(_device_row(outcome, cases[key.test_id], evidence_commit))
        if quality is not None:
            quality_rows.extend(_quality_rows(quality, evidence_commit))
            if quality.status == "quality-terminal":
                failures.append(
                    _failure_row(
                        key,
                        stage=quality.terminal_stage,
                        reason=quality.principal_reason,
                        evidence_path=quality.evidence_path,
                        evidence_sha256=quality.evidence_sha256,
                        kind="quality",
                        evidence_commit=evidence_commit,
                    )
                )
    for key, terminal in sorted(release.terminals.items()):
        runs.append(_terminal_run_row(key, terminal, cases[key.test_id], evidence_commit))
        failures.append(
            _failure_row(
                key,
                stage=terminal.get("stage"),
                reason=terminal.get("principal_reason"),
                evidence_path=terminal.get("evidence_path"),
                evidence_sha256=terminal.get("evidence_sha256"),
                kind="runtime",
                evidence_commit=evidence_commit,
            )
        )

    return {
        REGISTER_PATHS[0]: _traceability_rows(cases),
        REGISTER_PATHS[1]: _configuration_rows(cases, matrix_path, matrix_sha256),
        REGISTER_PATHS[2]: runs,
        REGISTER_PATHS[3]: performance,
        REGISTER_PATHS[4]: devices,
        REGISTER_PATHS[5]: quality_rows,
        REGISTER_PATHS[6]: failures,
        REGISTER_PATHS[7]: _evidence_rows(
            release,
            matrix_path=matrix_path,
            matrix_sha256=matrix_sha256,
            reconciliation_input_sha256=reconciliation_input_sha256,
            evidence_commit=evidence_commit,
        ),
        REGISTER_PATHS[8]: _completion_rows(
            release,
            evidence_commit,
            publication_final=publication_final,
            campaign_closed=campaign_closed,
            release_complete=release_complete,
        ),
        REGISTER_PATHS[9]: _revision_rows(reconciliation_input_sha256),
        REGISTER_PATHS[10]: (),
    }


def _read_csv_strict(path: Path) -> tuple[list[str], list[dict[str, str]], bool, str]:
    raw = Path(path).read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    newline = "\r\n" if b"\r\n" in raw else "\n"
    try:
        text = raw.decode("utf-8-sig")
    except UnicodeDecodeError as error:
        raise ValueError(f"CSV is not UTF-8: {path}") from error
    reader = csv.DictReader(io.StringIO(text, newline=""))
    header = reader.fieldnames
    if not header or any(not field for field in header) or len(header) != len(set(header)):
        raise ValueError(f"CSV header is missing or duplicated: {path}")
    rows: list[dict[str, str]] = []
    for index, raw_row in enumerate(reader, start=2):
        if None in raw_row:
            raise ValueError(f"CSV row {index} exceeds the header: {path}")
        if any(value is None for value in raw_row.values()):
            raise ValueError(f"CSV row {index} is incomplete: {path}")
        rows.append(dict(raw_row))  # type: ignore[arg-type]
    return list(header), rows, bom, newline


def _encode_csv(
    header: Sequence[str], rows: Sequence[Mapping[str, str]], *, bom: bool, newline: str
) -> bytes:
    stream = io.StringIO(newline="")
    writer = csv.DictWriter(
        stream,
        fieldnames=list(header),
        lineterminator=newline,
        extrasaction="raise",
    )
    writer.writeheader()
    for row in rows:
        unknown = set(row) - set(header)
        if unknown:
            raise ValueError(
                "CSV schema has no compatible header for projected fields: "
                + ", ".join(sorted(unknown))
            )
        values: dict[str, str] = {}
        for field in header:
            value = row.get(field, "")
            if not isinstance(value, str):
                raise ValueError(f"projected CSV field {field} must be text")
            values[field] = value
        writer.writerow(values)
    encoded = stream.getvalue().encode("utf-8")
    return (b"\xef\xbb\xbf" + encoded) if bom else encoded


def rewrite_csv_slice_atomically(
    path: Path,
    *,
    owned_key: Callable[[Mapping[str, str]], str | None],
    replacement_rows: Sequence[Mapping[str, str]],
    unique_key: Callable[[Mapping[str, str]], str | None] | None = None,
    replacement_sort_key: Callable[[Mapping[str, str]], object] | None = None,
) -> bytes:
    """Return a deterministic replacement slice without modifying ``path``."""

    header, existing, bom, newline = _read_csv_strict(Path(path))
    replacement = [dict(row) for row in replacement_rows]
    keyed: list[tuple[str, dict[str, str]]] = []
    seen: set[str] = set()
    for row in replacement:
        key = owned_key(row)
        if not isinstance(key, str) or not key:
            raise ValueError("every replacement row must have an owned key")
        if key in seen:
            raise ValueError(f"duplicate owned key: {key}")
        seen.add(key)
        keyed.append((key, row))
    unrelated = [row for row in existing if owned_key(row) is None]
    if unique_key is not None:
        replacement_global_keys: set[str] = set()
        for row in replacement:
            global_key = unique_key(row)
            if not isinstance(global_key, str) or not global_key:
                raise ValueError("replacement row lacks its global unique key")
            if global_key in replacement_global_keys:
                raise ValueError(f"duplicate global key: {global_key}")
            replacement_global_keys.add(global_key)
        for row in unrelated:
            global_key = unique_key(row)
            if not isinstance(global_key, str) or not global_key:
                raise ValueError("unrelated row lacks its global unique key")
            if global_key in replacement_global_keys:
                raise ValueError(f"duplicate global key: {global_key}")
    ordered_replacement = [
        row
        for _, row in sorted(
            keyed,
            key=(
                (lambda item: replacement_sort_key(item[1]))
                if replacement_sort_key is not None
                else (lambda item: item[0])
            ),
        )
    ]
    ordered = unrelated + ordered_replacement
    return _encode_csv(header, ordered, bom=bom, newline=newline)


@dataclass(frozen=True)
class PublicationInputs:
    """One validated release plus the metadata omitted from ComparisonRelease."""

    release: ComparisonRelease
    matrix_cases: Sequence[OpenVINOCase]
    matrix_path: Path
    matrix_sha256: str
    reconciliation_input_path: Path
    reconciliation_input_sha256: str
    publication_state_path: Path


@dataclass(frozen=True)
class PublicationBundle:
    """Fully validated candidate bytes; publication state remains non-recursive."""

    files: Mapping[str, bytes]
    publication_state_path: Path
    publication_state_bytes: bytes


def _hash_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _hash_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _inside(path: Path, root: Path, label: str) -> Path:
    resolved = Path(path).resolve()
    root = Path(root).resolve()
    if resolved != root and root not in resolved.parents:
        raise ValueError(f"{label} is outside repo_root")
    return resolved


def _repository_relative(path: Path, root: Path, label: str) -> Path:
    candidate = Path(path)
    resolved = candidate.resolve() if candidate.is_absolute() else (root / candidate).resolve()
    return Path(_inside(resolved, root, label).relative_to(Path(root).resolve()).as_posix())


def _strict_json(path: Path, label: str) -> dict[str, Any]:
    def object_pairs(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        result: dict[str, Any] = {}
        for key, value in pairs:
            if key in result:
                raise ValueError(f"{label} contains duplicate JSON key: {key}")
            result[key] = value
        return result

    try:
        value = json.loads(
            Path(path).read_text(encoding="utf-8-sig"),
            object_pairs_hook=object_pairs,
            parse_constant=lambda constant: (_ for _ in ()).throw(
                ValueError(f"{label} contains non-finite JSON number: {constant}")
            ),
        )
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValueError(f"{label} is unreadable") from error
    if not isinstance(value, dict):
        raise ValueError(f"{label} must be an object")
    return value


def _load_publication_inputs(release_input: Path, repo_root: Path) -> PublicationInputs:
    """Load Task 7 authority once, then reconcile the exact same input."""

    path = _inside(Path(release_input), repo_root, "release input")
    loaded = load_comparison_release_input(path)
    matrix_path = _inside(Path(loaded.matrix_path), repo_root, "validated matrix")
    cases = load_adaptive_comparison_matrix(matrix_path)
    release = reconcile_comparison_release(path)
    payload = _strict_json(path, "comparison release input")
    reconciliation_sha256 = _sha256(
        payload.get("release_input_sha256"), "reconciliation input SHA-256"
    )
    state_path = _inside(path.parent / "publication-state.json", repo_root, "publication state")
    return PublicationInputs(
        release=release,
        matrix_cases=cases,
        matrix_path=matrix_path,
        matrix_sha256=_sha256(loaded.matrix_sha256, "matrix SHA-256"),
        reconciliation_input_path=path,
        reconciliation_input_sha256=reconciliation_sha256,
        publication_state_path=state_path,
    )


def _normalize_display_path(value: Path, repo_root: Path, label: str) -> Path:
    return _repository_relative(Path(value), repo_root, label)


def _normalize_release_for_display(
    release: ComparisonRelease, repo_root: Path
) -> ComparisonRelease:
    """Copy a release with repository-relative display paths only."""

    runtime: dict[ComparisonKey, ComparisonRuntimeOutcome] = {}
    for key, outcome in release.runtime.items():
        samples: list[Mapping[str, Any]] = []
        for index, raw in enumerate(outcome.samples, start=1):
            sample = dict(raw)
            if "source" in sample:
                sample["source"] = _normalize_display_path(
                    Path(sample["source"]), repo_root, f"{key} sample-{index} evidence"
                ).as_posix()
            samples.append(sample)
        runtime[key] = replace(
            outcome,
            samples=(samples[0], samples[1], samples[2]),
            evidence_path=_normalize_display_path(
                outcome.evidence_path, repo_root, f"{key} runtime evidence"
            ),
        )
    quality = {
        key: replace(
            outcome,
            evidence_path=_normalize_display_path(
                outcome.evidence_path, repo_root, f"{key} quality evidence"
            ),
        )
        for key, outcome in release.quality.items()
    }
    terminals: dict[ComparisonKey, Mapping[str, Any]] = {}
    for key, terminal in release.terminals.items():
        copied = dict(terminal)
        copied["evidence_path"] = _normalize_display_path(
            Path(copied.get("evidence_path")), repo_root, f"{key} terminal evidence"
        )
        terminals[key] = copied
    return replace(release, runtime=runtime, quality=quality, terminals=terminals)


def _read_manifest_rows(path: Path) -> tuple[list[str], list[dict[str, str]]]:
    header, rows, _bom, _newline = _read_csv_strict(path)
    if [row.get("Workbook_ID") for row in rows] != [
        f"WB-{index:02d}" for index in range(1, 7)
    ]:
        raise ValueError("controlled workbook manifest must contain exact WB-01 to WB-06 rows")
    return header, rows


def _insert_controlled_metadata(source: str, manifest_row: Mapping[str, str]) -> str:
    required = (
        f"**Controlled filename:** `{manifest_row['Controlled_File']}`",
        "**Generated DOCX hash:** recorded in `Controlled-Workbook-Manifest.csv`",
        f"**Original source:** `{manifest_row['Source_File']}`",
        f"**Original source SHA-256:** `{manifest_row['Source_SHA256']}`",
    )
    prefixes = tuple(line.split(":", 1)[0] + ":" for line in required)
    lines = source.splitlines()
    existing = [line for line in lines if line.startswith(prefixes)]
    if existing:
        if tuple(existing) != required or any(source.count(line) != 1 for line in required):
            raise ValueError("canonical WB-04 metadata is contradictory or duplicated")
        return source
    if any(prefix in source for prefix in prefixes):
        raise ValueError("canonical WB-04 metadata is incomplete")
    if not lines or not lines[0].startswith("# 04 Official OpenVINO Controlled Retest Workbook"):
        raise ValueError("WB-04 source title is missing")
    return "\n".join((lines[0], "", *required, "", *lines[1:])).rstrip() + "\n"


def _replace_frozen_matrix_fact(
    source: str,
    *,
    matrix_path: Path,
    matrix_sha256: str,
    matrix_cases: Sequence[OpenVINOCase],
) -> str:
    matching = [line for line in source.splitlines() if line.startswith("| Frozen matrix |")]
    contexts = sum(len(tuple(case.contexts)) for case in matrix_cases)
    replacement = (
        f"| Adaptive comparison matrix | `{Path(matrix_path).as_posix()}`; "
        f"SHA-256 `{matrix_sha256}`; validated cases={len(matrix_cases)}; "
        f"declared contexts={contexts}. |"
    )
    adaptive = [
        line
        for line in source.splitlines()
        if line.startswith("| Adaptive comparison matrix |")
    ]
    if len(matching) == 1 and not adaptive:
        result = source.replace(matching[0], replacement)
    elif not matching and adaptive == [replacement]:
        result = source
    else:
        raise ValueError(
            "source must contain exactly one historical or matching adaptive matrix fact"
        )
    if result.count(matrix_sha256) != 1 or HISTORICAL_MATRIX_SHA256 in result:
        raise ValueError("adaptive matrix provenance replacement is not unique")
    return result


def _owned_key(
    path: str, comparison_ids: Sequence[str]
) -> Callable[[Mapping[str, str]], str | None]:
    comparison = set(comparison_ids)
    if path == REGISTER_PATHS[0]:
        return lambda row: row.get("Test_ID") if row.get("Workbook_ID") == WORKBOOK_ID and row.get("Test_ID") in comparison else None
    if path == REGISTER_PATHS[1]:
        return lambda row: row.get("Configuration_ID") if row.get("Workbook_ID") == WORKBOOK_ID and row.get("Test_ID") in comparison else None
    if path == REGISTER_PATHS[2]:
        return lambda row: row.get("Run_ID") if row.get("Workbook_ID") == WORKBOOK_ID and row.get("Test_ID") in comparison else None
    if path == REGISTER_PATHS[3]:
        return lambda row: row.get("Measurement_ID") if row.get("Workbook_ID") == WORKBOOK_ID and row.get("Test_ID") in comparison else None
    if path == REGISTER_PATHS[4]:
        return lambda row: row.get("Latest_Run_ID") if row.get("Workbook_ID") == WORKBOOK_ID and row.get("Test_ID") in comparison else None
    if path == REGISTER_PATHS[5]:
        return lambda row: row.get("Evaluation_ID") if row.get("Workbook_ID") == WORKBOOK_ID and row.get("Test_ID") in comparison else None
    if path == REGISTER_PATHS[6]:
        return lambda row: row.get("Failure_ID") if row.get("Workbook_ID") == WORKBOOK_ID and row.get("Test_ID") in comparison else None
    if path == REGISTER_PATHS[7]:
        return lambda row: row.get("Evidence_ID") if row.get("Workbook_ID") == WORKBOOK_ID and (row.get("Test_ID") in comparison or row.get("Test_ID") == "ADAPTIVE-COMPARISON") else None
    if path == REGISTER_PATHS[8]:
        return lambda row: row.get("Section_or_Test_ID") if row.get("Workbook_ID") == WORKBOOK_ID and row.get("Section_or_Test_ID") in {str(index) for index in range(1, 12)} else None
    if path == REGISTER_PATHS[9]:
        return lambda row: row.get("Record_ID") if row.get("Workbook_ID") == WORKBOOK_ID and row.get("Record_ID") in {"WR-036", "WR-037"} else None
    if path == MANIFEST_PATH:
        return lambda row: row.get("Workbook_ID") if row.get("Workbook_ID") == WORKBOOK_ID else None
    raise ValueError(f"no owned slice for {path}")


def _global_unique_key(path: str) -> Callable[[Mapping[str, str]], str | None]:
    fields = {
        REGISTER_PATHS[0]: "Test_ID",
        REGISTER_PATHS[1]: "Configuration_ID",
        REGISTER_PATHS[2]: "Run_ID",
        REGISTER_PATHS[3]: "Measurement_ID",
        REGISTER_PATHS[4]: "Latest_Run_ID",
        REGISTER_PATHS[5]: "Evaluation_ID",
        REGISTER_PATHS[6]: "Failure_ID",
        REGISTER_PATHS[7]: "Evidence_ID",
        REGISTER_PATHS[9]: "Record_ID",
    }
    if path == REGISTER_PATHS[8]:
        return lambda row: (
            f"{row.get('Workbook_ID')}:{row.get('Section_or_Test_ID')}"
            if row.get("Workbook_ID") and row.get("Section_or_Test_ID")
            else None
        )
    field = fields.get(path)
    if field is None:
        raise ValueError(f"no global unique key for {path}")
    return lambda row: row.get(field)


def _row_context(row: Mapping[str, str]) -> int:
    direct = row.get("Context_Target_Tokens")
    if direct and direct.isdigit():
        return int(direct)
    configuration = row.get("Configuration_ID", "")
    suffix = configuration.rsplit("-", 1)[-1]
    if suffix.isdigit():
        return int(suffix)
    run_id = row.get("Run_ID") or row.get("Latest_Run_ID") or ""
    parts = run_id.split("-")
    for part in reversed(parts):
        if part.isdigit():
            return int(part)
    for part in row.get("Notes", "").split(";"):
        name, separator, value = part.strip().partition("=")
        if separator and name == "context_tokens" and value.isdigit():
            return int(value)
    return -1


def _replacement_sort_key(
    path: str, comparison_ids: Sequence[str]
) -> Callable[[Mapping[str, str]], object]:
    order = {test_id: index for index, test_id in enumerate(COMPARISON_RUN_ORDER)}

    def identity(row: Mapping[str, str]) -> int:
        test_id = row.get("Test_ID", "")
        if test_id == "ADAPTIVE-COMPARISON":
            return -1
        if test_id not in order or test_id not in set(comparison_ids):
            raise ValueError(f"replacement row has unknown comparison identity: {test_id}")
        return order[test_id]

    if path == REGISTER_PATHS[0]:
        return lambda row: (identity(row),)
    if path in {REGISTER_PATHS[1], REGISTER_PATHS[2], REGISTER_PATHS[4], REGISTER_PATHS[6]}:
        return lambda row: (identity(row), _row_context(row))
    if path == REGISTER_PATHS[3]:
        return lambda row: (
            identity(row),
            _row_context(row),
            int(row.get("Repetition_Number", "0")),
        )
    if path == REGISTER_PATHS[5]:
        return lambda row: (
            identity(row),
            _row_context(row),
            PROMPT_IDS.index(row.get("Prompt_ID", "")),
        )
    if path == REGISTER_PATHS[7]:
        return lambda row: (
            identity(row),
            _row_context(row),
            row.get("Evidence_Type", ""),
            row.get("Evidence_ID", ""),
        )
    if path == REGISTER_PATHS[8]:
        return lambda row: (int(row.get("Section_or_Test_ID", "0")),)
    if path == REGISTER_PATHS[9]:
        return lambda row: (int(row.get("Record_ID", "WR-0").split("-")[-1]),)
    raise ValueError(f"no replacement sort key for {path}")


def _load_script(path: Path, name: str) -> Any:
    specification = importlib.util.spec_from_file_location(name, path)
    if specification is None or specification.loader is None:
        raise RuntimeError(f"cannot import staged generator dependency: {path}")
    module = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(module)
    return module


def _generate_real_docx(
    markdown_path: Path, docx_path: Path, revision_csv_path: Path
) -> None:
    root = Path(__file__).resolve().parents[3]
    generator = _load_script(
        root / "scripts/testing/tools/Generate-Controlled-Workbooks.py",
        "_wb04_controlled_generator",
    )
    revisions = _load_script(
        root / "scripts/testing/tools/Apply-Workbook-Revision-History.py",
        "_wb04_revision_history",
    )
    base_docx = docx_path.with_name(f".{docx_path.name}.base")
    try:
        generator.convert_markdown_to_docx(markdown_path, base_docx)
        histories = revisions.load_history(revision_csv_path)
        revisions.process(base_docx, docx_path, histories[WORKBOOK_ID])
    finally:
        base_docx.unlink(missing_ok=True)


def _audit_real_docx(
    docx_path: Path, manifest_path: Path, markdown_path: Path, profile: Any
) -> Mapping[str, Any]:
    from scripts.testing.campaigns.openvino.docx_audit import audit_comparison_docx

    return audit_comparison_docx(docx_path, manifest_path, markdown_path, profile)


def _comparison_profile(matrix_sha256: str) -> Any:
    from scripts.testing.campaigns.openvino.docx_audit import comparison_audit_profile

    return comparison_audit_profile(matrix_sha256)


def _run_revision_validator(repo_root: Path) -> Mapping[str, Any]:
    script = (
        Path(repo_root).resolve()
        / "scripts/testing/tools/Validate-Workbook-Revision-Control.py"
    )
    if not script.is_file():
        raise ValueError("revision-control validator is missing from the validation root")
    validator = _load_script(script, f"_wb04_revision_validator_{id(script)}")
    output = io.StringIO()
    with contextlib.redirect_stdout(output):
        result = validator.main()
    text = output.getvalue()
    if result != 0:
        raise ValueError("workbook revision control refused candidate: " + text.strip())
    if "WORKBOOK REVISION CONTROL: PASS" not in text:
        raise ValueError("workbook revision control did not emit its PASS contract")
    return {"accepted": True, "output": text}


def _validate_revision_overlay(
    *,
    repo_root: Path,
    staging_root: Path,
    files: Mapping[str, bytes],
    validator: Callable[[Path], Mapping[str, Any]],
) -> None:
    overlay = staging_root / "o"
    root = Path(repo_root).resolve()
    docs_source = _inside(
        root / "docs/testing", root, "canonical revision-control source docs/testing"
    )
    shutil.copytree(docs_source, overlay / "docs/testing")
    script_source = _inside(
        root / "scripts/testing/tools/Validate-Workbook-Revision-Control.py",
        root,
        "canonical revision-control validator source",
    )
    if not script_source.is_file():
        raise ValueError(
            "revision-control validator is missing from the explicit repo_root"
        )
    script_target = overlay / "scripts/testing/tools/Validate-Workbook-Revision-Control.py"
    script_target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(script_source, script_target)
    for relative, value in files.items():
        target = overlay / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(value)
    result = validator(overlay)
    if not isinstance(result, Mapping) or result.get("accepted") is not True:
        raise ValueError("workbook revision-control validator did not accept candidate")


def _canonical_json_bytes(value: Mapping[str, Any]) -> bytes:
    return (
        json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        )
        + "\n"
    ).encode("utf-8")


def _staging_parent(repo_root: Path) -> Path:
    root = Path(repo_root).resolve()
    system_temporary = Path(tempfile.gettempdir()).resolve()
    if (
        os.name == "nt"
        and root.drive.casefold() == system_temporary.drive.casefold()
    ):
        return system_temporary
    return root.parent


def _runtime_matrix_identity_hashes(
    matrix_path: Path,
    matrix_sha256: str,
    comparison_ids: Sequence[str],
) -> dict[str, str]:
    """Recreate Task 3's per-case matrix identity from the exact matrix file."""

    path = Path(matrix_path).resolve()
    expected_file_sha256 = _sha256(matrix_sha256, "matrix SHA-256")
    if not path.is_file() or _hash_file(path) != expected_file_sha256:
        raise ValueError("validated matrix file has hash drift")
    payload = _strict_json(path, "validated adaptive comparison matrix")
    raw_cases = payload.get("cases")
    if not isinstance(raw_cases, list) or not all(
        isinstance(case, Mapping) for case in raw_cases
    ):
        raise ValueError("validated adaptive comparison matrix cases are invalid")
    by_id: dict[str, Mapping[str, Any]] = {}
    for raw_case in raw_cases:
        test_id = raw_case.get("test_id")
        if not isinstance(test_id, str) or test_id in by_id:
            raise ValueError("validated adaptive comparison matrix identities are invalid")
        by_id[test_id] = raw_case
    if set(by_id) != set(comparison_ids):
        raise ValueError("validated adaptive comparison matrix identities are incomplete")
    result: dict[str, str] = {}
    for test_id in comparison_ids:
        identity = {
            "path": str(path),
            "file_sha256": expected_file_sha256,
            "schema_version": payload.get("schema_version"),
            "source_identity": payload.get("source_identity"),
            "build_identity": payload.get("build_identity"),
            "case": dict(by_id[test_id]),
        }
        encoded = json.dumps(
            identity,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        ).encode("utf-8")
        result[test_id] = hashlib.sha256(encoded).hexdigest()
    return result


def _stage_path(staging_root: Path, relative: str) -> Path:
    destination = staging_root / Path(relative)
    destination.parent.mkdir(parents=True, exist_ok=True)
    return destination


def _manifest_row(
    original: Mapping[str, str], *, markdown_bytes: bytes, docx_bytes: bytes
) -> dict[str, str]:
    row = dict(original)
    row.update(
        {
            "Canonical_Template_SHA256": _hash_bytes(markdown_bytes),
            "Last_Validated_DOCX_SHA256": _hash_bytes(docx_bytes),
            "Status": (
                "WB-04 revision 1.9: evidence-bound adaptive U4, U8, FP16, "
                "TBQ4 and TBQ3 comparison checkpoint"
            ),
            "Generation_Command": (
                "python .\\scripts\\testing\\Generate-Controlled-Workbooks.py "
                "--workbook-id WB-04; python .\\scripts\\testing\\"
                "Apply-Workbook-Revision-History.py --workbook-id WB-04"
            ),
            "Revision": "1.9",
        }
    )
    return row


def _rewrite_manifest_in_place(path: Path, replacement: Mapping[str, str]) -> bytes:
    header, rows, bom, newline = _read_csv_strict(path)
    matches = [index for index, row in enumerate(rows) if row.get("Workbook_ID") == WORKBOOK_ID]
    if matches != [3]:
        raise ValueError("controlled manifest must contain WB-04 exactly once in canonical order")
    rows[matches[0]] = dict(replacement)
    return _encode_csv(header, rows, bom=bom, newline=newline)


def _validate_evidence_commit(value: str) -> str:
    if (
        not isinstance(value, str)
        or len(value) != 40
        or any(character not in _HEX for character in value)
    ):
        raise ValueError("evidence_commit must be a 40-character lowercase Git SHA")
    return value


def build_publication_bundle(
    release_input: Path | ComparisonRelease,
    *,
    repo_root: Path,
    require_complete: bool = False,
    evidence_commit: str,
    matrix_cases: Sequence[OpenVINOCase] | None = None,
    matrix_path: Path | None = None,
    matrix_sha256: str | None = None,
    reconciliation_input_path: Path | None = None,
    reconciliation_input_sha256: str | None = None,
    publication_state_path: Path | None = None,
    generate_docx: Callable[[Path, Path, Path], None] | None = None,
    audit_docx_fn: Callable[[Path, Path, Path, Any], Mapping[str, Any]] | None = None,
    revision_validator: Callable[[Path], Mapping[str, Any]] | None = None,
    staging_observer: Callable[[Path], None] | None = None,
) -> PublicationBundle:
    """Build and validate all candidate bytes without touching destinations."""

    root = Path(repo_root).resolve()
    evidence_commit = _validate_evidence_commit(evidence_commit)
    synthetic_projection = isinstance(release_input, ComparisonRelease)
    if synthetic_projection:
        if any(
            value is None
            for value in (
                matrix_cases,
                matrix_path,
                matrix_sha256,
                reconciliation_input_path,
                reconciliation_input_sha256,
            )
        ):
            raise ValueError("synthetic release projection requires explicit validated metadata")
        inputs = PublicationInputs(
            release=release_input,
            matrix_cases=tuple(matrix_cases or ()),
            matrix_path=Path(matrix_path),  # type: ignore[arg-type]
            matrix_sha256=str(matrix_sha256),
            reconciliation_input_path=Path(reconciliation_input_path),  # type: ignore[arg-type]
            reconciliation_input_sha256=str(reconciliation_input_sha256),
            publication_state_path=(
                Path(publication_state_path)
                if publication_state_path is not None
                else Path(reconciliation_input_path).parent / "publication-state.json"  # type: ignore[arg-type]
            ),
        )
    else:
        inputs = _load_publication_inputs(Path(release_input), root)
    campaign_closed = False
    release_complete = False
    try:
        validate_closed_campaign(inputs.release)
    except ValueError:
        pass
    else:
        campaign_closed = True
        try:
            validate_complete_release(inputs.release)
        except ValueError:
            pass
        else:
            release_complete = True
    if require_complete:
        validate_complete_release(inputs.release)
        campaign_closed = True
        release_complete = True
    cases = tuple(inputs.matrix_cases)
    validated_cases = _validate_matrix_cases(cases)
    comparison_ids = tuple(
        test_id for test_id in COMPARISON_RUN_ORDER if test_id in validated_cases
    )
    matrix_sha = _sha256(inputs.matrix_sha256, "matrix SHA-256")
    reconciliation_sha = _sha256(
        inputs.reconciliation_input_sha256, "reconciliation input SHA-256"
    )
    runtime_matrix_identities = (
        {test_id: matrix_sha for test_id in comparison_ids}
        if synthetic_projection
        else _runtime_matrix_identity_hashes(
            inputs.matrix_path,
            matrix_sha,
            comparison_ids,
        )
    )
    matrix_relative = _repository_relative(inputs.matrix_path, root, "validated matrix")
    release_relative = _repository_relative(
        inputs.reconciliation_input_path, root, "reconciliation input"
    )
    state_path = _inside(inputs.publication_state_path, root, "publication state")
    state_relative = Path(state_path.relative_to(root).as_posix())
    display_release = _normalize_release_for_display(inputs.release, root)
    projected = {
        path: [dict(row) for row in rows]
        for path, rows in register_rows_from_release(
            display_release,
            matrix_cases=cases,
            matrix_path=matrix_relative,
            matrix_sha256=matrix_sha,
            evidence_commit=evidence_commit,
            reconciliation_input_sha256=reconciliation_sha,
            runtime_matrix_identity_sha256_by_test_id=runtime_matrix_identities,
            publication_final=require_complete,
            campaign_closed=campaign_closed,
            release_complete=release_complete,
        ).items()
    }
    for row in projected[REGISTER_PATHS[7]]:
        if row.get("Evidence_Type") == "reconciliation-input":
            row["Repository_Path"] = release_relative.as_posix()
            row["File_Name"] = release_relative.name
    for row in projected[REGISTER_PATHS[8]]:
        row["Source_Result_Path"] = state_relative.as_posix()

    staging = Path(tempfile.mkdtemp(prefix=".s-", dir=_staging_parent(root)))
    primary_error: BaseException | None = None
    try:
        if staging_observer is not None:
            staging_observer(staging)
        manifest_source = _inside(
            root / MANIFEST_PATH, root, f"canonical source {MANIFEST_PATH}"
        )
        _header, manifest_rows = _read_manifest_rows(manifest_source)
        wb04_manifest = next(row for row in manifest_rows if row["Workbook_ID"] == WORKBOOK_ID)
        markdown_source = _inside(
            root / MARKDOWN_PATH, root, f"canonical source {MARKDOWN_PATH}"
        )
        source = markdown_source.read_text(encoding="utf-8-sig")
        source = _insert_controlled_metadata(source, wb04_manifest)
        source = _replace_frozen_matrix_fact(
            source,
            matrix_path=matrix_relative,
            matrix_sha256=matrix_sha,
            matrix_cases=cases,
        )
        markdown = render_v19_workbook(source, display_release)
        validate_comparison_workbook_text(markdown, display_release)
        markdown_bytes = markdown.encode("utf-8")
        markdown_stage = _stage_path(staging, MARKDOWN_PATH)
        markdown_stage.write_bytes(markdown_bytes)

        files: dict[str, bytes] = {MARKDOWN_PATH: markdown_bytes}
        for register_path in REGISTER_PATHS[:-1]:
            register_source = _inside(
                root / register_path, root, f"canonical source {register_path}"
            )
            candidate = rewrite_csv_slice_atomically(
                register_source,
                owned_key=_owned_key(register_path, comparison_ids),
                replacement_rows=projected[register_path],
                unique_key=_global_unique_key(register_path),
                replacement_sort_key=_replacement_sort_key(
                    register_path, comparison_ids
                ),
            )
            files[register_path] = candidate
            _stage_path(staging, register_path).write_bytes(candidate)

        docx_stage = _stage_path(staging, DOCX_PATH)
        (generate_docx or _generate_real_docx)(
            markdown_stage,
            docx_stage,
            staging / REGISTER_PATHS[9],
        )
        if not docx_stage.is_file():
            raise ValueError("DOCX generation did not create the staged output")
        docx_bytes = docx_stage.read_bytes()
        files[DOCX_PATH] = docx_bytes

        manifest_replacement = _manifest_row(
            wb04_manifest,
            markdown_bytes=markdown_bytes,
            docx_bytes=docx_bytes,
        )
        manifest_bytes = _rewrite_manifest_in_place(
            manifest_source, manifest_replacement
        )
        files[MANIFEST_PATH] = manifest_bytes
        manifest_stage = _stage_path(staging, MANIFEST_PATH)
        manifest_stage.write_bytes(manifest_bytes)
        _read_manifest_rows(manifest_stage)

        report = (audit_docx_fn or _audit_real_docx)(
            docx_stage,
            manifest_stage,
            markdown_stage,
            _comparison_profile(matrix_sha),
        )
        if report.get("accepted") is not True:
            raise ValueError("DOCX audit did not accept the staged comparison")

        ordered_files = {path: files[path] for path in PUBLICATION_PATHS[:-1]}
        _validate_revision_overlay(
            repo_root=root,
            staging_root=staging,
            files=ordered_files,
            validator=revision_validator or _run_revision_validator,
        )
        published_hashes = {
            path: _hash_bytes(value) for path, value in ordered_files.items()
        }
        state_payload = {
            "schema": _PUBLICATION_STATE_SCHEMA,
            "evidence_commit": evidence_commit,
            "matrix": {"path": matrix_relative.as_posix(), "sha256": matrix_sha},
            "reconciliation_input": {
                "path": release_relative.as_posix(),
                "sha256": reconciliation_sha,
            },
            "published_hashes": published_hashes,
        }
        state_bytes = _canonical_json_bytes(state_payload)
        return PublicationBundle(
            files=ordered_files,
            publication_state_path=state_path,
            publication_state_bytes=state_bytes,
        )
    except BaseException as error:
        primary_error = error
        raise
    finally:
        try:
            shutil.rmtree(staging)
        except BaseException as cleanup_error:
            if primary_error is not None:
                raise BaseExceptionGroup(
                    "publication staging failed and cleanup also failed",
                    [primary_error, cleanup_error],
                ) from None
            raise


def _destination_map(
    repo_root: Path, publication_state_path: Path
) -> dict[str, Path]:
    root = Path(repo_root).resolve()
    result = {
        path: _inside(root / path, root, f"publication destination {path}")
        for path in PUBLICATION_PATHS[:-1]
    }
    result["publication-state.json"] = _inside(
        Path(publication_state_path),
        root,
        "publication destination publication-state.json",
    )
    return result


class _PublisherLock:
    """An atomic cross-process lock shared by every publisher for one repo."""

    def __init__(self, repo_root: Path):
        root = Path(repo_root).resolve()
        identity = hashlib.sha256(str(root).encode("utf-8")).hexdigest()[:16]
        self.path = root.parent / f".{root.name}.wb04-publisher-{identity}.lock"
        self._held = False

    def __enter__(self) -> "_PublisherLock":
        try:
            self.path.mkdir()
        except FileExistsError as error:
            raise RuntimeError(f"WB-04 comparison publisher is locked: {self.path}") from error
        self._held = True
        (self.path / "owner.txt").write_text(
            f"pid={os.getpid()}\n", encoding="utf-8"
        )
        return self

    def __exit__(self, *_: Any) -> None:
        if not self._held:
            return
        self._held = False
        (self.path / "owner.txt").unlink(missing_ok=True)
        self.path.rmdir()


def _snapshot(destinations: Mapping[str, Path]) -> dict[str, bytes | None]:
    return {
        name: path.read_bytes() if path.exists() else None
        for name, path in destinations.items()
    }


def _snapshot_matches(
    destinations: Mapping[str, Path], snapshot: Mapping[str, bytes | None]
) -> bool:
    return all(
        (path.read_bytes() if path.exists() else None) == snapshot[name]
        for name, path in destinations.items()
    )


def _atomic_restore(path: Path, value: bytes | None) -> None:
    if value is None:
        path.unlink(missing_ok=True)
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, name = tempfile.mkstemp(
        dir=path.parent, prefix=f".{path.name}.", suffix=".restore"
    )
    temporary = Path(name)
    try:
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(value)
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def _rollback_destination(path: Path, repo_root: Path, label: str) -> Path:
    """Re-resolve a snapshotted target without trusting a stale parent link."""

    resolved = Path(path).resolve()
    root = Path(repo_root).resolve()
    if resolved != root and root not in resolved.parents:
        raise ValueError(f"rollback destination {label} is outside repo_root")
    return resolved


def validate_published_bundle(
    repo_root: Path, publication_state_path: Path
) -> dict[str, Any]:
    root = Path(repo_root).resolve()
    state_path = _inside(publication_state_path, root, "publication state")
    state = _strict_json(state_path, "publication state")
    required = {
        "schema", "evidence_commit", "matrix", "reconciliation_input", "published_hashes"
    }
    if set(state) != required or state.get("schema") != _PUBLICATION_STATE_SCHEMA:
        raise ValueError("publication state fields are incomplete or unexpected")
    _validate_evidence_commit(state.get("evidence_commit"))
    references: dict[str, tuple[Path, str]] = {}
    for label in ("matrix", "reconciliation_input"):
        reference = state.get(label)
        if not isinstance(reference, Mapping) or set(reference) != {"path", "sha256"}:
            raise ValueError(f"publication state {label} reference is invalid")
        relative = _repository_relative(Path(reference["path"]), root, label)
        source = _inside(root / relative, root, f"publication state {label}")
        digest = _sha256(reference["sha256"], f"publication state {label} SHA-256")
        references[label] = (source, digest)

    matrix_path, matrix_sha256 = references["matrix"]
    if not matrix_path.is_file() or _hash_file(matrix_path) != matrix_sha256:
        raise ValueError("publication state matrix is missing or has hash drift")

    reconciliation_path, reconciliation_sha256 = references["reconciliation_input"]
    if not reconciliation_path.is_file():
        raise ValueError("publication state reconciliation input is missing or has hash drift")
    try:
        reconciliation_payload = _strict_json(
            reconciliation_path, "publication state reconciliation input"
        )
    except ValueError as error:
        raise ValueError("publication state reconciliation input has hash drift") from error
    embedded_hash = reconciliation_payload.get("release_input_sha256")
    if embedded_hash is None:
        reference_matches = _hash_file(reconciliation_path) == reconciliation_sha256
    else:
        unsigned = dict(reconciliation_payload)
        unsigned.pop("release_input_sha256", None)
        calculated = hashlib.sha256(
            json.dumps(
                unsigned,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=True,
                allow_nan=False,
            ).encode("utf-8")
        ).hexdigest()
        reference_matches = (
            embedded_hash == reconciliation_sha256 == calculated
        )
    if not reference_matches:
        raise ValueError("publication state reconciliation input has hash drift")

    loaded = load_comparison_release_input(reconciliation_path)
    loaded_matrix = _inside(
        Path(loaded.matrix_path), root, "Task 7 reconciliation matrix binding"
    )
    if (
        loaded_matrix != matrix_path
        or _sha256(loaded.matrix_sha256, "Task 7 matrix SHA-256") != matrix_sha256
    ):
        raise ValueError("Task 7 reconciliation input matrix binding disagrees with publication state")
    hashes = state.get("published_hashes")
    if not isinstance(hashes, Mapping):
        raise ValueError("publication state hash inventory is invalid")
    expected = set(PUBLICATION_PATHS[:-1])
    actual = set(hashes)
    if actual != expected:
        missing = sorted(expected - actual)
        extra = sorted(actual - expected)
        raise ValueError(
            f"publication state has missing/extra/unknown paths: missing={missing}; extra={extra}"
        )
    normalized: dict[str, str] = {}
    for relative in PUBLICATION_PATHS[:-1]:
        digest = _sha256(hashes.get(relative), f"published {relative} SHA-256")
        path = _inside(root / relative, root, f"published destination {relative}")
        if not path.is_file() or _hash_file(path) != digest:
            raise ValueError(f"published file hash drift: {relative}")
        normalized[relative] = digest
    _header, manifest_rows = _read_manifest_rows(root / MANIFEST_PATH)
    wb04 = next(row for row in manifest_rows if row["Workbook_ID"] == WORKBOOK_ID)
    if (
        wb04.get("Revision") != "1.9"
        or wb04.get("Canonical_Template_SHA256") != normalized[MARKDOWN_PATH]
        or wb04.get("Last_Validated_DOCX_SHA256") != normalized[DOCX_PATH]
    ):
        raise ValueError("WB-04 controlled manifest hashes or revision are invalid")
    return {
        "published_hashes": normalized,
        "publication_state_path": str(state_path),
        "publication_state_sha256": _hash_file(state_path),
    }


def publish_reconciled_checkpoint(
    release_input: Path,
    *,
    repo_root: Path,
    require_complete: bool,
    evidence_commit: str,
    generate_docx: Callable[[Path, Path, Path], None] | None = None,
    audit_docx_fn: Callable[[Path, Path, Path, Any], Mapping[str, Any]] | None = None,
    replace_observer: Callable[[str], None] | None = None,
    before_replace: Callable[[], None] | None = None,
    staging_observer: Callable[[Path], None] | None = None,
    revision_validator: Callable[[Path], Mapping[str, Any]] | None = None,
) -> dict[str, Any]:
    """Atomically publish one explicit reconciled checkpoint or restore all bytes."""

    root = Path(repo_root).resolve()
    release_path = _inside(Path(release_input), root, "release input")
    state_path = _inside(release_path.parent / "publication-state.json", root, "publication state")
    with _PublisherLock(root):
        destinations = _destination_map(root, state_path)
        previous = _snapshot(destinations)
        bundle = build_publication_bundle(
            release_path,
            repo_root=root,
            require_complete=require_complete,
            evidence_commit=evidence_commit,
            generate_docx=generate_docx,
            audit_docx_fn=audit_docx_fn,
            revision_validator=revision_validator,
            staging_observer=staging_observer,
        )
        if bundle.publication_state_path != state_path:
            raise ValueError(
                "publication state target differs from the explicit release checkpoint"
            )
        if before_replace is not None:
            before_replace()
        if not _snapshot_matches(_destination_map(root, state_path), previous):
            raise RuntimeError("publication destination drift detected before first replace")

        staging = Path(
            tempfile.mkdtemp(prefix=".r-", dir=_staging_parent(root))
        )
        replaced: list[str] = []
        primary_error: BaseException | None = None
        try:
            staged: dict[str, Path] = {}
            for relative in PUBLICATION_PATHS[:-1]:
                candidate = _stage_path(staging, relative)
                candidate.write_bytes(bundle.files[relative])
                staged[relative] = candidate
            state_candidate = _stage_path(staging, "publication-state.json")
            state_candidate.write_bytes(bundle.publication_state_bytes)
            staged["publication-state.json"] = state_candidate
            try:
                for name in PUBLICATION_PATHS:
                    current_destinations = _destination_map(root, state_path)
                    destination = current_destinations[name]
                    current = (
                        destination.read_bytes() if destination.exists() else None
                    )
                    if current != previous[name]:
                        raise RuntimeError(
                            f"publication destination drift detected before replace: {name}"
                        )
                    destination.parent.mkdir(parents=True, exist_ok=True)
                    os.replace(staged[name], destination)
                    replaced.append(name)
                    if replace_observer is not None:
                        replace_observer(Path(name).name)
                result = validate_published_bundle(root, state_path)
                post_revision = (revision_validator or _run_revision_validator)(root)
                if (
                    not isinstance(post_revision, Mapping)
                    or post_revision.get("accepted") is not True
                ):
                    raise ValueError(
                        "post-publish workbook revision-control validator did not accept publication"
                    )
            except BaseException as publication_error:
                rollback_errors: list[BaseException] = []
                for name in reversed(replaced):
                    try:
                        restore_destination = _rollback_destination(
                            destinations[name], root, name
                        )
                        _atomic_restore(restore_destination, previous[name])
                    except BaseException as rollback_error:
                        rollback_errors.append(rollback_error)
                if rollback_errors:
                    raise BaseExceptionGroup(
                        "publication failed and rollback was incomplete",
                        [publication_error, *rollback_errors],
                    ) from None
                raise
            return result
        except BaseException as error:
            primary_error = error
            raise
        finally:
            try:
                shutil.rmtree(staging)
            except BaseException as cleanup_error:
                if primary_error is not None:
                    raise BaseExceptionGroup(
                        "publication replacement failed and cleanup also failed",
                        [primary_error, cleanup_error],
                    ) from None
                raise


def parse_args(argv: Sequence[str] | None = None) -> argparse.Namespace:
    """Parse the explicit, evidence-bound publisher command line."""

    parser = argparse.ArgumentParser(
        description="Build, validate, and optionally publish one WB-04 checkpoint."
    )
    parser.add_argument("--release-input", type=Path, required=True)
    parser.add_argument("--repo-root", type=Path, required=True)
    parser.add_argument("--evidence-commit", required=True)
    parser.add_argument("--require-complete", action="store_true")
    parser.add_argument("--check-only", action="store_true")
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    """Run the publisher CLI and emit one deterministic JSON result."""

    args = parse_args(argv)
    release_input = args.release_input.resolve()
    repo_root = args.repo_root.resolve()
    try:
        evidence_commit = _validate_evidence_commit(args.evidence_commit)
        if args.check_only:
            bundle = build_publication_bundle(
                release_input,
                repo_root=repo_root,
                require_complete=args.require_complete,
                evidence_commit=evidence_commit,
            )
            result: dict[str, Any] = {
                "check_only": True,
                "publication_state_path": str(bundle.publication_state_path),
                "publication_state_sha256": _hash_bytes(
                    bundle.publication_state_bytes
                ),
                "published_hashes": {
                    path: _hash_bytes(value)
                    for path, value in bundle.files.items()
                },
            }
        else:
            result = dict(
                publish_reconciled_checkpoint(
                    release_input,
                    repo_root=repo_root,
                    require_complete=args.require_complete,
                    evidence_commit=evidence_commit,
                )
            )
        print(
            json.dumps(
                result,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=True,
                allow_nan=False,
            )
        )
        return 0
    except (OSError, RuntimeError, ValueError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1


__all__ = [
    "PUBLICATION_PATHS",
    "PublicationBundle",
    "PublicationInputs",
    "REGISTER_PATHS",
    "build_publication_bundle",
    "main",
    "parse_args",
    "publish_reconciled_checkpoint",
    "register_rows_from_release",
    "rewrite_csv_slice_atomically",
    "validate_published_bundle",
]


if __name__ == "__main__":
    raise SystemExit(main())
