---
title: "spiritbuun/buun-llama-cpp"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-SPIRITBUUN"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# spiritbuun/buun-llama-cpp

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-SPIRITBUUN](../00-sources/github-repositories.md#src-repo-spiritbuun), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/spiritbuun/buun-llama-cpp> |
| Branch | master |
| Commit | `Pin before testing; none recorded` |
| Last relevant update in supplied research | Active during June 2026 |
| Project position | **Research reference only** |

## What it does

FWHT rotation followed by Viterbi trellis coding, codebooks and norm storage. [SRC-REPO-SPIRITBUUN]

## Difference from formal TurboQuant

Replaces formal QJL with trellis coding and custom scaling.

## Storage

Packed TCQ paths; recorded 3.25 or 2.25 bits per value.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | TCQ placeholders/incomplete |
| CUDA | Main route |
| ROCm/HIP | Claimed/limited experimental evidence |
| Vulkan | No custom TCQ route |
| Intel SYCL | No |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Unknown

## Project recommendation

Do not include in the main Intel implementation. Keep as an extreme-compression reference.

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

- [SRC-REPO-SPIRITBUUN](../00-sources/github-repositories.md#src-repo-spiritbuun) — spiritbuun/buun-llama-cpp.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
