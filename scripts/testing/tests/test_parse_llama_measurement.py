import importlib.util
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
MODULE_PATH = ROOT / "scripts" / "testing" / "parse_llama_measurement.py"


def load_module():
    spec = importlib.util.spec_from_file_location("parse_llama_measurement", MODULE_PATH)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class MeasurementParserTests(unittest.TestCase):
    def test_summarizes_peak_kv_and_ttft(self):
        parser = load_module()
        events = [
            {"kind": "start", "elapsed_ms": 0},
            {"kind": "memory", "elapsed_ms": 100, "private_bytes": 100_000_000},
            {"kind": "memory", "elapsed_ms": 200, "private_bytes": 250_000_000},
            {
                "kind": "stderr",
                "elapsed_ms": 300,
                "text": "llama_kv_cache: layer 0: dev = CPU, size = 160.00 MiB",
            },
            {"kind": "first_response_byte", "elapsed_ms": 450},
            {"kind": "exit", "elapsed_ms": 900, "exit_code": 0},
        ]
        summary = parser.summarize_measurement(events)
        self.assertAlmostEqual(summary["peak_ram_mb"], 238.418579, places=5)
        self.assertEqual(summary["kv_mb"], 160.0)
        self.assertEqual(summary["ttft_ms"], 450)
        self.assertTrue(summary["valid"])

    def test_sums_multiple_runtime_kv_allocations_without_double_counting(self):
        parser = load_module()
        events = [
            {"kind": "start", "elapsed_ms": 0},
            {"kind": "memory", "elapsed_ms": 1, "private_bytes": 1},
            {"kind": "stderr", "elapsed_ms": 2, "text": "K cache size = 64.00 MiB"},
            {"kind": "stderr", "elapsed_ms": 3, "text": "V cache size = 32.00 MiB"},
            {"kind": "first_response_byte", "elapsed_ms": 4},
            {"kind": "exit", "elapsed_ms": 5, "exit_code": 0},
        ]
        self.assertEqual(parser.summarize_measurement(events)["kv_mb"], 96.0)

    def test_deduplicates_repeated_llama_kv_layer_groups(self):
        parser = load_module()
        events = [
            {"kind": "start", "elapsed_ms": 0},
            {"kind": "memory", "elapsed_ms": 1, "private_bytes": 1},
            {"kind": "stderr", "elapsed_ms": 2, "text": "llama_kv_cache: size = 4.00 MiB (1024 cells, 4 layers, 1/1 seqs)"},
            {"kind": "stderr", "elapsed_ms": 3, "text": "llama_kv_cache: size = 22.00 MiB (1024 cells, 22 layers, 1/1 seqs)"},
            {"kind": "stderr", "elapsed_ms": 4, "text": "llama_kv_cache: size = 4.00 MiB (1024 cells, 4 layers, 1/1 seqs)"},
            {"kind": "stderr", "elapsed_ms": 5, "text": "llama_kv_cache: size = 22.00 MiB (1024 cells, 22 layers, 1/1 seqs)"},
            {"kind": "first_response_byte", "elapsed_ms": 6},
            {"kind": "exit", "elapsed_ms": 7, "exit_code": 0},
        ]
        self.assertEqual(parser.summarize_measurement(events)["kv_mb"], 26.0)

    def test_missing_required_signal_invalidates_sample(self):
        parser = load_module()
        events = [
            {"kind": "start", "elapsed_ms": 0},
            {"kind": "memory", "elapsed_ms": 10, "private_bytes": 10},
            {"kind": "exit", "elapsed_ms": 20, "exit_code": 0},
        ]
        summary = parser.summarize_measurement(events)
        self.assertFalse(summary["valid"])
        self.assertEqual(set(summary["missing"]), {"kv_mb", "ttft_ms"})

    def test_median_uses_three_valid_samples(self):
        parser = load_module()
        samples = [
            {"valid": True, "peak_ram_mb": 120, "kv_mb": 80, "ttft_ms": 300},
            {"valid": True, "peak_ram_mb": 100, "kv_mb": 80, "ttft_ms": 200},
            {"valid": True, "peak_ram_mb": 110, "kv_mb": 80, "ttft_ms": 250},
        ]
        self.assertEqual(
            parser.median_valid(samples),
            {"peak_ram_mb": 110, "kv_mb": 80, "ttft_ms": 250},
        )

    def test_median_rejects_invalid_or_wrong_sample_count(self):
        parser = load_module()
        with self.assertRaisesRegex(ValueError, "exactly three valid"):
            parser.median_valid([{"valid": True}] * 2)
        with self.assertRaisesRegex(ValueError, "exactly three valid"):
            parser.median_valid(
                [
                    {"valid": True},
                    {"valid": False},
                    {"valid": True},
                ]
            )

    def test_launcher_captures_real_memory_kv_and_first_byte(self):
        launcher = ROOT / "scripts" / "testing" / "measure_llama_run.py"
        fixture = (
            ROOT
            / "scripts"
            / "testing"
            / "tests"
            / "fixtures"
            / "measurement_child.py"
        )
        with tempfile.TemporaryDirectory() as output_dir:
            completed = subprocess.run(
                [
                    sys.executable,
                    str(launcher),
                    "--output-dir",
                    output_dir,
                    "--sample-id",
                    "fixture-001",
                    "--",
                    sys.executable,
                    str(fixture),
                ],
                text=True,
                capture_output=True,
                timeout=10,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            summary = json.loads(
                (Path(output_dir) / "measurement.json").read_text(encoding="utf-8")
            )
            self.assertTrue(summary["valid"])
            self.assertGreater(summary["peak_ram_mb"], 1)
            self.assertEqual(summary["kv_mb"], 12.5)
            self.assertGreaterEqual(summary["ttft_ms"], 250)
            self.assertLess(summary["ttft_ms"], 1000)
            self.assertEqual(
                (Path(output_dir) / "stdout.txt").read_text(encoding="utf-8"),
                "Xdone\n",
            )

    def test_launcher_can_measure_first_non_whitespace_byte_after_marker(self):
        launcher = ROOT / "scripts" / "testing" / "measure_llama_run.py"
        with tempfile.TemporaryDirectory() as output_dir:
            marker = Path(output_dir) / "marker.txt"
            marker.write_text("PROMPT", encoding="utf-8")
            command = "import sys,time;sys.stdout.write('banner PROMPT\\n\\n');sys.stdout.flush();time.sleep(.3);sys.stdout.write('R');sys.stdout.flush()"
            completed = subprocess.run(
                [sys.executable, str(launcher), "--output-dir", output_dir,
                 "--sample-id", "marker-001", "--response-after-text-file", str(marker),
                 "--", sys.executable, "-c", command],
                text=True, capture_output=True, timeout=10,
            )
            summary = json.loads((Path(output_dir) / "measurement.json").read_text())
            self.assertGreaterEqual(summary["ttft_ms"], 250)

    def test_launcher_accepts_utf8_bom_environment_json(self):
        launcher = ROOT / "scripts" / "testing" / "measure_llama_run.py"
        with tempfile.TemporaryDirectory() as output_dir:
            environment = Path(output_dir) / "environment.json"
            environment.write_text('{"MEASUREMENT_FIXTURE": "present"}', encoding="utf-8-sig")
            subprocess.run(
                [sys.executable, str(launcher), "--output-dir", output_dir,
                 "--sample-id", "env-001", "--environment-json", str(environment),
                 "--", sys.executable, "-c", "import os;print(os.environ['MEASUREMENT_FIXTURE'])"],
                text=True, capture_output=True, timeout=10,
            )
            self.assertEqual((Path(output_dir) / "stdout.txt").read_text().strip(), "present")

    def test_server_collector_measures_request_to_first_token(self):
        collector = ROOT / "scripts" / "testing" / "measure_llama_server.py"
        fake_server = ROOT / "scripts" / "testing" / "tests" / "fixtures" / "fake_llama_server.py"
        with tempfile.TemporaryDirectory() as output_dir:
            completed = subprocess.run(
                [sys.executable, str(collector), "--output-dir", output_dir,
                 "--sample-id", "server-001", "--port", "18091",
                 "--prompt", "hello", "--", sys.executable, str(fake_server), "--port", "18091"],
                text=True, capture_output=True, timeout=15,
            )
            self.assertEqual(completed.returncode, 0, completed.stderr)
            result = json.loads((Path(output_dir) / "measurement.json").read_text())
            self.assertGreaterEqual(result["ttft_ms"], 250)
            self.assertLess(result["ttft_ms"], 1000)
            self.assertEqual(result["kv_mb"], 12.0)
            self.assertGreater(result["peak_ram_mb"], 1)

    def test_process_tree_snapshot_contains_current_process(self):
        import os
        sys.path.insert(0, str(ROOT / "scripts" / "testing"))
        from measure_llama_run import process_tree_pids
        self.assertIn(os.getpid(), process_tree_pids(os.getpid()))


if __name__ == "__main__":
    unittest.main()
