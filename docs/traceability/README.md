# Repository Traceability System

This directory makes the RTM tasks and their IDs understandable inside the repository.

The controlled Excel RTM remains authoritative for editable status, validation and dashboard calculations. The files here are a generated, reviewable repository snapshot so developers can search for an ID, understand the task, follow its relationships and open its planned evidence location without opening Excel.

## Start here

- [ID conventions](ID-Conventions.md) — explains what IDs such as `REQ:G-M01`, `WP:PD-01` and `EP:EP-001` mean.
- [Source-of-truth rules](Source-of-Truth-Rules.md) — explains which system controls task definitions, status, evidence and day-to-day work.
- [Active task catalogue](generated/Active-Task-Catalogue.md) — all 165 active tasks with statements, status, Definition of Done and traceability.
- [Task relationship matrix](generated/Task-Relationship-Matrix.md) — cross-links requirements, work packages, engineering practices, objectives and research questions.
- [Full requirement baseline](generated/Full-Requirement-Baseline.md) — all 111 requirement records, including Active, Deferred, Superseded and Excluded history.
- [Work-package issue plan](generated/Work-Package-Issue-Plan.md) — the reviewed plan for creating one operational GitHub issue per work package.

## What each layer does

| Layer | Purpose | Authoritative? |
|---|---|---|
| RTM workbook | Editable definitions, working status, validation, deadlines and dashboard calculations | Yes |
| `task-catalogue.json` | Machine-readable repository snapshot generated from the RTM | Generated snapshot |
| Generated Markdown | Human-readable repository views | Generated snapshot |
| Evidence files and folders | Proof that a task satisfies its Definition of Done or acceptance criteria | Yes for the evidence claim |
| GitHub issues | Assignment, discussion and execution tracking for work packages | Operational only |
| Pull requests and commits | Reviewed implementation history | Yes for repository changes |

## Generated files

Do not edit files in `data/` or `generated/` by hand, except the JSON schema.

To update the snapshot:

```powershell
# Run from the repository root and replace the workbook path with the controlled RTM file.
.\scripts\traceability\Update-Traceability.ps1 `
    -WorkbookPath "C:\Path\To\IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.xlsx" `
    -RepositoryPath .
```

The update process:

1. reads the RTM workbook without requiring Excel or third-party Python packages;
2. exports a normalized JSON catalogue;
3. generates all Markdown views;
4. checks task counts, IDs, relationships, evidence paths and generated files;
5. stops with an error when the snapshot is incomplete or inconsistent.

## Current snapshot

| Measure | Value |
|---|---:|
| Active tasks | 165 |
| Active requirements | 76 |
| All requirement lifecycle records | 111 |
| Work packages | 49 |
| Engineering practices | 40 |
| Objectives | 11 |
| Research questions | 5 |

## Important rule

A GitHub issue being closed does not automatically make a requirement Verified.

A task becomes Verified only when:

1. the implementation or required deliverable exists;
2. the planned evidence exists;
3. the Definition of Done or acceptance criteria are checked;
4. Validation is set to `Validated` in the controlled RTM.
