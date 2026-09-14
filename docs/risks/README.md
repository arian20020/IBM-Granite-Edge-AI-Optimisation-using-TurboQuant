# Risk, Assumption, Constraint and Licence Control

**Document ID:** IDX-RACL-001  
**Version:** 1.1<br>
**Status:** Operational and Verified for `G-M05`, `PD-05` and `EP-007`  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-09-14<br>
**Next review:** Event-driven register reviews and final release-package review  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related changes:** `CR-018` / `CHG-018`; `CR-019` / `CHG-019`  
**Related reviews:** `RV-004`–`RV-011`

## Purpose

This directory is the controlled home for project risks, assumptions, constraints and licence decisions.

- A **risk** is an uncertain event that may affect the project.
- An **assumption** is accepted for planning but requires evidence before confirmation.
- A **constraint** is an approved boundary the project must work within.
- A **licence record** controls how external material may be used, changed and distributed.

## Current release roles

| Area | Current role |
|---|---|
| Windows 11 x64, Intel, IBM Granite, upstream llama.cpp and one exact verified TurboQuant route | Core |
| OpenVINO | Should Have |
| TurboVec | Later feasibility investigation; full integration deferred |

## Authoritative records

| Record | File | Current state |
|---|---|---|
| Risks | [Risk Register](Risk-Register.md) | 254 identified items consolidated into 37 operational risks |
| Assumptions | [Assumption Register](Assumption-Register.md) | `A-001`–`A-017`: 9 Confirmed, 1 Rejected for the tested scope, 7 Pending |
| Constraints | [Constraint Register](Constraint-Register.md) | `C-001`–`C-015` approved Active |
| Licences | [Licence Register](Licence-Register.md) | `L-001`–`L-018` reviewed; final package gate remains Pending |

Supporting controls:

- [Control and Validation Plan](Control-and-Validation-Plan.md)
- [Review Log](Review-Log.md)
- [Cross-Register Validation Audit](Cross-Register-Validation-Audit.md)
- [Licence Review Notes](Licence-Review-Notes.md)
- [Risk Consolidation Map](Risk-Consolidation-Map.md)

Task evidence:

- [G-M05](../evidence/requirements/G-M05/README.md)
- [PD-05](../evidence/work-packages/PD-05/README.md)
- [EP-007](../evidence/engineering-practices/EP-007/README.md)

## Verified result

`AUD-RACL-001` / `RV-008` and controlled RTM v1.3.1 confirm:

- the four authoritative registers exist;
- risk duplication was reduced without losing history;
- High and Critical risks contain required treatment fields;
- assumptions, constraints and licences have clear owners, outcomes and evidence gates;
- no material cross-register contradiction prevents use;
- `G-M05`, `PD-05` and `EP-007` are `Implemented / Validated / Verified`.

`RV-009` records the later report-cut-off review. `RV-010` corrects the repeatability outcome, current risk wording, evidence links and dependency identities. These reviews do not claim that the final release package is approved.

`RV-011` records the independent OneDrive recovery test. All 226 final-evidence files and 2,482 raw-evidence files were restored with matching paths, sizes and SHA-256 hashes.

Controlled workbook:

- filename: `IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3.1_RACL_Validated.xlsx`;
- SHA-256: `2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96`;
- size: 120,290 bytes.

## Continuing controls

Continue event-driven assumption validation, constraint-compliance checks, risk-treatment review and final release-package licensing. These are ongoing project controls and do not reopen the three Verified governance tasks unless new evidence invalidates the system.
