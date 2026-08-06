from __future__ import annotations

import re
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
MODULE_PATH = REPOSITORY_ROOT / "scripts/testing/workbook05/Workbook05.Build.psm1"


class BuildPowerShellContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = MODULE_PATH.read_text(encoding="utf-8")

    def test_exports_the_reviewed_public_functions(self) -> None:
        expected = {
            "New-Wb05ExternalWorkspace",
            "Invoke-Wb05LoggedProcess",
            "Start-Wb05ResourceSampler",
            "Stop-Wb05ResourceSampler",
            "Write-Wb05Json",
            "Write-Wb05Manifest",
            "Assert-Wb05SafePath",
            "Get-Wb05BinaryRecords",
            "Restore-Wb05Environment",
        }
        match = re.search(r"Export-ModuleMember\s+-Function\s+@\((.*?)\)", self.text, re.S)
        self.assertIsNotNone(match)
        exported = set(re.findall(r"'([^']+)'", match.group(1)))
        self.assertEqual(expected, exported)

    def test_forbids_dangerous_global_or_destructive_operations(self) -> None:
        forbidden = (
            "Invoke-Expression",
            "git reset --hard",
            "git clean",
            "Set-ItemProperty HKLM:",
            "New-ItemProperty HKLM:",
            "config --global core.longpaths",
            "Remove-Item -LiteralPath 'C:\\w5a' -Recurse",
            "Remove-Item -LiteralPath 'C:\\w5b' -Recurse",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.text.lower())

    def test_native_process_adapter_preserves_argument_boundaries(self) -> None:
        self.assertIn("System.Diagnostics.ProcessStartInfo", self.text)
        self.assertIn("[string[]]$ArgumentList", self.text)
        self.assertIn("ConvertTo-Wb05WindowsCommandLineArgument", self.text)
        self.assertIn("$processStartInfo.Arguments", self.text)
        self.assertNotIn("Invoke-Expression", self.text)

    def test_native_process_adapter_drains_both_streams_asynchronously(self) -> None:
        self.assertGreaterEqual(self.text.count("ReadToEndAsync()"), 2)
        self.assertIn("$stdoutTask.GetAwaiter().GetResult()", self.text)
        self.assertIn("$stderrTask.GetAwaiter().GetResult()", self.text)

    def test_workspace_rejects_reparse_points_and_existing_run_directories(self) -> None:
        self.assertIn("[IO.FileAttributes]::ReparsePoint", self.text)
        self.assertRegex(self.text, r"if \(Test-Path -LiteralPath \$workDirectory\).*?throw", re.S)
        self.assertIn("External run directory already exists", self.text)

    def test_sampler_uses_two_second_intervals_and_ten_second_safety_windows(self) -> None:
        self.assertIn("[int]$SampleIntervalSeconds = 2", self.text)
        self.assertIn("[int]$ConsecutiveSafetySamples = 5", self.text)
        self.assertIn("1610612736", self.text)
        self.assertIn("[double]$MaximumCommitPercent = 90", self.text)

    def test_sampler_records_process_tree_peaks_and_heartbeat_timeout(self) -> None:
        self.assertIn("peak_working_set_bytes", self.text)
        self.assertIn("peak_private_bytes", self.text)
        self.assertIn("[int]$HeartbeatTimeoutSeconds = 900", self.text)
        self.assertIn("process_tree_ids", self.text)

    def test_environment_restoration_handles_existing_and_missing_values(self) -> None:
        self.assertRegex(self.text, r"function Restore-Wb05Environment")
        self.assertIn("Remove-Item -Path \"Env:$name\"", self.text)
        self.assertIn("Set-Item -Path \"Env:$name\"", self.text)

    def test_binary_records_never_copy_payloads(self) -> None:
        self.assertIn("copied_to_artifact = $false", self.text)
        self.assertIn("Get-FileHash", self.text)
        self.assertNotIn("Copy-Item", self.text)


if __name__ == "__main__":
    unittest.main()


ROUTE_A_RUNTIME_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteARuntimeBuild.ps1"
)


class RouteARuntimeBuildContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = ROUTE_A_RUNTIME_SCRIPT.read_text(encoding="utf-8")

    def test_pins_exact_source_toolchain_and_short_workspace(self) -> None:
        expected_tokens = (
            "https://github.com/openvinotoolkit/openvino.git",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "docs/dev/build_windows.md",
            "C:\\w5a",
            "Visual Studio 17 2022",
            "C:\\Program Files\\Python312\\python.exe",
            "Python 3.12.10",
            "C:\\Program Files\\Microsoft Visual Studio\\18\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\CMake\\CMake\\bin\\cmake.exe",
        )
        for token in expected_tokens:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_creates_separate_source_build_and_install_directories(self) -> None:
        self.assertIn("Join-Path $workspace.work_directory 'ov'", self.text)
        self.assertIn("Join-Path $workspace.work_directory 'b-ov'", self.text)
        self.assertIn("Join-Path $workspace.work_directory 'i-ov'", self.text)

    def test_follows_documented_source_acquisition_order(self) -> None:
        ordered_ids = (
            "route-a-git-init",
            "route-a-git-config-longpaths",
            "route-a-git-remote-add",
            "route-a-git-fetch",
            "route-a-git-checkout",
            "route-a-git-submodules",
            "route-a-git-remote-verify",
            "route-a-git-head-verify",
            "route-a-git-status-verify",
            "route-a-git-submodules-verify",
        )
        positions = [self.text.index(command_id) for command_id in ordered_ids]
        self.assertEqual(sorted(positions), positions)
        self.assertNotIn("git reset --hard", self.text.lower())
        self.assertNotIn("git clean", self.text.lower())

    def test_uses_the_exact_reviewed_runtime_configure_arguments(self) -> None:
        expected_arguments = (
            "'-G', $Generator",
            "'-A', 'x64'",
            "'-DCMAKE_BUILD_TYPE=Release'",
            "'-DENABLE_INTEL_GPU=OFF'",
            "'-DENABLE_INTEL_NPU=OFF'",
            "'-DENABLE_TESTS=OFF'",
            "'-DENABLE_FUNCTIONAL_TESTS=OFF'",
            "'-DENABLE_SAMPLES=ON'",
            "'-DENABLE_PYTHON=ON'",
            "'-DENABLE_WHEEL=OFF'",
            "\"-DPython3_EXECUTABLE=$PythonPath\"",
        )
        for argument in expected_arguments:
            with self.subTest(argument=argument):
                self.assertIn(argument, self.text)

    def test_builds_and_installs_release_with_conservative_parallelism(self) -> None:
        self.assertIn("'--config', 'Release'", self.text)
        self.assertIn("'--parallel', '2'", self.text)
        self.assertIn("'--verbose'", self.text)
        self.assertIn("'--install', $buildRoot", self.text)
        self.assertIn("'--prefix', $installRoot", self.text)

    def test_records_provenance_cache_resources_dependencies_binaries_and_decision(self) -> None:
        expected_tokens = (
            "environment.json",
            "source-provenance.json",
            "cmake-cache-summary.json",
            "dependencies.json",
            "binaries.json",
            "decision.json",
            "Write-Wb05Manifest",
            "Get-Wb05BinaryRecords",
            "MonitorResources",
        )
        for token in expected_tokens:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_decisions_keep_all_later_claims_disabled(self) -> None:
        for flag in (
            "granite_model_test_authorised = $false",
            "activation_claim_authorised = $false",
            "packed_storage_claim_authorised = $false",
            "performance_claim_authorised = $false",
            "quality_claim_authorised = $false",
        ):
            with self.subTest(flag=flag):
                self.assertIn(flag, self.text)

    def test_contains_no_model_or_inference_execution(self) -> None:
        forbidden = (
            "huggingface.co",
            ".gguf",
            ".safetensors",
            "generate(",
            "benchmark_app",
            "openvino_genai",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.text.lower())


ROUTE_A_GENAI_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteAGenAIBuild.ps1"
)


class RouteAGenAIBuildContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = ROUTE_A_GENAI_SCRIPT.read_text(encoding="utf-8")

    def test_pins_exact_genai_source_and_runtime_prerequisite(self) -> None:
        expected = (
            "https://github.com/openvinotoolkit/openvino.genai.git",
            "05e5c7670b597746f858946974d11f38e3baf42f",
            "src/docs/BUILD.md",
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            "route-a-merged-openvino",
            "RuntimeDecisionPath",
            "RuntimeInstallDirectory",
        )
        for token in expected:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_requires_passed_exact_runtime_decision(self) -> None:
        self.assertIn("$runtimeDecision.status -ne 'Passed'", self.text)
        self.assertIn("$runtimeDecision.component -ne 'runtime'", self.text)
        self.assertIn("$runtimeDecision.source_commit -ne $RuntimeSourceCommit", self.text)
        self.assertIn("$runtimeDecision.route_id -ne $RouteId", self.text)

    def test_resolves_exactly_one_installed_openvino_config(self) -> None:
        self.assertIn("-Filter 'OpenVINOConfig.cmake'", self.text)
        self.assertIn("$openvinoConfigs.Count -ne 1", self.text)
        self.assertIn("$openvinoConfigDirectory = $openvinoConfigs[0].DirectoryName", self.text)

    def test_uses_separate_genai_source_build_and_install_paths(self) -> None:
        self.assertIn("Join-Path $workDirectory 'genai'", self.text)
        self.assertIn("Join-Path $workDirectory 'b-genai'", self.text)
        self.assertIn("Join-Path $workDirectory 'i-genai'", self.text)
        self.assertIn("External GenAI path already exists", self.text)

    def test_configures_against_exact_runtime_install(self) -> None:
        expected = (
            "'--G', $Generator",
            "'-A', 'x64'",
            "'-DCMAKE_BUILD_TYPE=Release'",
            "\" -DOpenVINO_DIR=$openvinoConfigDirectory\"",
            "'-DENABLE_PYTHON=ON'",
            "'-DENABLE_JS=OFF'",
            "\" -DPython3_EXECUTABLE=$PythonPath\"",
        )
        for token in expected:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_builds_installs_and_records_compatibility_attempt(self) -> None:
        expected = (
            "'--build', $buildRoot",
            "'--config', 'Release'",
            "'--parallel', '2'",
            "'--install', $buildRoot",
            "'--prefix', $installRoot",
            "compatibility-attempt.json",
            "record_type = 'build-compatibility-attempt'",
            "retained = $true",
            "binaries.json",
            "decision.json",
        )
        for token in expected:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_snapshots_and_restores_runtime_environment(self) -> None:
        for name in ("PATH", "PYTHONPATH", "OPENVINO_LIB_PATHS", "OpenVINO_DIR"):
            with self.subTest(name=name):
                self.assertIn(f'{name}'', self.text)
        self.assertIn("Restore-Wb05Environment -Snapshot $environmentSnapshot", self.text)
        self.assertRegex(self.text, r"finally\s*\{.*?Restore-Wb05Environment", re.S)

    def test_does_not_mix_with_archives_or_execute_models(self) -> None:
        forbidden = (
            "download.openvino",
            "storage.openvinotoolkit.org/repositories/openvino_genai",
            ".zip",
            ".whl",
            "huggingface.co",
            "generate(",
            "benchmark_app",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.text.lower())
