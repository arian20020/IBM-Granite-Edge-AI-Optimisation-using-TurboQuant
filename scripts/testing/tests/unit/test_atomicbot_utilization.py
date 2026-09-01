import tempfile
import unittest
from pathlib import Path


class AtomicBotUtilizationTests(unittest.TestCase):
    def test_summary_reports_mean_median_peak_and_count(self):
        from scripts.testing.campaigns.atomicbot.utilization import summarize_utilization

        summary = summarize_utilization([
            {"cpu_percent": 10, "gpu_percent": 5},
            {"cpu_percent": 20, "gpu_percent": 15},
            {"cpu_percent": 90, "gpu_percent": 70},
        ])
        self.assertEqual(summary["cpu_percent"], {
            "mean": 40.0, "median": 20.0, "peak": 90.0, "sample_count": 3})
        self.assertEqual(summary["gpu_percent"], {
            "mean": 30.0, "median": 15.0, "peak": 70.0, "sample_count": 3})

    def test_summary_does_not_invent_missing_samples(self):
        from scripts.testing.campaigns.atomicbot.utilization import summarize_utilization

        self.assertEqual(summarize_utilization([]), {
            "cpu_percent": None, "gpu_percent": None})

    def test_formal_aggregate_keeps_bounds_and_source_count(self):
        from scripts.testing.tools.run_atomicbot_server_metrics import aggregate

        samples = []
        for cpu, gpu, count in ((10, 4, 3), (20, 8, 4), (30, 12, 5)):
            samples.append({
                "peak_ram_mb": 100, "kv_mb": 25, "ttft_ms": 50,
                "utilization": {
                    "cpu_percent": {"mean": cpu, "median": cpu, "peak": cpu + 1,
                                    "sample_count": count},
                    "gpu_percent": {"mean": gpu, "median": gpu, "peak": gpu + 1,
                                    "sample_count": count},
                },
            })
        result = aggregate(samples)
        self.assertEqual(result["cpu_percent"], {
            "mean": 20.0, "median": 20.0, "peak": 31, "sample_count": 12})
        self.assertEqual(result["gpu_percent"], {
            "mean": 8.0, "median": 8.0, "peak": 13, "sample_count": 12})
        self.assertLessEqual(result["gpu_percent"]["peak"], 100)

    def test_reconciliation_rejects_missing_gpu_mean(self):
        from scripts.testing.tools.reconcile_atomicbot_all_utilization import validate_summary

        sample = {
            "valid": True, "request_error": None,
            "utilization": {
                "cpu_percent": {"mean": 1, "median": 1, "peak": 1, "sample_count": 1},
                "gpu_percent": {"median": 1, "peak": 1, "sample_count": 1},
            },
        }
        with self.assertRaisesRegex(ValueError, "missing gpu_percent"):
            validate_summary({"test_id": "AB-X", "samples": [sample, sample, sample]})

    def test_reader_ignores_invalid_rows(self):
        from scripts.testing.campaigns.atomicbot.utilization import read_utilization_samples

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "samples.csv"
            path.write_text(
                "timestamp_utc,cpu_percent,gpu_percent,gpu_engine_count\n"
                "2026-07-17T00:00:00Z,12.5,44.25,2\n"
                "bad,row\n", encoding="utf-8")
            self.assertEqual(read_utilization_samples(path), [{
                "timestamp_utc": "2026-07-17T00:00:00Z", "cpu_percent": 12.5,
                "gpu_percent": 44.25, "gpu_engine_count": 2}])

    def test_reader_captures_gpu_memory_when_collector_supplies_it(self):
        from scripts.testing.campaigns.atomicbot.utilization import read_utilization_samples

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "samples.csv"
            path.write_text(
                "timestamp_utc,cpu_percent,gpu_percent,gpu_engine_count,gpu_dedicated_mb,gpu_shared_mb\n"
                "2026-07-18T00:00:00Z,12.5,44.25,2,128.5,64.25\n", encoding="utf-8")
            rows = read_utilization_samples(path)
            self.assertEqual(rows[0]["gpu_dedicated_mb"], 128.5)
            self.assertEqual(rows[0]["gpu_shared_mb"], 64.25)


if __name__ == "__main__":
    unittest.main()
