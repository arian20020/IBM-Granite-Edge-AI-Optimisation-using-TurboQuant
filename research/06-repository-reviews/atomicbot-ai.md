---
title: "AtomicBot-ai/atomic-llama-cpp-turboquant"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-ATOMICBOT"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# AtomicBot-ai/atomic-llama-cpp-turboquant

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-ATOMICBOT](../00-sources/github-repositories.md#src-repo-atomicbot), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant> |
| Branch | feature/turboquant-kv-cache |
| Commit | `b0e900a28ee4172bbb97df0d1ea1c78e86bc0ac6` |
| Last relevant update in supplied research | 17 June 2026 |
| Project position | **High-priority Intel Vulkan candidate** |

## What it does

128-value rotation plus packed turbo2/turbo3/turbo4 codebook formats; also experimental weight formats. [SRC-REPO-ATOMICBOT]

## Difference from formal TurboQuant

Default formats focus on the main approximation; complete formal QJL is not the default path.

## Storage

Real packed cache; about 2.1, 3.1 and 4.25 effective bits recorded.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Reference/correctness route |
| CUDA | Supported |
| ROCm/HIP | Partial |
| Vulkan | Important candidate, including Intel GPU possibility |
| Intel SYCL | No completed custom route |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Must be tested

## Project recommendation

Test early on Intel Vulkan. Do not promote to default until Granite quality, device support, speed and memory are reproduced.

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

- [SRC-REPO-ATOMICBOT](../00-sources/github-repositories.md#src-repo-atomicbot) — AtomicBot-ai/atomic-llama-cpp-turboquant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
