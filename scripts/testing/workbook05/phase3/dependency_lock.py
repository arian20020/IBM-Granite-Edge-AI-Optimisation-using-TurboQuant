"""Validate dependency locks and build fail-closed preflight evidence.

The two reviewed VCS packages are deliberately excluded from the ordinary wheel
lock. They are bound separately to full Git commits and complete source-tree
SHA-256 manifests. Every installed ordinary distribution must match both the
generated lock and the actual artifact recorded by pip's installation report.
"""

from __future__ import annotations

import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
)
from scripts.testing.workbook05.phase3.dependency_preflight import (
    DependencyCheck,
    DependencyPackage,
    SourceTreeEvidence,
    collect_dependency_preflight_record,
    is_reviewed_optimum_intel_version,
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


def _canonical_versions(
    values: Mapping[str, str] | None,
) -> dict[str, str]:
    """Canonicalise a required-version map and reject ambiguous duplicates."""

    if values is None:
        return {}
    result: dict[str, str] = {}
    for raw_name, raw_version in values.items():
        if not isinstance(raw_name, str) or not raw_name.strip():
            raise ValueError("A required distribution name is empty.")
        if not isinstance(raw_version, str) or not raw_version.strip():
            raise ValueError(f"A required version is empty for {raw_name!r}.")
        name = _normalise_name(raw_name.strip())
        version = raw_version.strip()
        if name in result:
            raise ValueError(f"Duplicate required distribution: {name}")
        result[name] = version
    return result


def _canonical_names(values: frozenset[str]) -> frozenset[str]:
    """Canonicalise one distribution-name allow/deny set."""

    result: set[str] = set()
    for raw_name in values:
        if not isinstance(raw_name, str) or not raw_name.strip():
            raise ValueError("A forbidden distribution name is empty.")
        result.add(_normalise_name(raw_name.strip()))
    return frozenset(result)


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


def _parse_exact_lock(text: str) -> tuple[LockedDistribution, ...]:
    """Parse exact hashed records without applying one lock's package policy."""

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
                "a normal-distribution lock."
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

    return tuple(
        observed[name]
        for name in sorted(observed, key=lambda item: (item.casefold(), item))
    )


def parse_hash_locked_requirements(
    text: str,
    *,
    required_direct_versions: Mapping[str, str] | None = None,
    forbidden_names: frozenset[str] = frozenset(),
) -> tuple[LockedDistribution, ...]:
    """Parse an exact hash lock under an explicit package-name policy."""

    packages = _parse_exact_lock(text)
    observed = {package.name: package for package in packages}
    required = _canonical_versions(required_direct_versions)
    forbidden = _canonical_names(forbidden_names)

    present_forbidden = sorted(set(observed).intersection(forbidden))
    if present_forbidden:
        raise ValueError(
            "The lock contains forbidden normal distributions: "
            + ", ".join(present_forbidden)
        )

    for name, expected_version in required.items():
        package = observed.get(name)
        if package is None:
            raise ValueError(f"The lock is missing required distribution: {name}")
        if package.version != expected_version:
            raise ValueError(
                f"{name} must remain at {expected_version}, found {package.version}."
            )
    return packages


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


def parse_install_report_against_lock(
    report: Mapping[str, Any],
    lock: Sequence[LockedDistribution],
    *,
    forbidden_names: frozenset[str] = frozenset(),
) -> tuple[DependencyPackage, ...]:
    """Bind every installed distribution to the exact lock package set."""

    rows = report.get("install")
    if not isinstance(rows, list) or not rows:
        raise ValueError("The pip install report contains no installed packages.")

    forbidden = _canonical_names(forbidden_names)
    locked = {package.name: package for package in lock}
    if len(locked) != len(tuple(lock)):
        raise ValueError("The lock contains duplicate canonical package names.")

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
        if name in forbidden:
            raise ValueError(
                f"VCS package or other forbidden normal distribution: {name}"
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
            direct=(item.get("requested") is True or item.get("is_direct") is True),
        )

    if set(observed) != set(locked):
        missing = sorted(set(locked).difference(observed))
        unexpected = sorted(set(observed).difference(locked))
        detail = []
        if missing:
            detail.append("missing=" + ",".join(missing))
        if unexpected:
            detail.append("unexpected=" + ",".join(unexpected))
        raise ValueError(
            "The install-report and lock package sets differ"
            + (": " + "; ".join(detail) if detail else ".")
        )

    return tuple(
        observed[name]
        for name in sorted(observed, key=lambda item: (item.casefold(), item))
    )


def parse_normal_install_report(
    report: Mapping[str, Any],
    lock: Sequence[LockedDistribution],
) -> tuple[DependencyPackage, ...]:
    """Compatibility wrapper for the reviewed ordinary conversion lock."""

    packages = parse_install_report_against_lock(
        report,
        lock,
        forbidden_names=VCS_PACKAGE_NAMES,
    )
    observed = {package.name: package for package in packages}
    for name, expected_version in DIRECT_NORMAL_VERSIONS.items():
        package = observed.get(name)
        if package is None:
            raise ValueError(
                f"The install report omitted reviewed direct package: {name}"
            )
        if package.version != expected_version:
            raise ValueError(
                f"Installed {name} must remain at {expected_version}."
            )

    # Historical fixture reports predate pip's requested/is_direct fields.
    # The reviewed ordinary direct set remains authoritative for this wrapper.
    return tuple(
        DependencyPackage(
            name=package.name,
            version=package.version,
            source_identity=package.source_identity,
            direct=package.name in DIRECT_NORMAL_VERSIONS,
        )
        for package in packages
    )


def _parse_bootstrap_install_report(
    report: Mapping[str, Any],
    lock: Sequence[LockedDistribution],
) -> tuple[DependencyPackage, ...]:
    """Validate the isolated bootstrap environment against its own lock."""

    packages = parse_install_report_against_lock(report, lock)
    by_name = {package.name: package for package in packages}
    pip_tools = by_name.get("pip-tools")
    if pip_tools is None or pip_tools.version != "7.5.0":
        raise ValueError("The bootstrap report must contain pip-tools==7.5.0.")
    return packages


def _source_tree(value: Mapping[str, Any]) -> SourceTreeEvidence:
    """Convert one raw source-tree row without trusting its identity."""

    return SourceTreeEvidence(
        name=str(value.get("name", "")),
        repository=str(value.get("repository", "")),
        origin=str(value.get("origin", "")),
        commit=str(value.get("commit", "")),
        clean=value.get("clean") is True,
        aggregate_sha256=str(value.get("aggregate_sha256", "")),
    )


def _dependency_check(value: Mapping[str, Any]) -> DependencyCheck:
    """Convert one raw check row while preserving interruption semantics."""

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


def _required_text(observation: Mapping[str, Any], name: str) -> str:
    """Read one mandatory text field without coercing a missing value."""

    value = observation.get(name)
    if not isinstance(value, str):
        raise ValueError(f"{name} must be supplied as UTF-8 text.")
    return value


def _is_fixture_report(report: Mapping[str, Any]) -> bool:
    """Identify only the existing repository-controlled synthetic fixture."""

    return report.get("fixture_mode") is True


def build_dependency_preflight_record(
    observation: Mapping[str, Any],
) -> dict[str, Any]:
    """Build one closed C1 preflight decision from observed command evidence."""

    lock_text = _required_text(observation, "lock_text")
    supplied_lock_sha = _required_text(observation, "lock_sha256")
    lock = parse_hash_locked_requirements(
        lock_text,
        required_direct_versions=DIRECT_NORMAL_VERSIONS,
        forbidden_names=VCS_PACKAGE_NAMES,
    )

    report = observation.get("normal_install_report")
    if not isinstance(report, Mapping):
        raise ValueError("normal_install_report must be a JSON object.")
    normal_packages = parse_normal_install_report(report, lock)
    fixture_mode = _is_fixture_report(report)

    # Live evidence must bind the hash-locked bootstrap and its actual pip
    # installation report. The historical offline fixture remains intentionally
    # non-authorising and may omit this newly introduced live-only relationship.
    bootstrap_field_names = (
        "bootstrap_lock_path",
        "bootstrap_lock_text",
        "bootstrap_lock_sha256",
        "bootstrap_install_report_path",
        "bootstrap_install_report_text",
        "bootstrap_install_report_sha256",
    )
    bootstrap_present = any(
        observation.get(name) is not None for name in bootstrap_field_names
    )
    if bootstrap_present and not all(
        observation.get(name) is not None for name in bootstrap_field_names
    ):
        raise ValueError("Bootstrap evidence must be supplied as one complete set.")
    if not bootstrap_present and not fixture_mode:
        raise ValueError("Live dependency evidence requires the bootstrap lock and report.")

    bootstrap_lock_text: str | None = None
    bootstrap_report_text: str | None = None
    supplied_bootstrap_lock_sha: str | None = None
    supplied_bootstrap_report_sha: str | None = None
    bootstrap_packages: tuple[DependencyPackage, ...] = ()
    bootstrap_lock_matches = True
    bootstrap_report_matches = True
    if bootstrap_present:
        bootstrap_lock_text = _required_text(observation, "bootstrap_lock_text")
        bootstrap_report_text = _required_text(
            observation,
            "bootstrap_install_report_text",
        )
        supplied_bootstrap_lock_sha = _required_text(
            observation,
            "bootstrap_lock_sha256",
        )
        supplied_bootstrap_report_sha = _required_text(
            observation,
            "bootstrap_install_report_sha256",
        )

        bootstrap_lock = parse_hash_locked_requirements(
            bootstrap_lock_text,
            required_direct_versions={"pip-tools": "7.5.0"},
            forbidden_names=VCS_PACKAGE_NAMES,
        )
        try:
            raw_bootstrap_report = json.loads(bootstrap_report_text)
        except json.JSONDecodeError as error:
            raise ValueError(
                "bootstrap_install_report_text must contain one JSON object."
            ) from error
        if not isinstance(raw_bootstrap_report, Mapping):
            raise ValueError(
                "bootstrap_install_report_text must contain one JSON object."
            )
        bootstrap_packages = _parse_bootstrap_install_report(
            raw_bootstrap_report,
            bootstrap_lock,
        )
        bootstrap_lock_matches = supplied_bootstrap_lock_sha == hashlib.sha256(
            bootstrap_lock_text.encode("utf-8")
        ).hexdigest()
        bootstrap_report_matches = supplied_bootstrap_report_sha == hashlib.sha256(
            bootstrap_report_text.encode("utf-8")
        ).hexdigest()

    raw_vcs_packages = observation.get("vcs_packages")
    if not isinstance(raw_vcs_packages, list):
        raise ValueError("vcs_packages must be an array.")
    vcs_packages: list[DependencyPackage] = []
    expected_vcs = {
        "optimum-intel": OPTIMUM_INTEL_COMMIT,
        "optimum": OPTIMUM_COMMIT,
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
        expected_commit = expected_vcs.get(name)
        if expected_commit is None or commit != expected_commit:
            raise ValueError(f"VCS package identity drifted: {name}")
        if name == "optimum-intel":
            # The old fixture is Blocked synthetic evidence. Keep its producer
            # unchanged during Task 1, but never carry its disproven 2.3 label
            # into the calculated decision.
            if fixture_mode and version == "2.3.0.dev0":
                version = "2.2.0.dev0"
            if not is_reviewed_optimum_intel_version(version):
                raise ValueError(f"VCS package identity drifted: {name}")
        elif version != "2.3.0":
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

    collector_arguments: dict[str, Any] = {
        "generated_at_utc": str(observation.get("generated_at_utc", "")),
        "workspace_root": Path(str(observation.get("workspace_root", ""))),
        "workspace_is_normal_local_directory": (
            observation.get("workspace_is_normal_local_directory") is True
        ),
        "workspace_is_fresh": observation.get("workspace_is_fresh") is True,
        "python_version": str(observation.get("python_version", "")),
        "python_executable_path": Path(
            str(observation.get("python_executable_path", ""))
        ),
        "python_executable_sha256": str(
            observation.get("python_executable_sha256", "")
        ),
        "pip_version": str(observation.get("pip_version", "")),
        "pip_executable_path": Path(
            str(observation.get("pip_executable_path", ""))
        ),
        "pip_executable_sha256": str(
            observation.get("pip_executable_sha256", "")
        ),
        "source_trees": tuple(_source_tree(item) for item in raw_source_trees),
        "direct_requirements": tuple(
            str(value) for value in observation.get("direct_requirements", [])
        ),
        "lock_path": str(observation.get("lock_path", "")),
        "lock_sha256": supplied_lock_sha,
        "lock_generator": str(observation.get("lock_generator", "")),
        "normal_distribution_count": len(normal_packages),
        "all_normal_artifacts_hashed": lock_matches,
        "vcs_sources_bound_separately": True,
        "packages": tuple(vcs_packages) + normal_packages,
        "checks": tuple(_dependency_check(item) for item in raw_checks),
        "import_modules": tuple(
            str(value) for value in observation.get("import_modules", [])
        ),
        "cli_help_exit_code": int(observation.get("cli_help_exit_code", -1)),
        "no_model_compatibility_exit_code": int(
            observation.get("no_model_compatibility_exit_code", -1)
        ),
    }
    if bootstrap_present:
        assert supplied_bootstrap_lock_sha is not None
        assert supplied_bootstrap_report_sha is not None
        collector_arguments.update(
            {
                "bootstrap_lock_path": str(
                    observation.get("bootstrap_lock_path", "")
                ),
                "bootstrap_lock_sha256": supplied_bootstrap_lock_sha,
                "bootstrap_install_report_path": str(
                    observation.get("bootstrap_install_report_path", "")
                ),
                "bootstrap_install_report_sha256": supplied_bootstrap_report_sha,
                "bootstrap_normal_distribution_count": len(bootstrap_packages),
                "bootstrap_all_normal_artifacts_hashed": (
                    bootstrap_lock_matches and bootstrap_report_matches
                ),
            }
        )

    record = collect_dependency_preflight_record(**collector_arguments)

    # Make each causal digest mismatch explicit instead of relying only on the
    # broader all-artifacts-hashed reason from the record collector.
    mismatch_messages: list[str] = []
    if not lock_matches:
        mismatch_messages.append(
            "Dependency lock SHA-256 mismatch: the supplied identity does not "
            "match the complete lock text."
        )
    if bootstrap_present and not bootstrap_lock_matches:
        mismatch_messages.append(
            "Bootstrap lock SHA-256 mismatch: the supplied identity does not "
            "match the complete bootstrap lock text."
        )
    if bootstrap_present and not bootstrap_report_matches:
        mismatch_messages.append(
            "Bootstrap install-report SHA-256 mismatch: the supplied identity "
            "does not match the complete report text."
        )
    if mismatch_messages:
        record["status"] = "IntegrityFailure"
        reasons = list(record.get("reasons", []))
        for message in mismatch_messages:
            if message not in reasons:
                reasons.append(message)
        record["reasons"] = reasons

    # This package only qualifies dependencies. It never authorises acquisition
    # or any later scientific claim, even when every check passes.
    for key in (
        "model_download_authorised",
        "granite_model_test_authorised",
        "activation_claim_authorised",
        "packed_storage_claim_authorised",
        "performance_claim_authorised",
        "quality_claim_authorised",
    ):
        record[key] = False
    return record
