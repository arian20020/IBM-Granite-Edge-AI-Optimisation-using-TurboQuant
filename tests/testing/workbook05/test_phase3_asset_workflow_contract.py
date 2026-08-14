from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = REPOSITORY_ROOT / ".github/workflows/workbook-05-phase3-assets.yml"
GATE_PATH = REPOSITORY_ROOT / "scripts/testing/Validate-Workbook05-Phase3.ps1"
BUILD_WORKFLOW_PATH = REPOSITORY_ROOT / ".github/workflows/build-and-test.yml"


class Phase3AssetWorkflowContractTests(unittest.TestCase):
    def _workflow(self) -> str:
        return WORKFLOW_PATH.read_text(encoding="utf-8")

    def _gate(self) -> str:
        return GATE_PATH.read_text(encoding="utf-8")

    def test_workflow_has_repository_preflight_and_asset_modes(self) -> None:
        text = self._workflow()
        self.assertIn("name: Workbook 05 Phase 3 assets", text)
        self.assertIn("dependency-preflight", text)
        self.assertIn("asset-lock", text)
        self.assertIn("repository-contract:", text)
        self.assertIn("collect-phase3:", text)
        self.assertIn("validate-phase3:", text)

    def test_pull_requests_cannot_reach_the_self_hosted_collector(self) -> None:
        text = self._workflow()
        collector = text.split("  collect-phase3:", 1)[1].split(
            "  validate-phase3:", 1
        )[0]
        self.assertIn("github.event_name == 'workflow_dispatch'", collector)
        self.assertIn("github.ref == 'refs/heads/main'", collector)
        self.assertIn("needs: repository-contract", collector)
        self.assertNotIn("github.event_name == 'pull_request'", collector)

    def test_collector_uses_exact_runner_labels_and_read_only_permissions(self) -> None:
        text = self._workflow()
        for label in (
            "self-hosted",
            "Windows",
            "X64",
            "workbook05",
            "intel-target",
        ):
            self.assertIn(f"- {label}", text)
        self.assertRegex(text, r"permissions:\s*\n\s+contents: read\s*\n\s+actions: read")
        self.assertNotIn("contents: write", text)

    def test_actions_are_pinned_and_checkouts_drop_credentials(self) -> None:
        text = self._workflow()
        uses = re.findall(r"uses:\s*([^\s]+)", text)
        self.assertTrue(uses)
        for action in uses:
            self.assertRegex(action, r"^[^@\s]+@[0-9a-f]{40}$")
        self.assertGreaterEqual(text.count("persist-credentials: false"), 3)
        self.assertIn("ref: ${{ github.sha }}", text)

    def test_same_attempt_artifacts_are_validated_as_data(self) -> None:
        text = self._workflow()
        self.assertIn(
            "workbook-05-phase3-${{ inputs.stage }}-${{ github.run_id }}-${{ github.run_attempt }}",
            text,
        )
        self.assertIn("validate_asset_bundle", text)
        self.assertIn("download-artifact", text)
        self.assertNotIn("Invoke-Expression", text)
        self.assertNotIn("git push", text)
        self.assertNotIn("cmd /c", text.casefold())
        self.assertIn("cancel-in-progress: false", text)

    def test_repository_gate_runs_every_phase3_boundary_before_pass(self) -> None:
        text = self._gate()
        required = (
            "python -m unittest discover",
            "test_phase3_",
            "*.Tests.ps1",
            "git diff --check",
            "safetensors",
            "WORKBOOK05_PHASE3_GATE_PASS",
        )
        for token in required:
            self.assertIn(token, text)
        marker = text.index("WORKBOOK05_PHASE3_GATE_PASS")
        for token in required[:-1]:
            self.assertLess(text.index(token), marker)

    def test_normal_build_sparse_checkout_contains_phase3_contract_inputs(self) -> None:
        text = BUILD_WORKFLOW_PATH.read_text(encoding="utf-8")
        for path in (
            "scripts/testing",
            "tests/testing/workbook05",
            "experiments/granite_turboquant_intel/schemas/workbook05",
            "experiments/granite_turboquant_intel/manifests/templates/workbook05",
            "experiments/granite_turboquant_intel/configurations/workbook05",
            "docs/testing/workbook05",
        ):
            self.assertIn(path, text)


if __name__ == "__main__":
    unittest.main()
