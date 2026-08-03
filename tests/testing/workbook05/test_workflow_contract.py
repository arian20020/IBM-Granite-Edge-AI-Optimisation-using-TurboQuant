from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
WORKFLOW = ROOT / ".github/workflows/workbook-05-preflight.yml"


class WorkflowContractTests(unittest.TestCase):
    def test_workflow_is_read_only_and_targets_the_exact_runner(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("contents: read", text)
        self.assertIn("actions: read", text)
        self.assertNotIn("contents: write", text)
        self.assertNotIn("pull-requests: write", text)
        for label in (
            "self-hosted",
            "Windows",
            "X64",
            "workbook05",
            "intel-target",
        ):
            self.assertIn(f"- {label}", text)
        self.assertIn("testing/workbook-05-two-route-memory-frontier", text)

    def test_actions_are_pinned_to_immutable_shas(self) -> None:
        text = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn(
            "actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0",
            text,
        )
        self.assertIn(
            "actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f",
            text,
        )
        self.assertIn(
            "actions/download-artifact@974686ed5098c7f9c9289ec946b9058e496a2561",
            text,
        )
        self.assertIn(
            "actions/setup-python@bfe8cc55a7890e3d6672eda6460ef37bfcc70755",
            text,
        )

    def test_workflow_contains_no_repository_write_command(self) -> None:
        lowered = WORKFLOW.read_text(encoding="utf-8").lower()
        for forbidden in (
            "git push",
            "gh pr create",
            "gh pr edit",
            "create-pull-request",
        ):
            self.assertNotIn(forbidden, lowered)


if __name__ == "__main__":
    unittest.main()
