---
title: "TiredOfEverything/llama-cpp-turboquant"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-TIREDOFEVERYTHING"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# TiredOfEverything/llama-cpp-turboquant

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-TIREDOFEVERYTHING](../00-sources/github-repositories.md#src-repo-tiredofeverything), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/TiredOfEverything/llama-cpp-turboquant> |
| Branch | master |
| Commit | `71ecbd7e82a4c7fd39b9d079a6fb40b9a6f5d5c1` |
| Last relevant update in supplied research | 29 March 2026 |
| Project position | **High-priority CUDA research candidate** |

## What it does

Optimised low-bit cache formats, custom CUDA cache/Flash-Attention paths and layer-adaptive precision. [SRC-REPO-TIREDOFEVERYTHING]

## Difference from formal TurboQuant

PolarQuant-style practical approximation; no complete formal QJL in the recorded active route.

## Storage

Real packed turbo2/turbo3/turbo4 structures.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Incomplete custom route |
| CUDA | Main and highly optimised route |
| ROCm/HIP | Not main |
| Vulkan | No custom low-bit kernels |
| Intel SYCL | No custom route |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Not tested in supplied evidence

## Project recommendation

Use for RTX/CUDA benchmarking and implementation study, not as the main Intel route.

## Required evidence before adoption

- pin and record the exact commit;
- build on the target Windows machine;
- run the same Granite GGUF baseline and prompts;
- prove real packed cache allocation;
- record memory, speed, stability and quality;
- compare against standard cache formats;
- save logs and known limitations.

The full, longer analysis and commands are preserved in `98-source-extracts` and mapped in the source register.

## Sources used

- [SRC-REPO-TIREDOFEVERYTHING](../00-sources/github-repositories.md#src-repo-tiredofeverything) — TiredOfEverything/llama-cpp-turboquant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
