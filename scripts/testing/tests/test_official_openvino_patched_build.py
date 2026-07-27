import hashlib
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

from scripts.testing.verify_openvino_turboquant_build import validate_build_manifest


PATCH = "2" * 40
REQUIRED_SUITES = {
    "TurboQuantCodec",
    "TurboQuantStateUpdate",
    "TurboQuantConfig",
    "TurboQuantPipelineActivation",
    "AffectedUpstream",
}
REQUIRED_BINARIES = {"openvino_genai.dll", "genai_unit_tests.exe"}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class PatchedBuildManifestTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        self.source.mkdir()
        subprocess.run(["git", "init"], cwd=self.source, check=True, capture_output=True)
        (self.source / "tracked.txt").write_text("source\n", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=self.source, check=True)
        subprocess.run(
            [
                "git",
                "-c",
                "user.name=Test",
                "-c",
                "user.email=test@example.invalid",
                "commit",
                "-m",
                "source",
            ],
            cwd=self.source,
            check=True,
            capture_output=True,
        )
        self.base_commit = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=self.source, text=True
        ).strip()
        (self.source / "tracked.txt").write_text("patched source\n", encoding="utf-8")
        subprocess.run(["git", "add", "."], cwd=self.source, check=True)
        subprocess.run(
            [
                "git",
                "-c",
                "user.name=Test",
                "-c",
                "user.email=test@example.invalid",
                "commit",
                "-m",
                "patch",
            ],
            cwd=self.source,
            check=True,
            capture_output=True,
        )
        self.actual_patch = subprocess.check_output(
            ["git", "rev-parse", "HEAD"], cwd=self.source, text=True
        ).strip()
        self.build = self.root / "build"
        self.build.mkdir()
        self.cache = self.build / "CMakeCache.txt"
        self.cache.write_text(
            f"CMAKE_HOME_DIRECTORY:INTERNAL={self.source.as_posix()}\n"
            "OPENVINO_GENAI_BUILD_TESTS:BOOL=ON\n"
            "OPENVINO_GENAI_BUILD_EXAMPLES:BOOL=OFF\n"
            "OPENVINO_GENAI_BUILD_BENCHMARKS:BOOL=OFF\n",
            encoding="utf-8",
        )
        self.configure_log = self.build / "configure.log"
        self.configure_log.write_text("configure passed\n", encoding="utf-8")
        self.build_log = self.build / "build.log"
        self.build_log.write_text("build passed\n", encoding="utf-8")
        self.binaries = []
        for name in sorted(REQUIRED_BINARIES):
            path = self.build / name
            path.write_bytes(f"binary:{name}".encode("utf-8"))
            self.binaries.append(
                {
                    "name": name,
                    "path": str(path),
                    "size_bytes": path.stat().st_size,
                    "sha256": sha256(path),
                }
            )
        self.tests = []
        for suite in sorted(REQUIRED_SUITES):
            for run_index in (1, 2):
                log = self.build / f"{suite}-{run_index}.log"
                log.write_text(f"{suite}: PASS\n", encoding="utf-8")
                self.tests.append(
                    {
                        "suite": suite,
                        "run_index": run_index,
                        "command": f"genai_unit_tests.exe --gtest_filter={suite}.*",
                        "selected": 3,
                        "passed": 3,
                        "failed": 0,
                        "skipped": 0,
                        "exit_code": 0,
                        "log_path": str(log),
                        "log_sha256": sha256(log),
                    }
                )
        self.manifest = {
            "schema_version": 1,
            "status": "passed",
            "source": {
                "path": str(self.source),
                "upstream_commit": self.base_commit,
                "patch_commit": self.actual_patch,
                "patch_series_sha256": "3" * 64,
                "dirty": False,
                "status_porcelain_sha256": hashlib.sha256(b"").hexdigest(),
            },
            "toolchain": {
                "cmake_version": "4.1.0",
                "generator": "Visual Studio 17 2022",
                "compiler_id": "MSVC",
                "compiler_version": "19.44",
                "architecture": "x64",
                "configuration": "Release",
            },
            "configure": {
                "exit_code": 0,
                "source_directory": str(self.source),
                "build_directory": str(self.build),
                "cache_path": str(self.cache),
                "cache_sha256": sha256(self.cache),
                "log_path": str(self.configure_log),
                "log_sha256": sha256(self.configure_log),
                "options": {
                    "OPENVINO_GENAI_BUILD_TESTS": "ON",
                    "OPENVINO_GENAI_BUILD_EXAMPLES": "OFF",
                    "OPENVINO_GENAI_BUILD_BENCHMARKS": "OFF",
                },
            },
            "build": {
                "exit_code": 0,
                "parallelism": 1,
                "minimum_available_ram_bytes": 2 * 1024**3,
                "log_path": str(self.build_log),
                "log_sha256": sha256(self.build_log),
            },
            "binaries": self.binaries,
            "tests": self.tests,
            "totals": {
                "selected": 30,
                "passed": 30,
                "failed": 0,
                "skipped": 0,
            },
            "cleanup": {
                "residual_process_count": 0,
                "queried": True,
            },
        }
        self.manifest_path = self.root / "manifest.json"

    def tearDown(self):
        self.temporary.cleanup()

    def validate(self, manifest=None):
        manifest = self.manifest if manifest is None else manifest
        self.manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        return validate_build_manifest(
            self.manifest_path,
            expected_upstream_commit=self.base_commit,
            expected_patch_commit=self.actual_patch,
            required_binaries=REQUIRED_BINARIES,
            required_test_suites=REQUIRED_SUITES,
        )

    def test_accepts_clean_hash_bound_build_with_each_suite_passing_twice(self):
        result = self.validate()
        self.assertEqual(result["status"], "passed")
        self.assertEqual(result["validated_test_runs"], 10)

    def test_rejects_wrong_commit_dirty_source_or_foreign_cmake_source(self):
        mutations = []
        wrong_upstream = json.loads(json.dumps(self.manifest))
        wrong_upstream["source"]["upstream_commit"] = PATCH
        mutations.append((wrong_upstream, "upstream commit"))
        wrong_commit = json.loads(json.dumps(self.manifest))
        wrong_commit["source"]["patch_commit"] = PATCH
        mutations.append((wrong_commit, "patch commit"))
        dirty = json.loads(json.dumps(self.manifest))
        dirty["source"]["dirty"] = True
        mutations.append((dirty, "dirty"))
        foreign = json.loads(json.dumps(self.manifest))
        foreign["configure"]["source_directory"] = str(self.root / "foreign")
        mutations.append((foreign, "configured source"))
        for manifest, message in mutations:
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(manifest)

    def test_rejects_missing_second_run_failure_skip_or_zero_selected_tests(self):
        for mutation, message in (
            (lambda tests: tests.pop(), "twice"),
            (lambda tests: tests[0].update(failed=1, passed=2), "failed"),
            (lambda tests: tests[0].update(skipped=1, passed=2), "skipped"),
            (lambda tests: tests[0].update(selected=0, passed=0), "selected"),
        ):
            manifest = json.loads(json.dumps(self.manifest))
            mutation(manifest["tests"])
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(manifest)

    def test_rejects_missing_tampered_or_unhashed_binary_and_test_log(self):
        for mutate, message in (
            (lambda manifest: manifest["binaries"].pop(), "required binaries"),
            (lambda manifest: manifest["binaries"][0].update(sha256="0" * 64), "binary hash"),
            (lambda manifest: manifest["tests"][0].update(log_sha256="0" * 64), "test log hash"),
        ):
            manifest = json.loads(json.dumps(self.manifest))
            mutate(manifest)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(manifest)

    def test_rejects_unbound_configure_build_logs_or_cache_options(self):
        for mutate, message in (
            (
                lambda manifest: manifest["configure"].update(log_sha256="0" * 64),
                "configure log hash",
            ),
            (
                lambda manifest: manifest["build"].update(log_sha256="0" * 64),
                "build log hash",
            ),
            (
                lambda manifest: manifest["configure"]["options"].update(
                    OPENVINO_GENAI_BUILD_EXAMPLES="ON"
                ),
                "configure option",
            ),
        ):
            manifest = json.loads(json.dumps(self.manifest))
            mutate(manifest)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(manifest)

    def test_rejects_aggregate_failure_or_residual_process(self):
        for path, value, message in (
            (("totals", "failed"), 1, "total failed"),
            (("build", "parallelism"), 0, "parallelism"),
            (("cleanup", "residual_process_count"), 1, "residual"),
            (("cleanup", "queried"), False, "cleanup query"),
        ):
            manifest = json.loads(json.dumps(self.manifest))
            manifest[path[0]][path[1]] = value
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(manifest)


if __name__ == "__main__":
    unittest.main()
