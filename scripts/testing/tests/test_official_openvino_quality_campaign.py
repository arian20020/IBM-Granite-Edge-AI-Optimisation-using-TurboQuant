import hashlib
import json
import math
import os
from dataclasses import replace
from pathlib import Path

import pytest

from scripts.testing.measure_official_openvino import (
    build_campaign_identity,
    run_measurement_sequence,
)
from scripts.testing.tests.test_measure_official_openvino_sequence import (
    _record,
    _setup_campaign,
)


ROOT = Path(__file__).resolve().parents[3]
PROMPT_SET = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "prompts"
    / "fixed-feasibility-prompt-set-v1.json"
)
RENDERED = PROMPT_SET.parent / "rendered"
RUBRIC = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "rubrics"
    / "quality-rubric-v1.json"
)


def _write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(
        (json.dumps(value, indent=2, sort_keys=True) + "\n").encode("utf-8")
    )


def _sha256_json(value):
    return hashlib.sha256(
        json.dumps(
            value,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        ).encode("utf-8")
    ).hexdigest()


def _quality_api():
    from scripts.testing.official_openvino.quality_campaign import (
        QualityCampaignInput,
        build_quality_worker_spec,
        load_accepted_quality_campaign,
    )

    return (
        QualityCampaignInput,
        load_accepted_quality_campaign,
        build_quality_worker_spec,
    )


def _skeletal_input(tmp_path):
    QualityCampaignInput, _, _ = _quality_api()
    values = _setup_campaign(tmp_path)
    (
        values["repo_root"]
        / "scripts"
        / "testing"
        / "measure_official_openvino.py"
    ).write_text("CONTROLLER = 1\n", encoding="utf-8")
    identity = build_campaign_identity(
        **{
            field: values[field]
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
    )
    root = values["campaign_root"]
    _write_json(root / "campaign-identity.json", identity)
    _write_json(
        root / "measurement-summary.json",
        {
            "schema": "official-openvino-wb04-measurement-summary/v1",
            "status": "measured",
            "accepted": True,
            "sample_count": 3,
            "sources": [
                {"sample_id": f"sample-{number}"} for number in range(1, 4)
            ],
            "activation": {"fallback": False},
            "cleanup_process_count": 0,
            "test_id": "OV-TQ-03",
            "context_tokens": 4096,
            "campaign_identity_sha256": identity["campaign_identity_sha256"],
            "runtime_config_sha256": _sha256_json(identity["identity"]["config"]),
        },
    )
    return QualityCampaignInput(
        campaign_root=root,
        spec_path=values["spec_path"],
        matrix_path=values["matrix_path"],
        artifact_manifest_path=values["artifact_manifest_path"],
        build_provenance_path=values["build_provenance_path"],
        build_root=values["build_root"],
        repo_root=values["repo_root"],
        python_executable=values["python_executable"],
        python_site_packages=values["python_site_packages"],
        openvino_libraries=values["openvino_libraries"],
        sampler_script=values["sampler_script"],
        prompt_set_path=PROMPT_SET,
        rendered_root=RENDERED,
        rubric_path=RUBRIC,
        output_root=tmp_path / "quality-output",
        timeout_seconds=1800.0,
    )


def _accepted_input(tmp_path):
    source = _skeletal_input(tmp_path)
    roles = ("pilot", "warmup", "sample-1", "sample-2", "sample-3")

    def fake_measurement(**run_kwargs):
        role = run_kwargs["role"]
        spec = json.loads(
            run_kwargs["spec_path"].read_text(encoding="utf-8")
        )
        record = _record(role, roles.index(role), spec)
        _write_json(run_kwargs["output_dir"] / "attempt.json", record)
        return record

    run_measurement_sequence(
        spec_path=source.spec_path,
        campaign_root=source.campaign_root,
        matrix_path=source.matrix_path,
        artifact_manifest_path=source.artifact_manifest_path,
        build_provenance_path=source.build_provenance_path,
        build_root=source.build_root,
        repo_root=source.repo_root,
        python_executable=source.python_executable,
        python_site_packages=source.python_site_packages,
        openvino_libraries=source.openvino_libraries,
        sampler_script=source.sampler_script,
        run_measurement=fake_measurement,
    )
    return source


def _mutate_json(path, mutate):
    value = json.loads(path.read_text(encoding="utf-8"))
    mutate(value)
    _write_json(path, value)


def test_quality_campaign_module_exposes_governed_adapter_api():
    QualityCampaignInput, load, build = _quality_api()

    assert QualityCampaignInput.__dataclass_params__.frozen is True
    assert callable(load)
    assert callable(build)


def test_skeletal_measurement_summary_without_accepted_sequence_rejects(tmp_path):
    source = _skeletal_input(tmp_path)
    _, load, _ = _quality_api()

    with pytest.raises(ValueError, match="attempt sequence"):
        load(source)


def test_attempt_sequence_duplicate_json_key_rejects(tmp_path):
    source = _accepted_input(tmp_path)
    _, load, _ = _quality_api()
    sequence_path = source.campaign_root / "attempt-sequence.json"
    raw = sequence_path.read_text(encoding="utf-8")
    sequence_path.write_text(
        raw.replace(
            '"schema": "official-openvino-wb04-attempt-sequence/v1",',
            '"schema": "official-openvino-wb04-attempt-sequence/v1",\n'
            '  "schema": "official-openvino-wb04-attempt-sequence/v1",',
            1,
        ),
        encoding="utf-8",
    )

    with pytest.raises(ValueError, match="invalid JSON artifact"):
        load(source)


def test_summary_whitespace_change_rejects_sequence_hash_binding(tmp_path):
    source = _accepted_input(tmp_path)
    _, load, _ = _quality_api()
    summary_path = source.campaign_root / "measurement-summary.json"
    summary_path.write_bytes(summary_path.read_bytes() + b"\n")

    with pytest.raises(ValueError, match="attempt sequence"):
        load(source)


def test_accepted_campaign_recomputes_identity_summary_environment_and_worker_spec(tmp_path):
    source = _accepted_input(tmp_path)
    _, load, build = _quality_api()

    campaign = load(source)
    worker_spec = build(campaign)

    assert campaign.identity["campaign_identity_sha256"] == campaign.campaign_identity_sha256
    assert campaign.measurement_summary["accepted"] is True
    assert campaign.measurement_summary_sha256 == hashlib.sha256(
        (source.campaign_root / "measurement-summary.json").read_bytes()
    ).hexdigest()
    assert campaign.worker_environment["PYTHONPATH"].split(os.pathsep)[:2] == [
        str(source.build_root.resolve()),
        str(source.repo_root.resolve()),
    ]
    assert worker_spec == {
        "schema": "official-openvino-wb04-quality-worker-spec/v1",
        "model_path": str(
            Path(campaign.identity["identity"]["model"]["validated_artifact"]["artifact_root"]).resolve()
        ),
        "device": "CPU",
        "properties": {
            "ATTENTION_BACKEND": "SDPA",
            "TURBOQUANT_KEY_ALGORITHM": "TBQ4",
            "TURBOQUANT_VALUE_ALGORITHM": "TBQ4",
            "TURBOQUANT_NORM_CORRECTION": True,
        },
        "generation_settings": {
            "max_new_tokens": 256,
            "do_sample": False,
            "rng_seed": 42,
            "apply_chat_template": False,
        },
        "prompts": [
            {"prompt_id": "P1", "turn_id": "turn_1", "prompt": campaign.prompt_contract["prompts"]["P1"]["execution"]["prompt"]},
            {"prompt_id": "P2", "turn_id": "turn_1", "prompt": campaign.prompt_contract["prompts"]["P2"]["execution"]["prompt"]},
            {"prompt_id": "P3", "turn_id": "turn_1", "prompt": campaign.prompt_contract["prompts"]["P3"]["execution"]["prompt"]},
            {"prompt_id": "P4", "turn_id": "turn_1", "prompt": campaign.prompt_contract["prompts"]["P4"]["execution"]["prompt"]},
            {"prompt_id": "P5", "turn_id": "turn_1", "prompt": campaign.prompt_contract["prompts"]["P5"]["execution"]["prompt"]},
            {"prompt_id": "P6", "turn_id": "turn_1", "prompt": campaign.prompt_contract["prompts"]["P6"]["execution"]["turn_1_prompt"]},
            {"prompt_id": "P6", "turn_id": "turn_2", "prompt": campaign.prompt_contract["prompts"]["P6"]["execution"]["turn_2_prompt"]},
        ],
    }


@pytest.mark.parametrize(
    "name, mutate",
    (
        ("GenAI DLL", lambda source: (source.build_root / "openvino_genai" / "openvino_genai.dll").write_bytes(b"altered")),
        ("artifact", lambda source: (Path(json.loads(source.spec_path.read_text(encoding="utf-8"))["model_path"]) / "openvino_model.bin").write_bytes(b"altered")),
        ("build", lambda source: _mutate_json(source.build_provenance_path, lambda value: value.update(status="failed"))),
        ("spec", lambda source: _mutate_json(source.spec_path, lambda value: value.update(prompt="substituted prompt"))),
        ("config", lambda source: _mutate_json(source.spec_path, lambda value: value.update(max_new_tokens=5))),
    ),
)
def test_identity_boundary_tampering_rejects_before_worker_spec(name, mutate, tmp_path):
    source = _accepted_input(tmp_path)
    _, load, _ = _quality_api()
    mutate(source)

    with pytest.raises((RuntimeError, ValueError), match="(identity|artifact|build provenance)"):
        load(source)


def test_persisted_identity_requires_the_exact_recomputed_bytes(tmp_path):
    source = _accepted_input(tmp_path)
    _, load, _ = _quality_api()
    identity_path = source.campaign_root / "campaign-identity.json"
    identity_path.write_bytes(identity_path.read_bytes() + b"\n")

    with pytest.raises(ValueError, match="persisted campaign identity"):
        load(source)


@pytest.mark.parametrize(
    "name, mutate, message",
    (
        ("sample count", lambda value: value.update(sample_count=2), "three"),
        ("cleanup", lambda value: value.update(cleanup_process_count=1), "cleanup"),
        ("acceptance", lambda value: value.update(accepted=False), "accepted"),
        ("test", lambda value: value.update(test_id="OV-TQ-99"), "test"),
        ("context", lambda value: value.update(context_tokens=1), "context"),
        ("campaign hash", lambda value: value.update(campaign_identity_sha256="a" * 64), "campaign identity"),
        ("runtime config hash", lambda value: value.update(runtime_config_sha256="a" * 64), "runtime config"),
    ),
)
def test_summary_tampering_rejects_before_worker_spec(name, mutate, message, tmp_path):
    source = _accepted_input(tmp_path)
    _, load, _ = _quality_api()
    _mutate_json(source.campaign_root / "measurement-summary.json", mutate)

    with pytest.raises(ValueError, match=message):
        load(source)


def test_worker_spec_cannot_accept_model_or_configuration_substitution(tmp_path):
    source = _accepted_input(tmp_path)
    _, load, build = _quality_api()
    campaign = load(source)

    with pytest.raises(TypeError):
        campaign.identity["identity"]["config"]["device"] = "GPU"
    with pytest.raises(TypeError):
        campaign.identity["identity"]["build"]["runtime_dll"]["sha256"] = (
            "a" * 64
        )
    with pytest.raises(TypeError):
        build(campaign, model_path="C:/attacker/model")


@pytest.mark.parametrize("construction", ("replace", "direct"))
def test_worker_spec_reloads_retained_campaign_not_forged_dataclass_fields(
    construction,
    tmp_path,
):
    source = _accepted_input(tmp_path)
    _, load, build = _quality_api()
    campaign = load(source)
    forged_identity = {
        "identity": {
            "config": {"device": "GPU", "properties": {"ATTACK": True}},
            "model": {
                "validated_artifact": {"artifact_root": "C:/attacker/model"}
            },
        }
    }
    if construction == "replace":
        forged = replace(campaign, identity=forged_identity)
    else:
        from scripts.testing.official_openvino.quality_campaign import (
            AcceptedQualityCampaign,
        )

        forged = AcceptedQualityCampaign(
            **{
                **{
                    field: getattr(campaign, field)
                    for field in campaign.__dataclass_fields__
                },
                "identity": forged_identity,
            }
        )

    worker_spec = build(forged)

    assert worker_spec["device"] == "CPU"
    assert worker_spec["model_path"] != "C:/attacker/model"
    assert worker_spec["properties"] != {"ATTACK": True}


def test_missing_sampler_script_rejects_before_worker_spec(tmp_path):
    source = replace(
        _accepted_input(tmp_path), sampler_script=tmp_path / "missing-sampler.py"
    )
    _, load, _ = _quality_api()

    with pytest.raises(ValueError, match="sampler script"):
        load(source)


@pytest.mark.parametrize(
    "mutate, message",
    (
        (lambda value: value.update(sample_count=3.0), "three"),
        (lambda value: value.update(cleanup_process_count=False), "cleanup"),
        (lambda value: value.update(context_tokens=4096.0), "context"),
        (
            lambda value: value["sources"].__setitem__(
                2, {"sample_id": "sample-2"}
            ),
            "samples",
        ),
        (lambda value: value["activation"].update(fallback=True), "fallback"),
    ),
)
def test_summary_counts_cleanup_and_context_require_exact_integer_types(
    mutate,
    message,
    tmp_path,
):
    source = _accepted_input(tmp_path)
    _, load, _ = _quality_api()
    _mutate_json(source.campaign_root / "measurement-summary.json", mutate)

    with pytest.raises(ValueError, match=message):
        load(source)


@pytest.mark.parametrize("timeout", (True, math.inf, math.nan, "1800"))
def test_campaign_timeout_requires_a_finite_positive_number(timeout, tmp_path):
    source = replace(_accepted_input(tmp_path), timeout_seconds=timeout)
    _, load, _ = _quality_api()

    with pytest.raises(ValueError, match="timeout"):
        load(source)
