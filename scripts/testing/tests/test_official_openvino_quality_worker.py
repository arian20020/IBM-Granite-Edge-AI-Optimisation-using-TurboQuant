import builtins
import hashlib
import json
import os
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


def _prompt_worker_cli_fixture(tmp_path):
    from scripts.testing.official_openvino.adaptive_quality import (
        build_quality_prompt_worker_spec,
    )
    from scripts.testing.official_openvino.quality_campaign import (
        load_accepted_quality_campaign,
    )
    from scripts.testing.tests.test_official_openvino_quality_campaign import (
        _accepted_input,
    )

    campaign = load_accepted_quality_campaign(_accepted_input(tmp_path))
    spec = build_quality_prompt_worker_spec(campaign, "P1")
    command = spec["bindings"]["command"]
    spec_path = Path(command[4])
    result_path = Path(command[6])
    spec_path.parent.mkdir(parents=True, exist_ok=True)
    spec_path.write_text(json.dumps(spec), encoding="utf-8")
    return spec_path, result_path


def _set_prompt_worker_authority(
    monkeypatch,
    spec_path,
    *,
    authority_path=None,
    authority_sha256=None,
):
    source = Path(spec_path)
    monkeypatch.setenv(
        "OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_PATH",
        str(Path(authority_path or source).resolve()),
    )
    monkeypatch.setenv(
        "OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_SHA256",
        authority_sha256 or hashlib.sha256(source.read_bytes()).hexdigest(),
    )


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
    ("substitution", "expected_error"),
    (
        ("spec", "spec path authority"),
        ("result", "result path authority"),
        ("interpreter", "bound command"),
    ),
)
def test_prompt_worker_cli_rejects_bound_command_substitution_before_import(
    tmp_path,
    monkeypatch,
    substitution,
    expected_error,
):
    from scripts.testing.official_openvino import quality_worker

    spec_path, result_path = _prompt_worker_cli_fixture(tmp_path)
    actual_spec_path = spec_path
    actual_result_path = result_path
    if substitution == "spec":
        actual_spec_path = tmp_path / "copied" / "worker-spec.json"
        actual_spec_path.parent.mkdir(parents=True)
        actual_spec_path.write_bytes(spec_path.read_bytes())
    elif substitution == "result":
        actual_result_path = tmp_path / "copied" / "worker-result.json"
    else:
        monkeypatch.setattr(
            sys,
            "executable",
            str(tmp_path / "substituted-python.exe"),
        )
    _set_prompt_worker_authority(monkeypatch, spec_path)

    imported = []
    real_import = builtins.__import__

    def reject_openvino_import(name, *args, **kwargs):
        if name == "openvino_genai":
            imported.append(name)
            raise AssertionError("OpenVINO import must follow CLI binding validation")
        return real_import(name, *args, **kwargs)

    monkeypatch.setattr(builtins, "__import__", reject_openvino_import)

    with pytest.raises(ValueError, match=expected_error):
        quality_worker.main(
            [
                "--spec",
                str(actual_spec_path),
                "--result",
                str(actual_result_path),
            ]
        )

    assert imported == []
    assert not actual_result_path.exists()


def test_prompt_worker_cli_accepts_exact_bound_command(tmp_path, monkeypatch):
    from scripts.testing.official_openvino import quality_worker

    spec_path, result_path = _prompt_worker_cli_fixture(tmp_path)
    _set_prompt_worker_authority(monkeypatch, spec_path)
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)

    assert quality_worker.main(
        ["--spec", str(spec_path), "--result", str(result_path)]
    ) == 0

    published = json.loads(result_path.read_text(encoding="utf-8"))
    assert published["schema"] == quality_worker.PROMPT_RESULT_SCHEMA
    assert published["prompt_id"] == "P1"


@pytest.mark.parametrize(
    "authority_failure",
    ("missing_path", "missing_sha256", "wrong_path", "wrong_sha256"),
)
def test_prompt_worker_cli_rejects_missing_or_wrong_guard_authority_before_import(
    tmp_path,
    monkeypatch,
    authority_failure,
):
    from scripts.testing.official_openvino import quality_worker

    spec_path, result_path = _prompt_worker_cli_fixture(tmp_path)
    _set_prompt_worker_authority(monkeypatch, spec_path)
    if authority_failure == "missing_path":
        monkeypatch.delenv(
            "OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_PATH"
        )
    elif authority_failure == "missing_sha256":
        monkeypatch.delenv(
            "OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_SHA256"
        )
    elif authority_failure == "wrong_path":
        copied = tmp_path / "wrong-authority" / "worker-spec.json"
        copied.parent.mkdir(parents=True)
        copied.write_bytes(spec_path.read_bytes())
        monkeypatch.setenv(
            "OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_PATH",
            str(copied.resolve()),
        )
    else:
        monkeypatch.setenv(
            "OFFICIAL_OPENVINO_GUARD_BOUND_QUALITY_WORKER_SPEC_SHA256",
            "0" * 64,
        )

    imported = []
    real_import = builtins.__import__

    def reject_openvino_import(name, *args, **kwargs):
        if name == "openvino_genai":
            imported.append(name)
            raise AssertionError("OpenVINO import must follow guard authority")
        return real_import(name, *args, **kwargs)

    monkeypatch.setattr(builtins, "__import__", reject_openvino_import)

    with pytest.raises(ValueError, match="authority"):
        quality_worker.main(
            ["--spec", str(spec_path), "--result", str(result_path)]
        )

    assert imported == []
    assert not result_path.exists()


def test_prompt_worker_cli_rejects_resigned_copied_spec_before_import(
    tmp_path,
    monkeypatch,
):
    from scripts.testing.official_openvino import quality_worker

    spec_path, _result_path = _prompt_worker_cli_fixture(tmp_path)
    copied_spec_path = tmp_path / "copied" / "P1" / "worker-spec.json"
    copied_result_path = copied_spec_path.with_name("worker-result.json")
    copied_spec_path.parent.mkdir(parents=True)
    copied_spec = json.loads(spec_path.read_text(encoding="utf-8"))
    copied_command = copied_spec["bindings"]["command"]
    copied_command[4] = str(copied_spec_path.resolve())
    copied_command[6] = str(copied_result_path.resolve())
    copied_spec["bindings"]["command_sha256"] = hashlib.sha256(
        json.dumps(
            copied_command,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
        ).encode("utf-8")
    ).hexdigest()
    copied_spec_path.write_text(json.dumps(copied_spec), encoding="utf-8")
    _set_prompt_worker_authority(monkeypatch, spec_path)

    imported = []
    real_import = builtins.__import__

    def reject_openvino_import(name, *args, **kwargs):
        if name == "openvino_genai":
            imported.append(name)
            raise AssertionError("OpenVINO import must follow guard authority")
        return real_import(name, *args, **kwargs)

    monkeypatch.setattr(builtins, "__import__", reject_openvino_import)

    with pytest.raises(ValueError, match="spec path authority"):
        quality_worker.main(
            [
                "--spec",
                str(copied_spec_path),
                "--result",
                str(copied_result_path),
            ]
        )

    assert imported == []
    assert not copied_result_path.exists()


@pytest.mark.parametrize(
    ("mutation", "expected_error"),
    (
        ("paths", "bound command"),
        ("interpreter", "command is not expected"),
    ),
)
def test_prompt_worker_cli_rejects_resigned_bound_command_before_import(
    tmp_path,
    monkeypatch,
    mutation,
    expected_error,
):
    from scripts.testing.official_openvino import quality_worker

    spec_path, result_path = _prompt_worker_cli_fixture(tmp_path)
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    command = spec["bindings"]["command"]
    if mutation == "paths":
        alternate_root = tmp_path / "resigned-command" / "P1"
        command[4] = str((alternate_root / "worker-spec.json").resolve())
        command[6] = str((alternate_root / "worker-result.json").resolve())
    else:
        command[0] = str((tmp_path / "resigned-python.exe").resolve())
    spec["bindings"]["command_sha256"] = hashlib.sha256(
        json.dumps(
            command,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
        ).encode("utf-8")
    ).hexdigest()
    spec_path.write_text(json.dumps(spec), encoding="utf-8")
    _set_prompt_worker_authority(monkeypatch, spec_path)

    imported = []
    real_import = builtins.__import__

    def reject_openvino_import(name, *args, **kwargs):
        if name == "openvino_genai":
            imported.append(name)
            raise AssertionError("OpenVINO import must follow command validation")
        return real_import(name, *args, **kwargs)

    monkeypatch.setattr(builtins, "__import__", reject_openvino_import)

    with pytest.raises(ValueError, match=expected_error):
        quality_worker.main(
            ["--spec", str(spec_path), "--result", str(result_path)]
        )

    assert imported == []
    assert not result_path.exists()


def test_prompt_worker_publishes_to_canonical_derived_result_path(
    tmp_path,
    monkeypatch,
):
    from scripts.testing.official_openvino import quality_worker

    spec_path, result_path = _prompt_worker_cli_fixture(tmp_path)
    _set_prompt_worker_authority(monkeypatch, spec_path)
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)
    monkeypatch.chdir(tmp_path)
    lexical_result = Path(os.path.relpath(result_path, tmp_path))
    published_paths = []
    real_publish = quality_worker._atomic_write_result

    def capture_publish(path, result):
        published_paths.append(Path(path))
        real_publish(path, result)

    monkeypatch.setattr(quality_worker, "_atomic_write_result", capture_publish)

    assert quality_worker.main(
        [
            "--spec",
            os.path.relpath(spec_path, tmp_path),
            "--result",
            str(lexical_result),
        ]
    ) == 0

    assert published_paths == [result_path.resolve()]
    assert result_path.is_file()


def test_prompt_worker_rejects_result_alias_instead_of_deriving_through_it(
    tmp_path,
    monkeypatch,
):
    from scripts.testing.official_openvino import quality_worker

    spec_path = tmp_path / "P1" / "worker-spec.json"
    spec_path.parent.mkdir(parents=True)
    spec_bytes = b'{"schema":"prompt"}\n'
    spec_path.write_bytes(spec_bytes)
    result_path = spec_path.with_name("worker-result.json")
    alias_target = tmp_path / "outside" / "redirected-result.json"
    _set_prompt_worker_authority(monkeypatch, spec_path)
    real_resolve = Path.resolve

    def resolve_result_alias(path, *args, **kwargs):
        if path == result_path:
            return alias_target
        return real_resolve(path, *args, **kwargs)

    monkeypatch.setattr(Path, "resolve", resolve_result_alias)

    with pytest.raises(ValueError, match="result path authority"):
        quality_worker._require_prompt_worker_guard_authority(
            spec_path=spec_path,
            result_path=result_path,
            spec_bytes=spec_bytes,
        )


def test_guarded_worker_rejects_schema_downgrade_with_wrong_authority_before_import(
    tmp_path,
    monkeypatch,
):
    from scripts.testing.official_openvino import quality_worker

    spec_path = tmp_path / "governed" / "worker-spec.json"
    result_path = spec_path.with_name("worker-result.json")
    spec_path.parent.mkdir(parents=True)
    spec_path.write_text(json.dumps(_spec()), encoding="utf-8")
    _set_prompt_worker_authority(
        monkeypatch,
        spec_path,
        authority_sha256="0" * 64,
    )
    imported = []
    real_import = builtins.__import__

    def reject_openvino_import(name, *args, **kwargs):
        if name == "openvino_genai":
            imported.append(name)
            raise AssertionError("OpenVINO import must follow guard authority")
        return real_import(name, *args, **kwargs)

    monkeypatch.setattr(builtins, "__import__", reject_openvino_import)

    with pytest.raises(ValueError, match="authority"):
        quality_worker.main(
            ["--spec", str(spec_path), "--result", str(result_path)]
        )

    assert imported == []
    assert not result_path.exists()


def test_historical_worker_accepts_complete_guard_authority(tmp_path, monkeypatch):
    from scripts.testing.official_openvino import quality_worker

    spec_path = tmp_path / "governed" / "worker-spec.json"
    result_path = spec_path.with_name("worker-result.json")
    spec_path.parent.mkdir(parents=True)
    spec_path.write_text(json.dumps(_spec()), encoding="utf-8")
    _set_prompt_worker_authority(monkeypatch, spec_path)
    fake = _FakeGenAI()
    monkeypatch.setitem(sys.modules, "openvino_genai", fake.module)

    assert quality_worker.main(
        ["--spec", str(spec_path), "--result", str(result_path)]
    ) == 0
    assert json.loads(result_path.read_text(encoding="utf-8"))["schema"] == (
        quality_worker.RESULT_SCHEMA
    )


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
