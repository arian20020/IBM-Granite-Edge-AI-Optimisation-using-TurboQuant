from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = REPOSITORY_ROOT / ".github/workflows/workbook-05-phase3-assets.yml"
LIVE_SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05Phase3AssetLockLive.ps1"
)
CONTROLLED_PROCESS_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Workbook05.ControlledProcess.psm1"
)


class Phase3LiveAssetLockBindingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.live_script = LIVE_SCRIPT_PATH.read_text(encoding="utf-8")
        cls.controlled_process = CONTROLLED_PROCESS_PATH.read_text(encoding="utf-8")

    def test_workflow_replaces_the_unconditional_block_with_exact_acceptance(self) -> None:
        self.assertNotIn(
            "Live asset locking is blocked in this revision",
            self.workflow,
        )
        self.assertIn(
            "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49",
            self.workflow,
        )
        self.assertIn("Invoke-Workbook05Phase3AssetLockLive.ps1", self.workflow)
        self.assertRegex(
            self.workflow,
            r"if:\s*\$\{\{\s*inputs\.operation\s*==\s*'live-asset-lock'\s*\}\}",
        )
        self.assertRegex(
            self.workflow,
            r"if:\s*\$\{\{\s*inputs\.operation\s*==\s*'offline-fixture'\s*\}\}",
        )

    def test_live_script_verifies_dependency_before_any_model_access(self) -> None:
        verification = self.live_script.index("dependency_acceptance")
        model_resolution = self.live_script.index("immutable-revision-resolution")
        self.assertLess(verification, model_resolution)
        self.assertIn("dependency-preflight-32211117536-1", self.live_script)
        self.assertIn(
            "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49",
            self.live_script,
        )
        self.assertIn("C:\\w5m", self.live_script)
        self.assertIn("C:\\w5c", self.live_script)

    def test_live_script_preserves_the_approved_c1_stage_order(self) -> None:
        stages = (
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
        )
        positions = [self.live_script.index(f"'{stage}'") for stage in stages]
        self.assertEqual(sorted(positions), positions)

    def test_live_path_uses_structured_processes_and_remote_code_stays_disabled(self) -> None:
        lowered = self.live_script.casefold()
        for forbidden in (
            "invoke-expression",
            "cmd /c",
            "--trust-remote-code",
            "remove-item -recurse",
            "iex ",
        ):
            with self.subTest(forbidden=forbidden):
                self.assertNotIn(forbidden, lowered)
        self.assertIn("Invoke-Wb05ControlledLoggedProcess", self.live_script)
        self.assertIn("-ArgumentList", self.live_script)
        self.assertNotRegex(self.live_script, re.compile(r"powershell\s+-command", re.I))

    def test_controlled_process_adapter_admits_only_the_assets_component_addition(self) -> None:
        self.assertIn("'assets'", self.controlled_process)
        self.assertIn("'dependency-preflight'", self.controlled_process)
        self.assertIn("'runtime'", self.controlled_process)
        self.assertIn("'genai'", self.controlled_process)


if __name__ == "__main__":
    unittest.main()
