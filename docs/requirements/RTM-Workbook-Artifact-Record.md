# Controlled RTM Workbook Artifact Record

**Record ID:** `ART-RTM-XLSX-001`  
**Artefact:** `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Categorised.xlsx`  
**Baseline:** RTM v1.3 / MoSCoW v1.2  
**Owner:** Arian B  
**Evidence date:** 2026-07-14  
**Repository status:** Human-readable exports, checksum and control record are version-controlled; direct binary placement remains pending  

## Purpose

This record controls the exact Excel workbook used to produce the reviewed RTM v1.3 baseline and categorised requirement views. It prevents repository evidence records from linking to a non-existent binary file.

## Integrity

| Field | Value |
|---|---|
| Filename | `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Categorised.xlsx` |
| SHA-256 | `1ebb8e25a1c624af805d98228c37b09933363c53e2fab790809abe74e863b588` |
| Size | 192,564 bytes |
| Controlled package | `MoSCoW_Categorised_Completion_Package_2026-07-14.zip` supplied to the project owner |
| Human-readable RTM | [Requirements-Traceability-Matrix-v1.3.md](Requirements-Traceability-Matrix-v1.3.md) |
| Reverse indexes | [Traceability-Reverse-Indexes-v1.3.md](Traceability-Reverse-Indexes-v1.3.md) |
| Workbook guidance | [RTM-Working-Baseline-README.md](RTM-Working-Baseline-README.md) |
| Checksum file | [RTM-Working-Baseline-SHA256.txt](RTM-Working-Baseline-SHA256.txt) |

## Reviewed contents

The controlled workbook contains:

- 111 requirement lifecycle records;
- 72 active requirements;
- 56 active Must Haves;
- 14 active Should Haves;
- 2 active Could Haves;
- 11 Deferred records;
- 10 Superseded records;
- 18 Excluded records;
- the numbered Task Checklist and Dashboard;
- separate Functional, Non-Functional, Research, Governance and Exclusion views;
- zero duplicate requirement IDs;
- zero active Must Haves missing required planning fields;
- zero detected spreadsheet calculation errors.

## Recovery and placement

The exact workbook must be recovered from the controlled completion package and copied to:

```text
docs/requirements/IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Categorised.xlsx
```

Verify it before use:

```powershell
$Path = 'docs/requirements/IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Categorised.xlsx'
$Expected = '1ebb8e25a1c624af805d98228c37b09933363c53e2fab790809abe74e863b588'
$Actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
if ($Actual -ne $Expected) {
    throw "RTM workbook checksum mismatch. Expected $Expected but found $Actual."
}
Write-Host 'RTM workbook checksum verified.' -ForegroundColor Green
```

## Evidence boundary

The Markdown RTM and reverse indexes are version-controlled and reviewable in Git. The binary workbook is independently controlled by filename, size, package and SHA-256. Until the exact binary is copied into Git, evidence records must link to this artifact record rather than falsely claiming that the `.xlsx` already exists in the repository.

## Change control

Replacing or editing the workbook requires:

1. a new approved change record;
2. a new workbook version or clearly recorded revision;
3. an updated SHA-256 and size;
4. regenerated Markdown RTM and reverse indexes;
5. revalidation of affected evidence records and Dashboard counts.