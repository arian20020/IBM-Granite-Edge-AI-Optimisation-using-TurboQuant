import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
SIGNING_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Invoke-SignedHardwareInspectionAcceptance.ps1"
)
GUEST_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1"
)


def powershell_executable():
    for name in ("powershell.exe", "powershell", "pwsh.exe", "pwsh"):
        candidate = shutil.which(name)
        if candidate:
            return candidate
    raise unittest.SkipTest("PowerShell executable is not available")


class DevelopmentAcceptanceBundleBoundaryTests(unittest.TestCase):
    def run_builder(self, destination):
        return subprocess.run(
            [
                powershell_executable(),
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(SIGNING_SCRIPT),
                "-CertificateThumbprint",
                "0" * 40,
                "-DevelopmentBundleDirectory",
                str(destination),
            ],
            cwd=REPOSITORY_ROOT,
            text=True,
            capture_output=True,
            timeout=30,
            check=False,
        )

    def assert_rejected_before_certificate_lookup(self, destination, message):
        result = self.run_builder(destination)
        self.assertNotEqual(0, result.returncode, result.stdout)
        self.assertIn(message, result.stderr)
        self.assertNotIn("Cert:\\CurrentUser", result.stderr)

    def test_builder_rejects_relative_bundle_directory(self):
        """Catch a bundle destination whose meaning depends on process CWD."""
        self.assert_rejected_before_certificate_lookup(
            Path("relative-development-bundle"),
            "The development bundle directory must be an absolute path.",
        )

    def test_builder_rejects_filesystem_root_bundle_directory(self):
        """Catch publication whose cleanup scope could become an entire drive."""
        filesystem_root = Path(tempfile.gettempdir()).anchor
        self.assert_rejected_before_certificate_lookup(
            Path(filesystem_root),
            "The development bundle directory must not be a filesystem root.",
        )

    def test_builder_rejects_repository_contained_bundle_directory(self):
        """Catch publication of signed binaries into a committable source tree."""
        with tempfile.TemporaryDirectory(dir=REPOSITORY_ROOT) as directory:
            self.assert_rejected_before_certificate_lookup(
                Path(directory),
                "The development bundle directory must be outside repository and build roots.",
            )

    def test_builder_rejects_nonempty_bundle_directory(self):
        """Catch stale or attacker-controlled files entering the closed bundle."""
        with tempfile.TemporaryDirectory() as directory:
            destination = Path(directory)
            (destination / "stale.bin").write_bytes(b"stale")
            self.assert_rejected_before_certificate_lookup(
                destination,
                "The development bundle directory must already exist and be empty.",
            )

    def test_builder_rejects_reparse_point_bundle_directory(self):
        """Catch publication through a redirected filesystem boundary."""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            target = root / "target"
            link = root / "link"
            target.mkdir()
            try:
                os.symlink(target, link, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"directory symlinks are unavailable: {error}")
            self.assert_rejected_before_certificate_lookup(
                link,
                "The development bundle directory must not be a reparse point.",
            )

    def test_bundle_writer_publishes_exact_hash_bound_inventory(self):
        """Catch missing, renamed, unbound, or noncanonical bundle members."""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package_source = root / "source.msix"
            certificate_source = root / "source.cer"
            guest_source = root / "source-guest.ps1"
            destination = root / "bundle"
            destination.mkdir()
            package_source.write_bytes(b"bounded-signed-msix")
            certificate_source.write_bytes(b"bounded-public-certificate")
            guest_source.write_bytes(b"bounded-guest-runner\n")

            environment = os.environ.copy()
            environment.update(
                {
                    "GRANITE_SIGNING_SCRIPT": str(SIGNING_SCRIPT),
                    "GRANITE_PACKAGE_SOURCE": str(package_source),
                    "GRANITE_CERTIFICATE_SOURCE": str(certificate_source),
                    "GRANITE_GUEST_SOURCE": str(guest_source),
                    "GRANITE_BUNDLE_DESTINATION": str(destination),
                }
            )
            probe = subprocess.run(
                [
                    powershell_executable(),
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-Command",
                    r"""
                    $tokens = $null
                    $errors = $null
                    $ast = [System.Management.Automation.Language.Parser]::ParseFile(
                        $env:GRANITE_SIGNING_SCRIPT,
                        [ref]$tokens,
                        [ref]$errors)
                    if ($errors.Count -ne 0) { throw 'Signing script did not parse.' }
                    $function = $ast.Find({
                        param($node)
                        $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                            $node.Name -ceq 'Write-DevelopmentAcceptanceBundle'
                    }, $true)
                    if ($null -eq $function) { throw 'Bundle writer function is missing.' }
                    Invoke-Expression $function.Extent.Text
                    Write-DevelopmentAcceptanceBundle `
                        -SignedPackagePath $env:GRANITE_PACKAGE_SOURCE `
                        -PublicCertificatePath $env:GRANITE_CERTIFICATE_SOURCE `
                        -GuestRunnerPath $env:GRANITE_GUEST_SOURCE `
                        -DestinationRoot $env:GRANITE_BUNDLE_DESTINATION `
                        -CertificateThumbprint ('A' * 40)
                    """,
                ],
                env=environment,
                text=True,
                capture_output=True,
                timeout=30,
                check=False,
            )
            self.assertEqual(0, probe.returncode, probe.stderr)
            self.assertEqual(
                {
                    "GraniteEdgeAI.UnitTests.msix",
                    "GraniteEdgeAI.cer",
                    "Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1",
                    "bundle-manifest.json",
                },
                {path.name for path in destination.iterdir()},
            )
            self.assertEqual(
                b'{"schema":"granite.hardware-inspection.development-acceptance-bundle/v1",'
                b'"packageName":"GraniteEdgeAI.WinUI.UnitTests",'
                b'"publisher":"CN=GraniteEdgeAI","version":"1.0.0.0",'
                b'"architecture":"x64",'
                b'"certificateThumbprint":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",'
                b'"files":['
                b'{"name":"GraniteEdgeAI.UnitTests.msix","length":19,'
                b'"sha256":"bc8b46eaa65a978ac136762846385487eec16569f9a7abfeae57434a89f57968"},'
                b'{"name":"GraniteEdgeAI.cer","length":26,'
                b'"sha256":"5145e8880846c892ed0dfeb2b7d43ac00ba86946ef319ce0d6a135dc0cac966f"},'
                b'{"name":"Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1","length":21,'
                b'"sha256":"a0339750f2a63c60b071d55d3d27e18c27f2ef5c9afb18aac36c46979caf3e58"}'
                b']}\n',
                (destination / "bundle-manifest.json").read_bytes(),
            )


class DevelopmentAcceptanceGuestBoundaryTests(unittest.TestCase):
    def run_guest_function(self, function_name, command, environment=None):
        probe_environment = os.environ.copy()
        probe_environment["GRANITE_GUEST_SCRIPT"] = str(GUEST_SCRIPT)
        if environment:
            probe_environment.update(environment)
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
                    $env:GRANITE_GUEST_SCRIPT,
                    [ref]$tokens,
                    [ref]$errors)
                if ($errors.Count -ne 0) {{ throw 'Guest script did not parse.' }}
                $function = $ast.Find({{
                    param($node)
                    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                        $node.Name -ceq '{function_name}'
                }}, $true)
                if ($null -eq $function) {{ throw 'Guest function is missing.' }}
                Invoke-Expression $function.Extent.Text
                {command}
                """,
            ],
            env=probe_environment,
            text=True,
            capture_output=True,
            timeout=30,
            check=False,
        )

    def create_closed_bundle(self, directory):
        root = Path(directory)
        payloads = {
            "GraniteEdgeAI.UnitTests.msix": b"bounded-msix-fixture",
            "GraniteEdgeAI.cer": b"bounded-certificate-fixture",
            "Invoke-HardwareInspectionDevelopmentAcceptanceGuest.ps1": (
                b"bounded-guest-runner-fixture\n"
            ),
        }
        for name, content in payloads.items():
            (root / name).write_bytes(content)
        manifest = {
            "schema": "granite.hardware-inspection.development-acceptance-bundle/v1",
            "packageName": "GraniteEdgeAI.WinUI.UnitTests",
            "publisher": "CN=GraniteEdgeAI",
            "version": "1.0.0.0",
            "architecture": "x64",
            "certificateThumbprint": "0" * 40,
            "files": [
                {
                    "name": name,
                    "length": len(payloads[name]),
                    "sha256": hashlib.sha256(payloads[name]).hexdigest(),
                }
                for name in sorted(payloads)
            ],
        }
        manifest_path = root / "bundle-manifest.json"
        manifest_path.write_bytes(
            json.dumps(manifest, ensure_ascii=False, separators=(",", ":")).encode(
                "utf-8"
            )
            + b"\n"
        )
        return manifest_path

    def run_guest(self, bundle_directory, result_directory):
        return subprocess.run(
            [
                powershell_executable(),
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(GUEST_SCRIPT),
                "-BundleDirectory",
                str(bundle_directory),
                "-ResultDirectory",
                str(result_directory),
                "-ConfirmDisposableGuest",
            ],
            cwd=REPOSITORY_ROOT,
            text=True,
            capture_output=True,
            timeout=30,
            check=False,
        )

    def assert_rejected_before_guest_mutation(
        self, bundle_directory, result_directory, message
    ):
        result = self.run_guest(bundle_directory, result_directory)
        self.assertNotEqual(0, result.returncode, result.stdout)
        self.assertIn(message, result.stderr)
        self.assertFalse((Path(result_directory) / "development-acceptance.json").exists())

    def test_guest_rejects_relative_bundle_directory(self):
        """Catch guest input whose identity depends on process CWD."""
        with tempfile.TemporaryDirectory() as result_directory:
            self.assert_rejected_before_guest_mutation(
                Path("relative-development-bundle"),
                Path(result_directory),
                "The bundle and result directories must be absolute paths.",
            )

    def test_guest_rejects_same_bundle_and_result_directory(self):
        """Catch output writes that could mutate the read-only input bundle."""
        with tempfile.TemporaryDirectory() as directory:
            self.assert_rejected_before_guest_mutation(
                Path(directory),
                Path(directory),
                "The bundle and result directories must be distinct.",
            )

    def test_guest_rejects_nonempty_result_directory(self):
        """Catch stale output being mistaken for this campaign's evidence."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                result_path = Path(result_directory)
                (result_path / "stale.json").write_text("stale", encoding="utf-8")
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    result_path,
                    "The result directory must already exist and be empty.",
                )

    def test_guest_rejects_reparse_point_bundle_directory(self):
        """Catch validation through a redirected bundle boundary."""
        with tempfile.TemporaryDirectory() as directory:
            with tempfile.TemporaryDirectory() as result_directory:
                root = Path(directory)
                target = root / "target"
                link = root / "link"
                target.mkdir()
                try:
                    os.symlink(target, link, target_is_directory=True)
                except OSError as error:
                    self.skipTest(f"directory symlinks are unavailable: {error}")
                self.assert_rejected_before_guest_mutation(
                    link,
                    Path(result_directory),
                    "The bundle and result directories must not be reparse points.",
                )

    def test_guest_rejects_reparse_point_result_directory(self):
        """Catch evidence publication through a redirected output boundary."""
        with tempfile.TemporaryDirectory() as directory:
            with tempfile.TemporaryDirectory() as bundle_directory:
                root = Path(directory)
                target = root / "target"
                link = root / "link"
                target.mkdir()
                try:
                    os.symlink(target, link, target_is_directory=True)
                except OSError as error:
                    self.skipTest(f"directory symlinks are unavailable: {error}")
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    link,
                    "The bundle and result directories must not be reparse points.",
                )

    def test_guest_rejects_payload_hash_drift(self):
        """Catch a payload replacement after the host bound its SHA-256."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                self.create_closed_bundle(bundle_directory)
                (Path(bundle_directory) / "GraniteEdgeAI.UnitTests.msix").write_bytes(
                    b"replaced-msix-fixture"
                )
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "A development bundle payload differs from its manifest.",
                )

    def test_guest_rejects_payload_length_drift(self):
        """Catch a payload whose recorded byte length was altered independently."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                manifest_path = self.create_closed_bundle(bundle_directory)
                manifest = manifest_path.read_bytes()
                manifest_path.write_bytes(
                    manifest.replace(b'"length":27', b'"length":28', 1)
                )
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "A development bundle payload differs from its manifest.",
                )

    def test_guest_rejects_missing_bundle_file(self):
        """Catch a manifest-bound payload removed before guest validation."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                self.create_closed_bundle(bundle_directory)
                (Path(bundle_directory) / "GraniteEdgeAI.cer").unlink()
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "The development bundle file inventory is not exact.",
                )

    def test_guest_rejects_unlisted_bundle_file(self):
        """Catch attacker-controlled material outside the manifest inventory."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                self.create_closed_bundle(bundle_directory)
                (Path(bundle_directory) / "unexpected.bin").write_bytes(b"unexpected")
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "The development bundle file inventory is not exact.",
                )

    def test_guest_rejects_crlf_manifest(self):
        """Catch a manifest whose framing is not the canonical one-LF form."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                manifest_path = self.create_closed_bundle(bundle_directory)
                manifest_path.write_bytes(manifest_path.read_bytes()[:-1] + b"\r\n")
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "The development bundle manifest has forbidden framing.",
                )

    def test_guest_rejects_utf8_bom_manifest(self):
        """Catch a byte-order mark outside the canonical manifest framing."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                manifest_path = self.create_closed_bundle(bundle_directory)
                manifest_path.write_bytes(b"\xef\xbb\xbf" + manifest_path.read_bytes())
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "The development bundle manifest has forbidden framing.",
                )

    def test_guest_rejects_malformed_utf8_manifest(self):
        """Catch invalid UTF-8 even when length and LF framing remain valid."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                manifest_path = self.create_closed_bundle(bundle_directory)
                manifest = manifest_path.read_bytes()
                manifest_path.write_bytes(manifest[:-2] + b"\xff\n")
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "The development bundle manifest is not strict UTF-8.",
                )

    def test_guest_rejects_unknown_manifest_property(self):
        """Catch schema expansion that could smuggle unvalidated instructions."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                manifest_path = self.create_closed_bundle(bundle_directory)
                manifest = manifest_path.read_bytes()
                manifest_path.write_bytes(
                    manifest.replace(b'{"schema":', b'{"unexpected":true,"schema":', 1)
                )
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "The development bundle manifest does not match the closed JSON-v1 grammar.",
                )

    def test_guest_rejects_wrong_manifest_package_identity(self):
        """Catch a bundle that requests installation of a different package name."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                manifest_path = self.create_closed_bundle(bundle_directory)
                manifest = manifest_path.read_bytes()
                manifest_path.write_bytes(
                    manifest.replace(
                        b"GraniteEdgeAI.WinUI.UnitTests",
                        b"GraniteEdgeAI.WinUI.OtherTest",
                        1,
                    )
                )
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "The development bundle manifest does not match the closed JSON-v1 grammar.",
                )

    def test_guest_validates_closed_bundle_before_certificate_parsing(self):
        """Catch a validator that rejects a complete hash-bound bundle prematurely."""
        with tempfile.TemporaryDirectory() as bundle_directory:
            with tempfile.TemporaryDirectory() as result_directory:
                self.create_closed_bundle(bundle_directory)
                self.assert_rejected_before_guest_mutation(
                    Path(bundle_directory),
                    Path(result_directory),
                    "The development bundle public certificate is invalid.",
                )

    def test_guest_rejects_certificate_thumbprint_mismatch(self):
        """Catch substitution of a different otherwise-valid signing certificate."""
        probe = self.run_guest_function(
            "Assert-DevelopmentBundleCertificate",
            r"""
            $certificate = [pscustomobject]@{
                Thumbprint = ('B' * 40)
                Subject = 'CN=GraniteEdgeAI'
                NotBefore = [DateTime]::UtcNow.AddDays(-1)
                NotAfter = [DateTime]::UtcNow.AddDays(1)
                HasPrivateKey = $false
                EnhancedKeyUsageList = @(
                    [pscustomobject]@{ ObjectId = '1.3.6.1.5.5.7.3.3' })
            }
            Assert-DevelopmentBundleCertificate `
                -Certificate $certificate `
                -ExpectedThumbprint ('A' * 40) `
                -Now ([DateTime]::UtcNow)
            """,
        )
        self.assertNotEqual(0, probe.returncode, probe.stdout)
        self.assertIn(
            "The development bundle public certificate does not satisfy the closed contract.",
            probe.stderr,
        )

    def test_guest_accepts_exact_public_code_signing_certificate_contract(self):
        """Catch rejection of the one certificate shape admitted by the bundle."""
        probe = self.run_guest_function(
            "Assert-DevelopmentBundleCertificate",
            r"""
            $certificate = [pscustomobject]@{
                Thumbprint = ('A' * 40)
                Subject = 'CN=GraniteEdgeAI'
                NotBefore = [DateTime]::UtcNow.AddDays(-1)
                NotAfter = [DateTime]::UtcNow.AddDays(1)
                HasPrivateKey = $false
                EnhancedKeyUsageList = @(
                    [pscustomobject]@{ ObjectId = '1.3.6.1.5.5.7.3.3' })
            }
            Assert-DevelopmentBundleCertificate `
                -Certificate $certificate `
                -ExpectedThumbprint ('A' * 40) `
                -Now ([DateTime]::UtcNow)
            """,
        )
        self.assertEqual(0, probe.returncode, probe.stderr)

    def test_guest_reads_canonical_all_pass_acceptance_result(self):
        """Catch a parser that loses the package identity or exact pass counters."""
        with tempfile.TemporaryDirectory() as directory:
            result_path = Path(directory) / "acceptance.json"
            result_path.write_bytes(
                b'{"schema":"granite.hardware-inspection.process-acceptance/v1",'
                b'"packageIdentityPresent":true,"total":2,"passed":2,"failed":[]}\n'
            )
            probe = self.run_guest_function(
                "Read-DevelopmentAcceptanceResult",
                r"""
                $result = Read-DevelopmentAcceptanceResult -Path $env:GRANITE_RESULT_PATH
                [pscustomobject]@{
                    PackageIdentityPresent = $result.PackageIdentityPresent
                    Total = $result.Total
                    Passed = $result.Passed
                    FailedCount = $result.Failed.Count
                } | ConvertTo-Json -Compress
                """,
                {"GRANITE_RESULT_PATH": str(result_path)},
            )
            self.assertEqual(0, probe.returncode, probe.stderr)
            self.assertEqual(
                {
                    "PackageIdentityPresent": True,
                    "Total": 2,
                    "Passed": 2,
                    "FailedCount": 0,
                },
                json.loads(probe.stdout),
            )

    def test_guest_rejects_acceptance_result_with_failures(self):
        """Catch a campaign that treats a well-formed failing run as acceptance."""
        with tempfile.TemporaryDirectory() as directory:
            result_path = Path(directory) / "acceptance.json"
            result_path.write_bytes(
                b'{"schema":"granite.hardware-inspection.process-acceptance/v1",'
                b'"packageIdentityPresent":true,"total":2,"passed":1,'
                b'"failed":["HardwareInspection.Failed"]}\n'
            )
            probe = self.run_guest_function(
                "Read-DevelopmentAcceptanceResult",
                "Read-DevelopmentAcceptanceResult -Path $env:GRANITE_RESULT_PATH",
                {"GRANITE_RESULT_PATH": str(result_path)},
            )
            self.assertNotEqual(0, probe.returncode, probe.stdout)
            self.assertIn(
                "A development acceptance repetition did not pass completely.",
                probe.stderr,
            )

    def test_guest_writes_canonical_three_run_development_summary(self):
        """Catch noncanonical, overbroad, or falsely public-trusted campaign evidence."""
        with tempfile.TemporaryDirectory() as result_directory:
            probe = self.run_guest_function(
                "Write-DevelopmentAcceptanceSummary",
                r"""
                $repetitions = @(
                    [pscustomobject]@{ Run = 1; PackageIdentityPresent = $true; Total = 2; Passed = 2; SignatureKind = 'Developer' },
                    [pscustomobject]@{ Run = 2; PackageIdentityPresent = $true; Total = 2; Passed = 2; SignatureKind = 'Developer' },
                    [pscustomobject]@{ Run = 3; PackageIdentityPresent = $true; Total = 2; Passed = 2; SignatureKind = 'Developer' })
                Write-DevelopmentAcceptanceSummary `
                    -ResultRoot $env:GRANITE_RESULT_DIRECTORY `
                    -Repetitions $repetitions
                """,
                {"GRANITE_RESULT_DIRECTORY": str(result_directory)},
            )
            self.assertEqual(0, probe.returncode, probe.stderr)
            self.assertEqual(
                b'{"schema":"granite.hardware-inspection.development-acceptance/v1",'
                b'"classification":"development-only","publicTrustVerified":false,'
                b'"smartAppControlVerified":false,"repetitions":['
                b'{"run":1,"packageIdentityPresent":true,"total":2,"passed":2,'
                b'"signatureKind":"Developer"},'
                b'{"run":2,"packageIdentityPresent":true,"total":2,"passed":2,'
                b'"signatureKind":"Developer"},'
                b'{"run":3,"packageIdentityPresent":true,"total":2,"passed":2,'
                b'"signatureKind":"Developer"}],"failures":[]}\n',
                (Path(result_directory) / "development-acceptance.json").read_bytes(),
            )

    def test_guest_refuses_summary_without_exactly_three_repetitions(self):
        """Catch publication of evidence from an incomplete campaign."""
        with tempfile.TemporaryDirectory() as result_directory:
            probe = self.run_guest_function(
                "Write-DevelopmentAcceptanceSummary",
                r"""
                Write-DevelopmentAcceptanceSummary `
                    -ResultRoot $env:GRANITE_RESULT_DIRECTORY `
                    -Repetitions @(
                        [pscustomobject]@{ Run = 1; PackageIdentityPresent = $true; Total = 2; Passed = 2; SignatureKind = 'Developer' })
                """,
                {"GRANITE_RESULT_DIRECTORY": str(result_directory)},
            )
            self.assertNotEqual(0, probe.returncode, probe.stdout)
            self.assertIn(
                "Development acceptance requires exactly three repetitions.",
                probe.stderr,
            )
            self.assertFalse(
                (Path(result_directory) / "development-acceptance.json").exists()
            )

    def test_guest_accepts_only_exact_normally_installed_package_identity(self):
        """Catch drift from the signed x64 Developer package and fixed App AUMID."""
        probe = self.run_guest_function(
            "Get-DevelopmentAcceptanceAumid",
            r"""
            $package = [pscustomobject]@{
                Name = 'GraniteEdgeAI.WinUI.UnitTests'
                Publisher = 'CN=GraniteEdgeAI'
                Version = [Version]'1.0.0.0'
                Architecture = 'X64'
                SignatureKind = 'Developer'
                InstallLocation = 'C:\Program Files\WindowsApps\ExactPackage'
                PackageFamilyName = 'ExactFamily'
            }
            Get-DevelopmentAcceptanceAumid `
                -Package $package `
                -BundleRoot 'C:\GraniteAcceptance\Bundle' `
                -ResultRoot 'C:\GraniteAcceptance\Result'
            """,
        )
        self.assertEqual(0, probe.returncode, probe.stderr)
        self.assertEqual("ExactFamily!App", probe.stdout.strip())

    def test_guest_rejects_package_installed_inside_bundle(self):
        """Catch loose or attacker-controlled execution from the transfer directory."""
        probe = self.run_guest_function(
            "Get-DevelopmentAcceptanceAumid",
            r"""
            $package = [pscustomobject]@{
                Name = 'GraniteEdgeAI.WinUI.UnitTests'
                Publisher = 'CN=GraniteEdgeAI'
                Version = [Version]'1.0.0.0'
                Architecture = 'X64'
                SignatureKind = 'Developer'
                InstallLocation = 'C:\GraniteAcceptance\Bundle\LoosePackage'
                PackageFamilyName = 'ExactFamily'
            }
            Get-DevelopmentAcceptanceAumid `
                -Package $package `
                -BundleRoot 'C:\GraniteAcceptance\Bundle' `
                -ResultRoot 'C:\GraniteAcceptance\Result'
            """,
        )
        self.assertNotEqual(0, probe.returncode, probe.stdout)
        self.assertIn(
            "The installed test package does not match the closed identity contract.",
            probe.stderr,
        )


if __name__ == "__main__":
    unittest.main()
