---
title: "Research Provenance Repair Audit"
status: "completed-audit"
version: "2.0"
last_updated: "2026-07-14"
---

# Research provenance repair audit

The earlier package preserved the research well, but it mainly showed **which DOCX produced which Markdown file**. This revision adds **where the information originally came from**.

## Issues fixed one by one

| # | Earlier issue | Fix now included | Evidence file |
|---:|---|---|---|
| 1 | Internal document traceability was being confused with external research provenance. | The two systems are now clearly separated. | [`00-sources/README.md`](../00-sources/README.md) |
| 2 | Central papers were mentioned only by filenames or arXiv numbers. | Full titles, authors, stable IDs, public links and BibTeX entries were added. | [`primary-research-papers.md`](../00-sources/primary-research-papers.md), [`references.bib`](../00-sources/references.bib) |
| 3 | Official IBM/model evidence was incomplete. | Public IBM documentation and official Granite 4.1 model cards were added. | [`official-documentation.md`](../00-sources/official-documentation.md), [`model-cards.md`](../00-sources/model-cards.md) |
| 4 | OpenVINO information relied partly on an older 2025 snapshot. | Current 2026 workflow, IR, Core, HETERO, devices and benchmark sources were added; the 2025 page remains for history. | [`official-documentation.md`](../00-sources/official-documentation.md) |
| 5 | Tool summaries lacked a consistent official source set. | Official llama.cpp, LM Studio, Ollama and Microsoft sources were catalogued. | [`official-documentation.md`](../00-sources/official-documentation.md), [`github-repositories.md`](../00-sources/github-repositories.md) |
| 6 | Repository reviews did not have a complete public source register. | Every reviewed repository now has a stable source ID and public URL. BeeLlama's missing URL was repaired. | [`github-repositories.md`](../00-sources/github-repositories.md) |
| 7 | Important claims did not have an explicit claim-to-source map. | A claim matrix now connects core claims to source IDs and evidence classes. | [`claim-source-matrix.md`](claim-source-matrix.md), [`claim-source-matrix.csv`](claim-source-matrix.csv) |
| 8 | Curated files cited only the original DOCX. | Each technical note now has `source_ids`, an evidence-basis note, an opening inline marker and a `Sources used` section. | Curated folders `01` to `07` |
| 9 | A private SharePoint URL could not support public reproducibility. | It is marked retired and replaced by public IBM/model-card sources in curated notes. The original remains untouched in `98` and `99`. | [`secondary-sources.md`](../00-sources/secondary-sources.md) |
| 10 | There was no machine-readable external source register. | A CSV catalogue with reliability, status, access date and use was added. | [`source-catalog.csv`](../00-sources/source-catalog.csv) |
| 11 | There was no explicit citation policy. | Citation, version-pinning and evidence-language rules were added. | [`citation-rules.md`](citation-rules.md) |
| 12 | There was no final provenance validation report. | Automated structural checks and preservation checks are included. | [`provenance-validation-report.md`](provenance-validation-report.md) |

## Preservation statement

The full Markdown extracts in `98-source-extracts` and the original DOCX files in `99-original-docx` were not rewritten. This means the new provenance layer improves the research record without deleting the original content.
