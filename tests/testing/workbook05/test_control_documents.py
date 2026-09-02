from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]


class ControlDocumentTests(unittest.TestCase):
    def test_spec_names_revision_1_4_as_the_next_wb05_revision(self) -> None:
        spec = (
            REPOSITORY_ROOT
            / "docs/superpowers/specs/2026-08-03-workbook-05-two-route-memory-frontier-design.md"
        ).read_text(encoding="utf-8")

        self.assertIn("Workbook 05 v1.3 remains unchanged", spec)
        self.assertIn("A new controlled v1.4 revision will", spec)
        self.assertNotIn("A new controlled v1.2 revision will", spec)

    def test_decision_log_records_the_two_route_memory_frontier_decision(self) -> None:
        decision_log = (
            REPOSITORY_ROOT / "docs/testing/Decision-Log.md"
        ).read_text(encoding="utf-8")

        self.assertIn("| TD-013 | 2026-08-03 |", decision_log)
        self.assertIn("two separately labelled OpenVINO routes", decision_log)
        self.assertIn("lowest verified KV storage first", decision_log)


if __name__ == "__main__":
    unittest.main()
