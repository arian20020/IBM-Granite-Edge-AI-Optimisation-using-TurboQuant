---
title: "IBM Granite Model Family"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "IBM Granite/IBM Granite.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-IBM-GRANITE-41-BLOG-2026"
source_ids:
  - "SRC-IBM-GRANITE-OVERVIEW-2026"
  - "SRC-IBM-GRANITE-41-DOCS-2026"
  - "SRC-IBM-GRANITE-41-BLOG-2026"
---

# IBM Granite model family

> **Evidence basis:** The main factual claims in this note are traced to [SRC-IBM-GRANITE-OVERVIEW-2026](../00-sources/official-documentation.md#src-ibm-granite-overview-2026), [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026), [SRC-IBM-GRANITE-41-BLOG-2026](../00-sources/official-documentation.md#src-ibm-granite-41-blog-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



IBM Granite is a family of models built for different kinds of AI work. [SRC-IBM-GRANITE-OVERVIEW-2026]

| Model group | Main purpose | Project relevance |
|---|---|---|
| Granite Language | Chat, text generation, coding and agent tasks | **Main focus** |
| Granite Vision | Understand images and visual documents | Possible later work |
| Granite Docling | Document processing and understanding | Useful for document workflows |
| Granite Speech | Speech recognition and spoken-language tasks | Outside the current core scope |
| Granite Embedding | Convert text into vectors for search and RAG | Relevant if RAG is added |
| Granite Guardian | Safety and responsible-AI checks | Useful for later guardrails |
| Granite Time Series | Forecast changing business data | Outside the current core scope |

The project mainly needs Granite Language models because it is testing local text generation on Intel PCs. Granite Embedding may also matter if local RAG or document search is added.

## Sources used

- [SRC-IBM-GRANITE-OVERVIEW-2026](../00-sources/official-documentation.md#src-ibm-granite-overview-2026) — Granite overview.
- [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026) — Granite 4.1 language model documentation.
- [SRC-IBM-GRANITE-41-BLOG-2026](../00-sources/official-documentation.md#src-ibm-granite-41-blog-2026) — Granite 4.1 LLMs: how they are built.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
