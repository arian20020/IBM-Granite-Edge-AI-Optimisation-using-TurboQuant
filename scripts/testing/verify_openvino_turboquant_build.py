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
GIT_OID_RE = re.compile(r"^[0-9a-f]{40}$")
MINIMUM_RAM_FLOOR = 2 * 1024**3
REQUIRED_OPTIONS = {
    "ENABLE_TESTS": "ON",
    "ENABLE_SAMPLES": "OFF",
    "ENABLE_TOOLS": "OFF",
    "ENABLE_PYTHON": "OFF",
    "ENABLE_JS": "OFF",
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


def _require_git_oid(value: Any, field: str) -> str:
    if not isinstance(value, str) or GIT_OID_RE.fullmatch(value) is None:
        raise ValueError(f"{field} must be a lowercase 40-character Git object ID")
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


def _path_is_within(child: Path, parent: Path) -> bool:
    child_text = os.path.normcase(str(child.resolve()))
    parent_text = os.path.normcase(str(parent.resolve()))
    try:
        return os.path.commonpath([child_text, parent_text]) == parent_text
    except ValueError:
        return False


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


def _read_json_object_bytes(path: Path, kind: str) -> tuple[dict[str, Any], bytes]:
    try:
        payload = path.read_bytes()
        value = json.loads(payload.decode("utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ValueError(f"{kind} is unreadable: {path}") from exc
    if not isinstance(value, dict):
        raise ValueError(f"{kind} must contain a JSON object")
    return value, payload


def _raw_git_blob_id(payload: bytes) -> str:
    header = f"blob {len(payload)}\0".encode("ascii")
    return hashlib.sha1(header + payload).hexdigest()


def _controlled_patch_blob_id(path: Path, payload: bytes) -> str:
    """Match patch_identity's attribute-aware `git hash-object --path`."""

    repository = subprocess.run(
        ["git", "-C", str(path.parent), "rev-parse", "--show-toplevel"],
        check=False,
        capture_output=True,
        text=True,
    )
    if repository.returncode:
        return _raw_git_blob_id(payload)
    repository_root = Path(repository.stdout.strip()).resolve()
    try:
        relative_path = path.resolve().relative_to(repository_root).as_posix()
    except ValueError as exc:
        raise ValueError(
            f"identity patch is outside its controlling repository: {path}"
        ) from exc
    result = subprocess.run(
        [
            "git",
            "-C",
            str(repository_root),
            "hash-object",
            f"--path={relative_path}",
            "--stdin",
        ],
        input=payload,
        check=False,
        capture_output=True,
    )
    if result.returncode:
        message = (result.stderr or result.stdout).decode(
            "utf-8", errors="replace"
        )
        raise ValueError(
            f"cannot compute controlled patch Git blob: {message.strip()}"
        )
    blob_id = result.stdout.decode("ascii", errors="strict").strip()
    return _require_git_oid(blob_id, f"computed patch blob for {path.name}")


def ordered_patch_series_sha256(patches: list[dict[str, str]]) -> str:
    """Hash ordered patch identities without depending on JSON whitespace."""

    payload = json.dumps(
        patches,
        sort_keys=True,
        separators=(",", ":"),
        ensure_ascii=True,
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def validate_source_identity(
    identity_path: Path,
    source_path: Path,
    *,
    expected_upstream_commit: str | None = None,
    expected_patch_commit: str | None = None,
) -> dict[str, Any]:
    """Bind one live derived checkout to its controller-produced patch identity."""

    identity_path = Path(identity_path).resolve()
    source_path = Path(source_path).resolve()
    if not identity_path.is_file():
        raise ValueError(f"source identity file missing: {identity_path}")
    if not (source_path / ".git").exists():
        raise ValueError(f"source Git checkout missing: {source_path}")

    identity, identity_bytes = _read_json_object_bytes(
        identity_path, "source identity"
    )
    base_commit = _require_git_oid(identity.get("base_commit"), "identity base_commit")
    upstream_commit = _require_git_oid(
        identity.get("upstream_commit"), "identity upstream_commit"
    )
    patch_commit = _require_git_oid(
        identity.get("patch_commit"), "identity patch_commit"
    )
    upstream_tree = _require_git_oid(
        identity.get("upstream_tree"), "identity upstream_tree"
    )
    derived_tree = _require_git_oid(
        identity.get("derived_tree"), "identity derived_tree"
    )
    if base_commit != upstream_commit:
        raise ValueError("identity base and upstream commits differ")
    if expected_upstream_commit is not None:
        expected_upstream_commit = _require_git_oid(
            expected_upstream_commit, "expected upstream commit"
        )
        if upstream_commit != expected_upstream_commit:
            raise ValueError("upstream commit mismatch")
    if expected_patch_commit is not None:
        expected_patch_commit = _require_git_oid(
            expected_patch_commit, "expected patch commit"
        )
        if patch_commit != expected_patch_commit:
            raise ValueError("patch commit mismatch")
    if identity.get("dirty") is not False:
        raise ValueError("source identity records a dirty patch workspace")
    branch = _require_nonblank(identity.get("branch"), "identity branch")

    identity_destination = _path(
        identity.get("destination_path"),
        "identity destination path",
        identity_path,
    )
    if not _same_path(identity_destination, source_path):
        raise ValueError("source identity destination does not match source path")

    patch_directory = _path(
        identity.get("patch_directory"),
        "identity patch directory",
        identity_path,
    )
    if not patch_directory.is_dir():
        raise ValueError(f"identity patch directory missing: {patch_directory}")

    applied_patches = identity.get("applied_patches")
    if (
        not isinstance(applied_patches, list)
        or not applied_patches
        or any(not isinstance(name, str) or not name for name in applied_patches)
    ):
        raise ValueError("identity applied patch list is invalid")
    if applied_patches != sorted(applied_patches) or len(set(applied_patches)) != len(
        applied_patches
    ):
        raise ValueError("identity applied patch list is not unique lexical order")

    raw_patch_records = identity.get("patches")
    if not isinstance(raw_patch_records, list) or not raw_patch_records:
        raise ValueError("identity ordered patch records are required")
    patch_records: list[dict[str, str]] = []
    for index, raw_record in enumerate(raw_patch_records):
        record = _require_mapping(raw_record, f"identity patch record {index}")
        name = _require_nonblank(record.get("name"), f"identity patch {index} name")
        if (
            name != Path(name).name
            or "/" in name
            or "\\" in name
            or not name.endswith(".patch")
        ):
            raise ValueError(f"identity patch name is unsafe: {name}")
        patch_records.append(
            {
                "name": name,
                "blob_id": _require_git_oid(
                    record.get("blob_id"), f"identity patch {name} blob_id"
                ),
                "sha256": _require_sha256(
                    record.get("sha256"), f"identity patch {name} SHA-256"
                ),
            }
        )

    patch_names = [record["name"] for record in patch_records]
    if patch_names != applied_patches:
        raise ValueError(
            "identity ordered patch records do not match the applied patch list"
        )
    current_patch_names = sorted(
        path.name for path in patch_directory.iterdir() if path.suffix == ".patch"
    )
    if current_patch_names != applied_patches:
        raise ValueError("identity applied patch list is stale")
    for record in patch_records:
        patch_path = patch_directory / record["name"]
        if not patch_path.is_file():
            raise ValueError(f"identity patch file missing: {patch_path}")
        payload = patch_path.read_bytes()
        if hashlib.sha256(payload).hexdigest() != record["sha256"]:
            raise ValueError(f"patch SHA-256 mismatch: {patch_path}")
        if _controlled_patch_blob_id(patch_path, payload) != record["blob_id"]:
            raise ValueError(f"patch Git blob mismatch: {patch_path}")

    live_head = _git(source_path, "rev-parse", "HEAD")
    if live_head != patch_commit:
        raise ValueError("live source patch commit mismatch")
    live_branch = _git(source_path, "branch", "--show-current")
    if live_branch != branch:
        raise ValueError("live source branch does not match source identity")
    live_derived_tree = _git(source_path, "rev-parse", "HEAD^{tree}")
    if live_derived_tree != derived_tree:
        raise ValueError("live source derived tree mismatch")
    try:
        _git(source_path, "cat-file", "-e", f"{upstream_commit}^{{commit}}")
        _git(
            source_path,
            "merge-base",
            "--is-ancestor",
            upstream_commit,
            patch_commit,
        )
    except ValueError as exc:
        raise ValueError(
            "upstream commit is not an ancestor of the patch commit"
        ) from exc
    live_upstream_tree = _git(
        source_path, "rev-parse", f"{upstream_commit}^{{tree}}"
    )
    if live_upstream_tree != upstream_tree:
        raise ValueError("live source upstream tree mismatch")
    live_status = _git(
        source_path, "status", "--porcelain", "--untracked-files=all"
    )
    if live_status:
        raise ValueError("live source is dirty")

    return {
        "identity_path": str(identity_path),
        "identity_sha256": hashlib.sha256(identity_bytes).hexdigest(),
        "path": str(source_path),
        "patch_directory": str(patch_directory),
        "branch": branch,
        "base_commit": base_commit,
        "upstream_commit": upstream_commit,
        "patch_commit": patch_commit,
        "upstream_tree": upstream_tree,
        "derived_tree": derived_tree,
        "applied_patches": list(applied_patches),
        "patches": patch_records,
        "patch_series_sha256": ordered_patch_series_sha256(patch_records),
        "dirty": False,
        "status_porcelain_sha256": hashlib.sha256(b"").hexdigest(),
    }


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

    expected_upstream_commit = _require_git_oid(
        expected_upstream_commit, "expected upstream commit"
    )
    expected_patch_commit = _require_git_oid(
        expected_patch_commit, "expected patch commit"
    )
    source = _require_mapping(manifest.get("source"), "source")
    source_path = _path(source.get("path"), "source path", manifest_path)
    identity_path = _validate_hashed_file(
        source,
        path_field="identity_path",
        hash_field="identity_sha256",
        manifest_path=manifest_path,
        kind="source identity",
    )
    validated_identity = validate_source_identity(
        identity_path,
        source_path,
        expected_upstream_commit=expected_upstream_commit,
        expected_patch_commit=expected_patch_commit,
    )
    identity_fields = {
        "identity_sha256": "identity hash",
        "base_commit": "base commit",
        "upstream_commit": "upstream commit",
        "patch_commit": "patch commit",
        "upstream_tree": "upstream tree",
        "derived_tree": "derived tree",
        "branch": "patch branch",
        "patch_directory": "patch directory",
        "applied_patches": "applied patch list",
        "patches": "ordered patch records",
        "patch_series_sha256": "patch series hash",
        "dirty": "dirty source state",
        "status_porcelain_sha256": "source status hash",
    }
    for field, label in identity_fields.items():
        if source.get(field) != validated_identity[field]:
            raise ValueError(f"recorded {label} does not match source identity")

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
    if cache.name != "CMakeCache.txt" or not _path_is_within(
        cache, build_directory
    ):
        raise ValueError("CMake cache is outside the configured build directory")
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
        if not _path_is_within(path, build_directory):
            raise ValueError(
                f"binary is outside the configured build directory: {name}"
            )
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
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--manifest", type=Path)
    mode.add_argument("--identity", type=Path)
    parser.add_argument("--source", type=Path)
    parser.add_argument("--expected-upstream-commit")
    parser.add_argument("--expected-patch-commit")
    parser.add_argument("--required-binary", action="append")
    parser.add_argument("--required-test-suite", action="append")
    args = parser.parse_args()
    if args.identity is not None:
        if args.source is None:
            parser.error("--source is required with --identity")
        result = validate_source_identity(
            args.identity,
            args.source,
            expected_upstream_commit=args.expected_upstream_commit,
            expected_patch_commit=args.expected_patch_commit,
        )
    else:
        if args.source is not None:
            parser.error("--source is only valid with --identity")
        if args.expected_upstream_commit is None:
            parser.error("--expected-upstream-commit is required with --manifest")
        if args.expected_patch_commit is None:
            parser.error("--expected-patch-commit is required with --manifest")
        if not args.required_binary:
            parser.error("--required-binary is required with --manifest")
        if not args.required_test_suite:
            parser.error("--required-test-suite is required with --manifest")
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
