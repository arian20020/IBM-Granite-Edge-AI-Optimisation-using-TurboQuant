import hashlib
import json
from pathlib import Path

import pytest


ROOT = Path(__file__).resolve().parents[3]
FIXTURE_MATRIX = (
    ROOT
    / "experiments"
    / "manifests"
    / "official-openvino"
    / "format-boundary-matrix-v1.json"
)
PROMPT_ROOT = ROOT / "experiments" / "granite_turboquant_intel" / "prompts"
V1_PROMPT_SET = PROMPT_ROOT / "fixed-feasibility-prompt-set-v1.json"
V2_PROMPT_SET = PROMPT_ROOT / "compact-feasibility-prompt-set-v2.json"
FROZEN_V1_SHA256 = "9ba512818e81e0ba8da3ddc89cf040dd3b779d1edc41db23d3a896d778de807f"


@pytest.fixture
def fake_config(tmp_path):
    from scripts.testing.official_openvino.format_boundary import BoundaryCampaignConfig

    return BoundaryCampaignConfig(
        repository_root=ROOT,
        campaign_root=tmp_path / "campaign",
        manifest_path=FIXTURE_MATRIX,
        prompt_set_path=V2_PROMPT_SET,
        rubric_path=ROOT / "experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json",
    )


def _accepted_runtime(case, **kwargs):
    return {
        "accepted": True,
        "accepted_sample_count": 3,
        "pilot_passed": True,
        "warmup_excluded": True,
        "cleanup_process_count": 0,
        "measurement_summary_path": "measurement-summary.json",
        "measurement_summary_sha256": "a" * 64,
        "case": case.internal_id,
    }


def _passing_quality(*args, **kwargs):
    return {
        "status": "passed",
        "completed_prompt_ids": ["P1", "P2", "P3", "P4", "P5", "P6"],
        "prompt_receipt_count": 6,
        "cleanup_process_count": 0,
    }


def test_second_matching_cpu_failure_stops_higher_formats(fake_config):
    from scripts.testing.official_openvino.format_boundary import RowFailure, run_boundary_campaign

    calls = []

    def fail_twice(case, **kwargs):
        calls.append(case.label)
        raise RowFailure("minimum_available_ram", hard=False)

    state = run_boundary_campaign(
        fake_config, run_measurement=fail_twice,
        run_quality=lambda *a, **k: pytest.fail("quality must not run"),
        available_ram=lambda: 8 * 1024**3,
    )
    assert calls == ["U4 weights + TBQ3 cache"] * 2
    assert state["cpu_lane"]["status"] == "stopped"
    assert state["cpu_lane"]["reason_code"] == "minimum_available_ram"
    assert state["cpu_lane"]["skipped_count"] == 8


def test_gpu_control_failure_does_not_close_cpu_lane(fake_config):
    from scripts.testing.official_openvino.format_boundary import RowFailure, run_boundary_campaign

    def lane_aware_runner(case, **kwargs):
        if case.lane == "gpu-control":
            raise RowFailure("gpu_control_failed", hard=False)
        return _accepted_runtime(case)

    state = run_boundary_campaign(
        fake_config, run_measurement=lane_aware_runner,
        run_quality=_passing_quality,
        available_ram=lambda: 8 * 1024**3,
    )
    assert state["gpu_lane"]["status"] == "stopped"
    assert state["cpu_lane"]["accepted_count"] > 3


def test_hard_ram_breach_is_not_retried(fake_config):
    from scripts.testing.official_openvino.format_boundary import RowFailure, run_boundary_campaign

    calls = []

    def hard_failure(case, **kwargs):
        calls.append(case.internal_id)
        raise RowFailure("emergency_minimum_available_ram", hard=True)

    state = run_boundary_campaign(
        fake_config, run_measurement=hard_failure, run_quality=_passing_quality,
        available_ram=lambda: 8 * 1024**3,
    )
    assert calls == ["cpu-u4-tbq3"]
    assert state["cpu_lane"]["attempt_count"] == 1
    terminal = json.loads((fake_config.campaign_root / "cpu" / "terminal-boundary.json").read_text())
    assert terminal["reason_code"] == "emergency_minimum_available_ram"
    assert terminal["envelope"] == "outside this laptop's configured safe RAM/time envelope"


def test_quality_only_runs_after_runtime_acceptance_and_atomic_state(fake_config):
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    quality_calls = []

    def quality(case, runtime, **kwargs):
        quality_calls.append((case.internal_id, runtime["accepted_sample_count"]))
        assert (fake_config.campaign_root / "campaign-state.json").is_file()
        return _passing_quality()

    state = run_boundary_campaign(
        fake_config, run_measurement=_accepted_runtime, run_quality=quality,
        available_ram=lambda: 8 * 1024**3,
    )
    assert len(quality_calls) == 10
    assert state["cpu_lane"]["accepted_count"] == 9
    assert not list(fake_config.campaign_root.glob("*.tmp"))
    accepted = fake_config.campaign_root / "cpu" / "cpu-u4-tbq3" / "accepted-row.json"
    assert json.loads(accepted.read_text())["runtime"]["accepted_sample_count"] == 3


def test_resume_revalidates_receipts_and_never_reruns_accepted_work(fake_config):
    from dataclasses import replace
    from scripts.testing.official_openvino.format_boundary import run_boundary_campaign

    initial_calls = []

    def initial(case, **kwargs):
        initial_calls.append(case.internal_id)
        return _accepted_runtime(case)

    run_boundary_campaign(
        fake_config, run_measurement=initial, run_quality=_passing_quality,
        available_ram=lambda: 8 * 1024**3,
    )
    resumed_calls = []
    state = run_boundary_campaign(
        replace(fake_config, resume=True),
        run_measurement=lambda case, **kwargs: resumed_calls.append(case.internal_id),
        run_quality=lambda *a, **k: pytest.fail("accepted quality must not rerun"),
        available_ram=lambda: 8 * 1024**3,
    )
    assert len(initial_calls) == 10
    assert resumed_calls == []
    assert state["cpu_lane"]["accepted_count"] == 9


def test_manifest_has_exact_low_to_high_cpu_order_and_separate_gpu_lane(tmp_path):
    from scripts.testing.official_openvino.format_boundary import (
        load_boundary_manifest,
    )

    manifest = load_boundary_manifest(FIXTURE_MATRIX)
    assert [case.label for case in manifest.cpu_cases] == [
        "U4 weights + TBQ3 cache", "U4 weights + TBQ4 cache",
        "U4 weights + standard F16 cache", "U8 weights + TBQ3 cache",
        "U8 weights + TBQ4 cache", "U8 weights + standard F16 cache",
        "F16 weights + TBQ3 cache", "F16 weights + TBQ4 cache",
        "F16 weights + standard F16 cache",
    ]
    assert [case.label for case in manifest.gpu_cases] == [
        "U4 weights + standard F16 cache (GPU control)"
    ]


def test_worker_spec_activates_tbq3_without_visible_test_codes(tmp_path):
    from scripts.testing.official_openvino.format_boundary import (
        build_boundary_worker_spec,
        load_boundary_manifest,
    )

    case = load_boundary_manifest(FIXTURE_MATRIX).cpu_cases[0]
    spec = build_boundary_worker_spec(case, role="pilot", cache_dir=tmp_path)
    assert spec["device"] == "CPU"
    assert spec["context"] == 512
    assert spec["properties"]["TURBOQUANT_KEY_ALGORITHM"] == "TBQ3"
    assert spec["properties"]["TURBOQUANT_VALUE_ALGORITHM"] == "TBQ3"
    assert spec["properties"]["TURBOQUANT_NORM_CORRECTION"] is True
    assert spec["properties"]["INFERENCE_NUM_THREADS"] == 1
    assert spec["properties"]["NUM_STREAMS"] == 1


def test_quality_contract_allow_lists_frozen_v1_and_compact_v2():
    from scripts.testing.official_openvino.quality_contracts import (
        load_quality_contract,
    )

    assert hashlib.sha256(V1_PROMPT_SET.read_bytes()).hexdigest() == FROZEN_V1_SHA256
    v1 = load_quality_contract(V1_PROMPT_SET)
    v2 = load_quality_contract(V2_PROMPT_SET)
    assert v1.prompt_set_id == "GTQ-PROMPTS-v1"
    assert v1.maximum_input_tokens is None
    assert v2.prompt_set_id == "GTQ-PROMPTS-v2"
    assert v2.rendered_root == PROMPT_ROOT / "rendered-v2"
    assert v2.maximum_input_tokens == 512


def test_compact_prompt_contract_rejects_substituted_rendered_root(tmp_path):
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    with pytest.raises(ValueError, match="rendered root"):
        load_prompt_contract(V2_PROMPT_SET, PROMPT_ROOT / "rendered")


def test_v2_is_accepted_by_quality_and_both_adjudicator_prompt_controls():
    from scripts.testing.adjudicate_official_openvino_adaptive_quality import (
        _prompt_controls as adaptive_prompt_controls,
    )
    from scripts.testing.adjudicate_official_openvino_quality import _prompt_controls
    from scripts.testing.official_openvino.quality import validate_response_record
    from scripts.testing.official_openvino.quality_contracts import (
        load_quality_contract,
    )

    contract = load_quality_contract(V2_PROMPT_SET)
    output = "MARKER:IXN-TQ-7319"
    record = {
        "schema_version": 1,
        "status": "complete",
        "test_id": "boundary-u4-tbq3",
        "context_tokens": 512,
        "prompt_id": "P5",
        "prompt_set_id": contract.prompt_set_id,
        "prompt_set_sha256": contract.prompt_set_sha256,
        "prompt_sha256": "a" * 64,
        "rubric_id": "GTQ-QUALITY-RUBRIC-v1",
        "runtime_summary_sha256": "b" * 64,
        "runtime_config_sha256": "c" * 64,
        "generation_settings": {
            "temperature": 0.0, "top_p": 1.0, "seed": 42,
            "max_output_tokens": 256,
        },
        "output": output,
        "output_sha256": hashlib.sha256(output.encode("utf-8")).hexdigest(),
    }
    assert validate_response_record(
        record,
        expected_runtime={
            "test_id": "boundary-u4-tbq3", "context_tokens": 512,
            "runtime_summary_sha256": "b" * 64,
            "runtime_config_sha256": "c" * 64,
        },
        expected_prompt_set_sha256=contract.prompt_set_sha256,
        expected_prompt_sha256="a" * 64,
    )["prompt_set_id"] == "GTQ-PROMPTS-v2"
    assert _prompt_controls(V2_PROMPT_SET)["P5"]["exact_output"] == output
    assert adaptive_prompt_controls(V2_PROMPT_SET)["P5"]["exact_output"] == output


class _FakeGraniteTokenizer:
    def __init__(self, token_count):
        self.token_count = token_count
        self.seen = []

    def encode(self, text):
        self.seen.append(text)
        return list(range(self.token_count))


def test_compact_prompt_uses_injected_tokenizer_and_persists_observed_count():
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    tokenizer = _FakeGraniteTokenizer(357)
    contract = load_prompt_contract(
        V2_PROMPT_SET,
        PROMPT_ROOT / "rendered-v2",
        tokenizer_loader=lambda path: tokenizer,
    )
    assert tokenizer.seen == [contract["prompts"]["P5"]["execution"]["prompt"]]
    assert contract["observed_input_tokens"]["P5"] == 357


def test_compact_prompt_rejects_513_tokens_before_generation():
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    with pytest.raises(ValueError, match="maximum input tokens"):
        load_prompt_contract(
            V2_PROMPT_SET,
            PROMPT_ROOT / "rendered-v2",
            tokenizer_loader=lambda path: _FakeGraniteTokenizer(513),
        )


def test_registry_binds_prompt_set_id_to_its_exact_registered_hash():
    from scripts.testing.official_openvino.quality_contracts import (
        require_quality_contract_identity,
    )

    v1 = require_quality_contract_identity("GTQ-PROMPTS-v1", FROZEN_V1_SHA256)
    v2 = require_quality_contract_identity(
        "GTQ-PROMPTS-v2",
        hashlib.sha256(V2_PROMPT_SET.read_bytes()).hexdigest(),
    )
    assert v1.prompt_set_id == "GTQ-PROMPTS-v1"
    assert v2.prompt_set_id == "GTQ-PROMPTS-v2"
    with pytest.raises(ValueError, match="hash"):
        require_quality_contract_identity("GTQ-PROMPTS-v1", v2.prompt_set_sha256)


def _temporary_v2_contract_with_p5_count(tmp_path, monkeypatch, token_count):
    from dataclasses import replace

    from scripts.testing.official_openvino.quality_contracts import (
        QUALITY_CONTRACTS,
    )

    payload = json.loads(V2_PROMPT_SET.read_text(encoding="utf-8"))
    payload["rendered_asset_manifest"]["P5_input_tokens"] = token_count
    path = tmp_path / "compact-feasibility-prompt-set-v2.json"
    fixture = tmp_path / "fixtures" / "P5-compact-context-v2.txt"
    fixture.parent.mkdir()
    fixture.write_bytes(
        (PROMPT_ROOT / "fixtures" / "P5-compact-context-v2.txt").read_bytes()
    )
    path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    raw = path.read_bytes()
    monkeypatch.setitem(
        QUALITY_CONTRACTS,
        "GTQ-PROMPTS-v2",
        replace(
            QUALITY_CONTRACTS["GTQ-PROMPTS-v2"],
            prompt_set_sha256=hashlib.sha256(raw).hexdigest(),
        ),
    )
    return path


def test_compact_prompt_accepts_exactly_512_tokens(tmp_path, monkeypatch):
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    contract = load_prompt_contract(
        _temporary_v2_contract_with_p5_count(tmp_path, monkeypatch, 512),
        PROMPT_ROOT / "rendered-v2",
        tokenizer_loader=lambda path: _FakeGraniteTokenizer(512),
    )
    assert contract["observed_input_tokens"] == {"P5": 512}


def test_compact_prompt_rejects_observed_count_that_differs_from_signed_manifest():
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    with pytest.raises(ValueError, match="observed token count"):
        load_prompt_contract(
            V2_PROMPT_SET,
            PROMPT_ROOT / "rendered-v2",
            tokenizer_loader=lambda path: _FakeGraniteTokenizer(512),
        )


def test_v1_prompt_contract_keeps_its_prior_shape_without_tokenizer_loading():
    from scripts.testing.run_official_openvino_quality import load_prompt_contract

    def forbidden_tokenizer_loader(path):
        pytest.fail(f"v1 must not load a tokenizer: {path}")

    contract = load_prompt_contract(
        V1_PROMPT_SET,
        PROMPT_ROOT / "rendered",
        tokenizer_loader=forbidden_tokenizer_loader,
    )
    assert set(contract) == {
        "prompt_set_id", "prompt_set_sha256", "generation_settings", "prompts",
    }
    assert contract["prompt_set_id"] == "GTQ-PROMPTS-v1"
    assert contract["prompt_set_sha256"] == FROZEN_V1_SHA256


def test_both_adjudicators_bind_v2_scoring_input_identity_to_registry_hash():
    from scripts.testing import adjudicate_official_openvino_adaptive_quality as adaptive
    from scripts.testing import adjudicate_official_openvino_quality as standard
    from scripts.testing.official_openvino.quality_contracts import (
        load_quality_contract,
    )

    v2 = load_quality_contract(V2_PROMPT_SET)
    assert standard._scoring_contract(v2.prompt_set_id, v2.prompt_set_sha256) == v2
    assert adaptive._scoring_contract(v2.prompt_set_id, v2.prompt_set_sha256) == v2
    with pytest.raises(ValueError, match="hash"):
        standard._scoring_contract("GTQ-PROMPTS-v1", v2.prompt_set_sha256)
    with pytest.raises(ValueError, match="hash"):
        adaptive._scoring_contract("GTQ-PROMPTS-v1", v2.prompt_set_sha256)
