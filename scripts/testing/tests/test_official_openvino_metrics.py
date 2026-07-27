import copy
import statistics
import unittest

from scripts.testing.official_openvino.metrics import MIB, summarize_samples


def utilization(values):
    return {
        "values": values,
        "mean": statistics.fmean(values),
        "median": statistics.median(values),
        "peak": max(values),
        "count": len(values),
        "query_succeeded": True,
    }


def sample(offset=0):
    payload = (96 + offset) * MIB
    metadata = (4 + offset) * MIB
    actual = payload + metadata
    dedicated = 48 + offset
    shared = 16 + offset
    return {
        "sample_id": f"sample-{offset + 1}",
        "load_ms": 100 + offset,
        "ttft_ms": 50 + offset,
        "prompt_tps": 20 + offset,
        "tpot_ms": 25 + offset,
        "decode_tps": 40 + offset,
        "generation_duration_ms": 500 + offset,
        "peak_working_set_mb": 1000 + offset,
        "peak_private_mb": 900 + offset,
        "available_ram_before_mb": 3500 - offset,
        "available_ram_min_mb": 3000 - offset,
        "available_ram_after_mb": 3300 - offset,
        "gpu_dedicated_memory_peak_mb": dedicated,
        "gpu_shared_memory_peak_mb": shared,
        "gpu_memory_peak_mb": dedicated + shared,
        "expected_persistent_kv_bytes": actual,
        "actual_persistent_kv_bytes": actual,
        "payload_kv_bytes": payload,
        "metadata_kv_bytes": metadata,
        "scratch_peak_bytes": (8 + offset) * MIB,
        "kv_mb": actual / MIB,
        "cpu_percent": utilization([40 + offset, 42 + offset, 41 + offset]),
        "gpu_percent": utilization([20 + offset, 22 + offset, 21 + offset]),
        "activation": {
            "requested_key_algorithm": "TBQ4",
            "requested_value_algorithm": "TBQ3",
            "activated_key_algorithm": "TBQ4",
            "activated_value_algorithm": "TBQ3",
            "norm_correction": True,
            "attention_path": "stateful-sdpa",
            "requested_device": "CPU",
            "actual_device": "CPU",
            "fallback": False,
            "expected_persistent_bytes": actual,
            "actual_persistent_bytes": actual,
            "output_valid": True,
        },
        "output_sha256": f"{offset + 1:064x}",
        "telemetry_sha256": f"{offset + 11:064x}",
        "source": f"attempt/sample-{offset + 1}/sample.json",
        "source_sha256": f"{offset + 21:064x}",
        "cleanup_process_count": 0,
    }


class OfficialOpenVINOMetricTests(unittest.TestCase):
    def test_summary_requires_exactly_three_samples(self):
        with self.assertRaisesRegex(ValueError, "exactly three"):
            summarize_samples([sample(), sample(1)])

    def test_summary_recomputes_pooled_utilization_not_median_of_medians(self):
        result = summarize_samples([sample(i) for i in range(3)])
        cpu_values = [40, 42, 41, 41, 43, 42, 42, 44, 43]
        gpu_values = [20, 22, 21, 21, 23, 22, 22, 24, 23]
        self.assertEqual(result["cpu_percent"]["mean"], statistics.fmean(cpu_values))
        self.assertEqual(result["cpu_percent"]["median"], statistics.median(cpu_values))
        self.assertEqual(result["cpu_percent"]["peak"], 44)
        self.assertEqual(result["cpu_percent"]["count"], 9)
        self.assertEqual(result["gpu_percent"]["mean"], statistics.fmean(gpu_values))
        self.assertEqual(result["gpu_percent"]["median"], statistics.median(gpu_values))
        self.assertEqual(result["gpu_percent"]["peak"], 24)
        self.assertEqual(result["gpu_percent"]["count"], 9)

    def test_summary_reports_all_memory_kv_timing_and_activation_evidence(self):
        result = summarize_samples([sample(i) for i in range(3)])
        self.assertEqual(result["ttft_ms"]["median"], 51)
        self.assertEqual(result["available_ram_before_mb"]["count"], 3)
        self.assertEqual(result["gpu_dedicated_memory_peak_mb"]["max"], 50)
        self.assertEqual(result["gpu_shared_memory_peak_mb"]["max"], 18)
        self.assertEqual(result["actual_persistent_kv_bytes"]["count"], 3)
        self.assertEqual(result["activation"]["sample_count"], 3)
        self.assertEqual(
            result["activation"]["activated_pairs"],
            [{"key": "TBQ4", "value": "TBQ3"}],
        )
        self.assertEqual(len(result["output_sha256"]), 3)

    def test_utilization_summary_and_raw_values_must_reconcile(self):
        for mutate, message in (
            (
                lambda rows: rows[0]["gpu_percent"].pop("mean"),
                "gpu_percent.mean",
            ),
            (
                lambda rows: rows[0]["cpu_percent"].update(mean=99),
                "cpu_percent.mean mismatch",
            ),
            (
                lambda rows: rows[0]["gpu_percent"].update(count=2),
                "gpu_percent.count mismatch",
            ),
            (
                lambda rows: rows[0]["gpu_percent"].update(query_succeeded=False),
                "gpu_percent query",
            ),
        ):
            rows = [sample(i) for i in range(3)]
            mutate(rows)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                summarize_samples(rows)

    def test_memory_kv_activation_output_and_cleanup_are_fail_closed(self):
        mutations = (
            (
                lambda rows: rows[0].update(available_ram_min_mb=4000),
                "available RAM minimum",
            ),
            (
                lambda rows: rows[0].update(gpu_memory_peak_mb=1),
                "GPU memory total",
            ),
            (
                lambda rows: rows[0].update(actual_persistent_kv_bytes=1),
                "persistent KV",
            ),
            (
                lambda rows: rows[0]["activation"].update(fallback=True),
                "fallback",
            ),
            (
                lambda rows: rows[0]["activation"].update(output_valid=False),
                "output validity",
            ),
            (
                lambda rows: rows[0].update(output_sha256="not-a-hash"),
                "output SHA256",
            ),
            (
                lambda rows: rows[0].update(cleanup_process_count=1),
                "cleanup",
            ),
        )
        for mutate, message in mutations:
            rows = [copy.deepcopy(sample(i)) for i in range(3)]
            mutate(rows)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                summarize_samples(rows)

    def test_duplicate_samples_or_sources_are_rejected(self):
        rows = [sample(i) for i in range(3)]
        rows[1]["sample_id"] = rows[0]["sample_id"]
        with self.assertRaisesRegex(ValueError, "duplicate sample"):
            summarize_samples(rows)
        rows = [sample(i) for i in range(3)]
        rows[1]["source_sha256"] = rows[0]["source_sha256"]
        with self.assertRaisesRegex(ValueError, "duplicate sample source"):
            summarize_samples(rows)

    def test_terminal_measurement_is_explicit_and_sourced(self):
        result = summarize_samples([], terminal={
            "status": "not-produced-by-expected-rejection",
            "evidence": "attempt/rejection.json",
            "evidence_sha256": "a" * 64,
        })
        self.assertEqual(result["status"], "not-produced-by-expected-rejection")
        with self.assertRaisesRegex(ValueError, "evidence SHA256"):
            summarize_samples([], terminal={
                "status": "not-produced-by-expected-rejection",
                "evidence": "attempt/rejection.json",
            })


if __name__ == "__main__":
    unittest.main()
