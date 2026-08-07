from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path, PureWindowsPath

from scripts.testing.workbook05.configure_probe import (
    DEFAULT_BUILD_ROOT,
    DEFAULT_CMAKE_PATH,
    DEFAULT_SOURCE_ROOT,
    build_route_a_configure_command,
    ensure_fresh_build_directory,
    evaluate_route_a_configure_probe,
    parse_cmake_cache,
    write_configure_probe_report,
)
from scripts.testing.workbook05.schema_validation import validate_json_file


REPOSITORY_ROOT = Path(__file__).resolve().parents[3]
FIXTURE_ROOT = (
    REPOSITORY_ROOT
    / "tests/testing/workbook05/fixtures/source-admission"
)
VALID_CACHE = (FIXTURE_ROOT / "cmake-cache-route-a.txt").read_text(
    encoding="utf-8"
)
WRONG_SOURCE_CACHE = (FIXTURE_ROOT / "cmake-cache-wrong-source.txt").read_text(
    encoding="utf-8"
)
REPORT_SCHEMA = (
    REPOSITORY_ROOT
    / "experiments/granite_turboquant_intel/schemas/workbook05/configure-probe-report.schema.json"
)


class ConfigureProbeTests(unittest.TestCase):
    def test_command_is_exact_cpu_only_generation(self) -> None:
        self.assertEqual(
            (
                str(DEFAULT_CMAKE_PATH),
                "-S",
                str(DEFAULT_SOURCE_ROOT),
                "-B",
                str(DEFAULT_BUILD_ROOT),
                "-G",
                "Visual Studio 17 2022",
                "-A",
                "x64",
                "-DENABLE_INTEL_GPU=OFF",
            ),
            build_route_a_configure_command(),
        )

    def test_valid_zero_exit_cache_passes(self) -> None:
        decision = evaluate_route_a_configure_probe(
            build_route_a_configure_command(),
            0,
            VALID_CACHE,
        )

        self.assertTrue(decision.passed)
        self.assertEqual((), decision.reasons)

    def test_zero_exit_with_wrong_source_fails(self) -> None:
        decision = evaluate_route_a_configure_probe(
            build_route_a_configure_command(),
            0,
            WRONG_SOURCE_CACHE,
        )

        self.assertFalse(decision.passed)
        self.assertTrue(
            any("CMAKE_HOME_DIRECTORY" in reason for reason in decision.reasons)
        )

    def test_gpu_enabled_cache_fails(self) -> None:
        decision = evaluate_route_a_configure_probe(
            build_route_a_configure_command(),
            0,
            VALID_CACHE.replace("ENABLE_INTEL_GPU:BOOL=OFF", "ENABLE_INTEL_GPU:BOOL=ON"),
        )

        self.assertFalse(decision.passed)
        self.assertTrue(any("ENABLE_INTEL_GPU" in reason for reason in decision.reasons))

    def test_build_target_or_route_b_command_is_rejected(self) -> None:
        build_command = (*build_route_a_configure_command(), "--build")
        route_b_command = build_route_a_configure_command(
            source_root=PureWindowsPath(r"C:\wb05\source\route-b\openvino")
        )

        build_decision = evaluate_route_a_configure_probe(
            build_command,
            0,
            VALID_CACHE,
        )
        route_b_decision = evaluate_route_a_configure_probe(
            route_b_command,
            0,
            VALID_CACHE,
        )

        self.assertFalse(build_decision.passed)
        self.assertTrue(any("Forbidden" in reason for reason in build_decision.reasons))
        self.assertFalse(route_b_decision.passed)
        self.assertTrue(
            any("frozen Route A" in reason for reason in route_b_decision.reasons)
        )

    def test_nonzero_exit_fails_even_with_valid_cache(self) -> None:
        decision = evaluate_route_a_configure_probe(
            build_route_a_configure_command(),
            1,
            VALID_CACHE,
        )

        self.assertFalse(decision.passed)
        self.assertTrue(any("exited with code 1" in reason for reason in decision.reasons))

    def test_cache_parser_preserves_value_after_first_equals(self) -> None:
        values = parse_cmake_cache("CUSTOM:STRING=a=b=c\n")

        self.assertEqual("a=b=c", values["CUSTOM"])

    def test_build_directory_must_be_absent_or_empty(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            missing = root / "missing"
            ensure_fresh_build_directory(missing)
            self.assertTrue(missing.is_dir())

            empty = root / "empty"
            empty.mkdir()
            ensure_fresh_build_directory(empty)

            occupied = root / "occupied"
            occupied.mkdir()
            (occupied / "sentinel.txt").write_text("preserve\n", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "not empty"):
                ensure_fresh_build_directory(occupied)
            self.assertTrue((occupied / "sentinel.txt").exists())

    def test_fake_cmake_generates_a_cache_without_compilation(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source"
            build = root / "build"
            source.mkdir()
            fake = FIXTURE_ROOT / "fake-cmake.py"
            completed = subprocess.run(
                [
                    sys.executable,
                    str(fake),
                    "-S",
                    str(source),
                    "-B",
                    str(build),
                    "-G",
                    "Visual Studio 17 2022",
                    "-A",
                    "x64",
                    "-DENABLE_INTEL_GPU=OFF",
                ],
                capture_output=True,
                text=True,
                check=False,
            )

            cache_path = build / "CMakeCache.txt"
            values = parse_cmake_cache(cache_path.read_text(encoding="utf-8"))

        self.assertEqual(0, completed.returncode)
        self.assertEqual("Visual Studio 17 2022", values["CMAKE_GENERATOR"])
        self.assertEqual("x64", values["CMAKE_GENERATOR_PLATFORM"])
        self.assertEqual("OFF", values["ENABLE_INTEL_GPU"])
        self.assertNotIn("build", completed.stdout.casefold())

    def test_report_passes_schema_and_states_no_build_install_or_package(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            cache_path = root / "CMakeCache.txt"
            cache_path.write_text(VALID_CACHE, encoding="utf-8")
            report_path = root / "configure-probe.json"
            decision = evaluate_route_a_configure_probe(
                build_route_a_configure_command(),
                0,
                VALID_CACHE,
            )
            report = write_configure_probe_report(
                report_path,
                decision=decision,
                started_utc="2026-08-05T00:00:00Z",
                ended_utc="2026-08-05T00:00:01Z",
                exit_code=0,
                stdout_path="configure/route-a-stdout.txt",
                stderr_path="configure/route-a-stderr.txt",
                cmake_cache_path=cache_path,
            )
            issues = validate_json_file(report_path, REPORT_SCHEMA)

        self.assertEqual([], issues)
        self.assertFalse(report["build_invoked"])
        self.assertFalse(report["install_invoked"])
        self.assertFalse(report["package_invoked"])


if __name__ == "__main__":
    unittest.main()
