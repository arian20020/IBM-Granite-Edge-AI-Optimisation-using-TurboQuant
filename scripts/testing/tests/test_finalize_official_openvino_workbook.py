import copy
import hashlib
import json
import math
import re
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[3]

PERFORMANCE_METRICS = (
    "load_ms",
    "ttft_ms",
    "prompt_tps",
    "tpot_ms",
    "decode_tps",
    "generation_duration_ms",
)
MEMORY_METRICS = (
    "peak_working_set_mb",
    "peak_private_mb",
    "available_ram_min_mb",
    "kv_mb",
    "gpu_memory_peak_mb",
)
ALL_METRICS = PERFORMANCE_METRICS + MEMORY_METRICS + (
    "cpu_percent",
    "gpu_percent",
)


def _sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def _canonical_sha256(value):
    return hashlib.sha256(
        (
            json.dumps(
                value,
                ensure_ascii=False,
                allow_nan=False,
                sort_keys=True,
                separators=(",", ":"),
            )
            + "\n"
        ).encode("utf-8")
    ).hexdigest()


def _runtime_json_sha256(value):
    return hashlib.sha256(
        json.dumps(
            value,
            ensure_ascii=True,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8")
    ).hexdigest()


def _aggregate(values):
    ordered = sorted(values)
    return {
        "count": len(values),
        "max": max(values),
        "mean": sum(values) / len(values),
        "median": ordered[len(ordered) // 2],
        "min": min(values),
    }


def _case(test_id="OV-TQ-13", contexts=(512,), **changes):
    case = {
        "test_id": test_id,
        "phase": "formal",
        "description": "Granite 3B governed row",
        "model": "granite-3b",
        "weight_precision": "u8",
        "device": "cpu",
        "contexts": list(contexts),
        "k_algorithm": "tbq4",
        "v_algorithm": "tbq4",
        "k_precision": "u4",
        "v_precision": "u4",
        "key_cache_precision": "u4",
        "value_cache_precision": "u4",
        "requested_device": "CPU",
        "runtime_key_algorithm": "TBQ4",
        "runtime_value_algorithm": "TBQ4",
        "norm_correction": True,
        "attention_path": "stateful_sdpa_reference_codec",
        "execution_route": "patched-stateful",
        "expected_outcome": "pass",
        "suitable_host_required": False,
        "numeric_generation_metrics_expected": True,
        "quality_required": True,
        "required_metrics": list(ALL_METRICS),
    }
    case.update(changes)
    return case


def _write_measurement(root, case, context, *, base=0.0):
    from scripts.testing.official_openvino.metrics import summarize_samples
    from scripts.testing.official_openvino.runtime_process import (
        measurement_sample,
    )

    summary_root = root / case["test_id"] / f"context-{context}"
    summary_root.mkdir(parents=True)
    properties = {
        "ATTENTION_BACKEND": "SDPA",
        "CACHE_DIR": str(
            (
                summary_root
                / "cache"
                / case["test_id"]
                / f"context-{context}"
            ).resolve()
        ),
        "ENABLE_CPU_PINNING": False,
        "INFERENCE_NUM_THREADS": 1,
        "NUM_STREAMS": 1,
        "PERFORMANCE_HINT": "LATENCY",
        "TURBOQUANT_KEY_ALGORITHM": case["runtime_key_algorithm"],
        "TURBOQUANT_VALUE_ALGORITHM": case["runtime_value_algorithm"],
        "TURBOQUANT_NORM_CORRECTION": case["norm_correction"],
    }
    config = {
        "device": case["requested_device"],
        "max_new_tokens": 4,
        "expected_input_tokens": context,
        "ignore_eos": True,
        "seed": 42,
        "apply_chat_template": False,
        "properties": properties,
    }
    prompt = "fixture governed prompt"
    model_root = summary_root / "fixture-model"
    identity = {
        "config": config,
        "context": context,
        "matrix": {
            "case": copy.deepcopy(case),
            "file_sha256": (
                "7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42"
            ),
        },
        "prompt": {
            "sha256": hashlib.sha256(prompt.encode("utf-8")).hexdigest(),
            "utf8_bytes": len(prompt.encode("utf-8")),
        },
        "model": {
            "validated_artifact": {
                "artifact_root": str(model_root),
            }
        },
    }
    campaign_identity = _runtime_json_sha256(identity)
    (summary_root / "campaign-identity.json").write_text(
        json.dumps(
            {
                "schema": "official-openvino-wb04-campaign-identity/v1",
                "identity": identity,
                "campaign_identity_sha256": campaign_identity,
            },
            sort_keys=True,
        ),
        encoding="utf-8",
    )

    sources = []
    canonical_samples = []
    receipts = {}
    roles = ("pilot", "warmup", "sample-1", "sample-2", "sample-3")
    for role_index, role in enumerate(roles, 1):
        sample_number = (
            int(role.rsplit("-", 1)[1])
            if role.startswith("sample-")
            else role_index
        )
        run = summary_root / "attempts" / role / "attempt-001" / "run"
        run.mkdir(parents=True)
        scalar = base + float(sample_number)
        output = f"{case['test_id']}/{context}/{role}/output"
        output_hash = hashlib.sha256(output.encode("utf-8")).hexdigest()
        kv_bytes = int((context + 128) * 1024)
        activation = {
            "status": "activated",
            "requested_key_algorithm": case["runtime_key_algorithm"],
            "requested_value_algorithm": case["runtime_value_algorithm"],
            "activated_key_algorithm": case["runtime_key_algorithm"],
            "activated_value_algorithm": case["runtime_value_algorithm"],
            "requested_key_cache_precision": case["key_cache_precision"],
            "requested_value_cache_precision": case["value_cache_precision"],
            "activated_key_cache_precision": case["key_cache_precision"],
            "activated_value_cache_precision": case["value_cache_precision"],
            "observed_key_state_precision": "u8+f32+i32",
            "observed_value_state_precision": "u8+f32+i32",
            "norm_correction": case["norm_correction"],
            "attention_path": case["attention_path"],
            "device": case["requested_device"],
            "actual_device": case["requested_device"],
            "fallback": False,
            "expected_bytes": kv_bytes,
            "actual_bytes": kv_bytes,
            "expected_persistent_standard_bytes": 0,
            "actual_persistent_standard_bytes": 0,
            "expected_persistent_payload_bytes": kv_bytes,
            "actual_persistent_payload_bytes": kv_bytes,
            "expected_persistent_norm_bytes": 0,
            "actual_persistent_norm_bytes": 0,
            "expected_persistent_metadata_bytes": 0,
            "actual_persistent_metadata_bytes": 0,
            "operation_type": "TurboQuantStateUpdateDecode",
            "operation_count": 1,
            "matched_state_count": 1,
            "transformed_model_hash": "b" * 64,
            "runtime_layer_type": "Reference",
            "build_commit": "c" * 40,
            "model_hash": "d" * 64,
            "decoded_scratch_bytes": 1024,
            "full_precision_equivalent_bytes": kv_bytes * 2,
        }
        worker = {
            "controlled_test_id": case["test_id"],
            "context": context,
            "device": case["requested_device"],
            "output_valid": True,
            "output": output,
            "load_ms": scalar,
            "ttft_ms": scalar + 10,
            "prompt_tps": scalar + 20,
            "tpot_ms": scalar + 30,
            "decode_tps": scalar + 40,
            "generation_duration_ms": scalar + 50,
        }
        cpu_values = [20.0 + sample_number, 21.0 + sample_number]
        gpu_values = [0.0, 0.0]
        attempt = {
            "schema": "official-openvino-wb04-governed-run/v1",
            "role": role,
            "valid": True,
            "validation_errors": [],
            "exit_code": 0,
            "sampler_exit_code": 0,
            "timed_out": False,
            "low_memory_stop": False,
            "cleanup_process_count": 0,
            "memory_sample_count": 2,
            "peak_working_set_bytes": int((scalar + 60) * 1024 * 1024),
            "peak_private_bytes": int((scalar + 70) * 1024 * 1024),
            "available_ram_bytes": {
                "before": int((scalar + 90) * 1024 * 1024),
                "minimum": int((scalar + 80) * 1024 * 1024),
                "after": int((scalar + 91) * 1024 * 1024),
            },
            "gpu_dedicated_memory_peak_mb": 0.0,
            "gpu_shared_memory_peak_mb": 0.0,
            "gpu_memory_peak_mb": 0.0,
            "cpu_percent": {
                "count": 2,
                "mean": sum(cpu_values) / 2,
                "median": sum(cpu_values) / 2,
                "peak": max(cpu_values),
                "query_succeeded": True,
                "values": cpu_values,
            },
            "gpu_percent": {
                "count": 2,
                "mean": 0.0,
                "median": 0.0,
                "peak": 0.0,
                "query_succeeded": True,
                "values": gpu_values,
            },
            "output_sha256": output_hash,
            "stdout_sha256": hashlib.sha256(b"stdout").hexdigest(),
            "stderr_sha256": hashlib.sha256(b"stderr").hexdigest(),
            "telemetry_sha256": _runtime_json_sha256(activation),
            "worker": worker,
            "activation": activation,
        }
        (run / "stdout.txt").write_bytes(b"stdout")
        (run / "stderr.txt").write_bytes(b"stderr")
        attempt_path = run / "attempt.json"
        attempt_path.write_text(json.dumps(attempt, sort_keys=True), encoding="utf-8")
        spec_path = run.parent.parent / "spec.json"
        spec = {
            "schema": "official-openvino-wb04-worker-spec/v1",
            "controlled_test_id": case["test_id"],
            "context": context,
            "role": role,
            "campaign_identity_sha256": campaign_identity,
            "device": config["device"],
            "max_new_tokens": config["max_new_tokens"],
            "expected_input_tokens": config["expected_input_tokens"],
            "ignore_eos": config["ignore_eos"],
            "seed": config["seed"],
            "apply_chat_template": config["apply_chat_template"],
            "properties": config["properties"],
            "prompt": prompt,
            "model_path": str(model_root),
        }
        spec_path.write_text(json.dumps(spec, sort_keys=True), encoding="utf-8")
        receipt = {
            "schema": "official-openvino-wb04-sequence-receipt/v1",
            "role": role,
            "attempt_number": 1,
            "campaign_identity_sha256": campaign_identity,
            "spec_sha256": _runtime_json_sha256(spec),
            "spec_path": spec_path.relative_to(summary_root).as_posix(),
            "spec_file_sha256": _sha256(spec_path),
            "runtime_record_path": attempt_path.relative_to(summary_root).as_posix(),
            "runtime_record_sha256": _sha256(attempt_path),
            "accepted": True,
        }
        receipts[role] = receipt
        if role.startswith("sample-"):
            sources.append(
                {
                    "sample_id": role,
                    "path": str(attempt_path),
                    "sha256": _sha256(attempt_path),
                }
            )
            canonical_samples.append(measurement_sample(attempt, attempt_path))

    summary = summarize_samples(canonical_samples)
    summary.update(
        {
            "accepted": True,
            "cleanup_process_count": 0,
            "test_id": case["test_id"],
            "context_tokens": context,
            "campaign_identity_sha256": campaign_identity,
            "runtime_config_sha256": _runtime_json_sha256(config),
        }
    )
    path = summary_root / "measurement-summary.json"
    path.write_text(json.dumps(summary, sort_keys=True), encoding="utf-8")
    sequence = {
        "schema": "official-openvino-wb04-attempt-sequence/v1",
        "campaign_identity_sha256": campaign_identity,
        "pilot_passed": True,
        "warmup_excluded": True,
        "pilot": receipts["pilot"],
        "warmup": receipts["warmup"],
        "accepted_samples": [
            receipts[f"sample-{number}"] for number in (1, 2, 3)
        ],
        "accepted_sample_count": 3,
        "cleanup_process_count": 0,
        "measurement_summary_path": "measurement-summary.json",
        "measurement_summary_sha256": _sha256(path),
    }
    (summary_root / "attempt-sequence.json").write_text(
        json.dumps(sequence, sort_keys=True),
        encoding="utf-8",
    )
    return path


def _terminal(root, case, context, *, status="host-resource-blocked"):
    reason = "fixed 2,048 MiB floor was crossed during bounded execution"
    metric_outcome = {
        "host-resource-blocked": "not-produced-by-resource-blocked",
        "device-resource-blocked": "not-produced-by-resource-blocked",
        "host-resource-blocked-not-launched": (
            "not-produced-by-resource-envelope"
        ),
        "suitable-host-required-not-launched": (
            "not-produced-by-suitable-host-requirement"
        ),
        "controlled-terminal-not-launched": (
            "not-produced-by-controlled-terminal"
        ),
    }[status]
    raw = root / f"{case['test_id']}-{context}-raw-resource-guard.json"
    raw.write_text(
        json.dumps(
            {
                "test_id": case["test_id"],
                "context_tokens": context,
                "cleanup_process_count": 0,
            }
        ),
        encoding="utf-8",
    )
    evidence = root / f"{case['test_id']}-{context}-resource-classification.json"
    evidence.write_text(
        json.dumps(
            {
                "schema": "official-openvino-wb04-terminal-classification/v1",
                "test_id": case["test_id"],
                "context_tokens": context,
                "status": status,
                "reason": reason,
                "sample_count": 0,
                "cleanup_process_count": 0,
                "metric_outcome": metric_outcome,
                "source_evidence": [
                    {"path": str(raw), "sha256": _sha256(raw)}
                ],
            },
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    return {
        "test_id": case["test_id"],
        "context_tokens": context,
        "accepted": False,
        "status": status,
        "reason": reason,
        "sample_count": 0,
        "cleanup_process_count": 0,
        "metric_outcome": metric_outcome,
        "evidence_path": str(evidence),
        "evidence_sha256": _sha256(evidence),
    }


def _write_governed_host_resource_terminal(root, case, context, attempt_count=3):
    campaign_root = root / case["test_id"] / f"context-{context}"
    campaign_root.mkdir(parents=True)
    properties = {"ATTENTION_BACKEND": "SDPA"}
    if case["runtime_key_algorithm"] == "STANDARD":
        properties["KEY_CACHE_PRECISION"] = case["key_cache_precision"]
    else:
        properties["TURBOQUANT_KEY_ALGORITHM"] = case[
            "runtime_key_algorithm"
        ]
    if case["runtime_value_algorithm"] == "STANDARD":
        properties["VALUE_CACHE_PRECISION"] = case[
            "value_cache_precision"
        ]
    else:
        properties["TURBOQUANT_VALUE_ALGORITHM"] = case[
            "runtime_value_algorithm"
        ]
    if (
        case["runtime_key_algorithm"] != "STANDARD"
        or case["runtime_value_algorithm"] != "STANDARD"
    ):
        properties["TURBOQUANT_NORM_CORRECTION"] = case["norm_correction"]
    config = {
        "device": case["requested_device"],
        "max_new_tokens": 4,
        "expected_input_tokens": context,
        "ignore_eos": True,
        "seed": 42,
        "apply_chat_template": False,
        "properties": properties,
    }
    prompt = "governed resource-terminal fixture"
    model_root = campaign_root / "fixture-model"
    identity = {
        "build": {"commit": "a" * 40},
        "config": config,
        "context": context,
        "matrix": {
            "case": copy.deepcopy(case),
            "file_sha256": (
                "7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42"
            ),
        },
        "model": {
            "validated_artifact": {
                "artifact_root": str(model_root),
            }
        },
        "prompt": {
            "sha256": hashlib.sha256(prompt.encode("utf-8")).hexdigest(),
            "utf8_bytes": len(prompt.encode("utf-8")),
        },
        "runtime": {"python": sys.version},
    }
    campaign_identity = _runtime_json_sha256(identity)
    (campaign_root / "campaign-identity.json").write_text(
        json.dumps(
            {
                "schema": "official-openvino-wb04-campaign-identity/v1",
                "identity": identity,
                "campaign_identity_sha256": campaign_identity,
            },
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    sources = []
    for number in range(1, attempt_count + 1):
        attempt_root = (
            campaign_root
            / "attempts"
            / "pilot"
            / f"attempt-{number:03d}"
        )
        run = attempt_root / "run"
        run.mkdir(parents=True)
        attempt = {
            "schema": "official-openvino-wb04-governed-run/v1",
            "role": "pilot",
            "valid": False,
            "validation_errors": ["workload did not exit successfully"],
            "exit_code": 137,
            "sampler_exit_code": 0,
            "timed_out": False,
            "low_memory_stop": True,
            "cleanup_process_count": 0,
            "memory_sample_count": 10,
            "available_ram_bytes": {
                "before": 8 * 1024 * 1024 * 1024,
                "minimum": (2048 * 1024 * 1024) - number,
                "after": 8 * 1024 * 1024 * 1024,
            },
            "minimum_available_ram_bytes": 2048 * 1024 * 1024,
            "activation": None,
            "worker": None,
            "output_sha256": None,
            "telemetry_sha256": None,
            "sampler_job": {
                "setup_ok": True,
                "query_ok": True,
                "survivor_pids_after_cleanup": [],
            },
            "workload_job": {
                "setup_ok": True,
                "query_ok": True,
                "survivor_pids_after_cleanup": [],
            },
            "command": [
                sys.executable,
                "-m",
                "scripts.testing.official_openvino.measurement_worker",
                "--spec",
                str(attempt_root / "spec.json"),
            ],
        }
        attempt_path = run / "attempt.json"
        attempt_path.write_text(
            json.dumps(attempt, sort_keys=True), encoding="utf-8"
        )
        spec = {
            "schema": "official-openvino-wb04-worker-spec/v1",
            "controlled_test_id": case["test_id"],
            "context": context,
            "role": "pilot",
            "campaign_identity_sha256": campaign_identity,
            "device": config["device"],
            "max_new_tokens": config["max_new_tokens"],
            "expected_input_tokens": config["expected_input_tokens"],
            "ignore_eos": config["ignore_eos"],
            "seed": config["seed"],
            "apply_chat_template": config["apply_chat_template"],
            "properties": config["properties"],
            "prompt": prompt,
            "model_path": str(model_root),
        }
        spec_path = attempt_root / "spec.json"
        spec_path.write_text(
            json.dumps(spec, sort_keys=True), encoding="utf-8"
        )
        receipt = {
            "schema": "official-openvino-wb04-sequence-receipt/v1",
            "role": "pilot",
            "attempt_number": number,
            "campaign_identity_sha256": campaign_identity,
            "spec_sha256": _runtime_json_sha256(spec),
            "spec_path": spec_path.relative_to(campaign_root).as_posix(),
            "spec_file_sha256": _sha256(spec_path),
            "runtime_record_path": attempt_path.relative_to(
                campaign_root
            ).as_posix(),
            "runtime_record_sha256": _sha256(attempt_path),
            "accepted": False,
            "controller_error": "fixed RAM floor crossed",
        }
        (attempt_root / "sequence-receipt.json").write_text(
            json.dumps(receipt, sort_keys=True), encoding="utf-8"
        )
        receipt_path = attempt_root / "sequence-receipt.json"
        sources.extend(
            [
                {
                    "path": str(attempt_path),
                    "sha256": _sha256(attempt_path),
                },
                {"path": str(spec_path), "sha256": _sha256(spec_path)},
                {
                    "path": str(receipt_path),
                    "sha256": _sha256(receipt_path),
                },
            ]
        )
    identity_path = campaign_root / "campaign-identity.json"
    sources.append(
        {"path": str(identity_path), "sha256": _sha256(identity_path)}
    )

    record = _terminal(root, case, context)
    evidence_path = Path(record["evidence_path"])
    classification = json.loads(evidence_path.read_text(encoding="utf-8"))
    classification["source_evidence"] = sources
    evidence_path.write_text(
        json.dumps(classification, sort_keys=True), encoding="utf-8"
    )
    record["evidence_sha256"] = _sha256(evidence_path)
    return record


def _write_direct_quality_resource_terminal(root, runtime):
    from scripts.testing.run_official_openvino_quality import (
        load_prompt_contract,
    )

    campaign = json.loads(
        (runtime.summary_path.parent / "campaign-identity.json").read_text(
            encoding="utf-8"
        )
    )["identity"]
    case = campaign["matrix"]["case"]
    properties = copy.deepcopy(campaign["config"]["properties"])
    prompt_root = (
        REPO_ROOT / "experiments" / "granite_turboquant_intel" / "prompts"
    )
    prompt_contract = load_prompt_contract(
        prompt_root / "fixed-feasibility-prompt-set-v1.json",
        prompt_root / "rendered",
    )
    prompts = [
        {
            "prompt_id": prompt_id,
            "turn_id": "turn_1",
            "prompt": prompt_contract["prompts"][prompt_id]["execution"][
                "prompt"
            ],
        }
        for prompt_id in ("P1", "P2", "P3", "P4", "P5")
    ]
    prompts.extend(
        [
            {
                "prompt_id": "P6",
                "turn_id": "turn_1",
                "prompt": prompt_contract["prompts"]["P6"]["execution"][
                    "turn_1_prompt"
                ],
            },
            {
                "prompt_id": "P6",
                "turn_id": "turn_2",
                "prompt": prompt_contract["prompts"]["P6"]["execution"][
                    "turn_2_prompt"
                ],
            },
        ]
    )
    guard_sources = []
    for number in (1, 2):
        governed = (
            root
            / f"attempt-{number:03d}"
            / runtime.key.test_id
            / f"context-{runtime.key.context_tokens}"
            / "governed"
        )
        governed.mkdir(parents=True)
        spec_path = governed / "worker-spec.json"
        spec = {
            "schema": "official-openvino-wb04-quality-worker-spec/v1",
            "device": case["requested_device"],
            "model_path": campaign["model"]["validated_artifact"][
                "artifact_root"
            ],
            "generation_settings": {
                "apply_chat_template": False,
                "do_sample": False,
                "max_new_tokens": 256,
                "rng_seed": 42,
            },
            "properties": properties,
            "prompts": prompts,
        }
        spec_path.write_text(
            json.dumps(spec, sort_keys=True), encoding="utf-8"
        )
        guard_path = governed / "guard-evidence.json"
        guard = {
            "schema": "official-openvino-owned-process-guard/v1",
            "valid": False,
            "validation_errors": [
                "available RAM fell below the configured floor"
            ],
            "exit_code": 1,
            "timed_out": False,
            "low_memory_stop": True,
            "cleanup_process_count": 0,
            "configured_minimum_available_ram_bytes": 2048 * 1024 * 1024,
            "observed_available_ram_bytes": {
                "before": 8 * 1024 * 1024 * 1024,
                "minimum": (2048 * 1024 * 1024) - number,
                "after": 8 * 1024 * 1024 * 1024,
            },
            "memory_sample_count": 10,
            "termination_reason": "minimum_available_ram",
            "run_id": hashlib.sha256(
                f"quality-guard-{number}".encode("utf-8")
            ).hexdigest(),
            "command": [
                sys.executable,
                "-m",
                "scripts.testing.official_openvino.quality_worker",
                "--spec",
                str(spec_path),
            ],
            "job_object": {
                "setup_ok": True,
                "query_ok": True,
                "survivor_pids_after_cleanup": [],
            },
            "bound_inputs": [
                {
                    "name": "quality_worker_spec",
                    "path": str(spec_path.resolve()),
                    "sha256": _sha256(spec_path),
                }
            ],
        }
        guard_path.write_text(
            json.dumps(guard, sort_keys=True), encoding="utf-8"
        )
        guard_sources.extend(
            [
                {
                    "path": str(guard_path),
                    "sha256": _sha256(guard_path),
                },
                {
                    "path": str(spec_path),
                    "sha256": _sha256(spec_path),
                },
            ]
        )
    reason = "two governed quality launches crossed the fixed RAM floor"
    evidence_path = root / "quality-resource-classification.json"
    evidence_path.write_text(
        json.dumps(
            {
                "schema": (
                    "official-openvino-wb04-quality-terminal-classification/v1"
                ),
                "test_id": runtime.key.test_id,
                "context_tokens": runtime.key.context_tokens,
                "status": "quality-host-resource-blocked",
                "reason": reason,
                "prompt_count": 0,
                "score_outcome": (
                    "not-produced-by-quality-resource-blocked"
                ),
                "cleanup_process_count": 0,
                "source_evidence": [
                    {
                        "path": str(runtime.summary_path),
                        "sha256": runtime.summary_sha256,
                    },
                    *guard_sources,
                ],
            },
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    return {
        "test_id": runtime.key.test_id,
        "context_tokens": runtime.key.context_tokens,
        "status": "quality-host-resource-blocked",
        "reason": reason,
        "prompt_count": 0,
        "score_outcome": "not-produced-by-quality-resource-blocked",
        "evidence_path": str(evidence_path),
        "evidence_sha256": _sha256(evidence_path),
    }


def _write_quality_resource_envelope_terminal(root, runtime, anchor_record):
    reason = "quality not launched because target cache demand is no smaller"
    evidence_path = root / "quality-resource-envelope-classification.json"
    evidence_path.parent.mkdir(parents=True, exist_ok=True)
    evidence_path.write_text(
        json.dumps(
            {
                "schema": (
                    "official-openvino-wb04-quality-terminal-classification/v1"
                ),
                "test_id": runtime.key.test_id,
                "context_tokens": runtime.key.context_tokens,
                "status": "quality-host-resource-blocked-not-launched",
                "reason": reason,
                "prompt_count": 0,
                "score_outcome": (
                    "not-produced-by-quality-resource-envelope"
                ),
                "cleanup_process_count": 0,
                "source_evidence": [
                    {
                        "path": str(runtime.summary_path),
                        "sha256": runtime.summary_sha256,
                    },
                    {
                        "path": anchor_record["evidence_path"],
                        "sha256": anchor_record["evidence_sha256"],
                    },
                ],
            },
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    return {
        "test_id": runtime.key.test_id,
        "context_tokens": runtime.key.context_tokens,
        "status": "quality-host-resource-blocked-not-launched",
        "reason": reason,
        "prompt_count": 0,
        "score_outcome": "not-produced-by-quality-resource-envelope",
        "evidence_path": str(evidence_path),
        "evidence_sha256": _sha256(evidence_path),
    }


def _write_controlled_terminal_inventory(root):
    artifacts_root = root / "artifacts"
    specs_root = root / "campaign-specs-dbbb784"
    artifact_path = artifacts_root / "granite-u8" / "artifact-manifest.json"
    artifact_path.parent.mkdir(parents=True)
    artifact_path.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "status": "validated",
                "model": {
                    "family": "granite-4.1",
                    "parameter_scale": "3b",
                    "precision": "u8",
                },
            },
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    spec_path = specs_root / "OV-03" / "context-4096" / "spec.json"
    spec_path.parent.mkdir(parents=True)
    spec_path.write_text(
        json.dumps(
            {
                "schema": "official-openvino-wb04-worker-spec/v1",
                "controlled_test_id": "OV-03",
                "context": 4096,
            },
            sort_keys=True,
        ),
        encoding="utf-8",
    )
    relative_artifact = artifact_path.relative_to(REPO_ROOT).as_posix()
    relative_spec = spec_path.relative_to(REPO_ROOT).as_posix()
    inventory = {
        "schema": "official-openvino-wb04-artifact-spec-inventory/v1",
        "matrix_sha256": (
            "7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42"
        ),
        "inventory_roots": [
            artifacts_root.relative_to(REPO_ROOT).as_posix(),
            specs_root.relative_to(REPO_ROOT).as_posix(),
        ],
        "artifact_entries": [
            {
                "path": relative_artifact,
                "sha256": _sha256(artifact_path),
                "model": "granite-3b",
                "weight_precision": "u8",
            }
        ],
        "spec_entries": [
            {
                "path": relative_spec,
                "sha256": _sha256(spec_path),
                "test_id": "OV-03",
                "context_tokens": 4096,
            }
        ],
        "conclusions": [
            {
                "test_id": "OV-01",
                "context_tokens": 1024,
                "status": "controlled-terminal-not-launched",
                "reason_code": "diagnostic-spec-absent",
                "matching_artifact_paths": [],
                "matching_spec_paths": [],
            },
            {
                "test_id": "OV-02",
                "context_tokens": 2048,
                "status": "controlled-terminal-not-launched",
                "reason_code": "fp16-artifact-and-spec-absent",
                "matching_artifact_paths": [],
                "matching_spec_paths": [],
            },
        ],
    }
    inventory["inventory_sha256"] = _canonical_sha256(inventory)
    inventory_path = root / "artifact-spec-inventory.json"
    inventory_path.write_text(
        json.dumps(inventory, sort_keys=True), encoding="utf-8"
    )
    return inventory_path, (artifacts_root, specs_root)


def _write_device_resource_terminal(root, case):
    initial_spec_path = (
        root
        / "campaign-specs"
        / "OV-06"
        / "context-4096"
        / "spec.json"
    )
    corrected_spec_path = (
        root
        / "campaign-specs-corrected"
        / "OV-06"
        / "context-4096"
        / "spec.json"
    )
    initial_spec_path.parent.mkdir(parents=True)
    corrected_spec_path.parent.mkdir(parents=True)
    initial_spec = {
        "schema": "official-openvino-wb04-worker-spec/v1",
        "controlled_test_id": "OV-06",
        "context": 4096,
        "role": "pilot",
        "campaign_identity_sha256": "a" * 64,
        "device": "GPU",
        "properties": {
            "ATTENTION_BACKEND": "SDPA",
            "NUM_STREAMS": 1,
        },
    }
    corrected_spec = copy.deepcopy(initial_spec)
    corrected_spec["properties"]["NUM_STREAMS"] = "1"
    initial_spec_path.write_text(
        json.dumps(initial_spec, sort_keys=True), encoding="utf-8"
    )
    corrected_spec_path.write_text(
        json.dumps(corrected_spec, sort_keys=True), encoding="utf-8"
    )
    sources = []
    expected_attempts = []
    for number in (1, 2, 3):
        run = root / "diagnostics" / f"attempt-{number:03d}" / "run"
        run.mkdir(parents=True)
        stderr = (
            "Invalid value: 1 for property: NUM_STREAMS"
            if number == 1
            else ""
        )
        (run / "stderr.txt").write_text(stderr, encoding="utf-8")
        (run / "stdout.txt").write_text("", encoding="utf-8")
        low_memory = number > 1
        spec_path = corrected_spec_path if low_memory else initial_spec_path
        attempt = {
            "schema": "official-openvino-wb04-governed-run/v1",
            "role": "pilot",
            "valid": False,
            "validation_errors": ["workload did not exit successfully"],
            "exit_code": 137 if low_memory else 1,
            "sampler_exit_code": 0,
            "timed_out": False,
            "low_memory_stop": low_memory,
            "cleanup_process_count": 0,
            "available_ram_bytes": {
                "before": 8 * 1024 * 1024 * 1024,
                "minimum": (
                    (2048 * 1024 * 1024) - number
                    if low_memory
                    else 4 * 1024 * 1024 * 1024
                ),
                "after": 8 * 1024 * 1024 * 1024,
            },
            "minimum_available_ram_bytes": 2048 * 1024 * 1024,
            "memory_sample_count": 10,
            "activation": None,
            "worker": None,
            "output_sha256": None,
            "telemetry_sha256": None,
            "stdout_sha256": _sha256(run / "stdout.txt"),
            "stderr_sha256": _sha256(run / "stderr.txt"),
            "command": [
                sys.executable,
                "-m",
                "scripts.testing.official_openvino.measurement_worker",
                "--spec",
                str(spec_path),
            ],
            "sampler_job": {
                "setup_ok": True,
                "query_ok": True,
                "survivor_pids_after_cleanup": [],
            },
            "workload_job": {
                "setup_ok": True,
                "query_ok": True,
                "survivor_pids_after_cleanup": [],
            },
        }
        attempt_path = run / "attempt.json"
        attempt_path.write_text(
            json.dumps(attempt, sort_keys=True), encoding="utf-8"
        )
        source = {"path": str(attempt_path), "sha256": _sha256(attempt_path)}
        sources.append(source)
        expected_attempts.append(
            (attempt_path.relative_to(REPO_ROOT).as_posix(), source["sha256"])
        )
    record = _terminal(
        root,
        case,
        4096,
        status="device-resource-blocked",
    )
    evidence_path = Path(record["evidence_path"])
    classification = json.loads(evidence_path.read_text(encoding="utf-8"))
    classification["source_evidence"] = sources
    evidence_path.write_text(
        json.dumps(classification, sort_keys=True), encoding="utf-8"
    )
    record["evidence_sha256"] = _sha256(evidence_path)
    return (
        record,
        tuple(expected_attempts),
        initial_spec_path,
        corrected_spec_path,
    )


class OfficialOpenVINOWorkbookFinalizerTests(unittest.TestCase):
    def _three_measured_runtime_outcomes(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            index_measurement_summaries,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            first_case = _case("OV-TQ-13", contexts=(512,))
            second_case = _case("OV-TQ-14", contexts=(512, 2048))
            return index_measurement_summaries(
                [
                    _write_measurement(root, first_case, 512),
                    _write_measurement(root, second_case, 512, base=100),
                    _write_measurement(root, second_case, 2048, base=200),
                ],
                [first_case, second_case],
            )

    def _presentation_runtime_outcomes(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            RuntimeOutcome,
        )

        rows = dict(self._three_measured_runtime_outcomes())
        terminal_keys = (
            ("OV-01", 1024), ("OV-02", 2048), ("OV-03", 4096),
            ("OV-06", 4096), ("OV-07", 2048), ("OV-08", 4096),
            ("OV-09", 4096), ("OV-10", 4096), ("OV-TQ-03", 4096),
            ("OV-TQ-04", 4096), ("OV-TQ-05", 4096), ("OV-TQ-06", 4096),
            ("OV-TQ-07", 4096), ("OV-TQ-08", 4096), ("OV-TQ-09", 4096),
            ("OV-TQ-10", 4096), ("OV-TQ-11", 4096), ("OV-TQ-12", 4096),
            ("OV-TQ-13", 2048), ("OV-TQ-13", 4096), ("OV-TQ-13", 8192),
            ("OV-TQ-14", 4096), ("OV-TQ-14", 8192), ("OV-TQ-15", 4096),
            ("OV-TQ-16", 4096), ("OV-TQ-17", 4096),
        )
        for test_id, context in terminal_keys:
            key = RuntimeKey(test_id, context)
            rows[key] = RuntimeOutcome(
                key=key,
                status="host-resource-blocked",
                configuration_id=f"{test_id}-{context}",
                accepted=False,
                sample_count=0,
                cleanup_process_count=0,
                metric_outcome="not-produced-by-resource-blocked",
                reason="RAM safety floor reached",
                evidence_path=Path(f"{test_id}-{context}.json"),
                evidence_sha256="d" * 64,
                summary_path=None,
                summary_sha256="d" * 64,
                campaign_identity_sha256="d" * 64,
                runtime_config_sha256="d" * 64,
                metrics={},
                activation={},
                samples={},
            )
        expected_rejection = RuntimeKey("OV-04", 4096)
        rows[expected_rejection] = RuntimeOutcome(
            key=expected_rejection,
            status="passed: expected-rejection",
            configuration_id="OV-04-4096",
            accepted=False,
            sample_count=0,
            cleanup_process_count=0,
            metric_outcome="not-produced-by-expected-rejection",
            reason="governed expected rejection",
            evidence_path=Path("OV-04-4096.json"),
            evidence_sha256="e" * 64,
            summary_path=None,
            summary_sha256="e" * 64,
            campaign_identity_sha256="e" * 64,
            runtime_config_sha256="e" * 64,
            metrics={},
            activation={},
            samples={},
        )
        return rows

    def _canonical_presentation_ids(self):
        matrix = json.loads(
            (REPO_ROOT / "experiments" / "manifests" / "official-openvino" / "retest-matrix.json").read_text(encoding="utf-8")
        )
        return {case["test_id"] for case in matrix["cases"]}

    def _structured_presentation_text(self):
        incomplete = {
            "OV-01", "OV-C01", "OV-02", "OV-B04", "OV-03", "OV-06",
            "OV-TQ-03", "OV-TQ-04", "OV-TQ-05", "OV-TQ-06", "OV-TQ-07",
            "OV-TQ-08", "OV-TQ-09", "OV-TQ-10", "OV-TQ-11", "OV-TQ-12",
            "OV-TQ-13", "OV-TQ-14", "OV-TQ-15", "OV-C04", "OV-C05",
            "OV-C06", "OV-07", "OV-08", "OV-09", "OV-10", "OV-TQ-16",
            "OV-TQ-17", "OV-B08", "OV-B09", "OV-B10", "OV-B12",
            "OV-TQS-01", "OV-TQS-02", "OV-TQS-03", "OV-TQS-04",
        }
        negative = {
            "OV-04", "OV-05", "OV-TQ-01", "OV-TQ-02", "OV-TQ-18",
            "OV-TQ-19", "OV-TQ-20",
        }
        success = self._canonical_presentation_ids() - incomplete - negative
        return "\n".join(
            [
                "# 1. Repository, runtime and host",
                " ".join(sorted(success)),
                "# 2. Successful build and recovery checks",
                "verified",
                "# 3. Successful bounded diagnostics",
                "verified",
                "# 4. Successful expected-rejection controls",
                " ".join(sorted(negative)),
                "# 5. Accepted formal runtime measurements",
                "verified",
                "# 6. Tests that did not complete",
                " ".join(sorted(incomplete)),
                "# 7. Quality boundary",
                "No numeric quality score and no winner.",
                "# 8. Final decision and evidence index",
                "E1 E2 E3",
                "",
            ]
        )

    def test_v18_selector_admits_only_the_three_hash_bound_measurements(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            select_presentation_measurements,
        )

        rows = self._three_measured_runtime_outcomes()
        selected = select_presentation_measurements(rows)
        self.assertEqual(
            [row.key for row in selected],
            [
                RuntimeKey("OV-TQ-13", 512),
                RuntimeKey("OV-TQ-14", 512),
                RuntimeKey("OV-TQ-14", 2048),
            ],
        )
        self.assertTrue(all(row.sample_count == 3 for row in selected))
        self.assertTrue(all(row.activation["fallback"] is False for row in selected))

    def test_v18_section_5_contains_three_compact_aggregate_tables(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            render_section_5,
            validate_section_5,
        )

        section = render_section_5(self._three_measured_runtime_outcomes())
        report = validate_section_5(section)
        self.assertEqual(report, {"table_count": 3, "measured_row_count": 3})
        self.assertEqual(section.count("| OV-TQ-13 | 512 |"), 3)
        self.assertEqual(section.count("| OV-TQ-14 | 512 |"), 3)
        self.assertEqual(section.count("| OV-TQ-14 | 2048 |"), 3)
        self.assertIn("| Test ID | Context | Load ms | TTFT ms | Prompt tok/s | TPOT ms | Decode tok/s | Generation ms |", section)
        self.assertIn("| Test ID | Context | Peak WS MiB | Peak private MiB | Available RAM min MiB | KV MiB | Cleanup |", section)
        self.assertIn("| Test ID | Context | CPU mean/median/peak % | GPU mean/median/peak % | CPU samples | GPU samples | Accepted runs | Fallback count | Evidence ref |", section)
        self.assertIn("| E1 |", section)
        self.assertIn("| E2 |", section)
        self.assertIn("| E3 |", section)
        self.assertNotIn("Sample ID", section)
        self.assertNotIn("not-produced-by-", section)

    def test_v18_incomplete_tests_are_six_compact_reason_bullets(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            render_section_6,
        )

        expected_ids = self._canonical_presentation_ids()

        section = render_section_6(self._presentation_runtime_outcomes(), expected_ids)
        bullets = [line for line in section.splitlines() if line.startswith("- ")]
        self.assertEqual(len(bullets), 6)
        self.assertIn("OV-TQ-03, OV-TQ-04", section)
        self.assertIn("RAM safety floor", section)
        self.assertIn("Larger host", section)
        self.assertNotIn("| Test ID |", section)
        self.assertNotRegex(section, r"[0-9a-f]{64}")

    def test_v18_incomplete_tests_refuse_missing_or_unexpected_non_success_rows(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            RuntimeOutcome,
            render_section_6,
        )

        expected_ids = self._canonical_presentation_ids()
        rows = self._presentation_runtime_outcomes()
        section = render_section_6(rows, expected_ids)
        self.assertIn("OV-TQ-13/2048, OV-TQ-13/4096, OV-TQ-13/8192", section)
        self.assertIn("OV-TQ-14/4096, OV-TQ-14/8192", section)

        missing = dict(rows)
        del missing[RuntimeKey("OV-TQ-13", 8192)]
        with self.assertRaisesRegex(ValueError, "non-success runtime key set"):
            render_section_6(missing, expected_ids)

        extra = dict(rows)
        unexpected = RuntimeKey("OV-TQ-99", 4096)
        extra[unexpected] = RuntimeOutcome(
            key=unexpected,
            status="host-resource-blocked",
            configuration_id="OV-TQ-99-4096",
            accepted=False,
            sample_count=0,
            cleanup_process_count=0,
            metric_outcome="not-produced-by-resource-blocked",
            reason="unexpected",
            evidence_path=Path("OV-TQ-99-4096.json"),
            evidence_sha256="f" * 64,
            summary_path=None,
            summary_sha256="f" * 64,
            campaign_identity_sha256="f" * 64,
            runtime_config_sha256="f" * 64,
            metrics={},
            activation={},
            samples={},
        )
        with self.assertRaisesRegex(ValueError, "non-success runtime key set"):
            render_section_6(extra, expected_ids)

    def test_v18_incomplete_tests_require_all_accepted_presentation_measurements(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            render_section_6,
        )

        expected_ids = self._canonical_presentation_ids()
        rows = self._presentation_runtime_outcomes()
        for key in (
            RuntimeKey("OV-TQ-13", 512),
            RuntimeKey("OV-TQ-14", 512),
            RuntimeKey("OV-TQ-14", 2048),
        ):
            missing = dict(rows)
            del missing[key]
            with self.assertRaisesRegex(ValueError, "measured-key set"):
                render_section_6(missing, expected_ids)

    def test_v18_quality_boundary_refuses_numeric_or_winner_claims(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            QualityOutcome,
            RuntimeKey,
            render_section_7,
        )

        quality_rows = {
            RuntimeKey("OV-TQ-13", 512): QualityOutcome(
                key=RuntimeKey("OV-TQ-13", 512),
                status="host-resource-blocked",
                prompt_scores={prompt_id: "not-produced" for prompt_id in ("P1", "P2", "P3", "P4", "P5", "P6")},
                mean_score="not-produced",
                critical_failure_or_cap="RAM safety floor",
                evidence_path=Path("quality-terminal.json"),
                evidence_sha256="a" * 64,
            )
        }

        section = render_section_7(quality_rows)
        self.assertIn("No governed P1-P6 quality campaign completed", section)
        self.assertIn("no numeric quality score", section)
        self.assertIn("no winner", section)
        self.assertNotRegex(section, r"\b[0-9]+(?:\.[0-9]+)?\s*/\s*10\b")

        numeric = QualityOutcome(
            key=RuntimeKey("OV-TQ-13", 512),
            status="complete",
            prompt_scores={prompt_id: 7.0 for prompt_id in ("P1", "P2", "P3", "P4", "P5", "P6")},
            mean_score=7.0,
            critical_failure_or_cap="none",
            evidence_path=Path("quality-score.json"),
            evidence_sha256="b" * 64,
        )
        with self.assertRaisesRegex(ValueError, "numeric quality score"):
            render_section_7({numeric.key: numeric})

        boolean = QualityOutcome(
            key=numeric.key,
            status="complete",
            prompt_scores={prompt_id: True for prompt_id in numeric.prompt_scores},
            mean_score=True,
            critical_failure_or_cap="none",
            evidence_path=Path("quality-boolean.json"),
            evidence_sha256="c" * 64,
        )
        with self.assertRaisesRegex(ValueError, "numeric quality score"):
            render_section_7({boolean.key: boolean})

    def test_v18_decision_has_exact_e1_e3_hash_bound_sources(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            render_section_8,
        )

        release_input_path = REPO_ROOT / "scripts" / "testing" / "examples" / "official-openvino-wb04-reconciliation-input.example.json"
        section = render_section_8(
            self._three_measured_runtime_outcomes(), release_input_path
        )
        self.assertIn("| E1 |", section)
        self.assertIn("| E2 |", section)
        self.assertIn("| E3 |", section)
        self.assertEqual(len(re.findall(r"[0-9a-f]{64}", section)), 4)
        self.assertNotIn("quality-qualified winner", section.casefold())

    def test_v18_presentation_validator_requires_canonical_inventory_and_placement(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            validate_presentation_text,
        )

        expected_ids = self._canonical_presentation_ids()
        valid = self._structured_presentation_text()
        self.assertEqual(
            validate_presentation_text(valid, expected_ids),
            {"controlled_id_count": 60},
        )
        with self.assertRaisesRegex(ValueError, "prohibited presentation claim"):
            validate_presentation_text(valid + "All tests passed.\n", expected_ids)
        with self.assertRaisesRegex(ValueError, "canonical 60-ID inventory"):
            validate_presentation_text(valid, {"OV-01"})
        misplaced = valid.replace("OV-TQ-03", "").replace("E1 E2 E3", "E1 E2 E3 OV-TQ-03")
        with self.assertRaisesRegex(ValueError, "section 6"):
            validate_presentation_text(misplaced, expected_ids)

    def test_measurements_use_composite_runtime_and_sample_keys(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            SampleKey,
            index_measurement_summaries,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(contexts=(512, 2048))
            first = _write_measurement(root, case, 512)
            second = _write_measurement(root, case, 2048, base=100)

            rows = index_measurement_summaries([first, second], [case])

            self.assertEqual(
                set(rows),
                {RuntimeKey("OV-TQ-13", 512), RuntimeKey("OV-TQ-13", 2048)},
            )
            self.assertEqual(
                set(rows[RuntimeKey("OV-TQ-13", 512)].samples),
                {
                    SampleKey("OV-TQ-13", 512, "sample-1"),
                    SampleKey("OV-TQ-13", 512, "sample-2"),
                    SampleKey("OV-TQ-13", 512, "sample-3"),
                },
            )
            self.assertNotEqual(
                rows[RuntimeKey("OV-TQ-13", 512)].configuration_id,
                rows[RuntimeKey("OV-TQ-13", 2048)].configuration_id,
            )

    def test_accepted_measurement_rejects_null_metric_and_wrong_sample_count(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            index_measurement_summaries,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case()
            path = _write_measurement(root, case, 512)
            payload = json.loads(path.read_text(encoding="utf-8"))
            payload["ttft_ms"]["median"] = None
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "ttft_ms"):
                index_measurement_summaries([path], [case])

            path = _write_measurement(root / "again", case, 512)
            payload = json.loads(path.read_text(encoding="utf-8"))
            payload["sample_count"] = 2
            path.write_text(json.dumps(payload), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "exactly three"):
                index_measurement_summaries([path], [case])

    def test_duplicate_composite_summary_requires_explicit_path_selection(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            index_measurement_summaries,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case()
            historical = _write_measurement(root / "historical", case, 512)
            reviewed = _write_measurement(root / "reviewed", case, 512, base=100)

            with self.assertRaisesRegex(ValueError, "duplicate runtime key"):
                index_measurement_summaries([historical, reviewed], [case])

            selected = index_measurement_summaries([reviewed], [case])
            self.assertEqual(len(selected), 1)
            self.assertEqual(
                next(iter(selected.values())).summary_path.resolve(),
                reviewed.resolve(),
            )

    def test_runtime_precision_must_be_proved_by_every_source_attempt(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            index_measurement_summaries,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case()
            path = _write_measurement(root, case, 512)
            summary = json.loads(path.read_text(encoding="utf-8"))
            attempt_path = Path(summary["sources"][1]["path"])
            attempt = json.loads(attempt_path.read_text(encoding="utf-8"))
            attempt["activation"]["activated_key_cache_precision"] = "f16"
            attempt["telemetry_sha256"] = _runtime_json_sha256(
                attempt["activation"]
            )
            attempt_path.write_text(json.dumps(attempt), encoding="utf-8")
            summary["sources"][1]["sha256"] = _sha256(attempt_path)
            path.write_text(json.dumps(summary), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "cache precision"):
                index_measurement_summaries([path], [case])

    def test_runtime_source_requires_canonical_output_validity(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            index_measurement_summaries,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case()
            path = _write_measurement(root, case, 512)
            summary = json.loads(path.read_text(encoding="utf-8"))
            attempt_path = Path(summary["sources"][0]["path"])
            attempt = json.loads(attempt_path.read_text(encoding="utf-8"))
            attempt["worker"]["output_valid"] = False
            attempt_path.write_text(json.dumps(attempt), encoding="utf-8")
            summary["sources"][0]["sha256"] = _sha256(attempt_path)
            path.write_text(json.dumps(summary), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "output valid"):
                index_measurement_summaries([path], [case])

    def test_terminal_records_are_explicit_hash_bound_and_never_execution_passes(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            validate_terminal_record,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case()
            record = _terminal(root, case, 512)
            validated = validate_terminal_record(
                record, case, allowed_statuses={"host-resource-blocked"}
            )
            self.assertEqual(validated.status, "host-resource-blocked")
            self.assertEqual(validated.sample_count, 0)
            with self.assertRaisesRegex(ValueError, "exactly three"):
                validate_terminal_record(record, case)

            for field, value, message in (
                ("status", "passed", "terminal status"),
                ("sample_count", 3, "zero samples"),
                ("metric_outcome", 0, "metric outcome"),
                ("reason", "", "reason"),
                ("evidence_sha256", "0" * 64, "evidence SHA-256"),
            ):
                broken = dict(record)
                broken[field] = value
                with self.assertRaisesRegex(ValueError, message):
                    validate_terminal_record(
                        broken,
                        case,
                        allowed_statuses={"host-resource-blocked"},
                    )

            wrong = dict(record)
            evidence = Path(record["evidence_path"])
            payload = json.loads(evidence.read_text(encoding="utf-8"))
            payload["test_id"] = "OV-TQ-14"
            evidence.write_text(json.dumps(payload), encoding="utf-8")
            wrong["evidence_sha256"] = _sha256(evidence)
            with self.assertRaisesRegex(ValueError, "does not bind"):
                validate_terminal_record(
                    wrong,
                    case,
                    allowed_statuses={"host-resource-blocked"},
                )

    def test_direct_host_resource_terminal_requires_three_governed_low_memory_runs(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RESOURCE_BLOCKED_LITERAL,
            validate_terminal_record,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(
                "OV-03",
                contexts=(4096,),
                phase="baseline",
                k_algorithm="standard",
                v_algorithm="standard",
                k_precision="f16",
                v_precision="f16",
                key_cache_precision="f16",
                value_cache_precision="f16",
                runtime_key_algorithm="STANDARD",
                runtime_value_algorithm="STANDARD",
                norm_correction=False,
                attention_path="stateful_sdpa_standard",
                execution_route="stateful-standard",
            )
            record = _write_governed_host_resource_terminal(
                root, case, 4096
            )

            outcome = validate_terminal_record(record, case)

            self.assertEqual(outcome.status, "host-resource-blocked")
            self.assertEqual(outcome.metric_outcome, RESOURCE_BLOCKED_LITERAL)
            self.assertEqual(outcome.metrics, {})
            self.assertEqual(outcome.sample_count, 0)

            two_attempts = _write_governed_host_resource_terminal(
                root / "two-attempts", case, 4096, attempt_count=2
            )
            with self.assertRaisesRegex(ValueError, "exactly three"):
                validate_terminal_record(two_attempts, case)

    def test_direct_host_resource_terminal_rejects_rehashed_incomplete_attempts(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            validate_terminal_record,
        )

        def rewrite_attempt(record, mutate):
            evidence_path = Path(record["evidence_path"])
            classification = json.loads(
                evidence_path.read_text(encoding="utf-8")
            )
            source = classification["source_evidence"][0]
            attempt_path = Path(source["path"])
            attempt = json.loads(attempt_path.read_text(encoding="utf-8"))
            mutate(attempt, attempt_path)
            attempt_path.write_text(
                json.dumps(attempt, sort_keys=True), encoding="utf-8"
            )
            source["sha256"] = _sha256(attempt_path)
            receipt_path = (
                attempt_path.parent.parent / "sequence-receipt.json"
            )
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["runtime_record_sha256"] = source["sha256"]
            receipt_path.write_text(
                json.dumps(receipt, sort_keys=True), encoding="utf-8"
            )
            for sibling_source in classification["source_evidence"]:
                sibling_path = Path(sibling_source["path"])
                if sibling_path.resolve() == receipt_path.resolve():
                    sibling_source["sha256"] = _sha256(receipt_path)
            evidence_path.write_text(
                json.dumps(classification, sort_keys=True), encoding="utf-8"
            )
            record["evidence_sha256"] = _sha256(evidence_path)

        case = _case(
            "OV-03",
            contexts=(4096,),
            phase="baseline",
            k_algorithm="standard",
            v_algorithm="standard",
            k_precision="f16",
            v_precision="f16",
            key_cache_precision="f16",
            value_cache_precision="f16",
            runtime_key_algorithm="STANDARD",
            runtime_value_algorithm="STANDARD",
            norm_correction=False,
            attention_path="stateful_sdpa_standard",
            execution_route="stateful-standard",
        )
        mutations = (
            (
                "zero-memory-samples",
                lambda attempt, _path: attempt.__setitem__(
                    "memory_sample_count", 0
                ),
            ),
            (
                "wrong-command-spec",
                lambda attempt, path: attempt.__setitem__(
                    "command",
                    [
                        sys.executable,
                        "-m",
                        "scripts.testing.official_openvino.measurement_worker",
                        "--spec",
                        str(path.parents[3] / "unbound-spec.json"),
                    ],
                ),
            ),
        )
        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            for label, mutate in mutations:
                with self.subTest(label=label):
                    record = _write_governed_host_resource_terminal(
                        root / label, case, 4096
                    )
                    rewrite_attempt(record, mutate)
                    with self.assertRaisesRegex(
                        ValueError, "clean fixed-floor stop|spec command"
                    ):
                        validate_terminal_record(record, case)

    def test_direct_host_resource_terminal_binds_exact_context_token_config(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            validate_terminal_record,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(
                "OV-03",
                contexts=(4096,),
                phase="baseline",
                k_algorithm="standard",
                v_algorithm="standard",
                k_precision="f16",
                v_precision="f16",
                key_cache_precision="f16",
                value_cache_precision="f16",
                runtime_key_algorithm="STANDARD",
                runtime_value_algorithm="STANDARD",
                norm_correction=False,
                attention_path="stateful_sdpa_standard",
                execution_route="stateful-standard",
            )
            record = _write_governed_host_resource_terminal(
                root, case, 4096
            )
            evidence_path = Path(record["evidence_path"])
            classification = json.loads(
                evidence_path.read_text(encoding="utf-8")
            )
            attempt_paths = [
                Path(source["path"])
                for source in classification["source_evidence"]
                if Path(source["path"]).name == "attempt.json"
            ]
            campaign_root = attempt_paths[0].parents[4]
            identity_path = campaign_root / "campaign-identity.json"
            identity_record = json.loads(
                identity_path.read_text(encoding="utf-8")
            )
            identity_record["identity"]["config"][
                "expected_input_tokens"
            ] = 1024
            campaign_sha256 = _runtime_json_sha256(
                identity_record["identity"]
            )
            identity_record["campaign_identity_sha256"] = campaign_sha256
            identity_path.write_text(
                json.dumps(identity_record, sort_keys=True),
                encoding="utf-8",
            )
            for attempt_path in attempt_paths:
                attempt_root = attempt_path.parent.parent
                spec_path = attempt_root / "spec.json"
                spec = json.loads(spec_path.read_text(encoding="utf-8"))
                spec["expected_input_tokens"] = 1024
                spec["campaign_identity_sha256"] = campaign_sha256
                spec_path.write_text(
                    json.dumps(spec, sort_keys=True), encoding="utf-8"
                )
                receipt_path = attempt_root / "sequence-receipt.json"
                receipt = json.loads(
                    receipt_path.read_text(encoding="utf-8")
                )
                receipt["campaign_identity_sha256"] = campaign_sha256
                receipt["spec_sha256"] = _runtime_json_sha256(spec)
                receipt["spec_file_sha256"] = _sha256(spec_path)
                receipt_path.write_text(
                    json.dumps(receipt, sort_keys=True), encoding="utf-8"
                )
            for source in classification["source_evidence"]:
                source_path = Path(source["path"])
                source["sha256"] = _sha256(source_path)
            evidence_path.write_text(
                json.dumps(classification, sort_keys=True),
                encoding="utf-8",
            )
            record["evidence_sha256"] = _sha256(evidence_path)

            with self.assertRaisesRegex(
                ValueError, "expected input tokens|context token"
            ):
                validate_terminal_record(record, case)

    def test_direct_host_resource_terminal_rejects_attempt_only_provenance(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            validate_terminal_record,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(
                "OV-03",
                contexts=(4096,),
                phase="baseline",
                k_algorithm="standard",
                v_algorithm="standard",
                k_precision="f16",
                v_precision="f16",
                key_cache_precision="f16",
                value_cache_precision="f16",
                runtime_key_algorithm="STANDARD",
                runtime_value_algorithm="STANDARD",
                norm_correction=False,
                attention_path="stateful_sdpa_standard",
                execution_route="stateful-standard",
            )
            record = _write_governed_host_resource_terminal(
                root, case, 4096
            )
            evidence_path = Path(record["evidence_path"])
            classification = json.loads(
                evidence_path.read_text(encoding="utf-8")
            )
            classification["source_evidence"] = [
                source
                for source in classification["source_evidence"]
                if Path(source["path"]).name == "attempt.json"
            ]
            evidence_path.write_text(
                json.dumps(classification, sort_keys=True),
                encoding="utf-8",
            )
            record["evidence_sha256"] = _sha256(evidence_path)

            with self.assertRaisesRegex(
                ValueError, "spec.*receipt.*campaign|provenance"
            ):
                validate_terminal_record(record, case)

    def test_direct_host_resource_terminal_hash_binds_sibling_spec_bytes(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            validate_terminal_record,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(
                "OV-03",
                contexts=(4096,),
                phase="baseline",
                k_algorithm="standard",
                v_algorithm="standard",
                k_precision="f16",
                v_precision="f16",
                key_cache_precision="f16",
                value_cache_precision="f16",
                runtime_key_algorithm="STANDARD",
                runtime_value_algorithm="STANDARD",
                norm_correction=False,
                attention_path="stateful_sdpa_standard",
                execution_route="stateful-standard",
            )
            record = _write_governed_host_resource_terminal(
                root, case, 4096
            )
            evidence_path = Path(record["evidence_path"])
            classification = json.loads(
                evidence_path.read_text(encoding="utf-8")
            )
            attempt_path = next(
                Path(source["path"])
                for source in classification["source_evidence"]
                if Path(source["path"]).name == "attempt.json"
            )
            spec_path = attempt_path.parent.parent / "spec.json"
            spec = json.loads(spec_path.read_text(encoding="utf-8"))
            spec["provenance_mutation"] = True
            spec_path.write_text(
                json.dumps(spec, sort_keys=True), encoding="utf-8"
            )
            receipt_path = (
                attempt_path.parent.parent / "sequence-receipt.json"
            )
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            receipt["spec_sha256"] = _runtime_json_sha256(spec)
            receipt["spec_file_sha256"] = _sha256(spec_path)
            receipt_path.write_text(
                json.dumps(receipt, sort_keys=True), encoding="utf-8"
            )

            with self.assertRaisesRegex(ValueError, "SHA-256 mismatch"):
                validate_terminal_record(record, case)

    def test_controlled_terminals_require_the_sealed_artifact_spec_inventory(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            validate_terminal_record,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            inventory_path, inventory_roots = (
                _write_controlled_terminal_inventory(root)
            )
            cases = (
                _case(
                    "OV-01",
                    contexts=(1024,),
                    model="diagnostic",
                    weight_precision="dynamic",
                    key_cache_precision="dynamic",
                    value_cache_precision="dynamic",
                ),
                _case(
                    "OV-02",
                    contexts=(2048,),
                    phase="baseline",
                    weight_precision="f16",
                    k_algorithm="standard",
                    v_algorithm="standard",
                    k_precision="f16",
                    v_precision="f16",
                    key_cache_precision="f16",
                    value_cache_precision="f16",
                    runtime_key_algorithm="STANDARD",
                    runtime_value_algorithm="STANDARD",
                    norm_correction=False,
                    attention_path="stateful_sdpa_standard",
                    execution_route="stateful-standard",
                ),
            )
            records = []
            for case, context in zip(cases, (1024, 2048)):
                record = _terminal(
                    root,
                    case,
                    context,
                    status="controlled-terminal-not-launched",
                )
                evidence_path = Path(record["evidence_path"])
                classification = json.loads(
                    evidence_path.read_text(encoding="utf-8")
                )
                classification["source_evidence"] = [
                    {
                        "path": str(inventory_path),
                        "sha256": _sha256(inventory_path),
                    }
                ]
                evidence_path.write_text(
                    json.dumps(classification, sort_keys=True),
                    encoding="utf-8",
                )
                record["evidence_sha256"] = _sha256(evidence_path)
                records.append(record)

            with mock.patch(
                "scripts.testing.finalize_official_openvino_workbook."
                "CONTROLLED_INVENTORY_ROOTS",
                inventory_roots,
            ):
                for record, case in zip(records, cases):
                    outcome = validate_terminal_record(record, case)
                    self.assertEqual(
                        outcome.status,
                        "controlled-terminal-not-launched",
                    )

                unexpected = (
                    inventory_roots[0]
                    / "granite-f16"
                    / "artifact-manifest.json"
                )
                unexpected.parent.mkdir()
                unexpected.write_text(
                    json.dumps(
                        {
                            "schema_version": 1,
                            "status": "validated",
                            "model": {
                                "family": "granite-4.1",
                                "parameter_scale": "3b",
                                "precision": "f16",
                            },
                        }
                    ),
                    encoding="utf-8",
                )
                with self.assertRaisesRegex(
                    ValueError, "does not match sealed roots"
                ):
                    validate_terminal_record(records[1], cases[1])

    def test_device_resource_terminal_requires_exact_corrected_gpu_attempts(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            validate_terminal_record,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(
                "OV-06",
                contexts=(4096,),
                phase="baseline",
                device="gpu",
                k_algorithm="frozen",
                v_algorithm="frozen",
                k_precision="frozen",
                v_precision="frozen",
                key_cache_precision="frozen",
                value_cache_precision="frozen",
                requested_device="GPU",
                runtime_key_algorithm="STANDARD",
                runtime_value_algorithm="STANDARD",
                norm_correction=False,
                attention_path="stateful_sdpa_standard",
                execution_route="device-standard",
            )
            (
                record,
                expected_attempts,
                initial_spec_path,
                corrected_spec_path,
            ) = _write_device_resource_terminal(root, case)
            with mock.patch(
                "scripts.testing.finalize_official_openvino_workbook."
                "OV06_DEVICE_ATTEMPTS",
                expected_attempts,
            ), mock.patch(
                "scripts.testing.finalize_official_openvino_workbook."
                "OV06_INITIAL_WORKER_SPEC_SHA256",
                _sha256(initial_spec_path),
            ), mock.patch(
                "scripts.testing.finalize_official_openvino_workbook."
                "OV06_CORRECTED_WORKER_SPEC_SHA256",
                _sha256(corrected_spec_path),
            ):
                outcome = validate_terminal_record(record, case)
                self.assertEqual(outcome.status, "device-resource-blocked")
                self.assertEqual(outcome.sample_count, 0)
                self.assertEqual(outcome.metrics, {})

                wrong_attempts = expected_attempts[:-1] + (
                    (expected_attempts[-1][0], "0" * 64),
                )
                with mock.patch(
                    "scripts.testing.finalize_official_openvino_workbook."
                    "OV06_DEVICE_ATTEMPTS",
                    wrong_attempts,
                ):
                    with self.assertRaisesRegex(ValueError, "exact corrected"):
                        validate_terminal_record(record, case)

                spec = json.loads(
                    corrected_spec_path.read_text(encoding="utf-8")
                )
                spec["rehashed_but_unapproved"] = True
                corrected_spec_path.write_text(
                    json.dumps(spec, sort_keys=True), encoding="utf-8"
                )
                with self.assertRaisesRegex(
                    ValueError, "worker spec SHA-256"
                ):
                    validate_terminal_record(record, case)

    def test_resource_envelope_classifies_only_explicit_rows(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            index_measurement_summaries,
            validate_resource_envelope_decision,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            accepted_case = _case(
                "OV-TQ-14",
                contexts=(2048, 4096),
                k_algorithm="tbq3",
                v_algorithm="tbq3",
                k_precision="u3",
                v_precision="u3",
                key_cache_precision="u3",
                value_cache_precision="u3",
                runtime_key_algorithm="TBQ3",
                runtime_value_algorithm="TBQ3",
            )
            blocked_case = _case("OV-TQ-13", contexts=(2048,))
            accepted_path = _write_measurement(
                root, accepted_case, 2048, base=2000
            )
            measured = index_measurement_summaries([accepted_path], [accepted_case, blocked_case])
            blocked = _write_governed_host_resource_terminal(
                root / "blocked", blocked_case, 2048
            )
            blocked_evidence = Path(blocked["evidence_path"])
            blocked_classification = json.loads(
                blocked_evidence.read_text(encoding="utf-8")
            )
            blocked_attempts = [
                source
                for source in blocked_classification["source_evidence"]
                if Path(source["path"]).name == "attempt.json"
            ]
            target = _terminal(
                root,
                accepted_case,
                4096,
                status="host-resource-blocked-not-launched",
            )
            target["metric_outcome"] = "not-produced-by-resource-envelope"
            target_evidence = Path(target["evidence_path"])
            target_classification = json.loads(
                target_evidence.read_text(encoding="utf-8")
            )
            target_classification["source_evidence"].append(
                {
                    "path": str(blocked_evidence),
                    "sha256": _sha256(blocked_evidence),
                }
            )
            target_evidence.write_text(
                json.dumps(target_classification, sort_keys=True),
                encoding="utf-8",
            )
            target["evidence_sha256"] = _sha256(target_evidence)
            decision = {
                "schema": "official-openvino-wb04-resource-envelope-decision/v1",
                "status": "accepted",
                "matrix_sha256": "f" * 64,
                "minimum_available_ram_mib": 2048,
                "accepted_anchor": {
                    "test_id": "OV-TQ-14",
                    "context_tokens": 2048,
                    "minimum_available_ram_mib": measured[
                        RuntimeKey("OV-TQ-14", 2048)
                    ].metrics["available_ram_min_mb"]["min"],
                    "measurement_summary_path": str(accepted_path),
                    "measurement_summary_sha256": _sha256(accepted_path),
                },
                "blocked_anchor": {
                    "test_id": "OV-TQ-13",
                    "context_tokens": 2048,
                    "terminal_record": blocked,
                    "attempt_count": 3,
                    "attempts": blocked_attempts,
                },
                "classified_rows": [target],
                "reason": "All named larger U8 CPU rows exceed the bounded host envelope.",
            }
            decision["decision_sha256"] = _canonical_sha256(decision)

            rows = validate_resource_envelope_decision(
                decision, [accepted_case, blocked_case], measured
            )
            self.assertEqual(
                set(rows),
                {
                    RuntimeKey("OV-TQ-13", 2048),
                    RuntimeKey("OV-TQ-14", 4096),
                },
            )

            missing = copy.deepcopy(decision)
            missing["classified_rows"] = []
            missing.pop("decision_sha256")
            missing["decision_sha256"] = _canonical_sha256(missing)
            self.assertEqual(
                validate_resource_envelope_decision(
                    missing, [accepted_case, blocked_case], measured
                ),
                {RuntimeKey("OV-TQ-13", 2048): rows[RuntimeKey("OV-TQ-13", 2048)]},
            )
            self.assertNotIn(RuntimeKey("OV-TQ-14", 4096), measured)

            tampered = copy.deepcopy(decision)
            tampered["reason"] = "different unsigned decision"
            with self.assertRaisesRegex(ValueError, "decision SHA-256"):
                validate_resource_envelope_decision(
                    tampered, [accepted_case, blocked_case], measured
                )

            incomparable = copy.deepcopy(blocked_case)
            incomparable["phase"] = "baseline"
            with self.assertRaisesRegex(
                ValueError, "anchors are not comparable|frozen matrix"
            ):
                validate_resource_envelope_decision(
                    missing, [accepted_case, incomparable], measured
                )

            accepted_higher_demand = copy.deepcopy(accepted_case)
            accepted_higher_demand["key_cache_precision"] = "u8"
            accepted_higher_demand["value_cache_precision"] = "u8"
            with self.assertRaisesRegex(
                ValueError, "accepted anchor demand"
            ):
                validate_resource_envelope_decision(
                    missing,
                    [accepted_higher_demand, blocked_case],
                    measured,
                )

            weak = copy.deepcopy(missing)
            attempt_ref = weak["blocked_anchor"]["attempts"][0]
            attempt_path = Path(attempt_ref["path"])
            attempt = json.loads(
                attempt_path.read_text(encoding="utf-8")
            )
            attempt["exit_code"] = 0
            attempt["sampler_exit_code"] = 17
            attempt["validation_errors"] = []
            attempt["memory_sample_count"] = 0
            attempt["minimum_available_ram_bytes"] = 1024
            attempt_path.write_text(
                json.dumps(attempt, sort_keys=True), encoding="utf-8"
            )
            attempt_sha256 = _sha256(attempt_path)
            attempt_ref["sha256"] = attempt_sha256
            receipt_path = (
                attempt_path.parent.parent / "sequence-receipt.json"
            )
            receipt = json.loads(
                receipt_path.read_text(encoding="utf-8")
            )
            receipt["runtime_record_sha256"] = attempt_sha256
            receipt_path.write_text(
                json.dumps(receipt, sort_keys=True), encoding="utf-8"
            )
            terminal_record = weak["blocked_anchor"]["terminal_record"]
            terminal_path = Path(terminal_record["evidence_path"])
            terminal_classification = json.loads(
                terminal_path.read_text(encoding="utf-8")
            )
            for source in terminal_classification["source_evidence"]:
                source_path = Path(source["path"])
                source["sha256"] = _sha256(source_path)
            terminal_path.write_text(
                json.dumps(terminal_classification, sort_keys=True),
                encoding="utf-8",
            )
            terminal_record["evidence_sha256"] = _sha256(terminal_path)
            weak.pop("decision_sha256")
            weak["decision_sha256"] = _canonical_sha256(weak)
            with self.assertRaisesRegex(
                ValueError, "clean fixed-floor stop"
            ):
                validate_resource_envelope_decision(
                    weak, [accepted_case, blocked_case], measured
                )

    def test_expected_rejection_requires_exact_non_numeric_literal(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            CANONICAL_MATRIX,
            EXPECTED_REJECTION_LITERAL,
            validate_expected_rejection_record,
        )
        from scripts.testing.official_openvino.expected_rejections import (
            generate_expected_rejection_evidence,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(
                "OV-TQ-18",
                contexts=(1024,),
                expected_outcome="expected-rejection",
                execution_route="expected-rejection",
                numeric_generation_metrics_expected=False,
            )
            aggregate = root / "expected-rejection-aggregate.json"
            aggregate_payload = generate_expected_rejection_evidence(
                CANONICAL_MATRIX
            )
            probe = next(
                item
                for item in aggregate_payload["probes"]
                if item["controlled_test_id"] == "OV-TQ-18"
            )
            aggregate.write_text(
                json.dumps(aggregate_payload, sort_keys=True),
                encoding="utf-8",
            )
            evidence = root / "expected-rejection.json"
            evidence.write_text(
                json.dumps(
                    {
                        "schema": (
                            "official-openvino-wb04-expected-rejection-"
                            "classification/v1"
                        ),
                        "test_id": "OV-TQ-18",
                        "context_tokens": 1024,
                        "status": "passed: expected-rejection",
                        "metric_outcome": (
                            "not-produced-by-expected-rejection"
                        ),
                        "generation_not_launched": True,
                        "cleanup_process_count": 0,
                        "source_aggregate_path": str(aggregate),
                        "source_aggregate_sha256": _sha256(aggregate),
                        "probe_sha256": probe["probe_sha256"],
                    }
                ),
                encoding="utf-8",
            )
            record = {
                "test_id": "OV-TQ-18",
                "context_tokens": 1024,
                "accepted": False,
                "status": "passed: expected-rejection",
                "sample_count": 0,
                "cleanup_process_count": 0,
                "metric_outcome": EXPECTED_REJECTION_LITERAL,
                "reason": "GPU TurboQuant property boundary rejected before generation.",
                "generation_not_launched": True,
                "evidence_path": str(evidence),
                "evidence_sha256": _sha256(evidence),
            }
            validated = validate_expected_rejection_record(record, case)
            self.assertEqual(validated.metric_outcome, EXPECTED_REJECTION_LITERAL)

            for invalid in (0, "not measured", "N/A"):
                broken = dict(record)
                broken["metric_outcome"] = invalid
                with self.assertRaisesRegex(ValueError, "not-produced-by-expected-rejection"):
                    validate_expected_rejection_record(broken, case)

            payload = json.loads(evidence.read_text(encoding="utf-8"))
            payload["context_tokens"] = 4096
            evidence.write_text(json.dumps(payload), encoding="utf-8")
            wrong = dict(record)
            wrong["evidence_sha256"] = _sha256(evidence)
            with self.assertRaisesRegex(ValueError, "does not bind"):
                validate_expected_rejection_record(wrong, case)

    def test_quality_scope_comes_from_matrix_not_a_legacy_id_list(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            index_measurement_summaries,
            reconcile_quality_rows,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(
                "OV-NEW-QUALITY",
                contexts=(512,),
                quality_required=False,
            )
            measured = index_measurement_summaries(
                [_write_measurement(root, case, 512)], [case]
            )
            with self.assertRaisesRegex(
                ValueError, "measured runtime requires numeric P1-P6 quality"
            ):
                reconcile_quality_rows([case], measured, {}, {}, {})

            evidence = root / "quality-guard.json"
            raw_guard = root / "quality-guard-raw.json"
            raw_guard.write_text(
                json.dumps(
                    {
                        "test_id": "OV-NEW-QUALITY",
                        "context_tokens": 512,
                        "cleanup_process_count": 0,
                    }
                ),
                encoding="utf-8",
            )
            evidence.write_text(
                json.dumps(
                    {
                        "schema": (
                            "official-openvino-wb04-quality-terminal-"
                            "classification/v1"
                        ),
                        "test_id": "OV-NEW-QUALITY",
                        "context_tokens": 512,
                        "status": "host-resource-blocked",
                        "reason": (
                            "quality worker crossed the fixed 2,048 MiB floor"
                        ),
                        "prompt_count": 0,
                        "score_outcome": "not-produced-by-resource-blocked",
                        "cleanup_process_count": 0,
                        "source_evidence": [
                            {
                                "path": str(raw_guard),
                                "sha256": _sha256(raw_guard),
                            }
                        ],
                    }
                ),
                encoding="utf-8",
            )
            quality_terminal = {
                RuntimeKey("OV-NEW-QUALITY", 512): {
                    "test_id": "OV-NEW-QUALITY",
                    "context_tokens": 512,
                    "status": "host-resource-blocked",
                    "reason": "quality worker crossed the fixed 2,048 MiB floor",
                    "prompt_count": 0,
                    "score_outcome": "not-produced-by-resource-blocked",
                    "evidence_path": str(evidence),
                    "evidence_sha256": _sha256(evidence),
                }
            }
            with self.assertRaisesRegex(
                ValueError, "measured quality terminal status is invalid"
            ):
                reconcile_quality_rows(
                    [case], measured, {}, {}, quality_terminal
                )

    def test_measured_quality_allows_only_governed_resource_terminals(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            index_measurement_summaries,
            reconcile_quality_rows,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            anchor_case = _case(
                "OV-TQ-14",
                contexts=(512,),
                k_algorithm="tbq3",
                v_algorithm="tbq3",
                k_precision="u3",
                v_precision="u3",
                key_cache_precision="u3",
                value_cache_precision="u3",
                runtime_key_algorithm="TBQ3",
                runtime_value_algorithm="TBQ3",
            )
            target_case = _case("OV-TQ-13", contexts=(512,))
            measured = index_measurement_summaries(
                [
                    _write_measurement(root / "anchor", anchor_case, 512),
                    _write_measurement(root / "target", target_case, 512),
                ],
                [anchor_case, target_case],
            )
            anchor_key = RuntimeKey("OV-TQ-14", 512)
            target_key = RuntimeKey("OV-TQ-13", 512)
            direct = _write_direct_quality_resource_terminal(
                root / "quality-anchor", measured[anchor_key]
            )
            envelope = _write_quality_resource_envelope_terminal(
                root / "quality-target",
                measured[target_key],
                direct,
            )

            rows = reconcile_quality_rows(
                [anchor_case, target_case],
                measured,
                {},
                {},
                {anchor_key: direct, target_key: envelope},
            )

            self.assertEqual(
                rows[anchor_key].mean_score,
                "not-produced-by-quality-resource-blocked",
            )
            self.assertEqual(
                rows[target_key].mean_score,
                "not-produced-by-quality-resource-envelope",
            )

            stale_path = root / "stale-quality-resource-classification.json"
            stale_path.write_bytes(
                Path(direct["evidence_path"]).read_bytes()
            )
            stale_direct = copy.deepcopy(direct)
            stale_direct["evidence_path"] = str(stale_path)
            stale_direct["evidence_sha256"] = _sha256(stale_path)
            stale_envelope = _write_quality_resource_envelope_terminal(
                root / "quality-target-stale",
                measured[target_key],
                stale_direct,
            )
            with self.assertRaisesRegex(
                ValueError, "selected quality terminal|envelope anchor"
            ):
                reconcile_quality_rows(
                    [anchor_case, target_case],
                    measured,
                    {},
                    {},
                    {anchor_key: direct, target_key: stale_envelope},
                )

            spec_mutations = (
                (
                    "cache-dir",
                    lambda spec: spec["properties"].__setitem__(
                        "CACHE_DIR", str(root / "unbound-cache")
                    ),
                ),
                (
                    "prompt",
                    lambda spec: spec["prompts"][0].__setitem__(
                        "prompt", "relabelled prompt"
                    ),
                ),
            )
            for label, mutate in spec_mutations:
                with self.subTest(label=label):
                    mutated = _write_direct_quality_resource_terminal(
                        root / f"quality-mutated-{label}",
                        measured[anchor_key],
                    )
                    classification = json.loads(
                        Path(mutated["evidence_path"]).read_text(
                            encoding="utf-8"
                        )
                    )
                    guard_path = Path(
                        classification["source_evidence"][1]["path"]
                    )
                    spec_path = guard_path.parent / "worker-spec.json"
                    spec = json.loads(
                        spec_path.read_text(encoding="utf-8")
                    )
                    mutate(spec)
                    spec_path.write_text(
                        json.dumps(spec, sort_keys=True),
                        encoding="utf-8",
                    )
                    spec_sha256 = _sha256(spec_path)
                    for source in classification["source_evidence"]:
                        if (
                            Path(source["path"]).resolve()
                            == spec_path.resolve()
                        ):
                            source["sha256"] = spec_sha256
                    guard = json.loads(
                        guard_path.read_text(encoding="utf-8")
                    )
                    guard["bound_inputs"][0]["sha256"] = spec_sha256
                    guard_path.write_text(
                        json.dumps(guard, sort_keys=True),
                        encoding="utf-8",
                    )
                    for source in classification["source_evidence"]:
                        if (
                            Path(source["path"]).resolve()
                            == guard_path.resolve()
                        ):
                            source["sha256"] = _sha256(guard_path)
                    classification_path = Path(mutated["evidence_path"])
                    classification_path.write_text(
                        json.dumps(classification, sort_keys=True),
                        encoding="utf-8",
                    )
                    mutated["evidence_sha256"] = _sha256(
                        classification_path
                    )
                    with self.assertRaisesRegex(
                        ValueError, "matrix/runtime binding"
                    ):
                        reconcile_quality_rows(
                            [anchor_case],
                            {anchor_key: measured[anchor_key]},
                            {},
                            {},
                            {anchor_key: mutated},
                        )

            incomplete = copy.deepcopy(direct)
            evidence_path = Path(incomplete["evidence_path"])
            classification = json.loads(
                evidence_path.read_text(encoding="utf-8")
            )
            classification["source_evidence"].pop()
            evidence_path.write_text(
                json.dumps(classification, sort_keys=True),
                encoding="utf-8",
            )
            incomplete["evidence_sha256"] = _sha256(evidence_path)
            with self.assertRaisesRegex(ValueError, "two governed quality"):
                reconcile_quality_rows(
                    [anchor_case],
                    {anchor_key: measured[anchor_key]},
                    {},
                    {},
                    {anchor_key: incomplete},
                )

    def test_quality_resource_blocker_requires_guard_bound_worker_spec(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            index_measurement_summaries,
            reconcile_quality_rows,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(
                "OV-TQ-14",
                contexts=(512,),
                k_algorithm="tbq3",
                v_algorithm="tbq3",
                k_precision="u3",
                v_precision="u3",
                key_cache_precision="u3",
                value_cache_precision="u3",
                runtime_key_algorithm="TBQ3",
                runtime_value_algorithm="TBQ3",
            )
            measured = index_measurement_summaries(
                [_write_measurement(root / "runtime", case, 512)], [case]
            )
            key = RuntimeKey("OV-TQ-14", 512)
            terminal = _write_direct_quality_resource_terminal(
                root / "quality", measured[key]
            )
            evidence_path = Path(terminal["evidence_path"])
            classification = json.loads(
                evidence_path.read_text(encoding="utf-8")
            )
            guard_source = next(
                source
                for source in classification["source_evidence"]
                if Path(source["path"]).name == "guard-evidence.json"
            )
            guard_path = Path(guard_source["path"])
            guard = json.loads(guard_path.read_text(encoding="utf-8"))
            guard.pop("bound_inputs", None)
            guard_path.write_text(
                json.dumps(guard, sort_keys=True), encoding="utf-8"
            )
            guard_source["sha256"] = _sha256(guard_path)
            evidence_path.write_text(
                json.dumps(classification, sort_keys=True),
                encoding="utf-8",
            )
            terminal["evidence_sha256"] = _sha256(evidence_path)

            with self.assertRaisesRegex(
                ValueError, "bound input|worker-spec provenance"
            ):
                reconcile_quality_rows(
                    [case], measured, {}, {}, {key: terminal}
                )

    def test_quality_resource_blocker_hash_binds_exact_worker_spec_bytes(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            index_measurement_summaries,
            reconcile_quality_rows,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case(
                "OV-TQ-14",
                contexts=(512,),
                k_algorithm="tbq3",
                v_algorithm="tbq3",
                k_precision="u3",
                v_precision="u3",
                key_cache_precision="u3",
                value_cache_precision="u3",
                runtime_key_algorithm="TBQ3",
                runtime_value_algorithm="TBQ3",
            )
            measured = index_measurement_summaries(
                [_write_measurement(root / "runtime", case, 512)], [case]
            )
            key = RuntimeKey("OV-TQ-14", 512)
            terminal = _write_direct_quality_resource_terminal(
                root / "quality", measured[key]
            )
            classification = json.loads(
                Path(terminal["evidence_path"]).read_text(encoding="utf-8")
            )
            guard_path = next(
                Path(source["path"])
                for source in classification["source_evidence"]
                if Path(source["path"]).name == "guard-evidence.json"
            )
            spec_path = guard_path.parent / "worker-spec.json"
            spec = json.loads(spec_path.read_text(encoding="utf-8"))
            spec_path.write_text(
                json.dumps(spec, ensure_ascii=False, indent=2) + "\n",
                encoding="utf-8",
            )
            for source in classification["source_evidence"]:
                if Path(source["path"]).resolve() == spec_path.resolve():
                    source["sha256"] = _sha256(spec_path)
            evidence_path = Path(terminal["evidence_path"])
            evidence_path.write_text(
                json.dumps(classification, sort_keys=True),
                encoding="utf-8",
            )
            terminal["evidence_sha256"] = _sha256(evidence_path)

            with self.assertRaisesRegex(
                ValueError, "bound input.*SHA-256|worker-spec provenance"
            ):
                reconcile_quality_rows(
                    [case], measured, {}, {}, {key: terminal}
                )

    def test_numeric_quality_scores_are_bound_to_the_hashed_evidence(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            index_measurement_summaries,
            reconcile_quality_rows,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            case = _case("OV-NEW-QUALITY", contexts=(512,))
            measured = index_measurement_summaries(
                [_write_measurement(root, case, 512)], [case]
            )
            runtime = measured[RuntimeKey("OV-NEW-QUALITY", 512)]
            prompts = {
                prompt_id: {
                    "final_score": float(index),
                    "critical_caps": [],
                    "critical_cap_reasons": [],
                }
                for index, prompt_id in enumerate(
                    ("P1", "P2", "P3", "P4", "P5", "P6"), 1
                )
            }
            blind_label = "cfg-a"
            raw_root = root / "raw-quality"
            capture_root = raw_root / blind_label
            capture_root.mkdir(parents=True)
            (capture_root / "capture-summary.json").write_text(
                json.dumps(
                    {
                        "test_id": case["test_id"],
                        "context_tokens": 512,
                        "campaign_identity_sha256": (
                            runtime.campaign_identity_sha256
                        ),
                        "runtime_summary_sha256": runtime.summary_sha256,
                        "runtime_config_sha256": runtime.runtime_config_sha256,
                    }
                ),
                encoding="utf-8",
            )
            rendered_root = root / "rendered-prompts"
            rendered_root.mkdir()
            artifact_payloads = {
                "adjudication": {
                    "configurations": [
                        {
                            "test_id": case["test_id"],
                            "blind_label": blind_label,
                            "status": "complete",
                            "prompt_count": 6,
                            "mean_score": 3.5,
                            "prompts": prompts,
                        }
                    ]
                },
                "scoring_input": {"controlled": "scoring-input"},
                "score_sheet": {"controlled": "score-sheet"},
                "blind_map": {blind_label: case["test_id"]},
                "rubric": {"controlled": "rubric"},
                "prompt_set": {"controlled": "prompt-set"},
            }
            artifact_paths = {}
            for name, payload in artifact_payloads.items():
                artifact_path = root / f"{name}.json"
                artifact_path.write_text(
                    json.dumps(payload, sort_keys=True), encoding="utf-8"
                )
                artifact_paths[name] = artifact_path
            evidence = root / "quality-score-evidence.json"
            evidence_payload = {
                "schema": (
                    "official-openvino-wb04-quality-score-evidence/v1"
                ),
                "test_id": case["test_id"],
                "context_tokens": 512,
                "blind_label": blind_label,
                "raw_root_path": str(raw_root),
                "rendered_root_path": str(rendered_root),
                "campaign_identity_sha256": runtime.campaign_identity_sha256,
                "runtime_summary_path": str(runtime.summary_path),
                "runtime_summary_sha256": runtime.summary_sha256,
                "runtime_config_sha256": runtime.runtime_config_sha256,
            }
            for name, artifact_path in artifact_paths.items():
                evidence_payload[f"{name}_path"] = str(artifact_path)
                evidence_payload[f"{name}_sha256"] = _sha256(artifact_path)
            evidence.write_text(
                json.dumps(evidence_payload, sort_keys=True),
                encoding="utf-8",
            )
            record = {
                "test_id": case["test_id"],
                "context_tokens": 512,
                "status": "complete",
                "prompt_count": 6,
                "campaign_identity_sha256": runtime.campaign_identity_sha256,
                "runtime_summary_sha256": runtime.summary_sha256,
                "runtime_config_sha256": runtime.runtime_config_sha256,
                "prompts": prompts,
                "mean_score": 3.5,
                "critical_failure_or_cap": "none",
                "evidence_path": str(evidence),
                "evidence_sha256": _sha256(evidence),
            }
            key = RuntimeKey(case["test_id"], 512)
            with (
                mock.patch(
                    "scripts.testing.finalize_official_openvino_workbook."
                    "build_blind_scoring_input",
                    return_value=artifact_payloads["scoring_input"],
                ),
                mock.patch(
                    "scripts.testing.finalize_official_openvino_workbook."
                    "adjudicate_quality",
                    return_value=artifact_payloads["adjudication"],
                ),
            ):
                rows = reconcile_quality_rows(
                    [case], measured, {}, {key: record}, {}
                )
                self.assertEqual(
                    rows[RuntimeKey(case["test_id"], 512)].mean_score,
                    3.5,
                )

                fabricated = copy.deepcopy(record)
                fabricated["prompts"]["P1"]["final_score"] = 7.0
                fabricated["mean_score"] = 4.5
                with self.assertRaisesRegex(ValueError, "does not bind"):
                    reconcile_quality_rows(
                        [case], measured, {}, {key: fabricated}, {}
                    )

    def test_static_section_body_is_hash_and_content_bound(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            validate_static_section_body,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            source = root / "section-source.json"
            source.write_text(json.dumps({"accepted": True}), encoding="utf-8")
            body = "| Field | Value |\n| --- | --- |\n| Result | passed |"
            evidence = root / "section-evidence.json"
            evidence.write_text(
                json.dumps(
                    {
                        "schema": (
                            "official-openvino-wb04-static-section-evidence/v1"
                        ),
                        "section_number": 1,
                        "body": body,
                        "source_evidence": [
                            {"path": str(source), "sha256": _sha256(source)}
                        ],
                    },
                    sort_keys=True,
                ),
                encoding="utf-8",
            )
            record = {
                "body": body,
                "evidence": {
                    "path": str(evidence),
                    "sha256": _sha256(evidence),
                },
            }
            self.assertEqual(validate_static_section_body(1, record), body)
            changed = copy.deepcopy(record)
            changed["body"] = body.replace("passed", "failed")
            with self.assertRaisesRegex(ValueError, "does not bind body content"):
                validate_static_section_body(1, changed)

    def test_final_section_replacement_uses_end_of_file_boundary(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            _replace_section_body,
        )

        template = "# 14. Failure log\n\nold\n\n# 15. Decision\n\nstale\n"
        rendered = _replace_section_body(template, 15, "released")
        self.assertEqual(
            rendered,
            "# 14. Failure log\n\nold\n\n# 15. Decision\n\nreleased\n",
        )

    def test_section_11_is_replaced_atomically_with_six_tables(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            RuntimeKey,
            index_measurement_summaries,
            render_section_11,
            replace_section_11,
            validate_section_11,
            validate_terminal_record,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            measured_case = _case("OV-TQ-13", contexts=(512,))
            terminal_case = _case("OV-TQ-14", contexts=(2048,))
            measured = index_measurement_summaries(
                [_write_measurement(root, measured_case, 512)],
                [measured_case, terminal_case],
            )
            terminal = validate_terminal_record(
                _terminal(root, terminal_case, 2048),
                terminal_case,
                allowed_statuses={"host-resource-blocked"},
            )
            rows = dict(measured)
            rows[RuntimeKey("OV-TQ-14", 2048)] = terminal

            section = render_section_11(rows)
            report = validate_section_11(section)
            self.assertEqual(report["table_count"], 6)
            self.assertEqual(report["aggregate_row_count"], 6)
            self.assertEqual(report["sample_row_count"], 9)
            self.assertNotIn("N/A", section)
            self.assertNotIn("TERMINAL-R001", section)
            self.assertIn("host-resource-blocked", section)
            self.assertIn("CPU mean %", section)
            self.assertIn("GPU mean %", section)

            template = (
                "# 10. Before\n\nold\n\n"
                "# 11. Formal performance and memory results\n\n"
                "| stale | table |\n| --- | --- |\n| old | value |\n\n"
                "# 12. Quality evaluation by format\n\n"
                "| keep | this |\n"
            )
            result = replace_section_11(template, section)
            self.assertNotIn("| stale | table |", result)
            self.assertEqual(result.count("# 11. Formal performance and memory results"), 1)
            self.assertEqual(result.count("# 12. Quality evaluation by format"), 1)
            self.assertIn("| keep | this |", result)
            validate_section_11(result.split("# 12.", 1)[0])

    def test_final_write_is_fail_closed(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            write_validated_workbook,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            destination = root / "workbook.md"
            destination.write_text("ORIGINAL\n", encoding="utf-8")
            bad = (
                "# 11. Formal performance and memory results\n\n"
                "| Field | Value |\n| --- | --- |\n| x | N/A |\n\n"
                "# 12. Quality evaluation by format\n"
            )
            with self.assertRaises(ValueError):
                write_validated_workbook(
                    destination,
                    bad,
                    expected_ids={"OV-TQ-13"},
                )
            self.assertEqual(destination.read_text(encoding="utf-8"), "ORIGINAL\n")

    def test_full_canonical_manifest_refuses_incomplete_release_without_writing(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            CANONICAL_MATRIX,
            CANONICAL_MATRIX_SHA256,
            finalize_release,
        )

        with tempfile.TemporaryDirectory(dir=REPO_ROOT) as directory:
            root = Path(directory)
            release_input = root / "reconciliation-input.json"
            release_input.write_text(
                json.dumps(
                    {
                        "schema": "official-openvino-wb04-release-input/v1",
                        "campaign_date": "2026-07-30",
                        "workbook_version": "1.7",
                        "revision_id": "WR-035",
                        "matrix_path": str(CANONICAL_MATRIX),
                        "matrix_sha256": CANONICAL_MATRIX_SHA256,
                        "selected_measurement_summaries": [],
                        "terminal_records": [],
                        "resource_envelope_decisions": [],
                        "expected_rejection_records": [],
                        "quality_records": [],
                        "quality_terminal_records": [],
                        "static_section_bodies": {},
                    },
                    sort_keys=True,
                ),
                encoding="utf-8",
            )
            destination = root / "workbook.md"
            destination.write_text("ORIGINAL\n", encoding="utf-8")
            with self.assertRaisesRegex(
                ValueError, "requires exactly one explicit runtime outcome"
            ):
                finalize_release(
                    release_input_path=release_input,
                    destination=destination,
                    check_only=False,
                )
            self.assertEqual(
                destination.read_text(encoding="utf-8"),
                "ORIGINAL\n",
            )

    def test_release_identity_is_strictly_controlled_and_rendered(self):
        from scripts.testing.finalize_official_openvino_workbook import (
            apply_release_identity,
        )

        template = (
            "# 04 Official OpenVINO Controlled Retest Workbook v1.6\n\n"
            "Controlled retest revision 1.6. Historical results remain legacy "
            "evidence and are not copied into active result cells.\n"
        )
        rendered = apply_release_identity(
            template,
            workbook_version="1.7",
            revision_id="WR-035",
        )
        self.assertIn(
            "# 04 Official OpenVINO Controlled Retest Workbook v1.7",
            rendered,
        )
        self.assertIn("Controlled retest revision 1.7 (WR-035).", rendered)
        self.assertNotIn("v1.6", rendered)
        self.assertNotIn("revision 1.6", rendered)

        with self.assertRaisesRegex(ValueError, "release identity"):
            apply_release_identity(
                template,
                workbook_version="1.8",
                revision_id="WR-036",
            )

    def test_direct_cli_help_is_available(self):
        root = Path(__file__).resolve().parents[3]
        script = root / "scripts/testing/finalize_official_openvino_workbook.py"
        result = subprocess.run(
            [sys.executable, str(script), "--help"],
            capture_output=True,
            text=True,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("--release-input", result.stdout)
        self.assertIn("official-openvino-wb04-", result.stdout)
        self.assertIn("reconciliation-", result.stdout)
        self.assertIn("input.example.json", result.stdout)

    def test_release_input_example_has_the_exact_top_level_contract(self):
        root = Path(__file__).resolve().parents[3]
        example_path = (
            root
            / "scripts"
            / "testing"
            / "examples"
            / "official-openvino-wb04-reconciliation-input.example.json"
        )
        example = json.loads(example_path.read_text(encoding="utf-8"))

        self.assertEqual(
            set(example),
            {
                "schema",
                "campaign_date",
                "workbook_version",
                "revision_id",
                "matrix_path",
                "matrix_sha256",
                "selected_measurement_summaries",
                "terminal_records",
                "resource_envelope_decisions",
                "expected_rejection_records",
                "quality_records",
                "quality_terminal_records",
                "static_section_bodies",
            },
        )
        self.assertEqual(
            example["schema"], "official-openvino-wb04-release-input/v1"
        )
        self.assertEqual(example["campaign_date"], "2026-07-30")
        self.assertEqual(example["workbook_version"], "1.7")
        self.assertEqual(example["revision_id"], "WR-035")
        self.assertEqual(
            set(example["static_section_bodies"]),
            {str(number) for number in range(1, 11)} | {"13", "14", "15"},
        )
        for section in example["static_section_bodies"].values():
            self.assertEqual(set(section), {"body", "evidence"})
            self.assertTrue(section["body"].strip())
            self.assertTrue(section["evidence"])


if __name__ == "__main__":
    unittest.main()
