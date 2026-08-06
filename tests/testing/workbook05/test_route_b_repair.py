from __future__ import annotations

import unittest
from pathlib import Path

# The implementation deliberately did not exist in the first TDD commit. The
# initial workflow therefore failed before this module was introduced, providing
# the required red evidence for the new behaviour.
from scripts.testing.workbook05.route_b_repair import (
    RouteBContractError,
    repair_target_per_test_text,
    verify_benchmark_contract,
)


# Model the complete architecture/common source-list shape at the exact pinned
# experimental commit. A partial fixture would weaken the fail-closed contract.
BROKEN_CMAKE = """\
if(X86_64)
    file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/instances/x64/${TEST_CLASS_FILE_NAME})
    file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/x64/${TEST_CLASS_FILE_NAME})
elseif(ARM OR AARCH64)
    file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/instances/arm/${TEST_CLASS_FILE_NAME})
elseif(RISCV64)
    file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/instances/riscv64/${TEST_CLASS_FILE_NAME})
endif()
file(GLOB_RECURSE LIST_OF_TEST_COMMON_INSTANCES ${TEST_DIR}/instances/common/${TEST_CLASS_FILE_NAME})
file(GLOB_RECURSE LIST_OF_TEST_COMMON_INSTANCES ${TEST_DIR}/common/${TEST_CLASS_FILE_NAME})
"""

EXPECTED_REPAIRED_FRAGMENTS = (
    "LIST_OF_TEST_ARCH_INSTANCES_INSTANCES_X64",
    "LIST_OF_TEST_ARCH_INSTANCES_X64",
    "LIST_OF_TEST_ARCH_INSTANCES_INSTANCES_ARM",
    "LIST_OF_TEST_ARCH_INSTANCES_INSTANCES_RISCV64",
    "list(APPEND LIST_OF_TEST_ARCH_INSTANCES",
    "LIST_OF_TEST_COMMON_INSTANCES_INSTANCES_COMMON",
    "LIST_OF_TEST_COMMON_INSTANCES_COMMON",
    "list(APPEND LIST_OF_TEST_COMMON_INSTANCES",
)

BENCHMARK_SOURCE = """\
std::vector<ov::AnyMap> precisions() {
    std::vector<ov::AnyMap> config{{{ov::hint::inference_precision.name(), std::string(\"f32\")}}};
    return config;
}
const std::vector<std::string> mode_none = {\"none\"};
const std::vector<std::string> mode_tbq = {\"tbq4\", \"tbq3\", \"tbq4_qjl\", \"tbq3_qjl\"};
INSTANTIATE_TEST_SUITE_P(benchmark_KVCacheBench_llama8b_Kf32_Vtbq,
                         ConcatSDPKVBenchTest,
                         ::testing::Combine(::testing::ValuesIn(precisions()),
                                            ::testing::ValuesIn(benchShapes_llama8b),
                                            ::testing::ValuesIn(mode_none),
                                            ::testing::ValuesIn(mode_tbq),
                                            ::testing::ValuesIn(gs_default),
                                            ::testing::ValuesIn(rot_wht)),
                         ConcatSDPKVBenchBase::getTestCaseName);
"""


class RouteBRepairTests(unittest.TestCase):
    def test_repair_accumulates_all_architecture_and_common_globs(self) -> None:
        repaired = repair_target_per_test_text(BROKEN_CMAKE)

        for expected in EXPECTED_REPAIRED_FRAGMENTS:
            self.assertIn(expected, repaired)

        # The unsafe repeated base-variable assignments must no longer remain.
        self.assertNotIn(
            "file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/instances/x64/",
            repaired,
        )
        self.assertNotIn(
            "file(GLOB_RECURSE LIST_OF_TEST_COMMON_INSTANCES ${TEST_DIR}/instances/common/",
            repaired,
        )

    def test_repair_resets_accumulators_for_each_test_class(self) -> None:
        repaired = repair_target_per_test_text(BROKEN_CMAKE)

        # BR6 linked concat_sdp.cpp into the concat_sdp_turboq target because the
        # new APPEND variables survived from the preceding foreach iteration.
        # Each class must start from empty architecture and common instance lists.
        arch_reset = "set(LIST_OF_TEST_ARCH_INSTANCES)"
        common_reset = "set(LIST_OF_TEST_COMMON_INSTANCES)"
        first_arch_glob = "file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES_INSTANCES_X64"
        first_common_glob = "file(GLOB_RECURSE LIST_OF_TEST_COMMON_INSTANCES_INSTANCES_COMMON"

        self.assertEqual(1, repaired.count(arch_reset))
        self.assertEqual(1, repaired.count(common_reset))
        self.assertLess(repaired.index(arch_reset), repaired.index(first_arch_glob))
        self.assertLess(repaired.index(common_reset), repaired.index(first_common_glob))

    def test_repair_is_idempotent(self) -> None:
        repaired_once = repair_target_per_test_text(BROKEN_CMAKE)
        repaired_twice = repair_target_per_test_text(repaired_once)

        self.assertEqual(repaired_once, repaired_twice)

    def test_repair_fails_closed_when_expected_source_shape_is_missing(self) -> None:
        with self.assertRaises(RouteBContractError):
            repair_target_per_test_text("set(UNRELATED true)\n")

    def test_repair_fails_closed_when_one_reviewed_directory_is_missing(self) -> None:
        incomplete = BROKEN_CMAKE.replace(
            "    file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/instances/riscv64/${TEST_CLASS_FILE_NAME})\n",
            "",
        )

        with self.assertRaisesRegex(RouteBContractError, "reviewed set"):
            repair_target_per_test_text(incomplete)

    def test_exact_benchmark_contract_already_has_f32_and_asymmetric_modes(self) -> None:
        decision = verify_benchmark_contract(BENCHMARK_SOURCE)

        self.assertTrue(decision.has_unconditional_f32)
        self.assertTrue(decision.has_k_f32_v_tbq)
        self.assertEqual((), decision.reasons)

    def test_benchmark_contract_reports_missing_guards_without_rewriting_source(self) -> None:
        decision = verify_benchmark_contract("std::vector<int> unrelated;\n")

        self.assertFalse(decision.has_unconditional_f32)
        self.assertFalse(decision.has_k_f32_v_tbq)
        self.assertEqual(2, len(decision.reasons))

    def test_workflow_has_intel_collection_and_independent_hosted_validation(self) -> None:
        repository_root = Path(__file__).resolve().parents[3]
        workflow = (
            repository_root / ".github" / "workflows" / "workbook-05-route-b-repair.yml"
        ).read_text(encoding="utf-8")

        self.assertIn("collect-route-b-evidence:", workflow)
        self.assertIn("validate-route-b-evidence:", workflow)
        self.assertIn("github.event.pull_request.head.repo.full_name == github.repository", workflow)
        self.assertIn("github.event.pull_request.head.ref == 'testing/workbook-05-route-b-repair-implementation'", workflow)
        for label in ("self-hosted", "Windows", "X64", "workbook05", "intel-target"):
            self.assertIn(f"- {label}", workflow)
        self.assertIn("Invoke-Workbook05RouteBRepair.ps1", workflow)
        self.assertIn("actions/upload-artifact@bbbca2ddaa5d8feaa63e36b76fdaad77386f024f", workflow)
        self.assertIn("needs: collect-route-b-evidence", workflow)
        self.assertIn("actions/download-artifact@974686ed5098c7f9c9289ec946b9058e496a2561", workflow)
        self.assertIn("route_b_repair_bundle_validation", workflow)
        self.assertIn("workbook-05-route-b-repair-${{ github.run_id }}-${{ github.run_attempt }}", workflow)

    def test_orchestrator_runs_nonzero_baseline_and_each_required_candidate_family(self) -> None:
        repository_root = Path(__file__).resolve().parents[3]
        orchestrator = (
            repository_root
            / "scripts"
            / "testing"
            / "workbook05"
            / "Invoke-Workbook05RouteBRepair.ps1"
        ).read_text(encoding="utf-8")

        expected_filters = (
            "baseline_f32 = '*Prc=f32*K=none_V=none*'",
            "qjl4 = '*Prc=f32*K=tbq4_qjl_V=tbq4_qjl*'",
            "qjl3 = '*Prc=f32*K=tbq3_qjl_V=tbq3_qjl*'",
            "polar4 = '*Prc=f32*K=polar4_V=polar4*'",
            "polar3 = '*Prc=f32*K=polar3_V=polar3*'",
            "asymmetric_f32_tbq4 = '*Prc=f32*K=none_V=tbq4*'",
        )
        for expected_filter in expected_filters:
            self.assertIn(expected_filter, orchestrator)

        self.assertIn("$testResults.Count -ne 6", orchestrator)
        self.assertIn("$runCount -gt 0", orchestrator)
        self.assertIn("granite_model_test_authorised = $false", orchestrator)
        self.assertIn("performance_claim_authorised = $false", orchestrator)
        self.assertIn("quality_claim_authorised = $false", orchestrator)

    def test_native_adapter_drains_stderr_without_using_powershell_error_stream(self) -> None:
        repository_root = Path(__file__).resolve().parents[3]
        orchestrator = (
            repository_root
            / "scripts"
            / "testing"
            / "workbook05"
            / "Invoke-Workbook05RouteBRepair.ps1"
        ).read_text(encoding="utf-8")

        self.assertIn("System.Diagnostics.ProcessStartInfo", orchestrator)
        self.assertIn("RedirectStandardOutput = $true", orchestrator)
        self.assertIn("RedirectStandardError = $true", orchestrator)
        self.assertGreaterEqual(orchestrator.count("ReadToEndAsync()"), 2)
        self.assertNotIn(
            "& $FilePath @ArgumentList 1> $stdoutPath 2> $stderrPath",
            orchestrator,
        )

    def test_external_workspace_is_short_and_git_long_path_aware(self) -> None:
        repository_root = Path(__file__).resolve().parents[3]
        orchestrator = (
            repository_root
            / "scripts"
            / "testing"
            / "workbook05"
            / "Invoke-Workbook05RouteBRepair.ps1"
        ).read_text(encoding="utf-8")

        self.assertIn("$ShortWorkspaceRoot = 'C:\\w5b'", orchestrator)
        self.assertIn("$SourceRoot = Join-Path $WorkRoot 'o'", orchestrator)
        self.assertIn("$BuildRoot = Join-Path $WorkRoot 'b'", orchestrator)
        self.assertIn("'core.longpaths=true'", orchestrator)
        self.assertNotIn(
            'Join-Path $env:RUNNER_TEMP "workbook-05-route-b-$runIdentity"',
            orchestrator,
        )

    def test_hosted_validator_requires_the_exact_six_case_catalogue(self) -> None:
        repository_root = Path(__file__).resolve().parents[3]
        validator = (
            repository_root
            / "scripts"
            / "testing"
            / "workbook05"
            / "route_b_repair_bundle_validation.py"
        ).read_text(encoding="utf-8")

        self.assertIn("len(results) != 6", validator)
        for case_id in (
            "baseline_f32",
            "qjl4",
            "qjl3",
            "polar4",
            "polar3",
            "asymmetric_f32_tbq4",
        ):
            self.assertIn(case_id, validator)


if __name__ == "__main__":
    unittest.main()
