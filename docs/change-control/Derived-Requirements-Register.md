# Derived Requirements Register

**Document ID:** REG-DR-001  
**Version:** 1.0  
**Status:** Baselined  
**Owner:** Arian B.  
**Last reviewed:** 2026-07-14

## Identification and rationale

| Derived ID | Date raised | Derived requirement | Parent/source | Rationale | Priority | Status | Owner |
|---|---|---|---|---|---|---|---|
| DR-WF-001 | 2026-07-13 | Every authoritative workflow document shall display a stable document ID, version, status, owner, review date, related tasks and supersession state. | PD-02; EP-003; audit practice 41 | Controlled documents must be identifiable and changes must not rely on file timestamps. | Must | Implemented | Arian B. |
| DR-WF-002 | 2026-07-13 | WF-JRN-001 shall control the end-to-end workflow order; stage-specific workflows shall not contradict it. | PD-02; Workflow audit | A single controlling workflow prevents divergent copies of the same behaviour. | Must | Implemented | Arian B. |
| DR-WF-003 | 2026-07-13 | Inspection shall support READY_WITH_WARNINGS separately from READY, CONVERSION_REQUIRED, UNSUPPORTED and INVALID_OR_INCOMPLETE. | PD-02 workflow correction | Recoverable or non-blocking limitations must not be treated as corruption. | Must | Implemented | Arian B. |
| DR-WF-004 | 2026-07-13 | Artifact format, runtime route, runtime build and target device shall be recorded as separate fields. | PD-02 acceptance criterion | GGUF/OpenVINO, llama.cpp/OpenVINO, exact executable and CPU/GPU/NPU are different concepts. | Must | Implemented | Arian B. |
| DR-WF-005 | 2026-07-13 | Runtime-only settings, including KV-cache method, context, offload and device selection, shall create a runtime profile and shall not be described as creating a new model artifact. | PD-02 acceptance criterion | The runtime cache is temporary and distinct from persistent model weights. | Must | Implemented | Arian B. |
| DR-WF-006 | 2026-07-13 | Persistent weight compression or format conversion shall explicitly identify the new GGUF file or OpenVINO artifact folder that it creates. | PD-02 workflow correction | Artifact-producing operations require output, provenance and validation handling. | Must | Implemented | Arian B. |
| DR-WF-007 | 2026-07-13 | A change from the recommended conversion route to an alternative route shall require explicit user confirmation. | Workflow audit CHG-WF-004 | Silent fallback can change model format, dependencies and output unexpectedly. | Must | Implemented | Arian B. |
| DR-WF-008 | 2026-07-13 | GGUF candidates shall remain within the llama.cpp route and OpenVINO IR candidates shall remain within the OpenVINO route unless a separate confirmed conversion workflow is used. | PD-02 acceptance criterion | Cross-route combinations are technically invalid or misleading. | Must | Implemented | Arian B. |
| DR-WF-009 | 2026-07-13 | An already-quantised GGUF artifact shall not be requantised by default; a suitable higher-precision source shall be required unless a controlled experiment explicitly permits otherwise. | Workflow audit CHG-WF-007 | Repeated quantisation can compound quality loss and weakens provenance. | Must | Implemented | Arian B. |
| DR-WF-010 | 2026-07-13 | OpenVINO persistent weight compression and OpenVINO runtime KV-cache compression shall be represented as separate operations. | Workflow audit CHG-WF-008 | They affect different data and produce different outputs. | Must | Implemented | Arian B. |
| DR-WF-011 | 2026-07-13 | OpenVINO TurboQuant shall be labelled Experimental and tied to a pinned build until Granite-specific integration and evaluation criteria pass. | F-M21; F-M22; N-M11; RTM CHG-003 | Merged or prototype capability is not equivalent to a validated project route. | Must | Implemented | Arian B. |
| DR-WF-012 | 2026-07-13 | NPU availability may be detected for diagnostics, but NPU shall not be labelled as a validated first-release route without suitable hardware evidence. | Workflow audit CHG-WF-009 | The current validation environment does not provide NPU evidence. | Must | Implemented | Arian B. |
| DR-WF-013 | 2026-07-13 | A newly created model artifact shall be structurally checked, hashed, reinspected and load-smoke-tested before it is marked ready. | PD-02 workflow correction | Creation success alone does not prove that the output is complete or loadable. | Must | Implemented | Arian B. |
| DR-WF-014 | 2026-07-13 | Runtime smoke testing shall be recorded separately from model-quality evaluation. | Workflow audit CHG-WF-011 | Loading and short generation do not establish that quality has been preserved. | Must | Implemented | Arian B. |
| DR-WF-015 | 2026-07-13 | Every route-specific capability shall carry one controlled maturity state: Validated, Experimental, Planned, Deferred or Unsupported. | Workflow audit | Capability claims must match project evidence rather than tool availability alone. | Must | Implemented | Arian B. |
| DR-WF-016 | 2026-07-13 | Illustrative optimisation combinations shall be labelled as examples; the application shall generate only candidates supported by the detected model, source, runtime build and device. | Workflow audit | Fixed examples must not be mistaken for universally supported configurations. | Should | Implemented | Arian B. |
| DR-WF-017 | 2026-07-13 | The final summary shall state whether the selected configuration is runtime-only or creates a persistent artifact before the user confirms it. | PD-02 workflow correction | The user must understand the permanence and output of the selected action. | Must | Implemented | Arian B. |
| DR-WF-018 | 2026-07-14 | Workflow diagrams and control tables shall remain readable within the DOCX page boundaries without rasterising editable text. | CHG-WF-013; usability/readability correction | Controlled evidence must be legible in Word, preview and print. | Should | Implemented | Arian B. |

## Acceptance, verification and traceability

| Derived ID | Affected workflows / records | Acceptance criterion | Verification method | Evidence | Approval state |
|---|---|---|---|---|---|
| DR-WF-001 | All 21 workflow documents; REG-WF-001 | All workflow files show the required control fields in the visible header. | Document inspection | Workflow package v1.1 | Approved for working baseline by project developer |
| DR-WF-002 | WF-JRN-001; all stage workflows | Register names WF-JRN-001 as controlling and no stage flow contradicts its runtime/artifact split. | Cross-document inspection | Workflow Register v1.2 | Approved for working baseline by project developer |
| DR-WF-003 | WF-JRN-001; WF-INS-001; WF-OVR-READY-001 | READY_WITH_WARNINGS has a non-blocking route and is not classified as invalid. | Workflow inspection | Affected workflow documents | Approved for working baseline by project developer |
| DR-WF-004 | WF-INS-001; WF-HW-001; WF-CFG-001; WF-RES-001 | The four concepts appear as separate fields wherever a configuration is displayed or recorded. | Terminology inspection | Affected workflow documents | Approved for working baseline by project developer |
| DR-WF-005 | WF-JRN-001; GGUF/OpenVINO mode rules | Runtime-only branches end in Runtime Profile Ready and do not offer model export. | Path inspection | WF-JRN-001 | Approved for working baseline by project developer |
| DR-WF-006 | WF-CONV-001; WF-JRN-001 | GGUF output is a file; OpenVINO output is an artifact folder; both use the artifact branch. | Path inspection | WF-CONV-001; WF-JRN-001 | Approved for working baseline by project developer |
| DR-WF-007 | WF-CONV-001; WF-JRN-001 | Alternative route is not run until user confirmation is shown in the workflow. | Path inspection | WF-CONV-001 | Approved for working baseline by project developer |
| DR-WF-008 | WF-CFG-001; SPEC-GGUF-OPT-001; SPEC-OV-OPT-001 | No candidate table combines route-specific fields without a separate conversion step. | Cross-document inspection | GGUF/OpenVINO scope documents | Approved for working baseline by project developer |
| DR-WF-009 | WF-GGUF-FIT-001; WF-GGUF-NOFIT-001; SPEC-GGUF-OPT-001 | Source-availability rule is present and default requantisation is prohibited. | Rule inspection | GGUF workflow set | Approved for working baseline by project developer |
| DR-WF-010 | WF-OV-FIT-001; WF-OV-NOFIT-001; SPEC-OV-OPT-001 | Weight and cache operations have separate descriptions and outputs. | Rule inspection | OpenVINO workflow set | Approved for working baseline by project developer |
| DR-WF-011 | OpenVINO workflow set; PLAN-OV-TQ-001 | TurboQuant is labelled Experimental and pinned; stable fallback has no TurboQuant dependency. | Terminology inspection | OpenVINO workflow set | Technical/supervisor approval pending for release scope |
| DR-WF-012 | WF-HW-001; OpenVINO workflow set | NPU is diagnostics/future work and is absent from validated first-release candidates. | Candidate inspection | Hardware/OpenVINO workflows | Technical/supervisor approval pending for release scope |
| DR-WF-013 | WF-CONV-001; WF-JRN-001 | The artifact completion path includes structure check, hash, reinspection and load smoke test. | Path inspection | Conversion workflow | Approved for working baseline by project developer |
| DR-WF-014 | GGUF/OpenVINO mode rules | Smoke-test wording is operational; quality acceptance points to separate evaluation. | Terminology inspection | Mode-selection documents | Approved for working baseline by project developer |
| DR-WF-015 | WF-HW-001; WF-CFG-001; mode summaries | Capability maturity state is recorded in candidate and summary information. | Field inspection | Hardware/configuration workflows | Approved for working baseline by project developer |
| DR-WF-016 | GGUF/OpenVINO candidate lists | Examples are explicitly illustrative and candidate generation is conditional. | Rule inspection | Mode-selection documents | Approved for working baseline by project developer |
| DR-WF-017 | WF-RES-OPT-001; GGUF/OpenVINO mode summaries | Confirmation summary states Creates new artifact: Yes/No. | Screen-adjacent workflow inspection | Optimisation workflow documents | Approved for working baseline by project developer |
| DR-WF-018 | All DOCX workflow/control documents | Rendered pages contain legible editable text with no clipped tree branches or control tables. | Render and visual inspection | v1.1 rendered QA set | Approved for working baseline by project developer |
