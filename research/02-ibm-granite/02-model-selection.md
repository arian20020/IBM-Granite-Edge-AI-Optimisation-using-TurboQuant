---
title: "Granite 4.1 Model Selection"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "IBM Granite/IBM Granite Language Models/IBM Granite 4.1 Models.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-BOOK-AI-ENGINEERING-2025"
source_ids:
  - "SRC-IBM-GRANITE-41-DOCS-2026"
  - "SRC-IBM-HF-GRANITE-41-3B-2026"
  - "SRC-IBM-HF-GRANITE-41-8B-2026"
  - "SRC-BOOK-AI-ENGINEERING-2025"
---

# Granite 4.1 model selection

> **Evidence basis:** The main factual claims in this note are traced to [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026), [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026), [SRC-IBM-HF-GRANITE-41-8B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-8b-2026), [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



Use a staged model order rather than starting with the largest model. [SRC-IBM-GRANITE-41-DOCS-2026]

| Model | Project role | Reason |
|---|---|---|
| Granite 4.1 3B | First baseline | Lowest expected memory and easiest local starting point |
| Granite 4.1 8B | Second target | Better quality, but a more demanding test of optimisation |
| Granite 4.1 30B | Stretch target | Closest to the large-model goal, but unsuitable for the first experiment |

## Decision rule

Do not move to the next model only because the previous model launched once. Move forward after the baseline is repeatable and includes:

- successful loading and generation;
- peak RAM/VRAM;
- time to first token;
- prompt and decode speed;
- quality results;
- saved command, commit and logs.

This staged approach reduces risk and makes failures easier to diagnose.

## Sources used

- [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026) — Granite 4.1 language model documentation.
- [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026) — ibm-granite/granite-4.1-3b.
- [SRC-IBM-HF-GRANITE-41-8B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-8b-2026) — ibm-granite/granite-4.1-8b.
- [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025) — AI Engineering: Building Applications with Foundation Models.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
