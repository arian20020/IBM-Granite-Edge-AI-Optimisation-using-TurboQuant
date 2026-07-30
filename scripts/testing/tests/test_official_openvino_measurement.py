import csv
import json
import statistics
import tempfile
import unittest
from pathlib import Path

from scripts.testing.official_openvino.runtime_measurement import (
    RESULT_MARKER,
    build_runtime_property_spec,
    execute_attempt_sequence,
    parse_cpu_samples,
    parse_gpu_samples,
    parse_worker_output,
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

    def test_worker_output_requires_one_result_and_one_activated_record(self):
        activation = {
            "status": "activated",
            "requested_key_algorithm": "TBQ4",
            "requested_value_algorithm": "TBQ3",
            "activated_key_algorithm": "TBQ4",
            "activated_value_algorithm": "TBQ3",
            "requested_key_cache_precision": "u4",
            "requested_value_cache_precision": "u3",
            "activated_key_cache_precision": "u4",
            "activated_value_cache_precision": "u3",
            "expected_bytes": 100,
            "actual_bytes": 100,
            "expected_persistent_standard_bytes": 0,
            "actual_persistent_standard_bytes": 0,
            "fallback": False,
        }
        result = {
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
        self.assertEqual(parsed["gpu_dedicated_memory_peak_mb"], 6)
        self.assertEqual(parsed["gpu_shared_memory_peak_mb"], 9)

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


if __name__ == "__main__":
    unittest.main()
