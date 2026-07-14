---
title: "TurboQuant Implementation Landscape"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "KV-Cache Compression/6. TurboQuant Implementations.docx"
  - "All repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-REPO-UNIXSYSDEV"
source_ids:
  - "SRC-PAPER-TURBOQUANT-2025"
  - "SRC-REPO-AMESIANX"
  - "SRC-REPO-ATOMICBOT"
  - "SRC-REPO-ANIMEHACKER"
  - "SRC-REPO-ATOMICMILKSHAKE"
  - "SRC-REPO-BEELLAMA"
  - "SRC-REPO-SPIRITBUUN"
  - "SRC-REPO-THEPRADIP"
  - "SRC-REPO-THETOM"
  - "SRC-REPO-TIREDOFEVERYTHING"
  - "SRC-REPO-UNIXSYSDEV"
---

# TurboQuant implementation landscape

> **Evidence basis:** The main factual claims in this note are traced to [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025), [SRC-REPO-AMESIANX](../00-sources/github-repositories.md#src-repo-amesianx), [SRC-REPO-ATOMICBOT](../00-sources/github-repositories.md#src-repo-atomicbot), [SRC-REPO-ANIMEHACKER](../00-sources/github-repositories.md#src-repo-animehacker), [SRC-REPO-ATOMICMILKSHAKE](../00-sources/github-repositories.md#src-repo-atomicmilkshake), [SRC-REPO-BEELLAMA](../00-sources/github-repositories.md#src-repo-beellama), plus 5 repository/source entries listed below. Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



The supplied research found that the practical repositories form several groups. [SRC-PAPER-TURBOQUANT-2025]

## 1. General TurboQuant-style GGUF prototypes

- TheTom
- AtomicBot
- atomicmilkshake
- TiredOfEverything
- AmesianX

These contain real packed cache formats and practical llama.cpp integration, but their active methods and hardware support differ.

## 2. Intel-specific candidate

- animehacker adds a custom TQ3_0 route to the Intel SYCL backend.

This is highly relevant, but the recorded evidence did not include Granite on the target integrated Intel GPU.

## 3. Extreme or alternative compression

- spiritbuun adds trellis-coded quantisation.
- BeeLlama combines TCQ with speculative decoding and other server features.

These are useful research references but are strongly CUDA-focused.

## 4. Narrow or superseded experiments

- thepradip uses a simpler 4-bit Lloyd-Max format rather than the full formal method.
- unixsysdev is the base of the later animehacker fork and records residual bits that are not functionally used in the inspected attention path.

See [`../06-repository-reviews/comparison-matrix.md`](../06-repository-reviews/comparison-matrix.md) for the standardised comparison.

## Sources used

- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.
- [SRC-REPO-AMESIANX](../00-sources/github-repositories.md#src-repo-amesianx) — AmesianX/TurboQuant.
- [SRC-REPO-ATOMICBOT](../00-sources/github-repositories.md#src-repo-atomicbot) — AtomicBot-ai/atomic-llama-cpp-turboquant.
- [SRC-REPO-ANIMEHACKER](../00-sources/github-repositories.md#src-repo-animehacker) — animehacker/llama-turboquant.
- [SRC-REPO-ATOMICMILKSHAKE](../00-sources/github-repositories.md#src-repo-atomicmilkshake) — atomicmilkshake/llama-cpp-turboquant.
- [SRC-REPO-BEELLAMA](../00-sources/github-repositories.md#src-repo-beellama) — Anbeeld/beellama.cpp.
- [SRC-REPO-SPIRITBUUN](../00-sources/github-repositories.md#src-repo-spiritbuun) — spiritbuun/buun-llama-cpp.
- [SRC-REPO-THEPRADIP](../00-sources/github-repositories.md#src-repo-thepradip) — thepradip/turboquant-llamacpp.
- [SRC-REPO-THETOM](../00-sources/github-repositories.md#src-repo-thetom) — TheTom/llama-cpp-turboquant.
- [SRC-REPO-TIREDOFEVERYTHING](../00-sources/github-repositories.md#src-repo-tiredofeverything) — TiredOfEverything/llama-cpp-turboquant.
- [SRC-REPO-UNIXSYSDEV](../00-sources/github-repositories.md#src-repo-unixsysdev) — unixsysdev/llama-turboquant.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
