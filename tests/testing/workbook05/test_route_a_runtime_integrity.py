from __future__ import annotations

import unittest
from pathlib import Path


# Resolve the repository root from this test file so the check always examines
# the real committed Route A Runtime orchestrator rather than a synthetic copy.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
RUNTIME_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1"
)


class RouteARuntimeScriptIntegrityTests(unittest.TestCase):
    """Protect the Runtime orchestrator from truncation or binary corruption."""

    def test_runtime_script_is_strict_utf8_text(self) -> None:
        # Read bytes first so an invalid UTF-8 sequence becomes a clear assertion
        # failure instead of aborting the whole test class during setUpClass.
        raw = RUNTIME_SCRIPT.read_bytes()

        try:
            text = raw.decode("utf-8", errors="strict")
        except UnicodeDecodeError as error:
            self.fail(
                "Route A Runtime script is not valid UTF-8 text: "
                f"invalid byte at offset {error.start}."
            )

        # PowerShell source may contain tabs and normal line endings, but no
        # other ASCII control bytes belong in the reviewed text-only script.
        disallowed_controls = [
            ord(character)
            for character in text
            if ord(character) < 32 and character not in "\t\r\n"
        ]
        self.assertEqual([], disallowed_controls)

    def test_runtime_script_contains_complete_build_lifecycle(self) -> None:
        # Decode strictly again because lifecycle assertions are meaningful only
        # after the source has been proven to be ordinary UTF-8 text.
        text = RUNTIME_SCRIPT.read_bytes().decode("utf-8", errors="strict")

        # These markers span the beginning, middle, and end of the approved Task
        # 4 lifecycle. A file truncated after its header cannot satisfy them.
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

        # Keep the committed script as a conventional complete text file. This
        # also catches an accidental partial write that drops the final newline.
        self.assertTrue(text.endswith("\n"))


if __name__ == "__main__":
    unittest.main()
