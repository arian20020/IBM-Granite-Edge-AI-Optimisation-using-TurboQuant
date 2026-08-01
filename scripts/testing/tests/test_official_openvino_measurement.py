import csv
import json
import os
import shutil
import statistics
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from scripts.testing.official_openvino import runtime_measurement
from scripts.testing.official_openvino.runtime_measurement import (
    RESULT_MARKER,
    build_runtime_property_spec,
    execute_attempt_sequence,
    parse_cpu_samples,
    parse_gpu_samples,
    parse_worker_output,
)
from scripts.testing.official_openvino.runtime_process import (
    measurement_sample,
    run_governed_process,
)
from scripts.testing.official_openvino.measurement_worker import (
    extract_performance_metrics,
    generate_decoded_result,
    materialize_openvino_properties,
)


def write_csv(path: Path, rows: list[dict]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def utilization_summary(values: list[float]) -> dict:
    return {
        "values": values,
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "peak": max(values),
        "count": len(values),
        "query_succeeded": True,
    }


def activation_telemetry(**overrides) -> dict:
    value = {
        "status": "activated",
        "requested_key_algorithm": "TBQ4",
        "requested_value_algorithm": "TBQ3",
        "activated_key_algorithm": "TBQ4",
        "activated_value_algorithm": "TBQ3",
        "requested_key_cache_precision": "u4",
        "requested_value_cache_precision": "u3",
        "activated_key_cache_precision": "u4",
        "activated_value_cache_precision": "u3",
        "observed_key_state_precision": "u8+f32+i32",
        "observed_value_state_precision": "u8+f32+i32",
        "norm_correction": True,
        "expected_bytes": 100,
        "actual_bytes": 100,
        "matched_state_count": 2,
        "expected_persistent_standard_bytes": 0,
        "actual_persistent_standard_bytes": 0,
        "expected_persistent_payload_bytes": 80,
        "actual_persistent_payload_bytes": 80,
        "expected_persistent_norm_bytes": 10,
        "actual_persistent_norm_bytes": 10,
        "expected_persistent_metadata_bytes": 10,
        "actual_persistent_metadata_bytes": 10,
        "decoded_scratch_bytes": 400,
        "full_precision_equivalent_bytes": 400,
        "operation_type": "TurboQuantStateUpdateDecode",
        "operation_count": 2,
        "transformed_model_hash": "0123456789abcdef",
        "attention_path": "stateful_sdpa_reference_codec",
        "device": "CPU",
        "actual_device": "CPU",
        "runtime_layer_type": "Reference",
        "fallback": False,
        "build_commit": "a" * 40,
        "model_hash": "fedcba9876543210",
    }
    value.update(overrides)
    return value


def worker_result() -> dict:
    return {
        "schema": "official-openvino-wb04-worker/v1",
        "output": "OK",
        "output_valid": True,
        "load_ms": 10.0,
        "ttft_ms": 2.0,
        "prompt_tps": 5.0,
        "tpot_ms": 4.0,
        "decode_tps": 250.0,
        "generation_duration_ms": 8.0,
    }


def standard_cpu_telemetry(**overrides) -> dict:
    value = activation_telemetry(
        status="not_requested",
        requested_key_algorithm="STANDARD",
        requested_value_algorithm="STANDARD",
        activated_key_algorithm="STANDARD",
        activated_value_algorithm="STANDARD",
        requested_key_cache_precision="u8",
        requested_value_cache_precision="u8",
        activated_key_cache_precision="u8",
        activated_value_cache_precision="u8",
        observed_key_state_precision="u8",
        observed_value_state_precision="u8",
        norm_correction=False,
        expected_bytes=128,
        actual_bytes=128,
        matched_state_count=0,
        expected_persistent_standard_bytes=128,
        actual_persistent_standard_bytes=128,
        expected_persistent_payload_bytes=0,
        actual_persistent_payload_bytes=0,
        expected_persistent_norm_bytes=0,
        actual_persistent_norm_bytes=0,
        expected_persistent_metadata_bytes=0,
        actual_persistent_metadata_bytes=0,
        decoded_scratch_bytes=0,
        full_precision_equivalent_bytes=128,
        operation_type="not_requested",
        operation_count=0,
        transformed_model_hash="not_requested",
        attention_path="stateful_sdpa_standard",
        runtime_layer_type="not_requested",
    )
    value.update(overrides)
    return value


def governed_record(activation: dict) -> dict:
    return {
        "valid": True,
        "role": "sample-1",
        "worker": worker_result(),
        "activation": activation,
        "peak_working_set_bytes": 1024**3,
        "peak_private_bytes": 512 * 1024**2,
        "available_ram_bytes": {
            "before": 4 * 1024**3,
            "minimum": 3 * 1024**3,
            "after": 4 * 1024**3,
        },
        "gpu_dedicated_memory_peak_mb": 1.0,
        "gpu_shared_memory_peak_mb": 2.0,
        "gpu_memory_peak_mb": 2.5,
        "cpu_percent": utilization_summary([10, 20]),
        "gpu_percent": utilization_summary([1, 2]),
        "gpu_engine_count": utilization_summary([1, 1]),
        "output_sha256": "c" * 64,
        "telemetry_sha256": "d" * 64,
        "cleanup_process_count": 0,
    }


class OfficialOpenVINORuntimeMeasurementTests(unittest.TestCase):
    def test_worker_uses_one_item_batch_to_preserve_decoded_perf_metrics(self):
        class Result:
            texts = ["measured output"]
            perf_metrics = object()

        class Pipeline:
            def __init__(self):
                self.inputs = None
                self.config = None
                self.streamer = None

            def generate(self, inputs, config, streamer=None):
                self.inputs = inputs
                self.config = config
                self.streamer = streamer
                streamer("measured")
                return Result()

        pipeline = Pipeline()
        chunks = []
        result, output = generate_decoded_result(
            pipeline,
            "prompt",
            object(),
            lambda chunk: chunks.append(chunk) or False,
        )

        self.assertIs(result.perf_metrics, Result.perf_metrics)
        self.assertEqual(output, "measured output")
        self.assertEqual(pipeline.inputs, ["prompt"])
        self.assertEqual(chunks, ["measured"])

    def test_worker_materializes_only_precision_property_values(self):
        class Type:
            f16 = object()
            u8 = object()
            u4 = object()

        class FakeOpenVINO:
            pass

        FakeOpenVINO.Type = Type
        raw = {
            "KEY_CACHE_PRECISION": "u8",
            "VALUE_CACHE_PRECISION": "u4",
            "PERFORMANCE_HINT": "LATENCY",
            "NUM_STREAMS": 1,
        }
        converted = materialize_openvino_properties(raw, FakeOpenVINO)
        self.assertIs(converted["KEY_CACHE_PRECISION"], Type.u8)
        self.assertIs(converted["VALUE_CACHE_PRECISION"], Type.u4)
        self.assertEqual(converted["PERFORMANCE_HINT"], "LATENCY")
        self.assertEqual(converted["NUM_STREAMS"], 1)

    def test_worker_extracts_source_backed_ttft_prompt_and_decode_metrics(self):
        class Pair:
            def __init__(self, mean):
                self.mean = mean
                self.std = 0

        class Raw:
            # OpenVINO exposes raw token inference durations in microseconds.
            token_infer_durations = [200_000.0, 20_000.0, 30_000.0]

        class Metrics:
            raw_metrics = Raw()

            def get_load_time(self):
                return 100.0

            def get_num_input_tokens(self):
                return 20

            def get_num_generated_tokens(self):
                return 3

            def get_ttft(self):
                return Pair(205.0)

            def get_tpot(self):
                return Pair(25.0)

            def get_throughput(self):
                return Pair(12.0)

            def get_generate_duration(self):
                return Pair(260.0)

        result = extract_performance_metrics(
            Metrics(), request_ttft_ms=210.0, wall_generation_ms=270.0
        )
        self.assertEqual(result["ttft_ms"], 210.0)
        self.assertEqual(result["perf_ttft_ms"], 205.0)
        self.assertEqual(result["prompt_tps"], 100.0)
        self.assertEqual(result["first_token_inference_ms"], 200.0)
        self.assertEqual(result["tpot_ms"], 25.0)
        self.assertEqual(result["decode_tps"], 40.0)
        self.assertEqual(result["generation_duration_ms"], 270.0)

    def test_runtime_property_spec_keeps_weight_cache_and_algorithm_separate(self):
        standard = build_runtime_property_spec(
            device="cpu",
            key_algorithm="STANDARD",
            value_algorithm="STANDARD",
            key_cache_precision="u8",
            value_cache_precision="u4",
            norm_correction=False,
            cache_dir="cache",
        )
        self.assertEqual(standard["device"], "CPU")
        self.assertEqual(standard["properties"]["KEY_CACHE_PRECISION"], "u8")
        self.assertEqual(standard["properties"]["VALUE_CACHE_PRECISION"], "u4")
        self.assertIs(type(standard["properties"]["NUM_STREAMS"]), int)
        self.assertEqual(standard["properties"]["NUM_STREAMS"], 1)
        self.assertNotIn("TURBOQUANT_KEY_ALGORITHM", standard["properties"])

        mixed = build_runtime_property_spec(
            device="cpu",
            key_algorithm="TBQ4",
            value_algorithm="STANDARD",
            key_cache_precision="u4",
            value_cache_precision="f16",
            norm_correction=True,
            cache_dir="cache",
        )
        self.assertEqual(
            mixed["properties"]["TURBOQUANT_KEY_ALGORITHM"], "TBQ4"
        )
        self.assertEqual(
            mixed["properties"]["TURBOQUANT_VALUE_ALGORITHM"], "STANDARD"
        )
        self.assertEqual(
            mixed["properties"]["VALUE_CACHE_PRECISION"], "f16"
        )
        self.assertNotIn("KEY_CACHE_PRECISION", mixed["properties"])
        self.assertIs(mixed["properties"]["TURBOQUANT_NORM_CORRECTION"], True)

    def test_gpu_standard_runtime_properties_are_plugin_owned_and_gpu_safe(self):
        expected_properties = {
            "ATTENTION_BACKEND": "SDPA",
            "CACHE_DIR": "cache",
            "NUM_STREAMS": "1",
            "PERFORMANCE_HINT": "LATENCY",
        }
        for device, expected_device in (
            ("gpu", "GPU"),
            ("GPU.0", "GPU.0"),
            ("gpu.12", "GPU.12"),
        ):
            with self.subTest(device=device):
                spec = build_runtime_property_spec(
                    device=device,
                    key_algorithm="STANDARD",
                    value_algorithm="STANDARD",
                    key_cache_precision="frozen",
                    value_cache_precision="frozen",
                    norm_correction=False,
                    cache_dir="cache",
                )
                self.assertEqual(spec["device"], expected_device)
                self.assertEqual(spec["properties"], expected_properties)
                self.assertIs(type(spec["properties"]["NUM_STREAMS"]), str)

    def test_gpu_runtime_property_spec_rejects_ambiguous_routes_and_overrides(self):
        common = {
            "key_algorithm": "STANDARD",
            "value_algorithm": "STANDARD",
            "key_cache_precision": "frozen",
            "value_cache_precision": "frozen",
            "norm_correction": False,
            "cache_dir": "cache",
        }
        for device in ("GPUX", "AUTO:GPU", "GPU.foo", "GPU.-1", "GPU.01"):
            with self.subTest(device=device):
                with self.assertRaisesRegex(ValueError, "CPU or GPU"):
                    build_runtime_property_spec(device=device, **common)

        for key_precision, value_precision in (
            ("u8", "frozen"),
            ("frozen", "u4"),
            ("f16", "f16"),
        ):
            with self.subTest(
                key_precision=key_precision,
                value_precision=value_precision,
            ):
                with self.assertRaisesRegex(ValueError, "plugin-owned"):
                    build_runtime_property_spec(
                        device="GPU.0",
                        key_algorithm="STANDARD",
                        value_algorithm="STANDARD",
                        key_cache_precision=key_precision,
                        value_cache_precision=value_precision,
                        norm_correction=False,
                        cache_dir="cache",
                    )

    def test_invalid_runtime_labels_and_non_cpu_turboquant_fail_closed(self):
        common = {
            "key_cache_precision": "u4",
            "value_cache_precision": "u4",
            "norm_correction": True,
            "cache_dir": "cache",
        }
        with self.assertRaisesRegex(ValueError, "uppercase"):
            build_runtime_property_spec(
                device="cpu",
                key_algorithm="tbq4",
                value_algorithm="TBQ4",
                **common,
            )
        with self.assertRaisesRegex(ValueError, "CPU"):
            build_runtime_property_spec(
                device="gpu",
                key_algorithm="TBQ4",
                value_algorithm="TBQ4",
                **common,
            )

    def test_unsupported_runtime_algorithms_name_field_and_value(self):
        common = {
            "device": "cpu",
            "key_cache_precision": "u4",
            "value_cache_precision": "u4",
            "norm_correction": True,
            "cache_dir": "cache",
        }
        with self.assertRaises(ValueError) as key_error:
            build_runtime_property_spec(
                key_algorithm="SCALAR",
                value_algorithm="TBQ4",
                **common,
            )
        self.assertEqual(
            str(key_error.exception),
            "unsupported runtime key algorithm: SCALAR; "
            "expected exact uppercase STANDARD, TBQ3, or TBQ4",
        )

        with self.assertRaises(ValueError) as value_error:
            build_runtime_property_spec(
                key_algorithm="STANDARD",
                value_algorithm="POLAR",
                **common,
            )
        self.assertEqual(
            str(value_error.exception),
            "unsupported runtime value algorithm: POLAR; "
            "expected exact uppercase STANDARD, TBQ3, or TBQ4",
        )

        with self.assertRaises(ValueError) as gpu_error:
            build_runtime_property_spec(
                device="gpu",
                key_algorithm="TBQ4",
                value_algorithm="TBQ4",
                key_cache_precision="u4",
                value_cache_precision="u4",
                norm_correction=True,
                cache_dir="cache",
            )
        self.assertEqual(
            str(gpu_error.exception),
            "project TurboQuant is supported only on CPU",
        )

    def test_worker_output_requires_one_result_and_one_activated_record(self):
        activation = activation_telemetry()
        result = worker_result()
        parsed = parse_worker_output(
            json.dumps(activation) + "\n" + RESULT_MARKER + json.dumps(result),
            "",
        )
        self.assertEqual(parsed["activation"], activation)
        self.assertEqual(parsed["result"], result)

        with self.assertRaisesRegex(ValueError, "activated"):
            parse_worker_output(RESULT_MARKER + json.dumps(result), "")
        with self.assertRaisesRegex(ValueError, "exactly one worker result"):
            parse_worker_output(
                json.dumps(activation)
                + "\n"
                + RESULT_MARKER
                + json.dumps(result)
                + "\n"
                + RESULT_MARKER
                + json.dumps(result),
                "",
            )

    def test_worker_output_accepts_measured_cpu_standard_telemetry(self):
        activation = standard_cpu_telemetry()
        parsed = parse_worker_output(
            json.dumps(activation)
            + "\n"
            + RESULT_MARKER
            + json.dumps(worker_result()),
            "",
        )
        self.assertEqual(parsed["activation"], activation)

    def test_worker_output_rejects_uninstrumented_gpu_standard_telemetry(self):
        activation = standard_cpu_telemetry(
            requested_key_cache_precision="not_requested",
            requested_value_cache_precision="not_requested",
            activated_key_cache_precision="not_requested",
            activated_value_cache_precision="not_requested",
            observed_key_state_precision="not_requested",
            observed_value_state_precision="not_requested",
            expected_bytes=0,
            actual_bytes=0,
            expected_persistent_standard_bytes=0,
            actual_persistent_standard_bytes=0,
            full_precision_equivalent_bytes=0,
            attention_path="not_requested",
            device="GPU",
            actual_device="GPU",
        )
        with self.assertRaisesRegex(ValueError, "GPU STANDARD"):
            parse_worker_output(
                json.dumps(activation)
                + "\n"
                + RESULT_MARKER
                + json.dumps(worker_result()),
                "",
            )

    def test_worker_output_accepts_instrumented_gpu_standard_telemetry(self):
        activation = standard_cpu_telemetry(
            device="GPU",
            actual_device="GPU.0",
        )
        parsed = parse_worker_output(
            json.dumps(activation)
            + "\n"
            + RESULT_MARKER
            + json.dumps(worker_result()),
            "",
        )
        self.assertEqual(parsed["activation"], activation)

    def test_worker_output_rejects_each_mismatched_byte_component(self):
        for component in ("standard", "payload", "norm", "metadata"):
            with self.subTest(component=component):
                activation = activation_telemetry(
                    **{f"actual_persistent_{component}_bytes": 999}
                )
                with self.assertRaisesRegex(ValueError, component):
                    parse_worker_output(
                        json.dumps(activation)
                        + "\n"
                        + RESULT_MARKER
                        + json.dumps(worker_result()),
                        "",
                    )

    def test_worker_output_rejects_totals_that_do_not_equal_components(self):
        activation = activation_telemetry(
            expected_bytes=101,
            actual_bytes=101,
        )
        with self.assertRaisesRegex(ValueError, "components"):
            parse_worker_output(
                json.dumps(activation)
                + "\n"
                + RESULT_MARKER
                + json.dumps(worker_result()),
                "",
            )

    def test_gpu_parser_preserves_every_observation_and_recomputes_statistics(self):
        rows = [
            {
                "timestamp_utc": f"2026-07-29T00:00:0{i}Z",
                "gpu_percent": value,
                "gpu_engine_count": i,
                "gpu_dedicated_mb": i * 2,
                "gpu_shared_mb": i * 3,
                "gpu_engine_query_ok": "true",
                "gpu_memory_query_ok": "true",
            }
            for i, value in enumerate((0.0, 20.0, 10.0), start=1)
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gpu.csv"
            write_csv(path, rows)
            parsed = parse_gpu_samples(path)
        self.assertEqual(parsed["gpu_percent"]["values"], [0.0, 20.0, 10.0])
        self.assertEqual(parsed["gpu_percent"]["mean"], statistics.fmean([0, 20, 10]))
        self.assertEqual(parsed["gpu_percent"]["median"], 10)
        self.assertEqual(parsed["gpu_percent"]["peak"], 20)
        self.assertEqual(parsed["gpu_percent"]["count"], 3)
        self.assertEqual(parsed["gpu_engine_count"]["values"], [1.0, 2.0, 3.0])
        self.assertEqual(parsed["gpu_engine_count"]["mean"], 2.0)
        self.assertEqual(parsed["gpu_engine_count"]["median"], 2.0)
        self.assertEqual(parsed["gpu_engine_count"]["peak"], 3.0)
        self.assertEqual(parsed["gpu_engine_count"]["count"], 3)
        self.assertEqual(parsed["gpu_dedicated_memory_peak_mb"], 6)
        self.assertEqual(parsed["gpu_shared_memory_peak_mb"], 9)
        self.assertNotIn("gpu_memory_peak_bytes", parsed)

    def test_gpu_memory_peak_uses_one_observation_not_independent_maxima(self):
        rows = [
            {
                "timestamp_utc": "2026-07-29T00:00:01Z",
                "gpu_percent": 10,
                "gpu_engine_count": 1,
                "gpu_dedicated_mb": 10,
                "gpu_shared_mb": 0,
                "gpu_engine_query_ok": "true",
                "gpu_memory_query_ok": "true",
            },
            {
                "timestamp_utc": "2026-07-29T00:00:02Z",
                "gpu_percent": 20,
                "gpu_engine_count": 1,
                "gpu_dedicated_mb": 0,
                "gpu_shared_mb": 9,
                "gpu_engine_query_ok": "true",
                "gpu_memory_query_ok": "true",
            },
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gpu.csv"
            write_csv(path, rows)
            parsed = parse_gpu_samples(path)
        self.assertEqual(parsed["gpu_dedicated_memory_peak_mb"], 10)
        self.assertEqual(parsed["gpu_shared_memory_peak_mb"], 9)
        self.assertEqual(parsed["gpu_memory_peak_mb"], 10)

    def test_cpu_parser_recomputes_mean_median_peak_and_count(self):
        rows = [
            {
                "timestamp_utc": f"2026-07-29T00:00:0{i}Z",
                "cpu_percent": value,
                "cpu_sample_definition": (
                    "lifetime_average_since_workload_resume"
                    if i == 1
                    else "interval_delta"
                ),
            }
            for i, value in enumerate((5.0, 15.0, 10.0), start=1)
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "cpu.csv"
            write_csv(path, rows)
            parsed = parse_cpu_samples(path)
        self.assertEqual(parsed["values"], [5.0, 15.0, 10.0])
        self.assertEqual(parsed["mean"], 10.0)
        self.assertEqual(parsed["median"], 10.0)
        self.assertEqual(parsed["peak"], 15.0)
        self.assertEqual(parsed["count"], 3)

    def test_gpu_parser_rejects_one_shot_or_failed_queries(self):
        row = {
            "timestamp_utc": "2026-07-29T00:00:01Z",
            "gpu_percent": 0,
            "gpu_engine_count": 0,
            "gpu_dedicated_mb": 0,
            "gpu_shared_mb": 0,
            "gpu_engine_query_ok": "true",
            "gpu_memory_query_ok": "true",
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gpu.csv"
            write_csv(path, [row])
            with self.assertRaisesRegex(ValueError, "at least two"):
                parse_gpu_samples(path)
            write_csv(path, [row, {**row, "gpu_engine_query_ok": "false"}])
            with self.assertRaisesRegex(ValueError, "query failed"):
                parse_gpu_samples(path)

    def test_gpu_parser_retains_binary_byte_proof_for_peak_memory(self):
        mib = 1024**2
        rows = [
            {
                "timestamp_utc": f"2026-07-29T00:00:0{index}Z",
                "gpu_percent": 0,
                "gpu_engine_count": 0,
                "gpu_dedicated_mb": f"{dedicated / mib:.6f}",
                "gpu_shared_mb": f"{shared / mib:.6f}",
                "gpu_memory_mb": f"{(dedicated + shared) / mib:.6f}",
                "gpu_dedicated_bytes": dedicated,
                "gpu_shared_bytes": shared,
                "gpu_memory_bytes": dedicated + shared,
                "gpu_engine_query_ok": "true",
                "gpu_memory_query_ok": "true",
            }
            for index, dedicated, shared in (
                (1, 2 * mib, 1 * mib),
                (2, 3 * mib, 4 * mib),
            )
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gpu.csv"
            write_csv(path, rows)
            parsed = parse_gpu_samples(path)

        self.assertEqual(parsed["gpu_memory_peak_mb"], 7.0)
        self.assertEqual(parsed["gpu_memory_peak_bytes"], 7 * mib)

    def test_gpu_parser_accepts_two_byte_proof_and_retains_peak_components(self):
        mib = 1024**2
        rows = [
            {
                "timestamp_utc": "2026-07-29T00:00:01Z",
                "gpu_percent": 0,
                "gpu_engine_count": 0,
                "gpu_dedicated_mb": "1.000000",
                "gpu_shared_mb": "0.000000",
                "gpu_dedicated_bytes": mib,
                "gpu_shared_bytes": 0,
                "gpu_engine_query_ok": "true",
                "gpu_memory_query_ok": "true",
            },
            {
                "timestamp_utc": "2026-07-29T00:00:02Z",
                "gpu_percent": 0,
                "gpu_engine_count": 0,
                "gpu_dedicated_mb": "0.000000",
                "gpu_shared_mb": "1.000000",
                "gpu_dedicated_bytes": 0,
                "gpu_shared_bytes": mib,
                "gpu_engine_query_ok": "true",
                "gpu_memory_query_ok": "true",
            },
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gpu.csv"
            write_csv(path, rows)
            parsed = parse_gpu_samples(path)

        self.assertEqual(parsed["gpu_dedicated_memory_peak_mb"], 1.0)
        self.assertEqual(parsed["gpu_shared_memory_peak_mb"], 1.0)
        self.assertEqual(parsed["gpu_memory_peak_mb"], 1.0)
        self.assertEqual(parsed["gpu_memory_peak_bytes"], mib)
        self.assertEqual(parsed["gpu_memory_peak_dedicated_bytes"], mib)
        self.assertEqual(parsed["gpu_memory_peak_shared_bytes"], 0)

    def test_gpu_parser_rejects_forged_component_displays_with_byte_proof(self):
        mib = 1024**2
        two_byte_row = {
            "timestamp_utc": "2026-07-29T00:00:01Z",
            "gpu_percent": 0,
            "gpu_engine_count": 0,
            "gpu_dedicated_mb": "1.000000",
            "gpu_shared_mb": "2.000000",
            "gpu_dedicated_bytes": mib,
            "gpu_shared_bytes": 2 * mib,
            "gpu_engine_query_ok": "true",
            "gpu_memory_query_ok": "true",
        }
        combined_row = {
            **two_byte_row,
            "gpu_memory_mb": "3.000000",
            "gpu_memory_bytes": 3 * mib,
        }
        invalid_rows = (
            {**two_byte_row, "gpu_dedicated_mb": "1000.000000"},
            {**two_byte_row, "gpu_shared_mb": "2.500000"},
            {**combined_row, "gpu_dedicated_mb": "1000.000000"},
            {**combined_row, "gpu_shared_mb": "2.500000"},
        )
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gpu.csv"
            for invalid in invalid_rows:
                write_csv(path, [invalid, invalid])
                with self.assertRaisesRegex(ValueError, "component MiB display"):
                    parse_gpu_samples(path)

    def test_binary_mib_serializer_uses_six_digit_half_up_rounding(self):
        cases = (
            (0, "0.000000"),
            (8191, "0.007812"),
            (8192, "0.007813"),
            (8193, "0.007813"),
            (1024**2, "1.000000"),
            (9007199254749183, "8589934592.007812"),
            (2**64 - 1, "17592186044415.999999"),
        )
        for byte_count, expected in cases:
            with self.subTest(byte_count=byte_count):
                self.assertEqual(
                    runtime_measurement.format_binary_mib(byte_count),
                    expected,
                )

    def test_binary_mib_serializer_rejects_outside_uint64_domain(self):
        for byte_count in (-1, 2**64):
            with self.subTest(byte_count=byte_count):
                with self.assertRaisesRegex(ValueError, "UInt64"):
                    runtime_measurement.format_binary_mib(byte_count)

    def test_gpu_parser_accepts_half_up_midpoint_in_both_byte_proof_formats(self):
        two_byte_row = {
            "timestamp_utc": "2026-07-29T00:00:01Z",
            "gpu_percent": 0,
            "gpu_engine_count": 0,
            "gpu_dedicated_mb": "0.007813",
            "gpu_shared_mb": "0.000000",
            "gpu_dedicated_bytes": 8192,
            "gpu_shared_bytes": 0,
            "gpu_engine_query_ok": "true",
            "gpu_memory_query_ok": "true",
        }
        rows_by_format = {
            "two-byte": two_byte_row,
            "combined": {
                **two_byte_row,
                "gpu_memory_mb": "0.007813",
                "gpu_memory_bytes": 8192,
            },
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gpu.csv"
            for proof_format, row in rows_by_format.items():
                with self.subTest(proof_format=proof_format):
                    write_csv(path, [row, row])
                    parsed = parse_gpu_samples(path)
                    self.assertEqual(parsed["gpu_memory_peak_bytes"], 8192)
                    self.assertEqual(parsed["gpu_memory_peak_mb"], 0.007813)

    def test_gpu_parser_rejects_ties_to_even_midpoint_in_both_proof_formats(self):
        two_byte_row = {
            "timestamp_utc": "2026-07-29T00:00:01Z",
            "gpu_percent": 0,
            "gpu_engine_count": 0,
            "gpu_dedicated_mb": "0.007812",
            "gpu_shared_mb": "0.000000",
            "gpu_dedicated_bytes": 8192,
            "gpu_shared_bytes": 0,
            "gpu_engine_query_ok": "true",
            "gpu_memory_query_ok": "true",
        }
        cases = (
            ("two-byte", two_byte_row, "component MiB display"),
            (
                "combined",
                {
                    **two_byte_row,
                    "gpu_dedicated_mb": "0.007813",
                    "gpu_memory_mb": "0.007812",
                    "gpu_memory_bytes": 8192,
                },
                "combined MiB display",
            ),
        )
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gpu.csv"
            for proof_format, row, message in cases:
                with self.subTest(proof_format=proof_format):
                    write_csv(path, [row, row])
                    with self.assertRaisesRegex(ValueError, message):
                        parse_gpu_samples(path)

    def test_powershell_sampler_binary_mib_helper_matches_python_contract(self):
        powershell = shutil.which("powershell")
        if powershell is None:
            self.skipTest("Windows PowerShell is unavailable")
        sampler = (
            Path(__file__).parents[1]
            / "collect_openvino_runtime_utilization.ps1"
        )
        command = """
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    $env:OPENVINO_SAMPLER_PATH,
    [ref]$tokens,
    [ref]$errors
)
if ($errors.Count -ne 0) { throw 'sampler script has parse errors' }
$helper = $ast.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Format-BinaryMiB'
}, $true)
if ($null -eq $helper) { throw 'Format-BinaryMiB helper is missing' }
Invoke-Expression $helper.Extent.Text
0, 8191, 8192, 8193, 1048576, 9007199254749183, 18446744073709551615 |
    ForEach-Object { Format-BinaryMiB $_ }
"""
        environment = {**os.environ, "OPENVINO_SAMPLER_PATH": str(sampler)}
        completed = subprocess.run(
            [powershell, "-NoProfile", "-Command", command],
            check=False,
            capture_output=True,
            text=True,
            env=environment,
        )
        self.assertEqual(completed.returncode, 0, completed.stderr)
        self.assertEqual(
            completed.stdout.splitlines(),
            [
                "0.000000",
                "0.007812",
                "0.007813",
                "0.007813",
                "1.000000",
                "8589934592.007812",
                "17592186044415.999999",
            ],
        )

    def test_gpu_parser_rejects_missing_or_invalid_combined_byte_proof(self):
        row = {
            "timestamp_utc": "2026-07-29T00:00:01Z",
            "gpu_percent": 0,
            "gpu_engine_count": 0,
            "gpu_dedicated_mb": 8192.000117,
            "gpu_shared_mb": 4096.000435,
            "gpu_memory_mb": 12288.000552,
            "gpu_dedicated_bytes": 8589934715,
            "gpu_shared_bytes": 4294967752,
            "gpu_memory_bytes": 12884902467,
            "gpu_engine_query_ok": "true",
            "gpu_memory_query_ok": "true",
        }
        invalid_rows = [
            {key: value for key, value in row.items() if key != "gpu_memory_bytes"},
            {
                key: value
                for key, value in row.items()
                if key not in {"gpu_shared_bytes", "gpu_memory_bytes", "gpu_memory_mb"}
            },
            {**row, "gpu_memory_bytes": 12884902466},
            {**row, "gpu_memory_bytes": "not-an-integer"},
        ]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "gpu.csv"
            for invalid in invalid_rows:
                write_csv(path, [invalid, invalid])
                with self.assertRaisesRegex(ValueError, "combined.*byte proof"):
                    parse_gpu_samples(path)

    def test_attempt_sequence_is_resume_safe_and_excludes_pilot_and_warmup(self):
        calls: list[str] = []

        def run(role: str, target: Path) -> dict:
            calls.append(role)
            target.mkdir(parents=True)
            record = {
                "role": role,
                "valid": True,
                "cleanup_process_count": 0,
                "source": str(target / "attempt.json"),
            }
            (target / "attempt.json").write_text(
                json.dumps(record), encoding="utf-8"
            )
            return record

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            first = execute_attempt_sequence(root, run)
            second = execute_attempt_sequence(root, run)

        self.assertEqual(
            calls, ["pilot", "warmup", "sample-1", "sample-2", "sample-3"]
        )
        self.assertTrue(first["pilot_passed"])
        self.assertTrue(first["warmup_excluded"])
        self.assertEqual(
            [row["role"] for row in first["accepted_samples"]],
            ["sample-1", "sample-2", "sample-3"],
        )
        self.assertEqual(second, first)

    def test_attempt_sequence_stops_after_an_invalid_pilot(self):
        def run(role: str, target: Path) -> dict:
            target.mkdir(parents=True)
            return {
                "role": role,
                "valid": False,
                "cleanup_process_count": 0,
                "validation_errors": ["low memory stop"],
            }

        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(RuntimeError, "pilot"):
                execute_attempt_sequence(Path(directory), run)

    def test_guard_rejects_missing_post_run_available_ram(self):
        launch_floor = 4 * 1024**3
        emergency_floor = 2 * 1024**3
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sampler = root / "sampler.ps1"
            sampler.write_text("", encoding="utf-8")
            with mock.patch(
                "scripts.testing.official_openvino.runtime_process.available_ram_bytes",
                side_effect=[launch_floor - 1, None],
            ):
                record = run_governed_process(
                    command=["never-launched"],
                    output_dir=root / "run",
                    role="pilot",
                    environment={},
                    sampler_script=sampler,
                    launch_minimum_available_ram_bytes=launch_floor,
                    emergency_minimum_available_ram_bytes=emergency_floor,
                )
        self.assertFalse(record["valid"])
        self.assertIn(
            "available RAM query failed after run",
            record["validation_errors"],
        )

    def test_guard_uses_4096_launch_reserve_and_2048_emergency_floor(self):
        mib = 1024**2
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sampler = root / "sampler.ps1"
            sampler.write_text("", encoding="utf-8")
            with mock.patch(
                "scripts.testing.official_openvino.runtime_process.available_ram_bytes",
                side_effect=[4095 * mib, 4095 * mib],
            ):
                record = run_governed_process(
                    command=["never-launched"],
                    output_dir=root / "run",
                    role="pilot",
                    environment={},
                    sampler_script=sampler,
                    launch_minimum_available_ram_bytes=4096 * mib,
                    emergency_minimum_available_ram_bytes=2048 * mib,
                )
        self.assertEqual(record["launch_minimum_available_ram_bytes"], 4096 * mib)
        self.assertEqual(record["emergency_minimum_available_ram_bytes"], 2048 * mib)
        self.assertIn(
            "available RAM is below the pre-launch floor",
            record["validation_errors"],
        )

    def test_guard_rejects_lower_configured_launch_floor(self):
        mib = 1024**2
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sampler = root / "sampler.ps1"
            sampler.write_text("", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "launch"):
                run_governed_process(
                    command=["never-launched"],
                    output_dir=root / "run",
                    role="pilot",
                    environment={},
                    sampler_script=sampler,
                    launch_minimum_available_ram_bytes=4095 * mib,
                    emergency_minimum_available_ram_bytes=2048 * mib,
                )

    def test_guard_rejects_lower_configured_emergency_floor(self):
        mib = 1024**2
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sampler = root / "sampler.ps1"
            sampler.write_text("", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "emergency"):
                run_governed_process(
                    command=["never-launched"],
                    output_dir=root / "run",
                    role="pilot",
                    environment={},
                    sampler_script=sampler,
                    launch_minimum_available_ram_bytes=4096 * mib,
                    emergency_minimum_available_ram_bytes=2047 * mib,
                )

    def test_guard_rejects_post_run_available_ram_below_floor(self):
        launch_floor = 4 * 1024**3
        emergency_floor = 2 * 1024**3
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sampler = root / "sampler.ps1"
            sampler.write_text("", encoding="utf-8")
            with mock.patch(
                "scripts.testing.official_openvino.runtime_process.available_ram_bytes",
                side_effect=[launch_floor - 1, emergency_floor - 1],
            ):
                record = run_governed_process(
                    command=["never-launched"],
                    output_dir=root / "run",
                    role="pilot",
                    environment={},
                    sampler_script=sampler,
                    launch_minimum_available_ram_bytes=launch_floor,
                    emergency_minimum_available_ram_bytes=emergency_floor,
                )
        self.assertFalse(record["valid"])
        self.assertIn(
            "available RAM is below the post-run floor",
            record["validation_errors"],
        )

    def test_governed_process_persists_parsed_gpu_engine_evidence(self):
        launch_floor = 4 * 1024**3
        emergency_floor = 2 * 1024**3
        gpu_evidence = {
            "gpu_percent": utilization_summary([4.0, 8.0]),
            "gpu_engine_count": utilization_summary([1.0, 2.0]),
            "gpu_dedicated_memory_peak_mb": 10.000010,
            "gpu_shared_memory_peak_mb": 1.000010,
            "gpu_memory_peak_mb": 11.000021,
            "gpu_dedicated_memory_peak_bytes": (10 * 1024**2) + 11,
            "gpu_shared_memory_peak_bytes": 1024**2 + 11,
            "gpu_memory_peak_dedicated_bytes": (10 * 1024**2) + 11,
            "gpu_memory_peak_shared_bytes": 1024**2 + 11,
            "gpu_memory_peak_bytes": (11 * 1024**2) + 22,
            "observation_count": 2,
        }
        parsed_worker = {
            "result": worker_result(),
            "activation": standard_cpu_telemetry(
                device="GPU",
                actual_device="GPU.0",
            ),
        }
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            sampler = root / "sampler.ps1"
            sampler.write_text("", encoding="utf-8")
            with (
                mock.patch(
                    "scripts.testing.official_openvino.runtime_process."
                    "available_ram_bytes",
                    side_effect=[launch_floor - 1, emergency_floor],
                ),
                mock.patch(
                    "scripts.testing.official_openvino.runtime_process."
                    "parse_cpu_samples",
                    return_value=utilization_summary([10.0, 12.0]),
                ),
                mock.patch(
                    "scripts.testing.official_openvino.runtime_process."
                    "parse_gpu_samples",
                    return_value=gpu_evidence,
                ),
                mock.patch(
                    "scripts.testing.official_openvino.runtime_process."
                    "parse_worker_output",
                    return_value=parsed_worker,
                ),
            ):
                record = run_governed_process(
                    command=["never-launched"],
                    output_dir=root / "run",
                    role="pilot",
                    environment={},
                    sampler_script=sampler,
                    launch_minimum_available_ram_bytes=launch_floor,
                    emergency_minimum_available_ram_bytes=emergency_floor,
                )
        self.assertEqual(
            record["gpu_engine_count"],
            gpu_evidence["gpu_engine_count"],
        )
        self.assertNotIn(
            "GPU combined memory peak is outside component bounds",
            record["validation_errors"],
        )

    def test_gpu_measurement_sample_requires_repeated_nonzero_observability(self):
        cases = (
            (
                "one GPU utilization observation",
                lambda record: record.__setitem__(
                    "gpu_percent", utilization_summary([8.0])
                ),
                "at least two",
            ),
            (
                "one engine-count observation",
                lambda record: record.__setitem__(
                    "gpu_engine_count", utilization_summary([1.0])
                ),
                "at least two",
            ),
            (
                "zero engine peak",
                lambda record: record.__setitem__(
                    "gpu_engine_count", utilization_summary([0.0, 0.0])
                ),
                "engine-count peak",
            ),
            (
                "zero utilization peak",
                lambda record: record.__setitem__(
                    "gpu_percent", utilization_summary([0.0, 0.0])
                ),
                "utilization peak",
            ),
            (
                "zero combined memory",
                lambda record: record.update(
                    gpu_dedicated_memory_peak_mb=0.0,
                    gpu_shared_memory_peak_mb=0.0,
                    gpu_memory_peak_mb=0.0,
                ),
                "combined memory peak",
            ),
            (
                "failed engine query",
                lambda record: record["gpu_engine_count"].update(
                    query_succeeded=False
                ),
                "engine-count query",
            ),
        )
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "attempt.json"
            source.write_text("{}", encoding="utf-8")
            for name, mutate, expected_error in cases:
                with self.subTest(name=name):
                    record = governed_record(
                        standard_cpu_telemetry(
                            device="GPU",
                            actual_device="GPU.0",
                        )
                    )
                    mutate(record)
                    with self.assertRaisesRegex(ValueError, expected_error):
                        measurement_sample(record, source)

    def test_integrated_gpu_shared_memory_observability_is_valid(self):
        record = governed_record(
            standard_cpu_telemetry(
                device="GPU",
                actual_device="GPU.0",
            )
        )
        record.update(
            gpu_dedicated_memory_peak_mb=0.0,
            gpu_shared_memory_peak_mb=24.0,
            gpu_memory_peak_mb=24.0,
        )
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "attempt.json"
            source.write_text("{}", encoding="utf-8")
            sample = measurement_sample(record, source)
        self.assertEqual(sample["gpu_dedicated_memory_peak_mb"], 0.0)
        self.assertEqual(sample["gpu_shared_memory_peak_mb"], 24.0)
        self.assertEqual(sample["gpu_memory_peak_mb"], 24.0)

    def test_gpu_measurement_sample_rejects_incomplete_peak_byte_receipt(self):
        cases = (
            (
                "missing paired dedicated bytes",
                lambda record: record.pop("gpu_memory_peak_dedicated_bytes"),
                "paired",
            ),
            (
                "mismatched paired bytes",
                lambda record: record.update(gpu_memory_peak_shared_bytes=1),
                "do not equal",
            ),
            (
                "noncanonical combined display",
                lambda record: record.update(gpu_memory_peak_mb=1.5),
                "serialization",
            ),
        )
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "attempt.json"
            source.write_text("{}", encoding="utf-8")
            for name, mutate, expected_error in cases:
                with self.subTest(name=name):
                    record = governed_record(
                        standard_cpu_telemetry(
                            device="GPU",
                            actual_device="GPU.0",
                        )
                    )
                    record.update(
                        gpu_dedicated_memory_peak_mb=1.0,
                        gpu_shared_memory_peak_mb=0.0,
                        gpu_memory_peak_mb=1.0,
                        gpu_memory_peak_bytes=1024**2,
                        gpu_memory_peak_dedicated_bytes=1024**2,
                        gpu_memory_peak_shared_bytes=0,
                    )
                    mutate(record)
                    with self.assertRaisesRegex(ValueError, expected_error):
                        measurement_sample(record, source)

    def test_gpu_measurement_sample_accepts_half_up_midpoint_byte_receipt(self):
        record = governed_record(
            standard_cpu_telemetry(device="GPU", actual_device="GPU.0")
        )
        record.update(
            gpu_dedicated_memory_peak_mb=0.007813,
            gpu_shared_memory_peak_mb=0.0,
            gpu_memory_peak_mb=0.007813,
            gpu_dedicated_memory_peak_bytes=8192,
            gpu_shared_memory_peak_bytes=0,
            gpu_memory_peak_dedicated_bytes=8192,
            gpu_memory_peak_shared_bytes=0,
            gpu_memory_peak_bytes=8192,
        )
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "attempt.json"
            source.write_text("{}", encoding="utf-8")
            sample = measurement_sample(record, source)

        self.assertEqual(sample["gpu_memory_peak_mb"], 0.007813)

    def test_cpu_measurement_sample_allows_honest_zero_gpu_evidence(self):
        record = governed_record(activation_telemetry())
        record.update(
            gpu_percent=utilization_summary([0.0, 0.0]),
            gpu_engine_count=utilization_summary([0.0, 0.0]),
            gpu_dedicated_memory_peak_mb=0.0,
            gpu_shared_memory_peak_mb=0.0,
            gpu_memory_peak_mb=0.0,
        )
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "attempt.json"
            source.write_text("{}", encoding="utf-8")
            sample = measurement_sample(record, source)
        self.assertEqual(sample["gpu_percent"]["peak"], 0.0)
        self.assertEqual(sample["gpu_memory_peak_mb"], 0.0)

    def test_measurement_sample_retains_runtime_identity_and_byte_components(self):
        activation = activation_telemetry()
        record = governed_record(activation)
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "attempt.json"
            source.write_text("{}", encoding="utf-8")
            sample = measurement_sample(record, source)
        retained = sample["activation"]
        for field in (
            "build_commit",
            "model_hash",
            "transformed_model_hash",
            "operation_type",
            "operation_count",
            "matched_state_count",
            "expected_persistent_standard_bytes",
            "actual_persistent_standard_bytes",
            "expected_persistent_payload_bytes",
            "actual_persistent_payload_bytes",
            "expected_persistent_norm_bytes",
            "actual_persistent_norm_bytes",
            "expected_persistent_metadata_bytes",
            "actual_persistent_metadata_bytes",
        ):
            self.assertEqual(retained[field], activation[field])

    def test_measurement_sample_rejects_invalid_runtime_identity(self):
        activation = activation_telemetry(build_commit="not-a-commit")
        record = governed_record(activation)
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "attempt.json"
            source.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "build commit"):
                measurement_sample(record, source)

    def test_measurement_sample_rejects_operation_state_count_mismatch(self):
        activation = activation_telemetry(matched_state_count=1)
        record = governed_record(activation)
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "attempt.json"
            source.write_text("{}", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "operation"):
                measurement_sample(record, source)


if __name__ == "__main__":
    unittest.main()
