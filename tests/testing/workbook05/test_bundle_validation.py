from __future__ import annotations

import json
import shutil
import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.bundle_validation import (
    validate_preflight_bundle,
)
from scripts.testing.workbook05.hash_manifest import write_hash_manifest


ROOT = Path(__file__).resolve().parents[3]


class BundleValidationTests(unittest.TestCase):
    def _create_bundle(self, destination: Path) -> None:
        campaign_root = (
            ROOT
            / "experiments/granite_turboquant_intel/manifests/campaigns/GTQ-WB05-MF-v1"
        )
        shutil.copytree(campaign_root, destination / "controls")
        report = json.loads(
            (
                ROOT
                / "experiments/granite_turboquant_intel/manifests/templates/workbook05/preflight-report-template.json"
            ).read_text(encoding="utf-8")
        )
        report["overall_status"] = "Passed"
        (destination / "preflight-report.json").write_text(
            json.dumps(report),
            encoding="utf-8",
        )
        (destination / "preflight-summary.md").write_text(
            "# Workbook 05 preflight\n",
            encoding="utf-8",
        )
        (destination / "environment-snapshot.json").write_text(
            "{}",
            encoding="utf-8",
        )
        write_hash_manifest(
            destination,
            destination / "hash-manifest.sha256",
        )

    def test_valid_bundle_has_no_issues(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            bundle = Path(temporary_directory)
            self._create_bundle(bundle)
            issues = validate_preflight_bundle(bundle, ROOT)
        self.assertEqual([], issues)

    def test_changed_file_breaks_the_hash_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            bundle = Path(temporary_directory)
            self._create_bundle(bundle)
            (bundle / "preflight-summary.md").write_text(
                "tampered",
                encoding="utf-8",
            )
            issues = validate_preflight_bundle(bundle, ROOT)
        self.assertTrue(
            any(issue.code == "HASH_MISMATCH" for issue in issues)
        )

    def test_secret_and_binary_are_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            bundle = Path(temporary_directory)
            self._create_bundle(bundle)
            (bundle / "leak.txt").write_text(
                "github_pat_0123456789abcdef",
                encoding="utf-8",
            )
            (bundle / "runtime.dll").write_bytes(b"MZ")
            write_hash_manifest(
                bundle,
                bundle / "hash-manifest.sha256",
            )
            issues = validate_preflight_bundle(bundle, ROOT)
        codes = {issue.code for issue in issues}
        self.assertIn("SECRET_PATTERN", codes)
        self.assertIn("FORBIDDEN_BINARY", codes)

    def test_bom_json_still_rejects_unsafe_evidence_paths(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            bundle = Path(temporary_directory)
            self._create_bundle(bundle)
            admission_path = (
                bundle / "controls/route-a-source-admission.json"
            )
            admission = json.loads(
                admission_path.read_text(encoding="utf-8-sig")
            )
            admission["proofs"][0]["evidence_path"] = "../escape"

            # Windows PowerShell 5.1 writes UTF-8 text with a byte-order mark.
            # The untrusted-bundle validator must parse that real output form,
            # not silently skip the JSON-specific path checks.
            admission_path.write_text(
                json.dumps(admission),
                encoding="utf-8-sig",
            )
            write_hash_manifest(
                bundle,
                bundle / "hash-manifest.sha256",
            )
            issues = validate_preflight_bundle(bundle, ROOT)

        self.assertTrue(
            any(
                issue.code == "UNSAFE_EVIDENCE_PATH"
                for issue in issues
            )
        )


if __name__ == "__main__":
    unittest.main()
