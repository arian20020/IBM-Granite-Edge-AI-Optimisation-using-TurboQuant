from __future__ import annotations

import json
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github"
    / "workflows"
    / "workbook-05-phase3-assets.yml"
)
LIVE_SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "Invoke-Workbook05Phase3AssetLockLive.ps1"
)
CONTROLLED_PROCESS_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "Workbook05.ControlledProcess.psm1"
)
ACCEPTANCE_PATH = (
    REPOSITORY_ROOT
    / "experiments"
    / "granite_turboquant_intel"
    / "manifests"
    / "campaigns"
    / "GTQ-WB05-MF-v1"
    / "phase3"
    / "accepted-dependency-preflight.json"
)


class Phase3LiveAssetLockContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.live_script = LIVE_SCRIPT_PATH.read_text(encoding="utf-8")
        cls.controlled_process = CONTROLLED_PROCESS_PATH.read_text(
            encoding="utf-8"
        )

    def test_workflow_no_longer_contains_the_unconditional_live_block(self) -> None:
        self.assertNotIn(
            "Live asset locking is blocked in this revision",
            self.workflow,
        )
        self.assertNotIn(
            "clean Windows dependency-preflight lock has not yet been accepted",
            self.workflow,
        )

    def test_live_operation_verifies_the_committed_acceptance_before_model_access(
        self,
    ) -> None:
        # The workflow invokes the reviewed module; that module owns the exact
        # committed acceptance-record path and fails closed on any identity drift.
        self.assertIn(
            "scripts.testing.workbook05.phase3.dependency_acceptance",
            self.workflow,
        )
        self.assertIn(
            "accepted_dependency_preflight_sha256",
            self.workflow,
        )
        self.assertIn(
            "Verify accepted dependency binding before model access",
            self.workflow,
        )
        self.assertIn(
            "Invoke-Workbook05Phase3AssetLockLive.ps1",
            self.workflow,
        )

        verify_index = self.workflow.index(
            "Verify accepted dependency binding before model access"
        )
        live_index = self.workflow.index(
            "Invoke-Workbook05Phase3AssetLockLive.ps1"
        )
        self.assertLess(verify_index, live_index)

    def test_offline_fixture_remains_network_and_model_free(self) -> None:
        self.assertIn(
            "Invoke-Workbook05Phase3AssetLock.ps1",
            self.workflow,
        )
        self.assertIn("-OfflineFixtureMode", self.workflow)
        self.assertIn("operation == 'offline-fixture'", self.workflow)

    def test_live_script_exists_and_preserves_the_approved_c1_order(self) -> None:
        script = self.live_script
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
        positions = []
        for stage in stages:
            self.assertIn(stage, script)
            positions.append(script.index(stage))
        self.assertEqual(positions, sorted(positions))

        dependency_verification = script.index("dependency_acceptance")
        model_root_access = script.index(
            "$ModelRoot = Assert-NormalDirectory -Path 'C:\\w5m'"
        )
        self.assertLess(dependency_verification, model_root_access)
        self.assertIn("Invoke-Wb05ControlledLoggedProcess", script)
        self.assertIn("-Component 'assets'", script)
        self.assertIn("live_asset_lock", script)
        self.assertNotIn("--trust-remote-code", script)
        self.assertNotIn("Invoke-Expression", script)
        self.assertNotIn("cmd /c", script.casefold())
        self.assertNotIn("Remove-Item -Recurse", script)

    def test_record_materialisation_restores_only_the_repository_validator(self) -> None:
        """Model work uses the accepted venv; schema records use hosted validators."""

        script = self.live_script
        record_start = script.index("$RecordResult =")
        record_end = script.index(
            "if ($RecordResult.record.exit_code -ne 0)",
            record_start,
        )
        record_section = script[record_start:record_end]
        record_prefix = script[max(0, record_start - 700):record_start]
        record_suffix = script[record_end:record_end + 700]

        self.assertIn("$env:PYTHONPATH = $OriginalPythonPath", record_prefix)
        self.assertIn("-FilePath $BasePythonPath", record_section)
        self.assertNotIn("-FilePath $AcceptedPythonPath", record_section)
        self.assertIn(
            "Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue",
            record_suffix,
        )

    def test_controlled_process_adapter_admits_the_assets_component(self) -> None:
        flattened = self.controlled_process.replace("\n", " ")
        for component in (
            "'runtime'",
            "'genai'",
            "'dependency-preflight'",
            "'assets'",
        ):
            self.assertIn(component, flattened)

    def test_committed_acceptance_is_exactly_the_successful_run(self) -> None:
        self.assertTrue(
            ACCEPTANCE_PATH.is_file(),
            "The independently verified dependency acceptance is not committed.",
        )
        record = json.loads(ACCEPTANCE_PATH.read_text(encoding="utf-8"))
        self.assertEqual("32211117536", record["workflow_run_id"])
        self.assertEqual(1, record["run_attempt"])
        self.assertEqual(
            "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49",
            record["decision_sha256"],
        )
        self.assertEqual(
            "b68a4f8af8c57a9f5d71347d2485855796f4d0dce0291c50b596512c309c0c21",
            record["independent_artifact_sha256"],
        )


if __name__ == "__main__":
    unittest.main()
