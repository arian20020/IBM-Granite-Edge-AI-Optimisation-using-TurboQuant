"""Validate the immutable build/test manifest for the patched WB-04 runtime."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import subprocess
from collections import defaultdict
from collections.abc import Iterable
from pathlib import Path
from typing import Any


SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
MINIMUM_RAM_FLOOR = 2 * 1024**3
REQUIRED_OPTIONS = {
    "OPENVINO_GENAI_BUILD_TESTS": "ON",
    "OPENVINO_GENAI_BUILD_EXAMPLES": "OFF",
    "OPENVINO_GENAI_BUILD_BENCHMARKS": "OFF",
}


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _require_sha256(value: Any, field: str) -> str:
    if not isinstance(value, str) or SHA256_RE.fullmatch(value) is None:
        raise ValueError(f"{field} must be a lowercase SHA256")
    return value


def _require_mapping(value: Any, field: str) -> dict:
    if not isinstance(value, dict):
        raise ValueError(f"{field} must be an object")
    return value


def _require_nonblank(value: Any, field: str) -> str:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field} must be non-blank")
    return value


def _path(value: Any, field: str, manifest_path: Path) -> Path:
    raw = Path(_require_nonblank(value, field))
    resolved = raw if raw.is_absolute() else manifest_path.parent / raw
    return resolved.resolve()


def _same_path(left: Path, right: Path) -> bool:
    return os.path.normcase(str(left.resolve())) == os.path.normcase(str(right.resolve()))


def _git(repository: Path, *arguments: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(repository), *arguments],
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode:
        raise ValueError(
            f"source git {' '.join(arguments)} failed: "
            f"{(result.stderr or result.stdout).strip()}"
        )
    return result.stdout.strip()


def _validate_hashed_file(
    record: dict,
    *,
    path_field: str,
    hash_field: str,
    manifest_path: Path,
    kind: str,
) -> Path:
    path = _path(record.get(path_field), f"{kind} path", manifest_path)
    if not path.is_file():
        raise ValueError(f"{kind} file missing: {path}")
    expected = _require_sha256(record.get(hash_field), f"{kind} hash")
    actual = _sha256(path)
    if actual != expected:
        raise ValueError(f"{kind} hash mismatch: {path}")
    return path


def _configured_source(cache: Path) -> Path:
    prefix = "CMAKE_HOME_DIRECTORY:INTERNAL="
    for line in cache.read_text(encoding="utf-8", errors="replace").splitlines():
        if line.startswith(prefix):
            return Path(line[len(prefix):]).resolve()
    raise ValueError("CMake cache has no configured source directory")


def _cmake_cache_values(cache: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    for line in cache.read_text(encoding="utf-8", errors="replace").splitlines():
        if not line or line.startswith(("//", "#")) or "=" not in line:
            continue
        key_with_type, value = line.split("=", 1)
        key = key_with_type.split(":", 1)[0]
        values[key] = value
    return values


def validate_build_manifest(
    manifest_path: Path,
    *,
    expected_upstream_commit: str,
    expected_patch_commit: str,
    required_binaries: Iterable[str],
    required_test_suites: Iterable[str],
) -> dict[str, Any]:
    """Fail closed unless build provenance, artifacts, tests, and cleanup agree."""

    manifest_path = Path(manifest_path).resolve()
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"build manifest is unreadable: {manifest_path}") from exc
    if not isinstance(manifest, dict) or manifest.get("schema_version") != 1:
        raise ValueError("build manifest schema version must be 1")
    if manifest.get("status") != "passed":
        raise ValueError("build manifest status must be passed")

    source = _require_mapping(manifest.get("source"), "source")
    if source.get("upstream_commit") != expected_upstream_commit:
        raise ValueError("upstream commit mismatch")
    if source.get("patch_commit") != expected_patch_commit:
        raise ValueError("patch commit mismatch")
    _require_sha256(source.get("patch_series_sha256"), "patch series hash")
    if source.get("dirty") is not False:
        raise ValueError("recorded source is dirty")
    empty_status_hash = hashlib.sha256(b"").hexdigest()
    if source.get("status_porcelain_sha256") != empty_status_hash:
        raise ValueError("source status hash does not prove a clean checkout")
    source_path = _path(source.get("path"), "source path", manifest_path)
    if not (source_path / ".git").exists():
        raise ValueError(f"source Git checkout missing: {source_path}")
    actual_head = _git(source_path, "rev-parse", "HEAD")
    if actual_head != expected_patch_commit:
        raise ValueError("live source patch commit mismatch")
    try:
        _git(source_path, "cat-file", "-e", f"{expected_upstream_commit}^{{commit}}")
        _git(source_path, "merge-base", "--is-ancestor", expected_upstream_commit, expected_patch_commit)
    except ValueError as exc:
        raise ValueError("upstream commit is not an ancestor of the patch commit") from exc
    live_status = _git(source_path, "status", "--porcelain", "--untracked-files=all")
    if live_status:
        raise ValueError("live source is dirty")

    toolchain = _require_mapping(manifest.get("toolchain"), "toolchain")
    for field in (
        "cmake_version",
        "generator",
        "compiler_id",
        "compiler_version",
        "architecture",
        "configuration",
    ):
        _require_nonblank(toolchain.get(field), f"toolchain {field}")

    configure = _require_mapping(manifest.get("configure"), "configure")
    if configure.get("exit_code") != 0:
        raise ValueError("configure exit code is not zero")
    configured_source = _path(
        configure.get("source_directory"), "configured source directory", manifest_path
    )
    if not _same_path(configured_source, source_path):
        raise ValueError("configured source does not match the patched source")
    build_directory = _path(
        configure.get("build_directory"), "configured build directory", manifest_path
    )
    if not build_directory.is_dir():
        raise ValueError(f"configured build directory missing: {build_directory}")
    cache = _validate_hashed_file(
        configure,
        path_field="cache_path",
        hash_field="cache_sha256",
        manifest_path=manifest_path,
        kind="CMake cache",
    )
    if not _same_path(_configured_source(cache), source_path):
        raise ValueError("CMake cache configured source does not match the patched source")
    _validate_hashed_file(
        configure,
        path_field="log_path",
        hash_field="log_sha256",
        manifest_path=manifest_path,
        kind="configure log",
    )
    options = _require_mapping(configure.get("options"), "configure options")
    cache_values = _cmake_cache_values(cache)
    for name, expected in REQUIRED_OPTIONS.items():
        if options.get(name) != expected:
            raise ValueError(f"required configure option mismatch: {name}")
        if cache_values.get(name) != expected:
            raise ValueError(f"CMake cache configure option mismatch: {name}")

    build = _require_mapping(manifest.get("build"), "build")
    if build.get("exit_code") != 0:
        raise ValueError("build exit code is not zero")
    parallelism = build.get("parallelism")
    if isinstance(parallelism, bool) or not isinstance(parallelism, int) or parallelism < 1:
        raise ValueError("build parallelism must be a positive integer")
    floor = build.get("minimum_available_ram_bytes")
    if isinstance(floor, bool) or not isinstance(floor, int) or floor < MINIMUM_RAM_FLOOR:
        raise ValueError("build minimum available RAM is below the 2 GiB floor")
    _validate_hashed_file(
        build,
        path_field="log_path",
        hash_field="log_sha256",
        manifest_path=manifest_path,
        kind="build log",
    )

    binary_records = manifest.get("binaries")
    if not isinstance(binary_records, list) or not binary_records:
        raise ValueError("binary records are required")
    binaries: dict[str, dict] = {}
    for record in binary_records:
        record = _require_mapping(record, "binary record")
        name = _require_nonblank(record.get("name"), "binary name")
        if name in binaries:
            raise ValueError(f"duplicate binary record: {name}")
        path = _validate_hashed_file(
            record,
            path_field="path",
            hash_field="sha256",
            manifest_path=manifest_path,
            kind="binary",
        )
        size = record.get("size_bytes")
        if isinstance(size, bool) or not isinstance(size, int) or size <= 0:
            raise ValueError(f"binary size is invalid: {name}")
        if path.stat().st_size != size:
            raise ValueError(f"binary size mismatch: {name}")
        binaries[name] = record
    required_binary_set = set(required_binaries)
    missing_binaries = sorted(required_binary_set - set(binaries))
    if missing_binaries:
        raise ValueError(f"required binaries missing: {missing_binaries}")

    tests = manifest.get("tests")
    if not isinstance(tests, list) or not tests:
        raise ValueError("test run records are required")
    runs_by_suite: dict[str, list[int]] = defaultdict(list)
    computed_totals = {"selected": 0, "passed": 0, "failed": 0, "skipped": 0}
    for record in tests:
        record = _require_mapping(record, "test run record")
        suite = _require_nonblank(record.get("suite"), "test suite")
        run_index = record.get("run_index")
        if isinstance(run_index, bool) or not isinstance(run_index, int) or run_index < 1:
            raise ValueError(f"test run index is invalid: {suite}")
        if run_index in runs_by_suite[suite]:
            raise ValueError(f"duplicate test run index: {suite}/{run_index}")
        runs_by_suite[suite].append(run_index)
        _require_nonblank(record.get("command"), f"{suite} test command")
        for field in computed_totals:
            value = record.get(field)
            if isinstance(value, bool) or not isinstance(value, int) or value < 0:
                raise ValueError(f"{suite} {field} count is invalid")
            computed_totals[field] += value
        if record["selected"] < 1:
            raise ValueError(f"{suite} selected test count must be positive")
        if record["failed"] != 0:
            raise ValueError(f"{suite} has failed tests")
        if record["skipped"] != 0:
            raise ValueError(f"{suite} has skipped required tests")
        if record["passed"] != record["selected"]:
            raise ValueError(f"{suite} passed count does not equal selected count")
        if record.get("exit_code") != 0:
            raise ValueError(f"{suite} test exit code is not zero")
        _validate_hashed_file(
            record,
            path_field="log_path",
            hash_field="log_sha256",
            manifest_path=manifest_path,
            kind="test log",
        )

    required_suite_set = set(required_test_suites)
    missing_suites = sorted(required_suite_set - set(runs_by_suite))
    if missing_suites:
        raise ValueError(f"required test suites missing: {missing_suites}")
    for suite in sorted(required_suite_set):
        if sorted(runs_by_suite[suite]) != [1, 2]:
            raise ValueError(f"required test suite must pass exactly twice: {suite}")

    totals = _require_mapping(manifest.get("totals"), "totals")
    if totals.get("failed") != 0:
        raise ValueError("total failed test count must be zero")
    if totals.get("skipped") != 0:
        raise ValueError("total skipped required test count must be zero")
    if totals != computed_totals:
        raise ValueError(
            f"test totals mismatch: recorded {totals}, computed {computed_totals}"
        )

    cleanup = _require_mapping(manifest.get("cleanup"), "cleanup")
    if cleanup.get("queried") is not True:
        raise ValueError("cleanup query was not completed")
    if cleanup.get("residual_process_count") != 0:
        raise ValueError("residual owned processes remain after build/test")

    return {
        "status": "passed",
        "source_path": str(source_path),
        "build_directory": str(build_directory),
        "validated_binaries": len(binaries),
        "validated_test_runs": len(tests),
        "test_totals": computed_totals,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--expected-upstream-commit", required=True)
    parser.add_argument("--expected-patch-commit", required=True)
    parser.add_argument("--required-binary", action="append", required=True)
    parser.add_argument("--required-test-suite", action="append", required=True)
    args = parser.parse_args()
    result = validate_build_manifest(
        args.manifest,
        expected_upstream_commit=args.expected_upstream_commit,
        expected_patch_commit=args.expected_patch_commit,
        required_binaries=args.required_binary,
        required_test_suites=args.required_test_suite,
    )
    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
