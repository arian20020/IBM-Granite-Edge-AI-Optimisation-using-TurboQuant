"""Typed, fixed-order input contract for the OpenVINO format boundary retest."""

from __future__ import annotations

import hashlib
import json
import math
import os
import re
import subprocess
import time
from dataclasses import dataclass, field, replace
from pathlib import Path
from typing import Any, Callable, Literal, Mapping

from scripts.testing.adjudicate_official_openvino_quality import (
    _prompt_controls,
    deterministic_gate,
)
from scripts.testing.official_openvino.adaptive_campaign_spec import (
    _build_root,
    _directory_sha256,
    _paths_overlap,
)
from scripts.testing.official_openvino.artifact_inventory import sha256_file
from scripts.testing.official_openvino.adaptive_metrics import (
    build_adaptive_runtime_sample,
    summarize_adaptive_runtime_samples,
)
from scripts.testing.official_openvino.adaptive_quality import (
    AdaptiveQualityCampaignInput,
    _input_evidence_paths,
    _validate_summary as _validate_quality_summary,
)
from scripts.testing.official_openvino.quality_campaign import (
    load_accepted_quality_campaign,
)
from scripts.testing.measure_official_openvino import (
    _adaptive_record,
    _persisted_record,
    build_worker_environment,
)
from scripts.testing.official_openvino.owned_process_guard import (
    CREATE_SUSPENDED,
    KillOnCloseJob,
    _resume_suspended_process,
    available_ram_bytes,
)
from scripts.testing.official_openvino.runtime_measurement import (
    build_runtime_property_spec,
)
from scripts.testing.official_openvino.quality_contracts import (
    load_quality_contract,
)
from scripts.testing.run_official_openvino_quality import (
    _load_frozen_rubric,
    load_prompt_contract,
)
from scripts.testing.official_openvino.workload import build_context_workload


_ROOT = Path(__file__).resolve().parents[3]
_CPU_ORDER = (
    ("u4", "TBQ3"), ("u4", "TBQ4"), ("u4", "STANDARD"),
    ("u8", "TBQ3"), ("u8", "TBQ4"), ("u8", "STANDARD"),
    ("f16", "TBQ3"), ("f16", "TBQ4"), ("f16", "STANDARD"),
)
_FORMAL_METRICS = (
    "available_ram_min_mb", "cpu_percent", "decode_tps",
    "generation_duration_ms", "gpu_memory_peak_mb", "gpu_percent", "kv_mb",
    "load_ms", "peak_private_mb", "peak_working_set_mb", "prompt_tps",
    "tpot_ms", "ttft_ms",
)
_SHA256_HEX = frozenset("0123456789abcdef")


@dataclass(frozen=True)
class ProjectionFile:
    path: Path
    sha256: str


@dataclass(frozen=True)
class ExecutableBoundaryInput:
    case_internal_id: str
    runtime_spec: ProjectionFile


@dataclass(frozen=True)
class TerminalBoundaryInput:
    case_internal_id: str
    descriptor: ProjectionFile


@dataclass(frozen=True)
class BoundaryEvidenceProjection:
    repository_root: Path
    campaign_root: Path
    build_root: Path
    boundary_manifest: ProjectionFile
    comparison_matrix: ProjectionFile
    runtime_specs: tuple[ExecutableBoundaryInput, ...]
    terminal_prerequisites: tuple[TerminalBoundaryInput, ...]
    projection_index: ProjectionFile


@dataclass(frozen=True)
class BoundaryCase:
    internal_id: str
    label: str
    lane: Literal["cpu", "gpu-control"]
    order: int
    weight_precision: Literal["u4", "u8", "f16"]
    key_algorithm: Literal["STANDARD", "TBQ3", "TBQ4"]
    value_algorithm: Literal["STANDARD", "TBQ3", "TBQ4"]
    key_precision: Literal["f16", "u4", "u3"]
    value_precision: Literal["f16", "u4", "u3"]
    device: Literal["CPU", "GPU"]
    context: int
    artifact_manifest_path: Path | None


@dataclass(frozen=True)
class BoundaryManifest:
    cpu_cases: tuple[BoundaryCase, ...]
    gpu_cases: tuple[BoundaryCase, ...]
    source_path: Path
    sha256: str


@dataclass(frozen=True)
class BoundaryCampaignConfig:
    """Immutable controller settings for one serial format-boundary campaign."""

    repository_root: Path
    campaign_root: Path
    manifest_path: Path
    prompt_set_path: Path
    rubric_path: Path
    comparison_matrix_path: Path | None = None
    build_root: Path | None = None
    python_executable: Path | None = None
    python_site_packages: Path | None = None
    openvino_libraries: Path | None = None
    sampler_script: Path | None = None
    rendered_root: Path | None = None
    resume: bool = False
    runtime_timeout_seconds: float = field(init=False, default=180.0)
    quality_timeout_seconds: float = field(init=False, default=90.0)
    row_timeout_seconds: float = field(init=False, default=720.0)
    launch_minimum_available_ram_mib: int = field(init=False, default=4096)
    emergency_minimum_available_ram_mib: int = field(init=False, default=3072)
    max_clean_retries: int = field(init=False, default=1)


class RowFailure(RuntimeError):
    """A governed row failure whose retry safety has already been decided."""

    def __init__(
        self,
        reason_code: str,
        *,
        role: str = "measurement",
        stage: str | None = None,
        hard: bool,
        retryable: bool | None = None,
        receipt: Path | None = None,
        raw_record: Mapping[str, Any] | None = None,
        fingerprint: str | None = None,
        cleanup_proof: Mapping[str, Any] | None = None,
        observed_available_ram_bytes: int | None = None,
        deadline_outcome: str | None = None,
    ):
        super().__init__(reason_code)
        self.reason_code = reason_code
        self.role = role
        self.stage = stage or role
        self.hard = hard
        self.retryable = (not hard) if retryable is None else retryable
        self.receipt = receipt
        self.raw_record = dict(raw_record) if raw_record is not None else None
        self.fingerprint = fingerprint
        self.cleanup_proof = (
            dict(cleanup_proof) if cleanup_proof is not None else None
        )
        self.observed_available_ram_bytes = observed_available_ram_bytes
        self.deadline_outcome = deadline_outcome


_MIB = 1024**2
_ENVELOPE_TEXT = "outside this laptop's configured safe RAM/time envelope"


def _canonical_bytes(value: Mapping[str, Any]) -> bytes:
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True,
                       allow_nan=False) + "\n").encode("utf-8")


def _atomic_write_json(path: Path, value: Mapping[str, Any]) -> None:
    """Commit one state/receipt document without exposing a partial JSON file."""

    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    temporary = target.with_name(f".{target.name}.{os.getpid()}.tmp")
    try:
        with temporary.open("xb") as handle:
            handle.write(_canonical_bytes(value))
            handle.flush()
            os.fsync(handle.fileno())
        temporary.replace(target)
    finally:
        if temporary.exists():
            temporary.unlink()


def _read_json_object(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(Path(path).read_bytes())
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ValueError(f"boundary controller record is unreadable: {path}") from error
    if not isinstance(value, dict):
        raise ValueError(f"boundary controller record is not an object: {path}")
    return value


def _file_sha256(path: Path | None) -> str | None:
    if path is None or not Path(path).is_file():
        return None
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def _evidence_binding(path: Path) -> dict[str, str]:
    source = Path(path).resolve()
    if not source.is_file():
        raise ValueError(f"boundary evidence is missing: {source}")
    return {"path": str(source), "sha256": sha256_file(source)}


def _plain_value(value: Any) -> Any:
    if isinstance(value, Mapping):
        return {key: _plain_value(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [_plain_value(item) for item in value]
    return value


def reconcile_runtime_evidence(
    campaign_input: AdaptiveQualityCampaignInput,
) -> dict[str, Any]:
    """Reopen and recompute all accepted runtime evidence for one row."""

    if not isinstance(campaign_input, AdaptiveQualityCampaignInput):
        raise TypeError("runtime reconciliation requires AdaptiveQualityCampaignInput")
    accepted = load_accepted_quality_campaign(campaign_input)
    root = Path(accepted.campaign_root).resolve()
    sequence_path = Path(campaign_input.attempt_sequence_path).resolve()
    expected_sequence_path = root / "attempt-sequence.json"
    if sequence_path != expected_sequence_path:
        raise ValueError("attempt sequence path does not bind the accepted runtime root")
    sequence = _read_json_object(sequence_path)
    if sequence.get("accepted_sample_count") != 3:
        raise ValueError("runtime reconciliation requires three accepted samples")
    if sequence.get("cleanup_process_count") != 0:
        raise ValueError("runtime attempt sequence cleanup proof is invalid")

    summary = accepted.measurement_summary
    sources = summary.get("sources")
    if not isinstance(sources, (list, tuple)) or len(sources) != 3:
        raise ValueError("runtime reconciliation requires three measured sources")
    source_bindings: list[dict[str, str]] = []
    adaptive_samples: list[dict[str, Any]] = []
    raw_records: list[dict[str, Any]] = []
    seen: set[Path] = set()
    for source in sources:
        if not isinstance(source, Mapping):
            raise ValueError("runtime measured source binding is invalid")
        raw_path = Path(str(source.get("path"))).resolve()
        if raw_path in seen:
            raise ValueError("runtime measured sources must be distinct")
        seen.add(raw_path)
        binding = _evidence_binding(raw_path)
        if source.get("sha256") != binding["sha256"]:
            raise ValueError("runtime measured source hash drift")
        record = _persisted_record(raw_path)
        if record is None:
            raise ValueError("runtime measured source is not canonical evidence")
        adaptive_samples.append(
            build_adaptive_runtime_sample(
                _adaptive_record(record, _plain_value(accepted.identity)), raw_path
            )
        )
        raw_records.append(record)
        source_bindings.append(binding)

    recomputed = summarize_adaptive_runtime_samples(adaptive_samples)
    adaptive_path = root / "adaptive-runtime-summary.json"
    adaptive_summary = _read_json_object(adaptive_path)
    if adaptive_summary != recomputed:
        raise ValueError("adaptive runtime summary does not match raw samples")
    sequence_binding = _evidence_binding(sequence_path)
    cleanup_safe = all(
        record.get("cleanup_process_count") == 0
        and record.get("residual_owned_process_count") == 0
        for record in raw_records
    )
    if not cleanup_safe:
        raise ValueError("runtime measured source cleanup proof is unsafe")
    return {
        "measurement_summary": _evidence_binding(
            root / "measurement-summary.json"
        ),
        "attempt_sequence": sequence_binding,
        "adaptive_runtime_summary": _evidence_binding(adaptive_path),
        "sample_sources": source_bindings,
        "adaptive_summary": adaptive_summary,
        "campaign_identity_sha256": accepted.campaign_identity_sha256,
        "runtime_config_sha256": accepted.runtime_config_sha256,
        "cleanup_proof": {
            "safe": True,
            "source": "validated-attempt-sequence",
            "attempt_sequence": sequence_binding,
            "cleanup_process_count": 0,
            "residual_owned_process_count": 0,
            "emergency_actions": [],
            "active_pids_after_cleanup": [],
        },
    }


def reconcile_quality_evidence(
    campaign_input: AdaptiveQualityCampaignInput,
) -> dict[str, Any]:
    """Reopen a fresh compact-v2 capture and bind its six governed prompts."""

    if not isinstance(campaign_input, AdaptiveQualityCampaignInput):
        raise TypeError("quality reconciliation requires AdaptiveQualityCampaignInput")
    accepted = load_accepted_quality_campaign(campaign_input)
    summary_path = Path(accepted.output_root).resolve() / "capture-summary.json"
    summary, _loaded = _validate_quality_summary(
        accepted,
        summary_path,
        evidence_paths=_input_evidence_paths(campaign_input),
    )
    if (
        summary.get("status") != "passed"
        or summary.get("completed_prompt_ids")
        != ["P1", "P2", "P3", "P4", "P5", "P6"]
        or summary.get("prompt_receipt_count") != 6
    ):
        raise ValueError("quality capture did not pass P1-P6")
    persisted_receipts = summary.get("prompt_receipts")
    if not isinstance(persisted_receipts, list) or len(persisted_receipts) != 6:
        raise ValueError("quality capture prompt receipts are incomplete")
    controls = _prompt_controls(Path(campaign_input.prompt_set_path).resolve())
    receipts: list[dict[str, Any]] = []
    gate_records: list[dict[str, Any]] = []
    for persisted in persisted_receipts:
        if not isinstance(persisted, Mapping):
            raise ValueError("quality prompt receipt is invalid")
        prompt_id = persisted.get("prompt_id")
        evidence_paths = {
            "worker_spec": Path(str(persisted.get("worker_spec_path"))).resolve(),
            "worker_result": Path(str(persisted.get("worker_result_path"))).resolve(),
            "worker_log": Path(str(persisted.get("worker_log_path"))).resolve(),
            "guard_evidence": Path(str(persisted.get("guard_evidence_path"))).resolve(),
        }
        bindings = {
            label: _evidence_binding(path)
            for label, path in evidence_paths.items()
        }
        expected_hash_fields = {
            "worker_spec": "worker_spec_sha256",
            "worker_result": "worker_result_sha256",
            "worker_log": "worker_log_sha256",
            "guard_evidence": "guard_evidence_sha256",
        }
        if any(
            bindings[label]["sha256"] != persisted.get(hash_field)
            for label, hash_field in expected_hash_fields.items()
        ):
            raise ValueError("quality prompt evidence hash drift")
        guard = _read_json_object(evidence_paths["guard_evidence"])
        containing = guard.get("containing_job_assignment")
        job = guard.get("job_object")
        if (
            persisted.get("status") != "passed"
            or persisted.get("cleanup_process_count") != 0
            or guard.get("cleanup_process_count") != 0
            or guard.get("emergency_actions") != []
            or not isinstance(containing, Mapping)
            or containing.get("query_ok_after_cleanup") is not True
            or containing.get("active_pids_after_cleanup") != []
            or not isinstance(job, Mapping)
            or job.get("query_ok") is not True
            or job.get("queried_active_process_count_after_cleanup") != 0
            or job.get("survivor_pids_after_cleanup") != []
        ):
            raise ValueError("quality prompt cleanup proof is unsafe")
        result = _read_json_object(evidence_paths["worker_result"])
        outcomes = result.get("outcomes")
        if (
            not isinstance(outcomes, list)
            or not outcomes
            or any(
                not isinstance(outcome, Mapping)
                or outcome.get("status") != "complete"
                or not isinstance(outcome.get("raw_output"), str)
                for outcome in outcomes
            )
        ):
            raise ValueError("quality prompt deterministic gate records are missing")
        turn_outputs = [
            {
                "turn_id": outcome["turn_id"],
                "output": outcome["raw_output"],
                "output_sha256": outcome["raw_output_sha256"],
            }
            for outcome in outcomes
        ]
        gate = deterministic_gate(
            str(prompt_id),
            turn_outputs[-1]["output"],
            turn_outputs,
            controls[str(prompt_id)],
        )
        gate_records.append(
            {
                "prompt_id": prompt_id,
                "deterministic_gate": gate,
                "deterministic_gate_sha256": hashlib.sha256(
                    json.dumps(
                        gate,
                        sort_keys=True,
                        separators=(",", ":"),
                        ensure_ascii=True,
                        allow_nan=False,
                    ).encode("utf-8")
                ).hexdigest(),
            }
        )
        receipts.append(
            {
                "prompt_id": prompt_id,
                "cleanup_process_count": 0,
                "active_pids_after_cleanup": [],
                "evidence": bindings,
            }
        )
    return {
        "capture_summary": _evidence_binding(summary_path),
        "completed_prompt_ids": list(summary["completed_prompt_ids"]),
        "prompt_receipts": receipts,
        "deterministic_gate_records": gate_records,
    }


def _empty_lane() -> dict[str, Any]:
    return {
        "status": "running",
        "reason_code": None,
        "terminal_status": None,
        "accepted_count": 0,
        "attempt_count": 0,
        "skipped_count": 0,
        "rows": {},
    }


def _limits() -> dict[str, Any]:
    return {
        "runtime_timeout_seconds": 180.0,
        "quality_timeout_seconds": 90.0,
        "row_timeout_seconds": 720.0,
        "launch_minimum_available_ram_mib": 4096,
        "emergency_minimum_available_ram_mib": 3072,
        "max_clean_retries": 1,
    }


def _initial_state(
    config: BoundaryCampaignConfig,
    manifest: BoundaryManifest,
    projection: BoundaryEvidenceProjection,
) -> dict[str, Any]:
    return {
        "schema": "official-openvino-format-boundary-state/v2",
        "manifest_path": str(manifest.source_path),
        "manifest_sha256": manifest.sha256,
        "projection_index_path": str(projection.projection_index.path),
        "projection_index_sha256": projection.projection_index.sha256,
        "input_bindings": _boundary_input_bindings(config, projection),
        "limits": _limits(),
        "global_halt": {"active": False, "reason_code": None},
        "cpu_lane": _empty_lane(),
        "gpu_lane": _empty_lane(),
    }


def _lane_name(case: BoundaryCase) -> str:
    return "cpu_lane" if case.lane == "cpu" else "gpu_lane"


def _lane_directory(case: BoundaryCase) -> str:
    return "cpu" if case.lane == "cpu" else "gpu-control"


def _case_descriptor(case: BoundaryCase) -> dict[str, Any]:
    artifact_path = (
        case.artifact_manifest_path.resolve()
        if case.artifact_manifest_path is not None
        else None
    )
    return {
        "internal_id": case.internal_id,
        "label": case.label,
        "lane": case.lane,
        "order": case.order,
        "device": case.device,
        "context": case.context,
        "weight_precision": case.weight_precision,
        "key_algorithm": case.key_algorithm,
        "value_algorithm": case.value_algorithm,
        "key_precision": case.key_precision,
        "value_precision": case.value_precision,
        "artifact_manifest_path": str(artifact_path) if artifact_path else None,
        "artifact_manifest_sha256": _file_sha256(artifact_path),
    }


def _not_launched_cleanup_proof() -> dict[str, Any]:
    return {
        "safe": True,
        "cleanup_process_count": 0,
        "residual_owned_process_count": 0,
        "emergency_actions": [],
        "job_active_pid_evidence": "not-launched",
        "active_pids_after_cleanup": [],
    }


def _cleanup_proof(record: Mapping[str, Any]) -> dict[str, Any]:
    cleanup = record.get("cleanup_process_count")
    residual = record.get("residual_owned_process_count")
    emergency = record.get("emergency_actions")
    job = record.get("job_object")
    containing = record.get("containing_job_assignment")
    job_safe = (
        isinstance(job, Mapping)
        and job.get("setup_ok") is True
        and job.get("query_ok") is True
        and job.get("queried_active_process_count_after_cleanup") == 0
        and job.get("survivor_pids_after_cleanup") == []
    )
    containing_safe = (
        isinstance(containing, Mapping)
        and containing.get("requested") is True
        and containing.get("assigned_before_fine_job") is True
        and containing.get("query_ok_after_cleanup") is True
        and containing.get("active_pids_after_cleanup") == []
    )
    task_three_jobs = []
    for field_name in ("workload_job", "sampler_job"):
        value = record.get(field_name)
        task_three_jobs.append(
            isinstance(value, Mapping)
            and value.get("setup_ok") is True
            and value.get("query_ok") is True
            and value.get("queried_active_process_count_after_cleanup") == 0
            and value.get("survivor_pids_after_cleanup") == []
        )
    alternate_jobs_safe = all(task_three_jobs)
    safe = (
        cleanup == 0
        and residual == 0
        and emergency == []
        and ((job_safe and containing_safe) or alternate_jobs_safe)
    )
    return {
        "safe": safe,
        "cleanup_process_count": cleanup,
        "residual_owned_process_count": residual,
        "emergency_actions": emergency,
        "job_object": dict(job) if isinstance(job, Mapping) else None,
        "containing_job_assignment": (
            dict(containing) if isinstance(containing, Mapping) else None
        ),
        "active_pids_after_cleanup": (
            list(containing["active_pids_after_cleanup"])
            if containing_safe
            else None
        ),
    }


def _quality_failure_evidence(
    summary_path: Path,
) -> tuple[Path, dict[str, Any], dict[str, Any]] | None:
    if not Path(summary_path).is_file():
        return None
    summary = _read_json_object(summary_path)
    receipts = summary.get("prompt_receipts")
    if not isinstance(receipts, list):
        return None
    attempted = [
        receipt
        for receipt in receipts
        if isinstance(receipt, Mapping)
        and receipt.get("status") not in {None, "not-run", "passed"}
    ]
    if not attempted:
        return None
    receipt = attempted[-1]
    guard_value = receipt.get("guard_evidence_path")
    guard_sha256 = receipt.get("guard_evidence_sha256")
    if not isinstance(guard_value, str):
        return None
    guard_path = Path(guard_value).resolve()
    if _file_sha256(guard_path) != guard_sha256:
        return None
    guard = _read_json_object(guard_path)
    cleanup = guard.get("cleanup_process_count")
    if guard.get("schema") == "official-openvino-adaptive-quality-terminal-guard/v1":
        active = guard.get("active_pids")
        safe = cleanup == 0 and active == []
        proof = {
            "safe": safe,
            "cleanup_process_count": cleanup,
            "residual_owned_process_count": 0 if safe else -1,
            "emergency_actions": [],
            "active_pids_after_cleanup": active,
            "guard_evidence": _evidence_binding(guard_path),
            "capture_summary": _evidence_binding(summary_path),
        }
    else:
        containing = guard.get("containing_job_assignment")
        job = guard.get("job_object")
        active = (
            containing.get("active_pids_after_cleanup")
            if isinstance(containing, Mapping)
            else None
        )
        safe = (
            cleanup == 0
            and guard.get("emergency_actions") == []
            and isinstance(containing, Mapping)
            and containing.get("query_ok_after_cleanup") is True
            and active == []
            and isinstance(job, Mapping)
            and job.get("query_ok") is True
            and job.get("queried_active_process_count_after_cleanup") == 0
            and job.get("survivor_pids_after_cleanup") == []
        )
        proof = {
            "safe": safe,
            "cleanup_process_count": cleanup,
            "residual_owned_process_count": 0 if safe else -1,
            "emergency_actions": guard.get("emergency_actions"),
            "job_object": dict(job) if isinstance(job, Mapping) else None,
            "containing_job_assignment": (
                dict(containing) if isinstance(containing, Mapping) else None
            ),
            "active_pids_after_cleanup": active,
            "guard_evidence": _evidence_binding(guard_path),
            "capture_summary": _evidence_binding(summary_path),
        }
    record = dict(guard)
    record["prompt_id"] = receipt.get("prompt_id")
    result_value = receipt.get("worker_result_path")
    result_path = Path(result_value).resolve() if isinstance(result_value, str) else None
    if result_path is not None and result_path.is_file():
        result = _read_json_object(result_path)
        outcomes = result.get("outcomes")
        failed = (
            [outcome for outcome in outcomes if isinstance(outcome, Mapping)]
            if isinstance(outcomes, list)
            else []
        )
        if failed:
            record["failure_stage"] = (
                failed[-1].get("failure_type") or receipt.get("status")
            )
    record.setdefault("failure_stage", receipt.get("status") or "quality-prompt")
    return guard_path, record, proof


def _normalized_code(value: Any) -> str:
    if isinstance(value, bool) or value is None:
        return "unspecified"
    text = str(value).strip().lower()
    return re.sub(r"[^a-z0-9]+", "-", text).strip("-") or "unspecified"


def _failure_category(record: Mapping[str, Any], reason: str) -> str:
    raw = record.get("failure_category") or record.get("category")
    if isinstance(raw, str) and raw.strip():
        return _normalized_code(raw)
    if record.get("low_memory_stop") is True or "ram" in reason:
        return "ram-floor"
    if "timeout" in reason or record.get("timed_out") is True:
        return "time"
    return "functional"


def _execution_route(case: BoundaryCase) -> str:
    return "patched-stateful" if case.key_algorithm != "STANDARD" else "stateful-standard"


def _case_build_identity(case: BoundaryCase) -> dict[str, str]:
    if case.artifact_manifest_path is None:
        return {"status": "artifact-unavailable"}
    manifest = _read_json_object(case.artifact_manifest_path)
    load_probe = manifest.get("load_probe")
    if not isinstance(load_probe, Mapping):
        raise ValueError("artifact manifest load-probe build identity is missing")
    manifest_sha256 = _require_sha256(
        load_probe.get("runtime_build_manifest_sha256"),
        "artifact runtime build manifest hash",
    )
    commit = load_probe.get("runtime_build_commit")
    if not isinstance(commit, str) or not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("artifact runtime build commit is invalid")
    return {
        "runtime_build_manifest_sha256": manifest_sha256,
        "runtime_build_commit": commit,
    }


def _failure_fingerprint(
    case: BoundaryCase,
    *,
    stage: str,
    category: str,
    reason_code: str,
) -> str:
    identity = {
        "stage": stage,
        "category": category,
        "case_internal_id": case.internal_id,
        "artifact_manifest_sha256": _file_sha256(case.artifact_manifest_path),
        "build_identity": _case_build_identity(case),
        "execution_route": _execution_route(case),
        "context_tokens": case.context,
        "launch_reserve_mib": 4096,
        "emergency_floor_mib": 3072,
        "normalized_failure_code": _normalized_code(reason_code),
    }
    return hashlib.sha256(_canonical_bytes(identity)).hexdigest()


def _normalise_failure(
    error: Exception,
    *,
    case: BoundaryCase,
    default_role: str,
    default_raw_path: Path | None = None,
) -> RowFailure:
    if isinstance(error, RowFailure):
        failure = error
        record = failure.raw_record or {}
        raw_path = failure.receipt
        role = failure.role or default_role
        reason = _normalized_code(failure.reason_code)
        proof = failure.cleanup_proof
    else:
        sequence_failure = getattr(error, "failure", None)
        raw_path_value = getattr(sequence_failure, "record_path", None)
        raw_path = Path(raw_path_value) if raw_path_value is not None else default_raw_path
        raw_record = getattr(sequence_failure, "record", None)
        record = dict(raw_record) if isinstance(raw_record, Mapping) else {}
        role = default_role
        if sequence_failure is not None and default_role == "measurement":
            role = "measurement"
        reason_value = (
            record.get("failure_code")
            or record.get("reason_code")
            or record.get("termination_reason")
            or f"{default_role}-failure"
        )
        reason = _normalized_code(reason_value)
        proof = None
        failure = RowFailure(
            reason,
            role=role,
            stage=(getattr(sequence_failure, "role", None) or default_role),
            hard=False,
            retryable=True,
            receipt=raw_path,
            raw_record=record,
        )
    if raw_path is None and default_raw_path is not None and default_raw_path.is_file():
        raw_path = default_raw_path
    if default_role == "quality" and default_raw_path is not None:
        quality_evidence = _quality_failure_evidence(default_raw_path)
        if quality_evidence is not None:
            raw_path, record, proof = quality_evidence
            prompt_id = record.get("prompt_id")
            stage_value = record.get("failure_stage") or "quality-prompt"
            reason = _normalized_code(
                f"{prompt_id or 'unknown'}-{stage_value}"
            )
            failure.stage = "quality"
    if proof is None:
        proof = _cleanup_proof(record)
    stage = failure.stage or role
    category = _failure_category(record, reason)
    available = record.get("available_ram_bytes")
    observed_minimum = (
        available.get("minimum") if isinstance(available, Mapping) else None
    )
    observed_available = (
        observed_minimum
        if isinstance(observed_minimum, int) and not isinstance(observed_minimum, bool)
        else failure.observed_available_ram_bytes
    )
    hard_ram = (
        isinstance(observed_available, int)
        and not isinstance(observed_available, bool)
        and observed_available < 3072 * _MIB
    )
    if hard_ram:
        reason = "emergency-ram-floor"
    hard = failure.hard or hard_ram
    retryable = failure.retryable and not hard and proof.get("safe") is True
    return RowFailure(
        reason,
        role=role,
        stage=stage,
        hard=hard,
        retryable=retryable,
        receipt=raw_path,
        raw_record=record,
        fingerprint=_failure_fingerprint(
            case, stage=stage, category=category, reason_code=reason
        ),
        cleanup_proof=proof,
        observed_available_ram_bytes=observed_available,
        deadline_outcome=failure.deadline_outcome,
    )


def _require_config_path(path: Path | None, label: str, *, directory: bool) -> Path:
    if path is None:
        raise ValueError(f"{label} is not configured")
    source = Path(path).resolve()
    valid = source.is_dir() if directory else source.is_file()
    if not valid:
        raise ValueError(f"{label} is missing: {source}")
    return source


def prepare_boundary_projection(
    config: BoundaryCampaignConfig,
    *,
    status_only: bool = False,
) -> BoundaryEvidenceProjection:
    """Validate immutable preflight inputs and (re)project Task 2A evidence."""

    if not isinstance(config, BoundaryCampaignConfig):
        raise TypeError("config must be a BoundaryCampaignConfig")
    repository = Path(config.repository_root).resolve()
    if repository != _ROOT.resolve():
        raise ValueError("boundary repository root must be this repository")
    _require_config_path(config.manifest_path, "boundary manifest", directory=False)
    matrix = _require_config_path(
        config.comparison_matrix_path, "authoritative comparison matrix", directory=False
    )
    build = _require_config_path(config.build_root, "build root", directory=True)
    _require_config_path(config.python_executable, "Python executable", directory=False)
    _require_config_path(config.python_site_packages, "Python site-packages", directory=True)
    _require_config_path(config.openvino_libraries, "OpenVINO libraries", directory=True)
    _require_config_path(config.sampler_script, "sampler script", directory=False)
    prompt = _require_config_path(config.prompt_set_path, "compact prompt set", directory=False)
    rendered = _require_config_path(config.rendered_root, "rendered prompt root", directory=True)
    rubric = _require_config_path(config.rubric_path, "quality rubric", directory=False)
    contract = load_quality_contract(prompt)
    if contract.prompt_set_id != "GTQ-PROMPTS-v2":
        raise ValueError("format boundary requires compact-v2 quality prompts")
    load_prompt_contract(prompt, rendered)
    _load_frozen_rubric(rubric)
    projection = project_boundary_evidence_inputs(
        repository_root=repository,
        campaign_root=Path(config.campaign_root),
        build_root=build,
        manifest_path=Path(config.manifest_path),
        comparison_matrix_path=matrix,
    )
    root = Path(config.campaign_root).resolve()
    allowed = {"execution-inputs", "preflight-receipt.json", "cache"}
    if config.resume or status_only:
        allowed.update({"campaign-state.json", "cpu", "gpu-control"})
    unexplained = {item.name for item in root.iterdir()} - allowed
    if unexplained:
        raise ValueError(
            "boundary campaign root contains unexplained entries: "
            + ", ".join(sorted(unexplained))
        )
    _validate_cache_tree(config)
    _validate_persisted_preflight(config, projection)
    return projection


def _directory_binding(path: Path) -> dict[str, str]:
    source = Path(path).resolve()
    return {"path": str(source), "sha256": _directory_sha256(source)}


def _boundary_input_bindings(
    config: BoundaryCampaignConfig,
    projection: BoundaryEvidenceProjection,
) -> dict[str, Any]:
    index = _projection_index(projection)
    build = index.get("build")
    if not isinstance(build, Mapping):
        raise ValueError("boundary projection build binding is missing")
    python = Path(config.python_executable).resolve()
    site_packages = Path(config.python_site_packages).resolve()
    openvino_package = site_packages / "openvino"
    return {
        "projection_index": _evidence_binding(projection.projection_index.path),
        "build": dict(build),
        "python_executable": _evidence_binding(python),
        "python_site_packages": {
            "path": str(site_packages),
            "openvino_package": _directory_binding(openvino_package),
        },
        "openvino_libraries": _directory_binding(
            Path(config.openvino_libraries).resolve()
        ),
        "sampler_script": _evidence_binding(Path(config.sampler_script).resolve()),
        "prompt_set": _evidence_binding(Path(config.prompt_set_path).resolve()),
        "rendered_root": _directory_binding(Path(config.rendered_root).resolve()),
        "rubric": _evidence_binding(Path(config.rubric_path).resolve()),
        "cache_root": str((Path(config.campaign_root).resolve() / "cache")),
    }


def _boundary_campaign_job_name(campaign_root: Path) -> str:
    identity = hashlib.sha256(
        str(Path(campaign_root).resolve()).encode("utf-8")
    ).hexdigest()[:24]
    return f"WB04-format-boundary-{identity}"


def _validate_cache_tree(config: BoundaryCampaignConfig) -> None:
    root = Path(config.campaign_root).resolve() / "cache"
    if not root.exists():
        return
    if not root.is_dir():
        raise ValueError("boundary cache root is not a directory")
    manifest = load_boundary_manifest(config.manifest_path)
    allowed = {
        case.internal_id
        for case in (*manifest.cpu_cases, *manifest.gpu_cases)
        if case.artifact_manifest_path is not None
    }
    for case_root in root.iterdir():
        if not case_root.is_dir() or case_root.name not in allowed:
            raise ValueError("boundary cache tree contains an unexplained case")
        for context_root in case_root.iterdir():
            if not context_root.is_dir() or context_root.name != "512":
                raise ValueError("boundary cache tree contains an unexplained context")


def _validate_persisted_preflight(
    config: BoundaryCampaignConfig,
    projection: BoundaryEvidenceProjection,
) -> None:
    path = Path(config.campaign_root).resolve() / "preflight-receipt.json"
    if not path.exists():
        return
    receipt = _read_json_object(path)
    probe = receipt.get("python_probe")
    owned = receipt.get("owned_process_probe")
    devices = receipt.get("detected_devices")
    if (
        receipt.get("schema") != "official-openvino-format-boundary-preflight/v1"
        or receipt.get("status") != "passed"
        or receipt.get("input_bindings")
        != _boundary_input_bindings(config, projection)
        or not isinstance(receipt.get("available_ram_bytes"), int)
        or receipt["available_ram_bytes"] < 4096 * _MIB
        or not isinstance(probe, Mapping)
        or probe.get("python_executable")
        != str(Path(config.python_executable).resolve())
        or not isinstance(owned, Mapping)
        or owned.get("query_ok") is not True
        or owned.get("active_pids") != []
        or not isinstance(devices, list)
        or not any(device == "CPU" or device.startswith("CPU.") for device in devices)
        or not any(device == "GPU" or device.startswith("GPU.") for device in devices)
    ):
        raise ValueError("persisted boundary preflight receipt is invalid")
    _validate_bound_files(receipt["input_bindings"])


def _probe_owned_campaign_pids(campaign_root: Path) -> dict[str, Any]:
    job = KillOnCloseJob(_boundary_campaign_job_name(campaign_root))
    try:
        return {"query_ok": True, "active_pids": sorted(job.active_pids())}
    finally:
        job.close()


def _probe_configured_python(config: BoundaryCampaignConfig) -> dict[str, Any]:
    environment = build_worker_environment(
        build_root=Path(config.build_root).resolve(),
        repo_root=Path(config.repository_root).resolve(),
        python_site_packages=Path(config.python_site_packages).resolve(),
        openvino_libraries=Path(config.openvino_libraries).resolve(),
    )
    script = (
        "import json,sys,openvino as ov,openvino_genai;"
        "print(json.dumps({"
        "'python_executable':sys.executable,"
        "'python_version':sys.version,"
        "'openvino':{'path':ov.__file__,'version':getattr(ov,'__version__','unknown')},"
        "'openvino_genai':{'path':openvino_genai.__file__,'version':getattr(openvino_genai,'__version__','unknown')},"
        "'available_devices':list(ov.Core().available_devices)"
        "},sort_keys=True,separators=(',',':')))"
    )
    job = KillOnCloseJob(
        _boundary_campaign_job_name(config.campaign_root) + "-python-preflight"
    )
    process: subprocess.Popen[str] | None = None
    try:
        process = subprocess.Popen(
            [str(Path(config.python_executable).resolve()), "-c", script],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            env=environment,
            creationflags=(
                CREATE_SUSPENDED
                | int(getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0x00000200))
            ),
        )
        job.assign_pid(process.pid)
        _resume_suspended_process(process.pid)
        stdout, stderr = process.communicate(timeout=30.0)
        if process.returncode != 0:
            raise RuntimeError(
                "configured Python preflight failed: " + stderr.strip()
            )
        value = json.loads(stdout)
        if not isinstance(value, dict):
            raise ValueError("configured Python preflight did not return an object")
        if job.active_pids():
            raise RuntimeError("configured Python preflight left owned processes")
        return value
    except subprocess.TimeoutExpired as error:
        job.terminate(124)
        if process is not None:
            process.wait(timeout=10.0)
        raise RuntimeError("configured Python preflight timed out") from error
    finally:
        job.close()


def run_boundary_preflight(
    config: BoundaryCampaignConfig,
    *,
    available_ram: Callable[[], int | None] = available_ram_bytes,
    python_probe: Callable[[BoundaryCampaignConfig], Mapping[str, Any]] = (
        _probe_configured_python
    ),
    owned_pid_probe: Callable[[Path], Mapping[str, Any]] = (
        _probe_owned_campaign_pids
    ),
) -> dict[str, Any]:
    """Prove host/runtime readiness without loading a model or generating text."""

    projection = prepare_boundary_projection(config)
    observed = available_ram()
    if isinstance(observed, bool) or not isinstance(observed, int):
        raise RuntimeError("available RAM query failed")
    if observed < 4096 * _MIB:
        raise RuntimeError("4096 MiB launch reserve is not restored")
    owned = dict(owned_pid_probe(Path(config.campaign_root).resolve()))
    if owned.get("query_ok") is not True or owned.get("active_pids") != []:
        raise RuntimeError("campaign-owned worker PID proof is not empty")
    probe = dict(python_probe(config))
    if probe.get("python_executable") != str(Path(config.python_executable).resolve()):
        raise ValueError("configured Python executable identity mismatch")
    devices = probe.get("available_devices")
    if (
        not isinstance(devices, list)
        or not all(isinstance(device, str) and device for device in devices)
        or not any(device == "CPU" or device.startswith("CPU.") for device in devices)
        or not any(device == "GPU" or device.startswith("GPU.") for device in devices)
    ):
        raise ValueError("configured OpenVINO must detect both CPU and GPU")
    for module_name, expected_root in (
        ("openvino", Path(config.python_site_packages).resolve() / "openvino"),
        ("openvino_genai", Path(config.build_root).resolve() / "openvino_genai"),
    ):
        module = probe.get(module_name)
        if not isinstance(module, Mapping):
            raise ValueError(f"configured {module_name} import identity is missing")
        module_path = Path(str(module.get("path"))).resolve()
        if module_path != expected_root / "__init__.py" or not isinstance(
            module.get("version"), str
        ) or not module["version"]:
            raise ValueError(f"configured {module_name} import identity mismatch")
    receipt = {
        "schema": "official-openvino-format-boundary-preflight/v1",
        "status": "passed",
        "available_ram_bytes": observed,
        "detected_devices": list(devices),
        "owned_process_probe": owned,
        "python_probe": probe,
        "input_bindings": _boundary_input_bindings(config, projection),
    }
    _atomic_write_json(
        Path(config.campaign_root).resolve() / "preflight-receipt.json",
        receipt,
    )
    return receipt


def _projection_index(projection: BoundaryEvidenceProjection) -> dict[str, Any]:
    index = _read_json_object(projection.projection_index.path)
    if _file_sha256(projection.projection_index.path) != projection.projection_index.sha256:
        raise ValueError("boundary projection index hash drift")
    return index


def _runtime_spec_map(
    projection: BoundaryEvidenceProjection,
) -> dict[str, ProjectionFile]:
    return {item.case_internal_id: item.runtime_spec for item in projection.runtime_specs}


def _terminal_map(
    projection: BoundaryEvidenceProjection,
) -> dict[str, ProjectionFile]:
    return {item.case_internal_id: item.descriptor for item in projection.terminal_prerequisites}


def _quality_input(
    config: BoundaryCampaignConfig,
    projection: BoundaryEvidenceProjection,
    case: BoundaryCase,
    runtime_spec: ProjectionFile,
    execution_root: Path,
    *,
    timeout_seconds: float,
) -> AdaptiveQualityCampaignInput:
    if case.artifact_manifest_path is None:
        raise ValueError("executable boundary row has no artifact manifest")
    index = _projection_index(projection)
    build = index.get("build")
    provenance = build.get("provenance") if isinstance(build, Mapping) else None
    if not isinstance(provenance, Mapping) or not isinstance(provenance.get("path"), str):
        raise ValueError("boundary projection build provenance binding is missing")
    runtime_root = execution_root / "runtime"
    sequence_path = runtime_root / "attempt-sequence.json"
    pilot_path = runtime_root / "attempts" / "pilot" / "attempt-001" / "spec.json"
    if sequence_path.is_file():
        sequence = _read_json_object(sequence_path)
        pilot = sequence.get("pilot")
        if isinstance(pilot, Mapping) and isinstance(pilot.get("spec_path"), str):
            pilot_path = (runtime_root / pilot["spec_path"]).resolve()
    return AdaptiveQualityCampaignInput(
        campaign_root=runtime_root,
        spec_path=runtime_spec.path,
        matrix_path=projection.comparison_matrix.path,
        artifact_manifest_path=case.artifact_manifest_path,
        build_provenance_path=Path(provenance["path"]).resolve(),
        build_root=Path(config.build_root).resolve(),
        repo_root=Path(config.repository_root).resolve(),
        python_executable=Path(config.python_executable).resolve(),
        python_site_packages=Path(config.python_site_packages).resolve(),
        openvino_libraries=Path(config.openvino_libraries).resolve(),
        sampler_script=Path(config.sampler_script).resolve(),
        prompt_set_path=Path(config.prompt_set_path).resolve(),
        rendered_root=Path(config.rendered_root).resolve(),
        rubric_path=Path(config.rubric_path).resolve(),
        output_root=execution_root / "quality",
        timeout_seconds=float(timeout_seconds),
        attempt_sequence_path=sequence_path,
        adaptive_runtime_spec_path=runtime_spec.path,
        pilot_spec_path=pilot_path,
        spec_index_path=projection.projection_index.path,
        artifact_inventory_path=case.artifact_manifest_path,
    )


def _iter_bindings(value: Any):
    if isinstance(value, Mapping):
        if set(value) == {"path", "sha256"}:
            yield value
        else:
            for item in value.values():
                yield from _iter_bindings(item)
    elif isinstance(value, (list, tuple)):
        for item in value:
            yield from _iter_bindings(item)


def _validate_bound_files(value: Any) -> None:
    for binding in _iter_bindings(value):
        path = Path(str(binding["path"])).resolve()
        if _file_sha256(path) != binding["sha256"]:
            raise ValueError(f"boundary receipt evidence hash drift: {path}")


def _validate_raw_record_pairs(value: Any) -> None:
    if isinstance(value, Mapping):
        if "raw_record_path" in value or "raw_record_sha256" in value:
            path_value = value.get("raw_record_path")
            digest = value.get("raw_record_sha256")
            if path_value is None:
                if digest is not None:
                    raise ValueError("boundary receipt raw record binding is invalid")
            else:
                path = Path(str(path_value)).resolve()
                if _file_sha256(path) != digest:
                    raise ValueError(f"boundary receipt raw record hash drift: {path}")
        for item in value.values():
            _validate_raw_record_pairs(item)
    elif isinstance(value, (list, tuple)):
        for item in value:
            _validate_raw_record_pairs(item)


def _validate_case(receipt: Mapping[str, Any], case: BoundaryCase) -> None:
    if receipt.get("case") != _case_descriptor(case):
        raise ValueError("boundary row receipt case identity drift")


def _validate_accepted_receipt(
    path: Path,
    case: BoundaryCase,
    campaign_input: AdaptiveQualityCampaignInput | None = None,
) -> dict[str, Any]:
    receipt = _read_json_object(path)
    selected = receipt.get("selected_attempt_number")
    attempt_count = receipt.get("attempt_count")
    timeouts = receipt.get("stage_timeouts")
    if (
        receipt.get("schema") != "official-openvino-format-boundary-accepted-row/v2"
        or receipt.get("status") != "accepted"
        or receipt.get("limits") != _limits()
        or type(selected) is not int
        or selected not in {1, 2}
        or attempt_count != selected
        or not isinstance(timeouts, Mapping)
        or set(timeouts) != {"measurement", "quality"}
        or any(
            isinstance(value, bool)
            or not isinstance(value, (int, float))
            or not math.isfinite(value)
            or value <= 0
            for value in timeouts.values()
        )
        or timeouts["measurement"] > 180.0
        or timeouts["quality"] > 90.0
        or receipt.get("deadline_outcome") != "within-deadline"
    ):
        raise ValueError("accepted-row receipt is invalid")
    _validate_case(receipt, case)
    runtime = receipt.get("runtime")
    quality = receipt.get("quality")
    if not isinstance(runtime, Mapping) or not isinstance(quality, Mapping):
        raise ValueError("accepted-row receipt evidence is incomplete")
    _validate_bound_files(runtime)
    _validate_bound_files(quality)
    if campaign_input is not None:
        if reconcile_runtime_evidence(campaign_input) != runtime:
            raise ValueError("accepted-row runtime evidence no longer reconciles")
        if reconcile_quality_evidence(campaign_input) != quality:
            raise ValueError("accepted-row quality evidence no longer reconciles")
    return receipt


def _accepted_campaign_input_from_receipt(
    config: BoundaryCampaignConfig,
    projection: BoundaryEvidenceProjection,
    case: BoundaryCase,
    runtime_spec: ProjectionFile,
    row_root: Path,
    receipt: Mapping[str, Any],
) -> AdaptiveQualityCampaignInput:
    selected = receipt.get("selected_attempt_number")
    if type(selected) is not int or selected not in {1, 2}:
        raise ValueError("accepted-row selected attempt is invalid")
    execution_root = (row_root / f"attempt-{selected:03d}").resolve()
    if receipt.get("execution_root") != str(execution_root):
        raise ValueError("accepted-row execution root binding is invalid")
    timeouts = receipt.get("stage_timeouts")
    quality_timeout = (
        timeouts.get("quality") if isinstance(timeouts, Mapping) else None
    )
    if (
        isinstance(quality_timeout, bool)
        or not isinstance(quality_timeout, (int, float))
        or not 0 < quality_timeout <= 90.0
    ):
        raise ValueError("accepted-row quality timeout binding is invalid")
    return _quality_input(
        config,
        projection,
        case,
        runtime_spec,
        execution_root,
        timeout_seconds=float(quality_timeout),
    )


def _validate_terminal_or_skipped_receipt(path: Path, case: BoundaryCase) -> dict[str, Any]:
    receipt = _read_json_object(path)
    _validate_case(receipt, case)
    if path.name == "skipped-row.json":
        if (
            receipt.get("schema")
            != "official-openvino-format-boundary-skipped-row/v2"
            or receipt.get("status")
            not in {"not-attempted-after-boundary", "not-attempted-global-halt"}
            or receipt.get("attempt_count") != 0
            or receipt.get("limits") != _limits()
        ):
            raise ValueError("skipped-row receipt is invalid")
    elif path.name == "terminal-boundary.json":
        attempts = receipt.get("attempts")
        attempt_count = receipt.get("attempt_count")
        if (
            receipt.get("schema")
            != "official-openvino-format-boundary-terminal-boundary/v2"
            or receipt.get("limits") != _limits()
            or type(attempt_count) is not int
            or attempt_count not in {1, 2}
            or not isinstance(attempts, list)
            or len(attempts) != attempt_count
        ):
            raise ValueError("terminal-boundary receipt is invalid")
        if not all(isinstance(attempt, Mapping) for attempt in attempts):
            raise ValueError("terminal-boundary receipt is invalid")
        prerequisite = receipt.get("status") == "artifact-unavailable"
        expected_numbers = [0] if prerequisite else list(range(1, attempt_count + 1))
        if [attempt.get("attempt_number") for attempt in attempts] != expected_numbers:
            raise ValueError("terminal-boundary receipt is invalid")
        last = attempts[-1]
        cleanup = receipt.get("cleanup_proof")
        if (
            not isinstance(cleanup, Mapping)
            or type(cleanup.get("safe")) is not bool
            or cleanup != last.get("cleanup_proof")
            or receipt.get("reason_code") != last.get("reason_code")
            or receipt.get("fingerprint") != last.get("fingerprint")
            or receipt.get("role") != last.get("role")
            or receipt.get("stage") != last.get("stage")
        ):
            raise ValueError("terminal-boundary receipt is invalid")
        if prerequisite:
            valid_status = (
                last.get("reason_code") == "artifact-unavailable"
                and last.get("role") == "terminal-prerequisite"
                and last.get("stage") == "preflight"
                and cleanup.get("safe") is True
            )
        else:
            valid_status = receipt.get("status") == _terminal_status(attempts)
        if not valid_status:
            raise ValueError("terminal-boundary receipt is invalid")
    else:
        raise ValueError("boundary receipt type is invalid")
    _validate_bound_files(receipt)
    _validate_raw_record_pairs(receipt)
    return receipt


def _state_from_durable_receipts(
    config: BoundaryCampaignConfig,
    manifest: BoundaryManifest,
    projection: BoundaryEvidenceProjection,
) -> dict[str, Any]:
    root = Path(config.campaign_root).resolve()
    reconstructed = _initial_state(config, manifest, projection)
    specs = _runtime_spec_map(projection)
    terminal_statuses: list[str] = []
    for cases in (manifest.cpu_cases, manifest.gpu_cases):
        lane = reconstructed[_lane_name(cases[0])]
        terminal_seen = False
        for case in cases:
            row_root = root / _lane_directory(case) / case.internal_id
            candidates = (
                row_root / "accepted-row.json",
                row_root / "terminal-boundary.json",
                row_root / "skipped-row.json",
            )
            existing = [path for path in candidates if path.exists()]
            if len(existing) > 1:
                raise ValueError("boundary row has ambiguous durable receipts")
            if not existing:
                continue
            path = existing[0]
            if not path.is_file():
                raise ValueError("boundary durable receipt is not a file")
            if path.name == "accepted-row.json":
                if terminal_seen:
                    raise ValueError("accepted row follows a terminal boundary")
                spec = specs.get(case.internal_id)
                if spec is None:
                    raise ValueError("accepted boundary row has no projected runtime spec")
                receipt = _read_json_object(path)
                campaign_input = _accepted_campaign_input_from_receipt(
                    config, projection, case, spec, row_root, receipt
                )
                receipt = _validate_accepted_receipt(path, case, campaign_input)
                lane["accepted_count"] += 1
                lane["attempt_count"] += receipt["attempt_count"]
                lane["rows"][case.internal_id] = "accepted"
                continue
            receipt = _validate_terminal_or_skipped_receipt(path, case)
            status = receipt["status"]
            lane["rows"][case.internal_id] = status
            if path.name == "terminal-boundary.json":
                if terminal_seen:
                    raise ValueError("boundary lane has multiple terminal receipts")
                terminal_seen = True
                lane["status"] = "stopped"
                lane["reason_code"] = receipt["reason_code"]
                lane["terminal_status"] = status
                terminal_statuses.append(status)
                lane["attempt_count"] += sum(
                    attempt.get("attempt_number", 0) > 0
                    for attempt in receipt["attempts"]
                )
            else:
                lane["skipped_count"] += 1
                if terminal_seen and status != "not-attempted-after-boundary":
                    raise ValueError("boundary skipped-row receipt is invalid")
        if not terminal_seen:
            row_statuses = lane["rows"]
            if len(row_statuses) == len(cases):
                if all(
                    status == "not-attempted-global-halt"
                    for status in row_statuses.values()
                ):
                    lane["status"] = "not-run-global-halt"
                else:
                    lane["status"] = "complete"
    if "unsafe-cleanup" in terminal_statuses:
        reconstructed["global_halt"] = {
            "active": True,
            "reason_code": "unsafe-cleanup",
        }
    return reconstructed


def _resume_state(
    config: BoundaryCampaignConfig,
    manifest: BoundaryManifest,
    projection: BoundaryEvidenceProjection,
) -> dict[str, Any]:
    root = Path(config.campaign_root).resolve()
    state = _read_json_object(root / "campaign-state.json")
    if state.get("schema") != "official-openvino-format-boundary-state/v2":
        raise ValueError("boundary campaign state schema is invalid")
    if (
        state.get("manifest_sha256") != manifest.sha256
        or state.get("projection_index_sha256") != projection.projection_index.sha256
        or state.get("limits") != _limits()
    ):
        raise ValueError("boundary campaign immutable state binding drift")
    reconstructed = _state_from_durable_receipts(config, manifest, projection)
    if state != reconstructed:
        raise ValueError("boundary campaign state does not match durable receipts")
    return reconstructed


def _admission_failure(case: BoundaryCase, observed: Any) -> RowFailure:
    if isinstance(observed, bool) or not isinstance(observed, int):
        reason = "available-ram-query-failed"
        hard = True
    elif observed < 3072 * _MIB:
        reason = "emergency-ram-floor"
        hard = True
    else:
        reason = "launch-ram-reserve"
        hard = False
    return RowFailure(
        reason,
        role="measurement",
        stage="admission",
        hard=hard,
        retryable=not hard,
        cleanup_proof=_not_launched_cleanup_proof(),
        observed_available_ram_bytes=(
            observed if isinstance(observed, int) and not isinstance(observed, bool) else None
        ),
        fingerprint=_failure_fingerprint(
            case, stage="admission", category="ram-floor", reason_code=reason
        ),
    )


def _attempt_record(
    failure: RowFailure,
    *,
    attempt_number: int,
    elapsed_seconds: float,
    stage_timeouts: Mapping[str, float],
) -> dict[str, Any]:
    raw_path = failure.receipt.resolve() if failure.receipt is not None else None
    return {
        "attempt_number": attempt_number,
        "role": failure.role,
        "stage": failure.stage,
        "reason_code": failure.reason_code,
        "hard": failure.hard,
        "retryable": failure.retryable,
        "fingerprint": failure.fingerprint,
        "raw_record_path": str(raw_path) if raw_path is not None else None,
        "raw_record_sha256": _file_sha256(raw_path),
        "cleanup_proof": failure.cleanup_proof,
        "observed_available_ram_bytes": failure.observed_available_ram_bytes,
        "elapsed_seconds": elapsed_seconds,
        "stage_timeouts": dict(stage_timeouts),
        "deadline_outcome": failure.deadline_outcome,
    }


def _terminal_status(attempts: list[dict[str, Any]]) -> str:
    last = attempts[-1]
    if last["cleanup_proof"].get("safe") is not True:
        return "unsafe-cleanup"
    if last["reason_code"] == "emergency-ram-floor":
        return "emergency-ram-floor"
    if last["reason_code"] == "row-deadline-exceeded":
        return "row-deadline-exceeded"
    if len(attempts) == 2:
        first = attempts[0]
        if (
            first["retryable"] is True
            and last["retryable"] is True
            and first["cleanup_proof"].get("safe") is True
            and last["cleanup_proof"].get("safe") is True
            and first["reason_code"] == last["reason_code"]
            and first["fingerprint"] == last["fingerprint"]
        ):
            return "confirmed-format-boundary"
        return "inconclusive-safety-boundary"
    return "safety-boundary"


def _terminal_receipt(
    case: BoundaryCase,
    attempts: list[dict[str, Any]],
    *,
    terminal_status: str,
    elapsed_seconds: float,
    stage_timeouts: Mapping[str, float],
) -> dict[str, Any]:
    last = attempts[-1]
    resource_failure = (
        "ram" in last["reason_code"] or "timeout" in last["reason_code"]
        or last["reason_code"] == "row-deadline-exceeded"
    )
    return {
        "schema": "official-openvino-format-boundary-terminal-boundary/v2",
        "status": terminal_status,
        "case": _case_descriptor(case),
        "role": last["role"],
        "stage": last["stage"],
        "reason_code": last["reason_code"],
        "fingerprint": last["fingerprint"],
        "attempt_count": len(attempts),
        "attempts": attempts,
        "observed_available_ram_bytes": last["observed_available_ram_bytes"],
        "configured_minimum_available_ram_mib": 4096,
        "configured_emergency_minimum_available_ram_mib": 3072,
        "limits": _limits(),
        "elapsed_seconds": elapsed_seconds,
        "stage_timeouts": dict(stage_timeouts),
        "deadline_outcome": last["deadline_outcome"] or "within-deadline",
        "raw_record_path": last["raw_record_path"],
        "raw_record_sha256": last["raw_record_sha256"],
        "cleanup_proof": last["cleanup_proof"],
        "envelope": _ENVELOPE_TEXT if resource_failure else None,
    }


def _skipped_receipt(case: BoundaryCase, status: str) -> dict[str, Any]:
    return {
        "schema": "official-openvino-format-boundary-skipped-row/v2",
        "status": status,
        "case": _case_descriptor(case),
        "attempt_count": 0,
        "limits": _limits(),
    }


def _write_skipped(
    root: Path,
    state: dict[str, Any],
    cases: tuple[BoundaryCase, ...],
    *,
    status: str,
) -> None:
    for case in cases:
        lane = state[_lane_name(case)]
        if case.internal_id in lane["rows"]:
            continue
        lane["rows"][case.internal_id] = status
        lane["skipped_count"] += 1
        _atomic_write_json(
            root / _lane_directory(case) / case.internal_id / "skipped-row.json",
            _skipped_receipt(case, status),
        )


def run_boundary_campaign(
    config: BoundaryCampaignConfig,
    *,
    run_measurement: Callable[..., Mapping[str, Any]],
    run_quality: Callable[..., Mapping[str, Any]],
    available_ram: Callable[[], int | None],
    monotonic: Callable[[], float] = time.monotonic,
) -> dict[str, Any]:
    """Execute the fixed serial CPU ladder and independent GPU control."""

    projection = prepare_boundary_projection(config)
    manifest = load_boundary_manifest(config.manifest_path)
    root = Path(config.campaign_root).resolve()
    state_path = root / "campaign-state.json"
    if config.resume:
        state = _resume_state(config, manifest, projection)
    else:
        allowed_fresh = {"execution-inputs", "preflight-receipt.json"}
        if {item.name for item in root.iterdir()} - allowed_fresh:
            raise ValueError(
                "fresh boundary root must contain only projected execution inputs "
                "and preflight receipt"
            )
        state = _initial_state(config, manifest, projection)
        _atomic_write_json(state_path, state)
    specs = _runtime_spec_map(projection)
    terminals = _terminal_map(projection)

    for cases in (manifest.cpu_cases, manifest.gpu_cases):
        lane_key = _lane_name(cases[0])
        lane = state[lane_key]
        if state["global_halt"]["active"]:
            lane["status"] = "not-run-global-halt"
            _write_skipped(root, state, cases, status="not-attempted-global-halt")
            _atomic_write_json(state_path, state)
            continue
        for index, case in enumerate(cases):
            row_root = root / _lane_directory(case) / case.internal_id
            accepted_path = row_root / "accepted-row.json"
            if accepted_path.exists():
                lane["rows"][case.internal_id] = "accepted"
                continue
            if case.internal_id in lane["rows"]:
                continue
            terminal_input = terminals.get(case.internal_id)
            if terminal_input is not None:
                descriptor = _read_json_object(terminal_input.path)
                if (
                    descriptor.get("reason") != "artifact-unavailable"
                    or descriptor.get("role") != "terminal-prerequisite"
                    or _file_sha256(terminal_input.path) != terminal_input.sha256
                ):
                    raise ValueError("terminal prerequisite projection is invalid")
                failure = RowFailure(
                    "artifact-unavailable",
                    role="terminal-prerequisite",
                    stage="preflight",
                    hard=True,
                    retryable=False,
                    receipt=terminal_input.path,
                    raw_record=descriptor,
                    fingerprint=_failure_fingerprint(
                        case,
                        stage="preflight",
                        category="terminal-prerequisite",
                        reason_code="artifact-unavailable",
                    ),
                    cleanup_proof=_not_launched_cleanup_proof(),
                )
                attempt = _attempt_record(
                    failure,
                    attempt_number=0,
                    elapsed_seconds=0.0,
                    stage_timeouts={},
                )
                terminal = _terminal_receipt(
                    case,
                    [attempt],
                    terminal_status="artifact-unavailable",
                    elapsed_seconds=0.0,
                    stage_timeouts={},
                )
                _atomic_write_json(row_root / "terminal-boundary.json", terminal)
                lane["status"] = "stopped"
                lane["reason_code"] = "artifact-unavailable"
                lane["terminal_status"] = "artifact-unavailable"
                lane["rows"][case.internal_id] = "artifact-unavailable"
                _write_skipped(
                    root,
                    state,
                    cases[index + 1 :],
                    status="not-attempted-after-boundary",
                )
                _atomic_write_json(state_path, state)
                break
            runtime_spec = specs.get(case.internal_id)
            if runtime_spec is None:
                raise ValueError(f"projected runtime spec is missing for {case.internal_id}")
            started = monotonic()
            row_deadline = started + 720.0
            attempts: list[dict[str, Any]] = []
            accepted_runtime: Mapping[str, Any] | None = None
            accepted_quality: Mapping[str, Any] | None = None
            final_timeouts: dict[str, float] = {}
            for attempt_number in (1, 2):
                lane["attempt_count"] += 1
                attempt_root = row_root / f"attempt-{attempt_number:03d}"
                observed_ram = available_ram()
                if (
                    isinstance(observed_ram, bool)
                    or not isinstance(observed_ram, int)
                    or observed_ram < 4096 * _MIB
                ):
                    failure = _admission_failure(case, observed_ram)
                    attempts.append(
                        _attempt_record(
                            failure,
                            attempt_number=attempt_number,
                            elapsed_seconds=monotonic() - started,
                            stage_timeouts={},
                        )
                    )
                else:
                    elapsed = monotonic() - started
                    remaining = row_deadline - monotonic()
                    if remaining <= 0:
                        failure = RowFailure(
                            "row-deadline-exceeded",
                            role="measurement",
                            stage="measurement",
                            hard=True,
                            retryable=False,
                            fingerprint=_failure_fingerprint(
                                case,
                                stage="measurement",
                                category="time",
                                reason_code="row-deadline-exceeded",
                            ),
                            cleanup_proof=_not_launched_cleanup_proof(),
                            deadline_outcome="exceeded-before-measurement",
                        )
                        attempts.append(
                            _attempt_record(
                                failure,
                                attempt_number=attempt_number,
                                elapsed_seconds=elapsed,
                                stage_timeouts={},
                            )
                        )
                    else:
                        runtime_timeout = min(180.0, remaining)
                        final_timeouts = {"measurement": runtime_timeout}
                        runtime_root = attempt_root / "runtime"
                        try:
                            run_measurement(
                                spec_path=runtime_spec.path,
                                campaign_root=runtime_root,
                                matrix_path=projection.comparison_matrix.path,
                                artifact_manifest_path=case.artifact_manifest_path,
                                build_provenance_path=_quality_input(
                                    config,
                                    projection,
                                    case,
                                    runtime_spec,
                                    attempt_root,
                                    timeout_seconds=90.0,
                                ).build_provenance_path,
                                build_root=Path(config.build_root).resolve(),
                                repo_root=Path(config.repository_root).resolve(),
                                python_executable=Path(config.python_executable).resolve(),
                                python_site_packages=Path(config.python_site_packages).resolve(),
                                openvino_libraries=Path(config.openvino_libraries).resolve(),
                                sampler_script=Path(config.sampler_script).resolve(),
                                timeout_seconds=runtime_timeout,
                                monotonic_deadline=row_deadline,
                                monotonic=monotonic,
                                launch_minimum_available_ram_mib=4096,
                                emergency_minimum_available_ram_mib=3072,
                            )
                            runtime_input = _quality_input(
                                config,
                                projection,
                                case,
                                runtime_spec,
                                attempt_root,
                                timeout_seconds=90.0,
                            )
                            accepted_runtime = reconcile_runtime_evidence(runtime_input)
                        except Exception as error:
                            failure = _normalise_failure(
                                error,
                                case=case,
                                default_role="measurement",
                                default_raw_path=runtime_root / "attempt-sequence.json",
                            )
                            attempts.append(
                                _attempt_record(
                                    failure,
                                    attempt_number=attempt_number,
                                    elapsed_seconds=monotonic() - started,
                                    stage_timeouts=final_timeouts,
                                )
                            )
                        else:
                            elapsed = monotonic() - started
                            remaining = row_deadline - monotonic()
                            if remaining <= 0:
                                failure = RowFailure(
                                    "row-deadline-exceeded",
                                    role="measurement",
                                    stage="measurement",
                                    hard=True,
                                    retryable=False,
                                    receipt=runtime_root / "attempt-sequence.json",
                                    fingerprint=_failure_fingerprint(
                                        case,
                                        stage="measurement",
                                        category="time",
                                        reason_code="row-deadline-exceeded",
                                    ),
                                    cleanup_proof=dict(
                                        accepted_runtime.get("cleanup_proof", {})
                                    ),
                                    deadline_outcome="exceeded-after-measurement",
                                )
                                attempts.append(
                                    _attempt_record(
                                        failure,
                                        attempt_number=attempt_number,
                                        elapsed_seconds=elapsed,
                                        stage_timeouts=final_timeouts,
                                    )
                                )
                            else:
                                quality_timeout = min(90.0, remaining)
                                final_timeouts["quality"] = quality_timeout
                                quality_input = replace(
                                    runtime_input, timeout_seconds=quality_timeout
                                )
                                try:
                                    run_quality(
                                        quality_input,
                                        resume=False,
                                        monotonic_deadline=row_deadline,
                                        monotonic=monotonic,
                                    )
                                    accepted_quality = reconcile_quality_evidence(
                                        quality_input
                                    )
                                except Exception as error:
                                    failure = _normalise_failure(
                                        error,
                                        case=case,
                                        default_role="quality",
                                        default_raw_path=(
                                            quality_input.output_root / "capture-summary.json"
                                        ),
                                    )
                                    attempts.append(
                                        _attempt_record(
                                            failure,
                                            attempt_number=attempt_number,
                                            elapsed_seconds=monotonic() - started,
                                            stage_timeouts=final_timeouts,
                                        )
                                    )
                                else:
                                    elapsed = monotonic() - started
                                    if elapsed >= 720.0:
                                        failure = RowFailure(
                                            "row-deadline-exceeded",
                                            role="quality",
                                            stage="quality",
                                            hard=True,
                                            retryable=False,
                                            receipt=(
                                                quality_input.output_root
                                                / "capture-summary.json"
                                            ),
                                            fingerprint=_failure_fingerprint(
                                                case,
                                                stage="quality",
                                                category="time",
                                                reason_code="row-deadline-exceeded",
                                            ),
                                            cleanup_proof={
                                                "safe": True,
                                                "cleanup_process_count": 0,
                                                "residual_owned_process_count": 0,
                                                "emergency_actions": [],
                                                "job_active_pid_evidence": "validated-quality-receipts",
                                                "active_pids_after_cleanup": [],
                                            },
                                            deadline_outcome="exceeded-after-quality",
                                        )
                                        attempts.append(
                                            _attempt_record(
                                                failure,
                                                attempt_number=attempt_number,
                                                elapsed_seconds=elapsed,
                                                stage_timeouts=final_timeouts,
                                            )
                                        )
                                    else:
                                        receipt = {
                                            "schema": "official-openvino-format-boundary-accepted-row/v2",
                                            "status": "accepted",
                                            "case": _case_descriptor(case),
                                            "attempt_count": attempt_number,
                                            "selected_attempt_number": attempt_number,
                                            "execution_root": str(attempt_root.resolve()),
                                            "limits": _limits(),
                                            "stage_timeouts": final_timeouts,
                                            "monotonic_deadline": row_deadline,
                                            "elapsed_seconds": elapsed,
                                            "deadline_outcome": "within-deadline",
                                            "runtime": dict(accepted_runtime),
                                            "quality": dict(accepted_quality),
                                        }
                                        _atomic_write_json(accepted_path, receipt)
                                        lane["accepted_count"] += 1
                                        lane["rows"][case.internal_id] = "accepted"
                                        _atomic_write_json(state_path, state)
                                        break
                if lane["rows"].get(case.internal_id) == "accepted":
                    break
                last_attempt = attempts[-1]
                safe_retry = (
                    attempt_number == 1
                    and last_attempt["retryable"] is True
                    and last_attempt["cleanup_proof"].get("safe") is True
                    and (720.0 - (monotonic() - started)) > 0
                )
                if not safe_retry:
                    break
            if lane["rows"].get(case.internal_id) == "accepted":
                continue
            terminal_status = _terminal_status(attempts)
            terminal = _terminal_receipt(
                case,
                attempts,
                terminal_status=terminal_status,
                elapsed_seconds=monotonic() - started,
                stage_timeouts=final_timeouts,
            )
            _atomic_write_json(row_root / "terminal-boundary.json", terminal)
            lane["status"] = "stopped"
            lane["reason_code"] = attempts[-1]["reason_code"]
            lane["terminal_status"] = terminal_status
            lane["rows"][case.internal_id] = terminal_status
            _write_skipped(
                root,
                state,
                cases[index + 1 :],
                status="not-attempted-after-boundary",
            )
            if terminal_status == "unsafe-cleanup":
                state["global_halt"] = {
                    "active": True,
                    "reason_code": "unsafe-cleanup",
                }
            _atomic_write_json(state_path, state)
            break
        else:
            if lane["status"] == "running":
                lane["status"] = "complete"
                _atomic_write_json(state_path, state)
    return state


def load_boundary_status(config: BoundaryCampaignConfig) -> dict[str, Any]:
    """Revalidate projection, state, receipts, and their evidence without launch."""

    checked = config if config.resume else replace(config, resume=True)
    projection = prepare_boundary_projection(checked, status_only=True)
    manifest = load_boundary_manifest(checked.manifest_path)
    return _resume_state(checked, manifest, projection)


def boundary_campaign_exit_code(state: Mapping[str, Any]) -> int:
    halt = state.get("global_halt")
    if isinstance(halt, Mapping) and halt.get("active") is True:
        return 3
    return 0


def durable_row_lines(
    config: BoundaryCampaignConfig,
    state: Mapping[str, Any],
) -> list[str]:
    """Render exactly one canonical JSON line for every durable row receipt."""

    if not isinstance(state, Mapping):
        raise TypeError("boundary campaign state must be a mapping")
    manifest = load_boundary_manifest(config.manifest_path)
    root = Path(config.campaign_root).resolve()
    lines: list[str] = []
    for case in (*manifest.cpu_cases, *manifest.gpu_cases):
        row_root = root / _lane_directory(case) / case.internal_id
        paths = (
            row_root / "accepted-row.json",
            row_root / "terminal-boundary.json",
            row_root / "skipped-row.json",
        )
        existing = [path for path in paths if path.is_file()]
        if not existing:
            continue
        if len(existing) != 1:
            raise ValueError("boundary row has ambiguous durable output")
        receipt = _read_json_object(existing[0])
        _validate_case(receipt, case)
        line: dict[str, Any] = {
            "lane": case.lane,
            "case": _case_descriptor(case),
            "status": receipt["status"],
            "attempt_count": receipt.get("attempt_count", 0),
        }
        if existing[0].name == "accepted-row.json":
            runtime = receipt.get("runtime")
            quality = receipt.get("quality")
            if not isinstance(runtime, Mapping) or not isinstance(quality, Mapping):
                raise ValueError("accepted boundary row output is incomplete")
            line["runtime_summary"] = runtime.get("measurement_summary")
            line["quality_summary"] = quality.get("capture_summary")
        elif existing[0].name == "terminal-boundary.json":
            line["reason_code"] = receipt.get("reason_code")
            line["fingerprint"] = receipt.get("fingerprint")
        lines.append(
            json.dumps(
                line,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=True,
                allow_nan=False,
            )
        )
    return lines


def _artifact_path(value: Any) -> Path | None:
    if value is None:
        return None
    if not isinstance(value, str) or not value:
        raise ValueError("artifact manifest path must be a non-blank string or null")
    candidate = Path(value)
    resolved = candidate.resolve() if candidate.is_absolute() else (_ROOT / candidate).resolve()
    try:
        resolved.relative_to(_ROOT.resolve())
    except ValueError as exc:
        raise ValueError("artifact manifest path escapes repository root") from exc
    return resolved


def _case(raw: Any) -> BoundaryCase:
    if not isinstance(raw, dict):
        raise ValueError("boundary case must be an object")
    try:
        case = BoundaryCase(
            internal_id=raw["internal_id"], label=raw["label"], lane=raw["lane"],
            order=raw["order"], weight_precision=raw["weight_precision"],
            key_algorithm=raw["key_algorithm"], value_algorithm=raw["value_algorithm"],
            key_precision=raw["key_precision"], value_precision=raw["value_precision"],
            device=raw["device"], context=raw["context"],
            artifact_manifest_path=_artifact_path(raw.get("artifact_manifest_path")),
        )
    except KeyError as exc:
        raise ValueError(f"boundary case is missing {exc.args[0]}") from exc
    if not isinstance(case.internal_id, str) or not case.internal_id:
        raise ValueError("boundary internal id must be non-blank")
    if not isinstance(case.label, str) or not case.label:
        raise ValueError("boundary label must be non-blank")
    if case.lane not in {"cpu", "gpu-control"} or case.device not in {"CPU", "GPU"}:
        raise ValueError("boundary lane or device is invalid")
    if case.weight_precision not in {"u4", "u8", "f16"}:
        raise ValueError("boundary weight precision is invalid")
    if case.key_algorithm not in {"STANDARD", "TBQ3", "TBQ4"} or case.value_algorithm != case.key_algorithm:
        raise ValueError("boundary cache algorithms must be matched supported values")
    if case.key_precision not in {"f16", "u4", "u3"} or case.value_precision != case.key_precision:
        raise ValueError("boundary cache precisions must be matched supported values")
    if type(case.order) is not int or case.order < 1 or case.context != 512:
        raise ValueError("boundary order or context is invalid")
    if (case.lane == "cpu") != (case.device == "CPU"):
        raise ValueError("boundary lane/device pairing is invalid")
    if case.device == "GPU" and case.key_algorithm != "STANDARD":
        raise ValueError("GPU TurboQuant cases are prohibited")
    if case.key_algorithm == "STANDARD" and case.key_precision != "f16":
        raise ValueError("STANDARD cache requires F16 precisions")
    if case.key_algorithm == "TBQ3" and case.key_precision != "u3":
        raise ValueError("TBQ3 cache requires U3 precisions")
    if case.key_algorithm == "TBQ4" and case.key_precision != "u4":
        raise ValueError("TBQ4 cache requires U4 precisions")
    return case


def load_boundary_manifest(path: Path) -> BoundaryManifest:
    raw = Path(path).read_bytes()
    try:
        document = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError("boundary manifest is invalid JSON") from exc
    if not isinstance(document, dict) or document.get("schema") != "official-openvino-format-boundary-matrix/v1":
        raise ValueError("boundary manifest schema is invalid")
    rows = document.get("cases")
    if not isinstance(rows, list):
        raise ValueError("boundary manifest cases must be a list")
    cases = tuple(_case(row) for row in rows)
    if len({case.internal_id for case in cases}) != len(cases):
        raise ValueError("duplicate boundary internal ids")
    cpu_cases = tuple(case for case in cases if case.lane == "cpu")
    gpu_cases = tuple(case for case in cases if case.lane == "gpu-control")
    for lane_cases in (cpu_cases, gpu_cases):
        if len({case.order for case in lane_cases}) != len(lane_cases):
            raise ValueError("duplicate boundary order")
    if tuple((case.weight_precision, case.key_algorithm) for case in sorted(cpu_cases, key=lambda item: item.order)) != _CPU_ORDER:
        raise ValueError("CPU boundary order differs from the global constraint")
    if len(gpu_cases) != 1 or gpu_cases[0].weight_precision != "u4":
        raise ValueError("boundary manifest requires one U4 GPU control")
    expected_terminal_ids = {
        "cpu-f16-tbq3", "cpu-f16-tbq4", "cpu-f16-standard",
    }
    actual_terminal_ids = {
        case.internal_id for case in cases if case.artifact_manifest_path is None
    }
    if actual_terminal_ids != expected_terminal_ids or any(
        case.weight_precision != "f16"
        for case in cases
        if case.internal_id in actual_terminal_ids
    ):
        raise ValueError(
            "only the three F16 boundary identities may have null artifacts"
        )
    return BoundaryManifest(
        cpu_cases=tuple(sorted(cpu_cases, key=lambda item: item.order)),
        gpu_cases=tuple(sorted(gpu_cases, key=lambda item: item.order)),
        source_path=Path(path).resolve(), sha256=hashlib.sha256(raw).hexdigest(),
    )


def _projection_file(path: Path) -> ProjectionFile:
    source = Path(path).resolve()
    return ProjectionFile(path=source, sha256=hashlib.sha256(source.read_bytes()).hexdigest())


def _write_or_validate_json(path: Path, value: Mapping[str, Any]) -> ProjectionFile:
    target = Path(path).resolve()
    expected = _canonical_bytes(value)
    if target.exists():
        if not target.is_file() or target.read_bytes() != expected:
            raise ValueError(f"immutable projection drift detected: {target}")
    else:
        _atomic_write_json(target, value)
    if target.read_bytes() != expected:
        raise ValueError(f"immutable projection drift detected: {target}")
    return ProjectionFile(path=target, sha256=hashlib.sha256(expected).hexdigest())


def _binding(file: ProjectionFile) -> dict[str, str]:
    return {"path": str(file.path), "sha256": file.sha256}


def _require_sha256(value: Any, field: str) -> str:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in _SHA256_HEX for character in value)
    ):
        raise ValueError(f"{field} must be a SHA-256 hex digest")
    return value


def _model_file_bindings(manifest: Mapping[str, Any], model_root: Path) -> list[dict[str, Any]]:
    rows = manifest.get("files")
    if not isinstance(rows, list):
        raise ValueError("artifact manifest files must be an array")
    selected: list[dict[str, Any]] = []
    for name in ("openvino_model.bin", "openvino_model.xml"):
        matches = [row for row in rows if isinstance(row, Mapping) and row.get("path") == name]
        if len(matches) != 1:
            raise ValueError(f"artifact manifest must bind exactly one {name}")
        row = matches[0]
        size = row.get("size_bytes")
        model_file = (model_root / name).resolve()
        if (
            isinstance(size, bool)
            or not isinstance(size, int)
            or size <= 0
            or not model_file.is_file()
            or model_file.stat().st_size != size
        ):
            raise ValueError(f"artifact model file is missing or has size drift: {name}")
        expected_sha256 = _require_sha256(row.get("sha256"), f"{name} hash")
        if sha256_file(model_file) != expected_sha256:
            raise ValueError(f"artifact model file hash drift: {name}")
        selected.append(
            {"path": name, "size_bytes": size, "sha256": expected_sha256}
        )
    return selected


def _build_execution_binding(
    build_root: Path,
    *,
    repository_root: Path,
    build_identity: Mapping[str, Any],
) -> dict[str, Any]:
    build = _build_root(build_root)
    package = build / "openvino_genai"
    modules = sorted(package.glob("py_openvino_genai*.pyd"))
    initializer = package / "__init__.py"
    runtime_dll = package / "openvino_genai.dll"
    provenance_value = build_identity.get("path")
    if not isinstance(provenance_value, str) or not provenance_value:
        raise ValueError("build identity provenance path is missing")
    provenance = (repository_root / provenance_value).resolve()
    try:
        provenance.relative_to(repository_root)
    except ValueError as error:
        raise ValueError("build provenance escapes repository root") from error
    if not provenance.is_file():
        raise ValueError(f"build provenance is missing: {provenance}")
    provenance_sha256 = sha256_file(provenance)
    if provenance_sha256 != _require_sha256(
        build_identity.get("sha256"), "build provenance hash"
    ):
        raise ValueError("build provenance hash differs from frozen identity")
    provenance_payload = _read_json_object(provenance)
    if provenance_payload.get("status") != "passed":
        raise ValueError("build provenance does not report passed status")

    def executable_file(path: Path) -> dict[str, str]:
        return {"path": str(path.resolve()), "sha256": sha256_file(path)}

    return {
        "root": str(build),
        "root_sha256": _directory_sha256(build),
        "package_initializer": executable_file(initializer),
        "python_module": executable_file(modules[0]),
        "runtime_dll": executable_file(runtime_dll),
        "provenance": {"path": str(provenance), "sha256": provenance_sha256},
    }


def _artifact_binding(
    case: BoundaryCase,
    *,
    repository_root: Path,
    authoritative_cases: tuple[Any, ...],
) -> dict[str, Any]:
    if case.artifact_manifest_path is None:
        raise ValueError("executable boundary case lacks an artifact manifest")
    candidates = [
        row for row in authoritative_cases
        if row.weight_precision == case.weight_precision
        and row.artifact_status == "available"
    ]
    identities = {
        (
            row.artifact_id,
            str(Path(row.artifact_manifest_path).resolve()),
            row.artifact_manifest_sha256,
        )
        for row in candidates
    }
    if len(identities) != 1:
        raise ValueError(
            f"authoritative artifact identity is ambiguous for {case.weight_precision}"
        )
    artifact_id, expected_path, expected_sha256 = identities.pop()
    manifest_path = case.artifact_manifest_path.resolve()
    if manifest_path != Path(expected_path) or not manifest_path.is_file():
        raise ValueError(f"{case.internal_id} artifact manifest differs from proven identity")
    manifest_sha256 = hashlib.sha256(manifest_path.read_bytes()).hexdigest()
    if manifest_sha256 != expected_sha256:
        raise ValueError(f"{case.internal_id} artifact manifest hash differs from proven identity")
    manifest = _read_json_object(manifest_path)
    if manifest.get("artifact_id") != artifact_id:
        raise ValueError(f"{case.internal_id} artifact id differs from proven identity")
    model_root_raw = manifest.get("artifact_root")
    if not isinstance(model_root_raw, str) or not model_root_raw.strip():
        raise ValueError("artifact manifest model root is missing")
    model_root = Path(model_root_raw).resolve()
    try:
        model_root.relative_to(repository_root)
    except ValueError as error:
        raise ValueError("artifact model root escapes repository root") from error
    if not model_root.is_dir():
        raise ValueError(f"artifact model root is missing: {model_root}")
    return {
        "artifact_id": artifact_id,
        "artifact_manifest_path": str(manifest_path),
        "artifact_manifest_sha256": manifest_sha256,
        "artifact_inventory_sha256": _require_sha256(
            manifest.get("inventory_sha256"), "artifact inventory hash"
        ),
        "model_path": str(model_root),
        "model_files": _model_file_bindings(manifest, model_root),
    }


def _projected_matrix_case(
    case: BoundaryCase,
    *,
    artifact: Mapping[str, Any] | None,
    terminal: ProjectionFile | None,
) -> dict[str, Any]:
    turboquant = case.key_algorithm != "STANDARD"
    return {
        "test_id": case.internal_id,
        "phase": "formal",
        "description": case.label,
        "model": "granite-3b",
        "weight_precision": case.weight_precision,
        "k_algorithm": case.key_algorithm.lower(),
        "v_algorithm": case.value_algorithm.lower(),
        "k_precision": case.key_precision,
        "v_precision": case.value_precision,
        "device": case.device.lower(),
        "contexts": [512],
        "guard": "ram-2048-mib",
        "quality_required": True,
        "required_metrics": list(_FORMAL_METRICS),
        "key_cache_precision": case.key_precision,
        "value_cache_precision": case.value_precision,
        "requested_device": case.device,
        "runtime_key_algorithm": case.key_algorithm,
        "runtime_value_algorithm": case.value_algorithm,
        "norm_correction": turboquant,
        "attention_path": (
            "stateful_sdpa_reference_codec" if turboquant else "stateful_sdpa_standard"
        ),
        "execution_route": "patched-stateful" if turboquant else "stateful-standard",
        "expected_outcome": "pass",
        "suitable_host_required": False,
        "numeric_generation_metrics_expected": True,
        "artifact_id": artifact["artifact_id"] if artifact is not None else None,
        "artifact_manifest_path": (
            artifact["artifact_manifest_path"] if artifact is not None else None
        ),
        "artifact_manifest_sha256": (
            artifact["artifact_manifest_sha256"] if artifact is not None else None
        ),
        "artifact_status": "available" if artifact is not None else "artifact-unavailable",
        "artifact_terminal_path": str(terminal.path) if terminal is not None else None,
        "artifact_terminal_sha256": terminal.sha256 if terminal is not None else None,
    }


def project_boundary_evidence_inputs(
    *,
    repository_root: Path,
    campaign_root: Path,
    build_root: Path,
    manifest_path: Path,
    comparison_matrix_path: Path,
) -> BoundaryEvidenceProjection:
    """Project immutable matrix/spec/prerequisite inputs without model execution."""

    from scripts.testing.official_openvino.matrix import (
        load_matrix,
        load_matrix_metadata,
    )

    repository = Path(repository_root).resolve()
    campaign = Path(campaign_root).resolve()
    requested_build = Path(build_root).resolve()
    if repository != _ROOT.resolve() or not repository.is_dir():
        raise ValueError("repository_root must be this repository")
    if _paths_overlap(campaign, requested_build):
        raise ValueError("campaign root must not overlap build root")
    manifest = load_boundary_manifest(Path(manifest_path))
    comparison_source = Path(comparison_matrix_path).resolve()
    for source, field in (
        (manifest.source_path, "boundary manifest"),
        (comparison_source, "authoritative comparison matrix"),
    ):
        try:
            source.relative_to(repository)
        except ValueError as error:
            raise ValueError(f"{field} escapes repository root") from error
        if not source.is_file():
            raise ValueError(f"{field} is missing: {source}")

    identities = load_matrix_metadata(comparison_source)
    build_binding = _build_execution_binding(
        requested_build,
        repository_root=repository,
        build_identity=identities["build_identity"],
    )
    build = Path(build_binding["root"])
    authoritative_cases = tuple(load_matrix(comparison_source))
    boundary_file = ProjectionFile(manifest.source_path, manifest.sha256)
    authoritative_file = _projection_file(comparison_source)
    output_root = campaign / "execution-inputs"
    cases = (*manifest.cpu_cases, *manifest.gpu_cases)
    artifacts: dict[str, dict[str, Any]] = {}
    verified_artifacts: dict[tuple[str, Path], dict[str, Any]] = {}
    terminals: list[TerminalBoundaryInput] = []
    terminal_files: dict[str, ProjectionFile] = {}

    for case in cases:
        if case.artifact_manifest_path is not None:
            artifact_key = (
                case.weight_precision,
                case.artifact_manifest_path.resolve(),
            )
            if artifact_key not in verified_artifacts:
                verified_artifacts[artifact_key] = _artifact_binding(
                    case,
                    repository_root=repository,
                    authoritative_cases=authoritative_cases,
                )
            artifacts[case.internal_id] = verified_artifacts[artifact_key]
            continue
        descriptor = {
            "schema": "official-openvino-format-boundary-terminal-prerequisite/v1",
            "role": "terminal-prerequisite",
            "reason": "artifact-unavailable",
            "case": {
                "internal_id": case.internal_id,
                "label": case.label,
                "lane": case.lane,
                "order": case.order,
                "weight_precision": case.weight_precision,
                "key_algorithm": case.key_algorithm,
                "value_algorithm": case.value_algorithm,
            },
            "boundary_manifest": _binding(boundary_file),
            "source_identity": identities["source_identity"],
            "build_identity": identities["build_identity"],
        }
        descriptor_file = _write_or_validate_json(
            output_root / "terminal-prerequisites" / f"{case.internal_id}.json",
            descriptor,
        )
        terminal_files[case.internal_id] = descriptor_file
        terminals.append(TerminalBoundaryInput(case.internal_id, descriptor_file))

    matrix_payload = {
        "source_identity": identities["source_identity"],
        "build_identity": identities["build_identity"],
        "cases": [
            _projected_matrix_case(
                case,
                artifact=artifacts.get(case.internal_id),
                terminal=terminal_files.get(case.internal_id),
            )
            for case in cases
        ],
    }
    matrix_file = _write_or_validate_json(
        output_root / "comparison-matrix.json", matrix_payload
    )
    # Validate through the actual governed loader before exposing any specs.
    load_matrix(matrix_file.path)

    executable: list[ExecutableBoundaryInput] = []
    runtime_index: list[dict[str, Any]] = []
    workload = {**build_context_workload(512), "actual_input_tokens": 512}
    for case in cases:
        artifact = artifacts.get(case.internal_id)
        if artifact is None:
            continue
        runtime = build_boundary_worker_spec(
            case,
            role="pilot",
            cache_dir=campaign / "cache" / case.internal_id / "512",
        )
        spec_payload = {
            "schema": "official-openvino-adaptive-comparison-runtime-spec/v1",
            "controlled_test_id": case.internal_id,
            "artifact_id": artifact["artifact_id"],
            "artifact_manifest_path": artifact["artifact_manifest_path"],
            "artifact_manifest_sha256": artifact["artifact_manifest_sha256"],
            "model_path": artifact["model_path"],
            "device": runtime["device"],
            "context_tokens": 512,
            "workload": workload,
            "properties": runtime["properties"],
            "max_new_tokens": 4,
            "ignore_eos": True,
            "seed": 42,
            "apply_chat_template": False,
        }
        spec_file = _write_or_validate_json(
            output_root / "runtime-specs" / case.internal_id / "512" / "runtime-spec.json",
            spec_payload,
        )
        executable.append(ExecutableBoundaryInput(case.internal_id, spec_file))
        runtime_index.append(
            {
                "case_internal_id": case.internal_id,
                "runtime_spec": _binding(spec_file),
                "artifact_binding": artifact,
            }
        )

    terminal_index = [
        {"case_internal_id": item.case_internal_id,
         "descriptor": _binding(item.descriptor)}
        for item in terminals
    ]
    index_payload = {
        "schema": "official-openvino-format-boundary-projection-index/v1",
        "roots": {
            "repository": str(repository),
            "campaign": str(campaign),
            "build": str(build),
        },
        "boundary_manifest": _binding(boundary_file),
        "authoritative_comparison_matrix": _binding(authoritative_file),
        "projected_comparison_matrix": _binding(matrix_file),
        "source_identity": identities["source_identity"],
        "build_identity": identities["build_identity"],
        "build": build_binding,
        "runtime_specs": runtime_index,
        "terminal_prerequisites": terminal_index,
    }
    index_file = _write_or_validate_json(output_root / "projection-index.json", index_payload)
    return BoundaryEvidenceProjection(
        repository_root=repository,
        campaign_root=campaign,
        build_root=build,
        boundary_manifest=boundary_file,
        comparison_matrix=matrix_file,
        runtime_specs=tuple(executable),
        terminal_prerequisites=tuple(terminals),
        projection_index=index_file,
    )


def build_boundary_worker_spec(
    case: BoundaryCase, *, role: str, cache_dir: Path,
) -> dict[str, Any]:
    if role not in {"pilot", "sample", "warmup"}:
        raise ValueError("boundary worker role is invalid")
    if not isinstance(cache_dir, Path):
        raise ValueError("cache_dir must be a Path")
    if case.device == "CPU":
        runtime = build_runtime_property_spec(
            device=case.device, key_algorithm=case.key_algorithm,
            value_algorithm=case.value_algorithm, key_cache_precision=case.key_precision,
            value_cache_precision=case.value_precision,
            norm_correction=case.key_algorithm != "STANDARD", cache_dir=str(cache_dir),
        )
    else:
        runtime = {
            "device": "GPU",
            "properties": {
                "ATTENTION_BACKEND": "SDPA", "CACHE_DIR": str(cache_dir),
                "PERFORMANCE_HINT": "LATENCY", "KEY_CACHE_PRECISION": "f16",
                "VALUE_CACHE_PRECISION": "f16",
            },
        }
    return {"role": role, "internal_id": case.internal_id, "label": case.label,
            "context": case.context, **runtime}
