import builtins
import hashlib
import json
import sys
from collections.abc import Mapping
from pathlib import Path
from types import SimpleNamespace
from types import MappingProxyType

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


def test_prompt_worker_rejects_copied_bound_file_before_openvino_import(
    tmp_path,
    monkeypatch,
):
    from scripts.testing.official_openvino.adaptive_quality import (
        build_quality_prompt_worker_spec,
    )
    from scripts.testing.official_openvino.quality_campaign import (
        load_accepted_quality_campaign,
    )
    from scripts.testing.official_openvino.quality_worker import (
        execute_quality_prompt_worker,
    )
    from scripts.testing.tests.test_official_openvino_quality_campaign import (
        _accepted_input,
    )

    source = _accepted_input(tmp_path)
    campaign = load_accepted_quality_campaign(source)
    spec = build_quality_prompt_worker_spec(campaign, "P1")
    copied_prompt_set = tmp_path / "copied-prompt-set.json"
    copied_prompt_set.write_bytes(source.prompt_set_path.read_bytes())
    spec["bindings"]["prompt_set_path"] = str(copied_prompt_set.resolve())
    assert spec["bindings"]["prompt_set_sha256"] == hashlib.sha256(
        copied_prompt_set.read_bytes()
    ).hexdigest()

    imported = []
    real_import = builtins.__import__

    def reject_openvino_import(name, *args, **kwargs):
        if name == "openvino_genai":
            imported.append(name)
            raise AssertionError("OpenVINO import must follow binding validation")
        return real_import(name, *args, **kwargs)

    monkeypatch.setattr(builtins, "__import__", reject_openvino_import)

    with pytest.raises(ValueError, match="prompt_set path"):
        execute_quality_prompt_worker(spec)

    assert imported == []


@pytest.mark.parametrize(
    "binding",
    (
        "runtime_summary",
        "raw_sample",
        "attempt_sequence",
        "adaptive_runtime_spec",
        "pilot_spec",
        "spec_index",
        "artifact_inventory",
        "matrix",
        "artifact_manifest",
        "prompt_set",
        "rubric",
        "build_provenance",
        "quality_worker",
    ),
)
def test_prompt_worker_rejects_same_bound_bytes_at_any_other_path(
    tmp_path, binding
):
    from scripts.testing.official_openvino.adaptive_quality import (
        build_quality_prompt_worker_spec,
    )
    from scripts.testing.official_openvino.quality_campaign import (
        load_accepted_quality_campaign,
    )
    from scripts.testing.official_openvino.quality_worker import (
        _validate_prompt_worker_spec,
    )
    from scripts.testing.tests.test_official_openvino_quality_campaign import (
        _accepted_input,
    )

    source = _accepted_input(tmp_path)
    campaign = load_accepted_quality_campaign(source)
    spec = build_quality_prompt_worker_spec(campaign, "P1")
    if binding == "raw_sample":
        original = Path(spec["bindings"]["raw_samples"][0]["path"])
    else:
        original = Path(spec["bindings"][f"{binding}_path"])
    copied = tmp_path / "copied-bindings" / binding / original.name
    copied.parent.mkdir(parents=True)
    copied.write_bytes(original.read_bytes())
    if binding == "raw_sample":
        spec["bindings"]["raw_samples"][0]["path"] = str(copied.resolve())
    else:
        spec["bindings"][f"{binding}_path"] = str(copied.resolve())

    with pytest.raises(ValueError):
        _validate_prompt_worker_spec(spec)


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


def test_generation_settings_constant_cannot_be_mutated_or_weaken_validation(
    monkeypatch,
):
    from scripts.testing.official_openvino import quality_worker

    assert isinstance(quality_worker.GENERATION_SETTINGS, MappingProxyType)
    with pytest.raises(TypeError):
        quality_worker.GENERATION_SETTINGS["max_new_tokens"] = 1

    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    spec = _spec()
    spec["generation_settings"]["max_new_tokens"] = 1

    with pytest.raises(ValueError, match="generation settings"):
        _worker()(spec)

    assert fake.instances == []


@pytest.mark.parametrize(
    "field, value",
    [
        ("max_new_tokens", 256.0),
        ("max_new_tokens", True),
        ("do_sample", 0),
        ("rng_seed", 42.0),
        ("rng_seed", False),
        ("apply_chat_template", 0),
    ],
)
def test_generation_settings_require_exact_types_before_pipeline_creation(
    monkeypatch,
    field,
    value,
):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    spec = _spec()
    spec["generation_settings"][field] = value

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


@pytest.mark.parametrize(
    "field",
    (
        "score",
        "rubric",
        "rank",
        "privateLabel",
        "private label",
        "codec",
        "performanceTarget",
    ),
)
def test_forbidden_quality_or_performance_fields_reject_before_pipeline_creation(
    monkeypatch,
    field,
):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    spec = _spec(**{field: 10})

    with pytest.raises(ValueError, match="forbidden"):
        _worker()(spec)

    assert fake.instances == []


def test_forbidden_fields_inside_tuple_reject_before_pipeline_creation(monkeypatch):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    spec = _spec(properties={"NESTED": ({"performance target": 1},)})

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


def test_p6_turn_one_failure_still_makes_a_seventh_real_generation_call(
    monkeypatch,
):
    p6_turn_one = "  remember amber:4821  \n"
    fake = _FakeGenAI(fail_prompt=p6_turn_one)
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)

    result = _worker()(_spec())

    assert len(fake.calls) == 7
    assert fake.calls[-1][0] == (
        "User: remember amber:4821\n"
        "Assistant: \n"
        "User: repeat only the code"
    )
    assert "Failure:" not in fake.calls[-1][0]
    assert result["outcomes"][5]["status"] == "failed"
    assert result["outcomes"][5]["failure_type"] == "RuntimeError"
    assert (
        result["outcomes"][5]["failure_message"]
        == "synthetic generation failure"
    )
    assert result["outcomes"][6]["status"] == "complete"
    assert result["outcomes"][6]["failure_type"] is None


class _ChangingSpec(Mapping):
    def __init__(self, value):
        self.value = value
        self.reads = {"model_path": 0, "device": 0, "properties": 0}

    def __iter__(self):
        return iter(self.value)

    def __len__(self):
        return len(self.value)

    def __getitem__(self, key):
        if key in self.reads:
            self.reads[key] += 1
            if self.reads[key] > 1:
                return {
                    "model_path": "C:/attacker/model",
                    "device": "GPU",
                    "properties": {"CACHE_DIR": "C:/attacker/cache"},
                }[key]
        return self.value[key]


def test_worker_executes_immutable_normalized_spec_not_a_stateful_mapping(
    monkeypatch,
):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)

    _worker()(_ChangingSpec(_spec()))

    assert fake.instances == [
        ("C:/bound/model", "CPU", {"CACHE_DIR": "C:/bound/cache"})
    ]


class _ChangingNestedMapping(Mapping):
    def __init__(self, safe, changed, *, safe_reads):
        self.safe = safe
        self.changed = changed
        self.safe_reads = safe_reads
        self.reads = {key: 0 for key in safe}

    def __iter__(self):
        return iter(self.safe)

    def __len__(self):
        return len(self.safe)

    def __getitem__(self, key):
        self.reads[key] += 1
        if self.reads[key] <= self.safe_reads:
            return self.safe[key]
        return self.changed[key]


def test_worker_uses_detached_nested_properties_mapping(monkeypatch):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    properties = _ChangingNestedMapping(
        {"CACHE_DIR": "C:/bound/cache"},
        {"CACHE_DIR": "C:/attacker/cache"},
        safe_reads=1,
    )

    _worker()(_spec(properties=properties))

    assert fake.instances == [
        ("C:/bound/model", "CPU", {"CACHE_DIR": "C:/bound/cache"})
    ]


def test_worker_uses_detached_nested_settings_mapping(monkeypatch):
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    settings = _ChangingNestedMapping(
        {
            "max_new_tokens": 256,
            "do_sample": False,
            "rng_seed": 42,
            "apply_chat_template": False,
        },
        {
            "max_new_tokens": 0,
            "do_sample": True,
            "rng_seed": 7,
            "apply_chat_template": True,
        },
        safe_reads=2,
    )

    _worker()(_spec(generation_settings=settings))

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
    assert raw == (
        json.dumps(
            published,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8")
    assert published["worker_result_sha256"] == _canonical_worker_hash(published)
    assert not list(result_path.parent.glob(".result.json.*.tmp"))


def test_atomic_result_publication_keeps_existing_result_on_replace_failure(
    tmp_path,
    monkeypatch,
):
    from scripts.testing.official_openvino import quality_worker

    result_path = tmp_path / "result.json"
    result_path.write_bytes(b"previous-result")
    monkeypatch.setattr(
        quality_worker.os,
        "replace",
        lambda source, destination: (
            _ for _ in ()
        ).throw(OSError("replace failed")),
    )

    with pytest.raises(OSError, match="replace failed"):
        quality_worker._atomic_write_result(result_path, {"schema": "test"})

    assert result_path.read_bytes() == b"previous-result"
    assert not list(result_path.parent.glob(".result.json.*.tmp"))
