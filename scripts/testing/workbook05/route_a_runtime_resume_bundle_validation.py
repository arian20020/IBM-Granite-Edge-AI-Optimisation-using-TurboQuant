from __future__ import annotations

import argparse
import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path, PurePosixPath


CAMPAIGN_ID = "GTQ-WB05-MF-v1"
ROUTE_ID = "route-a-merged-openvino"
COMPONENT = "runtime"
SOURCE_REPOSITORY = "https://github.com/openvinotoolkit/openvino.git"
SOURCE_COMMIT = "b9a1f201c109e0bed74763934f79483cf6c4cbf4"
BUILD_DOCUMENT = "docs/dev/build_windows.md"
BUILD_DOCUMENT_SHA256 = (
    "1eb445141e72a0f0d5a1186de28bfe2144db500357b860b6a239843c85e5aa65"
)
PYTHON_PATH = r"C:\Program Files\Python312\python.exe"

REQUIRED_PATHS = {
    "bundle.json",
    "environment.json",
    "source-provenance.json",
    "cmake-cache-summary.json",
    "dependencies.json",
    "commands/route-a-runtime-configure.command.json",
    "commands/route-a-runtime-configure.stdout.log",
    "commands/route-a-runtime-configure.stderr.log",
    "commands/route-a-runtime-configure.resources.json",
    "commands/route-a-runtime-build.resources.csv",
    "manifest.sha256",
}
FORBIDDEN_COMPLETION_PATHS = {
    "decision.json",
    "integrity-failure.json",
    "commands/route-a-runtime-build.command.json",
}
FORBIDDEN_SUFFIXES = {
    ".exe",
    ".dll",
    ".lib",
    ".pdb",
    ".pyd",
    ".zip",
    ".7z",
    ".tar",
    ".gz",
    ".gguf",
    ".safetensors",
    ".bin",
}
MAX_FILE_BYTES = 16 * 1024 * 1024
SECRET_PATTERNS = (
    re.compile(r"gh[pousr]_[A-Za-z0-9_]{20,}"),
    re.compile(r"github_pat_[A-Za-z0-9_]{20,}"),
    re.compile(r"AKIA[0-9A-Z]{16}"),
    re.compile(r"(?i)(password|api[_-]?key|secret|token)\s*[:=]\s*[^\s]+"),
)
EXPECTED_CACHE_VALUES = {
    "CMAKE_GENERATOR": "Visual Studio 17 2022",
    "CMAKE_GENERATOR_PLATFORM": "x64",
    "ENABLE_INTEL_CPU": "ON",
    "ENABLE_INTEL_GPU": "OFF",
    "ENABLE_INTEL_NPU": "OFF",
    "ENABLE_TESTS": "OFF",
    "ENABLE_FUNCTIONAL_TESTS": "OFF",
    "ENABLE_SAMPLES": "OFF",
    "ENABLE_PYTHON": "ON",
    "ENABLE_WHEEL": "OFF",
    "ENABLE_JS": "OFF",
    "ENABLE_OV_IR_FRONTEND": "ON",
    "ENABLE_OV_ONNX_FRONTEND": "ON",
    "ENABLE_OV_PADDLE_FRONTEND": "OFF",
    "ENABLE_OV_TF_FRONTEND": "ON",
    "ENABLE_OV_TF_LITE_FRONTEND": "OFF",
    "ENABLE_OV_PYTORCH_FRONTEND": "OFF",
    "ENABLE_OV_JAX_FRONTEND": "OFF",
    "ENABLE_SYSTEM_PROTOBUF": "OFF",
    "Python3_EXECUTABLE": PYTHON_PATH,
}


@dataclass(frozen=True)
class ValidationResult:
    valid: bool
    file_count: int
    manifest_entry_count: int
    reasons: tuple[str, ...]


def _issue(code: str, path: str, message: str) -> str:
    return f"{code} {path}: {message}"


def _is_safe_relative_path(raw: str) -> bool:
    if not raw or "\\" in raw or raw.startswith("/"):
        return False
    path = PurePosixPath(raw)
    return (
        not path.is_absolute()
        and ".." not in path.parts
        and "." not in path.parts
        and all(part not in {"", ".", ".."} for part in path.parts)
    )


def _files(root: Path) -> list[Path]:
    return sorted(candidate for candidate in root.rglob("*") if candidate.is_file())


def _load_json(path: Path, issues: list[str]) -> object | None:
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        issues.append(_issue("JSON_INVALID", path.name, str(error)))
        return None


def _validate_payloads(root: Path, issues: list[str]) -> int:
    files = _files(root)
    for candidate in files:
        relative = candidate.relative_to(root).as_posix()
        if not _is_safe_relative_path(relative):
            issues.append(_issue("PATH_UNSAFE", relative, "Unsafe bundle path."))
            continue
        if candidate.suffix.lower() in FORBIDDEN_SUFFIXES:
            issues.append(
                _issue(
                    "PAYLOAD_FORBIDDEN",
                    relative,
                    "Executable, archive, model, or binary payload is forbidden.",
                )
            )
        try:
            size = candidate.stat().st_size
        except OSError as error:
            issues.append(_issue("PAYLOAD_UNREADABLE", relative, str(error)))
            continue
        if size > MAX_FILE_BYTES:
            issues.append(
                _issue(
                    "PAYLOAD_TOO_LARGE",
                    relative,
                    f"Text evidence exceeds {MAX_FILE_BYTES} bytes.",
                )
            )
            continue
        try:
            text = candidate.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeError) as error:
            issues.append(_issue("PAYLOAD_NOT_TEXT", relative, str(error)))
            continue
        for pattern in SECRET_PATTERNS:
            if pattern.search(text):
                issues.append(
                    _issue(
                        "SECRET_PATTERN",
                        relative,
                        "Potential secret pattern found in evidence.",
                    )
                )
                break
    return len(files)


def _read_manifest(root: Path, issues: list[str]) -> dict[str, str]:
    manifest = root / "manifest.sha256"
    entries: dict[str, str] = {}
    if not manifest.is_file():
        return entries
    try:
        lines = manifest.read_text(encoding="utf-8-sig").splitlines()
    except (OSError, UnicodeError) as error:
        issues.append(_issue("MANIFEST_INVALID", "manifest.sha256", str(error)))
        return entries

    for number, line in enumerate(lines, start=1):
        match = re.fullmatch(r"([0-9a-f]{64})  (.+)", line)
        if match is None:
            issues.append(
                _issue(
                    "MANIFEST_INVALID",
                    "manifest.sha256",
                    f"Line {number} is not '<sha256><two spaces><path>'.",
                )
            )
            continue
        digest, relative = match.groups()
        if not _is_safe_relative_path(relative):
            issues.append(_issue("MANIFEST_PATH_UNSAFE", "manifest.sha256", relative))
            continue
        if relative in entries:
            issues.append(
                _issue(
                    "MANIFEST_DUPLICATE",
                    "manifest.sha256",
                    f"Duplicate path {relative!r}.",
                )
            )
            continue
        entries[relative] = digest

    actual_paths = {
        candidate.relative_to(root).as_posix()
        for candidate in _files(root)
        if candidate.name != "manifest.sha256"
    }
    manifest_paths = set(entries)
    for missing in sorted(actual_paths - manifest_paths):
        issues.append(_issue("MANIFEST_ENTRY_MISSING", missing, "File is not covered."))
    for missing in sorted(manifest_paths - actual_paths):
        issues.append(_issue("MANIFEST_FILE_MISSING", missing, "Manifest target is absent."))
    for relative in sorted(actual_paths & manifest_paths):
        actual = hashlib.sha256((root / relative).read_bytes()).hexdigest()
        if actual != entries[relative]:
            issues.append(
                _issue(
                    "SHA-256_MISMATCH",
                    relative,
                    f"Expected {entries[relative]}, calculated {actual}.",
                )
            )
    return entries


def _expect(
    issues: list[str],
    *,
    code: str,
    path: str,
    label: str,
    actual: object,
    expected: object,
) -> None:
    if actual != expected:
        issues.append(
            _issue(code, path, f"{label} expected {expected!r}, found {actual!r}.")
        )


def _validate_records(
    root: Path,
    *,
    expected_run_id: str,
    expected_run_attempt: int,
    expected_workspace: str,
    expected_cache_sha256: str,
    issues: list[str],
) -> None:
    bundle = _load_json(root / "bundle.json", issues)
    environment = _load_json(root / "environment.json", issues)
    provenance = _load_json(root / "source-provenance.json", issues)
    cache = _load_json(root / "cmake-cache-summary.json", issues)
    dependencies = _load_json(root / "dependencies.json", issues)
    configure = _load_json(
        root / "commands/route-a-runtime-configure.command.json", issues
    )
    configure_resources = _load_json(
        root / "commands/route-a-runtime-configure.resources.json", issues
    )

    if isinstance(bundle, dict):
        expected = {
            "schema_version": "1.0",
            "campaign_id": CAMPAIGN_ID,
            "route_id": ROUTE_ID,
            "component": COMPONENT,
            "source_commit": SOURCE_COMMIT,
            "run_id": expected_run_id,
            "run_attempt": expected_run_attempt,
        }
        for key, value in expected.items():
            _expect(
                issues,
                code="IDENTITY_MISMATCH",
                path="bundle.json",
                label=("Run attempt" if key == "run_attempt" else key),
                actual=bundle.get(key),
                expected=value,
            )

    source_root = expected_workspace + r"\ov"
    build_root = expected_workspace + r"\b-ov"
    install_root = expected_workspace + r"\i-ov"
    if isinstance(environment, dict):
        expected = {
            "schema_version": "1.0",
            "campaign_id": CAMPAIGN_ID,
            "route_id": ROUTE_ID,
            "component": COMPONENT,
            "work_directory": expected_workspace,
            "source_directory": source_root,
            "build_directory": build_root,
            "install_directory": install_root,
            "generator": "Visual Studio 17 2022",
            "platform": "x64",
            "configuration": "Release",
            "parallelism": 1,
            "python": "Python 3.12.10",
        }
        for key, value in expected.items():
            _expect(
                issues,
                code="ENVIRONMENT_MISMATCH",
                path="environment.json",
                label=("Workspace" if key == "work_directory" else key),
                actual=environment.get(key),
                expected=value,
            )

    if isinstance(provenance, dict):
        expected = {
            "schema_version": "1.0",
            "campaign_id": CAMPAIGN_ID,
            "route_id": ROUTE_ID,
            "component": COMPONENT,
            "requested_repository": SOURCE_REPOSITORY,
            "actual_repository": SOURCE_REPOSITORY,
            "requested_commit": SOURCE_COMMIT,
            "actual_commit": SOURCE_COMMIT,
            "clean_before_configure": True,
            "recursive_submodules_complete": True,
            "build_document": BUILD_DOCUMENT,
            "build_document_sha256": BUILD_DOCUMENT_SHA256,
            "source_root": source_root,
        }
        for key, value in expected.items():
            _expect(
                issues,
                code="PROVENANCE_MISMATCH",
                path="source-provenance.json",
                label=key,
                actual=provenance.get(key),
                expected=value,
            )
        count = provenance.get("recursive_submodule_count")
        if isinstance(count, bool) or not isinstance(count, int) or count <= 0:
            issues.append(
                _issue(
                    "PROVENANCE_MISMATCH",
                    "source-provenance.json",
                    "recursive_submodule_count must be a positive integer.",
                )
            )

    if isinstance(cache, dict):
        _expect(
            issues,
            code="CACHE_MISMATCH",
            path="cmake-cache-summary.json",
            label="CMake cache digest",
            actual=cache.get("sha256"),
            expected=expected_cache_sha256,
        )
        values = cache.get("values")
        if not isinstance(values, dict):
            issues.append(
                _issue(
                    "CACHE_MISMATCH",
                    "cmake-cache-summary.json",
                    "values must be a JSON object.",
                )
            )
        else:
            for key, value in EXPECTED_CACHE_VALUES.items():
                _expect(
                    issues,
                    code="CACHE_MISMATCH",
                    path="cmake-cache-summary.json",
                    label=key,
                    actual=values.get(key),
                    expected=value,
                )

    if not isinstance(dependencies, list):
        issues.append(
            _issue(
                "DEPENDENCIES_INVALID",
                "dependencies.json",
                "Dependencies must be a JSON array.",
            )
        )
    else:
        for index, dependency in enumerate(dependencies):
            if not isinstance(dependency, dict):
                issues.append(
                    _issue(
                        "DEPENDENCIES_INVALID",
                        "dependencies.json",
                        f"Dependency {index} must be a JSON object.",
                    )
                )
                continue
            for key, value in {
                "campaign_id": CAMPAIGN_ID,
                "record_type": "build-dependency",
                "route_id": ROUTE_ID,
                "component": COMPONENT,
                "producer_command_id": "route-a-runtime-configure",
            }.items():
                _expect(
                    issues,
                    code="DEPENDENCIES_INVALID",
                    path="dependencies.json",
                    label=f"dependency[{index}].{key}",
                    actual=dependency.get(key),
                    expected=value,
                )

    if isinstance(configure, dict):
        expected = {
            "campaign_id": CAMPAIGN_ID,
            "record_type": "build-command",
            "command_id": "route-a-runtime-configure",
            "route_id": ROUTE_ID,
            "component": COMPONENT,
            "working_directory": expected_workspace,
            "exit_code": 0,
            "stdout_path": "commands/route-a-runtime-configure.stdout.log",
            "stderr_path": "commands/route-a-runtime-configure.stderr.log",
        }
        for key, value in expected.items():
            _expect(
                issues,
                code="CONFIGURE_INVALID",
                path="commands/route-a-runtime-configure.command.json",
                label=key,
                actual=configure.get(key),
                expected=value,
            )
        arguments = configure.get("arguments")
        if not isinstance(arguments, list):
            issues.append(
                _issue(
                    "CONFIGURE_INVALID",
                    "commands/route-a-runtime-configure.command.json",
                    "arguments must be a JSON array.",
                )
            )
        else:
            for argument in ("-S", source_root, "-B", build_root):
                if argument not in arguments:
                    issues.append(
                        _issue(
                            "CONFIGURE_INVALID",
                            "commands/route-a-runtime-configure.command.json",
                            f"Missing reviewed configure argument {argument!r}.",
                        )
                    )

    if isinstance(configure_resources, dict):
        expected = {
            "campaign_id": CAMPAIGN_ID,
            "record_type": "build-resource-summary",
            "route_id": ROUTE_ID,
            "component": COMPONENT,
            "command_id": "route-a-runtime-configure",
            "safety_stop_triggered": False,
        }
        for key, value in expected.items():
            _expect(
                issues,
                code="CONFIGURE_RESOURCE_INVALID",
                path="commands/route-a-runtime-configure.resources.json",
                label=key,
                actual=configure_resources.get(key),
                expected=value,
            )

    trace = root / "commands/route-a-runtime-build.resources.csv"
    try:
        nonempty = [
            line
            for line in trace.read_text(encoding="utf-8-sig").splitlines()
            if line.strip()
        ]
    except (OSError, UnicodeError) as error:
        issues.append(
            _issue(
                "BUILD_RESOURCE_TRACE_INVALID",
                "commands/route-a-runtime-build.resources.csv",
                str(error),
            )
        )
    else:
        if len(nonempty) < 2:
            issues.append(
                _issue(
                    "BUILD_RESOURCE_TRACE_INVALID",
                    "commands/route-a-runtime-build.resources.csv",
                    "Runtime build resource trace is empty or has no data row.",
                )
            )


def validate_runtime_resume_bundle(
    bundle_root: Path,
    *,
    expected_run_id: str,
    expected_run_attempt: int,
    expected_workspace: str,
    expected_cache_sha256: str,
) -> ValidationResult:
    root = Path(bundle_root)
    issues: list[str] = []

    if re.fullmatch(r"[0-9]+", expected_run_id) is None:
        issues.append(_issue("INPUT_INVALID", "expected_run_id", "Must contain digits only."))
    if expected_run_attempt <= 0:
        issues.append(_issue("INPUT_INVALID", "expected_run_attempt", "Must be positive."))
    if re.fullmatch(r"[0-9a-f]{64}", expected_cache_sha256) is None:
        issues.append(
            _issue(
                "INPUT_INVALID",
                "expected_cache_sha256",
                "Must be a lowercase SHA-256 digest.",
            )
        )
    if re.fullmatch(r"C:\\w5a\\[A-Za-z0-9._-]+", expected_workspace) is None:
        issues.append(
            _issue(
                "INPUT_INVALID",
                "expected_workspace",
                r"Workspace must be one direct normal child of C:\w5a.",
            )
        )
    if not root.is_dir():
        issues.append(_issue("BUNDLE_MISSING", ".", "Bundle root is not a directory."))
        return ValidationResult(False, 0, 0, tuple(issues))

    file_count = _validate_payloads(root, issues)
    present = {candidate.relative_to(root).as_posix() for candidate in _files(root)}
    for required in sorted(REQUIRED_PATHS - present):
        issues.append(_issue("REQUIRED_PATH_MISSING", required, "Required file is missing."))
    for forbidden in sorted(FORBIDDEN_COMPLETION_PATHS & present):
        if forbidden == "decision.json":
            message = "decision.json must be absent from the externally cancelled prerequisite."
        elif forbidden.endswith("build.command.json"):
            message = "Completed Runtime build command record must be absent from timeout evidence."
        else:
            message = "Integrity failure record is incompatible with controlled resume."
        issues.append(_issue("UNEXPECTED_COMPLETION_RECORD", forbidden, message))

    entries = _read_manifest(root, issues)
    if REQUIRED_PATHS - present:
        return ValidationResult(False, file_count, len(entries), tuple(issues))

    _validate_records(
        root,
        expected_run_id=expected_run_id,
        expected_run_attempt=expected_run_attempt,
        expected_workspace=expected_workspace,
        expected_cache_sha256=expected_cache_sha256,
        issues=issues,
    )
    return ValidationResult(not issues, file_count, len(entries), tuple(issues))


def _write_report(
    path: Path,
    *,
    result: ValidationResult,
    expected_run_id: str,
    expected_run_attempt: int,
    expected_workspace: str,
    expected_cache_sha256: str,
) -> None:
    lines = [
        "# Workbook 05 Route A Runtime controlled-resume prerequisite validation",
        "",
        f"- Workflow run: `{expected_run_id}`",
        f"- Run attempt: `{expected_run_attempt}`",
        f"- Workspace: `{expected_workspace}`",
        f"- CMake cache SHA-256: `{expected_cache_sha256}`",
        f"- Files checked: `{result.file_count}`",
        f"- Manifest entries: `{result.manifest_entry_count}`",
        "",
    ]
    if result.valid:
        lines.append(
            "Validation passed: the exact timeout bundle is internally consistent "
            "and may be used only as a controlled-resume prerequisite."
        )
    else:
        lines.append(f"Validation failed with {len(result.reasons)} issue(s).")
        lines.append("")
        lines.extend(f"- `{reason}`" for reason in result.reasons)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Validate one incomplete Route A Runtime timeout artifact as "
            "untrusted controlled-resume prerequisite data."
        )
    )
    parser.add_argument("--bundle-root", type=Path, required=True)
    parser.add_argument("--expected-run-id", required=True)
    parser.add_argument("--expected-run-attempt", type=int, required=True)
    parser.add_argument("--expected-workspace", required=True)
    parser.add_argument("--expected-cache-sha256", required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args(argv)

    result = validate_runtime_resume_bundle(
        args.bundle_root,
        expected_run_id=args.expected_run_id,
        expected_run_attempt=args.expected_run_attempt,
        expected_workspace=args.expected_workspace,
        expected_cache_sha256=args.expected_cache_sha256,
    )
    _write_report(
        args.report,
        result=result,
        expected_run_id=args.expected_run_id,
        expected_run_attempt=args.expected_run_attempt,
        expected_workspace=args.expected_workspace,
        expected_cache_sha256=args.expected_cache_sha256,
    )
    print(args.report.read_text(encoding="utf-8"), end="")
    return 0 if result.valid else 1


if __name__ == "__main__":
    raise SystemExit(main())
