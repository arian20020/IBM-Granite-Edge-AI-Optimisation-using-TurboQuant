from __future__ import annotations

import json
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[3]
SCRIPT_PATH = ROOT / "scripts/testing/workbook05/Invoke-Workbook05SourceAdmission.ps1"
MODULE_PATH = ROOT / "scripts/testing/workbook05/Workbook05.SourceAdmission.psm1"

EXPECTED_STEP_ORDER = (
    "preflight-validation",
    "workspace-validation",
    "measurement-control-capture",
    "route-a-runtime-verification",
    "route-a-genai-verification",
    "route-b-verification",
    "document-capture",
    "capability-inspection",
    "route-b-cmake-audit",
    "route-a-configure-probe",
    "route-decisions",
    "hashes",
)


def _powershell_executable() -> str:
    """Return the Windows PowerShell executable used by the controlled runner."""

    executable = shutil.which("powershell.exe") or shutil.which("powershell")
    if executable is None:
        raise unittest.SkipTest("Windows PowerShell is required for orchestration tests.")
    return executable


def _strip_powershell_comments(text: str) -> str:
    """Remove comments before checking executable command text."""

    without_blocks = re.sub(r"(?s)<#.*?#>", "", text)
    return re.sub(r"(?m)^\s*#.*$", "", without_blocks)


def _run_powershell(command: str) -> subprocess.CompletedProcess[str]:
    """Run one isolated PowerShell harness and preserve both output streams."""

    return subprocess.run(
        [
            _powershell_executable(),
            "-NoLogo",
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            command,
        ],
        cwd=ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=False,
    )


class SourceAdmissionOrchestratorStaticTests(unittest.TestCase):
    def test_script_declares_the_exact_approved_step_order(self) -> None:
        text = SCRIPT_PATH.read_text(encoding="utf-8")
        match = re.search(r"(?s)\$StepOrder\s*=\s*@\((.*?)\)", text)
        self.assertIsNotNone(match, "The orchestrator must declare one visible StepOrder array.")
        declared = tuple(re.findall(r"['\"]([^'\"]+)['\"]", match.group(1)))
        self.assertEqual(EXPECTED_STEP_ORDER, declared)

    def test_script_contains_no_prohibited_execution_command(self) -> None:
        executable_text = _strip_powershell_comments(
            SCRIPT_PATH.read_text(encoding="utf-8")
        ).casefold()

        forbidden_literals = (
            "invoke-expression",
            "git reset --hard",
            "git clean",
            "cmake --build",
            "cmake --install",
            "cmake --package",
            "--target",
            "huggingface.co",
            "allow_route_b_configure_while_blocked = true",
        )
        for forbidden in forbidden_literals:
            with self.subTest(forbidden=forbidden):
                self.assertNotIn(forbidden, executable_text)

        self.assertNotRegex(
            executable_text,
            r"remove-item[^\r\n]*c:\\wb05[^\r\n]*-recurse",
            "The controlled external workspace must never be recursively removed.",
        )
        self.assertNotRegex(
            executable_text,
            r"(?:cmake\.exe|cmake_path)[^\r\n]{0,200}route-b",
            "No CMake invocation may target Route B while RB-SRC-001 is open.",
        )

    def test_script_uses_argument_list_execution_and_not_a_command_shell(self) -> None:
        text = SCRIPT_PATH.read_text(encoding="utf-8")
        self.assertIn("Invoke-Workbook05SourceAdmissionPipeline", text)
        self.assertIn("Invoke-Workbook05RecordedCommand", text)
        self.assertIn("source_admission_orchestration", text)
        self.assertNotIn("cmd.exe", text.casefold())


class SourceAdmissionOrchestratorSimulationTests(unittest.TestCase):
    def test_scientific_route_b_blocker_completes_with_zero_exit_semantics(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            output_root = Path(temporary_directory) / "bundle"
            required_files = [f"steps/{step_id}.json" for step_id in EXPECTED_STEP_ORDER]
            required_files.extend(("orchestration-report.json", "hash-snapshot.txt"))
            step_array = ",".join(f"'{step_id}'" for step_id in EXPECTED_STEP_ORDER)
            required_array = ",".join(f"'{path}'" for path in required_files)
            command = rf"""
$ErrorActionPreference = 'Stop'
Import-Module '{MODULE_PATH.as_posix()}' -Force
$stepOrder = @({step_array})
$requiredFiles = @({required_array})
$executor = {{
    param([string]$StepId, [string]$OutputDirectory)
    $stepDirectory = Join-Path $OutputDirectory 'steps'
    New-Item -ItemType Directory -Path $stepDirectory -Force | Out-Null
    @{{ step_id = $StepId; completed = $true }} |
        ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $stepDirectory "$StepId.json") -Encoding UTF8

    if ($StepId -eq 'route-b-cmake-audit') {{
        return [pscustomobject]@{{
            StepId = $StepId
            Kind = 'ScientificBlocker'
            Status = 'Blocked'
            Reason = 'RB-SRC-001 remains open.'
        }}
    }}
    if ($StepId -eq 'route-decisions') {{
        return [pscustomobject]@{{
            StepId = $StepId
            Kind = 'Success'
            Status = 'Passed'
            RouteAStatus = 'Admitted'
            RouteBStatus = 'Blocked'
            CheckpointStatus = 'Passed'
            Reason = 'Route A may continue while Route B is truthfully blocked.'
        }}
    }}
    if ($StepId -eq 'hashes') {{
        $reportPath = Join-Path $OutputDirectory 'orchestration-report.json'
        if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {{
            return [pscustomobject]@{{
                StepId = $StepId
                Kind = 'IntegrityFailure'
                Status = 'Failed'
                Reason = 'The orchestration report was not final before hashing.'
            }}
        }}
        $snapshotPath = Join-Path $OutputDirectory 'hash-snapshot.txt'
        $relativeFiles = @(
            Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File |
                Where-Object {{ $_.FullName -ne $snapshotPath }} |
                ForEach-Object {{
                    $_.FullName.Substring($OutputDirectory.Length).TrimStart('\').Replace('\', '/')
                }} |
                Sort-Object
        )
        $relativeFiles | Set-Content -LiteralPath $snapshotPath -Encoding UTF8
        return [pscustomobject]@{{
            StepId = $StepId
            Kind = 'Success'
            Status = 'Passed'
            Reason = 'The final simulated bundle was snapshotted.'
        }}
    }}
    return [pscustomobject]@{{
        StepId = $StepId
        Kind = 'Success'
        Status = 'Passed'
        Reason = 'Simulated controlled success.'
    }}
}}
$result = Invoke-Workbook05SourceAdmissionPipeline `
    -OutputDirectory '{output_root.as_posix()}' `
    -StepOrder $stepOrder `
    -StepExecutor $executor `
    -RequiredBundleFiles $requiredFiles
$result | ConvertTo-Json -Depth 20 -Compress
"""
            completed = _run_powershell(command)
            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads(completed.stdout.strip().splitlines()[-1])
            self.assertEqual("Completed", result["Outcome"])
            self.assertEqual("Admitted", result["RouteAStatus"])
            self.assertEqual("Blocked", result["RouteBStatus"])
            self.assertEqual("Passed", result["CheckpointStatus"])
            self.assertEqual(list(EXPECTED_STEP_ORDER), result["ExecutedSteps"])

            snapshot = {
                line.strip()
                for line in (output_root / "hash-snapshot.txt").read_text(
                    encoding="utf-8-sig"
                ).splitlines()
                if line.strip()
            }
            actual = {
                path.relative_to(output_root).as_posix()
                for path in output_root.rglob("*")
                if path.is_file() and path.name != "hash-snapshot.txt"
            }
            self.assertEqual(actual, snapshot)

    def test_integrity_failure_stops_the_pipeline_and_returns_nonzero(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            output_root = Path(temporary_directory) / "bundle"
            step_array = ",".join(f"'{step_id}'" for step_id in EXPECTED_STEP_ORDER)
            command = rf"""
$ErrorActionPreference = 'Stop'
Import-Module '{MODULE_PATH.as_posix()}' -Force
$stepOrder = @({step_array})
$executor = {{
    param([string]$StepId, [string]$OutputDirectory)
    if ($StepId -eq 'route-a-runtime-verification') {{
        return [pscustomobject]@{{
            StepId = $StepId
            Kind = 'IntegrityFailure'
            Status = 'Failed'
            Reason = 'The pinned origin did not match.'
        }}
    }}
    return [pscustomobject]@{{
        StepId = $StepId
        Kind = 'Success'
        Status = 'Passed'
        Reason = 'Simulated controlled success.'
    }}
}}
try {{
    Invoke-Workbook05SourceAdmissionPipeline `
        -OutputDirectory '{output_root.as_posix()}' `
        -StepOrder $stepOrder `
        -StepExecutor $executor `
        -RequiredBundleFiles @() | Out-Null
    exit 0
}}
catch {{
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 17
}}
"""
            completed = _run_powershell(command)
            self.assertEqual(17, completed.returncode)
            self.assertTrue((output_root / "orchestration-error.json").is_file())
            error = json.loads(
                (output_root / "orchestration-error.json").read_text(
                    encoding="utf-8-sig"
                )
            )
            self.assertEqual("route-a-runtime-verification", error["failed_step"])
            self.assertIn("pinned origin", error["reason"])

    def test_missing_required_bundle_file_is_an_orchestration_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            output_root = Path(temporary_directory) / "bundle"
            command = rf"""
$ErrorActionPreference = 'Stop'
Import-Module '{MODULE_PATH.as_posix()}' -Force
$executor = {{
    param([string]$StepId, [string]$OutputDirectory)
    return [pscustomobject]@{{
        StepId = $StepId
        Kind = 'Success'
        Status = 'Passed'
        Reason = 'No file was written.'
    }}
}}
try {{
    Invoke-Workbook05SourceAdmissionPipeline `
        -OutputDirectory '{output_root.as_posix()}' `
        -StepOrder @('hashes') `
        -StepExecutor $executor `
        -RequiredBundleFiles @('hash-manifest.sha256') | Out-Null
    exit 0
}}
catch {{
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 19
}}
"""
            completed = _run_powershell(command)
            self.assertEqual(19, completed.returncode)
            self.assertIn("required bundle file", completed.stderr.casefold())


if __name__ == "__main__":
    unittest.main()
