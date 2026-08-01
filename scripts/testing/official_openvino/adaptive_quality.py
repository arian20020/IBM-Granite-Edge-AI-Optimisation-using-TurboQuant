"""Isolated, append-only P1-P6 quality capture for adaptive campaigns."""

from __future__ import annotations

import hashlib
import json
import math
import os
from collections.abc import Callable, Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from scripts.testing.official_openvino.guarded_build import (
    GUARD_SCHEMA,
    GuardLimits,
    _effective_environment,
    run_guarded_command,
)
from scripts.testing.official_openvino.owned_process_guard import (
    KillOnCloseJob,
    available_ram_bytes,
)
from scripts.testing.official_openvino.quality_campaign import (
    AcceptedQualityCampaign,
    QualityCampaignInput,
    _accepted_campaign_fingerprint,
    _accepted_input_from_campaign,
    _canonical_identity_bytes,
    _sha256_json,
    _strict_object,
    load_accepted_quality_campaign,
)
from scripts.testing.official_openvino.quality_worker import (
    GENERATION_SETTINGS,
    PROMPT_RESULT_SCHEMA,
    PROMPT_SPEC_SCHEMA,
    _canonical_json,
)


PROMPT_IDS = ("P1", "P2", "P3", "P4", "P5", "P6")
CAPTURE_SCHEMA = "official-openvino-adaptive-quality-capture/v1"
TERMINAL_GUARD_SCHEMA = "official-openvino-adaptive-quality-terminal-guard/v1"
QUALITY_RECOVERY_SCHEMA = "official-openvino-adaptive-quality-recovery/v1"
SPEC_INDEX_SCHEMA = "official-openvino-adaptive-comparison-spec-index/v1"
MIB = 1024**2
LAUNCH_FLOOR_BYTES = 4096 * MIB
EMERGENCY_FLOOR_BYTES = 2048 * MIB


@dataclass(frozen=True)
class GovernedQualityPromptResult:
    prompt_id: str
    status: str
    prompt_root: Path
    worker_spec_sha256: str
    worker_result_sha256: str
    guard_evidence_sha256: str
    cleanup_process_count: int


@dataclass(frozen=True)
class AdaptiveQualityCampaignInput(QualityCampaignInput):
    attempt_sequence_path: Path
    adaptive_runtime_spec_path: Path
    pilot_spec_path: Path
    spec_index_path: Path
    artifact_inventory_path: Path


def _input_evidence_paths(
    campaign_input: QualityCampaignInput,
) -> dict[str, Path] | None:
    if not isinstance(campaign_input, AdaptiveQualityCampaignInput):
        return None
    return {
        field: Path(getattr(campaign_input, field)).resolve()
        for field in (
            "attempt_sequence_path",
            "adaptive_runtime_spec_path",
            "pilot_spec_path",
            "spec_index_path",
            "artifact_inventory_path",
        )
    }


def _sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with Path(path).open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _plain_json(value: Any) -> Any:
    if isinstance(value, Mapping):
        return {key: _plain_json(item) for key, item in value.items()}
    if isinstance(value, (list, tuple)):
        return [_plain_json(item) for item in value]
    return value


def _require_prompt_id(prompt_id: Any) -> str:
    if prompt_id not in PROMPT_IDS:
        raise ValueError("quality prompt id must be P1 through P6")
    return str(prompt_id)


def _prompt_root(campaign: AcceptedQualityCampaign, prompt_id: str) -> Path:
    return Path(campaign.output_root).resolve() / prompt_id


def _prompt_evidence_paths(
    campaign: AcceptedQualityCampaign,
    evidence_paths: Mapping[str, Any] | None,
) -> dict[str, Path]:
    expected = {
        "attempt_sequence_path",
        "adaptive_runtime_spec_path",
        "pilot_spec_path",
        "spec_index_path",
        "artifact_inventory_path",
    }
    if evidence_paths is None:
        attempt_sequence = (campaign.campaign_root / "attempt-sequence.json").resolve()
        sequence = _strict_object(
            attempt_sequence.read_bytes(), source=attempt_sequence
        )
        pilot = sequence.get("pilot")
        if not isinstance(pilot, Mapping) or not isinstance(
            pilot.get("spec_path"), str
        ):
            raise ValueError("quality prompt pilot spec binding is missing")
        pilot_spec = (campaign.campaign_root / pilot["spec_path"]).resolve()
        candidate_index = campaign.spec_path.parent / "spec-index.json"
        return {
            "attempt_sequence_path": attempt_sequence,
            "adaptive_runtime_spec_path": Path(campaign.spec_path).resolve(),
            "pilot_spec_path": pilot_spec,
            "spec_index_path": (
                candidate_index.resolve()
                if candidate_index.is_file()
                else Path(campaign.spec_path).resolve()
            ),
            "artifact_inventory_path": Path(
                campaign.artifact_manifest_path
            ).resolve(),
        }
    if not isinstance(evidence_paths, Mapping) or set(evidence_paths) != expected:
        raise ValueError("quality prompt evidence path bindings are invalid")
    normalized = {
        field: Path(str(evidence_paths[field])).resolve() for field in expected
    }
    if any(not path.is_file() for path in normalized.values()):
        raise ValueError("quality prompt evidence path binding is missing")
    return normalized


def _build_prompt_spec(
    campaign: AcceptedQualityCampaign,
    prompt_id: str,
    *,
    prompt_root: Path,
    evidence_paths: Mapping[str, Any] | None = None,
) -> dict[str, Any]:
    accepted = load_accepted_quality_campaign(
        _accepted_input_from_campaign(campaign)
    )
    identity = accepted.identity["identity"]
    config = identity["config"]
    prompt = accepted.prompt_contract["prompts"][prompt_id]["execution"]
    if prompt_id == "P6":
        turns = [
            {"turn_id": "P6-turn-1", "prompt": prompt["turn_1_prompt"]},
            {
                "turn_id": "P6-turn-2",
                "prompt": prompt["turn_2_prompt"],
                "history_source_turn_id": "P6-turn-1",
            },
        ]
    else:
        turns = [{"turn_id": prompt_id, "prompt": prompt["prompt"]}]
    spec_path = Path(prompt_root).resolve() / "worker-spec.json"
    result_path = Path(prompt_root).resolve() / "worker-result.json"
    command = [
        str(accepted.python_executable),
        "-m",
        "scripts.testing.official_openvino.quality_worker",
        "--spec",
        str(spec_path),
        "--result",
        str(result_path),
    ]
    build = identity["build"]
    model = identity["model"]
    summary_sources = accepted.measurement_summary.get("sources", ())
    if not isinstance(summary_sources, (list, tuple)) or len(summary_sources) != 3:
        raise ValueError("quality prompt runtime raw samples are incomplete")
    raw_samples = []
    for source in summary_sources:
        if not isinstance(source, Mapping):
            raise ValueError("quality prompt runtime raw sample is invalid")
        raw_path = Path(str(source.get("path"))).resolve()
        raw_samples.append(
            {"path": str(raw_path), "sha256": str(source.get("sha256"))}
        )
    evidence = _prompt_evidence_paths(accepted, evidence_paths)
    build_identity = _plain_json(build)
    runtime_property = _plain_json(config)
    quality_worker_path = Path(__file__).resolve().with_name("quality_worker.py")
    return {
        "schema": PROMPT_SPEC_SCHEMA,
        "prompt_id": prompt_id,
        "model_path": str(
            Path(model["validated_artifact"]["artifact_root"]).resolve()
        ),
        "device": config["device"],
        "properties": dict(config["properties"]),
        "generation_settings": dict(GENERATION_SETTINGS),
        "turns": turns,
        "private_controller": {
            "test_id": accepted.measurement_summary["test_id"],
            "context_tokens": accepted.measurement_summary["context_tokens"],
        },
        "bindings": {
            "runtime_summary_path": str(
                (accepted.campaign_root / "measurement-summary.json").resolve()
            ),
            "runtime_summary_sha256": accepted.measurement_summary_sha256,
            "raw_samples": raw_samples,
            "attempt_sequence_path": str(evidence["attempt_sequence_path"]),
            "attempt_sequence_sha256": _sha256_file(
                evidence["attempt_sequence_path"]
            ),
            "adaptive_runtime_spec_path": str(
                evidence["adaptive_runtime_spec_path"]
            ),
            "adaptive_runtime_spec_sha256": _sha256_file(
                evidence["adaptive_runtime_spec_path"]
            ),
            "pilot_spec_path": str(evidence["pilot_spec_path"]),
            "pilot_spec_sha256": _sha256_file(evidence["pilot_spec_path"]),
            "spec_index_path": str(evidence["spec_index_path"]),
            "spec_index_sha256": _sha256_file(evidence["spec_index_path"]),
            "artifact_inventory_path": str(
                evidence["artifact_inventory_path"]
            ),
            "artifact_inventory_sha256": _sha256_file(
                evidence["artifact_inventory_path"]
            ),
            "matrix_path": str(accepted.matrix_path),
            "matrix_sha256": _sha256_file(accepted.matrix_path),
            "artifact_manifest_path": str(accepted.artifact_manifest_path),
            "artifact_manifest_sha256": model["artifact_manifest_sha256"],
            "prompt_set_path": str(accepted.prompt_set_path),
            "prompt_set_sha256": accepted.prompt_contract["prompt_set_sha256"],
            "rubric_path": str(accepted.rubric_path),
            "rubric_sha256": accepted.rubric_sha256,
            "build_provenance_path": str(accepted.build_provenance_path),
            "build_provenance_sha256": build["provenance_sha256"],
            "quality_worker_path": str(quality_worker_path),
            "quality_worker_sha256": _sha256_file(quality_worker_path),
            "build_identity": build_identity,
            "build_identity_sha256": _sha256_json(build_identity),
            "runtime_property": runtime_property,
            "runtime_property_sha256": accepted.runtime_config_sha256,
            "command": command,
            "command_sha256": _sha256_json(command),
        },
    }


def build_quality_prompt_worker_spec(
    campaign: AcceptedQualityCampaign,
    prompt_id: str,
) -> dict[str, Any]:
    """Build one private prompt controller spec bound to accepted evidence."""

    if not isinstance(campaign, AcceptedQualityCampaign):
        raise TypeError("campaign must be AcceptedQualityCampaign")
    checked = _require_prompt_id(prompt_id)
    return _build_prompt_spec(
        campaign,
        checked,
        prompt_root=_prompt_root(campaign, checked),
    )


def _write_fresh(path: Path, raw: bytes) -> None:
    destination = Path(path)
    destination.parent.mkdir(parents=True, exist_ok=True)
    with destination.open("xb") as handle:
        handle.write(raw)
        handle.flush()
        os.fsync(handle.fileno())


def _quality_prompt_job_name(prompt_root: Path) -> str:
    identity = hashlib.sha256(
        str(Path(prompt_root).resolve()).casefold().encode("utf-8")
    ).hexdigest()[:24]
    return f"WB04-adaptive-quality-{identity}"


def _terminal_worker_result(
    spec: Mapping[str, Any],
    *,
    failure_type: str,
    failure_message: str,
) -> dict[str, Any]:
    outcomes: list[dict[str, Any]] = []
    first_output = ""
    for index, turn in enumerate(spec["turns"]):
        raw_prompt = turn["prompt"]
        if spec["prompt_id"] == "P6" and index == 1:
            raw_prompt = (
                f"User: {spec['turns'][0]['prompt'].strip()}\n"
                f"Assistant: {first_output}\n"
                f"User: {turn['prompt'].strip()}"
            )
        outcome = {
            "turn_id": turn["turn_id"],
            "raw_prompt": raw_prompt,
            "raw_prompt_sha256": hashlib.sha256(
                raw_prompt.encode("utf-8")
            ).hexdigest(),
            "status": "failed",
            "raw_output": None,
            "raw_output_sha256": None,
            "failure_type": failure_type,
            "failure_message": failure_message,
        }
        if spec["prompt_id"] == "P6" and index == 1:
            outcome["history_source_sha256"] = hashlib.sha256(b"").hexdigest()
        outcomes.append(outcome)
    unsigned = {
        "schema": PROMPT_RESULT_SCHEMA,
        "prompt_id": spec["prompt_id"],
        "worker_spec_sha256": hashlib.sha256(_canonical_json(spec)).hexdigest(),
        "outcomes": outcomes,
    }
    return {
        **unsigned,
        "worker_result_sha256": hashlib.sha256(
            _canonical_json(unsigned)
        ).hexdigest(),
    }


def _publish_terminal_prompt_evidence(
    *,
    root: Path,
    spec: Mapping[str, Any],
    stage: str,
    error: BaseException,
    cleanup_process_count: int = -1,
    active_pids: list[int] | None = None,
) -> None:
    result_path = root / "worker-result.json"
    log_path = root / "worker.log"
    evidence_path = root / "guard-evidence.json"
    message = f"{type(error).__name__}: {error}"
    if not result_path.exists():
        _write_fresh(
            result_path,
            _canonical_json(
                _terminal_worker_result(
                    spec,
                    failure_type="QualityAdmissionFailure",
                    failure_message=message,
                )
            ),
        )
    if not log_path.exists():
        _write_fresh(log_path, b"")
    if not evidence_path.exists():
        unsigned = {
            "schema": TERMINAL_GUARD_SCHEMA,
            "status": "quality-blocked",
            "failure_stage": stage,
            "failure_type": type(error).__name__,
            "failure_message": str(error),
            "worker_spec_sha256": hashlib.sha256(
                _canonical_json(spec)
            ).hexdigest(),
            "worker_result_sha256": _sha256_file(result_path),
            "worker_log_sha256": _sha256_file(log_path),
            "cleanup_process_count": cleanup_process_count,
            "active_pids": active_pids,
        }
        evidence = {
            **unsigned,
            "terminal_guard_sha256": hashlib.sha256(
                _canonical_json(unsigned)
            ).hexdigest(),
        }
        _write_fresh(evidence_path, _canonical_json(evidence))


def _validate_worker_result(
    raw: bytes,
    *,
    spec: Mapping[str, Any],
    source: Path,
) -> tuple[dict[str, Any], str]:
    value = _strict_object(raw, source=source)
    if raw != _canonical_json(value):
        raise ValueError("quality prompt worker result bytes are not canonical")
    if set(value) != {
        "schema",
        "prompt_id",
        "worker_spec_sha256",
        "outcomes",
        "worker_result_sha256",
    }:
        raise ValueError("quality prompt worker result fields are invalid")
    if (
        value.get("schema") != PROMPT_RESULT_SCHEMA
        or value.get("prompt_id") != spec["prompt_id"]
        or value.get("worker_spec_sha256")
        != hashlib.sha256(_canonical_json(spec)).hexdigest()
    ):
        raise ValueError("quality prompt worker result identity is invalid")
    outcomes = value.get("outcomes")
    turns = spec["turns"]
    if not isinstance(outcomes, list) or len(outcomes) != len(turns):
        raise ValueError("quality prompt worker outcomes are invalid")
    status = "passed"
    for outcome, turn in zip(outcomes, turns, strict=True):
        outcome_fields = {
            "turn_id",
            "raw_prompt",
            "raw_prompt_sha256",
            "status",
            "raw_output",
            "raw_output_sha256",
            "failure_type",
            "failure_message",
        }
        if spec["prompt_id"] == "P6" and turn["turn_id"] == "P6-turn-2":
            outcome_fields.add("history_source_sha256")
        if (
            not isinstance(outcome, dict)
            or set(outcome) != outcome_fields
            or outcome.get("turn_id") != turn["turn_id"]
        ):
            raise ValueError("quality prompt worker outcomes are not ordered")
        raw_prompt = outcome.get("raw_prompt")
        if not isinstance(raw_prompt, str) or not raw_prompt.strip():
            raise ValueError("quality prompt worker raw prompt is invalid")
        if outcome.get("raw_prompt_sha256") != hashlib.sha256(
            raw_prompt.encode("utf-8")
        ).hexdigest():
            raise ValueError("quality prompt worker raw prompt hash is invalid")
        outcome_status = outcome.get("status")
        if outcome_status == "complete":
            output = outcome.get("raw_output")
            if (
                not isinstance(output, str)
                or outcome.get("raw_output_sha256")
                != hashlib.sha256(output.encode("utf-8")).hexdigest()
                or outcome.get("failure_type") is not None
                or outcome.get("failure_message") is not None
            ):
                raise ValueError("quality prompt worker output is invalid")
        elif outcome_status == "failed":
            status = "failed"
            if (
                outcome.get("raw_output") is not None
                or outcome.get("raw_output_sha256") is not None
                or not isinstance(outcome.get("failure_type"), str)
                or not isinstance(outcome.get("failure_message"), str)
            ):
                raise ValueError("quality prompt worker failure is invalid")
        else:
            raise ValueError("quality prompt worker outcome status is invalid")
    if spec["prompt_id"] != "P6":
        if outcomes[0]["raw_prompt"] != turns[0]["prompt"]:
            raise ValueError("quality prompt worker executed a different prompt")
    else:
        first_output = outcomes[0]["raw_output"] or ""
        expected_second_prompt = (
            f"User: {turns[0]['prompt'].strip()}\n"
            f"Assistant: {first_output}\n"
            f"User: {turns[1]['prompt'].strip()}"
        )
        if (
            outcomes[0]["raw_prompt"] != turns[0]["prompt"]
            or outcomes[1]["raw_prompt"] != expected_second_prompt
            or outcomes[1].get("history_source_sha256")
            != hashlib.sha256(first_output.encode("utf-8")).hexdigest()
        ):
            raise ValueError("quality prompt worker P6 history is invalid")
    unsigned = {
        key: item for key, item in value.items() if key != "worker_result_sha256"
    }
    if value.get("worker_result_sha256") != hashlib.sha256(
        _canonical_json(unsigned)
    ).hexdigest():
        raise ValueError("quality prompt worker result hash is invalid")
    serialized = json.dumps(value, sort_keys=True, separators=(",", ":"))
    for private_label in ('"test_id"', '"context_tokens"', '"properties"'):
        if private_label in serialized:
            raise ValueError("blind prompt response contains private controller labels")
    return value, status


def _validate_guard(
    evidence: Mapping[str, Any],
    *,
    command: list[str],
    campaign: AcceptedQualityCampaign,
    prompt_root: Path,
    timeout_seconds: float,
    spec_sha256: str,
    log_sha256: str,
) -> tuple[str, int]:
    _, environment_sha256 = _effective_environment(campaign.worker_environment)
    spec_path = prompt_root / "worker-spec.json"
    expected = {
        "schema": GUARD_SCHEMA,
        "command": command,
        "working_directory": str(campaign.repo_root),
        "log_path": str((prompt_root / "worker.log").resolve()),
        "evidence_path": str((prompt_root / "guard-evidence.json").resolve()),
        "environment_sha256": environment_sha256,
        "bound_inputs": [
            {
                "name": "quality_worker_spec",
                "path": str(spec_path.resolve()),
                "sha256": spec_sha256,
            }
        ],
        "configured_minimum_available_ram_bytes": EMERGENCY_FLOOR_BYTES,
        "maximum_runtime_seconds": float(timeout_seconds),
    }
    for field, wanted in expected.items():
        if type(evidence.get(field)) is not type(wanted) or evidence.get(field) != wanted:
            raise ValueError(f"quality prompt guard {field} is invalid")
    root_pid = evidence.get("root_pid")
    if isinstance(root_pid, bool) or not isinstance(root_pid, int) or root_pid <= 0:
        raise ValueError("quality prompt guard process identity is invalid")
    if not isinstance(evidence.get("run_id"), str) or not evidence["run_id"].strip():
        raise ValueError("quality prompt guard run identity is invalid")
    cleanup = evidence.get("cleanup_process_count")
    job = evidence.get("job_object")
    containing = evidence.get("containing_job_assignment")
    containing_proven = (
        isinstance(containing, Mapping)
        and set(containing)
        == {
            "requested",
            "assigned_before_fine_job",
            "assigned_pid",
            "query_ok_after_cleanup",
            "active_pids_after_cleanup",
        }
        and containing.get("requested") is True
        and containing.get("assigned_before_fine_job") is True
        and containing.get("assigned_pid") == root_pid
        and containing.get("query_ok_after_cleanup") is True
        and containing.get("active_pids_after_cleanup") == []
    )
    cleanup_proven = (
        cleanup == 0
        and containing_proven
        and isinstance(job, Mapping)
        and job.get("setup_ok") is True
        and job.get("query_ok") is True
        and job.get("queried_active_process_count_after_cleanup") == 0
        and job.get("survivor_pids_after_cleanup") == []
        and evidence.get("emergency_actions") == []
    )
    passed = (
        cleanup_proven
        and evidence.get("valid") is True
        and evidence.get("exit_code") == 0
        and evidence.get("timed_out") is False
        and evidence.get("low_memory_stop") is False
        and evidence.get("log_sha256") == log_sha256
    )
    return ("passed" if passed else "failed"), (0 if cleanup_proven else -1)


def _validate_terminal_guard(
    evidence: Mapping[str, Any],
    *,
    spec_sha256: str,
    result_sha256: str,
    log_sha256: str,
) -> int:
    fields = {
        "schema",
        "status",
        "failure_stage",
        "failure_type",
        "failure_message",
        "worker_spec_sha256",
        "worker_result_sha256",
        "worker_log_sha256",
        "cleanup_process_count",
        "active_pids",
        "terminal_guard_sha256",
    }
    if set(evidence) != fields:
        raise ValueError("quality prompt terminal guard fields are invalid")
    if (
        evidence.get("schema") != TERMINAL_GUARD_SCHEMA
        or evidence.get("status") != "quality-blocked"
        or not isinstance(evidence.get("failure_stage"), str)
        or not evidence["failure_stage"].strip()
        or not isinstance(evidence.get("failure_type"), str)
        or not evidence["failure_type"].strip()
        or not isinstance(evidence.get("failure_message"), str)
        or evidence.get("worker_spec_sha256") != spec_sha256
        or evidence.get("worker_result_sha256") != result_sha256
        or evidence.get("worker_log_sha256") != log_sha256
    ):
        raise ValueError("quality prompt terminal guard identity is invalid")
    cleanup = evidence.get("cleanup_process_count")
    active_pids = evidence.get("active_pids")
    if cleanup not in {0, -1} or (
        active_pids is not None
        and (
            not isinstance(active_pids, list)
            or any(
                isinstance(pid, bool) or not isinstance(pid, int) or pid <= 0
                for pid in active_pids
            )
            or active_pids != sorted(set(active_pids))
        )
    ):
        raise ValueError("quality prompt terminal cleanup evidence is invalid")
    if cleanup == 0 and active_pids != []:
        raise ValueError("quality prompt terminal zero-survivor proof is invalid")
    unsigned = {
        key: item for key, item in evidence.items() if key != "terminal_guard_sha256"
    }
    if evidence.get("terminal_guard_sha256") != hashlib.sha256(
        _canonical_json(unsigned)
    ).hexdigest():
        raise ValueError("quality prompt terminal guard hash is invalid")
    return cleanup


def _load_prompt_execution(
    campaign: AcceptedQualityCampaign,
    prompt_id: str,
    prompt_root: Path,
    timeout_seconds: float,
    *,
    evidence_paths: Mapping[str, Any] | None = None,
) -> GovernedQualityPromptResult:
    root = Path(prompt_root).resolve()
    expected_names = {
        "worker-spec.json",
        "worker-result.json",
        "worker.log",
        "guard-evidence.json",
    }
    children = list(root.iterdir())
    if (
        {child.name for child in children} != expected_names
        or any(child.is_symlink() or not child.is_file() for child in children)
    ):
        raise ValueError("quality prompt evidence is incomplete or unexpected")
    spec = _build_prompt_spec(
        campaign,
        prompt_id,
        prompt_root=root,
        evidence_paths=evidence_paths,
    )
    spec_raw = (root / "worker-spec.json").read_bytes()
    if spec_raw != _canonical_json(spec):
        raise ValueError("quality prompt worker spec does not match accepted campaign")
    spec_sha256 = hashlib.sha256(spec_raw).hexdigest()
    result_raw = (root / "worker-result.json").read_bytes()
    _result, worker_status = _validate_worker_result(
        result_raw, spec=spec, source=root / "worker-result.json"
    )
    log_sha256 = _sha256_file(root / "worker.log")
    evidence_raw = (root / "guard-evidence.json").read_bytes()
    evidence = _strict_object(evidence_raw, source=root / "guard-evidence.json")
    command = [
        str(campaign.python_executable),
        "-m",
        "scripts.testing.official_openvino.quality_worker",
        "--spec",
        str((root / "worker-spec.json").resolve()),
        "--result",
        str((root / "worker-result.json").resolve()),
    ]
    if evidence.get("schema") == TERMINAL_GUARD_SCHEMA:
        if evidence_raw != _canonical_json(evidence):
            raise ValueError("quality prompt terminal guard bytes are not canonical")
        cleanup = _validate_terminal_guard(
            evidence,
            spec_sha256=spec_sha256,
            result_sha256=hashlib.sha256(result_raw).hexdigest(),
            log_sha256=log_sha256,
        )
        guard_status = "failed"
    else:
        if evidence_raw != _canonical_identity_bytes(evidence):
            raise ValueError("quality prompt guard evidence bytes are not canonical")
        guard_status, cleanup = _validate_guard(
            evidence,
            command=command,
            campaign=campaign,
            prompt_root=root,
            timeout_seconds=timeout_seconds,
            spec_sha256=spec_sha256,
            log_sha256=log_sha256,
        )
    return GovernedQualityPromptResult(
        prompt_id=prompt_id,
        status="passed" if guard_status == worker_status == "passed" else "failed",
        prompt_root=root,
        worker_spec_sha256=spec_sha256,
        worker_result_sha256=hashlib.sha256(result_raw).hexdigest(),
        guard_evidence_sha256=hashlib.sha256(evidence_raw).hexdigest(),
        cleanup_process_count=cleanup,
    )


def _run_governed_quality_prompt(
    campaign: AcceptedQualityCampaign,
    prompt_id: str,
    output_root: Path,
    timeout_seconds: float,
    *,
    run_command: Callable[..., Mapping[str, Any]] = run_guarded_command,
    evidence_paths: Mapping[str, Any] | None = None,
) -> GovernedQualityPromptResult:
    """Launch one fresh prompt worker and reopen every persisted byte."""

    if not isinstance(campaign, AcceptedQualityCampaign):
        raise TypeError("campaign must be AcceptedQualityCampaign")
    checked = _require_prompt_id(prompt_id)
    if (
        isinstance(timeout_seconds, bool)
        or not isinstance(timeout_seconds, (int, float))
        or not math.isfinite(timeout_seconds)
        or timeout_seconds <= 0
    ):
        raise ValueError("quality prompt timeout must be finite and positive")
    accepted = load_accepted_quality_campaign(
        _accepted_input_from_campaign(campaign)
    )
    root = Path(output_root).resolve()
    root.mkdir(parents=True, exist_ok=False)
    spec = _build_prompt_spec(
        accepted,
        checked,
        prompt_root=root,
        evidence_paths=evidence_paths,
    )
    spec_path = root / "worker-spec.json"
    result_path = root / "worker-result.json"
    log_path = root / "worker.log"
    evidence_path = root / "guard-evidence.json"
    _write_fresh(spec_path, _canonical_json(spec))
    command = [
        str(accepted.python_executable),
        "-m",
        "scripts.testing.official_openvino.quality_worker",
        "--spec",
        str(spec_path),
        "--result",
        str(result_path),
    ]
    containing_job: KillOnCloseJob | None = None
    active_pids: list[int] | None = None
    cleanup_process_count = -1
    stage = "containing-job-creation"
    try:
        containing_job = KillOnCloseJob(_quality_prompt_job_name(root))
        if type(containing_job) is not KillOnCloseJob:
            raise RuntimeError("quality prompt containing Job type is invalid")
        stage = "containing-job-survivor-query"
        active_pids = KillOnCloseJob.active_pids(containing_job)
        if active_pids:
            raise RuntimeError(
                "quality prompt containing Job has pre-launch survivors"
            )
        cleanup_process_count = 0
        stage = "available-ram-admission"
        available = available_ram_bytes()
        if not isinstance(available, int) or isinstance(available, bool):
            raise RuntimeError("quality prompt available RAM query failed")
        if available < LAUNCH_FLOOR_BYTES:
            raise RuntimeError(
                "quality prompt launch requires at least 4096 MiB RAM"
            )
        stage = "guarded-worker-launch"
        returned = run_command(
            command=command,
            cwd=accepted.repo_root,
            log_path=log_path,
            evidence_path=evidence_path,
            expected_exit="zero",
            limits=GuardLimits(
                minimum_available_ram_bytes=EMERGENCY_FLOOR_BYTES,
                maximum_runtime_seconds=float(timeout_seconds),
            ),
            environment=dict(accepted.worker_environment),
            bound_inputs={"quality_worker_spec": spec_path},
            _containing_job=containing_job,
        )
        stage = "guard-evidence-reopen"
        if not isinstance(returned, Mapping):
            raise RuntimeError("quality prompt guard returned invalid evidence")
        persisted = _strict_object(evidence_path.read_bytes(), source=evidence_path)
        if dict(returned) != persisted:
            raise RuntimeError("quality prompt guard return does not match evidence")
        return _load_prompt_execution(
            accepted,
            checked,
            root,
            float(timeout_seconds),
            evidence_paths=evidence_paths,
        )
    except Exception as error:
        if containing_job is not None:
            try:
                active_pids = sorted(KillOnCloseJob.active_pids(containing_job))
                cleanup_process_count = 0 if active_pids == [] else -1
            except (OSError, RuntimeError):
                active_pids = None
                cleanup_process_count = -1
        _publish_terminal_prompt_evidence(
            root=root,
            spec=spec,
            stage=stage,
            error=error,
            cleanup_process_count=cleanup_process_count,
            active_pids=active_pids,
        )
        return _load_prompt_execution(
            accepted,
            checked,
            root,
            float(timeout_seconds),
            evidence_paths=evidence_paths,
        )
    finally:
        if containing_job is not None:
            containing_job.close()


def run_governed_quality_prompt(
    campaign: AcceptedQualityCampaign,
    prompt_id: str,
    output_root: Path,
    timeout_seconds: float,
    *,
    run_command: Callable[..., Mapping[str, Any]] = run_guarded_command,
) -> GovernedQualityPromptResult:
    """Launch one fresh prompt worker using internally derived bindings."""

    return _run_governed_quality_prompt(
        campaign,
        prompt_id,
        output_root,
        timeout_seconds,
        run_command=run_command,
    )


def _receipt(result: GovernedQualityPromptResult | None, prompt_id: str, root: Path) -> dict[str, Any]:
    prompt_root = result.prompt_root if result is not None else root / prompt_id
    process_identity = None
    if result is not None:
        evidence = _strict_object(
            (prompt_root / "guard-evidence.json").read_bytes(),
            source=prompt_root / "guard-evidence.json",
        )
        process_identity = evidence.get("run_id")
    return {
        "prompt_id": prompt_id,
        "status": result.status if result is not None else "not-run",
        "prompt_root": str(prompt_root.resolve()),
        "worker_spec_path": str((prompt_root / "worker-spec.json").resolve()),
        "worker_spec_sha256": result.worker_spec_sha256 if result else None,
        "worker_result_path": str((prompt_root / "worker-result.json").resolve()),
        "worker_result_sha256": result.worker_result_sha256 if result else None,
        "worker_log_path": str((prompt_root / "worker.log").resolve()),
        "worker_log_sha256": (
            _sha256_file(prompt_root / "worker.log") if result else None
        ),
        "guard_evidence_path": str((prompt_root / "guard-evidence.json").resolve()),
        "guard_evidence_sha256": result.guard_evidence_sha256 if result else None,
        "cleanup_process_count": result.cleanup_process_count if result else None,
        "process_identity": process_identity,
    }


def _summary(
    campaign: AcceptedQualityCampaign,
    results: Mapping[str, GovernedQualityPromptResult],
    *,
    summary_path: Path,
    previous_summary_path: Path | None = None,
) -> dict[str, Any]:
    completed = [
        prompt_id
        for prompt_id in PROMPT_IDS
        if prompt_id in results and results[prompt_id].status == "passed"
    ]
    receipts = [
        _receipt(results.get(prompt_id), prompt_id, campaign.output_root)
        for prompt_id in PROMPT_IDS
    ]
    unsigned = {
        "schema": CAPTURE_SCHEMA,
        "status": "passed" if len(completed) == 6 else "quality-blocked",
        "completed_prompt_ids": completed,
        "prompt_receipt_count": 6,
        "prompt_receipts": receipts,
        "capture_summary_path": str(Path(summary_path).resolve()),
        "previous_capture_summary_path": (
            str(Path(previous_summary_path).resolve())
            if previous_summary_path is not None
            else None
        ),
        "previous_capture_summary_sha256": (
            _sha256_file(previous_summary_path)
            if previous_summary_path is not None
            else None
        ),
        "campaign_identity_sha256": campaign.campaign_identity_sha256,
        "runtime_summary_sha256": campaign.measurement_summary_sha256,
        "matrix_sha256": _sha256_file(campaign.matrix_path),
        "artifact_manifest_sha256": campaign.identity["identity"]["model"][
            "artifact_manifest_sha256"
        ],
        "prompt_set_sha256": campaign.prompt_contract["prompt_set_sha256"],
        "rubric_sha256": campaign.rubric_sha256,
        "runtime_property_sha256": campaign.runtime_config_sha256,
    }
    return {
        **unsigned,
        "capture_summary_sha256": hashlib.sha256(
            _canonical_json(unsigned)
        ).hexdigest(),
    }


def _validate_summary(
    campaign: AcceptedQualityCampaign,
    path: Path,
    *,
    evidence_paths: Mapping[str, Any] | None = None,
    previous_summary_path: Path | None = None,
) -> tuple[dict[str, Any], dict[str, GovernedQualityPromptResult]]:
    raw = Path(path).read_bytes()
    value = _strict_object(raw, source=path)
    if raw != _canonical_json(value):
        raise ValueError("adaptive quality capture summary is not canonical")
    receipts = value.get("prompt_receipts")
    if (
        value.get("schema") != CAPTURE_SCHEMA
        or value.get("prompt_receipt_count") != 6
        or not isinstance(receipts, list)
        or [item.get("prompt_id") for item in receipts if isinstance(item, Mapping)]
        != list(PROMPT_IDS)
    ):
        raise ValueError("adaptive quality capture summary is invalid")
    unsigned = {
        key: item for key, item in value.items() if key != "capture_summary_sha256"
    }
    if value.get("capture_summary_sha256") != hashlib.sha256(
        _canonical_json(unsigned)
    ).hexdigest():
        raise ValueError("adaptive quality capture summary hash is invalid")
    loaded: dict[str, GovernedQualityPromptResult] = {}
    process_identities: list[str] = []
    seen_absent = False
    for receipt in receipts:
        prompt_id = receipt["prompt_id"]
        if receipt.get("status") == "not-run":
            seen_absent = True
            continue
        if seen_absent:
            raise ValueError("adaptive quality prompt evidence has a gap")
        prompt_root = Path(str(receipt.get("prompt_root"))).resolve()
        if (
            prompt_root.parent != Path(campaign.output_root).resolve()
            or prompt_root.name
            not in {prompt_id, f"{prompt_id}-recovery-001"}
        ):
            raise ValueError("adaptive quality prompt path is invalid")
        result = _load_prompt_execution(
            campaign,
            prompt_id,
            prompt_root,
            campaign.timeout_seconds,
            evidence_paths=evidence_paths,
        )
        if _receipt(result, prompt_id, campaign.output_root) != receipt:
            raise ValueError("adaptive quality prompt receipt does not match evidence")
        process_identities.append(receipt["process_identity"])
        loaded[prompt_id] = result
        if result.status != "passed":
            seen_absent = True
    if len(process_identities) != len(set(process_identities)):
        raise ValueError("adaptive quality workers did not use fresh processes")
    expected = _summary(
        campaign,
        loaded,
        summary_path=path,
        previous_summary_path=previous_summary_path,
    )
    if value != expected:
        raise ValueError("adaptive quality capture summary does not match evidence")
    return value, loaded


def _latest_summary(root: Path) -> Path | None:
    primary = root / "capture-summary.json"
    recovery = sorted(root.glob("capture-summary-recovery-*.json"))
    return recovery[-1] if recovery else (primary if primary.is_file() else None)


def _summary_history(root: Path) -> list[Path]:
    primary = root / "capture-summary.json"
    recovery = sorted(root.glob("capture-summary-recovery-*.json"))
    return ([primary] if primary.is_file() else []) + recovery


def _validate_summary_transition(
    previous: Mapping[str, Any],
    current: Mapping[str, Any],
) -> None:
    previous_receipts = previous["prompt_receipts"]
    current_receipts = current["prompt_receipts"]
    changed = False
    for prompt_id, old, new in zip(
        PROMPT_IDS, previous_receipts, current_receipts, strict=True
    ):
        if old == new:
            continue
        changed = True
        old_status = old.get("status")
        new_root = Path(str(new.get("prompt_root"))).resolve()
        if old_status == "passed":
            raise ValueError("adaptive quality history replaced passed evidence")
        if old_status == "failed":
            old_root = Path(str(old.get("prompt_root"))).resolve()
            if (
                old_root.name != prompt_id
                or new_root.name != f"{prompt_id}-recovery-001"
                or new.get("status") not in {"passed", "failed"}
            ):
                raise ValueError("adaptive quality history recovery is invalid")
        elif old_status == "not-run":
            if (
                new_root.name != prompt_id
                or new.get("status") not in {"passed", "failed"}
            ):
                raise ValueError("adaptive quality history append is invalid")
        else:
            raise ValueError("adaptive quality history status is invalid")
    if not changed:
        raise ValueError("adaptive quality recovery summary adds no evidence")


def _validate_summary_history(
    campaign: AcceptedQualityCampaign,
    root: Path,
    *,
    evidence_paths: Mapping[str, Any] | None,
) -> tuple[
    Path | None,
    dict[str, Any] | None,
    dict[str, GovernedQualityPromptResult],
]:
    paths = _summary_history(root)
    previous_path: Path | None = None
    previous_value: dict[str, Any] | None = None
    latest_results: dict[str, GovernedQualityPromptResult] = {}
    process_roots: dict[str, Path] = {}
    for path in paths:
        value, loaded = _validate_summary(
            campaign,
            path,
            evidence_paths=evidence_paths,
            previous_summary_path=previous_path,
        )
        if previous_value is not None:
            _validate_summary_transition(previous_value, value)
        for receipt in value["prompt_receipts"]:
            identity = receipt.get("process_identity")
            if not isinstance(identity, str):
                continue
            prompt_root = Path(str(receipt["prompt_root"])).resolve()
            earlier = process_roots.setdefault(identity, prompt_root)
            if earlier != prompt_root:
                raise ValueError(
                    "adaptive quality history reused a worker process identity"
                )
        previous_path = path
        previous_value = value
        latest_results = loaded
    return previous_path, previous_value, latest_results


def _validate_root_entries(root: Path) -> None:
    allowed_directories = {
        *PROMPT_IDS,
        *(f"{prompt_id}-recovery-001" for prompt_id in PROMPT_IDS),
    }
    recovery_numbers: list[int] = []
    for child in root.iterdir():
        if child.is_symlink():
            raise ValueError("adaptive quality root contains an alias")
        if child.is_dir() and child.name in allowed_directories:
            continue
        if child.is_file() and child.name == "capture-summary.json":
            continue
        if child.is_file() and child.name.startswith("capture-summary-recovery-"):
            suffix = child.name.removeprefix("capture-summary-recovery-").removesuffix(
                ".json"
            )
            if len(suffix) == 3 and suffix.isdigit():
                recovery_numbers.append(int(suffix))
                continue
        raise ValueError("adaptive quality root contains unexpected evidence")
    if sorted(recovery_numbers) != list(range(1, len(recovery_numbers) + 1)):
        raise ValueError("adaptive quality recovery summary numbering has a gap")
    if recovery_numbers and not (root / "capture-summary.json").is_file():
        raise ValueError("adaptive quality recovery has no original summary")


def quality_campaign_input_from_recovery(
    recovery: Mapping[str, Any],
) -> QualityCampaignInput:
    """Reopen a complete Task-4 recovery object without path discovery."""

    required = {
        "schema",
        "test_id",
        "context_tokens",
        "runtime_evidence_path",
        "runtime_evidence_sha256",
        "runtime_summary",
        "runtime_summary_sha256",
        "adaptive_runtime_spec_path",
        "adaptive_runtime_spec_sha256",
        "pilot_spec_path",
        "pilot_spec_sha256",
        "spec_index_path",
        "spec_index_sha256",
        "artifact_inventory_path",
        "artifact_inventory_sha256",
        "matrix",
        "matrix_sha256",
        "artifact_manifest_path",
        "artifact_manifest_sha256",
        "prompt_set",
        "prompt_set_sha256",
        "rubric",
        "rubric_sha256",
        "model_path",
        "build_root",
        "build_provenance_path",
        "build_provenance_sha256",
        "python_executable",
        "python_site_packages",
        "openvino_libraries",
        "sampler_script",
        "sampler_script_sha256",
        "output_root",
        "timeout_seconds",
        "quality_recovery_sha256",
    }
    if (
        not isinstance(recovery, Mapping)
        or set(recovery) != required
        or recovery.get("schema") != QUALITY_RECOVERY_SCHEMA
    ):
        raise ValueError("quality recovery arguments are incomplete or unexpected")
    unsigned_recovery = {
        key: item
        for key, item in recovery.items()
        if key != "quality_recovery_sha256"
    }
    if recovery.get("quality_recovery_sha256") != _sha256_json(
        unsigned_recovery
    ):
        raise ValueError("quality recovery self-hash is invalid")

    def bound_file(path_field: str, hash_field: str) -> Path:
        path = Path(str(recovery[path_field])).resolve()
        if not path.is_file() or _sha256_file(path) != recovery[hash_field]:
            raise ValueError(f"quality recovery {path_field} hash drift")
        return path

    runtime_evidence = bound_file(
        "runtime_evidence_path", "runtime_evidence_sha256"
    )
    runtime_summary = bound_file("runtime_summary", "runtime_summary_sha256")
    adaptive_runtime_spec = bound_file(
        "adaptive_runtime_spec_path", "adaptive_runtime_spec_sha256"
    )
    pilot_spec = bound_file("pilot_spec_path", "pilot_spec_sha256")
    spec_index = bound_file("spec_index_path", "spec_index_sha256")
    artifact_inventory = bound_file(
        "artifact_inventory_path", "artifact_inventory_sha256"
    )
    matrix = bound_file("matrix", "matrix_sha256")
    bound_artifact_manifest = bound_file(
        "artifact_manifest_path", "artifact_manifest_sha256"
    )
    prompt_set = bound_file("prompt_set", "prompt_set_sha256")
    rubric = bound_file("rubric", "rubric_sha256")
    bound_build_provenance = bound_file(
        "build_provenance_path", "build_provenance_sha256"
    )
    sampler = bound_file("sampler_script", "sampler_script_sha256")
    campaign_root = runtime_summary.parent
    if runtime_evidence != campaign_root / "attempt-sequence.json":
        raise ValueError("quality recovery runtime evidence is not the accepted sequence")
    sequence = _strict_object(runtime_evidence.read_bytes(), source=runtime_evidence)
    pilot = sequence.get("pilot")
    if not isinstance(pilot, Mapping) or not isinstance(pilot.get("spec_path"), str):
        raise ValueError("quality recovery pilot spec binding is missing")
    spec_path = (campaign_root / pilot["spec_path"]).resolve()
    if (
        spec_path != pilot_spec
        or not spec_path.is_file()
        or _sha256_file(spec_path) != pilot.get("spec_file_sha256")
    ):
        raise ValueError("quality recovery pilot spec hash drift")
    index = _strict_object(spec_index.read_bytes(), source=spec_index)
    entries = index.get("runtime_specs")
    matches = (
        [
            entry
            for entry in entries
            if isinstance(entry, Mapping)
            and entry.get("test_id") == recovery["test_id"]
            and entry.get("context_tokens") == recovery["context_tokens"]
        ]
        if isinstance(entries, list)
        else []
    )
    if (
        index.get("schema") != SPEC_INDEX_SCHEMA
        or index.get("matrix_sha256") != recovery["matrix_sha256"]
        or Path(str(index.get("artifact_inventory_path"))).resolve()
        != artifact_inventory
        or index.get("artifact_inventory_sha256")
        != recovery["artifact_inventory_sha256"]
        or len(matches) != 1
        or (spec_index.parent / str(matches[0].get("path"))).resolve()
        != adaptive_runtime_spec
        or matches[0].get("sha256")
        != recovery["adaptive_runtime_spec_sha256"]
    ):
        raise ValueError("quality recovery spec-index row binding is invalid")
    adaptive_spec = _strict_object(
        adaptive_runtime_spec.read_bytes(), source=adaptive_runtime_spec
    )
    if (
        adaptive_spec.get("controlled_test_id") != recovery["test_id"]
        or adaptive_spec.get("context_tokens") != recovery["context_tokens"]
        or Path(str(adaptive_spec.get("artifact_manifest_path"))).resolve()
        != bound_artifact_manifest
        or adaptive_spec.get("artifact_manifest_sha256")
        != recovery["artifact_manifest_sha256"]
        or Path(str(adaptive_spec.get("model_path"))).resolve()
        != Path(str(recovery["model_path"])).resolve()
    ):
        raise ValueError("quality recovery adaptive runtime spec binding is invalid")
    identity_path = campaign_root / "campaign-identity.json"
    identity = _strict_object(identity_path.read_bytes(), source=identity_path)
    native = identity.get("identity")
    if not isinstance(native, Mapping):
        raise ValueError("quality recovery native campaign identity is invalid")
    model = native.get("model")
    build = native.get("build")
    runtime = native.get("runtime")
    if not all(isinstance(item, Mapping) for item in (model, build, runtime)):
        raise ValueError("quality recovery native identity sections are missing")
    artifact_manifest = Path(str(model["artifact_manifest_path"])).resolve()
    build_provenance = Path(str(build["provenance_path"])).resolve()
    repo_root = Path(str(runtime["repository_root"])).resolve()
    expected_paths = {
        "model_path": Path(
            str(model["validated_artifact"]["artifact_root"])
        ).resolve(),
        "build_root": Path(str(build["root"])).resolve(),
        "python_executable": Path(str(runtime["python_executable"]["path"])).resolve(),
        "python_site_packages": Path(
            str(runtime["python_openvino_package"]["path"])
        ).resolve().parent,
        "openvino_libraries": Path(
            str(runtime["openvino_libraries"]["path"])
        ).resolve(),
    }
    for field, expected in expected_paths.items():
        if Path(str(recovery[field])).resolve() != expected:
            raise ValueError(f"quality recovery {field} differs from runtime identity")
    if (
        artifact_manifest != bound_artifact_manifest
        or model.get("artifact_manifest_sha256")
        != recovery["artifact_manifest_sha256"]
        or build_provenance != bound_build_provenance
        or build.get("provenance_sha256")
        != recovery["build_provenance_sha256"]
    ):
        raise ValueError("quality recovery artifact or build identity drift")
    expected_prompt_set = (
        Path(__file__).resolve().parents[3]
        / "experiments"
        / "granite_turboquant_intel"
        / "prompts"
        / "fixed-feasibility-prompt-set-v1.json"
    ).resolve()
    expected_rubric = (
        Path(__file__).resolve().parents[3]
        / "experiments"
        / "granite_turboquant_intel"
        / "rubrics"
        / "quality-rubric-v1.json"
    ).resolve()
    if prompt_set != expected_prompt_set or rubric != expected_rubric:
        raise ValueError("quality recovery prompt or rubric substitution")
    if (
        type(recovery.get("timeout_seconds")) is not float
        or recovery["timeout_seconds"] != 1800.0
    ):
        raise ValueError("quality recovery timeout is not frozen")
    summary = _strict_object(runtime_summary.read_bytes(), source=runtime_summary)
    if (
        summary.get("test_id") != recovery["test_id"]
        or summary.get("context_tokens") != recovery["context_tokens"]
        or summary.get("cleanup_process_count") != 0
        or not isinstance(summary.get("activation"), Mapping)
        or summary["activation"].get("fallback") is not False
    ):
        raise ValueError("quality recovery runtime summary is not a passed row")
    sources = summary.get("sources")
    if not isinstance(sources, list) or len(sources) != 3:
        raise ValueError("quality recovery runtime raw samples are incomplete")
    for source in sources:
        if not isinstance(source, Mapping):
            raise ValueError("quality recovery runtime raw sample is invalid")
        raw_path = Path(str(source.get("path"))).resolve()
        if not raw_path.is_file() or _sha256_file(raw_path) != source.get("sha256"):
            raise ValueError("quality recovery runtime raw sample hash drift")
    return AdaptiveQualityCampaignInput(
        campaign_root=campaign_root,
        spec_path=spec_path,
        matrix_path=matrix,
        artifact_manifest_path=artifact_manifest,
        build_provenance_path=build_provenance,
        build_root=expected_paths["build_root"],
        repo_root=repo_root,
        python_executable=expected_paths["python_executable"],
        python_site_packages=expected_paths["python_site_packages"],
        openvino_libraries=expected_paths["openvino_libraries"],
        sampler_script=sampler,
        prompt_set_path=prompt_set,
        rendered_root=prompt_set.parent / "rendered",
        rubric_path=rubric,
        output_root=Path(str(recovery["output_root"])).resolve(),
        timeout_seconds=float(recovery["timeout_seconds"]),
        attempt_sequence_path=runtime_evidence,
        adaptive_runtime_spec_path=adaptive_runtime_spec,
        pilot_spec_path=pilot_spec,
        spec_index_path=spec_index,
        artifact_inventory_path=artifact_inventory,
    )


def capture_isolated_quality_campaign(
    campaign_input: QualityCampaignInput | Mapping[str, Any],
    *,
    resume: bool,
    run_command: Callable[..., Mapping[str, Any]] = run_guarded_command,
) -> dict[str, Any]:
    """Capture P1-P6 in six processes, preserving every prior artifact."""

    if type(resume) is not bool:
        raise TypeError("resume must be a boolean")
    if isinstance(campaign_input, Mapping):
        campaign_input = quality_campaign_input_from_recovery(campaign_input)
    if not isinstance(campaign_input, QualityCampaignInput):
        raise TypeError("campaign_input must be QualityCampaignInput or recovery mapping")
    evidence_paths = _input_evidence_paths(campaign_input)
    accepted = load_accepted_quality_campaign(campaign_input)
    root = Path(accepted.output_root).resolve()
    results: dict[str, GovernedQualityPromptResult] = {}
    latest_path: Path | None = None
    latest_value: dict[str, Any] | None = None
    reconciled_append = False
    if resume:
        if not root.is_dir():
            raise ValueError("adaptive quality resume root is missing")
        _validate_root_entries(root)
        latest_path, latest_value, results = _validate_summary_history(
            accepted, root, evidence_paths=evidence_paths
        )

        def require_no_later_prompt_artifacts(index: int) -> None:
            for later in PROMPT_IDS[index + 1 :]:
                if (root / later).exists() or (
                    root / f"{later}-recovery-001"
                ).exists():
                    raise ValueError(
                        "adaptive quality prompt evidence has a gap"
                    )

        # Reconcile every consecutive append that survived a controller
        # interruption. This deliberately continues through P5 and P6.
        for index, prompt_id in enumerate(PROMPT_IDS):
            current = results.get(prompt_id)
            primary_root = root / prompt_id
            recovery_root = root / f"{prompt_id}-recovery-001"
            primary_exists = primary_root.is_dir()
            recovery_exists = recovery_root.is_dir()
            if current is not None:
                if current.prompt_root == primary_root:
                    if current.status == "passed":
                        if recovery_exists:
                            raise ValueError(
                                "adaptive quality passed prompt has ambiguous recovery"
                            )
                        continue
                    if recovery_exists:
                        if current.cleanup_process_count != 0:
                            raise ValueError(
                                "adaptive quality cleanup uncertainty prohibits recovery"
                            )
                        current = _load_prompt_execution(
                            accepted,
                            prompt_id,
                            recovery_root,
                            accepted.timeout_seconds,
                            evidence_paths=evidence_paths,
                        )
                        results[prompt_id] = current
                        reconciled_append = True
                        if current.status == "passed":
                            continue
                    require_no_later_prompt_artifacts(index)
                    break
                if current.prompt_root != recovery_root or not primary_exists:
                    raise ValueError("adaptive quality recovery evidence is ambiguous")
                if current.status == "passed":
                    continue
                require_no_later_prompt_artifacts(index)
                break
            if not primary_exists:
                if recovery_exists:
                    raise ValueError("adaptive quality recovery has no primary evidence")
                require_no_later_prompt_artifacts(index)
                break
            current = _load_prompt_execution(
                accepted,
                prompt_id,
                primary_root,
                accepted.timeout_seconds,
                evidence_paths=evidence_paths,
            )
            results[prompt_id] = current
            reconciled_append = True
            if recovery_exists:
                if current.status == "passed":
                    raise ValueError(
                        "adaptive quality passed prompt has ambiguous recovery"
                    )
                if current.cleanup_process_count != 0:
                    raise ValueError(
                        "adaptive quality cleanup uncertainty prohibits recovery"
                    )
                current = _load_prompt_execution(
                    accepted,
                    prompt_id,
                    recovery_root,
                    accepted.timeout_seconds,
                    evidence_paths=evidence_paths,
                )
                results[prompt_id] = current
            if current.status == "passed":
                continue
            require_no_later_prompt_artifacts(index)
            break

        if (
            latest_value is not None
            and latest_value["status"] == "passed"
            and not reconciled_append
        ):
            return latest_value
    else:
        root.mkdir(parents=True, exist_ok=False)

    failed = next(
        (
            prompt_id
            for prompt_id in PROMPT_IDS
            if prompt_id in results and results[prompt_id].status != "passed"
        ),
        None,
    )
    terminal_without_launch = False
    if failed is not None:
        failed_result = results[failed]
        if failed_result.cleanup_process_count != 0:
            if latest_value is not None and not reconciled_append:
                return latest_value
            terminal_without_launch = True
        elif failed_result.prompt_root.name.startswith(f"{failed}-recovery-"):
            if latest_value is not None and not reconciled_append:
                return latest_value
            terminal_without_launch = True
        elif latest_path is None and reconciled_append:
            # Preserve the interrupted primary failure in the first summary;
            # a later resume may append its single recovery.
            terminal_without_launch = True
        else:
            del results[failed]

    if not terminal_without_launch:
        start = next(
            (
                index
                for index, prompt_id in enumerate(PROMPT_IDS)
                if prompt_id not in results
            ),
            6,
        )
        for prompt_id in PROMPT_IDS[start:]:
            for prior in results.values():
                if prior.cleanup_process_count != 0:
                    raise RuntimeError("prior quality prompt cleanup proof is invalid")
            prompt_root = root / prompt_id
            if failed == prompt_id:
                prompt_root = root / f"{prompt_id}-recovery-001"
            result = _run_governed_quality_prompt(
                accepted,
                prompt_id,
                prompt_root,
                accepted.timeout_seconds,
                run_command=run_command,
                evidence_paths=evidence_paths,
            )
            results[prompt_id] = result
            if result.status != "passed" or result.cleanup_process_count != 0:
                break

    refreshed = load_accepted_quality_campaign(campaign_input)
    if _accepted_campaign_fingerprint(refreshed) != _accepted_campaign_fingerprint(accepted):
        raise ValueError("accepted quality campaign changed during capture")
    recovery_count = len(list(root.glob("capture-summary-recovery-*.json")))
    summary_path = (
        root / "capture-summary.json"
        if latest_path is None
        else root / f"capture-summary-recovery-{recovery_count + 1:03d}.json"
    )
    result = _summary(
        refreshed,
        results,
        summary_path=summary_path,
        previous_summary_path=latest_path,
    )
    _write_fresh(summary_path, _canonical_json(result))
    validated_path, persisted, _loaded = _validate_summary_history(
        refreshed, root, evidence_paths=evidence_paths
    )
    if validated_path != summary_path or persisted is None:
        raise RuntimeError("adaptive quality summary history publication failed")
    return persisted


__all__ = [
    "GovernedQualityPromptResult",
    "build_quality_prompt_worker_spec",
    "capture_isolated_quality_campaign",
    "run_governed_quality_prompt",
    "quality_campaign_input_from_recovery",
]
