---
title: "AmesianX/TurboQuant"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-AMESIANX"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# AmesianX/TurboQuant

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-AMESIANX](../00-sources/github-repositories.md#src-repo-amesianx), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/AmesianX/TurboQuant> |
| Branch | main |
| Commit | `65737790af0e7c731bec2281cccb127d23f3eeae` |
| Last relevant update in supplied research | 26 June 2026 |
| Project position | **Secondary research candidate** |

## What it does

WHT-style rotation, low-bit codebooks and optional residual/QJL information with head-dimension-specific routes. [SRC-REPO-AMESIANX]

## Difference from formal TurboQuant

Practical adaptation; QJL/residual handling changes by head dimension.

## Storage

Real packed 3-4 bit TBQ/TBQP cache structures.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Incomplete/not well validated for TurboQuant |
| CUDA | Main supported route |
| ROCm/HIP | Unclear |
| Vulkan | Not recorded as a custom route |
| Intel SYCL | No completed route |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Not confirmed in supplied evidence

## Project recommendation

Use for comparison of QJL/residual ideas, not as the main Intel or OpenVINO backend.

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

- [SRC-REPO-AMESIANX](../00-sources/github-repositories.md#src-repo-amesianx) — AmesianX/TurboQuant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
