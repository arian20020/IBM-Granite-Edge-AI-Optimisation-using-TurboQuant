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
            "'-DENABLE_INTEL_GPU=OFF'",
            "'-DENABLE_INTEL_NPU=OFF'",
            "'-DENABLE_TESTS=OFF'",
            "'-DENABLE_FUNCTIONAL_TESTS=OFF'",
            "'-DENABLE_SAMPLES=ON'",
            "'-DENABLE_PYTHON=ON'",
            "'-DENABLE_WHEEL=OFF'",
            "'--parallel', '2'",
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
            "05e5c7670b597746f858946974d11f38e3baf42f",
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
        required = (
            "-Filter 'OpenVINOConfig.cmake'",
            "$openvinoConfigs.Count -ne 1",
            "$openvinoConfigDirectory = $openvinoConfigs[0].DirectoryName",
            "'-G',$Generator",
            "'-A','x64'",
            "'-DCMAKE_BUILD_TYPE=Release'",
            '"-DOpenVINO_DIR=$openvinoConfigDirectory"',
            "'-DENABLE_PYTHON=ON'",
            "'-DENABLE_JS=OFF'",
            '"-DPython3_EXECUTABLE=$PythonPath"',
            "'--build',$buildRoot",
            "'--parallel','2'",
            "'--install',$buildRoot",
            "'--prefix',$installRoot",
            "compatibility-attempt.json",
            "binaries.json",
            "decision.json",
            "Restore-Wb05Environment -Snapshot $environmentSnapshot",
        )
        for token in required:
            with self.subTest(token=token):
                self.assertIn(token, self.text)
        for name in ("PATH", "PYTHONPATH", "OPENVINO_LIB_PATHS", "OpenVINO_DIR"):
            self.assertIn(name, self.text)
        self.assertRegex(self.text, r"finally\s*\{.*?Restore-Wb05Environment", re.S)

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
