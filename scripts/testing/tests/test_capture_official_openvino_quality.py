import hashlib
import json
import shutil
from dataclasses import replace
from pathlib import Path

import pytest

from scripts.testing.run_official_openvino_quality import (
    QualityGenerationRequest,
    QualityRuntimeIdentity,
    capture_quality_responses,
)


ROOT = Path(__file__).resolve().parents[3]
PROMPT_ROOT = (
    ROOT / "experiments" / "granite_turboquant_intel" / "prompts"
)
PROMPT_SET = PROMPT_ROOT / "fixed-feasibility-prompt-set-v1.json"
RENDERED = PROMPT_ROOT / "rendered"
RUBRIC = (
    ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "rubrics"
    / "quality-rubric-v1.json"
)
SETTINGS = {
    "temperature": 0.0,
    "top_p": 1.0,
    "seed": 42,
    "max_output_tokens": 256,
}


class FakeGenerator:
    def __init__(self):
        self.calls: list[QualityGenerationRequest] = []

    def __call__(self, request: QualityGenerationRequest):
        self.calls.append(request)
        if request.prompt_id == "P6" and request.turn_id == "turn_1":
            return {"status": "complete", "output": "ACTUAL-SAVED"}
        return {
            "status": "complete",
            "output": f"  exact {request.prompt_id}/{request.turn_id}\r\n",
        }


def _sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def _sha256_json(value) -> str:
    return hashlib.sha256(
        json.dumps(
            value,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()


def _read_exact(path: Path) -> str:
    return path.read_bytes().decode("utf-8-sig")


def _write_measurement_summary(path: Path) -> QualityRuntimeIdentity:
    identity = QualityRuntimeIdentity(
        test_id="OV-TQ-03",
        context_tokens=4096,
        campaign_identity_sha256="c" * 64,
    )
    path.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "status": "measured",
                "accepted": True,
                "test_id": identity.test_id,
                "context_tokens": identity.context_tokens,
                "campaign_identity_sha256": (
                    identity.campaign_identity_sha256
                ),
                "runtime_config_sha256": "d" * 64,
                "sample_count": 3,
                "cleanup_process_count": 0,
            },
            indent=2,
            sort_keys=True,
        )
        + "\n",
        encoding="utf-8",
    )
    return identity


def _capture(
    tmp_path: Path,
    generator,
    *,
    prompt_set: Path = PROMPT_SET,
    rendered: Path = RENDERED,
    resume: bool = False,
):
    measurement_summary = tmp_path / "measurement-summary.json"
    if not measurement_summary.exists():
        identity = _write_measurement_summary(measurement_summary)
    else:
        identity = QualityRuntimeIdentity(
            test_id="OV-TQ-03",
            context_tokens=4096,
            campaign_identity_sha256="c" * 64,
        )
    return capture_quality_responses(
        measurement_summary_path=measurement_summary,
        expected_runtime=identity,
        prompt_set_path=prompt_set,
        rendered_root=rendered,
        rubric_path=RUBRIC,
        output_root=tmp_path / "quality",
        generate=generator,
        resume=resume,
    )


def test_fake_generator_receives_exact_p1_p6_material_and_records_hashes(
    tmp_path,
):
    generator = FakeGenerator()

    result = _capture(tmp_path, generator)

    assert result["status"] == "captured"
    assert [(call.prompt_id, call.turn_id) for call in generator.calls] == [
        ("P1", "turn_1"),
        ("P2", "turn_1"),
        ("P3", "turn_1"),
        ("P4", "turn_1"),
        ("P5", "turn_1"),
        ("P6", "turn_1"),
        ("P6", "turn_2"),
    ]
    by_call = {
        (call.prompt_id, call.turn_id): call for call in generator.calls
    }
    for prompt_id in ("P1", "P2", "P3", "P4"):
        assert by_call[(prompt_id, "turn_1")].raw_prompt == (
            _read_exact(RENDERED / f"{prompt_id}.txt")
        )
    assert by_call[("P5", "turn_1")].raw_prompt == (
        _read_exact(PROMPT_ROOT / "fixtures" / "P5-long-context-v1.txt")
        + _read_exact(RENDERED / "P5-instruction.txt")
    )
    assert by_call[("P6", "turn_1")].raw_prompt == (
        _read_exact(RENDERED / "P6-turn1.txt")
    )
    assert by_call[("P6", "turn_2")].raw_prompt == (
        "User: Remember this project code for the next turn: amber:4821. "
        "Reply only with SAVED.\n"
        "Assistant: ACTUAL-SAVED\n"
        "User: What project code did I ask you to remember? "
        "Reply with the code only."
    )
    assert all(call.generation_settings == SETTINGS for call in generator.calls)

    record = json.loads(
        (tmp_path / "quality" / "P1" / "response.json").read_text(
            encoding="utf-8"
        )
    )
    assert record["output"] == "  exact P1/turn_1\r\n"
    assert record["output_sha256"] == _sha256_text(record["output"])
    assert record["raw_prompt_sha256"] == _sha256_text(record["raw_prompt"])
    assert record["generation_settings_sha256"] == _sha256_text(
        json.dumps(
            SETTINGS,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
    )
    assert len(record["runtime_summary_sha256"]) == 64
    assert record["runtime_config_sha256"] == "d" * 64
    assert record["campaign_identity_sha256"] == "c" * 64
    assert not any(
        "score" in key or "judge" in key
        for key in record
    )


@pytest.mark.parametrize(
    ("field", "wrong"),
    [
        ("test_id", "OV-TQ-04"),
        ("context_tokens", 2048),
        ("campaign_identity_sha256", "e" * 64),
    ],
)
def test_capture_rejects_measurement_identity_mismatch_before_generation(
    tmp_path,
    field,
    wrong,
):
    summary = tmp_path / "measurement-summary.json"
    identity = _write_measurement_summary(summary)
    expected = QualityRuntimeIdentity(
        test_id=wrong if field == "test_id" else identity.test_id,
        context_tokens=(
            wrong if field == "context_tokens" else identity.context_tokens
        ),
        campaign_identity_sha256=(
            wrong
            if field == "campaign_identity_sha256"
            else identity.campaign_identity_sha256
        ),
    )
    generator = FakeGenerator()

    with pytest.raises(RuntimeError, match=field.replace("_", " ")):
        capture_quality_responses(
            measurement_summary_path=summary,
            expected_runtime=expected,
            prompt_set_path=PROMPT_SET,
            rendered_root=RENDERED,
            rubric_path=RUBRIC,
            output_root=tmp_path / "quality",
            generate=generator,
        )

    assert generator.calls == []
    assert not (tmp_path / "quality").exists()


def test_unaccepted_measurement_summary_cannot_launch_quality(tmp_path):
    summary = tmp_path / "measurement-summary.json"
    identity = _write_measurement_summary(summary)
    payload = json.loads(summary.read_text(encoding="utf-8"))
    payload["accepted"] = False
    summary.write_text(json.dumps(payload), encoding="utf-8")
    generator = FakeGenerator()

    with pytest.raises(RuntimeError, match="accepted measurement summary"):
        capture_quality_responses(
            measurement_summary_path=summary,
            expected_runtime=identity,
            prompt_set_path=PROMPT_SET,
            rendered_root=RENDERED,
            rubric_path=RUBRIC,
            output_root=tmp_path / "quality",
            generate=generator,
        )

    assert generator.calls == []
    assert not (tmp_path / "quality").exists()


def test_duplicate_measurement_identity_key_is_rejected_before_generation(
    tmp_path,
):
    summary = tmp_path / "measurement-summary.json"
    identity = _write_measurement_summary(summary)
    raw = summary.read_text(encoding="utf-8")
    raw = raw.replace(
        '"accepted": true,',
        '"accepted": false, "accepted": true,',
    )
    summary.write_text(raw, encoding="utf-8")
    generator = FakeGenerator()

    with pytest.raises(ValueError, match="duplicate JSON key.*accepted"):
        capture_quality_responses(
            measurement_summary_path=summary,
            expected_runtime=identity,
            prompt_set_path=PROMPT_SET,
            rendered_root=RENDERED,
            rubric_path=RUBRIC,
            output_root=tmp_path / "quality",
            generate=generator,
        )

    assert generator.calls == []
    assert not (tmp_path / "quality").exists()


def test_missing_p5_fixture_fails_closed_before_generation(tmp_path, monkeypatch):
    from scripts.testing.campaigns.openvino.quality_contracts import (
        QUALITY_CONTRACTS,
    )

    copied_prompts = tmp_path / "prompts"
    shutil.copytree(PROMPT_ROOT, copied_prompts)
    (copied_prompts / "fixtures" / "P5-long-context-v1.txt").unlink()
    monkeypatch.setitem(
        QUALITY_CONTRACTS,
        "GTQ-PROMPTS-v1",
        replace(
            QUALITY_CONTRACTS["GTQ-PROMPTS-v1"],
            rendered_root=(copied_prompts / "rendered").resolve(),
        ),
    )
    generator = FakeGenerator()

    with pytest.raises(ValueError, match="P5|fixture"):
        _capture(
            tmp_path,
            generator,
            prompt_set=(
                copied_prompts / "fixed-feasibility-prompt-set-v1.json"
            ),
            rendered=copied_prompts / "rendered",
        )

    assert generator.calls == []
    assert not (tmp_path / "quality").exists()


def test_p6_uses_actual_first_turn_output_and_hashes_both_turns(tmp_path):
    generator = FakeGenerator()

    _capture(tmp_path, generator)

    record = json.loads(
        (tmp_path / "quality" / "P6" / "response.json").read_text(
            encoding="utf-8"
        )
    )
    assert record["turn_1"] == "ACTUAL-SAVED"
    assert record["turn_1_sha256"] == _sha256_text("ACTUAL-SAVED")
    assert record["turn_prompts"][1]["raw_prompt"] == generator.calls[-1].raw_prompt
    assert "Assistant: ACTUAL-SAVED" in record["raw_prompt"]
    assert record["output"] == "  exact P6/turn_2\r\n"


def test_failed_generation_preserves_exact_failure_and_partial_output(
    tmp_path,
):
    class FailingGenerator(FakeGenerator):
        def __call__(self, request):
            self.calls.append(request)
            if request.prompt_id == "P2":
                return {
                    "status": "failed",
                    "output": " partial\x00bytes \r\n",
                    "failure": "device error: line 1\r\nline 2 ",
                }
            return {"status": "complete", "output": request.prompt_id}

    result = _capture(tmp_path, FailingGenerator())

    assert result["status"] == "captured-with-failures"
    record = json.loads(
        (tmp_path / "quality" / "P2" / "response.json").read_text(
            encoding="utf-8"
        )
    )
    assert record["status"] == "failed"
    assert record["output"] == " partial\x00bytes \r\n"
    assert record["failure"] == "device error: line 1\r\nline 2 "
    assert record["output_sha256"] == _sha256_text(record["output"])
    assert record["failure_sha256"] == _sha256_text(record["failure"])


def test_resume_validates_and_never_rewrites_immutable_records(tmp_path):
    first = FakeGenerator()
    _capture(tmp_path, first)
    before = {
        path.relative_to(tmp_path / "quality"): path.read_bytes()
        for path in (tmp_path / "quality").rglob("*.json")
    }

    def must_not_generate(_request):
        raise AssertionError("a valid immutable response must be resumed")

    resumed = _capture(tmp_path, must_not_generate, resume=True)
    after = {
        path.relative_to(tmp_path / "quality"): path.read_bytes()
        for path in (tmp_path / "quality").rglob("*.json")
    }

    assert resumed["status"] == "captured"
    assert after == before


def test_generation_settings_are_immutable_for_every_model_call(tmp_path):
    mutation_succeeded = []

    def mutating_generator(request):
        try:
            request.generation_settings["seed"] = 999
        except TypeError:
            mutation_succeeded.append(False)
        else:
            mutation_succeeded.append(True)
        return {"status": "complete", "output": request.turn_id}

    _capture(tmp_path, mutating_generator)

    assert mutation_succeeded == [False] * 7
    records = [
        json.loads(path.read_text(encoding="utf-8"))
        for path in sorted((tmp_path / "quality").glob("P*/response.json"))
    ]
    assert all(record["generation_settings"] == SETTINGS for record in records)


def test_completed_capture_with_missing_response_fails_without_writes(
    tmp_path,
):
    _capture(tmp_path, FakeGenerator())
    missing = tmp_path / "quality" / "P3" / "response.json"
    missing.unlink()
    before = {
        path.relative_to(tmp_path / "quality"): path.read_bytes()
        for path in (tmp_path / "quality").rglob("*")
        if path.is_file()
    }
    calls = []

    def forbidden_generator(request):
        calls.append(request)
        return "must not run"

    with pytest.raises(ValueError, match="completed quality capture.*P3"):
        _capture(tmp_path, forbidden_generator, resume=True)

    after = {
        path.relative_to(tmp_path / "quality"): path.read_bytes()
        for path in (tmp_path / "quality").rglob("*")
        if path.is_file()
    }
    assert calls == []
    assert after == before
    assert not missing.exists()


def test_resume_rejects_scoring_fields_even_with_recomputed_record_hash(
    tmp_path,
):
    _capture(tmp_path, FakeGenerator())
    summary = tmp_path / "quality" / "capture-summary.json"
    summary.unlink()
    response = tmp_path / "quality" / "P1" / "response.json"
    record = json.loads(response.read_text(encoding="utf-8"))
    record["score"] = 10
    unsigned = {
        key: value for key, value in record.items() if key != "record_sha256"
    }
    record["record_sha256"] = _sha256_json(unsigned)
    response.write_text(json.dumps(record), encoding="utf-8")
    before = response.read_bytes()
    calls = []

    def forbidden_generator(request):
        calls.append(request)
        return "must not run"

    with pytest.raises(ValueError, match="unexpected fields"):
        _capture(tmp_path, forbidden_generator, resume=True)

    assert calls == []
    assert response.read_bytes() == before


@pytest.mark.parametrize(
    ("field", "wrong"),
    (
        ("max_new_tokens", 255),
        ("do_sample", True),
        ("rng_seed", 41),
        ("apply_chat_template", True),
    ),
)
def test_governed_settings_projection_requires_the_exact_worker_contract(
    field,
    wrong,
):
    from scripts.testing.run_official_openvino_quality import (
        _map_governed_generation_settings,
    )

    worker_settings = {
        "max_new_tokens": 256,
        "do_sample": False,
        "rng_seed": 42,
        "apply_chat_template": False,
    }
    assert _map_governed_generation_settings(worker_settings) == SETTINGS

    worker_settings[field] = wrong
    with pytest.raises(ValueError, match="worker generation settings"):
        _map_governed_generation_settings(worker_settings)
