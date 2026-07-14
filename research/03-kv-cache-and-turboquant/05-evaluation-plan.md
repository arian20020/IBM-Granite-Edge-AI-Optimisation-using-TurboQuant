---
title: "KV Cache Compression Evaluation Plan"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "KV-Cache Compression/3. KV Cache compression Evaluation Idea.docx"
  - "Quantisation implementations/llama.cpp quantisation/Tests to run.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-OLLAMA-USAGE"
source_ids:
  - "SRC-BOOK-AI-ENGINEERING-2025"
  - "SRC-BOOK-SYSTEMS-ENGINEERING-2020"
  - "SRC-OV-BENCHMARK-2026"
  - "SRC-OLLAMA-USAGE"
---

# KV-cache compression evaluation plan

> **Evidence basis:** The main factual claims in this note are traced to [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025), [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020), [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026), [SRC-OLLAMA-USAGE](../00-sources/official-documentation.md#src-ollama-usage). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Baseline first

Run a full-precision or standard cache configuration before testing a custom format. At minimum compare: [SRC-BOOK-AI-ENGINEERING-2025]

1. F16 key and value cache.
2. Q8_0 key and value cache.
3. Repository-specific low-bit format.
4. Safer asymmetric combinations, such as higher-precision keys and lower-precision values.

## Quality measurements

- fixed task prompts;
- instruction following;
- factual and answer relevance checks;
- structured JSON validity;
- coding correctness where applicable;
- perplexity when the runtime supports it;
- long-context retrieval at several positions;
- repeated-run consistency;
- manual review of obvious corruption or looping.

## Memory measurements

- theoretical cache bytes;
- actual allocated KV-cache bytes;
- peak process RAM;
- peak VRAM/shared GPU memory;
- metadata overhead;
- maximum stable context.

## Performance measurements

- model-load time;
- time to first token;
- prompt tokens per second;
- decode tokens per second;
- total response time;
- quantisation/dequantisation overhead;
- device utilisation and power where available.

## Test dimensions

Test more than one context size, for example short, medium and long. A low-bit format may have little benefit at short context but become important at long context. Run warm-up trials before recorded trials and repeat each measured case.

## Acceptance logic

A candidate should only progress when it:

- builds reproducibly;
- runs Granite without a crash;
- produces usable output;
- gives real packed-memory savings;
- stays within an agreed quality-loss limit;
- has acceptable speed on the target hardware;
- has saved evidence for the result.

## Further reading

- *AI Engineering*, Chapters 3-4 and 9.
- *Systems Engineering: Principles and Practice*, Chapter 17.

## Sources used

- [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025) — AI Engineering: Building Applications with Foundation Models.
- [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020) — Systems Engineering: Principles and Practice, Third Edition.
- [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026) — OpenVINO Benchmark Tool.
- [SRC-OLLAMA-USAGE](../00-sources/official-documentation.md#src-ollama-usage) — Ollama generate endpoint and performance fields.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
