# Workflow Register

**Document ID:** REG-WF-001  
**Version:** 1.2  
**Status:** Baselined  
**Owner:** Arian B.  
**Last reviewed:** 2026-07-14

## Control rules

- `WF-JRN-001` controls the end-to-end order of activities.
- Stage-specific workflows control their named stage and must not contradict `WF-JRN-001`.
- Existing application screen concepts and the established red-tree visual structure are preserved.
- A behavioural or requirement change must update the affected workflow version, the Derived Requirements Register, the Requirements and Scope Change Log and the Workflow Change Log.

| Document ID | Title | Version | Status | Owner | Authoritative | Related tasks | Controlled source path |
|---|---|---:|---|---|---|---|---|
| WF-IMP-001 | 1.User imports or selects model | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/1.User Imports or selects a model/1.User imports or selects model.docx |
| WF-INS-001 | 2. Model inspection begins | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/2. Model Inspection/2. Model inspection begins.docx |
| WF-OVR-READY-001 | 3.1 Display Successful Model Overview | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/3. Display Model Overview/3.1 Display Successful Model Overview.docx |
| WF-OVR-CONV-001 | 3.2 Display conversion-required overview | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/3. Display Model Overview/3.2 Display conversion-required overview.docx |
| WF-OVR-UNSUP-001 | 3.3 Display unsupported-model overview | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/3. Display Model Overview/3.3 Display unsupported-model overview.docx |
| WF-OVR-INVALID-001 | 3.4 Display invalid or incomplete-model result | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/3. Display Model Overview/3.4 Display invalid or incomplete-model result.docx |
| WF-CONV-SCR-001 | Model Conversion Screens | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/3. Display Model Overview/Model Conversion Screens.docx |
| WF-DIAG-SCR-001 | View Technical Details | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/3. Display Model Overview/View Technical Details.docx |
| WF-HW-001 | 4. Hardware Analysis using LLM Fit | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/4. Hardware Analysis using LLM Fit/4. Hardware Analysis using LLM Fit.docx |
| WF-CFG-001 | 5. Generate viable optimisation configurations | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/5. Generate Viable Optimisation Configurations/5. Generate viable optimisation configurations.docx |
| WF-RES-001 | 6. Display Model Compatibility Result | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/6. Display Optimisation Modes/6. Display Model Compatibility Result.docx |
| WF-RES-READY-001 | Display Model Ready to Use | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/6. Display Optimisation Modes/Display Model Ready to Use.docx |
| WF-RES-OPT-001 | Display Optimisation Required | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/6. Display Optimisation Modes/Display Optimisation Required.docx |
| WF-GGUF-FIT-001 | 7. Fitted Model Optimisation Mode Selection GGUF | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/7.Optimisation mode definition/GGUF/7. Fitted Model Optimisation Mode Selection GGUF.docx |
| WF-GGUF-NOFIT-001 | 7. Unfitted Model Optimisation Mode Selection GGUF | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/7.Optimisation mode definition/GGUF/7. Unfitted Model Optimisation Mode Selection GGUF.docx |
| SPEC-GGUF-OPT-001 | 7.Optimisation Mode Implementation Scope GGUF | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/7.Optimisation mode definition/GGUF/7.Optimisation Mode Implementation Scope GGUF.docx |
| WF-OV-FIT-001 | 7. Fitted Model Optimisation Mode Selection OpenVINO IR | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/7.Optimisation mode definition/OpenVINO IR/7. Fitted Model Optimisation Mode Selection OpenVINO IR.docx |
| WF-OV-NOFIT-001 | 7. Unfitted Model Optimisation Mode Selection OpenVINO IR | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/7.Optimisation mode definition/OpenVINO IR/7. Unfitted Model Optimisation Mode Selection OpenVINO IR.docx |
| SPEC-OV-OPT-001 | 7.Optimisation Mode Implementation Scope OpenVINO IR | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/7.Optimisation mode definition/OpenVINO IR/7.Optimisation Mode Implementation Scope OpenVINO IR.docx |
| PLAN-OV-TQ-001 | OpenVINO IR and TurboQuant Implementation Plan | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003 | Backend Workflows/7.Optimisation mode definition/OpenVINO IR/OpenVINO IR and TurboQuant Implementation Plan.docx |
| WF-JRN-001 | Overall User Journey Workflows | 1.1 | Baselined | Arian B. | Yes | PD-02; EP-003; EP-008 | Overall User Journey Workflows.docx |
| REG-DR-001 | Derived Requirements Register | 1.0 | Baselined | Arian B. | Yes | EP-006; PD-04 | 00. Workflow Control/EP-006 Requirements Change Control/Derived Requirements Register v1.0.docx |
| LOG-REQ-CHG-001 | Requirements and Scope Change Log | 1.0 | Baselined | Arian B. | Yes | EP-006; PD-04 | 00. Workflow Control/EP-006 Requirements Change Control/Requirements and Scope Change Log v1.0.docx |
| LOG-CRD-001 | Change Request and Decision Register | 1.0 | Baselined | Arian B. | Yes | EP-006; PD-04 | 00. Workflow Control/EP-006 Requirements Change Control/Change Request and Decision Register v1.0.docx |
| REC-EP006-001 | EP-006 Validation Record | 1.0 | Baselined | Arian B. | Yes | EP-006; PD-04 | 00. Workflow Control/EP-006 Requirements Change Control/EP-006 Validation Record v1.0.docx |
