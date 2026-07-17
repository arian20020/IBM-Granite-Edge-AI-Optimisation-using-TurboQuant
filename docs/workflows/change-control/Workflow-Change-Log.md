# Workflow Change Log

**Document ID:** LOG-WF-CHG-001  
**Version:** 1.2  
**Status:** Baselined  
**Owner:** Arian B.  
**Last reviewed:** 2026-07-14

## Purpose

This append-only log records controlled corrections to the workflow baseline. The changes preserve the existing screen concepts and red-tree structure while correcting naming, route terminology, runtime/artifact distinctions and document control.

| Change ID | Change | Affected documents | Reason |
|---|---|---|---|
| CHG-WF-001 | Added a visible version-control header to every workflow document. | All 21 workflow documents | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-002 | Corrected the two unfitted-workflow filename errors. | GGUF and OpenVINO IR unfitted workflows | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-003 | Included Ready with Warnings within the existing READY branch without redesigning the red tree. | WF-JRN-001 | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-004 | Replaced silent route selection and automatic fallback with explicit user confirmation. | WF-JRN-001 | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-005 | Separated runtime profiles from persistent model-artifact outputs and limited export to artifact-producing operations. | WF-JRN-001; GGUF/OpenVINO rules | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-006 | Separated artifact format, runtime route, runtime build and target device. | Inspection, hardware and configuration workflows | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-007 | Added source-availability and no-default-requantisation rules for GGUF weight outputs. | GGUF fitted/unfitted and implementation scope | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-008 | Separated OpenVINO persistent weight compression from runtime KV-cache compression. | OpenVINO fitted/unfitted, scope and plan | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-009 | Deferred NPU from the first-release validated route while retaining diagnostic detection. | OpenVINO and hardware workflows | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-010 | Marked OpenVINO TurboQuant as experimental and pinned until Granite-specific validation passes. | OpenVINO workflows and plan | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-011 | Separated runtime smoke testing from model-quality evaluation. | GGUF/OpenVINO mode rules | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-012 | Added the Workflow Register and this controlled change log. | 00. Workflow Control | Resolve PD-02 technical contradiction or document-control gap while preserving the existing workflow/screen design. |
| CHG-WF-013 | Improved text clarity by increasing small text, strengthening all visible version headers and reformatting the wide master workflow in landscape without changing its workflow decisions or the existing application screens. | All workflow documents; WF-JRN-001; workflow control documents | Make the controlled workflow set easier to read in Microsoft Word and document previews while preserving the established visual tree structure. |
| CHG-WF-014 | Added a controlled Derived Requirements Register with stable IDs, sources, rationales, acceptance criteria, verification methods, evidence and approval states. | 00. Workflow Control/EP-006 Requirements Change Control | Complete the derived-requirement half of EP-006 without changing the existing workflows or screens. |
| CHG-WF-015 | Added a project Requirements and Scope Change Log that retains RTM change history and records workflow-derived baseline changes. | 00. Workflow Control/EP-006 Requirements Change Control | Complete the controlled change-log half of EP-006 and prevent requirement history from being rewritten. |
| CHG-WF-016 | Added a Change Request and Decision Register plus an EP-006 validation record and Git-friendly Markdown mirrors. | 00. Workflow Control/EP-006 Requirements Change Control; Workflow Register | Record impact, decision, approval and closure evidence and make the records suitable for repository review. |
