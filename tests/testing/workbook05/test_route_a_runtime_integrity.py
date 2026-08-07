from __future__ import annotations

import unittest
from pathlib import Path


# Resolve the repository root from this test file so every integrity check reads
# the actual committed Phase 2 control surface rather than a synthetic fixture.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
RUNTIME_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1"
)
PHASE2_POWERSHELL_FILES = (
    REPOSITORY_ROOT / "scripts/testing/Validate-Workbook05-BuildStage.ps1",
    REPOSITORY_ROOT / "scripts/testing/workbook05/Workbook05.Build.psm1",
    RUNTIME_SCRIPT,
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1",
    REPOSITORY_ROOT / "scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1",
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Write-Workbook05BuildBundleMetadata.ps1",
)


class Phase2PowerShellIntegrityTests(unittest.TestCase):
    """Protect the Phase 2 PowerShell controls from partial/binary writes."""

    def test_phase2_powershell_files_are_strict_utf8_text(self) -> None:
        # Read raw bytes first so an invalid sequence becomes a named assertion
        # failure instead of an opaque setUpClass exception in another test.
        for path in PHASE2_POWERSHELL_FILES:
            with self.subTest(path=path.relative_to(REPOSITORY_ROOT).as_posix()):
                raw = path.read_bytes()
                try:
                    text = raw.decode("utf-8", errors="strict")
                except UnicodeDecodeError as error:
                    self.fail(
                        f"{path.name} is not valid UTF-8 text: "
                        f"invalid byte at offset {error.start}."
                    )

                # Tabs and line endings are the only ASCII control characters
                # permitted in this reviewed text-only source boundary.
                disallowed_controls = [
                    ord(character)
                    for character in text
                    if ord(character) < 32 and character not in "\t\r\n"
                ]
                self.assertEqual([], disallowed_controls)

                # A final newline provides one simple defence against accidental
                # partial writes and keeps Git/text tooling behaviour consistent.
                self.assertTrue(text.endswith("\n"))


class RouteARuntimeScriptIntegrityTests(unittest.TestCase):
    """Protect the Runtime orchestrator from lifecycle truncation."""

    def test_runtime_script_contains_complete_build_lifecycle(self) -> None:
        # Decode strictly because lifecycle assertions are meaningful only after
        # the source has been proved to be ordinary UTF-8 text.
        text = RUNTIME_SCRIPT.read_bytes().decode("utf-8", errors="strict")

        # These markers span source acquisition, configure, build, install,
        # evidence collection, and the final successful build-only decision.
        required_markers = (
            "route-a-runtime-git-fetch",
            "route-a-runtime-git-checkout",
            "route-a-runtime-git-submodules",
            "route-a-runtime-configure",
            "route-a-runtime-build",
            "route-a-runtime-install",
            "cmake-cache-summary.json",
            "dependencies.json",
            "binaries.json",
            "Complete-RouteARuntimeEvidence -Status 'Passed'",
        )
        for marker in required_markers:
            with self.subTest(marker=marker):
                self.assertIn(marker, text)

    def test_command_output_reader_handles_empty_stdout_without_masking_missing_files(
        self,
    ) -> None:
        # Run 31182994474 proved that a successful `git status --porcelain=v1`
        # may leave a readable zero-byte stdout file. The reader must represent
        # that as the empty string while retaining Get-Content's terminating
        # error for a genuinely missing or unreadable evidence file.
        text = RUNTIME_SCRIPT.read_bytes().decode("utf-8", errors="strict")

        # Assert the semantic pieces independently so checkout line-ending policy
        # cannot make this Windows-focused regression test fail spuriously.
        required_tokens = (
            "$capturedOutput = Get-Content",
            "-LiteralPath $Result.stdout_path",
            "-Raw",
            "-ErrorAction Stop",
            "if ($null -eq $capturedOutput)",
            "return ''",
            "return $capturedOutput.Trim()",
        )
        for token in required_tokens:
            with self.subTest(token=token):
                self.assertIn(token, text)

        # Protect against reintroducing the exact one-expression form that threw
        # InvokeMethodOnNull at line 286 in the production Runtime attempt.
        self.assertNotIn(
            "return (Get-Content -LiteralPath $Result.stdout_path -Raw "
            "-ErrorAction Stop).Trim()",
            text,
        )


if __name__ == "__main__":
    unittest.main()
