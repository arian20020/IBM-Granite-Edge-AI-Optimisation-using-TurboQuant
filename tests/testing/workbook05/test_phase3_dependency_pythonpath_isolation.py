from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1"
)
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github/workflows/workbook-05-phase3-dependency-preflight.yml"
)


class Phase3DependencyPythonPathIsolationTests(unittest.TestCase):
    """Keep runner-only validation packages out of the clean final venv."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.script = SCRIPT_PATH.read_text(encoding="utf-8")
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")

    def test_live_collector_clears_workflow_pythonpath_before_processes_start(self) -> None:
        # Live run 32086814542 proved that the workflow intentionally exports a
        # temporary jsonschema directory through PYTHONPATH for repository gates.
        # That directory must not enter the separately qualified bootstrap/final
        # environments, otherwise pip can report a locked package as already
        # satisfied outside the new venv and omit it from the installation report.
        self.assertIn(
            '"PYTHONPATH=$dependencyDirectory" | Add-Content -LiteralPath '
            "$env:GITHUB_ENV",
            self.workflow,
        )

        capture = "$OriginalPythonPath = $env:PYTHONPATH"
        clear = "Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue"
        first_live_stage = "Start-PreflightStage -Stage 'workspace-validation'"

        self.assertIn(capture, self.script)
        self.assertIn(clear, self.script)
        self.assertLess(self.script.index(capture), self.script.index(clear))
        self.assertLess(self.script.index(clear), self.script.index(first_live_stage))

    def test_live_collector_restores_the_callers_pythonpath_in_finally(self) -> None:
        finally_block = self.script.rsplit("finally {", maxsplit=1)[1]
        required = (
            "if ($null -eq $OriginalPythonPath)",
            "Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue",
            "$env:PYTHONPATH = $OriginalPythonPath",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, finally_block)


if __name__ == "__main__":
    unittest.main()
