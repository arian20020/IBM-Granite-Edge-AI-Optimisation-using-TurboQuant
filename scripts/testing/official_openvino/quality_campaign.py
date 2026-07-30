"""Validate one accepted measurement campaign before quality capture."""

from __future__ import annotations

import hashlib
import json
import math
import os
import stat
import tempfile
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from types import MappingProxyType
from typing import Any

from scripts.testing.measure_official_openvino import (
    SEQUENCE_ROLES,
    SEQUENCE_SCHEMA,
    _resumable_attempt,
    _role_spec,
    _sequence_spec,
    build_campaign_identity,
    build_worker_environment,
    measurement_sample,
)
from scripts.testing.official_openvino.metrics import summarize_samples
from scripts.testing.official_openvino.quality_worker import (
    GENERATION_SETTINGS,
    RESULT_SCHEMA,
    SPEC_SCHEMA,
    _canonical_json,
)
from scripts.testing.official_openvino.guarded_build import (
    GUARD_SCHEMA,
    GuardLimits,
    _effective_environment,
    run_guarded_command,
)
from scripts.testing.run_official_openvino_quality import (
    PROMPT_IDS,
    QualityRuntimeIdentity,
    _build_capture_summary,
    _build_governed_capture_records,
    _load_frozen_rubric,
    _validate_capture_record,
    _validate_governed_capture_summary,
    load_prompt_contract,
    parse_json_bytes_strict,
)


SUMMARY_SCHEMA = "official-openvino-wb04-measurement-summary/v1"


@dataclass(frozen=True)
class QualityCampaignInput:
    campaign_root: Path
    spec_path: Path
    matrix_path: Path
    artifact_manifest_path: Path
    build_provenance_path: Path
    build_root: Path
    repo_root: Path
    python_executable: Path
    python_site_packages: Path
    openvino_libraries: Path
    sampler_script: Path
    prompt_set_path: Path
    rendered_root: Path
    rubric_path: Path
    output_root: Path
    timeout_seconds: float


@dataclass(frozen=True)
class AcceptedQualityCampaign:
    identity: Mapping[str, Any]
    measurement_summary: Mapping[str, Any]
    measurement_summary_sha256: str
    runtime_config_sha256: str
    campaign_identity_sha256: str
    worker_environment: Mapping[str, str]
    prompt_contract: Mapping[str, Any]
    rubric_sha256: str
    campaign_root: Path
    spec_path: Path
    matrix_path: Path
    artifact_manifest_path: Path
    build_provenance_path: Path
    build_root: Path
    repo_root: Path
    python_executable: Path
    python_site_packages: Path
    openvino_libraries: Path
    sampler_script: Path
    prompt_set_path: Path
    rendered_root: Path
    rubric_path: Path
    output_root: Path
    timeout_seconds: float


@dataclass(frozen=True)
class GovernedQualityWorkerResult:
    worker_result: Mapping[str, Any]
    worker_result_sha256: str
    guard_evidence: Mapping[str, Any]
    guard_evidence_sha256: str
    quality_worker_spec_sha256: str
    worker_log_sha256: str


@dataclass(frozen=True)
class _EvidenceSnapshot:
    kind: str
    identity: tuple[int, ...]
    raw: bytes | None


def _canonical_identity_bytes(value: Mapping[str, Any]) -> bytes:
    return (
        json.dumps(value, indent=2, sort_keys=True, allow_nan=False) + "\n"
    ).encode("utf-8")


def _sha256_json(value: Any) -> str:
    return hashlib.sha256(
        json.dumps(
            _thaw(value),
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        ).encode("utf-8")
    ).hexdigest()


def _thaw(value: Any) -> Any:
    if isinstance(value, Mapping):
        return {key: _thaw(item) for key, item in value.items()}
    if isinstance(value, tuple):
        return [_thaw(item) for item in value]
    return value


def _freeze(value: Any) -> Any:
    if isinstance(value, Mapping):
        return MappingProxyType({key: _freeze(item) for key, item in value.items()})
    if isinstance(value, list):
        return tuple(_freeze(item) for item in value)
    if isinstance(value, tuple):
        return tuple(_freeze(item) for item in value)
    return value


def _read_identity(path: Path, expected: Mapping[str, Any]) -> Mapping[str, Any]:
    source = Path(path) / "campaign-identity.json"
    try:
        raw = source.read_bytes()
    except OSError as error:
        raise ValueError("persisted campaign identity is missing") from error
    if raw != _canonical_identity_bytes(expected):
        raise ValueError("persisted campaign identity does not match recomputed identity")
    value = parse_json_bytes_strict(raw, source=source)
    if value != dict(expected):
        raise ValueError("persisted campaign identity is invalid")
    return _freeze(value)


def _read_summary(
    root: Path,
    *,
    identity: Mapping[str, Any],
    spec_path: Path,
) -> tuple[Mapping[str, Any], str, str]:
    source = Path(root) / "measurement-summary.json"
    try:
        raw = source.read_bytes()
    except OSError as error:
        raise ValueError("measurement summary is missing") from error
    value = parse_json_bytes_strict(raw, source=source)
    if not isinstance(value, Mapping):
        raise ValueError("measurement summary must contain an object")
    summary = dict(value)
    if summary.get("schema") != SUMMARY_SCHEMA:
        raise ValueError("measurement summary schema is invalid")
    if summary.get("status") != "measured" or summary.get("accepted") is not True:
        raise ValueError("measurement summary is not accepted")
    if type(summary.get("sample_count")) is not int or summary["sample_count"] != 3:
        raise ValueError("measurement summary requires exactly three samples")
    sources = summary.get("sources")
    if not isinstance(sources, list) or len(sources) != 3:
        raise ValueError("measurement summary requires exactly three measured samples")
    sample_ids = [row.get("sample_id") for row in sources if isinstance(row, Mapping)]
    if len(sample_ids) != 3 or any(not isinstance(item, str) or not item for item in sample_ids) or len(set(sample_ids)) != 3:
        raise ValueError("measurement summary samples are incomplete")
    if type(summary.get("cleanup_process_count")) is not int or summary["cleanup_process_count"] != 0:
        raise ValueError("measurement summary cleanup process count is not zero")
    activation = summary.get("activation")
    if not isinstance(activation, Mapping) or activation.get("fallback") is not False:
        raise ValueError("measurement summary fallback state is invalid")
    expected_identity = identity["campaign_identity_sha256"]
    expected_config = _sha256_json(identity["identity"]["config"])
    expected_fields = {
        "test_id": identity["identity"]["matrix"]["case"]["test_id"],
        "context_tokens": identity["identity"]["context"],
        "campaign_identity_sha256": expected_identity,
        "runtime_config_sha256": expected_config,
    }
    if type(summary.get("context_tokens")) is not int:
        raise ValueError("measurement summary context tokens mismatch")
    for field, expected_value in expected_fields.items():
        if summary.get(field) != expected_value:
            raise ValueError(f"measurement summary {field.replace('_', ' ')} mismatch")
    try:
        template = _sequence_spec(spec_path)
        completed = []
        for role in SEQUENCE_ROLES:
            resumed = _resumable_attempt(
                campaign_root=Path(root),
                role=role,
                role_spec=_role_spec(template, role, expected_identity),
                identity_sha256=expected_identity,
                matrix_case=identity["identity"]["matrix"]["case"],
            )
            if resumed is None:
                raise ValueError(f"{role} accepted attempt is missing")
            completed.append(resumed)
        expected_summary = summarize_samples(
            [measurement_sample(record, source) for _, record, source in completed[2:]]
        )
    except (RuntimeError, ValueError) as error:
        raise ValueError(f"measurement attempt sequence is invalid: {error}") from error
    expected_summary.update(
        {
            "accepted": True,
            "cleanup_process_count": 0,
            "test_id": template["controlled_test_id"],
            "context_tokens": template["context"],
            "campaign_identity_sha256": expected_identity,
            "runtime_config_sha256": expected_config,
        }
    )
    expected_summary = json.loads(
        json.dumps(expected_summary, allow_nan=False)
    )
    if summary != expected_summary:
        raise ValueError("measurement summary does not match accepted attempts")
    expected_sequence = {
        "schema": SEQUENCE_SCHEMA,
        "campaign_identity_sha256": expected_identity,
        "pilot_passed": True,
        "warmup_excluded": True,
        "pilot": completed[0][0],
        "warmup": completed[1][0],
        "accepted_samples": [receipt for receipt, _, _ in completed[2:]],
        "accepted_sample_count": 3,
        "cleanup_process_count": 0,
        "measurement_summary_path": "measurement-summary.json",
        "measurement_summary_sha256": hashlib.sha256(raw).hexdigest(),
    }
    sequence_source = Path(root) / "attempt-sequence.json"
    try:
        sequence_raw = sequence_source.read_bytes()
    except OSError as error:
        raise ValueError("measurement attempt sequence is missing or unreadable") from error
    sequence = parse_json_bytes_strict(sequence_raw, source=sequence_source)
    if sequence != expected_sequence:
        raise ValueError("measurement attempt sequence does not match accepted attempts")
    return _freeze(summary), hashlib.sha256(raw).hexdigest(), expected_config


def _identity_arguments(value: QualityCampaignInput) -> dict[str, Path]:
    return {
        field: Path(getattr(value, field))
        for field in (
            "spec_path",
            "matrix_path",
            "artifact_manifest_path",
            "build_provenance_path",
            "build_root",
            "repo_root",
            "python_executable",
            "python_site_packages",
            "openvino_libraries",
        )
    }


def _validated_lexical_output_root(path: Path) -> Path:
    """Preserve the requested path while rejecting aliasing ancestry."""

    absolute = Path(os.path.abspath(Path(path)))
    current = Path(absolute.anchor)
    for component in absolute.parts[1:]:
        current /= component
        if not os.path.lexists(current):
            continue
        try:
            metadata = os.lstat(current)
        except OSError as error:
            raise ValueError(
                "quality output root ancestry is unreadable"
            ) from error
        if _is_reparse(metadata):
            raise ValueError(
                "quality output root contains a link or reparse alias"
            )
    return absolute


def load_accepted_quality_campaign(
    input: QualityCampaignInput,
) -> AcceptedQualityCampaign:
    """Recompute and validate a complete accepted runtime campaign."""

    if not isinstance(input, QualityCampaignInput):
        raise TypeError("quality campaign input must be QualityCampaignInput")
    if (
        isinstance(input.timeout_seconds, bool)
        or not isinstance(input.timeout_seconds, (int, float))
        or not math.isfinite(input.timeout_seconds)
        or input.timeout_seconds <= 0
    ):
        raise ValueError("quality campaign timeout must be positive")
    if not Path(input.sampler_script).resolve().is_file():
        raise ValueError("sampler script file is missing")
    recomputed = build_campaign_identity(**_identity_arguments(input))
    identity = _read_identity(input.campaign_root, recomputed)
    summary, summary_sha256, config_sha256 = _read_summary(
        input.campaign_root,
        identity=identity,
        spec_path=input.spec_path,
    )
    prompt_contract = _freeze(
        load_prompt_contract(input.prompt_set_path, input.rendered_root)
    )
    _rubric, rubric_sha256 = _load_frozen_rubric(input.rubric_path)
    environment = _freeze(
        build_worker_environment(
            build_root=input.build_root,
            repo_root=input.repo_root,
            python_site_packages=input.python_site_packages,
            openvino_libraries=input.openvino_libraries,
            base_environment={
                key: value
                for key, value in os.environ.items()
                if key.strip() and value.strip()
            },
        )
    )
    return AcceptedQualityCampaign(
        identity=identity,
        measurement_summary=summary,
        measurement_summary_sha256=summary_sha256,
        runtime_config_sha256=config_sha256,
        campaign_identity_sha256=identity["campaign_identity_sha256"],
        worker_environment=environment,
        prompt_contract=prompt_contract,
        rubric_sha256=rubric_sha256,
        output_root=_validated_lexical_output_root(input.output_root),
        **{
            field: Path(getattr(input, field)).resolve()
            for field in (
                "campaign_root", "spec_path", "matrix_path", "artifact_manifest_path",
                "build_provenance_path", "build_root", "repo_root", "python_executable",
                "python_site_packages", "openvino_libraries", "sampler_script",
                "prompt_set_path", "rendered_root", "rubric_path",
            )
        },
        timeout_seconds=float(input.timeout_seconds),
    )


def build_quality_worker_spec(campaign: AcceptedQualityCampaign) -> dict[str, Any]:
    """Build the Task-1 worker schema from the accepted frozen campaign."""

    if not isinstance(campaign, AcceptedQualityCampaign):
        raise TypeError("campaign must be AcceptedQualityCampaign")
    refreshed = load_accepted_quality_campaign(
        QualityCampaignInput(
            **{
                field: getattr(campaign, field)
                for field in QualityCampaignInput.__dataclass_fields__
            }
        )
    )
    identity = refreshed.identity["identity"]
    config = identity["config"]
    model_path = identity["model"]["validated_artifact"]["artifact_root"]
    prompts = refreshed.prompt_contract["prompts"]
    return {
        "schema": SPEC_SCHEMA,
        "model_path": str(Path(model_path).resolve()),
        "device": config["device"],
        "properties": dict(config["properties"]),
        "generation_settings": dict(GENERATION_SETTINGS),
        "prompts": [
            {"prompt_id": prompt_id, "turn_id": "turn_1", "prompt": prompts[prompt_id]["execution"]["prompt"]}
            for prompt_id in ("P1", "P2", "P3", "P4", "P5")
        ] + [
            {"prompt_id": "P6", "turn_id": "turn_1", "prompt": prompts["P6"]["execution"]["turn_1_prompt"]},
            {"prompt_id": "P6", "turn_id": "turn_2", "prompt": prompts["P6"]["execution"]["turn_2_prompt"]},
        ],
    }


def _accepted_input_from_campaign(
    campaign: AcceptedQualityCampaign,
) -> QualityCampaignInput:
    return QualityCampaignInput(
        **{
            field: getattr(campaign, field)
            for field in QualityCampaignInput.__dataclass_fields__
        }
    )


def _strict_object(raw: bytes, *, source: Path) -> dict[str, Any]:
    def duplicate_free(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
        value: dict[str, Any] = {}
        for key, item in pairs:
            if key in value:
                raise ValueError(f"duplicate JSON key: {key}")
            value[key] = item
        return value

    def reject_constant(value: str) -> None:
        raise ValueError(f"non-finite JSON constant: {value}")

    try:
        value = json.loads(
            raw.decode("utf-8-sig"),
            object_pairs_hook=duplicate_free,
            parse_constant=reject_constant,
        )
    except (UnicodeError, json.JSONDecodeError, ValueError) as error:
        raise ValueError(f"invalid JSON artifact: {source}") from error
    if not isinstance(value, dict):
        raise ValueError(f"JSON artifact must be an object: {source}")
    return value


def _validate_guard_evidence(
    record: Mapping[str, Any],
    *,
    command: list[str],
    cwd: Path,
    log_path: Path,
    evidence_path: Path,
    environment: Mapping[str, str],
    timeout_seconds: float,
    log_sha256: str,
) -> None:
    _, expected_environment_sha256 = _effective_environment(environment)
    required = {
        "schema": GUARD_SCHEMA,
        "valid": True,
        "command": command,
        "working_directory": str(cwd),
        "log_path": str(log_path),
        "evidence_path": str(evidence_path),
        "environment_sha256": expected_environment_sha256,
        "configured_minimum_available_ram_bytes": 2_048 * 1024 * 1024,
        "maximum_runtime_seconds": float(timeout_seconds),
        "timed_out": False,
        "low_memory_stop": False,
        "emergency_actions": [],
        "cleanup_process_count": 0,
        "exit_code": 0,
        "log_sha256": log_sha256,
    }
    for field, expected in required.items():
        actual = record.get(field)
        if type(actual) is not type(expected) or actual != expected:
            label = (
                "timeout"
                if field in {"timed_out", "maximum_runtime_seconds"}
                else field.replace("_", "-")
            )
            raise RuntimeError(f"guard {label} is invalid")
    job = record.get("job_object")
    if type(job) is not dict:
        raise RuntimeError("guard cleanup is invalid")
    for field, expected in {
        "setup_ok": True,
        "query_ok": True,
        "queried_active_process_count_after_cleanup": 0,
        "survivor_pids_after_cleanup": [],
    }.items():
        actual = job.get(field)
        if type(actual) is not type(expected) or actual != expected:
            raise RuntimeError("guard cleanup is invalid")


def _validate_worker_result(raw: bytes, *, source: Path) -> Mapping[str, Any]:
    value = _strict_object(raw, source=source)
    if raw != _canonical_json(value):
        raise ValueError("quality worker result bytes are not canonical")
    if set(value) != {"schema", "outcomes", "worker_result_sha256"}:
        raise ValueError("quality worker result fields are invalid")
    if type(value.get("schema")) is not str or value["schema"] != RESULT_SCHEMA:
        raise ValueError("quality worker result schema is invalid")
    outcomes = value.get("outcomes")
    expected_turns = [
        "P1-turn-1", "P2-turn-1", "P3-turn-1", "P4-turn-1", "P5-turn-1",
        "P6-turn-1", "P6-turn-2",
    ]
    if type(outcomes) is not list or len(outcomes) != len(expected_turns):
        raise ValueError("quality worker result outcomes are invalid")
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
    for outcome, expected_turn in zip(outcomes, expected_turns, strict=True):
        if type(outcome) is not dict or set(outcome) != outcome_fields:
            raise ValueError("quality worker result outcome fields are invalid")
        if (
            type(outcome["turn_id"]) is not str
            or outcome["turn_id"] != expected_turn
        ):
            raise ValueError("quality worker result outcomes are invalid")
        prompt = outcome["raw_prompt"]
        if type(prompt) is not str or not prompt.strip():
            raise ValueError("quality worker result outcome prompt is invalid")
        prompt_sha256 = hashlib.sha256(prompt.encode("utf-8")).hexdigest()
        if (
            type(outcome["raw_prompt_sha256"]) is not str
            or outcome["raw_prompt_sha256"] != prompt_sha256
        ):
            raise ValueError("quality worker result outcome prompt hash is invalid")
        status = outcome["status"]
        if type(status) is not str or status not in {"complete", "failed"}:
            raise ValueError("quality worker result outcome status is invalid")
        if status == "complete":
            output = outcome["raw_output"]
            if type(output) is not str:
                raise ValueError("quality worker result outcome output is invalid")
            output_sha256 = hashlib.sha256(output.encode("utf-8")).hexdigest()
            if (
                type(outcome["raw_output_sha256"]) is not str
                or outcome["raw_output_sha256"] != output_sha256
                or outcome["failure_type"] is not None
                or outcome["failure_message"] is not None
            ):
                raise ValueError(
                    "quality worker result outcome completion fields are invalid"
                )
        elif (
            outcome["raw_output"] is not None
            or outcome["raw_output_sha256"] is not None
            or type(outcome["failure_type"]) is not str
            or not outcome["failure_type"].strip()
            or type(outcome["failure_message"]) is not str
        ):
            raise ValueError("quality worker result outcome failure fields are invalid")
    expected_hash = hashlib.sha256(
        _canonical_json({"schema": value["schema"], "outcomes": outcomes})
    ).hexdigest()
    if (
        type(value.get("worker_result_sha256")) is not str
        or value["worker_result_sha256"] != expected_hash
    ):
        raise ValueError("quality worker result hash is invalid")
    return _freeze(value)


def run_governed_quality_worker(
    campaign: AcceptedQualityCampaign,
    output_root: Path,
    timeout_seconds: float,
    run_command=run_guarded_command,
) -> GovernedQualityWorkerResult:
    """Launch exactly one accepted quality worker behind the owned-process guard."""

    if not isinstance(campaign, AcceptedQualityCampaign):
        raise TypeError("campaign must be AcceptedQualityCampaign")
    if (
        isinstance(timeout_seconds, bool)
        or not isinstance(timeout_seconds, (int, float))
        or not math.isfinite(timeout_seconds)
        or timeout_seconds <= 0
    ):
        raise ValueError("timeout must be finite and positive")
    accepted = load_accepted_quality_campaign(_accepted_input_from_campaign(campaign))
    root = Path(output_root).resolve()
    if root.exists():
        raise FileExistsError("governed quality output directory must be fresh")
    root.mkdir(parents=True)
    spec_path = root / "worker-spec.json"
    result_path = root / "worker-result.json"
    log_path = root / "worker.log"
    evidence_path = root / "guard-evidence.json"
    spec = build_quality_worker_spec(accepted)
    spec_bytes = _canonical_json(spec)
    spec_sha256 = hashlib.sha256(spec_bytes).hexdigest()
    spec_path.write_bytes(spec_bytes)
    command = [
        str(accepted.python_executable),
        "-m",
        "scripts.testing.official_openvino.quality_worker",
        "--spec",
        str(spec_path),
        "--result",
        str(result_path),
    ]
    environment = dict(accepted.worker_environment)
    evidence = run_command(
        command=command,
        cwd=accepted.repo_root,
        log_path=log_path,
        evidence_path=evidence_path,
        expected_exit="zero",
        limits=GuardLimits(
            minimum_available_ram_bytes=2_048 * 1024 * 1024,
            maximum_runtime_seconds=float(timeout_seconds),
        ),
        environment=environment,
    )
    if not isinstance(evidence, Mapping):
        raise RuntimeError("guard evidence is invalid")
    try:
        persisted_spec = spec_path.read_bytes()
    except OSError as error:
        raise ValueError("quality worker spec is missing after launch") from error
    if (
        persisted_spec != spec_bytes
        or hashlib.sha256(persisted_spec).hexdigest() != spec_sha256
    ):
        raise ValueError("quality worker spec was altered during launch")
    try:
        log_bytes = log_path.read_bytes()
    except OSError as error:
        raise RuntimeError("guard log is missing after launch") from error
    log_sha256 = hashlib.sha256(log_bytes).hexdigest()
    try:
        raw_evidence = evidence_path.read_bytes()
    except OSError as error:
        raise RuntimeError("guard evidence is missing after launch") from error
    try:
        persisted_evidence = _strict_object(raw_evidence, source=evidence_path)
    except ValueError as error:
        raise RuntimeError("guard evidence is invalid") from error
    if raw_evidence != _canonical_identity_bytes(persisted_evidence):
        raise RuntimeError("guard evidence bytes are not canonical")
    try:
        returned_evidence_bytes = _canonical_identity_bytes(dict(evidence))
    except (TypeError, ValueError) as error:
        raise RuntimeError("returned guard evidence is invalid") from error
    if (
        persisted_evidence != dict(evidence)
        or raw_evidence != returned_evidence_bytes
    ):
        raise RuntimeError("guard evidence does not match returned record")
    _validate_guard_evidence(
        persisted_evidence,
        command=command,
        cwd=accepted.repo_root,
        log_path=log_path,
        evidence_path=evidence_path,
        environment=environment,
        timeout_seconds=float(timeout_seconds),
        log_sha256=log_sha256,
    )
    try:
        worker_raw = result_path.read_bytes()
    except OSError as error:
        raise ValueError("quality worker result is missing after launch") from error
    worker_result = _validate_worker_result(worker_raw, source=result_path)
    return GovernedQualityWorkerResult(
        worker_result=worker_result,
        worker_result_sha256=hashlib.sha256(worker_raw).hexdigest(),
        guard_evidence=_freeze(persisted_evidence),
        guard_evidence_sha256=hashlib.sha256(raw_evidence).hexdigest(),
        quality_worker_spec_sha256=spec_sha256,
        worker_log_sha256=log_sha256,
    )


def _load_governed_quality_worker(
    campaign: AcceptedQualityCampaign,
    output_root: Path,
    timeout_seconds: float,
    *,
    artifact_bytes: Mapping[str, bytes] | None = None,
) -> GovernedQualityWorkerResult:
    """Strictly revalidate one persisted Task-3 execution without launching."""

    accepted = load_accepted_quality_campaign(
        _accepted_input_from_campaign(campaign)
    )
    root = Path(output_root).resolve()
    expected_names = {
        "worker-spec.json",
        "worker-result.json",
        "worker.log",
        "guard-evidence.json",
    }
    if artifact_bytes is None:
        try:
            children = list(root.iterdir())
        except OSError as error:
            raise ValueError(
                "governed quality execution is incomplete"
            ) from error
        if (
            {child.name for child in children} != expected_names
            or any(
                child.is_symlink() or not child.is_file()
                for child in children
            )
        ):
            raise ValueError(
                "governed quality execution has incomplete or unexpected state"
            )
        try:
            raw_by_name = {
                name: (root / name).read_bytes()
                for name in expected_names
            }
        except OSError as error:
            raise ValueError(
                "governed quality execution is incomplete"
            ) from error
    else:
        if (
            not isinstance(artifact_bytes, Mapping)
            or set(artifact_bytes) != expected_names
            or any(type(raw) is not bytes for raw in artifact_bytes.values())
        ):
            raise ValueError(
                "governed quality execution snapshot is invalid"
            )
        raw_by_name = dict(artifact_bytes)

    spec_path = root / "worker-spec.json"
    result_path = root / "worker-result.json"
    log_path = root / "worker.log"
    evidence_path = root / "guard-evidence.json"
    expected_spec = build_quality_worker_spec(accepted)
    expected_spec_bytes = _canonical_json(expected_spec)
    spec_bytes = raw_by_name["worker-spec.json"]
    if spec_bytes != expected_spec_bytes:
        raise ValueError(
            "quality worker spec bytes do not match accepted campaign"
        )
    spec_sha256 = hashlib.sha256(spec_bytes).hexdigest()

    worker_raw = raw_by_name["worker-result.json"]
    worker_result = _validate_worker_result(worker_raw, source=result_path)

    log_bytes = raw_by_name["worker.log"]
    log_sha256 = hashlib.sha256(log_bytes).hexdigest()

    try:
        guard_raw = raw_by_name["guard-evidence.json"]
        guard = _strict_object(guard_raw, source=evidence_path)
    except ValueError as error:
        raise RuntimeError("guard evidence is missing or invalid") from error
    if guard_raw != _canonical_identity_bytes(guard):
        raise RuntimeError("guard evidence bytes are not canonical")
    command = [
        str(accepted.python_executable),
        "-m",
        "scripts.testing.official_openvino.quality_worker",
        "--spec",
        str(spec_path),
        "--result",
        str(result_path),
    ]
    _validate_guard_evidence(
        guard,
        command=command,
        cwd=accepted.repo_root,
        log_path=log_path,
        evidence_path=evidence_path,
        environment=dict(accepted.worker_environment),
        timeout_seconds=float(timeout_seconds),
        log_sha256=log_sha256,
    )
    return GovernedQualityWorkerResult(
        worker_result=worker_result,
        worker_result_sha256=hashlib.sha256(worker_raw).hexdigest(),
        guard_evidence=_freeze(guard),
        guard_evidence_sha256=hashlib.sha256(guard_raw).hexdigest(),
        quality_worker_spec_sha256=spec_sha256,
        worker_log_sha256=log_sha256,
    )


_GOVERNED_RECEIPT_SCHEMA = (
    "official-openvino-wb04-governed-quality-execution/v1"
)


def _accepted_campaign_fingerprint(
    campaign: AcceptedQualityCampaign,
) -> tuple[str, ...]:
    return (
        campaign.campaign_identity_sha256,
        campaign.measurement_summary_sha256,
        campaign.runtime_config_sha256,
        campaign.prompt_contract["prompt_set_sha256"],
        campaign.rubric_sha256,
        _sha256_json(campaign.worker_environment),
    )


def _governed_execution_receipt(
    campaign: AcceptedQualityCampaign,
    governed: GovernedQualityWorkerResult,
) -> dict[str, Any]:
    guard = governed.guard_evidence
    job = guard["job_object"]
    receipt: dict[str, Any] = {
        "schema": _GOVERNED_RECEIPT_SCHEMA,
        "status": "valid",
        "campaign_identity_sha256": campaign.campaign_identity_sha256,
        "measurement_summary_sha256": campaign.measurement_summary_sha256,
        "runtime_config_sha256": campaign.runtime_config_sha256,
        "prompt_set_sha256": campaign.prompt_contract["prompt_set_sha256"],
        "rubric_sha256": campaign.rubric_sha256,
        "governed_root": "governed",
        "quality_worker_spec_path": "governed/worker-spec.json",
        "quality_worker_spec_sha256": (
            governed.quality_worker_spec_sha256
        ),
        "worker_result_path": "governed/worker-result.json",
        "worker_result_sha256": governed.worker_result_sha256,
        "worker_log_path": "governed/worker.log",
        "worker_log_sha256": governed.worker_log_sha256,
        "guard_evidence_path": "governed/guard-evidence.json",
        "guard_evidence_sha256": governed.guard_evidence_sha256,
        "guard_valid": guard["valid"],
        "guard_timed_out": guard["timed_out"],
        "guard_low_memory_stop": guard["low_memory_stop"],
        "guard_cleanup_process_count": guard["cleanup_process_count"],
        "guard_exit_code": guard["exit_code"],
        "guard_queried_active_process_count_after_cleanup": (
            job["queried_active_process_count_after_cleanup"]
        ),
        "guard_survivor_pids_after_cleanup": list(
            job["survivor_pids_after_cleanup"]
        ),
    }
    receipt["governed_execution_sha256"] = hashlib.sha256(
        _canonical_json(receipt)
    ).hexdigest()
    return receipt


def _exact_json_equal(actual: Any, expected: Any) -> bool:
    if isinstance(expected, Mapping):
        return (
            isinstance(actual, Mapping)
            and set(actual) == set(expected)
            and all(
                _exact_json_equal(actual[key], expected[key])
                for key in expected
            )
        )
    if isinstance(expected, (list, tuple)):
        return (
            type(actual) is list
            and len(actual) == len(expected)
            and all(
                _exact_json_equal(actual_item, expected_item)
                for actual_item, expected_item in zip(
                    actual,
                    expected,
                    strict=True,
                )
            )
        )
    return type(actual) is type(expected) and actual == expected


def _validate_lower_sha256(value: Any, *, field: str) -> str:
    if (
        type(value) is not str
        or len(value) != 64
        or any(character not in "0123456789abcdef" for character in value)
    ):
        raise ValueError(f"{field} must be a lowercase SHA-256")
    return value


def _validate_governed_execution_receipt(
    receipt: Any,
    *,
    expected: Mapping[str, Any],
) -> dict[str, Any]:
    if type(receipt) is not dict or type(expected) is not dict:
        raise ValueError("governed execution receipt must be an object")
    if set(receipt) != set(expected):
        raise ValueError("governed execution receipt fields are invalid")
    for field in (
        "campaign_identity_sha256",
        "measurement_summary_sha256",
        "runtime_config_sha256",
        "prompt_set_sha256",
        "rubric_sha256",
        "quality_worker_spec_sha256",
        "worker_result_sha256",
        "worker_log_sha256",
        "guard_evidence_sha256",
        "governed_execution_sha256",
    ):
        _validate_lower_sha256(receipt.get(field), field=field)
    unsigned = {
        key: value
        for key, value in receipt.items()
        if key != "governed_execution_sha256"
    }
    expected_hash = hashlib.sha256(_canonical_json(unsigned)).hexdigest()
    if receipt["governed_execution_sha256"] != expected_hash:
        raise ValueError("governed execution receipt hash is invalid")
    if not _exact_json_equal(receipt, expected):
        raise ValueError(
            "governed execution receipt does not match evidence"
        )
    return dict(receipt)


def _publish_canonical_json(path: Path, value: Mapping[str, Any]) -> None:
    destination = Path(path)
    if destination.exists():
        raise FileExistsError(f"refusing to overwrite evidence: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    temporary: Path | None = None
    try:
        with tempfile.NamedTemporaryFile(
            mode="wb",
            delete=False,
            dir=destination.parent,
            prefix=f".{destination.name}.",
            suffix=".tmp",
        ) as handle:
            temporary = Path(handle.name)
            handle.write(_canonical_json(dict(value)))
            handle.flush()
            os.fsync(handle.fileno())
        try:
            os.link(temporary, destination)
        except FileExistsError as error:
            raise FileExistsError(
                f"refusing to overwrite evidence: {destination}"
            ) from error
        temporary.unlink()
        temporary = None
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def _decode_canonical_capture_artifact(
    raw: bytes,
    *,
    source: Path,
    label: str,
) -> dict[str, Any]:
    try:
        value = _strict_object(raw, source=source)
    except ValueError as error:
        raise ValueError(f"{label} is invalid") from error
    if raw != _canonical_json(value):
        raise ValueError(f"{label} bytes are not canonical")
    return value


def _capture_file_paths() -> set[str]:
    files = {
        "governed/worker-spec.json",
        "governed/worker-result.json",
        "governed/worker.log",
        "governed/guard-evidence.json",
        "governed-execution.json",
        "capture-summary.json",
    }
    files.update(f"{prompt_id}/response.json" for prompt_id in PROMPT_IDS)
    return files


def _expected_capture_tree() -> set[str]:
    expected = _capture_file_paths()
    expected.update(
        {
            "governed",
            *PROMPT_IDS,
        }
    )
    return expected


_GOVERNED_FILE_PATHS = {
    "worker-spec.json",
    "worker-result.json",
    "worker.log",
    "guard-evidence.json",
}


def _stat_identity(
    value: os.stat_result,
    *,
    handle_compatible: bool = False,
) -> tuple[int, ...]:
    ctime_ns = (
        getattr(value, "st_birthtime_ns", value.st_ctime_ns)
        if handle_compatible and os.name == "nt"
        else value.st_ctime_ns
    )
    return (
        int(value.st_dev),
        int(value.st_ino),
        int(value.st_mode),
        int(value.st_size),
        int(value.st_mtime_ns),
        int(ctime_ns),
        int(value.st_nlink),
        int(getattr(value, "st_file_attributes", 0)),
        int(getattr(value, "st_reparse_tag", 0)),
    )


def _is_reparse(value: os.stat_result) -> bool:
    return bool(
        stat.S_ISLNK(value.st_mode)
        or getattr(value, "st_reparse_tag", 0)
        or (getattr(value, "st_file_attributes", 0) & 0x400)
    )


def _claimed_directory_identity(path: Path) -> tuple[int, int]:
    lexical = _validated_lexical_output_root(path)
    try:
        metadata = os.lstat(lexical)
    except OSError as error:
        raise ValueError("claimed quality output root is missing") from error
    if _is_reparse(metadata) or not stat.S_ISDIR(metadata.st_mode):
        raise ValueError(
            "claimed quality output root is a link, alias, or invalid directory"
        )
    identity = (int(metadata.st_dev), int(metadata.st_ino))
    if identity[1] == 0:
        raise ValueError(
            "claimed quality output root has no stable file identity"
        )
    return identity


def _assert_claimed_directory_identity(
    path: Path,
    expected: tuple[int, int],
) -> None:
    if _claimed_directory_identity(path) != expected:
        raise ValueError(
            "claimed quality output root identity changed during capture"
        )


def _snapshot_path(path: Path, *, kind: str) -> _EvidenceSnapshot:
    try:
        before = os.lstat(path)
    except OSError as error:
        raise ValueError(f"quality evidence is missing: {path}") from error
    if _is_reparse(before):
        raise ValueError(f"quality evidence link or reparse alias: {path}")
    before_identity = _stat_identity(before)
    before_handle_identity = _stat_identity(before, handle_compatible=True)
    if kind == "directory":
        if not stat.S_ISDIR(before.st_mode):
            raise ValueError(f"quality evidence directory is invalid: {path}")
        try:
            after = os.lstat(path)
        except OSError as error:
            raise ValueError(
                f"quality evidence directory changed: {path}"
            ) from error
        if (
            _is_reparse(after)
            or _stat_identity(after) != before_identity
        ):
            raise ValueError(
                f"quality evidence directory identity changed: {path}"
            )
        return _EvidenceSnapshot(
            kind="directory",
            identity=before_identity,
            raw=None,
        )
    if kind != "file" or not stat.S_ISREG(before.st_mode):
        raise ValueError(f"quality evidence file is invalid: {path}")
    if before.st_nlink != 1:
        raise ValueError(f"quality evidence hardlink alias is invalid: {path}")
    try:
        with Path(path).open("rb") as handle:
            opened = os.fstat(handle.fileno())
            if (
                _is_reparse(opened)
                or not stat.S_ISREG(opened.st_mode)
                or opened.st_nlink != 1
                or _stat_identity(opened, handle_compatible=True)
                != before_handle_identity
            ):
                raise ValueError(
                    f"quality evidence file identity changed: {path}"
                )
            raw = handle.read()
            read_complete = os.fstat(handle.fileno())
    except OSError as error:
        raise ValueError(
            f"quality evidence file changed while reading: {path}"
        ) from error
    try:
        after = os.lstat(path)
    except OSError as error:
        raise ValueError(
            f"quality evidence file changed after reading: {path}"
        ) from error
    if (
        _stat_identity(read_complete, handle_compatible=True)
        != before_handle_identity
        or _is_reparse(after)
        or _stat_identity(after) != before_identity
    ):
        raise ValueError(f"quality evidence file identity changed: {path}")
    return _EvidenceSnapshot(
        kind="file",
        identity=before_identity,
        raw=raw,
    )


def _enumerate_tree(root: Path) -> set[str]:
    actual: set[str] = set()
    pending = [Path(root)]
    while pending:
        directory = pending.pop()
        try:
            with os.scandir(directory) as entries:
                children = list(entries)
        except OSError as error:
            raise ValueError(
                "governed quality capture state is incomplete"
            ) from error
        for child in children:
            relative = (
                Path(child.path).relative_to(root).as_posix()
            )
            try:
                metadata = child.stat(follow_symlinks=False)
            except OSError as error:
                raise ValueError(
                    "governed quality capture state changed"
                ) from error
            if _is_reparse(metadata):
                raise ValueError(
                    "governed quality capture contains a link or reparse alias"
                )
            actual.add(relative)
            if stat.S_ISDIR(metadata.st_mode):
                pending.append(Path(child.path))
            elif not stat.S_ISREG(metadata.st_mode):
                raise ValueError(
                    "governed quality capture contains an invalid path"
                )
    return actual


def _snapshot_tree(
    root: Path,
    *,
    expected_tree: set[str],
    file_paths: set[str],
) -> dict[str, _EvidenceSnapshot]:
    root = Path(root)
    root_snapshot = _snapshot_path(root, kind="directory")
    if _enumerate_tree(root) != expected_tree:
        raise ValueError(
            "governed quality capture has incomplete or unexpected state"
        )
    snapshots = {".": root_snapshot}
    file_identities: dict[tuple[int, int], str] = {}
    for relative in sorted(expected_tree):
        kind = "file" if relative in file_paths else "directory"
        snapshot = _snapshot_path(root / relative, kind=kind)
        snapshots[relative] = snapshot
        if kind == "file":
            file_identity = (
                snapshot.identity[0],
                snapshot.identity[1],
            )
            if (
                file_identity[1] != 0
                and file_identity in file_identities
            ):
                raise ValueError(
                    "governed quality capture contains a file identity alias"
                )
            file_identities[file_identity] = relative
    if _snapshot_path(root, kind="directory") != root_snapshot:
        raise ValueError("governed quality capture root changed")
    return snapshots


def _snapshot_capture_tree(root: Path) -> dict[str, _EvidenceSnapshot]:
    return _snapshot_tree(
        root,
        expected_tree=_expected_capture_tree(),
        file_paths=_capture_file_paths(),
    )


def _snapshot_governed_tree(root: Path) -> dict[str, _EvidenceSnapshot]:
    return _snapshot_tree(
        root,
        expected_tree=set(_GOVERNED_FILE_PATHS),
        file_paths=set(_GOVERNED_FILE_PATHS),
    )


def _assert_snapshot_unchanged(
    root: Path,
    snapshot: Mapping[str, _EvidenceSnapshot],
    *,
    governed_only: bool,
) -> None:
    refreshed = (
        _snapshot_governed_tree(root)
        if governed_only
        else _snapshot_capture_tree(root)
    )
    if refreshed != dict(snapshot):
        raise ValueError(
            "governed quality capture changed after its evidence snapshot"
        )


def _snapshot_file_bytes(
    snapshot: Mapping[str, _EvidenceSnapshot],
    relative_path: str,
) -> bytes:
    entry = snapshot.get(relative_path)
    if (
        not isinstance(entry, _EvidenceSnapshot)
        or entry.kind != "file"
        or type(entry.raw) is not bytes
    ):
        raise ValueError(
            f"quality evidence snapshot is incomplete: {relative_path}"
        )
    return entry.raw


def _governed_artifact_bytes(
    snapshot: Mapping[str, _EvidenceSnapshot],
    *,
    prefix: str,
) -> dict[str, bytes]:
    return {
        name: _snapshot_file_bytes(
            snapshot,
            f"{prefix}{name}",
        )
        for name in _GOVERNED_FILE_PATHS
    }


def _validate_capture_snapshot(
    snapshot: Mapping[str, _EvidenceSnapshot],
    *,
    root: Path,
    accepted: AcceptedQualityCampaign,
    runtime: QualityRuntimeIdentity,
    evidence_hashes: Mapping[str, str],
    receipt: Mapping[str, Any],
    records: Mapping[str, Mapping[str, Any]],
    summary: Mapping[str, Any],
) -> dict[str, Any]:
    receipt_raw = _snapshot_file_bytes(
        snapshot,
        "governed-execution.json",
    )
    persisted_receipt = _decode_canonical_capture_artifact(
        receipt_raw,
        source=root / "governed-execution.json",
        label="governed execution receipt",
    )
    _validate_governed_execution_receipt(
        persisted_receipt,
        expected=receipt,
    )
    if receipt_raw != _canonical_json(dict(receipt)):
        raise ValueError(
            "governed execution receipt does not match evidence"
        )
    for prompt_id in PROMPT_IDS:
        relative = f"{prompt_id}/response.json"
        raw = _snapshot_file_bytes(snapshot, relative)
        persisted = _decode_canonical_capture_artifact(
            raw,
            source=root / relative,
            label=f"{prompt_id} quality response",
        )
        validated = _validate_capture_record(
            persisted,
            prompt_id=prompt_id,
            contract=accepted.prompt_contract,
            rubric_sha256=accepted.rubric_sha256,
            expected_runtime=runtime,
            runtime_summary_sha256=accepted.measurement_summary_sha256,
            runtime_config_sha256=accepted.runtime_config_sha256,
            evidence_hashes=evidence_hashes,
        )
        if (
            not _exact_json_equal(validated, records[prompt_id])
            or raw != _canonical_json(dict(records[prompt_id]))
        ):
            raise ValueError(
                f"{prompt_id} quality response does not match worker evidence"
            )
    summary_raw = _snapshot_file_bytes(snapshot, "capture-summary.json")
    persisted_summary = _decode_canonical_capture_artifact(
        summary_raw,
        source=root / "capture-summary.json",
        label="quality capture summary",
    )
    validated_summary = _validate_governed_capture_summary(
        persisted_summary,
        expected=summary,
    )
    if summary_raw != _canonical_json(dict(summary)):
        raise ValueError(
            "quality capture summary does not match response records"
        )
    return validated_summary


def capture_governed_quality_campaign(
    input: QualityCampaignInput,
    *,
    resume: bool,
) -> dict[str, Any]:
    """Capture one accepted campaign through the governed seven-turn worker."""

    if type(resume) is not bool:
        raise TypeError("resume must be a boolean")
    accepted = load_accepted_quality_campaign(input)
    root = accepted.output_root
    governed_root = root / "governed"
    claimed_root_identity: tuple[int, int] | None = None
    if resume:
        capture_snapshot = _snapshot_capture_tree(root)
        governed = _load_governed_quality_worker(
            accepted,
            governed_root,
            accepted.timeout_seconds,
            artifact_bytes=_governed_artifact_bytes(
                capture_snapshot,
                prefix="governed/",
            ),
        )
    else:
        try:
            root.mkdir(parents=True, exist_ok=False)
        except FileExistsError as error:
            raise FileExistsError(
                f"refusing to overwrite pre-existing quality evidence: {root}"
            ) from error
        claimed_root_identity = _claimed_directory_identity(root)
        _assert_claimed_directory_identity(root, claimed_root_identity)
        launched = run_governed_quality_worker(
            accepted,
            governed_root,
            accepted.timeout_seconds,
        )
        _assert_claimed_directory_identity(root, claimed_root_identity)
        if not isinstance(launched, GovernedQualityWorkerResult):
            raise RuntimeError("governed quality worker result is invalid")
        governed_snapshot = _snapshot_governed_tree(governed_root)
        governed = _load_governed_quality_worker(
            accepted,
            governed_root,
            accepted.timeout_seconds,
            artifact_bytes=_governed_artifact_bytes(
                governed_snapshot,
                prefix="",
            ),
        )
        for field in (
            "quality_worker_spec_sha256",
            "worker_result_sha256",
            "worker_log_sha256",
            "guard_evidence_sha256",
        ):
            if getattr(governed, field) != getattr(launched, field):
                raise RuntimeError(
                    "governed quality launch result does not match artifacts"
                )
        _assert_claimed_directory_identity(root, claimed_root_identity)
    if not isinstance(governed, GovernedQualityWorkerResult):
        raise RuntimeError("governed quality worker result is invalid")

    refreshed = load_accepted_quality_campaign(input)
    if _accepted_campaign_fingerprint(refreshed) != (
        _accepted_campaign_fingerprint(accepted)
    ):
        raise ValueError("accepted quality campaign changed during capture")
    accepted = refreshed
    receipt = _governed_execution_receipt(accepted, governed)
    if claimed_root_identity is not None:
        _assert_claimed_directory_identity(root, claimed_root_identity)
    evidence_hashes = {
        "quality_worker_spec_sha256": (
            governed.quality_worker_spec_sha256
        ),
        "worker_result_sha256": governed.worker_result_sha256,
        "guard_evidence_sha256": governed.guard_evidence_sha256,
    }
    runtime = QualityRuntimeIdentity(
        test_id=accepted.measurement_summary["test_id"],
        context_tokens=accepted.measurement_summary["context_tokens"],
        campaign_identity_sha256=accepted.campaign_identity_sha256,
    )
    records = _build_governed_capture_records(
        worker_spec=build_quality_worker_spec(accepted),
        worker_result=governed.worker_result,
        contract=accepted.prompt_contract,
        rubric_sha256=accepted.rubric_sha256,
        expected_runtime=runtime,
        runtime_summary_sha256=accepted.measurement_summary_sha256,
        runtime_config_sha256=accepted.runtime_config_sha256,
        evidence_hashes=evidence_hashes,
    )
    summary = _build_capture_summary(
        records=records,
        contract=accepted.prompt_contract,
        rubric_sha256=accepted.rubric_sha256,
        expected_runtime=runtime,
        runtime_summary_sha256=accepted.measurement_summary_sha256,
        runtime_config_sha256=accepted.runtime_config_sha256,
        evidence_hashes=evidence_hashes,
    )

    if resume:
        persisted_summary = _validate_capture_snapshot(
            capture_snapshot,
            root=root,
            accepted=accepted,
            runtime=runtime,
            evidence_hashes=evidence_hashes,
            receipt=receipt,
            records=records,
            summary=summary,
        )
        _assert_snapshot_unchanged(
            root,
            capture_snapshot,
            governed_only=False,
        )
        return persisted_summary

    if claimed_root_identity is None:
        raise RuntimeError("fresh quality capture has no claimed root owner")
    _assert_claimed_directory_identity(root, claimed_root_identity)
    _assert_snapshot_unchanged(
        governed_root,
        governed_snapshot,
        governed_only=True,
    )
    _publish_canonical_json(root / "governed-execution.json", receipt)
    _assert_claimed_directory_identity(root, claimed_root_identity)
    for prompt_id in PROMPT_IDS:
        _publish_canonical_json(
            root / prompt_id / "response.json",
            records[prompt_id],
        )
        _assert_claimed_directory_identity(root, claimed_root_identity)
    _publish_canonical_json(root / "capture-summary.json", summary)
    _assert_claimed_directory_identity(root, claimed_root_identity)
    capture_snapshot = _snapshot_capture_tree(root)
    persisted_summary = _validate_capture_snapshot(
        capture_snapshot,
        root=root,
        accepted=accepted,
        runtime=runtime,
        evidence_hashes=evidence_hashes,
        receipt=receipt,
        records=records,
        summary=summary,
    )
    _assert_claimed_directory_identity(root, claimed_root_identity)
    _assert_snapshot_unchanged(
        root,
        capture_snapshot,
        governed_only=False,
    )
    _assert_claimed_directory_identity(root, claimed_root_identity)
    return persisted_summary


__all__ = [
    "AcceptedQualityCampaign",
    "GovernedQualityWorkerResult",
    "QualityCampaignInput",
    "build_quality_worker_spec",
    "capture_governed_quality_campaign",
    "load_accepted_quality_campaign",
    "run_governed_quality_worker",
]
