import unittest

from scripts.testing.official_openvino.metrics import summarize_samples


def sample(offset=0):
    return {
        "load_ms": 100 + offset, "ttft_ms": 50 + offset,
        "prompt_tps": 20 + offset, "tpot_ms": 25 + offset,
        "decode_tps": 40 + offset, "generation_duration_ms": 500 + offset,
        "peak_working_set_mb": 1000 + offset, "peak_private_mb": 900 + offset,
        "available_ram_min_mb": 3000 - offset, "kv_mb": 128 + offset,
        "gpu_memory_peak_mb": 64 + offset,
        "cpu_percent": {"mean": 40 + offset, "median": 39 + offset,
                        "peak": 70 + offset, "count": 10},
        "gpu_percent": {"mean": 20 + offset, "median": 19 + offset,
                        "peak": 50 + offset, "count": 10},
    }


class OfficialOpenVINOMetricTests(unittest.TestCase):
    def test_summary_requires_exactly_three_samples(self):
        with self.assertRaisesRegex(ValueError, "exactly three"):
            summarize_samples([sample(), sample(1)])

    def test_summary_requires_all_utilization_statistics(self):
        rows = [sample(i) for i in range(3)]
        del rows[0]["gpu_percent"]["mean"]
        with self.assertRaisesRegex(ValueError, "gpu_percent.mean"):
            summarize_samples(rows)

    def test_summary_reports_medians_means_peaks_and_counts(self):
        result = summarize_samples([sample(i) for i in range(3)])
        self.assertEqual(result["ttft_ms"]["median"], 51)
        self.assertEqual(result["cpu_percent"]["mean"], 41)
        self.assertEqual(result["gpu_percent"]["peak"], 52)
        self.assertEqual(result["gpu_percent"]["count"], 30)

    def test_terminal_measurement_is_explicit_and_sourced(self):
        result = summarize_samples([], terminal={
            "status": "not-measured: conversion memory gate",
            "evidence": "conversion/conversion-results.json",
        })
        self.assertEqual(result["status"], "not-measured: conversion memory gate")


if __name__ == "__main__":
    unittest.main()
