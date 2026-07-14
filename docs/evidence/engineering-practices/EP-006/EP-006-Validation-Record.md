# EP-006 Validation Record

**Document ID:** REC-EP006-001  
**Version:** 1.0  
**Status:** Baselined  
**Owner:** Arian B.  
**Last reviewed:** 2026-07-14

| No. | Validation check | Result | Evidence |
|---|---|---|---|
| 1 | A controlled Derived Requirements Register exists. | Pass | REG-DR-001 |
| 2 | Every active derived requirement has a stable ID, date, source/parent, rationale, priority, status and owner. | Pass | REG-DR-001 Section A |
| 3 | Every active derived requirement has affected artefacts, acceptance criteria, verification method, evidence and approval state. | Pass | REG-DR-001 Section B |
| 4 | A project Requirements and Scope Change Log exists and retains the earlier RTM change history. | Pass | LOG-REQ-CHG-001 |
| 5 | Each change records date, ID, description, reason, affected IDs, decision/status, approval and evidence. | Pass | LOG-REQ-CHG-001 |
| 6 | A Change Request and Decision Register records request origin, impact, rationale, decision owner, approval state and closure. | Pass | LOG-CRD-001 |
| 7 | Unapproved external decisions are explicitly marked pending rather than presented as approved. | Pass | CHG-002 to CHG-004; CHG-009; CR-007 |
| 8 | The Workflow Register links the EP-006 control documents. | Pass | Workflow Register v1.2 |
| 9 | The Workflow Change Log records the addition of the EP-006 control set. | Pass | CHG-WF-014 to CHG-WF-016 |
| 10 | Repository evidence path and commit hash are recorded after integration. | Pass after merge | The branch and pull request provide repository evidence; replace the pending reference with the merge commit after merge. |

## Repository evidence

- Evidence path: `docs/evidence/engineering-practices/EP-006/`
- Git evidence: `docs/ep-006-change-control-logs` branch and associated pull request
- Merge commit hash: `[record after merge]`
- Final status: Implemented and validated on the change branch; Verified after pull-request merge.
