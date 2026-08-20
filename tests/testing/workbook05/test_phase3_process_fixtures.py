"""Behavioral contracts for deterministic C2 child-process fixtures."""

from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
FIXTURE_ROOT = REPOSITORY_ROOT / "tests/testing/workbook05/fixtures/phase3/process"


class Phase3ProcessFixtureTests(unittest.TestCase):
    """Require safe, bounded fixtures for normal and failure paths."""

    def _run(
        self,
        filename: str,
        workspace: Path,
        *extra: str,
        timeout: int = 15,
    ) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [
                sys.executable,
                str(FIXTURE_ROOT / filename),
                "--workspace",
                str(workspace),
                *extra,
            ],
            check=False,
            capture_output=True,
            text=True,
            timeout=timeout,
        )

    def test_normal_fixture_preserves_output_and_argument_boundaries(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            workspace = Path(directory)
            completed = self._run(
                "normal_child.py",
                workspace,
                "--echo-arg",
                "value with spaces",
                "--echo-arg",
                'quote"inside',
                "--echo-arg",
                "",
                "--echo-arg",
                "trailing\\",
            )

            self.assertEqual(0, completed.returncode, completed.stderr)
            self.assertIn("normal-child-stdout", completed.stdout)
            self.assertEqual("NORMAL_OUTPUT\n", (workspace / "raw.txt").read_text())
            payload = json.loads((workspace / "received-arguments.json").read_text())
            self.assertEqual(
                ["value with spaces", 'quote"inside', "", "trailing\\"],
                payload["values"],
            )
            self.assertTrue((workspace / "heartbeat.txt").is_file())

    def test_event_fixture_emits_first_token_boundary(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            workspace = Path(directory)
            completed = self._run("event_stream_child.py", workspace)
            self.assertEqual(0, completed.returncode, completed.stderr)
            events = [
                json.loads(line)
                for line in (workspace / "events.jsonl").read_text().splitlines()
            ]
            self.assertEqual(
                ["started", "first_token", "token", "completed"],
                [event["event"] for event in events],
            )

    def test_stderr_fixture_uses_stderr_and_nonzero_exit(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            completed = self._run("stderr_child.py", Path(directory))
            self.assertEqual(7, completed.returncode)
            self.assertEqual("deterministic-stderr\n", completed.stderr)

    def test_fail_once_fixture_has_exactly_one_transient_failure(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            workspace = Path(directory)
            first = self._run("fail_once_child.py", workspace)
            second = self._run("fail_once_child.py", workspace)
            third = self._run("fail_once_child.py", workspace)

            self.assertEqual(75, first.returncode)
            self.assertEqual(0, second.returncode)
            self.assertEqual(0, third.returncode)

    def test_malformed_event_fixture_exits_zero_but_writes_invalid_jsonl(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            workspace = Path(directory)
            completed = self._run("malformed_event_child.py", workspace)
            self.assertEqual(0, completed.returncode, completed.stderr)
            with self.assertRaises(json.JSONDecodeError):
                json.loads((workspace / "events.jsonl").read_text().splitlines()[0])

    def test_heartbeat_fixture_supports_update_and_stall_modes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            update_workspace = Path(directory) / "update"
            stall_workspace = Path(directory) / "stall"
            update_workspace.mkdir()
            stall_workspace.mkdir()

            updated = self._run(
                "heartbeat_child.py",
                update_workspace,
                "--mode",
                "update",
                "--duration-seconds",
                "1",
            )
            stalled = self._run(
                "heartbeat_child.py",
                stall_workspace,
                "--mode",
                "stall",
                "--duration-seconds",
                "1",
            )

            self.assertEqual(0, updated.returncode)
            self.assertEqual(0, stalled.returncode)
            self.assertGreater(
                (update_workspace / "heartbeat.txt").stat().st_mtime_ns,
                (stall_workspace / "heartbeat.txt").stat().st_mtime_ns,
            )


if __name__ == "__main__":
    unittest.main()
