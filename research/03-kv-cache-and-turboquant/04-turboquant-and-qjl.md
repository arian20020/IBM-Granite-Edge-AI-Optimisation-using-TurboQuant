---
title: "TurboQuant and QJL"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "KV-Cache Compression/5. TurboQuant.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-REVISIT-2026"
source_ids:
  - "SRC-PAPER-TURBOQUANT-2025"
  - "SRC-PAPER-POLARQUANT-2025"
  - "SRC-PAPER-QJL-2024"
  - "SRC-PAPER-TURBOQUANT-REVISIT-2026"
---

# TurboQuant and QJL

> **Evidence basis:** The main factual claims in this note are traced to [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025), [SRC-PAPER-POLARQUANT-2025](../00-sources/primary-research-papers.md#src-paper-polarquant-2025), [SRC-PAPER-QJL-2024](../00-sources/primary-research-papers.md#src-paper-qjl-2024), [SRC-PAPER-TURBOQUANT-REVISIT-2026](../00-sources/primary-research-papers.md#src-paper-turboquant-revisit-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



The supplied research treats TurboQuant as a two-stage idea: [SRC-PAPER-TURBOQUANT-2025]

1. **Main approximation:** compress the vector with a geometry-aware low-bit method such as PolarQuant-style preconditioning and codebooks.
2. **Residual or inner-product correction:** use QJL-style information to improve the query-key attention score after strong compression.

Practical repositories often change this design. Some keep only Stage 1, some store direct residual signs, some use QJL only for selected head dimensions and some replace it with trellis coding.

## QJL in simple English

QJL does not try to rebuild every key coordinate accurately. It focuses on the information needed for query-key inner products.

```text
Key k
-> shared random projection S
-> projected values Sk
-> keep only sign(Sk)
-> also store ||k||
```

When a query arrives:

```text
Query q
-> precise projected query Sq
-> compare Sq with stored key signs
-> use stored key norm
-> estimate q . k
```

The one-bit signs keep direction-like evidence, while the norm keeps overall magnitude. More projected measurements usually improve stability but use more memory and computation.

## What "zero overhead" does not mean

QJL still stores a key norm and uses a shared projection. The phrase refers to avoiding many block-level scales and zero points, not to storing no supporting information.

## Accuracy statement

QJL estimates the inner product; it does not reproduce it exactly for every query and key. The formal results concern unbiased estimation and bounded probability of large error under stated assumptions.

## Values

The standalone QJL approach mainly targets keys. Values can use ordinary token-wise quantisation because their role comes after attention weights have already been calculated.

## Project wording rule

Do not call a repository a complete TurboQuant implementation simply because it uses "TurboQuant" in its name. Confirm whether its active inference path actually applies the claimed correction information.

## Sources used

- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.
- [SRC-PAPER-POLARQUANT-2025](../00-sources/primary-research-papers.md#src-paper-polarquant-2025) — PolarQuant: Quantizing KV Caches with Polar Transformation.
- [SRC-PAPER-QJL-2024](../00-sources/primary-research-papers.md#src-paper-qjl-2024) — QJL: 1-Bit Quantized JL Transform for KV Cache Quantization with Zero Overhead.
- [SRC-PAPER-TURBOQUANT-REVISIT-2026](../00-sources/primary-research-papers.md#src-paper-turboquant-revisit-2026) — Revisiting RaBitQ and TurboQuant: A Symmetric Comparison of Methods, Theory, and Experiments.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
