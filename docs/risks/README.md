# Risk, Assumption, Constraint and Licence Control

**Document ID:** IDX-RACL-001  
**Version:** 0.6  
**Status:** Populated, consolidated and register-approved — evidence and release-package gates pending  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Assumption evidence gates, final licence-package review and baseline review  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`  
**Related reviews:** `RV-004`; `RV-005`; `RV-006`; `RV-007`

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
- [Licence Review Notes](Licence-Review-Notes.md)
- [Risk Consolidation Map](Risk-Consolidation-Map.md)
- [Candidate Risk Backlog](candidates/README.md)
- [Baseline controls](baselines/README.md)

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

## Completion boundary

Completed:

- risk consolidation;
- current scope alignment;
- approval of the controlled planning-assumption set;
- approval of the 15 Active constraints;
- initial source-based licence review.

Still required:

1. validate assumptions before confirming them;
2. review constraint compliance at dependent gates;
3. complete exact release-file and third-party-notice inventories;
4. complete the final licence review of the produced package;
5. decide the project’s own root licence;
6. check Critical and High risk treatment evidence;
7. run the cross-register consistency audit;
8. complete `PD-05`, `EP-007` and `G-M05` evidence records and RTM validation;
9. freeze baseline `v1.0` only after the baseline review passes.

The folder is operational and approved at register level, but it is not yet fully evidence-validated or release-approved.
