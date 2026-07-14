# TiredOfEverything/llama-cpp-turboquant

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-TIREDOFEVERYTHING](../00-sources/github-repositories.md#src-repo-tiredofeverything), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/TiredOfEverything/llama-cpp-turboquant> |
| Branch | master |
| Commit | `71ecbd7e82a4c7fd39b9d079a6fb40b9a6f5d5c1` |
| Last relevant update in supplied research | 29 March 2026 |
| Project position | **High-priority CUDA research candidate** |

## What it does

Optimised low-bit cache formats, custom CUDA cache/Flash-Attention paths and layer-adaptive precision. [SRC-REPO-TIREDOFEVERYTHING]

## Difference from formal TurboQuant

PolarQuant-style practical approximation; no complete formal QJL in the recorded active route.

## Storage

Real packed turbo2/turbo3/turbo4 structures.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Incomplete custom route |
| CUDA | Main and highly optimised route |
| ROCm/HIP | Not main |
| Vulkan | No custom low-bit kernels |
| Intel SYCL | No custom route |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Not tested in supplied evidence

## Project recommendation

Use for RTX/CUDA benchmarking and implementation study, not as the main Intel route.

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

- [SRC-REPO-TIREDOFEVERYTHING](../00-sources/github-repositories.md#src-repo-tiredofeverything) — TiredOfEverything/llama-cpp-turboquant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: TiredOfEverything repo final analysis

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/TiredOfEverything llama-cpp-turboquant repo/TiredOfEverything repo final analysis.docx`

Repository: llama-cpp-turboquant

URL: <https://github.com/TiredOfEverything/llama-cpp-turboquant>

Owner: TiredOfEverything

Branch: master

Commit: 71ecbd7e82a4c7fd39b9d079a6fb40b9a6f5d5c1

Last relevant update: 29 March 2026. The latest commit fixed Windows compilation with Microsoft Visual C++ and Clang. Most of the CUDA implementation and benchmark work was added during 26–28 March 2026.

Licence: MIT.

Purpose: Provide highly optimised TurboQuant-style KV-cache compression inside llama.cpp, primarily for NVIDIA CUDA hardware and long-context GGUF inference. It adds custom cache-writing, cache-reading, Flash Attention and tensor-core prefill paths. It also inherits a Metal implementation from TheTom’s original fork.

Claimed method: The README claims full TurboQuant-style compression using Fast Walsh–Hadamard rotation, Lloyd–Max quantisation, norm correction and optional QJL sign correction for Turbo4. It also claims custom Flash Attention and tensor-core prefill support.

Actual method: The current source divides KV vectors into 128-value transform groups, calculates their L2 norm, normalises them, applies fixed sign changes and a 128-value Walsh–Hadamard Transform, and maps the rotated coordinates to low-bit Lloyd–Max centroids. It then physically packs the indices and stores a corrected FP16 norm so that the reconstructed vector retains approximately the same length as the original vector.

Difference from formal TurboQuant: The active formats do not currently implement the complete QJL stage. Turbo2 uses two-bit PolarQuant-style indices, Turbo3 uses a normal three-bit codebook, and the current Turbo4 format uses a direct four-bit codebook. Turbo3’s field named signs stores the upper bit of the ordinary three-bit centroid index; it is not a QJL residual sign. Current CUDA source explicitly states that QJL signs were removed from Turbo4. The repository also adds practical engineering features such as norm correction, optional InnerQ channel equalisation and layer-adaptive q8_0 fallbacks that are not the basic formal paper algorithm.

Weight quantisation: Mainly inherited llama.cpp GGUF weight formats. The repository does not add a separate TurboQuant model-weight format. Its custom Turbo2, Turbo3 and Turbo4 formats are intended for the runtime KV cache.

KV-cache quantisation: Yes; this is the repository’s main purpose.

Key-cache types: turbo2, turbo3, turbo4, q8_0 and the other standard llama.cpp cache types.

Value-cache types: turbo2, turbo3, turbo4, q8_0 and the other standard llama.cpp cache types. The CUDA Flash Attention implementation contains direct support for compressed values as well as keys.

Bits per value:

- turbo2 uses 2.5 effective bits per value.

- turbo3 uses 3.5 effective bits per value.

- turbo4 uses 4.125 effective bits per value.

The README’s references to 3.25-bit Turbo3 and 4.25-bit Turbo4 are outdated relative to the current block structures.

Block structure: Turbo2 and Turbo3 use 32-value physical storage blocks, but four consecutive blocks form one 128-value rotation and normalisation group. Turbo4 uses one 128-value block directly.

Metadata overhead:

- Turbo2: eight bytes of two-bit indices and one two-byte FP16 norm per 32-value block.

- Turbo3: eight bytes containing the lower two index bits, four bytes containing the upper index bit and one two-byte FP16 norm per 32-value block.

- Turbo4: 64 bytes of four-bit indices and one two-byte FP16 norm per 128-value block.

For Turbo2 and Turbo3, the CUDA kernel calculates one corrected norm for the complete 128-value group and repeats it across the associated 32-value blocks.

Actual memory packing: Yes. The KV cache is physically stored using the packed low-bit structures rather than retaining FP16 values alongside them.

CPU support: Standard llama.cpp CPU inference is supported, but the custom TurboQuant CPU route is incomplete. Turbo2 and Turbo3 CPU quantisers are simplified stubs, and the repository states that TurboQuant cache types have no complete CPU vector-dot kernel. CPU-bound layers therefore fall back automatically to q8_0. Turbo4 has a more complete CPU reference implementation, but it is not a fully optimised CPU route.

CUDA support: Yes; this is the main and best-supported route. CUDA includes:

- Turbo2, Turbo3 and Turbo4 cache-writing kernels;

- compressed cache-reading and dequantisation;

- direct compressed key-query dot products;

- compressed value dequantisation inside Flash Attention;

- mixed cache-type combinations;

- a Turbo3 tensor-core prefill path;

- layer-adaptive q8_0 precision.

ROCm support: Potentially partial through llama.cpp’s shared CUDA/HIP code, but the repository’s custom TurboQuant work is documented and benchmarked primarily on NVIDIA CUDA. There is no equivalent detailed ROCm validation in this fork, so AMD support should be treated as experimental.

Metal support: Yes, inherited from TheTom’s original implementation. The original fork already contained Metal shaders, cache formats and graph-level rotation support before this repository added the CUDA implementation. However, the current repository’s main benchmark and optimisation effort is CUDA rather than Metal.

Vulkan support: General llama.cpp Vulkan support may be inherited, but no custom Turbo2, Turbo3 or Turbo4 Vulkan kernels are provided.

SYCL support: General llama.cpp SYCL support may be inherited, but no TurboQuant-specific Intel SYCL implementation is provided.

OpenVINO support: No custom TurboQuant/OpenVINO integration.

NPU support: No Intel NPU TurboQuant route.

Supported and unsupported routes:

Normal GGUF + llama.cpp CPU  
→ Supported

TurboQuant + CPU-only inference  
→ Incomplete; CPU-bound layers normally fall back to q8_0

Normal GGUF + NVIDIA CUDA  
→ Supported

Turbo2 + NVIDIA CUDA  
→ Supported, but most aggressive and least validated format

Turbo3 + NVIDIA CUDA  
→ Supported; main recommended format

Turbo4 + NVIDIA CUDA  
→ Supported, but the current format is pure four-bit quantisation without QJL

TurboQuant K and V + CUDA Flash Attention  
→ Supported

Turbo3 + CUDA tensor-core prefill  
→ Supported

Turbo4 + CUDA tensor-core prefill  
→ Not used in the older QJL-based version because the correction signal was lost through FP16; current Turbo4 has since changed, so this should be retested

Normal GGUF + AMD ROCm  
→ Potentially supported through inherited llama.cpp

TurboQuant + AMD ROCm  
→ Not clearly validated in this repository

Normal GGUF + Apple Metal  
→ Supported

TurboQuant + Apple Metal  
→ Inherited implementation exists

Normal GGUF + Vulkan  
→ Potentially supported

TurboQuant + Vulkan  
→ Not supported through custom kernels

Normal GGUF + Intel SYCL  
→ Potentially supported through inherited llama.cpp

TurboQuant + Intel SYCL  
→ Not supported

TurboQuant + OpenVINO  
→ Not supported

TurboQuant + Intel NPU  
→ Not supported

Windows build: Yes. The latest commit specifically corrected Windows compilation issues and was tested with both Microsoft Visual C++ and Clang. A CUDA-capable NVIDIA GPU is required for the main accelerated route.

Required tools: Git, CMake, Visual Studio C++ Build Tools, the NVIDIA CUDA Toolkit and current NVIDIA graphics drivers. Ninja may also be used but is not essential.

Build command:

git clone <https://github.com/TiredOfEverything/llama-cpp-turboquant.git>  
cd llama-cpp-turboquant

cmake -S . -B build \`  
-DGGML_CUDA=ON \`  
-DGGML_NATIVE=ON \`  
-DGGML_CUDA_FA=ON \`  
-DGGML_CUDA_FA_ALL_QUANTS=ON \`  
-DCMAKE_BUILD_TYPE=Release

cmake --build build --config Release -j

The repository recommends enabling the CUDA Flash Attention and all-quant kernel options.

Run command:

\$env:TURBO_LAYER_ADAPTIVE="1"

.\build\bin\Release\llama-server.exe \`  
-m granite.gguf \`  
-ngl 99 \`  
-c 8192 \`  
-fa on \`  
-ctk turbo3 \`  
-ctv turbo3 \`  
--host 127.0.0.1 \`  
--port 8080

For contexts requiring more compression:

\$env:TURBO_LAYER_ADAPTIVE="5"

For maximum Turbo3 compression:

Remove-Item Env:TURBO_LAYER_ADAPTIVE -ErrorAction SilentlyContinue

The documented modes use q8_0 for selected first or final layers while keeping the other layers in Turbo3.

Granite compatibility: General IBM Granite GGUF support is inherited from llama.cpp, but Granite with Turbo2, Turbo3 or Turbo4 through this CUDA implementation has not been confirmed. Granite Hybrid models may also contain recurrent or state-space memory that is not compressed by KV-cache quantisation.

Tested Granite model: None documented.

Supported head dimensions: The native rotation group is 128 values. Head dimensions of 128 work directly, while dimensions such as 256 can be processed as multiple 128-value groups. Dimensions below 128 or dimensions that are not compatible with 128-value grouping require separate validation and may need padding or fallback behaviour. A previously discovered bug affecting dimensions larger than one 128-value block was fixed during Qwen testing.

Flash Attention: Yes. The repository includes custom CUDA Flash Attention vector kernels for Turbo2, Turbo3 and Turbo4 keys and values. Turbo3 also has a bulk-dequantisation and tensor-core MMA path for faster prompt processing.

Maximum tested context: 131,072 tokens using Turbo3 with Qwen3.5-27B Q6_K on an NVIDIA RTX 3090 with 24 GB VRAM. The repository reports that q8_0 ran out of memory at approximately 65K in the same configuration.

Published benchmarks: Yes. Most published measurements use Qwen3.5-27B Q6_K on an RTX 3090. Additional experiments include Qwen3.5-35B-A3B, Gemma models and different layer-adaptive configurations.

Quality tests: WikiText perplexity, long-context perplexity, coherent-generation checks and model-specific experiments. The repository also reports error ranges showing that some small apparent improvements over q8_0 may be benchmark noise rather than definite quality gains.

Memory tests: Yes. Theoretical KV-cache compression is approximately:

- Turbo2: 6.4× compared with FP16.

- Turbo3: 4.57× compared with FP16.

- Turbo4: 3.88× compared with FP16.

Layer-adaptive modes reduce these ratios because selected layers remain in q8_0.

Speed tests: Yes. On the main RTX 3090 benchmark:

- q8_0 prefill: approximately 1,133 tokens per second;

- layer-adaptive Turbo3 prefill: approximately 1,128 tokens per second;

- q8_0 decode: approximately 31.04 tokens per second;

- layer-adaptive Turbo3 decode: approximately 30.25 tokens per second.

This means the recommended Turbo3 mode retained around 99.6% of q8_0 prefill performance and 97.5% of its decode performance on that particular GPU and model.

Known bugs and limitations:

- The README and some implementation documents describe an older QJL-based Turbo4 format, while current source uses direct four-bit PolarQuant-style indices without QJL.

- README bit-width figures do not match the current block structures.

- Turbo2 and Turbo3 CPU quantisation paths are incomplete stubs.

- There is no complete CPU vector-dot path for TurboQuant cache types.

- Multiple serious CUDA bugs were encountered during development, including incorrect query strides, incorrect register types, missing cache operations, missing compiled kernels and incorrect transform handling.

- A bug affecting Turbo4 when the head dimension exceeded 128 was identified and corrected.

- A Gemma sliding-window-attention value-cache bug was found and fixed, showing that model architecture can affect compatibility.

- Performance can vary significantly by GPU. One community result showed a larger Turbo3 slowdown than the RTX 3090 results.

- Granite has not been tested.

- There is no custom Vulkan, SYCL, OpenVINO or Intel NPU path.

Files containing the main implementation:

README.md  
TURBOQUANT_CUDA_IMPLEMENTATION.md  
benchmark-results.md  
experiments.md

ggml/src/ggml-common.h  
ggml/src/ggml-turbo-quant.c  
ggml/src/ggml-quants.c  
ggml/src/ggml-quants.h

ggml/src/ggml-cuda/turbo-quant-cuda.cuh  
ggml/src/ggml-cuda/turbo-wht.cu  
ggml/src/ggml-cuda/fattn-common.cuh  
ggml/src/ggml-cuda/fattn-vec.cuh  
ggml/src/ggml-cuda/fattn.cu  
ggml/src/ggml-cuda/set-rows.cu  
ggml/src/ggml-cuda/getrows.cu  
ggml/src/ggml-cuda/ggml-cuda.cu

ggml/src/ggml-metal/ggml-metal.metal  
ggml/src/ggml-metal/turbo-wht.h  
ggml/src/ggml-metal/turbo-matrices.h

src/llama-kv-cache.cpp  
src/llama-kv-cache.h  
src/llama-graph.cpp  
src/llama-context.cpp

common/arg.cpp

Ease of integration: Medium for NVIDIA testing. The easiest method is to compile llama-server, run it as a separate local process and communicate with it through its OpenAI-compatible HTTP interface. Integration is difficult for the Intel-focused application because none of the custom TurboQuant formats has a Vulkan, SYCL or OpenVINO implementation.

Maintenance risk: High. The repository is experimental, contains many hardware-specific CUDA modifications, has rapidly changed format definitions and includes documentation that no longer fully matches the active source.

How we could use it: Use it as the main CUDA research candidate on the Lenovo RTX 4060. Test a selected Granite GGUF using:

F16 keys + F16 values  
q8_0 keys + q8_0 values  
Turbo4 keys + Turbo4 values  
Turbo3 keys + Turbo3 values  
Turbo2 keys + Turbo2 values  
Layer-adaptive Turbo3

Compare:

Successful model loading  
Output quality  
Perplexity or task accuracy  
KV-cache memory  
Total GPU memory  
Prompt-processing speed  
Generation speed  
Maximum stable context  
Repeated-request stability

It can also provide useful reference code for later porting 128-value FWHT rotation, norm correction and compressed Flash Attention to Intel hardware.

Final recommendation: Use as a high-priority CUDA research and benchmarking implementation, particularly for the RTX 4060 system. It is one of the more advanced practical repositories for low-bit PolarQuant-style KV-cache compression, custom Flash Attention, layer-adaptive precision and long-context CUDA inference. However, it does not currently provide formal QJL correction or a custom Intel execution route. It should therefore remain a secondary NVIDIA backend and research reference rather than the main Vulkan, SYCL, OpenVINO or Intel NPU implementation.

## Original research: TiredOfEverything repo summary

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/TiredOfEverything llama-cpp-turboquant repo/TiredOfEverything repo summary.docx`

#### **Repository overview**

**Repository:** llama-cpp-turboquant  
**URL:** <https://github.com/TiredOfEverything/llama-cpp-turboquant>  
**Owner:** TiredOfEverything  
**Branch:** master  
**Current inspected commit:** 71ecbd7e82a4c7fd39b9d079a6fb40b9a6f5d5c1  
**Last relevant update:** 29 March 2026  
**Licence:** MIT

The latest commit specifically corrected Windows compilation problems and was tested using both Clang and Microsoft Visual C++.

#### **1. What this repository is**

This is a fork of TheTom’s TurboQuant-enabled llama.cpp repository that concentrates on adding highly optimised **NVIDIA CUDA execution**.

It adds:

- low-bit TurboQuant-style KV-cache formats;

- custom CUDA cache-writing and cache-reading kernels;

- CUDA Flash Attention support;

- tensor-core prefill optimisation;

- layer-adaptive cache precision;

- Windows build corrections.

The main objective is to run long-context GGUF models on NVIDIA GPUs while significantly reducing KV-cache memory.

The repository is particularly relevant to the project as a potential **RTX 4060 research and benchmarking backend**, but it is not a direct Intel implementation.

### **2. Available cache formats**

The current source defines three TurboQuant-style formats:

| **Format** | **Physical storage**    | **Effective precision** | **Compression vs FP16** |
|------------|-------------------------|-------------------------|-------------------------|
| turbo2     | 10 bytes per 32 values  | 2.5 bits/value          | 6.4×                    |
| turbo3     | 14 bytes per 32 values  | 3.5 bits/value          | 4.57×                   |
| turbo4     | 66 bytes per 128 values | 4.125 bits/value        | approximately 3.88×     |

Some README figures describe these as 3.25-bit and 4.25-bit formats. Those descriptions are outdated. The current block structures show that the active formats use 3.5 and 4.125 effective bits per value.

#### **Turbo2**

Turbo2 uses four Lloyd–Max centroids:

128-value rotation group  
↓  
Normalise vector  
↓  
Apply fixed-sign FWHT rotation  
↓  
Map values to four centroids  
↓  
Store two-bit indices  
↓  
Store corrected norm

It provides the greatest compression but is expected to have the highest quality risk.

#### **Turbo3**

Turbo3 uses eight Lloyd–Max centroids. Each three-bit index is split into:

Lower two bits  
→ qs array

Upper one bit  
→ signs array

Despite the field being called signs, it is not QJL residual information. It is simply the third bit of the ordinary eight-level codebook index.

#### **Turbo4**

The current Turbo4 implementation uses:

16 Lloyd–Max centroids  
+  
four-bit packed indices  
+  
one corrected FP16 norm

The current source explicitly states that QJL signs were removed and that Turbo4 now uses pure four-bit PolarQuant-style quantisation.

### **3. How the actual algorithm works**

The principal CUDA compression process is:

Take a 128-value KV-cache group  
↓  
Optionally equalise individual channels using InnerQ  
↓  
Calculate the original L2 norm  
↓  
Normalise the vector  
↓  
Apply fixed positive/negative sign changes  
↓  
Apply a normalised 128-point Walsh–Hadamard Transform  
↓  
Map each value to a low-bit Lloyd–Max centroid  
↓  
Calculate the reconstructed centroid vector’s norm  
↓  
Correct the stored norm  
↓  
Pack the indices into the KV cache

The FWHT spreads outliers and structure more evenly across the vector, making the fixed codebooks more suitable. The transform uses fixed sign patterns and a 128-point normalised Hadamard transform.

#### **Norm correction**

One of the repository’s most important additions is norm correction:

corrected norm  
=  
original group norm ÷ reconstructed centroid-vector norm

This means that when the quantised vector is reconstructed, its overall length remains close to the original vector’s length.

The CUDA Turbo3 kernel calculates one corrected norm across the full 128-value group and writes it to the four associated 32-value blocks.

Turbo4 applies the same broad principle to its single 128-value block.

This is not QJL. It is an additional scaling correction intended to preserve attention-score magnitude.

### **4. Does it implement formal TurboQuant?**

It implements a substantial version of the **first stage**, but not the complete two-stage algorithm.

PolarQuant-style random preconditioning  
→ Implemented

128-value FWHT rotation  
→ Implemented

Low-bit Lloyd–Max quantisation  
→ Implemented

Physical KV-cache packing  
→ Implemented

Norm correction  
→ Added as a practical enhancement

Formal QJL residual correction  
→ Not used by the current active formats

The repository still contains some old CPU code and documentation referring to QJL matrices and residual correction.

However, the authoritative current CUDA structures and kernels show:

- Turbo2: two-bit PolarQuant, no QJL;

- Turbo3: ordinary three-bit codebook, no QJL;

- Turbo4: ordinary four-bit codebook, no QJL.

Therefore, the repository should be described as:

**A practical 128-dimensional FWHT and PolarQuant-style KV-cache implementation with two-, three- and four-bit codebooks, norm correction and CUDA attention integration, but without the current use of formal QJL residual correction.**

It is closer to PolarQuant than the simpler 32-value-block repositories, but it is not the full formal TurboQuant pipeline.

### **5. CUDA implementation**

CUDA is the strongest part of this repository.

The custom CUDA path includes:

- SET_ROWS quantisation when new keys and values enter the cache;

- GET_ROWS dequantisation;

- direct compressed-key dot products;

- compressed-value dequantisation inside Flash Attention;

- symmetric and asymmetric cache combinations;

- prefill tensor-core acceleration;

- query rotation and output transformation;

- layer-adaptive cache precision.

The Flash Attention kernel can directly calculate attention against Turbo2, Turbo3 and Turbo4 key caches and can dequantise compressed value caches during attention.

CUDA template instances are included for several mixed configurations, such as:

turbo3 keys + turbo3 values  
turbo3 keys + turbo4 values  
turbo3 keys + q8_0 values  
q8_0 keys + turbo3 values

#### **Prefill optimisation**

The repository can temporarily dequantise Turbo3 KV data into FP16 buffers during prompt processing and then use NVIDIA tensor-core MMA kernels.

The reported result for Turbo3 was:

Old vector-kernel prefill  
→ approximately 631 tokens/s

Dequantise + tensor-core MMA  
→ approximately 1,121 tokens/s

The decode path continues using direct low-bit cache access.

The equivalent optimisation was not retained for the older QJL-based Turbo4 experiment because the small correction signal was lost during the FP16 round trip. Current source has since redesigned Turbo4 without QJL, which illustrates that parts of the benchmark documentation refer to an older format revision.

### **6. Layer-adaptive compression**

The repository can use different cache formats in different model layers through:

TURBO_LAYER_ADAPTIVE

Examples include:

Mode 1  
First four and last four layers → q8_0  
Middle layers → TurboQuant

Mode 5  
First two and last two layers → q8_0  
Remaining layers → TurboQuant

It also contains experimental modes that promote only keys or only values.

This allows a balance between memory and quality:

More q8_0 layers  
→ greater quality protection  
→ less compression

More TurboQuant layers  
→ greater compression  
→ potentially greater quality risk

If a model is partially offloaded to the CPU, TurboQuant cache types automatically fall back to q8_0 on CPU-bound layers because the necessary CPU vector-dot path is missing.

### **7. CPU and other backends**

#### **CPU**

Normal llama.cpp CPU inference works, but full TurboQuant CPU execution is incomplete.

The CPU reference code shows:

- Turbo2 quantisation only calculates a norm and writes zero indices;

- Turbo3 quantisation is explicitly described as a simplified stub;

- Turbo4 has a more complete CPU rotation, quantisation and inverse-rotation implementation.

Therefore, this should not be considered a complete CPU TurboQuant implementation.

#### **Metal**

The fork inherited substantial Apple Metal TurboQuant work from TheTom’s implementation. The repository’s own documentation says that Metal shaders, CLI integration and graph scaffolding already existed before this fork added CUDA support.

#### **Intel and other routes**

Custom TurboQuant + NVIDIA CUDA  
→ Supported; main route

Custom TurboQuant + Apple Metal  
→ Inherited implementation

Custom TurboQuant + CPU  
→ Incomplete

Custom TurboQuant + AMD ROCm  
→ Not clearly validated in this fork

Custom TurboQuant + Vulkan  
→ No custom route found

Custom TurboQuant + Intel SYCL  
→ No custom route found

Custom TurboQuant + OpenVINO  
→ Not supported

Custom TurboQuant + Intel NPU  
→ Not supported

The presence of general Vulkan and SYCL code inherited from llama.cpp does not mean that Turbo2, Turbo3 or Turbo4 work through those backends.

### **8. Windows support**

The latest commit fixes Windows compilation problems and was tested with:

- Microsoft Visual C++;

- Clang on Windows.

A suitable NVIDIA CUDA build is:

cmake -B build \`  
-DGGML_CUDA=ON \`  
-DGGML_NATIVE=ON \`  
-DGGML_CUDA_FA=ON \`  
-DGGML_CUDA_FA_ALL_QUANTS=ON \`  
-DCMAKE_BUILD_TYPE=Release

cmake --build build --config Release -j

The documented run command is:

\$env:TURBO_LAYER_ADAPTIVE="1"

.\build\bin\Release\llama-server.exe \`  
-m granite.gguf \`  
-ngl 99 \`  
-c 8192 \`  
-fa on \`  
-ctk turbo3 \`  
-ctv turbo3 \`  
--port 8080

The repository documents the required CUDA Flash Attention build options and cache arguments.

### **9. Published results**

The principal benchmark used:

GPU: NVIDIA RTX 3090, 24 GB  
Model: Qwen3.5-27B Q6_K

Reported results include:

| **Configuration** | **Perplexity**       | **Prefill** | **Decode** | **Compression**    |
|-------------------|----------------------|-------------|------------|--------------------|
| q8_0              | 5.8375               | 1,133 t/s   | 31.04 t/s  | Baseline           |
| LA-1 Turbo3       | 5.7690               | 1,128 t/s   | 30.25 t/s  | Approximately 3.5× |
| Uniform Turbo3    | 5.8501 in later test | 1,125 t/s   | 30.04 t/s  | Approximately 4.9× |

The repository also reports that Turbo3 enabled 128K context on the 24 GB RTX 3090, whereas q8_0 ran out of memory at approximately 65K.

However, the repository’s longer-context tests acknowledge that the measured perplexity differences are sometimes smaller than the benchmark error range. Therefore, claims that Turbo3 is universally “better than q8_0” should not be generalised from one model and dataset.

### **10. Known risks**

The implementation history includes several serious bugs that were found and corrected:

- missing CUDA cache-write operations;

- missing compiled Flash Attention kernels;

- null function pointers during prefill;

- incorrect query strides causing gibberish;

- FP16/FP32 register-type mismatches;

- incorrect query and key rotation;

- incorrect block handling for head dimensions above 128;

- model-specific attention-graph errors.

This demonstrates active technical work, but it also means that the implementation is highly experimental.

Another major risk is documentation inconsistency:

README and old benchmark documents  
→ describe QJL-based Turbo4

Current active source  
→ pure four-bit Turbo4 without QJL

The current source should be treated as authoritative.

### **11. Granite relevance**

The repository has no documented IBM Granite benchmark using Turbo2, Turbo3 or Turbo4.

Normal Granite GGUF loading may work through inherited llama.cpp model support, but the custom cache path must still be tested for:

- Granite’s attention-head dimension;

- Flash Attention compatibility;

- ordinary Granite versus Granite Hybrid;

- model output quality;

- Windows CUDA stability;

- maximum context;

- repeated server requests.

The active transformation is based around 128-value groups. A selected Granite model whose K/V head dimensions are not compatible with those groups may require padding, fallback logic or further code changes.

For Granite Hybrid, TurboQuant would compress the attention KV cache but would not automatically compress the recurrent or state-space memory. Total process-memory savings may therefore be lower than the headline KV-cache compression ratio.

### **12. Relevance to the application**

#### **Direct implementation relevance**

Main Intel backend  
→ No

Vulkan backend candidate  
→ No

Intel SYCL candidate  
→ No

OpenVINO or NPU candidate  
→ No

#### **Research relevance**

RTX 4060 CUDA benchmark  
→ High

Reference for proper 128-value PolarQuant rotation  
→ High

Reference for norm correction  
→ High

Reference for custom Flash Attention  
→ High

Reference for layer-adaptive compression  
→ High

Reference for future Intel port  
→ Potentially useful

This repository is more relevant than the simpler Unixsysdev implementation for NVIDIA testing because it supports:

- both keys and values;

- Flash Attention;

- several bit widths;

- layer-adaptive precision;

- long-context testing;

- CUDA tensor-core prefill.

However, it is not a replacement for the animehacker SYCL repository because it provides no Intel TurboQuant path.

### **Final assessment**

Formal PolarQuant fidelity  
→ Relatively high

Formal QJL implementation  
→ No in the current active formats

Available formats  
→ Turbo2, Turbo3 and Turbo4

Primary hardware  
→ NVIDIA CUDA

Windows relevance  
→ Good for NVIDIA systems

Intel relevance  
→ Low for direct implementation

Granite validation  
→ None documented

Research value  
→ High

Production readiness  
→ Low to medium

Maintenance risk  
→ High

**Final recommendation:** Retain this repository as a high-priority CUDA research and Granite benchmarking candidate for the Lenovo RTX 4060. It is one of the more developed implementations for studying 128-value FWHT rotation, norm correction, custom Flash Attention and layer-adaptive KV-cache compression. It should not be selected as the main Intel backend because it has no custom Vulkan, SYCL, OpenVINO or NPU route. Granite compatibility and all quality claims must be independently tested.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-REPO-TIREDOFEVERYTHING`
- `SRC-LLAMACPP-GITHUB`
- `SRC-PAPER-TURBOQUANT-2025`
