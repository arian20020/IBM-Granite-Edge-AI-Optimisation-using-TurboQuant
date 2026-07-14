# Change Request and Decision Register

**Document ID:** LOG-CRD-001  
**Version:** 1.0  
**Status:** Baselined  
**Owner:** Arian B.  
**Last reviewed:** 2026-07-14

## Change requests

| Request ID | Date | Requested by | Request | Category | Affected IDs | Decision summary | Status |
|---|---|---|---|---|---|---|---|
| CR-001 | 2026-07-13 | Project developer | Correct workflow naming, duplicates and technical contradictions. | Corrective | PD-02; EP-003 | Approved and implemented | Closed |
| CR-002 | 2026-07-13 | Project developer | Preserve existing screen designs and red-tree visual structure while correcting workflow logic. | Constraint | All workflow documents | Approved and implemented | Closed |
| CR-003 | 2026-07-13 | Project developer | Add visible version-control metadata to every workflow document. | Document control | DR-WF-001 | Approved and implemented | Closed |
| CR-004 | 2026-07-13 | Workflow audit | Separate runtime KV-cache profiles from persistent weight/model artifacts. | Technical correction | DR-WF-005; DR-WF-006; DR-WF-017 | Approved and implemented | Closed |
| CR-005 | 2026-07-13 | Workflow audit | Separate GGUF/llama.cpp and OpenVINO IR/OpenVINO runtime routes. | Technical correction | DR-WF-004; DR-WF-008; DR-WF-010 | Approved and implemented | Closed |
| CR-006 | 2026-07-13 | Workflow audit | Require explicit confirmation before route fallback or conversion-route changes. | User control | DR-WF-007 | Approved and implemented | Closed |
| CR-007 | 2026-07-13 | Workflow audit | Defer NPU from validated first release and label OpenVINO TurboQuant experimental/pinned. | Capability boundary | DR-WF-011; DR-WF-012 | Implemented in documentation; external approval pending | Open |
| CR-008 | 2026-07-13 | Workflow audit | Prevent default requantisation of already-quantised GGUF artifacts. | Quality/provenance | DR-WF-009 | Approved and implemented | Closed |
| CR-009 | 2026-07-13 | Workflow audit | Separate runtime smoke testing from model-quality evaluation. | Evaluation control | DR-WF-014 | Approved and implemented | Closed |
| CR-010 | 2026-07-14 | Project developer | Increase text clarity while keeping editable text and original workflow design. | Documentation quality | DR-WF-018 | Approved and implemented | Closed |
| CR-011 | 2026-07-14 | Project developer | Create the derived-requirement, requirements-change and decision records needed for EP-006. | Engineering practice | EP-006; PD-04 | Approved and implemented | Closed |
| CR-012 | 2026-07-14 | Project developer | Freeze the app-specific evaluation addendum and create evidence records for PD-09 and EP-019. | Evaluation control | PD-09; EP-019; APP-EVAL-01-APP-EVAL-12 | Approved and implemented; RTM synchronisation follows review and merge | Closed |

## Impact, decision and approval record

| Request ID | Affected artefacts | Impact assessment | Decision and rationale | Decision owner | Approval state | Implementation evidence |
|---|---|---|---|---|---|---|
| CR-001 | All workflow files | Medium documentation impact; no UI redesign. | Correct the controlled workflow set and preserve original design. | Project developer | Approved for working baseline | Workflow Change Log; v1.1 package |
| CR-002 | All screen/workflow documents | High risk of accidental redesign if unrestricted. | Treat screen content and established red-tree structure as preserved constraints. | Project developer | Approved | User instruction; rendered comparisons |
| CR-003 | All 21 workflow documents | Low behavioural impact; high configuration-control benefit. | Add visible header with stable ID/version/status/owner/review information. | Project developer | Approved | Headers; Workflow Register |
| CR-004 | WF-JRN-001; mode workflows | High technical correctness impact. | Create separate Runtime Profile Ready and New Artifact Ready outcomes. | Project developer | Approved | DR-WF-005; DR-WF-006 |
| CR-005 | Inspection/hardware/configuration/mode workflows | High correctness and traceability impact. | Keep candidates route-specific and model route/build/device separately. | Project developer | Approved | DR-WF-004; DR-WF-008; DR-WF-010 |
| CR-006 | Conversion workflow | Medium user-control and data-output impact. | Require confirmation before alternative route execution. | Project developer | Approved | DR-WF-007 |
| CR-007 | Hardware and OpenVINO workflows | May narrow claimed first-release capability. | Document NPU as deferred and OpenVINO TurboQuant as experimental/pinned until validated. | Project developer | Working decision; supervisor/technical approval pending | DR-WF-011; DR-WF-012 |
| CR-008 | GGUF mode workflows | Reduces unsupported quality-risk paths. | Require suitable higher-precision source for normal persistent requantisation. | Project developer | Approved | DR-WF-009 |
| CR-009 | Mode and evaluation wording | Clarifies evidence claims. | Use smoke test only for load/stability; use controlled evaluation for quality. | Project developer | Approved | DR-WF-014 |
| CR-010 | All DOCX workflow/control documents | Formatting-only revision. | Increase font clarity and fit wide trees in landscape while preserving behaviour. | Project developer | Approved | DR-WF-018; CHG-WF-013 |
| CR-011 | 00. Workflow Control | Adds controlled records but no product behaviour. | Create EP-006 logs and link them through the Workflow Register. | Project developer | Approved | CHG-011; EP-006 validation record |
| CR-012 | App-specific evaluation addendum; PD-09 and EP-019 evidence folders; testing indexes and decision log | High traceability benefit; no claim that hardware, runtime, model or application tests passed. | Preserve 224 route IDs, add separate app-evaluation IDs, freeze inputs/schemas/gates and validate the planning deliverables. | Project developer | Approved | EVAL-ADDENDUM-v1; PD-09 evidence; EP-019 evidence; TD-012; CHG-012 |
