import hashlib
import json
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

from scripts.testing.official_openvino.guarded_build import (
    GUARD_SCHEMA,
    _effective_environment,
)
from scripts.testing.tests.test_official_openvino_quality_campaign import (
    _accepted_input,
)


MIB = 1024**2


def _canonical(value):
    return (
        json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
        + "\n"
    ).encode("utf-8")


def _signed_result(spec, *, fail=False):
    outcomes = []
    first_output = None
    for index, turn in enumerate(spec["turns"]):
        output = None if fail else f"output for {turn['turn_id']}"
        raw_prompt = turn["prompt"]
        if spec["prompt_id"] == "P6" and index == 1:
            history = first_output or ""
            raw_prompt = (
                f"User: {spec['turns'][0]['prompt'].strip()}\n"
                f"Assistant: {history}\n"
                f"User: {turn['prompt'].strip()}"
            )
        outcome = {
            "turn_id": turn["turn_id"],
            "raw_prompt": raw_prompt,
            "raw_prompt_sha256": hashlib.sha256(
                raw_prompt.encode("utf-8")
            ).hexdigest(),
            "status": "failed" if fail else "complete",
            "raw_output": output,
            "raw_output_sha256": (
                hashlib.sha256(output.encode("utf-8")).hexdigest()
                if output is not None
                else None
            ),
            "failure_type": "RuntimeError" if fail else None,
            "failure_message": "synthetic failure" if fail else None,
        }
        if spec["prompt_id"] == "P6" and index == 1:
            outcome["history_source_sha256"] = hashlib.sha256(
                (first_output or "").encode("utf-8")
            ).hexdigest()
        outcomes.append(outcome)
        if index == 0:
            first_output = output
    unsigned = {
        "schema": "official-openvino-adaptive-quality-prompt-result/v1",
        "prompt_id": spec["prompt_id"],
        "worker_spec_sha256": hashlib.sha256(_canonical(spec)).hexdigest(),
        "outcomes": outcomes,
    }
    return {
        **unsigned,
        "worker_result_sha256": hashlib.sha256(_canonical(unsigned)).hexdigest(),
    }


class RecordingGuardRunner:
    def __init__(self, failed_prompt=None):
        self.failed_prompt = failed_prompt
        self.calls = []

    def __call__(self, **kwargs):
        spec_path = Path(kwargs["bound_inputs"]["quality_worker_spec"])
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
        self.calls.append(
            SimpleNamespace(
                prompt_id=spec["prompt_id"],
                process_identity=1000 + len(self.calls),
                spec=spec,
            )
        )
        result_path = Path(kwargs["command"][-1])
        result_path.write_bytes(
            _canonical(
                _signed_result(
                    spec,
                    fail=spec["prompt_id"] == self.failed_prompt,
                )
            )
        )
        log_path = Path(kwargs["log_path"])
        log_path.write_bytes(b"fake guarded worker\n")
        _, environment_sha256 = _effective_environment(kwargs["environment"])
        evidence = {
            "schema": GUARD_SCHEMA,
            "run_id": hashlib.sha256(
                str(Path(kwargs["evidence_path"]).resolve()).encode("utf-8")
            ).hexdigest(),
            "command": [str(value) for value in kwargs["command"]],
            "working_directory": str(Path(kwargs["cwd"]).resolve()),
            "log_path": str(log_path.resolve()),
            "evidence_path": str(Path(kwargs["evidence_path"]).resolve()),
            "environment_sha256": environment_sha256,
            "bound_inputs": [
                {
                    "name": "quality_worker_spec",
                    "path": str(spec_path.resolve()),
                    "sha256": hashlib.sha256(spec_path.read_bytes()).hexdigest(),
                }
            ],
            "configured_minimum_available_ram_bytes": 2048 * MIB,
            "maximum_runtime_seconds": float(
                kwargs["limits"].maximum_runtime_seconds
            ),
            "root_pid": 1000 + len(self.calls),
            "timed_out": False,
            "low_memory_stop": False,
            "emergency_actions": [],
            "cleanup_process_count": 0,
            "exit_code": 0,
            "log_sha256": hashlib.sha256(log_path.read_bytes()).hexdigest(),
            "valid": True,
            "job_object": {
                "setup_ok": True,
                "query_ok": True,
                "terminate_job_called": False,
                "queried_active_process_count_after_cleanup": 0,
                "survivor_pids_after_cleanup": [],
            },
        }
        evidence_path = Path(kwargs["evidence_path"])
        evidence_path.write_bytes(
            (json.dumps(evidence, indent=2, sort_keys=True) + "\n").encode("utf-8")
        )
        return evidence


def test_prompt_worker_uses_one_pipeline_and_actual_p6_history(monkeypatch):
    from scripts.testing.official_openvino.quality_worker import (
        execute_quality_prompt_worker,
    )

    calls = []

    class Pipeline:
        def __init__(self, *_args, **_kwargs):
            pass

        def generate(self, prompt, _config):
            calls.append(prompt)
            return "actual turn one" if len(calls) == 1 else "actual turn two"

    monkeypatch.setitem(
        sys.modules,
        "openvino_genai",
        SimpleNamespace(LLMPipeline=Pipeline, GenerationConfig=type("Config", (), {})),
    )
    spec = {
        "schema": "official-openvino-adaptive-quality-prompt-spec/v1",
        "prompt_id": "P6",
        "model_path": "C:/model",
        "device": "CPU",
        "properties": {},
        "generation_settings": {
            "max_new_tokens": 256,
            "do_sample": False,
            "rng_seed": 42,
            "apply_chat_template": False,
        },
        "turns": [
            {"turn_id": "P6-turn-1", "prompt": "remember amber"},
            {
                "turn_id": "P6-turn-2",
                "prompt": "repeat it",
                "history_source_turn_id": "P6-turn-1",
            },
        ],
        "private_controller": {"test_id": "OV-11", "context_tokens": 512},
        "bindings": {"runtime_summary_sha256": "a" * 64},
    }

    result = execute_quality_prompt_worker(spec)

    assert len(calls) == 2
    assert calls[1] == (
        "User: remember amber\nAssistant: actual turn one\nUser: repeat it"
    )
    assert result["outcomes"][1]["history_source_sha256"] == hashlib.sha256(
        b"actual turn one"
    ).hexdigest()
    assert "test_id" not in result and "context_tokens" not in result


def test_capture_launches_six_fresh_workers_and_resume_relaunches_none(
    tmp_path, monkeypatch
):
    from scripts.testing.official_openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    first = RecordingGuardRunner()

    result = adaptive_quality.capture_isolated_quality_campaign(
        source, resume=False, run_command=first
    )

    assert [call.prompt_id for call in first.calls] == [
        "P1",
        "P2",
        "P3",
        "P4",
        "P5",
        "P6",
    ]
    assert len({call.process_identity for call in first.calls}) == 6
    assert result["prompt_receipt_count"] == 6
    assert result["status"] == "passed"
    assert all(len(call.spec["turns"]) == 1 for call in first.calls[:5])
    assert [turn["turn_id"] for turn in first.calls[-1].spec["turns"]] == [
        "P6-turn-1",
        "P6-turn-2",
    ]

    second = RecordingGuardRunner()
    resumed = adaptive_quality.capture_isolated_quality_campaign(
        source, resume=True, run_command=second
    )
    assert second.calls == []
    assert resumed == result


def test_failed_prompt_preserves_evidence_stops_and_has_no_scores(
    tmp_path, monkeypatch
):
    from scripts.testing.official_openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    runner = RecordingGuardRunner(failed_prompt="P4")

    result = adaptive_quality.capture_isolated_quality_campaign(
        source, resume=False, run_command=runner
    )

    assert [call.prompt_id for call in runner.calls] == ["P1", "P2", "P3", "P4"]
    assert result["status"] == "quality-blocked"
    assert result["completed_prompt_ids"] == ["P1", "P2", "P3"]
    assert "quality_mean" not in result
    for prompt_id in ("P1", "P2", "P3", "P4"):
        assert {
            path.name for path in (source.output_root / prompt_id).iterdir()
        } == {
            "worker-spec.json",
            "worker-result.json",
            "worker.log",
            "guard-evidence.json",
        }


def test_resume_appends_one_recovery_then_remaining_prompts(tmp_path, monkeypatch):
    from scripts.testing.official_openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=RecordingGuardRunner(failed_prompt="P4"),
    )
    original = (source.output_root / "P4" / "worker-result.json").read_bytes()
    resumed_runner = RecordingGuardRunner()

    result = adaptive_quality.capture_isolated_quality_campaign(
        source, resume=True, run_command=resumed_runner
    )

    assert [call.prompt_id for call in resumed_runner.calls] == ["P4", "P5", "P6"]
    assert result["status"] == "passed"
    assert (source.output_root / "P4" / "worker-result.json").read_bytes() == original
    assert (source.output_root / "P4-recovery-001").is_dir()
    assert result["capture_summary_path"].endswith("capture-summary-recovery-001.json")


def test_failed_recovery_is_not_retried_automatically(tmp_path, monkeypatch):
    from scripts.testing.official_openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    adaptive_quality.capture_isolated_quality_campaign(
        source,
        resume=False,
        run_command=RecordingGuardRunner(failed_prompt="P4"),
    )
    recovery = RecordingGuardRunner(failed_prompt="P4")
    blocked = adaptive_quality.capture_isolated_quality_campaign(
        source, resume=True, run_command=recovery
    )
    forbidden = RecordingGuardRunner()

    repeated = adaptive_quality.capture_isolated_quality_campaign(
        source, resume=True, run_command=forbidden
    )

    assert [call.prompt_id for call in recovery.calls] == ["P4"]
    assert blocked["status"] == "quality-blocked"
    assert repeated == blocked
    assert forbidden.calls == []


def test_resume_rejects_hash_consistent_summary_with_tampered_worker_spec(
    tmp_path, monkeypatch
):
    from scripts.testing.official_openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB)
    adaptive_quality.capture_isolated_quality_campaign(
        source, resume=False, run_command=RecordingGuardRunner()
    )
    spec_path = source.output_root / "P2" / "worker-spec.json"
    value = json.loads(spec_path.read_text(encoding="utf-8"))
    value["device"] = "GPU"
    spec_path.write_bytes(_canonical(value))

    with pytest.raises(ValueError, match="spec"):
        adaptive_quality.capture_isolated_quality_campaign(
            source, resume=True, run_command=RecordingGuardRunner()
        )


def test_prelaunch_ram_floor_rejects_before_creating_prompt_directory(
    tmp_path, monkeypatch
):
    from scripts.testing.official_openvino import adaptive_quality

    source = _accepted_input(tmp_path)
    monkeypatch.setattr(adaptive_quality, "available_ram_bytes", lambda: 4095 * MIB)

    with pytest.raises(RuntimeError, match="4096"):
        adaptive_quality.capture_isolated_quality_campaign(
            source, resume=False, run_command=RecordingGuardRunner()
        )

    assert not (source.output_root / "P1").exists()


def test_adaptive_quality_cli_campaign_mode_is_mutually_exclusive(tmp_path):
    from scripts.testing.run_official_openvino_adaptive_quality import main

    with pytest.raises(SystemExit):
        main(
            [
                "--campaign-state",
                str(tmp_path / "state.json"),
                "--resume-quality-blocked",
                "--runtime-summary",
                str(tmp_path / "summary.json"),
            ]
        )


def test_task_four_recovery_object_reopens_exact_native_runtime(tmp_path):
    from scripts.testing.official_openvino.adaptive_quality import (
        quality_campaign_input_from_recovery,
    )

    source = _accepted_input(tmp_path)
    summary_path = source.campaign_root / "measurement-summary.json"
    sequence_path = source.campaign_root / "attempt-sequence.json"
    summary = json.loads(summary_path.read_text(encoding="utf-8"))
    recovery = {
        "test_id": summary["test_id"],
        "context_tokens": summary["context_tokens"],
        "runtime_evidence_path": str(sequence_path.resolve()),
        "runtime_evidence_sha256": hashlib.sha256(sequence_path.read_bytes()).hexdigest(),
        "runtime_summary": str(summary_path.resolve()),
        "runtime_summary_sha256": hashlib.sha256(summary_path.read_bytes()).hexdigest(),
        "matrix": str(source.matrix_path.resolve()),
        "matrix_sha256": hashlib.sha256(source.matrix_path.read_bytes()).hexdigest(),
        "prompt_set": str(source.prompt_set_path.resolve()),
        "prompt_set_sha256": hashlib.sha256(source.prompt_set_path.read_bytes()).hexdigest(),
        "rubric": str(source.rubric_path.resolve()),
        "rubric_sha256": hashlib.sha256(source.rubric_path.read_bytes()).hexdigest(),
        "model_path": str(
            Path(
                json.loads(source.spec_path.read_text(encoding="utf-8"))["model_path"]
            ).resolve()
        ),
        "build_root": str(source.build_root.resolve()),
        "python_executable": str(source.python_executable.resolve()),
        "python_site_packages": str(source.python_site_packages.resolve()),
        "openvino_libraries": str(source.openvino_libraries.resolve()),
        "sampler_script": str(source.sampler_script.resolve()),
        "sampler_script_sha256": hashlib.sha256(source.sampler_script.read_bytes()).hexdigest(),
        "output_root": str(source.output_root.resolve()),
        "timeout_seconds": 1800.0,
    }

    reopened = quality_campaign_input_from_recovery(recovery)

    assert reopened.campaign_root == source.campaign_root
    assert reopened.spec_path == (
        source.campaign_root / "attempts" / "pilot" / "attempt-001" / "spec.json"
    ).resolve()
    assert reopened.output_root == source.output_root.resolve()
