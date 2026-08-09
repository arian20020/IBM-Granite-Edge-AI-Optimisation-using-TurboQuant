from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MODULE_PATH = REPOSITORY_ROOT / "scripts/testing/workbook05/Workbook05.Build.psm1"
RUNTIME_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1"
)
GENAI_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1"
)


class BuildPowerShellContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = MODULE_PATH.read_text(encoding="utf-8")

    def test_exports_reviewed_functions(self) -> None:
        expected = {
            "New-Wb05ExternalWorkspace",
            "Invoke-Wb05LoggedProcess",
            "Start-Wb05ResourceSampler",
            "Stop-Wb05ResourceSampler",
            "Write-Wb05Json",
            "Write-Wb05Manifest",
            "Assert-Wb05SafePath",
            "Test-Wb05SameWindowsPath",
            "Get-Wb05CMakeCacheValue",
            "Get-Wb05BinaryRecords",
            "Restore-Wb05Environment",
        }
        match = re.search(
            r"Export-ModuleMember\s+-Function\s+@\((.*?)\)",
            self.text,
            re.S,
        )
        self.assertIsNotNone(match)
        self.assertEqual(expected, set(re.findall(r"'([^']+)'", match.group(1))))

    def test_process_adapter_and_resource_controls_are_present(self) -> None:
        required = (
            "System.Diagnostics.ProcessStartInfo",
            "[string[]]$ArgumentList",
            "ConvertTo-Wb05WindowsCommandLineArgument",
            "$processStartInfo.Arguments",
            "ReadToEndAsync()",
            "[int]$SampleIntervalSeconds = 2",
            "[int]$ConsecutiveSafetySamples = 5",
            "1610612736",
            "[double]$MaximumCommitPercent = 90",
            "[int]$HeartbeatTimeoutSeconds = 900",
            "peak_working_set_bytes",
            "peak_private_bytes",
            "process_tree_ids",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_workspace_environment_and_binary_boundaries_are_fail_closed(self) -> None:
        required = (
            "[IO.FileAttributes]::ReparsePoint",
            "External run directory already exists",
            'Remove-Item -Path "Env:$name"',
            'Set-Item -Path "Env:$name"',
            "copied_to_artifact = $false",
            "Get-FileHash",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

        forbidden = (
            "Invoke-Expression",
            "git reset --hard",
            "git clean",
            "config --global core.longpaths",
            "Set-ItemProperty HKLM:",
            "New-ItemProperty HKLM:",
            "Copy-Item",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.text.lower())


class RouteARuntimeBuildContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = RUNTIME_SCRIPT.read_text(encoding="utf-8")

    def test_pins_source_toolchain_paths_and_configure_controls(self) -> None:
        required = (
            "https://github.com/openvinotoolkit/openvino.git",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "docs/dev/build_windows.md",
            "C:\\w5a",
            "Visual Studio 17 2022",
            "Python 3.12.10",
            "Join-Path $workspace.work_directory 'ov'",
            "Join-Path $workspace.work_directory 'b-ov'",
            "Join-Path $workspace.work_directory 'i-ov'",
            "'-G', $Generator",
            "'-A', 'x64'",
            "'-DENABLE_INTEL_CPU=ON'",
            "'-DENABLE_OV_IR_FRONTEND=ON'",
            "'-DENABLE_OV_ONNX_FRONTEND=ON'",
            "'-DENABLE_OV_PADDLE_FRONTEND=OFF'",
            "'-DENABLE_OV_TF_FRONTEND=ON'",
            "'-DENABLE_OV_TF_LITE_FRONTEND=OFF'",
            "'-DENABLE_OV_PYTORCH_FRONTEND=OFF'",
            "'-DENABLE_OV_JAX_FRONTEND=OFF'",
            "'-DENABLE_INTEL_GPU=OFF'",
            "'-DENABLE_INTEL_NPU=OFF'",
            "'-DENABLE_TESTS=OFF'",
            "'-DENABLE_FUNCTIONAL_TESTS=OFF'",
            "'-DENABLE_SAMPLES=OFF'",
            "'-DENABLE_JS=OFF'",
            "'-DENABLE_PYTHON=ON'",
            "'-DENABLE_WHEEL=OFF'",
            "parallelism = 1",
            "'--parallel', '1'",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

        # The Runtime hand-off must keep unrelated frontends and samples disabled,
        # while retaining the conservative one-job build boundary.
        self.assertNotIn("'-DENABLE_SAMPLES=ON'", self.text)
        self.assertNotIn("'-DENABLE_OV_ONNX_FRONTEND=OFF'", self.text)
        self.assertNotIn("'-DENABLE_OV_TF_FRONTEND=OFF'", self.text)
        self.assertNotIn("parallelism = 2", self.text)
        self.assertNotIn("'--parallel', '2'", self.text)

    def test_requires_materialized_runtime_cache_controls(self) -> None:
        required_cache_checks = (
            "$cacheValues.ENABLE_INTEL_CPU -eq 'ON'",
            "$cacheValues.ENABLE_OV_IR_FRONTEND -eq 'ON'",
            "$cacheValues.ENABLE_OV_ONNX_FRONTEND -eq 'ON'",
            "$cacheValues.ENABLE_OV_PADDLE_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_TF_FRONTEND -eq 'ON'",
            "$cacheValues.ENABLE_OV_TF_LITE_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_PYTORCH_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_OV_JAX_FRONTEND -eq 'OFF'",
            "$cacheValues.ENABLE_INTEL_GPU -eq 'OFF'",
            "$cacheValues.ENABLE_INTEL_NPU -eq 'OFF'",
            "$cacheValues.ENABLE_TESTS -eq 'OFF'",
            "$cacheValues.ENABLE_FUNCTIONAL_TESTS -eq 'OFF'",
            "$cacheValues.ENABLE_SAMPLES -eq 'OFF'",
            "$cacheValues.ENABLE_JS -eq 'OFF'",
            "$cacheValues.ENABLE_PYTHON -eq 'ON'",
            "$cacheValues.ENABLE_WHEEL -eq 'OFF'",
            "$null -eq $cacheValues.ENABLE_SYSTEM_PROTOBUF",
            "$cacheValues.ENABLE_SYSTEM_PROTOBUF -eq 'OFF'",
        )
        for token in required_cache_checks:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_runtime_install_requires_genai_tokenizer_frontend_headers(self) -> None:
        required = (
            "$RequiredGenAIFrontendHeaders",
            "runtime/include/openvino/frontend/onnx/extension/conversion.hpp",
            "runtime/include/openvino/frontend/tensorflow/extension/conversion.hpp",
            "$missingGenAIFrontendHeaders",
            "Route A Runtime install is missing OpenVINO frontend headers required by the pinned GenAI tokenizer submodule",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_records_build_evidence_and_disables_later_claims(self) -> None:
        required = (
            "environment.json",
            "source-provenance.json",
            "cmake-cache-summary.json",
            "dependencies.json",
            "binaries.json",
            "decision.json",
            "Write-Wb05Manifest",
            "Get-Wb05BinaryRecords",
            "granite_model_test_authorised = $false",
            "activation_claim_authorised = $false",
            "packed_storage_claim_authorised = $false",
            "performance_claim_authorised = $false",
            "quality_claim_authorised = $false",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)


class RouteAGenAIBuildContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = GENAI_SCRIPT.read_text(encoding="utf-8")

    def test_pins_source_and_requires_exact_runtime_decision(self) -> None:
        required = (
            "https://github.com/openvinotoolkit/openvino.genai.git",
            "bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0",
            "src/docs/BUILD.md",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "RuntimeDecisionPath",
            "RuntimeInstallDirectory",
            "$runtimeDecision.status -ne 'Passed'",
            "$runtimeDecision.component -ne 'runtime'",
            "$runtimeDecision.source_commit -ne $RuntimeSourceCommit",
            "$runtimeDecision.route_id -ne $RouteId",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_uses_fresh_genai_workspace_and_read_only_runtime_install(self) -> None:
        required = (
            "Assert-Wb05SafePath",
            "Split-Path -Leaf $resolved",
            "accepted Route A i-ov directory",
            "New-Wb05ExternalWorkspace",
            "-Root $WorkspaceRoot",
            "-RunIdentity $RunIdentity",
            "$workDirectory = $workspace.work_directory",
            "Join-Path $workDirectory 'genai'",
            "Join-Path $workDirectory 'b-genai'",
            "Join-Path $workDirectory 'i-genai'",
            "External GenAI path already exists",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)
        self.assertNotIn("$expectedRuntimeInstall", self.text)
        self.assertNotIn("exact Route A Runtime install for this run", self.text)

    def test_configures_builds_installs_and_restores_environment(self) -> None:
        # These are semantic tokens whose exact whitespace is not meaningful.
        # Keep direct checks for fixed flags/records, and use whitespace-tolerant
        # regular expressions for PowerShell argument-array pairs.
        required = (
            "-Filter 'OpenVINOConfig.cmake'",
            "$openvinoConfigs.Count -ne 1",
            "$openvinoConfigDirectory = $openvinoConfigs[0].DirectoryName",
            "'-DCMAKE_BUILD_TYPE=Release'",
            '"-DOpenVINO_DIR=$openvinoConfigDirectory"',
            "'-DENABLE_PYTHON=ON'",
            "'-DENABLE_JS=OFF'",
            '"-DPython3_EXECUTABLE=$PythonPath"',
            "Test-Wb05SameWindowsPath",
            "$openvinoConfigPathMatches",
            "-not $openvinoConfigPathMatches",
            "compatibility-attempt.json",
            "binaries.json",
            "decision.json",
            "Restore-Wb05Environment -Snapshot $environmentSnapshot",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

        self.assertNotIn(
            "$cacheValues.OpenVINO_DIR -ne $openvinoConfigDirectory.Replace",
            self.text,
        )

        argument_pairs = (
            r"'-G'\s*,\s*\$Generator",
            r"'-A'\s*,\s*'x64'",
            r"'--build'\s*,\s*\$buildRoot",
            r"'--parallel'\s*,\s*'2'",
            r"'--install'\s*,\s*\$buildRoot",
            r"'--prefix'\s*,\s*\$installRoot",
        )
        for pattern in argument_pairs:
            with self.subTest(pattern=pattern):
                self.assertRegex(self.text, pattern)

        for name in ("PATH", "PYTHONPATH", "OPENVINO_LIB_PATHS", "OpenVINO_DIR"):
            self.assertIn(name, self.text)

        # Compile with DOTALL explicitly. unittest.assertRegex's third argument is
        # a failure message, not regex flags, so passing re.S there never enabled
        # matching across the multi-line finally block.
        finally_pattern = re.compile(
            r"finally\s*\{.*?Restore-Wb05Environment",
            re.S,
        )
        self.assertRegex(self.text, finally_pattern)

    def test_forbids_models_archives_and_destructive_commands(self) -> None:
        forbidden = (
            "download.openvino",
            ".zip",
            ".whl",
            "huggingface.co",
            ".gguf",
            ".safetensors",
            "generate(",
            "benchmark_app",
            "Invoke-Expression",
            "git reset --hard",
            "git clean",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.text.lower())


if __name__ == "__main__":
    unittest.main()
