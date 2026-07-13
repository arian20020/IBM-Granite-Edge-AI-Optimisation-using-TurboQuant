# Workbook Revision Control

**Status:** Controlling change-management procedure  
**Canonical register:** `Workbook-Revision-Register.csv`

## Purpose

This control makes every workbook change visible, reviewable and traceable to its reason, affected test IDs, source evidence, branch, pull request, commit, hashes and validation status. The register is append-only: an older revision row is never deleted or silently rewritten.

## Current controlled revisions

| Workbook | Current version | Current change | Technical scope source |
|---|---:|---|---|
| WB-01 Upstream llama.cpp | 1.1 | Embedded formal revision history; no test-scope change | v1.0 / PR #4 |
| WB-02 AtomicBot TurboQuant | 1.1 | Embedded formal revision history; no test-scope change | v1.0 / PR #4 |
| WB-03 animehacker TQ3_0 | 1.1 | Embedded formal revision history; no test-scope change | v1.0 / PR #4 |
| WB-04 Official OpenVINO | 1.2 | Embedded formal revision history; no new test-scope change | v1.1 / PR #5 |
| WB-05 Custom OpenVINO | 1.2 | Embedded formal revision history; no new test-scope change | v1.1 / PR #5 |
| WB-06 Cross-route comparison | 1.2 | Embedded formal revision history; no new test-scope change | v1.1 / PR #5 |

## Mandatory rules

1. Append a new register row before changing a controlled workbook.
2. Keep every older row and mark it `Superseded` when a successor becomes current.
3. Increment the visible workbook version for technical, structural or administrative changes.
4. Record the reason, affected IDs, source evidence, branch, pull request and commit.
5. Generate DOCX working copies from the canonical Markdown templates, then apply the register with `Apply-Workbook-Revision-History.py`.
6. Keep canonical template filenames stable. The current visible version is controlled by the register, not by renaming the file.
7. Record canonical-template and generated-DOCX SHA-256 values in `workbooks/Controlled-Workbook-Manifest.csv`; a workbook must not embed its own hash because that creates a circular value.
8. Generate twice and confirm byte-identical output, then render and inspect every changed page.
9. After merge, replace `Pending merge` with the final merge commit and change the current status to `Current`.
10. A document revision never proves that a model, device, backend or codec test passed.

## Controlled generation

```powershell
python .\scripts\testing\Generate-Controlled-Workbooks.py
python .\scripts\testing\Apply-Workbook-Revision-History.py
python .\scripts\testing\Validate-Workbook-Revision-Control.py
```

The first command generates the six workbooks from their Git-reviewable Markdown templates. The second inserts each workbook's complete revision history and current visible version from the central CSV. The third verifies the register, manifest, hashes and embedded tables.

## Approval workflow

`propose -> append revision row -> change controlled source -> generate twice -> validate -> render every page -> review PR -> merge -> record merge commit`
