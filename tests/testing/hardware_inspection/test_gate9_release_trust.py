import copy
import json
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
VALIDATOR = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Test-HardwareInspectionGate9ReleaseTrust.ps1"
)
CONTROLLER = (
    REPOSITORY_ROOT
    / "scripts"
    / "hardware-inspection"
    / "Invoke-HardwareInspectionGate9Acceptance.ps1"
)
PACKAGE_HASH = "b" * 64
EXPECTED_COMMIT = "a" * 40
EXPECTED_PUBLISHER = "CN=GraniteEdgeAI"
FIXED_ERROR = "HI-GATE9-RELEASE-TRUST-INVALID: release trust validation failed."


def powershell_executable():
    for name in ("powershell.exe", "powershell", "pwsh.exe", "pwsh"):
        candidate = shutil.which(name)
        if candidate:
            return candidate
    raise unittest.SkipTest("PowerShell executable is not available")


def canonical(value):
    return json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode(
        "utf-8"
    ) + b"\n"


def valid_record(signature_kind="Enterprise"):
    return {
        "schema": "granite.hardware-inspection.gate9-release-trust/v1",
        "classification": "local-sanitized",
        "packageSha256": PACKAGE_HASH,
        "evaluatedCommit": EXPECTED_COMMIT,
        "publisher": EXPECTED_PUBLISHER,
        "signatureKind": signature_kind,
        "certificateTimeValidityVerified": True,
        "codeSigningEkuVerified": True,
        "publicChainVerified": True,
        "timestampVerified": True,
        "smartAppControlVerified": True,
    }


class Gate9ReleaseTrustTests(unittest.TestCase):
    def run_validator(self, payload, **overrides):
        with tempfile.TemporaryDirectory() as directory:
            record_path = Path(directory) / "gate9-release-trust.json"
            record_path.write_bytes(payload)
            command = [
                powershell_executable(),
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(VALIDATOR),
                "-Path",
                str(record_path),
                "-ExpectedPackageSha256",
                overrides.get("package_hash", PACKAGE_HASH),
                "-ExpectedPublisher",
                overrides.get("publisher", EXPECTED_PUBLISHER),
                "-ExpectedCommit",
                overrides.get("commit", EXPECTED_COMMIT),
            ]
            return subprocess.run(
                command,
                cwd=REPOSITORY_ROOT,
                text=True,
                capture_output=True,
                timeout=15,
                check=False,
            )

    def assert_rejected(self, payload, **overrides):
        result = self.run_validator(payload, **overrides)
        self.assertNotEqual(0, result.returncode, result.stdout)
        self.assertEqual("", result.stdout)
        self.assertEqual(FIXED_ERROR + "\n", result.stderr)

    def test_structurally_accepts_exact_enterprise_and_store_records(self):
        """Prove only the parser contract; synthetic fixtures are not real trust."""
        for signature_kind in ("Enterprise", "Store"):
            with self.subTest(signature_kind=signature_kind):
                result = self.run_validator(canonical(valid_record(signature_kind)))
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual("", result.stdout)
                self.assertEqual("", result.stderr)

    def test_rejects_developer_self_signed_and_unknown_signature_kinds(self):
        """Catch promotion of the existing Developer certificate or kind drift."""
        for signature_kind in ("Developer", "Unknown", "enterprise", ""):
            with self.subTest(signature_kind=signature_kind):
                self.assert_rejected(canonical(valid_record(signature_kind)))

    def test_rejects_wrong_package_commit_or_publisher_binding(self):
        """Catch reuse of trust evidence for a different reviewed payload."""
        mutations = {
            "package": {"packageSha256": "c" * 64},
            "commit": {"evaluatedCommit": "d" * 40},
            "publisher": {"publisher": "CN=SomeoneElse"},
        }
        for name, change in mutations.items():
            with self.subTest(name=name):
                value = valid_record()
                value.update(change)
                self.assert_rejected(canonical(value))

    def test_rejects_unverified_certificate_time_eku_chain_timestamp_or_sac(self):
        """Catch expired/future, wrong-purpose, private-root, untimestamped, or blocked trust."""
        boolean_fields = (
            "certificateTimeValidityVerified",
            "codeSigningEkuVerified",
            "publicChainVerified",
            "timestampVerified",
            "smartAppControlVerified",
        )
        for field in boolean_fields:
            with self.subTest(field=field):
                value = valid_record()
                value[field] = False
                self.assert_rejected(canonical(value))

    def test_rejects_non_boolean_verification_claims(self):
        """Catch truthy strings, numbers, null, and unavailable state."""
        for invalid in ("true", 1, None, "Unavailable"):
            with self.subTest(value=invalid):
                value = valid_record()
                value["smartAppControlVerified"] = invalid
                self.assert_rejected(canonical(value))

    def test_rejects_unsafe_or_noncanonical_publisher_text(self):
        """Catch raw distinguished names and unsafe text entering retained evidence."""
        for publisher in (
            "CN=GraniteEdgeAI\nOU=Raw",
            "CN=GraniteEdgeAI, O=Raw Organization",
            "CN=GraniteEdgeAI\\Injected",
            "cn=graniteedgeai",
        ):
            with self.subTest(publisher=publisher):
                value = valid_record()
                value["publisher"] = publisher
                self.assert_rejected(canonical(value))

    def test_rejects_schema_smuggling_order_and_shape_changes(self):
        """Catch extra, missing, duplicate, reordered, or case-drifted properties."""
        missing = valid_record()
        del missing["timestampVerified"]
        extra = valid_record()
        extra["certificateBytes"] = "forbidden"
        reordered = valid_record()
        reordered["schema"] = reordered.pop("schema")
        mutations = {
            "missing": canonical(missing),
            "extra": canonical(extra),
            "reordered": canonical(reordered),
            "case-drift": canonical(valid_record()).replace(
                b'"publisher":', b'"Publisher":', 1
            ),
            "duplicate": canonical(valid_record()).replace(
                b'{"schema":', b'{"schema":"ignored","schema":', 1
            ),
            "array": b"[]\n",
        }
        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)

    def test_rejects_noncanonical_bounded_utf8_lf_framing(self):
        """Catch BOM, CR, malformed UTF-8, whitespace, LF, and size drift."""
        valid = canonical(valid_record())
        mutations = {
            "bom": b"\xef\xbb\xbf" + valid,
            "crlf": valid[:-1] + b"\r\n",
            "missing-lf": valid[:-1],
            "extra-lf": valid + b"\n",
            "invalid-utf8": valid[:-2] + b"\xff\n",
            "leading-space": b" " + valid,
            "trailing-space": valid[:-1] + b" \n",
            "oversize": b" " * (8 * 1024) + valid,
            "empty": b"",
        }
        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)

    def test_rejects_invalid_expected_arguments_with_fixed_error(self):
        """Catch weak caller bindings and exception-detail disclosure."""
        valid = canonical(valid_record())
        mutations = (
            {"package_hash": "B" * 64},
            {"commit": "A" * 40},
            {"publisher": "CN=GraniteEdgeAI, O=Raw"},
        )
        for overrides in mutations:
            with self.subTest(overrides=overrides):
                self.assert_rejected(valid, **overrides)

    def test_validator_and_controller_never_mutate_trust_or_reread_unlocked(self):
        """Catch certificate-store mutation or trust-record TOCTOU regression."""
        validator = VALIDATOR.read_text(encoding="utf-8").lower()
        controller = CONTROLLER.read_text(encoding="utf-8").lower()
        for forbidden in (
            "add-certificatetrustlist",
            "cert:\\",
            "x509store",
            "trustedpeople",
            "trustedpublisher",
            "import-certificate",
            "set-mppreference",
        ):
            self.assertNotIn(forbidden, validator)
        self.assertIn("[io.file]::open(", controller)
        self.assertIn("[io.fileshare]::read", controller)
        self.assertNotIn(
            "get-content -literalpath $releasetrustrecord", controller
        )


if __name__ == "__main__":
    unittest.main()
