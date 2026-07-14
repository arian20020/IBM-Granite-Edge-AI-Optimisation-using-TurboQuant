---
title: "Granite 4.1 3B"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "IBM Granite/IBM Granite Language Models/IBM Granite 4.1 3B/Granite 4.1 3B.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-IBM-GRANITE-41-DOCS-2026"
source_ids:
  - "SRC-IBM-HF-GRANITE-41-3B-2026"
  - "SRC-IBM-GRANITE-41-DOCS-2026"
---

# Granite 4.1 3B

> **Evidence basis:** The main factual claims in this note are traced to [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026), [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



Granite 4.1 3B is the main starting model for the project. The supplied research records it as an instruction model for long-context chat and tasks such as summarisation, extraction, question answering, RAG, coding, function calling and multilingual dialogue. [SRC-IBM-HF-GRANITE-41-3B-2026]

## Recorded architecture details

| Property | Recorded value |
|---|---:|
| Approximate parameters | 3.4 billion |
| Stored weight size before runtime overhead | About 6.8 GB |
| Embedding size | 2,560 |
| Transformer layers | 40 |
| Query heads | 40 |
| KV heads | 8 |
| MLP hidden size | 8,192 |
| Maximum sequence length recorded in the research | 131,072 tokens |

These values explain why even a small model needs optimisation. The model weights are only one part of memory. Inference also needs the KV cache, runtime buffers, framework memory and temporary tensors.

## Why the KV heads matter

The model records keys and values for earlier tokens at each attention layer. The cache grows with context length. Using fewer KV heads than query heads reduces cache size compared with storing a separate key and value head for every query head, but long contexts can still make the cache large.

## Baseline metrics

Measure the same items before and after optimisation:

- time to first token;
- prompt-processing speed;
- generation tokens per second;
- end-to-end response time;
- peak RAM and VRAM;
- KV-cache memory;
- maximum stable context;
- task and instruction-following accuracy;
- long-context retrieval;
- factual correctness and relevance.

## Questions the experiment must answer

- Did memory use fall?
- Did speed improve or become worse?
- Was answer quality damaged?
- Did the optimised configuration make local use more practical?

## Sources used

- [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026) — ibm-granite/granite-4.1-3b.
- [SRC-IBM-GRANITE-41-DOCS-2026](../00-sources/official-documentation.md#src-ibm-granite-41-docs-2026) — Granite 4.1 language model documentation.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
