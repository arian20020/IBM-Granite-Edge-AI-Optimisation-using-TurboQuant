# G-M05 — Consolidated Risk, Assumption, Constraint and Licence Register

> **Template version:** 1.1.1

## 1. Evidence metadata

| Field | Value |
|---|---|
| Template version | `1.1.1` |
| Evidence record version | `1.0` |
| Record ID | `REQ:G-M05` |
| Record type | Requirement |
| Source baseline / version | MoSCoW v1.2 / RTM v1.3 |
| Working status | Implemented |
| Validation state | Not Validated |
| Effective status | Implemented |
| Owner | Arian B |
| Evidence date | 2026-07-14 |
| Last reviewed | 2026-07-14 |
| Validation date | Pending controlled RTM synchronisation |
| Validator | Arian B |
| Validation independence | Self-review |
| Approval scope | Developer working baseline |
| Validation method | Documentary requirement review against the four authoritative registers, consolidation map, control plan, licence evidence, formal reviews and cross-register audit |
| Supersedes | None |
| Superseded by | None |

## 2. Statement being evidenced

> **G-M05 — The project must maintain a consolidated risk, assumption, constraint and licence register.**

## 3. Definition of Done or acceptance-criterion mapping

| Criterion ID | Definition of Done or acceptance criterion | Result | Evidence item ID(s) | Reviewer note |
|---|---|---|---|---|
| AC-01 | The project maintains an authoritative Risk Register. | Pass | EV-01; EV-02 | The live register contains 37 operational risks and preserves all original risk IDs through consolidation history. |
| AC-02 | The project maintains an authoritative Assumption Register. | Pass | EV-03 | The approved planning set contains `A-001`–`A-017` with evidence-gated outcomes. |
| AC-03 | The project maintains an authoritative Constraint Register. | Pass | EV-04 | The project has 15 approved Active boundaries. |
| AC-04 | The project maintains an authoritative Licence Register. | Pass | EV-05; EV-06 | The register separates use, modification, redistribution and final packaging permission. |
| AC-05 | The four registers are consolidated through one index, control plan, review log and change process. | Pass | EV-07; EV-08 | They form one governance system without merging incompatible record types into one table. |
| AC-06 | Important risks have complete treatment and monitoring fields. | Pass | EV-01; EV-09 | High/Critical risks have validation, mitigation, contingency, owner, status and the wider operational schema. |
| AC-07 | Open risks, Pending assumptions, Active constraints and Restricted/Pending licences remain visible and reviewable. | Pass | EV-01; EV-03; EV-04; EV-05 | The requirement is met without falsely claiming all uncertainty has disappeared. |
| AC-08 | A formal consistency and validation review has been completed. | Pass | EV-09 | `AUD-RACL-001` / `RV-008` found no material contradiction preventing use. |
| AC-09 | Requirement evidence, work-package evidence and engineering-practice evidence are linked. | Pass | This record; PD-05 evidence; EP-007 evidence | The three related evidence packs use the common template and consistent claim boundaries. |
| AC-10 | The controlled RTM status and generated catalogues show the validated conclusion. | Pending | EV-10 | The external controlled workbook must be synchronised before the effective status becomes Verified. |

## 4. Evidence summary and claim boundary

### Evidence summary

G-M05 is implemented by the controlled `docs/risks/` area. The project maintains separate authoritative registers because risks, assumptions, constraints and licences require different fields and outcomes, while the shared README, Control and Validation Plan, Review Log and audit keep them consolidated as one governance system.

The risk list was reduced from a broad 254-item discovery set to 37 operational risks. Assumptions and constraints received formal content/approval reviews. The licence register was expanded through research into the main models, runtimes, packages, forks, data and assets. The cross-register audit confirms that the system is coherent and proportionate.

### Claim boundary

| Boundary | Statement |
|---|---|
| What this evidence proves | The project maintains the consolidated RACL governance system required by G-M05 and can use it during planning, development, testing and release control. |
| What this evidence does not prove | It does not prove that every recorded uncertainty has been resolved or that the final software package is ready, safe or licensed for distribution. |

## 5. Authoritative evidence

| Evidence ID | Type | Evidence item | Repository path or external controlled location | Version / commit / run ID | SHA-256 or immutable identifier | Evidence date | What it proves | Status |
|---|---|---|---|---|---|---|---|---|
| EV-01 | Document | Risk Register | [Risk-Register.md](../../../risks/Risk-Register.md) | v0.5 | Git history | 2026-07-14 | Authoritative operational risk records. | Available |
| EV-02 | Document | Risk Consolidation Map | [Risk-Consolidation-Map.md](../../../risks/Risk-Consolidation-Map.md) | Current | Git history | 2026-07-14 | Consolidation and preserved history. | Available |
| EV-03 | Document | Assumption Register | [Assumption-Register.md](../../../risks/Assumption-Register.md) | v0.4 | Git history | 2026-07-14 | Authoritative planning assumptions and validation gates. | Available |
| EV-04 | Document | Constraint Register | [Constraint-Register.md](../../../risks/Constraint-Register.md) | v0.3 | Git history | 2026-07-14 | Authoritative active project boundaries. | Available |
| EV-05 | Document | Licence Register | [Licence-Register.md](../../../risks/Licence-Register.md) | v0.3 | Git history | 2026-07-14 | Authoritative permission and packaging decisions. | Available |
| EV-06 | Document | Licence Review Notes | [Licence-Review-Notes.md](../../../risks/Licence-Review-Notes.md) | v1.0 | Authoritative source links | 2026-07-14 | Evidence basis for the initial licence conclusions. | Available |
| EV-07 | Document | RACL index and Control Plan | [README.md](../../../risks/README.md); [Control-and-Validation-Plan.md](../../../risks/Control-and-Validation-Plan.md) | v0.6 / v1.4 | Git history | 2026-07-14 | Consolidated governance, scoring, maintenance and claim rules. | Available |
| EV-08 | Document | Review Log | [Review-Log.md](../../../risks/Review-Log.md) | `RV-001`–`RV-008` | Git history | 2026-07-14 | Formal review history and accurate approval scope. | Available |
| EV-09 | Audit | Cross-Register Validation Audit | [Cross-Register-Validation-Audit.md](../../../risks/Cross-Register-Validation-Audit.md) | `AUD-RACL-001` v1.0 | Git history | 2026-07-14 | Requirement-level audit and consistency conclusion. | Available |
| EV-10 | Controlled status source | RTM workbook and generated catalogues | [RTM-Workbook-Artifact-Record.md](../../../requirements/RTM-Workbook-Artifact-Record.md) | RTM v1.3 | Recorded workbook SHA-256 | 2026-07-14 | Authoritative status source; synchronisation remains. | Pending |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass | EV-01–EV-09. |
| Every required criterion is mapped to evidence | Pass | AC-01–AC-10. |
| Definition of Done or acceptance criteria checked | Pass | `AUD-RACL-001` and `RV-008`. |
| Evidence is version-controlled or independently backed up | Pass | Git controls the governance documents and PR history; the external RTM workbook is checksum-controlled. |
| Evidence version, run or integrity identifiers are sufficient | Pass | Register versions, review IDs, PRs and the RTM artifact record are explicit. |
| Claim boundary is explicit and proportionate | Pass | Requirement completion is separated from technical/release completion. |
| Validation independence and approval scope are stated accurately | Pass | Self-review; developer working baseline. |
| RTM status fields match this evidence record | Pending | The external workbook has not yet been updated. |
| No unresolved contradiction affects the claim | Pass | Cross-register audit passed. |

**Validation review result:** `Partially Validated`

**Validation conclusion:**  
G-M05 is substantively satisfied: the consolidated governance area exists, is populated, reviewed and operational. The controlled validation state remains Not Validated until the RTM workbook is synchronised and the generated catalogues are regenerated.

**Status source-of-truth rule:**  
The controlled RTM remains authoritative. After synchronisation, update this record to `Implemented / Validated / Verified`.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `G-M05` |
| Work package(s) | `PD-05` |
| Engineering practice(s) | `EP-007` |
| Objective(s) | `O11` |
| Research question(s) | Governance support for all project research questions |
| Test / experiment / evidence IDs | `AUD-RACL-001`; `RV-001`–`RV-008` |
| Change request / decision IDs | `CR-018`; `CHG-018` |
| Pull request / commit / release | PRs `#22`–`#26` |

## 8. Limitations, gaps, follow-up and revalidation

### Limitations, gaps and follow-up

- Synchronise the controlled RTM and regenerate the catalogues.
- Continue maintaining the registers as project evidence changes.
- Complete final release-package licence review later; that does not invalidate the existence and operation of the register system.

### Revalidation triggers

| Trigger | Applies? | Required action / owner |
|---|---|---|
| Parent requirement, Definition of Done or acceptance criterion changes | Yes | Update this evidence record and RTM — Arian B. |
| Affected code, architecture, workflow or controlled document changes | Yes | Review affected governance entries — Arian B. |
| Runtime, model, dependency, dataset or external artefact version changes | Yes | Update related risks, assumptions and licences — Arian B. |
| Test method, prompt, rubric, metric or processing script changes | Conditional | Revalidate only where governance/evidence rules are affected — Arian B. |
| Target hardware, operating system or deployment environment changes | Yes | Review related constraints and risks — Arian B. |
| New contradictory, negative or superseding evidence is discovered | Yes | Record it and repeat the audit — Arian B. |

**Revalidation required now:** `Yes — controlled RTM synchronisation only`  
**Next review date:** `Immediately after RTM synchronisation`

## 9. Change control

Any material change must update:

1. the affected live register and control documents;
2. `AUD-RACL-001` where the consistency conclusion changes;
3. this G-M05 evidence record;
4. the related PD-05 and EP-007 evidence records;
5. the controlled RTM workbook and generated catalogues;
6. evidence indexes and report traceability;
7. change-control records when the baseline changes.
