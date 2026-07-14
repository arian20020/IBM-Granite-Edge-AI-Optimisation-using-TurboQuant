# Requirements and Scope Change Log

**Document ID:** LOG-REQ-CHG-001  
**Version:** 1.2  
**Status:** Baselined  
**Owner:** Arian B  
**Last reviewed:** 2026-07-14

| Date | Change ID | Description | Reason | Affected IDs | Decision / status | Approved by | Evidence / link |
|---|---|---|---|---|---|---|---|
| 2026-07-11 | CHG-001 | Imported MoSCoW Requirements Baseline v1.1. | Create the first controlled requirements catalogue. | All v1.1 IDs | Recorded | Arian B | RTM change history |
| 2026-07-13 | CHG-002 | Proposed promotion of model download, streaming/two-turn chat, CLI adapter and GGUF processing into Must scope. | Align the draft with the expanded intended product. | F-M16–F-M24 | Superseded by CHG-013 decision | Pending at time raised | Project Definition v1.0 draft |
| 2026-07-13 | CHG-003 | Proposed an app-integrated TurboQuant Must Have with dependable upstream fallback. | Require TurboQuant to run through the application while remaining Experimental. | F-M21; F-M22; N-M11 | Approved with bounded technical conditions by CHG-013 | Arian B | MoSCoW v1.2; RTM v1.3 |
| 2026-07-13 | CHG-004 | Proposed bounded TurboVec knowledge-file/vector/retrieval scope. | Explore local vector compression and retrieval. | F-M25–F-M27; R-M02; R-M13 | Full integration deferred by CHG-013; decision gate retained | Arian B | ADR-TurboVec; MoSCoW v1.2 |
| 2026-07-13 | CHG-005 | Created Excel master RTM and GitHub Markdown snapshot. | Close the traceability gap. | G-M03; PD-04 | Implemented | Arian B | RTM workbook and Markdown |
| 2026-07-13 | CHG-006 | Added a numbered master Task Checklist and synchronised detailed-tab statuses. | Provide one place to manage active work and validation. | All active IDs | Implemented | Arian B | Task Checklist |
| 2026-07-13 | CHG-007 | Corrected and version-controlled the workflow baseline. | Resolve naming and technical contradictions. | PD-02; EP-003; DR-WF-001–DR-WF-017 | Implemented | Arian B | Workflow package v1.1 |
| 2026-07-13 | CHG-008 | Separated runtime KV-cache configuration from persistent model conversion. | Prevent runtime settings being presented as new model files. | DR-WF-005; DR-WF-006; DR-WF-017 | Implemented | Arian B | Workflow baseline |
| 2026-07-13 | CHG-009 | Clarified OpenVINO TurboQuant as experimental/pinned and NPU as unvalidated. | Align capability claims with evidence. | DR-WF-011; DR-WF-012; F-M21; F-M22; N-M11 | Working technical boundary | Arian B; external review pending | OpenVINO/hardware workflows |
| 2026-07-14 | CHG-010 | Improved workflow text clarity and page fit. | Keep documentation legible without redesigning screens. | DR-WF-018 | Implemented | Arian B | Workflow package v1.1 |
| 2026-07-14 | CHG-011 | Established EP-006 derived-requirement and change-control records. | Create stable, reviewable change records. | EP-006; PD-04 | Implemented and validated | Arian B | PR #10; EP-006 evidence |
| 2026-07-14 | CHG-012 | Froze the app-specific evaluation addendum. | Control remaining evaluation IDs, schemas, gates and claims. | PD-09; EP-019; APP-EVAL-01–12 | Implemented and validated | Arian B | PR #11/#12 |
| 2026-07-14 | CHG-013 | Resolved CHG-002, CHG-003 and CHG-004 and froze the MoSCoW v1.2 working baseline. | Protect the essential first-release route, retain one bounded TurboQuant contribution and defer unproven TurboVec integration. | F-M16–F-M27; R-M02; R-M13; G-M02; PD-04; EP-004; EP-005 | Developer-approved working baseline; supervisor review pending | Arian B | Project Definition v1.1; MoSCoW v1.2; RTM v1.3; CR-013 |
| 2026-07-14 | CHG-014 | Split the requirements catalogue into separate Functional, Non-Functional, Research, Governance and Exclusion views. | Improve readability and reviewability without changing controlled requirement content. | All requirement lifecycle records; MoSCoW v1.2; RTM v1.3 | Implemented — presentation only; no scope change | Arian B | Categorised workbook; repository category catalogues; CR-014 |

## CHG-013 priority results

| ID | Result |
|---|---|
| F-M16; F-M17 | Active Should Haves |
| F-M18 | Remains Must |
| F-M19 | Remains Must with core loading/generation acceptance; optional routes inherit the rule if implemented |
| F-M20 | Active Should Have |
| F-M21; F-M22; N-M11 | Remain bounded Must Haves |
| F-M23; F-M24 | Active Should Haves |
| R-M02 | Remains Must as the TurboVec decision gate |
| F-M25; F-M26; F-M27; R-M13 | Deferred from first release |

## CHG-014 control statement

CHG-014 changes presentation only. It does not alter any requirement ID, priority, release role, lifecycle state, acceptance criterion, verification method or traceability relationship.

Future material changes must append a new row. Do not overwrite CHG-013 or CHG-014.
