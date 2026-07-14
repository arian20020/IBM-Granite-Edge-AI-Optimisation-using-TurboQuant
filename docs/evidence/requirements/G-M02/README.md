# G-M02 — Versioned MoSCoW requirements baseline

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `REQ:G-M02` |
| Record type | Requirement |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B |
| Evidence date | `2026-07-14` |
| Validation date | `2026-07-14` |
| Validator | Arian B |
| Validation method | Requirements-quality review, RTM completeness audit, catalogue integrity review and Git history inspection |

## 2. Statement being evidenced

> The project must maintain one versioned MoSCoW requirements baseline with stable IDs and measurable acceptance criteria.

## 3. Definition of Done or acceptance criteria

- [x] One current versioned MoSCoW baseline is identified.
- [x] Every active Must Have has a stable requirement ID.
- [x] Every active Must Have has a source and rationale.
- [x] Every active Must Have maps to objectives or research questions and work packages.
- [x] Every active Must Have maps to a planned component.
- [x] Every active Must Have has measurable acceptance criteria.
- [x] Every active Must Have has a verification method, evidence path and owner.
- [x] Priority and lifecycle changes are retained through dated change control.
- [x] Supervisor-review status is stated honestly and no unreceived approval is claimed.

## 4. Evidence summary

MoSCoW v1.2 and RTM v1.3 form the controlled requirements baseline. The RTM records 111 lifecycle records, including 72 active requirements and 56 active Must Haves. The validation review found no duplicate IDs and no active Must Have missing the fields required by G-M02.

CHG-013 froze the first-release priority boundary. CHG-014 reorganised the presentation into separate Functional, Non-Functional, Research, Governance and Exclusion catalogues without changing controlled requirement content. The original full RTM remains authoritative.

## 5. Authoritative evidence

| Evidence item | Repository path or external controlled location | What it proves | Status |
|---|---|---|---|
| MoSCoW Requirements Baseline v1.2 | [MoSCoW-Requirements-v1.2.md](../../../requirements/MoSCoW-Requirements-v1.2.md) | Identifies the approved working baseline, counts, scope decisions and category catalogues. | Available |
| Requirements Traceability Matrix v1.3 | [Requirements-Traceability-Matrix-v1.3.md](../../../requirements/Requirements-Traceability-Matrix-v1.3.md) | Records the requirement definitions, mappings, acceptance criteria, verification methods and evidence paths. | Available |
| Controlled workbook artifact record | [RTM-Workbook-Artifact-Record.md](../../../requirements/RTM-Workbook-Artifact-Record.md) | Controls the exact binary filename, size, SHA-256, package and recovery/placement procedure without using a broken repository link. | Available |
| Workbook integrity record | [RTM-Working-Baseline-SHA256.txt](../../../requirements/RTM-Working-Baseline-SHA256.txt) | Identifies the reviewed workbook by SHA-256. | Available |
| Project Definition v1.1 | [Project-Definition-v1.1.md](../../../planning/Project-Definition-v1.1.md) | Defines the aim, objectives, research questions and agreed first-release boundary. | Available |
| MoSCoW review checklist | [MoSCoW-v1.2-Review-Checklist.md](MoSCoW-v1.2-Review-Checklist.md) | Records the individual requirement-quality and catalogue checks. | Available |
| Categorised catalogue audit | [Categorised-Requirements-Catalogue-Audit.md](../../../requirements/Categorised-Requirements-Catalogue-Audit.md) | Confirms all 111 lifecycle records appear in exactly one readable category view with no scope change. | Available |
| Requirements and scope change log | [Requirements-and-Scope-Change-Log.md](../../../change-control/Requirements-and-Scope-Change-Log.md) | Retains CHG-013 and CHG-014 and their affected IDs and decisions. | Available |
| Change request and decision register | [Change-Request-and-Decision-Register.md](../../../change-control/Change-Request-and-Decision-Register.md) | Retains CR-013 and CR-014 with decision owner, rationale, impact and evidence. | Available |
| Common evidence template | [Evidence-Record-Template.md](../../templates/Evidence-Record-Template.md) | Confirms this README follows the repository evidence-record structure. | Available |
| Scope baseline pull request | [PR #14](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/14) | Preserves the MoSCoW v1.2 and RTM v1.3 completion review. | Available |
| Catalogue-organisation pull request | [PR #15](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/15) | Preserves the category split and CHG-014 audit. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass | MoSCoW v1.2, RTM v1.3, the checksum and the controlled workbook artifact record are present in Git; the exact binary is independently controlled by package and SHA-256. |
| Definition of Done checked | Pass | All criteria in section 3 were checked against the RTM and review checklist. |
| Active Must Haves reviewed | Pass | 56 of 56. |
| Stable IDs | Pass | 56 of 56 active Must Haves; 0 duplicate lifecycle-record IDs. |
| Source and rationale | Pass | 56 of 56. |
| Objective/RQ and work-package mapping | Pass | 56 of 56. |
| Planned component mapping | Pass | 56 of 56. |
| Measurable acceptance criteria | Pass | 56 of 56. |
| Verification method, evidence path and owner | Pass | 56 of 56. |
| Lifecycle history retained | Pass | 11 Deferred, 10 Superseded and 18 Excluded records remain visible. |
| Evidence is version-controlled or independently backed up | Pass | Human-readable records and checksums are in Git; the exact binary is independently retained in the controlled completion package and identified by filename, size and SHA-256. |
| No unresolved contradiction affects the claim | Pass with limitation | Supervisor review and direct binary Git placement remain pending, and both states are stated explicitly. |

**Validation result:** Validated

**Validation conclusion:**  
G-M02 is verified as a requirements-governance deliverable. The validation proves that one complete, versioned and traceable MoSCoW baseline exists. It does not prove that all active product requirements have been implemented or tested.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `G-M02`; related `G-M01`, `G-M03`, `G-M06` |
| Work package(s) | `PD-04` |
| Engineering practice(s) | `EP-004`, `EP-005`, `EP-006` |
| Objective(s) | `O11` |
| Research question(s) | `RQ1`, `RQ2`, `RQ3`, `RQ4`, `RQ-TV` through the controlled requirement mappings |
| Test / experiment / evidence IDs | `AC-G-M02`; `ART-RTM-XLSX-001`; MoSCoW review checklist; stable-catalogue audit; Must-Have coverage audit; bidirectional-traceability audit |

## 8. Limitations, gaps and follow-up

- Supervisor review is pending; no supervisor approval is claimed.
- Direct placement of the exact `.xlsx` binary in Git remains pending; the repository contains its artifact record, checksum, human-readable exports and recovery instructions.
- This evidence validates the requirements baseline, not implementation of all 56 active Must Haves.
- Application-code, test-result, commit and final release evidence will continue to be added under the individual requirement records and the broader G-M03 release audit.
- Any material scope or priority change requires a new change request and revalidation of the affected catalogue counts and mappings.

## 9. Change control

Any later change that affects this evidence claim must update:

1. the controlled RTM workbook artifact and checksum;
2. `docs/requirements/MoSCoW-Requirements-v1.2.md` or a versioned successor;
3. the generated RTM and category catalogues;
4. this G-M02 evidence record and its review checklist;
5. the related PD-04, EP-004 and EP-005 evidence records;
6. the Requirements and Scope Change Log and Change Request and Decision Register;
7. the Task Checklist, Dashboard and affected report sections.