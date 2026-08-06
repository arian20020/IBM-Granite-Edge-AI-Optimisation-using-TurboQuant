from __future__ import annotations

import unittest

# The implementation deliberately does not exist in the first TDD commit.
# This import makes the initial workflow fail for the right reason: the
# behaviour has been specified before the repair code is written.
from scripts.testing.workbook05.route_b_repair import (
    RouteBContractError,
    repair_target_per_test_text,
    verify_benchmark_contract,
)


BROKEN_CMAKE = """\
if(X86_64)
    file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/instances/x64/${TEST_CLASS_FILE_NAME})
    file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/x64/${TEST_CLASS_FILE_NAME})
endif()
file(GLOB_RECURSE LIST_OF_TEST_COMMON_INSTANCES ${TEST_DIR}/instances/common/${TEST_CLASS_FILE_NAME})
file(GLOB_RECURSE LIST_OF_TEST_COMMON_INSTANCES ${TEST_DIR}/common/${TEST_CLASS_FILE_NAME})
"""

EXPECTED_REPAIRED_FRAGMENTS = (
    "LIST_OF_TEST_ARCH_INSTANCES_INSTANCES_X64",
    "LIST_OF_TEST_ARCH_INSTANCES_X64",
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

        # The unsafe repeated assignments must no longer remain.
        self.assertNotIn(
            "file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/instances/x64/",
            repaired,
        )
        self.assertNotIn(
            "file(GLOB_RECURSE LIST_OF_TEST_COMMON_INSTANCES ${TEST_DIR}/instances/common/",
            repaired,
        )

    def test_repair_is_idempotent(self) -> None:
        repaired_once = repair_target_per_test_text(BROKEN_CMAKE)
        repaired_twice = repair_target_per_test_text(repaired_once)

        self.assertEqual(repaired_once, repaired_twice)

    def test_repair_fails_closed_when_expected_source_shape_is_missing(self) -> None:
        with self.assertRaises(RouteBContractError):
            repair_target_per_test_text("set(UNRELATED true)\n")

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


if __name__ == "__main__":
    unittest.main()
