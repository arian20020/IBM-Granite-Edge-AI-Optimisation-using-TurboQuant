---
title: "Research Governance"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
---

# Research governance

This directory controls completeness, evidence language, provenance and GitHub import.

## Main files

- [`source-register.md`](source-register.md) and [`source-register.csv`](source-register.csv): internal document traceability from original DOCX to extract and curated note.
- [`../00-sources/`](../00-sources/): external research provenance for papers, official documentation, model cards and repositories.
- [`claim-source-matrix.md`](claim-source-matrix.md): important claim-to-source mappings.
- [`citation-rules.md`](citation-rules.md): how to cite, classify and version evidence.
- [`source-audit-report.md`](source-audit-report.md): issues fixed during the provenance rebuild.
- [`link-check-report.md`](link-check-report.md): source availability status on the access date.
- [`provenance-validation-report.md`](provenance-validation-report.md): automated checks.
- [`completeness-report.md`](completeness-report.md): preservation summary.
- [`package-manifest.json`](package-manifest.json): SHA-256 checksums for the whole research package.
- [`github-import.ps1`](github-import.ps1): Windows PowerShell import helper.

## Control principle

A clear source link is necessary, but it is not enough. Repository claims and paper results become **project results** only after they are reproduced with pinned inputs, saved logs and a recorded environment.
