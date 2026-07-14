---
title: "Backend Decision Record"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-LLAMACPP-GITHUB"
source_ids:
  - "SRC-BOOK-SOFTWARE-ARCHITECTURE-2025"
  - "SRC-MS-WINUI3-2026"
  - "SRC-MS-WINDOWS-APP-SDK-2026"
  - "SRC-OV-GENAI-2026"
  - "SRC-LLAMACPP-GITHUB"
---

# Backend decision record

> **Evidence basis:** The main factual claims in this note are traced to [SRC-BOOK-SOFTWARE-ARCHITECTURE-2025](../00-sources/books-and-project-guidance.md#src-book-software-architecture-2025), [SRC-MS-WINUI3-2026](../00-sources/official-documentation.md#src-ms-winui3-2026), [SRC-MS-WINDOWS-APP-SDK-2026](../00-sources/official-documentation.md#src-ms-windows-app-sdk-2026), [SRC-OV-GENAI-2026](../00-sources/official-documentation.md#src-ov-genai-2026), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Current research position

The project should maintain two main runtime tracks. [SRC-BOOK-SOFTWARE-ARCHITECTURE-2025]

### Track A: GGUF / llama.cpp

Purpose:

- practical local Granite inference;
- standard and custom KV-cache formats;
- real packed TurboQuant-style experiments;
- local server integration with the Windows UI.

Main research candidates:

- TheTom for the broad prototype;
- AtomicBot for Vulkan;
- animehacker for Intel SYCL;
- a CUDA fork for NVIDIA comparison.

### Track B: OpenVINO

Purpose:

- Intel-native baseline and acceleration;
- CPU/GPU/NPU comparison;
- IR and device-plugin execution;
- standard OpenVINO model and cache-precision capabilities.

## Why the tracks stay separate initially

The supplied research found no finished repository that combines the reviewed TurboQuant cache formats with a proven OpenVINO or Intel NPU implementation. Keeping separate tracks makes the baseline clear and reduces integration risk.

## Decision status

**Status: provisional.** Final backend choice depends on target-device experiments and application integration evidence.

## Architecture principle

Use a backend interface in the Windows application so the UI does not depend directly on one experimental runtime. This supports replacement, comparison and fallback.

## Further reading

- *Fundamentals of Software Architecture*, Chapters 2, 4-6 and 19.
- *Engineering Software Products*, Chapter 4.
- `windows-apps.pdf`, WinUI and Windows App SDK guidance.

## Sources used

- [SRC-BOOK-SOFTWARE-ARCHITECTURE-2025](../00-sources/books-and-project-guidance.md#src-book-software-architecture-2025) — Fundamentals of Software Architecture: A Modern Engineering Approach, Second Edition.
- [SRC-MS-WINUI3-2026](../00-sources/official-documentation.md#src-ms-winui3-2026) — WinUI 3.
- [SRC-MS-WINDOWS-APP-SDK-2026](../00-sources/official-documentation.md#src-ms-windows-app-sdk-2026) — Windows App SDK.
- [SRC-OV-GENAI-2026](../00-sources/official-documentation.md#src-ov-genai-2026) — Generative AI workflow.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
