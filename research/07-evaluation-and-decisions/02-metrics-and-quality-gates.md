---
title: "Metrics and Quality Gates"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-OV-BENCHMARK-2026"
source_ids:
  - "SRC-BOOK-AI-ENGINEERING-2025"
  - "SRC-BOOK-SYSTEMS-ENGINEERING-2020"
  - "SRC-OV-BENCHMARK-2026"
---

# Metrics and quality gates

> **Evidence basis:** The main factual claims in this note are traced to [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025), [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020), [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Correctness gate

- application starts;
- model loads;
- first response completes;
- no obvious corrupted or repeated output;
- deterministic settings are stable enough for comparison.

## Memory gate

- cache bytes are lower than the selected baseline;
- total peak memory is recorded;
- metadata and padding are included;
- no hidden full-precision duplicate cache remains.

## Performance gate

- warm-up separated from recorded runs;
- prompt and decode stages reported separately;
- repeated trials include median and variation;
- speed loss is judged against the memory/context benefit.

## Quality gate

- fixed benchmark prompts are versioned;
- long-context retrieval is tested at different positions;
- structured outputs are validated automatically;
- manual review uses a written rubric;
- quality loss remains inside the project's agreed threshold.

## Stability gate

- repeated runs do not crash;
- long-context allocation succeeds;
- unsupported combinations fail clearly;
- logs do not show silent fallback without being recorded.

## User-value gate

The configuration should map to a clear user profile: quality, balanced or efficiency. A technically interesting result that offers no practical user benefit should remain a research finding rather than a product default. [SRC-BOOK-AI-ENGINEERING-2025]

## Further reading

- *AI Engineering*, Chapters 3-4.
- *The UX Book*, Chapters 21-27.
- *Systems Engineering: Principles and Practice*, Chapter 17.

## Sources used

- [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025) — AI Engineering: Building Applications with Foundation Models.
- [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020) — Systems Engineering: Principles and Practice, Third Edition.
- [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026) — OpenVINO Benchmark Tool.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
