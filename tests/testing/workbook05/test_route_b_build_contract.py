from __future__ import annotations

import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
ROUTE_B_BUILD_SCRIPT = (
    REPOSITORY_ROOT
    / "scripts/testing/workbook05/Invoke-Workbook05RouteBBuild.ps1"
)


class RouteBBuildContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.text = ROUTE_B_BUILD_SCRIPT.read_text(encoding="utf-8")

    def test_requires_exact_br8_identity_digest_and_owner_acceptance_inputs(self) -> None:
        expected = (
            "Br8BundleDirectory",
            "Br8ExpectedArtifactDigest",
            "Br8ActualArtifactDigest",
            "Br8AcceptedByProjectOwner",
            "1827f6458d049de11c1a8203c793af67c99935dc",
            "route-b-experimental-qjl-polar",
            "C:\\w5b",
        )
        for token in expected:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_validates_br8_before_any_external_source_or_cmake_command(self) -> None:
        marker = self.text.index("ROUTE_B_BR8_PREREQUISITE_ACCEPTED")
        first_git = self.text.index("route-b-git-init")
        first_cmake = self.text.index("route-b-runtime-configure")
        self.assertLess(marker, first_git)
        self.assertLess(marker, first_cmake)
        self.assertIn(
            "scripts.testing.workbook05.route_b_repair_bundle_validation",
            self.text,
        )

    def test_rejects_digest_owner_decision_source_and_claim_mismatches(self) -> None:
        expected = (
            "$Br8ActualArtifactDigest -ne $Br8ExpectedArtifactDigest",
            "if (-not $Br8AcceptedByProjectOwner)",
            "$br8Decision.status -ne 'ExecutableCandidate'",
            "$br8Decision.source_commit -ne $SourceCommit",
            "$br8Decision.granite_model_test_authorised -ne $false",
            "$br8Decision.performance_claim_authorised -ne $false",
            "$br8Decision.quality_claim_authorised -ne $false",
        )
        for token in expected:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_rejects_algorithm_drift_and_requires_exact_changed_file(self) -> None:
        expected = (
            "$changedFiles.algorithm_files_changed -ne $false",
            "src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake",
            "$actualChangedFiles.Count -ne 1",
            "Repair changed an unexpected external file set",
        )
        for token in expected:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_requires_all_six_br8_cases_to_have_nonzero_unskipped_passes(self) -> None:
        for case_id in (
            "baseline_f32",
            "qjl4",
            "qjl3",
            "polar4",
            "polar3",
            "asymmetric_f32_tbq4",
        ):
            with self.subTest(case_id=case_id):
                self.assertIn(f"'{case_id}'", self.text)
        self.assertIn("$result.run_count -le 0", self.text)
        self.assertIn("$result.skipped_count -ne 0", self.text)
        self.assertIn("$result.passed -ne $true", self.text)

    def test_reapplies_only_reviewed_repair_and_repeats_narrow_gate_before_full_build(self) -> None:
        ordered_tokens = (
            "scripts.testing.workbook05.route_b_repair",
            "route-b-runtime-configure",
            "route-b-narrow-build",
            "route-b-gtest-discovery",
            "route-b-test-baseline_f32",
            "route-b-test-qjl4",
            "route-b-test-qjl3",
            "route-b-test-polar4",
            "route-b-test-polar3",
            "route-b-test-asymmetric_f32_tbq4",
            "route-b-runtime-build",
            "route-b-runtime-install",
        )
        positions = [self.text.index(token) for token in ordered_tokens]
        self.assertEqual(sorted(positions), positions)
        self.assertIn("$discoveredCount -le 0", self.text)
        self.assertIn("'--parallel', '2'", self.text)

    def test_records_text_evidence_and_keeps_all_later_claims_disabled(self) -> None:
        expected = (
            "source-provenance.json",
            "target-membership.json",
            "test-discovery-summary.json",
            "test-results.json",
            "binaries.json",
            "decision.json",
            "Write-Wb05Manifest",
            "granite_model_test_authorised = $false",
            "activation_claim_authorised = $false",
            "packed_storage_claim_authorised = $false",
            "performance_claim_authorised = $false",
            "quality_claim_authorised = $false",
        )
        for token in expected:
            with self.subTest(token=token):
                self.assertIn(token, self.text)

    def test_contains_no_model_execution_or_unreviewed_destructive_command(self) -> None:
        forbidden = (
            "huggingface.co",
            ".gguf",
            ".safetensors",
            "generate(",
            "benchmark_app",
            "git reset --hard",
            "git clean",
            "Invoke-Expression",
        )
        for token in forbidden:
            with self.subTest(token=token):
                self.assertNotIn(token.lower(), self.text.lower())


if __name__ == "__main__":
    unittest.main()
