"""Policy and orchestration tests for the adaptive comparison controller."""

from __future__ import annotations

import base64
import hashlib
import inspect
import json
import os
from dataclasses import replace
from pathlib import Path
import subprocess
import sys
from types import CodeType

import pytest

from scripts.testing import build_official_openvino_boundary_index as boundary_cli
from scripts.testing import run_official_openvino_adaptive_comparison as campaign_cli
from scripts.testing.campaigns.openvino import adaptive_campaign as adaptive_controller
from scripts.testing.campaigns.openvino import runtime_measurement
from scripts.testing.campaigns.openvino import runtime_process
from scripts.testing.build_official_openvino_adaptive_matrix import (
    build_adaptive_comparison_matrix,
)
from scripts.testing.measure_official_openvino import (
    CampaignLock,
    MeasurementFailureRecord,
    MeasurementSequenceFailure,
    _sequence_spec,
)
from scripts.testing.campaigns.openvino.adaptive_campaign_spec import (
    generate_adaptive_format_comparison_specs,
)
from scripts.testing.campaigns.openvino.adaptive_campaign import (
    AdaptiveCampaignConfig,
    CANDIDATE_ORDER,
    CONTEXTS,
    START_RESERVE_MIB,
    build_boundary_index,
    build_ladder,
    campaign_status,
    eligible_steps,
    load_or_create_state,
    preflight_adaptive_campaign,
    run_adaptive_campaign,
)
from scripts.testing.campaigns.openvino.conversion import validate_artifact_manifest
from scripts.testing.campaigns.openvino.owned_process_guard import (
    CREATE_SUSPENDED,
    KillOnCloseJob,
)


ROOT = Path(__file__).resolve().parents[3]
HISTORICAL_MATRIX = ROOT / "experiments/manifests/official-openvino/retest-matrix.json"


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _sha256_json(value: object) -> str:
    encoded = json.dumps(
        value,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
        allow_nan=False,
    ).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(value, sort_keys=True, separators=(",", ":")) + "\n",
        encoding="utf-8",
    )


def _binding(
    tmp_path: Path,
    precision: str,
    *,
    manifest_change: str | None = None,
) -> dict[str, object | None]:
    model_root = tmp_path / f"{precision}-model"
    model_root.mkdir()
    element_type = {"f16": "f16", "u8": "u8", "u4": "i4"}[precision]
    files = {
        "openvino_model.xml": (
            f'<net><data element_type="{element_type}"/></net>'
        ),
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
        (model_root / name).write_text(contents, encoding="utf-8")
    inventory = [
        {
            "path": source.name,
            "size_bytes": source.stat().st_size,
            "sha256": _sha256(source),
        }
        for source in sorted(model_root.iterdir())
        if source.is_file()
    ]
    inventory.sort(key=lambda item: item["path"])
    inventory_sha256 = _sha256_json(inventory)
    readme_sha256 = next(
        item["sha256"] for item in inventory if item["path"] == "README.md"
    )
    xml_sha256 = next(
        item["sha256"]
        for item in inventory
        if item["path"] == "openvino_model.xml"
    )
    load_probe_log = tmp_path / f"{precision}-load-probe.log"
    load_probe_log.write_text("CPU generation passed\n", encoding="utf-8")
    output = f"Granite {precision} probe output"
    artifact_id = f"artifact-{precision}"
    manifest_payload: dict[str, object] = {
        "schema_version": 1,
        "status": "load-proven",
        "artifact_id": artifact_id,
        "artifact_root": str(model_root.resolve()),
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
            "command": [
                "optimum-cli",
                "export",
                "openvino",
                "--weight-format",
                precision,
            ],
            "tool_versions": {
                "optimum-intel": "2.1.0",
                "transformers": "5.5.0",
            },
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
        "license": {
            "spdx": "Apache-2.0",
            "path": "README.md",
            "sha256": readme_sha256,
        },
        "load_probe": {
            "status": "passed",
            "device_requested": "CPU",
            "device_actual": "CPU",
            "fallback": False,
            "model_path": str(model_root.resolve()),
            "command": [
                "probe.exe",
                "--model",
                str(model_root.resolve()),
                "--device",
                "CPU",
            ],
            "generated_tokens": 4,
            "output": output,
            "output_sha256": hashlib.sha256(output.encode("utf-8")).hexdigest(),
            "artifact_inventory_sha256": inventory_sha256,
            "runtime_build_manifest_sha256": "c" * 64,
            "exit_code": 0,
            "cleanup_process_count": 0,
            "log_path": str(load_probe_log.resolve()),
            "log_sha256": _sha256(load_probe_log),
        },
    }
    if manifest_change == "minimal":
        manifest_payload = {
            "status": "load-proven",
            "artifact_id": artifact_id,
            "artifact_root": str(model_root.resolve()),
            "model": {"precision": precision},
            "inventory_sha256": inventory_sha256,
            "load_probe": {"generated_tokens": 4},
        }
    elif manifest_change == "missing-schema":
        manifest_payload.pop("schema_version")
    elif manifest_change == "missing-status":
        manifest_payload.pop("status")
    elif manifest_change == "missing-conversion-provenance":
        conversion = manifest_payload["conversion"]
        assert isinstance(conversion, dict)
        conversion.pop("provenance_path")
    elif manifest_change == "missing-file-inventory":
        manifest_payload.pop("files")
    elif manifest_change == "invalid-cpu-load-probe-binding":
        load_probe = manifest_payload["load_probe"]
        assert isinstance(load_probe, dict)
        load_probe["device_actual"] = "GPU"
    elif manifest_change is not None:  # pragma: no cover - test helper contract
        raise AssertionError(f"unsupported manifest mutation: {manifest_change}")
    manifest = tmp_path / f"{precision}-manifest.json"
    _write_json(manifest, manifest_payload)
    return {
        "precision": precision,
        "status": "available",
        "artifact_id": artifact_id,
        "model_root": str(model_root),
        "manifest_path": str(manifest),
        "manifest_sha256": _sha256(manifest),
        "terminal_stage": None,
        "terminal_receipt_path": None,
        "terminal_receipt_sha256": None,
    }


def _build_matrix(
    tmp_path: Path,
    *,
    u8_manifest_change: str | None = None,
) -> Path:
    inventory = tmp_path / "artifact-inventory.json"
    _write_json(
        inventory,
        {
            "schema": "official-openvino-adaptive-artifact-inventory/v1",
            "launch_reserve_mib": 4096,
            "emergency_floor_mib": 2048,
            "bindings": {
                precision: _binding(
                    tmp_path,
                    precision,
                    manifest_change=(
                        u8_manifest_change if precision == "u8" else None
                    ),
                )
                for precision in ("u4", "u8", "f16")
            },
        },
    )
    output = tmp_path / "matrix.json"
    build_adaptive_comparison_matrix(
        artifact_inventory_path=inventory,
        historical_matrix_path=HISTORICAL_MATRIX,
        output_path=output,
    )
    return output


@pytest.fixture
def matrix(tmp_path: Path) -> Path:
    return _build_matrix(tmp_path)


def _empty_state() -> dict[str, object]:
    return {"steps": {}}


def _passing_step(*, quality: str = "passed") -> dict[str, object]:
    return {
        "runtime_status": "passed",
        "quality_status": quality,
        "attempt_count": 1,
    }


def test_ladder_runs_breadth_first_in_expected_memory_order(matrix: Path) -> None:
    assert build_ladder(matrix)[:10] == (
        ("OV-11", 512),
        ("OV-TQ-22", 512),
        ("OV-TQ-21", 512),
        ("OV-12", 512),
        ("OV-13", 512),
        ("OV-11", 1024),
        ("OV-TQ-22", 1024),
        ("OV-TQ-21", 1024),
        ("OV-12", 1024),
        ("OV-13", 1024),
    )
    assert build_ladder(matrix) == tuple(
        (test_id, context)
        for context in CONTEXTS
        for test_id in CANDIDATE_ORDER
    )


def test_quality_blocked_does_not_stop_runtime_promotion() -> None:
    state = _empty_state()
    state["steps"]["OV-11:512"] = _passing_step(quality="quality-blocked")

    assert ("OV-11", 1024) in eligible_steps(state)


def test_confirmed_boundary_prunes_only_that_candidate() -> None:
    state = _empty_state()
    for test_id in CANDIDATE_ORDER:
        state["steps"][f"{test_id}:512"] = _passing_step()
    state["steps"]["OV-12:1024"] = {
        "runtime_status": "boundary-confirmed",
        "quality_status": "not-run",
        "attempt_count": 2,
    }
    state["steps"]["OV-11:1024"] = _passing_step()

    remaining = eligible_steps(state)

    assert ("OV-12", 2048) not in remaining
    assert ("OV-11", 2048) in remaining


def test_no_intermediate_level_can_be_skipped() -> None:
    state = _empty_state()
    state["steps"]["OV-11:512"] = _passing_step()

    assert ("OV-11", 1024) in eligible_steps(state)
    assert ("OV-11", 2048) not in eligible_steps(state)


def _campaign_inputs(
    tmp_path: Path,
    *,
    max_context: int = 512,
    fp16_terminal: bool = False,
    u8_manifest_change: str | None = None,
) -> AdaptiveCampaignConfig:
    if fp16_terminal:
        terminal_receipt = tmp_path / "f16-terminal.json"
        _write_json(
            terminal_receipt,
            {
                "schema": "official-openvino-artifact-preparation-terminal/v1",
                "status": "artifact-preparation-terminal",
            },
        )
        inventory_path = tmp_path / "artifact-inventory.json"
        _write_json(
            inventory_path,
            {
                "schema": "official-openvino-adaptive-artifact-inventory/v1",
                "launch_reserve_mib": 4096,
                "emergency_floor_mib": 2048,
                "bindings": {
                    "u4": _binding(tmp_path, "u4"),
                    "u8": _binding(
                        tmp_path,
                        "u8",
                        manifest_change=u8_manifest_change,
                    ),
                    "f16": {
                        "precision": "f16",
                        "status": "artifact-preparation-terminal",
                        "artifact_id": None,
                        "model_root": None,
                        "manifest_path": None,
                        "manifest_sha256": None,
                        "terminal_stage": "artifact-preparation",
                        "terminal_receipt_path": str(terminal_receipt),
                        "terminal_receipt_sha256": _sha256(terminal_receipt),
                    },
                },
            },
        )
        matrix_path = tmp_path / "matrix.json"
        build_adaptive_comparison_matrix(
            artifact_inventory_path=inventory_path,
            historical_matrix_path=HISTORICAL_MATRIX,
            output_path=matrix_path,
        )
    else:
        matrix_path = _build_matrix(
            tmp_path,
            u8_manifest_change=u8_manifest_change,
        )
    inventory_path = tmp_path / "artifact-inventory.json"
    build_root = tmp_path / "build"
    package = build_root / "openvino_genai"
    package.mkdir(parents=True)
    (package / "__init__.py").write_text("", encoding="utf-8")
    (package / "py_openvino_genai.pyd").write_bytes(b"extension")
    (package / "openvino_genai.dll").write_bytes(b"runtime")
    cache_root = tmp_path / "cache"
    cache_root.mkdir()
    spec_root = tmp_path / "specs"
    generate_adaptive_format_comparison_specs(
        matrix_path=matrix_path,
        build_root=build_root,
        artifact_inventory_path=inventory_path,
        cache_root=cache_root,
        output_root=spec_root,
    )
    build_provenance = tmp_path / "build-provenance.json"
    _write_json(build_provenance, {"schema": "test-build", "status": "passed"})
    python_executable = tmp_path / "python.exe"
    python_executable.write_bytes(b"python")
    site_packages = tmp_path / "site-packages"
    site_packages.mkdir()
    openvino_libraries = tmp_path / "openvino-libraries"
    openvino_libraries.mkdir()
    sampler = tmp_path / "sampler.ps1"
    sampler.write_text("# sampler\n", encoding="utf-8")
    return AdaptiveCampaignConfig(
        matrix_path=matrix_path,
        spec_root=spec_root,
        campaign_root=tmp_path / "campaign",
        build_root=build_root,
        build_provenance_path=build_provenance,
        python_executable=python_executable,
        python_site_packages=site_packages,
        openvino_libraries=openvino_libraries,
        sampler_script=sampler,
        max_context=max_context,
    )


def test_generated_adaptive_spec_is_consumable_by_sequence_loader(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    generated = config.spec_root / "OV-11" / "512" / "runtime-spec.json"

    projected = _sequence_spec(generated)

    assert projected["schema"] == "official-openvino-wb04-worker-spec/v1"
    assert projected["role"] == "pilot"
    assert projected["controlled_test_id"] == "OV-11"
    assert projected["context"] == 512
    assert projected["expected_input_tokens"] == 512
    assert projected["prompt"] == " test" * 512


@pytest.mark.skipif(os.name != "nt", reason="Windows MAX_PATH is required")
def test_adaptive_runtime_root_keeps_atomic_evidence_below_max_path(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    campaign = tmp_path
    while len(str(campaign)) < 188:
        remaining = 188 - len(str(campaign)) - 1
        campaign /= "x" * min(40, remaining)
    compact = adaptive_controller._adaptive_runtime_root(
        campaign, "OV-TQ-22", 8192
    )
    legacy = campaign / "runtime" / "OV-TQ-22" / "context-8192"
    compact_attempt = compact / "attempts" / "sample-3" / "attempt-001"
    legacy_receipt = (
        legacy / "attempts" / "pilot" / "attempt-001" / "sequence-receipt.json"
    )
    output = compact_attempt / "run"
    command = output / "command.json"
    receipt = compact_attempt / "sequence-receipt.json"
    sampler_stderr = output / "sampler.stderr.txt"
    temporary_example = output / ".wb04-12345678"
    sampler = tmp_path / "sampler.ps1"
    sampler.write_text("# no launch\n", encoding="utf-8")

    assert len(str(legacy_receipt)) >= 260
    assert max(
        len(str(command)),
        len(str(receipt)),
        len(str(sampler_stderr)),
        len(str(temporary_example)),
    ) < 260
    mib = 1024**2
    monkeypatch.setattr(
        runtime_process,
        "available_ram_bytes",
        lambda: 4095 * mib,
    )
    record = runtime_process.run_governed_process(
        command=["never-launched"],
        output_dir=output,
        role="sample-3",
        environment={},
        sampler_script=sampler,
        launch_minimum_available_ram_bytes=4096 * mib,
        emergency_minimum_available_ram_bytes=2048 * mib,
    )
    runtime_measurement.atomic_write_json(receipt, {"kind": "receipt"})
    assert record["root_pid"] is None
    assert record["valid"] is False
    assert json.loads(command.read_text(encoding="utf-8"))["command"] == [
        "never-launched"
    ]
    assert sampler_stderr.read_text(encoding="utf-8") == ""
    assert json.loads(receipt.read_text(encoding="utf-8")) == {
        "kind": "receipt"
    }


def test_legacy_runtime_evidence_fails_closed_before_compact_launch(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    spec_path = config.spec_root / "OV-11" / "512" / "runtime-spec.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    legacy_record = (
        config.campaign_root
        / "runtime"
        / "OV-11"
        / "context-512"
        / "attempts"
        / "pilot"
        / "attempt-001"
        / "run"
        / "attempt.json"
    )
    _write_json(legacy_record, {"schema": "legacy-native-runtime/v1"})

    with pytest.raises(ValueError, match="legacy adaptive runtime evidence"):
        adaptive_controller._native_runtime_failures_for_step(
            config=config,
            test_id="OV-11",
            context=512,
            adaptive_spec=spec,
        )


def _failure_record(
    config: AdaptiveCampaignConfig,
    *,
    test_id: str = "OV-11",
    context: int = 512,
    category: str = "ram-floor",
    code: str = "RAM_FLOOR",
    cleanup: int = 0,
    survivors: int = 0,
    emergency_actions: list[object] | None = None,
    os_instability: bool = False,
    ram_query_succeeded: bool = True,
    launch_reserve_restored: bool = True,
) -> MeasurementFailureRecord:
    case_payload = json.loads(config.matrix_path.read_text(encoding="utf-8"))
    case = next(item for item in case_payload["cases"] if item["test_id"] == test_id)
    record = {
        "controlled_test_id": test_id,
        "context_tokens": context,
        "stage": "pilot",
        "failure_category": category,
        "failure_code": code,
        "artifact_manifest_sha256": case["artifact_manifest_sha256"],
        "artifact_id": case["artifact_id"],
        "model": case["model"],
        "device": case["device"],
        "execution_route": case["execution_route"],
        "launch_minimum_available_ram_mib": 4096,
        "emergency_minimum_available_ram_mib": 2048,
        "cleanup_process_count": cleanup,
        "residual_owned_process_count": survivors,
        "emergency_actions": emergency_actions or [],
        "os_instability": os_instability,
        "ram_query_succeeded": ram_query_succeeded,
        "launch_reserve_restored": launch_reserve_restored,
        "workload_job": {
            "setup_ok": True,
            "query_ok": True,
            "terminate_job_called": False,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
        "sampler_job": {
            "setup_ok": True,
            "query_ok": True,
            "terminate_job_called": False,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
    }
    path = config.campaign_root.parent / f"failure-{test_id}-{context}-{code}.json"
    _write_json(path, record)
    return MeasurementFailureRecord(
        role="pilot",
        record_path=path,
        record=record,
        fingerprint="task-three-sequence-identity",
    )


def _native_directory_identity(path: Path) -> dict[str, object]:
    root = path.resolve()
    files = [
        {
            "path": source.relative_to(root).as_posix(),
            "bytes": source.stat().st_size,
            "sha256": _sha256(source),
        }
        for source in sorted(root.rglob("*"))
        if source.is_file()
        and "__pycache__" not in source.relative_to(root).parts
        and source.suffix.lower() not in {".pyc", ".pyo"}
    ]
    return {
        "path": str(root),
        "files": files,
        "content_sha256": _sha256_json(files),
    }


def _write_native_task_three_failure(
    config: AdaptiveCampaignConfig,
    campaign_root: Path,
    *,
    attempt_number: int,
    test_id: str = "OV-12",
    context: int = 4096,
    identity_artifact_id: str | None = None,
    include_convenience_identity: bool = False,
    launch_floor_bytes: int = START_RESERVE_MIB * 1024**2,
    emergency_floor_bytes: int = 2048 * 1024**2,
    identity_section_change: tuple[str, str] | None = None,
    allow_invalid_artifact_manifest: bool = False,
) -> Path:
    matrix_payload = json.loads(config.matrix_path.read_text(encoding="utf-8"))
    case = next(item for item in matrix_payload["cases"] if item["test_id"] == test_id)
    adaptive_spec_path = (
        config.spec_root / test_id / str(context) / "runtime-spec.json"
    )
    adaptive_spec = json.loads(adaptive_spec_path.read_text(encoding="utf-8"))
    artifact_manifest = json.loads(
        Path(adaptive_spec["artifact_manifest_path"]).read_text(encoding="utf-8")
    )
    if allow_invalid_artifact_manifest:
        validated_artifact = {
            "accepted": True,
            "status": "load-proven",
            "artifact_id": adaptive_spec["artifact_id"],
            "artifact_root": adaptive_spec["model_path"],
            "precision": case["weight_precision"],
            "inventory_sha256": artifact_manifest["inventory_sha256"],
            "generated_tokens": artifact_manifest["load_probe"][
                "generated_tokens"
            ],
        }
    else:
        validated_artifact = validate_artifact_manifest(
            Path(adaptive_spec["artifact_manifest_path"]),
            expected_precision=case["weight_precision"],
        )
    if identity_artifact_id is not None:
        validated_artifact = {
            **validated_artifact,
            "artifact_id": identity_artifact_id,
        }
    role = "pilot"
    prompt = adaptive_spec["workload"]["prompt"]
    package = config.build_root / "openvino_genai"
    python_module = package / "py_openvino_genai.pyd"
    runtime_dll = package / "openvino_genai.dll"
    runtime_sources = (
        ROOT / "scripts" / "testing" / "campaigns" / "openvino"
    )
    controller_source = ROOT / "scripts" / "testing" / "measure_official_openvino.py"
    openvino_package = config.python_site_packages / "openvino"
    identity = {
        "config": {
            "device": adaptive_spec["device"],
            "max_new_tokens": adaptive_spec["max_new_tokens"],
            "expected_input_tokens": context,
            "ignore_eos": adaptive_spec["ignore_eos"],
            "seed": adaptive_spec["seed"],
            "apply_chat_template": adaptive_spec["apply_chat_template"],
            "properties": adaptive_spec["properties"],
        },
        "context": context,
        "matrix": {
            "path": str(config.matrix_path.resolve()),
            "file_sha256": _sha256(config.matrix_path),
            "schema_version": matrix_payload.get("schema_version"),
            "source_identity": matrix_payload["source_identity"],
            "build_identity": matrix_payload["build_identity"],
            "case": case,
        },
        "build": {
            "root": str(config.build_root.resolve()),
            "provenance_path": str(config.build_provenance_path.resolve()),
            "provenance_sha256": _sha256(config.build_provenance_path),
            "python_module": {
                "path": str(python_module.resolve()),
                "sha256": _sha256(python_module),
            },
            "runtime_dll": {
                "path": str(runtime_dll.resolve()),
                "sha256": _sha256(runtime_dll),
            },
        },
        "model": {
            "artifact_manifest_path": adaptive_spec["artifact_manifest_path"],
            "artifact_manifest_sha256": adaptive_spec[
                "artifact_manifest_sha256"
            ],
            "validated_artifact": validated_artifact,
        },
        "prompt": {
            "utf8_bytes": len(prompt.encode("utf-8")),
            "sha256": hashlib.sha256(prompt.encode("utf-8")).hexdigest(),
        },
        "runtime": {
            "repository_root": str(ROOT.resolve()),
            "repository_runtime_sources": _native_directory_identity(
                runtime_sources
            ),
            "controller_source": {
                "path": str(controller_source.resolve()),
                "sha256": _sha256(controller_source),
            },
            "python_executable": {
                "path": str(config.python_executable.resolve()),
                "sha256": _sha256(config.python_executable),
            },
            "python_openvino_package": (
                _native_directory_identity(openvino_package)
                if openvino_package.is_dir()
                else {
                    "path": str(openvino_package.resolve()),
                    "files": [],
                    "content_sha256": _sha256_json([]),
                }
            ),
            "openvino_libraries": _native_directory_identity(
                config.openvino_libraries
            ),
        },
    }
    if identity_section_change is not None:
        section, change = identity_section_change
        if change == "missing":
            identity.pop(section)
        elif section == "build" and change == "mismatched":
            identity["build"]["root"] = str((campaign_root / "other-build").resolve())
        elif section == "prompt" and change == "mismatched":
            identity["prompt"]["sha256"] = "f" * 64
        elif section == "runtime" and change == "mismatched":
            identity["runtime"]["repository_root"] = str(
                (campaign_root / "other-repository").resolve()
            )
        else:  # pragma: no cover - test helper contract
            raise AssertionError("unsupported identity section mutation")
    identity_hash = _sha256_json(identity)
    campaign_identity = {
        "schema": "official-openvino-wb04-campaign-identity/v1",
        "identity": identity,
        "campaign_identity_sha256": identity_hash,
    }
    identity_path = campaign_root / "campaign-identity.json"
    if identity_path.exists():
        assert json.loads(identity_path.read_text(encoding="utf-8")) == campaign_identity
    else:
        _write_json(identity_path, campaign_identity)

    role_spec = {
        "schema": "official-openvino-wb04-worker-spec/v1",
        "role": role,
        "controlled_test_id": test_id,
        "model_path": adaptive_spec["model_path"],
        "device": adaptive_spec["device"],
        "prompt": adaptive_spec["workload"]["prompt"],
        "context": context,
        "expected_input_tokens": context,
        "properties": adaptive_spec["properties"],
        "max_new_tokens": adaptive_spec["max_new_tokens"],
        "ignore_eos": adaptive_spec["ignore_eos"],
        "seed": adaptive_spec["seed"],
        "apply_chat_template": adaptive_spec["apply_chat_template"],
        "campaign_identity_sha256": identity_hash,
    }
    attempt_dir = (
        campaign_root
        / "attempts"
        / role
        / f"attempt-{attempt_number:03d}"
    )
    spec_path = attempt_dir / "spec.json"
    _write_json(spec_path, role_spec)
    record = {
        "schema": "official-openvino-wb04-governed-run/v1",
        "role": role,
        "run_nonce": f"native-failure-{attempt_number}",
        "worker": {
            "role": role,
            "controlled_test_id": test_id,
            "context": context,
            "expected_input_tokens": context,
            "device": adaptive_spec["device"],
            "model_path": adaptive_spec["model_path"],
        },
        "low_memory_stop": True,
        "exit_code": 137,
        "launch_minimum_available_ram_bytes": launch_floor_bytes,
        "emergency_minimum_available_ram_bytes": emergency_floor_bytes,
        "available_ram_bytes": {
            "before": 5000 * 1024**2,
            "minimum": 1900 * 1024**2,
            "after": 4500 * 1024**2,
        },
        "cleanup_process_count": 0,
        "residual_owned_process_count": 0,
        "emergency_actions": [],
        "os_instability": False,
        "workload_job": {
            "setup_ok": True,
            "query_ok": True,
            "terminate_job_called": True,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
        "sampler_job": {
            "setup_ok": True,
            "query_ok": True,
            "terminate_job_called": True,
            "queried_active_process_count_after_cleanup": 0,
            "survivor_pids_after_cleanup": [],
        },
        "validation_errors": ["available RAM is below the emergency floor"],
    }
    if include_convenience_identity:
        record.update(
            {
                "controlled_test_id": test_id,
                "context_tokens": context,
                "artifact_id": case["artifact_id"],
                "artifact_manifest_sha256": case[
                    "artifact_manifest_sha256"
                ],
                "model": case["model"],
                "device": case["device"],
                "execution_route": case["execution_route"],
                "launch_minimum_available_ram_mib": START_RESERVE_MIB,
                "emergency_minimum_available_ram_mib": 2048,
            }
        )
    record_path = attempt_dir / "run" / "attempt.json"
    _write_json(record_path, record)
    root = campaign_root.resolve()
    receipt = {
        "schema": "official-openvino-wb04-sequence-receipt/v1",
        "role": role,
        "attempt_number": attempt_number,
        "campaign_identity_sha256": identity_hash,
        "spec_sha256": _sha256_json(role_spec),
        "spec_path": spec_path.resolve().relative_to(root).as_posix(),
        "spec_file_sha256": _sha256(spec_path),
        "runtime_record_path": record_path.resolve().relative_to(root).as_posix(),
        "runtime_record_sha256": _sha256(record_path),
        "accepted": False,
        "controller_error": "RuntimeError: guarded low-memory failure",
    }
    _write_json(attempt_dir / "sequence-receipt.json", receipt)
    return record_path


class _FakeRunner:
    def __init__(self, outcomes: list[object]):
        self.outcomes = list(outcomes)
        self.calls: list[tuple[str, int]] = []
        self.campaign_jobs: list[object] = []

    def __call__(self, **kwargs: object) -> dict[str, object]:
        spec = json.loads(Path(kwargs["spec_path"]).read_text(encoding="utf-8"))
        test_id = spec["controlled_test_id"]
        context = spec["context_tokens"]
        self.calls.append((test_id, context))
        self.campaign_jobs.append(kwargs.get("campaign_job"))
        outcome = self.outcomes.pop(0) if self.outcomes else {}
        if isinstance(outcome, MeasurementFailureRecord):
            raise MeasurementSequenceFailure("guarded failure", outcome)
        runtime_root = Path(kwargs["campaign_root"])
        pilot_spec = runtime_root / "synthetic-pilot-spec.json"
        _write_json(pilot_spec, spec)
        measurement_summary = runtime_root / "measurement-summary.json"
        _write_json(
            measurement_summary,
            {
                "schema": "synthetic-task-four-measurement-summary/v1",
                "test_id": test_id,
                "context_tokens": context,
                "cleanup_process_count": 0,
            },
        )
        result = {
            "schema": "official-openvino-wb04-attempt-sequence/v1",
            "pilot": {
                "spec_path": pilot_spec.relative_to(runtime_root).as_posix(),
                "spec_file_sha256": _sha256(pilot_spec),
            },
            "accepted_sample_count": 3,
            "cleanup_process_count": 0,
            **dict(outcome),
        }
        evidence = runtime_root / "attempt-sequence.json"
        _write_json(evidence, result)
        return result


def _run(
    config: AdaptiveCampaignConfig,
    runner: _FakeRunner,
    *,
    checkpoints: list[tuple[Path, bool]] | None = None,
) -> dict[str, object]:
    def publish(path: Path, final: bool) -> dict[str, object]:
        assert path.is_file()
        assert checkpoints is not None
        checkpoints.append((path, final))
        return {"published": True}

    return run_adaptive_campaign(
        config,
        run_runtime=runner,
        run_quality=None,
        publish_checkpoint=publish if checkpoints is not None else None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )


def test_first_clean_guarded_failure_retries_same_context_once(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config), {}])

    result = _run(config, runner)

    assert runner.calls[:2] == [("OV-11", 512), ("OV-11", 512)]
    assert runner.calls.count(("OV-11", 512)) == 2
    assert result["steps"]["OV-11:512"]["runtime_status"] == "passed"
    assert result["steps"]["OV-11:512"]["attempt_count"] == 2


def test_two_matching_failures_confirm_boundary(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config), _failure_record(config)])

    result = _run(config, runner)

    step = result["steps"]["OV-11:512"]
    assert step["runtime_status"] == "boundary-confirmed"
    assert step["attempt_count"] == 2


def test_two_nonmatching_failures_are_inconclusive(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner(
        [_failure_record(config), _failure_record(config, code="FUNCTIONAL_OTHER")]
    )

    result = _run(config, runner)

    assert (
        result["steps"]["OV-11:512"]["runtime_status"]
        == "inconclusive-safety-boundary"
    )


@pytest.mark.parametrize(
    "changes",
    [
        {"cleanup": 1},
        {"survivors": 1},
        {"emergency_actions": [{"action": "terminate"}]},
        {"os_instability": True},
        {"ram_query_succeeded": False},
        {"launch_reserve_restored": False},
    ],
)
def test_unsafe_failure_prohibits_retry(
    tmp_path: Path, changes: dict[str, object]
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config, **changes)])

    result = _run(config, runner)

    assert runner.calls == [("OV-11", 512)]
    assert result["steps"]["OV-11:512"]["runtime_status"] == "safety-boundary"


def test_resume_rejects_matrix_spec_inventory_or_build_drift(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    load_or_create_state(config)
    _write_json(config.build_provenance_path, {"schema": "changed", "status": "passed"})

    with pytest.raises(ValueError, match="drift"):
        load_or_create_state(config)


def test_controller_receipts_are_immutable_and_monotonic(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config), {}])

    result = _run(config, runner)

    receipts = result["steps"]["OV-11:512"]["attempts"]
    assert [item["attempt_number"] for item in receipts] == [1, 2]
    paths = [config.campaign_root / item["receipt_path"] for item in receipts]
    assert all(path.is_file() for path in paths)
    before = [path.read_bytes() for path in paths]
    resumed = _run(config, _FakeRunner([{} for _ in range(4)]))
    assert [path.read_bytes() for path in paths] == before
    assert resumed["steps"]["OV-11:512"] == result["steps"]["OV-11:512"]


def test_checkpoint_is_published_after_each_completed_or_terminal_step(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    checkpoints: list[tuple[Path, bool]] = []
    outcomes = [_failure_record(config), {}, {}, {}, {}, {}]

    result = _run(config, _FakeRunner(outcomes), checkpoints=checkpoints)

    assert len(checkpoints) == 5
    assert all(final is False for _, final in checkpoints)
    assert all(step["quality_status"] == "quality-blocked" for step in result["steps"].values())


def test_passed_row_stores_complete_self_hashed_quality_recovery(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)

    result = _run(config, _FakeRunner([{} for _ in range(5)]))

    recovery = result["steps"]["OV-11:512"]["quality_recovery"]
    assert recovery["schema"] == "official-openvino-adaptive-quality-recovery/v1"
    assert recovery["quality_recovery_sha256"] == _sha256_json(
        {
            key: value
            for key, value in recovery.items()
            if key != "quality_recovery_sha256"
        }
    )
    assert recovery["adaptive_runtime_spec_path"] == str(
        (config.spec_root / "OV-11" / "512" / "runtime-spec.json").resolve()
    )
    assert recovery["spec_index_path"] == str(
        (config.spec_root / "spec-index.json").resolve()
    )
    assert recovery["artifact_inventory_path"] == str(
        (tmp_path / "artifact-inventory.json").resolve()
    )
    assert recovery["timeout_seconds"] == 1800.0
    assert recovery["output_root"] == str(
        (config.campaign_root / "quality" / "OV-11" / "512").resolve()
    )


def test_resume_rejects_quality_recovery_state_or_self_hash_substitution(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    _run(config, _FakeRunner([{} for _ in range(5)]))
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state["steps"]["OV-11:512"]["quality_recovery"][
        "timeout_seconds"
    ] = 1801.0
    _write_json(state_path, state)

    with pytest.raises(ValueError, match="quality recovery"):
        load_or_create_state(config)


def test_task_five_state_recovery_is_exact_and_keeps_ladder_order(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    from scripts.testing import run_official_openvino_adaptive_quality as quality_cli

    config = _campaign_inputs(tmp_path)
    _run(config, _FakeRunner([{} for _ in range(5)]))
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    monkeypatch.setattr(
        quality_cli,
        "quality_campaign_input_from_recovery",
        lambda recovery: recovery,
    )
    monkeypatch.setattr(
        quality_cli,
        "load_accepted_quality_campaign",
        lambda recovery: recovery,
    )

    _state, recoveries = quality_cli._validate_campaign_state(state_path)

    assert [(test_id, context) for test_id, context, _ in recoveries] == [
        ("OV-11", 512),
        ("OV-TQ-22", 512),
        ("OV-TQ-21", 512),
        ("OV-12", 512),
        ("OV-13", 512),
    ]

    state = json.loads(state_path.read_text(encoding="utf-8"))
    recovery = state["steps"]["OV-11:512"]["quality_recovery"]
    recovery["output_root"] = str(tmp_path / "substituted-quality-output")
    recovery.pop("quality_recovery_sha256")
    recovery["quality_recovery_sha256"] = _sha256_json(recovery)
    _write_json(state_path, state)
    with pytest.raises(ValueError, match="adaptive campaign state"):
        quality_cli._validate_campaign_state(state_path)


def test_quality_callback_receives_and_state_retains_same_complete_recovery(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    received = []

    def quality(recovery, *, resume):
        assert resume is False
        received.append(dict(recovery))
        return {"status": "passed"}

    result = run_adaptive_campaign(
        config,
        run_runtime=_FakeRunner([{} for _ in range(5)]),
        run_quality=quality,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )

    assert len(received) == 5
    assert result["steps"]["OV-11:512"]["quality_recovery"] == received[0]
    assert received[0]["quality_recovery_sha256"] == _sha256_json(
        {
            key: value
            for key, value in received[0].items()
            if key != "quality_recovery_sha256"
        }
    )


def test_preflight_validates_and_does_not_launch_or_create_attempts(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    config = _campaign_inputs(tmp_path)
    real_active_pids = KillOnCloseJob.active_pids
    queried_jobs: list[KillOnCloseJob] = []

    def record_query(job: KillOnCloseJob) -> list[int]:
        queried_jobs.append(job)
        return real_active_pids(job)

    monkeypatch.setattr(KillOnCloseJob, "active_pids", record_query)

    state = preflight_adaptive_campaign(
        config,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )
    status = campaign_status(config, state)

    assert status["next_eligible_step"] == ["OV-11", 512]
    assert len(queried_jobs) == 1
    assert type(queried_jobs[0]) is KillOnCloseJob
    assert getattr(queried_jobs[0], "_handle", object()) is None
    assert not any(config.campaign_root.rglob("attempt-*"))


def test_explicit_equivalent_boundary_skips_dangerous_relaunch(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / "historical"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
            test_id="OV-12",
            context=4096,
        )
    index_path = tmp_path / "boundary-index.json"
    build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=index_path,
    )
    config = AdaptiveCampaignConfig(
        **{**config.__dict__, "reference_boundary_index": index_path}
    )
    runner = _FakeRunner([{} for _ in range(19)])

    result = _run(config, runner)

    assert ("OV-12", 4096) not in runner.calls
    assert result["steps"]["OV-12:4096"]["runtime_status"] == "boundary-confirmed"


def test_non_equivalent_boundary_evidence_is_rejected(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    index = tmp_path / "boundary-index.json"
    _write_json(
        index,
        {
            "schema": "official-openvino-adaptive-boundary-index/v1",
            "matrix_sha256": _sha256(config.matrix_path),
            "spec_index_sha256": _sha256(config.spec_root / "spec-index.json"),
            "boundaries": [
                {
                    "test_id": "OV-12",
                    "context_tokens": 512,
                    "failure_fingerprint": "f" * 64,
                    "matching_attempt_count": 2,
                    "identity": {"artifact_manifest_sha256": "0" * 64},
                    "attempts": [],
                }
            ],
        },
    )
    config = AdaptiveCampaignConfig(
        **{**config.__dict__, "reference_boundary_index": index}
    )

    with pytest.raises(ValueError, match="equivalent boundary"):
        load_or_create_state(config)


def test_reference_boundary_requires_native_campaign_identity_hash(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / "native-reference-historical"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
        )
    index_path = tmp_path / "native-reference-index.json"
    index = build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=index_path,
    )
    index["boundaries"][0].pop("native_campaign_identity_sha256")
    _write_json(index_path, index)
    config = AdaptiveCampaignConfig(
        **{**config.__dict__, "reference_boundary_index": index_path}
    )

    with pytest.raises(ValueError, match="native campaign identity"):
        load_or_create_state(config)


def test_campaign_cli_exposes_only_plan_listed_flags() -> None:
    parser = campaign_cli._parser()
    flags = {
        option
        for action in parser._actions
        for option in action.option_strings
        if option != "--help"
    }
    assert flags == {
        "-h",
        "--matrix",
        "--spec-root",
        "--campaign-root",
        "--build-root",
        "--build-provenance",
        "--python-executable",
        "--python-site-packages",
        "--openvino-libraries",
        "--sampler-script",
        "--reference-boundary-index",
        "--max-context",
        "--resume",
        "--publish-checkpoints",
        "--evidence-commit",
        "--preflight-only",
    }
    max_context = next(action for action in parser._actions if action.dest == "max_context")
    assert tuple(max_context.choices) == CONTEXTS


def test_publication_callback_builds_immutable_state_hash_inputs_and_forwards_exact_keywords(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config = _campaign_inputs(tmp_path)
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    state_path.parent.mkdir(parents=True, exist_ok=True)
    state_path.write_bytes(b"state-one\n")
    builds: list[tuple[Path, Path, Path]] = []
    publications: list[tuple[Path, dict[str, object]]] = []

    def build(matrix: Path, state: Path, output: Path) -> dict[str, object]:
        builds.append((matrix, state, output))
        payload = b"release:" + hashlib.sha256(state.read_bytes()).hexdigest().encode()
        output.parent.mkdir(parents=True, exist_ok=True)
        if output.exists() and output.read_bytes() != payload:
            raise ValueError("immutable release input differs")
        output.write_bytes(payload)
        return {"release_input_sha256": "1" * 64}

    def publish(path: Path, **keywords: object) -> dict[str, object]:
        publications.append((path, keywords))
        return {"published": True, "path": str(path)}

    monkeypatch.setattr(campaign_cli, "build_comparison_release_input", build)
    monkeypatch.setattr(
        campaign_cli, "load_comparison_release_input", lambda _path: {"validated": True}
    )
    monkeypatch.setattr(campaign_cli, "publish_reconciled_checkpoint", publish)
    callback = campaign_cli._publication_callback(config, "a" * 40)

    first = callback(state_path, False)
    repeated = callback(state_path, True)
    first_release = publications[0][0]
    first_bytes = first_release.read_bytes()
    first_state_sha256 = hashlib.sha256(b"state-one\n").hexdigest()
    first_state_tag = base64.urlsafe_b64encode(
        bytes.fromhex(first_state_sha256)
    ).decode("ascii").rstrip("=")
    state_path.write_bytes(b"state-two\n")
    changed = callback(state_path, False)
    second_release = publications[-1][0]
    second_state_sha256 = hashlib.sha256(b"state-two\n").hexdigest()
    second_state_tag = base64.urlsafe_b64encode(
        bytes.fromhex(second_state_sha256)
    ).decode("ascii").rstrip("=")

    assert first["published"] and repeated["published"] and changed["published"]
    assert first_release == (
        config.campaign_root
        / "checkpoints"
        / f"r-{first_state_tag}.json"
    ).resolve()
    assert publications[1][0] == first_release
    assert first_release.read_bytes() == first_bytes
    assert second_release != first_release
    assert second_release.name == f"r-{second_state_tag}.json"
    assert all(item[0] == config.matrix_path for item in builds)
    assert all(item[1] != state_path.resolve() for item in builds)
    assert all(item[1].parent == config.campaign_root.resolve() for item in builds)
    assert builds[0][1].name == f"s-{first_state_tag}.json"
    assert builds[-1][1].name == f"s-{second_state_tag}.json"
    assert builds[0][1].read_bytes() == b"state-one\n"
    assert builds[-1][1].read_bytes() == b"state-two\n"
    assert publications[0][1] == {
        "repo_root": campaign_cli.ROOT,
        "require_complete": False,
        "evidence_commit": "a" * 40,
    }
    assert publications[1][1]["require_complete"] is True


def test_publication_callback_builds_from_one_immutable_state_snapshot(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    config = _campaign_inputs(tmp_path)
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    state_path.parent.mkdir(parents=True, exist_ok=True)
    state_path.write_bytes(b"stable checkpoint\n")
    stable_sha = hashlib.sha256(b"stable checkpoint\n").hexdigest()
    stable_tag = base64.urlsafe_b64encode(
        bytes.fromhex(stable_sha)
    ).decode("ascii").rstrip("=")
    built_from: list[Path] = []

    def build(_matrix: Path, snapshot: Path, output: Path) -> dict[str, object]:
        built_from.append(snapshot)
        assert snapshot != state_path.resolve()
        assert snapshot.name == f"s-{stable_tag}.json"
        frozen = snapshot.read_bytes()
        state_path.write_bytes(b"new live state during build\n")
        assert snapshot.read_bytes() == frozen == b"stable checkpoint\n"
        output.write_bytes(b"release-from:" + frozen)
        return {"release_input_sha256": "1" * 64}

    monkeypatch.setattr(campaign_cli, "build_comparison_release_input", build)
    monkeypatch.setattr(
        campaign_cli, "load_comparison_release_input", lambda _path: {"validated": True}
    )
    monkeypatch.setattr(
        campaign_cli,
        "publish_reconciled_checkpoint",
        lambda path, **_keywords: {"release": str(path)},
    )

    result = campaign_cli._publication_callback(config, "a" * 40)(state_path, False)
    immutable_release = Path(result["release"])
    assert immutable_release.name == f"r-{stable_tag}.json"
    assert immutable_release.read_bytes() == b"release-from:stable checkpoint\n"
    assert built_from[0].read_bytes() == b"stable checkpoint\n"


def test_publication_callback_fits_legacy_windows_max_path(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    inputs = tmp_path / "inputs"
    inputs.mkdir()
    config = _campaign_inputs(inputs)
    base = tmp_path.resolve()
    campaign_root_length = 188
    component_length = campaign_root_length - len(str(base)) - 1
    assert 0 < component_length <= 255
    campaign_root = base / ("c" * component_length)
    assert len(str(campaign_root)) == campaign_root_length
    config = replace(config, campaign_root=campaign_root)
    state_path = campaign_root / "adaptive-campaign-state.json"
    state_path.parent.mkdir(parents=True)
    state_path.write_bytes(b"legacy max path checkpoint\n")

    real_mkstemp = campaign_cli.tempfile.mkstemp
    real_link = campaign_cli.os.link

    def legacy_mkstemp(*args: object, **kwargs: object) -> tuple[int, str]:
        directory = Path(str(kwargs["dir"]))
        prefix = str(kwargs.get("prefix", "tmp"))
        suffix = str(kwargs.get("suffix", ""))
        candidate = directory / f"{prefix}12345678{suffix}"
        if len(str(candidate)) >= 260:
            raise FileNotFoundError(2, "legacy MAX_PATH", str(candidate))
        return real_mkstemp(*args, **kwargs)

    def legacy_link(source: object, destination: object, *args: object, **kwargs: object) -> None:
        if len(str(Path(destination))) >= 260:
            raise FileNotFoundError(2, "legacy MAX_PATH", str(destination))
        real_link(source, destination, *args, **kwargs)

    def build(_matrix: Path, _snapshot: Path, output: Path) -> dict[str, object]:
        output.write_bytes(b"compact release input\n")
        return {"release_input_sha256": "1" * 64}

    monkeypatch.setattr(campaign_cli.tempfile, "mkstemp", legacy_mkstemp)
    monkeypatch.setattr(campaign_cli.os, "link", legacy_link)
    monkeypatch.setattr(campaign_cli, "build_comparison_release_input", build)
    monkeypatch.setattr(
        campaign_cli, "load_comparison_release_input", lambda _path: {"validated": True}
    )
    monkeypatch.setattr(
        campaign_cli,
        "publish_reconciled_checkpoint",
        lambda path, **_keywords: {"release": str(path)},
    )

    result = campaign_cli._publication_callback(config, "a" * 40)(state_path, False)

    release_path = Path(result["release"])
    assert release_path.is_file()
    assert len(str(release_path)) < 260


def test_publication_callback_preserves_reloadable_real_task7_history(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    from scripts.testing.campaigns.openvino.comparison_reconcile import (
        load_comparison_release_input,
        reconcile_comparison_release,
    )
    from scripts.testing.tests.test_official_openvino_comparison_reconcile import (
        _path as task7_path,
        valid_release_input,
    )

    authority = tmp_path / "real-task7-authority"
    authority.mkdir()
    mapping = valid_release_input(authority)
    original_state = task7_path(mapping, mapping["campaign_state"])
    state_path = authority / "adaptive-campaign-state.json"
    original_state.replace(state_path)
    matrix_path = task7_path(mapping, mapping["matrix"])
    config_root = tmp_path / "config"
    config_root.mkdir()
    config = replace(
        _campaign_inputs(config_root),
        matrix_path=matrix_path,
        campaign_root=authority,
    )
    published: list[Path] = []

    def publish(path: Path, **_keywords: object) -> dict[str, str]:
        published.append(Path(path))
        return {"release": str(path)}

    monkeypatch.setattr(campaign_cli, "publish_reconciled_checkpoint", publish)
    callback = campaign_cli._publication_callback(config, "a" * 40)
    callback(state_path, False)
    first_release = published[-1]
    first_bytes = first_release.read_bytes()
    load_comparison_release_input(first_release)
    first_reconciled = reconcile_comparison_release(first_release)

    state_value = json.loads(state_path.read_text(encoding="utf-8"))
    state_path.write_text(
        json.dumps(state_value, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    callback(state_path, False)
    second_release = published[-1]

    assert second_release != first_release
    assert first_release.read_bytes() == first_bytes
    load_comparison_release_input(first_release)
    assert reconcile_comparison_release(first_release) == first_reconciled
    load_comparison_release_input(second_release)


def test_publication_callback_rejects_invalid_commit_and_nonexact_state_path(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    exact = config.campaign_root / "adaptive-campaign-state.json"
    exact.parent.mkdir(parents=True, exist_ok=True)
    exact.write_bytes(b"state\n")
    for commit in ("a" * 39, "A" * 40, "g" * 40):
        with pytest.raises(ValueError, match="40-character lowercase"):
            campaign_cli._publication_callback(config, commit)
    callback = campaign_cli._publication_callback(config, "a" * 40)
    for wrong in (
        config.campaign_root / "other.json",
        config.campaign_root.parent / "adaptive-campaign-state.json",
    ):
        wrong.parent.mkdir(parents=True, exist_ok=True)
        wrong.write_bytes(b"state\n")
        with pytest.raises(ValueError, match="exact campaign state"):
            callback(wrong, False)


def test_campaign_cli_requires_evidence_commit_exactly_with_checkpoint_publication(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    common = [
        "--matrix", str(config.matrix_path),
        "--spec-root", str(config.spec_root),
        "--campaign-root", str(config.campaign_root),
        "--build-root", str(config.build_root),
        "--build-provenance", str(config.build_provenance_path),
        "--python-executable", str(config.python_executable),
        "--python-site-packages", str(config.python_site_packages),
        "--openvino-libraries", str(config.openvino_libraries),
        "--sampler-script", str(config.sampler_script),
        "--preflight-only",
    ]
    with pytest.raises(SystemExit):
        campaign_cli.main([*common, "--publish-checkpoints"])
    with pytest.raises(SystemExit):
        campaign_cli.main([*common, "--evidence-commit", "a" * 40])


def test_campaign_cli_preflight_prints_first_step_without_attempt(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    config = _campaign_inputs(tmp_path)
    monkeypatch.setattr(
        campaign_cli,
        "available_ram_bytes",
        lambda: START_RESERVE_MIB * 1024**2,
    )
    argv = [
        "--matrix", str(config.matrix_path),
        "--spec-root", str(config.spec_root),
        "--campaign-root", str(config.campaign_root),
        "--build-root", str(config.build_root),
        "--build-provenance", str(config.build_provenance_path),
        "--python-executable", str(config.python_executable),
        "--python-site-packages", str(config.python_site_packages),
        "--openvino-libraries", str(config.openvino_libraries),
        "--sampler-script", str(config.sampler_script),
        "--max-context", "512",
        "--preflight-only",
    ]

    assert campaign_cli.main(argv) == 0

    printed = json.loads(capsys.readouterr().out)
    assert printed["next_eligible_step"] == ["OV-11", 512]
    assert not any(config.campaign_root.rglob("attempt-*"))


def _preflight_cli_inputs(tmp_path: Path) -> AdaptiveCampaignConfig:
    config = _campaign_inputs(tmp_path)
    historical = tmp_path / "empty-historical"
    historical.mkdir()
    boundary_index = tmp_path / "reference-boundary-index.json"
    build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=boundary_index,
    )
    return replace(config, reference_boundary_index=boundary_index)


def _preflight_cli_argv(
    config: AdaptiveCampaignConfig,
    *,
    resume: bool = False,
) -> list[str]:
    argv = [
        "--matrix",
        str(config.matrix_path),
        "--spec-root",
        str(config.spec_root),
        "--campaign-root",
        str(config.campaign_root),
        "--build-root",
        str(config.build_root),
        "--build-provenance",
        str(config.build_provenance_path),
        "--python-executable",
        str(config.python_executable),
        "--python-site-packages",
        str(config.python_site_packages),
        "--openvino-libraries",
        str(config.openvino_libraries),
        "--sampler-script",
        str(config.sampler_script),
        "--reference-boundary-index",
        str(config.reference_boundary_index),
        "--max-context",
        "512",
        "--preflight-only",
    ]
    if resume:
        argv.append("--resume")
    return argv


def test_cli_preflight_writes_exact_zero_launch_report_from_one_ram_sample(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    observed_ram = START_RESERVE_MIB * 1024**2 + 12345
    ram_reads = 0

    def sample_ram_once() -> int:
        nonlocal ram_reads
        ram_reads += 1
        return observed_ram

    def forbidden_worker(*_args: object, **_kwargs: object) -> None:
        raise AssertionError("preflight invoked the runtime worker")

    monkeypatch.setattr(campaign_cli, "available_ram_bytes", sample_ram_once)
    monkeypatch.setattr(campaign_cli, "run_adaptive_campaign", forbidden_worker)
    monkeypatch.setattr(campaign_cli, "_quality_callback", forbidden_worker)

    assert campaign_cli.main(_preflight_cli_argv(config)) == 0

    json.loads(capsys.readouterr().out)
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    state = json.loads(state_path.read_text(encoding="utf-8"))
    probe_entry = state["safety_probes"][-1]
    probe_path = config.campaign_root / probe_entry["receipt_path"]
    report_path = config.campaign_root / "preflight" / "preflight-report.json"
    report = json.loads(report_path.read_text(encoding="utf-8"))

    assert ram_reads == 1
    assert report == {
        "schema": "official-openvino-adaptive-preflight-report/v1",
        "matrix_path": str(config.matrix_path.resolve()),
        "matrix_sha256": _sha256(config.matrix_path),
        "spec_index_path": str((config.spec_root / "spec-index.json").resolve()),
        "spec_index_sha256": _sha256(config.spec_root / "spec-index.json"),
        "reference_boundary_index_path": str(
            config.reference_boundary_index.resolve()
        ),
        "reference_boundary_index_sha256": _sha256(
            config.reference_boundary_index
        ),
        "build_provenance_path": str(config.build_provenance_path.resolve()),
        "build_provenance_sha256": _sha256(config.build_provenance_path),
        "campaign_state_path": str(state_path.resolve()),
        "campaign_state_sha256": _sha256(state_path),
        "observed_available_ram_bytes": observed_ram,
        "launch_reserve_bytes": START_RESERVE_MIB * 1024**2,
        "next_eligible_step": ["OV-11", 512],
        "campaign_halt": None,
        "owned_survivor_count": 0,
        "zero_survivor_safety_probe_path": str(probe_path.resolve()),
        "zero_survivor_safety_probe_sha256": _sha256(probe_path),
    }
    assert not any(config.campaign_root.rglob("attempt-*"))


def test_preflight_report_is_byte_identical_for_same_inputs_and_state(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    observed_ram = START_RESERVE_MIB * 1024**2 + 99
    monkeypatch.setattr(campaign_cli, "available_ram_bytes", lambda: observed_ram)
    assert campaign_cli.main(_preflight_cli_argv(config)) == 0
    capsys.readouterr()
    report_path = config.campaign_root / "preflight" / "preflight-report.json"
    before = report_path.read_bytes()
    state = json.loads(
        (config.campaign_root / "adaptive-campaign-state.json").read_text(
            encoding="utf-8"
        )
    )

    campaign_cli._write_preflight_report(
        config,
        state,
        observed_available_ram_bytes=observed_ram,
    )

    assert report_path.read_bytes() == before
    assert list(report_path.parent.glob(".*.stage")) == []


def test_cli_preflight_rejects_conflicting_report_without_overwrite(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    report_path = config.campaign_root / "preflight" / "preflight-report.json"
    report_path.parent.mkdir(parents=True)
    original = b'{"conflict":true}\n'
    report_path.write_bytes(original)
    monkeypatch.setattr(
        campaign_cli,
        "available_ram_bytes",
        lambda: START_RESERVE_MIB * 1024**2,
    )

    with pytest.raises(ValueError, match="conflicting existing preflight report"):
        campaign_cli.main(_preflight_cli_argv(config))

    assert report_path.read_bytes() == original
    assert list(report_path.parent.glob(".*.stage")) == []
    assert not (config.campaign_root / "adaptive-campaign-state.json").exists()
    assert not (config.campaign_root / "safety-probes").exists()
    assert not any(config.campaign_root.rglob("attempt-*"))


def test_cli_preflight_reuses_valid_report_without_mutating_state_or_probe_chain(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    observed_ram = START_RESERVE_MIB * 1024**2 + 7
    ram_reads = 0

    def sample_ram() -> int:
        nonlocal ram_reads
        ram_reads += 1
        return observed_ram

    monkeypatch.setattr(campaign_cli, "available_ram_bytes", sample_ram)
    assert campaign_cli.main(_preflight_cli_argv(config)) == 0
    capsys.readouterr()
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    report_path = config.campaign_root / "preflight" / "preflight-report.json"
    before_state = state_path.read_bytes()
    before_report = report_path.read_bytes()

    assert campaign_cli.main(_preflight_cli_argv(config, resume=True)) == 0
    capsys.readouterr()

    assert ram_reads == 1
    assert state_path.read_bytes() == before_state
    assert report_path.read_bytes() == before_report
    state = json.loads(before_state)
    assert len(state["safety_probes"]) == 1


def test_preflight_finalizer_runs_while_campaign_lock_is_held(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    observed_ram = START_RESERVE_MIB * 1024**2 + 11
    finalized: list[tuple[dict[str, object], int]] = []

    def finalize(state: dict[str, object], observed: int) -> None:
        with pytest.raises(RuntimeError, match="locked"):
            with CampaignLock(config.campaign_root):
                pass
        finalized.append((state, observed))

    state = preflight_adaptive_campaign(
        config,
        available_ram=lambda: observed_ram,
        finalize_preflight=finalize,
    )

    assert finalized == [(state, observed_ram)]


def test_failed_post_publish_validation_removes_only_new_report(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    observed_ram = START_RESERVE_MIB * 1024**2 + 1
    state = preflight_adaptive_campaign(
        config,
        available_ram=lambda: observed_ram,
    )
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    report_path = config.campaign_root / "preflight" / "preflight-report.json"
    real_publish = campaign_cli._publish_preflight_report

    def publish_then_drift(path: Path, report: dict[str, object]) -> bool:
        created = real_publish(path, report)
        state_path.write_bytes(state_path.read_bytes() + b" ")
        return created

    monkeypatch.setattr(
        campaign_cli,
        "_publish_preflight_report",
        publish_then_drift,
    )

    with pytest.raises(ValueError, match="campaign state hash binding drift"):
        campaign_cli._write_preflight_report(
            config,
            state,
            observed_available_ram_bytes=observed_ram,
        )

    assert not report_path.exists()
    assert list(report_path.parent.glob(".*.stage")) == []


def test_preflight_report_rejects_coordinated_rehashed_spec_semantic_tamper(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    report_path = _write_valid_preflight_report(config, monkeypatch, capsys)
    spec_path = config.spec_root / "OV-11" / "512" / "runtime-spec.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    spec["ignore_eos"] = False
    _write_json(spec_path, spec)
    index_path = config.spec_root / "spec-index.json"
    index = json.loads(index_path.read_text(encoding="utf-8"))
    entry = next(
        item
        for item in index["runtime_specs"]
        if item["test_id"] == "OV-11" and item["context_tokens"] == 512
    )
    entry["sha256"] = _sha256(spec_path)
    _write_json(index_path, index)
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state["bindings"]["spec_index_sha256"] = _sha256(index_path)
    _write_json(state_path, state)
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report["spec_index_sha256"] = _sha256(index_path)
    report["campaign_state_sha256"] = _sha256(state_path)
    _write_json(report_path, report)

    with pytest.raises(ValueError, match="runtime spec property drift"):
        campaign_cli._validate_preflight_report(config, report_path)


def _write_valid_preflight_report(
    config: AdaptiveCampaignConfig,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> Path:
    monkeypatch.setattr(
        campaign_cli,
        "available_ram_bytes",
        lambda: START_RESERVE_MIB * 1024**2 + 1,
    )
    assert campaign_cli.main(_preflight_cli_argv(config)) == 0
    capsys.readouterr()
    return config.campaign_root / "preflight" / "preflight-report.json"


def test_preflight_report_rejects_safety_probe_path_escape(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    report_path = _write_valid_preflight_report(config, monkeypatch, capsys)
    report = json.loads(report_path.read_text(encoding="utf-8"))
    real_probe = Path(report["zero_survivor_safety_probe_path"])
    escaped = config.campaign_root.parent / "escaped-probe.json"
    escaped.write_bytes(real_probe.read_bytes())
    report["zero_survivor_safety_probe_path"] = str(escaped.resolve())
    _write_json(report_path, report)

    with pytest.raises(ValueError, match="safety probe path escapes campaign root"):
        campaign_cli._validate_preflight_report(config, report_path)


@pytest.mark.parametrize(
    ("hash_field", "label"),
    [
        ("matrix_sha256", "matrix"),
        ("spec_index_sha256", "spec index"),
        ("reference_boundary_index_sha256", "reference boundary index"),
        ("build_provenance_sha256", "build provenance"),
        ("campaign_state_sha256", "campaign state"),
        ("zero_survivor_safety_probe_sha256", "safety probe"),
    ],
)
def test_preflight_report_rejects_substituted_hash_bindings(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    hash_field: str,
    label: str,
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    report_path = _write_valid_preflight_report(config, monkeypatch, capsys)
    report = json.loads(report_path.read_text(encoding="utf-8"))
    report[hash_field] = "0" * 64
    _write_json(report_path, report)

    with pytest.raises(ValueError, match=f"{label} hash binding drift"):
        campaign_cli._validate_preflight_report(config, report_path)


def test_preflight_report_rejects_missing_or_drifted_referenced_evidence(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    report_path = _write_valid_preflight_report(config, monkeypatch, capsys)
    config.reference_boundary_index.unlink()

    with pytest.raises(ValueError, match="reference boundary index is missing"):
        campaign_cli._validate_preflight_report(config, report_path)


def test_preflight_report_rejects_state_changed_after_receipt(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    report_path = _write_valid_preflight_report(config, monkeypatch, capsys)
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    state_path.write_bytes(state_path.read_bytes() + b" ")

    with pytest.raises(ValueError, match="campaign state hash binding drift"):
        campaign_cli._validate_preflight_report(config, report_path)


def test_preflight_report_rejects_hash_consistent_nonzero_survivor_probe(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
) -> None:
    config = _preflight_cli_inputs(tmp_path)
    report_path = _write_valid_preflight_report(config, monkeypatch, capsys)
    report = json.loads(report_path.read_text(encoding="utf-8"))
    state_path = config.campaign_root / "adaptive-campaign-state.json"
    state = json.loads(state_path.read_text(encoding="utf-8"))
    probe_path = Path(report["zero_survivor_safety_probe_path"])
    probe = json.loads(probe_path.read_text(encoding="utf-8"))
    probe["active_pids"] = [123]
    _write_json(probe_path, probe)
    probe_sha256 = _sha256(probe_path)
    state["safety_probes"][-1]["active_pids"] = [123]
    state["safety_probes"][-1]["receipt_sha256"] = probe_sha256
    _write_json(state_path, state)
    report["zero_survivor_safety_probe_sha256"] = probe_sha256
    report["campaign_state_sha256"] = _sha256(state_path)
    report["owned_survivor_count"] = 1
    _write_json(report_path, report)

    with pytest.raises(ValueError, match="zero owned survivors"):
        campaign_cli._validate_preflight_report(config, report_path)


def test_boundary_builder_cli_emits_valid_empty_index(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    config = _campaign_inputs(tmp_path)
    historical = tmp_path / "empty-historical"
    historical.mkdir()
    output = tmp_path / "empty-boundaries.json"

    assert boundary_cli.main(
        [
            "--matrix", str(config.matrix_path),
            "--spec-root", str(config.spec_root),
            "--historical-root", str(historical),
            "--output", str(output),
        ]
    ) == 0

    result = json.loads(output.read_text(encoding="utf-8"))
    assert result["boundaries"] == []
    assert json.loads(capsys.readouterr().out)["boundary_count"] == 0


def test_task_three_native_clean_ram_record_is_retryable(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    failure = _failure_record(config)
    record = dict(failure.record)
    for convenience in (
        "failure_category",
        "ram_query_succeeded",
        "launch_reserve_restored",
    ):
        record.pop(convenience)
    record.update(
        low_memory_stop=True,
        available_ram_bytes={
            "before": 5000 * 1024**2,
            "minimum": 1900 * 1024**2,
            "after": 4500 * 1024**2,
        },
        validation_errors=["available RAM is below the emergency floor"],
    )
    _write_json(failure.record_path, record)
    native = MeasurementFailureRecord(
        role=failure.role,
        record_path=failure.record_path,
        record=record,
        fingerprint=failure.fingerprint,
    )
    runner = _FakeRunner([native, {}])

    result = _run(config, runner)

    assert runner.calls[:2] == [("OV-11", 512), ("OV-11", 512)]
    assert result["steps"]["OV-11:512"]["runtime_status"] == "passed"


def test_resume_rejects_tampered_controller_receipt(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    result = _run(config, _FakeRunner([{} for _ in range(5)]))
    relative = result["steps"]["OV-11:512"]["attempts"][0]["receipt_path"]
    receipt = config.campaign_root / relative
    _write_json(receipt, {"schema": "tampered"})

    with pytest.raises(ValueError, match="receipt"):
        load_or_create_state(config)


def test_crash_after_clean_failure_resumes_with_monotonic_attempt_number(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([_failure_record(config)])
    calls = 0

    def interrupted_ram() -> int:
        nonlocal calls
        calls += 1
        if calls == 2:
            raise KeyboardInterrupt("simulated controller interruption")
        return START_RESERVE_MIB * 1024**2

    with pytest.raises(KeyboardInterrupt):
        run_adaptive_campaign(
            config,
            run_runtime=runner,
            run_quality=None,
            available_ram=interrupted_ram,
        )

    first_receipt = (
        config.campaign_root
        / "controller-receipts"
        / "OV-11"
        / "512"
        / "attempt-001.json"
    )
    before = first_receipt.read_bytes()
    resumed = _run(config, _FakeRunner([{} for _ in range(5)]))

    assert first_receipt.read_bytes() == before
    assert resumed["steps"]["OV-11:512"]["attempt_count"] == 2
    assert (
        config.campaign_root
        / "controller-receipts"
        / "OV-11"
        / "512"
        / "attempt-002.json"
    ).is_file()


def test_preflight_obeys_whole_campaign_lock(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)

    with CampaignLock(config.campaign_root):
        with pytest.raises(RuntimeError, match="locked"):
            preflight_adaptive_campaign(
                config,
                available_ram=lambda: START_RESERVE_MIB * 1024**2,
            )


def test_preflight_rejects_hash_updated_runtime_property_tamper(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    spec_path = config.spec_root / "OV-11" / "512" / "runtime-spec.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    spec["properties"]["INFERENCE_NUM_THREADS"] = 2
    _write_json(spec_path, spec)
    index_path = config.spec_root / "spec-index.json"
    index = json.loads(index_path.read_text(encoding="utf-8"))
    entry = next(
        item
        for item in index["runtime_specs"]
        if item["test_id"] == "OV-11" and item["context_tokens"] == 512
    )
    entry["sha256"] = _sha256(spec_path)
    _write_json(index_path, index)

    with pytest.raises(ValueError, match="property"):
        preflight_adaptive_campaign(
            config,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
        )


def test_unsafe_boundary_persists_campaign_halt_across_resume(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    unsafe = _failure_record(config, cleanup=1)
    first_runner = _FakeRunner([unsafe])

    first = _run(config, first_runner)

    assert first["campaign_halt"]["reason"] == "unsafe-runtime-failure"
    assert eligible_steps(first) == ()
    assert campaign_status(config, first)["next_eligible_step"] is None

    resumed_runner = _FakeRunner([{} for _ in range(5)])
    resumed = _run(config, resumed_runner)

    assert resumed_runner.calls == []
    assert resumed["campaign_halt"] == first["campaign_halt"]
    with pytest.raises(RuntimeError, match="halted"):
        preflight_adaptive_campaign(
            config,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
        )


def test_resume_reopens_actual_artifact_inventory_bytes(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    load_or_create_state(config)
    inventory = tmp_path / "artifact-inventory.json"
    inventory.write_bytes(inventory.read_bytes() + b" ")

    with pytest.raises(ValueError, match="artifact inventory.*drift"):
        load_or_create_state(config)


def test_module_exposes_no_private_runtime_launcher_or_job_ownership_helper() -> None:
    assert not hasattr(adaptive_controller, "_execute_runtime_step")
    assert not hasattr(adaptive_controller, "_run_adaptive_campaign_with_probe")
    assert not hasattr(adaptive_controller, "_CampaignOwnedJobProbe")

    module_functions = {
        name: value
        for name, value in vars(adaptive_controller).items()
        if inspect.isfunction(value)
        and value.__module__ == adaptive_controller.__name__
    }
    module_classes = {
        name
        for name, value in vars(adaptive_controller).items()
        if inspect.isclass(value)
        and value.__module__ == adaptive_controller.__name__
    }
    private_runtime_launchers = []
    job_accepting_functions = []
    callback_accepting_functions = []
    for name, function in module_functions.items():
        signature = inspect.signature(function)
        if name != "run_adaptive_campaign" and (
            "run_runtime" in signature.parameters
            or "run_runtime" in function.__code__.co_names
            or "run_measurement_sequence" in function.__code__.co_names
        ):
            private_runtime_launchers.append(name)
        if any(
            parameter.annotation is KillOnCloseJob
            or "KillOnCloseJob" in str(parameter.annotation)
            or any(
                ownership_word in parameter_name.lower()
                for ownership_word in ("job", "guard", "probe")
            )
            for parameter_name, parameter in signature.parameters.items()
        ) or "KillOnCloseJob" in str(signature.return_annotation):
            job_accepting_functions.append(name)
        if any(
            "Callable" in str(parameter.annotation)
            for parameter in signature.parameters.values()
        ):
            callback_accepting_functions.append(name)

    assert module_classes == {"AdaptiveCampaignConfig", "StepOutcome"}
    assert private_runtime_launchers == []
    assert job_accepting_functions == []
    assert set(callback_accepting_functions) == {
        "_require_start_reserve",
        "run_adaptive_campaign",
        "preflight_adaptive_campaign",
    }

    for entry_point in (run_adaptive_campaign, preflight_adaptive_campaign):
        parameters = inspect.signature(entry_point).parameters
        assert not {
            "campaign_guard",
            "campaign_job",
            "campaign_job_factory",
            "owned_survivor_probe",
            "probe",
        }.intersection(parameters)


def test_nested_runtime_launcher_captures_owned_job_without_ownership_parameters() -> None:
    nested_launcher = next(
        constant
        for constant in run_adaptive_campaign.__code__.co_consts
        if isinstance(constant, CodeType)
        and constant.co_name == "execute_runtime_step"
    )
    parameters = set(
        nested_launcher.co_varnames[
            : nested_launcher.co_argcount + nested_launcher.co_kwonlyargcount
        ]
    )

    assert not {
        "campaign_guard",
        "campaign_job",
        "campaign_job_factory",
        "owned_survivor_probe",
        "probe",
        "run_runtime",
    }.intersection(parameters)
    assert {"campaign_job", "run_runtime"}.issubset(
        nested_launcher.co_freevars
    )


def test_former_private_fake_guard_exploit_cannot_launch(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])

    class FabricatedZeroGuard:
        fake_job = object()

        def require_job(self) -> object:
            return self.fake_job

        def __call__(self, _campaign_root: Path) -> dict[str, object]:
            return {
                "schema": "official-openvino-adaptive-job-probe/v1",
                "job_name": "fabricated",
                "query_ok": True,
                "active_pids": [],
            }

    private_runner = getattr(
        adaptive_controller,
        "_run_adaptive_campaign_with_probe",
        None,
    )
    if private_runner is not None:
        try:
            private_runner(
                config,
                run_runtime=runner,
                run_quality=None,
                publish_checkpoint=None,
                available_ram=lambda: START_RESERVE_MIB * 1024**2,
                campaign_guard=FabricatedZeroGuard(),
            )
        except (RuntimeError, TypeError):
            pass

    assert runner.calls == []


@pytest.mark.skipif(os.name != "nt", reason="Windows Job Objects are required")
def test_unrelated_real_job_has_no_private_runtime_launcher_target(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    state = load_or_create_state(config)
    spec_path = config.spec_root / "OV-11" / "512" / "runtime-spec.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    runner = _FakeRunner([{}])
    suffix = hashlib.sha256(str(tmp_path).encode("utf-8")).hexdigest()[:16]
    unrelated_job = KillOnCloseJob(f"WB04-unrelated-adaptive-job-{suffix}")
    private_launcher = getattr(adaptive_controller, "_execute_runtime_step", None)
    try:
        if private_launcher is not None:
            private_launcher(
                state,
                config=config,
                test_id="OV-11",
                context=512,
                spec_path=spec_path,
                spec=spec,
                run_runtime=runner,
                available_ram=lambda: START_RESERVE_MIB * 1024**2,
                campaign_job=unrelated_job,
            )
    finally:
        unrelated_job.close()

    assert runner.calls == []
    assert private_launcher is None


def test_public_fake_campaign_job_factory_is_rejected_before_launch(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])

    with pytest.raises(TypeError, match="campaign_job_factory"):
        run_adaptive_campaign(
            config,
            run_runtime=runner,
            run_quality=None,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
            campaign_job_factory=lambda _: object(),
        )

    assert runner.calls == []


def test_preflight_public_fake_campaign_job_factory_is_rejected(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)

    with pytest.raises(TypeError, match="campaign_job_factory"):
        preflight_adaptive_campaign(
            config,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
            campaign_job_factory=lambda _: object(),
        )


def test_live_campaign_job_proof_fails_closed_before_launch(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])

    def fail_query(_job: KillOnCloseJob) -> list[int]:
        raise OSError("simulated real Job Object query failure")

    monkeypatch.setattr(KillOnCloseJob, "active_pids", fail_query)

    result = run_adaptive_campaign(
        config,
        run_runtime=runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )

    assert runner.calls == []
    assert "query" in result["campaign_halt"]["reason"]


@pytest.mark.skipif(os.name != "nt", reason="Windows Job Objects are required")
def test_closed_internally_created_campaign_job_is_rejected_before_query(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])
    real_init = KillOnCloseJob.__init__
    query_called = False

    def create_closed_job(job: KillOnCloseJob, name: str) -> None:
        real_init(job, name)
        job.close()

    def fabricated_query(_job: KillOnCloseJob) -> list[int]:
        nonlocal query_called
        query_called = True
        return []

    monkeypatch.setattr(KillOnCloseJob, "__init__", create_closed_job)
    monkeypatch.setattr(KillOnCloseJob, "active_pids", fabricated_query)

    result = run_adaptive_campaign(
        config,
        run_runtime=runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )

    assert runner.calls == []
    assert query_called is False
    assert result["campaign_halt"]["reason"] == (
        "owned-survivor-proof-query-failed"
    )


@pytest.mark.skipif(os.name != "nt", reason="Windows Job Objects are required")
def test_campaign_job_handle_is_revalidated_before_task_three(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])
    real_active_pids = KillOnCloseJob.active_pids
    query_count = 0

    def close_after_launch_query(job: KillOnCloseJob) -> list[int]:
        nonlocal query_count
        active = real_active_pids(job)
        query_count += 1
        if query_count == 2:
            job.close()
        return active

    monkeypatch.setattr(
        KillOnCloseJob,
        "active_pids",
        close_after_launch_query,
    )

    result = run_adaptive_campaign(
        config,
        run_runtime=runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )

    assert runner.calls == []
    assert "handle is not open" in result["campaign_halt"]["detail"]["error"]


def test_live_zero_campaign_job_proof_is_required_for_every_launch(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])

    result = run_adaptive_campaign(
        config,
        run_runtime=runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )

    assert len(runner.calls) == 5
    assert all(item["query_ok"] is True for item in result["safety_probes"])


def test_legacy_zero_callback_cannot_bypass_real_campaign_job(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])

    with pytest.raises(TypeError, match="owned_survivor_probe"):
        run_adaptive_campaign(
            config,
            run_runtime=runner,
            run_quality=None,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
            owned_survivor_probe=lambda _: {
                "schema": "official-openvino-adaptive-job-probe/v1",
                "query_ok": True,
                "active_pids": [],
            },
        )

    assert runner.calls == []


def test_real_campaign_job_is_exact_object_passed_to_every_runtime_launch(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    config = _campaign_inputs(tmp_path)
    runner = _FakeRunner([{} for _ in range(5)])
    real_active_pids = KillOnCloseJob.active_pids
    queried_jobs: list[KillOnCloseJob] = []

    def record_query(job: KillOnCloseJob) -> list[int]:
        queried_jobs.append(job)
        return real_active_pids(job)

    monkeypatch.setattr(KillOnCloseJob, "active_pids", record_query)

    result = run_adaptive_campaign(
        config,
        run_runtime=runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )

    assert len(runner.calls) == 5
    assert all(type(job) is KillOnCloseJob for job in runner.campaign_jobs)
    assert all(job is runner.campaign_jobs[0] for job in runner.campaign_jobs)
    assert queried_jobs
    assert all(job is runner.campaign_jobs[0] for job in queried_jobs)
    assert getattr(runner.campaign_jobs[0], "_handle", object()) is None
    assert result["campaign_halt"] is None


@pytest.mark.skipif(os.name != "nt", reason="Windows Job Objects are required")
def test_nonempty_real_campaign_job_blocks_controller_launch(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    identity = hashlib.sha256(
        str(config.campaign_root.resolve()).encode("utf-8")
    ).hexdigest()[:24]
    campaign_job = KillOnCloseJob(f"WB04-adaptive-campaign-{identity}")
    children = [
        subprocess.Popen(
            [sys.executable, "-c", "import time; time.sleep(30)"],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            creationflags=(
                subprocess.CREATE_NEW_PROCESS_GROUP | CREATE_SUSPENDED
            ),
        )
        for _ in range(2)
    ]
    for child in children:
        campaign_job.assign_pid(child.pid)
    runner = _FakeRunner([{}])
    try:
        result = run_adaptive_campaign(
            config,
            run_runtime=runner,
            run_quality=None,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
        )
    finally:
        try:
            campaign_job.close()
        except OSError:
            pass
        for child in children:
            try:
                child.wait(timeout=5)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait(timeout=5)

    assert runner.calls == []
    assert result["safety_probes"][0]["active_pids"] == sorted(
        child.pid for child in children
    )
    assert result["campaign_halt"]["reason"] == (
        "owned-survivor-proof-survivors-present"
    )


def test_missing_task_three_job_cleanup_proof_is_unsafe(tmp_path: Path) -> None:
    config = _campaign_inputs(tmp_path)
    failure = _failure_record(config)
    record = dict(failure.record)
    record.pop("workload_job", None)
    record.pop("sampler_job", None)
    _write_json(failure.record_path, record)
    native = MeasurementFailureRecord(
        role=failure.role,
        record_path=failure.record_path,
        record=record,
        fingerprint=failure.fingerprint,
    )
    runner = _FakeRunner([native, {}])

    result = _run(config, runner)

    assert runner.calls == [("OV-11", 512)]
    assert result["steps"]["OV-11:512"]["runtime_status"] == "safety-boundary"


def test_boundary_builder_accepts_genuine_task_three_validated_manifest_pair(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / "native-historical"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
        )

    result = build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=tmp_path / "native-boundaries.json",
    )

    assert len(result["boundaries"]) == 1
    boundary = result["boundaries"][0]
    assert (boundary["test_id"], boundary["context_tokens"]) == ("OV-12", 4096)
    assert boundary["identity"]["launch_reserve_mib"] == 4096
    assert boundary["identity"]["emergency_floor_mib"] == 2048
    assert boundary["native_campaign_identity_sha256"] == _sha256_json(
        json.loads(
            (historical / "campaign-identity.json").read_text(encoding="utf-8")
        )["identity"]
    )


@pytest.mark.parametrize(
    "manifest_change",
    [
        "minimal",
        "missing-schema",
        "missing-status",
        "missing-conversion-provenance",
        "missing-file-inventory",
        "invalid-cpu-load-probe-binding",
    ],
)
def test_boundary_builder_rejects_manifest_that_task_three_rejects(
    tmp_path: Path,
    manifest_change: str,
) -> None:
    config = _campaign_inputs(
        tmp_path,
        max_context=4096,
        u8_manifest_change=manifest_change,
    )
    historical = tmp_path / f"invalid-manifest-{manifest_change}"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
            allow_invalid_artifact_manifest=True,
        )

    result = build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=tmp_path / f"invalid-manifest-{manifest_change}.json",
    )

    assert result["boundaries"] == []


def test_boundary_builder_rejects_unbound_top_level_convenience_identity(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / "mismatched-native-historical"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
            identity_artifact_id="different-artifact",
            include_convenience_identity=True,
        )

    result = build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=tmp_path / "mismatched-native-boundaries.json",
    )

    assert result["boundaries"] == []


def test_boundary_builder_rejects_convenience_mib_when_native_byte_floor_differs(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / "wrong-byte-floor-historical"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
            include_convenience_identity=True,
            launch_floor_bytes=START_RESERVE_MIB * 1024**2 + 1,
        )

    result = build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=tmp_path / "wrong-byte-floor-boundaries.json",
    )

    assert result["boundaries"] == []


@pytest.mark.parametrize(
    ("section", "change"),
    [
        ("build", "missing"),
        ("build", "mismatched"),
        ("prompt", "missing"),
        ("prompt", "mismatched"),
        ("runtime", "missing"),
        ("runtime", "mismatched"),
    ],
)
def test_boundary_builder_rejects_incomplete_or_mismatched_native_section(
    tmp_path: Path,
    section: str,
    change: str,
) -> None:
    config = _campaign_inputs(tmp_path, max_context=4096)
    historical = tmp_path / f"{section}-{change}-historical"
    for number in (1, 2):
        _write_native_task_three_failure(
            config,
            historical,
            attempt_number=number,
            identity_section_change=(section, change),
        )

    result = build_boundary_index(
        matrix_path=config.matrix_path,
        spec_root=config.spec_root,
        historical_root=historical,
        output_path=tmp_path / f"{section}-{change}-boundaries.json",
    )

    assert result["boundaries"] == []


def test_artifact_preparation_terminal_is_receipted_checkpointed_and_resumable(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path, fp16_terminal=True)
    runner = _FakeRunner([{} for _ in range(4)])
    checkpoints: list[tuple[Path, bool]] = []

    result = _run(config, runner, checkpoints=checkpoints)

    assert len(runner.calls) == 4
    terminal = result["steps"]["OV-13:512"]
    assert terminal["runtime_status"] == "artifact-preparation-terminal"
    assert terminal["attempt_count"] == 0
    terminal_receipt = config.campaign_root / terminal["terminal_receipt_path"]
    assert terminal_receipt.is_file()
    assert _sha256(terminal_receipt) == terminal["terminal_receipt_sha256"]
    before = terminal_receipt.read_bytes()
    assert len(checkpoints) == 5

    resumed_runner = _FakeRunner([{}])
    resumed_checkpoints: list[tuple[Path, bool]] = []
    resumed = _run(
        config,
        resumed_runner,
        checkpoints=resumed_checkpoints,
    )

    assert resumed_runner.calls == []
    assert resumed_checkpoints == []
    assert terminal_receipt.read_bytes() == before
    assert resumed["steps"]["OV-13:512"] == terminal


class _CrashAfterNativeTaskThreeFailure:
    def __init__(self, config: AdaptiveCampaignConfig, attempt_number: int):
        self.config = config
        self.attempt_number = attempt_number
        self.calls: list[tuple[str, int]] = []

    def __call__(self, **kwargs: object) -> dict[str, object]:
        spec = json.loads(Path(kwargs["spec_path"]).read_text(encoding="utf-8"))
        test_id = spec["controlled_test_id"]
        context = spec["context_tokens"]
        self.calls.append((test_id, context))
        _write_native_task_three_failure(
            self.config,
            Path(kwargs["campaign_root"]),
            attempt_number=self.attempt_number,
            test_id=test_id,
            context=context,
        )
        raise KeyboardInterrupt("simulated crash before controller receipt")


def test_resume_reconciles_native_failure_persisted_before_controller_receipt(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    crash = _CrashAfterNativeTaskThreeFailure(config, 1)

    with pytest.raises(KeyboardInterrupt):
        run_adaptive_campaign(
            config,
            run_runtime=crash,
            run_quality=None,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
        )

    resumed_runner = _FakeRunner([{} for _ in range(5)])
    resumed = run_adaptive_campaign(
        config,
        run_runtime=resumed_runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )

    assert resumed_runner.calls.count(("OV-11", 512)) == 1
    assert resumed["steps"]["OV-11:512"]["attempt_count"] == 2
    assert [
        item["attempt_number"]
        for item in resumed["steps"]["OV-11:512"]["attempts"]
    ] == [1, 2]


def test_two_unreceipted_native_failures_close_boundary_without_third_launch(
    tmp_path: Path,
) -> None:
    config = _campaign_inputs(tmp_path)
    first_crash = _CrashAfterNativeTaskThreeFailure(config, 1)
    with pytest.raises(KeyboardInterrupt):
        run_adaptive_campaign(
            config,
            run_runtime=first_crash,
            run_quality=None,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
        )

    second_crash = _CrashAfterNativeTaskThreeFailure(config, 2)
    with pytest.raises(KeyboardInterrupt):
        run_adaptive_campaign(
            config,
            run_runtime=second_crash,
            run_quality=None,
            available_ram=lambda: START_RESERVE_MIB * 1024**2,
        )

    forbidden_runner = _FakeRunner([{}])
    resumed = run_adaptive_campaign(
        config,
        run_runtime=forbidden_runner,
        run_quality=None,
        available_ram=lambda: START_RESERVE_MIB * 1024**2,
    )

    assert forbidden_runner.calls.count(("OV-11", 512)) == 0
    step = resumed["steps"]["OV-11:512"]
    assert step["attempt_count"] == 2
    assert step["runtime_status"] == "boundary-confirmed"
