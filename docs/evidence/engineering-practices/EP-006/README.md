# EP-006 — Maintain derived-requirement and change log

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `EP:EP-006` |
| Record type | Engineering Practice |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B |
| Evidence date | `2026-07-14` |
| Validation date | `2026-07-14` |
| Validator | Arian B; repository review through PR #10 and later requirements-control reviews |
| Validation method | Document review, cross-link inspection, change-register audit and Git history review |

## 2. Statement being evidenced

> Maintain derived-requirement and change log.

## 3. Definition of Done or acceptance criteria

- [x] Stable derived-requirement IDs exist.
- [x] Every derived requirement records its source, rationale, acceptance criteria, verification method, evidence and approval state.
- [x] Requirement and scope changes are dated and retain affected IDs, decisions, approval state and evidence.
- [x] Change requests record origin, impact, decision, owner and closure state.
- [x] Workflow changes are retained in an append-only workflow change log.
- [x] Experiment and test-workbook changes are linked to the controlled testing registers.
- [x] MoSCoW scope decisions CHG-013/CR-013 and presentation decision CHG-014/CR-014 are retained without rewriting earlier proposals.
- [x] The records are stored in Git-reviewable formats and linked through a central index.
- [x] Pending technical or supervisor decisions remain visibly pending.

## 4. Evidence summary

The repository contains a connected change-control system for requirements, workflows, experiments and testing workbooks. It reuses the existing authoritative testing decision and workbook-revision controls rather than creating contradictory duplicate logs.

CHG-013/CR-013 preserve the decision that froze MoSCoW v1.2, including the exact priority and deferral outcomes. CHG-014/CR-014 preserve the later presentation-only decision that split the requirements into category catalogues. The original CHG-002, CHG-003 and CHG-004 proposals remain visible in history and are not silently rewritten.

## 5. Authoritative evidence

| Evidence item | Repository path or external controlled location | What it proves | Status |
|---|---|---|---|
| Change-control index | [change-control README](../../../change-control/README.md) | Provides one entry point for requirements, workflow and testing change records. | Available |
| Derived Requirements Register | [Derived-Requirements-Register.md](../../../change-control/Derived-Requirements-Register.md) | Records derived requirements with stable IDs and verification data. | Available |
| Requirements and Scope Change Log | [Requirements-and-Scope-Change-Log.md](../../../change-control/Requirements-and-Scope-Change-Log.md) | Retains CHG-001 onward, including CHG-013 and CHG-014. | Available |
| Change Request and Decision Register | [Change-Request-and-Decision-Register.md](../../../change-control/Change-Request-and-Decision-Register.md) | Records requests, impacts, decisions, owners, approval states and closure. | Available |
| Project Change Log pointer | [planning Change-Log.md](../../../planning/Change-Log.md) | Directs readers to the authoritative registers and summarises the frozen baseline decisions. | Available |
| Workflow Change Log | [Workflow-Change-Log.md](../../../workflows/change-control/Workflow-Change-Log.md) | Preserves workflow corrections and document revisions. | Available |
| Testing Decision Log | [Decision-Log.md](../../../testing/Decision-Log.md) | Controls testing-scope and interpretation decisions. | Available |
| Workbook Revision Register | [Workbook-Revision-Register.csv](../../../testing/Workbook-Revision-Register.csv) | Preserves append-only workbook revision history. | Available |
| Experiment/Test Change-Control Index | [Experiment-and-Test-Change-Control-Index.md](../../../testing/Experiment-and-Test-Change-Control-Index.md) | Connects experiment, run, failure, evidence and workbook controls. | Available |
| Controlled workbook artifact record | [RTM-Workbook-Artifact-Record.md](../../../requirements/RTM-Workbook-Artifact-Record.md) | Controls the exact binary filename, size, SHA-256, package and recovery/placement procedure. | Available |
| Workbook integrity record | [RTM-Working-Baseline-SHA256.txt](../../../requirements/RTM-Working-Baseline-SHA256.txt) | Identifies the controlled workbook by SHA-256. | Available |
| Common evidence template | [Evidence-Record-Template.md](../../templates/Evidence-Record-Template.md) | Confirms this README follows the repository evidence-record structure. | Available |
| Original EP-006 implementation review | [PR #10](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/10) | Preserves the initial derived-requirement and change-control implementation. | Available |
| MoSCoW baseline review | [PR #14](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/14) | Preserves CHG-013/CR-013 and the baseline validation records. | Available |
| Catalogue-organisation review | [PR #15](https://github.com/arian20020/IBM-Granite-Edge-AI-Optimisation-using-TurboQuant/pull/15) | Preserves CHG-014/CR-014 and the categorised-catalogue audit. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverables exist | Pass | All linked Markdown/CSV records and the workbook artifact record are present in version control. |
| Definition of Done checked | Pass | Every criterion in section 3 is satisfied. |
| Stable derived-requirement IDs | Pass | The Derived Requirements Register retains stable `DR-*` identifiers. |
| Requirements/scope changes retained | Pass | CHG-001 onward remain append-only; CHG-013 and CHG-014 resolve later decisions without erasing history. |
| Requests and decisions linked | Pass | CR-001 onward record requester, category, affected IDs, decision and state. |
| Testing logs reused | Pass | Decision-Log.md and workbook revision controls remain authoritative. |
| Evidence is version-controlled or independently backed up | Pass | Change records and checksums are in Git; the exact workbook binary is independently retained in the controlled completion package and identified by filename, size and SHA-256. |
| No unresolved contradiction affects the claim | Pass | Pending technical/supervisor decisions and direct binary Git placement remain explicitly Pending or Open. |

**Validation result:** Validated

**Validation conclusion:**  
EP-006 is verified because the project has an operational, version-controlled and cross-linked change-control system. The validation proves that changes and decisions are recorded and reviewable; it does not imply that every open technical or supervisor decision has been approved.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `G-M02`, `G-M03`, `G-M06` |
| Work package(s) | `PD-04`, `PD-08`, `FR-05` where applicable |
| Engineering practice(s) | `EP-006`, related `EP-003`, `EP-004`, `EP-005`, `EP-019` |
| Objective(s) | `O11` |
| Research question(s) | All controlled RQs where a change affects their scope, method or evidence |
| Test / experiment / evidence IDs | `ART-RTM-XLSX-001`, `DR-*`, `CHG-*`, `CR-*`, `TD-*`, `WB-01`–`WB-06` |

## 8. Limitations, gaps and follow-up

- Technical or supervisor approval remains pending for decisions explicitly marked Pending or Open.
- Direct placement of the exact `.xlsx` binary in Git remains pending; its artifact record and checksum are version-controlled.
- Future changes must append a new record rather than overwrite previous proposals or decisions.
- G-M06 remains an ongoing release-level obligation: every later material change must link affected requirements, work packages, tests, evidence and report sections.
- The change-control audit must be repeated at final release.

## 9. Change control

Any later change that affects this evidence claim must update:

1. the relevant authoritative change register;
2. this EP-006 evidence record;
3. the controlled RTM artifact/checksum and Task Checklist status;
4. the affected requirement, work-package and engineering-practice evidence records;
5. the related tests, evidence and report sections;
6. the project change log where the controlled baseline changes.