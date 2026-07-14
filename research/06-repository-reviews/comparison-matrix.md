---
title: "TurboQuant Repository Comparison Matrix"
status: "curated"
version: "2.0"
last_updated: "2026-07-14"
source_documents:
  - "All repository-analysis DOCX files"
verification_note: "Supplied research reorganised; time-sensitive claims must be rechecked before testing."
  - "SRC-PAPER-TURBOQUANT-2025"
source_ids:
  - "SRC-REPO-AMESIANX"
  - "SRC-REPO-ATOMICBOT"
  - "SRC-REPO-ANIMEHACKER"
  - "SRC-REPO-ATOMICMILKSHAKE"
  - "SRC-REPO-BEELLAMA"
  - "SRC-REPO-SPIRITBUUN"
  - "SRC-REPO-THEPRADIP"
  - "SRC-REPO-THETOM"
  - "SRC-REPO-TIREDOFEVERYTHING"
  - "SRC-REPO-UNIXSYSDEV"
  - "SRC-PAPER-TURBOQUANT-2025"
---

# TurboQuant repository comparison matrix

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-AMESIANX](../00-sources/github-repositories.md#src-repo-amesianx), [SRC-REPO-ATOMICBOT](../00-sources/github-repositories.md#src-repo-atomicbot), [SRC-REPO-ANIMEHACKER](../00-sources/github-repositories.md#src-repo-animehacker), [SRC-REPO-ATOMICMILKSHAKE](../00-sources/github-repositories.md#src-repo-atomicmilkshake), [SRC-REPO-BEELLAMA](../00-sources/github-repositories.md#src-repo-beellama), [SRC-REPO-SPIRITBUUN](../00-sources/github-repositories.md#src-repo-spiritbuun), plus 5 repository/source entries listed below. Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.



The table is a decision aid, not proof of runtime compatibility. Each cell reflects the supplied research at its recorded commit/date. [SRC-REPO-AMESIANX]

| Repository | Project position | CPU | CUDA | Vulkan | Intel SYCL | OpenVINO | Granite evidence |
|---|---|---|---|---|---|---|---|
| [AmesianX/TurboQuant](amesianx-turboquant.md) | Secondary research candidate | Incomplete/not well validated for TurboQuant | Main supported route | Not recorded as a custom route | No completed route | No | Not confirmed in supplied evidence |
| [AtomicBot-ai/atomic-llama-cpp-turboquant](atomicbot-ai.md) | High-priority Intel Vulkan candidate | Reference/correctness route | Supported | Important candidate, including Intel GPU possibility | No completed custom route | No | Must be tested |
| [BeeLlama.cpp](beellama.md) | Research reference / CUDA comparison | Inherited llama.cpp route | Strongest feature routes | Inherited, but custom feature support not proven | Inherited, but custom feature support not proven | Inherited references do not prove feature compatibility | No Granite-specific evidence recorded |
| [TheTom/llama-cpp-turboquant](thetom.md) | Main general GGUF prototype | Present | Strong and suitable for NVIDIA testing | General backend inherited; custom support not established | Incomplete/experimental in supplied summary | Custom Turbo formats not documented | Must be tested |
| [TiredOfEverything/llama-cpp-turboquant](tiredofeverything.md) | High-priority CUDA research candidate | Incomplete custom route | Main and highly optimised route | No custom low-bit kernels | No custom route | No | Not tested in supplied evidence |
| [animehacker/llama-turboquant](animehacker.md) | High-priority Intel SYCL candidate | Supported but slower | Supported/experimental | No custom TQ3_0 route | Custom Intel route; key project value | No custom integration | Normal support inherited, but Granite + TQ3_0 + target Intel GPU not confirmed |
| [atomicmilkshake/llama-cpp-turboquant](atomicmilkshake.md) | CUDA/Metal research candidate | Reference/fallback | Main route | No custom route | No custom route | No | Not specifically validated |
| [spiritbuun/buun-llama-cpp](spiritbuun.md) | Research reference only | TCQ placeholders/incomplete | Main route | No custom TCQ route | No | No | Unknown |
| [thepradip/turboquant-llamacpp](thepradip.md) | Exclude from main candidates | Most complete route | Partially integrated; correctness needs validation | No custom route | No custom route | No | Not proven |
| [unixsysdev/llama-turboquant](unixsysdev.md) | Reference / superseded base | Fallback | Implemented/experimental | No custom route | No custom route | No | Not proven |

## Recommended test order

1. Standard upstream llama.cpp baseline.
2. TheTom as the broad GGUF/TurboQuant prototype.
3. AtomicBot as the cross-vendor Vulkan candidate.
4. animehacker as the Intel-specific SYCL candidate.
5. One strong NVIDIA implementation for comparison, such as TiredOfEverything or atomicmilkshake.
6. Keep the remaining repositories as research references unless a later requirement justifies them.

## Selection rule

A candidate does not become the application backend because it has the best claimed compression ratio. It must pass the common test protocol on the target Granite model and target computer.

## Sources used

- [SRC-REPO-AMESIANX](../00-sources/github-repositories.md#src-repo-amesianx) — AmesianX/TurboQuant.
- [SRC-REPO-ATOMICBOT](../00-sources/github-repositories.md#src-repo-atomicbot) — AtomicBot-ai/atomic-llama-cpp-turboquant.
- [SRC-REPO-ANIMEHACKER](../00-sources/github-repositories.md#src-repo-animehacker) — animehacker/llama-turboquant.
- [SRC-REPO-ATOMICMILKSHAKE](../00-sources/github-repositories.md#src-repo-atomicmilkshake) — atomicmilkshake/llama-cpp-turboquant.
- [SRC-REPO-BEELLAMA](../00-sources/github-repositories.md#src-repo-beellama) — Anbeeld/beellama.cpp.
- [SRC-REPO-SPIRITBUUN](../00-sources/github-repositories.md#src-repo-spiritbuun) — spiritbuun/buun-llama-cpp.
- [SRC-REPO-THEPRADIP](../00-sources/github-repositories.md#src-repo-thepradip) — thepradip/turboquant-llamacpp.
- [SRC-REPO-THETOM](../00-sources/github-repositories.md#src-repo-thetom) — TheTom/llama-cpp-turboquant.
- [SRC-REPO-TIREDOFEVERYTHING](../00-sources/github-repositories.md#src-repo-tiredofeverything) — TiredOfEverything/llama-cpp-turboquant.
- [SRC-REPO-UNIXSYSDEV](../00-sources/github-repositories.md#src-repo-unixsysdev) — unixsysdev/llama-turboquant.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.
