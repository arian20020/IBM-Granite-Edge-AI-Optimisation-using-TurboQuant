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
