# Requirements and Scope Change Log

**Document ID:** LOG-REQ-CHG-001  
**Version:** 1.0  
**Status:** Baselined  
**Owner:** Arian B.  
**Last reviewed:** 2026-07-14

| Date | Change ID | Description | Reason | Affected IDs | Decision / status | Approved by | Evidence / link |
|---|---|---|---|---|---|---|---|
| 2026-07-11 | CHG-001 | Imported MoSCoW Requirements Baseline v1.1. | Create the first controlled requirements catalogue. | All v1.1 IDs | Recorded | Project developer | RTM v1.3 Change Log |
| 2026-07-13 | CHG-002 | Promoted controlled model download, streaming/two-turn chat, CLI adapter and GGUF processing into Must scope. | Align requirements with the agreed aim, objectives and first-release scope. | F-M16-F-M24 and superseded v1.1 IDs | Draft - supervisor approval required | Pending | Project Definition v1 draft |
| 2026-07-13 | CHG-003 | Added an app-integrated TurboQuant Must Have with dependable upstream fallback. | TurboQuant must run through the application while remaining labelled Experimental. | F-M21; F-M22; N-M11 | Draft - supervisor approval required | Pending | Project Definition Section 5.2.11 |
| 2026-07-13 | CHG-004 | Added bounded TurboVec knowledge-file/vector/retrieval scope. | User confirmed TurboVec as intended scope; exact implementation remains gated. | F-M25-F-M27; R-M02; R-M13 | Draft - technical and supervisor gate required | Pending | Project Definition Section 5.2.17 |
| 2026-07-13 | CHG-005 | Created Excel master RTM and GitHub Markdown snapshot. | Close the highest-priority traceability gap. | G-M03; PD-04 | In progress | Project developer | RTM workbook and Markdown snapshot |
| 2026-07-13 | CHG-006 | Added a numbered master Task Checklist and synchronised detailed-tab statuses. | Provide one place to manage active requirements, work packages and engineering practices. | All active IDs | Implemented in RTM v1.3 Draft | Project developer | Task Checklist tab |
| 2026-07-13 | CHG-007 | Corrected and version-controlled the workflow baseline. | Resolve duplicate naming, route terminology and runtime/artifact contradictions. | PD-02; EP-003; DR-WF-001-DR-WF-017 | Implemented - working baseline | Project developer | Workflow package v1.1; Workflow Change Log |
| 2026-07-13 | CHG-008 | Clarified that runtime KV-cache configuration and persistent weight conversion are separate outcomes. | Prevent runtime settings from being reported or exported as new model files. | PD-02; DR-WF-005; DR-WF-006; DR-WF-017 | Implemented - working baseline | Project developer | WF-JRN-001; GGUF/OpenVINO rules |
| 2026-07-13 | CHG-009 | Clarified route maturity: OpenVINO TurboQuant is experimental/pinned and NPU is deferred from validated first release. | Align workflow claims with available project evidence and current hardware. | DR-WF-011; DR-WF-012; F-M21; F-M22; N-M11 | Implemented in workflows; release approval pending | Pending technical/supervisor approval | OpenVINO workflow set; hardware workflow |
| 2026-07-14 | CHG-010 | Improved workflow text clarity and page fit without changing behaviour or screens. | Make controlled documentation legible in Word and previews. | DR-WF-018; all workflow documents | Implemented - no scope change | Project developer | Workflow package v1.1; CHG-WF-013 |
| 2026-07-14 | CHG-011 | Established the EP-006 derived-requirement and change-control records. | Convert workflow-derived behaviour and prior scope changes into stable, reviewable records. | EP-006; PD-04; DR-WF-001-DR-WF-018 | Implemented - document validation complete | Project developer | This control set |
