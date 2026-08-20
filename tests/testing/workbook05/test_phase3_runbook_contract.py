from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
RUNBOOK_PATH = (
    REPOSITORY_ROOT
    / "docs"
    / "testing"
    / "workbook05"
    / "phase3-asset-lock-runbook.md"
)
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github"
    / "workflows"
    / "workbook-05-phase3-assets.yml"
)


class Phase3RunbookContractTests(unittest.TestCase):
    """Prevent operator instructions from drifting past workflow safeguards."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.runbook = RUNBOOK_PATH.read_text(encoding="utf-8")
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

    def test_runbook_names_exact_accepted_phase2_inputs(self) -> None:
        for value in (
            r"C:\w5a\phase2-31391119557-4\i-ov",
            r"C:\w5a\accepted-route-a-runtime-31656417607-1\decision.json",
            r"C:\w5a\phase2-31661571860-1\i-genai",
            r"C:\w5a\accepted-route-a-genai-31661571860-1\decision.json",
            "5dae00b38edb9a20f2d82a99dcf4cd1e2d4e7aeaaf6984873ccc55abccf3ae38",
            "0f273e1f345e1512e8e159af9b83f9ad521a26aa5e0ae813f2599161a5cb4a79",
        ):
            self.assertIn(value, self.runbook)

    def test_runbook_and_workflow_share_exact_manual_inputs(self) -> None:
        for value in (
            "operation",
            "offline-fixture",
            "live-asset-lock",
            "confirm_live_asset_lock",
            "accepted_dependency_preflight_sha256",
        ):
            self.assertIn(value, self.runbook)
            self.assertIn(value, self.workflow)

    def test_runbook_records_the_exact_accepted_dependency_binding(self) -> None:
        for value in (
            "32211117536",
            "9350956534",
            "b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21",
            "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49",
            r"C:\w5c\dependency-preflight-32211117536-1",
            "Verify accepted dependency binding before model access",
        ):
            self.assertIn(value, self.runbook)
        self.assertNotIn("The repository currently keeps this operation blocked", self.runbook)
        self.assertNotIn("Live asset locking is blocked", self.workflow)

    def test_runbook_preserves_exact_stage_order(self) -> None:
        expected = [
            "prerequisite-verification",
            "path-root-verification",
            "disk-preflight",
            "immutable-revision-resolution",
            "source-snapshot-download",
            "source-file-hash-inventory",
            "conversion-new-output-directory",
            "converted-file-hash-inventory",
            "schema-validation",
            "manifest-generation",
        ]
        positions = [self.runbook.index(value) for value in expected]
        self.assertEqual(sorted(positions), positions)

    def test_runbook_prominently_lists_c1_nonclaims(self) -> None:
        nonclaim_section = self.runbook.split("## C1 non-claims", maxsplit=1)[1]
        for value in (
            "model loading",
            "text generation",
            "TurboQuant U3 or U4 activation",
            "QJL or PolarQuant activation",
            "TTFT, TPOT, or throughput",
            "P1–P6 quality",
        ):
            self.assertIn(value, nonclaim_section)


if __name__ == "__main__":
    unittest.main()
