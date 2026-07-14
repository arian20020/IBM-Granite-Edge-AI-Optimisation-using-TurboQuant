---
title: "BeeLlama.cpp"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-BEELLAMA"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# BeeLlama.cpp

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-BEELLAMA](../00-sources/github-repositories.md#src-repo-beellama), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/Anbeeld/beellama.cpp> |
| Branch | Not recorded |
| Commit | `Not recorded` |
| Last relevant update in supplied research | Not recorded |
| Project position | **Research reference / CUDA comparison** |

## What it does

Combines DFlash speculative decoding, TurboQuant/TCQ cache compression and adaptive server controls. [SRC-REPO-BEELLAMA]

## Difference from formal TurboQuant

A broader combined fork, not one exact formal TurboQuant implementation.

## Storage

Includes classic TurboQuant and TCQ formats; extreme low-bit modes have larger quality risk.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Inherited llama.cpp route |
| CUDA | Strongest feature routes |
| ROCm/HIP | Inherited/varies |
| Vulkan | Inherited, but custom feature support not proven |
| Intel SYCL | Inherited, but custom feature support not proven |
| OpenVINO | Inherited references do not prove feature compatibility |
| Intel NPU | No proven custom route |

## Granite status

No Granite-specific evidence recorded

## Project recommendation

Use to study speculative decoding and TCQ, or as an NVIDIA comparison. Not a ready Intel TurboQuant backend.

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

- [SRC-REPO-BEELLAMA](../00-sources/github-repositories.md#src-repo-beellama) — Anbeeld/beellama.cpp.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
