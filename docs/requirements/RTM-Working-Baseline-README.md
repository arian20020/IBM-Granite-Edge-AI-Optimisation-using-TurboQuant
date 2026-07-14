# Controlled RTM workbook — categorised presentation revision

**File:** `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Categorised.xlsx`  
**Baseline:** RTM v1.3 / MoSCoW v1.2  
**Owner:** Arian B  
**Date:** 14 July 2026  
**Change:** CHG-014 / CR-014 — presentation only; no scope change

## What changed

The original `Requirements` sheet remains the authoritative full traceability table.
The workbook now also contains:

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
- G-M02, PD-04, EP-004, EP-005 and EP-006 remain Implemented / Validated / Verified;
- no duplicate requirement IDs;
- no active Must Have missing required traceability fields;
- all 111 records assigned to exactly one category view;
- no spreadsheet calculation errors detected.

## Integrity

SHA-256:

`1ebb8e25a1c624af805d98228c37b09933363c53e2fab790809abe74e863b588`

After placing the workbook in this directory, run:

```powershell
(Get-FileHash `
  -LiteralPath 'docs/requirements/IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Categorised.xlsx' `
  -Algorithm SHA256).Hash.ToLowerInvariant()
```

The category sheets are readable views. Controlled requirement changes must still be made in the master `Requirements` sheet and recorded through change control.

The human-readable baseline is split across:

- `MoSCoW-Requirements-v1.2.md`;
- `catalogue/Functional-Requirements.md`;
- `catalogue/Non-Functional-Requirements.md`;
- `catalogue/Research-Requirements.md`;
- `catalogue/Governance-Requirements.md`;
- `catalogue/Exclusions-and-Boundaries.md`;
- `Requirements-Traceability-Matrix-v1.3.md`;
- `Traceability-Reverse-Indexes-v1.3.md`.
