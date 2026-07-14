# EP-007 — Consolidate Risk, Assumption, Constraint and Licence Register

> **Template version:** 1.1.1

## 1. Evidence metadata

| Field | Value |
|---|---|
| Template version | `1.1.1` |
| Evidence record version | `1.0` |
| Record ID | `EP:EP-007` |
| Record type | Engineering Practice |
| Source baseline / version | Engineering Practice catalogue / RTM v1.3 |
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
| Validation method | Review of register consolidation, ownership/status fields, evidence links, risk deduplication, formal reviews and cross-register consistency audit |
| Supersedes | None |
| Superseded by | None |

The practice has been implemented and substantively reviewed. Full controlled validation remains pending only until the RTM workbook is synchronised and the generated catalogues are regenerated.

## 2. Statement being evidenced

> **EP-007 — Consolidate risk/assumption/constraint/licence register.**

## 3. Definition of Done or acceptance-criterion mapping

| Criterion ID | Definition of Done or acceptance criterion | Result | Evidence item ID(s) | Reviewer note |
|---|---|---|---|---|
| AC-01 | Replace the earlier mixed or incomplete content with four authoritative register types governed as one control area. | Pass | EV-01; EV-03; EV-04; EV-05; EV-07 | The project has separate Risk, Assumption, Constraint and Licence registers under one controlled index and plan. |
| AC-02 | Consolidate duplicate and low-value risk candidates into a smaller operational set. | Pass | EV-01; EV-02 | 254 identified items were reduced to 37 operational risks. |
| AC-03 | Keep stable IDs and preserve the history of merged, rejected, closed or superseded content. | Pass | EV-02; EV-09 | The consolidation map and Git history preserve all original risk IDs and governance decisions. |
| AC-04 | Give live records clear owners, statuses, evidence needs and review timing. | Pass | EV-01; EV-03; EV-04; EV-05 | Owners and status/outcome fields exist across all four registers. |
| AC-05 | Distinguish planning approval from technical proof and packaging approval. | Pass | EV-03; EV-05; EV-07 | Pending assumptions and Restricted licences remain visible rather than being falsely approved. |
| AC-06 | Record formal reviews and maintenance/change-control rules. | Pass | EV-07; EV-08; EV-09 | Reviews `RV-001`–`RV-008` and PRs `#22`–`#26` provide a controlled history. |
| AC-07 | Complete a cross-register consistency audit and record the claim boundary. | Pass | EV-08 | No material contradiction prevents use of the governance system. |
| AC-08 | Link the engineering practice to a formal evidence record and its parent work package/requirement. | Pass | This record; PD-05 evidence; G-M05 evidence | The three linked evidence records were added together. |
| AC-09 | Synchronise the controlled RTM status and regenerated catalogues. | Pending | EV-10 | The exact controlled workbook is external to the current checkout and still needs the final status update. |

## 4. Evidence summary and claim boundary

### Evidence summary

EP-007 was implemented through several controlled steps rather than one unreviewed document dump:

1. the register structure was created;
2. important project records were populated;
3. 254 risk items were consolidated into 37 operational risks;
4. the release-role wording was corrected and aligned;
5. assumptions and constraints received formal review decisions;
6. licences received a source-based permission and packaging review;
7. a cross-register audit checked field completeness, consistency and claim boundaries.

This practice is now part of the project's continuing governance process. New examples should normally update an existing risk rather than create duplicate rows, and all material changes have defined revalidation rules.

### Claim boundary

| Boundary | Statement |
|---|---|
| What this evidence proves | EP-007 has been applied: the project now maintains a consolidated, owned, status-controlled and evidence-linked RACL governance area. |
| What this evidence does not prove | It does not prove that all future register updates, technical validations, mitigations or final release-licence checks are already complete. |

## 5. Authoritative evidence

| Evidence ID | Type | Evidence item | Repository path or external controlled location | Version / commit / run ID | SHA-256 or immutable identifier | Evidence date | What it proves | Status |
|---|---|---|---|---|---|---|---|---|
| EV-01 | Document | Risk Register | [Risk-Register.md](../../../risks/Risk-Register.md) | v0.5 | Git history | 2026-07-14 | Shows the operational risk schema and retained risk set. | Available |
| EV-02 | Document | Risk Consolidation Map | [Risk-Consolidation-Map.md](../../../risks/Risk-Consolidation-Map.md) | Current | Git history | 2026-07-14 | Shows duplicate consolidation and preserved ID history. | Available |
| EV-03 | Document | Assumption Register | [Assumption-Register.md](../../../risks/Assumption-Register.md) | v0.4 | Git history | 2026-07-14 | Shows approved planning assumptions with evidence-gated outcomes. | Available |
| EV-04 | Document | Constraint Register | [Constraint-Register.md](../../../risks/Constraint-Register.md) | v0.3 | Git history | 2026-07-14 | Shows approved active constraints. | Available |
| EV-05 | Document | Licence Register and review notes | [Licence-Register.md](../../../risks/Licence-Register.md); [Licence-Review-Notes.md](../../../risks/Licence-Review-Notes.md) | v0.3 / v1.0 | Git history and authoritative links | 2026-07-14 | Shows use/modification/redistribution decisions and packaging restrictions. | Available |
| EV-06 | Document | RACL controlling index | [README.md](../../../risks/README.md) | v0.6 | Git history | 2026-07-14 | Defines authoritative files, common rules, maintenance and completion boundaries. | Available |
| EV-07 | Document | Control and Validation Plan | [Control-and-Validation-Plan.md](../../../risks/Control-and-Validation-Plan.md) | v1.4 | Git history | 2026-07-14 | Defines the recurring governance practice and validation gates. | Available |
| EV-08 | Audit | Cross-Register Validation Audit | [Cross-Register-Validation-Audit.md](../../../risks/Cross-Register-Validation-Audit.md) | v1.0 / `RV-008` | Git history | 2026-07-14 | Validates consolidation and consistency. | Available |
| EV-09 | Change history | Pull requests `#22`–`#26` and Review Log | [Review-Log.md](../../../risks/Review-Log.md) | `RV-001`–`RV-008` | Stable GitHub PR history | 2026-07-14 | Demonstrates controlled evolution and review. | Available |
| EV-10 | Controlled status source | RTM workbook artifact and generated catalogues | [RTM-Workbook-Artifact-Record.md](../../../requirements/RTM-Workbook-Artifact-Record.md) | RTM v1.3 | Recorded SHA-256 | 2026-07-14 | Controls task status; final status synchronisation remains. | Pending |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass | EV-01–EV-09. |
| Every required criterion is mapped to evidence | Pass | AC-01–AC-09. |
| Definition of Done or acceptance criteria checked | Pass | Cross-register audit and `RV-008`. |
| Evidence is version-controlled or independently backed up | Pass | Git history controls all documents; RTM workbook is checksum-controlled externally. |
| Evidence version, run or integrity identifiers are sufficient | Pass | Versions, PRs, review IDs and artifact record are present. |
| Claim boundary is explicit and proportionate | Pass | Continuing operation is separated from initial implementation. |
| Validation independence and approval scope are stated accurately | Pass | Self-review for developer working baseline. |
| RTM status fields match this evidence record | Pending | RTM still reports In Progress / Not Validated. |
| No unresolved contradiction affects the claim | Pass | `AUD-RACL-001` found no material contradiction. |

**Validation review result:** `Partially Validated`

**Validation conclusion:**  
EP-007 has been implemented correctly and all substantive criteria pass. Full controlled validation is pending only because the controlled RTM workbook and generated catalogues have not yet been synchronised. After that action, this record should change to `Implemented / Validated / Verified`.

**Status source-of-truth rule:**  
The controlled RTM remains authoritative and must be updated before this evidence record claims effective Verified status.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `G-M05` |
| Work package(s) | `PD-05` |
| Engineering practice(s) | `EP-007` |
| Objective(s) | `O11` |
| Research question(s) | Governance support for `RQ1`–`RQ4` and `RQ-TV` |
| Test / experiment / evidence IDs | `AUD-RACL-001`; `RV-001`–`RV-008` |
| Change request / decision IDs | `CR-018`; `CHG-018` |
| Pull request / commit / release | PRs `#22`–`#26` |

## 8. Limitations, gaps, follow-up and revalidation

### Limitations, gaps and follow-up

- Synchronise the controlled RTM and regenerate the catalogues.
- Continue reviewing risk controls, assumptions, constraints and licences at their stated event gates.
- Final release-package licensing remains a later release gate and is not part of the initial EP-007 implementation claim.

### Revalidation triggers

| Trigger | Applies? | Required action / owner |
|---|---|---|
| Parent requirement, Definition of Done or acceptance criterion changes | Yes | Revalidate this record and RTM mapping — Arian B. |
| Affected code, architecture, workflow or controlled document changes | Yes | Update affected records and repeat consistency checks — Arian B. |
| Runtime, model, dependency, dataset or external artefact version changes | Yes | Review related assumptions, risks and licences — Arian B. |
| Test method, prompt, rubric, metric or processing script changes | Conditional | Update the governance evidence only where it changes risk or evidence controls — Arian B. |
| Target hardware, operating system or deployment environment changes | Yes | Review constraints and hardware-related risks — Arian B. |
| New contradictory, negative or superseding evidence is discovered | Yes | Record it and rerun the audit — Arian B. |

**Revalidation required now:** `Yes — controlled RTM synchronisation only`  
**Next review date:** `Immediately after the RTM workbook is updated`

## 9. Change control

A material change to this practice must update:

1. the affected register or governance rule;
2. the Review Log and Cross-Register Validation Audit;
3. this EP-007 evidence record;
4. the PD-05 and G-M05 evidence records;
5. the controlled RTM and generated outputs;
6. related evidence indexes and report sections.
