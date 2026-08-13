from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
RUNTIME_RESUME_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeResume.ps1"
)


class RouteARuntimeResumeModuleArgumentTests(unittest.TestCase):
    def test_prerequisite_validator_module_name_is_one_python_argument(self) -> None:
        text = RUNTIME_RESUME_SCRIPT.read_text(encoding="utf-8")

        # Python's ``-m`` option must receive one complete module name. Splitting
        # this name across PowerShell array expressions makes Python import only
        # ``scripts.testing.workbook05.`` and fail before validating the bundle.
        expected_module_argument = (
            "'scripts.testing.workbook05."
            "route_a_runtime_resume_bundle_validation'"
        )

        self.assertIn(expected_module_argument, text)
        self.assertNotIn("'scripts.testing.workbook05.' +", text)


if __name__ == "__main__":
    unittest.main()
