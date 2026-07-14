# Risk Register

**Document ID:** REG-RISK-001  
**Version:** 0.1  
**Status:** Draft structure — entries require review  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Pending content-population review  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`

## Purpose

This register records uncertain events or conditions that may affect project scope, schedule, quality, safety, evidence, reproducibility, licensing or technical delivery.

The existing draft risk entries have been retained below so no earlier project information is lost. Their newly introduced fields remain marked for review where the earlier register did not contain enough information.

## Status vocabulary

| Status | Meaning |
|---|---|
| Open | The risk exists and requires active treatment. |
| Monitoring | Controls are in place and the risk is being observed. |
| Triggered | The risk event has occurred or its trigger condition has been met. |
| Accepted | The residual risk has been consciously accepted with a recorded reason. |
| Closed | The risk is no longer applicable or has been fully resolved. |
| Superseded | A later risk record replaces this one. |

## Risk records

| Risk ID | Category | Description | Cause | Probability | Impact | Exposure | Trigger | Validation method | Mitigation | Contingency | Owner | Residual risk | Status | Evidence | Related IDs | Last reviewed | Next review |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| R-001 | Schedule | Development may exceed the 15 August target. | Draft carry-forward; detailed cause review pending. | Medium | High | Pending assessment | Weekly schedule review identifies missed gates or insufficient remaining capacity. | Compare completed work and remaining Must-Have effort against the controlled timetable. | Protect Must Haves and control scope growth. | Defer Experimental and Should-Have work where permitted by the controlled baseline. | Arian B | Pending assessment | Open | Timetable and task catalogue; exact evidence link pending | `PD-05`; project schedule | 2026-07-14 | Pending |
| R-002 | Technical | TurboQuant may fail, fail to activate or silently fall back to another route. | Experimental implementation and route-specific compatibility limitations. | High | High | Pending assessment | Activation evidence is absent, requested and actual state differ, or the route fails to load or generate. | Review activation markers, backend/device evidence, runtime logs and matched fallback tests. | Keep the dependable upstream route available and require truthful requested-versus-actual reporting. | Remove or disable the Experimental option for the affected configuration and use the upstream route. | Arian B | Pending assessment | Open | Controlled test workbooks and activation evidence; exact links pending | `F-M21`; `F-M22`; `N-M11`; `QX-04` | 2026-07-14 | Pending |

## Entry rule

A risk record is not complete until its cause, trigger, validation method, mitigation, contingency, owner, residual risk, status, evidence and review dates are populated or explicitly marked not applicable with justification.

High risks must not be treated as controlled merely because they appear in this table. Their mitigation and contingency must be reviewable and linked to evidence.
