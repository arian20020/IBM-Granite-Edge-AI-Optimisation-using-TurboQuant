---
title: "thepradip/turboquant-llamacpp"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "Matching repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-THEPRADIP"
  - "SRC-LLAMACPP-GITHUB"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# thepradip/turboquant-llamacpp

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-THEPRADIP](../00-sources/github-repositories.md#src-repo-thepradip), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/thepradip/turboquant-llamacpp> |
| Branch | master |
| Commit | `8598f2edfbfd87b92d8f70e95f9d3de623ceff3d` |
| Last relevant update in supplied research | 8 April 2026 |
| Project position | **Exclude from main candidates** |

## What it does

32-value RMS-normalised 4-bit Lloyd-Max codebook with one FP16 scale. [SRC-REPO-THEPRADIP]

## Difference from formal TurboQuant

No formal rotation, recursive polar quantisation or QJL; a narrower TurboQuant-inspired scalar quantiser.

## Storage

18 bytes per 32 FP16 values, about 4.5 effective bits.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Most complete route |
| CUDA | Partially integrated; correctness needs validation |
| ROCm/HIP | Not validated |
| Vulkan | No custom route |
| Intel SYCL | No custom route |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Not proven

## Project recommendation

Keep only as a simple reference. Prioritise better Intel candidates.

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

- [SRC-REPO-THEPRADIP](../00-sources/github-repositories.md#src-repo-thepradip) — thepradip/turboquant-llamacpp.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
