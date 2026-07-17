# Controlled RTM workbook — v1.3.1 RACL validation revision

**File:** `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.1_RACL_Validated.xlsx`  
**Artifact record:** [`ART-RTM-XLSX-001`](RTM-Workbook-Artifact-Record.md)  
**Baseline:** RTM v1.3.1 / MoSCoW v1.2  
**Owner:** Arian B  
**Date:** 15 July 2026  
**Change:** `CHG-019` / `CR-019` — status, validation and schedule-control synchronisation; no first-release scope change  
**Direct binary Git placement:** Not required for validation; exact binary is independently controlled by checksum.

## What changed

The workbook now records:

- `REQ:G-M05` as `Implemented / Validated / Verified`;
- `WP:PD-05` as `Implemented / Validated / Verified`;
- `EP:EP-007` as `Implemented / Validated / Verified`;
- matching evidence paths and validation notes;
- synchronised detailed-tab statuses;
- updated Dashboard, Evidence Register, Change Log and Status History;
- yellow-highlighted tasks as scheduled after implementation rather than overdue July work.

## Integrity

- **SHA-256:** `2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96`
- **Size:** 120,290 bytes
- **Worksheets reviewed:** 11
- **Formula-error scan:** no detected `#REF!`, `#DIV/0!`, `#VALUE!`, `#NAME?` or `#N/A` errors

## Verification

```powershell
$Path = 'IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.1_RACL_Validated.xlsx'
$Expected = '2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96'
$Actual = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
if ($Actual -ne $Expected) {
    throw "RTM workbook checksum mismatch. Expected $Expected but found $Actual."
}
Write-Host 'RTM workbook checksum verified.' -ForegroundColor Green
```

## Control rule

The workbook is the authoritative editable status source. Markdown evidence and catalogues must reflect it and must not independently redefine task status. A later edit requires a new revision, checksum, change entry and evidence revalidation.