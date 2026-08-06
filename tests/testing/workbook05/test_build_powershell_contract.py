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
