---
title: "Research to Development Handoff"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-MS-WINDOWS-DESKTOP-2026"
source_ids:
  - "SRC-BOOK-ENGINEERING-SOFTWARE-PRODUCTS-2021"
  - "SRC-BOOK-SYSTEMS-ENGINEERING-2020"
  - "SRC-MS-WINDOWS-DESKTOP-2026"
---

# Research-to-development handoff

> **Evidence basis:** The main factual claims in this note are traced to [SRC-BOOK-ENGINEERING-SOFTWARE-PRODUCTS-2021](../00-sources/books-and-project-guidance.md#src-book-engineering-software-products-2021), [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020), [SRC-MS-WINDOWS-DESKTOP-2026](../00-sources/official-documentation.md#src-ms-windows-desktop-2026). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



Research is ready to become a development task when it has: [SRC-BOOK-ENGINEERING-SOFTWARE-PRODUCTS-2021]

- a clear problem and expected benefit;
- a selected source and pinned version;
- an identified model and hardware target;
- acceptance criteria;
- a baseline and comparison plan;
- known risks and unsupported routes;
- an evidence folder location;
- a rollback or fallback path.

## Example handoff: Intel SYCL TQ3_0

**Research finding:** animehacker contains a custom Intel SYCL TQ3_0 route.

**Unknowns:** Granite compatibility, target integrated-GPU support, quality, memory and speed.

**Development task:** build the pinned commit on the target Windows machine and execute the common test sequence.

**Acceptance:** real TQ3_0 cache allocation, stable Granite output and measured benefit against F16/Q8_0.

**Fallback:** standard llama.cpp SYCL or CPU without custom cache compression.

## Example handoff: OpenVINO baseline

**Research finding:** OpenVINO provides a separate Intel execution route using Runtime and device plugins.

**Development task:** run the same Granite model or closest supported exported model on CPU and GPU, then record compatibility, speed and memory.

**Acceptance:** reproducible local generation and a saved device comparison.

## Further reading

- *Systems Engineering: Principles and Practice*, Chapters 3, 6 and 12.
- *Engineering Software Products*, agile planning and architecture chapters.

## Sources used

- [SRC-BOOK-ENGINEERING-SOFTWARE-PRODUCTS-2021](../00-sources/books-and-project-guidance.md#src-book-engineering-software-products-2021) — Engineering Software Products: An Introduction to Modern Software Engineering.
- [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020) — Systems Engineering: Principles and Practice, Third Edition.
- [SRC-MS-WINDOWS-DESKTOP-2026](../00-sources/official-documentation.md#src-ms-windows-desktop-2026) — Build desktop apps for Windows.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
