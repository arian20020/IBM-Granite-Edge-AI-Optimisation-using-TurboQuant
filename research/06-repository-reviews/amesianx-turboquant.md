# AmesianX/TurboQuant

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

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

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: Final analysis of AmesianX TurboQuant Repo

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/AmesianX TurboQuant/Final analysis of AmesianX TurboQuant Repo.docx`

**Repository:** TurboQuant  
**URL:** <https://github.com/AmesianX/TurboQuant>  
**Owner:** AmesianX  
**Branch:** main  
**Commit:** 65737790af0e7c731bec2281cccb127d23f3eeae  
**Last relevant update:** 26 June 2026  
**Licence:** MIT

**Purpose:** Implement TurboQuant KV-cache compression directly inside llama.cpp for GGUF inference, especially on NVIDIA hardware and long contexts.

**Claimed method:** Walsh–Hadamard rotation, Lloyd–Max codebook quantisation and optional QJL residual correction.

**Actual method:** Rotates and normalises KV vectors, compresses coordinates into low-bit codebook indices and optionally adds residual-sign or QJL information. It also uses model- and head-dimension-specific fallbacks.

**Difference from formal TurboQuant:** It adapts the method depending on head dimension. It may use QJL at dimension 256, direct residual signs at dimension 128 and q8_0 keys at dimension 64. It also adds attention correction, model-specific kernels and many llama.cpp engineering features.

**Weight quantisation:** Mainly inherited llama.cpp formats, plus experimental FP4/FP8 support. TurboQuant-specific weight compression is not the repository’s main purpose.

**KV-cache quantisation:** Yes; main feature.

**Key-cache types:** q8_0, tbq3, tbq4, tbqp3, tbqp4, with dimension-specific variants.

**Value-cache types:** Usually tbq3 or tbq4; TBQP/QJL formats are not intended for values.

**Bits per value:** Approximately 3–4 bits for the main TBQ/TBQP formats, depending on subtype and metadata.

**Block structure:** Varies according to attention head dimension, including 64, 128, 256 and 512-value arrangements.

**Metadata overhead:** Norms, packed indices and optional one-bit residual information.

**Actual memory packing:** Yes; the KV cache is physically stored in packed low-bit structures.

**CPU support:** Standard llama.cpp CPU inference works. TurboQuant CPU execution is incomplete and not well validated.

**CUDA support:** Yes; main and best-supported route.

**ROCm support:** Normal llama.cpp may support ROCm, but this repository’s custom TurboQuant formats are not clearly documented or validated on ROCm.

**SYCL support:** No completed Intel TurboQuant route documented.

**OpenVINO support:** No completed TurboQuant/OpenVINO integration documented.

**NPU support:** No.

**Supported and unsupported routes:**

Normal GGUF + llama.cpp CPU  
→ Supported

Normal GGUF + NVIDIA CUDA  
→ Supported

TBQ/TBQP TurboQuant + NVIDIA CUDA  
→ Supported; main route

TBQ/TBQP TurboQuant entirely on CPU  
→ Incomplete and not well validated

TBQ/TBQP + AMD ROCm  
→ Not clearly validated

TBQ/TBQP + Intel SYCL  
→ Not supported

TBQ/TBQP + OpenVINO  
→ Not supported

TBQ/TBQP + Intel NPU  
→ Not supported

**Windows build:** Standard llama.cpp Windows builds should work, but the repository’s main testing is CUDA/Linux-focused.

**Required tools:** Git, CMake, C++ compiler, CUDA Toolkit and current NVIDIA drivers.

**Build command:**

git clone <https://github.com/AmesianX/TurboQuant.git>  
cd TurboQuant  
cmake -S . -B build -DGGML_CUDA=ON  
cmake --build build --config Release -j

**Run command:**

llama-cli.exe -m granite.gguf -ngl 99 -fa on -c 8192 \`  
-ctk tbqp3_0 -ctv tbq3_0 -p "Test prompt"

**Granite compatibility:** Not confirmed.

**Tested Granite model:** None documented.

**Supported head dimensions:** 64, 128, 256 and 512, but different safety policies are used for each.

**Flash Attention:** Yes, mainly CUDA.

**Maximum tested context:** 1,048,576 tokens using DeepSeek-V4 and TBQ3 across two DGX Spark systems.

**Published benchmarks:** Yes, mainly Qwen, GPT-OSS and DeepSeek models.

**Quality tests:** Perplexity, long-context generation, exact-answer tests and the repository’s “Pauli Test.”

**Memory tests:** Yes; reports up to around 5.2× KV-cache compression in some configurations.

**Speed tests:** Yes; prompt-processing and generation speed.

**Known bugs:** Low-bit key compression can break output at head dimension 64 even when perplexity looks better. The repository therefore falls back to q8_0 keys for some models.

**Files containing main implementation:**  
ggml/src/ggml-common.h  
ggml/src/ggml-cuda/  
src/llama-kv-cache.cpp  
src/llama-context.cpp  
turboquant/

**Ease of integration:** Medium to difficult. Easiest through llama-server as a separate backend process.

**Maintenance risk:** High; very experimental, rapidly changing and heavily customised for specific models and NVIDIA hardware.

**How we could use it:** Test Granite GGUF with formal-style QJL or residual-corrected TurboQuant on the Lenovo RTX 4060 and compare it with TheTom’s simpler TurboQuant+ implementation.

**Final recommendation:** Use as a secondary research implementation for comparing QJL/residual correction. Do not use it as the main Intel, OpenVINO or CPU-only backend.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-REPO-AMESIANX`
- `SRC-LLAMACPP-GITHUB`
- `SRC-PAPER-TURBOQUANT-2025`
