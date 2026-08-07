from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = REPOSITORY_ROOT / ".github/workflows/workbook-05-documented-build.yml"
GATE_PATH = REPOSITORY_ROOT / "scripts/testing/Validate-Workbook05-BuildStage.ps1"


class DocumentedBuildWorkflowContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.gate = GATE_PATH.read_text(encoding="utf-8")

    def _job_block(self, job_id: str) -> str:
        # Locate the requested top-level job declaration exactly. Workflow job
        # IDs are indented by two spaces; nested keys use deeper indentation.
        marker = f"  {job_id}:\n"
        start = self.workflow.index(marker)
        content_start = start + len(marker)

        # Match only a later line with exactly two leading spaces and a YAML
        # mapping key. This avoids mistaking nested keys such as `steps:` or
        # `run:` for the next job declaration.
        next_job = re.search(
            r"(?m)^  [A-Za-z0-9_-]+:\s*$",
            self.workflow[content_start:],
        )

        # The final job extends to end-of-file when no later job exists.
        end = (
            len(self.workflow)
            if next_job is None
            else content_start + next_job.start()
        )
        return self.workflow[start:end]

    def test_workflow_has_manual_stage_choices_and_no_privileged_trigger(self) -> None:
        self.assertIn("workflow_dispatch:", self.workflow)
        self.assertIn("stage:", self.workflow)
        for stage in (
            "route-a-runtime",
            "route-a-genai",
            "route-b-runtime",
            "route-b-genai",
        ):
            with self.subTest(stage=stage):
                self.assertIn(f"- {stage}", self.workflow)
        self.assertNotIn("pull_request_target:", self.workflow)

    def test_workflow_permissions_are_read_only(self) -> None:
        self.assertIn("permissions:", self.workflow)
        self.assertIn("contents: read", self.workflow)
        self.assertIn("actions: read", self.workflow)
        for permission in (
            "contents: write",
            "actions: write",
            "pull-requests: write",
            "issues: write",
        ):
            with self.subTest(permission=permission):
                self.assertNotIn(permission, self.workflow)

    def test_actions_are_pinned_and_checkout_drops_credentials(self) -> None:
        uses_lines = [
            line.strip()
            for line in self.workflow.splitlines()
            if "uses:" in line
        ]
        self.assertGreaterEqual(len(uses_lines), 6)
        for line in uses_lines:
            with self.subTest(line=line):
                self.assertRegex(
                    line,
                    r"uses:\s+actions/[A-Za-z0-9_-]+@[0-9a-f]{40}$",
                )
        self.assertIn("persist-credentials: false", self.workflow)

    def test_sparse_checkout_contains_every_full_suite_input_family(self) -> None:
        # The repository gate intentionally runs every Workbook 05 Python test.
        # Therefore every data/document family those tests read must be present in
        # the sparse checkout. Keep this list aligned with the already-proven
        # source-admission workflow rather than silently weakening the full gate.
        required_paths = (
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
        for path in required_paths:
            with self.subTest(path=path):
                self.assertIn(path, self.workflow)

    def test_self_hosted_jobs_use_exact_labels_timeout_and_same_repository_guard(self) -> None:
        self.assertGreaterEqual(
            self.workflow.count(
                "runs-on: [self-hosted, Windows, X64, workbook05, intel-target]"
            ),
            4,
        )
        self.assertGreaterEqual(self.workflow.count("timeout-minutes: 360"), 4)
        self.assertIn(
            "github.repository == 'arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant'",
            self.workflow,
        )
        self.assertIn("github.event_name == 'workflow_dispatch'", self.workflow)
        self.assertIn("cancel-in-progress: false", self.workflow)

    def test_self_hosted_collection_jobs_use_process_scoped_powershell_shell(self) -> None:
        # This is the exact shell already proven by the runner-smoke and
        # preflight workflows on the Lenovo self-hosted runner.
        expected_shell = (
            'shell: powershell -NoLogo -NoProfile -ExecutionPolicy Bypass '
            '-Command ". \'{0}\'"'
        )

        # Every live collection job runs on the same restricted self-hosted
        # Windows runner and therefore needs the process-scoped shell default.
        for job_id in (
            "collect-route-a-runtime",
            "collect-route-a-genai",
            "collect-route-b-runtime",
            "collect-route-b-genai",
        ):
            with self.subTest(job_id=job_id):
                block = self._job_block(job_id)
                self.assertIn("defaults:\n      run:\n        " + expected_shell, block)

                # A step-level built-in shell would override the job default and
                # recreate the exact PSSecurityException seen in run 31177582965.
                self.assertNotIn("\n        shell: powershell\n", block)

    def test_hosted_bundle_validators_receive_temporary_python_dependencies(
        self,
    ) -> None:
        # The repository gate installs jsonschema beneath RUNNER_TEMP and then
        # correctly restores its caller's PYTHONPATH. The later hosted validator
        # step is a new process, so it must explicitly receive that temporary
        # dependency directory rather than relying on cross-step process state.
        expected = (
            "- name: Validate artifact as untrusted data\n"
            "        env:\n"
            "          PYTHONPATH: ${{ runner.temp }}\\workbook05-build-stage-python\n"
        )

        # Every hosted build-bundle validator imports the same Python validation
        # module and therefore shares the same dependency hand-off contract.
        for job_id in (
            "validate-route-a-runtime",
            "validate-route-a-genai",
            "validate-route-b-runtime",
            "validate-route-b-genai",
        ):
            with self.subTest(job_id=job_id):
                block = self._job_block(job_id)
                self.assertIn(expected, block)

    def test_each_stage_runs_gate_collects_text_and_has_hosted_validation(self) -> None:
        expected_jobs = (
            "verify-build-contract:",
            "collect-route-a-runtime:",
            "validate-route-a-runtime:",
            "collect-route-a-genai:",
            "validate-route-a-genai:",
            "collect-route-b-runtime:",
            "validate-route-b-runtime:",
            "collect-route-b-genai:",
            "validate-route-b-genai:",
        )
        for job in expected_jobs:
            with self.subTest(job=job):
                self.assertIn(job, self.workflow)
        self.assertGreaterEqual(
            self.workflow.count("Validate-Workbook05-BuildStage.ps1"),
            5,
        )
        self.assertGreaterEqual(
            self.workflow.count("runs-on: windows-latest"),
            5,
        )
        self.assertIn(
            "scripts.testing.workbook05.build_bundle_validation",
            self.workflow,
        )
        self.assertIn("github.run_id", self.workflow)
        self.assertIn("github.run_attempt", self.workflow)
        self.assertIn("if: always()", self.workflow)
        self.assertIn("retention-days: 30", self.workflow)

    def test_workflow_cannot_run_models_or_upload_binary_payloads(self) -> None:
        forbidden = (
            "huggingface.co",
            ".gguf",
            ".safetensors",
            "benchmark_app",
            "openvino_genai.generate",
            "git push",
            "gh pr merge",
            "merge_pull_request",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.workflow.lower())
        self.assertIn("include-hidden-files: false", self.workflow)

    def test_gate_runs_full_suite_module_import_forbidden_scan_and_diff_check(self) -> None:
        required = (
            "unittest discover -v",
            "tests/testing/workbook05",
            "Import-Module",
            "Workbook05.Build.psm1",
            "test_build_workflow_contract",
            "git diff --check",
            "WORKBOOK05_BUILD_STAGE_GATE_PASS",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.gate)
        self.assertIn("Select-Object -First 1", self.gate)
        self.assertIn("Get-Command", self.gate)

    def test_gate_prints_pass_only_after_every_required_check(self) -> None:
        pass_position = self.gate.index("WORKBOOK05_BUILD_STAGE_GATE_PASS")
        for earlier in (
            "unittest discover -v",
            "Import-Module",
            "test_build_workflow_contract",
            "git diff --check",
        ):
            with self.subTest(earlier=earlier):
                self.assertLess(self.gate.index(earlier), pass_position)

    def test_gate_forbids_model_destructive_and_dynamic_execution(self) -> None:
        forbidden = (
            "Invoke-Expression",
            "git reset --hard",
            "git clean",
            "huggingface.co",
            ".gguf",
            ".safetensors",
            "benchmark_app",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.gate.lower())


if __name__ == "__main__":
    unittest.main()
