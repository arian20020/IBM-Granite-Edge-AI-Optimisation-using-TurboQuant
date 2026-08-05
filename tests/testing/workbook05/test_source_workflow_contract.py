from __future__ import annotations

import unittest
from pathlib import Path


# Resolve the repository root from this test file so the contract works in both
# the Intel runner checkout and the independent GitHub-hosted checkout.
ROOT = Path(__file__).resolve().parents[3]
WORKFLOW = ROOT / ".github/workflows/workbook-05-source-admission.yml"

# Keep security-sensitive identifiers in one place so every assertion checks
# the same reviewed branch, runner labels, and immutable Action revisions.
SOURCE_ADMISSION_BRANCH = "testing/workbook-05-source-admission"
CHECKOUT_SHA = "9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0"
SETUP_PYTHON_SHA = "bfe8cc55a7890e3d6672eda6460ef37bfcc70755"
UPLOAD_ARTIFACT_SHA = "bbbca2ddaa5d8feaa63e36b76fdaad77386f024f"
DOWNLOAD_ARTIFACT_SHA = "974686ed5098c7f9c9289ec946b9058e496a2561"
REPOSITORY_GATE = ".\\scripts\\testing\\Validate-Workbook05-SourceAdmission.ps1"


class SourceAdmissionWorkflowContractTests(unittest.TestCase):
    """Protect the dedicated Phase 1 workflow from unsafe drift."""

    def _workflow_text(self) -> str:
        """Read the workflow only after producing a clear missing-file failure."""

        self.assertTrue(
            WORKFLOW.is_file(),
            "The dedicated Workbook 05 source-admission workflow is missing.",
        )
        return WORKFLOW.read_text(encoding="utf-8")

    def _job_section(self, job_id: str, next_job_id: str | None = None) -> str:
        """Return one job block without introducing a YAML dependency."""

        text = self._workflow_text()
        marker = f"  {job_id}:"
        self.assertIn(marker, text)
        section = text.split(marker, 1)[1]
        if next_job_id is not None:
            section = section.split(f"\n  {next_job_id}:", 1)[0]
        return section

    def test_workflow_has_phase_specific_identity_and_inputs(self) -> None:
        """The new workflow must not reuse the generic preflight identity."""

        text = self._workflow_text()
        self.assertIn("name: Workbook 05 source admission", text)
        required_paths = (
            ".github/workflows/workbook-05-source-admission.yml",
            "docs/superpowers/**",
            "docs/testing/**",
            "scripts/testing/**",
            "tests/testing/workbook05/**",
            "experiments/granite_turboquant_intel/configurations/workbook05/**",
            "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1/**",
            "experiments/granite_turboquant_intel/manifests/templates/workbook05/**",
            "experiments/granite_turboquant_intel/prompts/**",
            "experiments/granite_turboquant_intel/rubrics/**",
            "experiments/granite_turboquant_intel/schemas/workbook05/**",
        )
        for required_path in required_paths:
            self.assertIn(f"- '{required_path}'", text)

    def test_permissions_are_read_only_and_concurrency_never_cancels(self) -> None:
        """Evidence collection must not receive repository-write authority."""

        text = self._workflow_text()
        self.assertIn("permissions:\n  contents: read\n  actions: read", text)
        self.assertIn("group: workbook-05-source-admission", text)
        self.assertIn("cancel-in-progress: false", text)
        for forbidden_permission in (
            "contents: write",
            "actions: write",
            "pull-requests: write",
            "issues: write",
            "packages: write",
            "id-token: write",
        ):
            self.assertNotIn(forbidden_permission, text)

    def test_intel_job_is_limited_to_the_reviewed_same_repository_branch(self) -> None:
        """Untrusted forks or unrelated branches must never reach the laptop."""

        job = self._job_section(
            "collect-source-admission",
            "validate-source-admission",
        )
        self.assertIn("github.event_name == 'workflow_dispatch' &&", job)
        self.assertIn("github.ref == 'refs/heads/main'", job)
        self.assertIn(
            f"github.ref == 'refs/heads/{SOURCE_ADMISSION_BRANCH}'",
            job,
        )
        self.assertIn(
            "github.event.pull_request.head.repo.full_name == github.repository",
            job,
        )
        self.assertIn(
            f"github.event.pull_request.head.ref == '{SOURCE_ADMISSION_BRANCH}'",
            job,
        )
        self.assertNotIn("testing/workbook-05-two-route-memory-frontier", job)
        self.assertNotIn("github.event_name == 'workflow_dispatch' ||", job)

    def test_intel_job_uses_exact_labels_and_180_minute_timeout(self) -> None:
        """The scientific collection job must target only the approved machine."""

        job = self._job_section(
            "collect-source-admission",
            "validate-source-admission",
        )
        labels = (
            "self-hosted",
            "Windows",
            "X64",
            "workbook05",
            "intel-target",
        )
        for label in labels:
            self.assertIn(f"- {label}", job)
        self.assertIn("timeout-minutes: 180", job)

    def test_both_jobs_checkout_the_exact_head_with_pinned_actions(self) -> None:
        """Collection and validation must inspect the same immutable revision."""

        text = self._workflow_text()
        checkout_marker = f"uses: actions/checkout@{CHECKOUT_SHA}"
        checkout_sections = text.split(checkout_marker)[1:]
        self.assertEqual(2, len(checkout_sections))

        required_sparse_roots = (
            ".github/workflows",
            "docs/superpowers",
            "docs/testing",
            "experiments/granite_turboquant_intel/configurations/workbook05",
            "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1",
            "experiments/granite_turboquant_intel/manifests/templates/workbook05",
            "experiments/granite_turboquant_intel/prompts",
            "experiments/granite_turboquant_intel/rubrics",
            "experiments/granite_turboquant_intel/schemas/workbook05",
            "scripts/testing",
            "tests/testing/workbook05",
        )
        exact_ref = "ref: ${{ github.event.pull_request.head.sha || github.sha }}"

        for checkout_number, checkout_section in enumerate(
            checkout_sections,
            start=1,
        ):
            checkout_step = checkout_section.split("\n      - name:", 1)[0]
            self.assertIn("persist-credentials: false", checkout_step)
            self.assertIn(exact_ref, checkout_step)
            self.assertIn("sparse-checkout: |", checkout_step)
            for required_root in required_sparse_roots:
                self.assertIn(
                    required_root,
                    checkout_step,
                    f"Checkout {checkout_number} is missing {required_root}.",
                )

        for action in (
            f"actions/setup-python@{SETUP_PYTHON_SHA}",
            f"actions/upload-artifact@{UPLOAD_ARTIFACT_SHA}",
            f"actions/download-artifact@{DOWNLOAD_ARTIFACT_SHA}",
        ):
            self.assertIn(action, text)
        self.assertIn("python-version: '3.12.10'", text)

    def test_repository_phase_gate_runs_before_intel_collection(self) -> None:
        """The laptop must not collect evidence before the indivisible R5 gate."""

        job = self._job_section(
            "collect-source-admission",
            "validate-source-admission",
        )
        gate_position = job.index("- name: Run repository-level Phase 1 gate")
        collection_position = job.index(
            "- name: Collect read-only source-admission evidence"
        )
        self.assertLess(gate_position, collection_position)
        self.assertIn(REPOSITORY_GATE, job)
        self.assertIn(
            "-PythonPath 'C:\\Program Files\\Python312\\python.exe'",
            job,
        )
        self.assertNotIn("- name: Run every Workbook 05 Python test", job)
        self.assertNotIn("- name: Run every Workbook 05 PowerShell test", job)

    def test_collection_uploads_the_exact_artifact_even_for_scientific_blockers(self) -> None:
        """A truthful blocked route remains reviewable instead of disappearing."""

        job = self._job_section(
            "collect-source-admission",
            "validate-source-admission",
        )
        self.assertIn(
            "& '.\\scripts\\testing\\workbook05\\Invoke-Workbook05SourceAdmission.ps1'",
            job,
        )
        self.assertIn(
            "$outputDirectory = Join-Path $env:RUNNER_TEMP "
            "'workbook-05-source-admission'",
            job,
        )
        upload = job.split("- name: Upload source-admission evidence", 1)[1]
        self.assertIn("if: ${{ always() }}", upload)
        self.assertIn(
            "name: workbook-05-source-admission-${{ github.run_id }}-"
            "${{ github.run_attempt }}",
            upload,
        )
        self.assertIn(
            "path: ${{ runner.temp }}/workbook-05-source-admission",
            upload,
        )
        self.assertIn("if-no-files-found: error", upload)

    def test_hosted_job_validates_the_exact_same_attempt_as_untrusted_data(self) -> None:
        """A separate clean runner must validate, not execute, the evidence."""

        job = self._job_section("validate-source-admission")
        self.assertIn("needs: collect-source-admission", job)
        self.assertIn("runs-on: windows-latest", job)
        self.assertIn(
            "if: ${{ always() && "
            "needs.collect-source-admission.result != 'skipped' }}",
            job,
        )
        self.assertIn(
            "name: workbook-05-source-admission-${{ github.run_id }}-"
            "${{ github.run_attempt }}",
            job,
        )
        gate_position = job.index("- name: Run repository-level Phase 1 gate")
        validation_position = job.index(
            "- name: Validate the downloaded source-admission bundle as "
            "untrusted data"
        )
        self.assertLess(gate_position, validation_position)
        self.assertIn(REPOSITORY_GATE, job)
        self.assertIn("-PythonPath 'python'", job)
        self.assertNotIn("- name: Run every Workbook 05 Python test", job)
        self.assertNotIn("- name: Run every Workbook 05 PowerShell test", job)
        self.assertIn(
            "python -m scripts.testing.workbook05."
            "source_admission_bundle_validation",
            job,
        )
        self.assertIn("--bundle-root $bundleRoot", job)
        self.assertIn("--repository-root $env:GITHUB_WORKSPACE", job)
        self.assertIn("--report $reportPath", job)
        self.assertIn("Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY", job)

    def test_workflow_contains_no_repository_write_or_payload_execution_shortcut(self) -> None:
        """The workflow must remain evidence-only and repository read-only."""

        lowered = self._workflow_text().casefold()
        for forbidden in (
            "git push",
            "gh pr create",
            "gh pr edit",
            "create-pull-request",
            "invoke-expression",
            "cmake --build",
            "huggingface.co/",
        ):
            self.assertNotIn(forbidden, lowered)


if __name__ == "__main__":
    unittest.main()
