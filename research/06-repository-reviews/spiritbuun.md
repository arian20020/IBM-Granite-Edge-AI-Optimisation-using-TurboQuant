# spiritbuun/buun-llama-cpp

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-SPIRITBUUN](../00-sources/github-repositories.md#src-repo-spiritbuun), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/spiritbuun/buun-llama-cpp> |
| Branch | master |
| Commit | `Pin before testing; none recorded` |
| Last relevant update in supplied research | Active during June 2026 |
| Project position | **Research reference only** |

## What it does

FWHT rotation followed by Viterbi trellis coding, codebooks and norm storage. [SRC-REPO-SPIRITBUUN]

## Difference from formal TurboQuant

Replaces formal QJL with trellis coding and custom scaling.

## Storage

Packed TCQ paths; recorded 3.25 or 2.25 bits per value.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | TCQ placeholders/incomplete |
| CUDA | Main route |
| ROCm/HIP | Claimed/limited experimental evidence |
| Vulkan | No custom TCQ route |
| Intel SYCL | No |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Unknown

## Project recommendation

Do not include in the main Intel implementation. Keep as an extreme-compression reference.

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

- [SRC-REPO-SPIRITBUUN](../00-sources/github-repositories.md#src-repo-spiritbuun) — spiritbuun/buun-llama-cpp.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: spiritbuun buun-llama-cpp overview

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/spiritbuun buun-llama-cpp/spiritbuun buun-llama-cpp overview.docx`

#### What this repository is

This is a **highly experimental fork of TheTom’s llama.cpp TurboQuant repository**. Its main addition is **Trellis-Coded Quantization, or TCQ**, for compressing the KV cache to around 2–3 bits per value.

Instead of replacing TheTom’s TurboQuant+ implementation, it builds on it and adds a different low-bit compression technique intended to improve quality at very low precision. The repository itself warns that it is experimental.

Official llama.cpp  
↓  
TheTom TurboQuant+ fork  
↓  
Buun fork  
↓  
Adds Trellis-Coded Quantization

The default branch is master, the licence is MIT, and the latest repository-specific update visible during this review was on 3 June 2026.

#### What TCQ means

Normal scalar quantisation treats every value separately:

Value 1 → nearest code  
Value 2 → nearest code  
Value 3 → nearest code

TCQ instead chooses a **whole sequence of codes together**. The permitted sequence must follow paths through a trellis.

KV vector  
↓  
Rotate values  
↓  
Consider many possible code sequences  
↓  
Viterbi algorithm finds the lowest-error path  
↓  
Store the path as a compact bitstream

The theory is that a 3-bit value may only store eight direct choices, but the trellis gives the complete sequence access to a much larger effective set of patterns.

The repository uses:

- a **512-state trellis** for 3-bit TCQ;

- a **256-state trellis** for 2-bit TCQ;

- Viterbi encoding to find the best path;

- direct sliding-window lookup when decoding, so the Viterbi search is not repeated during attention.

#### How the complete method works

##### 1. Normalise the cache vector

The system calculates the vector’s length and divides the values by it.

Original vector  
→ norm + normalised vector

##### 2. Apply FWHT rotation

A Fast Walsh–Hadamard Transform and random sign changes spread outliers across the vector, producing a distribution that is easier to compress.

##### 3. Run Viterbi encoding

For every position, the algorithm considers the possible trellis states and chooses the overall path with the lowest reconstruction error.

##### 4. Pack the path

The selected path is stored as a compact bitstream rather than storing a separate full index for every value.

##### 5. Store a corrected norm

A small FP16 norm value is stored with the bitstream so that reconstructed values retain an appropriate overall magnitude.

##### 6. Decode during Flash Attention

The GPU reads a small window of bits, identifies the trellis state and obtains the reconstructed value from the trained codebook. The CUDA implementation can perform this inside the Flash Attention path.

### Compression formats

#### turbo3_tcq

The 3-bit TCQ format stores:

128 values  
+  
2-byte FP16 norm  
+  
49-byte trellis bitstream  
+  
1 byte alignment  
=  
52 bytes

That equals:

52 × 8 ÷ 128  
= 3.25 bits per value

The repository describes this as approximately **5× smaller than FP16 KV cache**.

#### turbo2_tcq

The 2-bit TCQ format stores:

128 values  
+  
2-byte FP16 norm  
+  
33-byte trellis bitstream  
+  
1 byte alignment  
=  
36 bytes

That equals:

36 × 8 ÷ 128  
= 2.25 bits per value

The repository describes this as approximately **7× smaller than FP16 KV cache**.

#### Other included formats

The repository also retains and extends the scalar formats:

- turbo2

- turbo3

- turbo4

- turbo8

The scalar turbo2 and turbo3 formats provide useful controls because they use similar bit widths without the trellis. This allows the effect of TCQ itself to be measured.

### Recommended configurations

The repository recommends these main tests.

##### Safest quality option

-ctk turbo4 -ctv turbo4

##### Full 3-bit TCQ

-ctk turbo3_tcq -ctv turbo3_tcq

##### Full 2-bit TCQ

-ctk turbo2_tcq -ctv turbo2_tcq

##### Asymmetric TCQ

-ctk turbo3_tcq -ctv turbo2_tcq

The asymmetric option keeps keys at 3-bit and compresses values to 2-bit. The repository reports that this performs better than reversing the combination because key-cache errors have a greater influence on attention selection.

### Hardware support

#### CUDA

CUDA is the main and most developed implementation.

The repository contains CUDA code for:

- FWHT rotation;

- Viterbi encoding;

- trellis bit packing;

- codebook decoding;

- compressed KV-cache writing;

- Flash Attention;

- fused TCQ dequantisation.

This makes it suitable for testing on your Lenovo RTX 4060.

#### ROCm/HIP

ROCm support is also claimed and has been tested by the repository on AMD RDNA hardware. The CUDA-oriented header is written to use the HIP compatibility layer when compiled with ROCm.

#### CPU

The normal scalar Turbo formats have CPU-related code, but the **TCQ CPU implementation is not complete**.

The CPU functions for turbo3_tcq and turbo2_tcq are explicitly stubs: they write zeroed bitstreams or return zero values. The real Viterbi encoder runs through the CUDA/HIP kernel.

Therefore:

TCQ on NVIDIA CUDA  
→ real implementation

TCQ on AMD HIP  
→ supported experimental path

TCQ on CPU  
→ not a usable complete implementation

#### SYCL, OpenVINO and NPU

There is no documented TCQ implementation for:

- Intel SYCL;

- OpenVINO;

- Intel NPU.

The underlying llama.cpp fork may contain those general backends, but the custom TCQ encoding and decoding kernels are centred on CUDA/HIP.

This means the repository is useful on your Lenovo, but it is **not currently an Intel-ready TCQ solution**.

### Published evidence

The main benchmark uses Qwen3.5-27B Q6_K on an RTX 3090.

The reported 3-bit TCQ result was:

3.25 bits per value  
Perplexity: 5.802

FP16 KV-cache perplexity:  
5.805

The difference is extremely small and should not be interpreted as proof that compression is generally better than FP16. It suggests that, for that particular test, TCQ introduced no measurable quality loss and may have produced a small regularisation effect.

The reported decode speeds were:

q8_0: 31.04 tokens/second  
turbo3_tcq: 30.04 tokens/second

The repository therefore reported approximately 97% of the q8_0 decode speed in that test.

A later CUDA optimisation reported a 32% TCQ decode improvement by copying the codebook into shared GPU memory, avoiding slow divergent accesses to constant memory.

These are repository results, not independent Granite results.

### Model support

The repository says models with attention head dimensions that are multiples of 128 work directly. Other dimensions can use automatic zero-padding.

Its documented test list includes:

- Qwen3.5-27B;

- Qwen3-32B;

- Gemma 3;

- Gemma 4;

- Harmonic Hermes;

- Phi-3.

Granite is supported by the inherited llama.cpp model-loading system, but **no Granite TCQ benchmark is documented** in the repository’s tested model list.

For your project, Granite compatibility must therefore be tested rather than assumed.

### Important implementation files

**README.md**  
Main explanation, commands, formats and headline benchmarks.

**ggml/src/ggml-common.h**  
Contains the packed block structures and actual bits-per-value calculations for turbo3_tcq and turbo2_tcq.

**ggml/src/ggml-cuda/turbo-quant-cuda.cuh**  
The main TCQ implementation:

- FWHT rotation;

- Viterbi encoding;

- codebooks;

- norm correction;

- InnerQ channel scaling;

- bit packing;

- CUDA/HIP implementation.

**ggml/src/ggml-cuda/set-rows.cu**  
Writes new K and V vectors into the compressed cache.

**ggml/src/ggml-cuda/fattn-mma-f16.cuh**  
Loads and reconstructs TCQ cache values inside fused Flash Attention.

**ggml/src/ggml-cuda/fattn-mma-turbo.cuh**  
Turbo-specific fused attention templates.

**ggml/src/ggml-cuda/fattn.cu**  
Selects and dispatches the relevant Flash Attention kernel.

**ggml/src/ggml-turbo-quant.c**  
Scalar reference functions and CPU placeholders. Its TCQ CPU functions are currently stubs.

**codebooks/**  
Contains trained 2-bit and 3-bit codebooks.

**scripts/tcq_train\_\*.py**  
Contains scripts for training alternative TCQ codebooks.

### Known risks

The repository has several open or previously reported issues:

- crashes involving speculative decoding and TurboQuant;

- prompt-processing crashes with TCQ configurations;

- degraded output on some NVIDIA architectures;

- multi-GPU illegal memory access problems;

- features such as vision/MTP swapping not restoring correctly;

- hardware-specific behaviour that differs between GPU generations.

The latest code also continues to add new formats such as turbo8 and fused asymmetric cache combinations, so the repository is changing quickly. Pinning an exact commit would be essential for reproducible testing.

### Relevance to your project

This repository could provide a **second experimental GGUF KV-cache compression implementation**.

You could compare:

q8_0 K / q8_0 V  
turbo4 K / turbo4 V  
turbo3 K / turbo3 V  
turbo3_tcq K / turbo3_tcq V  
turbo3_tcq K / turbo2_tcq V  
turbo2_tcq K / turbo2_tcq V

This would help determine:

- whether trellis coding improves quality over scalar TurboQuant;

- whether 2–3-bit KV caches work with Granite;

- how much context can fit in RTX 4060 VRAM;

- whether the Viterbi encoding cost affects prompt processing;

- whether compressed decoding maintains useful speed;

- whether the quality benefits claimed for Qwen transfer to Granite.

#### Overall assessment

This repository is **more experimental and narrower than TheTom’s repository**, but it is technically valuable.

TheTom repository  
→ stronger general-purpose TurboQuant+ candidate  
→ broader backend and application integration

Buun repository  
→ stronger experimental 2–3-bit TCQ candidate  
→ CUDA-focused research comparison

For your project, it should be treated as a **secondary comparison implementation on the Lenovo RTX 4060**, not as the main Intel implementation. Its main value is testing whether TCQ can improve very-low-bit KV-cache quality beyond ordinary scalar turbo2 and turbo3.

## Original research: spiritbuun final info

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/spiritbuun buun-llama-cpp/spiritbuun final info.docx`

**Repository:** buun-llama-cpp  
**URL:** <https://github.com/spiritbuun/buun-llama-cpp>  
**Owner:** spiritbuun  
**Branch:** master  
**Commit:** Pin the exact commit if tested; none selected yet  
**Last relevant update:** Active development during June 2026  
**Licence:** MIT

**Purpose:** Experimental llama.cpp fork adding Trellis-Coded Quantization for extremely compressed KV caches.  
**Claimed method:** FWHT rotation, Viterbi trellis encoding, trained codebooks and context-adaptive norm scaling.  
**Actual method:** Rotates 128-value vectors, uses Viterbi to select a trellis path, then stores the path as a packed bitstream plus an FP16 norm.  
**Difference from formal TurboQuant:** Replaces the formal QJL correction stage with trellis coding and adds custom scaling, codebooks and CUDA kernels.

**Weight quantisation:** No established TCQ weight quantisation; standard GGUF weight formats are inherited from llama.cpp  
**KV-cache quantisation:** Yes  
**Key-cache types:** turbo3_tcq, turbo2_tcq, turbo4, turbo3, turbo2, turbo8  
**Value-cache types:** Same; recommended asymmetric option is turbo3_tcq K with turbo2_tcq V  
**Bits per value:** turbo3_tcq = 3.25; turbo2_tcq = 2.25  
**Block structure:** 128 values per TCQ block  
**Metadata overhead:** FP16 norm, one padding byte and small trellis-prefix overhead  
**Actual memory packing:** Yes; compressed trellis paths are stored as packed byte streams.

**CPU support:** Normal llama.cpp only; TCQ CPU functions are incomplete placeholders  
**CUDA support:** Yes; main and best-tested implementation  
**ROCm support:** Claimed and experimentally tested on limited AMD hardware  
**SYCL support:** No TCQ implementation  
**OpenVINO support:** No TCQ implementation  
**NPU support:** No

**Windows build:** Supported for NVIDIA CUDA  
**Required tools:** Git, CMake, Visual Studio 2022 C++ Build Tools and NVIDIA CUDA Toolkit  
**Build command:**

cmake -B build -DGGML_CUDA=ON -DGGML_NATIVE=ON -DGGML_CUDA_FA=ON -DGGML_CUDA_FA_ALL_QUANTS=ON  
cmake --build build --config Release -j

**Run command:**

llama-server.exe -m granite.gguf -ngl 99 -fa \`  
-ctk turbo3_tcq -ctv turbo2_tcq

**Granite compatibility:** Unknown; must be tested  
**Tested Granite model:** None documented  
**Supported head dimensions:** Multiples of 128 natively; other sizes use automatic zero-padding  
**Flash Attention:** Yes on CUDA  
**Maximum tested context:** Published testing reaches 128K; some 262K configurations have also been reported.

**Published benchmarks:** Yes, mainly Qwen models on NVIDIA GPUs  
**Quality tests:** KL divergence, perplexity and output checks  
**Memory tests:** Packed bits-per-value and estimated compression ratios  
**Speed tests:** Prompt-processing and generation tokens per second  
**Known bugs:** Prompt-processing crashes, degraded output on some GPUs, speculative-decoding conflicts and multi-GPU errors.

**Files containing main implementation:**  
ggml/src/ggml-common.h  
ggml/src/ggml-cuda/turbo-quant-cuda.cuh  
ggml/src/ggml-cuda/set-rows.cu  
ggml/src/ggml-cuda/fattn-mma-f16.cuh  
ggml/src/ggml-turbo-quant.c

**Ease of integration:** Medium through llama-server, but only practical for NVIDIA or experimental AMD deployment  
**Maintenance risk:** Very high; highly experimental, rapidly changing and hardware-specific

**How we could use it:** Only as an optional CUDA experiment to compare TCQ against ordinary TurboQuant on the Lenovo RTX 4060. It cannot provide the project’s final Intel CPU, GPU or NPU implementation.

**Final recommendation:** Do not include it in the main implementation. Keep it only as a research reference and move to a repository with CPU, SYCL or OpenVINO support.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-REPO-SPIRITBUUN`
- `SRC-LLAMACPP-GITHUB`
- `SRC-PAPER-TURBOQUANT-2025`
