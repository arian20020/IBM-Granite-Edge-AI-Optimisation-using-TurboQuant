"""Validate one accepted measurement campaign before quality capture."""

from __future__ import annotations

import hashlib
import json
import math
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
    run_guarded_command,
)
from scripts.testing.run_official_openvino_quality import (
    _load_frozen_rubric,
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
        **{
            field: Path(getattr(input, field)).resolve()
            for field in (
                "campaign_root", "spec_path", "matrix_path", "artifact_manifest_path",
                "build_provenance_path", "build_root", "repo_root", "python_executable",
                "python_site_packages", "openvino_libraries", "sampler_script",
                "prompt_set_path", "rendered_root", "rubric_path", "output_root",
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


def _environment_sha256(value: Mapping[str, str]) -> str:
    return hashlib.sha256(
        json.dumps(
            dict(value),
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        ).encode("utf-8")
    ).hexdigest()


def _validate_guard_evidence(
    record: Mapping[str, Any],
    *,
    command: list[str],
    cwd: Path,
    log_path: Path,
    evidence_path: Path,
    environment: Mapping[str, str],
    timeout_seconds: float,
) -> None:
    expected_environment_sha256 = _environment_sha256(environment)
    required = {
        "schema": GUARD_SCHEMA,
        "valid": True,
        "command": command,
        "working_directory": str(cwd),
        "log_path": str(log_path),
        "evidence_path": str(evidence_path),
        "environment_sha256": expected_environment_sha256,
        "configured_minimum_available_ram_bytes": 2_048 * 1024 * 1024,
        "timed_out": False,
        "low_memory_stop": False,
        "emergency_actions": [],
        "cleanup_process_count": 0,
        "exit_code": 0,
    }
    for field, expected in required.items():
        if record.get(field) != expected:
            label = "timeout" if field == "timed_out" else field.replace("_", "-")
            raise RuntimeError(f"guard {label} is invalid")
    if record.get("maximum_runtime_seconds") not in (None, timeout_seconds):
        raise RuntimeError("guard timeout is invalid")
    job = record.get("job_object")
    if not isinstance(job, Mapping) or any(
        job.get(field) != expected
        for field, expected in {
            "setup_ok": True,
            "query_ok": True,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        }.items()
    ):
        raise RuntimeError("guard cleanup is invalid")


def _validate_worker_result(raw: bytes, *, source: Path) -> Mapping[str, Any]:
    value = _strict_object(raw, source=source)
    if set(value) != {"schema", "outcomes", "worker_result_sha256"}:
        raise ValueError("quality worker result fields are invalid")
    if value.get("schema") != RESULT_SCHEMA:
        raise ValueError("quality worker result schema is invalid")
    outcomes = value.get("outcomes")
    expected_turns = [
        "P1-turn-1", "P2-turn-1", "P3-turn-1", "P4-turn-1", "P5-turn-1",
        "P6-turn-1", "P6-turn-2",
    ]
    if not isinstance(outcomes, list) or [
        row.get("turn_id") if isinstance(row, Mapping) else None
        for row in outcomes
    ] != expected_turns:
        raise ValueError("quality worker result outcomes are invalid")
    expected_hash = hashlib.sha256(
        _canonical_json({"schema": value["schema"], "outcomes": outcomes})
    ).hexdigest()
    if value.get("worker_result_sha256") != expected_hash:
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
    if any(path.exists() for path in (spec_path, result_path, log_path, evidence_path)):
        raise FileExistsError("governed quality evidence target already exists")
    spec = build_quality_worker_spec(accepted)
    spec_bytes = _canonical_json(spec)
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
    raw_evidence = evidence_path.read_bytes()
    persisted_evidence = _strict_object(raw_evidence, source=evidence_path)
    if persisted_evidence != dict(evidence):
        raise RuntimeError("guard evidence does not match returned record")
    _validate_guard_evidence(
        persisted_evidence,
        command=command,
        cwd=accepted.repo_root,
        log_path=log_path,
        evidence_path=evidence_path,
        environment=environment,
        timeout_seconds=float(timeout_seconds),
    )
    worker_raw = result_path.read_bytes()
    worker_result = _validate_worker_result(worker_raw, source=result_path)
    return GovernedQualityWorkerResult(
        worker_result=worker_result,
        worker_result_sha256=hashlib.sha256(worker_raw).hexdigest(),
        guard_evidence=_freeze(persisted_evidence),
        guard_evidence_sha256=hashlib.sha256(raw_evidence).hexdigest(),
    )


__all__ = [
    "AcceptedQualityCampaign",
    "GovernedQualityWorkerResult",
    "QualityCampaignInput",
    "build_quality_worker_spec",
    "load_accepted_quality_campaign",
    "run_governed_quality_worker",
]
