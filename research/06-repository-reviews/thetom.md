---
title: "TheTom/llama-cpp-turboquant"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-THETOM"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# TheTom/llama-cpp-turboquant

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-THETOM](../00-sources/github-repositories.md#src-repo-thetom), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/TheTom/llama-cpp-turboquant> |
| Branch | feature/turboquant-kv-cache |
| Commit | `Pin before testing; not selected in supplied summary` |
| Last relevant update in supplied research | Active development in June 2026 |
| Project position | **Main general GGUF prototype** |

## What it does

Norm extraction, fixed signs/WHT rotation and low-bit codebook formats; experimental weight and cache quantisation. [SRC-REPO-THETOM]

## Difference from formal TurboQuant

TurboQuant-inspired/TurboQuant+; production formats do not necessarily use the complete formal QJL stage.

## Storage

Real turbo2/turbo3/turbo4 cache formats and TQ3_1S/TQ4_1S weight formats.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Present |
| CUDA | Strong and suitable for NVIDIA testing |
| ROCm/HIP | Experimental |
| Vulkan | General backend inherited; custom support not established |
| Intel SYCL | Incomplete/experimental in supplied summary |
| OpenVINO | Custom Turbo formats not documented |
| Intel NPU | No |

## Granite status

Must be tested

## Project recommendation

Use as the main general GGUF/TurboQuant prototype and NVIDIA comparison, but not as proof of finished Intel support.

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

- [SRC-REPO-THETOM](../00-sources/github-repositories.md#src-repo-thetom) — TheTom/llama-cpp-turboquant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
