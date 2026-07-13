# Workbook Revision Control

**Status:** Controlling change-management procedure  
**Canonical register:** `Workbook-Revision-Register.csv`

## Purpose

This control ensures that every change to a testing workbook is visible, reviewable and traceable to its reason, affected test IDs, evidence, branch, pull request, commit and validation result. The register is append-only: an older revision is never deleted or overwritten.

## Current controlled revisions

| Workbook | Current version | Main change in the current version | Change reference |
|---|---:|---|---|
| WB-01 Upstream llama.cpp | 1.1 | Added formal embedded revision history; no test scope change | Revision-control PR |
| WB-02 AtomicBot TurboQuant | 1.1 | Added formal embedded revision history; no test scope change | Revision-control PR |
| WB-03 animehacker TQ3_0 | 1.1 | Added formal embedded revision history; no test scope change | Revision-control PR |
| WB-04 Official OpenVINO | 1.2 | Added formal embedded revision history; technical TBQ3/TBQ4 scope remains from v1.1 | PR #5 + revision-control PR |
| WB-05 Custom OpenVINO | 1.2 | Added formal embedded revision history; QJL/Polar/all-pairs scope remains from v1.1 | PR #5 + revision-control PR |
| WB-06 Cross-route comparison | 1.2 | Added formal embedded revision history; expanded codec comparison remains from v1.1 | PR #5 + revision-control PR |

## Mandatory rules

1. Add a new row to `Workbook-Revision-Register.csv` before changing a canonical workbook template.
2. Never edit or delete an older revision row; mark it `Superseded` when a successor becomes current.
3. Increment the workbook version for any technical, structural or administrative change that affects the generated document.
4. Record the reason, affected test IDs, source evidence, branch and pull request.
5. After merge, replace `Pending merge` with the final merge commit SHA and mark the revision `Current`.
6. Generate the DOCX files from the canonical Markdown templates and the revision register.
7. Record generated DOCX SHA-256 values centrally after generation; do not embed a workbook's own hash inside itself.
8. Render and inspect every changed DOCX before approval.
9. A workbook revision does not prove that any model, backend or codec test passed.

## Generation rule

`scripts/testing/Generate-Controlled-Workbooks.py` reads the register and inserts the complete revision history for the matching workbook near the beginning of each generated Word document. This keeps the visible Word table and the Git-reviewable CSV synchronized.

## Approval workflow

`propose change -> append revision row -> edit canonical template -> generate DOCX -> validate and render -> review pull request -> merge -> record merge commit`
