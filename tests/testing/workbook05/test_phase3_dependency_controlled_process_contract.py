from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MODULE_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Workbook05.ControlledProcess.psm1"
)


class DependencyControlledProcessContractTests(unittest.TestCase):
    """Freeze the narrow Task 5 extension before changing the shared adapter."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.text = MODULE_PATH.read_text(encoding="utf-8")

    def test_dependency_component_log_extension_and_atomic_mode_are_declared(self) -> None:
        self.assertRegex(
            self.text,
            re.compile(
                r"\[ValidateSet\([^\]]*'runtime'[^\]]*'genai'[^\]]*"
                r"'dependency-preflight'[^\]]*\)\]\s*"
                r"\[string\]\$Component",
                re.S,
            ),
        )
        self.assertRegex(
            self.text,
            re.compile(
                r"\[ValidateSet\(\s*'log'\s*,\s*'txt'\s*\)\]\s*"
                r"\[string\]\$LogFileExtension\s*=\s*'log'",
                re.S,
            ),
        )
        self.assertIn("[switch]$AtomicJsonEvidence", self.text)

    def test_log_extension_controls_only_stdout_and_stderr_suffixes(self) -> None:
        self.assertIn('$safeId.stdout.$LogFileExtension', self.text)
        self.assertIn('$safeId.stderr.$LogFileExtension', self.text)
        self.assertIn('$safeId.command.json', self.text)
        self.assertIn('$safeId.resources.json', self.text)
        self.assertIn('$safeId.resources.csv', self.text)

    def test_atomic_mode_uses_sibling_temporary_json_without_weakening_defaults(self) -> None:
        required = (
            "function Write-Wb05ControlledJsonEvidence",
            "[IO.File]::Move($temporaryPath, $Path)",
            "Final controlled JSON evidence already exists",
            "Temporary controlled JSON evidence already exists",
            "-Atomic:$AtomicJsonEvidence",
            "[string]$LogFileExtension = 'log'",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

        # Existing Runtime and GenAI callers rely on the default .log suffix and
        # non-atomic shared writer. Task 5 may add options but not alter defaults.
        self.assertNotIn("[string]$LogFileExtension = 'txt'", self.text)
        self.assertNotIn("[switch]$AtomicJsonEvidence = $true", self.text)


if __name__ == "__main__":
    unittest.main()
