"""Static trust-boundary checks for the hosted C2 harness workflow."""

from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = (
    REPOSITORY_ROOT / ".github/workflows/workbook-05-phase3-harness-tests.yml"
)


class Phase3HarnessWorkflowContractTests(unittest.TestCase):
    """Keep C2 fixture execution hosted, exact-head, and model-free."""

    def setUp(self) -> None:
        self.text = WORKFLOW_PATH.read_text(encoding="utf-8")

    def test_workflow_has_two_independent_hosted_jobs(self) -> None:
        self.assertIn("produce-fixture-evidence:", self.text)
        self.assertIn("validate-fixture-evidence:", self.text)
        self.assertGreaterEqual(self.text.count("runs-on: windows-latest"), 2)
        self.assertNotIn("self-hosted", self.text)

    def test_workflow_is_read_only_and_exact_head(self) -> None:
        self.assertRegex(self.text, r"permissions:\s*\n\s+contents: read")
        self.assertIn("persist-credentials: false", self.text)
        self.assertIn("github.event.pull_request.head.sha || github.sha", self.text)
        self.assertIn("python-version: '3.12.10'", self.text)

    def test_actions_are_pinned_to_full_commits(self) -> None:
        action_lines = [
            line.strip()
            for line in self.text.splitlines()
            if line.strip().startswith("uses:")
        ]
        self.assertGreaterEqual(len(action_lines), 6)
        for line in action_lines:
            with self.subTest(line=line):
                self.assertRegex(line, r"@[0-9a-f]{40}$")

    def test_fixture_route_contains_no_model_or_external_source(self) -> None:
        lowered = self.text.casefold()
        for forbidden in (
            "huggingface",
            "granite-4.1",
            "c:\\w5a",
            "c:\\w5m",
            "openvino_genai",
            "trust_remote_code",
        ):
            with self.subTest(forbidden=forbidden):
                self.assertNotIn(forbidden, lowered)

    def test_same_attempt_artifact_is_produced_then_downloaded(self) -> None:
        self.assertIn(
            "workbook-05-phase3-harness-fixtures-${{ github.run_id }}-${{ github.run_attempt }}",
            self.text,
        )
        self.assertIn("actions/upload-artifact@", self.text)
        self.assertIn("actions/download-artifact@", self.text)
        self.assertIn("process_bundle_validation", self.text)

    def test_complete_phase3_gate_runs_before_fixture_generation(self) -> None:
        gate_index = self.text.index("Validate-Workbook05-Phase3.ps1")
        fixture_index = self.text.index("Invoke-Workbook05Phase3HarnessFixture.ps1")
        self.assertLess(gate_index, fixture_index)


if __name__ == "__main__":
    unittest.main()
