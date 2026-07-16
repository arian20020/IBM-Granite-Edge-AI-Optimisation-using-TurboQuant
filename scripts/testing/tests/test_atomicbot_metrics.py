import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]


class AtomicBotMetricTests(unittest.TestCase):
    def test_parses_compact_atomicbot_timing(self):
        from scripts.testing.atomicbot.metrics import parse_runtime_metrics

        text = (ROOT / "scripts/testing/tests/fixtures/atomicbot_compact_timing.txt").read_text()
        result = parse_runtime_metrics([
            {"kind": "stderr", "text": text},
            {"kind": "memory", "working_set_bytes": 200 * 1024 * 1024,
             "private_bytes": 180 * 1024 * 1024, "available_ram_bytes": 4 * 1024**3},
            {"kind": "stderr", "text": "llama_kv_cache: size = 12.00 MiB (1024 cells, 4 layers, 1/1 seqs)"},
            {"kind": "first_response_byte", "elapsed_ms": 315.0},
            {"kind": "exit", "exit_code": 0},
        ])
        self.assertEqual(result.prompt_tps, 28.8)
        self.assertEqual(result.decode_tps, 9.4)
        self.assertEqual(result.peak_ram_mb, 200.0)
        self.assertEqual(result.peak_private_mb, 180.0)
        self.assertEqual(result.minimum_available_ram_mb, 4096.0)
        self.assertEqual(result.kv_mb, 12.0)
        self.assertEqual(result.ttft_ms, 315.0)
        self.assertTrue(result.valid)

    def test_ambiguous_timing_invalidates_sample(self):
        from scripts.testing.atomicbot.metrics import parse_runtime_metrics

        result = parse_runtime_metrics([
            {"kind": "stderr", "text": "[ Prompt: 20 t/s | Generation: 8 t/s ]"},
            {"kind": "stderr", "text": "[ Prompt: 30 t/s | Generation: 9 t/s ]"},
            {"kind": "memory", "working_set_bytes": 1, "private_bytes": 1,
             "available_ram_bytes": 1},
            {"kind": "stderr", "text": "KV cache size = 1 MiB"},
            {"kind": "first_response_byte", "elapsed_ms": 1},
            {"kind": "exit", "exit_code": 0},
        ])
        self.assertFalse(result.valid)
        self.assertIn("ambiguous_timing", result.errors)

    def test_aggregate_reports_median_and_range(self):
        from scripts.testing.atomicbot.metrics import RuntimeMetrics, aggregate_samples

        def sample(value):
            return RuntimeMetrics(value, value, value, value, value, value, value,
                                  None, None, True, ())
        aggregate = aggregate_samples([sample(10), sample(30), sample(20)])
        self.assertEqual(aggregate["ttft_ms"], {"median": 20, "min": 10, "max": 30})
        self.assertEqual(aggregate["peak_ram_mb"]["median"], 20)

    def test_gpu_memory_is_explicitly_unavailable_without_collector(self):
        from scripts.testing.atomicbot.metrics import parse_runtime_metrics

        result = parse_runtime_metrics([])
        self.assertEqual(result.gpu_dedicated_mb.status, "N/A")
        self.assertEqual(result.gpu_shared_mb.reason, "collector-unavailable")

    def test_memory_gate_checks_physical_and_commit_headroom(self):
        from scripts.testing.atomicbot.safety import evaluate_memory_gate

        passed = evaluate_memory_gate(4, 10, 9, 2)
        self.assertTrue(passed.allowed)
        physical = evaluate_memory_gate(9, 10, 20, 2)
        self.assertFalse(physical.allowed)
        self.assertEqual(physical.reason, "insufficient-available-ram")
        commit = evaluate_memory_gate(9, 20, 10, 2)
        self.assertFalse(commit.allowed)
        self.assertEqual(commit.reason, "insufficient-commit-headroom")

    def test_server_collector_labels_working_set_private_and_available_ram(self):
        collector = ROOT / "scripts/testing/measure_llama_server.py"
        fake_server = ROOT / "scripts/testing/tests/fixtures/fake_llama_server.py"
        with tempfile.TemporaryDirectory() as directory:
            completed = subprocess.run(
                [sys.executable, str(collector), "--output-dir", directory,
                 "--sample-id", "memory-labels", "--port", "18092",
                 "--prompt", "hello", "--", sys.executable, str(fake_server),
                 "--port", "18092"],
                capture_output=True, text=True, timeout=15,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            events = [json.loads(line) for line in
                      (Path(directory) / "events.jsonl").read_text().splitlines()]
            memory = next(event for event in events if event["kind"] == "memory")
            self.assertGreater(memory["working_set_bytes"], 0)
            self.assertGreater(memory["private_bytes"], 0)
            self.assertGreater(memory["available_ram_bytes"], 0)

    def test_server_collector_timestamps_token_with_empty_decoded_content(self):
        collector = ROOT / "scripts/testing/measure_llama_server.py"
        fake_server = ROOT / "scripts/testing/tests/fixtures/fake_llama_server.py"
        with tempfile.TemporaryDirectory() as directory:
            completed = subprocess.run(
                [sys.executable, str(collector), "--output-dir", directory,
                 "--sample-id", "empty-token", "--port", "18093", "--prompt", "hello",
                 "--", sys.executable, str(fake_server), "--port", "18093",
                 "--empty-content-token"], capture_output=True, text=True, timeout=15,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            result = json.loads((Path(directory) / "measurement.json").read_text())
            self.assertTrue(result["valid"])
            self.assertGreater(result["ttft_ms"], 250)


if __name__ == "__main__":
    unittest.main()
