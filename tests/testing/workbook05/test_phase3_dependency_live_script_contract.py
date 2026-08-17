from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05Phase3DependencyPreflightLive.ps1"
)
DECISION_PATH = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/phase3/dependency_decision.py"
)


class Phase3DependencyLiveScriptContractTests(unittest.TestCase):
    """Freeze Tasks 6–9 before the live collector is introduced."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.script = SCRIPT_PATH.read_text(encoding="utf-8")
        cls.decision = DECISION_PATH.read_text(encoding="utf-8")

    def test_exact_stage_order_and_workspace_identity_are_fixed(self) -> None:
        expected = (
            "workspace-validation",
            "source-verification",
            "lock-generation",
            "normal-install",
            "vcs-install",
            "imports",
            "cli-help",
            "no-model-compatibility",
            "record-generation",
            "manifest-generation",
        )
        positions = [self.script.index(f"'{stage}'") for stage in expected]
        self.assertEqual(sorted(positions), positions)
        self.assertIn('$runIdentity = "dependency-preflight-$RunId-$RunAttempt"', self.script)
        self.assertIn("Join-Path 'C:\\w5c' $runIdentity", self.script)
        self.assertIn("C1 dependency-preflight workspace already exists", self.script)

    def test_exact_reviewed_sources_and_immutable_git_commands_are_present(self) -> None:
        required = (
            "https://github.com/huggingface/optimum.git",
            "982e495540364f95da1e4b6f62d2d4e5907d08fd",
            "https://github.com/huggingface/optimum-intel.git",
            "a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0",
            "remote add origin",
            "fetch --no-tags --depth 1 origin",
            "checkout --detach",
            "remote get-url origin",
            "rev-parse HEAD",
            "status --porcelain --untracked-files=all",
            "ls-files",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.script)

        forbidden = (
            "git reset --hard",
            "git clean",
            "Invoke-Expression",
            "cmd /c",
            "--trust-remote-code",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token, self.script)

    def test_two_environments_hash_locks_and_local_source_installs_are_explicit(self) -> None:
        required = (
            "workspace\\bootstrap-venv",
            "workspace\\environment",
            "requirements.phase3-bootstrap.txt",
            "--require-hashes",
            "--report",
            "piptools",
            "compile",
            "--resolver=backtracking",
            "--generate-hashes",
            "--no-emit-index-url",
            "--no-emit-trusted-host",
            "--no-deps",
            "--no-build-isolation",
            "optimum-intel",
            "optimum",
            "pip check",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.script)

    def test_exact_six_checks_and_no_model_helpers_are_connected(self) -> None:
        expected = (
            "resolver",
            "install",
            "imports",
            "cli_help",
            "no_model_compatibility",
            "remote_code_disabled",
        )
        positions = [self.script.index(f"'{name}'") for name in expected]
        self.assertEqual(sorted(positions), positions)
        required = (
            "dependency_lock_cli",
            "dependency_source_contract_cli",
            "dependency_import_check",
            "dependency_no_model_check",
            "dependency_decision_cli",
            "Invoke-Wb05ControlledLoggedProcess",
            "-Component 'dependency-preflight'",
            "-LogFileExtension 'txt'",
            "-AtomicJsonEvidence",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.script)

    def test_success_and_failure_evidence_are_fail_closed(self) -> None:
        required = (
            "function Write-PreflightAtomicJson",
            ".tmp",
            "[IO.File]::Move($temporaryPath, $Path)",
            "Blocked",
            "IntegrityFailure",
            "InfrastructureInterrupted",
            "first_causal_message",
            "completed_stages",
            "command-index.json",
            "stage-order.json",
            "failure.json",
            "manifest.sha256",
            "decision.status -ne 'Passed'",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.script)
        self.assertLess(
            self.script.index("decision.status -ne 'Passed'"),
            self.script.rindex("manifest.sha256"),
        )

    def test_simulation_seam_is_explicitly_test_only(self) -> None:
        self.assertIn("[switch]$SimulationMode", self.script)
        self.assertIn("[string]$SimulationRoot", self.script)
        self.assertIn("SimulationMode is a repository-test seam only", self.script)
        self.assertIn("dependency_preflight_fixture", self.script)

    def test_live_builder_requires_the_adopted_bootstrap_versions(self) -> None:
        self.assertIn('"pip-tools": "7.6.0"', self.decision)
        self.assertIn('"pip": "26.1.2"', self.decision)
        self.assertIn('LIVE_LOCK_GENERATOR = "pip-tools==7.6.0"', self.decision)
        self.assertNotIn('pip-tools==7.5.0', self.decision)


if __name__ == "__main__":
    unittest.main()
