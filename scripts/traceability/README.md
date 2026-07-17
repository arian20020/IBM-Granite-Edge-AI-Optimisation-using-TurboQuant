# Traceability Export and Validation Scripts

These scripts convert the controlled Excel RTM into searchable repository documentation.

They use only Python's standard library. Excel, pandas, openpyxl and PowerShell ImportExcel are not required.

## Files

| File | Purpose |
|---|---|
| `traceability_common.py` | Reads XLSX XML, normalizes IDs, builds relationships and writes stable files. |
| `export_task_catalogue.py` | Exports the workbook into `task-catalogue.json`. |
| `generate_traceability_documents.py` | Creates the human-readable Markdown catalogues. |
| `validate_task_catalogue.py` | Checks counts, duplicate IDs, relationships, evidence paths and generated outputs. |
| `Update-Traceability.ps1` | Beginner-friendly PowerShell entry point that runs the complete pipeline. |
| `Create-WorkPackageIssues.ps1` | Optional reviewed step for creating one GitHub issue per work package. |

## Normal update

Run from the repository root:

```powershell
.\scripts\traceability\Update-Traceability.ps1 `
    -WorkbookPath "C:\Path\To\IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.xlsx" `
    -RepositoryPath .
```

Review the generated changes:

```powershell
git status --short
git diff -- docs/traceability
```

## Strict evidence-path validation

Use this after all expected evidence directories have been created:

```powershell
.\scripts\traceability\Update-Traceability.ps1 `
    -WorkbookPath "C:\Path\To\IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.xlsx" `
    -RepositoryPath . `
    -CheckEvidencePaths
```

## Direct Python commands

```powershell
py -3 .\scripts\traceability\export_task_catalogue.py `
    --workbook "C:\Path\To\RTM.xlsx" `
    --repository-root . `
    --generated-on 2026-07-14

py -3 .\scripts\traceability\generate_traceability_documents.py `
    --repository-root .

py -3 .\scripts\traceability\validate_task_catalogue.py `
    --repository-root .
```

## Determinism

Given the same workbook bytes and the same `--generated-on` date, the scripts produce the same catalogue and Markdown output. This keeps pull-request diffs reviewable.

## Work-package issues

Do not create 165 issues. Requirements and engineering practices remain linked catalogue records. The optional issue script creates at most one issue for each of the 49 work packages and defaults to preview mode.
