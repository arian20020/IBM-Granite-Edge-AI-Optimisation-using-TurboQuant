# Change Request and Decision Register

**Document ID:** LOG-CRD-001  
**Version:** 1.1  
**Status:** Baselined  
**Owner:** Arian B  
**Last reviewed:** 2026-07-14

## Change requests

| Request ID | Date | Requested by | Request | Category | Affected IDs | Decision summary | Status |
|---|---|---|---|---|---|---|---|
| CR-001 | 2026-07-13 | Arian B | Correct workflow naming, duplicates and technical contradictions. | Corrective | PD-02; EP-003 | Approved and implemented | Closed |
| CR-002 | 2026-07-13 | Arian B | Preserve existing screens and red-tree visual structure while correcting workflow logic. | Constraint | Workflow documents | Approved and implemented | Closed |
| CR-003 | 2026-07-13 | Arian B | Add visible version-control metadata to every workflow document. | Document control | DR-WF-001 | Approved and implemented | Closed |
| CR-004 | 2026-07-13 | Workflow audit | Separate runtime KV-cache profiles from persistent model artefacts. | Technical correction | DR-WF-005; DR-WF-006; DR-WF-017 | Approved and implemented | Closed |
| CR-005 | 2026-07-13 | Workflow audit | Separate GGUF/llama.cpp and OpenVINO routes. | Technical correction | DR-WF-004; DR-WF-008; DR-WF-010 | Approved and implemented | Closed |
| CR-006 | 2026-07-13 | Workflow audit | Require confirmation before route fallback or conversion-route changes. | User control | DR-WF-007 | Approved and implemented | Closed |
| CR-007 | 2026-07-13 | Workflow audit | Defer NPU claims and label OpenVINO TurboQuant experimental/pinned. | Capability boundary | DR-WF-011; DR-WF-012 | Working decision; external review pending | Open |
| CR-008 | 2026-07-13 | Workflow audit | Prevent default requantisation of already-quantised GGUF artefacts. | Quality/provenance | DR-WF-009 | Approved and implemented | Closed |
| CR-009 | 2026-07-13 | Workflow audit | Separate runtime smoke testing from model-quality evaluation. | Evaluation control | DR-WF-014 | Approved and implemented | Closed |
| CR-010 | 2026-07-14 | Arian B | Increase text clarity while preserving editable text and workflow design. | Documentation quality | DR-WF-018 | Approved and implemented | Closed |
| CR-011 | 2026-07-14 | Arian B | Create the derived-requirement, scope-change and decision records for EP-006. | Engineering practice | EP-006; PD-04 | Approved and implemented | Closed |
| CR-012 | 2026-07-14 | Arian B | Freeze the app-specific evaluation addendum and evidence records. | Evaluation control | PD-09; EP-019 | Approved and implemented | Closed |
| CR-013 | 2026-07-14 | Arian B | Resolve the draft first-release priority boundary and freeze MoSCoW v1.2. | Requirements and scope | F-M16–F-M27; R-M02; R-M13; G-M02; PD-04; EP-004; EP-005 | Approved as developer working baseline; supervisor review pending | Closed |

## CR-013 decision record

**Decision owner:** Arian B  
**Decision date:** 2026-07-14  
**Approval state:** Developer-approved working baseline; supervisor review pending  

### Decision

1. Preserve stable requirement IDs and change the Priority/Release Role/Lifecycle fields rather than renaming IDs.
2. Classify F-M16, F-M17, F-M20, F-M23 and F-M24 as active Should Haves.
3. Retain F-M18 and the narrowed F-M19 as Must Haves.
4. Retain one pinned app-integrated TurboQuant route, dependable upstream fallback and truthful activation reporting as Must Haves.
5. Retain R-M02 as the mandatory TurboVec identification and release-decision gate.
6. Defer F-M25, F-M26, F-M27 and R-M13 from the first release.
7. Align the Project Definition, MoSCoW baseline, RTM, work packages, objectives, evidence records and dashboard.

### Rationale

The original draft bundled multiple independent features into Must scope. The revised boundary keeps the essential research prototype and the project’s main TurboQuant contribution while preventing optional download, multi-turn, model-processing and full TurboVec subsystems from displacing the core route.

### Impact

- Active requirements: 72
- Active Must Haves: 56
- Active Should Haves: 14
- Active Could Haves: 2
- Full TurboVec integration work packages TV-02 to TV-04: Deferred
- Supervisor approval: not claimed

### Evidence

- `docs/planning/Project-Definition-v1.1.md`
- `docs/requirements/MoSCoW-Requirements-v1.2.md`
- `docs/requirements/Requirements-Traceability-Matrix-v1.3.md`
- controlled RTM workbook and SHA-256
- G-M02, PD-04, EP-004 and EP-005 evidence packs
