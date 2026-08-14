"""Validate the C1 normal-distribution lock and build preflight evidence.

The two reviewed VCS packages are deliberately excluded from the normal wheel
lock. They are bound separately to full Git commits and complete source-tree
SHA-256 manifests. Every ordinary installed distribution must match one hash in
the generated lock and the actual artifact recorded by pip's install report.
"""

from __future__ import annotations

import hashlib
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_DIRECT_REQUIREMENTS,
)
from scripts.testing.workbook05.phase3.dependency_preflight import (
    IMPORT_MODULES,
    DependencyCheck,
    DependencyPackage,
    SourceTreeEvidence,
    collect_dependency_preflight_record,
)


_HASH_ARGUMENT = re.compile(r"--hash=sha256:([0-9a-f]{64})(?=\s|$)")
_PINNED_REQUIREMENT = re.compile(
    r"^([A-Za-z0-9][A-Za-z0-9_.-]*)==([^\s;]+)(?:\s*;\s*(.+))?$"
)
_NAME_SEPARATOR = re.compile(r"[-_.]+")
_SHA256 = re.compile(r"^[0-9a-f]{64}$")

VCS_PACKAGE_NAMES = frozenset({"optimum", "optimum-intel"})
DIRECT_NORMAL_VERSIONS: dict[str, str] = {
    "transformers": "5.5.0",
    "huggingface-hub": "1.21.0",
    "nncf": "3.2.0",
    "openvino": "2026.2.1",
    "openvino-tokenizers": "2026.2.1.0",
}


@dataclass(frozen=True, slots=True)
class LockedDistribution:
    """One exact ordinary package version and its acceptable archive hashes."""

    name: str
    version: str
    hashes: tuple[str, ...]
    marker: str | None = None


def _normalise_name(value: str) -> str:
    """Return the canonical PEP 503 distribution name."""

    return _NAME_SEPARATOR.sub("-", value).casefold()


def _logical_requirement_records(text: str) -> list[str]:
    """Join pip-compile backslash continuations without executing the file."""

    if not isinstance(text, str) or not text.strip():
        raise ValueError("The dependency lock must be non-empty UTF-8 text.")

    records: list[str] = []
    current: list[str] = []
    for line_number, raw_line in enumerate(text.splitlines(), start=1):
        stripped = raw_line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        if stripped.startswith("--") and not current:
            raise ValueError(
                "Dependency lock options and alternate indexes are forbidden: "
                f"line {line_number}."
            )

        continued = stripped.endswith("\\")
        segment = stripped[:-1].rstrip() if continued else stripped
        current.append(segment)
        if continued:
            continue

        records.append(" ".join(current))
        current = []

    if current:
        raise ValueError("The dependency lock ends with an incomplete continuation.")
    if not records:
        raise ValueError("The dependency lock contains no requirement records.")
    return records


def parse_hash_locked_requirements(text: str) -> tuple[LockedDistribution, ...]:
    """Parse a complete ordinary-package lock and reject moving/unhashed input."""

    observed: dict[str, LockedDistribution] = {}
    for record in _logical_requirement_records(text):
        lowered = record.casefold()
        if (
            "git+" in lowered
            or " @ " in record
            or "https://" in lowered
            or "http://" in lowered
            or record.startswith("-e ")
        ):
            raise ValueError(
                "VCS, URL, editable, and moving requirements are forbidden in "
                "the normal-distribution lock."
            )

        hashes = tuple(sorted(set(_HASH_ARGUMENT.findall(record))))
        if not hashes:
            raise ValueError(f"A requirement is not hash locked: {record}")

        requirement_text = _HASH_ARGUMENT.sub("", record)
        requirement_text = " ".join(requirement_text.split()).strip()
        match = _PINNED_REQUIREMENT.fullmatch(requirement_text)
        if match is None:
            raise ValueError(
                "Every normal requirement must use one exact `name==version` "
                f"pin: {requirement_text}"
            )

        raw_name, version, marker = match.groups()
        name = _normalise_name(raw_name)
        if name in observed:
            raise ValueError(f"Duplicate locked distribution: {name}")
        observed[name] = LockedDistribution(
            name=name,
            version=version,
            hashes=hashes,
            marker=marker,
        )

    for name, expected_version in DIRECT_NORMAL_VERSIONS.items():
        package = observed.get(name)
        if package is None:
            raise ValueError(
                f"The lock is missing reviewed direct normal package: {name}"
            )
        if package.version != expected_version:
            raise ValueError(
                f"{name} must remain at {expected_version}, found {package.version}."
            )

    return tuple(
        observed[name]
        for name in sorted(observed, key=lambda item: (item.casefold(), item))
    )


def _archive_sha256(item: Mapping[str, Any]) -> str:
    """Return the actual artifact SHA-256 from one pip install-report row."""

    download_info = item.get("download_info")
    if not isinstance(download_info, Mapping):
        raise ValueError("A pip install-report item lacks download_info.")
    archive_info = download_info.get("archive_info")
    if not isinstance(archive_info, Mapping):
        raise ValueError("A pip install-report item lacks archive_info.")

    hashes = archive_info.get("hashes")
    digest: object | None = None
    if isinstance(hashes, Mapping):
        digest = hashes.get("sha256")
    if digest is None:
        legacy_hash = archive_info.get("hash")
        if isinstance(legacy_hash, str) and legacy_hash.startswith("sha256="):
            digest = legacy_hash.removeprefix("sha256=")

    if not isinstance(digest, str) or not _SHA256.fullmatch(digest):
        raise ValueError(
            "Every installed normal distribution requires an actual artifact "
            "SHA-256 in pip's install report."
        )
    return digest


def parse_normal_install_report(
    report: Mapping[str, Any],
    lock: Sequence[LockedDistribution],
) -> tuple[DependencyPackage, ...]:
    """Bind each installed normal distribution to one allowed lock hash."""

    rows = report.get("install")
    if not isinstance(rows, list) or not rows:
        raise ValueError("The pip install report contains no installed packages.")

    locked = {package.name: package for package in lock}
    observed: dict[str, DependencyPackage] = {}
    for item in rows:
        if not isinstance(item, Mapping):
            raise ValueError("A pip install-report row is not an object.")
        metadata = item.get("metadata")
        if not isinstance(metadata, Mapping):
            raise ValueError("A pip install-report row lacks package metadata.")
        raw_name = metadata.get("name")
        version = metadata.get("version")
        if not isinstance(raw_name, str) or not raw_name:
            raise ValueError("An installed package lacks a name.")
        if not isinstance(version, str) or not version:
            raise ValueError(f"Installed package {raw_name} lacks a version.")

        name = _normalise_name(raw_name)
        if name in VCS_PACKAGE_NAMES:
            raise ValueError(
                f"VCS package {name} must not be admitted as a normal distribution."
            )
        if name in observed:
            raise ValueError(f"Duplicate installed distribution: {name}")

        locked_package = locked.get(name)
        if locked_package is None:
            raise ValueError(
                f"Installed package {name} is not present in the reviewed lock."
            )
        if version != locked_package.version:
            raise ValueError(
                f"Installed {name} version {version} differs from lock "
                f"version {locked_package.version}."
            )

        digest = _archive_sha256(item)
        if digest not in locked_package.hashes:
            raise ValueError(
                f"Installed artifact SHA-256 for {name} is not present in the lock."
            )
        observed[name] = DependencyPackage(
            name=name,
            version=version,
            source_identity=f"sha256:{digest}",
            direct=name in DIRECT_NORMAL_VERSIONS,
        )

    for name in DIRECT_NORMAL_VERSIONS:
        if name not in observed:
            raise ValueError(
                f"The install report omitted reviewed direct package: {name}"
            )

    return tuple(
        observed[name]
        for name in sorted(observed, key=lambda item: (item.casefold(), item))
    )


def _source_tree(value: Mapping[str, Any]) -> SourceTreeEvidence:
    return SourceTreeEvidence(
        name=str(value.get("name", "")),
        repository=str(value.get("repository", "")),
        origin=str(value.get("origin", "")),
        commit=str(value.get("commit", "")),
        clean=value.get("clean") is True,
        aggregate_sha256=str(value.get("aggregate_sha256", "")),
    )


def _dependency_check(value: Mapping[str, Any]) -> DependencyCheck:
    exit_code = value.get("exit_code")
    if exit_code is not None and (
        isinstance(exit_code, bool) or not isinstance(exit_code, int)
    ):
        raise ValueError("Dependency check exit_code must be an integer or null.")
    return DependencyCheck(
        name=str(value.get("name", "")),
        status=str(value.get("status", "")),
        exit_code=exit_code,
    )


def build_dependency_preflight_record(
    observation: Mapping[str, Any],
) -> dict[str, Any]:
    """Build one closed C1 preflight decision from observed command evidence."""

    lock_text = observation.get("lock_text")
    supplied_lock_sha = observation.get("lock_sha256")
    if not isinstance(lock_text, str):
        raise ValueError("lock_text must be supplied as UTF-8 text.")
    if not isinstance(supplied_lock_sha, str):
        raise ValueError("lock_sha256 must be supplied.")

    lock = parse_hash_locked_requirements(lock_text)
    report = observation.get("normal_install_report")
    if not isinstance(report, Mapping):
        raise ValueError("normal_install_report must be a JSON object.")
    normal_packages = parse_normal_install_report(report, lock)

    raw_vcs_packages = observation.get("vcs_packages")
    if not isinstance(raw_vcs_packages, list):
        raise ValueError("vcs_packages must be an array.")
    vcs_packages: list[DependencyPackage] = []
    expected_vcs = {
        "optimum-intel": ("2.3.0.dev0", OPTIMUM_INTEL_COMMIT),
        "optimum": ("2.3.0", OPTIMUM_COMMIT),
    }
    observed_vcs_names: set[str] = set()
    for item in raw_vcs_packages:
        if not isinstance(item, Mapping):
            raise ValueError("A VCS package row is not an object.")
        name = _normalise_name(str(item.get("name", "")))
        version = str(item.get("version", ""))
        commit = str(item.get("commit", ""))
        if name in observed_vcs_names:
            raise ValueError(f"Duplicate VCS package: {name}")
        observed_vcs_names.add(name)
        expected = expected_vcs.get(name)
        if expected is None or (version, commit) != expected:
            raise ValueError(f"VCS package identity drifted: {name}")
        vcs_packages.append(
            DependencyPackage(
                name=name,
                version=version,
                source_identity=commit,
                direct=True,
            )
        )
    if observed_vcs_names != set(expected_vcs):
        raise ValueError("The exact two reviewed VCS packages are required.")

    raw_source_trees = observation.get("source_trees")
    raw_checks = observation.get("checks")
    if not isinstance(raw_source_trees, list) or not isinstance(raw_checks, list):
        raise ValueError("source_trees and checks must be arrays.")

    actual_lock_sha = hashlib.sha256(lock_text.encode("utf-8")).hexdigest()
    lock_matches = supplied_lock_sha == actual_lock_sha

    record = collect_dependency_preflight_record(
        generated_at_utc=str(observation.get("generated_at_utc", "")),
        workspace_root=Path(str(observation.get("workspace_root", ""))),
        workspace_is_normal_local_directory=(
            observation.get("workspace_is_normal_local_directory") is True
        ),
        workspace_is_fresh=observation.get("workspace_is_fresh") is True,
        python_version=str(observation.get("python_version", "")),
        python_executable_path=Path(
            str(observation.get("python_executable_path", ""))
        ),
        python_executable_sha256=str(
            observation.get("python_executable_sha256", "")
        ),
        pip_version=str(observation.get("pip_version", "")),
        pip_executable_path=Path(
            str(observation.get("pip_executable_path", ""))
        ),
        pip_executable_sha256=str(
            observation.get("pip_executable_sha256", "")
        ),
        source_trees=tuple(_source_tree(item) for item in raw_source_trees),
        direct_requirements=tuple(
            str(value) for value in observation.get("direct_requirements", [])
        ),
        lock_path=str(observation.get("lock_path", "")),
        lock_sha256=supplied_lock_sha,
        lock_generator=str(observation.get("lock_generator", "")),
        normal_distribution_count=len(normal_packages),
        all_normal_artifacts_hashed=lock_matches,
        vcs_sources_bound_separately=True,
        packages=tuple(vcs_packages) + normal_packages,
        checks=tuple(_dependency_check(item) for item in raw_checks),
        import_modules=tuple(
            str(value) for value in observation.get("import_modules", [])
        ),
        cli_help_exit_code=int(observation.get("cli_help_exit_code", -1)),
        no_model_compatibility_exit_code=int(
            observation.get("no_model_compatibility_exit_code", -1)
        ),
    )

    # Make the first causal integrity problem explicit rather than relying only
    # on the broader all-artifacts-hashed reason from the record collector.
    if not lock_matches:
        record["status"] = "IntegrityFailure"
        reasons = list(record.get("reasons", []))
        message = (
            "Dependency lock SHA-256 mismatch: the supplied identity does not "
            "match the complete lock text."
        )
        if message not in reasons:
            reasons.append(message)
        record["reasons"] = reasons

    # This package only qualifies dependencies. It never authorises acquisition
    # or any later scientific claim, even when every check passes.
    record["model_download_authorised"] = False
    record["granite_model_test_authorised"] = False
    record["activation_claim_authorised"] = False
    record["packed_storage_claim_authorised"] = False
    record["performance_claim_authorised"] = False
    record["quality_claim_authorised"] = False
    return record
