from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SCRIPT_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "Invoke-Workbook05Phase3DependencyPreflightLive.ps1"
)
NORMAL_INPUT_PATH = (
    REPOSITORY_ROOT
    / "scripts"
    / "testing"
    / "workbook05"
    / "requirements.phase3-assets.normal.in"
)


class Phase3DependencyPreflightLiveContractTests(unittest.TestCase):
    """Keep the live Windows preflight package-only, pinned, and fail closed."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.script = SCRIPT_PATH.read_text(encoding="utf-8")
        cls.normal_input = NORMAL_INPUT_PATH.read_text(encoding="utf-8")

    def test_exact_stage_order_is_declared(self) -> None:
        expected = [
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
        ]
        positions = [self.script.index(f"'{value}'") for value in expected]
        self.assertEqual(sorted(positions), positions)
        for value in expected:
            self.assertIn(f"Start-Stage -Stage '{value}'", self.script)

    def test_exact_vcs_sources_are_immutable(self) -> None:
        for value in (
            "a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0",
            "982e495540364f95da1e4b6f62d2d4e5907d08fd",
            "https://github.com/huggingface/optimum-intel.git",
            "https://github.com/huggingface/optimum.git",
        ):
            self.assertIn(value, self.script)
        self.assertIn("fetch', '--no-tags', '--depth=1", self.script)
        self.assertIn("checkout', '--detach', 'FETCH_HEAD'", self.script)
        self.assertIn("status', '--porcelain=v1'", self.script)
        self.assertIn("ls-files', '--recurse-submodules'", self.script)

    def test_normal_packages_are_hash_locked_and_binary_only(self) -> None:
        self.assertIn("--generate-hashes", self.script)
        self.assertIn("--pip-args=--only-binary=:all:", self.script)
        self.assertIn("--require-hashes", self.script)
        self.assertGreaterEqual(self.script.count("--only-binary=:all:"), 3)
        for value in (
            "transformers==5.5.0",
            "huggingface-hub==1.21.0",
            "nncf==3.2.0",
            "openvino==2026.2.1",
            "openvino-tokenizers==2026.2.1.0",
        ):
            self.assertIn(value, self.normal_input)

    def test_vcs_install_is_local_no_deps_and_no_build_isolation(self) -> None:
        self.assertGreaterEqual(self.script.count("'--no-deps'"), 2)
        self.assertGreaterEqual(self.script.count("'--no-build-isolation'"), 2)
        self.assertIn("$Optimum.root", self.script)
        self.assertIn("$OptimumIntel.root", self.script)
        self.assertIn("'-m', 'pip', 'check'", self.script)

    def test_import_cli_and_no_model_checks_are_required(self) -> None:
        self.assertIn(
            "scripts.testing.workbook05.phase3.dependency_import_check",
            self.script,
        )
        self.assertIn("$OptimumCli", self.script)
        self.assertIn("@('--help')", self.script)
        self.assertIn(
            "tests.testing.workbook05.test_phase3_conversion",
            self.script,
        )
        self.assertIn(
            "tests.testing.workbook05.test_phase3_dependency_lock",
            self.script,
        )

    def test_no_model_or_remote_code_operation_exists(self) -> None:
        lowered = self.script.casefold()
        for forbidden in (
            "granite-4.1-3b",
            "snapshot_download",
            "huggingface_hub",
            "--trust-remote-code",
            "optimum-cli export",
            "git push",
            "invoke-expression",
        ):
            self.assertNotIn(forbidden, lowered)

    def test_manifest_is_last_and_its_logs_are_outside_the_bundle(self) -> None:
        manifest_stage = self.script.index("Start-Stage -Stage 'manifest-generation'")
        manifest_call = self.script.index(
            "'-m', 'scripts.testing.workbook05.hash_manifest'",
            manifest_stage,
        )
        self.assertLess(manifest_stage, manifest_call)
        self.assertIn(
            "$OutOfBundleLogRoot = Join-Path $WorkspaceRoot",
            self.script,
        )
        self.assertIn(
            "-LogPrefix (Join-Path $OutOfBundleLogRoot 'manifest-generation')",
            self.script,
        )
        tail = self.script[manifest_call:]
        self.assertNotIn("Join-Path $LogRoot 'manifest-generation'", tail)

    def test_failure_path_preserves_nonclaims_and_avoids_cleanup(self) -> None:
        catch = self.script.split("catch {", maxsplit=1)[1]
        for flag in (
            "model_download_authorised = $false",
            "granite_model_test_authorised = $false",
            "activation_claim_authorised = $false",
            "packed_storage_claim_authorised = $false",
            "performance_claim_authorised = $false",
            "quality_claim_authorised = $false",
        ):
            self.assertIn(flag, catch)
        self.assertNotRegex(
            catch,
            re.compile(r"Remove-Item.+(?:WorkspaceRoot|SourcesRoot|TargetVenv)"),
        )


if __name__ == "__main__":
    unittest.main()
