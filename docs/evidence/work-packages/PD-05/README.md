# PD-05 — Risk, Assumption, Constraint and Licence Register

> **Template version:** 1.1.1

## 1. Evidence metadata

| Field | Value |
|---|---|
| Template version | `1.1.1` |
| Evidence record version | `1.0` |
| Record ID | `WP:PD-05` |
| Record type | Work Package |
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
| Validation method | Documentary audit of the four registers, risk-consolidation mapping, review history, licence evidence and criterion-to-evidence traceability |
| Supersedes | None |
| Superseded by | None |

The status fields above reflect the current controlled RTM position. The substantive validation review passed, but the RTM workbook must be synchronised before `Validation state` becomes `Validated` and `Effective status` becomes `Verified`.

## 2. Statement being evidenced

> **PD-05 — Risk/assumption/constraint/licence register.**

Controlled Definition of Done:

> High risks have validation, mitigation, contingency, owner and status.

## 3. Definition of Done or acceptance-criterion mapping

| Criterion ID | Definition of Done or acceptance criterion | Result | Evidence item ID(s) | Reviewer note |
|---|---|---|---|---|
| AC-01 | A single controlled governance area contains separate authoritative Risk, Assumption, Constraint and Licence registers. | Pass | EV-01; EV-03; EV-04; EV-05 | The four record types remain separate but are governed through one index, plan and review log. |
| AC-02 | The risk discovery backlog is consolidated into a manageable operational set without deleting history. | Pass | EV-01; EV-02 | 254 identified items were reduced to 37 operational risks; original IDs remain traceable. |
| AC-03 | High and Critical risks have validation, mitigation, contingency, owner and status. | Pass | EV-01; EV-08 | The live Risk Register also records cause, probability, impact, exposure, trigger, residual risk, evidence and review timing. |
| AC-04 | Assumptions have an owner, validation method, expected evidence, outcome, consequence if false and review gate. | Pass | EV-03; EV-08 | The assumption set is approved for planning; evidence-dependent outcomes correctly remain Pending. |
| AC-05 | Constraints have sources, affected scope, practical impact, project response, owner, status and review timing. | Pass | EV-04; EV-08 | `C-001`–`C-015` are approved Active boundaries. |
| AC-06 | Licence records distinguish use, modification and redistribution from final bundling permission. | Pass | EV-05; EV-06; EV-08 | Restricted and Pending outcomes prevent unsupported packaging claims. |
| AC-07 | Formal reviews, maintenance rules, change control and claim boundaries exist. | Pass | EV-07; EV-08; EV-09 | Reviews `RV-001`–`RV-008` and PR history preserve decisions and limitations. |
| AC-08 | A cross-register audit finds no material contradiction that prevents use of the governance system. | Pass | EV-08 | Open technical and packaging questions are represented as controlled outcomes, not hidden contradictions. |
| AC-09 | The controlled RTM mirrors the validated conclusion and evidence path. | Pending | EV-10 | Exact RTM workbook synchronisation and regeneration remain the sole status-control blocker. |

## 4. Evidence summary and claim boundary

### Evidence summary

PD-05 has been implemented through a controlled `docs/risks/` governance area containing four authoritative registers, one operational scoring and validation plan, formal review history, a risk-consolidation map and evidence-based licence notes.

The risk exercise initially identified 254 items. It was deliberately reduced to 37 important operational risks. Every retained risk has the fields required by the PD-05 Definition of Done. The assumptions and constraints have been approved at the correct governance level, while evidence-dependent assumption outcomes remain visible. The initial licence review records what may be used, modified or redistributed and what remains restricted before packaging.

The Cross-Register Validation Audit found the deliverable substantively complete and coherent. The only outstanding control action is synchronising the external controlled RTM workbook and regenerating the derived catalogues.

### Claim boundary

| Boundary | Statement |
|---|---|
| What this evidence proves | PD-05 has produced a live, consolidated and reviewable Risk, Assumption, Constraint and Licence governance system, and High/Critical risks contain the required operational treatment fields. |
| What this evidence does not prove | It does not prove every risk treatment has already succeeded, every assumption is confirmed, every constraint-compliance test has run or one final release bundle is licensed. |

## 5. Authoritative evidence

| Evidence ID | Type | Evidence item | Repository path or external controlled location | Version / commit / run ID | SHA-256 or immutable identifier | Evidence date | What it proves | Status |
|---|---|---|---|---|---|---|---|---|
| EV-01 | Document | Risk Register | [Risk-Register.md](../../../risks/Risk-Register.md) | v0.5 | Git history | 2026-07-14 | Contains 37 operational risks with complete treatment fields. | Available |
| EV-02 | Document | Risk Consolidation Map | [Risk-Consolidation-Map.md](../../../risks/Risk-Consolidation-Map.md) | Current | Git history | 2026-07-14 | Preserves mapping from all original risk IDs to retained risks. | Available |
| EV-03 | Document | Assumption Register | [Assumption-Register.md](../../../risks/Assumption-Register.md) | v0.4 | Git history | 2026-07-14 | Records the approved planning assumptions and their validation gates. | Available |
| EV-04 | Document | Constraint Register | [Constraint-Register.md](../../../risks/Constraint-Register.md) | v0.3 | Git history | 2026-07-14 | Records the 15 approved Active project boundaries. | Available |
| EV-05 | Document | Licence Register | [Licence-Register.md](../../../risks/Licence-Register.md) | v0.3 | Git history | 2026-07-14 | Records use, modification, redistribution and packaging decisions. | Available |
| EV-06 | Document | Licence Review Notes | [Licence-Review-Notes.md](../../../risks/Licence-Review-Notes.md) | v1.0 | Git history and authoritative source links | 2026-07-14 | Supports the initial licence decisions and restrictions. | Available |
| EV-07 | Document | Control and Validation Plan | [Control-and-Validation-Plan.md](../../../risks/Control-and-Validation-Plan.md) | v1.4 | Git history | 2026-07-14 | Defines scoring, validation, maintenance and baseline rules. | Available |
| EV-08 | Audit | Cross-Register Validation Audit | [Cross-Register-Validation-Audit.md](../../../risks/Cross-Register-Validation-Audit.md) | v1.0 / `RV-008` | Git history | 2026-07-14 | Checks the full PD-05 Definition of Done and cross-register consistency. | Available |
| EV-09 | Change history | Governance pull requests | PRs `#22`–`#26` | GitHub PR history | Stable PR URLs | 2026-07-14 | Preserves structural creation, population, consolidation, corrections and validation review. | Available |
| EV-10 | Controlled status source | RTM workbook and generated catalogues | [RTM Workbook Artifact Record](../../../requirements/RTM-Workbook-Artifact-Record.md) | RTM v1.3 | Recorded workbook SHA-256 | 2026-07-14 | Controls status; requires the final PD-05 synchronisation. | Pending |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass | EV-01–EV-08. |
| Every required criterion is mapped to evidence | Pass | AC-01–AC-09 are mapped above. |
| Definition of Done or acceptance criteria checked | Pass | `RV-008` and EV-08 checked the High/Critical risk fields directly. |
| Evidence is version-controlled or independently backed up | Pass | Repository documents and PR history are version-controlled; the RTM workbook is separately controlled by checksum. |
| Evidence version, run or integrity identifiers are sufficient | Pass | Register versions, PRs and the RTM artifact record identify the reviewed state. |
| Claim boundary is explicit and proportionate | Pass | Completion of the governance system is separated from later technical and packaging outcomes. |
| Validation independence and approval scope are stated accurately | Pass | Self-review; developer working baseline. |
| RTM status fields match this evidence record | Pending | The controlled workbook still reports the earlier In Progress / Not Validated state. |
| No unresolved contradiction affects the claim | Pass | EV-08 found no material contradiction. |

**Validation review result:** `Partially Validated`

**Validation conclusion:**  
PD-05 is substantively complete and the Definition of Done passes. Full controlled validation is withheld only because the external RTM workbook has not yet been updated and regenerated. After that synchronisation, this record should change to `Implemented / Validated / Verified` without changing the evidence conclusion.

**Status source-of-truth rule:**  
The controlled RTM remains authoritative. This evidence record must be updated immediately after the RTM status is changed and regenerated.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `G-M05` |
| Work package(s) | `PD-05` |
| Engineering practice(s) | `EP-007` |
| Objective(s) | `O11` |
| Research question(s) | Project-governance support for `RQ1`–`RQ4` and `RQ-TV` |
| Test / experiment / evidence IDs | `AUD-RACL-001`; `RV-001`–`RV-008` |
| Change request / decision IDs | `CR-018`; `CHG-018`; `RV-004`–`RV-008` |
| Pull request / commit / release | PRs `#22`, `#23`, `#24`, `#25`, `#26` |

## 8. Limitations, gaps, follow-up and revalidation

### Limitations, gaps and follow-up

- The controlled RTM workbook must be updated and regenerated before full validation is claimed.
- Risk treatment evidence continues to be reviewed at dependent implementation, test and release gates.
- Assumption outcomes continue to move from Pending only when their stated evidence exists.
- Final licence and package approval requires inspection of exact release files and notices.
- These continuing controls do not prevent the PD-05 register deliverable from being implemented.

### Revalidation triggers

| Trigger | Applies? | Required action / owner |
|---|---|---|
| Parent requirement, Definition of Done or acceptance criterion changes | Yes | Revalidate PD-05 and update the RTM — Arian B. |
| Affected code, architecture, workflow or controlled document changes | Yes | Review affected register entries and audit consistency — Arian B. |
| Runtime, model, dependency, dataset or external artefact version changes | Yes | Revisit affected risks, assumptions and licence records — Arian B. |
| Test method, prompt, rubric, metric or processing script changes | Yes | Update only affected evidence and risks where the governance claim changes — Arian B. |
| Target hardware, operating system or deployment environment changes | Yes | Review constraints and affected technical risks — Arian B. |
| New contradictory, negative or superseding evidence is discovered | Yes | Record the evidence and rerun the cross-register audit — Arian B. |

**Revalidation required now:** `Yes — controlled RTM synchronisation only`  
**Next review date:** `Immediately after the RTM workbook is updated`

## 9. Change control

Any later change that affects this evidence claim must update, where applicable:

1. the affected live register and review record;
2. `Cross-Register-Validation-Audit.md`;
3. this PD-05 evidence record;
4. the related EP-007 and G-M05 evidence records;
5. the controlled RTM workbook and regenerated catalogues;
6. evidence indexes and report traceability;
7. the project change log when the baseline changes.
