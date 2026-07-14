# EP-005 — Build bidirectional requirements traceability

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `EP:EP-005` |
| Record type | Engineering Practice |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B |
| Evidence date | `2026-07-14` |
| Validation date | `2026-07-14` |
| Validator | Arian B |
| Validation method | Forward-mapping completeness review, reverse-index audit, evidence-path inspection and Git history review |

## 2. Statement being evidenced

> Build and maintain a bidirectional requirements traceability matrix.

## 3. Definition of Done or acceptance criteria

- [x] Every active requirement has a forward mapping to its current priority and lifecycle state.
- [x] Every active requirement maps to objectives or research questions.
- [x] Every active requirement maps to work packages and planned components.
- [x] Every active requirement records acceptance criteria and a verification method.
- [x] Every active requirement records a test/evidence ID or planned evidence path.
- [x] Reverse indexes exist for objectives, research questions, work packages and components.
- [x] Reverse indexes exist for test/evidence IDs and evidence paths.
- [x] No active Must Have is missing the fields required by G-M02 and PD-04.
- [x] The RTM and reverse indexes are version-controlled and linked from the evidence packs.
- [x] The boundary between planning traceability and final implementation evidence is documented.

## 4. Evidence summary

EP-005 was completed by producing RTM v1.3 and a separate reverse-index document. The forward RTM maps each of the 72 active requirements to its source, rationale, objective/RQ relationships, work packages, planned components, acceptance criteria, verification method and evidence location. The reverse indexes allow the project to start from an objective, RQ, work package, component, test/evidence ID or evidence path and identify the linked requirements.

The audit found zero active requirements missing traceability and zero active Must Haves missing the required mapping fields. This validates the traceability structure; G-M03 remains open for the later release-level proof that actual implementation files, commits and executed tests satisfy every active Must Have.

## 5. Authoritative evidence

| Evidence item | Repository path or external controlled location | What it proves | Status |
|---|---|---|---|
| Requirements Traceability Matrix v1.3 | [Requirements-Traceability-Matrix-v1.3.md](../../../requirements/Requirements-Traceability-Matrix-v1.3.md) | Provides the forward requirement-to-objective/RQ/WP/component/acceptance/evidence mappings. | Available |
| Traceability reverse indexes | [Traceability-Reverse-Indexes-v1.3.md](../../../requirements/Traceability-Reverse-Indexes-v1.3.md) | Provides reverse mappings from objectives, RQs, WPs, components, test/evidence IDs and evidence paths. | Available |
| Controlled RTM workbook | [IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Categorised.xlsx](../../../requirements/IXN_IBM_Granite_Requirements_Traceability_Matrix_v1.3_Categorised.xlsx) | Provides the authoritative editable RTM, formulas, Task Checklist and Dashboard. | Available |
| Workbook integrity record | [RTM-Working-Baseline-SHA256.txt](../../../requirements/RTM-Working-Baseline-SHA256.txt) | Identifies the reviewed workbook by SHA-256. | Available |
| Bidirectional traceability audit | [Bidirectional-Traceability-Audit.md](Bidirectional-Traceability-Audit.md) | Records the forward and reverse audit results and index counts. | Available |
| Must-Have coverage audit | [Must-Have-Coverage-Audit.md](../../work-packages/PD-04/Must-Have-Coverage-Audit.md) | Confirms all 56 active Must Haves contain the required planning fields. | Available |
| Related PD-04 evidence | [PD-04 evidence record](../../work-packages/PD-04/README.md) | Validates the Must-Have RTM and acceptance-design work package. | Available |
| Related G-M02 evidence | [G-M02 evidence record](../../requirements/G-M02/README.md) | Validates the versioned MoSCoW baseline used by the RTM. | Available |
| Common evidence template | [Evidence-Record-Template.md](../../templates/Evidence-Record-Template.md) | Confirms this README follows the repository evidence-record structure. | Available |
| Scope baseline pull request | [PR #14](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/14) | Preserves the reviewed RTM, reverse indexes and evidence packs. | Available |
| Catalogue-organisation pull request | [PR #15](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/15) | Preserves the readable category views without changing traceability. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass | RTM v1.3, reverse indexes, workbook and audit are present. |
| Definition of Done checked | Pass | Every criterion in section 3 was checked against the controlled RTM. |
| Active requirements with forward traceability | Pass | 72 of 72. |
| Active Must Haves with required mapping | Pass | 56 of 56. |
| Duplicate requirement IDs | Pass | 0. |
| Objective reverse-index keys | Pass | 11. |
| Research-question reverse-index keys | Pass | 5. |
| Work-package reverse-index keys | Pass | 48. |
| Planned-component reverse-index keys | Pass | 113. |
| Test/evidence reverse-index keys | Pass | 71. |
| Evidence-path reverse-index keys | Pass | 69. |
| Evidence is version-controlled or independently backed up | Pass | Documents and workbook are in Git; the workbook has a matching SHA-256 record. |
| No unresolved contradiction affects the claim | Pass with boundary | Final implementation and executed-test links remain ongoing under G-M03; that does not invalidate the completed planning RTM. |

**Validation result:** Validated

**Validation conclusion:**  
EP-005 is verified because the project has a complete forward RTM and reviewable reverse indexes with no active traceability gaps. The evidence validates the mapping system, not final completion of every implementation and test.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `G-M02`, `G-M03`, `G-M06`, and all active requirement IDs represented by the RTM |
| Work package(s) | `PD-04` |
| Engineering practice(s) | `EP-005`, related `EP-004`, `EP-006` |
| Objective(s) | `O1`–`O11` |
| Research question(s) | `RQ1`, `RQ2`, `RQ3`, `RQ4`, `RQ-TV` |
| Test / experiment / evidence IDs | Requirement-specific acceptance and evidence IDs; bidirectional-traceability audit |

## 8. Limitations, gaps and follow-up

- G-M03 remains open until the final release audit links actual implementation files, GitHub issues, commits, executed tests and final evidence to every active Must Have.
- Reverse-index counts must be regenerated after any approved requirement, work-package, component, test/evidence or evidence-path change.
- A category presentation change does not alter traceability, but a material scope change requires a repeat audit.
- Supervisor review of the working baseline remains pending and is not claimed.

## 9. Change control

Any later change that affects this evidence claim must update:

1. the controlled RTM workbook;
2. the generated RTM and reverse indexes;
3. `Bidirectional-Traceability-Audit.md`;
4. this EP-005 evidence record;
5. the related G-M02, PD-04 and EP-004 evidence records;
6. the Requirements and Scope Change Log and Change Request and Decision Register;
7. the Task Checklist, Dashboard and affected report sections.