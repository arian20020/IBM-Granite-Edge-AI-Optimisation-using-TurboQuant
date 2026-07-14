---
title: "unixsysdev/llama-turboquant"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-UNIXSYSDEV"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# unixsysdev/llama-turboquant

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-UNIXSYSDEV](../00-sources/github-repositories.md#src-repo-unixsysdev), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/unixsysdev/llama-turboquant> |
| Branch | main |
| Commit | `03fa8abc4708dfc13858de0a74695075702c8e26` |
| Last relevant update in supplied research | 25 March 2026 |
| Project position | **Reference / superseded base** |

## What it does

32-value WHT, two-bit codebook indices, stored residual signs and one FP16 scale. [SRC-REPO-UNIXSYSDEV]

## Difference from formal TurboQuant

Stored residual signs are not used in the inspected dequantisation or fused attention path, so formal QJL correction is not functionally applied.

## Storage

14 bytes per 32 key values, about 3.5 effective bits; unused residual storage remains.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Fallback |
| CUDA | Implemented/experimental |
| ROCm/HIP | Implemented but build-dependent |
| Vulkan | No custom route |
| Intel SYCL | No custom route |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Not proven

## Project recommendation

Do not use as a main candidate. Use to study the base design behind animehacker.

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

- [SRC-REPO-UNIXSYSDEV](../00-sources/github-repositories.md#src-repo-unixsysdev) — unixsysdev/llama-turboquant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
