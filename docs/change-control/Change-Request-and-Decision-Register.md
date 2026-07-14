# Change Request and Decision Register

**Document ID:** LOG-CRD-001  
**Version:** 1.5  
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
| CR-015 | 2026-07-14 | Arian B | Bring the completed MoSCoW/RTM evidence packs into full template compliance, repair their indexes and replace the missing-workbook link with a controlled artifact record. | Evidence and configuration control | G-M02; PD-04; EP-004; EP-005; EP-006; ART-RTM-XLSX-001 | Approved and implemented through PR #17; no scope or completion-status change | Closed |
| CR-016 | 2026-07-14 | Arian B | Strengthen the common evidence-record template while preserving existing validated evidence and avoiding unnecessary mass migration. | Evidence governance | Evidence Record Template; all future REQ/WP/EP/EXP evidence records; final release evidence audit | Approved and implemented as template v1.1; no requirement, scope or completion-status change | Closed |
| CR-017 | 2026-07-14 | Evidence-template compatibility review | Align the v1.1 status fields and guidance with the controlled RTM instead of introducing a competing validation vocabulary. | Corrective evidence governance | Evidence Record Template v1.1; template guidance; all future evidence records | Approved and implemented as v1.1.1 compatibility patch; no task status changed | Closed |

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
- PR #14

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
- PR #15

## CR-015 decision record

**Decision owner:** Arian B  
**Decision date:** 2026-07-14  
**Approval state:** Approved and implemented through PR #17  

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
- PR #17

## CR-016 decision record

**Decision owner:** Arian B  
**Decision date:** 2026-07-14  
**Approval state:** Approved and implemented as the developer-controlled evidence baseline  

### Problem found

Template v1.0 provided a sound nine-section evidence structure, but it did not require an explicit mapping from each acceptance criterion to evidence. It also lacked template/record versioning, a mandatory claim boundary, validation-independence fields, controlled effective-status guidance and clear revalidation triggers. These gaps could lead to inconsistent future records or claims that are broader than the evidence supports.

### Decision

1. Retain the familiar nine-section evidence-record structure.
2. Release common Evidence Record Template v1.1.
3. Require template version, record version, source baseline, last review, validation independence, approval scope and supersession metadata.
4. Require stable local criterion IDs and direct criterion-to-evidence mapping.
5. Require explicit statements of what the evidence proves and does not prove.
6. Strengthen evidence identification with version, commit, run and checksum fields where relevant.
7. Define the controlled relationship between Working status, Validation state and Effective status.
8. Add revalidation triggers for changes to requirements, code, architecture, models, dependencies, methods, environments or contradictory evidence.
9. Make v1.1 mandatory for new evidence records.
10. Preserve existing validated v1.0 records unless their evidence is unsound; migrate them when materially changed, revalidated or superseded and inspect active Verified records during the final release audit.
11. Do not change any requirement, scope, priority, acceptance criterion or existing completion decision through this template revision alone.

### Rationale

The revision improves auditability and makes overclaiming harder while avoiding disruptive, cosmetic rewrites of records that have already been validated. It creates a controlled forward standard and a proportionate migration path.

### Impact

- New evidence records have stronger criterion-level traceability and claim boundaries.
- Existing v1.0 records are not automatically invalidated.
- No task becomes Verified or loses Verified status merely because the template changed.
- Final release audit gains a clear migration and revalidation check.
- No first-release scope or application behaviour changes.

### Evidence

- `docs/evidence/templates/Evidence-Record-Template.md`
- `docs/evidence/templates/README.md`
- `docs/evidence/templates/Evidence-Template-Revision-History.md`
- `docs/evidence/README.md`
- CHG-016
- PR #19

## CR-017 decision record

**Decision owner:** Arian B  
**Decision date:** 2026-07-14  
**Approval state:** Approved corrective patch  

### Problem found

The first v1.1 template draft changed the controlled status vocabulary by removing `Partially Verified` and `Verified` from Working status and adding `Partially Validated` as a metadata Validation state. The controlled RTM is the authoritative source for status and currently uses the established Working status, Validation and Effective status fields. A template must mirror that source rather than create a second status model.

### Decision

1. Release Evidence Record Template v1.1.1 as a compatibility correction.
2. Restore the existing RTM Working-status and Effective-status vocabulary.
3. Keep the controlled Validation field binary: `Not Validated` or `Validated`.
4. Permit `Partially Validated` only as a narrative validation-review conclusion, not as a third RTM Validation value.
5. Require evidence records to copy all three status fields from the controlled RTM.
6. Require discrepancies to be corrected in the RTM first and then reflected in the evidence record.
7. Preserve all v1.1 evidence-governance improvements, including criterion mapping, claim boundaries, integrity identifiers and revalidation triggers.
8. Do not alter any existing task status through this corrective patch.

### Rationale

A single authoritative status model prevents the RTM, generated catalogues and evidence READMEs from disagreeing. The patch keeps the stronger evidence controls while restoring compatibility with the project’s existing traceability system.

### Impact

- No requirement, priority, lifecycle, acceptance criterion or task completion status changed.
- The RTM remains the status source of truth.
- New evidence records use v1.1.1.
- Existing v1.0/v1.1 records migrate on material change, revalidation, supersession or final release audit.

### Evidence

- `docs/evidence/templates/Evidence-Record-Template.md`
- `docs/evidence/templates/README.md`
- `docs/evidence/templates/Evidence-Template-Revision-History.md`
- `docs/evidence/README.md`
- CHG-017
- status-compatibility pull request
