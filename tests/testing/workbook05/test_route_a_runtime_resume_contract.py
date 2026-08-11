from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MODULE_PATH = REPOSITORY_ROOT / "scripts/testing/workbook05/Workbook05.Build.psm1"
RUNTIME_RESUME_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeResume.ps1"
)
ARTIFACT_IDENTITY_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Assert-Workbook05ArtifactIdentity.ps1"
)


class BuildProcessDeadlineContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = MODULE_PATH.read_text(encoding="utf-8")

    def test_process_adapter_exposes_and_propagates_controlled_deadline(self) -> None:
        required = (
            "[int]$MaximumElapsedSeconds = 0",
            "-MaximumElapsedSeconds $MaximumElapsedSeconds",
            "controlled elapsed-time boundary",
            "$MaximumElapsedSeconds -lt 0",
            "$MaximumElapsedSeconds -gt 0 -and -not $MonitorResources",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)


class RouteARuntimeResumeContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = RUNTIME_RESUME_SCRIPT.read_text(encoding="utf-8")

    def test_requalifies_exact_source_workspace_cache_and_process_boundary(self) -> None:
        required = (
            "https://github.com/openvinotoolkit/openvino.git",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "docs/dev/build_windows.md",
            "C:\\w5a",
            "[IO.FileAttributes]::ReparsePoint",
            "cmake",
            "MSBuild",
            "cl",
            "ninja",
            "vctip",
            "route_a_runtime_resume_bundle_validation",
            "CMakeCache.txt",
            "ExpectedCacheSha256",
            "Get-FileHash",
            "Get-Wb05CMakeCacheValue",
            "$actualRemote -ne $SourceRepository",
            "$actualHead -ne $SourceCommit",
            "$sourceStatus",
            "recursive submodule provenance is incomplete",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_preserves_reviewed_cache_and_compiler_controls(self) -> None:
        required = (
            "$cacheValues.CMAKE_GENERATOR -eq $Generator",
            "$cacheValues.CMAKE_GENERATOR_PLATFORM -eq 'x64'",
            "$cacheValues.ENABLE_INTEL_CPU -eq 'ON'",
            "$cacheValues.ENABLE_INTEL_GPU -eq 'OFF'",
            "$cacheValues.ENABLE_INTEL_NPU -eq 'OFF'",
            "$cacheValues.ENABLE_TESTS -eq 'OFF'",
            "$cacheValues.ENABLE_FUNCTIONAL_TESTS -eq 'OFF'",
            "$cacheValues.ENABLE_SAMPLES -eq 'OFF'",
            "$cacheValues.ENABLE_PYTHON -eq 'ON'",
            "$cacheValues.ENABLE_WHEEL -eq 'OFF'",
            "$cacheValues.ENABLE_JS -eq 'OFF'",
            "$cacheValues.ENABLE_OV_IR_FRONTEND -eq 'ON'",
            "$cacheValues.ENABLE_OV_ONNX_FRONTEND -eq 'ON'",
            "$cacheValues.ENABLE_OV_PADDLE_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_TF_FRONTEND -eq 'ON'",
            "$cacheValues.ENABLE_OV_TF_LITE_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_PYTORCH_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_JAX_FRONTEND -eq 'OFF'",
            "'--parallel', '1'",
            "'--', '/p:CL_MPCount=1'",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

        self.assertNotIn("'--parallel', '2'", self.text)
        self.assertNotIn("'/p:CL_MPCount=2'", self.text)

    def test_uses_internal_deadline_then_installs_and_checks_handoff(self) -> None:
        required = (
            "[int]$InternalDeadlineSeconds = 39600",
            "$deadlineUtc",
            "MaximumElapsedSeconds",
            "Infrastructure interrupted",
            "'--install'",
            "'--prefix'",
            "runtime/include/openvino/frontend/onnx/extension/conversion.hpp",
            "runtime/include/openvino/frontend/tensorflow/extension/conversion.hpp",
            "dependencies.json",
            "binaries.json",
            "Get-Wb05BinaryRecords",
            "decision.json",
            "granite_model_test_authorised = $false",
            "activation_claim_authorised = $false",
            "packed_storage_claim_authorised = $false",
            "performance_claim_authorised = $false",
            "quality_claim_authorised = $false",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_resume_path_contains_no_destructive_or_model_execution_shortcut(self) -> None:
        forbidden = (
            "Invoke-Expression",
            "git reset --hard",
            "git clean",
            "huggingface.co",
            ".gguf",
            ".safetensors",
            "benchmark_app",
            "generate(",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.text.lower())


class ArtifactIdentityContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = ARTIFACT_IDENTITY_SCRIPT.read_text(encoding="utf-8")

    def test_requires_exact_one_unexpired_digest_and_head_match(self) -> None:
        required = (
            "actions/runs/$RunId/artifacts?per_page=100",
            "$matches.Count -ne 1",
            "$artifact.expired",
            "$ExpectedArtifactDigest -ne $ActualArtifactDigest",
            "$artifact.digest -ne $ExpectedArtifactDigest",
            "$artifact.workflow_run.head_sha -ne $ExpectedHeadSha",
            "WORKBOOK05_ARTIFACT_IDENTITY_ACCEPTED",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_token_is_used_only_for_request_authorization(self) -> None:
        self.assertIn("'Authorization' = \"Bearer $GitHubToken\"", self.text)
        self.assertNotIn("Write-Wb05Json", self.text)
        self.assertNotIn("ConvertTo-Json $GitHubToken", self.text)
        self.assertNotIn("Write-Host $GitHubToken", self.text)


if __name__ == "__main__":
    unittest.main()
