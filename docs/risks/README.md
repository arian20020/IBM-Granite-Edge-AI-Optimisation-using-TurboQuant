# Risk, Assumption, Constraint and Licence Control

**Document ID:** IDX-RACL-001  
**Version:** 0.7  
**Status:** Register system implemented and substantively validated — controlled RTM synchronisation pending  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Controlled RTM synchronisation, event-driven register reviews and final release-package review  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`  
**Related reviews:** `RV-004`; `RV-005`; `RV-006`; `RV-007`; `RV-008`

## Purpose

This directory is the controlled home for project risks, assumptions, constraints and licence decisions.

- A **risk** is an uncertain event that may affect the project.
- An **assumption** is accepted for planning but requires evidence before confirmation.
- A **constraint** is an approved boundary the project must work within.
- A **licence record** controls how external material may be used, changed and distributed.

## Current release roles

| Area | Current role |
|---|---|
| Windows 11 x64 and WinUI 3 | Core Must Have |
| Intel hardware focus | Core Must Have |
| IBM Granite | Core model family |
| Upstream llama.cpp | Core dependable route and fallback |
| TurboQuant | Core Experimental route; one exact configuration must be proved |
| OpenVINO | Active Should Have |
| TurboVec | Later feasibility investigation; full integration remains deferred |

## Authoritative files

| Record | File | Current state |
|---|---|---|
| Risks | [Risk Register](Risk-Register.md) | 254 identified items consolidated into 37 operational risks |
| Assumptions | [Assumption Register](Assumption-Register.md) | `A-001`–`A-017` approved as the controlled planning set; evidence outcomes remain pending |
| Constraints | [Constraint Register](Constraint-Register.md) | `C-001`–`C-015` approved as Active boundaries |
| Licences | [Licence Register](Licence-Register.md) | `L-001`–`L-015` reviewed at source level; final package approval remains pending |

Supporting files:

- [Control and Validation Plan](Control-and-Validation-Plan.md)
- [Review Log](Review-Log.md)
- [Cross-Register Validation Audit](Cross-Register-Validation-Audit.md)
- [Licence Review Notes](Licence-Review-Notes.md)
- [Risk Consolidation Map](Risk-Consolidation-Map.md)
- [Candidate Risk Backlog](candidates/README.md)
- [Baseline controls](baselines/README.md)

Task-level evidence:

- [G-M05 evidence](../evidence/requirements/G-M05/README.md)
- [PD-05 evidence](../evidence/work-packages/PD-05/README.md)
- [EP-007 evidence](../evidence/engineering-practices/EP-007/README.md)

## Common rules

1. Use stable IDs and preserve decision history.
2. Keep one authoritative record and cross-reference it.
3. Do not mark an assumption Confirmed without its stated evidence.
4. Do not treat a risk as controlled until its treatment evidence passes.
5. Do not bundle a Pending licence item or use a Restricted item outside its recorded conditions.
6. Record owners, status, evidence and review dates.
7. Keep requested settings, actual behaviour, published claims, estimates and measurements separate.
8. Use change control for material scope or governance changes.

## Review cycle

Review records weekly, before dependent technical and packaging gates, when a dependency or licence changes, and before a baseline or release is approved. Every formal review is recorded in [Review-Log.md](Review-Log.md).

## Validation result for G-M05, PD-05 and EP-007

Review `RV-008` and `AUD-RACL-001` found that the register-system deliverable is substantively complete:

- the four authoritative registers exist and are governed as one control area;
- risk duplication has been reduced without losing history;
- High and Critical risks contain the required treatment fields;
- assumptions, constraints and licences use clear owners, outcomes, restrictions and evidence gates;
- no material cross-register contradiction prevents use;
- the three common-template evidence records exist.

The current controlled status remains `Implemented / Not Validated / Implemented` only because the authoritative RTM workbook is maintained outside the current Git checkout and has not yet been synchronised. After that update and regeneration, the evidence records may move to `Implemented / Validated / Verified`.

## Completion boundary

Completed:

- risk consolidation;
- current scope alignment;
- approval of the controlled planning-assumption set;
- approval of the 15 Active constraints;
- initial source-based licence review;
- cross-register consistency and task-validation audit;
- evidence records for `G-M05`, `PD-05` and `EP-007`.

Immediate status-control action:

1. update the three rows in the controlled RTM workbook;
2. regenerate traceability catalogues and evidence maps;
3. update the evidence metadata to `Validated / Verified`.

Continuing project controls that do not invalidate this register deliverable:

- validate individual assumptions at their dependent gates;
- review constraint compliance during implementation and release work;
- check Critical and High risk treatment evidence at the relevant gates;
- complete exact release-file and third-party-notice inventories;
- perform the final licence review of the produced package;
- decide the project’s root source-code licence;
- freeze a later baseline only when its separate baseline criteria pass.

The governance system itself is implemented and has passed substantive developer validation. Final effective Verified status is blocked only by controlled RTM synchronisation.
