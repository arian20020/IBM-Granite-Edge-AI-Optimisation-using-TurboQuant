"""Contract tests for hash-bound WB-04 adaptive artifact inventory."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

import pytest

import scripts.testing.campaigns.openvino.artifact_inventory as artifact_inventory
from scripts.testing.campaigns.openvino.artifact_inventory import (
    ArtifactBinding,
    prepare_adaptive_artifacts,
    run_guarded_fp16_preparation,
)


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_canonical_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n",
        encoding="utf-8",
    )


def make_valid_manifest(tmp_path: Path, *, precision: str, artifact_id: str) -> Path:
    """Build the same strict, hash-bound manifest shape as conversion tests."""

    artifact_root = tmp_path / artifact_id
    artifact_root.mkdir(exist_ok=True)
    element_type = {"f16": "f16", "u8": "u8", "u4": "i4"}[precision]
    files = {
        "openvino_model.xml": f'<net><data element_type="{element_type}"/></net>',
        "openvino_model.bin": "packed model",
        "openvino_tokenizer.xml": "<net/>",
        "openvino_tokenizer.bin": "tokenizer IR",
        "openvino_detokenizer.xml": "<net/>",
        "openvino_detokenizer.bin": "detokenizer IR",
        "tokenizer.json": '{"version":"1.0"}',
        "tokenizer_config.json": '{"model_max_length":4096}',
        "config.json": '{"model_type":"granite"}',
        "generation_config.json": '{"do_sample":false}',
        "README.md": "---\nlicense: apache-2.0\n---\nConverted model.\n",
    }
    if precision != "f16":
        files["openvino_config.json"] = '{"optimum_version":"2.1.0"}'
    for name, contents in files.items():
        (artifact_root / name).write_text(contents, encoding="utf-8")
    inventory = [
        {
            "path": path.name,
            "size_bytes": path.stat().st_size,
            "sha256": sha256_file(path),
        }
        for path in sorted(artifact_root.iterdir())
        if path.is_file()
    ]
    inventory.sort(key=lambda item: item["path"])
    inventory_sha256 = hashlib.sha256(
        json.dumps(inventory, sort_keys=True, separators=(",", ":")).encode("utf-8")
    ).hexdigest()
    readme_sha256 = next(item["sha256"] for item in inventory if item["path"] == "README.md")
    xml_sha256 = next(
        item["sha256"] for item in inventory if item["path"] == "openvino_model.xml"
    )
    log = tmp_path / f"{artifact_id}.log"
    log.write_text("CPU generation passed\n", encoding="utf-8")
    output = "Granite probe output"
    manifest = {
        "schema_version": 1,
        "status": "load-proven",
        "artifact_id": artifact_id,
        "artifact_root": str(artifact_root),
        "model": {
            "family": "granite-4.1",
            "parameter_scale": "3b",
            "precision": precision,
            "source_repository": "ibm-granite/granite-4.1-3b",
            "source_revision": "a" * 40,
            "artifact_repository": f"publisher/granite-4.1-3b-{precision}-ov",
            "artifact_revision": "b" * 40,
        },
        "conversion": {
            "kind": "published-preconverted",
            "command": ["optimum-cli", "export", "openvino", "--weight-format", precision],
            "tool_versions": {"optimum-intel": "2.1.0", "transformers": "5.5.0"},
            "provenance_path": "README.md",
            "provenance_sha256": readme_sha256,
        },
        "files": inventory,
        "inventory_sha256": inventory_sha256,
        "precision_proof": {
            "path": "openvino_model.xml",
            "sha256": xml_sha256,
            "element_type": element_type,
            "element_type_count": 1,
        },
        "license": {"spdx": "Apache-2.0", "path": "README.md", "sha256": readme_sha256},
        "load_probe": {
            "status": "passed",
            "device_requested": "CPU",
            "device_actual": "CPU",
            "fallback": False,
            "model_path": str(artifact_root),
            "command": ["probe.exe", "--model", str(artifact_root), "--device", "CPU"],
            "generated_tokens": 4,
            "output": output,
            "output_sha256": hashlib.sha256(output.encode("utf-8")).hexdigest(),
            "artifact_inventory_sha256": inventory_sha256,
            "runtime_build_manifest_sha256": "c" * 64,
            "exit_code": 0,
            "cleanup_process_count": 0,
            "log_path": str(log),
            "log_sha256": sha256_file(log),
        },
    }
    path = tmp_path / f"{artifact_id}-manifest.json"
    write_canonical_json(path, manifest)
    return path


def terminal_fp16(output: Path, classification: str) -> ArtifactBinding:
    receipt = output / "fp16-terminal.json"
    write_canonical_json(receipt, {"classification": classification})
    return ArtifactBinding(
        precision="f16",
        status="artifact-preparation-terminal",
        artifact_id=None,
        model_root=None,
        manifest_path=None,
        manifest_sha256=None,
        terminal_stage="artifact-preparation",
        terminal_receipt_path=receipt,
        terminal_receipt_sha256=sha256_file(receipt),
    )


def base_inputs(tmp_path: Path) -> dict[str, object]:
    return {
        "u4_manifest": make_valid_manifest(tmp_path, precision="u4", artifact_id="u4-a"),
        "u8_manifest": make_valid_manifest(tmp_path, precision="u8", artifact_id="u8-a"),
        "fp16_manifest": None,
        "output_root": tmp_path / "inventory",
        "prepare_fp16": lambda output: terminal_fp16(output, "not-present"),
    }


def prepared_inventory(tmp_path: Path, *, fp16_status: str):
    inputs = base_inputs(tmp_path)
    inputs["prepare_fp16"] = lambda output: terminal_fp16(output, fp16_status)
    return prepare_adaptive_artifacts(
        **inputs,
    )


def test_existing_u4_and_u8_manifests_are_hash_bound(tmp_path: Path) -> None:
    u4 = make_valid_manifest(tmp_path, precision="u4", artifact_id="u4-a")
    u8 = make_valid_manifest(tmp_path, precision="u8", artifact_id="u8-a")
    result = prepare_adaptive_artifacts(
        u4_manifest=u4,
        u8_manifest=u8,
        fp16_manifest=None,
        output_root=tmp_path / "inventory",
        prepare_fp16=lambda output: terminal_fp16(output, "not-present"),
    )
    assert result.bindings["u4"].manifest_sha256 == sha256_file(u4)
    assert result.bindings["u8"].manifest_sha256 == sha256_file(u8)


def test_fp16_preparation_failure_is_not_an_inference_failure(tmp_path: Path) -> None:
    result = prepared_inventory(tmp_path, fp16_status="memory-gate-not-run")
    fp16 = result.bindings["f16"]
    assert fp16.status == "artifact-preparation-terminal"
    assert fp16.terminal_stage == "artifact-preparation"
    assert fp16.terminal_receipt_sha256 == sha256_file(fp16.terminal_receipt_path)


def test_fp16_executor_requires_4096_mib_and_never_lowers_runtime_floor(
    tmp_path: Path,
) -> None:
    with pytest.raises(ValueError, match="launch reserve"):
        prepare_adaptive_artifacts(
            **base_inputs(tmp_path),
            launch_reserve_mib=4095,
            emergency_floor_mib=2048,
        )
    with pytest.raises(ValueError, match="emergency floor"):
        prepare_adaptive_artifacts(
            **base_inputs(tmp_path),
            launch_reserve_mib=4096,
            emergency_floor_mib=2047,
        )


def test_direct_fp16_executor_rejects_unsafe_thresholds(tmp_path: Path) -> None:
    with pytest.raises(ValueError, match="launch reserve"):
        run_guarded_fp16_preparation(
            tmp_path / "launch",
            launch_reserve_mib=4095,
            emergency_floor_mib=2048,
        )
    with pytest.raises(ValueError, match="emergency floor"):
        run_guarded_fp16_preparation(
            tmp_path / "floor",
            launch_reserve_mib=4096,
            emergency_floor_mib=2047,
        )


def test_fp16_child_stops_at_emergency_floor_and_records_sampled_evidence(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    class FakeProcess:
        pid = 1234
        returncode = 0

        def poll(self):
            return None

        def wait(self, timeout: float) -> None:
            return None

    class FakeJob:
        def __init__(self, name: str) -> None:
            self.terminated = []

        def assign_pid(self, pid: int) -> None:
            assert pid == 1234

        def terminate(self, exit_code: int) -> None:
            self.terminated.append(exit_code)

    job = FakeJob("test")

    def fake_popen(command: list[str], **kwargs: object) -> FakeProcess:
        result = Path(command[command.index("--result") + 1])
        write_canonical_json(result, {"classification": "fp16-manifest-not-provided"})
        return FakeProcess()

    samples = iter([4096 * artifact_inventory.MIB, 2047 * artifact_inventory.MIB, 4096 * artifact_inventory.MIB])
    monkeypatch.setattr(artifact_inventory, "available_ram_bytes", lambda: next(samples))
    monkeypatch.setattr(artifact_inventory, "KillOnCloseJob", lambda name: job)
    monkeypatch.setattr(artifact_inventory.subprocess, "Popen", fake_popen)
    monkeypatch.setattr(artifact_inventory, "_resume_suspended_process", lambda pid: None)
    monkeypatch.setattr(artifact_inventory, "_wait_process", lambda *args: True)
    monkeypatch.setattr(
        artifact_inventory,
        "_cleanup_job",
        lambda *args: ({"queried_active_process_count_after_cleanup": 0}, False),
    )
    monkeypatch.setattr(artifact_inventory, "_close_run_resources", lambda *args: None)

    binding = run_guarded_fp16_preparation(tmp_path / "prepared")

    memory = json.loads((tmp_path / "prepared" / "memory.json").read_text(encoding="utf-8"))
    cleanup = json.loads((tmp_path / "prepared" / "cleanup.json").read_text(encoding="utf-8"))
    assert job.terminated == [137]
    assert memory["available_ram_bytes_samples"] == [2047 * artifact_inventory.MIB]
    assert cleanup["emergency_floor_triggered"] is True
    assert binding.terminal_receipt_sha256 == sha256_file(binding.terminal_receipt_path)


def test_fp16_child_passes_and_hashes_exact_environment(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    captured: dict[str, object] = {}

    class FakeProcess:
        pid = 1234
        returncode = 0

        def poll(self):
            return 0

        def wait(self, timeout: float) -> None:
            return None

    class FakeJob:
        def assign_pid(self, pid: int) -> None:
            pass

    def fake_popen(command: list[str], **kwargs: object) -> FakeProcess:
        captured["environment"] = kwargs.get("env")
        result = Path(command[command.index("--result") + 1])
        write_canonical_json(result, {"classification": "fp16-manifest-not-provided"})
        return FakeProcess()

    expected_environment = dict(artifact_inventory.os.environ)
    monkeypatch.setattr(artifact_inventory, "available_ram_bytes", lambda: 4096 * artifact_inventory.MIB)
    monkeypatch.setattr(artifact_inventory, "KillOnCloseJob", lambda name: FakeJob())
    monkeypatch.setattr(artifact_inventory.subprocess, "Popen", fake_popen)
    monkeypatch.setattr(artifact_inventory, "_resume_suspended_process", lambda pid: None)
    monkeypatch.setattr(
        artifact_inventory,
        "_cleanup_job",
        lambda *args: ({"queried_active_process_count_after_cleanup": 0}, False),
    )
    monkeypatch.setattr(artifact_inventory, "_close_run_resources", lambda *args: None)

    run_guarded_fp16_preparation(tmp_path / "prepared")

    environment_path = tmp_path / "prepared" / "environment.json"
    receipt = json.loads((tmp_path / "prepared" / "terminal-receipt.json").read_text(encoding="utf-8"))
    assert captured["environment"] == expected_environment
    assert json.loads(environment_path.read_text(encoding="utf-8")) == expected_environment
    assert receipt["environment_sha256"] == sha256_file(environment_path)
