# Risk, Assumption, Constraint and Licence Control and Validation Plan

**Document ID:** PLAN-RACL-001  
**Version:** 1.9<br>
**Status:** Active control plan  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-09-14<br>
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related reviews:** `RV-004`–`RV-011`

## Purpose

This plan controls how risks, assumptions, constraints and licences are recorded, reviewed, validated and maintained without confusing governance completion with later technical or release success.

## Current scope roles

| Area | Role |
|---|---|
| Windows 11 x64, Intel, Granite, llama.cpp and one exact verified TurboQuant route | Core |
| OpenVINO | Should Have |
| TurboVec | Later feasibility investigation; full integration deferred unless reactivated |

## Current state

| Control | State | Continuing work |
|---|---|---|
| Risk Register | 254 identified items consolidated into 37 operational risks | Review controls and residual risk at dependent gates |
| Assumption Register | `A-001`–`A-017` approved for planning | Confirm/reject only with stated evidence |
| Constraint Register | `C-001`–`C-015` approved Active | Check compliance at dependent gates |
| Licence Register | `L-001`–`L-018` reviewed | Complete exact release-package review later |
| Cross-register audit | Passed as `AUD-RACL-001`; updated through `RV-011` | Revalidate after material changes |
| G-M05 / PD-05 / EP-007 evidence | Implemented, Validated and Verified | Maintain event-driven controls |
| Controlled RTM | v1.3.1 synchronised | New revision required for later status changes |

## Completed stages

1. Scope alignment — `RV-005`.
2. Risk consolidation — `RV-004`.
3. Assumption-set and constraint approval — `RV-006`.
4. Initial licence review — `RV-007`.
5. Cross-register audit — `AUD-RACL-001`; updates reviewed through `RV-011`.
6. Evidence records for G-M05, PD-05 and EP-007.
7. RTM v1.3.1 status synchronisation and checksum control.

## Controlled outcomes

- An assumption becomes `Confirmed` only for the exact scope supported by evidence.
- A risk is not treated as controlled until its mitigation evidence passes at the relevant gate.
- A `Pending` licence item cannot be bundled.
- A `Restricted` item may be used only within its recorded conditions.
- Material scope changes must update planning, requirements, RTM, ADRs, evidence and registers together.

## Task-level Definition of Done

`G-M05`, `PD-05` and `EP-007` are complete because:

- four authoritative registers exist;
- the risk backlog is consolidated;
- High and Critical risks contain required treatment fields;
- assumptions, constraints and licence outcomes are explicit and owned;
- the cross-register audit passes;
- the three evidence records exist;
- the controlled RTM records `Implemented / Validated / Verified`.

## Continuing project controls

Continue to:

- review Critical and High risk treatment evidence;
- validate assumptions at their dependent gates;
- check constraint compliance;
- pin exact release artefacts and notices;
- complete the final licence/package review;
- conduct a later baseline and final release review.

These continuing controls do not reopen the completed G-M05, PD-05 or EP-007 tasks unless new evidence invalidates the governance system.
