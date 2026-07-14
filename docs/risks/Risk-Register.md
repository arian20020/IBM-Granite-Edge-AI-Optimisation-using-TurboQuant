# Risk Register

**Document ID:** REG-RISK-001  
**Version:** 0.2  
**Status:** Draft content population — candidates require formal review  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Pending candidate consolidation and assessment  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`

## Purpose

This register records uncertain events or conditions that may affect project scope, schedule, quality, safety, evidence, reproducibility, licensing or technical delivery.

The existing full risk records remain below. A wider project risk inventory has now been added under [`candidates/`](candidates/README.md). It covers the intended Windows 11 x64 and Intel-focused application, Granite, llama.cpp, OpenVINO, TurboQuant and TurboVec scope, together with testing, security, usability, licensing and release concerns.

Candidate entries are not yet assessed or baselined. They must be reviewed, merged where appropriate, scored and completed before they are promoted into the full risk-record table.

## Status vocabulary

| Status | Meaning |
|---|---|
| Open | The risk exists and requires active treatment. |
| Monitoring | Controls are in place and the risk is being observed. |
| Triggered | The risk event has occurred or its trigger condition has been met. |
| Accepted | The residual risk has been consciously accepted with a recorded reason. |
| Closed | The risk is no longer applicable or has been fully resolved. |
| Superseded | A later risk record replaces this one. |

## Full risk records

| Risk ID | Category | Description | Cause | Probability | Impact | Exposure | Trigger | Validation method | Mitigation | Contingency | Owner | Residual risk | Status | Evidence | Related IDs | Last reviewed | Next review |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| R-001 | Schedule | Development may exceed the 15 August target. | Draft carry-forward; detailed cause review pending. | Medium | High | Pending assessment | Weekly schedule review identifies missed gates or insufficient remaining capacity. | Compare completed work and remaining Must-Have effort against the controlled timetable. | Protect Must Haves and control scope growth. | Use approved change control to defer lower-priority work where necessary. | Arian B | Pending assessment | Open | Timetable and task catalogue; exact evidence link pending | `PD-05`; project schedule | 2026-07-14 | Pending |
| R-002 | Technical | TurboQuant may fail, fail to activate or silently fall back to another route. | Experimental implementation and route-specific compatibility limitations. | High | High | Pending assessment | Activation evidence is absent, requested and actual state differ, or the route fails to load or generate. | Review activation markers, backend/device evidence, runtime logs and matched fallback tests. | Keep the dependable upstream route available and require truthful requested-versus-actual reporting. | Disable the affected Experimental configuration and use the upstream route while the defect is investigated. | Arian B | Pending assessment | Open | Controlled test workbooks and activation evidence; exact links pending | `F-M21`; `F-M22`; `N-M11`; `QX-04` | 2026-07-14 | Pending |

## Candidate risk inventory

The complete candidate inventory contains `R-003` to `R-254`:

| Candidate table | IDs | Main areas |
|---|---|---|
| [`01-project-scope-windows.md`](candidates/01-project-scope-windows.md) | `R-003`–`R-066` | Schedule, scope, requirements, Windows, model import and llama.cpp |
| [`02-openvino-turboquant.md`](candidates/02-openvino-turboquant.md) | `R-067`–`R-098` | OpenVINO and TurboQuant |
| [`03-turbovec-hardware-performance.md`](candidates/03-turbovec-hardware-performance.md) | `R-099`–`R-150` | TurboVec, Intel hardware, memory and performance |
| [`04-security-ux-ai-quality.md`](candidates/04-security-ux-ai-quality.md) | `R-151`–`R-199` | Security, privacy, UX, accessibility and AI quality |
| [`05-testing-evidence.md`](candidates/05-testing-evidence.md) | `R-200`–`R-224` | Testing, evidence and reproducibility |
| [`06-licence-release.md`](candidates/06-licence-release.md) | `R-225`–`R-254` | Licensing, third-party supply chain, release and reporting |

## Candidate review rule

During the next formal review:

1. merge duplicate or closely related candidates;
2. confirm that each row describes an uncertain event rather than an existing issue, assumption or constraint;
3. define cause, trigger, validation method, mitigation and contingency;
4. assess probability, impact and exposure;
5. identify related requirements, work packages, experiments and evidence;
6. promote accepted candidates into the full risk-record table;
7. mark rejected or merged candidates as `Superseded` with the replacement ID;
8. update the Review Log before any baseline is frozen.

## Entry rule

A full risk record is not complete until its cause, trigger, validation method, mitigation, contingency, owner, residual risk, status, evidence and review dates are populated or explicitly marked not applicable with justification.

High risks must not be treated as controlled merely because they appear in this register. Their mitigation and contingency must be reviewable and linked to evidence.
