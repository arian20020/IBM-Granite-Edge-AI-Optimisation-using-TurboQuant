# Change Request and Decision Register

**Document ID:** LOG-CRD-001  
**Version:** 1.3  
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
| CR-014 | 2026-07-14 | Arian B | Separate Functional, Non-Functional, Research, Governance and Exclusion requirements into readable catalogues. | Documentation structure | All requirement lifecycle records; MoSCoW v1.2; RTM v1.3 | Approved and implemented as a presentation-only revision | Closed |
| CR-015 | 2026-07-14 | Arian B | Bring the completed MoSCoW/RTM evidence packs into full template compliance, repair their indexes and replace the missing-workbook link with a controlled artifact record. | Evidence and configuration control | G-M02; PD-04; EP-004; EP-005; EP-006; ART-RTM-XLSX-001 | Approved and implemented; no scope or completion-status change | Closed |

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
- controlled RTM workbook artifact record and SHA-256
- G-M02, PD-04, EP-004 and EP-005 evidence packs

## CR-014 decision record

**Decision owner:** Arian B  
**Decision date:** 2026-07-14  
**Approval state:** Approved and implemented  

### Decision

1. Retain the original `Requirements` worksheet as the authoritative full RTM table used by formulas and traceability.
2. Add separate workbook views for Functional, Non-Functional, Research, Governance and Exclusion requirements.
3. Replace the mixed MoSCoW table with an index that links to separate repository catalogues for each requirement type.
4. Preserve every existing requirement ID, priority, release role, lifecycle state, acceptance criterion, verification method and traceability relationship.

### Rationale

The combined table was technically correct but difficult to read. Separating requirement types improves reviewability without changing the approved first-release scope.

### Evidence

- controlled RTM workbook artifact record and checksum
- `docs/requirements/MoSCoW-Requirements-v1.2.md`
- `docs/requirements/catalogue/`
- `docs/evidence/requirements/G-M02/Categorised-Catalogue-Audit.md`
- CHG-014

## CR-015 decision record

**Decision owner:** Arian B  
**Decision date:** 2026-07-14  
**Approval state:** Approved and implemented  

### Problem found

The original G-M02, PD-04, EP-004 and EP-005 READMEs recorded valid conclusions but did not contain every section required by the repository Evidence Record Template. The evidence indexes did not list all five completed tasks. Several records also linked directly to an `.xlsx` path that was not present in Git.

### Decision

1. Rewrite G-M02, PD-04, EP-004 and EP-005 as full nine-section evidence records.
2. Refresh EP-006 to include the later MoSCoW and catalogue decisions.
3. Add the MoSCoW and RTM Evidence Index and update all evidence collection indexes.
4. Add `ART-RTM-XLSX-001` to control the exact workbook by filename, size, package and SHA-256.
5. Replace broken direct `.xlsx` links with the artifact record until direct binary Git placement is completed.
6. Preserve the existing Implemented / Validated / Verified decisions because the underlying deliverables and audits remain valid.
7. State the binary-placement gap explicitly rather than falsely claiming the workbook is already stored in Git.

### Impact

- No requirement, priority, lifecycle, acceptance criterion or verification method changed.
- No completed task was newly validated by this correction.
- Repository evidence navigation and auditability improved.
- The direct binary Git-placement gap remains open and visible.

### Evidence

- `docs/evidence/templates/Evidence-Record-Template.md`
- `docs/evidence/indexes/MoSCoW-and-RTM-Evidence-Index.md`
- `docs/requirements/RTM-Workbook-Artifact-Record.md`
- G-M02, PD-04, EP-004, EP-005 and EP-006 evidence READMEs
- CHG-015