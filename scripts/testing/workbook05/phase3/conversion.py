"""Build immutable OpenVINO conversion requests and evidence records."""

from __future__ import annotations

import re
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any, Iterable, Sequence


CAMPAIGN_ID = "GTQ-WB05-MF-v1"
ROUTE_ID = "route-a-merged-openvino"
FORMAL_GRANITE_REPOSITORY = "ibm-granite/granite-4.1-3b"

OPTIMUM_INTEL_COMMIT = "a3b6012a4c02f4147260da4d4601bb6a3c0d2bb0"
OPTIMUM_COMMIT = "982e495540364f95da1e4b6f62d2d4e5907d08fd"

# This is the only direct candidate that the later clean preflight may resolve.
# A different order, source, version, or moving VCS reference is a new candidate.
REVIEWED_DIRECT_REQUIREMENTS: tuple[str, ...] = (
    (
        "optimum-intel @ "
        "git+https://github.com/huggingface/optimum-intel.git@"
        + OPTIMUM_INTEL_COMMIT
    ),
    (
        "optimum @ "
        "git+https://github.com/huggingface/optimum.git@"
        + OPTIMUM_COMMIT
    ),
    "transformers==5.5.0",
    "huggingface-hub==1.21.0",
    "nncf==3.2.0",
    "openvino==2026.2.1",
    "openvino-tokenizers==2026.2.1.0",
)

# The preflight must extract and compare the complete install requirements from
# the exact pinned Optimum Intel source before it is allowed to install anything.
REVIEWED_OPTIMUM_INTEL_CONSTRAINTS: tuple[str, ...] = (
    "torch>=2.1",
    "safetensors<0.8.0",
    "optimum~=2.3.0",
    "transformers>=4.51,<5.6",
    "setuptools",
    "huggingface-hub>=0.23.2,<1.22",
    "nncf>=2.19.0",
    "openvino>=2026.0",
    "openvino-tokenizers>=2026.0",
    "requests>=2.33,<3.0",
)

# The exact pinned Optimum source is inspected independently from setup.py data.
# A changed, missing, or additional runtime requirement is a new candidate.
REVIEWED_OPTIMUM_CONSTRAINTS: tuple[str, ...] = (
    "transformers>=4.29",
    "torch>=1.11",
    "packaging",
    "numpy",
    "huggingface_hub>=0.8.0",
)

# These ordinary distributions are resolved together for the final clean
# environment. The two VCS packages remain separate verified local installs.
REVIEWED_NORMAL_REQUIREMENT_INPUT: tuple[str, ...] = (
    "transformers==5.5.0",
    "huggingface-hub==1.21.0",
    "nncf==3.2.0",
    "openvino==2026.2.1",
    "openvino-tokenizers==2026.2.1.0",
    "torch>=2.1",
    "safetensors<0.8.0",
    "setuptools",
    "requests>=2.33,<3.0",
    "packaging",
    "numpy",
    "wheel",
)

_FULL_COMMIT_PATTERN = re.compile(r"^[0-9a-f]{40}$")
_SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")
_CONVERSION_ID_PATTERN = re.compile(r"^CONV-WB05-[A-Z0-9-]+$")
_ASSET_ID_PATTERN = re.compile(r"^MODEL-WB05-[A-Z0-9-]+$")
_OPTIMUM_INTEL_VERSION_PATTERN = re.compile(
    r"^2\.2\.0\.dev0(?:\+[0-9a-f]{7,40})?$"
)

_EXPECTED_PACKAGE_VERSIONS: dict[str, str] = {
    "optimum": "2.3.0",
    "transformers": "5.5.0",
    "huggingface-hub": "1.21.0",
    "nncf": "3.2.0",
    "openvino": "2026.2.1",
    "openvino-tokenizers": "2026.2.1.0",
}


@dataclass(frozen=True, slots=True)
class ConversionRequest:
    """The one approved data-free Granite 4.1 3B conversion candidate."""

    optimum_cli: Path
    source_directory: Path
    output_directory: Path
    weight_format: str = "int4"
    symmetric: bool = False
    group_size: int = 128
    ratio: float = 1.0
    task: str = "text-generation-with-past"
    data_free: bool = True
    trust_remote_code: bool = False


@dataclass(frozen=True, slots=True)
class ConversionPackage:
    """One observed package identity from the accepted dependency preflight."""

    name: str
    version: str
    source_identity: str


@dataclass(frozen=True, slots=True)
class ConversionFile:
    """One observed regular output file recorded as data, never uploaded here."""

    relative_path: str
    size_bytes: int
    sha256: str


def validate_dependency_candidate(requirements: Sequence[str]) -> None:
    """Require the exact reviewed direct requirement set and order."""

    if tuple(requirements) != REVIEWED_DIRECT_REQUIREMENTS:
        raise ValueError(
            "The conversion dependency candidate does not match the reviewed "
            "direct set exactly; automatic version drift is forbidden."
        )


def validate_optimum_intel_constraints(constraints: Sequence[str]) -> None:
    """Require the exact install requirements declared by the pinned source."""

    if tuple(constraints) != REVIEWED_OPTIMUM_INTEL_CONSTRAINTS:
        raise ValueError(
            "The pinned optimum-intel declared constraints do not match the "
            "reviewed source contract."
        )


def _windows_path(
    path: Path,
    *,
    label: str,
    approved_root: PureWindowsPath,
    required_suffix: str | None = None,
) -> PureWindowsPath:
    """Return a contained Windows path without touching the host filesystem."""

    text = str(path)
    lowered = text.casefold()
    if text.startswith("\\\\") or lowered.startswith(("\\\\?\\", "\\\\.\\")):
        raise ValueError(f"{label} must not be a UNC or device path: {text}")

    candidate = PureWindowsPath(text)
    if not candidate.is_absolute() or not candidate.drive:
        raise ValueError(f"{label} must be an absolute Windows path: {text}")
    if candidate.drive.casefold() != approved_root.drive.casefold():
        raise ValueError(f"{label} must remain on {approved_root.drive}: {text}")

    candidate_parts = tuple(part.casefold() for part in candidate.parts)
    root_parts = tuple(part.casefold() for part in approved_root.parts)
    if (
        len(candidate_parts) <= len(root_parts)
        or candidate_parts[: len(root_parts)] != root_parts
    ):
        raise ValueError(f"{label} must be a child of {approved_root}: {text}")
    if any(part in {"", ".", ".."} for part in candidate.parts):
        raise ValueError(f"{label} contains an unsafe path segment: {text}")
    if required_suffix is not None and not candidate.name.casefold().endswith(
        required_suffix.casefold()
    ):
        raise ValueError(f"{label} must end with {required_suffix}: {text}")
    return candidate


def _is_same_or_child(
    candidate: PureWindowsPath,
    root: PureWindowsPath,
) -> bool:
    """Compare Windows path components case-insensitively."""

    candidate_parts = tuple(part.casefold() for part in candidate.parts)
    root_parts = tuple(part.casefold() for part in root.parts)
    return (
        len(candidate_parts) >= len(root_parts)
        and candidate_parts[: len(root_parts)] == root_parts
    )


def _validate_request(request: ConversionRequest) -> None:
    """Reject every command or path drift before an argument list exists."""

    if request.weight_format != "int4":
        raise ValueError("The reviewed conversion weight format is exactly int4.")
    if request.symmetric is not False:
        raise ValueError("The reviewed conversion is asymmetric; --sym is forbidden.")
    if isinstance(request.group_size, bool) or request.group_size != 128:
        raise ValueError("The reviewed conversion group size is exactly 128.")
    if isinstance(request.ratio, bool) or request.ratio != 1.0:
        raise ValueError("The reviewed conversion ratio is exactly 1.0.")
    if request.task != "text-generation-with-past":
        raise ValueError(
            "The reviewed conversion task is text-generation-with-past."
        )
    if request.data_free is not True:
        raise ValueError("The reviewed conversion must remain data-free.")
    if request.trust_remote_code is not False:
        raise ValueError(
            "Remote model code must remain disabled for the primary conversion."
        )

    executable = _windows_path(
        request.optimum_cli,
        label="Optimum CLI",
        approved_root=PureWindowsPath(r"C:\w5c"),
        required_suffix=".exe",
    )
    source = _windows_path(
        request.source_directory,
        label="Immutable source directory",
        approved_root=PureWindowsPath(r"C:\w5m\sources"),
    )
    output = _windows_path(
        request.output_directory,
        label="Conversion output directory",
        approved_root=PureWindowsPath(r"C:\w5m\converted"),
    )
    if executable.name.casefold() != "optimum-cli.exe":
        raise ValueError(
            "The conversion executable must be the reviewed optimum-cli.exe."
        )
    if _is_same_or_child(output, source) or _is_same_or_child(source, output):
        raise ValueError(
            "Source and output directories must remain separate non-overlapping trees."
        )


def build_optimum_argument_list(request: ConversionRequest) -> list[str]:
    """Return the structured arguments for the approved data-free export."""

    _validate_request(request)
    return [
        "export",
        "openvino",
        "--model",
        str(request.source_directory),
        "--task",
        request.task,
        "--weight-format",
        request.weight_format,
        "--group-size",
        str(request.group_size),
        "--ratio",
        format(request.ratio, ".1f"),
        str(request.output_directory),
    ]


def _portable_relative_path(value: str, *, label: str) -> str:
    """Return one canonical forward-slash evidence path."""

    if not isinstance(value, str) or not value:
        raise ValueError(f"{label} must be a non-empty string.")
    if value != value.strip() or "\\" in value or "\x00" in value:
        raise ValueError(
            f"{label} must be a canonical portable relative path: {value!r}"
        )
    if any(ord(character) < 32 for character in value):
        raise ValueError(f"{label} contains a control character: {value!r}")
    if value.startswith("/") or value.endswith("/") or "//" in value:
        raise ValueError(
            f"{label} must be a canonical portable relative path: {value!r}"
        )

    raw_parts = value.split("/")
    if any(part in {"", ".", ".."} for part in raw_parts):
        raise ValueError(f"{label} contains an unsafe segment: {value!r}")

    posix = PurePosixPath(value)
    windows = PureWindowsPath(value)
    if posix.is_absolute() or windows.is_absolute() or windows.drive:
        raise ValueError(f"{label} must not be absolute: {value!r}")
    if posix.as_posix() != value:
        raise ValueError(f"{label} is not canonical: {value!r}")
    return value


def _sha256(value: str, *, label: str) -> str:
    """Require a lowercase SHA-256 identity."""

    if not isinstance(value, str) or not _SHA256_PATTERN.fullmatch(value):
        raise ValueError(f"{label} must be a lowercase SHA-256 digest.")
    return value


def _timestamp(value: str, *, label: str) -> datetime:
    """Parse one explicit RFC 3339 UTC timestamp."""

    if not isinstance(value, str) or not value.endswith("Z"):
        raise ValueError(f"{label} must be an RFC 3339 UTC timestamp.")
    try:
        return datetime.fromisoformat(value[:-1] + "+00:00")
    except ValueError as error:
        raise ValueError(
            f"{label} must be an RFC 3339 UTC timestamp: {value!r}"
        ) from error


def _package_records(
    packages: Sequence[ConversionPackage],
) -> list[dict[str, str]]:
    """Validate the exact package catalogue while retaining observed hashes."""

    if not packages:
        raise ValueError("The conversion package catalogue must not be empty.")

    observed: dict[str, ConversionPackage] = {}
    for package in packages:
        if not all(
            isinstance(value, str) and value
            for value in (
                package.name,
                package.version,
                package.source_identity,
            )
        ):
            raise ValueError("Every conversion package field must be non-empty.")
        key = package.name.casefold()
        if key in observed:
            raise ValueError(
                f"The package catalogue contains a duplicate package: {package.name}"
            )
        observed[key] = package

    expected_names = {
        "optimum-intel",
        "optimum",
        "transformers",
        "huggingface-hub",
        "nncf",
        "openvino",
        "openvino-tokenizers",
    }
    if set(observed) != expected_names:
        missing = sorted(expected_names - set(observed))
        unexpected = sorted(set(observed) - expected_names)
        raise ValueError(
            "The package catalogue differs from the reviewed candidate. "
            f"Missing={missing}; unexpected={unexpected}."
        )

    optimum_intel = observed["optimum-intel"]
    if not _OPTIMUM_INTEL_VERSION_PATTERN.fullmatch(
        optimum_intel.version
    ) or optimum_intel.source_identity != OPTIMUM_INTEL_COMMIT:
        raise ValueError(
            "optimum-intel must retain the reviewed version line and exact commit."
        )

    optimum = observed["optimum"]
    if (
        optimum.version != _EXPECTED_PACKAGE_VERSIONS["optimum"]
        or optimum.source_identity != OPTIMUM_COMMIT
    ):
        raise ValueError(
            "optimum must retain version 2.3.0 and the exact reviewed commit."
        )

    for name, expected_version in _EXPECTED_PACKAGE_VERSIONS.items():
        if name == "optimum":
            continue
        if observed[name].version != expected_version:
            raise ValueError(
                f"{name} version drifted from {expected_version}: "
                f"{observed[name].version}"
            )
        if not re.fullmatch(
            r"sha256:[0-9a-f]{64}",
            observed[name].source_identity,
        ):
            raise ValueError(
                f"{name} must retain its reviewed wheel or source-distribution "
                "digest as source_identity."
            )

    return [
        {
            "name": package.name,
            "version": package.version,
            "source_identity": package.source_identity,
        }
        for package in sorted(
            packages,
            key=lambda item: (item.name.casefold(), item.name),
        )
    ]


def _output_records(
    files: Sequence[ConversionFile],
) -> tuple[list[dict[str, Any]], set[str]]:
    """Validate and sort one observed output-file catalogue."""

    records: list[dict[str, Any]] = []
    identities: set[str] = set()
    for item in files:
        relative_path = _portable_relative_path(
            item.relative_path,
            label="Conversion output path",
        )
        key = relative_path.casefold()
        if key in identities:
            raise ValueError(
                "The output catalogue contains a duplicate output path: "
                f"{relative_path}"
            )
        identities.add(key)
        if (
            isinstance(item.size_bytes, bool)
            or not isinstance(item.size_bytes, int)
            or item.size_bytes < 0
        ):
            raise ValueError(
                f"Output size must be a non-negative integer: {relative_path}"
            )
        records.append(
            {
                "relative_path": relative_path,
                "size_bytes": item.size_bytes,
                "sha256": _sha256(
                    item.sha256,
                    label=f"Output digest for {relative_path}",
                ),
            }
        )

    records.sort(
        key=lambda item: (
            str(item["relative_path"]).casefold(),
            str(item["relative_path"]),
        )
    )
    return records, identities


def _unique_portable_paths(
    values: Iterable[str],
    *,
    label: str,
) -> tuple[str, ...]:
    """Validate and sort a duplicate-free portable path sequence."""

    result: list[str] = []
    seen: set[str] = set()
    for value in values:
        canonical = _portable_relative_path(value, label=label)
        key = canonical.casefold()
        if key in seen:
            raise ValueError(f"{label} contains a duplicate path: {canonical}")
        seen.add(key)
        result.append(canonical)
    return tuple(sorted(result, key=lambda value: (value.casefold(), value)))


def collect_conversion_record(
    *,
    request: ConversionRequest,
    conversion_id: str,
    asset_id: str,
    source_repository: str,
    resolved_revision: str,
    aggregate_model_sha256: str,
    aggregate_tokenizer_sha256: str,
    dependency_preflight_status: str,
    dependency_preflight_record_path: str,
    dependency_preflight_record_sha256: str,
    python_version: str,
    executable_sha256: str,
    packages: Sequence[ConversionPackage],
    started_at_utc: str,
    completed_at_utc: str,
    exit_code: int,
    stdout_path: str,
    stderr_path: str,
    output_files: Sequence[ConversionFile],
    aggregate_output_sha256: str,
    required_output_paths: Sequence[str],
    unrecorded_output_paths: Sequence[str],
) -> dict[str, Any]:
    """Build one schema-shaped record without executing or accepting a model."""

    arguments = build_optimum_argument_list(request)
    if dependency_preflight_status != "Passed":
        raise ValueError(
            "The conversion dependency preflight must be Passed before a "
            "conversion record can be constructed."
        )
    if not _CONVERSION_ID_PATTERN.fullmatch(conversion_id):
        raise ValueError("conversion_id does not match the reviewed identifier form.")
    if not _ASSET_ID_PATTERN.fullmatch(asset_id):
        raise ValueError("asset_id does not match the reviewed identifier form.")
    if source_repository != FORMAL_GRANITE_REPOSITORY:
        raise ValueError("The conversion source repository is not approved.")
    if not _FULL_COMMIT_PATTERN.fullmatch(resolved_revision):
        raise ValueError(
            "The conversion source revision must be a full lowercase commit."
        )
    if python_version != "3.12.10":
        raise ValueError("The conversion Python version must remain 3.12.10.")
    if isinstance(exit_code, bool) or not isinstance(exit_code, int):
        raise ValueError("The conversion exit code must be an integer.")

    started = _timestamp(started_at_utc, label="Conversion start")
    completed = _timestamp(completed_at_utc, label="Conversion completion")
    if completed < started:
        raise ValueError("Conversion completion cannot precede its start.")

    portable_preflight = _portable_relative_path(
        dependency_preflight_record_path,
        label="Dependency preflight record path",
    )
    portable_stdout = _portable_relative_path(
        stdout_path,
        label="Conversion stdout path",
    )
    portable_stderr = _portable_relative_path(
        stderr_path,
        label="Conversion stderr path",
    )
    package_records = _package_records(packages)
    file_records, observed_identities = _output_records(output_files)
    required = _unique_portable_paths(
        required_output_paths,
        label="Required conversion output path",
    )
    unrecorded = _unique_portable_paths(
        unrecorded_output_paths,
        label="Unrecorded conversion output path",
    )

    missing = [
        path
        for path in required
        if path.casefold() not in observed_identities
    ]
    if unrecorded:
        status = "IntegrityFailure"
        reasons = [
            "The conversion output tree contains unrecorded output paths: "
            + ", ".join(unrecorded)
        ]
    elif exit_code != 0:
        status = "Failed"
        reasons = [
            f"The conversion process exited with nonzero exit code {exit_code}."
        ]
    elif missing:
        status = "Failed"
        reasons = [
            "The zero-exit conversion is missing required output files: "
            + ", ".join(missing)
        ]
    elif not file_records:
        status = "Failed"
        reasons = [
            "The zero-exit conversion produced no recorded output files."
        ]
    else:
        status = "Candidate"
        reasons = [
            "The conversion exited zero, every required output is recorded, "
            "and no unrecorded output path was observed. Load and inference "
            "qualification remain separate C3/C5 gates."
        ]

    return {
        "schema_version": "1.0",
        "campaign_id": CAMPAIGN_ID,
        "record_type": "model-conversion-record",
        "route_id": ROUTE_ID,
        "conversion_id": conversion_id,
        "asset_id": asset_id,
        "source": {
            "repository": source_repository,
            "resolved_revision": resolved_revision,
            "source_directory": str(request.source_directory),
            "aggregate_model_sha256": _sha256(
                aggregate_model_sha256,
                label="Aggregate model digest",
            ),
            "aggregate_tokenizer_sha256": _sha256(
                aggregate_tokenizer_sha256,
                label="Aggregate tokenizer digest",
            ),
        },
        "dependency_preflight": {
            "status": dependency_preflight_status,
            "record_path": portable_preflight,
            "record_sha256": _sha256(
                dependency_preflight_record_sha256,
                label="Dependency preflight digest",
            ),
        },
        "toolchain": {
            "python_version": python_version,
            "executable_path": str(request.optimum_cli),
            "executable_sha256": _sha256(
                executable_sha256,
                label="Optimum CLI executable digest",
            ),
            "packages": package_records,
        },
        "conversion": {
            "task": request.task,
            "weight_format": request.weight_format,
            "symmetric": request.symmetric,
            "group_size": request.group_size,
            "ratio": request.ratio,
            "data_free": request.data_free,
            "trust_remote_code": request.trust_remote_code,
            "arguments": arguments,
        },
        "output": {
            "directory": str(request.output_directory),
            "files": file_records,
            "aggregate_sha256": _sha256(
                aggregate_output_sha256,
                label="Aggregate conversion output digest",
            ),
        },
        "process": {
            "started_at_utc": started_at_utc,
            "completed_at_utc": completed_at_utc,
            "exit_code": exit_code,
            "stdout_path": portable_stdout,
            "stderr_path": portable_stderr,
        },
        "status": status,
        "reasons": reasons,
        "granite_model_test_authorised": False,
        "activation_claim_authorised": False,
        "packed_storage_claim_authorised": False,
        "performance_claim_authorised": False,
        "quality_claim_authorised": False,
    }
