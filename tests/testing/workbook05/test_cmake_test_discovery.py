from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from scripts.testing.workbook05.cmake_test_discovery import audit_target_per_test


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
FIXTURE_ROOT = (
    REPOSITORY_ROOT
    / "tests/testing/workbook05/fixtures/source-admission"
)
CMAKE_TEXT = (FIXTURE_ROOT / "route-b-target-per-test.cmake").read_text(
    encoding="utf-8"
)
GENERATED_OMITTED = (FIXTURE_ROOT / "generated-target-omitted.txt").read_text(
    encoding="utf-8"
)
GENERATED_COMPLETE = (FIXTURE_ROOT / "generated-target-complete.txt").read_text(
    encoding="utf-8"
)
EXPECTED = {
    "instances/x64/concat_sdp_turboq.cpp",
    "x64/concat_sdp_turboq.cpp",
    "instances/common/concat_sdp_turboq.cpp",
    "common/concat_sdp_turboq.cpp",
}


def _create_test_tree(root: Path) -> None:
    for relative in EXPECTED:
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("// controlled test instance\n", encoding="utf-8")


class CMakeTestDiscoveryTests(unittest.TestCase):
    def test_repeated_assignments_omit_first_glob_results(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            test_root = Path(directory) / "tests"
            _create_test_tree(test_root)

            decision = audit_target_per_test(
                CMAKE_TEXT,
                test_root,
                "concat_sdp_turboq.cpp",
                GENERATED_OMITTED,
            )

        self.assertTrue(decision.blocker_confirmed)
        self.assertEqual(EXPECTED, set(decision.intended_sources))
        self.assertEqual(
            {
                "instances/x64/concat_sdp_turboq.cpp",
                "instances/common/concat_sdp_turboq.cpp",
            },
            set(decision.omitted_sources),
        )
        self.assertEqual(
            {
                "x64/concat_sdp_turboq.cpp",
                "common/concat_sdp_turboq.cpp",
            },
            set(decision.final_sources),
        )

    def test_complete_generated_target_disproves_actual_omission(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            test_root = Path(directory) / "tests"
            _create_test_tree(test_root)

            decision = audit_target_per_test(
                CMAKE_TEXT,
                test_root,
                "concat_sdp_turboq.cpp",
                GENERATED_COMPLETE,
            )

        self.assertFalse(decision.blocker_confirmed)
        self.assertEqual((), decision.omitted_sources)
        self.assertEqual(EXPECTED, set(decision.final_sources))
        self.assertTrue(
            any("despite the repeated assignments" in reason for reason in decision.reasons)
        )

    def test_without_generated_metadata_the_source_risk_is_not_claimed_as_confirmed(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            test_root = Path(directory) / "tests"
            _create_test_tree(test_root)

            decision = audit_target_per_test(
                CMAKE_TEXT,
                test_root,
                "concat_sdp_turboq.cpp",
                "",
            )

        self.assertFalse(decision.blocker_confirmed)
        self.assertEqual(
            {
                "instances/x64/concat_sdp_turboq.cpp",
                "instances/common/concat_sdp_turboq.cpp",
            },
            set(decision.omitted_sources),
        )

    def test_missing_intended_file_is_not_invented(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            test_root = Path(directory) / "tests"
            _create_test_tree(test_root)
            (test_root / "instances/x64/concat_sdp_turboq.cpp").unlink()

            decision = audit_target_per_test(
                CMAKE_TEXT,
                test_root,
                "concat_sdp_turboq.cpp",
                GENERATED_OMITTED,
            )

        self.assertNotIn(
            "instances/x64/concat_sdp_turboq.cpp",
            decision.intended_sources,
        )

    def test_expression_cannot_escape_test_root(self) -> None:
        unsafe = "file(GLOB_RECURSE LIST_OF_TEST_ARCH_INSTANCES ${TEST_DIR}/../${TEST_CLASS_FILE_NAME})"
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            outside = root / "concat_sdp_turboq.cpp"
            outside.write_text("// outside\n", encoding="utf-8")
            test_root = root / "tests"
            test_root.mkdir()

            with self.assertRaisesRegex(ValueError, "escapes"):
                audit_target_per_test(
                    unsafe,
                    test_root,
                    "concat_sdp_turboq.cpp",
                    "",
                )


if __name__ == "__main__":
    unittest.main()
