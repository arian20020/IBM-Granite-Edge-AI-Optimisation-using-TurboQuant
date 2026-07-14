# EP-006 — Maintain derived-requirement and change log

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `EP:EP-006` |
| Record type | Engineering Practice |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified after merge |
| Owner | Arian B. |
| Evidence date | 2026-07-14 |
| Validation date | 2026-07-14 |
| Validator | Project developer; repository review pending merge |
| Validation method | Document review, cross-link inspection and Git history review |

## 2. Statement being evidenced

> Maintain derived-requirement and change log.

## 3. Definition of Done

- [x] Stable derived-requirement IDs exist.
- [x] Every derived requirement records its source, rationale, acceptance criteria, verification method, evidence and approval state.
- [x] Requirement and scope changes are dated and retain affected IDs, decisions, approval state and evidence.
- [x] Change requests record origin, impact, decision, owner and closure state.
- [x] Workflow changes are retained in an append-only workflow change log.
- [x] Experiment and test-workbook changes are linked to the existing controlled testing registers.
- [x] The records are stored in Git-reviewable formats and linked through a central index.

## 4. Evidence summary

The repository now contains one connected change-control system for requirements, workflows, experiments and testing workbooks. It reuses the existing authoritative testing decision and workbook revision controls rather than creating contradictory duplicate logs.

## 5. Authoritative evidence

| Evidence item | Repository path | What it proves | Status |
|---|---|---|---|
| Change-control index | [`../../../change-control/README.md`](../../../change-control/README.md) | One entry point links all change-control records. | Available |
| Derived Requirements Register | [`../../../change-control/Derived-Requirements-Register.md`](../../../change-control/Derived-Requirements-Register.md) | Derived requirements have stable IDs and verification data. | Available |
| Requirements and Scope Change Log | [`../../../change-control/Requirements-and-Scope-Change-Log.md`](../../../change-control/Requirements-and-Scope-Change-Log.md) | Baseline changes are dated and retained. | Available |
| Change Request and Decision Register | [`../../../change-control/Change-Request-and-Decision-Register.md`](../../../change-control/Change-Request-and-Decision-Register.md) | Requests, impacts, decisions and approvals are controlled. | Available |
| Workflow Change Log | [`../../../workflows/change-control/Workflow-Change-Log.md`](../../../workflows/change-control/Workflow-Change-Log.md) | Workflow corrections and document revisions are retained. | Available |
| Testing Decision Log | [`../../../testing/Decision-Log.md`](../../../testing/Decision-Log.md) | Testing-scope and interpretation decisions are controlled. | Available |
| Workbook Revision Register | [`../../../testing/Workbook-Revision-Register.csv`](../../../testing/Workbook-Revision-Register.csv) | WB-01 to WB-06 revision history is append-only. | Available |
| Experiment/Test Change-Control Index | [`../../../testing/Experiment-and-Test-Change-Control-Index.md`](../../../testing/Experiment-and-Test-Change-Control-Index.md) | Experiment, run, failure, evidence and workbook controls are connected. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverables exist | Pass | All linked Markdown/CSV records exist on the change branch. |
| Definition of Done checked | Pass | All criteria above are satisfied. |
| Evidence is version-controlled | Pass after merge | Branch and pull request retain reviewable history. |
| Existing testing logs were reused | Pass | Decision-Log.md and workbook revision controls remain authoritative. |
| No unresolved contradiction affects the claim | Pass | Pending technical/supervisor decisions remain explicitly Pending/Open. |

**Validation result:** Validated; effective status becomes Verified when this pull request is merged.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `REQ:G-M02`, `REQ:G-M03` where applicable |
| Work package(s) | `WP:PD-04` |
| Engineering practice(s) | `EP:EP-006`, related `EP:EP-003` |
| Test / experiment / evidence IDs | `WB-01`–`WB-06`, testing decision IDs `TD-*` |

## 8. Limitations and follow-up

- Technical or supervisor approval remains pending for the release-scope decisions explicitly marked Pending/Open in the registers.
- Future changes must append a new record rather than overwrite history.

## 9. Change control

Any later change affecting this evidence claim must update the authoritative register, this evidence README, the RTM/Task Checklist and the project change log.
