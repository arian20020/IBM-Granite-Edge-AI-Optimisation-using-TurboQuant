# Controlled RTM workbook — categorised presentation revision

**File:** `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Categorised.xlsx`  
**Artifact record:** [`ART-RTM-XLSX-001`](RTM-Workbook-Artifact-Record.md)  
**Baseline:** RTM v1.3 / MoSCoW v1.2  
**Owner:** Arian B  
**Date:** 14 July 2026  
**Change:** CHG-014 / CR-014 — presentation only; no scope change  
**Direct binary Git placement:** Pending  

## What changed

The original `Requirements` sheet remains the authoritative full traceability table.
The reviewed workbook also contains:

- `Requirements Overview`
- `Functional Requirements`
- `Non-Functional Reqs`
- `Research Requirements`
- `Governance Requirements`
- `Exclusions & Boundaries`

## Verified contents

- 111 requirement lifecycle records;
- 72 active requirements;
- 56 active Must Haves;
- 14 active Should Haves;
- 2 active Could Haves;
- 13 validated master tasks;
- G-M02, PD-04, EP-004, EP-005 and EP-006 marked Implemented / Validated / Verified;
- no duplicate requirement IDs;
- no active Must Have missing required traceability fields;
- all 111 records assigned to exactly one category view;
- no spreadsheet calculation errors detected.

## Integrity and current storage state

SHA-256:

`1ebb8e25a1c624af805d98228c37b09933363c53e2fab790809abe74e863b588`

The repository contains the human-readable RTM, reverse indexes, category catalogues, checksum and [controlled workbook artifact record](RTM-Workbook-Artifact-Record.md). The exact `.xlsx` binary is independently retained in `MoSCoW_Categorised_Completion_Package_2026-07-14.zip` supplied to the project owner. Direct placement of the binary in Git remains pending and is not falsely claimed.

After recovering the exact workbook from the controlled package, place it at:

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

## Human-readable baseline

- [MoSCoW Requirements Baseline v1.2](MoSCoW-Requirements-v1.2.md)
- [Requirements Traceability Matrix v1.3](Requirements-Traceability-Matrix-v1.3.md)
- [Traceability Reverse Indexes v1.3](Traceability-Reverse-Indexes-v1.3.md)
- [Functional requirements](catalogue/Functional-Requirements.md)
- [Non-Functional requirements](catalogue/Non-Functional-Requirements.md)
- [Research requirements](catalogue/Research-Requirements.md)
- [Governance requirements](catalogue/Governance-Requirements.md)
- [Exclusions and boundaries](catalogue/Exclusions-and-Boundaries.md)
- [Categorised catalogue audit](Categorised-Requirements-Catalogue-Audit.md)
- [MoSCoW and RTM evidence index](../evidence/indexes/MoSCoW-and-RTM-Evidence-Index.md)

## Control rule

The category sheets and Markdown catalogues are readable views. Controlled requirement changes must begin in the authoritative workbook, use dated change control, regenerate the Markdown views and update all affected evidence records.