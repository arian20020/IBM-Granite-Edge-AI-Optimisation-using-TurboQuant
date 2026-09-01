import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[4]
MATRIX = ROOT / "experiments/manifests/atomicbot-turboquant/retest-matrix.json"


class AtomicBotRunnerTests(unittest.TestCase):
    def test_select_cases_supports_only_from_and_skip(self):
        from scripts.testing.campaigns.atomicbot.matrix import load_matrix
        from scripts.testing.campaigns.atomicbot.runner import select_cases

        cases = load_matrix(MATRIX)
        self.assertEqual([case.test_id for case in select_cases(cases, only={"AB-02"})], ["AB-02"])
        selected = select_cases(cases, start_at="AB-14", skip={"AB-15"})
        self.assertEqual([case.test_id for case in selected], ["AB-14", "AB-15M"])

    def test_resume_skips_only_reconciled_complete_rows(self):
        from scripts.testing.campaigns.atomicbot.matrix import load_matrix
        from scripts.testing.campaigns.atomicbot.runner import select_cases

        cases = load_matrix(MATRIX)
        state = {"attempts": {
            "AB-01": {"status": "complete", "reconciled": True},
            "AB-02": {"status": "complete", "reconciled": False},
        }}
        ids = [case.test_id for case in select_cases(cases, resume_state=state)]
        self.assertNotIn("AB-01", ids)
        self.assertIn("AB-02", ids)

    def test_run_ids_increment_without_collision(self):
        from scripts.testing.campaigns.atomicbot.runner import next_run_id

        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "AB-01-20260716-R0001").mkdir()
            self.assertEqual(next_run_id("AB-01", "20260716", root), "AB-01-20260716-R0002")

    def test_completed_attempt_requires_replace_flag(self):
        from scripts.testing.campaigns.atomicbot.runner import ensure_replace_allowed

        completed = {"status": "complete", "reconciled": True}
        with self.assertRaisesRegex(ValueError, "replace-attempt"):
            ensure_replace_allowed(completed, replace_attempt=False)
        ensure_replace_allowed(completed, replace_attempt=True)

    def test_cli_dry_run_launches_no_runtime_and_lists_exact_selection(self):
        with tempfile.TemporaryDirectory() as directory:
            marker = Path(directory) / "should-not-exist"
            command = [sys.executable, str(ROOT / "scripts/testing/tools/run_atomicbot_retest.py"),
                       "--matrix", str(MATRIX), "--dry-run", "--only", "AB-01"]
            completed = subprocess.run(command, capture_output=True, text=True, timeout=10,
                                       env={**__import__("os").environ, "ATOMICBOT_TEST_MARKER": str(marker)})
            self.assertEqual(completed.returncode, 0, completed.stderr)
            payload = json.loads(completed.stdout)
            self.assertEqual([case["test_id"] for case in payload["cases"]], ["AB-01"])
            self.assertFalse(marker.exists())

    def test_timed_command_kills_process_tree(self):
        from scripts.testing.campaigns.atomicbot.runner import run_timed_command

        fixture = ROOT / "scripts/testing/tests/fixtures/fake_atomicbot_runtime.py"
        with tempfile.TemporaryDirectory() as directory:
            result = run_timed_command(
                [sys.executable, str(fixture), "--mode", "child"],
                Path(directory), timeout_seconds=0.5,
            )
            self.assertEqual(result["status"], "timeout")
            self.assertTrue(result["cleanup_attempted"])
            self.assertTrue((Path(directory) / "command.json").exists())

    def test_server_command_uses_case_cache_context_and_backend(self):
        from scripts.testing.campaigns.atomicbot.matrix import TestCase
        from scripts.testing.campaigns.atomicbot.runner import build_server_command

        case = TestCase("AB-12", "granite-4.1-3b", "MODEL", "turbo3",
                        "vulkan-partial", 4096, "none", "turbo3", False,
                        ("ttft_ms",))
        command = build_server_command(case, Path("llama-server.exe"),
                                       Path("model.gguf"), 18112)
        self.assertEqual(command[command.index("-ctk") + 1], "turbo3")
        self.assertEqual(command[command.index("-ctv") + 1], "turbo3")
        self.assertEqual(command[command.index("-c") + 1], "4096")
        self.assertEqual(command[command.index("-ngl") + 1], "1")
        self.assertIn("--no-webui", command)

    def test_server_metric_resume_reuses_only_valid_measurement(self):
        from scripts.testing.tools.run_atomicbot_server_metrics import read_valid_measurement

        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "measurement.json"
            path.write_text('{"valid": false, "request_error": "interrupted"}')
            self.assertIsNone(read_valid_measurement(path))
            path.write_text('{"valid": true, "ttft_ms": 10}')
            self.assertEqual(read_valid_measurement(path)["ttft_ms"], 10)


if __name__ == "__main__":
    unittest.main()
