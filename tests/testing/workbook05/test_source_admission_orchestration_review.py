from __future__ import annotations

import json
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from scripts.testing.workbook05 import source_admission_orchestration as orchestration


ROOT = Path(__file__).resolve().parents[3]
SCRIPT_PATH = ROOT / "scripts/testing/workbook05/Invoke-Workbook05SourceAdmission.ps1"
MODULE_PATH = ROOT / "scripts/testing/workbook05/Workbook05.SourceAdmission.psm1"
STEP_ORDER = (
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
    executable = shutil.which("powershell.exe") or shutil.which("powershell")
    if executable is None:
        raise unittest.SkipTest("Windows PowerShell is required.")
    return executable


def _run_powershell(command: str) -> subprocess.CompletedProcess[str]:
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


def _quoted(values: list[str] | tuple[str, ...]) -> str:
    return ",".join(f"'{value}'" for value in values)


class SourceAdmissionOrchestrationReviewTests(unittest.TestCase):
    def test_script_parses_without_powershell_syntax_errors(self) -> None:
        command = rf"""
$tokens = $null
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile(
    '{SCRIPT_PATH.as_posix()}',
    [ref]$tokens,
    [ref]$errors
) | Out-Null
if ($errors.Count -gt 0) {{
    $errors | ForEach-Object {{ [Console]::Error.WriteLine($_.Message) }}
    exit 31
}}
exit 0
"""
        completed = _run_powershell(command)
        self.assertEqual(0, completed.returncode, completed.stderr)
        self.assertNotIn(
            "testing-placeholder",
            SCRIPT_PATH.read_text(encoding="utf-8"),
        )

    def test_fully_admitted_simulation_produces_a_complete_bundle(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            output_root = Path(temporary_directory) / "bundle"
            required = [f"steps/{step}.json" for step in STEP_ORDER]
            required += ["orchestration-report.json", "hash-manifest.sha256"]
            command = rf"""
$ErrorActionPreference = 'Stop'
Import-Module '{MODULE_PATH.as_posix()}' -Force
$executor = {{
    param([string]$StepId, [string]$OutputDirectory)
    $steps = Join-Path $OutputDirectory 'steps'
    New-Item -ItemType Directory -Path $steps -Force | Out-Null
    @{{ step_id = $StepId; completed = $true }} |
        ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $steps "$StepId.json") -Encoding UTF8

    if ($StepId -eq 'route-decisions') {{
        return [pscustomobject]@{{
            StepId = $StepId; Kind = 'Success'; Status = 'Passed'
            RouteAStatus = 'Admitted'; RouteBStatus = 'Admitted'
            CheckpointStatus = 'Passed'; Reason = 'Both routes passed.'
        }}
    }}
    if ($StepId -eq 'hashes') {{
        if (-not (Test-Path -LiteralPath (Join-Path $OutputDirectory 'orchestration-report.json'))) {{
            return [pscustomobject]@{{
                StepId = $StepId; Kind = 'IntegrityFailure'; Status = 'Failed'
                Reason = 'The orchestration report was missing before hashing.'
            }}
        }}
        'simulated-hash-manifest' |
            Set-Content -LiteralPath (Join-Path $OutputDirectory 'hash-manifest.sha256') -Encoding UTF8
    }}
    return [pscustomobject]@{{
        StepId = $StepId; Kind = 'Success'; Status = 'Passed'
        Reason = 'Simulated controlled success.'
    }}
}}
$result = Invoke-Workbook05SourceAdmissionPipeline `
    -OutputDirectory '{output_root.as_posix()}' `
    -StepOrder @({_quoted(STEP_ORDER)}) `
    -StepExecutor $executor `
    -RequiredBundleFiles @({_quoted(required)})
$result | ConvertTo-Json -Depth 20 -Compress
"""
            completed = _run_powershell(command)
            self.assertEqual(0, completed.returncode, completed.stderr)
            result = json.loads(completed.stdout.strip().splitlines()[-1])
            self.assertEqual("Admitted", result["RouteAStatus"])
            self.assertEqual("Admitted", result["RouteBStatus"])
            self.assertEqual("Passed", result["CheckpointStatus"])
            for relative_path in required:
                self.assertTrue((output_root / relative_path).is_file(), relative_path)

    def test_pinned_source_mismatch_is_an_integrity_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            temporary_root = Path(temporary_directory)
            settings_path = temporary_root / "settings.json"
            settings_path.write_text(
                json.dumps({"clone_timeout_seconds": 60}),
                encoding="utf-8",
            )
            arguments = SimpleNamespace(
                step="route-a-runtime-verification",
                repository_root=ROOT,
                output_directory=temporary_root / "bundle",
                settings=settings_path,
            )
            blocked_report = {
                "decision": "Blocked",
                "decision_reason": "The pinned origin did not match.",
            }
            verification_result = SimpleNamespace(report=blocked_report)
            configured = {
                "repository_full_name": "openvinotoolkit/openvino",
                "origin_url": "https://github.com/openvinotoolkit/openvino.git",
                "commit": "a" * 40,
                "source_directory": str(temporary_root / "source"),
            }

            with patch.object(
                orchestration,
                "verify_source_tree",
                return_value=verification_result,
            ), patch.object(orchestration, "_assert_schema"):
                result = orchestration._run_source_verification(
                    arguments,
                    route_id="route-a-merged-openvino",
                    source_role="runtime",
                    configured=configured,
                    route_folder="route-a",
                    report_name="source-tree-runtime.json",
                )

            self.assertEqual("IntegrityFailure", result["Kind"])
            self.assertEqual("Failed", result["Status"])
            self.assertIn("pinned origin", result["Reason"])


if __name__ == "__main__":
    unittest.main()
