from __future__ import annotations

import json
import subprocess
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github"
    / "workflows"
    / "workbook-05-phase3-c1-resume.yml"
)
RESUME_SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "Invoke-Workbook05Phase3AssetLockResume.ps1"
)
RESUME_MODULE_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "phase3"
    / "c1_resume.py"
)
CONTROLLED_PROCESS_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "Workbook05.ControlledProcess.psm1"
)
RUNBOOK_PATH = (
    REPOSITORY_ROOT
    / "docs"
    / "testing"
    / "workbook05"
    / "phase3-asset-lock-runbook.md"
)


class Phase3C1ResumeContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        """Read the reviewed surfaces once for focused static contracts."""

        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.resume_script = RESUME_SCRIPT_PATH.read_text(encoding="utf-8")
        cls.resume_module = RESUME_MODULE_PATH.read_text(encoding="utf-8")
        cls.controlled_process = CONTROLLED_PROCESS_PATH.read_text(encoding="utf-8")
        cls.runbook = RUNBOOK_PATH.read_text(encoding="utf-8")

    def test_dedicated_workflow_is_manual_read_only_and_exactly_bound(self) -> None:
        """Pull requests verify code only; only manual main dispatch reaches Lenovo."""

        workflow = self.workflow
        for required in (
            "name: Workbook 05 Phase 3 C1 controlled source resume",
            "workflow_dispatch:",
            "permissions:",
            "contents: read",
            "actions: read",
            "cancel-in-progress: false",
            "github.event_name == 'workflow_dispatch'",
            "github.ref == 'refs/heads/main'",
            "self-hosted",
            "Windows",
            "X64",
            "workbook05",
            "intel-target",
            "32410714130",
            "workbook-05-phase3-assets-32410714130-1",
            "sha256:33d3ca32236d6da973c21c5d95ba36d088c5ed7384d93eb3f353dbd081671257",
            "80946fc2e06767a8aeff878deab7d31f10fd6676",
            "Assert-Workbook05ArtifactIdentity.ps1",
            "Invoke-Workbook05Phase3AssetLockResume.ps1",
            "c1_resume_bundle_validation",
        ):
            self.assertIn(required, workflow)

        # Pull-request execution must never be able to reach the self-hosted job.
        collector = workflow.index("collect-c1-resume:")
        validator = workflow.index("validate-c1-resume:")
        collector_section = workflow[collector:validator]
        self.assertIn("github.event_name == 'workflow_dispatch'", collector_section)
        self.assertNotIn("pull_request.head", collector_section)

        # Every third-party action must remain pinned to one immutable commit.
        self.assertNotRegex(workflow, r"uses:\s+[^\s]+@(main|master|v\d+)(?:\s|$)")

    def test_resume_script_requalifies_before_conversion_and_never_downloads(self) -> None:
        """Only the retained source is reused; the partial conversion is never reused."""

        script = self.resume_script
        required_order = (
            "prerequisite-verification",
            "prior-artifact-validation",
            "retained-source-requalification",
            "conversion-disk-preflight",
            "resource-preflight",
            "conversion-new-output-directory",
            "converted-file-hash-inventory",
            "schema-validation",
            "manifest-generation",
        )
        positions = []
        for stage in required_order:
            self.assertIn(stage, script)
            positions.append(script.index(stage))
        self.assertEqual(positions, sorted(positions))

        validation_index = script.index("validate-prior-bundle")
        source_index = script.index("rehash-retained-source")
        conversion_index = script.index("$ConversionResult =")
        self.assertLess(validation_index, source_index)
        self.assertLess(source_index, conversion_index)

        for required in (
            r"C:\w5m\sources\granite41-3b-c0650403",
            "c0650403e44e78ec0262dab1c90914c65b196c4e",
            "58e7e6635ac57fbf201e4258cc17c01f9ac04a997bbd9cc5315ec52caba79956",
            "21b6eb2dd3b049017077d62aaab88d38bbc639c6765e4bcb33c2bfc44e463b4e",
            "phase3-assets-$RunId-$RunAttempt-resume",
            "-RequireHighCommitForLowMemoryStop",
            "-LowMemoryCommitPercent 80",
            "-MinimumAvailableMemoryBytes 536870912",
            "HF_HUB_OFFLINE",
            "TRANSFORMERS_OFFLINE",
        ):
            self.assertIn(required, script)

        # Recovery must not contact Hugging Face or reuse/delete the failed output.
        self.assertNotIn("resolve-download", script)
        self.assertNotIn("snapshot_download", script)
        self.assertNotIn("model_info", script)
        self.assertNotIn("--trust-remote-code", script)
        self.assertNotIn("Invoke-Expression", script)
        self.assertNotIn("Remove-Item -Recurse", script)
        self.assertNotIn(
            r"C:\w5m\converted\granite41-3b-int4a-g128-r100-c0650403'",
            script,
        )

    def test_resume_module_and_runbook_keep_every_scientific_claim_closed(self) -> None:
        """C1 recovery qualifies assets only and cannot become inference evidence."""

        for text in (self.resume_module, self.resume_script, self.runbook):
            for flag in (
                "model_execution_authorised",
                "activation_claim_authorised",
                "packed_storage_claim_authorised",
                "performance_claim_authorised",
                "quality_claim_authorised",
            ):
                self.assertIn(flag, text)

        for forbidden in (
            "llama-cli",
            "benchmark prompt",
            "tokens per second",
            "perplexity result",
            "TurboQuant activated",
        ):
            self.assertNotIn(forbidden, self.resume_script)

    def test_pressure_policy_ignores_low_free_ram_when_commit_is_safe(self) -> None:
        """The prior 1.0 GiB/41% observation must not be killed by the resume policy."""

        # Invoke the private pure policy helper inside its module scope. This uses
        # synthetic observations and never starts or terminates a real process.
        script = rf"""
$ErrorActionPreference = 'Stop'
Import-Module '{CONTROLLED_PROCESS_PATH}' -Force
$module = Get-Module Workbook05.ControlledProcess
$result = & $module {{
    Get-Wb05ControlledPressureDecision `
        -AvailableMemoryBytes 1080770560 `
        -CommitPercent 41 `
        -MinimumAvailableMemoryBytes 536870912 `
        -LowMemoryCommitPercent 80 `
        -MaximumCommitPercent 90 `
        -LowMemorySamples 4 `
        -HighCommitSamples 0 `
        -ConsecutiveSafetySamples 5 `
        -SampleIntervalSeconds 2 `
        -RequireHighCommitForLowMemoryStop
}}
$result | ConvertTo-Json -Compress
"""
        completed = subprocess.run(
            [
                "powershell.exe",
                "-NoLogo",
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                script,
            ],
            cwd=REPOSITORY_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )

        self.assertEqual(
            0,
            completed.returncode,
            msg=completed.stdout + completed.stderr,
        )
        result = json.loads(completed.stdout.strip().splitlines()[-1])
        self.assertFalse(result["safety_stop_triggered"])
        self.assertEqual(0, result["low_memory_samples"])
        self.assertEqual(0, result["high_commit_samples"])

    def test_pressure_policy_still_stops_combined_or_excessive_commit_pressure(self) -> None:
        """The safer resume policy remains fail-closed under genuine pressure."""

        cases = (
            # Combined pressure: less than 512 MiB and commit above 80%.
            (400_000_000, 85, 4, 0, "combined memory pressure"),
            # Commit pressure remains an independent hard stop above 90%.
            (2_000_000_000, 91, 0, 4, "commit usage"),
        )
        for available, commit, low_samples, high_samples, expected in cases:
            with self.subTest(expected=expected):
                script = rf"""
$ErrorActionPreference = 'Stop'
Import-Module '{CONTROLLED_PROCESS_PATH}' -Force
$module = Get-Module Workbook05.ControlledProcess
$result = & $module {{
    Get-Wb05ControlledPressureDecision `
        -AvailableMemoryBytes {available} `
        -CommitPercent {commit} `
        -MinimumAvailableMemoryBytes 536870912 `
        -LowMemoryCommitPercent 80 `
        -MaximumCommitPercent 90 `
        -LowMemorySamples {low_samples} `
        -HighCommitSamples {high_samples} `
        -ConsecutiveSafetySamples 5 `
        -SampleIntervalSeconds 2 `
        -RequireHighCommitForLowMemoryStop
}}
$result | ConvertTo-Json -Compress
"""
                completed = subprocess.run(
                    [
                        "powershell.exe",
                        "-NoLogo",
                        "-NoProfile",
                        "-ExecutionPolicy",
                        "Bypass",
                        "-Command",
                        script,
                    ],
                    cwd=REPOSITORY_ROOT,
                    capture_output=True,
                    text=True,
                    check=False,
                )
                self.assertEqual(
                    0,
                    completed.returncode,
                    msg=completed.stdout + completed.stderr,
                )
                result = json.loads(completed.stdout.strip().splitlines()[-1])
                self.assertTrue(result["safety_stop_triggered"])
                self.assertIn(expected, result["safety_stop_reason"])


if __name__ == "__main__":
    unittest.main()
