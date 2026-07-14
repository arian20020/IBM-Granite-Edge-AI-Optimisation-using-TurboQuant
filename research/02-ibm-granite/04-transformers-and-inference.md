---
title: "Transformers and Granite Inference"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "IBM Granite/Transformers.docx"
  - "IBM Granite/IBM Granite Language Models/Full Granite Inference Guide.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-IBM-HF-GRANITE-41-3B-2026"
source_ids:
  - "SRC-PAPER-TRANSFORMER-2017"
  - "SRC-HF-TRANSFORMERS-CACHE"
  - "SRC-IBM-HF-GRANITE-41-3B-2026"
---

# Transformers and Granite inference

> **Evidence basis:** The main factual claims in this note are traced to [SRC-PAPER-TRANSFORMER-2017](../00-sources/primary-research-papers.md#src-paper-transformer-2017), [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache), [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Simple mental model

A language model does not read a sentence in the same way a person does. It follows this loop: [SRC-PAPER-TRANSFORMER-2017]

```text
Text prompt
-> tokens
-> token IDs
-> embedding vectors
-> repeated transformer layers
-> scores for the next token
-> choose one token
-> add it to the sequence
-> repeat
```

## Attention

Attention helps the model decide which earlier tokens matter for the current calculation. Each layer creates three versions of its input:

- **Query:** what the current token is looking for.
- **Key:** how each stored token can be matched.
- **Value:** the information supplied when a token is relevant.

A query is compared with earlier keys. The resulting scores are normalised and used to combine the matching values.

## Granite 4.1 3B layer flow

For the recorded 3B architecture:

1. RMSNorm rescales the input so calculations remain stable.
2. Query, key and value projections are created.
3. Position information is applied to queries and keys.
4. Keys and values are saved in that layer's KV cache.
5. Each query head compares against the key/value head shared by its group.
6. Attention produces a context-aware result.
7. A feed-forward block processes the result.
8. Residual connections preserve and combine earlier information.
9. The output enters the next layer.

The same process happens across 40 layers. Every layer has its own KV cache because each layer receives a different, more contextual representation.

## Prefill and decoding

- **Prefill:** the full prompt is processed and the first KV cache is created.
- **Decode:** one new token is processed at a time while earlier keys and values are reused.

This reuse is why the KV cache improves speed. It is also why long conversations consume more memory.

The detailed diagrams and numerical examples are retained in the full source extracts.

## Sources used

- [SRC-PAPER-TRANSFORMER-2017](../00-sources/primary-research-papers.md#src-paper-transformer-2017) — Attention Is All You Need.
- [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache) — Transformers caching explanation.
- [SRC-IBM-HF-GRANITE-41-3B-2026](../00-sources/model-cards.md#src-ibm-hf-granite-41-3b-2026) — ibm-granite/granite-4.1-3b.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
