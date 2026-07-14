---
title: "animehacker/llama-turboquant"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-ANIMEHACKER"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# animehacker/llama-turboquant

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-ANIMEHACKER](../00-sources/github-repositories.md#src-repo-animehacker), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/animehacker/llama-turboquant> |
| Branch | main |
| Commit | `5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc` |
| Last relevant update in supplied research | 4 May 2026 |
| Project position | **High-priority Intel SYCL candidate** |

## What it does

32-value signs and Walsh-Hadamard transform, eight-value codebook and packed TQ3_0 with one FP16 scale. [SRC-REPO-ANIMEHACKER]

## Difference from formal TurboQuant

Implements the main approximation stage only; the third bit is a codebook index bit, not QJL correction.

## Storage

14 bytes per 32 values, about 3.5 effective bits and 4.57x recorded compression.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Supported but slower |
| CUDA | Supported/experimental |
| ROCm/HIP | Experimental with reported stability issues |
| Vulkan | No custom TQ3_0 route |
| Intel SYCL | Custom Intel route; key project value |
| OpenVINO | No custom integration |
| Intel NPU | No |

## Granite status

Normal support inherited, but Granite + TQ3_0 + target Intel GPU not confirmed

## Project recommendation

Test as the main Intel-specific SYCL candidate after a standard SYCL baseline.

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

- [SRC-REPO-ANIMEHACKER](../00-sources/github-repositories.md#src-repo-animehacker) — animehacker/llama-turboquant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
