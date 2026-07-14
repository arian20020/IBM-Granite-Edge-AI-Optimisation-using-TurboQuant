---
title: "OpenVINO End-to-End LLM Flow"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "OpenVINO/Complete OpenVINO layout.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-OV-BENCHMARK-2026"
source_ids:
  - "SRC-OV-GENAI-2026"
  - "SRC-OV-LLMPIPELINE-2025"
  - "SRC-OV-BENCHMARK-2026"
---

# OpenVINO end-to-end LLM flow

> **Evidence basis:** The main factual claims in this note are traced to [SRC-OV-GENAI-2026](../00-sources/official-documentation.md#src-ov-genai-2026), [SRC-OV-LLMPIPELINE-2025](../00-sources/official-documentation.md#src-ov-llmpipeline-2025), [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



The supplied research separates the system into three levels. [SRC-OV-GENAI-2026]

## Level 1: Generative pipeline

```text
User text
-> tokenizer
-> token IDs
-> generation settings
-> next-token selection
-> text output
```

## Level 2: Language-model architecture

```text
Token embeddings
-> position information
-> repeated model blocks
-> attention or other sequence processing
-> feed-forward or experts
-> final projection
-> logits
```

For transformer attention:

```text
Q and K -> scaled masked scores -> softmax
softmax weights x V -> attention output
```

The KV cache stores earlier keys and values during decoding.

## Level 3: OpenVINO execution

```text
Detailed operation graph in IR
-> Runtime validates and loads it
-> device plugin compiles it
-> hardware executes it
```

## Granite architecture warning

The source research also discusses hybrid Granite 4.0 models that combine Mamba-style state blocks with transformer attention blocks. Conventional KV-cache compression only applies to the transformer-attention cache, not automatically to every Mamba state.

The full source extraction keeps the longer formulas, diagrams and operation-level examples.

## Sources used

- [SRC-OV-GENAI-2026](../00-sources/official-documentation.md#src-ov-genai-2026) — Generative AI workflow.
- [SRC-OV-LLMPIPELINE-2025](../00-sources/official-documentation.md#src-ov-llmpipeline-2025) — openvino_genai.LLMPipeline (2025 documentation snapshot).
- [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026) — OpenVINO Benchmark Tool.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
