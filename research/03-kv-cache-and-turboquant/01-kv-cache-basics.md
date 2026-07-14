---
title: "KV Cache Basics"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "KV-Cache Compression/1. Understanding KV Cache.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-HF-TRANSFORMERS-CACHE"
source_ids:
  - "SRC-PAPER-TRANSFORMER-2017"
  - "SRC-HF-TRANSFORMERS-CACHE"
---

# KV cache basics

> **Evidence basis:** The main factual claims in this note are traced to [SRC-PAPER-TRANSFORMER-2017](../00-sources/primary-research-papers.md#src-paper-transformer-2017), [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Why the cache exists

When a model generates a new token, it needs information from earlier tokens. Recalculating every earlier key and value at every step would waste time. The KV cache stores them once and reuses them. [SRC-PAPER-TRANSFORMER-2017]

```text
Without cache: recalculate old K and V every step
With cache:     reuse old K and V, calculate only the new token
```

## What is stored

At each attention layer, every token produces:

- query vectors;
- key vectors;
- value vectors.

Queries are mainly needed for the current calculation. Earlier keys and values are kept because future queries must compare against them.

## Query, key and value

- Query: "What am I looking for?"
- Key: "What information do I represent?"
- Value: "What information should I contribute if selected?"

The query-key dot product creates a relevance score. Softmax turns all scores into attention weights. Those weights are applied to the values.

## One cache per layer

Layer 1 and layer 40 do not store identical information. Each layer receives a different representation, so each layer creates and keeps its own keys and values.

## What makes the cache grow

Cache size increases with:

- number of layers;
- number of KV heads;
- head dimension;
- context length;
- batch size;
- numerical precision.

Multi-head, grouped-query and multi-query attention differ in how many KV heads are stored. Fewer KV heads can reduce memory.

## Sources used

- [SRC-PAPER-TRANSFORMER-2017](../00-sources/primary-research-papers.md#src-paper-transformer-2017) — Attention Is All You Need.
- [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache) — Transformers caching explanation.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
