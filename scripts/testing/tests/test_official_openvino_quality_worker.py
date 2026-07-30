import hashlib
import json
import sys
from types import SimpleNamespace

import pytest


def _sha256_text(value):
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def _canonical_worker_hash(value):
    unsigned = {
        key: item
        for key, item in value.items()
        if key != "worker_result_sha256"
    }
    return hashlib.sha256(
        (
            json.dumps(
                unsigned,
                ensure_ascii=False,
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
    ).hexdigest()


def _spec(**overrides):
    value = {
        "schema": "official-openvino-wb04-quality-worker-spec/v1",
        "model_path": "C:/bound/model",
        "device": "CPU",
        "properties": {"CACHE_DIR": "C:/bound/cache"},
        "generation_settings": {
            "max_new_tokens": 256,
            "do_sample": False,
            "rng_seed": 42,
            "apply_chat_template": False,
        },
        "prompts": [
            {"prompt_id": "P1", "turn_id": "turn_1", "prompt": " raw P1\r\n"},
            {"prompt_id": "P2", "turn_id": "turn_1", "prompt": "raw P2"},
            {"prompt_id": "P3", "turn_id": "turn_1", "prompt": "raw P3"},
            {"prompt_id": "P4", "turn_id": "turn_1", "prompt": "raw P4"},
            {"prompt_id": "P5", "turn_id": "turn_1", "prompt": "raw P5"},
            {
                "prompt_id": "P6",
                "turn_id": "turn_1",
                "prompt": "  remember amber:4821  \n",
            },
            {
                "prompt_id": "P6",
                "turn_id": "turn_2",
                "prompt": "  repeat only the code  \n",
            },
        ],
    }
    value.update(overrides)
    return value


class _FakeGenAI:
    def __init__(self, *, fail_prompt=None):
        self.instances = []
        self.calls = []
        self.fail_prompt = fail_prompt

        fake = self

        class GenerationConfig:
            pass

        class LLMPipeline:
            def __init__(self, model_path, device, **properties):
                fake.instances.append((model_path, device, properties))

            def generate(self, prompt, config):
                fake.calls.append((prompt, config))
                if prompt == fake.fail_prompt:
                    raise RuntimeError("synthetic generation failure")
                if "remember amber:4821" in prompt:
                    return "ACTUAL\r\nTURN-ONE"
                return f"OUTPUT<{prompt}>"

        self.module = SimpleNamespace(
            GenerationConfig=GenerationConfig,
            LLMPipeline=LLMPipeline,
        )


def _worker():
    from scripts.testing.official_openvino.quality_worker import (
        execute_quality_worker,
    )

    return execute_quality_worker


def test_quality_worker_module_imports():
    assert callable(_worker())


def test_worker_executes_one_pipeline_and_seven_frozen_ordered_turns(monkeypatch):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)

    result = _worker()(_spec())

    assert fake.instances == [
        ("C:/bound/model", "CPU", {"CACHE_DIR": "C:/bound/cache"})
    ]
    assert [outcome["turn_id"] for outcome in result["outcomes"]] == [
        "P1-turn-1",
        "P2-turn-1",
        "P3-turn-1",
        "P4-turn-1",
        "P5-turn-1",
        "P6-turn-1",
        "P6-turn-2",
    ]
    assert [prompt for prompt, _ in fake.calls] == [
        " raw P1\r\n",
        "raw P2",
        "raw P3",
        "raw P4",
        "raw P5",
        "  remember amber:4821  \n",
        "User: remember amber:4821\nAssistant: ACTUAL\r\nTURN-ONE\nUser: repeat only the code",
    ]
    assert all(
        (
            config.max_new_tokens,
            config.do_sample,
            config.rng_seed,
            config.apply_chat_template,
        )
        == (256, False, 42, False)
        for _, config in fake.calls
    )


def test_worker_preserves_raw_output_and_canonical_hashes(monkeypatch):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)

    result = _worker()(_spec())

    first = result["outcomes"][0]
    assert first["raw_prompt"] == " raw P1\r\n"
    assert first["raw_prompt_sha256"] == _sha256_text(" raw P1\r\n")
    assert first["raw_output"] == "OUTPUT< raw P1\r\n>"
    assert first["raw_output_sha256"] == _sha256_text("OUTPUT< raw P1\r\n>")
    assert result["worker_result_sha256"] == _canonical_worker_hash(result)


def test_non_frozen_generation_settings_reject_before_pipeline_creation(monkeypatch):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    spec = _spec()
    spec["generation_settings"]["max_new_tokens"] = 255

    with pytest.raises(ValueError, match="generation settings"):
        _worker()(spec)

    assert fake.instances == []


@pytest.mark.parametrize(
    "mutate, message",
    [
        (
            lambda value: value["prompts"].pop(),
            "seven ordered prompts",
        ),
        (
            lambda value: value["prompts"].__setitem__(
                1,
                {"prompt_id": "P1", "turn_id": "turn_1", "prompt": "duplicate"},
            ),
            "seven ordered prompts",
        ),
    ],
)
def test_missing_or_duplicate_prompt_rejects_before_pipeline_creation(
    monkeypatch,
    mutate,
    message,
):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    spec = _spec()
    mutate(spec)

    with pytest.raises(ValueError, match=message):
        _worker()(spec)

    assert fake.instances == []


def test_forbidden_quality_or_performance_fields_reject_before_pipeline_creation(
    monkeypatch,
):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    spec = _spec(score=10)

    with pytest.raises(ValueError, match="forbidden"):
        _worker()(spec)

    assert fake.instances == []


def test_worker_preserves_partial_failure_facts_without_scoring(monkeypatch):
    fake = _FakeGenAI(fail_prompt="raw P2")
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)

    result = _worker()(_spec())

    failed = result["outcomes"][1]
    assert failed == {
        "turn_id": "P2-turn-1",
        "raw_prompt": "raw P2",
        "raw_prompt_sha256": _sha256_text("raw P2"),
        "status": "failed",
        "raw_output": None,
        "raw_output_sha256": None,
        "failure_type": "RuntimeError",
        "failure_message": "synthetic generation failure",
    }
    assert all(
        "score" not in key and "rubric" not in key
        for outcome in result["outcomes"]
        for key in outcome
    )
    assert len(result["outcomes"]) == 7


def test_cli_atomically_publishes_canonical_worker_result(tmp_path, monkeypatch):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    from scripts.testing.official_openvino.quality_worker import main

    spec_path = tmp_path / "spec.json"
    result_path = tmp_path / "nested" / "result.json"
    spec_path.write_text(json.dumps(_spec()), encoding="utf-8")

    assert main(["--spec", str(spec_path), "--result", str(result_path)]) == 0

    raw = result_path.read_bytes()
    published = json.loads(raw)
    assert raw.endswith(b"\n")
    assert published["worker_result_sha256"] == _canonical_worker_hash(published)
    assert not list(result_path.parent.glob(".result.json.*.tmp"))
