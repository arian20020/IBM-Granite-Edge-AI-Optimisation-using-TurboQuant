from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github"
    / "workflows"
    / "workbook-05-phase3-assets.yml"
)
GATE_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "Validate-Workbook05-Phase3.ps1"
)


class Phase3AssetWorkflowContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.gate = GATE_PATH.read_text(encoding="utf-8")

    def _job(self, start: str, end: str | None = None) -> str:
        section = self.workflow.split(f"  {start}:", maxsplit=1)[1]
        if end is not None:
            section = section.split(f"  {end}:", maxsplit=1)[0]
        return section

    def test_workflow_exposes_only_the_staged_safe_operations(self) -> None:
        self.assertIn("name: Workbook 05 Phase 3 assets", self.workflow)
        self.assertIn("offline-fixture", self.workflow)
        self.assertIn("live-asset-lock", self.workflow)
        self.assertNotIn("\n          - dependency-preflight", self.workflow)
        self.assertIn("repository-contract:", self.workflow)
        self.assertIn("collect-assets:", self.workflow)
        self.assertIn("validate-assets:", self.workflow)
        self.assertNotIn(
            "Live asset locking is blocked in this revision",
            self.workflow,
        )
        self.assertIn(
            "Verify accepted dependency binding before model access",
            self.workflow,
        )
        self.assertIn(
            "429b90548ce2b4c8463941c5b2983c8ff0cf6cc3ea3865375c8cae78d7193b49",
            self.workflow,
        )

    def test_pull_requests_cannot_reach_the_self_hosted_collector(self) -> None:
        collector = self._job("collect-assets", "validate-assets")
        self.assertIn(
            "github.event_name == 'workflow_dispatch'",
            collector,
        )
        self.assertIn("github.ref == 'refs/heads/main'", collector)
        self.assertIn("needs: repository-contract", collector)
        self.assertNotIn(
            "github.event_name == 'pull_request'",
            collector,
        )

    def test_collector_uses_exact_runner_labels_and_read_only_permissions(
        self,
    ) -> None:
        collector = self._job("collect-assets", "validate-assets")
        for label in (
            "self-hosted",
            "Windows",
            "X64",
            "workbook05",
            "intel-target",
        ):
            self.assertIn(f"- {label}", collector)
        self.assertRegex(
            self.workflow,
            r"permissions:\s*\n\s+contents: read\s*\n\s+actions: read",
        )
        self.assertNotIn("contents: write", self.workflow)

    def test_actions_are_pinned_and_checkouts_drop_credentials(self) -> None:
        actions = re.findall(r"uses:\s*([^\s]+)", self.workflow)
        self.assertTrue(actions)
        for action in actions:
            self.assertRegex(action, r"^[^@\s]+@[0-9a-f]{40}$")
        self.assertGreaterEqual(
            self.workflow.count("persist-credentials: false"),
            3,
        )
        self.assertIn(
            "ref: ${{ github.event.pull_request.head.sha || github.sha }}",
            self.workflow,
        )

    def test_same_attempt_asset_is_validated_only_as_data(self) -> None:
        artifact = (
            "workbook-05-phase3-assets-${{ github.run_id }}-"
            "${{ github.run_attempt }}"
        )
        self.assertGreaterEqual(self.workflow.count(artifact), 2)
        self.assertIn(
            "scripts.testing.workbook05.phase3.asset_bundle_validation",
            self.workflow,
        )
        self.assertIn("actions/download-artifact@", self.workflow)
        self.assertNotIn("Invoke-Expression", self.workflow)
        self.assertNotIn("git push", self.workflow)
        self.assertNotIn("cmd /c", self.workflow.casefold())
        self.assertIn("cancel-in-progress: false", self.workflow)

    def test_each_job_installs_pinned_validation_dependency_before_use(
        self,
    ) -> None:
        jobs = (
            (
                self._job("repository-contract", "collect-assets"),
                "Run focused dependency-fixture diagnostic",
            ),
            (
                self._job("collect-assets", "validate-assets"),
                "Run the complete Phase 3 repository gate",
            ),
            (
                self._job("validate-assets"),
                "Run the complete Phase 3 repository gate",
            ),
        )

        for job, first_python_use in jobs:
            with self.subTest(first_python_use=first_python_use):
                install_name = "Install pinned Phase 3 validation dependency"
                self.assertIn(install_name, job)
                self.assertIn("$env:RUNNER_TEMP", job)
                self.assertIn("--target $dependencyDirectory", job)
                self.assertIn(
                    "scripts/testing/workbook05/requirements.txt",
                    job,
                )
                self.assertIn(
                    '"PYTHONPATH=$dependencyDirectory"',
                    job,
                )
                self.assertLess(
                    job.index(install_name),
                    job.index(first_python_use),
                )

        # One isolated dependency setup belongs to each job. The package is
        # never installed into the repository or a machine-wide environment.
        self.assertEqual(
            3,
            self.workflow.count(
                "Install pinned Phase 3 validation dependency"
            ),
        )

    def test_hosted_jobs_select_one_exact_python_application(self) -> None:
        hosted_jobs = (
            self._job("repository-contract", "collect-assets"),
            self._job("validate-assets"),
        )
        for job in hosted_jobs:
            with self.subTest(job_start=job[:80]):
                self.assertIn(
                    "Get-Command -Name python -CommandType Application -All",
                    job,
                )
                self.assertIn("Select-Object -First 1", job)
                self.assertIn("$pythonPath = $pythonCommand.Source", job)
                self.assertNotIn(
                    "(Get-Command python -CommandType Application "
                    "-ErrorAction Stop).Source",
                    job,
                )

    def test_repository_contract_runs_bounded_dependency_fixture_diagnostic(
        self,
    ) -> None:
        repository_contract = self._job("repository-contract", "collect-assets")
        diagnostic_name = "Run focused dependency-fixture diagnostic"
        full_gate_name = "Run the complete Phase 3 repository gate"

        self.assertIn(diagnostic_name, repository_contract)
        self.assertIn("timeout-minutes: 5", repository_contract)
        self.assertIn(
            "Invoke-Phase3DependencyPreflightTests.Tests.ps1",
            repository_contract,
        )
        self.assertIn(
            "-RepositoryRoot $env:GITHUB_WORKSPACE",
            repository_contract,
        )
        self.assertIn("-PythonPath 'python'", repository_contract)
        self.assertLess(
            repository_contract.index(diagnostic_name),
            repository_contract.index(full_gate_name),
        )

    def test_repository_gate_delegates_to_complete_existing_gate_first(
        self,
    ) -> None:
        required = (
            "Validate-Workbook05-BuildStage.ps1",
            "& $ExistingGate",
            "git diff --check",
            "safetensors",
            "WORKBOOK05_PHASE3_GATE_PASS",
        )
        for token in required:
            self.assertIn(token, self.gate)
        marker = self.gate.index("WORKBOOK05_PHASE3_GATE_PASS")
        for token in required[:-1]:
            self.assertLess(self.gate.index(token), marker)

    def test_phase3_sparse_checkout_contains_all_contract_inputs(self) -> None:
        for path in (
            ".github/workflows",
            "docs/superpowers",
            "docs/testing",
            "experiments/granite_turboquant_intel/configurations/workbook05",
            "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1",
            "experiments/granite_turboquant_intel/manifests/templates/workbook05",
            "experiments/granite_turboquant_intel/schemas/workbook05",
            "scripts/testing",
            "tests/testing/workbook05",
        ):
            self.assertIn(path, self.workflow)


if __name__ == "__main__":
    unittest.main()
