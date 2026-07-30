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
    SPEC_SCHEMA,
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


__all__ = [
    "AcceptedQualityCampaign",
    "QualityCampaignInput",
    "build_quality_worker_spec",
    "load_accepted_quality_campaign",
]
