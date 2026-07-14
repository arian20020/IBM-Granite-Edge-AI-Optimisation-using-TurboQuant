---
title: "Common Repository Test Sequence"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/Tests to run.docx"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-OLLAMA-USAGE"
source_ids:
  - "SRC-BOOK-SYSTEMS-ENGINEERING-2020"
  - "SRC-BOOK-AI-ENGINEERING-2025"
  - "SRC-OV-BENCHMARK-2026"
  - "SRC-OLLAMA-USAGE"
---

# Common repository test sequence

> **Evidence basis:** The main factual claims in this note are traced to [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020), [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025), [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026), [SRC-OLLAMA-USAGE](../00-sources/official-documentation.md#src-ollama-usage). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



Every implementation should pass the same first sequence. [SRC-BOOK-SYSTEMS-ENGINEERING-2020]

1. Clone the correct branch.
2. Record and pin the exact commit.
3. Record the machine, operating system, drivers and toolchain.
4. Build successfully from a clean environment.
5. Run `llama-bench` or a basic smoke command.
6. Load the same Granite GGUF model.
7. Run F16 cache baseline.
8. Run Q8_0 cache baseline.
9. Run the repository-specific low-bit mode.
10. Compare output quality.
11. Measure RAM, VRAM and actual KV-cache allocation.
12. Measure prompt speed, decode speed and time to first token.
13. Repeat longer contexts.
14. Save logs, commands, screenshots and result tables.
15. Record pass, fail, blocked or inconclusive.

A failure at an early stage should still create an evidence record explaining why later stages were not run.

## Sources used

- [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020) — Systems Engineering: Principles and Practice, Third Edition.
- [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025) — AI Engineering: Building Applications with Foundation Models.
- [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026) — OpenVINO Benchmark Tool.
- [SRC-OLLAMA-USAGE](../00-sources/official-documentation.md#src-ollama-usage) — Ollama generate endpoint and performance fields.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
