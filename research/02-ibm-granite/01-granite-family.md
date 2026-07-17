# IBM Granite model family

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This document introduces the IBM Granite family before narrowing the project to the language models. It explains the different Granite model groups, what each group is designed to do, and why the current project is mainly interested in Granite Language models for private, offline text generation.

## Step-by-step way to understand the model family

1. Start with **Granite as a family**, not one single model.
2. Separate language generation from vision, speech, embeddings, document processing, safety and time-series work.
3. Match the project task to the correct family. This project needs local text generation, so the language family is the main route.
4. Treat embedding and guardian models as possible later additions rather than mixing them into the first inference milestone.
5. Select a specific language model only after checking its size, memory needs, context length, licence, model format and backend support.

## Quick overview

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

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: IBM Granite

> **Original document:** `IBM Granite/IBM Granite.docx`

IBM Granite is a family of AI models supplied by IBM.

### There are different models for different tasks:

Granite Language Models – used for text generation, coding, chat, agent-style tasks.

Granite Vision – works with documents and images.

Granite Docling – help process and understand documents

Granite Speech – work with speech recognition and spoken language understanding.

Granite Embedding – turn text into numerical vectors, useful for RAG and semantic search.

Granite Guardian – help with safety, content moderaton, responsible AI use

Granite Time Series models are used for business data that changes over time, such as forecasting.

Granite also provides libraries and tools that extend the models and make them more useful in different applications.

### Which Models are important in our Case?

Granite language models, because we’re investigating local AI use on Intel PCs

Granit Embedding models too if project includes RAG or document search.

## What this means for the project

The first development milestone should stay with Granite Language. Other Granite families can be recorded as future extensions, but they should not complicate the initial local-chat and model-import workflow.

## Summary

- Granite is a model family, not one model.
- The language family is the current project focus.
- Embedding and guardian models are possible later extensions.
- A specific model must be selected using memory, format, licence and backend evidence.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-IBM-GRANITE-OVERVIEW-2026`
- `SRC-IBM-GRANITE-41-DOCS-2026`
- `SRC-IBM-GRANITE-41-BLOG-2026`
