---
title: "External Research Sources"
status: "verified-source-register"
version: "2.0"
last_updated: "2026-07-14"
---

# External research sources

This directory records **where the technical information originally came from**. It is separate from `00-governance/source-register.*`, which records how the supplied DOCX files were transformed into the curated Markdown files.

## Two kinds of traceability

1. **Document traceability:** original DOCX in the controlled archive → full written Markdown extract in Git → curated note.
2. **Research provenance:** technical claim → source ID → paper, official documentation, model card or pinned source repository.

Use the stable source IDs, such as `[SRC-PAPER-TURBOQUANT-2025]`, inside research notes and the dissertation. The IDs remain the same even if a title or URL needs to be updated later.

## Files

- [`source-catalog.csv`](source-catalog.csv) — machine-readable master source list.
- [`primary-research-papers.md`](primary-research-papers.md) — TurboQuant, PolarQuant, QJL, KIVI and transformer papers.
- [`official-documentation.md`](official-documentation.md) — IBM, OpenVINO, Hugging Face, LM Studio, Ollama and Microsoft documentation.
- [`model-cards.md`](model-cards.md) — official IBM Granite model cards.
- [`github-repositories.md`](github-repositories.md) — canonical and experimental source repositories.
- [`secondary-sources.md`](secondary-sources.md) — blogs, guides and retired links, clearly labelled by reliability.
- [`books-and-project-guidance.md`](books-and-project-guidance.md) — the project books, UCL guidance and the Windows documentation snapshot.
- [`references.bib`](references.bib) — BibTeX starter file for papers and major official sources.

## Reliability rule

- **A1:** primary paper, official model card or formal project guidance.
- **A2:** official documentation or canonical source repository.
- **B1:** official blog or an experimental repository/README. Useful, but results must be reproduced.
- **C1:** secondary guide. Use for orientation only.
- **R:** retired or inaccessible source. Keep only for audit history.

The access date is 2026-07-14. Fast-moving documentation and repositories must be checked again before the final report is submitted.
