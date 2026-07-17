# Project Change-Control Index

**Status:** Controlling index  
**Owner:** Arian B.  
**Last reviewed:** 2026-07-14

This index links the append-only records used to control requirements, workflows, experiments and testing workbooks. Authoritative records remain in their specialist folders; this page avoids duplicating them.

## Requirements and scope

- [Derived Requirements Register](Derived-Requirements-Register.md) — stable derived IDs, parent/source, rationale, acceptance criteria, verification and approval state.
- [Requirements and Scope Change Log](Requirements-and-Scope-Change-Log.md) — dated changes to scope, requirements and controlled baselines.
- [Change Request and Decision Register](Change-Request-and-Decision-Register.md) — request origin, impact assessment, decision, approval and closure.

## Workflow control

- [Workflow Register](../workflows/change-control/Workflow-Register.md) — stable workflow IDs and controlling document map.
- [Workflow Change Log](../workflows/change-control/Workflow-Change-Log.md) — append-only history of workflow corrections and document-control revisions.

## Experiment and test-workbook control

- [Testing Decision Log](../testing/Decision-Log.md) — accepted testing and interpretation decisions.
- [Experiment and Test Change-Control Index](../testing/Experiment-and-Test-Change-Control-Index.md) — entry point for run, failure, workbook and evidence controls.
- [Workbook Revision Control](../testing/Workbook-Revision-Control.md) — mandatory workbook revision procedure.
- [Workbook Revision Register](../testing/Workbook-Revision-Register.csv) — append-only revision history for WB-01 to WB-06.
- [Workbook Completion Register](../testing/Workbook-Completion-Register.csv) — section/test completion and evidence-commit control.
- [Controlled Workbook Manifest](../testing/workbooks/Controlled-Workbook-Manifest.csv) — source, template and generated-DOCX hashes.

## Permanent rules

1. Never delete or silently rewrite a historical change, decision, failed run or superseded revision.
2. Use a new stable ID or revision row for later changes.
3. Keep measured results, estimates, published claims and decisions separate.
4. A document revision does not prove that a test passed.
5. A requirement or task is not Verified until its acceptance evidence is reviewed.
