import hashlib
import json
import statistics
from pathlib import Path

import pytest

from scripts.testing.official_openvino.adaptive_metrics import (
    build_adaptive_runtime_sample,
    summarize_adaptive_runtime_samples,
)


MIB = 1024**2


def _binary_mib_receipt() -> dict:
    sources = {
        "peak_working_set_mb": (
            "owned_process_memory.working_set_bytes",
            "owned-process memory sampler",
        ),
        "peak_private_mb": (
            "owned_process_memory.private_bytes",
            "owned-process memory sampler",
        ),
        "available_ram_min_mb": (
            "available_ram_bytes.minimum",
            "available-RAM sampler",
        ),
        "kv_mb": (
            "activation.actual_bytes",
            "OpenVINO activation telemetry",
        ),
        "gpu_memory_peak_mb": (
            "GPUProcessMemory.DedicatedUsage+SharedUsage",
            "Windows GPUProcessMemory sampler",
        ),
    }
    return {
        "schema": "official-openvino-memory-unit-receipt/v2",
        "binary_mib_bytes": MIB,
        "projections": {
            field: {
                "collector": collector,
                "source_field": source,
                "source_unit": "bytes",
                "conversion": "divide-by-binary-mib",
                "conversion_divisor_bytes": MIB,
                "projected_unit": "MiB",
            }
            for field, (source, collector) in sources.items()
        },
    }


def _utilization(values: list[float]) -> dict:
    return {
        "values": values,
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "peak": max(values),
        "count": len(values),
        "query_succeeded": True,
    }


def _activation() -> dict:
    return {
        "status": "activated",
        "requested_key_algorithm": "TBQ4",
        "requested_value_algorithm": "TBQ4",
        "activated_key_algorithm": "TBQ4",
        "activated_value_algorithm": "TBQ4",
        "requested_key_cache_precision": "u4",
        "requested_value_cache_precision": "u4",
        "activated_key_cache_precision": "u4",
        "activated_value_cache_precision": "u4",
        "observed_key_state_precision": "u8+f32+i32",
        "observed_value_state_precision": "u8+f32+i32",
        "norm_correction": True,
        "attention_path": "stateful_sdpa_reference_codec",
        "device": "CPU",
        "actual_device": "CPU",
        "fallback": False,
        "expected_bytes": 100 * MIB,
        "actual_bytes": 100 * MIB,
        "expected_persistent_standard_bytes": 0,
        "actual_persistent_standard_bytes": 0,
        "expected_persistent_payload_bytes": 96 * MIB,
        "actual_persistent_payload_bytes": 96 * MIB,
        "expected_persistent_norm_bytes": 2 * MIB,
        "actual_persistent_norm_bytes": 2 * MIB,
        "expected_persistent_metadata_bytes": 2 * MIB,
        "actual_persistent_metadata_bytes": 2 * MIB,
        "decoded_scratch_bytes": 8 * MIB,
        "full_precision_equivalent_bytes": 800 * MIB,
        "operation_type": "TurboQuantStateUpdateDecode",
        "operation_count": 80,
        "matched_state_count": 80,
        "transformed_model_hash": "model-transform",
        "runtime_layer_type": "Reference",
        "build_commit": "a" * 40,
        "model_hash": "model",
    }


def _command_hash(command: list[str]) -> str:
    return hashlib.sha256(
        json.dumps(
            command,
            sort_keys=True,
            separators=(",", ":"),
            ensure_ascii=True,
            allow_nan=False,
        ).encode("utf-8")
    ).hexdigest()


def complete_record(ordinal: int = 0) -> dict:
    command = ["python", "-m", "worker", "--spec", "sample.json"]
    return {
        "valid": True,
        "role": f"sample-{ordinal + 1}",
        "exit_code": 0,
        "fallback_count": 0,
        "residual_owned_process_count": 0,
        "cleanup_process_count": 0,
        "gpu_sampler_supported": True,
        "memory_unit_receipt": _binary_mib_receipt(),
        "identity_hashes": {
            "artifact_manifest_sha256": "1" * 64,
            "prompt_sha256": "2" * 64,
            "matrix_sha256": "3" * 64,
            "build_provenance_sha256": "4" * 64,
            "command_sha256": _command_hash(command),
            "evidence_sha256": "6" * 64,
        },
        "peak_working_set_bytes": (2900 + 100 * ordinal) * MIB,
        "command": command,
        "peak_private_bytes": (1900 + 100 * ordinal) * MIB,
        "available_ram_bytes": {
            "before": 3400 * MIB,
            "minimum": (2400 - 100 * ordinal) * MIB,
            "after": 3000 * MIB,
        },
        "gpu_dedicated_memory_peak_mb": 0.0,
        "gpu_shared_memory_peak_mb": 0.0,
        "gpu_memory_peak_mb": 0.0,
        "gpu_memory_peak_bytes": 0,
        "cpu_percent": _utilization([10 + ordinal, 20 + ordinal, 30 + ordinal]),
        "gpu_percent": _utilization([0.0, 0.0, 0.0]),
        "gpu_engine_count": _utilization([0.0, 0.0, 0.0]),
        "worker": {
            "load_ms": 10.0 + ordinal,
            "ttft_ms": 10.0 + 10 * ordinal,
            "prompt_tps": 20.0 + ordinal,
            "tpot_ms": 30.0 + ordinal,
            "decode_tps": 40.0 + ordinal,
            "generation_duration_ms": 50.0 + ordinal,
            "num_input_tokens": 512,
            "num_generated_tokens": 4,
            "output_valid": True,
        },
        "activation": _activation(),
        "output_sha256": "7" * 64,
        "telemetry_sha256": "8" * 64,
    }


def _sample(record: dict, tmp_path: Path) -> dict:
    source = tmp_path / f"{record['role']}.json"
    source.write_text("{}", encoding="utf-8")
    return build_adaptive_runtime_sample(record, source)


def three_complete_samples(tmp_path: Path) -> list[dict]:
    return [_sample(complete_record(index), tmp_path) for index in range(3)]


def test_adaptive_sample_retains_tokens_exit_cleanup_and_identity_hashes(tmp_path: Path) -> None:
    sample = _sample(complete_record(), tmp_path)

    assert sample["num_input_tokens"] == 512
    assert sample["num_generated_tokens"] == 4
    assert sample["exit_code"] == 0
    assert sample["fallback_count"] == 0
    assert sample["residual_owned_process_count"] == 0
    assert set(sample["identity_hashes"]) == {
        "artifact_manifest_sha256",
        "prompt_sha256",
        "matrix_sha256",
        "build_provenance_sha256",
        "command_sha256",
        "evidence_sha256",
    }


def test_adaptive_summary_reports_all_required_statistics(tmp_path: Path) -> None:
    result = summarize_adaptive_runtime_samples(three_complete_samples(tmp_path))

    assert result["timing"]["ttft_ms"]["mean"] == pytest.approx(20.0)
    assert result["timing"]["ttft_ms"]["median"] == pytest.approx(20.0)
    assert result["memory"]["peak_working_set_mib"]["median"] == pytest.approx(3000.0)
    assert result["memory"]["peak_working_set_mib"]["worst_max"] == pytest.approx(3100.0)
    assert result["memory"]["available_ram_mib"]["global_min"] == pytest.approx(2200.0)
    assert result["utilisation"]["cpu_percent"]["count"] == 9
    assert result["utilisation"]["gpu_percent"]["count"] == 9
    assert result["utilisation"]["gpu_percent"]["mean"] == pytest.approx(0.0)


@pytest.mark.parametrize(
    ("field", "value"),
    [
        ("num_generated_tokens", 3),
        ("fallback_count", 1),
        ("exit_code", 1),
        ("residual_owned_process_count", 1),
    ],
)
def test_adaptive_summary_rejects_invalid_formal_samples(
    tmp_path: Path,
    field: str,
    value: int,
) -> None:
    samples = three_complete_samples(tmp_path)
    samples[1][field] = value

    with pytest.raises(ValueError, match=field):
        summarize_adaptive_runtime_samples(samples)


def test_adaptive_sample_rejects_legacy_label_only_memory_receipt(
    tmp_path: Path,
) -> None:
    record = complete_record()
    record["memory_unit_receipt"] = {
        "peak_working_set_mb": "MiB",
        "peak_private_mb": "MiB",
        "available_ram_min_mb": "MiB",
        "kv_mb": "MiB",
        "gpu_memory_peak_mb": "MiB",
    }

    with pytest.raises(ValueError, match="provenance"):
        _sample(record, tmp_path)


@pytest.mark.parametrize(
    "mutate",
    [
        lambda receipt: receipt.update(binary_mib_bytes=1_000_000),
        lambda receipt: receipt["projections"]["kv_mb"].update(
            conversion_divisor_bytes=1_000_000
        ),
        lambda receipt: receipt["projections"]["gpu_memory_peak_mb"].update(
            source_field="forged.decimal_mb"
        ),
    ],
)
def test_adaptive_sample_rejects_forged_or_decimal_mib_provenance(
    tmp_path: Path,
    mutate,
) -> None:
    record = complete_record()
    mutate(record["memory_unit_receipt"])

    with pytest.raises(ValueError, match="(binary MiB|provenance)"):
        _sample(record, tmp_path)


def test_adaptive_sample_rejects_command_hash_that_does_not_match_payload(
    tmp_path: Path,
) -> None:
    record = complete_record()
    record["command"] = ["python", "-m", "worker", "--spec", "sample.json"]
    record["identity_hashes"]["command_sha256"] = "0" * 64

    with pytest.raises(ValueError, match="command_sha256"):
        _sample(record, tmp_path)


def test_adaptive_summary_rejects_zero_gpu_without_supported_sampler(
    tmp_path: Path,
) -> None:
    samples = three_complete_samples(tmp_path)
    samples[1]["gpu_sampler_supported"] = False

    with pytest.raises(ValueError, match="zero GPU utilization"):
        summarize_adaptive_runtime_samples(samples)


@pytest.mark.parametrize(
    "mutate",
    [
        lambda record: record.pop("gpu_memory_peak_bytes"),
        lambda record: record.update(gpu_memory_peak_mb=2.5),
        lambda record: record.update(gpu_memory_peak_bytes=2_000_000),
    ],
)
def test_adaptive_sample_rejects_unproven_or_mismatched_gpu_mib(
    tmp_path: Path,
    mutate,
) -> None:
    record = complete_record()
    record.update(gpu_memory_peak_mb=2.0, gpu_memory_peak_bytes=2 * MIB)
    mutate(record)

    with pytest.raises(ValueError, match="gpu_memory_peak_mb"):
        _sample(record, tmp_path)


def test_adaptive_sample_accepts_gpu_mib_rounded_by_sampler_csv(tmp_path: Path) -> None:
    record = complete_record()
    gpu_memory_bytes = (2 * MIB) + 1
    record.update(
        gpu_memory_peak_mb=round(gpu_memory_bytes / MIB, 6),
        gpu_memory_peak_bytes=gpu_memory_bytes,
    )

    assert _sample(record, tmp_path)["gpu_memory_peak_mb"] == pytest.approx(
        gpu_memory_bytes / MIB
    )


def test_adaptive_summary_rejects_supported_zero_gpu_without_observations(
    tmp_path: Path,
) -> None:
    samples = three_complete_samples(tmp_path)
    samples[1]["gpu_percent"] = {
        "values": [],
        "count": 0,
        "query_succeeded": True,
    }

    with pytest.raises(ValueError, match="gpu_percent sampler observations"):
        summarize_adaptive_runtime_samples(samples)
