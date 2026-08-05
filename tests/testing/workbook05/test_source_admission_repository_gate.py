from __future__ import annotations

import unittest
from pathlib import Path


# Resolve every contract path from the repository rather than the caller's
# working directory so the test behaves identically on both Windows runners.
ROOT = Path(__file__).resolve().parents[3]
GATE = ROOT / "scripts/testing/Validate-Workbook05-SourceAdmission.ps1"
WORKFLOW = ROOT / ".github/workflows/workbook-05-source-admission.yml"


class SourceAdmissionRepositoryGateContractTests(unittest.TestCase):
    """Protect the single deterministic Phase 1 repository gate."""

    def _gate_text(self) -> str:
        """Read the gate only after reporting a clear missing-file failure."""

        self.assertTrue(
            GATE.is_file(),
            "The Workbook 05 source-admission repository gate is missing.",
        )
        return GATE.read_text(encoding="utf-8")

    def _workflow_text(self) -> str:
        """Read the dedicated workflow from the same exact checkout."""

        self.assertTrue(WORKFLOW.is_file())
        return WORKFLOW.read_text(encoding="utf-8")

    def _job_section(self, job_id: str, next_job_id: str | None = None) -> str:
        """Return one workflow job block without adding a YAML dependency."""

        text = self._workflow_text()
        marker = f"  {job_id}:"
        self.assertIn(marker, text)
        section = text.split(marker, 1)[1]
        if next_job_id is not None:
            section = section.split(f"\n  {next_job_id}:", 1)[0]
        return section

    def test_gate_runs_every_required_phase_1_validation(self) -> None:
        """One command must cover every repository-controlled Phase 1 check."""

        text = self._gate_text()
        required_markers = (
            "-m unittest discover",
            "-p 'test_*.py'",
            "Invoke-PreflightModuleTests.ps1",
            "Invoke-SourceAdmissionModuleTests.ps1",
            "Validate-Controlled-Testing-Workspace.ps1",
            "Validate-OpenVINO-Codec-Extension.ps1",
            "test_measurement_controls.py",
            "test_schema_validation.py",
            "test_source_admission_settings.py",
            "test_source_workflow_contract.py",
            "git diff --check",
        )
        for marker in required_markers:
            self.assertIn(marker, text)

    def test_gate_orders_checks_before_its_controlled_pass_line(self) -> None:
        """The final PASS claim must be unreachable until every check succeeds."""

        text = self._gate_text()
        ordered_markers = (
            "-p 'test_*.py'",
            "Invoke-PreflightModuleTests.ps1",
            "Invoke-SourceAdmissionModuleTests.ps1",
            "Validate-Controlled-Testing-Workspace.ps1",
            "Validate-OpenVINO-Codec-Extension.ps1",
            "test_measurement_controls.py",
            "test_schema_validation.py",
            "test_source_admission_settings.py",
            "test_source_workflow_contract.py",
            "git diff --check",
            "WORKBOOK 05 SOURCE-ADMISSION PHASE 1 GATE: PASS",
        )
        positions = [text.index(marker) for marker in ordered_markers]
        self.assertEqual(sorted(positions), positions)
        self.assertEqual(
            1,
            text.count("WORKBOOK 05 SOURCE-ADMISSION PHASE 1 GATE: PASS"),
        )

    def test_gate_checks_native_process_failures_and_avoids_unsafe_execution(self) -> None:
        """A later success must not hide Python or Git failure."""

        text = self._gate_text()
        self.assertIn("$LASTEXITCODE", text)
        self.assertIn("throw", text)
        self.assertIn("Set-StrictMode -Version Latest", text)
        self.assertIn("$ErrorActionPreference = 'Stop'", text)

        lowered = text.casefold()
        for forbidden in (
            "invoke-expression",
            "cmake --build",
            "start-process",
            "huggingface.co/",
            "git push",
        ):
            self.assertNotIn(forbidden, lowered)

    def test_both_workflow_jobs_must_call_the_single_gate(self) -> None:
        """Neither evidence production nor hosted validation may bypass R5."""

        workflow = self._workflow_text()
        gate_path = (
            ".\\scripts\\testing\\Validate-Workbook05-SourceAdmission.ps1"
        )
        self.assertEqual(2, workflow.count(gate_path))

        collector = self._job_section(
            "collect-source-admission",
            "validate-source-admission",
        )
        hosted = self._job_section("validate-source-admission")

        for job in (collector, hosted):
            self.assertIn("- name: Run repository-level Phase 1 gate", job)
            self.assertIn(gate_path, job)
            self.assertNotIn("- name: Run every Workbook 05 Python test", job)
            self.assertNotIn("- name: Run every Workbook 05 PowerShell test", job)

        self.assertLess(
            collector.index("- name: Run repository-level Phase 1 gate"),
            collector.index("- name: Collect read-only source-admission evidence"),
        )
        self.assertLess(
            hosted.index("- name: Run repository-level Phase 1 gate"),
            hosted.index(
                "- name: Validate the downloaded source-admission bundle as "
                "untrusted data"
            ),
        )


if __name__ == "__main__":
    unittest.main()
