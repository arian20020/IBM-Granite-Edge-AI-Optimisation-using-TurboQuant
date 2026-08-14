"""Build fail-closed evidence for the C1 conversion dependency preflight."""

from __future__ import annotations

import re
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Sequence

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_DIRECT_REQUIREMENTS,
)


# The Windows orchestrator must execute these checks in this exact order.
REQUIRED_CHECK_NAMES: tuple[str, ...] = (
    "resolver",
    "install",
    "imports",
    "cli_help",
    "no_model_compatibility",
    "remote_code_disabled",
)

# Imports run in a new process so cached modules cannot hide a broken install.
IMPORT_MODULES: tuple[str, ...] = (
    "optimum",
    "optimum.intel",
    "transformers",
    "nncf",
    "openvino",
)

CAMPAIGN_ID = "GTQ-WB05-MF-v1"
ROUTE_ID = "route-a-merged-openvino"
RECORD_TYPE = "conversion-dependency-preflight"

_SHA256 = re.compile(r"^[0-9a-f]{64}$")
_HASH_IDENTITY = re.compile(r"^sha256:[0-9a-f]{64}$")
_OPTIMUM_INTEL_VERSION = re.compile(r"^2\.3\.0\.dev0(?:\+[0-9a-f]+)?$")

# Repository, origin, and commit are all checked. A matching package version is
# not enough to prove which source tree produced a VCS installation.
_EXPECTED_SOURCES: dict[str, tuple[str, str, str]] = {
    "optimum-intel": (
        "huggingface/optimum-intel",
        "https://github.com/huggingface/optimum-intel.git",
        OPTIMUM_INTEL_COMMIT,
    ),
    "optimum": (
        "huggingface/optimum",
        "https://github.com/huggingface/optimum.git",
        OPTIMUM_COMMIT,
    ),
}

# Normal distributions use wheel/sdist SHA-256 identities. The two VCS packages
# use their reviewed full commit identities and separate source-tree manifests.
_EXPECTED_DIRECT_PACKAGES: dict[str, tuple[str, str | None]] = {
    "optimum-intel": ("2.3.0.dev0", OPTIMUM_INTEL_COMMIT),
    "optimum": ("2.3.0", OPTIMUM_COMMIT),
    "transformers": ("5.5.0", None),
    "huggingface-hub": ("1.21.0", None),
    "nncf": ("3.2.0", None),
    "openvino": ("2026.2.1", None),
    "openvino-tokenizers": ("2026.2.1.0", None),
}


@dataclass(frozen=True, slots=True)
class SourceTreeEvidence:
    """One immutable VCS checkout and its complete source-tree identity."""

    name: str
    repository: str
    origin: str
    commit: str
    clean: bool
    aggregate_sha256: str


@dataclass(frozen=True, slots=True)
class DependencyPackage:
    """One installed package observed after installing the reviewed lock."""

    name: str
    version: str
    source_identity: str
    direct: bool


@dataclass(frozen=True, slots=True)
class DependencyCheck:
    """One independently observable check from the clean environment."""

    name: str
    status: str
    exit_code: int | None


def _text(value: object, label: str) -> str:
    """Return one exact non-empty string without silently normalising it."""

    if not isinstance(value, str) or not value or value != value.strip():
        raise ValueError(f"{label} must be a non-empty exact string.")
    if "\x00" in value or any(ord(character) < 32 for character in value):
        raise ValueError(f"{label} must not contain control characters.")
    return value


def _sha256(value: object, label: str) -> str:
    """Require one lowercase SHA-256 digest."""

    if not isinstance(value, str) or not _SHA256.fullmatch(value):
        raise ValueError(f"{label} must be a lowercase SHA-256 digest.")
    return value


def _utc_timestamp(value: object) -> str:
    """Require an explicit RFC 3339 UTC timestamp."""

    if not isinstance(value, str) or not value.endswith("Z"):
        raise ValueError("generated_at_utc must be an RFC 3339 UTC timestamp.")
    try:
        parsed = datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise ValueError(
            "generated_at_utc must be an RFC 3339 UTC timestamp."
        ) from error
    if parsed.utcoffset() is None or parsed.utcoffset().total_seconds() != 0:
        raise ValueError("generated_at_utc must use UTC.")
    return value


def _portable_path(value: object) -> str:
    """Validate one canonical forward-slash path stored in portable evidence."""

    text = _text(value, "Dependency lock path")
    if "\\" in text or text.startswith("/") or text.endswith("/") or "//" in text:
        raise ValueError("Dependency lock path must be portable and relative.")
    parts = text.split("/")
    if any(part in {"", ".", ".."} for part in parts):
        raise ValueError("Dependency lock path contains an unsafe segment.")
    if PurePosixPath(text).is_absolute() or PureWindowsPath(text).drive:
        raise ValueError("Dependency lock path must not be absolute.")
    return text


def _windows_path(value: Path, label: str) -> PureWindowsPath:
    """Parse an unambiguous absolute Windows path without touching this host."""

    text = str(value)
    lowered = text.casefold()
    if text.startswith("\\\\") or lowered.startswith(("\\\\?\\", "\\\\.\\")):
        raise ValueError(f"{label} must not be a UNC or device path: {text}")
    if "\x00" in text or any(ord(character) < 32 for character in text):
        raise ValueError(f"{label} must not contain control characters.")
    path = PureWindowsPath(text)
    if not path.is_absolute() or not path.drive:
        raise ValueError(f"{label} must be an absolute Windows path: {text}")
    if any(part in {"", ".", ".."} for part in path.parts):
        raise ValueError(f"{label} contains an unsafe path segment: {text}")
    return path


def _strict_child(candidate: PureWindowsPath, root: PureWindowsPath) -> bool:
    """Compare Windows components case-insensitively and reject root equality."""

    candidate_parts = tuple(part.casefold() for part in candidate.parts)
    root_parts = tuple(part.casefold() for part in root.parts)
    return (
        len(candidate_parts) > len(root_parts)
        and candidate_parts[: len(root_parts)] == root_parts
    )


def _source_records(
    values: Sequence[SourceTreeEvidence],
) -> tuple[list[dict[str, Any]], list[str]]:
    """Validate the exact pair of reviewed VCS trees."""

    records: list[dict[str, Any]] = []
    issues: list[str] = []
    observed: dict[str, SourceTreeEvidence] = {}

    for value in values:
        name = _text(value.name, "Source-tree name")
        key = name.casefold()
        if key in observed:
            raise ValueError(f"duplicate source-tree identity: {name}")
        observed[key] = value
        if not isinstance(value.clean, bool):
            raise ValueError(f"Clean flag for {name} must be Boolean.")
        records.append(
            {
                "name": name,
                "repository": _text(value.repository, f"Repository for {name}"),
                "origin": _text(value.origin, f"Origin for {name}"),
                "commit": _text(value.commit, f"Commit for {name}"),
                "clean": value.clean,
                "aggregate_sha256": _sha256(
                    value.aggregate_sha256,
                    f"Source-tree digest for {name}",
                ),
            }
        )

    if set(observed) != set(_EXPECTED_SOURCES):
        issues.append("The source-tree catalogue differs from the reviewed VCS set.")
    for name, expected in _EXPECTED_SOURCES.items():
        value = observed.get(name)
        if value is None:
            continue
        repository, origin, commit = expected
        if (value.repository, value.origin, value.commit) != (
            repository,
            origin,
            commit,
        ):
            issues.append(f"The {name} source identity does not match the reviewed commit.")
        if not value.clean:
            issues.append(f"The {name} source checkout is not clean.")

    records.sort(key=lambda item: str(item["name"]).casefold())
    return records, issues


def _package_records(
    values: Sequence[DependencyPackage],
) -> tuple[list[dict[str, Any]], list[str]]:
    """Validate direct package identities and hashed transitive artifacts."""

    records: list[dict[str, Any]] = []
    issues: list[str] = []
    observed: dict[str, DependencyPackage] = {}

    for value in values:
        name = _text(value.name, "Package name")
        key = name.casefold()
        if key in observed:
            raise ValueError(f"duplicate package identity: {name}")
        observed[key] = value
        if not isinstance(value.direct, bool):
            raise ValueError(f"Direct flag for {name} must be Boolean.")
        records.append(
            {
                "name": name,
                "version": _text(value.version, f"Version for {name}"),
                "source_identity": _text(
                    value.source_identity,
                    f"Source identity for {name}",
                ),
                "direct": value.direct,
            }
        )

    direct = {name: value for name, value in observed.items() if value.direct}
    if set(direct) != set(_EXPECTED_DIRECT_PACKAGES):
        issues.append("The direct package catalogue differs from the reviewed set.")

    for name, (expected_version, expected_commit) in _EXPECTED_DIRECT_PACKAGES.items():
        value = direct.get(name)
        if value is None:
            continue
        version_ok = value.version == expected_version
        if name == "optimum-intel":
            version_ok = bool(_OPTIMUM_INTEL_VERSION.fullmatch(value.version))
        if not version_ok:
            issues.append(f"The {name} package version does not match the reviewed set.")
        if expected_commit is not None:
            identity_ok = value.source_identity == expected_commit
        else:
            identity_ok = bool(_HASH_IDENTITY.fullmatch(value.source_identity))
        if not identity_ok:
            issues.append(f"The {name} package source identity is not approved.")

    for name, value in observed.items():
        if not value.direct and not _HASH_IDENTITY.fullmatch(value.source_identity):
            issues.append(f"The transitive package {name} source identity is not hashed.")

    records.sort(key=lambda item: str(item["name"]).casefold())
    return records, issues


def _check_records(
    values: Sequence[DependencyCheck],
) -> tuple[list[dict[str, Any]], list[str], list[str]]:
    """Validate the exact check catalogue and classify its observations."""

    records: list[dict[str, Any]] = []
    integrity: list[str] = []
    blocked: list[str] = []
    observed: dict[str, DependencyCheck] = {}

    for value in values:
        name = _text(value.name, "Check name")
        key = name.casefold()
        if key in observed:
            raise ValueError(f"duplicate check identity: {name}")
        observed[key] = value
        status = _text(value.status, f"Status for {name}")
        if status not in {"Passed", "Failed", "Blocked", "IntegrityFailure"}:
            raise ValueError(f"Unsupported check status for {name}: {status}")
        if value.exit_code is not None and (
            isinstance(value.exit_code, bool)
            or not isinstance(value.exit_code, int)
        ):
            raise ValueError(f"Exit code for {name} must be an integer or null.")
        records.append(
            {"name": name, "status": status, "exit_code": value.exit_code}
        )

    if set(observed) != set(REQUIRED_CHECK_NAMES):
        integrity.append("The dependency check catalogue is incomplete or unexpected.")
    for name in REQUIRED_CHECK_NAMES:
        value = observed.get(name)
        if value is None:
            continue
        if value.status == "IntegrityFailure":
            integrity.append(f"Dependency check {name} reported IntegrityFailure.")
        elif value.status != "Passed":
            blocked.append(
                f"Dependency check {name} did not pass "
                f"(status={value.status}, exit_code={value.exit_code})."
            )

    order = {name: index for index, name in enumerate(REQUIRED_CHECK_NAMES)}
    records.sort(key=lambda item: order.get(str(item["name"]), len(order)))
    return records, integrity, blocked


def collect_dependency_preflight_record(
    *,
    generated_at_utc: str,
    workspace_root: Path,
    workspace_is_normal_local_directory: bool,
    workspace_is_fresh: bool,
    python_version: str,
    python_executable_path: Path,
    python_executable_sha256: str,
    pip_version: str,
    pip_executable_path: Path,
    pip_executable_sha256: str,
    source_trees: Sequence[SourceTreeEvidence],
    direct_requirements: Sequence[str],
    lock_path: str,
    lock_sha256: str,
    lock_generator: str,
    normal_distribution_count: int,
    all_normal_artifacts_hashed: bool,
    vcs_sources_bound_separately: bool,
    packages: Sequence[DependencyPackage],
    checks: Sequence[DependencyCheck],
    import_modules: Sequence[str],
    cli_help_exit_code: int,
    no_model_compatibility_exit_code: int,
) -> dict[str, Any]:
    """Return a deterministic preflight record without authorising download."""

    # Malformed input raises because an unambiguous record cannot be created.
    generated_at = _utc_timestamp(generated_at_utc)
    workspace = _windows_path(workspace_root, "Dependency-preflight workspace")
    python_path = _windows_path(python_executable_path, "Python executable")
    pip_path = _windows_path(pip_executable_path, "pip executable")
    python_digest = _sha256(python_executable_sha256, "Python executable digest")
    pip_digest = _sha256(pip_executable_sha256, "pip executable digest")
    lock_digest = _sha256(lock_sha256, "Dependency lock digest")
    portable_lock_path = _portable_path(lock_path)
    python_version_text = _text(python_version, "Python version")
    pip_version_text = _text(pip_version, "pip version")
    generator = _text(lock_generator, "Dependency lock generator")

    if isinstance(normal_distribution_count, bool) or not isinstance(
        normal_distribution_count,
        int,
    ) or normal_distribution_count < 0:
        raise ValueError("normal_distribution_count must be non-negative.")
    for label, value in (
        ("workspace_is_normal_local_directory", workspace_is_normal_local_directory),
        ("workspace_is_fresh", workspace_is_fresh),
        ("all_normal_artifacts_hashed", all_normal_artifacts_hashed),
        ("vcs_sources_bound_separately", vcs_sources_bound_separately),
    ):
        if not isinstance(value, bool):
            raise ValueError(f"{label} must be Boolean.")
    for label, value in (
        ("cli_help_exit_code", cli_help_exit_code),
        ("no_model_compatibility_exit_code", no_model_compatibility_exit_code),
    ):
        if isinstance(value, bool) or not isinstance(value, int):
            raise ValueError(f"{label} must be an integer.")

    source_records, source_issues = _source_records(source_trees)
    package_records, package_issues = _package_records(packages)
    check_records, check_integrity, check_blocked = _check_records(checks)
    integrity = source_issues + package_issues + check_integrity
    blocked = list(check_blocked)

    # Well-formed observations that violate policy are retained as evidence and
    # classified as IntegrityFailure rather than being discarded as exceptions.
    if not _strict_child(workspace, PureWindowsPath(r"C:\w5c")):
        integrity.append("The dependency-preflight workspace is outside C:\\w5c.")
    if not _strict_child(python_path, workspace):
        integrity.append("The Python executable is outside the preflight workspace.")
    if python_path.name.casefold() != "python.exe":
        integrity.append("The Python executable path does not end with python.exe.")
    if not _strict_child(pip_path, workspace):
        integrity.append("The pip executable is outside the preflight workspace.")
    if pip_path.name.casefold() != "pip.exe":
        integrity.append("The pip executable path does not end with pip.exe.")
    if not workspace_is_normal_local_directory:
        integrity.append("The dependency-preflight workspace is not a normal local directory.")
    if not workspace_is_fresh:
        integrity.append("The dependency-preflight workspace is not fresh.")
    if python_version_text != "3.12.10":
        integrity.append(f"Python version drifted from 3.12.10: {python_version_text}.")
    if generator != "pip-tools==7.5.0":
        integrity.append(f"The lock generator drifted from pip-tools==7.5.0: {generator}.")

    reviewed_requirements = tuple(
        _text(value, "Direct requirement")
        for value in direct_requirements
    )
    if reviewed_requirements != REVIEWED_DIRECT_REQUIREMENTS:
        integrity.append("The direct requirements do not match the reviewed set exactly.")
    if not all_normal_artifacts_hashed:
        integrity.append("One or more normal dependency artifacts are missing a SHA-256 hash.")
    if not vcs_sources_bound_separately:
        integrity.append("The VCS commits and source-tree manifests were not bound separately.")

    observed_imports = tuple(_text(value, "Import module") for value in import_modules)
    if observed_imports != IMPORT_MODULES:
        integrity.append("The import-module catalogue does not match the reviewed checks.")

    # Duplicate exit-code fields deliberately make tampering detectable.
    by_name = {str(item["name"]): item for item in check_records}
    cli_check = by_name.get("cli_help")
    model_check = by_name.get("no_model_compatibility")
    if cli_check is not None and cli_check["exit_code"] != cli_help_exit_code:
        integrity.append("The cli_help exit code disagrees with its check record.")
    elif cli_help_exit_code != 0:
        blocked.append(f"cli_help exited with code {cli_help_exit_code}.")
    if model_check is not None and model_check["exit_code"] != no_model_compatibility_exit_code:
        integrity.append("The no_model_compatibility exit code disagrees with its check record.")
    elif no_model_compatibility_exit_code != 0:
        blocked.append(
            "no_model_compatibility exited with code "
            f"{no_model_compatibility_exit_code}."
        )

    if integrity:
        status = "IntegrityFailure"
        reasons = integrity + blocked
    elif blocked:
        status = "Blocked"
        reasons = blocked
    else:
        status = "Passed"
        reasons = [
            "The reviewed dependency identities and no-model checks passed."
        ]

    return {
        "schema_version": "1.0",
        "campaign_id": CAMPAIGN_ID,
        "record_type": RECORD_TYPE,
        "route_id": ROUTE_ID,
        "generated_at_utc": generated_at,
        "workspace": {
            "root": str(workspace),
            "normal_local_directory": workspace_is_normal_local_directory,
            "fresh": workspace_is_fresh,
        },
        "python": {
            "version": python_version_text,
            "executable_path": str(python_path),
            "executable_sha256": python_digest,
        },
        "pip": {
            "version": pip_version_text,
            "executable_path": str(pip_path),
            "executable_sha256": pip_digest,
        },
        "source_trees": source_records,
        "direct_requirements": list(reviewed_requirements),
        "lock": {
            "path": portable_lock_path,
            "sha256": lock_digest,
            "generator": generator,
            "normal_distribution_count": normal_distribution_count,
            "all_normal_artifacts_hashed": all_normal_artifacts_hashed,
            "vcs_sources_bound_separately": vcs_sources_bound_separately,
        },
        "packages": package_records,
        "checks": check_records,
        "import_modules": list(observed_imports),
        "cli_help_exit_code": cli_help_exit_code,
        "no_model_compatibility_exit_code": no_model_compatibility_exit_code,
        "status": status,
        "reasons": list(dict.fromkeys(reasons)),
        "model_download_authorised": False,
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }
