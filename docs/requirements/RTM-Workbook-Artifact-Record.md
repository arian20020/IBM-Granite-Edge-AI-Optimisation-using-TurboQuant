# Controlled RTM Workbook Artifact Record

**Record ID:** `ART-RTM-XLSX-001`  
**Artefact:** `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.1_RACL_Validated.xlsx`  
**Baseline:** RTM v1.3.1 / MoSCoW v1.2  
**Owner:** Arian B  
**Evidence date:** 2026-07-15  
**Repository status:** Human-readable controls, checksum and artifact record are version-controlled; the exact binary is independently controlled outside Git.

## Purpose

This record controls the exact Excel workbook used to validate the current Task Checklist and its linked Requirements, Work Packages and Engineering Practices views.

## Integrity

| Field | Value |
|---|---|
| Filename | `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.1_RACL_Validated.xlsx` |
| SHA-256 | `2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96` |
| Size | 120,290 bytes |
| Controlled copy | Supplied to the project owner on 2026-07-15 |
| Checksum file | [RTM-Working-Baseline-SHA256.txt](RTM-Working-Baseline-SHA256.txt) |

## Validated status changes

The workbook records these rows as `Implemented / Validated / Verified`:

- `REQ:G-M05`;
- `WP:PD-05`;
- `EP:EP-007`.

The workbook also synchronises corresponding detailed-tab rows, the Evidence Register, Change Log, Dashboard and Status History. Yellow-highlighted work remains scheduled after implementation and is not treated as currently overdue.

## Workbook structure and checks

The reviewed workbook contains 11 worksheets:

- Lists;
- Requirements;
- Work Packages;
- Objectives & RQs;
- Engineering Practices;
- Evidence Register;
- Dashboard;
- Status History;
- Change Log;
- Sources & Guidance;
- Task Checklist.

Checks completed:

- relevant Task Checklist and detailed-tab statuses agree;
- evidence paths for G-M05, PD-05 and EP-007 are present;
- Status History contains a fixed RACL validation snapshot;
- no detected `#REF!`, `#DIV/0!`, `#VALUE!`, `#NAME?` or `#N/A` errors;
- workbook opens and exports successfully.

## Verification command

```powershell
$Path = 'IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.1_RACL_Validated.xlsx'
$Expected = '2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96'
$Actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
if ($Actual -ne $Expected) {
    throw "RTM workbook checksum mismatch. Expected $Expected but found $Actual."
}
Write-Host 'RTM workbook checksum verified.' -ForegroundColor Green
```

## Evidence boundary

The workbook is the authoritative editable status source. The exact binary is controlled by filename, size and SHA-256 even though it is not stored directly in Git. Evidence records may rely on this artifact record without claiming that the binary exists in the repository.

## Change control

A later workbook edit requires a new revision, checksum, size, change-log entry and revalidation of affected evidence records.