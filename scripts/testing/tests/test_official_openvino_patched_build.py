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


def patch_series_sha256(patches: list[dict[str, str]]) -> str:
    payload = json.dumps(
        patches,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


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
        self.upstream_tree = subprocess.check_output(
            ["git", "rev-parse", f"{self.base_commit}^{{tree}}"],
            cwd=self.source,
            text=True,
        ).strip()
        self.derived_tree = subprocess.check_output(
            ["git", "rev-parse", f"{self.actual_patch}^{{tree}}"],
            cwd=self.source,
            text=True,
        ).strip()
        self.control = self.root / "control"
        self.control.mkdir()
        subprocess.run(["git", "init"], cwd=self.control, check=True, capture_output=True)
        (self.control / ".gitattributes").write_text(
            "* text=auto\n",
            encoding="utf-8",
        )
        self.patch_directory = (
            self.control / "experiments" / "patches" / "openvino-turboquant"
        )
        self.patch_directory.mkdir(parents=True)
        for index, content in enumerate(
            (b"first patch\r\n", b"second patch\r\n"),
            start=1,
        ):
            (self.patch_directory / f"000{index}-fixture.patch").write_bytes(
                content
            )
        subprocess.run(["git", "add", "."], cwd=self.control, check=True)
        subprocess.run(
            [
                "git",
                "-c",
                "user.name=Test",
                "-c",
                "user.email=test@example.invalid",
                "commit",
                "-m",
                "controlled patches",
            ],
            cwd=self.control,
            check=True,
            capture_output=True,
        )
        self.patch_records = [
            {
                "name": patch.name,
                "blob_id": subprocess.check_output(
                    [
                        "git",
                        "rev-parse",
                        "HEAD:"
                        + patch.relative_to(self.control).as_posix(),
                    ],
                    cwd=self.control,
                    text=True,
                ).strip(),
                "sha256": sha256(patch),
            }
            for patch in sorted(self.patch_directory.glob("*.patch"))
        ]
        self.applied_patches = [record["name"] for record in self.patch_records]
        self.identity = {
            "upstream_path": str(self.root / "upstream"),
            "destination_path": str(self.source),
            "destination_operation_path": str(self.source),
            "patch_directory": str(self.patch_directory),
            "branch": subprocess.check_output(
                ["git", "branch", "--show-current"],
                cwd=self.source,
                text=True,
            ).strip(),
            "base_commit": self.base_commit,
            "upstream_commit": self.base_commit,
            "patch_commit": self.actual_patch,
            "upstream_tree": self.upstream_tree,
            "derived_tree": self.derived_tree,
            "applied_patches": self.applied_patches,
            "patches": self.patch_records,
            "dirty": False,
        }
        self.identity_path = self.root / "source.identity.json"
        self.write_identity()
        self.build = self.root / "build"
        self.build.mkdir()
        self.cache = self.build / "CMakeCache.txt"
        self.cache.write_text(
            f"CMAKE_HOME_DIRECTORY:INTERNAL={self.source.as_posix()}\n"
            "ENABLE_TESTS:BOOL=ON\n"
            "ENABLE_SAMPLES:BOOL=OFF\n"
            "ENABLE_TOOLS:BOOL=OFF\n"
            "ENABLE_PYTHON:BOOL=OFF\n"
            "ENABLE_JS:BOOL=OFF\n",
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
                "identity_path": str(self.identity_path),
                "identity_sha256": sha256(self.identity_path),
                "patch_directory": str(self.patch_directory),
                "branch": self.identity["branch"],
                "base_commit": self.base_commit,
                "upstream_commit": self.base_commit,
                "patch_commit": self.actual_patch,
                "upstream_tree": self.upstream_tree,
                "derived_tree": self.derived_tree,
                "applied_patches": self.applied_patches,
                "patches": self.patch_records,
                "patch_series_sha256": patch_series_sha256(self.patch_records),
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
                    "ENABLE_TESTS": "ON",
                    "ENABLE_SAMPLES": "OFF",
                    "ENABLE_TOOLS": "OFF",
                    "ENABLE_PYTHON": "OFF",
                    "ENABLE_JS": "OFF",
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

    def write_identity(self):
        self.identity_path.write_text(
            json.dumps(self.identity, indent=2),
            encoding="utf-8",
        )

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

    def test_rejects_stale_identity_patch_list_or_patch_bytes(self):
        stale_identity_hash = json.loads(json.dumps(self.manifest))
        stale_identity_hash["source"]["identity_sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "identity hash"):
            self.validate(stale_identity_hash)

        stale_patch_list = json.loads(json.dumps(self.manifest))
        stale_patch_list["source"]["applied_patches"].reverse()
        with self.assertRaisesRegex(ValueError, "applied patch"):
            self.validate(stale_patch_list)

        (self.patch_directory / self.applied_patches[0]).write_text(
            "tampered patch\n",
            encoding="utf-8",
        )
        with self.assertRaisesRegex(ValueError, "patch SHA-256"):
            self.validate()

    def test_rejects_stale_identity_even_when_its_manifest_hash_is_updated(self):
        self.identity["applied_patches"].reverse()
        self.write_identity()
        manifest = json.loads(json.dumps(self.manifest))
        manifest["source"]["identity_sha256"] = sha256(self.identity_path)
        manifest["source"]["applied_patches"] = list(
            self.identity["applied_patches"]
        )

        with self.assertRaisesRegex(ValueError, "identity applied patch list"):
            self.validate(manifest)

    def test_rejects_wrong_recorded_or_live_source_trees(self):
        wrong_recorded_tree = json.loads(json.dumps(self.manifest))
        wrong_recorded_tree["source"]["derived_tree"] = "f" * 40
        with self.assertRaisesRegex(ValueError, "derived tree"):
            self.validate(wrong_recorded_tree)

        self.identity["derived_tree"] = "e" * 40
        self.write_identity()
        wrong_live_tree = json.loads(json.dumps(self.manifest))
        wrong_live_tree["source"]["identity_sha256"] = sha256(self.identity_path)
        wrong_live_tree["source"]["derived_tree"] = "e" * 40
        with self.assertRaisesRegex(ValueError, "live source derived tree"):
            self.validate(wrong_live_tree)

    def test_rejects_identity_branch_that_is_not_live(self):
        self.identity["branch"] = "project/not-the-live-branch"
        self.write_identity()
        manifest = json.loads(json.dumps(self.manifest))
        manifest["source"]["identity_sha256"] = sha256(self.identity_path)
        manifest["source"]["branch"] = self.identity["branch"]

        with self.assertRaisesRegex(ValueError, "live source branch"):
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
                    ENABLE_SAMPLES="ON"
                ),
                "configure option",
            ),
        ):
            manifest = json.loads(json.dumps(self.manifest))
            mutate(manifest)
            with self.subTest(message=message), self.assertRaisesRegex(ValueError, message):
                self.validate(manifest)

    def test_rejects_cache_or_binary_outside_the_configured_build(self):
        foreign_cache = self.root / "foreign-CMakeCache.txt"
        foreign_cache.write_bytes(self.cache.read_bytes())
        foreign_cache_manifest = json.loads(json.dumps(self.manifest))
        foreign_cache_manifest["configure"]["cache_path"] = str(foreign_cache)
        foreign_cache_manifest["configure"]["cache_sha256"] = sha256(foreign_cache)
        with self.assertRaisesRegex(ValueError, "cache.*build directory"):
            self.validate(foreign_cache_manifest)

        foreign_binary = self.root / "foreign-output.dll"
        foreign_binary.write_bytes(b"foreign output")
        foreign_binary_manifest = json.loads(json.dumps(self.manifest))
        foreign_binary_manifest["binaries"][0].update(
            path=str(foreign_binary),
            size_bytes=foreign_binary.stat().st_size,
            sha256=sha256(foreign_binary),
        )
        with self.assertRaisesRegex(ValueError, "binary.*build directory"):
            self.validate(foreign_binary_manifest)

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
