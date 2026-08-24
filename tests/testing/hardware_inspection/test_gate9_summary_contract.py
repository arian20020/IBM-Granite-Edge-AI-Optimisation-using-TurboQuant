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
    / "Test-HardwareInspectionGate9Summary.ps1"
)
EXPECTED_COMMIT = "a" * 40
FIXED_ERROR = "HI-GATE9-SUMMARY-INVALID: canonical summary validation failed."
VALID = (
    b'{"schema":"granite.hardware-inspection.gate9-engineering-acceptance/v1",'
    b'"classification":"local-sanitized","evaluatedCommit":"'
    + EXPECTED_COMMIT.encode("ascii")
    + b'","target":{"windows11":true,"x64":true,"intel":true,"physical":true},'
    b'"offline":true,"noRelevantNetworkEndpointObserved":true,"repetitions":['
    b'{"run":1,"packageIdentityPresent":true,"outcome":"CompletedWithWarnings",'
    b'"stageCount":7,"handoffPresent":true,"manifestFieldCount":19,"diagnostics":[]},'
    b'{"run":2,"packageIdentityPresent":true,"outcome":"CompletedWithWarnings",'
    b'"stageCount":7,"handoffPresent":true,"manifestFieldCount":19,"diagnostics":[]},'
    b'{"run":3,"packageIdentityPresent":true,"outcome":"CompletedWithWarnings",'
    b'"stageCount":7,"handoffPresent":true,"manifestFieldCount":19,"diagnostics":[]}],'
    b'"cleanupVerified":true,"failures":[],"releaseTrust":{'
    b'"signatureKind":"Developer","publicTrustVerified":false,'
    b'"smartAppControlVerified":false},'
    b'"disposition":"EngineeringPassedReleaseBlocked"}\n'
)


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


class Gate9SummaryContractTests(unittest.TestCase):
    def run_validator(self, payload, expected_commit=EXPECTED_COMMIT):
        with tempfile.TemporaryDirectory() as directory:
            summary_path = Path(directory) / "gate9-engineering-acceptance.json"
            summary_path.write_bytes(payload)
            command = [
                powershell_executable(),
                "-NoProfile",
                "-NonInteractive",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                str(VALIDATOR),
                "-Path",
                str(summary_path),
            ]
            if expected_commit is not None:
                command.extend(["-ExpectedCommit", expected_commit])
            return subprocess.run(
                command,
                cwd=REPOSITORY_ROOT,
                text=True,
                capture_output=True,
                timeout=15,
                check=False,
            )

    def assert_rejected(self, payload, expected_commit=EXPECTED_COMMIT):
        result = self.run_validator(payload, expected_commit)
        self.assertNotEqual(0, result.returncode, result.stdout)
        self.assertEqual("", result.stdout)
        self.assertEqual(FIXED_ERROR + "\n", result.stderr)

    def valid_object(self):
        return json.loads(VALID)

    def test_accepts_exact_developer_blocked_summary_without_output(self):
        """Catch rejection or disclosure for the canonical engineering-only result."""
        result = self.run_validator(VALID)

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("", result.stdout)
        self.assertEqual("", result.stderr)

    def test_accepts_matching_enterprise_or_store_release_pass(self):
        """Catch a validator that can never admit genuine matching release trust."""
        for signature_kind in ("Enterprise", "Store"):
            with self.subTest(signature_kind=signature_kind):
                value = self.valid_object()
                value["releaseTrust"] = {
                    "signatureKind": signature_kind,
                    "publicTrustVerified": True,
                    "smartAppControlVerified": True,
                }
                value["disposition"] = "Passed"

                result = self.run_validator(canonical(value))

                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual("", result.stdout)
                self.assertEqual("", result.stderr)

    def test_accepts_all_completed_repetitions_and_optional_commit_check(self):
        """Catch accidental coupling to warnings or a mandatory expected-commit switch."""
        value = self.valid_object()
        for repetition in value["repetitions"]:
            repetition["outcome"] = "Completed"

        result = self.run_validator(canonical(value), expected_commit=None)

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("", result.stdout)
        self.assertEqual("", result.stderr)

    def test_accepts_bounded_unique_sorted_safe_diagnostics(self):
        """Catch replacement of token validation with a blanket diagnostics rejection."""
        value = self.valid_object()
        value["repetitions"][0]["diagnostics"] = ["HI-A-WARNING", "HI-B-WARNING"]

        result = self.run_validator(canonical(value))

        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual("", result.stdout)
        self.assertEqual("", result.stderr)

    def test_rejects_noncanonical_byte_framing_and_encoding(self):
        """Catch BOM, CR, LF, UTF-8, size, and lexical framing drift."""
        mutations = {
            "bom": b"\xef\xbb\xbf" + VALID,
            "crlf": VALID[:-1] + b"\r\n",
            "missing-final-lf": VALID[:-1],
            "extra-final-lf": VALID + b"\n",
            "invalid-utf8": VALID[:-2] + b"\xff\n",
            "empty": b"",
            "oversize": b" " * (16 * 1024) + VALID,
            "leading-space": b" " + VALID,
            "trailing-space": VALID[:-1] + b" \n",
        }
        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)

    def test_rejects_property_duplicates_case_drift_order_and_shape_changes(self):
        """Catch property smuggling or a parser that ignores the closed ordered schema."""
        missing = self.valid_object()
        del missing["classification"]
        extra = self.valid_object()
        extra["rawHardware"] = "forbidden"
        target_extra = self.valid_object()
        target_extra["target"]["manufacturer"] = "forbidden"
        repetition_extra = self.valid_object()
        repetition_extra["repetitions"][0]["detail"] = "forbidden"
        reordered = self.valid_object()
        reordered["schema"] = reordered.pop("schema")
        mutations = {
            "duplicate": VALID.replace(
                b'{"schema":',
                b'{"schema":"ignored","schema":',
                1,
            ),
            "case-drift": VALID.replace(b'"offline":', b'"Offline":', 1),
            "missing": canonical(missing),
            "extra": canonical(extra),
            "target-extra": canonical(target_extra),
            "repetition-extra": canonical(repetition_extra),
            "reordered": canonical(reordered),
            "top-level-array": b"[]\n",
        }
        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)

    def test_rejects_wrong_schema_classification_or_commit(self):
        """Catch evidence from another schema, classification, or reviewed head."""
        mutations = {}
        for field, value in (
            ("schema", "granite.hardware-inspection.gate9-engineering-acceptance/v2"),
            ("classification", "public"),
            ("evaluatedCommit", "A" * 40),
            ("evaluatedCommit", "a" * 39),
        ):
            candidate = self.valid_object()
            candidate[field] = value
            mutations[f"{field}-{value[-1:]}"] = canonical(candidate)

        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)

        self.assert_rejected(VALID, expected_commit="b" * 40)
        self.assert_rejected(VALID, expected_commit="B" * 40)

    def test_rejects_false_or_missing_supported_target_facts(self):
        """Catch a summary retained from an unsupported or incompletely checked target."""
        mutations = {}
        for field in ("windows11", "x64", "intel", "physical"):
            false_value = self.valid_object()
            false_value["target"][field] = False
            mutations[f"{field}-false"] = canonical(false_value)
            missing_value = self.valid_object()
            del missing_value["target"][field]
            mutations[f"{field}-missing"] = canonical(missing_value)

        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)

    def test_rejects_incomplete_or_inconsistent_repetitions(self):
        """Catch partial, reordered, mismatched, or structurally invalid campaigns."""
        mutations = {}
        too_few = self.valid_object()
        too_few["repetitions"] = too_few["repetitions"][:2]
        mutations["too-few"] = canonical(too_few)
        too_many = self.valid_object()
        too_many["repetitions"].append(copy.deepcopy(too_many["repetitions"][-1]))
        too_many["repetitions"][-1]["run"] = 4
        mutations["too-many"] = canonical(too_many)
        wrong_order = self.valid_object()
        wrong_order["repetitions"][1]["run"] = 3
        mutations["wrong-order"] = canonical(wrong_order)
        inconsistent = self.valid_object()
        inconsistent["repetitions"][1]["outcome"] = "Completed"
        mutations["inconsistent-outcome"] = canonical(inconsistent)

        for field, value in (
            ("packageIdentityPresent", False),
            ("outcome", "Failed"),
            ("stageCount", 6),
            ("stageCount", True),
            ("handoffPresent", False),
            ("manifestFieldCount", 18),
        ):
            candidate = self.valid_object()
            candidate["repetitions"][1][field] = value
            mutations[f"{field}-{value}"] = canonical(candidate)

        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)

    def test_rejects_unsafe_duplicate_unsorted_or_excess_diagnostics(self):
        """Catch raw values or noncanonical diagnostic collections entering evidence."""
        mutations = {}
        for name, diagnostics in (
            ("unsafe", [r"C:\\Users\\Student"]),
            ("lowercase", ["hi-warning"]),
            ("duplicate", ["HI-WARNING", "HI-WARNING"]),
            ("unsorted", ["ZZ-WARNING", "AA-WARNING"]),
            ("too-many", [f"HI-WARNING-{index}" for index in range(9)]),
            ("too-long", ["H" * 65]),
        ):
            candidate = self.valid_object()
            candidate["repetitions"][0]["diagnostics"] = diagnostics
            mutations[name] = canonical(candidate)

        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)

    def test_rejects_failed_engineering_or_nonempty_failure_state(self):
        """Catch a failed preflight, endpoint observation, cleanup, or campaign."""
        mutations = {}
        for field in (
            "offline",
            "noRelevantNetworkEndpointObserved",
            "cleanupVerified",
        ):
            candidate = self.valid_object()
            candidate[field] = False
            mutations[field] = canonical(candidate)
        failures = self.valid_object()
        failures["failures"] = ["HI-GATE9-FAILED"]
        mutations["failures"] = canonical(failures)

        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)

    def test_rejects_invalid_or_contradictory_release_trust(self):
        """Catch Developer promotion and any trust/disposition contradiction."""
        mutations = {}
        invalid_kind = self.valid_object()
        invalid_kind["releaseTrust"]["signatureKind"] = "Public"
        mutations["invalid-kind"] = canonical(invalid_kind)
        developer_public = self.valid_object()
        developer_public["releaseTrust"]["publicTrustVerified"] = True
        mutations["developer-public"] = canonical(developer_public)
        developer_sac = self.valid_object()
        developer_sac["releaseTrust"]["smartAppControlVerified"] = True
        mutations["developer-sac"] = canonical(developer_sac)
        developer_passed = self.valid_object()
        developer_passed["disposition"] = "Passed"
        mutations["developer-passed"] = canonical(developer_passed)
        enterprise_false_passed = self.valid_object()
        enterprise_false_passed["releaseTrust"]["signatureKind"] = "Enterprise"
        enterprise_false_passed["disposition"] = "Passed"
        mutations["enterprise-false-passed"] = canonical(enterprise_false_passed)
        enterprise_true_blocked = self.valid_object()
        enterprise_true_blocked["releaseTrust"] = {
            "signatureKind": "Enterprise",
            "publicTrustVerified": True,
            "smartAppControlVerified": True,
        }
        mutations["enterprise-true-blocked"] = canonical(enterprise_true_blocked)
        sac_without_public = self.valid_object()
        sac_without_public["releaseTrust"] = {
            "signatureKind": "Store",
            "publicTrustVerified": False,
            "smartAppControlVerified": True,
        }
        mutations["sac-without-public"] = canonical(sac_without_public)

        for name, payload in mutations.items():
            with self.subTest(name=name):
                self.assert_rejected(payload)


if __name__ == "__main__":
    unittest.main()
