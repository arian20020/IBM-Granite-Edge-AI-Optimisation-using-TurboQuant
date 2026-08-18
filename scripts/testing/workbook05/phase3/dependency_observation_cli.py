"""Materialise the live Workbook 05 dependency observation from retained evidence.

The PowerShell collector owns process supervision and supplies only explicit paths
and scalar identities. This module reads the retained UTF-8 evidence, preserves the
exact lock/report text used for SHA-256 relationships, and publishes one observation
JSON file through a sibling temporary file.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
from typing import Any, Mapping, Sequence


DIRECT_REQUIREMENTS: tuple[str, ...] = (
    "transformers==5.5.0",
    "huggingface-hub==1.21.0",
    "nncf==3.2.0",
    "openvino==2026.2.1",
    "openvino-tokenizers==2026.2.1.0",
)
IMPORT_MODULES: tuple[str, ...] = (
    "optimum",
    "optimum.intel",
    "transformers",
    "nncf",
    "openvino",
)
EXPECTED_SOURCE_RELATIVES: tuple[str, ...] = (
    "sources/optimum.json",
    "sources/optimum-intel.json",
)
EXPECTED_INPUT_RELATIVES: dict[str, str] = {
    "bootstrap_lock": "locks/requirements.phase3-bootstrap.txt",
    "bootstrap_install_report": "reports/bootstrap-install-report.json",
    "normal_lock": "locks/requirements.phase3-assets.txt",
    "normal_install_report": "reports/normal-install-report.json",
    "vcs_packages": "reports/vcs-packages.json",
    "checks": "checks.json",
    "final_environment_packages": "reports/final-environment-packages.json",
}


def _normal_path(path: Path, *, label: str) -> Path:
    """Return one existing, non-symlink regular file as an absolute path."""

    if not path.is_file() or path.is_symlink():
        raise ValueError(f"{label} must be one regular file: {path}")
    return path.resolve(strict=True)


def _require_exact_evidence_path(
    path: Path,
    *,
    evidence_root: Path,
    relative: str,
    label: str,
) -> Path:
    """Keep every input at its reviewed location beneath the output root."""

    actual = _normal_path(path, label=label)
    expected = (evidence_root / Path(relative)).resolve(strict=True)
    if actual != expected:
        raise ValueError(
            f"{label} must remain at {relative} beneath the evidence root."
        )
    return actual


def _read_utf8(path: Path, *, label: str) -> tuple[bytes, str]:
    """Read exact bytes once and decode them as strict, BOM-free UTF-8."""

    payload = path.read_bytes()
    try:
        text = payload.decode("utf-8")
    except UnicodeDecodeError as error:
        raise ValueError(f"{label} must contain valid UTF-8 text.") from error
    if text.startswith("\ufeff"):
        raise ValueError(f"{label} must use BOM-free UTF-8 text.")
    return payload, text


def _load_object(path: Path, *, label: str) -> tuple[bytes, str, dict[str, Any]]:
    """Load one retained JSON object without altering its original text bytes."""

    payload, text = _read_utf8(path, label=label)
    try:
        value = json.loads(text)
    except json.JSONDecodeError as error:
        raise ValueError(f"{label} must contain one JSON object.") from error
    if not isinstance(value, dict):
        raise ValueError(f"{label} must contain one JSON object.")
    return payload, text, value


def _required_array(document: Mapping[str, Any], key: str, *, label: str) -> list[Any]:
    """Read one mandatory JSON array from a retained evidence document."""

    value = document.get(key)
    if not isinstance(value, list):
        raise ValueError(f"{label}.{key} must contain one JSON array.")
    return value


def _sha256(payload: bytes) -> str:
    """Return the lowercase SHA-256 of the exact retained byte sequence."""

    return hashlib.sha256(payload).hexdigest()


def _write_atomic(path: Path, value: Mapping[str, Any]) -> None:
    """Publish deterministic UTF-8 JSON without overwriting prior evidence."""

    temporary = path.with_name(path.name + ".tmp")
    if path.exists():
        raise FileExistsError(f"Final observation already exists: {path}")
    if temporary.exists():
        raise FileExistsError(f"Temporary observation already exists: {temporary}")
    if not path.parent.is_dir():
        raise FileNotFoundError(f"Observation parent does not exist: {path.parent}")

    # Keep Unicode data intact instead of replacing every non-ASCII character
    # with an escape-only representation.
    payload = json.dumps(value, indent=2, sort_keys=True, ensure_ascii=False) + "\n"
    created = False
    try:
        with temporary.open("x", encoding="utf-8", newline="\n") as handle:
            created = True
            handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())

        # Check again immediately before the atomic rename. The controlled
        # collector has one writer, but prior evidence must still win a race.
        if path.exists():
            raise FileExistsError(f"Final observation already exists: {path}")
        os.rename(temporary, path)
    except Exception:
        if created and temporary.exists():
            temporary.unlink()
        raise


def _build_observation(arguments: argparse.Namespace) -> dict[str, Any]:
    """Build the unchanged live observation shape from retained evidence."""

    output = arguments.output
    evidence_root = output.parent.resolve(strict=True)

    # Bind every supplied path to the exact reviewed location under the same
    # evidence root as observation.json.
    inputs = {
        name: _require_exact_evidence_path(
            getattr(arguments, name),
            evidence_root=evidence_root,
            relative=relative,
            label=name.replace("_", " "),
        )
        for name, relative in EXPECTED_INPUT_RELATIVES.items()
    }

    source_paths = tuple(arguments.source_tree)
    if len(source_paths) != len(EXPECTED_SOURCE_RELATIVES):
        raise ValueError("Exactly two reviewed source-tree records are required.")
    source_files = tuple(
        _require_exact_evidence_path(
            path,
            evidence_root=evidence_root,
            relative=relative,
            label=f"source tree {index + 1}",
        )
        for index, (path, relative) in enumerate(
            zip(source_paths, EXPECTED_SOURCE_RELATIVES, strict=True)
        )
    )

    # Preserve the exact text and hashes for fields recomputed by the decision
    # builder and independent hosted validator.
    bootstrap_lock_bytes, bootstrap_lock_text = _read_utf8(
        inputs["bootstrap_lock"],
        label="bootstrap lock",
    )
    bootstrap_report_bytes, bootstrap_report_text, _ = _load_object(
        inputs["bootstrap_install_report"],
        label="bootstrap install report",
    )
    normal_lock_bytes, normal_lock_text = _read_utf8(
        inputs["normal_lock"],
        label="normal lock",
    )
    _, _, normal_report = _load_object(
        inputs["normal_install_report"],
        label="normal install report",
    )

    # The remaining records are already small controlled JSON documents. Load
    # only their required arrays and let the existing decision boundary validate
    # package, source, and check identities.
    source_trees = [
        _load_object(path, label=f"source tree {index + 1}")[2]
        for index, path in enumerate(source_files)
    ]
    vcs_document = _load_object(
        inputs["vcs_packages"],
        label="vcs packages",
    )[2]
    checks_document = _load_object(
        inputs["checks"],
        label="checks",
    )[2]
    final_packages_document = _load_object(
        inputs["final_environment_packages"],
        label="final environment packages",
    )[2]

    return {
        "generated_at_utc": arguments.generated_at_utc,
        "simulation_mode": False,
        "workspace_root": arguments.workspace_root,
        "workspace_is_normal_local_directory": True,
        "workspace_is_fresh": True,
        "python_version": arguments.python_version,
        "python_executable_path": arguments.python_executable_path,
        "python_executable_sha256": arguments.python_executable_sha256,
        "pip_version": arguments.pip_version,
        "pip_executable_path": arguments.pip_executable_path,
        "pip_executable_sha256": arguments.pip_executable_sha256,
        "source_trees": source_trees,
        "direct_requirements": list(DIRECT_REQUIREMENTS),
        "bootstrap_lock_path": EXPECTED_INPUT_RELATIVES["bootstrap_lock"],
        "bootstrap_lock_text": bootstrap_lock_text,
        "bootstrap_lock_sha256": _sha256(bootstrap_lock_bytes),
        "bootstrap_install_report_path": EXPECTED_INPUT_RELATIVES[
            "bootstrap_install_report"
        ],
        "bootstrap_install_report_text": bootstrap_report_text,
        "bootstrap_install_report_sha256": _sha256(bootstrap_report_bytes),
        "lock_path": EXPECTED_INPUT_RELATIVES["normal_lock"],
        "lock_text": normal_lock_text,
        "lock_sha256": _sha256(normal_lock_bytes),
        "lock_generator": "pip-tools==7.6.0",
        "normal_install_report": normal_report,
        "vcs_packages": _required_array(
            vcs_document,
            "packages",
            label="vcs packages",
        ),
        "checks": _required_array(
            checks_document,
            "checks",
            label="checks",
        ),
        "import_modules": list(IMPORT_MODULES),
        "cli_help_exit_code": 0,
        "no_model_compatibility_exit_code": 0,
        "final_environment_packages": _required_array(
            final_packages_document,
            "packages",
            label="final environment packages",
        ),
        "source_contracts_path": "reports/source-contracts.json",
        "no_model_compatibility_path": "reports/no-model-compatibility.json",
        "command_index_path": "command-index.json",
        "stage_order_path": "stage-order.json",
    }


def _parser() -> argparse.ArgumentParser:
    """Build the explicit scalar/path-only command-line contract."""

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--generated-at-utc", required=True)
    parser.add_argument("--workspace-root", required=True)
    parser.add_argument("--python-version", required=True)
    parser.add_argument("--python-executable-path", required=True)
    parser.add_argument("--python-executable-sha256", required=True)
    parser.add_argument("--pip-version", required=True)
    parser.add_argument("--pip-executable-path", required=True)
    parser.add_argument("--pip-executable-sha256", required=True)
    parser.add_argument("--bootstrap-lock", type=Path, required=True)
    parser.add_argument("--bootstrap-install-report", type=Path, required=True)
    parser.add_argument("--normal-lock", type=Path, required=True)
    parser.add_argument("--normal-install-report", type=Path, required=True)
    parser.add_argument(
        "--source-tree",
        type=Path,
        action="append",
        required=True,
    )
    parser.add_argument("--vcs-packages", type=Path, required=True)
    parser.add_argument("--checks", type=Path, required=True)
    parser.add_argument(
        "--final-environment-packages",
        type=Path,
        required=True,
    )
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    """Materialise one observation and return zero after atomic publication."""

    arguments = _parser().parse_args(argv)

    # Validate the output before reading any evidence so prior final or temporary
    # records are never silently reused.
    if arguments.output.exists():
        raise FileExistsError(f"Final observation already exists: {arguments.output}")
    temporary = arguments.output.with_name(arguments.output.name + ".tmp")
    if temporary.exists():
        raise FileExistsError(f"Temporary observation already exists: {temporary}")
    if not arguments.output.parent.is_dir():
        raise FileNotFoundError(
            f"Observation parent does not exist: {arguments.output.parent}"
        )

    observation = _build_observation(arguments)
    _write_atomic(arguments.output, observation)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
