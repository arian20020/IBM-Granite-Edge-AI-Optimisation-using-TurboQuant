# Controlled RTM Workbook

**File:** `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Working_Baseline.xlsx`  
**SHA-256:** `88385b743cbc52921d71d5a0805a1d8fefd6b6c513c6507ad263a24b2e65ea50`  
**Owner:** Arian B  
**Baseline date:** 14 July 2026  

## Verified contents

- 111 requirement lifecycle records;
- 72 active requirements;
- 56 active Must Haves;
- 14 active Should Haves;
- 2 active Could Haves;
- 13 validated master tasks;
- G-M02, PD-04, EP-004, EP-005 and EP-006 set to Implemented / Validated / Verified;
- no duplicate requirement IDs;
- no active requirement missing traceability;
- no Excel formula errors detected.

## Integrity check

After placing the workbook in this directory, run:

```powershell
(Get-FileHash `
  -LiteralPath 'docs/requirements/IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Working_Baseline.xlsx' `
  -Algorithm SHA256).Hash.ToLowerInvariant()
```

The result must equal the checksum above. Do not treat a workbook with a different checksum as this reviewed baseline unless a new change record explains the difference.

The human-readable baseline is preserved in:

- `MoSCoW-Requirements-v1.2.md`;
- `Requirements-Traceability-Matrix-v1.3.md`;
- `Traceability-Reverse-Indexes-v1.3.md`.
