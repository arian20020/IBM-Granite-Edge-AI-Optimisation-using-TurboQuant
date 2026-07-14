# Risk, Assumption, Constraint and Licence Control and Validation Plan

**Document ID:** PLAN-RACL-001  
**Version:** 1.5  
**Status:** Active working plan  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related reviews:** `RV-004`; `RV-005`; `RV-006`; `RV-007`; `RV-008`

## 1. Purpose

This plan explains how the Risk, Assumption, Constraint and Licence registers move from working records to evidence-reviewed controls and, later, a frozen baseline.

It prevents four common mistakes:

- treating a listed risk as though its controls have already passed;
- treating an approved planning assumption as though it has been proved;
- treating public code or a model page as automatic permission to distribute every derived artefact;
- treating a future idea as part of the current release without an approved scope change.

## 2. Current controlled scope

| Area | Current release role |
|---|---|
| Windows 11 x64 and WinUI 3 | Core Must Have |
| Intel hardware focus | Core Must Have |
| IBM Granite | Core model family |
| Upstream llama.cpp | Core dependable inference route and fallback |
| TurboQuant | Core Experimental route; one exact configuration must be proved |
| OpenVINO | Active Should Have after its integration gate passes |
| TurboVec | Later feasibility investigation; full integration remains deferred unless reactivated through change control |

This alignment was confirmed in `RV-005`.

## 3. Current register state

| Register / control | Current state | What remains |
|---|---|---|
| Risk Register | 254 identified items consolidated into 37 operational risks | Review treatment evidence and reassess residual risks at dependent gates |
| Assumption Register | `A-001`–`A-017` approved as the controlled planning set | Run validation methods and set evidence-backed outcomes |
| Constraint Register | `C-001`–`C-015` approved as Active boundaries | Check compliance evidence at dependent gates |
| Licence Register | `L-001`–`L-015` reviewed at source level | Pin final artefacts, inspect notices and approve the produced release package |
| Cross-register audit | `AUD-RACL-001` completed as `RV-008` | Revalidate after material changes |
| Task evidence | `G-M05`, `PD-05` and `EP-007` evidence records created | Synchronise the controlled RTM and then promote the records to effective Verified |
| Baselines | Template and freeze rules prepared | Create `v1.0` only after a separate Baseline Review passes |

## 4. Review order

### Stage 1 — Confirm release scope

**Status:** Completed as `RV-005`.

Future changes to the OpenVINO or TurboVec release role must update the Project Definition, requirements, RTM, ADRs, work packages, evidence paths and affected registers together.

### Stage 2 — Consolidate and assess risks

**Status:** Completed as `RV-004`.

The live result is controlled by:

- `Risk-Register.md` — 37 operational risks;
- `Risk-Consolidation-Map.md` — mapping from original IDs;
- `candidates/` — historical discovery records.

Remaining risk work:

1. check planned controls at the relevant gates;
2. link exact evidence;
3. reassess probability, impact and residual risk when evidence changes;
4. change status only with a recorded reason;
5. avoid duplicate risk growth.

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

| Probability | High impact | Medium impact | Low impact |
|---|---|---|---|
| High | Critical | High | Medium |
| Medium | High | Medium | Low |
| Low | Medium | Low | Low |

Critical and High risks require an owner, trigger, validation method, mitigation, contingency, residual-risk statement and review date. Their controls must be checked against evidence before the dependent gate passes.

### Stage 3 — Approve and validate assumptions

**Register-content status:** Approved as part of `RV-006`.

**Evidence-outcome status:** Ongoing.

Validate in dependency order:

1. evidence recovery and backup — `A-001`, `A-015`;
2. model source and exact identities — `A-002`, `A-003`;
3. upstream baseline and Intel hardware — `A-004`, `A-006`, `A-007`;
4. OpenVINO route — `A-005`;
5. memory-fit estimator — `A-008`;
6. TurboQuant build and activation — `A-009`, `A-010`;
7. TurboVec feasibility and any later reactivation — `A-011`, `A-012`;
8. offline and no-port operation — `A-013`;
9. repeatable evaluation — `A-014`;
10. licence and packaging permission — `A-016`;
11. target-user usability — `A-017`.

An assumption becomes `Confirmed` only for the exact model, build, device, package or workflow supported by reviewed evidence. Planning approval is not evidence confirmation.

### Stage 4 — Approve and monitor constraints

**Status:** Approved as part of `RV-006`.

The 15 constraints are the current Active boundaries. At each dependent gate:

1. confirm the project still operates within the boundary;
2. link the compliance evidence;
3. record a breach as a risk, issue or change request;
4. update every affected controlled record if the boundary changes.

Constraint approval does not replace later offline, no-port, hardware, privacy, packaging or academic-integrity checks.

### Stage 5 — Complete licence and packaging control

**Initial source review:** Completed as `RV-007`.

**Final release-package review:** Pending.

The initial review established:

- MIT permission for pinned llama.cpp and the reviewed TurboQuant forks;
- Apache-2.0 permission for official Granite 4.1 3B/8B, OpenVINO and OpenVINO GenAI;
- restricted package treatment for Windows App SDK, Windows SDK Build Tools and .NET until the exact release inventory is inspected;
- Pending decisions for TurboVec, asset provenance, project-source licensing and selected converted model artefacts.

Before bundling any item:

1. pin the exact version, commit, model revision or converted artefact;
2. archive the authoritative licence, NOTICE and third-party notices;
3. identify every distributed file;
4. record attribution, modification and distribution duties;
5. decide whether the item is bundled, downloaded separately, used only for development or excluded;
6. inspect the produced package;
7. set the final decision to `Approved`, `Restricted` or `Rejected`.

No `Pending` item may be bundled. A `Restricted` item may be used only within its recorded conditions.

### Stage 6 — Run the cross-register audit

**Status:** Completed as `AUD-RACL-001` / `RV-008`.

The audit checked that:

- risks, assumptions, constraints and licence decisions do not materially conflict;
- all retained IDs and consolidation mappings resolve;
- owners, dates and statuses are present;
- release roles agree with planning and requirements;
- licence restrictions are explicit;
- evidence paths exist or are explicitly pending;
- negative and superseded history remains visible;
- the PD-05 Definition of Done is satisfied.

The audit found no material contradiction preventing use of the governance system. The only remaining task-status blocker is controlled RTM synchronisation.

### Stage 7 — Complete evidence records

**Status:** Substantively completed as part of `RV-008`.

The following Evidence Template v1.1.1 records now exist:

- `docs/evidence/work-packages/PD-05/README.md`;
- `docs/evidence/engineering-practices/EP-007/README.md`;
- `docs/evidence/requirements/G-M05/README.md`.

Their criteria and authoritative evidence pass. Their controlled metadata remains `Implemented / Not Validated / Implemented` until the RTM workbook is updated and regenerated.

### Stage 8 — Synchronise the controlled RTM

**Status:** Pending.

Update `REQ:G-M05`, `WP:PD-05` and `EP:EP-007` to:

- Working status: `Implemented`;
- Validation: `Validated`;
- Effective status: `Verified`;
- evidence paths pointing to the three completed evidence records.

Then:

1. update the workbook revision, SHA-256 and size records;
2. regenerate traceability catalogues and evidence maps;
3. confirm the generated outputs show all three tasks as Verified;
4. update the evidence metadata and validation conclusions to remove the RTM synchronisation caveat.

### Stage 9 — Freeze a later baseline

Create `baselines/v1.0/` only after a separate recorded Baseline Review passes.

The snapshot must contain:

- `Baseline-Record.md`;
- `Risk-Register.md`;
- `Assumption-Register.md`;
- `Constraint-Register.md`;
- `Licence-Register.md`.

The baseline record must identify the reviewed commit, review ID, approval scope, open gaps and included files.

## 5. Definition of Done

### Task-level Definition of Done for `G-M05`, `PD-05` and `EP-007`

The task-level register deliverable is complete when:

- all four authoritative registers exist;
- the risk backlog is consolidated;
- High and Critical risks have the required treatment fields;
- assumptions, constraints and licence outcomes are explicit and owned;
- a cross-register audit passes;
- the three evidence records exist;
- the controlled RTM matches the validated evidence state.

All substantive criteria have passed. RTM synchronisation is the only remaining status-control step.

### Continuing control-area and release work

The wider `docs/risks/` area continues throughout the project. Later technical and release claims require:

- gate evidence for Critical and High risk controls;
- evidence-backed outcomes for assumptions relied upon by completed claims;
- constraint-compliance evidence at relevant gates;
- exact release-file licence and packaging decisions;
- a later baseline review and frozen snapshot where appropriate.

Those continuing controls do not invalidate completion of the initial register-consolidation work package and engineering practice.
