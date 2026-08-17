from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
WORKFLOW_PATH = (
    REPOSITORY_ROOT
    / ".github"
    / "workflows"
    / "workbook-05-phase3-dependency-preflight.yml"
)
LIVE_SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "Invoke-Workbook05Phase3DependencyPreflightLive.ps1"
)
DEPENDENCY_RUNBOOK_PATH = (
    REPOSITORY_ROOT
    / "docs"
    / "testing"
    / "workbook05"
    / "phase3-dependency-preflight-runbook.md"
)
C1_STATUS_PATH = (
    REPOSITORY_ROOT
    / "docs"
    / "testing"
    / "workbook05"
    / "phase3-c1-implementation-status.md"
)
ASSET_LOCK_RUNBOOK_PATH = (
    REPOSITORY_ROOT
    / "docs"
    / "testing"
    / "workbook05"
    / "phase3-asset-lock-runbook.md"
)


class Phase3DependencyPreflightWorkflowContractTests(unittest.TestCase):
    """Freeze the dedicated hosted → Lenovo → hosted trust boundaries."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = WORKFLOW_PATH.read_text(encoding="utf-8")
        cls.live_script = LIVE_SCRIPT_PATH.read_text(encoding="utf-8")
        cls.dependency_runbook = DEPENDENCY_RUNBOOK_PATH.read_text(
            encoding="utf-8"
        )
        cls.c1_status = C1_STATUS_PATH.read_text(encoding="utf-8")
        cls.asset_lock_runbook = ASSET_LOCK_RUNBOOK_PATH.read_text(
            encoding="utf-8"
        )

    def _job(self, start: str, end: str | None = None) -> str:
        section = self.workflow.split(f"  {start}:", maxsplit=1)[1]
        if end is not None:
            section = section.split(f"  {end}:", maxsplit=1)[0]
        return section

    def test_workflow_has_exact_three_boundaries(self) -> None:
        self.assertIn(
            "name: Workbook 05 Phase 3 dependency preflight",
            self.workflow,
        )
        self.assertIn("  repository-contract:", self.workflow)
        self.assertIn("  collect-dependencies:", self.workflow)
        self.assertIn("  validate-dependencies:", self.workflow)
        self.assertEqual(1, self.workflow.count("  repository-contract:"))
        self.assertEqual(1, self.workflow.count("  collect-dependencies:"))
        self.assertEqual(1, self.workflow.count("  validate-dependencies:"))

    def test_live_collector_requires_manual_main_and_explicit_confirmation(self) -> None:
        collector = self._job("collect-dependencies", "validate-dependencies")
        for token in (
            "github.event_name == 'workflow_dispatch'",
            "github.ref == 'refs/heads/main'",
            "inputs.confirm_live_dependency_preflight == true",
            "needs: repository-contract",
        ):
            self.assertIn(token, collector)
        self.assertNotIn("github.event_name == 'pull_request'", collector)
        self.assertIn("timeout-minutes: 240", collector)

    def test_collector_uses_exact_runner_labels(self) -> None:
        collector = self._job("collect-dependencies", "validate-dependencies")
        for label in (
            "self-hosted",
            "Windows",
            "X64",
            "workbook05",
            "intel-target",
        ):
            self.assertIn(f"- {label}", collector)

    def test_permissions_actions_checkout_and_concurrency_are_fail_closed(self) -> None:
        self.assertRegex(
            self.workflow,
            r"permissions:\s*\n\s+contents: read\s*\n\s+actions: read",
        )
        self.assertNotIn("contents: write", self.workflow)
        self.assertNotIn("pull-requests: write", self.workflow)
        self.assertIn("cancel-in-progress: false", self.workflow)

        actions = re.findall(r"uses:\s*([^\s]+)", self.workflow)
        self.assertTrue(actions)
        for action in actions:
            with self.subTest(action=action):
                self.assertRegex(action, r"^[^@\s]+@[0-9a-f]{40}$")
        self.assertGreaterEqual(
            self.workflow.count("persist-credentials: false"),
            3,
        )
        self.assertGreaterEqual(
            self.workflow.count(
                "ref: ${{ github.event.pull_request.head.sha || github.sha }}"
            ),
            3,
        )

    def test_repository_contract_is_hosted_and_runs_simulations_before_full_gate(self) -> None:
        repository = self._job("repository-contract", "collect-dependencies")
        self.assertIn("runs-on: windows-latest", repository)
        self.assertNotIn("self-hosted", repository)
        simulation = "Run dependency-preflight repository simulations"
        full_gate = "Run the complete Phase 3 repository gate"
        self.assertIn(simulation, repository)
        self.assertIn(
            "Invoke-Phase3DependencyPreflightLiveTests.Tests.ps1",
            repository,
        )
        self.assertIn(full_gate, repository)
        self.assertLess(repository.index(simulation), repository.index(full_gate))

    def test_each_job_installs_only_the_pinned_repository_validator(self) -> None:
        jobs = (
            self._job("repository-contract", "collect-dependencies"),
            self._job("collect-dependencies", "validate-dependencies"),
            self._job("validate-dependencies"),
        )
        for job in jobs:
            with self.subTest(job=job[:100]):
                self.assertIn(
                    "Install pinned Phase 3 validation dependency",
                    job,
                )
                self.assertIn(
                    "scripts/testing/workbook05/requirements.txt",
                    job,
                )
                self.assertIn("--target $dependencyDirectory", job)
                self.assertIn("jsonschema", job)
        self.assertEqual(
            3,
            self.workflow.count(
                "Install pinned Phase 3 validation dependency"
            ),
        )

    def test_collector_calls_exact_live_entry_point_without_operator_controlled_identity(self) -> None:
        collector = self._job("collect-dependencies", "validate-dependencies")
        self.assertIn(
            "Invoke-Workbook05Phase3DependencyPreflightLive.ps1",
            collector,
        )
        self.assertIn("-RepositoryRoot $env:GITHUB_WORKSPACE", collector)
        self.assertIn("-RunId $env:GITHUB_RUN_ID", collector)
        self.assertIn("-RunAttempt ([int]$env:GITHUB_RUN_ATTEMPT)", collector)
        self.assertIn(
            "-BasePythonPath 'C:\\Program Files\\Python312\\python.exe'",
            collector,
        )
        for forbidden in (
            "-SimulationMode",
            "-SimulationRoot",
            "-FailureStage",
            "source_revision",
            "dependency_version",
            "model_path",
            "command_string",
        ):
            self.assertNotIn(forbidden, collector)

    def test_same_attempt_text_only_artifact_is_uploaded_and_validated(self) -> None:
        artifact = (
            "workbook-05-phase3-dependency-preflight-"
            "${{ github.run_id }}-${{ github.run_attempt }}"
        )
        self.assertGreaterEqual(self.workflow.count(artifact), 2)
        collector = self._job("collect-dependencies", "validate-dependencies")
        validator = self._job("validate-dependencies")
        self.assertIn("if: ${{ always() }}", collector)
        self.assertIn("actions/upload-artifact@", collector)
        self.assertIn("actions/download-artifact@", validator)
        self.assertIn(
            "scripts.testing.workbook05.phase3.dependency_bundle_validation",
            validator,
        )
        self.assertIn("--require-passed", validator)
        self.assertNotIn("$bundle | Add-Content -LiteralPath $env:GITHUB_PATH", validator)
        self.assertNotIn("PYTHONPATH=$bundle", validator)

    def test_workflow_contains_no_model_or_repository_write_operation(self) -> None:
        forbidden = (
            "snapshot_download",
            "huggingface-cli download",
            "optimum-cli export",
            "git push",
            "Invoke-Expression",
            "cmd /c",
            "Remove-Item C:\\w5m -Recurse",
            "pagefile",
            "trust-remote-code",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.casefold(), self.workflow.casefold())

    def test_sparse_checkout_contains_every_repository_contract_input(self) -> None:
        for path in (
            ".github/workflows",
            "docs/superpowers",
            "docs/testing",
            "experiments/granite_turboquant_intel/configurations/workbook05",
            "experiments/granite_turboquant_intel/manifests/templates/workbook05",
            "experiments/granite_turboquant_intel/schemas/workbook05",
            "scripts/testing",
            "tests/testing/workbook05",
        ):
            self.assertIn(path, self.workflow)

    def test_live_script_keeps_model_and_scientific_boundaries_closed(self) -> None:
        for token in (
            "model_download_authorised = $false",
            "granite_model_test_authorised = $false",
            "activation_claim_authorised = $false",
            "packed_storage_claim_authorised = $false",
            "performance_claim_authorised = $false",
            "quality_claim_authorised = $false",
        ):
            self.assertIn(token, self.live_script)

    def test_operator_runbook_keeps_live_asset_lock_separate(self) -> None:
        for token in (
            "confirm_live_dependency_preflight",
            "workbook-05-phase3-dependency-preflight-<run-id>-<attempt>",
            "Do not enable `live-asset-lock` from a green workflow screen alone.",
            "all model and scientific authorisation flags as `false`",
        ):
            with self.subTest(token=token):
                self.assertIn(token, self.dependency_runbook)

    def test_c1_status_records_repository_completion_and_live_pending_state(self) -> None:
        for token in (
            "Dependency-preflight repository implementation: **Complete**",
            "Live dependency-preflight acceptance: **Pending**",
            "Live C1 asset locking: **Blocked**",
            "phase3-dependency-preflight-runbook.md",
            "PR `#72`",
        ):
            with self.subTest(token=token):
                self.assertIn(token, self.c1_status)

    def test_asset_lock_runbook_cross_links_preflight_without_opening_live_gate(self) -> None:
        self.assertIn(
            "[clean dependency-preflight runbook](phase3-dependency-preflight-runbook.md)",
            self.asset_lock_runbook,
        )
        self.assertIn(
            "`offline-fixture` is the only permitted operation",
            self.asset_lock_runbook,
        )
        self.assertIn(
            "fails closed if `live-asset-lock` is selected",
            self.asset_lock_runbook,
        )


if __name__ == "__main__":
    unittest.main()
