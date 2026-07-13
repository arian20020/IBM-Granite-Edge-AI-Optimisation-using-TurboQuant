"""Validate traceability IDs, relationships, paths and generated snapshots."""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path
from typing import Any

from traceability_common import load_json


EXPECTED_COUNTS = {
    "active_tasks": 165,
    "requirements_all_lifecycles": 111,
    "requirements_active": 76,
    "work_packages": 49,
    "engineering_practices": 40,
    "objectives": 11,
    "research_questions": 5,
}

GENERATED_FILES = [
    "Active-Task-Catalogue.md",
    "Requirement-Catalogue.md",
    "Full-Requirement-Baseline.md",
    "Work-Package-Catalogue.md",
    "Engineering-Practice-Catalogue.md",
    "Task-Relationship-Matrix.md",
    "Tasks-By-Phase.md",
    "Tasks-By-Deadline.md",
    "Validation-Pending.md",
    "Work-Package-Issue-Plan.md",
]


def parse_arguments() -> argparse.Namespace:
    """Read validator command-line arguments."""

    parser = argparse.ArgumentParser(description="Validate the repository traceability catalogue.")
    parser.add_argument(
        "--repository-root",
        type=Path,
        default=Path.cwd(),
        help="Repository root. Defaults to the current directory.",
    )
    parser.add_argument(
        "--check-evidence-paths",
        action="store_true",
        help="Also require every repository evidence path to exist.",
    )
    return parser.parse_args()


def add_error(errors: list[str], message: str) -> None:
    """Append one human-readable validation error."""

    errors.append(message)


def path_is_safe(path: str | None) -> bool:
    """Accept repository-relative paths and HTTPS links, but reject local absolute paths."""

    if not path:
        return False
    if re.match(r"^https://", path, re.IGNORECASE):
        return True
    if re.match(r"^[A-Za-z]:[/\\]", path):
        return False
    if path.startswith("/") or ".." in Path(path).parts:
        return False
    return True


def main() -> int:
    """Run every catalogue consistency check and return a process exit code."""

    args = parse_arguments()
    repository_root = args.repository_root.resolve()
    data_directory = repository_root / "docs" / "traceability" / "data"
    generated_directory = repository_root / "docs" / "traceability" / "generated"
    catalogue_path = data_directory / "task-catalogue.json"
    schema_path = data_directory / "task-catalogue.schema.json"
    errors: list[str] = []

    if not catalogue_path.is_file():
        raise FileNotFoundError(f"Missing catalogue: {catalogue_path}")
    if not schema_path.is_file():
        add_error(errors, f"Missing JSON schema: {schema_path}")

    catalogue = load_json(catalogue_path)
    metadata = catalogue.get("metadata", {})
    tasks = catalogue.get("tasks", [])
    requirements = catalogue.get("requirements_baseline", [])

    # Check the known v1.3 baseline counts so partial exports fail loudly.
    counts = metadata.get("counts", {})
    for key, expected in EXPECTED_COUNTS.items():
        actual = counts.get(key)
        if actual != expected:
            add_error(errors, f"Count mismatch for {key}: expected {expected}, found {actual}.")

    # Check stable IDs and task ordering.
    master_keys = [task.get("master_key") for task in tasks]
    source_keys = [(task.get("task_type"), task.get("source_id")) for task in tasks]
    numbers = [task.get("task_number") for task in tasks]

    for value, count in Counter(master_keys).items():
        if not value or count > 1:
            add_error(errors, f"Master key is missing or duplicated: {value!r} ({count}).")
    for value, count in Counter(source_keys).items():
        if not value[1] or count > 1:
            add_error(errors, f"Source ID is missing or duplicated within task type: {value!r} ({count}).")
    if numbers != list(range(1, len(tasks) + 1)):
        add_error(errors, "Task numbers are not a complete ordered sequence beginning at 1.")

    requirement_ids = {item["requirement_id"] for item in requirements}
    active_requirement_ids = {
        item["requirement_id"] for item in requirements if item.get("lifecycle") == "Active"
    }
    work_package_ids = {
        task["source_id"] for task in tasks if task.get("task_type") == "work-package"
    }
    practice_ids = {
        task["source_id"] for task in tasks if task.get("task_type") == "engineering-practice"
    }
    objective_ids = set(catalogue.get("objectives", {}))
    question_ids = set(catalogue.get("research_questions", {}))

    # Check each task's required fields, relationships and evidence path.
    for task in tasks:
        key = task.get("master_key", "<missing>")
        if not task.get("title"):
            add_error(errors, f"{key} has no task statement.")
        if not task.get("definition_of_done"):
            add_error(errors, f"{key} has no Definition of Done / next action.")
        if not path_is_safe(task.get("evidence_path")):
            add_error(errors, f"{key} has a missing or unsafe evidence path: {task.get('evidence_path')!r}.")

        relationships = task.get("relationships", {})
        unknown_requirements = set(relationships.get("requirements", [])) - active_requirement_ids
        unknown_work_packages = set(relationships.get("work_packages", [])) - work_package_ids
        unknown_practices = set(relationships.get("engineering_practices", [])) - practice_ids
        unknown_objectives = set(relationships.get("objectives", [])) - objective_ids
        unknown_questions = set(relationships.get("research_questions", [])) - question_ids

        if unknown_requirements:
            add_error(errors, f"{key} references unknown active requirements: {sorted(unknown_requirements)}.")
        if unknown_work_packages:
            add_error(errors, f"{key} references unknown work packages: {sorted(unknown_work_packages)}.")
        if unknown_practices:
            add_error(errors, f"{key} references unknown engineering practices: {sorted(unknown_practices)}.")
        if unknown_objectives:
            add_error(errors, f"{key} references unknown objectives: {sorted(unknown_objectives)}.")
        if unknown_questions:
            add_error(errors, f"{key} references unknown research questions: {sorted(unknown_questions)}.")

        if task.get("effective_status") == "Verified":
            if task.get("validation") != "Validated":
                add_error(errors, f"{key} is Verified but its validation state is not Validated.")
            if not task.get("evidence_path"):
                add_error(errors, f"{key} is Verified without an evidence path.")

        if args.check_evidence_paths and task.get("evidence_path"):
            evidence_path = task["evidence_path"]
            if not re.match(r"^https://", evidence_path, re.IGNORECASE):
                resolved = repository_root / evidence_path.rstrip("/")
                if not resolved.exists():
                    add_error(errors, f"{key} evidence path does not exist: {evidence_path}.")

    # Check requirement IDs and lifecycle allocation.
    for requirement in requirements:
        requirement_id = requirement.get("requirement_id")
        if requirement_id not in requirement_ids:
            add_error(errors, f"Requirement ID could not be indexed: {requirement_id!r}.")
        if requirement.get("lifecycle") == "Active" and not requirement.get("work_packages"):
            add_error(errors, f"Active requirement {requirement_id} is not allocated to a work package.")

    # Check generated files are present and clearly marked as generated.
    for filename in GENERATED_FILES:
        path = generated_directory / filename
        if not path.is_file():
            add_error(errors, f"Missing generated document: {path}.")
            continue
        first_line = path.read_text(encoding="utf-8").splitlines()[0]
        if "GENERATED FILE" not in first_line:
            add_error(errors, f"Generated document lacks its warning header: {path}.")

    # Check the JSON can be serialized again without custom object types.
    try:
        json.dumps(catalogue, ensure_ascii=False)
    except TypeError as exc:
        add_error(errors, f"Catalogue contains a non-JSON value: {exc}.")

    print("Traceability validation summary")
    print(f"Active tasks checked: {len(tasks)}")
    print(f"Requirements checked: {len(requirements)}")
    print(f"Generated documents expected: {len(GENERATED_FILES)}")
    print(f"Errors: {len(errors)}")

    if errors:
        print("\nValidation FAILED:")
        for error in errors:
            print(f"- {error}")
        return 1

    print("\nTraceability validation PASSED.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
