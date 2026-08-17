"""Build the live Workbook 05 dependency-preflight decision from raw evidence.

This module is the live decision boundary introduced after the bootstrap was
upgraded to pip-tools 7.6.0 and pip 26.1.2. The older collector remains the
closed schema/precedence engine for the previously approved record shape; this
module validates the new generator itself, normalises the full seven-item direct
candidate, and then records the truthful live generator in the returned decision.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any, Mapping

from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_DIRECT_REQUIREMENTS,
)
from scripts.testing.workbook05.phase3.dependency_lock import (
    DIRECT_NORMAL_VERSIONS,
    VCS_PACKAGE_NAMES,
    parse_hash_locked_requirements,
    parse_install_report_against_lock,
    parse_normal_install_report,
)
from scripts.testing.workbook05.phase3.dependency_preflight import (
    DependencyCheck,
    DependencyPackage,
    SourceTreeEvidence,
    collect_dependency_preflight_record,
    is_reviewed_optimum_intel_version,
)


BOOTSTRAP_DIRECT_VERSIONS: dict[str, str] = {
    "pip-tools": "7.6.0",
    "pip": "26.1.2",
}
LIVE_LOCK_GENERATOR = "pip-tools==7.6.0"
_LEGACY_COLLECTOR_GENERATOR = "pip-tools==7.5.0"
_NORMAL_ONLY_DIRECT_REQUIREMENTS = tuple(
    f"{name}=={version}" for name, version in DIRECT_NORMAL_VERSIONS.items()
)

CLAIM_KEYS: tuple[str, ...] = (
    "model_download_authorised",
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)


def _required_text(observation: Mapping[str, Any], name: str) -> str:
    value = observation.get(name)
    if not isinstance(value, str):
        raise ValueError(f"{name} must be supplied as UTF-8 text.")
    return value


def _required_object(observation: Mapping[str, Any], name: str) -> Mapping[str, Any]:
    value = observation.get(name)
    if not isinstance(value, Mapping):
        raise ValueError(f"{name} must be one JSON object.")
    return value


def _required_array(observation: Mapping[str, Any], name: str) -> list[Any]:
    value = observation.get(name)
    if not isinstance(value, list):
        raise ValueError(f"{name} must be one JSON array.")
    return value


def _source_tree(value: Mapping[str, Any]) -> SourceTreeEvidence:
    return SourceTreeEvidence(
        name=str(value.get("name", "")),
        repository=str(value.get("repository", "")),
        origin=str(value.get("origin", "")),
        commit=str(value.get("commit", "")),
        clean=value.get("clean") is True,
        aggregate_sha256=str(value.get("aggregate_sha256", "")),
    )


def _check(value: Mapping[str, Any]) -> DependencyCheck:
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


def _vcs_packages(rows: list[Any]) -> tuple[DependencyPackage, ...]:
    expected = {
        "optimum": ("2.3.0", OPTIMUM_COMMIT),
        "optimum-intel": ("2.2.0.dev0", OPTIMUM_INTEL_COMMIT),
    }
    observed: dict[str, DependencyPackage] = {}
    for row in rows:
        if not isinstance(row, Mapping):
            raise ValueError("A VCS package row is not an object.")
        name = str(row.get("name", "")).replace("_", "-").casefold()
        version = str(row.get("version", ""))
        commit = str(row.get("commit", ""))
        if name in observed:
            raise ValueError(f"Duplicate VCS package: {name}")
        expected_row = expected.get(name)
        if expected_row is None or commit != expected_row[1]:
            raise ValueError(f"VCS package identity drifted: {name}")
        if name == "optimum-intel":
            if not is_reviewed_optimum_intel_version(version):
                raise ValueError(f"VCS package version drifted: {name}")
        elif version != expected_row[0]:
            raise ValueError(f"VCS package version drifted: {name}")
        observed[name] = DependencyPackage(
            name=name,
            version=version,
            source_identity=commit,
            direct=True,
        )
    if set(observed) != set(expected):
        raise ValueError("The exact two reviewed VCS packages are required.")
    return tuple(observed[name] for name in sorted(observed))


def _validate_observed_direct_requirements(
    observation: Mapping[str, Any],
) -> None:
    """Admit either the full candidate or the five ordinary direct pins.

    The live PowerShell collector records the two VCS identities separately and
    therefore lists the five index-resolved direct requirements in observation
    data. Older fixtures already retain the full seven-item candidate. Both forms
    are unambiguous; the closed decision always records the full reviewed set.
    """

    raw = observation.get("direct_requirements")
    if not isinstance(raw, list) or not all(isinstance(value, str) for value in raw):
        raise ValueError("direct_requirements must be one string array.")
    observed = tuple(raw)
    if observed not in (
        REVIEWED_DIRECT_REQUIREMENTS,
        _NORMAL_ONLY_DIRECT_REQUIREMENTS,
    ):
        raise ValueError(
            "direct_requirements differ from both reviewed dependency forms."
        )


def build_live_dependency_preflight_record(
    observation: Mapping[str, Any],
) -> dict[str, Any]:
    """Recompute one live dependency decision without trusting supplied summaries."""

    bootstrap_lock_text = _required_text(observation, "bootstrap_lock_text")
    bootstrap_report_text = _required_text(
        observation,
        "bootstrap_install_report_text",
    )
    normal_lock_text = _required_text(observation, "lock_text")
    normal_report = _required_object(observation, "normal_install_report")
    _validate_observed_direct_requirements(observation)

    bootstrap_lock = parse_hash_locked_requirements(
        bootstrap_lock_text,
        required_direct_versions=BOOTSTRAP_DIRECT_VERSIONS,
        forbidden_names=VCS_PACKAGE_NAMES,
    )
    try:
        bootstrap_report = json.loads(bootstrap_report_text)
    except json.JSONDecodeError as error:
        raise ValueError(
            "bootstrap_install_report_text must contain one JSON object."
        ) from error
    if not isinstance(bootstrap_report, Mapping):
        raise ValueError(
            "bootstrap_install_report_text must contain one JSON object."
        )
    bootstrap_packages = parse_install_report_against_lock(
        bootstrap_report,
        bootstrap_lock,
        forbidden_names=VCS_PACKAGE_NAMES,
    )

    normal_lock = parse_hash_locked_requirements(
        normal_lock_text,
        required_direct_versions=DIRECT_NORMAL_VERSIONS,
        forbidden_names=VCS_PACKAGE_NAMES,
    )
    normal_packages = parse_normal_install_report(normal_report, normal_lock)

    source_rows = _required_array(observation, "source_trees")
    check_rows = _required_array(observation, "checks")
    vcs_rows = _required_array(observation, "vcs_packages")

    supplied_bootstrap_lock_sha = _required_text(
        observation,
        "bootstrap_lock_sha256",
    )
    supplied_bootstrap_report_sha = _required_text(
        observation,
        "bootstrap_install_report_sha256",
    )
    supplied_normal_lock_sha = _required_text(observation, "lock_sha256")

    actual_bootstrap_lock_sha = hashlib.sha256(
        bootstrap_lock_text.encode("utf-8")
    ).hexdigest()
    actual_bootstrap_report_sha = hashlib.sha256(
        bootstrap_report_text.encode("utf-8")
    ).hexdigest()
    actual_normal_lock_sha = hashlib.sha256(
        normal_lock_text.encode("utf-8")
    ).hexdigest()

    lock_generator = str(observation.get("lock_generator", ""))
    if lock_generator != LIVE_LOCK_GENERATOR:
        raise ValueError(
            f"lock_generator must remain {LIVE_LOCK_GENERATOR}, found {lock_generator}."
        )

    packages = _vcs_packages(vcs_rows) + tuple(normal_packages)
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
        source_trees=tuple(_source_tree(row) for row in source_rows),
        # The closed evidence schema has always represented the complete direct
        # candidate, including separately bound VCS packages.
        direct_requirements=REVIEWED_DIRECT_REQUIREMENTS,
        bootstrap_lock_path=str(observation.get("bootstrap_lock_path", "")),
        bootstrap_lock_sha256=supplied_bootstrap_lock_sha,
        bootstrap_install_report_path=str(
            observation.get("bootstrap_install_report_path", "")
        ),
        bootstrap_install_report_sha256=supplied_bootstrap_report_sha,
        bootstrap_normal_distribution_count=len(bootstrap_packages),
        bootstrap_all_normal_artifacts_hashed=(
            supplied_bootstrap_lock_sha == actual_bootstrap_lock_sha
            and supplied_bootstrap_report_sha == actual_bootstrap_report_sha
        ),
        lock_path=str(observation.get("lock_path", "")),
        lock_sha256=supplied_normal_lock_sha,
        # The underlying collector owns the approved schema and precedence but
        # predates the generator upgrade. The new boundary already validated
        # 7.6.0 above, so feed the legacy sentinel and replace only the reported
        # generator after the collector has classified every other observation.
        lock_generator=_LEGACY_COLLECTOR_GENERATOR,
        normal_distribution_count=len(normal_packages),
        all_normal_artifacts_hashed=(
            supplied_normal_lock_sha == actual_normal_lock_sha
        ),
        vcs_sources_bound_separately=True,
        packages=packages,
        checks=tuple(_check(row) for row in check_rows),
        import_modules=tuple(
            str(value) for value in observation.get("import_modules", [])
        ),
        cli_help_exit_code=int(observation.get("cli_help_exit_code", -1)),
        no_model_compatibility_exit_code=int(
            observation.get("no_model_compatibility_exit_code", -1)
        ),
    )
    record["lock"]["generator"] = lock_generator

    mismatch_messages: list[str] = []
    if supplied_bootstrap_lock_sha != actual_bootstrap_lock_sha:
        mismatch_messages.append("Bootstrap lock SHA-256 mismatch.")
    if supplied_bootstrap_report_sha != actual_bootstrap_report_sha:
        mismatch_messages.append("Bootstrap install-report SHA-256 mismatch.")
    if supplied_normal_lock_sha != actual_normal_lock_sha:
        mismatch_messages.append("Dependency lock SHA-256 mismatch.")
    if mismatch_messages:
        record["status"] = "IntegrityFailure"
        record["reasons"] = list(record.get("reasons", [])) + mismatch_messages

    for key in CLAIM_KEYS:
        record[key] = False
    return record
