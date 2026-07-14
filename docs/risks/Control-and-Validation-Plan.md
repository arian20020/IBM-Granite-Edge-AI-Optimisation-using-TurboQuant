# Risk, Assumption, Constraint and Licence Control and Validation Plan

**Document ID:** PLAN-RACL-001  
**Version:** 1.0  
**Status:** Active working plan  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`

## 1. Purpose

This plan defines how the four live registers will move from populated drafts to a reviewed and frozen developer working baseline.

The plan prevents three common errors:

- treating a listed risk as though it has already been controlled;
- treating a reasonable assumption as though it has been proven;
- treating public source code or a model page as automatic permission to bundle or redistribute it.

## 2. Current state

| Register | Current content state | What remains |
|---|---|---|
| Risk Register | `R-001` and `R-002` are full draft records; `R-003`–`R-254` form a candidate backlog | Consolidate duplicates, confirm record type, assess importance and complete controls for accepted risks |
| Assumption Register | `A-001`–`A-017` populated | Execute validation methods, link exact evidence and set evidence-backed outcomes |
| Constraint Register | `C-001`–`C-015` populated | Confirm authoritative sources, check scope consistency and approve the active wording |
| Licence Register | `L-001`–`L-013` populated | Pin exact versions, inspect package/model terms and approve or restrict the final packaging decisions |
| Review Log | Structural review recorded | Record content, gate, baseline and final-release reviews |
| Baselines | Structure prepared | Freeze `v1.0` only after the criteria in this plan are met |

## 3. Review order

The review must follow this order because later decisions depend on earlier ones.

### Stage 1 — Confirm the controlled release scope

Before the first baseline review, resolve the current difference between the intended Windows/Intel/OpenVINO/TurboQuant/TurboVec scope and any controlled requirements or ADRs that still classify OpenVINO or full TurboVec work differently.

Required output:

- an approved or explicitly pending scope-change decision;
- synchronised Project Definition, requirements, RTM and ADR wording where approved;
- updated risk and constraint relationships.

The registers may remain populated while this is pending, but the first baseline must state the unresolved scope gap clearly.

### Stage 2 — Consolidate and assess risks

The candidate backlog is an identification list, not the final operational register.

For each candidate:

1. confirm that it describes an uncertain future event or condition;
2. move existing failures to the Failure Register rather than duplicating them as risks;
3. move beliefs requiring proof to the Assumption Register;
4. move fixed boundaries to the Constraint Register;
5. merge duplicate or closely related candidates;
6. promote accepted risks into the full risk table;
7. retain merged or rejected IDs as `Superseded` with a replacement or reason.

#### Probability scale

| Rating | Meaning |
|---|---|
| Low | Unlikely under the current plan; no strong warning signs |
| Medium | Plausible or dependent on unresolved technical evidence |
| High | Likely, already showing warning signs or strongly dependent on an experimental route |

#### Impact scale

| Rating | Meaning |
|---|---|
| Low | Local rework with no important requirement, evidence or release effect |
| Medium | Delays or weakens a work package, route, experiment or report claim |
| High | Threatens a Must-Have, safety boundary, evidence integrity, legal permission, deadline or final release |

#### Exposure and treatment priority

| Probability | Impact | Exposure |
|---|---|---|
| High | High | Critical |
| High | Medium, or Medium | High | High |
| Medium | Medium, High | Low | Medium |
| Low | Medium or Low | Low |

Critical and High risks require an owner, trigger, validation method, mitigation, contingency, residual-risk assessment and review date before the baseline can pass. Medium and Low risks may use proportionate controls but cannot have blank mandatory fields.

### Stage 3 — Validate assumptions

Validate the assumptions in dependency order:

1. evidence recovery and backup — `A-001`, `A-015`;
2. legitimate model source and exact identities — `A-002`, `A-003`;
3. dependable upstream baseline and available Intel hardware — `A-004`, `A-006`, `A-007`;
4. OpenVINO route — `A-005`;
5. memory-fit estimator — `A-008`;
6. TurboQuant build and activation — `A-009`, `A-010`;
7. TurboVec implementation and retrieval quality — `A-011`, `A-012`;
8. offline/no-port operation — `A-013`;
9. repeatable evaluation — `A-014`;
10. licensing — `A-016`;
11. target-user usability — `A-017`.

An assumption may be:

- `Confirmed` only for the exact scope proven by evidence;
- `Rejected` when evidence shows it is false or unreliable;
- left `Pending` only with an owner, planned action and review date;
- `Superseded` when a later decision removes or replaces it.

### Stage 4 — Review constraints

For every constraint:

1. confirm the authoritative source;
2. confirm that it is a real boundary rather than a preference or risk;
3. check that the Project Definition, requirements, architecture, tests and report obey it;
4. record any change through change control;
5. retain removed or replaced constraints with their history.

The review must pay special attention to:

- the Windows 11 x64 and Intel release boundary;
- local and no-port operation;
- available hardware and memory limits;
- experimental-claim boundaries;
- sensitive-data exclusion;
- prototype and academic-integrity boundaries.

### Stage 5 — Complete the licence gate

Review licences using the exact files and versions actually used, not only family-level or repository-level claims.

For each release-relevant item:

1. pin the exact model, package, repository commit or asset;
2. archive or link the authoritative licence and NOTICE files;
3. inspect transitive dependencies and third-party notices;
4. decide whether it is referenced, used during development, downloaded separately, bundled or excluded;
5. record all attribution and distribution duties;
6. mark the result `Approved`, `Restricted` or `Rejected`.

No `Pending` component may be bundled into the release.

### Stage 6 — Run a cross-register consistency audit

Check that:

- the same fact does not appear as conflicting risk, assumption and constraint entries;
- risk IDs and related IDs resolve to real records;
- owners and review dates are current;
- the exact first-release scope is consistent across planning, requirements, architecture and these registers;
- licence restrictions are reflected in packaging and model-download requirements;
- evidence paths exist or are clearly marked pending;
- closed, rejected, removed and superseded history remains visible.

### Stage 7 — Complete evidence records

Create and validate:

- `docs/evidence/work-packages/PD-05/README.md`;
- `docs/evidence/engineering-practices/EP-007/README.md`;
- any required `G-M05` requirement evidence record.

Each record must use the current evidence template and map every acceptance criterion to authoritative evidence.

### Stage 8 — Freeze the first baseline

Create `baselines/v1.0/` only after a recorded Baseline Review passes.

The snapshot must contain:

- `Baseline-Record.md`;
- `Risk-Register.md`;
- `Assumption-Register.md`;
- `Constraint-Register.md`;
- `Licence-Register.md`.

The baseline record must identify the reviewed commit, review ID, approval scope, open gaps and included files. Later findings update the live registers and lead to a later baseline; they do not rewrite the frozen snapshot.

## 4. Definition of Done for the folder

The `docs/risks/` control area is operationally complete when:

- all four live registers contain the important project entries;
- the candidate-risk backlog has been consolidated into a manageable operational set;
- every Critical or High risk has complete and reviewable treatment fields;
- critical assumptions have evidence-backed outcomes or explicit assigned pending actions;
- all active constraints have approved sources, effects and project responses;
- every release-relevant third-party component has a clear licence and packaging decision;
- the Review Log records the substantive review;
- PD-05, EP-007 and G-M05 evidence records are complete;
- the RTM matches the evidence and validation state;
- the first controlled baseline is frozen without hiding open gaps.

Until these conditions are met, the folder may be described as **populated and operational**, but not fully validated or baselined.
