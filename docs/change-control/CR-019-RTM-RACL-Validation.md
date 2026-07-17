# CR-019 — Synchronise RACL Validation in the Controlled RTM

**Request ID:** `CR-019`  
**Date:** 2026-07-15  
**Requested by:** Arian B  
**Category:** Evidence and configuration control  
**Affected IDs:** `G-M05`; `PD-05`; `EP-007`; `ART-RTM-XLSX-001`  
**Decision:** Approved and implemented  
**Related change:** `CHG-019`

## Request

Synchronise the controlled RTM with the completed evidence and validation review for the Risk, Assumption, Constraint and Licence governance system.

## Decision

1. Record `REQ:G-M05`, `WP:PD-05` and `EP:EP-007` as `Implemented / Validated / Verified`.
2. Link the three completed evidence records.
3. Record `AUD-RACL-001` / `RV-008` as the validation review.
4. Advance the workbook to RTM v1.3.1.
5. Control the exact workbook using filename, size and SHA-256.
6. Synchronise the detailed tabs, Evidence Register, Dashboard, Change Log and Status History.
7. Preserve yellow-highlighted work as scheduled after implementation rather than treating it as overdue current work.

## Controlled workbook

- Filename: `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.1_RACL_Validated.xlsx`
- Size: 120,290 bytes
- SHA-256: `2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96`

## Validation

- Relevant Task Checklist and detailed-tab rows agree.
- Evidence paths are present.
- Status History contains a fixed validation snapshot.
- No detected spreadsheet formula errors.
- Evidence records and `AUD-RACL-001` now agree with the RTM.

## Boundary

This decision validates the register-governance deliverable. It does not confirm every technical assumption, mitigation, constraint-compliance test or final release-package licence decision.