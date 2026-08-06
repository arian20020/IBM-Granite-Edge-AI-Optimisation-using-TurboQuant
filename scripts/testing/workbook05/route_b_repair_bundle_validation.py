"""Validate a Workbook 05 Route B repair artifact strictly as untrusted data."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path, PurePosixPath
from typing import Final


EXPECTED_SOURCE_COMMIT: Final = "1827f6458d049de11c1a8203c793af67c99935dc"
EXPECTED_REPAIR_PATH: Final = (
    "src/plugins/intel_cpu/tests/functional/cmake/target_per_test.cmake"
)
EXPECTED_TEST_FILTERS: Final = {
    "baseline_f32": "*Prc=f32*K=none_V=none*",
    "qjl4": "*Prc=f32*K=tbq4_qjl_V=tbq4_qjl*",
    "qjl3": "*Prc=f32*K=tbq3_qjl_V=tbq3_qjl*",
    "polar4": "*Prc=f32*K=polar4_V=polar4*",
    "polar3": "*Prc=f32*K=polar3_V=polar3*",
    "asymmetric_f32_tbq4": "*Prc=f32*K=none_V=tbq4*",
}
FORBIDDEN_SUFFIXES: Final = {
    ".7z",
    ".a",
    ".bin",
    ".dll",
    ".dylib",
    ".exe",
    ".gguf",
    ".gz",
    ".lib",
    ".obj",
    ".onnx",
    ".pdb",
    ".safetensors",
    ".sln",
    ".so",
    ".tar",
    ".tgz",
    ".vcxproj",
    ".whl",
    ".xml",
    ".zip",
}
SECRET_PATTERNS: Final = (
    re.compile(rb"ghp_[A-Za-z0-9]{20,}"),
    re.compile(rb"github_pat_[A-Za-z0-9_]{20,}"),
    re.compile(rb"hf_[A-Za-z0-9]{20,}"),
    re.compile(rb"Bearer\s+[A-Za-z0-9._~+/=-]{20,}", flags=re.IGNORECASE),
)
MAX_FILE_BYTES: Final = 25 * 1024 * 1024


class RouteBArtifactError(ValueError):
    """Raised when an untrusted Route B evidence bundle violates its contract."""


@dataclass(frozen=True)
class ValidationResult:
    """The independently recalculated artifact result."""

    valid: bool
    scientific_status: str
    file_count: int
    manifest_entry_count: int
    reasons: tuple[str, ...]


def _safe_relative_path(raw: str) -> str:
    """Reject absolute, parent-traversal, empty and backslash-based paths."""

    if not raw or "\\" in raw:
        raise RouteBArtifactError(f"Unsafe manifest path: {raw!r}")
    path = PurePosixPath(raw)
    if path.is_absolute() or ".." in path.parts or "." in path.parts:
        raise RouteBArtifactError(f"Unsafe manifest path: {raw!r}")
    return path.as_posix()


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _load_json(path: Path) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise RouteBArtifactError(f"Invalid JSON data in {path.name}: {error}") from error


def _parse_manifest(bundle_root: Path) -> dict[str, str]:
    manifest_path = bundle_root / "manifest.sha256"
    if not manifest_path.is_file() or manifest_path.is_symlink():
        raise RouteBArtifactError("manifest.sha256 is missing or unsafe.")

    entries: dict[str, str] = {}
    for line_number, raw_line in enumerate(
        manifest_path.read_text(encoding="utf-8-sig").splitlines(),
        start=1,
    ):
        if not raw_line.strip():
            continue
        match = re.fullmatch(r"([0-9a-f]{64})  (.+)", raw_line)
        if match is None:
            raise RouteBArtifactError(f"Malformed manifest line {line_number}.")
        relative = _safe_relative_path(match.group(2))
        if relative in entries:
            raise RouteBArtifactError(f"Duplicate manifest path: {relative}")
        entries[relative] = match.group(1)
    if not entries:
        raise RouteBArtifactError("The evidence manifest is empty.")
    return entries


def _validate_files(bundle_root: Path, entries: dict[str, str]) -> int:
    actual_files: set[str] = set()
    for path in bundle_root.rglob("*"):
        if path.is_symlink():
            raise RouteBArtifactError(f"Symbolic links are forbidden: {path}")
        if path.is_dir():
            continue
        relative = path.relative_to(bundle_root).as_posix()
        actual_files.add(relative)
        if relative == "manifest.sha256":
            continue
        if path.suffix.casefold() in FORBIDDEN_SUFFIXES:
            raise RouteBArtifactError(f"Forbidden artifact payload type: {relative}")
        if path.stat().st_size > MAX_FILE_BYTES:
            raise RouteBArtifactError(f"Evidence file exceeds size limit: {relative}")
        content = path.read_bytes()
        for pattern in SECRET_PATTERNS:
            if pattern.search(content):
                raise RouteBArtifactError(f"Potential secret found in: {relative}")

    expected_files = set(entries) | {"manifest.sha256"}
    if actual_files != expected_files:
        missing = sorted(expected_files - actual_files)
        extra = sorted(actual_files - expected_files)
        raise RouteBArtifactError(
            f"Manifest membership mismatch; missing={missing}, extra={extra}"
        )

    for relative, expected_hash in entries.items():
        path = bundle_root / relative
        if not path.is_file() or path.is_symlink():
            raise RouteBArtifactError(f"Manifest file is missing or unsafe: {relative}")
        actual_hash = _sha256(path)
        if actual_hash != expected_hash:
            raise RouteBArtifactError(f"SHA-256 mismatch for: {relative}")
    return len(actual_files)


def _expect_mapping(value: object, name: str) -> dict[str, object]:
    if not isinstance(value, dict):
        raise RouteBArtifactError(f"{name} must contain a JSON object.")
    return value


def _validate_disabled_claims(record: dict[str, object], record_name: str) -> None:
    """Require every later-campaign authorisation to remain explicitly false."""

    for claim_field in (
        "granite_model_test_authorised",
        "performance_claim_authorised",
        "quality_claim_authorised",
    ):
        if record.get(claim_field) is not False:
            raise RouteBArtifactError(
                f"{record_name} must keep {claim_field} false."
            )


def _validate_scientific_records(bundle_root: Path) -> str:
    integrity_path = bundle_root / "integrity-failure.json"
    decision_path = bundle_root / "decision.json"
    if integrity_path.is_file() and decision_path.is_file():
        raise RouteBArtifactError(
            "Artifact cannot contain both decision and integrity failure records."
        )
    if integrity_path.is_file():
        failure = _expect_mapping(_load_json(integrity_path), integrity_path.name)
        if failure.get("source_commit") != EXPECTED_SOURCE_COMMIT:
            raise RouteBArtifactError("Integrity-failure source commit is incorrect.")
        _validate_disabled_claims(failure, "Integrity failure")
        return "IntegrityFailure"
    if not decision_path.is_file():
        raise RouteBArtifactError(
            "Neither decision.json nor integrity-failure.json exists."
        )

    decision = _expect_mapping(_load_json(decision_path), decision_path.name)
    status = decision.get("status")
    if status not in {"Blocked", "ExecutableCandidate"}:
        raise RouteBArtifactError(f"Unsupported scientific status: {status!r}")
    if decision.get("source_commit") != EXPECTED_SOURCE_COMMIT:
        raise RouteBArtifactError("Decision source commit is incorrect.")
    _validate_disabled_claims(decision, "Decision")

    provenance = _expect_mapping(
        _load_json(bundle_root / "source-provenance.json"),
        "source-provenance.json",
    )
    if provenance.get("repository") != "https://github.com/EgorDuplensky/openvino.git":
        raise RouteBArtifactError("External source repository is incorrect.")
    if provenance.get("commit") != EXPECTED_SOURCE_COMMIT:
        raise RouteBArtifactError("External source commit is incorrect.")
    if provenance.get("clean_before_repair") is not True:
        raise RouteBArtifactError("External source was not clean before repair.")
    if provenance.get("recursive_submodules_complete") is not True:
        raise RouteBArtifactError("Recursive submodule evidence is incomplete.")

    changed = _expect_mapping(
        _load_json(bundle_root / "changed-files.json"),
        "changed-files.json",
    )
    if changed.get("actual") != [EXPECTED_REPAIR_PATH]:
        raise RouteBArtifactError("Repair changed files outside the approved boundary.")
    if changed.get("algorithm_files_changed") is not False:
        raise RouteBArtifactError("Artifact reports an algorithm-source change.")

    repair = _expect_mapping(
        _load_json(bundle_root / "repair" / "route-b-repair-report.json"),
        "route-b-repair-report.json",
    )
    if repair.get("source_commit") != EXPECTED_SOURCE_COMMIT:
        raise RouteBArtifactError("Repair report source commit is incorrect.")
    if repair.get("target_path") != EXPECTED_REPAIR_PATH:
        raise RouteBArtifactError("Repair report target path is incorrect.")
    if repair.get("changed") is not True:
        raise RouteBArtifactError("Repair report does not prove a source change.")
    if repair.get("before_sha256") == repair.get("after_sha256"):
        raise RouteBArtifactError("Repair before/after hashes are identical.")
    benchmark = _expect_mapping(repair.get("benchmark_contract"), "benchmark_contract")
    if benchmark.get("has_unconditional_f32") is not True:
        raise RouteBArtifactError("Pinned benchmark F32 safeguard was not verified.")
    if benchmark.get("has_k_f32_v_tbq") is not True:
        raise RouteBArtifactError(
            "Pinned asymmetric benchmark safeguard was not verified."
        )

    if status == "ExecutableCandidate":
        membership = _expect_mapping(
            _load_json(bundle_root / "target-membership.json"),
            "target-membership.json",
        )
        if membership.get("class_source_present") is not True:
            raise RouteBArtifactError("Generated target omits the test class source.")
        if membership.get("x64_instance_present") is not True:
            raise RouteBArtifactError("Generated target omits the X64 instance source.")

        discovery = _expect_mapping(
            _load_json(bundle_root / "test-discovery-summary.json"),
            "test-discovery-summary.json",
        )
        if (
            not isinstance(discovery.get("discovered_test_count"), int)
            or discovery["discovered_test_count"] <= 0
        ):
            raise RouteBArtifactError("Executable candidate has no discovered tests.")
        for field in (
            "has_f32_named_cases",
            "has_baseline_none_none_cases",
            "has_qjl3_cases",
            "has_qjl4_cases",
            "has_polar3_cases",
            "has_polar4_cases",
            "has_asymmetric_none_tbq_cases",
        ):
            if discovery.get(field) is not True:
                raise RouteBArtifactError(
                    f"Executable candidate is missing discovery proof: {field}"
                )

        results = _load_json(bundle_root / "test-results.json")
        if not isinstance(results, list) or len(results) != 6:
            raise RouteBArtifactError(
                "Executable candidate must contain exactly six test-filter results."
            )

        seen_ids: set[str] = set()
        for result in results:
            record = _expect_mapping(result, "test result")
            case_id = record.get("id")
            if not isinstance(case_id, str) or case_id not in EXPECTED_TEST_FILTERS:
                raise RouteBArtifactError(f"Unexpected test-filter ID: {case_id!r}")
            if case_id in seen_ids:
                raise RouteBArtifactError(f"Duplicate test-filter ID: {case_id}")
            seen_ids.add(case_id)

            if record.get("filter") != EXPECTED_TEST_FILTERS[case_id]:
                raise RouteBArtifactError(
                    f"Test filter changed for required case: {case_id}"
                )
            if record.get("passed") is not True:
                raise RouteBArtifactError(
                    f"Required test filter did not pass: {case_id}"
                )
            if record.get("exit_code") != 0:
                raise RouteBArtifactError(
                    f"Required test filter has a non-zero exit code: {case_id}"
                )
            if (
                not isinstance(record.get("run_count"), int)
                or record["run_count"] <= 0
            ):
                raise RouteBArtifactError(
                    f"Required test filter ran zero tests: {case_id}"
                )
            if record.get("skipped_count") != 0:
                raise RouteBArtifactError(
                    f"Required test filter contains skipped tests: {case_id}"
                )

        if seen_ids != set(EXPECTED_TEST_FILTERS):
            missing = sorted(set(EXPECTED_TEST_FILTERS) - seen_ids)
            raise RouteBArtifactError(
                f"Executable candidate is missing required test-filter IDs: {missing}"
            )
    return str(status)


def validate_bundle(bundle_root: Path) -> ValidationResult:
    bundle_root = bundle_root.resolve()
    reasons: list[str] = []
    try:
        if not bundle_root.is_dir() or bundle_root.is_symlink():
            raise RouteBArtifactError("Bundle root is missing or unsafe.")
        entries = _parse_manifest(bundle_root)
        file_count = _validate_files(bundle_root, entries)
        status = _validate_scientific_records(bundle_root)
    except RouteBArtifactError as error:
        reasons.append(str(error))
        return ValidationResult(
            valid=False,
            scientific_status="Invalid",
            file_count=0,
            manifest_entry_count=0,
            reasons=tuple(reasons),
        )

    return ValidationResult(
        valid=True,
        scientific_status=status,
        file_count=file_count,
        manifest_entry_count=len(entries),
        reasons=(),
    )


def _write_report(path: Path, result: ValidationResult) -> None:
    lines = [
        "# Workbook 05 Route B Repair Artifact Validation",
        "",
        f"- Valid: **{str(result.valid).lower()}**",
        f"- Scientific status: **{result.scientific_status}**",
        f"- Files including manifest: **{result.file_count}**",
        f"- Manifest entries: **{result.manifest_entry_count}**",
    ]
    if result.reasons:
        lines.extend(["", "## Reasons", ""])
        lines.extend(f"- {reason}" for reason in result.reasons)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def _parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = _parse_args()
    result = validate_bundle(args.bundle_root)
    _write_report(args.report, result)
    if not result.valid:
        for reason in result.reasons:
            print(f"ROUTE_B_ARTIFACT_INVALID: {reason}")
        return 1
    print(
        "ROUTE_B_ARTIFACT_VALID: "
        f"status={result.scientific_status} entries={result.manifest_entry_count}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
