from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
GATE_PATH = REPOSITORY_ROOT / "scripts/testing/Validate-Workbook05-BuildStage.ps1"


class BuildGateDependencyContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = GATE_PATH.read_text(encoding="utf-8")

    def test_gate_installs_only_pinned_requirements_into_runner_temp(self) -> None:
        required = (
            "scripts/testing/workbook05/requirements.txt",
            "--disable-pip-version-check",
            "--target",
            "workbook05-build-stage-python",
            "$env:RUNNER_TEMP",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)
        self.assertNotIn("pip install --upgrade", self.text.lower())

    def test_gate_restores_pythonpath_after_validation(self) -> None:
        self.assertIn("$originalPythonPath", self.text)
        self.assertIn("finally", self.text)
        self.assertIn("$env:PYTHONPATH = $originalPythonPath", self.text)
        self.assertIn("Remove-Item -Path 'Env:PYTHONPATH'", self.text)


if __name__ == "__main__":
    unittest.main()
