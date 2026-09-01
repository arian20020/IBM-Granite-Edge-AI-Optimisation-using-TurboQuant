"""Behavioral tests for independent adaptive comparison reconciliation."""

from __future__ import annotations

import hashlib
import json
import os
import statistics
import subprocess
import sys
from copy import deepcopy
from pathlib import Path
from typing import Any, Callable, Mapping

import pytest

from scripts.testing.campaigns.openvino.adaptive_metrics import IDENTITY_HASH_FIELDS
from scripts.testing.campaigns.openvino.adaptive_campaign import CANDIDATE_ORDER, CONTEXTS
from scripts.testing.campaigns.openvino.adaptive_campaign_spec import (
    generate_adaptive_format_comparison_specs,
)
from scripts.testing.campaigns.openvino import adaptive_quality
from scripts.testing.campaigns.openvino.adaptive_quality import (
    capture_isolated_quality_campaign,
)
from scripts.testing.campaigns.openvino.matrix import load_adaptive_comparison_matrix
from scripts.testing.tests.test_measure_official_openvino_sequence import _record
from scripts.testing.tests.test_official_openvino_adaptive_campaign import _build_matrix
from scripts.testing.measure_official_openvino import run_measurement_sequence
from scripts.testing.campaigns.openvino.metrics import summarize_samples
from scripts.testing.campaigns.openvino.runtime_process import measurement_sample
from scripts.testing.adjudicate_official_openvino_adaptive_quality import (
    adjudicate_adaptive_quality,
    build_adaptive_blind_bundle,
)
from scripts.testing.tests.test_adjudicate_official_openvino_adaptive_quality import (
    PassingGuardRunner,
    _empty_manual,
    _empty_pairwise,
    _score_sheet,
)
from scripts.testing.tests.test_official_openvino_adaptive_quality import MIB

from scripts.testing.campaigns.openvino.comparison_reconcile import (
    ComparisonKey,
    ComparisonQualityOutcome,
    ComparisonRelease,
    ComparisonRuntimeOutcome,
    build_comparison_release_input,
    reconcile_comparison_release,
    reconcile_terminal,
    validate_complete_release,
    validate_closed_campaign,
)


_RUNTIME_PROMPT = "Synthetic governed comparison prompt."
_RUNTIME_PROMPT_SHA256 = hashlib.sha256(
    _RUNTIME_PROMPT.encode("utf-8")
).hexdigest()


def _runtime_config(context: int) -> dict[str, Any]:
    return {
        "device": "CPU",
        "max_new_tokens": 4,
        "expected_input_tokens": context,
        "ignore_eos": True,
        "seed": 42,
        "apply_chat_template": False,
        "properties": {},
    }


def _canonical_bytes(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=True,
            sort_keys=True,
            separators=(",", ":"),
            allow_nan=False,
        )
        + "\n"
    ).encode("utf-8")


def _canonical_sha256(value: Any) -> str:
    return hashlib.sha256(_canonical_bytes(value).rstrip(b"\n")).hexdigest()


def _sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _write_json(path: Path, value: Any) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(_canonical_bytes(value))
    return path


def _reference(path: Path, *, base: Path | None = None) -> dict[str, str]:
    source = Path(path).resolve()
    if base is None:
        return {"path": str(source), "sha256": _sha256(source)}
    return {
        "path": os.path.relpath(source, Path(base).resolve()).replace("\\", "/"),
        "sha256": _sha256(source),
    }


def _base(release_input: dict[str, Any]) -> Path:
    return Path(release_input["__base_path"]).resolve()


def _path(release_input: dict[str, Any], reference: Mapping[str, str]) -> Path:
    return (_base(release_input) / reference["path"]).resolve()


def _resign(value: dict[str, Any], field: str) -> dict[str, Any]:
    value.pop(field, None)
    value[field] = _canonical_sha256(value)
    return value


def _resign_campaign_identity(value: dict[str, Any]) -> dict[str, Any]:
    value["campaign_identity_sha256"] = _canonical_sha256(value["identity"])
    return value


def _percent(values: list[float]) -> dict[str, Any]:
    return {
        "values": values,
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "peak": max(values),
        "count": len(values),
        "query_succeeded": True,
    }


def _sample_record(
    *,
    role: str,
    ordinal: int,
    test_id: str,
    context: int,
    artifact_sha256: str,
    campaign_identity_sha256: str,
    matrix_identity: Mapping[str, Any],
) -> dict[str, Any]:
    formal_position = ordinal - 2 if role.startswith("sample-") else ordinal
    record = _record(
        role,
        ordinal,
        {
            "model_path": str(Path("synthetic-model").resolve()),
            "device": "CPU",
            "controlled_test_id": test_id,
            "context": context,
            "expected_input_tokens": context,
        },
    )
    worker = record["worker"]
    worker.update(
        {
            "controlled_test_id": test_id,
            "context": context,
            "expected_input_tokens": context,
            "ttft_ms": float(formal_position * 10),
            "num_input_tokens": context,
        }
    )
    record["peak_private_bytes"] = (2600 + formal_position * 100) * 1024**2
    record["available_ram_bytes"] = {
        "before": 3000 * 1024**2,
        "minimum": (2100 + formal_position * 100) * 1024**2,
        "after": 2800 * 1024**2,
    }
    cpu_values = {
        1: [0.0, 0.0, 100.0],
        2: [0.0, 100.0, 100.0],
        3: [0.0, 0.0, 100.0],
    }.get(formal_position, [10.0, 20.0, 30.0])
    gpu_values = {
        1: [0.0, 5.0, 10.0],
        2: [0.0, 10.0, 20.0],
        3: [0.0, 5.0, 10.0],
    }.get(formal_position, [1.0, 2.0, 3.0])
    record["cpu_percent"] = _percent(cpu_values)
    record["gpu_percent"] = _percent(gpu_values)
    hashes = {
        "artifact_manifest_sha256": artifact_sha256,
        "prompt_sha256": _RUNTIME_PROMPT_SHA256,
        "matrix_sha256": _canonical_sha256(matrix_identity),
        "build_provenance_sha256": "c" * 64,
        "command_sha256": _canonical_sha256(record["command"]),
        "evidence_sha256": campaign_identity_sha256,
    }
    assert set(hashes) == set(IDENTITY_HASH_FIELDS)
    record["identity_hashes"] = hashes
    record["runtime_property_sha256"] = _canonical_sha256(_runtime_config(context))
    return record


def _attempt_receipt(
    root: Path,
    *,
    role: str,
    record: dict[str, Any],
    test_id: str,
    context: int,
    campaign_identity_sha256: str,
    attempt_number: int = 1,
) -> dict[str, Any]:
    attempt = root / "attempts" / role / f"attempt-{attempt_number:03d}"
    worker = record["worker"]
    config = _runtime_config(context)
    spec = {
        "schema": "official-openvino-wb04-worker-spec/v1",
        "role": role,
        "controlled_test_id": test_id,
        "model_path": worker["model_path"],
        "device": worker["device"],
        "prompt": _RUNTIME_PROMPT,
        "context": context,
        **config,
        "campaign_identity_sha256": campaign_identity_sha256,
    }
    spec_path = _write_json(attempt / "spec.json", spec)
    record_path = _write_json(attempt / "run" / "attempt.json", record)
    receipt = {
        "schema": "official-openvino-wb04-sequence-receipt/v1",
        "role": role,
        "attempt_number": attempt_number,
        "campaign_identity_sha256": campaign_identity_sha256,
        "spec_sha256": _canonical_sha256(spec),
        "spec_path": spec_path.relative_to(root).as_posix(),
        "spec_file_sha256": _sha256(spec_path),
        "runtime_record_path": record_path.relative_to(root).as_posix(),
        "runtime_record_sha256": _sha256(record_path),
        "accepted": True,
    }
    _write_json(attempt / "sequence-receipt.json", receipt)
    return receipt


def _comparison_foundation(tmp_path: Path, matrix_path: Path) -> dict[str, Path]:
    """Build the exact Task 3/4 inputs without launching inference."""

    bindings = tmp_path / "bindings"
    build_root = bindings / "build"
    package = build_root / "openvino_genai"
    package.mkdir(parents=True, exist_ok=True)
    (package / "__init__.py").write_text("", encoding="utf-8")
    (package / "py_openvino_genai.synthetic.pyd").write_bytes(b"extension")
    (package / "openvino_genai.dll").write_bytes(b"runtime")
    cache_root = bindings / "cache"
    cache_root.mkdir(exist_ok=True)
    spec_root = bindings / "specs"
    generate_adaptive_format_comparison_specs(
        matrix_path=matrix_path,
        build_root=build_root,
        artifact_inventory_path=matrix_path.parent / "artifact-inventory.json",
        cache_root=cache_root,
        output_root=spec_root,
    )
    provenance = _write_json(
        bindings / "build-provenance.json",
        {"schema": "test-build", "status": "passed"},
    )
    site_packages = bindings / "site-packages"
    (site_packages / "openvino").mkdir(parents=True, exist_ok=True)
    libraries = bindings / "openvino-libraries"
    libraries.mkdir(exist_ok=True)
    (libraries / "openvino.dll").write_bytes(b"openvino")
    sampler = bindings / "sampler.ps1"
    sampler.write_text("# sampler\n", encoding="utf-8")
    return {
        "build_root": build_root,
        "provenance": provenance,
        "spec_root": spec_root,
        "site_packages": site_packages,
        "libraries": libraries,
        "sampler": sampler,
    }


def _authoritative_record(
    *,
    role: str,
    ordinal: int,
    worker_spec: Mapping[str, Any],
    case: Mapping[str, Any],
    artifact_sha256: str,
    context: int,
) -> dict[str, Any]:
    record = _sample_record(
        role=role,
        ordinal=ordinal,
        test_id=str(worker_spec["controlled_test_id"]),
        context=context,
        artifact_sha256=artifact_sha256,
        campaign_identity_sha256="a" * 64,
        matrix_identity={},
    )
    record["worker"].update(
        model_path=str(Path(str(worker_spec["model_path"])).resolve()),
        device=worker_spec["device"],
        num_input_tokens=context,
    )
    activation = record["activation"]
    key_algorithm = case["runtime_key_algorithm"]
    value_algorithm = case["runtime_value_algorithm"]
    key_precision = case["key_cache_precision"]
    value_precision = case["value_cache_precision"]
    activation.update(
        requested_key_algorithm=key_algorithm,
        requested_value_algorithm=value_algorithm,
        activated_key_algorithm=key_algorithm,
        activated_value_algorithm=value_algorithm,
        requested_key_cache_precision=key_precision,
        requested_value_cache_precision=value_precision,
        activated_key_cache_precision=key_precision,
        activated_value_cache_precision=value_precision,
        observed_key_state_precision=(
            "u8+f32+i32" if key_algorithm != "STANDARD" else key_precision
        ),
        observed_value_state_precision=(
            "u8+f32+i32" if value_algorithm != "STANDARD" else value_precision
        ),
        norm_correction=case["norm_correction"],
        attention_path=case["attention_path"],
        device=worker_spec["device"],
        actual_device=worker_spec["device"],
    )
    if key_algorithm == value_algorithm == "STANDARD":
        standard = 100 * 1024**2
        activation.update(
            status="not_requested",
            expected_bytes=standard,
            actual_bytes=standard,
            expected_persistent_standard_bytes=standard,
            actual_persistent_standard_bytes=standard,
            expected_persistent_payload_bytes=0,
            actual_persistent_payload_bytes=0,
            expected_persistent_norm_bytes=0,
            actual_persistent_norm_bytes=0,
            expected_persistent_metadata_bytes=0,
            actual_persistent_metadata_bytes=0,
            decoded_scratch_bytes=0,
            full_precision_equivalent_bytes=standard,
            operation_type="not_requested",
            operation_count=0,
            matched_state_count=0,
            transformed_model_hash="not_requested",
            runtime_layer_type="not_requested",
        )
    return record


def _runtime_step(
    root: Path,
    *,
    test_id: str,
    context: int,
    matrix_path: Path,
    foundation: Mapping[str, Path],
    attempt_number: int = 1,
) -> dict[str, Any]:
    adaptive_spec_path = foundation["spec_root"] / test_id / str(context) / "runtime-spec.json"
    adaptive_spec = json.loads(adaptive_spec_path.read_text(encoding="utf-8"))
    matrix_payload = json.loads(matrix_path.read_text(encoding="utf-8"))
    case = next(case for case in matrix_payload["cases"] if case["test_id"] == test_id)
    template = {
        "schema": "official-openvino-wb04-worker-spec/v1",
        "role": "pilot",
        "controlled_test_id": test_id,
        "model_path": adaptive_spec["model_path"],
        "device": adaptive_spec["device"],
        "prompt": adaptive_spec["workload"]["prompt"],
        "context": context,
        "max_new_tokens": adaptive_spec["max_new_tokens"],
        "expected_input_tokens": context,
        "ignore_eos": adaptive_spec["ignore_eos"],
        "seed": adaptive_spec["seed"],
        "apply_chat_template": adaptive_spec["apply_chat_template"],
        "properties": adaptive_spec["properties"],
    }
    template_path = _write_json(root / "worker-template.json", template)
    roles = ("pilot", "warmup", "sample-1", "sample-2", "sample-3")
    if attempt_number > 1:
        for role in roles:
            # The runner owns the real accepted attempt.  These immutable,
            # non-accepted predecessors ensure its actual receipt number is
            # greater than one without inventing a receipt in the fixture.
            (root / "attempts" / role / "attempt-001").mkdir(
                parents=True,
                exist_ok=False,
            )

    def fake_measurement(**run_kwargs: Any) -> dict[str, Any]:
        role = str(run_kwargs["role"])
        worker_spec = json.loads(Path(run_kwargs["spec_path"]).read_text(encoding="utf-8"))
        record = _authoritative_record(
            role=role,
            ordinal=roles.index(role) + 1,
            worker_spec=worker_spec,
            case=case,
            artifact_sha256=adaptive_spec["artifact_manifest_sha256"],
            context=context,
        )
        identity = json.loads(
            (root / "campaign-identity.json").read_text(encoding="utf-8")
        )["identity"]
        record["identity_hashes"] = {
            "artifact_manifest_sha256": identity["model"]["artifact_manifest_sha256"],
            "prompt_sha256": identity["prompt"]["sha256"],
            "matrix_sha256": _canonical_sha256(identity["matrix"]),
            "build_provenance_sha256": identity["build"]["provenance_sha256"],
            "command_sha256": _canonical_sha256(record["command"]),
            "evidence_sha256": worker_spec["campaign_identity_sha256"],
        }
        record["runtime_property_sha256"] = _canonical_sha256(identity["config"])
        _write_json(Path(run_kwargs["output_dir"]) / "attempt.json", record)
        return record

    sequence = run_measurement_sequence(
        spec_path=template_path,
        campaign_root=root,
        matrix_path=matrix_path,
        artifact_manifest_path=Path(adaptive_spec["artifact_manifest_path"]),
        build_provenance_path=foundation["provenance"],
        build_root=foundation["build_root"],
        repo_root=Path(__file__).resolve().parents[3],
        python_executable=Path(sys.executable),
        python_site_packages=foundation["site_packages"],
        openvino_libraries=foundation["libraries"],
        sampler_script=foundation["sampler"],
        run_measurement=fake_measurement,
    )
    sequence_path = root / "attempt-sequence.json"
    assert json.loads(sequence_path.read_text(encoding="utf-8")) == sequence
    return {
        "test_id": test_id,
        "context_tokens": context,
        "runtime": _reference(sequence_path, base=root.parents[2]),
    }


def _quality_step(
    root: Path,
    *,
    test_id: str,
    context: int,
    complete: bool = True,
) -> dict[str, dict[str, str]]:
    receipts = []
    for prompt_id in ("P1", "P2", "P3", "P4", "P5", "P6"):
        worker = _write_json(
            root / "workers" / f"{prompt_id}.json",
            _resign(
                {
                    "schema": "official-openvino-adaptive-quality-prompt-result/v1",
                    "prompt_id": prompt_id,
                    "outcomes": [],
                },
                "worker_result_sha256",
            ),
        )
        receipts.append(
            {
                "prompt_id": prompt_id,
                "status": "passed" if complete else "not-run",
                "worker_result_path": str(worker.resolve()),
                "worker_result_sha256": _sha256(worker),
            }
        )
    capture = _write_json(
        root / "capture.json",
        _resign(
            {
                "schema": "official-openvino-adaptive-quality-capture/v1",
                "prompt_receipts": receipts,
            },
            "capture_summary_sha256",
        ),
    )
    adjudication = _write_json(
        root / "adjudication.json",
        _resign(
            {
                "schema": "official-openvino-adaptive-quality-adjudication/v1",
                "configurations": [
                    {
                        "test_id": test_id,
                        "context_tokens": context,
                        "status": "complete",
                        "prompt_scores": {
                            prompt_id: float(index)
                            for index, prompt_id in enumerate(
                                ("P1", "P2", "P3", "P4", "P5", "P6"),
                                start=1,
                            )
                        },
                        "aggregates": {"mean": 3.5},
                    }
                ],
            },
            "adjudication_sha256",
        ),
    )
    base = root.parents[2]
    return {
        "quality_capture": _reference(capture, base=base),
        "quality_adjudication": _reference(adjudication, base=base),
    }


def _release_input(steps: list[dict[str, Any]], matrix: Path, *, base: Path) -> dict[str, Any]:
    value = _resign(
        {
            "schema": "official-openvino-comparison-release-input-v1",
            "matrix": _reference(matrix, base=base),
            "steps": steps,
        },
        "release_input_sha256",
    )
    value["__base_path"] = str(Path(base).resolve())
    return value


def _quality_ready_comparison_matrix(root: Path) -> Path:
    """Keep optional unavailable-artifact fields absent, as production does."""

    matrix = _build_matrix(root)
    payload = json.loads(matrix.read_text(encoding="utf-8"))
    payload["schema_version"] = 1
    for case in payload["cases"]:
        if isinstance(case, dict):
            for field in (
                "artifact_terminal_path",
                "artifact_terminal_sha256",
            ):
                if case.get(field) is None:
                    case.pop(field, None)
    return _write_json(matrix, payload)


def valid_release_input(
    tmp_path: Path,
    *,
    ids: tuple[str, ...] = ("OV-11",),
    contexts: tuple[int, ...] = (512,),
    attempt_number: int = 1,
) -> dict[str, Any]:
    matrix_root = tmp_path / "matrix"
    matrix_root.mkdir()
    matrix = _quality_ready_comparison_matrix(matrix_root)
    foundation = _comparison_foundation(tmp_path, matrix)
    steps = []
    for test_id in ids:
        for context in contexts:
            root = tmp_path / "runtime" / test_id / str(context)
            step = _runtime_step(
                root,
                test_id=test_id,
                context=context,
                matrix_path=matrix,
                foundation=foundation,
                attempt_number=attempt_number,
            )
            steps.append(step)
    return _with_state(tmp_path, _release_input(steps, matrix, base=tmp_path))


def _resign_release_input(release_input: dict[str, Any]) -> None:
    base = release_input.pop("__base_path", None)
    _resign(release_input, "release_input_sha256")
    if base is not None:
        release_input["__base_path"] = base


def _find_step(release_input: dict[str, Any], test_id: str, context: int) -> dict[str, Any]:
    return next(
        step
        for step in release_input["steps"]
        if step["test_id"] == test_id and step["context_tokens"] == context
    )


def corrupt_declared_summary(release_input: dict[str, Any], *, field: str, value: float) -> None:
    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    summary = sequence_path.parent / sequence["measurement_summary_path"]
    declared = json.loads(summary.read_text(encoding="utf-8"))
    target: dict[str, Any] = declared
    parts = field.split(".")
    for part in parts[:-1]:
        target = target[part]
    target[parts[-1]] = value
    _write_json(summary, declared)
    sequence["measurement_summary_sha256"] = _sha256(summary)
    _write_json(sequence_path, sequence)
    _resign_runtime_step(release_input, step)


def _refresh_state_runtime_binding(release_input: dict[str, Any], step: dict[str, Any]) -> None:
    state_path = _path(release_input, release_input["campaign_state"])
    state = json.loads(state_path.read_text(encoding="utf-8"))
    key = f"{step['test_id']}:{step['context_tokens']}"
    state_step = state["steps"][key]
    sequence = _path(release_input, step["runtime"])
    evidence_sha = _sha256(sequence)
    state_step["evidence_path"] = str(sequence)
    state_step["evidence_sha256"] = evidence_sha
    controller = state_step["attempts"][-1]
    receipt_path = state_path.parent / controller["receipt_path"]
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["evidence_path"] = str(sequence)
    receipt["evidence_sha256"] = evidence_sha
    _write_json(receipt_path, receipt)
    controller.update(
        evidence_path=str(sequence), evidence_sha256=evidence_sha,
        receipt_sha256=_sha256(receipt_path),
    )
    recovery = state_step.get("quality_recovery")
    if isinstance(recovery, dict):
        recovery["runtime_evidence_path"] = str(sequence)
        recovery["runtime_evidence_sha256"] = evidence_sha
        summary = sequence.parent / "measurement-summary.json"
        if summary.is_file():
            recovery["runtime_summary"] = str(summary)
            recovery["runtime_summary_sha256"] = _sha256(summary)
        pilot_spec = Path(str(recovery["pilot_spec_path"]))
        if pilot_spec.is_file():
            recovery["pilot_spec_sha256"] = _sha256(pilot_spec)
        _resign(recovery, "quality_recovery_sha256")
    _write_json(state_path, state)
    release_input["campaign_state"] = _reference(state_path, base=_base(release_input))


def _resign_runtime_step(release_input: dict[str, Any], step: dict[str, Any]) -> None:
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    for receipt in (sequence["pilot"], sequence["warmup"], *sequence["accepted_samples"]):
        raw = sequence_path.parent / receipt["runtime_record_path"]
        receipt["runtime_record_sha256"] = _sha256(raw)
        receipt_path = raw.parent.parent / "sequence-receipt.json"
        _write_json(receipt_path, receipt)
    _write_json(sequence_path, sequence)
    step["runtime"] = _reference(sequence_path, base=_base(release_input))
    _refresh_state_runtime_binding(release_input, step)
    _resign_release_input(release_input)


def _add_unreferenced_accepted_attempt(
    release_input: dict[str, Any],
    *,
    role: str = "pilot",
) -> Path:
    """Create valid Task 3 evidence that the release never names."""

    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    selected = (
        sequence[role]
        if role in {"pilot", "warmup"}
        else next(
            item for item in sequence["accepted_samples"] if item["role"] == role
        )
    )
    receipt = dict(selected)
    root = sequence_path.parent
    source_spec = root / receipt["spec_path"]
    source_record = root / receipt["runtime_record_path"]
    destination = root / "attempts" / role / "attempt-002"
    spec_path = _write_json(
        destination / "spec.json",
        json.loads(source_spec.read_text(encoding="utf-8")),
    )
    record_path = _write_json(
        destination / "run" / "attempt.json",
        json.loads(source_record.read_text(encoding="utf-8")),
    )
    receipt.update(
        attempt_number=2,
        spec_path=spec_path.relative_to(root).as_posix(),
        spec_file_sha256=_sha256(spec_path),
        runtime_record_path=record_path.relative_to(root).as_posix(),
        runtime_record_sha256=_sha256(record_path),
    )
    _write_json(destination / "sequence-receipt.json", receipt)
    return destination


def _rebind_campaign_identity(
    release_input: dict[str, Any],
    mutate: Callable[[dict[str, Any]], None],
) -> None:
    """Reseal every mutable Task 3 link after a deliberate identity change."""

    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    root = sequence_path.parent
    identity_path = root / "campaign-identity.json"
    identity = json.loads(identity_path.read_text(encoding="utf-8"))
    mutate(identity["identity"])
    _write_json(identity_path, _resign_campaign_identity(identity))
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    sequence["campaign_identity_sha256"] = identity["campaign_identity_sha256"]
    for receipt in (
        sequence["pilot"], sequence["warmup"], *sequence["accepted_samples"]
    ):
        receipt["campaign_identity_sha256"] = identity["campaign_identity_sha256"]
        spec_path = root / receipt["spec_path"]
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
        spec["campaign_identity_sha256"] = identity["campaign_identity_sha256"]
        _write_json(spec_path, spec)
        receipt["spec_file_sha256"] = _sha256(spec_path)
        receipt["spec_sha256"] = _canonical_sha256(spec)
        record_path = root / receipt["runtime_record_path"]
        record = json.loads(record_path.read_text(encoding="utf-8"))
        record["identity_hashes"]["evidence_sha256"] = identity[
            "campaign_identity_sha256"
        ]
        _write_json(record_path, record)
    summary_path = root / "measurement-summary.json"
    summary = summarize_samples(
        [
            measurement_sample(
                json.loads((root / receipt["runtime_record_path"]).read_text(encoding="utf-8")),
                root / receipt["runtime_record_path"],
            )
            for receipt in sequence["accepted_samples"]
        ]
    )
    summary.update(
        {
            "accepted": True,
            "cleanup_process_count": 0,
            "test_id": step["test_id"],
            "context_tokens": step["context_tokens"],
            "campaign_identity_sha256": identity["campaign_identity_sha256"],
            "runtime_config_sha256": _canonical_sha256(identity["identity"]["config"]),
        }
    )
    _write_json(summary_path, summary)
    sequence["measurement_summary_sha256"] = _sha256(summary_path)
    _write_json(sequence_path, sequence)
    _resign_runtime_step(release_input, step)


def remove_raw_field(release_input: dict[str, Any], role: str, field: str) -> None:
    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    receipt = next(
        receipt
        for receipt in (sequence["pilot"], sequence["warmup"], *sequence["accepted_samples"])
        if receipt["role"] == role
    )
    raw_path = sequence_path.parent / receipt["runtime_record_path"]
    raw = json.loads(raw_path.read_text(encoding="utf-8"))
    raw["identity_hashes"].pop(field)
    _write_json(raw_path, raw)
    _resign_runtime_step(release_input, step)


def resign_sample_with_different_artifact(release_input: dict[str, Any], test_id: str) -> None:
    step = _find_step(release_input, test_id, 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    root = sequence_path.parent
    identity_path = root / "campaign-identity.json"
    identity = json.loads(identity_path.read_text(encoding="utf-8"))
    identity["identity"]["model"]["artifact_manifest_sha256"] = "f" * 64
    _write_json(identity_path, _resign_campaign_identity(identity))
    campaign_identity_sha256 = identity["campaign_identity_sha256"]
    sequence["campaign_identity_sha256"] = campaign_identity_sha256
    for receipt in (sequence["pilot"], sequence["warmup"], *sequence["accepted_samples"]):
        receipt["campaign_identity_sha256"] = campaign_identity_sha256
        spec_path = sequence_path.parent / receipt["spec_path"]
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
        spec["campaign_identity_sha256"] = campaign_identity_sha256
        _write_json(spec_path, spec)
        receipt["spec_file_sha256"] = _sha256(spec_path)
        receipt["spec_sha256"] = _canonical_sha256(spec)
        raw_path = sequence_path.parent / receipt["runtime_record_path"]
        raw = json.loads(raw_path.read_text(encoding="utf-8"))
        raw["identity_hashes"]["artifact_manifest_sha256"] = "f" * 64
        raw["identity_hashes"]["evidence_sha256"] = campaign_identity_sha256
        _write_json(raw_path, raw)
    _write_json(sequence_path, sequence)
    _resign_runtime_step(release_input, step)


def remove_quality_prompt(release_input: dict[str, Any], prompt_id: str) -> None:
    step = _find_step(release_input, "OV-11", 512)
    capture_path = _path(release_input, step["quality_capture"])
    capture = json.loads(capture_path.read_text(encoding="utf-8"))
    capture["prompt_receipts"] = [
        receipt for receipt in capture["prompt_receipts"] if receipt["prompt_id"] != prompt_id
    ]
    _write_json(capture_path, _resign(capture, "capture_summary_sha256"))
    step["quality_capture"] = _reference(capture_path, base=_base(release_input))
    _resign_release_input(release_input)


def _add_synthetic_quality(release_input: dict[str, Any]) -> None:
    """Attach deliberately non-governed quality evidence for negative tests."""

    step = _find_step(release_input, "OV-11", 512)
    quality = _quality_step(
        _base(release_input) / "quality" / "OV-11" / "512",
        test_id="OV-11",
        context=512,
    )
    step["quality_capture"] = quality["quality_capture"]
    state_path = _path(release_input, release_input["campaign_state"])
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state_step = state["steps"]["OV-11:512"]
    state_step["quality_status"] = "passed"
    state_step["quality_result"] = {
        "status": "passed",
        "capture_summary_path": str(_path(release_input, step["quality_capture"])),
    }
    _write_json(state_path, state)
    release_input["campaign_state"] = _reference(state_path, base=_base(release_input))
    _resign_release_input(release_input)


def tamper_raw_attempt_without_updating_index(release_input: dict[str, Any]) -> None:
    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    raw_path = sequence_path.parent / sequence["accepted_samples"][1]["runtime_record_path"]
    raw = json.loads(raw_path.read_text(encoding="utf-8"))
    raw["worker"]["ttft_ms"] = 777.0
    _write_json(raw_path, raw)


def reconcile_one_runtime(release_input: dict[str, Any]):
    return reconcile_comparison_release(release_input).runtime[ComparisonKey("OV-11", 512)]


def median_of_run_means(row: Any) -> float:
    return statistics.median(
        statistics.fmean(sample["cpu_percent"]["values"])
        for sample in row.samples
    )


def test_release_rejects_a_runtime_summary_that_task_three_cannot_recompute(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    corrupt_declared_summary(release_input, field="ttft_ms.mean", value=999999.0)

    with pytest.raises(ValueError, match="Task 3 evidence|measurement summary"):
        reconcile_comparison_release(release_input)


def test_runtime_requires_tokens_activation_cleanup_and_all_hashes(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    remove_raw_field(release_input, "sample-2", "command_sha256")

    with pytest.raises(ValueError, match="Task 3 evidence|command_sha256"):
        reconcile_comparison_release(release_input)


def test_memory_uses_median_worst_case_and_global_ram_minimum(tmp_path: Path) -> None:
    row = reconcile_one_runtime(valid_release_input(tmp_path))

    assert row.memory["peak_private_mib"]["median"] == pytest.approx(2800.0)
    assert row.memory["peak_private_mib"]["worst_max"] == pytest.approx(2900.0)
    assert row.memory["available_ram_mib"]["global_min"] == pytest.approx(2200.0)


def test_utilisation_flattens_all_timestamped_samples(tmp_path: Path) -> None:
    row = reconcile_one_runtime(valid_release_input(tmp_path))

    assert row.utilisation["cpu_percent"]["count"] == 9
    assert row.utilisation["gpu_percent"]["count"] == 9
    assert row.utilisation["cpu_percent"]["mean"] != median_of_run_means(row)


def test_shared_cache_comparison_requires_identical_u8_artifact(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path, ids=("OV-12", "OV-TQ-21", "OV-TQ-22"))
    resign_sample_with_different_artifact(release_input, "OV-TQ-22")

    with pytest.raises(ValueError, match="Task 3 evidence|identical U8 artifact"):
        reconcile_comparison_release(release_input)


def boundary_release_input(tmp_path: Path) -> dict[str, Any]:
    release_input = valid_release_input(tmp_path, contexts=(512, 1024, 2048))
    root = tmp_path / "terminal"
    evidence = _write_json(root / "boundary.json", {"reason": "ram floor"})
    release_input["steps"].append(
        {
            "test_id": "OV-11",
            "context_tokens": 4096,
            "terminal": {
                "stage": "runtime-boundary",
                "principal_reason": "confirmed memory boundary",
                "evidence": _reference(evidence, base=_base(release_input)),
            },
        }
    )
    _resign_release_input(release_input)
    return release_input


def test_boundary_separates_runtime_and_fully_comparable_contexts(tmp_path: Path) -> None:
    from scripts.testing.campaigns.openvino.comparison_reconcile import derive_boundaries

    runtime = {}
    quality = {}
    for context in (512, 1024, 2048):
        key = ComparisonKey("OV-11", context)
        runtime[key] = ComparisonRuntimeOutcome(key, ({}, {}, {}), {}, {}, {}, {}, {}, tmp_path / f"{context}.json", "a" * 64)
        quality[key] = ComparisonQualityOutcome(
            key,
            "quality-complete" if context < 2048 else "quality-blocked",
            {prompt: 1.0 for prompt in ("P1", "P2", "P3", "P4", "P5", "P6")} if context < 2048 else None,
            {"mean": 1.0} if context < 2048 else None,
            tmp_path / f"quality-{context}.json", "b" * 64,
        )
    matrix_input = valid_release_input(tmp_path)
    matrix = load_adaptive_comparison_matrix(_path(matrix_input, matrix_input["matrix"]))
    boundary = derive_boundaries(
        [case for case in matrix if case.test_id == "OV-11"], runtime, quality,
        {ComparisonKey("OV-11", 4096): {"stage": "runtime-boundary", "evidence_sha256": "c" * 64}},
    )["OV-11"]
    assert boundary.highest_runtime_context == 2048
    assert boundary.highest_fully_comparable_context == 1024
    assert boundary.first_confirmed_blocked_context == 4096


def valid_terminal_input(tmp_path: Path) -> dict[str, Any]:
    release_input = valid_release_input(tmp_path)
    matrix = _path(release_input, release_input["matrix"])
    evidence = _write_json(tmp_path / "terminal" / "terminal.json", {"reason": "unavailable"})
    state = _write_controller_state(tmp_path, release_input, steps={})
    state_value = json.loads(state.read_text(encoding="utf-8"))
    receipt_path = tmp_path / "controller-receipts" / "OV-13" / "artifact-preparation-terminal.json"
    receipt = {
        "schema": "official-openvino-adaptive-controller-terminal-receipt/v1",
        "test_id": "OV-13", "context_tokens": 512,
        "status": "artifact-preparation-terminal", "stage": "artifact-preparation",
        "source_receipt_path": str(evidence), "source_receipt_sha256": _sha256(evidence),
        "matrix_sha256": state_value["bindings"]["matrix_sha256"],
        "spec_index_sha256": state_value["bindings"]["spec_index_sha256"],
    }
    _write_json(receipt_path, receipt)
    state_value["steps"] = {
        "OV-13:512": {
            "test_id": "OV-13", "context_tokens": 512,
            "runtime_status": "artifact-preparation-terminal", "quality_status": "not-run",
            "attempt_count": 0, "failure_fingerprint": None,
            "evidence_path": str(evidence), "evidence_sha256": _sha256(evidence), "attempts": [],
            "terminal_receipt_path": receipt_path.relative_to(tmp_path).as_posix(),
            "terminal_receipt_sha256": _sha256(receipt_path),
        }
    }
    _write_json(state, state_value)
    from scripts.testing.campaigns.openvino.comparison_reconcile import build_comparison_release_input
    output = tmp_path / "terminal-release-input.json"
    result = build_comparison_release_input(matrix, state, output)
    result["__base_path"] = str(tmp_path)
    return result


def test_terminal_record_contains_no_fabricated_metrics(tmp_path: Path) -> None:
    terminal = reconcile_terminal(valid_terminal_input(tmp_path))

    assert set(terminal) == {"test_id", "context_tokens", "stage", "principal_reason", "evidence_path", "evidence_sha256"}


def test_hash_mismatch_refuses_reconciliation(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    tamper_raw_attempt_without_updating_index(release_input)

    with pytest.raises(ValueError, match="hash"):
        reconcile_comparison_release(release_input)


def test_release_evidence_cli_writes_hash_bound_input_and_capture_index(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    runtime = _find_step(release_input, "OV-11", 512)["runtime"]
    matrix = _path(release_input, release_input["matrix"])
    state = _path(release_input, release_input["campaign_state"])
    output = tmp_path / "comparison-release-input.json"
    capture_index = tmp_path / "quality-capture-index.json"
    completed = subprocess.run(
        [
            sys.executable,
            "scripts/testing/build_official_openvino_comparison_release_evidence.py",
            "--matrix",
            str(matrix),
            "--campaign-state",
            str(state),
            "--output",
            str(output),
            "--quality-capture-index",
            str(capture_index),
        ],
        cwd=Path(__file__).resolve().parents[3],
        text=True,
        capture_output=True,
        check=False,
    )

    assert completed.returncode == 0, completed.stderr
    index = json.loads(output.read_text(encoding="utf-8"))
    assert index["schema"] == "official-openvino-comparison-release-input-v1"
    assert not Path(index["matrix"]["path"]).is_absolute()
    captures = json.loads(capture_index.read_text(encoding="utf-8"))
    assert captures["captures"] == []


def test_closed_campaign_accepts_a_complete_contiguous_8192_ladder(tmp_path: Path) -> None:
    runtime = {}
    quality = {}
    for test_id in CANDIDATE_ORDER:
        for context in CONTEXTS:
            key = ComparisonKey(test_id, context)
            runtime[key] = ComparisonRuntimeOutcome(
                key=key,
                samples=({}, {}, {}),
                timing={},
                memory={},
                utilisation={},
                activation={},
                identity_hashes={},
                evidence_path=tmp_path / f"{test_id}-{context}.json",
                evidence_sha256="a" * 64,
            )
            quality[key] = ComparisonQualityOutcome(
                key=key,
                status="quality-complete",
                prompt_scores={prompt_id: 1.0 for prompt_id in ("P1", "P2", "P3", "P4", "P5", "P6")},
                aggregates={"mean": 1.0, "median": 1.0, "minimum": 1.0, "maximum": 1.0},
                evidence_path=tmp_path / f"{test_id}-{context}-quality.json",
                evidence_sha256="b" * 64,
            )
    release = ComparisonRelease(
        runtime=runtime,
        quality=quality,
        terminals={},
        shared_cache_contexts=CONTEXTS,
        shared_standard_contexts=CONTEXTS,
        boundaries={},
    )

    validate_closed_campaign(release)


def _write_controller_state(
    tmp_path: Path,
    release_input: dict[str, Any],
    *,
    steps: dict[str, Any] | None = None,
) -> Path:
    matrix = _path(release_input, release_input["matrix"])
    state_steps = steps if steps is not None else {}
    if not state_steps:
        for step in release_input["steps"]:
            if "runtime" not in step:
                continue
            test_id = step["test_id"]
            context = step["context_tokens"]
            runtime = _path(release_input, step["runtime"])
            receipt_path = (
                tmp_path / "controller-receipts" / test_id / str(context) / "attempt-001.json"
            )
            receipt = {
                "schema": "official-openvino-adaptive-controller-receipt/v1",
                "test_id": test_id,
                "context_tokens": context,
                "attempt_number": 1,
                "status": "passed",
                "evidence_path": str(runtime),
                "evidence_sha256": _sha256(runtime),
                "failure_fingerprint": None,
                "residual_owned_process_count": 0,
                "launch_reserve_mib": 4096,
                "emergency_floor_mib": 2048,
            }
            _write_json(receipt_path, receipt)
            state_steps[f"{step['test_id']}:{step['context_tokens']}"] = {
                "test_id": test_id,
                "context_tokens": context,
                "runtime_status": "passed",
                "quality_status": "not-run",
                "attempt_count": 1,
                "failure_fingerprint": None,
                "evidence_path": str(runtime),
                "evidence_sha256": _sha256(runtime),
                "attempts": [{
                    "attempt_number": 1,
                    "status": "passed",
                    "receipt_path": receipt_path.relative_to(tmp_path).as_posix(),
                    "receipt_sha256": _sha256(receipt_path),
                    "evidence_path": str(runtime),
                    "evidence_sha256": _sha256(runtime),
                    "failure_fingerprint": None,
                    "residual_owned_process_count": 0,
                }],
            }
            if "quality_capture" in step and "quality_adjudication" in step:
                state_steps[f"{test_id}:{context}"]["quality_status"] = "passed"
                state_steps[f"{test_id}:{context}"]["quality_result"] = {
                    "status": "passed",
                    "capture_summary_path": str(_path(release_input, step["quality_capture"])),
                    "quality_adjudication_path": str(_path(release_input, step["quality_adjudication"])),
                }
    import scripts.testing.campaigns.openvino.adaptive_campaign as controller

    bindings_root = tmp_path / "bindings"
    build_root = bindings_root / "build"
    spec_index = bindings_root / "specs" / "spec-index.json"
    inventory = matrix.parent / "artifact-inventory.json"
    provenance = bindings_root / "build-provenance.json"
    sampler = bindings_root / "sampler.ps1"
    prompt_set = (
        Path(__file__).resolve().parents[3]
        / "experiments"
        / "granite_turboquant_intel"
        / "prompts"
        / "fixed-feasibility-prompt-set-v1.json"
    )
    rubric = (
        Path(__file__).resolve().parents[3]
        / "experiments"
        / "granite_turboquant_intel"
        / "rubrics"
        / "quality-rubric-v1.json"
    )
    bindings = {
        "matrix_path": str(matrix), "matrix_sha256": _sha256(matrix),
        "spec_index_path": str(spec_index), "spec_index_sha256": _sha256(spec_index),
        "artifact_inventory_path": str(inventory), "artifact_inventory_sha256": _sha256(inventory),
        "build_root": str(build_root), "build_root_sha256": controller._directory_sha256(build_root),
        "build_provenance_path": str(provenance), "build_provenance_sha256": _sha256(provenance),
        "sampler_script_path": str(sampler), "sampler_script_sha256": _sha256(sampler),
        "quality_prompt_set_path": str(prompt_set), "quality_prompt_set_sha256": _sha256(prompt_set),
        "quality_rubric_path": str(rubric), "quality_rubric_sha256": _sha256(rubric),
        "quality_output_root": str(tmp_path / "quality"), "quality_timeout_seconds": 1800.0,
        "reference_boundary_index_path": None, "reference_boundary_index_sha256": None,
    }
    for state_step in state_steps.values():
        if state_step.get("runtime_status") != "passed":
            continue
        sequence_path = Path(str(state_step["evidence_path"]))
        sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
        evidence_sha = _sha256(sequence_path)
        state_step["evidence_sha256"] = evidence_sha
        controller_receipt_row = state_step["attempts"][-1]
        receipt_path = tmp_path / controller_receipt_row["receipt_path"]
        controller_receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        controller_receipt["evidence_sha256"] = evidence_sha
        _write_json(receipt_path, controller_receipt)
        controller_receipt_row["evidence_sha256"] = evidence_sha
        controller_receipt_row["receipt_sha256"] = _sha256(receipt_path)
        pilot_spec = sequence_path.parent / sequence["pilot"]["spec_path"]
        adaptive_spec = (
            bindings_root / "specs" / state_step["test_id"]
            / str(state_step["context_tokens"]) / "runtime-spec.json"
        )
        runtime_summary = sequence_path.parent / "measurement-summary.json"
        adaptive = json.loads(adaptive_spec.read_text(encoding="utf-8"))
        recovery_unsigned = {
            "schema": "official-openvino-adaptive-quality-recovery/v1",
            "test_id": state_step["test_id"], "context_tokens": state_step["context_tokens"],
            "runtime_evidence_path": str(sequence_path), "runtime_evidence_sha256": _sha256(sequence_path),
            "runtime_summary": str(runtime_summary), "runtime_summary_sha256": _sha256(runtime_summary),
            "adaptive_runtime_spec_path": str(adaptive_spec), "adaptive_runtime_spec_sha256": _sha256(adaptive_spec),
            "pilot_spec_path": str(pilot_spec), "pilot_spec_sha256": _sha256(pilot_spec),
            "spec_index_path": bindings["spec_index_path"], "spec_index_sha256": bindings["spec_index_sha256"],
            "artifact_inventory_path": bindings["artifact_inventory_path"], "artifact_inventory_sha256": bindings["artifact_inventory_sha256"],
            "matrix": bindings["matrix_path"], "matrix_sha256": bindings["matrix_sha256"],
            "artifact_manifest_path": adaptive["artifact_manifest_path"], "artifact_manifest_sha256": adaptive["artifact_manifest_sha256"],
            "prompt_set": bindings["quality_prompt_set_path"], "prompt_set_sha256": bindings["quality_prompt_set_sha256"],
            "rubric": bindings["quality_rubric_path"], "rubric_sha256": bindings["quality_rubric_sha256"],
            "model_path": adaptive["model_path"], "build_root": bindings["build_root"],
            "build_provenance_path": bindings["build_provenance_path"], "build_provenance_sha256": bindings["build_provenance_sha256"],
            "python_executable": str(Path(sys.executable)), "python_site_packages": str(bindings_root / "site-packages"),
            "openvino_libraries": str(bindings_root / "openvino-libraries"), "sampler_script": bindings["sampler_script_path"],
            "sampler_script_sha256": bindings["sampler_script_sha256"],
            "output_root": str(tmp_path / "quality" / state_step["test_id"] / str(state_step["context_tokens"])),
            "timeout_seconds": 1800.0,
        }
        state_step["quality_recovery"] = {
            **recovery_unsigned,
            "quality_recovery_sha256": _canonical_sha256(recovery_unsigned),
        }
    return _write_json(
        tmp_path / "authoritative-state.json",
        {
            "schema": "official-openvino-adaptive-campaign-state/v1",
            "policy": {
                "contexts": list(CONTEXTS), "candidate_order": list(CANDIDATE_ORDER),
                "start_reserve_mib": 4096, "runtime_floor_mib": 2048, "max_guarded_attempts": 2,
            },
            "bindings": bindings,
            "steps": state_steps,
            "boundaries": {}, "campaign_halt": None, "safety_probes": [],
        },
    )


def _with_state(tmp_path: Path, release_input: dict[str, Any]) -> dict[str, Any]:
    state = _write_controller_state(tmp_path, release_input)
    release_input["campaign_state"] = _reference(state, base=_base(release_input))
    _resign_release_input(release_input)
    return release_input


def _real_task5_recovery_history(
    release_input: dict[str, Any],
    monkeypatch: pytest.MonkeyPatch,
) -> Path:
    """Create original, recovery-001, and recovery-002 Task 5 evidence."""

    state_path = _path(release_input, release_input["campaign_state"])
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state_step = state["steps"]["OV-11:512"]
    recovery = state_step["quality_recovery"]
    monkeypatch.setattr(
        adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB
    )
    capture_isolated_quality_campaign(
        recovery,
        resume=False,
        run_command=PassingGuardRunner(failed_prompt="P2"),
    )
    capture_isolated_quality_campaign(
        recovery,
        resume=True,
        run_command=PassingGuardRunner(failed_prompt="P3"),
    )
    result = capture_isolated_quality_campaign(
        recovery,
        resume=True,
        run_command=PassingGuardRunner(),
    )
    capture = Path(result["capture_summary_path"]).resolve()
    assert capture.name == "capture-summary-recovery-002.json"
    assert (capture.parent / "capture-summary.json").is_file()
    assert (capture.parent / "capture-summary-recovery-001.json").is_file()
    state_step["quality_status"] = "passed"
    state_step["quality_result"] = {
        "status": "passed",
        "capture_summary_path": str(capture),
    }
    _write_json(state_path, state)
    release_input["campaign_state"] = _reference(state_path, base=_base(release_input))
    _resign_release_input(release_input)
    return capture


def _real_task5_capture_blocked_at_p4(
    release_input: dict[str, Any],
    monkeypatch: pytest.MonkeyPatch,
) -> Path:
    """Persist a genuine Task 5 partial capture whose P4 worker fails."""

    state_path = _path(release_input, release_input["campaign_state"])
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state_step = state["steps"]["OV-11:512"]
    recovery = state_step["quality_recovery"]
    monkeypatch.setattr(
        adaptive_quality, "available_ram_bytes", lambda: 4096 * MIB
    )
    result = capture_isolated_quality_campaign(
        recovery,
        resume=False,
        run_command=PassingGuardRunner(failed_prompt="P4"),
    )
    capture = Path(result["capture_summary_path"]).resolve()
    assert capture.name == "capture-summary.json"
    assert result["status"] == "quality-blocked"
    state_step["quality_status"] = "quality-blocked"
    state_step["quality_result"] = result
    _write_json(state_path, state)
    release_input["campaign_state"] = _reference(state_path, base=_base(release_input))
    _resign_release_input(release_input)
    return capture


def _project_authoritative_release(release_input: dict[str, Any], output: Path) -> Path:
    """Use the committed state-to-release projection, not a synthetic step."""

    build_comparison_release_input(
        _path(release_input, release_input["matrix"]),
        _path(release_input, release_input["campaign_state"]),
        output,
    )
    return output


def _attach_governed_quality_terminal(
    release_input: dict[str, Any],
    *,
    tmp_path: Path,
    stage: str,
    principal_reason: str,
) -> Path:
    """Attach an exact controller-receipted terminal to one passed runtime."""

    state_path = _path(release_input, release_input["campaign_state"])
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state_step = state["steps"]["OV-11:512"]
    evidence = _write_json(
        tmp_path / f"{stage}-terminal.json",
        {
            "schema": "official-openvino-adaptive-quality-terminal/v1",
            "stage": stage,
            "principal_reason": principal_reason,
        },
    )
    receipt = _write_json(
        tmp_path / "quality-terminal-receipts" / stage / "quality-terminal.json",
        {
            "schema": "official-openvino-adaptive-quality-terminal-receipt/v1",
            "test_id": "OV-11",
            "context_tokens": 512,
            "stage": stage,
            "principal_reason": principal_reason,
            "evidence_path": str(evidence),
            "evidence_sha256": _sha256(evidence),
            "runtime_evidence_path": state_step["evidence_path"],
            "runtime_evidence_sha256": state_step["evidence_sha256"],
        },
    )
    state_step["quality_terminal"] = {
        "stage": stage,
        "principal_reason": principal_reason,
        "evidence_path": str(evidence),
        "evidence_sha256": _sha256(evidence),
        "controller_receipt_path": receipt.relative_to(tmp_path).as_posix(),
        "controller_receipt_sha256": _sha256(receipt),
    }
    _write_json(state_path, state)
    release_input["campaign_state"] = _reference(state_path, base=_base(release_input))
    _resign_release_input(release_input)
    return evidence


def _real_task6_bundle(
    release_input: dict[str, Any],
    capture: Path,
) -> None:
    """Persist all six Task 6 artifacts generated from the real Task 5 capture."""

    state_path = _path(release_input, release_input["campaign_state"])
    state = json.loads(state_path.read_text(encoding="utf-8"))
    recovery = state["steps"]["OV-11:512"]["quality_recovery"]
    artifact_root = capture.parent / "task6"
    scoring, private = build_adaptive_blind_bundle(
        [capture],
        prompt_set_path=Path(recovery["prompt_set"]),
        rubric_path=Path(recovery["rubric"]),
    )
    score_a = _score_sheet(scoring, "blind-reviewer-a", 8.0)
    score_b = _score_sheet(scoring, "blind-reviewer-b", 8.0)
    pairwise = _empty_pairwise(scoring)
    manual = _empty_manual(scoring)
    result = adjudicate_adaptive_quality(
        scoring_input=scoring,
        judge_score_sheets=[score_a, score_b],
        pairwise_reviews=pairwise,
        manual_adjudications=manual,
        blind_map_reader=lambda: private,
        prompt_set_path=Path(recovery["prompt_set"]),
        rubric_path=Path(recovery["rubric"]),
    )
    paths = {
        "result": _write_json(artifact_root / "adjudication.json", result),
        "scoring_input": _write_json(artifact_root / "blind-scoring.json", scoring),
        "private_map": _write_json(artifact_root / "blind-map-private.json", private),
        "pairwise_reviews": _write_json(artifact_root / "pairwise-reviews.json", pairwise),
        "manual_adjudications": _write_json(artifact_root / "manual-adjudications.json", manual),
    }
    score_paths = [
        _write_json(artifact_root / "judge-a.json", score_a),
        _write_json(artifact_root / "judge-b.json", score_b),
    ]
    state_step = state["steps"]["OV-11:512"]
    state_step["quality_result"] = {
        "status": "passed",
        "capture_summary_path": str(capture),
        "quality_adjudication_bundle": {
            **{name: _reference(path) for name, path in paths.items()},
            "judge_score_sheets": [_reference(path) for path in score_paths],
        },
    }
    _write_json(state_path, state)
    release_input["campaign_state"] = _reference(state_path, base=_base(release_input))
    _resign_release_input(release_input)


def test_quality_accepts_later_real_task_five_recovery_with_validated_history(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    release_input = valid_release_input(tmp_path)
    _real_task5_recovery_history(release_input, monkeypatch)
    release = _project_authoritative_release(
        release_input, tmp_path / "release-input.json"
    )

    reconciled = reconcile_comparison_release(release)

    assert reconciled.quality[ComparisonKey("OV-11", 512)].status == (
        "capture-complete-awaiting-adjudication"
    )


def test_quality_blocks_a_real_task_five_capture_that_stops_at_p4(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    release_input = valid_release_input(tmp_path)
    capture = _real_task5_capture_blocked_at_p4(release_input, monkeypatch)
    release = _project_authoritative_release(
        release_input, tmp_path / "release-input.json"
    )

    reconciled = reconcile_comparison_release(release)

    quality = reconciled.quality[ComparisonKey("OV-11", 512)]
    assert quality.status == "quality-blocked"
    assert quality.aggregates is None
    assert quality.evidence_path == capture


def test_quality_ignores_an_unreferenced_later_task_five_recovery(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    release_input = valid_release_input(tmp_path)
    capture = _real_task5_capture_blocked_at_p4(release_input, monkeypatch)
    state = json.loads(
        _path(release_input, release_input["campaign_state"]).read_text(
            encoding="utf-8"
        )
    )
    later = capture_isolated_quality_campaign(
        state["steps"]["OV-11:512"]["quality_recovery"],
        resume=True,
        run_command=PassingGuardRunner(failed_prompt="P5"),
    )
    assert Path(later["capture_summary_path"]).name == (
        "capture-summary-recovery-001.json"
    )
    release = _project_authoritative_release(
        release_input, tmp_path / "release-input.json"
    )

    reconciled = reconcile_comparison_release(release)

    quality = reconciled.quality[ComparisonKey("OV-11", 512)]
    assert quality.status == "quality-blocked"
    assert quality.evidence_path == capture


def test_quality_recomputes_full_real_task_six_bundle_after_task_five_recovery(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    release_input = valid_release_input(tmp_path)
    capture = _real_task5_recovery_history(release_input, monkeypatch)
    _real_task6_bundle(release_input, capture)
    release = _project_authoritative_release(
        release_input, tmp_path / "release-input.json"
    )

    reconciled = reconcile_comparison_release(release)

    quality = reconciled.quality[ComparisonKey("OV-11", 512)]
    assert quality.status == "quality-complete"
    assert quality.prompt_scores == {
        prompt_id: 8.0 for prompt_id in ("P1", "P2", "P3", "P4", "P5", "P6")
    }


@pytest.mark.parametrize(
    ("capture_mode", "stage", "principal_reason"),
    (
        ("none", "quality-worker", "guard admission failed"),
        ("blocked", "quality-capture", "P4 capture stopped"),
        ("complete", "quality-adjudication", "no governed adjudicator available"),
    ),
)
def test_quality_terminal_preserves_its_validated_stage_and_principal_reason(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
    capture_mode: str,
    stage: str,
    principal_reason: str,
) -> None:
    release_input = valid_release_input(tmp_path)
    if capture_mode == "blocked":
        _real_task5_capture_blocked_at_p4(release_input, monkeypatch)
    elif capture_mode == "complete":
        _real_task5_recovery_history(release_input, monkeypatch)
    _attach_governed_quality_terminal(
        release_input,
        tmp_path=tmp_path,
        stage=stage,
        principal_reason=principal_reason,
    )
    release = _project_authoritative_release(
        release_input, tmp_path / f"{capture_mode}-terminal-release.json"
    )

    outcome = reconcile_comparison_release(release).quality[
        ComparisonKey("OV-11", 512)
    ]

    assert outcome.status == "quality-terminal"
    assert outcome.terminal_stage == stage
    assert outcome.principal_reason == principal_reason


def test_nonterminal_quality_outcomes_have_no_terminal_metadata(
    tmp_path: Path,
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    blocked_root = tmp_path / "blocked"
    awaiting_root = tmp_path / "awaiting"
    blocked_root.mkdir()
    awaiting_root.mkdir()
    blocked = reconcile_comparison_release(valid_release_input(blocked_root))
    awaiting_input = valid_release_input(awaiting_root)
    _real_task5_recovery_history(awaiting_input, monkeypatch)
    awaiting_release = _project_authoritative_release(
        awaiting_input, tmp_path / "awaiting-release.json"
    )
    awaiting = reconcile_comparison_release(awaiting_release)

    for outcome in (
        blocked.quality[ComparisonKey("OV-11", 512)],
        awaiting.quality[ComparisonKey("OV-11", 512)],
    ):
        assert outcome.status != "quality-terminal"
        assert outcome.terminal_stage is None
        assert outcome.principal_reason is None


def test_release_rejects_missing_authoritative_campaign_state(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    release_input.pop("campaign_state")
    _resign_release_input(release_input)
    with pytest.raises(ValueError, match="campaign state"):
        reconcile_comparison_release(release_input)


def test_release_rejects_steps_that_do_not_exactly_match_campaign_state(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    release_input["steps"].append(dict(release_input["steps"][0]))
    _resign_release_input(release_input)

    with pytest.raises(ValueError, match="campaign state"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_tampered_persisted_sequence_receipt(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    receipt_path = (
        sequence_path.parent
        / sequence["accepted_samples"][0]["runtime_record_path"]
    ).parent.parent / "sequence-receipt.json"
    receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
    receipt["accepted"] = False
    _write_json(receipt_path, receipt)

    with pytest.raises(ValueError, match="Task 3 evidence|sequence receipt"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_cross_sequence_receipt_substitution(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    sample_receipt = sequence["accepted_samples"][0]
    target = (
        sequence_path.parent
        / sample_receipt["runtime_record_path"]
    ).parent.parent / "sequence-receipt.json"
    foreign = deepcopy(sequence["warmup"])
    foreign["role"] = "sample-1"
    _write_json(target, foreign)

    with pytest.raises(ValueError, match="Task 3 evidence|sequence receipt"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_wrong_attempt_number_in_rehashed_sequence(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    sequence["accepted_samples"][0]["attempt_number"] = 2
    _write_json(sequence_path, sequence)
    step["runtime"] = _reference(sequence_path, base=_base(release_input))
    _refresh_state_runtime_binding(release_input, step)
    _resign_release_input(release_input)

    with pytest.raises(ValueError, match="Task 3 evidence|attempt_number"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_missing_or_mismatched_campaign_identity(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    step = _find_step(release_input, "OV-11", 512)
    root = _path(release_input, step["runtime"]).parent
    _write_json(root / "campaign-identity.json", {"schema": "wrong", "identity": {}})

    with pytest.raises(ValueError, match="Task 3 evidence|campaign identity"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_fabricated_identity_hash_mapping(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    for receipt in sequence["accepted_samples"]:
        raw_path = sequence_path.parent / receipt["runtime_record_path"]
        raw = json.loads(raw_path.read_text(encoding="utf-8"))
        raw["identity_hashes"] = {field: "f" * 64 for field in IDENTITY_HASH_FIELDS}
        raw["identity_hashes"]["command_sha256"] = _canonical_sha256(raw["command"])
        _write_json(raw_path, raw)
    _resign_runtime_step(release_input, step)

    with pytest.raises(ValueError, match="Task 3 evidence|identity"):
        reconcile_comparison_release(release_input)


def test_quality_rejects_rehashed_synthetic_worker_stub(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    _add_synthetic_quality(release_input)
    with pytest.raises(ValueError, match="quality capture"):
        reconcile_comparison_release(release_input)


def test_quality_rejects_capture_runtime_identity_substitution(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    _add_synthetic_quality(release_input)
    step = _find_step(release_input, "OV-11", 512)
    capture_path = _path(release_input, step["quality_capture"])
    capture = json.loads(capture_path.read_text(encoding="utf-8"))
    capture["runtime_summary_sha256"] = "f" * 64
    _write_json(capture_path, _resign(capture, "capture_summary_sha256"))
    step["quality_capture"] = _reference(capture_path, base=_base(release_input))
    _resign_release_input(release_input)

    with pytest.raises(ValueError, match="quality capture"):
        reconcile_comparison_release(release_input)


def test_quality_rejects_rehashed_blind_adjudication_substitution(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    _add_synthetic_quality(release_input)
    step = _find_step(release_input, "OV-11", 512)
    capture_path = _path(release_input, step["quality_capture"])
    capture = json.loads(capture_path.read_text(encoding="utf-8"))
    capture["scoring_input_sha256"] = "f" * 64
    _write_json(capture_path, _resign(capture, "capture_summary_sha256"))
    step["quality_capture"] = _reference(capture_path, base=_base(release_input))
    _resign_release_input(release_input)

    with pytest.raises(ValueError, match="quality capture|adjudication"):
        reconcile_comparison_release(release_input)


def test_state_builder_rejects_terminal_without_controller_receipt(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    evidence = _write_json(tmp_path / "terminal-only.json", {"status": "terminal"})
    state = _write_controller_state(
        tmp_path,
        release_input,
        steps={
            "OV-13:512": {
                "test_id": "OV-13",
                "context_tokens": 512,
                "runtime_status": "artifact-preparation-terminal",
                "quality_status": "not-run",
                "attempt_count": 0,
                "failure_fingerprint": None,
                "evidence_path": str(evidence),
                "evidence_sha256": _sha256(evidence),
                "attempts": [],
                "terminal_receipt_path": "controller-receipts/OV-13/artifact-preparation-terminal.json",
                "terminal_receipt_sha256": "a" * 64,
            }
        },
    )
    from scripts.testing.campaigns.openvino.comparison_reconcile import (
        build_comparison_release_input,
    )

    with pytest.raises(ValueError, match="terminal"):
        build_comparison_release_input(
            _path(release_input, release_input["matrix"]), state, tmp_path / "built.json"
        )


def test_reconciler_rejects_an_unapproved_terminal_stage(tmp_path: Path) -> None:
    release_input = valid_terminal_input(tmp_path)
    release_input["steps"][0]["terminal"]["stage"] = "made-up-terminal"
    _resign_release_input(release_input)

    with pytest.raises(ValueError, match="terminal stage"):
        reconcile_terminal(release_input)


def test_state_projects_a_governed_quality_terminal_without_scores(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    evidence = _write_json(
        tmp_path / "quality-terminal.json",
        {
            "schema": "official-openvino-adaptive-quality-terminal/v1",
            "stage": "quality-worker",
            "principal_reason": "guard admission failed",
        },
    )
    state = _write_controller_state(tmp_path, release_input)
    state_value = json.loads(state.read_text(encoding="utf-8"))
    runtime_state = state_value["steps"]["OV-11:512"]
    receipt = _write_json(
        tmp_path / "quality-terminal-receipts" / "OV-11" / "512" / "quality-terminal.json",
        {
            "schema": "official-openvino-adaptive-quality-terminal-receipt/v1",
            "test_id": "OV-11",
            "context_tokens": 512,
            "stage": "quality-worker",
            "principal_reason": "guard admission failed",
            "evidence_path": str(evidence),
            "evidence_sha256": _sha256(evidence),
            "runtime_evidence_path": runtime_state["evidence_path"],
            "runtime_evidence_sha256": runtime_state["evidence_sha256"],
        },
    )
    state_value["steps"]["OV-11:512"]["quality_terminal"] = {
        "stage": "quality-worker",
        "principal_reason": "guard admission failed",
        "evidence_path": str(evidence),
        "evidence_sha256": _sha256(evidence),
        "controller_receipt_path": receipt.relative_to(tmp_path).as_posix(),
        "controller_receipt_sha256": _sha256(receipt),
    }
    _write_json(state, state_value)
    from scripts.testing.campaigns.openvino.comparison_reconcile import (
        build_comparison_release_input,
    )

    built = build_comparison_release_input(
        _path(release_input, release_input["matrix"]), state, tmp_path / "quality-terminal-input.json"
    )

    assert built["steps"][0]["quality_terminal"] == {
        "stage": "quality-worker",
        "principal_reason": "guard admission failed",
        "evidence": _reference(evidence, base=tmp_path),
    }


def test_builder_preserves_explicit_external_boundary_authority(tmp_path: Path) -> None:
    campaign = tmp_path / "campaign"
    campaign.mkdir()
    release_input = valid_release_input(campaign)
    boundary = _write_json(
        tmp_path / "reference-boundaries" / "boundary-index.json",
        {"schema": "official-openvino-adaptive-boundary-index/v1", "boundaries": []},
    )
    state = _write_controller_state(
        campaign,
        release_input,
        steps={
            "OV-11:512": {
                "test_id": "OV-11",
                "context_tokens": 512,
                "runtime_status": "boundary-confirmed",
                "quality_status": "not-run",
                "attempt_count": 0,
                "failure_fingerprint": None,
                "evidence_path": str(boundary),
                "evidence_sha256": _sha256(boundary),
                "boundary_source": "explicit-reference-index",
                "attempts": [],
            }
        },
    )
    state_value = json.loads(state.read_text(encoding="utf-8"))
    state_value["bindings"]["reference_boundary_index_path"] = str(boundary)
    state_value["bindings"]["reference_boundary_index_sha256"] = _sha256(boundary)
    _write_json(state, state_value)
    from scripts.testing.campaigns.openvino.comparison_reconcile import (
        build_comparison_release_input,
    )

    built = build_comparison_release_input(
        _path(release_input, release_input["matrix"]), state, campaign / "release-input.json"
    )

    reference = built["steps"][0]["terminal"]["evidence"]
    assert not Path(reference["path"]).is_absolute()
    assert reference["sha256"] == _sha256(boundary)
    assert reference["authority"] == {
        "kind": "state-reference-boundary-root",
        "root": str(boundary.parent.resolve()),
    }
    assert ".." not in reference["path"]


def test_closed_campaign_rejects_inconclusive_terminal_stage(tmp_path: Path) -> None:
    runtime = {}
    quality = {}
    for test_id in CANDIDATE_ORDER:
        if test_id == "OV-11":
            continue
        for context in CONTEXTS:
            key = ComparisonKey(test_id, context)
            runtime[key] = ComparisonRuntimeOutcome(key, ({}, {}, {}), {}, {}, {}, {}, {}, tmp_path / "runtime.json", "a" * 64)
            quality[key] = ComparisonQualityOutcome(key, "quality-complete", {prompt: 1.0 for prompt in ("P1", "P2", "P3", "P4", "P5", "P6")}, {"mean": 1.0}, tmp_path / "quality.json", "b" * 64)
    release = ComparisonRelease(
        runtime=runtime,
        quality=quality,
        terminals={
            ComparisonKey("OV-11", 512): {
                "test_id": "OV-11",
                "context_tokens": 512,
                "stage": "inconclusive-safety-boundary",
                "principal_reason": "not confirmed",
                "evidence_path": tmp_path / "terminal.json",
                "evidence_sha256": "c" * 64,
            }
        },
        shared_cache_contexts=(),
        shared_standard_contexts=(),
        boundaries={},
    )

    with pytest.raises(ValueError, match="confirmed"):
        validate_closed_campaign(release)


def test_complete_release_requires_exactly_six_numeric_prompt_scores(tmp_path: Path) -> None:
    runtime = {}
    quality = {}
    for test_id in CANDIDATE_ORDER:
        for context in CONTEXTS:
            key = ComparisonKey(test_id, context)
            runtime[key] = ComparisonRuntimeOutcome(key, ({}, {}, {}), {}, {}, {}, {}, {}, tmp_path / "runtime.json", "a" * 64)
            scores: dict[str, Any] = {prompt: 1.0 for prompt in ("P1", "P2", "P3", "P4", "P5", "P6")}
            if key == ComparisonKey("OV-11", 512):
                scores.pop("P6")
            quality[key] = ComparisonQualityOutcome(key, "quality-complete", scores, {"mean": 1.0}, tmp_path / "quality.json", "b" * 64)
    release = ComparisonRelease(runtime, quality, {}, CONTEXTS, CONTEXTS, {})

    with pytest.raises(ValueError, match="six numeric"):
        validate_complete_release(release)


def test_complete_release_allows_governed_quality_terminal_but_not_awaiting_capture(
    tmp_path: Path,
) -> None:
    runtime = {}
    quality = {}
    terminal_key = ComparisonKey("OV-11", 512)
    for test_id in CANDIDATE_ORDER:
        for context in CONTEXTS:
            key = ComparisonKey(test_id, context)
            runtime[key] = ComparisonRuntimeOutcome(
                key, ({}, {}, {}), {}, {}, {}, {}, {},
                tmp_path / "runtime.json", "a" * 64,
            )
            quality[key] = ComparisonQualityOutcome(
                key,
                "quality-terminal" if key == terminal_key else "quality-complete",
                None if key == terminal_key else {
                    prompt: 1.0
                    for prompt in ("P1", "P2", "P3", "P4", "P5", "P6")
                },
                None if key == terminal_key else {"mean": 1.0},
                tmp_path / "governed-quality-terminal.json",
                "b" * 64,
            )
    release = ComparisonRelease(runtime, quality, {}, CONTEXTS, CONTEXTS, {})

    validate_closed_campaign(release)
    validate_complete_release(release)

    quality[terminal_key] = ComparisonQualityOutcome(
        terminal_key,
        "capture-complete-awaiting-adjudication",
        None,
        None,
        tmp_path / "complete-capture.json",
        "c" * 64,
    )
    with pytest.raises(ValueError, match="numeric adjudication"):
        validate_complete_release(release)


@pytest.mark.parametrize("reference_field", ("matrix", "runtime", "quality_capture"))
def test_release_rejects_absolute_references(tmp_path: Path, reference_field: str) -> None:
    release_input = valid_release_input(tmp_path)
    if reference_field in {"quality_capture", "quality_adjudication"}:
        _add_synthetic_quality(release_input)
    if reference_field == "matrix":
        release_input["matrix"]["path"] = str(_path(release_input, release_input["matrix"]))
    else:
        step = _find_step(release_input, "OV-11", 512)
        step[reference_field]["path"] = str(_path(release_input, step[reference_field]))
    _resign_release_input(release_input)

    with pytest.raises(ValueError, match="relative"):
        reconcile_comparison_release(release_input)


def test_loader_requires_the_brief_release_schema_string(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    release_input["schema"] = "official-openvino-comparison-release-input-v1"
    _resign_release_input(release_input)

    reconcile_comparison_release(release_input)


def test_cli_required_mode_does_not_publish_partial_outputs(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    state = _write_controller_state(tmp_path, release_input)
    output = tmp_path / "must-not-exist.json"
    capture_index = tmp_path / "must-not-exist-captures.json"
    completed = subprocess.run(
        [
            sys.executable,
            "scripts/testing/build_official_openvino_comparison_release_evidence.py",
            "--matrix",
            release_input["matrix"]["path"],
            "--campaign-state",
            str(state),
            "--output",
            str(output),
            "--quality-capture-index",
            str(capture_index),
            "--require-complete",
        ],
        cwd=Path(__file__).resolve().parents[3],
        text=True,
        capture_output=True,
        check=False,
    )

    assert completed.returncode != 0
    assert not output.exists()
    assert not capture_index.exists()


# Re-review correction probes.  Each is intentionally behavioral: the test
# reseals any mutable fixture layer it touches and asserts that a missing
# authority binding cannot be made valid merely by recomputing hashes.


def test_runtime_accepts_the_explicitly_receipted_attempt_two(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path, attempt_number=2)
    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))

    for receipt in (
        sequence["pilot"],
        sequence["warmup"],
        *sequence["accepted_samples"],
    ):
        assert receipt["attempt_number"] == 2
        assert "/attempt-002/" in receipt["spec_path"]
        assert (sequence_path.parent / receipt["spec_path"]).is_file()

    reconcile_comparison_release(release_input)


def test_runtime_ignores_a_valid_unreferenced_accepted_attempt_directory(
    tmp_path: Path,
) -> None:
    release_input = valid_release_input(tmp_path)
    unreferenced = _add_unreferenced_accepted_attempt(release_input)

    reconciled = reconcile_comparison_release(release_input)

    assert unreferenced.is_dir()
    assert ComparisonKey("OV-11", 512) in reconciled.runtime


def test_runtime_rejects_rehashed_unbound_worker_spec_field(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    step = _find_step(release_input, "OV-11", 512)
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    receipt = sequence["accepted_samples"][0]
    spec_path = sequence_path.parent / receipt["spec_path"]
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    spec["unbound_override"] = "fabricated"
    _write_json(spec_path, spec)
    receipt["spec_file_sha256"] = _sha256(spec_path)
    receipt["spec_sha256"] = _canonical_sha256(spec)
    _write_json(
        spec_path.parent / "sequence-receipt.json", receipt
    )
    _write_json(sequence_path, sequence)
    _resign_runtime_step(release_input, step)

    with pytest.raises(ValueError, match="Task 3 evidence|spec"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_rehashed_prompt_identity_substitution(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    step = _find_step(release_input, "OV-11", 512)
    root = _path(release_input, step["runtime"]).parent
    identity_path = root / "campaign-identity.json"
    identity = json.loads(identity_path.read_text(encoding="utf-8"))
    identity["identity"]["prompt"]["sha256"] = "f" * 64
    _write_json(identity_path, _resign_campaign_identity(identity))
    sequence_path = _path(release_input, step["runtime"])
    sequence = json.loads(sequence_path.read_text(encoding="utf-8"))
    sequence["campaign_identity_sha256"] = identity["campaign_identity_sha256"]
    for receipt in (sequence["pilot"], sequence["warmup"], *sequence["accepted_samples"]):
        receipt["campaign_identity_sha256"] = identity["campaign_identity_sha256"]
        spec_path = sequence_path.parent / receipt["spec_path"]
        spec = json.loads(spec_path.read_text(encoding="utf-8"))
        spec["campaign_identity_sha256"] = identity["campaign_identity_sha256"]
        _write_json(spec_path, spec)
        receipt["spec_file_sha256"] = _sha256(spec_path)
        receipt["spec_sha256"] = _canonical_sha256(spec)
        record_path = sequence_path.parent / receipt["runtime_record_path"]
        record = json.loads(record_path.read_text(encoding="utf-8"))
        record["identity_hashes"]["prompt_sha256"] = "f" * 64
        record["identity_hashes"]["evidence_sha256"] = identity["campaign_identity_sha256"]
        _write_json(record_path, record)
    _write_json(sequence_path, sequence)
    _resign_runtime_step(release_input, step)

    with pytest.raises(ValueError, match="Task 3 evidence|campaign identity"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_rehashed_model_artifact_identity_substitution(
    tmp_path: Path,
) -> None:
    release_input = valid_release_input(tmp_path)

    _rebind_campaign_identity(
        release_input,
        lambda identity: identity["model"]["validated_artifact"].update(
            artifact_root=str(tmp_path / "forged-model-artifact")
        ),
    )

    with pytest.raises(ValueError, match="model|artifact"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_rehashed_runtime_identity_substitution(
    tmp_path: Path,
) -> None:
    release_input = valid_release_input(tmp_path)

    _rebind_campaign_identity(
        release_input,
        lambda identity: identity["runtime"]["python_executable"].update(
            path=str(tmp_path / "forged-python.exe")
        ),
    )

    with pytest.raises(ValueError, match="python_executable|runtime"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_rehashed_prompt_utf8_length_substitution(
    tmp_path: Path,
) -> None:
    release_input = valid_release_input(tmp_path)

    _rebind_campaign_identity(
        release_input,
        lambda identity: identity["prompt"].update(utf8_bytes=0),
    )

    with pytest.raises(ValueError, match="Task 3 evidence|campaign identity"):
        reconcile_comparison_release(release_input)


def test_runtime_rejects_rehashed_build_module_and_dll_substitution(
    tmp_path: Path,
) -> None:
    release_input = valid_release_input(tmp_path)

    _rebind_campaign_identity(
        release_input,
        lambda identity: identity["build"].update(
            python_module={"path": str(tmp_path / "forged.pyd"), "sha256": "f" * 64},
            runtime_dll={"path": str(tmp_path / "forged.dll"), "sha256": "e" * 64},
        ),
    )

    with pytest.raises(ValueError, match="Task 3 evidence|campaign identity"):
        reconcile_comparison_release(release_input)


def test_passed_state_requires_a_full_quality_recovery_and_receipt_chain(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    state_path = _path(release_input, release_input["campaign_state"])
    state = json.loads(state_path.read_text(encoding="utf-8"))
    state["steps"]["OV-11:512"].pop("quality_recovery")
    _write_json(state_path, state)
    release_input["campaign_state"] = _reference(state_path, base=_base(release_input))
    _resign_release_input(release_input)

    with pytest.raises(ValueError, match="quality recovery|receipt"):
        reconcile_comparison_release(release_input)


def test_state_projection_requires_an_explicit_task_six_bundle(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    state_path = _path(release_input, release_input["campaign_state"])
    state = json.loads(state_path.read_text(encoding="utf-8"))
    capture = _write_json(tmp_path / "capture.json", {"schema": "synthetic-capture"})
    bundle_root = tmp_path / "quality"
    references = {
        key: _write_json(bundle_root / f"{key}.json", {"schema": f"synthetic-{key}"})
        for key in (
            "result", "scoring_input", "private_map", "pairwise_reviews", "manual_adjudications",
        )
    }
    sheets = [
        _write_json(bundle_root / f"judge-{name}.json", {"schema": f"synthetic-judge-{name}"})
        for name in ("a", "b")
    ]
    state["steps"]["OV-11:512"]["quality_result"] = {
        "status": "passed",
        "capture_summary_path": str(capture),
        "quality_adjudication_bundle": {
            key: {"path": str(path), "sha256": _sha256(path)}
            for key, path in references.items()
        } | {"judge_score_sheets": [{"path": str(path), "sha256": _sha256(path)} for path in sheets]},
    }
    _write_json(state_path, state)
    from scripts.testing.campaigns.openvino.comparison_reconcile import build_comparison_release_input

    built = build_comparison_release_input(
        _path(release_input, release_input["matrix"]), state_path, tmp_path / "bundle-release.json"
    )

    assert isinstance(built["steps"][0]["quality_adjudication"], dict)
    assert set(built["steps"][0]["quality_adjudication"]) == {
        "result", "scoring_input", "private_map", "judge_score_sheets", "pairwise_reviews", "manual_adjudications"
    }


def test_release_rejects_nonnormalized_relative_reference(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    matrix = release_input["matrix"]
    matrix["path"] = "matrix/../" + matrix["path"]
    _resign_release_input(release_input)

    with pytest.raises(ValueError, match="normalized|relative"):
        reconcile_comparison_release(release_input)


def test_release_rejects_self_hashed_unknown_top_level_field(tmp_path: Path) -> None:
    release_input = valid_release_input(tmp_path)
    release_input["fabricated_authority"] = True
    _resign_release_input(release_input)

    with pytest.raises(ValueError, match="fields"):
        reconcile_comparison_release(release_input)


def test_boundaries_exclude_inconclusive_terminals(tmp_path: Path) -> None:
    from scripts.testing.campaigns.openvino.comparison_reconcile import derive_boundaries

    matrix_input = valid_release_input(tmp_path)
    matrix = [
        case for case in load_adaptive_comparison_matrix(_path(matrix_input, matrix_input["matrix"]))
        if case.test_id == "OV-11"
    ]
    boundaries = derive_boundaries(
        matrix, {}, {}, {
            ComparisonKey("OV-11", 512): {
                "stage": "inconclusive-safety-boundary", "evidence_sha256": "a" * 64,
            }
        }
    )

    assert boundaries["OV-11"].first_confirmed_blocked_context is None


def test_closed_campaign_rejects_ov13_artifact_terminal_after_512(tmp_path: Path) -> None:
    runtime = {}
    quality = {}
    for test_id in CANDIDATE_ORDER:
        contexts = CONTEXTS if test_id != "OV-13" else (512,)
        for context in contexts:
            key = ComparisonKey(test_id, context)
            runtime[key] = ComparisonRuntimeOutcome(key, ({}, {}, {}), {}, {}, {}, {}, {}, tmp_path / "runtime.json", "a" * 64)
            quality[key] = ComparisonQualityOutcome(key, "quality-complete", {prompt: 1.0 for prompt in ("P1", "P2", "P3", "P4", "P5", "P6")}, {"mean": 1.0}, tmp_path / "quality.json", "b" * 64)
    release = ComparisonRelease(
        runtime, quality,
        {ComparisonKey("OV-13", 1024): {"stage": "artifact-preparation"}},
        CONTEXTS, CONTEXTS, {},
    )

    with pytest.raises(ValueError, match="OV-13|512"):
        validate_closed_campaign(release)


def test_cli_existing_destinations_are_never_overwritten(
    tmp_path: Path,
) -> None:
    import scripts.testing.build_official_openvino_comparison_release_evidence as cli

    output = tmp_path / "release.json"
    captures = tmp_path / "captures.json"
    output.write_text("old-release", encoding="utf-8")
    captures.write_text("old-captures", encoding="utf-8")

    with pytest.raises(FileExistsError, match="already exists"):
        cli.main([
            "--matrix", str(tmp_path / "matrix.json"),
            "--campaign-state", str(tmp_path / "state.json"),
            "--output", str(output),
            "--quality-capture-index", str(captures),
        ])

    assert output.read_text(encoding="utf-8") == "old-release"
    assert captures.read_text(encoding="utf-8") == "old-captures"


def test_cli_second_publication_failure_leaves_only_private_capture_index(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    import scripts.testing.build_official_openvino_comparison_release_evidence as cli

    output = tmp_path / "release.json"
    captures = tmp_path / "captures.json"
    def fake_release(_matrix: Path, _state: Path, staged: Path) -> dict[str, Any]:
        payload = {"schema": "official-openvino-comparison-release-input-v1", "release_input_sha256": "a" * 64, "steps": []}
        staged.write_text(json.dumps(payload), encoding="utf-8")
        return payload

    def fake_captures(_state: Path, staged: Path) -> dict[str, Any]:
        payload = {"schema": "official-openvino-quality-capture-index-v1", "captures": []}
        staged.write_text(json.dumps(payload), encoding="utf-8")
        return payload

    real_link = cli.os.link
    calls = 0

    def fail_second(source: Path, destination: Path) -> None:
        nonlocal calls
        calls += 1
        if calls == 2:
            raise OSError("injected second publication failure")
        real_link(source, destination)

    monkeypatch.setattr(cli, "build_comparison_release_input", fake_release)
    monkeypatch.setattr(cli, "build_quality_capture_index", fake_captures)
    monkeypatch.setattr(cli.os, "link", fail_second)

    with pytest.raises(OSError, match="second publication"):
        cli.main([
            "--matrix", str(tmp_path / "matrix.json"),
            "--campaign-state", str(tmp_path / "state.json"),
            "--output", str(output),
            "--quality-capture-index", str(captures),
        ])

    assert not output.exists()
    assert json.loads(captures.read_text(encoding="utf-8"))["captures"] == []
