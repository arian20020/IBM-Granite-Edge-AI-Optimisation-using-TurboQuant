"""Generate human-readable Markdown views from task-catalogue.json."""

from __future__ import annotations

import argparse
from collections import defaultdict
from datetime import date
from pathlib import Path
from typing import Any

from traceability_common import (
    ensure_text,
    load_json,
    markdown_escape,
    repository_relative_link,
    slugify_id,
)


GENERATED_NOTICE = (
    "<!-- GENERATED FILE: edit the RTM workbook or generator, then regenerate. -->\n\n"
)


def parse_arguments() -> argparse.Namespace:
    """Read generator command-line arguments."""

    parser = argparse.ArgumentParser(description="Generate repository traceability Markdown documents.")
    parser.add_argument(
        "--repository-root",
        type=Path,
        default=Path.cwd(),
        help="Repository root. Defaults to the current directory.",
    )
    return parser.parse_args()


def md_link(label: str, target: str | None, current_file: Path) -> str:
    """Return a relative Markdown link, or an em dash when no target exists."""

    if not target:
        return "—"
    link = repository_relative_link(current_file, target)
    return f"[{label}]({link})"


def internal_task_link(task: dict[str, Any], current_file: Path, target_file: Path) -> str:
    """Link to a task's stable anchor in the active catalogue."""

    relative = repository_relative_link(current_file, str(target_file))
    return f"[`{task['master_key']}`]({relative}#{task['anchor']})"


def task_catalogue_document(catalogue: dict[str, Any], output: Path) -> str:
    """Build the complete active-task catalogue."""

    metadata = catalogue["metadata"]
    tasks = catalogue["tasks"]
    lines = [
        GENERATED_NOTICE,
        "# Active Task Catalogue",
        "",
        f"**Catalogue version:** {metadata['catalogue_version']}",
        f"**Generated on:** {metadata['generated_on']}",
        f"**Source workbook SHA-256:** `{metadata['source_sha256']}`",
        f"**Active task count:** {metadata['counts']['active_tasks']}",
        "",
        "> This is a generated repository snapshot. Update status and validation in the controlled RTM workbook, then regenerate these files.",
        "",
        "## Quick navigation",
        "",
        "- [Requirements](#requirements)",
        "- [Work packages](#work-packages)",
        "- [Engineering practices](#engineering-practices)",
        "",
    ]

    groups = [
        ("Requirements", "requirement"),
        ("Work packages", "work-package"),
        ("Engineering practices", "engineering-practice"),
    ]

    for heading, task_type in groups:
        lines.extend([f"## {heading}", ""])
        for task in [item for item in tasks if item["task_type"] == task_type]:
            lines.extend(
                [
                    f"<a id=\"{task['anchor']}\"></a>",
                    f"### {task['master_key']} — {task['title']}",
                    "",
                    "| Field | Value |",
                    "|---|---|",
                    f"| Task number | {task['task_number']} |",
                    f"| Source tab | {markdown_escape(task['source_tab'])} |",
                    f"| Priority | {markdown_escape(task['priority'])} |",
                    f"| Release role / phase | {markdown_escape(task['release_role_or_phase'])} |",
                    f"| Deadline | {markdown_escape(task['deadline'])} |",
                    f"| Working status | {markdown_escape(task['working_status'])} |",
                    f"| Validation | {markdown_escape(task['validation'])} |",
                    f"| Effective status | **{markdown_escape(task['effective_status'])}** |",
                    f"| Planned evidence | {md_link(task['evidence_path'] or 'Evidence path', task['evidence_path'], output)} |",
                    "",
                    "#### Task statement",
                    "",
                    str(task["title"] or "—"),
                    "",
                    "#### Definition of Done / next action",
                    "",
                    str(task["definition_of_done"] or "—"),
                    "",
                    "#### Traceability",
                    "",
                    f"- Requirements: {', '.join(f'`{value}`' for value in task['relationships']['requirements']) or '—'}",
                    f"- Work packages: {', '.join(f'`{value}`' for value in task['relationships']['work_packages']) or '—'}",
                    f"- Engineering practices: {', '.join(f'`{value}`' for value in task['relationships']['engineering_practices']) or '—'}",
                    f"- Objectives: {', '.join(f'`{value}`' for value in task['relationships']['objectives']) or '—'}",
                    f"- Research questions: {', '.join(f'`{value}`' for value in task['relationships']['research_questions']) or '—'}",
                    "",
                ]
            )
    return "\n".join(lines)


def requirement_catalogue_document(catalogue: dict[str, Any], output: Path, active_only: bool) -> str:
    """Build a detailed requirement catalogue for active or all lifecycles."""

    requirements = catalogue["requirements_baseline"]
    if active_only:
        requirements = [item for item in requirements if item["lifecycle"] == "Active"]
        title = "Active Requirement Catalogue"
    else:
        title = "Full Requirement Baseline"

    lines = [
        GENERATED_NOTICE,
        f"# {title}",
        "",
        f"**Generated on:** {catalogue['metadata']['generated_on']}",
        f"**Requirement count:** {len(requirements)}",
        "",
    ]

    if not active_only:
        lines.extend(
            [
                "> This baseline deliberately retains Active, Deferred, Superseded and Excluded records so that scope history is not lost.",
                "",
            ]
        )

    for requirement in requirements:
        anchor = requirement["anchor"]
        lines.extend(
            [
                f"<a id=\"{anchor}\"></a>",
                f"## REQ:{requirement['requirement_id']} — {requirement['requirement']}",
                "",
                "| Field | Value |",
                "|---|---|",
                f"| Category | {markdown_escape(requirement['category'])} |",
                f"| Priority | {markdown_escape(requirement['priority'])} |",
                f"| Release role | {markdown_escape(requirement['release_role'])} |",
                f"| Lifecycle | **{markdown_escape(requirement['lifecycle'])}** |",
                f"| Baseline version | {markdown_escape(requirement['baseline_version'])} |",
                f"| Status | {markdown_escape(requirement['status'])} |",
                f"| Target date | {markdown_escape(requirement['target_date'])} |",
                f"| Owner | {markdown_escape(requirement['owner'])} |",
                f"| Planned evidence | {md_link(requirement['planned_evidence_path'] or 'Evidence path', requirement['planned_evidence_path'], output)} |",
                "",
                "### Rationale",
                "",
                str(requirement["rationale"] or "—"),
                "",
                "### Acceptance and verification",
                "",
                f"- **Acceptance criteria:** {requirement['acceptance_criteria'] or '—'}",
                f"- **Verification method:** {requirement['verification_method'] or '—'}",
                f"- **Test/evidence ID:** `{requirement['test_evidence_id']}`" if requirement["test_evidence_id"] else "- **Test/evidence ID:** —",
                "",
                "### Allocation",
                "",
                f"- Objectives: {', '.join(f'`{value}`' for value in requirement['objectives']) or '—'}",
                f"- Research questions: {', '.join(f'`{value}`' for value in requirement['research_questions']) or '—'}",
                f"- Work packages: {', '.join(f'`{value}`' for value in requirement['work_packages']) or '—'}",
                f"- Planned components: {', '.join(f'`{value}`' for value in requirement['planned_components']) or '—'}",
                f"- Dependencies: {', '.join(f'`{value}`' for value in requirement['dependencies']) or '—'}",
                "",
                "### Source and history",
                "",
                f"- Source: {requirement['source'] or '—'}",
                f"- Previous/replacement ID: {requirement['previous_or_replacement_id'] or '—'}",
                f"- Notes: {requirement['notes'] or '—'}",
                "",
            ]
        )

    return "\n".join(lines)


def task_type_catalogue(
    catalogue: dict[str, Any], output: Path, task_type: str, title: str
) -> str:
    """Build a compact catalogue for one active task type."""

    tasks = [task for task in catalogue["tasks"] if task["task_type"] == task_type]
    active_catalogue = output.parent / "Active-Task-Catalogue.md"
    lines = [
        GENERATED_NOTICE,
        f"# {title}",
        "",
        f"**Generated on:** {catalogue['metadata']['generated_on']}",
        f"**Record count:** {len(tasks)}",
        "",
        "| ID | Task | Priority | Phase / role | Status | Deadline | Planned evidence |",
        "|---|---|---|---|---|---|---|",
    ]

    for task in tasks:
        evidence = md_link("Open", task["evidence_path"], output) if task["evidence_path"] else "—"
        lines.append(
            "| "
            + " | ".join(
                [
                    internal_task_link(task, output, active_catalogue),
                    markdown_escape(task["title"]),
                    markdown_escape(task["priority"]),
                    markdown_escape(task["release_role_or_phase"]),
                    markdown_escape(task["effective_status"]),
                    markdown_escape(task["deadline"]),
                    evidence,
                ]
            )
            + " |"
        )
    return "\n".join(lines)


def relationship_matrix_document(catalogue: dict[str, Any], output: Path) -> str:
    """Build a cross-reference matrix for all active tasks."""

    lines = [
        GENERATED_NOTICE,
        "# Task Relationship Matrix",
        "",
        "This view shows how requirements, work packages and engineering practices connect. Relationships are derived from the controlled workbook allocation fields.",
        "",
        "| Master key | Requirements | Work packages | Engineering practices | Objectives | Research questions | Planned evidence |",
        "|---|---|---|---|---|---|---|",
    ]

    for task in catalogue["tasks"]:
        relationships = task["relationships"]
        evidence = md_link("Open", task["evidence_path"], output) if task["evidence_path"] else "—"
        lines.append(
            "| "
            + " | ".join(
                [
                    f"`{task['master_key']}`",
                    markdown_escape(", ".join(relationships["requirements"])),
                    markdown_escape(", ".join(relationships["work_packages"])),
                    markdown_escape(", ".join(relationships["engineering_practices"])),
                    markdown_escape(", ".join(relationships["objectives"])),
                    markdown_escape(", ".join(relationships["research_questions"])),
                    evidence,
                ]
            )
            + " |"
        )
    return "\n".join(lines)


def grouped_task_document(
    catalogue: dict[str, Any], output: Path, grouping_key: str, title: str
) -> str:
    """Group active tasks by phase, deadline or validation state."""

    groups: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for task in catalogue["tasks"]:
        value = task.get(grouping_key)
        group = str(value).strip() if value not in (None, "") else "Unspecified"
        groups[group].append(task)

    lines = [GENERATED_NOTICE, f"# {title}", ""]
    active_catalogue = output.parent / "Active-Task-Catalogue.md"

    def sort_group(value: str) -> tuple[int, str]:
        if grouping_key == "deadline":
            return (0 if re_date(value) else 1, value)
        return (0, value)

    for group in sorted(groups, key=sort_group):
        lines.extend(
            [
                f"## {group}",
                "",
                "| ID | Task | Type | Status | Planned evidence |",
                "|---|---|---|---|---|",
            ]
        )
        for task in sorted(groups[group], key=lambda item: item["task_number"]):
            evidence = md_link("Open", task["evidence_path"], output) if task["evidence_path"] else "—"
            lines.append(
                "| "
                + " | ".join(
                    [
                        internal_task_link(task, output, active_catalogue),
                        markdown_escape(task["title"]),
                        markdown_escape(task["task_type_label"]),
                        markdown_escape(task["effective_status"]),
                        evidence,
                    ]
                )
                + " |"
            )
        lines.append("")
    return "\n".join(lines)


def re_date(value: str) -> bool:
    """Return True for a simple ISO date string."""

    try:
        date.fromisoformat(value)
        return True
    except ValueError:
        return False


def validation_pending_document(catalogue: dict[str, Any], output: Path) -> str:
    """List implemented or partially verified tasks that still need validation."""

    tasks = [
        task
        for task in catalogue["tasks"]
        if task["validation"] != "Validated"
        and task["working_status"] in {"Implemented", "Partially Verified", "In Progress"}
    ]
    active_catalogue = output.parent / "Active-Task-Catalogue.md"
    lines = [
        GENERATED_NOTICE,
        "# Validation Pending",
        "",
        "These tasks have work underway or implemented artefacts but are not formally validated in the controlled RTM.",
        "",
        "| ID | Task | Working status | Definition of Done | Planned evidence |",
        "|---|---|---|---|---|",
    ]
    for task in sorted(tasks, key=lambda item: item["task_number"]):
        evidence = md_link("Open", task["evidence_path"], output) if task["evidence_path"] else "—"
        lines.append(
            "| "
            + " | ".join(
                [
                    internal_task_link(task, output, active_catalogue),
                    markdown_escape(task["title"]),
                    markdown_escape(task["working_status"]),
                    markdown_escape(task["definition_of_done"]),
                    evidence,
                ]
            )
            + " |"
        )
    return "\n".join(lines)


def issue_plan_document(catalogue: dict[str, Any], output: Path) -> str:
    """Generate the reviewed plan for creating one GitHub issue per work package."""

    tasks = [task for task in catalogue["tasks"] if task["task_type"] == "work-package"]
    lines = [
        GENERATED_NOTICE,
        "# Work-Package GitHub Issue Plan",
        "",
        "This file is a reviewable plan. It does not create issues. After the catalogue is approved, the issue-creation script can create one issue per work package while requirements and engineering practices remain linked catalogue records.",
        "",
        "| WP ID | Suggested issue title | Phase | Priority | Requirements | Engineering practices | Planned evidence |",
        "|---|---|---|---|---|---|---|",
    ]
    for task in tasks:
        relationships = task["relationships"]
        evidence = md_link("Open", task["evidence_path"], output) if task["evidence_path"] else "—"
        lines.append(
            "| "
            + " | ".join(
                [
                    f"`{task['source_id']}`",
                    markdown_escape(f"[{task['source_id']}] {task['title']}"),
                    markdown_escape(task["release_role_or_phase"]),
                    markdown_escape(task["priority"]),
                    markdown_escape(", ".join(relationships["requirements"])),
                    markdown_escape(", ".join(relationships["engineering_practices"])),
                    evidence,
                ]
            )
            + " |"
        )
    return "\n".join(lines)


def main() -> int:
    """Generate every human-readable traceability view."""

    args = parse_arguments()
    repository_root = args.repository_root.resolve()
    catalogue_path = repository_root / "docs" / "traceability" / "data" / "task-catalogue.json"
    output_directory = repository_root / "docs" / "traceability" / "generated"
    catalogue = load_json(catalogue_path)

    outputs = {
        "Active-Task-Catalogue.md": task_catalogue_document,
        "Requirement-Catalogue.md": lambda value, path: requirement_catalogue_document(value, path, True),
        "Full-Requirement-Baseline.md": lambda value, path: requirement_catalogue_document(value, path, False),
        "Work-Package-Catalogue.md": lambda value, path: task_type_catalogue(value, path, "work-package", "Work-Package Catalogue"),
        "Engineering-Practice-Catalogue.md": lambda value, path: task_type_catalogue(value, path, "engineering-practice", "Engineering-Practice Catalogue"),
        "Task-Relationship-Matrix.md": relationship_matrix_document,
        "Tasks-By-Phase.md": lambda value, path: grouped_task_document(value, path, "release_role_or_phase", "Tasks by Phase or Release Role"),
        "Tasks-By-Deadline.md": lambda value, path: grouped_task_document(value, path, "deadline", "Tasks by Deadline"),
        "Validation-Pending.md": validation_pending_document,
        "Work-Package-Issue-Plan.md": issue_plan_document,
    }

    for filename, builder in outputs.items():
        output = output_directory / filename
        ensure_text(output, builder(catalogue, output))
        print(f"Generated: {output}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
