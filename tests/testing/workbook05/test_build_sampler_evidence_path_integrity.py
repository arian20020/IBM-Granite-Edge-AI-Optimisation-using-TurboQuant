from __future__ import annotations

import re
import unittest
from pathlib import Path


# Resolve the repository files whose exact ordering/contracts failed in the live
# Windows run. These are static integrity tests because the process-exit race is
# nondeterministic; the later Lenovo rerun remains the behavioral proof.
REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MODULE_TEXT = (
    REPOSITORY_ROOT / "scripts/testing/workbook05/Workbook05.Build.psm1"
).read_text(encoding="utf-8", errors="strict")
RUNTIME_TEXT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1"
).read_text(encoding="utf-8", errors="strict")
GENAI_TEXT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1"
).read_text(encoding="utf-8", errors="strict")
ROUTE_B_TEXT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1"
).read_text(encoding="utf-8", errors="strict")


def _function_body(text: str, function_name: str) -> str:
    """Return one PowerShell function body up to the next function declaration."""

    # Locate the requested function first. Failing here means the production
    # contract itself disappeared, which should be visible as a test error.
    start = text.index(f"function {function_name}")

    # Stop at the next top-level PowerShell function declaration so assertions
    # cannot accidentally succeed on tokens that belong to a different helper.
    next_function = re.search(r"(?m)^function\s+", text[start + 1 :])
    if next_function is None:
        return text[start:]
    return text[start : start + 1 + next_function.start()]


class BuildSamplerEvidencePathIntegrityTests(unittest.TestCase):
    def test_sampler_stops_before_sum_when_resolved_process_tree_is_empty(self) -> None:
        # The production race happened after process resolution and before the
        # first aggregate. Require an explicit normal-exit guard in that window.
        processes_start = MODULE_TEXT.index("$processes = @(")
        first_sum = MODULE_TEXT.index(
            "Measure-Object -Property WorkingSet64 -Sum",
            processes_start,
        )
        guarded_window = MODULE_TEXT[processes_start:first_sum]

        # The guard must both test the zero-count condition and terminate the
        # sampling loop before either memory aggregate can be evaluated.
        self.assertIn("if ($processes.Count -eq 0)", guarded_window)
        self.assertRegex(
            guarded_window,
            r"if\s*\(\$processes\.Count\s*-eq\s*0\)\s*\{\s*break\s*\}",
        )

    def test_command_log_references_are_relative_to_the_bundle_root(self) -> None:
        # The shared producer must write portable references from the artifact
        # root, while still storing command files physically under commands/.
        adapter = _function_body(MODULE_TEXT, "Invoke-Wb05LoggedProcess")
        self.assertIn("[string]$EvidenceRoot", adapter)
        self.assertIn(
            "stdout_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stdoutPath",
            adapter,
        )
        self.assertIn(
            "stderr_path = Get-Wb05RelativePath -Root $EvidenceRoot -Path $stderrPath",
            adapter,
        )

        # Every live caller must pass the bundle root explicitly; otherwise one
        # route could silently reintroduce the producer/validator mismatch.
        for name, text in (
            ("runtime", RUNTIME_TEXT),
            ("genai", GENAI_TEXT),
            ("route-b", ROUTE_B_TEXT),
        ):
            with self.subTest(route=name):
                self.assertIn("-EvidenceRoot $OutputDirectory", text)

    def test_genai_and_route_b_preserve_legitimate_empty_stdout(self) -> None:
        # Both later stages perform the same clean git-status read that already
        # failed in Runtime, so they must share the proven null-safe semantics.
        for name, text, function_name in (
            ("genai", GENAI_TEXT, "Read-Result"),
            ("route-b", ROUTE_B_TEXT, "Read-CommandText"),
        ):
            with self.subTest(route=name):
                body = _function_body(text, function_name)

                # Assert the new semantic tokens before indexing them. On the
                # RED head these assertions must fail cleanly rather than raise a
                # ValueError test-harness error for an intentionally missing fix.
                self.assertIn("$capturedOutput = Get-Content", body)
                self.assertIn("if ($null -eq $capturedOutput)", body)
                self.assertIn("return ''", body)
                self.assertIn("return $capturedOutput.Trim()", body)

                # The ordering matters: trimming is legal only after the null
                # representation has been handled explicitly.
                captured = body.index("$capturedOutput = Get-Content")
                null_check = body.index("if ($null -eq $capturedOutput)", captured)
                empty_return = body.index("return ''", null_check)
                trim_return = body.index("return $capturedOutput.Trim()", empty_return)
                self.assertLess(captured, null_check)
                self.assertLess(null_check, empty_return)
                self.assertLess(empty_return, trim_return)

                # Keep the old one-expression form permanently forbidden because
                # it is the exact pattern already proven to fail on the Lenovo.
                self.assertNotRegex(
                    body,
                    r"\(Get-Content[\s\S]*?\)\.Trim\(\)",
                )


if __name__ == "__main__":
    unittest.main()
