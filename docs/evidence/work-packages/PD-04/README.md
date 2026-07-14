# PD-04 — Must-Have RTM and acceptance criteria

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `WP:PD-04` |
| Record type | Work Package |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B |
| Evidence date | `2026-07-14` |
| Validation date | `2026-07-14` |
| Validator | Arian B |
| Validation method | Must-Have coverage audit, RTM field-completeness review and cross-link inspection |

## 2. Statement being evidenced

> Create and maintain the Must-Have requirements traceability matrix and measurable acceptance criteria.

## 3. Definition of Done or acceptance criteria

- [x] Every active Must Have has a stable requirement ID and statement.
- [x] Every active Must Have records a source and rationale.
- [x] Every active Must Have maps to objectives or research questions.
- [x] Every active Must Have maps to one or more work packages.
- [x] Every active Must Have maps to a planned implementation component.
- [x] Every active Must Have has measurable acceptance criteria.
- [x] Every active Must Have has a verification method.
- [x] Every active Must Have has a test/evidence ID or evidence path and an owner.
- [x] Forward and reverse traceability views are available.
- [x] The controlled workbook record, generated Markdown and evidence records agree on the reviewed baseline counts.

## 4. Evidence summary

PD-04 produced the controlled RTM v1.3 baseline for the 56 active Must Haves. The coverage audit confirms that all 56 records contain the planning and verification fields required to guide later implementation and testing. The reverse-index document allows objectives, research questions, work packages, components, test/evidence IDs and evidence paths to be traced back to requirements.

The work package proves completeness of the requirements and acceptance design. It does not claim that the later code, experiments or tests for all 56 Must Haves have passed.

## 5. Authoritative evidence

| Evidence item | Repository path or external controlled location | What it proves | Status |
|---|---|---|---|
| Requirements Traceability Matrix v1.3 | [Requirements-Traceability-Matrix-v1.3.md](../../../requirements/Requirements-Traceability-Matrix-v1.3.md) | Provides the forward requirement mappings and complete acceptance/verification records. | Available |
| Traceability reverse indexes | [Traceability-Reverse-Indexes-v1.3.md](../../../requirements/Traceability-Reverse-Indexes-v1.3.md) | Provides reverse links from objectives, RQs, WPs, components, test/evidence IDs and evidence paths. | Available |
| Controlled workbook artifact record | [RTM-Workbook-Artifact-Record.md](../../../requirements/RTM-Workbook-Artifact-Record.md) | Controls the exact binary filename, size, SHA-256, package and recovery/placement procedure. | Available |
| Workbook integrity record | [RTM-Working-Baseline-SHA256.txt](../../../requirements/RTM-Working-Baseline-SHA256.txt) | Identifies the reviewed workbook by SHA-256. | Available |
| MoSCoW Requirements Baseline v1.2 | [MoSCoW-Requirements-v1.2.md](../../../requirements/MoSCoW-Requirements-v1.2.md) | Identifies the current priority and lifecycle boundary. | Available |
| Project Definition v1.1 | [Project-Definition-v1.1.md](../../../planning/Project-Definition-v1.1.md) | Provides the objectives, research questions and scope used by the RTM. | Available |
| Must-Have coverage audit | [Must-Have-Coverage-Audit.md](Must-Have-Coverage-Audit.md) | Records 56/56 completion for each required planning field. | Available |
| Related G-M02 evidence | [G-M02 evidence record](../../requirements/G-M02/README.md) | Validates the versioned MoSCoW baseline used by PD-04. | Available |
| Related EP-005 evidence | [EP-005 evidence record](../../engineering-practices/EP-005/README.md) | Validates the bidirectional traceability structure. | Available |
| Common evidence template | [Evidence-Record-Template.md](../../templates/Evidence-Record-Template.md) | Confirms this README follows the repository evidence-record structure. | Available |
| Scope baseline pull request | [PR #14](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/14) | Preserves the reviewed MoSCoW and RTM completion changes. | Available |
| Catalogue-organisation pull request | [PR #15](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/15) | Preserves the categorised requirement views and audit. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass | RTM v1.3, reverse indexes, the workbook artifact record, checksum and coverage audit are present in Git; the binary is independently controlled. |
| Definition of Done checked | Pass | Every criterion in section 3 was checked against the controlled RTM. |
| Stable requirement ID and statement | Pass | 56 of 56 active Must Haves. |
| Source and rationale | Pass | 56 of 56. |
| Objective/RQ mapping | Pass | 56 of 56. |
| Work-package mapping | Pass | 56 of 56. |
| Planned component mapping | Pass | 56 of 56. |
| Measurable acceptance criteria | Pass | 56 of 56. |
| Verification method | Pass | 56 of 56. |
| Evidence path and owner | Pass | 56 of 56. |
| Reverse indexes available | Pass | Six reverse-index families are version-controlled. |
| Evidence is version-controlled or independently backed up | Pass | Markdown records and checksums are in Git; the exact binary is retained in the controlled completion package and identified by filename, size and SHA-256. |
| No unresolved contradiction affects the claim | Pass with limitation | Direct binary Git placement remains pending, but the artifact status is explicit and no broken link is used. |

**Validation result:** Validated

**Validation conclusion:**  
PD-04 is verified because the complete Must-Have RTM and acceptance design exist and have been checked for coverage. This validation concerns planning and traceability completeness; it does not prove that the 56 product implementations or their later tests are complete.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `G-M02`, `G-M03`, and all 56 active Must Haves |
| Work package(s) | `PD-04` |
| Engineering practice(s) | `EP-004`, `EP-005`, `EP-006` |
| Objective(s) | `O1`–`O11`, as mapped per requirement |
| Research question(s) | `RQ1`, `RQ2`, `RQ3`, `RQ4`, `RQ-TV`, as mapped per requirement |
| Test / experiment / evidence IDs | `ART-RTM-XLSX-001`; requirement-specific acceptance IDs; Must-Have coverage audit; bidirectional-traceability audit |

## 8. Limitations, gaps and follow-up

- PD-04 validates the design and coverage of the RTM, not completion of all implementation work.
- Direct placement of the exact `.xlsx` binary in Git remains pending; the artifact record and checksum prevent an untraceable or broken reference.
- GitHub issue, commit, executed-test and final-evidence links remain to be populated for individual requirements during development.
- G-M03 remains open until final release traceability proves every active Must Have against implementation and executed evidence.
- A future approved scope change requires a repeat coverage audit for all affected Must Haves.

## 9. Change control

Any later change that affects this evidence claim must update:

1. the controlled RTM workbook artifact and checksum;
2. the generated RTM and reverse indexes;
3. `Must-Have-Coverage-Audit.md`;
4. this PD-04 evidence record;
5. the related G-M02, EP-004 and EP-005 evidence records;
6. the requirements and scope change-control records;
7. the Task Checklist, Dashboard and affected report sections.