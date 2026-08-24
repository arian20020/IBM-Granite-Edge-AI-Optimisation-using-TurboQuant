import json
import os
import shutil
import subprocess
import tempfile
import textwrap
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
CONTROLLER = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Invoke-HardwareInspectionGate9Acceptance.ps1"
)
SUMMARY_VALIDATOR = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Test-HardwareInspectionGate9Summary.ps1"
)
LLMFIT_SHA256 = "db82bcb17f065b7ff7528ffe9904b2b0e1cce0c843fcd659b4ee1432a2e72e19"


def powershell_executable():
    for name in ("powershell.exe", "powershell", "pwsh.exe", "pwsh"):
        candidate = shutil.which(name)
        if candidate:
            return candidate
    raise unittest.SkipTest("PowerShell executable is not available")


class Gate9AcceptanceControllerTests(unittest.TestCase):
    def run_functions(self, function_names, command, environment=None, timeout=20):
        probe_environment = os.environ.copy()
        probe_environment["GRANITE_GATE9_CONTROLLER"] = str(CONTROLLER)
        if environment:
            probe_environment.update(environment)
        names = ",".join("'" + name + "'" for name in function_names)
        return subprocess.run(
            [
                powershell_executable(),
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-Command",
                rf"""
                $ErrorActionPreference = 'Stop'
                $tokens = $null
                $errors = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                    $env:GRANITE_GATE9_CONTROLLER,
                    [ref]$tokens,
                    [ref]$errors)
                if ($errors.Count -ne 0) {{ throw 'Controller did not parse.' }}
                foreach ($name in @({names})) {{
                    $function = $ast.Find({{
                        param($node)
                        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $node.Name -ceq $name
                    }}, $true)
                    if ($null -eq $function) {{ throw "Missing function: $name" }}
                    Invoke-Expression $function.Extent.Text
                }}
                {command}
                """,
            ],
            cwd=REPOSITORY_ROOT,
            env=probe_environment,
            text=True,
            capture_output=True,
            timeout=timeout,
            check=False,
        )

    def test_controller_has_strict_interface_and_forbidden_mutations_absent(self):
        """Catch an operational controller that broadens authority or leaks raw state."""
        source = CONTROLLER.read_text(encoding="utf-8")
        normalized = source.lower()
        for required in (
            "$bundledirectory",
            "$expectedbundlesha256",
            "$expectedcommit",
            "$confirmsupportedinteltarget",
            "$releasetrustrecord",
            "graniteedgeai\\hardwareinspection\\llmfit\\1.1.9\\win-x64",
            LLMFIT_SHA256,
            "graniteedgeai.unittests.msix",
            "bundle-manifest.json",
            "--hardware-inspection-gate9-acceptance",
            "--result-token",
            "get-nettcpconnection",
            "get-netudpendpoint",
            "get-ciminstance",
            "win32_process",
            "parentprocessid",
            "add-appxpackage",
            "remove-appxpackage",
            "180",
            "milliseconds 20",
            "gate9-engineering-acceptance.json",
            "monitoring did not observe the activated root process",
            "$cleanuppackages",
        ):
            self.assertIn(required, normalized)

        for forbidden in (
            "set-netadapter",
            "disable-netadapter",
            "enable-netadapter",
            "new-netfirewallrule",
            "set-netfirewallprofile",
            "set-executionpolicy",
            "set-mppreference",
            "add-mppreference",
            "remove-mppreference",
            "smartappcontrolstate =",
            "internetsetoption",
            "invoke-webrequest",
            "invoke-restmethod",
            "start-bitstransfer",
            "system.net.http",
            "webclient",
            "downloadfile",
            "downloadstring",
            "trustedpeople",
            "x509store",
            "cert:\\",
        ):
            self.assertNotIn(forbidden, normalized)

        self.assertNotIn("hardwareSnapshot", source)
        self.assertNotIn("ConvertTo-Json -Depth 100", source)
        self.assertNotIn("Remove-Item -Path", source)

    def test_partial_install_cleanup_recovers_one_owned_package_identity(self):
        """Catch a failed Add-AppxPackage attempt leaving its partial registration behind."""
        source = CONTROLLER.read_text(encoding="utf-8")
        discovery = source.index("$cleanupPackages = @(")
        capture = source.index(
            "$installedPackageFullName = [string] $cleanupPackages[0].PackageFullName"
        )
        removal = source.index("Remove-AppxPackage")

        self.assertLess(discovery, capture)
        self.assertLess(capture, removal)
        self.assertIn("$cleanupPackages.Count -eq 1", source)
        self.assertIn("$cleanupPackages.Count -gt 1", source)

    def test_reparse_ancestor_check_accepts_physical_file_and_directory_paths(self):
        """Catch treating FileInfo as DirectoryInfo while walking path ancestors."""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "physical"
            root.mkdir()
            file_path = root / "bundle.zip"
            file_path.write_bytes(b"bundle")

            result = self.run_functions(
                ["Assert-Gate9NoReparseAncestors"],
                r"""
                Set-StrictMode -Version Latest
                Assert-Gate9NoReparseAncestors -Path $env:GRANITE_GATE9_DIRECTORY
                Assert-Gate9NoReparseAncestors -Path $env:GRANITE_GATE9_FILE
                'OK'
                """,
                environment={
                    "GRANITE_GATE9_DIRECTORY": str(root),
                    "GRANITE_GATE9_FILE": str(file_path),
                },
            )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("OK\n", result.stdout)

    def test_supported_target_facts_accept_exact_windows11_x64_intel_physical(self):
        """Catch rejection of the one explicitly supported physical target class."""
        result = self.run_functions(
            ["Assert-Gate9SupportedTargetFacts"],
            r"""
            Assert-Gate9SupportedTargetFacts `
                -IsAdministrator $true `
                -OsBuild 26100 `
                -OsArchitecture 'X64' `
                -ProcessorManufacturers @('GenuineIntel') `
                -ComputerManufacturer 'LENOVO' `
                -ComputerModel '21ABCTO1WW' `
                -ConnectedPhysicalAdapterCount 0
            'OK'
            """,
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("OK\n", result.stdout)

    def test_operating_system_architecture_uses_windows_powershell_compatible_probe(self):
        """Catch dependence on RuntimeInformation members absent in some PS 5.1 hosts."""
        result = self.run_functions(
            ["Get-Gate9OperatingSystemArchitecture"],
            r"""
            $expected = if ([Environment]::Is64BitOperatingSystem) {
                'X64'
            }
            else {
                'X86'
            }
            $actual = Get-Gate9OperatingSystemArchitecture
            if ($actual -cne $expected) {
                throw "Expected $expected but received $actual."
            }
            'OK'
            """,
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("OK\n", result.stdout)
        self.assertNotIn(
            "RuntimeInformation]::OSArchitecture",
            CONTROLLER.read_text(encoding="utf-8"),
        )

    def test_supported_target_facts_fail_closed_for_every_precondition(self):
        """Catch AMD, x86, VM, connected, old-Windows, or non-admin acceptance."""
        self.assertTrue(CONTROLLER.is_file(), "Gate 9 controller is missing")
        cases = {
            "not-admin": "$false 26100 'X64' @('GenuineIntel') 'LENOVO' '21AB' 0",
            "windows10": "$true 19045 'X64' @('GenuineIntel') 'LENOVO' '21AB' 0",
            "arm64": "$true 26100 'Arm64' @('GenuineIntel') 'LENOVO' '21AB' 0",
            "amd": "$true 26100 'X64' @('AuthenticAMD') 'LENOVO' '21AB' 0",
            "mixed-cpu": "$true 26100 'X64' @('GenuineIntel','AuthenticAMD') 'LENOVO' '21AB' 0",
            "azure-vm": "$true 26100 'X64' @('GenuineIntel') 'Microsoft Corporation' 'Virtual Machine' 0",
            "vmware": "$true 26100 'X64' @('GenuineIntel') 'VMware, Inc.' 'VMware Virtual Platform' 0",
            "connected": "$true 26100 'X64' @('GenuineIntel') 'LENOVO' '21AB' 1",
        }
        for name, arguments in cases.items():
            with self.subTest(name=name):
                result = self.run_functions(
                    ["Assert-Gate9SupportedTargetFacts"],
                    rf"Assert-Gate9SupportedTargetFacts {arguments}",
                )
                self.assertNotEqual(0, result.returncode, result.stdout)

    def test_reads_only_canonical_successful_production_result(self):
        """Catch loss of exact stage, handoff, manifest, or sanitized result fields."""
        payload = (
            b'{"schema":"granite.hardware-inspection.gate9-production-run/v1",'
            b'"packageIdentityPresent":true,"outcome":"CompletedWithWarnings",'
            b'"stageCount":7,"handoffPresent":true,"manifestFieldCount":19,'
            b'"diagnostics":[]}\n'
        )
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "result.json"
            path.write_bytes(payload)
            result = self.run_functions(
                ["Read-Gate9ProductionResult"],
                r"""
                $result = Read-Gate9ProductionResult -Path $env:GRANITE_RESULT_PATH
                [ordered]@{
                    Outcome = $result.Outcome
                    StageCount = $result.StageCount
                    HandoffPresent = $result.HandoffPresent
                    ManifestFieldCount = $result.ManifestFieldCount
                    DiagnosticCount = $result.Diagnostics.Count
                } | ConvertTo-Json -Compress
                """,
                {"GRANITE_RESULT_PATH": str(path)},
            )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(
            {
                "Outcome": "CompletedWithWarnings",
                "StageCount": 7,
                "HandoffPresent": True,
                "ManifestFieldCount": 19,
                "DiagnosticCount": 0,
            },
            json.loads(result.stdout),
        )

    def test_rejects_mutated_production_results(self):
        """Catch permissive parsing of failed, raw, malformed, or incomplete runs."""
        self.assertTrue(CONTROLLER.is_file(), "Gate 9 controller is missing")
        valid = (
            b'{"schema":"granite.hardware-inspection.gate9-production-run/v1",'
            b'"packageIdentityPresent":true,"outcome":"Completed",'
            b'"stageCount":7,"handoffPresent":true,"manifestFieldCount":19,'
            b'"diagnostics":[]}\n'
        )
        mutations = {
            "bom": b"\xef\xbb\xbf" + valid,
            "crlf": valid[:-1] + b"\r\n",
            "extra-property": valid.replace(b'{"schema":', b'{"raw":"host","schema":', 1),
            "wrong-schema": valid.replace(b"production-run/v1", b"production-run/v2"),
            "no-package": valid.replace(b'"packageIdentityPresent":true', b'"packageIdentityPresent":false'),
            "failed": valid.replace(b'"outcome":"Completed"', b'"outcome":"Failed"'),
            "six-stages": valid.replace(b'"stageCount":7', b'"stageCount":6'),
            "no-handoff": valid.replace(b'"handoffPresent":true', b'"handoffPresent":false'),
            "wrong-manifest": valid.replace(b'"manifestFieldCount":19', b'"manifestFieldCount":18'),
            "diagnostic": valid.replace(b'"diagnostics":[]', b'"diagnostics":["HI-WARNING"]'),
            "oversize": b" " * 4096 + valid,
        }
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "result.json"
            for name, payload in mutations.items():
                with self.subTest(name=name):
                    path.write_bytes(payload)
                    result = self.run_functions(
                        ["Read-Gate9ProductionResult"],
                        "Read-Gate9ProductionResult -Path $env:GRANITE_RESULT_PATH",
                        {"GRANITE_RESULT_PATH": str(path)},
                    )
                    self.assertNotEqual(0, result.returncode, result.stdout)

    def test_pid_ancestry_includes_only_root_and_recursive_descendants(self):
        """Catch endpoint attribution to unrelated processes or loss of grandchildren."""
        result = self.run_functions(
            ["Get-Gate9OwnedProcessIds"],
            r"""
            $records = @(
                [pscustomobject]@{ ProcessId = 100; ParentProcessId = 10 },
                [pscustomobject]@{ ProcessId = 101; ParentProcessId = 100 },
                [pscustomobject]@{ ProcessId = 102; ParentProcessId = 101 },
                [pscustomobject]@{ ProcessId = 200; ParentProcessId = 10 },
                [pscustomobject]@{ ProcessId = 201; ParentProcessId = 200 })
            @(Get-Gate9OwnedProcessIds -RootProcessId 100 -ProcessRecords $records) -join ','
            """,
        )

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("100,101,102\n", result.stdout)

    def test_endpoint_classifier_flags_listen_bound_and_any_udp_for_owned_pids(self):
        """Catch a listener/UDP observation hidden by state or unrelated-PID filtering."""
        commands = {
            "none": (
                "-OwnedProcessIds @(100) "
                "-TcpRecords @([pscustomobject]@{OwningProcess=100;State='Established'}) "
                "-UdpRecords @()",
                False,
            ),
            "listen": (
                "-OwnedProcessIds @(100) "
                "-TcpRecords @([pscustomobject]@{OwningProcess=100;State='Listen'}) "
                "-UdpRecords @()",
                True,
            ),
            "bound": (
                "-OwnedProcessIds @(100) "
                "-TcpRecords @([pscustomobject]@{OwningProcess=100;State='Bound'}) "
                "-UdpRecords @()",
                True,
            ),
            "udp": (
                "-OwnedProcessIds @(100) -TcpRecords @() "
                "-UdpRecords @([pscustomobject]@{OwningProcess=100})",
                True,
            ),
            "unrelated": (
                "-OwnedProcessIds @(100) "
                "-TcpRecords @([pscustomobject]@{OwningProcess=200;State='Listen'}) "
                "-UdpRecords @([pscustomobject]@{OwningProcess=200})",
                False,
            ),
        }
        for name, (arguments, expected) in commands.items():
            with self.subTest(name=name):
                result = self.run_functions(
                    ["Test-Gate9OwnedEndpointRecords"],
                    f"Test-Gate9OwnedEndpointRecords {arguments}",
                )
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual(str(expected).lower() + "\n", result.stdout.lower())

    def test_writer_publishes_validator_accepted_developer_blocked_summary(self):
        """Catch noncanonical evidence or accidental promotion of the Developer package."""
        with tempfile.TemporaryDirectory() as directory:
            destination = Path(directory) / "gate9-engineering-acceptance.json"
            result = self.run_functions(
                ["Write-Gate9BytesAtomically", "Write-Gate9EngineeringSummary"],
                r"""
                $repetitions = @(
                    [pscustomobject]@{ Run=1; Outcome='CompletedWithWarnings'; Diagnostics=@() },
                    [pscustomobject]@{ Run=2; Outcome='CompletedWithWarnings'; Diagnostics=@() },
                    [pscustomobject]@{ Run=3; Outcome='CompletedWithWarnings'; Diagnostics=@() })
                Write-Gate9EngineeringSummary `
                    -DestinationPath $env:GRANITE_SUMMARY_PATH `
                    -ExpectedCommit ('a' * 40) `
                    -Repetitions $repetitions `
                    -SignatureKind 'Developer' `
                    -PublicTrustVerified $false `
                    -SmartAppControlVerified $false `
                    -SummaryValidatorPath $env:GRANITE_SUMMARY_VALIDATOR
                """,
                {
                    "GRANITE_SUMMARY_PATH": str(destination),
                    "GRANITE_SUMMARY_VALIDATOR": str(SUMMARY_VALIDATOR),
                },
            )
            payload = destination.read_bytes() if destination.exists() else b""

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("", result.stdout)
        self.assertTrue(payload.endswith(b"\n"))
        self.assertNotIn(b"\r", payload)
        value = json.loads(payload)
        self.assertEqual("EngineeringPassedReleaseBlocked", value["disposition"])
        self.assertEqual([], value["failures"])
        self.assertFalse(value["releaseTrust"]["publicTrustVerified"])
        self.assertFalse(value["releaseTrust"]["smartAppControlVerified"])

    def test_scripts_readme_documents_user_controlled_offline_gate9_run(self):
        """Catch an undocumented controller or instructions that imply network mutation."""
        readme = (REPOSITORY_ROOT / "scripts" / "README.md").read_text(encoding="utf-8")
        self.assertIn("Invoke-HardwareInspectionGate9Acceptance.ps1", readme)
        self.assertIn("Test-HardwareInspectionGate9Summary.ps1", readme)
        self.assertIn("user-controlled", readme)
        self.assertIn("physically disconnected", readme)
        self.assertIn("EngineeringPassedReleaseBlocked", readme)


if __name__ == "__main__":
    unittest.main()
