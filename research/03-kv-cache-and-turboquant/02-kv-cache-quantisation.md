---
title: "KV Cache Quantisation"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "KV-Cache Compression/2. KV Cache Quantization.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-HF-TRANSFORMERS-CACHE"
source_ids:
  - "SRC-PAPER-KIVI-2024"
  - "SRC-HF-TRANSFORMERS-CACHE"
---

# KV-cache quantisation

> **Evidence basis:** The main factual claims in this note are traced to [SRC-PAPER-KIVI-2024](../00-sources/primary-research-papers.md#src-paper-kivi-2024), [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



KV-cache quantisation stores cached key and value numbers with fewer bits. [SRC-PAPER-KIVI-2024]

| Example format | Bits per value | Number of possible codes |
|---|---:|---:|
| FP16 | 16 | Very large floating-point range |
| INT4 | 4 | 16 |
| INT2 | 2 | 4 |

Fewer bits reduce memory and memory traffic, but increase approximation error.

## Scale, range and error

A basic quantiser maps a range of real numbers onto a small set of codes. The decoder needs scale and range information to rebuild approximate values. Outliers can make the range too wide, leaving poor precision for ordinary values.

## Per-token and per-channel grouping

Imagine the cache as a table:

- each row is one token vector;
- each column is one fixed channel.

**Per-token quantisation** groups across a row. It is often suitable for values.

**Per-channel quantisation** groups the same channel across tokens. It can isolate persistent outlier channels and is often safer for keys.

The KIVI research recorded in the source notes uses this asymmetric approach:

- keys: per channel;
- values: per token;
- newest entries: temporarily kept in full precision.

## Streaming cache design

The cache is created during inference, so quantisation must happen as tokens arrive. A practical design keeps a small recent full-precision section and moves older complete groups into the packed low-bit section.

## Mixed-precision attention

The query searches both sections:

```text
scores from older quantised keys
+ scores from recent full-precision keys
-> one softmax
-> one set of attention weights
```

The corresponding quantised and full-precision values are then combined into the final attention result.

## Fake and real quantisation

- **Fake quantisation:** simulate low precision, then use floating-point storage. Useful for quality experiments, but does not prove memory savings.
- **Real quantisation:** physically pack the low-bit codes and use them during attention. Required for genuine memory and performance claims.

## Important reporting rule

A six-times smaller KV cache does not mean six-times lower total application memory. Model weights and other runtime memory remain.

## Sources used

- [SRC-PAPER-KIVI-2024](../00-sources/primary-research-papers.md#src-paper-kivi-2024) — KIVI: A Tuning-Free Asymmetric 2bit Quantization for KV Cache.
- [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache) — Transformers caching explanation.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
