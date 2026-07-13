"""Shared helpers for exporting and validating the IXN traceability catalogue.

The implementation deliberately uses only Python's standard library so that the
repository does not require Excel, the ImportExcel PowerShell module, pandas or
openpyxl. An .xlsx file is a ZIP package containing XML documents, so the code
below reads the required sheets directly from that package.
"""

from __future__ import annotations

import hashlib
import json
import re
import zipfile
from collections import defaultdict
from dataclasses import dataclass
from datetime import date, datetime, timedelta
from pathlib import Path
from typing import Any, Iterable
from xml.etree import ElementTree as ET


# Excel's Windows date system uses 1899-12-30 as the practical conversion base.
EXCEL_DATE_BASE = datetime(1899, 12, 30)

# XML namespaces used by the Open XML workbook format.
MAIN_NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
REL_NS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
PKG_REL_NS = "http://schemas.openxmlformats.org/package/2006/relationships"
NS = {"m": MAIN_NS, "r": REL_NS, "p": PKG_REL_NS}


@dataclass(frozen=True)
class WorkbookSheet:
    """A worksheet name and the ZIP member that stores its XML."""

    name: str
    path: str


def sha256_file(path: Path) -> str:
    """Return a lowercase SHA-256 digest for a file."""

    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def excel_serial_to_iso(value: Any) -> str | None:
    """Convert a cached Excel serial date to YYYY-MM-DD when possible."""

    if value in (None, ""):
        return None

    if isinstance(value, str):
        text = value.strip()
        if not text:
            return None
        # Already-normalized ISO date values are preserved.
        if re.fullmatch(r"\d{4}-\d{2}-\d{2}", text):
            return text
        try:
            numeric = float(text)
        except ValueError:
            return text
    elif isinstance(value, (int, float)):
        numeric = float(value)
    else:
        return str(value)

    converted = EXCEL_DATE_BASE + timedelta(days=numeric)
    return converted.date().isoformat()


def split_ids(value: Any) -> list[str]:
    """Split semicolon/comma-separated traceability IDs into a stable list."""

    if value in (None, ""):
        return []

    text = str(value).strip()
    if not text:
        return []

    # Semicolons are the workbook's normal separator. Commas are accepted for
    # resilience, while slashes inside descriptive text are left untouched.
    parts = re.split(r"\s*[;,]\s*", text)
    return [part.strip() for part in parts if part.strip()]




def expand_research_questions(values: list[str], question_ids: Iterable[str]) -> list[str]:
    """Expand workbook aliases such as ``All RQs`` into concrete IDs."""

    concrete = list(question_ids)
    expanded: list[str] = []
    for value in values:
        if value == "All RQs":
            expanded.extend(concrete)
        else:
            expanded.append(value)
    return sorted(dict.fromkeys(expanded))


def expand_work_package_references(
    values: list[str], work_package_ids: Iterable[str]
) -> tuple[list[str], list[str]]:
    """Expand workbook work-package groups and return IDs plus group labels."""

    all_ids = list(work_package_ids)
    groups: list[str] = []
    expanded: list[str] = []

    for value in values:
        if value == "All WPs":
            groups.append(value)
            expanded.extend(all_ids)
        elif value == "All implementation WPs":
            groups.append(value)
            expanded.extend(
                item
                for item in all_ids
                if item.split("-", 1)[0] in {"IM", "DL", "HE", "RT", "QX", "OV", "TV", "FR"}
            )
        elif value == "All runtime WPs":
            groups.append(value)
            expanded.extend(
                item for item in all_ids if item.split("-", 1)[0] in {"RT", "QX", "OV"}
            )
        else:
            expanded.append(value)

    return sorted(dict.fromkeys(expanded)), sorted(dict.fromkeys(groups))

def slugify_id(value: str) -> str:
    """Create a predictable lowercase HTML anchor from a stable project ID."""

    return re.sub(r"[^a-z0-9]+", "-", value.lower()).strip("-")


def markdown_escape(value: Any) -> str:
    """Escape text for safe use inside a Markdown table cell."""

    if value in (None, ""):
        return "—"
    return str(value).replace("|", "\\|").replace("\n", "<br>")


def normalize_path(value: Any) -> str | None:
    """Normalize repository-style evidence paths without touching URLs."""

    if value in (None, ""):
        return None
    text = str(value).strip().replace("\\", "/")
    return text or None


def _column_index(cell_reference: str) -> int:
    """Convert an Excel column reference such as AJ into a zero-based index."""

    letters = "".join(character for character in cell_reference if character.isalpha())
    index = 0
    for character in letters.upper():
        index = index * 26 + (ord(character) - ord("A") + 1)
    return index - 1


def _read_shared_strings(archive: zipfile.ZipFile) -> list[str]:
    """Read the workbook's shared-string table when one exists."""

    try:
        xml = archive.read("xl/sharedStrings.xml")
    except KeyError:
        return []

    root = ET.fromstring(xml)
    strings: list[str] = []
    for item in root.findall("m:si", NS):
        fragments = [node.text or "" for node in item.findall(".//m:t", NS)]
        strings.append("".join(fragments))
    return strings


def _discover_sheets(archive: zipfile.ZipFile) -> list[WorkbookSheet]:
    """Resolve worksheet names to their XML paths inside the XLSX package."""

    workbook_root = ET.fromstring(archive.read("xl/workbook.xml"))
    relationships_root = ET.fromstring(archive.read("xl/_rels/workbook.xml.rels"))

    relationship_targets: dict[str, str] = {}
    for relation in relationships_root.findall("p:Relationship", NS):
        relation_id = relation.attrib.get("Id", "")
        target = relation.attrib.get("Target", "")
        relationship_targets[relation_id] = target

    sheets: list[WorkbookSheet] = []
    for sheet in workbook_root.findall("m:sheets/m:sheet", NS):
        name = sheet.attrib["name"]
        relation_id = sheet.attrib[f"{{{REL_NS}}}id"]
        target = relationship_targets[relation_id]
        normalized = target.lstrip("/")
        if not normalized.startswith("xl/"):
            normalized = f"xl/{normalized}"
        sheets.append(WorkbookSheet(name=name, path=normalized))
    return sheets


def read_xlsx(path: Path) -> dict[str, list[list[Any]]]:
    """Read every worksheet into a list-of-rows representation."""

    with zipfile.ZipFile(path) as archive:
        shared_strings = _read_shared_strings(archive)
        sheets = _discover_sheets(archive)
        result: dict[str, list[list[Any]]] = {}

        for sheet in sheets:
            root = ET.fromstring(archive.read(sheet.path))
            output_rows: list[list[Any]] = []

            for row_node in root.findall("m:sheetData/m:row", NS):
                values_by_column: dict[int, Any] = {}
                highest_column = -1

                for cell in row_node.findall("m:c", NS):
                    reference = cell.attrib.get("r", "A1")
                    column = _column_index(reference)
                    highest_column = max(highest_column, column)
                    cell_type = cell.attrib.get("t")

                    if cell_type == "inlineStr":
                        text_nodes = cell.findall(".//m:t", NS)
                        value: Any = "".join(node.text or "" for node in text_nodes)
                    else:
                        value_node = cell.find("m:v", NS)
                        raw = value_node.text if value_node is not None else None

                        if raw is None:
                            value = None
                        elif cell_type == "s":
                            value = shared_strings[int(raw)]
                        elif cell_type == "b":
                            value = raw == "1"
                        elif cell_type in {"str", "e"}:
                            value = raw
                        else:
                            try:
                                numeric = float(raw)
                                value = int(numeric) if numeric.is_integer() else numeric
                            except ValueError:
                                value = raw

                    values_by_column[column] = value

                if highest_column < 0:
                    output_rows.append([])
                    continue

                row_values = [None] * (highest_column + 1)
                for column, value in values_by_column.items():
                    row_values[column] = value
                output_rows.append(row_values)

            result[sheet.name] = output_rows

    return result


def find_header_row(rows: list[list[Any]], first_header: str) -> tuple[int, list[str]]:
    """Find a sheet header row using its first expected column heading."""

    for index, row in enumerate(rows):
        if row and str(row[0]).strip() == first_header:
            headers = [str(value).strip() if value is not None else "" for value in row]
            return index, headers
    raise ValueError(f"Could not find header row beginning with {first_header!r}.")


def sheet_records(rows: list[list[Any]], first_header: str) -> list[dict[str, Any]]:
    """Convert a worksheet table into dictionaries, skipping blank ID rows."""

    header_index, headers = find_header_row(rows, first_header)
    records: list[dict[str, Any]] = []

    for row in rows[header_index + 1 :]:
        padded = list(row) + [None] * max(0, len(headers) - len(row))
        record = {header: padded[index] for index, header in enumerate(headers) if header}
        if record.get(first_header) not in (None, ""):
            records.append(record)
    return records


def parse_objectives(rows: list[list[Any]]) -> tuple[dict[str, dict[str, str]], dict[str, dict[str, str]]]:
    """Read objectives and research questions from the combined worksheet."""

    objectives: dict[str, dict[str, str]] = {}
    questions: dict[str, dict[str, str]] = {}
    mode: str | None = None

    for row in rows:
        first = str(row[0]).strip() if row and row[0] is not None else ""
        if first == "Objective ID":
            mode = "objective"
            continue
        if first == "RQ ID":
            mode = "question"
            continue
        if not first or first in {"Project Objectives and Research Questions"}:
            continue

        title = str(row[1]).strip() if len(row) > 1 and row[1] is not None else ""
        statement = str(row[2]).strip() if len(row) > 2 and row[2] is not None else ""

        if mode == "objective" and re.fullmatch(r"O\d+", first):
            objectives[first] = {"title": title, "statement": statement}
        elif mode == "question" and first.startswith("RQ"):
            questions[first] = {"title": title, "statement": statement}

    return objectives, questions


def task_type_slug(task_type: str) -> str:
    """Normalize workbook task types to stable machine-readable values."""

    mapping = {
        "Requirement": "requirement",
        "Work Package": "work-package",
        "Engineering Practice": "engineering-practice",
    }
    return mapping.get(task_type, slugify_id(task_type))


def source_details_for_task(
    task: dict[str, Any],
    requirements: dict[str, dict[str, Any]],
    work_packages: dict[str, dict[str, Any]],
    practices: dict[str, dict[str, Any]],
) -> dict[str, Any]:
    """Return the detailed source-row fields relevant to a task."""

    source_id = str(task["Source ID"])
    task_type = str(task["Task Type"])

    if task_type == "Requirement":
        source = requirements[source_id]
        return {
            "category": source.get("Category"),
            "priority": source.get("Priority"),
            "release_role": source.get("Release Role"),
            "lifecycle": source.get("Lifecycle"),
            "baseline_version": source.get("Baseline Version"),
            "requirement": source.get("Requirement"),
            "rationale": source.get("Rationale"),
            "source": source.get("Source"),
            "objectives": split_ids(source.get("Objective(s)")),
            "research_questions": split_ids(source.get("Research Question(s)")),
            "workflows": split_ids(source.get("Workflow(s)")),
            "work_packages": split_ids(source.get("Work Package(s)")),
            "planned_components": split_ids(source.get("Planned Component(s)")),
            "dependencies": split_ids(source.get("Dependencies")),
            "acceptance_criteria": source.get("Acceptance Criteria"),
            "verification_method": source.get("Verification Method"),
            "test_evidence_id": source.get("Test/Evidence ID"),
            "existing_evidence": source.get("Existing Evidence"),
            "owner": source.get("Owner"),
            "confidence": source.get("Confidence"),
            "report_section": source.get("Report Section"),
            "previous_or_replacement_id": source.get("Previous/Replacement ID"),
            "notes": source.get("Notes / Next Action"),
            "traceability_complete": source.get("Traceability Complete?"),
        }

    if task_type == "Work Package":
        source = work_packages[source_id]
        return {
            "phase": source.get("Phase"),
            "priority": source.get("Priority"),
            "title": source.get("Work Package"),
            "planned_start": excel_serial_to_iso(source.get("Start")),
            "planned_end": excel_serial_to_iso(source.get("End")),
            "planned_hours": source.get("Planned Hours"),
            "complexity": source.get("Complexity"),
            "dependencies_or_reused_work": source.get("Dependencies / Reused Work"),
            "definition_of_done": source.get("Definition of Done"),
            "report_use": source.get("Report Use"),
            "evidence_output": source.get("Evidence Output"),
            "notes": source.get("Notes / Link"),
        }

    source = practices[source_id]
    return {
        "phase": source.get("Phase"),
        "title": source.get("Engineering Practice"),
        "priority": source.get("Priority"),
        "work_packages": split_ids(source.get("Work Package(s)")),
        "current_finding_or_next_step": source.get("Current Finding / Next Step"),
        "evidence_or_report_link": normalize_path(source.get("Evidence / Report Link")),
    }


def build_catalogue(workbook_path: Path, generated_on: date) -> dict[str, Any]:
    """Build the normalized traceability catalogue from the workbook."""

    sheets = read_xlsx(workbook_path)

    requirement_rows = sheet_records(sheets["Requirements"], "Requirement ID")
    work_package_rows = sheet_records(sheets["Work Packages"], "WP ID")
    practice_rows = sheet_records(sheets["Engineering Practices"], "Practice ID")
    task_rows = sheet_records(sheets["Task Checklist"], "Task No.")
    objectives, questions = parse_objectives(sheets["Objectives & RQs"])

    requirements = {str(row["Requirement ID"]): row for row in requirement_rows}
    work_packages = {str(row["WP ID"]): row for row in work_package_rows}
    practices = {str(row["Practice ID"]): row for row in practice_rows}
    work_package_ids = list(work_packages)
    question_ids = list(questions)

    requirements_by_wp: dict[str, list[str]] = defaultdict(list)
    for requirement_id, requirement in requirements.items():
        if requirement.get("Lifecycle") != "Active":
            continue
        allocated_work_packages, _ = expand_work_package_references(
            split_ids(requirement.get("Work Package(s)")), work_package_ids
        )
        for work_package_id in allocated_work_packages:
            requirements_by_wp[work_package_id].append(requirement_id)

    practices_by_wp: dict[str, list[str]] = defaultdict(list)
    for practice_id, practice in practices.items():
        allocated_work_packages, _ = expand_work_package_references(
            split_ids(practice.get("Work Package(s)")), work_package_ids
        )
        for work_package_id in allocated_work_packages:
            practices_by_wp[work_package_id].append(practice_id)

    normalized_tasks: list[dict[str, Any]] = []
    for source_task in task_rows:
        master_key = str(source_task["Master Key"]).strip()
        source_id = str(source_task["Source ID"]).strip()
        task_type = str(source_task["Task Type"]).strip()
        raw_work_package_references = split_ids(source_task.get("Work Package(s)"))
        task_work_package_ids, work_package_groups = expand_work_package_references(
            raw_work_package_references, work_packages.keys()
        )

        if task_type == "Requirement":
            related_requirements = [source_id]
            related_work_packages = task_work_package_ids
            related_practices = sorted(
                {practice for wp in related_work_packages for practice in practices_by_wp.get(wp, [])}
            )
            source_requirement = requirements[source_id]
            related_objectives = split_ids(source_requirement.get("Objective(s)"))
            related_questions = expand_research_questions(split_ids(source_requirement.get("Research Question(s)")), question_ids)
        elif task_type == "Work Package":
            related_work_packages = [source_id]
            related_requirements = sorted(requirements_by_wp.get(source_id, []))
            related_practices = sorted(practices_by_wp.get(source_id, []))
            related_objectives = sorted(
                {
                    objective
                    for requirement_id in related_requirements
                    for objective in split_ids(requirements[requirement_id].get("Objective(s)"))
                }
            )
            related_questions = sorted(
                {
                    question
                    for requirement_id in related_requirements
                    for question in expand_research_questions(split_ids(requirements[requirement_id].get("Research Question(s)")), question_ids)
                }
            )
        else:
            related_practices = [source_id]
            related_work_packages = task_work_package_ids
            related_requirements = sorted(
                {
                    requirement
                    for wp in related_work_packages
                    for requirement in requirements_by_wp.get(wp, [])
                }
            )
            related_objectives = sorted(
                {
                    objective
                    for requirement_id in related_requirements
                    for objective in split_ids(requirements[requirement_id].get("Objective(s)"))
                }
            )
            related_questions = sorted(
                {
                    question
                    for requirement_id in related_requirements
                    for question in expand_research_questions(split_ids(requirements[requirement_id].get("Research Question(s)")), question_ids)
                }
            )

        deadline = excel_serial_to_iso(source_task.get("Deadline"))
        days_remaining: int | None = None
        if deadline:
            try:
                days_remaining = (date.fromisoformat(deadline) - generated_on).days
            except ValueError:
                days_remaining = None

        normalized_tasks.append(
            {
                "task_number": int(source_task["Task No."]),
                "master_key": master_key,
                "anchor": slugify_id(master_key),
                "source_id": source_id,
                "source_tab": source_task.get("Source Tab"),
                "task_type": task_type_slug(task_type),
                "task_type_label": task_type,
                "title": source_task.get("Task to Complete"),
                "priority": source_task.get("Priority"),
                "release_role_or_phase": source_task.get("Release Role / Phase"),
                "work_packages": task_work_package_ids,
                "work_package_groups": work_package_groups,
                "deadline": deadline,
                "days_remaining_at_generation": days_remaining,
                "deadline_state": source_task.get("Deadline State"),
                "working_status": source_task.get("Working Status"),
                "validation": source_task.get("Validation"),
                "effective_status": source_task.get("Effective Status"),
                "evidence_path": normalize_path(source_task.get("Evidence / Link")),
                "validation_date": excel_serial_to_iso(source_task.get("Validation Date")),
                "definition_of_done": source_task.get("Next Action / Definition of Done"),
                "relationships": {
                    "requirements": related_requirements,
                    "work_packages": related_work_packages,
                    "engineering_practices": related_practices,
                    "objectives": related_objectives,
                    "research_questions": related_questions,
                },
                "source_details": source_details_for_task(
                    source_task, requirements, work_packages, practices
                ),
            }
        )

    normalized_requirements: list[dict[str, Any]] = []
    for source in requirement_rows:
        requirement_id = str(source["Requirement ID"])
        normalized_requirements.append(
            {
                "requirement_id": requirement_id,
                "anchor": slugify_id(f"REQ:{requirement_id}"),
                "category": source.get("Category"),
                "priority": source.get("Priority"),
                "release_role": source.get("Release Role"),
                "lifecycle": source.get("Lifecycle"),
                "baseline_version": source.get("Baseline Version"),
                "requirement": source.get("Requirement"),
                "rationale": source.get("Rationale"),
                "source": source.get("Source"),
                "objectives": split_ids(source.get("Objective(s)")),
                "research_questions": expand_research_questions(
                    split_ids(source.get("Research Question(s)")), question_ids
                ),
                "workflows": split_ids(source.get("Workflow(s)")),
                "work_packages": expand_work_package_references(
                    split_ids(source.get("Work Package(s)")), work_package_ids
                )[0],
                "planned_components": split_ids(source.get("Planned Component(s)")),
                "dependencies": split_ids(source.get("Dependencies")),
                "acceptance_criteria": source.get("Acceptance Criteria"),
                "verification_method": source.get("Verification Method"),
                "test_evidence_id": source.get("Test/Evidence ID"),
                "existing_evidence": source.get("Existing Evidence"),
                "planned_evidence_path": normalize_path(source.get("Planned Evidence Path")),
                "target_date": excel_serial_to_iso(source.get("Target Date")),
                "owner": source.get("Owner"),
                "status": source.get("Status"),
                "progress": source.get("Progress %"),
                "confidence": source.get("Confidence"),
                "github_issue": source.get("GitHub Issue"),
                "pr_or_commit": source.get("PR / Commit"),
                "report_section": source.get("Report Section"),
                "previous_or_replacement_id": source.get("Previous/Replacement ID"),
                "notes": source.get("Notes / Next Action"),
                "last_updated": excel_serial_to_iso(source.get("Last Updated")),
                "traceability_complete": source.get("Traceability Complete?"),
            }
        )

    metadata = {
        "catalogue_version": "1.3",
        "schema_version": "1.0",
        "generated_on": generated_on.isoformat(),
        "generated_from": workbook_path.name,
        "source_sha256": sha256_file(workbook_path),
        "source_sheets": [
            "Task Checklist",
            "Requirements",
            "Work Packages",
            "Engineering Practices",
            "Objectives & RQs",
        ],
        "counts": {
            "active_tasks": len(normalized_tasks),
            "requirements_all_lifecycles": len(normalized_requirements),
            "requirements_active": sum(
                1 for requirement in normalized_requirements if requirement["lifecycle"] == "Active"
            ),
            "work_packages": len(work_package_rows),
            "engineering_practices": len(practice_rows),
            "objectives": len(objectives),
            "research_questions": len(questions),
        },
    }

    return {
        "metadata": metadata,
        "objectives": objectives,
        "research_questions": questions,
        "tasks": normalized_tasks,
        "requirements_baseline": normalized_requirements,
    }


def write_json(path: Path, value: Any) -> None:
    """Write stable, readable UTF-8 JSON with a trailing newline."""

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def load_json(path: Path) -> Any:
    """Load UTF-8 JSON from disk."""

    return json.loads(path.read_text(encoding="utf-8"))


def repository_relative_link(from_file: Path, target: str | None) -> str:
    """Build a relative Markdown link from one repository document to another."""

    if not target:
        return "—"
    if re.match(r"^[a-z]+://", target, re.IGNORECASE):
        return target

    import os

    source_file = from_file.resolve()
    source_parent = source_file.parent
    target_path = Path(target.rstrip("/"))

    if target_path.is_absolute():
        absolute_target = target_path
    else:
        # Generated documents live under <repo>/docs. Resolve repository paths
        # against that root rather than against the process working directory.
        parts = source_file.parts
        if "docs" in parts:
            docs_index = parts.index("docs")
            repository_root = Path(*parts[:docs_index])
        else:
            repository_root = source_parent
        absolute_target = repository_root / target_path

    return os.path.relpath(absolute_target, source_parent).replace("\\", "/")


def ensure_text(path: Path, content: str) -> None:
    """Write normalized UTF-8 text and create parent directories."""

    path.parent.mkdir(parents=True, exist_ok=True)
    normalized = content.replace("\r\n", "\n").rstrip() + "\n"
    path.write_text(normalized, encoding="utf-8")


def chunked(values: Iterable[Any], size: int) -> Iterable[list[Any]]:
    """Yield fixed-size lists from an iterable."""

    batch: list[Any] = []
    for value in values:
        batch.append(value)
        if len(batch) == size:
            yield batch
            batch = []
    if batch:
        yield batch
