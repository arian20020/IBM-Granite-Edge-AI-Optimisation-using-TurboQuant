"""Generate deterministic text-only dependency-preflight simulation evidence.

The module exists solely for repository tests. The production workflow contract
forbids every simulation option, and hosted live validation rejects an observation
that declares ``simulation_mode``.
"""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable, Mapping, Sequence

from scripts.testing.workbook05.hash_manifest import write_hash_manifest
from scripts.testing.workbook05.phase3.conversion import (
    OPTIMUM_COMMIT,
    OPTIMUM_INTEL_COMMIT,
    REVIEWED_DIRECT_REQUIREMENTS,
)
from scripts.testing.workbook05.phase3.dependency_decision import (
    build_live_dependency_preflight_record,
)
from scripts.testing.workbook05.phase3.dependency_lock import (
    LockedDistribution,
    parse_hash_locked_requirements,
)
from scripts.testing.workbook05.phase3.dependency_preflight import IMPORT_MODULES


STAGE_ORDER: tuple[str, ...] = (
    "workspace-validation",
    "source-verification",
    "lock-generation",
    "normal-install",
    "vcs-install",
    "imports",
    "cli-help",
    "no-model-compatibility",
    "record-generation",
    "manifest-generation",
)
CHECK_ORDER: tuple[str, ...] = (
    "resolver",
    "install",
    "imports",
    "cli_help",
    "no_model_compatibility",
    "remote_code_disabled",
)
CLAIM_KEYS: tuple[str, ...] = (
    "model_download_authorised",
    "granite_model_test_authorised",
    "activation_claim_authorised",
    "packed_storage_claim_authorised",
    "performance_claim_authorised",
    "quality_claim_authorised",
)


def _write_text(path: Path, text: str) -> None:
    if path.exists():
        raise FileExistsError(f"Fixture output already exists: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def _write_json(path: Path, value: Mapping[str, Any] | list[Any]) -> None:
    _write_text(path, json.dumps(value, indent=2, sort_keys=True) + "\n")


def _digest(character: str) -> str:
    return character * 64


def _normal_lock_text() -> str:
    rows = (
        ("transformers", "5.5.0", "a"),
        ("huggingface-hub", "1.21.0", "b"),
        ("nncf", "3.2.0", "c"),
        ("openvino", "2026.2.1", "d"),
        ("openvino-tokenizers", "2026.2.1.0", "e"),
        ("numpy", "2.3.0", "f"),
    )
    return "".join(
        f"{name}=={version} --hash=sha256:{_digest(character)}\n"
        for name, version, character in rows
    )


def _pip_report(
    lock: Sequence[LockedDistribution],
    direct_names: frozenset[str],
    pip_version: str = "26.1.2",
) -> dict[str, Any]:
    return {
        "version": "1",
        "pip_version": pip_version,
        "install": [
            {
                "download_info": {
                    "url": (
                        "https://files.pythonhosted.org/packages/fixture/"
                        f"{package.name}-{package.version}-py3-none-any.whl"
                    ),
                    "archive_info": {
                        "hashes": {"sha256": package.hashes[0]}
                    },
                },
                "requested": package.name in direct_names,
                "is_direct": package.name in direct_names,
                "metadata": {
                    "name": package.name,
                    "version": package.version,
                },
            }
            for package in lock
        ],
    }


def _source_rows(name: str) -> list[dict[str, Any]]:
    contents = {
        "setup.py": f"fixture source metadata for {name}\n",
        "pyproject.toml": "[tool.fixture]\nvalue = true\n",
        "README.md": f"# {name} fixture\n",
    }
    return [
        {
            "relative_path": relative,
            "size_bytes": len(content.encode("utf-8")),
            "sha256": hashlib.sha256(content.encode("utf-8")).hexdigest(),
        }
        for relative, content in sorted(contents.items())
    ]


def _aggregate(rows: Sequence[Mapping[str, Any]]) -> str:
    digest = hashlib.sha256()
    for row in rows:
        line = (
            f"{row['relative_path']}\0{row['size_bytes']}\0{row['sha256']}\n"
        ).encode("utf-8")
        digest.update(line)
    return digest.hexdigest()


def _write_source_csv(path: Path, rows: Sequence[Mapping[str, Any]]) -> None:
    if path.exists():
        raise FileExistsError(f"Fixture source CSV already exists: {path}")
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(
            handle,
            fieldnames=("relative_path", "size_bytes", "sha256"),
            lineterminator="\n",
        )
        writer.writeheader()
        writer.writerows(rows)


def _command_record(command_id: str, stage: str) -> dict[str, Any]:
    return {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "build-command",
        "command_id": command_id,
        "route_id": "route-a-merged-openvino",
        "component": "dependency-preflight",
        "stage": stage,
        "executable": r"C:\Program Files\Python312\python.exe",
        "arguments": ["-m", "fixture", stage],
        "working_directory": r"C:\w5c\fixture",
        "environment_allowlist": {},
        "started_utc": "2026-08-16T00:00:00Z",
        "ended_utc": "2026-08-16T00:00:01Z",
        "elapsed_seconds": 1.0,
        "exit_code": 0,
        "stdout_path": f"commands/{command_id}.stdout.txt",
        "stderr_path": f"commands/{command_id}.stderr.txt",
    }


def _write_command_evidence(evidence: Path) -> list[dict[str, str]]:
    records: list[dict[str, str]] = []
    for index, stage in enumerate(STAGE_ORDER[:-2], start=1):
        command_id = f"dep-{index:02d}-{stage}"
        record_path = evidence / "commands" / f"{command_id}.command.json"
        stdout_path = evidence / "commands" / f"{command_id}.stdout.txt"
        stderr_path = evidence / "commands" / f"{command_id}.stderr.txt"
        resource_path = evidence / "commands" / f"{command_id}.resources.json"
        resource_csv = evidence / "commands" / f"{command_id}.resources.csv"
        _write_json(record_path, _command_record(command_id, stage))
        _write_text(stdout_path, f"fixture {stage} passed\n")
        _write_text(stderr_path, "")
        _write_json(
            resource_path,
            {
                "schema_version": "1.0",
                "record_type": "build-resource-summary",
                "campaign_id": "GTQ-WB05-MF-v1",
                "route_id": "route-a-merged-openvino",
                "component": "dependency-preflight",
                "command_id": command_id,
                "sample_count": 1,
                "safety_stop_triggered": False,
                "safety_stop_reason": None,
            },
        )
        _write_text(
            resource_csv,
            "timestamp_utc,root_process_id,working_set_bytes\n"
            "2026-08-16T00:00:00Z,1,1024\n",
        )
        records.append(
            {
                "command_id": command_id,
                "stage": stage,
                "record_path": record_path.relative_to(evidence).as_posix(),
                "stdout_path": stdout_path.relative_to(evidence).as_posix(),
                "stderr_path": stderr_path.relative_to(evidence).as_posix(),
                "resource_summary_path": resource_path.relative_to(evidence).as_posix(),
                "resource_csv_path": resource_csv.relative_to(evidence).as_posix(),
            }
        )
    return records


def _failure_record(
    run_id: str,
    run_attempt: int,
    stage: str,
    completed: Sequence[str],
    classification: str,
) -> dict[str, Any]:
    record: dict[str, Any] = {
        "schema_version": "1.0",
        "campaign_id": "GTQ-WB05-MF-v1",
        "record_type": "dependency-preflight-failure",
        "route_id": "route-a-merged-openvino",
        "run_id": run_id,
        "run_attempt": run_attempt,
        "workspace_root": rf"C:\w5c\dependency-preflight-{run_id}-{run_attempt}",
        "current_stage": stage,
        "completed_stages": list(completed),
        "classification": classification,
        "first_causal_message": (
            f"Injected repository simulation failure at stage {stage}."
        ),
        "recorded_at_utc": datetime.now(timezone.utc).isoformat().replace(
            "+00:00", "Z"
        ),
    }
    record.update({key: False for key in CLAIM_KEYS})
    return record


def generate_fixture(
    output_root: Path,
    run_id: str,
    run_attempt: int,
    repository_root: Path,
    *,
    failure_stage: str | None = None,
    failure_classification: str = "Blocked",
) -> Path:
    attempt = output_root / f"dependency-preflight-{run_id}-{run_attempt}"
    if attempt.exists():
        raise FileExistsError(
            f"C1 dependency-preflight workspace already exists: {attempt}"
        )
    evidence = attempt / "evidence"
    workspace = attempt / "workspace"
    for directory in (
        evidence / "locks",
        evidence / "reports",
        evidence / "sources",
        evidence / "commands",
        workspace / "bootstrap-venv",
        workspace / "environment",
    ):
        directory.mkdir(parents=True, exist_ok=False)

    completed: list[str] = []
    for stage in STAGE_ORDER:
        if failure_stage == stage:
            _write_json(
                evidence / "stage-order.json",
                {
                    "schema_version": "1.0",
                    "stage_order": list(STAGE_ORDER),
                    "completed_stages": completed,
                    "current_stage": stage,
                },
            )
            _write_json(
                evidence / "failure.json",
                _failure_record(
                    run_id,
                    run_attempt,
                    stage,
                    completed,
                    failure_classification,
                ),
            )
            _write_text(
                evidence / "summary.md",
                "# Dependency preflight simulation failure\n\n"
                f"Classification: {failure_classification}\n",
            )
            return attempt
        completed.append(stage)

    bootstrap_path = (
        repository_root
        / "scripts/testing/workbook05/requirements.phase3-bootstrap.txt"
    )
    bootstrap_text = bootstrap_path.read_text(encoding="utf-8")
    bootstrap_lock = parse_hash_locked_requirements(
        bootstrap_text,
        required_direct_versions={"pip-tools": "7.6.0", "pip": "26.1.2"},
    )
    bootstrap_report = _pip_report(
        bootstrap_lock,
        frozenset({"pip-tools", "pip"}),
    )
    bootstrap_report_text = json.dumps(
        bootstrap_report,
        indent=2,
        sort_keys=True,
    ) + "\n"

    normal_text = _normal_lock_text()
    normal_lock = parse_hash_locked_requirements(
        normal_text,
        required_direct_versions={
            "transformers": "5.5.0",
            "huggingface-hub": "1.21.0",
            "nncf": "3.2.0",
            "openvino": "2026.2.1",
            "openvino-tokenizers": "2026.2.1.0",
        },
        forbidden_names=frozenset({"optimum", "optimum-intel"}),
    )
    normal_report = _pip_report(
        normal_lock,
        frozenset(
            {
                "transformers",
                "huggingface-hub",
                "nncf",
                "openvino",
                "openvino-tokenizers",
            }
        ),
    )

    _write_text(
        evidence / "locks" / "requirements.phase3-bootstrap.txt",
        bootstrap_text,
    )
    _write_json(
        evidence / "reports" / "bootstrap-install-report.json",
        bootstrap_report,
    )
    _write_text(
        evidence / "locks" / "requirements.phase3-assets.txt",
        normal_text,
    )
    _write_json(
        evidence / "reports" / "normal-install-report.json",
        normal_report,
    )

    source_trees: list[dict[str, Any]] = []
    for name, repository, origin, commit in (
        (
            "optimum",
            "huggingface/optimum",
            "https://github.com/huggingface/optimum.git",
            OPTIMUM_COMMIT,
        ),
        (
            "optimum-intel",
            "huggingface/optimum-intel",
            "https://github.com/huggingface/optimum-intel.git",
            OPTIMUM_INTEL_COMMIT,
        ),
    ):
        rows = _source_rows(name)
        source = {
            "name": name,
            "repository": repository,
            "origin": origin,
            "commit": commit,
            "clean": True,
            "file_count": len(rows),
            "aggregate_sha256": _aggregate(rows),
            "manifest_path": f"sources/{name}.csv",
        }
        source_trees.append(source)
        _write_source_csv(evidence / "sources" / f"{name}.csv", rows)
        _write_json(evidence / "sources" / f"{name}.json", source)

    source_contracts = {
        "schema_version": "1.0",
        "record_type": "dependency-source-contract",
        "status": "Passed",
        "source_metadata_execution": False,
        "build_system_declared": False,
        "installer_build_mode": "setuptools-no-build-isolation",
        "reviewed_build_tools": ["setuptools", "wheel"],
        "sources": {
            "optimum": {
                "name": "optimum",
                "base_version": "2.3.0",
                "reviewed_commit": OPTIMUM_COMMIT,
            },
            "optimum-intel": {
                "name": "optimum-intel",
                "base_version": "2.2.0.dev0",
                "reviewed_commit": OPTIMUM_INTEL_COMMIT,
            },
        },
    }
    _write_json(evidence / "reports" / "source-contracts.json", source_contracts)

    vcs_packages = [
        {
            "name": "optimum",
            "version": "2.3.0",
            "commit": OPTIMUM_COMMIT,
        },
        {
            "name": "optimum-intel",
            "version": "2.2.0.dev0+a3b6012",
            "commit": OPTIMUM_INTEL_COMMIT,
        },
    ]
    _write_json(
        evidence / "reports" / "vcs-packages.json",
        {"packages": vcs_packages},
    )
    _write_json(
        evidence / "reports" / "normal-packages.json",
        {
            "packages": [
                {"name": row.name, "version": row.version}
                for row in normal_lock
            ]
        },
    )
    final_packages = (
        [{"name": "pip", "version": "26.1.2"}]
        + [{"name": row.name, "version": row.version} for row in normal_lock]
        + [
            {"name": "optimum", "version": "2.3.0"},
            {"name": "optimum-intel", "version": "2.2.0.dev0+a3b6012"},
        ]
    )
    _write_json(
        evidence / "reports" / "final-environment-packages.json",
        {"packages": final_packages},
    )

    checks = [
        {"name": name, "status": "Passed", "exit_code": None if name == "remote_code_disabled" else 0}
        for name in CHECK_ORDER
    ]
    _write_json(evidence / "checks.json", {"checks": checks})

    no_model = {
        "schema_version": "1.0",
        "record_type": "dependency-no-model-compatibility",
        "status": "Passed",
        "trust_remote_code": False,
        "model_opened": False,
        "network_contacted": False,
        "process_executed": False,
        "output_directory_created": False,
        "scientific_authorisations": {key: False for key in CLAIM_KEYS},
    }
    _write_json(
        evidence / "reports" / "no-model-compatibility.json",
        no_model,
    )

    command_index = _write_command_evidence(evidence)
    _write_json(
        evidence / "command-index.json",
        {
            "schema_version": "1.0",
            "record_type": "dependency-command-index",
            "commands": command_index,
        },
    )

    observation: dict[str, Any] = {
        "generated_at_utc": datetime.now(timezone.utc).isoformat().replace(
            "+00:00", "Z"
        ),
        "simulation_mode": True,
        "workspace_root": rf"C:\w5c\dependency-preflight-{run_id}-{run_attempt}",
        "workspace_is_normal_local_directory": True,
        "workspace_is_fresh": True,
        "python_version": "3.12.10",
        "python_executable_path": (
            rf"C:\w5c\dependency-preflight-{run_id}-{run_attempt}"
            r"\workspace\environment\Scripts\python.exe"
        ),
        "python_executable_sha256": _digest("1"),
        "pip_version": "26.1.2",
        "pip_executable_path": (
            rf"C:\w5c\dependency-preflight-{run_id}-{run_attempt}"
            r"\workspace\environment\Scripts\pip.exe"
        ),
        "pip_executable_sha256": _digest("2"),
        "source_trees": source_trees,
        "direct_requirements": list(REVIEWED_DIRECT_REQUIREMENTS),
        "bootstrap_lock_path": "locks/requirements.phase3-bootstrap.txt",
        "bootstrap_lock_text": bootstrap_text,
        "bootstrap_lock_sha256": hashlib.sha256(
            bootstrap_text.encode("utf-8")
        ).hexdigest(),
        "bootstrap_install_report_path": "reports/bootstrap-install-report.json",
        "bootstrap_install_report_text": bootstrap_report_text,
        "bootstrap_install_report_sha256": hashlib.sha256(
            bootstrap_report_text.encode("utf-8")
        ).hexdigest(),
        "lock_path": "locks/requirements.phase3-assets.txt",
        "lock_text": normal_text,
        "lock_sha256": hashlib.sha256(normal_text.encode("utf-8")).hexdigest(),
        "lock_generator": "pip-tools==7.6.0",
        "normal_install_report": normal_report,
        "vcs_packages": vcs_packages,
        "checks": checks,
        "import_modules": list(IMPORT_MODULES),
        "cli_help_exit_code": 0,
        "no_model_compatibility_exit_code": 0,
        "final_environment_packages": final_packages,
        "source_contracts_path": "reports/source-contracts.json",
        "no_model_compatibility_path": "reports/no-model-compatibility.json",
        "command_index_path": "command-index.json",
        "stage_order_path": "stage-order.json",
    }
    _write_json(evidence / "observation.json", observation)
    decision = build_live_dependency_preflight_record(observation)
    if decision.get("status") != "Passed":
        raise ValueError(
            "The repository simulation did not produce a Passed dependency decision: "
            + "; ".join(str(value) for value in decision.get("reasons", []))
        )
    _write_json(evidence / "decision.json", decision)

    _write_json(
        evidence / "stage-order.json",
        {
            "schema_version": "1.0",
            "stage_order": list(STAGE_ORDER),
            "completed_stages": list(STAGE_ORDER),
            "current_stage": None,
        },
    )
    _write_text(
        evidence / "summary.md",
        "# Workbook 05 dependency-preflight simulation\n\n"
        "Repository orchestration only. No model was opened, downloaded, "
        "converted, or executed, and no scientific claim is authorised.\n",
    )
    write_hash_manifest(evidence, evidence / "manifest.sha256")
    return attempt


def main(argv: Iterable[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-root", type=Path, required=True)
    parser.add_argument("--repository-root", type=Path, required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--run-attempt", type=int, required=True)
    parser.add_argument("--failure-stage", choices=STAGE_ORDER)
    parser.add_argument(
        "--failure-classification",
        choices=("Blocked", "IntegrityFailure", "InfrastructureInterrupted"),
        default="Blocked",
    )
    arguments = parser.parse_args(list(argv) if argv is not None else None)

    generate_fixture(
        arguments.output_root,
        arguments.run_id,
        arguments.run_attempt,
        arguments.repository_root,
        failure_stage=arguments.failure_stage,
        failure_classification=arguments.failure_classification,
    )
    if arguments.failure_stage:
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
