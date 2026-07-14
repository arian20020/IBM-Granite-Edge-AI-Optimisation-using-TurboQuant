# Risk, Assumption, Constraint and Licence Control and Validation Plan

**Document ID:** PLAN-RACL-001  
**Version:** 1.3  
**Status:** Active working plan  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related reviews:** `RV-004`; `RV-005`

## 1. Purpose

This plan explains how the Risk, Assumption, Constraint and Licence registers move from working documents to a reviewed and frozen baseline.

It prevents four common mistakes:

- treating a listed risk as though its controls have already passed;
- treating an assumption as proven without evidence;
- treating public code or a model page as automatic permission to redistribute it;
- treating a future idea as part of the current release without an approved scope change.

## 2. Current controlled scope

The current documents are aligned around these release roles:

| Area | Current release role |
|---|---|
| Windows 11 x64 and WinUI 3 | Core Must Have |
| Intel hardware focus | Core Must Have |
| IBM Granite | Core model family |
| Upstream llama.cpp | Core dependable inference route and fallback |
| TurboQuant | Core Experimental route; one exact configuration must be proved |
| OpenVINO | Active Should Have after its integration gate passes |
| TurboVec | Later feasibility investigation; full integration remains deferred unless reactivated through change control |

This alignment was confirmed in review `RV-005`. No scope-change request is needed for the current wording.

A future decision to promote OpenVINO to Must Have or reactivate full TurboVec integration would be a material scope change and must update every affected controlled record.

## 3. Current register state

| Register | Current state | What still remains |
|---|---|---|
| Risk Register | 254 identified items consolidated into 37 operational risks | Review treatment evidence and reassess residual risks at the relevant gates |
| Assumption Register | `A-001`–`A-017` populated | Run validation methods, link evidence and set evidence-backed outcomes |
| Constraint Register | `C-001`–`C-015` populated | Confirm sources, wording and project compliance |
| Licence Register | `L-001`–`L-013` populated | Pin exact versions and complete packaging decisions |
| Review Log | Structural, content, consistency, consolidation and scope-alignment reviews recorded | Record assumption, constraint, licence, baseline and release reviews |
| Baselines | Template and freeze rules prepared | Create `v1.0` only after the baseline review passes |

## 4. Review order

### Stage 1 — Confirm the release scope

**Status:** Completed for the current developer working baseline.

The review confirmed that:

- Windows, Intel, Granite, llama.cpp and TurboQuant are the core focus;
- OpenVINO remains a Should Have;
- TurboVec remains a later feasibility investigation;
- full TurboVec application integration remains deferred.

Future scope changes must use project change control and update the Project Definition, requirements, RTM, ADRs, work packages, evidence paths and affected registers together.

### Stage 2 — Consolidate and assess risks

**Status:** Completed for the developer working register.

The original candidate list is preserved as history. The live operational result is controlled by:

- `Risk-Register.md` — 37 active operational risks;
- `Risk-Consolidation-Map.md` — mapping from original IDs to retained risks;
- `candidates/` — historical discovery records.

The remaining risk work is to:

1. check that planned controls are implemented where required;
2. link exact evidence at each dependent gate;
3. reassess probability, impact and residual risk when evidence changes;
4. change status only with a recorded reason;
5. avoid creating duplicate risks for new examples of an existing uncertainty.

#### Probability scale

| Rating | Meaning |
|---|---|
| Low | Unlikely under the current plan; no strong warning signs |
| Medium | Plausible or dependent on unresolved evidence |
| High | Likely, already showing warning signs or strongly dependent on an experimental route |

#### Impact scale

| Rating | Meaning |
|---|---|
| Low | Local rework with no important release or evidence effect |
| Medium | Delays or weakens a work package, route, experiment or report claim |
| High | Threatens a Must Have, safety boundary, legal permission, deadline, evidence integrity or final release |

#### Exposure matrix

| Probability | Impact | Exposure |
|---|---|---|
| High | High | Critical |
| High | Medium | High |
| Medium | High | High |
| Medium | Medium | Medium |
| High | Low | Medium |
| Low | High | Medium |
| Medium | Low | Low |
| Low | Medium | Low |
| Low | Low | Low |

Critical and High risks require a clear owner, trigger, validation method, mitigation, contingency, residual-risk statement and review date. Their controls must be checked against evidence before the dependent gate can pass.

### Stage 3 — Validate assumptions

Validate assumptions in dependency order:

1. evidence recovery and backup — `A-001`, `A-015`;
2. model source and exact identities — `A-002`, `A-003`;
3. upstream baseline and available Intel hardware — `A-004`, `A-006`, `A-007`;
4. OpenVINO route — `A-005`;
5. memory-fit estimator — `A-008`;
6. TurboQuant build and activation — `A-009`, `A-010`;
7. TurboVec feasibility and later role decision — `A-011`, `A-012`;
8. offline and no-port operation — `A-013`;
9. repeatable evaluation — `A-014`;
10. licensing — `A-016`;
11. target-user usability — `A-017`.

An assumption may be:

- `Confirmed` only for the exact scope proved by evidence;
- `Rejected` when evidence shows it is false or unreliable;
- `Pending` only with an owner, planned action and review date;
- `Superseded` when a later decision replaces it.

A deferred feature does not require full implementation evidence. Its assumptions must instead support the current feasibility or decision gate.

### Stage 4 — Review constraints

For each constraint:

1. confirm the authoritative source;
2. confirm that it is a real boundary rather than a preference or risk;
3. check that planning, architecture, testing and reporting obey it;
4. record any material change through change control;
5. keep removed or superseded constraints in the history.

Important checks include:

- Windows 11 x64 and Intel focus;
- local and no-port operation;
- available hardware and memory limits;
- Experimental-claim boundaries;
- no real patient or pupil data;
- research-prototype and academic-integrity boundaries.

### Stage 5 — Complete the licence gate

For each release-relevant model, package, fork, asset or dataset:

1. pin the exact version, commit or revision;
2. link or archive the authoritative licence and NOTICE files;
3. check third-party and transitive notices;
4. decide whether it is referenced, used only during development, downloaded separately, bundled or excluded;
5. record attribution and distribution duties;
6. set the outcome to `Approved`, `Restricted` or `Rejected`.

No `Pending` component may be bundled into the release.

### Stage 6 — Run a cross-register audit

Check that:

- the same fact is not recorded as conflicting risk, assumption and constraint entries;
- retained risk IDs and consolidation mappings resolve correctly;
- owners, dates and statuses are current;
- release roles match the Project Definition, requirements, RTM, ADRs and work packages;
- licence restrictions match packaging and download decisions;
- evidence paths exist or are clearly marked pending;
- closed, rejected, removed and superseded history remains visible.

### Stage 7 — Complete evidence records

Create and validate:

- `docs/evidence/work-packages/PD-05/README.md`;
- `docs/evidence/engineering-practices/EP-007/README.md`;
- `docs/evidence/requirements/G-M05/README.md` where required.

Each record must use Evidence Template v1.1.1 and map every acceptance criterion to authoritative evidence.

### Stage 8 — Freeze baseline `v1.0`

Create `baselines/v1.0/` only after a recorded Baseline Review passes.

The snapshot must contain:

- `Baseline-Record.md`;
- `Risk-Register.md`;
- `Assumption-Register.md`;
- `Constraint-Register.md`;
- `Licence-Register.md`.

The baseline record must identify the reviewed commit, review ID, approval scope, open gaps and included files. Later changes update the live registers and create a later baseline; they do not rewrite the frozen snapshot.

## 5. Definition of Done for `docs/risks/`

The control area is ready for full validation when:

- all four live registers contain the important project entries;
- the risk backlog is consolidated into a manageable operational set;
- Critical and High risks have complete treatment fields and evidence reviews at their dependent gates;
- critical assumptions have evidence-backed outcomes or explicitly accepted pending actions;
- active constraints have approved sources, effects and project responses;
- every release-relevant third-party item has a licence and packaging decision;
- the cross-register audit passes or records accepted gaps;
- the Review Log records the substantive reviews;
- `PD-05`, `EP-007` and `G-M05` evidence records are complete;
- the RTM matches the evidence and validation state;
- the first baseline is frozen without hiding unresolved gaps.

Risk consolidation and current scope alignment are complete. The folder remains **populated and operational**, but not fully validated or baselined, until the remaining assumption, constraint, licence, evidence and baseline gates pass.
