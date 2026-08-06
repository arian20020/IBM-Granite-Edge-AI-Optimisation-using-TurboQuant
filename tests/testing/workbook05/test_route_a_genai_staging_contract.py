from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1"
)


class RouteAGenAIStagingContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = SCRIPT_PATH.read_text(encoding="utf-8")

    def test_genai_uses_a_fresh_external_workspace(self) -> None:
        self.assertIn("New-Wb05ExternalWorkspace", self.text)
        self.assertIn("-Root $WorkspaceRoot", self.text)
        self.assertIn("-RunIdentity $RunIdentity", self.text)
        self.assertIn("$workDirectory = $workspace.work_directory", self.text)
        self.assertIn("Join-Path $workDirectory 'genai'", self.text)
        self.assertIn("Join-Path $workDirectory 'b-genai'", self.text)
        self.assertIn("Join-Path $workDirectory 'i-genai'", self.text)

    def test_runtime_install_is_not_coupled_to_the_genai_run_identity(self) -> None:
        self.assertNotIn(
            "$expectedRuntimeInstall = Join-Path $workDirectory 'i-ov'",
            self.text,
        )
        self.assertNotIn("Route A Runtime work directory does not exist", self.text)
        self.assertNotIn("exact Route A Runtime install for this run", self.text)

    def test_runtime_install_must_be_an_existing_i_ov_beneath_route_a_root(self) -> None:
        self.assertIn("Assert-Wb05SafePath", self.text)
        self.assertIn("Split-Path -Leaf $resolvedRuntimeInstall", self.text)
        self.assertIn("accepted Route A i-ov directory", self.text)


if __name__ == "__main__":
    unittest.main()
