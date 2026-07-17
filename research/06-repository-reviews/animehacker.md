# animehacker/llama-turboquant

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

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

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: Animehacker repo final analysis

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/animehacker repo/Animehacker repo final analysis.docx`

Repository: llama-turboquant  
URL: <https://github.com/animehacker/llama-turboquant>  
Owner: animehacker  
Branch: main  
Commit: 5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc  
Last relevant update: 4 May 2026, when the Intel SYCL TQ3_0 port was merged  
Licence: MIT

Purpose: Implement low-bit TQ3_0 KV-cache compression inside llama.cpp, with CPU, CUDA, HIP/ROCm and a custom Intel SYCL execution path.

Claimed method: Walsh–Hadamard rotation followed by 3-bit Lloyd–Max codebook quantisation for keys and values.

Actual method: Divides KV-cache data into groups of 32 values, applies fixed sign changes and a 32-value Walsh–Hadamard Transform, maps each transformed value to one of eight codebook values and physically packs the result with one FP16 scale.

Difference from formal TurboQuant: It implements only Stage 1, the PolarQuant-style compression stage. It does not implement QJL residual correction. The third stored bit is the upper bit of the normal codebook index, not a QJL residual sign. It also uses separate 32-value rotations rather than rotating the complete attention-head vector.

Weight quantisation: Mainly inherited llama.cpp GGUF weight formats. The repository does not add a separate TurboQuant weight-compression format.

KV-cache quantisation: Yes; main feature.

Key-cache types: TQ3_0, plus inherited formats such as F16, q8_0 and q4_0.

Value-cache types: TQ3_0, plus inherited formats such as F16, q8_0 and q4_0.

Bits per value: TQ3_0 uses 3.5 effective bits per value after including its FP16 scale.

Block structure: 32 values per block.

Metadata overhead: Eight bytes for the lower two index bits, four bytes for the upper index bits and two bytes for one FP16 scale.

Actual memory packing: Yes. Each 32-value block occupies 14 bytes rather than 64 bytes for FP16, giving approximately 4.57× compression.

CPU support: Yes. TQ3_0 can be dequantised through the CPU, although it is slower than GPU execution.

CUDA support: Yes. The original implementation includes CUDA TQ3_0 kernels, but some CUDA configurations have experienced crashes or unsupported-operation fallbacks.

ROCm support: Yes, but experimental. Published AMD benchmarks exist, although an open issue reports a TQ3_0 segmentation fault on an RX 9060 XT.

SYCL support: Yes. This is the repository’s most important feature for the project. It includes custom TQ3_0 quantisation, dequantisation and KV-cache write operations for Intel GPUs.

OpenVINO support: Normal llama.cpp may contain OpenVINO-related functionality, but no custom TQ3_0/OpenVINO integration is provided.

NPU support: No custom Intel NPU route.

Supported and unsupported routes:

Normal GGUF + llama.cpp CPU  
→ Supported

TQ3_0 + CPU  
→ Supported but slower

Normal GGUF + NVIDIA CUDA  
→ Supported

TQ3_0 + NVIDIA CUDA  
→ Supported but experimental

Normal GGUF + AMD ROCm  
→ Supported

TQ3_0 + AMD ROCm  
→ Implemented but has reported stability problems

Normal GGUF + Intel SYCL  
→ Supported

TQ3_0 + Intel SYCL  
→ Supported and tested on Intel Battlemage B70

TQ3_0 + OpenVINO  
→ Not supported

TQ3_0 + Intel NPU  
→ Not supported

Windows build: Yes. The inherited llama.cpp SYCL backend supports Windows, and the repository includes a Windows oneAPI/SYCL build script.

Required tools: Git, CMake, Ninja, Visual Studio C++ Build Tools, Intel oneAPI or Intel Deep Learning Essentials, and current Intel GPU drivers.

Build command:

git clone <https://github.com/animehacker/llama-turboquant.git>  
cd llama-turboquant

call "C:\Program Files (x86)\Intel\oneAPI\setvars.bat" intel64 --force

cmake -B build -G Ninja \`  
-DLLAMA_OPENSSL=OFF \`  
-DGGML_SYCL=ON \`  
-DCMAKE_C_COMPILER=cl \`  
-DCMAKE_CXX_COMPILER=icx \`  
-DBUILD_SHARED_LIBS=ON \`  
-DCMAKE_BUILD_TYPE=Release

cmake --build build -j

The repository’s Windows script uses the same oneAPI setup and SYCL build route.

Run command:

set UR_L0_ENABLE_RELAXED_ALLOCATION_LIMITS=1

build\bin\llama-server.exe \`  
-m granite.gguf \`  
-ngl 99 \`  
-c 8192 \`  
-fa on \`  
-ctk tq3_0 \`  
-ctv tq3_0 \`  
--port 8080

Granite compatibility: Normal Granite and Granite Hybrid model code exists in the inherited llama.cpp runtime, but Granite with TQ3_0 through Intel SYCL has not been confirmed.

Tested Granite model: None documented.

Supported head dimensions: No fixed list is documented. TQ3_0 operates on 32-value blocks, so KV-cache row dimensions must be compatible with 32-value packing.

Flash Attention: Yes for the tested Intel SYCL route. The B70 test used Flash Attention with TQ3_0 keys and values. Some older README statements saying Flash Attention is disabled are inconsistent with the newer merged SYCL implementation.

Maximum tested context: The repository claims 72K+ context for its wider TQ3_0 implementation. The Intel SYCL results discuss approximately 32K context for Qwen2.5-32B on a 32 GB Battlemage GPU.

Published benchmarks: Yes, mainly Qwen models on AMD Radeon 8060S and Intel Battlemage B70 hardware.

Quality tests: WikiText-2 perplexity and low-level comparisons against a Python reference implementation.

Memory tests: Yes. TQ3_0 reduced a tested Intel KV cache from 512 MiB with FP16 to 112 MiB.

Speed tests: Yes. On Intel B70, prompt processing was approximately unchanged, while token generation was around 8% slower because TQ3_0 values must be dequantised during attention.

Known bugs: AMD HIP has a reported TQ3_0 segmentation fault. A Windows CUDA user also reported a crash caused by an unsupported GPU copy path; a possible fix was placed on another branch but was not confirmed before the issue was closed as stale. Documentation is also partly outdated, particularly around Flash Attention and build URLs.

Files containing main implementation:  
ggml/src/ggml-common.h  
ggml/src/ggml-quants.c  
ggml/src/ggml-sycl/convert.cpp  
ggml/src/ggml-sycl/cpy.cpp  
ggml/src/ggml-sycl/cpy.hpp  
ggml/src/ggml-sycl/set_rows.cpp  
ggml/src/ggml-sycl/ggml-sycl.cpp  
src/llama-graph.cpp  
src/llama-context.cpp

Ease of integration: Medium. The easiest approach is to build the SYCL version of llama-server and communicate with it as a separate local backend process.

Maintenance risk: Medium to high. It is an experimental llama.cpp fork with limited Intel testing, inconsistent documentation and reported backend-specific stability problems.

How we could use it: Use it as the main Intel SYCL fallback when the preferred Vulkan TurboQuant route is unavailable or unstable. Test Granite with F16, q8_0 and TQ3_0 caches on the target Intel PCs and compare memory, speed, quality and stability.

Final recommendation: Use as a high-priority Intel SYCL implementation candidate and as the secondary backend after Vulkan. It provides a genuine Intel TQ3_0 route with some real hardware validation, but it should not become the default until Granite, and the target integrated Intel GPUs have been tested successfully.

## Original research: animehacker repo review

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/animehacker repo/animehacker repo review.docx`

#### Main conclusion

This repository is **highly relevant to the Intel side of the project**.

Unlike most of the previous TurboQuant forks, it does not merely inherit llama.cpp’s general SYCL backend. It adds custom TQ3_0 quantisation and dequantisation operations directly to the SYCL backend and has been tested on an Intel Battlemage B70 GPU.

It should therefore become one of our main implementation candidates:

Primary Intel-specific candidate  
→ animehacker TQ3_0 through SYCL

Alternative cross-vendor candidate  
→ AtomicBot turbo3 through Vulkan

General Intel fallback  
→ Standard SYCL, CPU or OpenVINO without TurboQuant

However, it is still experimental and has not been tested with IBM Granite.

### 1. What the repository implements

The repository adds one custom KV-cache format:

tq3_0

It can be selected independently for the key and value caches:

--cache-type-k tq3_0  
--cache-type-v tq3_0

The model weights remain in an ordinary GGUF format such as Q4_K_M or Q5_K_M. Only the information stored in the KV cache during inference is compressed.

Granite GGUF model weights  
→ Remain in their existing GGUF format

Keys and values created during inference  
→ Stored using TQ3_0

The repository specifically adds TQ3_0 to the type system, CPU implementation, CUDA/HIP backends, command-line arguments, benchmark tools and, most importantly for us, the Intel SYCL backend.

### 2. How TQ3_0 works

Each group of 32 KV-cache values is processed separately.

32 original values  
↓  
Apply fixed positive and negative sign changes  
↓  
Apply a 32-value Walsh–Hadamard Transform  
↓  
Find the overall scale of the transformed values  
↓  
Approximate each value using one of eight codebook values  
↓  
Store each code using 3 bits  
↓  
Store one FP16 scale for the block

The eight approximation values are Lloyd–Max centroids designed for Gaussian-like data.

The physical block contains:

| **Field**                    | **Storage**  |
|------------------------------|--------------|
| Lower two bits of 32 indices | 8 bytes      |
| Upper bit of 32 indices      | 4 bytes      |
| FP16 scale                   | 2 bytes      |
| **Total**                    | **14 bytes** |

An FP16 block of 32 values normally uses 64 bytes. Therefore:

64 ÷ 14  
≈ 4.57× compression

The actual storage is 3.5 bits per value rather than exactly 3 bits because the scale adds metadata overhead.

### 3. What the rotation accomplishes

KV-cache values may originally contain uneven distributions and large outliers. Directly converting them to three bits can therefore introduce substantial error.

The fixed sign changes and Walsh–Hadamard Transform mix the values together:

Before rotation  
→ Some dimensions may contain unusually large or structured values

After rotation  
→ Information is spread more evenly across the 32 coordinates

This produces a more Gaussian-like distribution, which makes the fixed Lloyd–Max codebook more effective.

The transform is:

- deterministic;

- reversible;

- relatively cheap;

- applied independently to every 32-value block.

The repository corrected an earlier normalisation error by using:

1 / √32

rather than:

1 / 32

### 4. This is not complete formal TurboQuant

This point is important.

Formal TurboQuant contains two stages:

Stage 1  
PolarQuant-style main approximation

Stage 2  
QJL residual correction

This repository implements only the first stage.

Its format uses all three index bits to select one of eight Lloyd–Max values:

2 lower index bits  
+  
1 upper index bit  
=  
3-bit codebook index

The field called qr does **not** store QJL residual signs. It stores the upper bit of the normal 3-bit index.

Therefore:

PolarQuant-style 3-bit compression  
→ Implemented

QJL residual correction  
→ Not implemented

The repository’s corrected README and commit history explicitly confirm this.

It also differs from the paper by using separate 32-value rotations rather than one rotation across the complete attention-head dimension. This simplifies integration into llama.cpp, but it is an engineering approximation rather than an exact reproduction of the paper.

### 5. How it works through Intel SYCL

The SYCL port adds the operations required to store and retrieve TQ3_0 blocks on an Intel GPU.

#### Writing to the cache

When the model generates new keys and values:

New F32 KV values  
↓  
SYCL SET_ROWS operation  
↓  
WHT and 3-bit quantisation on the Intel GPU  
↓  
Packed TQ3_0 block stored in GPU/shared memory

SET_ROWS is essential because llama.cpp uses it when inserting newly generated values into the KV cache. An earlier version of the port could convert TQ3_0 but aborted during actual inference because this cache-write operation was missing. It was subsequently added and tested.

#### Reading from the cache

During attention:

Packed TQ3_0 block  
↓  
SYCL dequantisation kernel  
↓  
Codebook lookup  
↓  
Inverse Walsh–Hadamard Transform  
↓  
Reconstructed F16 or F32 values  
↓  
Attention calculation

The SYCL port includes:

- TQ3_0 to F16 conversion;

- TQ3_0 to F32 conversion;

- F32 to TQ3_0 conversion;

- SET_ROWS cache writes;

- backend capability declarations.

The dequantisation kernel assigns one 32-thread work group to each 32-value block and uses local GPU memory during the inverse WHT.

### 6. It saves memory but still dequantises for attention

The Intel implementation does not yet have a fully fused attention kernel that directly calculates attention from packed TQ3_0 values.

Instead:

KV cache remains compressed between inference steps  
↓  
Required blocks are dequantised when attention reads them  
↓  
Attention operates on reconstructed values

This still creates substantial persistent memory savings, but the dequantisation adds computation during every generated token.

The Intel benchmark found:

- prompt-processing performance was approximately unchanged;

- token-generation performance was approximately 8% slower;

- KV-cache memory was 4.57× smaller.

A future fused TQ3_0 attention or vector-dot kernel could reduce that overhead, but it is not currently part of the SYCL implementation.

### 7. Intel validation already completed

The merged SYCL pull request tested:

- **GPU:** Intel Battlemage B70;

- **runtime:** Intel Level Zero;

- **oneAPI:** 2025.3;

- **model:** Qwen2.5-32B-Instruct-Q5_K_M;

- **cache:** TQ3_0 keys and TQ3_0 values;

- **Flash Attention:** enabled.

The reported results were:

| **KV format** | **KV memory** | **Perplexity** | **Prompt speed** | **Generation speed** |
|---------------|---------------|----------------|------------------|----------------------|
| F16           | 512 MiB       | 3.9127         | 240 t/s          | 10.58 t/s            |
| Q8_0          | 272 MiB       | 3.9150         | 242 t/s          | 10.47 t/s            |
| TQ3_0         | **112 MiB**   | 4.0406         | 241 t/s          | 9.72 t/s             |

This represents:

- approximately 4.57× less KV memory than FP16;

- approximately 3.3% relative perplexity degradation;

- essentially unchanged prompt processing;

- approximately 8% slower token generation.

The low-level SYCL results were also compared against a Python reference on six input distributions. The output matched the reference at approximately floating-point precision, demonstrating that the SYCL port performs the intended algorithm correctly.

### 8. Flash Attention requires clarification

The repository documentation is inconsistent.

The older README says that Flash Attention is automatically disabled with TQ3_0 keys.

However, the newer merged SYCL implementation:

- explicitly requires Flash Attention;

- was tested with Flash Attention enabled;

- successfully ran TQ3_0 for both keys and values;

- allows the current runtime to determine whether Flash Attention is supported by the selected backend.

Therefore, for the current SYCL branch:

TQ3_0 + Intel SYCL + Flash Attention  
→ Implemented and tested on B70

TQ3_0 without Flash Attention  
→ Not the preferred Intel route

Flash Attention on other Intel GPUs  
→ Must be validated per device

The commit history and current code should be treated as more reliable than the outdated section of the README.

### 9. Granite compatibility

The fork contains llama.cpp model implementations for:

- standard Granite;

- Granite Hybrid;

- Granite Vision-related support.

This means the underlying runtime can recognise Granite architectures.

However:

Normal Granite loading  
→ Supported by the inherited llama.cpp model code

Granite + TQ3_0  
→ Not tested

Granite + TQ3_0 + Intel SYCL  
→ Not tested

Granite 4 hybrid-state compression  
→ Not confirmed

There are two separate questions:

1.  Can the selected Granite GGUF load through this version of llama.cpp?

2.  Can its attention KV cache use TQ3_0 correctly through SYCL?

For a hybrid Granite model, TQ3_0 would compress attention keys and values. It would not automatically compress unrelated recurrent or state-space buffers. Therefore, the real total memory saving may be lower than 4.57× if a large part of the model’s runtime memory is not traditional KV cache.

The exact Granite model must also have cache rows compatible with the 32-value block structure. This needs to be checked during model inspection.

### 10. Windows implementation route

The repository includes a Windows SYCL build script that:

- loads the Intel oneAPI environment;

- enables the SYCL backend;

- uses Ninja;

- builds the llama.cpp tools and server.

A corrected Windows build sequence for this actual repository would be:

git clone <https://github.com/animehacker/llama-turboquant.git>  
cd llama-turboquant

call "C:\Program Files (x86)\Intel\oneAPI\setvars.bat" intel64 --force

cmake --build build -j

For Intel GPUs that need allocations larger than 4 GB:

set UR_L0_ENABLE_RELAXED_ALLOCATION_LIMITS=1

The repository’s Windows SYCL run script uses this environment setting.

A proposed Granite test command would be:

build\bin\llama-server.exe \`  
-m granite.gguf \`  
-ngl 99 \`  
-c 8192 \`  
-fa on \`  
-ctk tq3_0 \`  
-ctv tq3_0 \`  
--host 127.0.0.1 \`  
--port 8080

One documentation problem is that the README’s build examples still clone the older unixsysdev repository. Following those commands would not necessarily give us the newer Intel SYCL implementation. We must clone animehacker/llama-turboquant directly.

### 11. Proposed application integration

The simplest implementation is still to use llama-server as a separate backend process.

Our desktop application  
↓  
Detect Intel GPU and SYCL availability  
↓  
Launch animehacker llama-server  
↓  
Load Granite GGUF  
↓  
Select TQ3_0 key/value cache  
↓  
Communicate through local HTTP

#### Device-selection outcomes

Full SYCL and TQ3_0 support  
↓  
Use Intel GPU with TQ3_0 K and V

SYCL works but TQ3_0 fails  
↓  
Use normal SYCL with q8_0 or F16 KV cache

SYCL unavailable but Vulkan works  
↓  
Try AtomicBot’s Vulkan TurboQuant route

No usable GPU route  
↓  
Use CPU or standard OpenVINO fallback

Model does not fit through any route  
↓  
Explain the limitation and recommend  
a smaller model or context length

### 12. What we should test first

Keep the Granite GGUF weights fixed and compare:

| **Keys** | **Values** | **Purpose**                        |
|----------|------------|------------------------------------|
| F16      | F16        | Accuracy and memory baseline       |
| Q8_0     | Q8_0       | Standard compressed-cache baseline |
| TQ3_0    | F16        | Measure key-compression effect     |
| Q8_0     | TQ3_0      | Safer asymmetric possibility       |
| TQ3_0    | TQ3_0      | Maximum TQ3_0 compression          |

Keys may be more sensitive to low-bit compression than values, so q8_0 keys with tq3_0 values may provide a safer balance than compressing both equally.

Testing should establish:

- whether Granite loads;

- whether the Intel GPU is detected;

- whether the cache is genuinely stored on the GPU;

- whether Flash Attention remains enabled;

- actual KV-memory reduction;

- prompt and generation speed;

- maximum stable context;

- perplexity or answer-quality change;

- stability over long prompts and repeated requests.

Context should increase gradually:

2K  
→ 4K  
→ 8K  
→ 16K  
→ 32K

### 13. Main risks

##### Limited Intel validation

Only one custom TQ3_0 Intel configuration is documented: Battlemage B70. Integrated Arc, Iris Xe, Meteor Lake and Lunar Lake remain unverified for this custom format.

##### No Granite benchmark

The successful Qwen test proves the Intel kernel works, but not that Granite will preserve the same quality.

##### Additional dequantisation overhead

The cache remains compressed, but the attention path reconstructs values during use. This explains the reported generation slowdown.

##### Experimental maintenance state

An open HIP issue reports a segmentation fault with TQ3_0 on an AMD RX 9060 XT and Gemma 3. Another CUDA issue required an additional GPU copy path and was closed after becoming stale rather than after confirmed user validation.

##### Documentation inconsistency

The README, current source and merged SYCL PR do not always describe Flash Attention and QJL consistently. We need to rely on the exact tested commit rather than general README claims.

### Final recommendation

This repository should move into the **primary implementation-testing group**.

Intel relevance  
→ High

Actual custom SYCL implementation  
→ Yes

Intel validation  
→ Yes, but only Battlemage B70

Granite validation  
→ No

Formal QJL  
→ No

Production readiness  
→ Not yet

Our practical position should be:

Use animehacker/llama-turboquant as the main SYCL candidate for testing TQ3_0 KV-cache compression on Intel GPUs. Compare it with AtomicBot’s Vulkan route. Do not assume Granite or integrated-GPU compatibility until the selected Granite model passes memory, quality, stability and performance tests.

## Original research: post animehacker review implemntation thoughts

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/animehacker repo/post animehacker review implemntation thoughts.docx`

The supplied DOCX contains no body text or embedded research content. The exact original file is preserved in the controlled provenance ZIP; `99-original-docx` contains the archive notice.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-REPO-ANIMEHACKER`
- `SRC-LLAMACPP-GITHUB`
- `SRC-PAPER-TURBOQUANT-2025`
