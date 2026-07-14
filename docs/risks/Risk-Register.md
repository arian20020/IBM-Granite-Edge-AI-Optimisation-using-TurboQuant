# Risk Register

**Document ID:** REG-RISK-001  
**Version:** 0.3  
**Status:** Draft content populated — candidate consolidation and treatment review pending  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Formal candidate consolidation and assessment  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`

## Purpose

This register records uncertain events or conditions that may affect project scope, schedule, quality, safety, evidence, reproducibility, licensing or technical delivery.

The existing full risk records remain below. A wider project risk inventory has been added under [`candidates/`](candidates/README.md). It covers the intended Windows 11 x64 and Intel-focused application, Granite, llama.cpp, OpenVINO, TurboQuant and TurboVec scope, together with testing, security, usability, licensing and release concerns.

Candidate entries are not yet assessed or baselined. They must be reviewed, merged where appropriate, scored and completed before they are promoted into the full risk-record table. The scoring and review method is controlled by [Control-and-Validation-Plan.md](Control-and-Validation-Plan.md).

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
| R-001 | Schedule | Development may exceed the 15 August target. | The first-release scope contains several dependent technical routes and the work is being completed by one developer within a fixed academic timetable. | Medium | High | High | Weekly schedule review identifies missed gates, overdue critical work or insufficient remaining capacity. | Compare completed work, remaining Must-Have effort, dependency gates and report/testing time against the controlled timetable. | Protect the approved first-release scope, review progress weekly, sequence dependent work and control scope growth. | Use approved change control to reduce or defer lower-priority depth while preserving truthful evidence and the core deliverable. | Arian B | Medium until the remaining work is re-estimated and schedule gates are met | Open | Timetable, active task catalogue and weekly journal; exact review links pending | `PD-05`; project schedule; scope risks | 2026-07-14 | Weekly during active development |
| R-002 | Technical | TurboQuant may fail, fail to activate or silently fall back to another route. | TurboQuant integrations are experimental and depend on an exact model, fork, build, backend, cache format, driver and target device combination. | High | High | Critical | Activation evidence is absent, requested and actual state differ, cache allocation is not compressed, or the route fails to load or generate. | Review activation markers, cache-format and allocation evidence, requested-versus-actual backend/device records, runtime logs and matched fallback tests. | Pin one exact route, require activation proof, test quality and stability, preserve the upstream fallback and report requested-versus-actual state truthfully. | Disable the affected Experimental configuration, use the verified upstream route and record the blocker or fallback without claiming TurboQuant success. | Arian B | High until one exact Granite/Windows/Intel configuration is proven with repeatable activation, quality and fallback evidence | Open | Controlled test workbooks, activation logs and memory evidence; exact validated links pending | `F-M21`; `F-M22`; `N-M11`; `QX-04`; `A-009`; `A-010` | 2026-07-14 | Before every TurboQuant implementation or release gate |

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
4. assess probability, impact and exposure using the controlled method;
5. identify related requirements, work packages, experiments and evidence;
6. promote accepted candidates into the full risk-record table;
7. mark rejected or merged candidates as `Superseded` with the replacement ID or recorded reason;
8. update the Review Log before any baseline is frozen.

## Entry rule

A full risk record is not complete until its cause, trigger, validation method, mitigation, contingency, owner, residual risk, status, evidence and review dates are populated or explicitly marked not applicable with justification.

Critical and High risks must not be treated as controlled merely because they appear in this register. Their mitigation, contingency and residual-risk decision must be reviewable and linked to evidence.
