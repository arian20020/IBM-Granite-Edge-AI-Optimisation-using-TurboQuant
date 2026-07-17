# Research governance

This directory controls completeness, evidence language, provenance and GitHub import.

## Main files

- [`source-register.md`](source-register.md) and [`source-register.csv`](source-register.csv): internal document traceability from archived original DOCX to Git extract and curated note.
- [`../00-sources/`](../00-sources/): external research provenance for papers, official documentation, model cards and repositories.
- [`claim-source-matrix.md`](claim-source-matrix.md): important claim-to-source mappings.
- [`citation-rules.md`](citation-rules.md): how to cite, classify and version evidence.
- [`source-audit-report.md`](source-audit-report.md): issues fixed during the provenance rebuild.
- [`link-check-report.md`](link-check-report.md): source availability status on the access date.
- [`provenance-validation-report.md`](provenance-validation-report.md): automated checks.
- [`completeness-report.md`](completeness-report.md): preservation summary.
- [`v3-git-tree-manifest.json`](v3-git-tree-manifest.json): SHA-256 checksums for the detailed version-3 Git research tree.
- [`v3-validation.json`](v3-validation.json): automated version-3 validation results.
- [`v3-revision-report.md`](v3-revision-report.md): problems corrected in this rebuild.
- [`github-import.ps1`](github-import.ps1): Windows PowerShell import helper.

## Control principle

A clear source link is necessary, but it is not enough. Repository claims and paper results become **project results** only after they are reproduced with pinned inputs, saved logs and a recorded environment.
