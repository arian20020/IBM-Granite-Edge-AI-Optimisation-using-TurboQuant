---
title: "atomicmilkshake/llama-cpp-turboquant"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-ATOMICMILKSHAKE"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# atomicmilkshake/llama-cpp-turboquant

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-ATOMICMILKSHAKE](../00-sources/github-repositories.md#src-repo-atomicmilkshake), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/atomicmilkshake/llama-cpp-turboquant> |
| Branch | feature/triattention |
| Commit | `75016369b38c841da095244d07ab86453023b11a` |
| Last relevant update in supplied research | 8 April 2026 |
| Project position | **CUDA/Metal research candidate** |

## What it does

128-value rotation and packed turbo2/3/4 plus experimental TriAttention token pruning. [SRC-REPO-ATOMICMILKSHAKE]

## Difference from formal TurboQuant

Main approximation is active; legacy QJL code is not the default. Adds padding, adaptive precision and pruning.

## Storage

Real packed structures; recorded about 2.125, 3.125 and 4.25 bits.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Reference/fallback |
| CUDA | Main route |
| ROCm/HIP | Secondary/experimental |
| Vulkan | No custom route |
| Intel SYCL | No custom route |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Not specifically validated

## Project recommendation

Useful for CUDA/Metal experiments and TriAttention research. Do not use as the Intel backend.

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

- [SRC-REPO-ATOMICMILKSHAKE](../00-sources/github-repositories.md#src-repo-atomicmilkshake) — atomicmilkshake/llama-cpp-turboquant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
