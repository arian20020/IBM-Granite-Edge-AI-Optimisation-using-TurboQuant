# EP-004 — Maintain stable MoSCoW catalogue

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `EP:EP-004` |
| Record type | Engineering Practice |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B |
| Evidence date | `2026-07-14` |
| Validation date | `2026-07-14` |
| Validator | Arian B |
| Validation method | Catalogue integrity audit, lifecycle review, change-control inspection and repository-link check |

## 2. Statement being evidenced

> Maintain a stable, versioned MoSCoW requirements catalogue.

## 3. Definition of Done or acceptance criteria

- [x] One current MoSCoW baseline is identified and versioned.
- [x] Requirement IDs remain stable when only priority or release role changes.
- [x] Active, Deferred, Superseded and Excluded lifecycle records are preserved.
- [x] No requirement ID appears more than once.
- [x] Baseline counts agree across the workbook record, Markdown catalogue and audits.
- [x] Material scope and priority changes are dated, owned and linked to decisions.
- [x] Presentation-only changes are distinguished from scope changes.
- [x] Supervisor-review state is shown honestly.
- [x] The readable category catalogues cover every lifecycle record exactly once.

## 4. Evidence summary

EP-004 was applied by freezing MoSCoW v1.2, preserving stable IDs through CHG-013 and retaining the full lifecycle history. CHG-014 split the catalogue into readable Functional, Non-Functional, Research, Governance and Exclusion views while preserving the original authoritative RTM table and every controlled requirement field.

The stable-catalogue audit records 111 lifecycle records, 72 active requirements, 56 active Must Haves, 14 active Should Haves, 2 active Could Haves, 11 Deferred, 10 Superseded and 18 Excluded records, with zero duplicate IDs.

## 5. Authoritative evidence

| Evidence item | Repository path or external controlled location | What it proves | Status |
|---|---|---|---|
| MoSCoW Requirements Baseline v1.2 | [MoSCoW-Requirements-v1.2.md](../../../requirements/MoSCoW-Requirements-v1.2.md) | Identifies the current baseline, priority boundary and separate category catalogues. | Available |
| Controlled workbook artifact record | [RTM-Workbook-Artifact-Record.md](../../../requirements/RTM-Workbook-Artifact-Record.md) | Controls the exact binary filename, size, SHA-256, package and recovery/placement procedure. | Available |
| Workbook integrity record | [RTM-Working-Baseline-SHA256.txt](../../../requirements/RTM-Working-Baseline-SHA256.txt) | Identifies the reviewed workbook by SHA-256. | Available |
| Stable catalogue audit | [Stable-Catalogue-Audit.md](Stable-Catalogue-Audit.md) | Records the version, counts, lifecycle and duplicate-ID checks. | Available |
| Categorised catalogue audit | [Categorised-Requirements-Catalogue-Audit.md](../../../requirements/Categorised-Requirements-Catalogue-Audit.md) | Confirms all 111 records appear in one and only one readable category view. | Available |
| Functional catalogue | [Functional-Requirements.md](../../../requirements/catalogue/Functional-Requirements.md) | Provides the functional requirement view. | Available |
| Non-Functional catalogue | [Non-Functional-Requirements.md](../../../requirements/catalogue/Non-Functional-Requirements.md) | Provides the non-functional requirement view. | Available |
| Research catalogue | [Research-Requirements.md](../../../requirements/catalogue/Research-Requirements.md) | Provides the research requirement view. | Available |
| Governance catalogue | [Governance-Requirements.md](../../../requirements/catalogue/Governance-Requirements.md) | Provides the governance requirement view. | Available |
| Exclusions and boundaries | [Exclusions-and-Boundaries.md](../../../requirements/catalogue/Exclusions-and-Boundaries.md) | Preserves explicit Won't Have and claim boundaries. | Available |
| Requirements and scope change log | [Requirements-and-Scope-Change-Log.md](../../../change-control/Requirements-and-Scope-Change-Log.md) | Retains CHG-013 and CHG-014 without rewriting history. | Available |
| Change request and decision register | [Change-Request-and-Decision-Register.md](../../../change-control/Change-Request-and-Decision-Register.md) | Records CR-013 and CR-014 decisions, rationale and impact. | Available |
| Common evidence template | [Evidence-Record-Template.md](../../templates/Evidence-Record-Template.md) | Confirms this README follows the repository evidence-record structure. | Available |
| Catalogue-organisation pull request | [PR #15](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/15) | Preserves the category split and audit in Git history. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass | MoSCoW v1.2, the category catalogues, workbook artifact record and checksum are present in Git; the exact binary is independently controlled. |
| Definition of Done checked | Pass | Every criterion in section 3 was checked against the controlled RTM exports and audits. |
| One current baseline | Pass | MoSCoW v1.2 / RTM v1.3. |
| Duplicate requirement IDs | Pass | 0. |
| Active requirement count | Pass | 72. |
| Active Must / Should / Could counts | Pass | 56 / 14 / 2. |
| Lifecycle history retained | Pass | 11 Deferred, 10 Superseded, 18 Excluded. |
| Stable IDs retained across priority changes | Pass | Priority-only changes did not rename IDs. |
| Category coverage | Pass | 111 of 111 lifecycle records appear in exactly one category view. |
| Change decisions dated and owned | Pass | CHG-013/CR-013 and CHG-014/CR-014 are dated and owned by Arian B. |
| Evidence is version-controlled or independently backed up | Pass | Documents and checksums are in Git; the exact binary is retained in the controlled completion package and identified by filename, size and SHA-256. |
| No unresolved contradiction affects the claim | Pass with limitation | Supervisor review and direct binary Git placement remain pending, and both states are explicit. |

**Validation result:** Validated

**Validation conclusion:**  
EP-004 is verified because one stable, versioned and internally consistent MoSCoW catalogue exists, its lifecycle history is preserved and all records have readable category views. This does not validate implementation of the product requirements themselves.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `G-M02`, related `G-M01`, `G-M03`, `G-M06` |
| Work package(s) | `PD-04` |
| Engineering practice(s) | `EP-004`, related `EP-005`, `EP-006` |
| Objective(s) | `O11` |
| Research question(s) | All controlled RQs through requirement mappings |
| Test / experiment / evidence IDs | `ART-RTM-XLSX-001`; stable-catalogue audit; categorised-catalogue audit; MoSCoW review checklist |

## 8. Limitations, gaps and follow-up

- Supervisor review remains pending and is not claimed.
- Direct placement of the exact `.xlsx` binary in Git remains pending; the artifact record and checksum prevent a broken or false repository link.
- The category files are generated/readable views; controlled requirement edits must start in the authoritative workbook.
- Any material scope, priority or lifecycle change requires a new change record and repeat catalogue audit.
- Final implementation status remains governed by individual requirement evidence and the release-level G-M03 audit.

## 9. Change control

Any later change that affects this evidence claim must update:

1. the controlled RTM workbook artifact and checksum;
2. the MoSCoW baseline and affected category catalogue;
3. `Stable-Catalogue-Audit.md` and the categorised-catalogue audit;
4. this EP-004 evidence record;
5. the related G-M02, PD-04 and EP-005 evidence records;
6. the Requirements and Scope Change Log and Change Request and Decision Register;
7. the Task Checklist, Dashboard and affected report sections.