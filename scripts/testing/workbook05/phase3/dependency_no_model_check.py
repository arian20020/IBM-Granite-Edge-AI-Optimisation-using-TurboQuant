"""Perform the repository-controlled compatibility check without touching a model."""

from __future__ import annotations

import argparse
import json
import os
import re
from importlib import import_module
from importlib.metadata import PackageNotFoundError
from importlib.metadata import version as distribution_version
from pathlib import Path, PureWindowsPath
from typing import Any, Mapping, Sequence

from scripts.testing.workbook05.phase3.conversion import (
    ConversionRequest,
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_NORMAL_REQUIREMENT_INPUT,
    build_optimum_argument_list,
)
from scripts.testing.workbook05.phase3.dependency_preflight import (
    CAMPAIGN_ID,
    IMPORT_MODULES,
    ROUTE_ID,
)
from scripts.testing.workbook05.phase3.dependency_source_contract import (
    validate_reviewed_source_contracts,
)


# Task 4 is deliberately narrower than a conversion smoke test. These values are
# inert command arguments only; the function below never opens either path.
_INERT_SOURCE_DIRECTORY = Path(r"C:\w5m\sources\not-opened-source")
_INERT_OUTPUT_DIRECTORY = Path(r"C:\w5m\converted\not-created-output")

# Exact normal-package versions are derived from the single reviewed requirements
# catalogue instead of maintaining another hand-written production copy.
_EXACT_PIN_PATTERN = re.compile(r"^([A-Za-z0-9_.-]+)==([^\s]+)$")
_OPTIMUM_INTEL_VERSION_PATTERN = re.compile(
    r"^2\.2\.0\.dev0(?:\+([0-9a-f]{7,40}))?$"
)

# The compatibility record cannot authorise any later model or scientific claim.
_SCIENTIFIC_AUTHORISATIONS: dict[str, bool] = {
    "model_download_authorised": False,
    "model_conversion_authorised": False,
    "model_execution_authorised": False,
    "codec_activation_claim_authorised": False,
    "packed_storage_claim_authorised": False,
    "performance_claim_authorised": False,
    "quality_claim_authorised": False,
}


class NoModelCheckError(ValueError):
    """Raised when the installed environment drifts from the reviewed boundary."""


def _canonical_package_name(value: str) -> str:
    """Return the package spelling used by the evidence contract."""

    return re.sub(r"[-_.]+", "-", value).casefold()


def _expected_exact_versions() -> dict[str, str]:
    """Extract the exact normal pins from the reviewed ordinary input."""

    expected: dict[str, str] = {}
    for requirement in REVIEWED_NORMAL_REQUIREMENT_INPUT:
        match = _EXACT_PIN_PATTERN.fullmatch(requirement)
        if match is None:
            continue
        name = _canonical_package_name(match.group(1))
        if name in expected:
            raise NoModelCheckError(
                f"Duplicate exact package pin in the reviewed input: {name}"
            )
        expected[name] = match.group(2)

    required = {
        "transformers",
        "huggingface-hub",
        "nncf",
        "openvino",
        "openvino-tokenizers",
    }
    if set(expected) != required:
        raise NoModelCheckError(
            "The reviewed ordinary input no longer exposes the exact five "
            "conversion package pins."
        )
    return expected


def _is_reviewed_optimum_intel_version(value: str) -> bool:
    """Accept the source version and only a truthful pinned-commit suffix."""

    match = _OPTIMUM_INTEL_VERSION_PATTERN.fullmatch(value)
    if match is None:
        return False
    suffix = match.group(1)
    return suffix is None or OPTIMUM_INTEL_COMMIT.startswith(suffix)


def _pure_windows_child(root: Path, candidate_text: str, *, label: str) -> str:
    """Validate Windows containment lexically without probing the filesystem."""

    root_path = PureWindowsPath(str(root))
    candidate = PureWindowsPath(candidate_text)
    if not root_path.is_absolute() or not root_path.drive:
        raise NoModelCheckError(
            f"The supplied environment root must be an absolute Windows path: {root}"
        )
    if not candidate.is_absolute() or not candidate.drive:
        raise NoModelCheckError(f"{label} is not an absolute Windows path: {candidate_text}")
    if root_path.drive.casefold() != candidate.drive.casefold():
        raise NoModelCheckError(
            f"{label} is outside the supplied environment: {candidate_text}"
        )

    root_parts = tuple(part.casefold() for part in root_path.parts)
    candidate_parts = tuple(part.casefold() for part in candidate.parts)
    if (
        len(candidate_parts) <= len(root_parts)
        or candidate_parts[: len(root_parts)] != root_parts
    ):
        raise NoModelCheckError(
            f"{label} is outside the supplied environment: {candidate_text}"
        )
    return str(candidate)


def _observe_imports(environment_root: Path) -> list[dict[str, str]]:
    """Import only the reviewed modules and bind each file to the final environment."""

    observations: list[dict[str, str]] = []
    for module_name in IMPORT_MODULES:
        module = import_module(module_name)
        module_file = getattr(module, "__file__", None)
        if not isinstance(module_file, str) or not module_file:
            raise NoModelCheckError(
                f"Imported module does not expose a file identity: {module_name}"
            )
        contained_file = _pure_windows_child(
            environment_root,
            module_file,
            label=f"Imported module {module_name}",
        )
        observations.append(
            {
                "module": module_name,
                "file": contained_file,
            }
        )
    return observations


def _observe_packages() -> list[dict[str, str]]:
    """Require every direct installed package to match the reviewed source candidate."""

    expected = _expected_exact_versions()
    expected.update(
        {
            "optimum": "2.3.0",
            "optimum-intel": "2.2.0.dev0",
        }
    )

    observations: list[dict[str, str]] = []
    for package_name in (
        "optimum",
        "optimum-intel",
        "transformers",
        "huggingface-hub",
        "nncf",
        "openvino",
        "openvino-tokenizers",
    ):
        try:
            observed = distribution_version(package_name)
        except PackageNotFoundError as error:
            raise NoModelCheckError(
                f"Required package is not installed: {package_name}"
            ) from error

        if package_name == "optimum-intel":
            valid = _is_reviewed_optimum_intel_version(observed)
        else:
            valid = observed == expected[package_name]
        if not valid:
            raise NoModelCheckError(
                f"Unexpected {package_name} version: {observed}; "
                f"expected {expected[package_name]}."
            )

        source_identity = (
            OPTIMUM_COMMIT
            if package_name == "optimum"
            else OPTIMUM_INTEL_COMMIT
            if package_name == "optimum-intel"
            else "ordinary-hash-locked-distribution"
        )
        observations.append(
            {
                "name": package_name,
                "version": observed,
                "source_identity": source_identity,
            }
        )
    return observations


def _validate_source_report(report: Mapping[str, Any]) -> dict[str, Any]:
    """Require the source inspector to remain data-only and retain both identities."""

    if report.get("source_metadata_execution") is not False:
        raise NoModelCheckError(
            "The reviewed source contracts were not validated through the data-only path."
        )
    contracts = report.get("contracts")
    if not isinstance(contracts, Mapping):
        raise NoModelCheckError("The source-contract report has no contract mapping.")

    expected = {
        "optimum": ("2.3.0", OPTIMUM_COMMIT),
        "optimum-intel": ("2.2.0.dev0", OPTIMUM_INTEL_COMMIT),
    }
    for name, (version, commit) in expected.items():
        contract = contracts.get(name)
        if not isinstance(contract, Mapping):
            raise NoModelCheckError(f"The source-contract report is missing {name}.")
        if contract.get("name") != name:
            raise NoModelCheckError(f"The {name} source contract changed package name.")
        if contract.get("base_version") != version:
            raise NoModelCheckError(f"The {name} source contract changed version.")
        # Live source reports include the commit. Focused fixtures may inject it
        # explicitly; a different or missing identity fails closed.
        if contract.get("source_commit") != commit:
            raise NoModelCheckError(f"The {name} source contract changed commit.")
    return dict(report)


def run_no_model_check(
    environment_root: Path,
    optimum_root: Path,
    optimum_intel_root: Path,
) -> dict[str, Any]:
    """Construct and inspect the approved conversion request without executing it."""

    # Validate both pinned repositories as data before importing the installed
    # conversion stack. The source helper never executes setup.py.
    source_report = _validate_source_report(
        validate_reviewed_source_contracts(optimum_root, optimum_intel_root)
    )

    # Bind imported modules and distribution versions to the supplied final
    # environment. This check deliberately performs no model or output-path probe.
    imported_modules = _observe_imports(environment_root)
    installed_packages = _observe_packages()

    # Use the real conversion request builder with inert model-path strings. The
    # builder validates the exact approved INT4 asymmetric candidate as data only.
    conversion_executable = environment_root / "Scripts" / "optimum-cli.exe"
    request = ConversionRequest(
        optimum_cli=conversion_executable,
        source_directory=_INERT_SOURCE_DIRECTORY,
        output_directory=_INERT_OUTPUT_DIRECTORY,
        trust_remote_code=False,
    )
    arguments = build_optimum_argument_list(request)
    if any(argument.casefold() == "--trust-remote-code" for argument in arguments):
        raise NoModelCheckError(
            "The no-model conversion argument array enables remote model code."
        )

    # The false observations below are guarantees of this helper's control flow,
    # not claims inferred from a model run. No external process is launched here.
    return {
        "schema_version": "1.0",
        "campaign_id": CAMPAIGN_ID,
        "route_id": ROUTE_ID,
        "record_type": "dependency-no-model-compatibility",
        "status": "Passed",
        "environment_root": str(environment_root),
        "source_contracts": source_report,
        "installed_packages": installed_packages,
        "imported_modules": imported_modules,
        "conversion_executable": str(conversion_executable),
        "conversion_arguments": arguments,
        "trust_remote_code": False,
        "model_opened": False,
        "network_contacted": False,
        "process_executed": False,
        "output_directory_created": False,
        "scientific_authorisations": dict(_SCIENTIFIC_AUTHORISATIONS),
    }


def write_no_model_check(path: Path, payload: Mapping[str, Any]) -> None:
    """Write one deterministic JSON record atomically without overwriting evidence."""

    parent = path.parent
    if not parent.is_dir():
        raise FileNotFoundError(f"Output parent directory does not exist: {parent}")

    temporary = Path(str(path) + ".tmp")
    if path.exists():
        raise FileExistsError(f"Final no-model record already exists: {path}")
    if temporary.exists():
        raise FileExistsError(f"Temporary no-model record already exists: {temporary}")

    text = json.dumps(payload, ensure_ascii=False, indent=2, sort_keys=True) + "\n"
    try:
        # Exclusive creation prevents another process from replacing a record
        # between the checks above and the write.
        with temporary.open("x", encoding="utf-8", newline="\n") as stream:
            stream.write(text)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    except Exception:
        # Remove only the temporary file created by this function. Existing final
        # evidence is never deleted or rewritten.
        if temporary.exists():
            temporary.unlink()
        raise


def _parse_arguments(argv: Sequence[str] | None = None) -> argparse.Namespace:
    """Parse the closed command-line surface used by the live collector."""

    parser = argparse.ArgumentParser(
        description="Run the Workbook 05 no-model conversion compatibility check."
    )
    parser.add_argument("--environment-root", type=Path, required=True)
    parser.add_argument("--optimum-root", type=Path, required=True)
    parser.add_argument("--optimum-intel-root", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None) -> int:
    """Run the inert check and write the resulting text-only evidence record."""

    arguments = _parse_arguments(argv)
    payload = run_no_model_check(
        arguments.environment_root,
        arguments.optimum_root,
        arguments.optimum_intel_root,
    )
    write_no_model_check(arguments.output, payload)
    return 0


if __name__ == "__main__":  # pragma: no cover - exercised through the live script.
    raise SystemExit(main())
