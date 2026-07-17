# unixsysdev/llama-turboquant

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

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

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: unixsysdev llama-turboquant Summary

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/unixsysdev llama-turboquant/unixsysdev llama-turboquant Summary.docx`

**Repository:** llama-turboquant  
**URL:** <https://github.com/unixsysdev/llama-turboquant>  
**Owner:** unixsysdev  
**Branch:** main  
**Current commit:** 03fa8abc4708dfc13858de0a74695075702c8e26  
**Last relevant update:** 25 March 2026  
**Licence:** MIT

#### **Main conclusion**

This repository is the **original base implementation behind the animehacker fork** that we already examined.

It adds one format:

TQ3_0

However, its README overstates how closely it follows formal TurboQuant. The source calculates and stores one-bit residual signs, but those residual bits are not used during CUDA dequantisation or the fused attention dot product. Therefore, the repository does **not actually apply QJL residual correction during inference**.

It is also limited to CPU, CUDA and HIP/ROCm. There is no custom Vulkan, SYCL, OpenVINO or Intel NPU implementation.

For our application, this repository is therefore largely superseded by animehacker/llama-turboquant, which is based on this exact commit but later corrects the quantisation description and adds Intel SYCL support.

### **1. What the repository claims to implement**

The README describes TQ3_0 as a combination of:

Walsh–Hadamard rotation  
+  
2-bit Lloyd–Max codebook  
+  
1-bit QJL residual correction

Each block contains 32 values and is stored in 14 bytes:

| **Field** | **Size**     | **Claimed purpose**                 |
|-----------|--------------|-------------------------------------|
| qs\[8\]   | 8 bytes      | Thirty-two 2-bit codebook indices   |
| qr\[4\]   | 4 bytes      | Thirty-two 1-bit QJL residual signs |
| gamma     | 2 bytes      | FP16 scale                          |
| **Total** | **14 bytes** | **3.5 bits per value**              |

Compared with 64 bytes for 32 FP16 values, this provides:

64 ÷ 14 ≈ 4.57× K-cache compression

### **2. What the source code actually does**

The actual GPU quantisation process is:

32 original key-cache values  
↓  
Apply fixed positive and negative sign changes  
↓  
Apply a 32-value Walsh–Hadamard Transform  
↓  
Find the maximum absolute transformed value  
↓  
Calculate scale = maximum ÷ 1.510  
↓  
Map every value to one of four centroids  
↓  
Store each centroid index using 2 bits  
↓  
Calculate the sign of each elementwise residual  
↓  
Store those signs in qr

The four codebook values are approximately:

-1.510  
-0.453  
+0.453  
+1.510

The GPU quantiser sets gamma to the main scalar-quantisation scale and stores whether each residual is positive or negative.

#### **Critical problem: the residual bits are not used**

During GPU dequantisation, the implementation reads:

- qs;

- gamma.

It does **not** read qr. It reconstructs the four-level approximation and applies the inverse WHT, but never adds a residual correction.

The fused query-key dot-product kernel also reads:

- qs;

- gamma.

It likewise ignores qr.

A repository-wide source search finds the qr field only in:

- the block definition;

- the README;

- the quantisation code that writes it.

No inference code was found that reads those bits and converts them into an attention-score correction.

Therefore, the practical representation is:

Useful information:  
2-bit centroid indices  
+  
FP16 scale

Stored but currently unused:  
1-bit residual signs

Without the unused four residual bytes, the structure would require only ten bytes per 32 values, or 2.5 bits per value. Instead, the repository stores 3.5 bits per value while receiving no demonstrated QJL benefit from the additional bit.

### **3. Why this is not genuine QJL**

Formal QJL residual correction requires more than recording whether each direct residual coordinate is positive or negative.

The intended process is broadly:

Calculate the residual vector  
↓  
Apply a separate random projection to the residual  
↓  
Store the signs of the projected values  
↓  
Store the residual norm  
↓  
Project the query in the same way  
↓  
Use both sign sketches to estimate the lost inner product

This repository does not provide that complete process:

- no separate QJL projection matrix is applied;

- the signs are taken directly from elementwise residuals;

- gamma stores the main quantiser scale rather than a separately used residual norm;

- the query-key dot product never reads the residual signs;

- no QJL correction is added to the attention score.

The source comment describes gamma as a residual norm, but the actual GPU quantisation code assigns it the primary scale amax / 1.510.

This repository is therefore more accurately described as:

**A 2-bit WHT-preconditioned scalar K-cache quantiser stored in a 3.5-bit structure, with residual-sign metadata that is written but not used during inference.**

### **4. Difference from the PolarQuant stage**

The repository does implement a useful form of random preconditioning:

Fixed sign changes  
+  
WHT32

However, the WHT is applied independently to every 32-value block. Formal PolarQuant applies its random preconditioning across the vector or attention-head dimension.

The README openly acknowledges this difference:

- the paper uses a full d × d rotation;

- this implementation uses separate 32 × 32 transforms;

- the smaller transform avoids changing the full attention graph;

- it is an engineering approximation rather than an exact paper implementation.

Therefore:

Random/WHT preconditioning  
→ Implemented in simplified 32-value blocks

Scalar Gaussian codebook quantisation  
→ Implemented

Full head-dimensional PolarQuant transformation  
→ Not implemented exactly

QJL residual correction  
→ Not functionally implemented

### **5. Attention calculation**

The repository provides a useful fused CUDA/HIP query-key operation.

Because WHT is orthogonal:

q · k = WHT(q) · WHT(k)

The implementation transforms the query inside the fused dot-product kernel and calculates the attention score directly against the compressed, rotated key representation. This avoids reconstructing the complete original key vector before every dot product.

This is a useful engineering idea and helps explain why its reported throughput remains close to Q4_0 despite the extra transform.

However, the fused operation only uses the two-bit centroid approximation. It does not include the stored residual signs.

### **6. Key cache versus value cache**

This implementation is mainly a **key-cache format**.

The documented command is:

--cache-type-k tq3_0

The README also suggests combining TQ3_0 keys with a quantised value cache:

--cache-type-k tq3_0 --cache-type-v q8_0

However, that combination is not currently functional in normal server operation.

#### **Flash Attention conflict**

The code automatically disables Flash Attention when TQ3_0 is used for keys:

TQ3_0 key cache  
↓  
Flash Attention forced off

But llama.cpp requires Flash Attention for quantised value-cache storage:

Quantised value cache  
↓  
Flash Attention required

This creates a conflict:

TQ3_0 K + q8_0 V  
↓  
TQ3_0 disables Flash Attention  
↓  
q8_0 V requires Flash Attention  
↓  
Context creation fails

The practical working configuration is therefore:

TQ3_0 keys  
+  
FP16 values

A user confirmed that llama-bench worked on an RTX 5090, but llama-server failed with a quantised value cache. The issue was closed automatically as stale rather than being fixed.

If keys and values occupy approximately equal memory, compressing only keys by 4.57× reduces the **complete KV cache** by only about:

Total original size:  
1 K + 1 V = 2 units

Compressed:  
0.219 K + 1 V = 1.219 units

Overall reduction:  
2 ÷ 1.219 ≈ 1.64×

Therefore, the headline 4.57× result applies to the key cache, not necessarily to total KV-cache memory.

### **7. Backend support**

#### **CPU**

A CPU quantisation and dequantisation path exists. The README describes it as functional but slower than GPU execution.

#### **CUDA**

Custom CUDA code exists for:

- TQ3_0 cache writing;

- WHT transformation;

- dequantisation;

- fused query-key dot products;

- MMVQ dispatch.

CUDA benchmarking has been confirmed by a user on NVIDIA Blackwell, although the server/value-cache problem remains.

#### **HIP/ROCm**

The same shared GPU implementation can be compiled through HIP. The repository’s own published tests used an AMD Radeon 8060S with ROCm 7.2.

#### **Unsupported custom routes**

Repository-wide TQ3_0 references are confined to:

- common type registration;

- CPU;

- CUDA/HIP;

- llama.cpp integration and benchmark code.

No custom implementation appears in:

- Vulkan;

- SYCL;

- Metal;

- OpenVINO;

- Intel NPU code.

The supported route summary is:

Normal llama.cpp CPU  
→ Supported

TQ3_0 + CPU  
→ Functional fallback

Normal NVIDIA CUDA  
→ Supported

TQ3_0 + CUDA  
→ Supported for K-cache benchmarking and inference

Normal AMD HIP/ROCm  
→ Supported

TQ3_0 + HIP/ROCm  
→ Implemented and benchmarked

TQ3_0 + Vulkan  
→ Not implemented

TQ3_0 + Intel SYCL  
→ Not implemented

TQ3_0 + OpenVINO  
→ Not implemented

TQ3_0 + Intel NPU  
→ Not implemented

### **8. Published results**

The repository reports tests on an AMD Radeon 8060S using Qwen3.5-0.8B.

One perplexity comparison reports:

| **Cache** | **Perplexity** | **Change** |
|-----------|----------------|------------|
| FP16      | 15.49          | Baseline   |
| TQ3_0     | 16.20          | +4.6%      |

A second run using a Q5_K_M model reports:

| **Cache** | **Perplexity** | **Change** |
|-----------|----------------|------------|
| FP16      | 20.05          | Baseline   |
| Q4_0      | 20.14          | +0.4%      |
| TQ3_0     | 21.21          | +5.8%      |

Generation speed was reported as:

| **Cache** | **Generation speed** |
|-----------|----------------------|
| FP16      | 181.8 tokens/s       |
| Q4_0      | 179.1 tokens/s       |
| TQ3_0     | 177.9 tokens/s       |

Thus, TQ3_0 was approximately 2.1% slower than FP16 in that particular test, but its quality loss was significantly larger than ordinary Q4_0.

### **9. Granite compatibility**

The inherited llama.cpp code recognises Granite architectures.

However:

Normal Granite GGUF  
→ Likely supported through inherited llama.cpp

Granite + TQ3_0 K cache  
→ Not documented or tested

Granite + TQ3_0 + Intel GPU  
→ Not supported

Granite + TQ3_0 K + quantised V  
→ Blocked by the Flash Attention limitation

For Granite Hybrid models, the compression would apply only to transformer attention KV states. It would not automatically compress recurrent or state-space memory, so total application memory savings could be smaller.

### **10. Relationship with the animehacker repository**

This is the most important point for our repository selection.

The current unixsysdev head commit:

03fa8abc4708dfc13858de0a74695075702c8e26

also appears in the animehacker repository’s history, confirming that animehacker was built on top of this implementation.

The later animehacker lineage then:

1.  corrected the misleading QJL description;

2.  converted the extra bit into the upper bit of a genuine 3-bit, eight-centroid index;

3.  explicitly documented that QJL was not implemented;

4.  added an Intel SYCL path;

5.  added the SET_ROWS support needed for Intel KV-cache writes.

Therefore:

unixsysdev  
→ Older original implementation  
→ Misleading QJL claim  
→ CPU/CUDA/HIP only

animehacker  
→ Newer corrected format  
→ Honest PolarQuant-only description  
→ Adds Intel SYCL

### **Relevance to the application**

#### **Useful parts**

The repository remains useful for studying:

- how a custom KV-cache type is registered in GGML;

- fixed-sign WHT32 preconditioning;

- physically packed low-bit key-cache storage;

- fused query transformation and key dot products;

- CUDA/HIP cache-writing kernels;

- memory and perplexity comparisons.

#### **Limited direct relevance**

It is not a strong implementation candidate because:

- there is only one custom format;

- the actual code does not use QJL;

- its one residual bit appears wasted;

- it compresses mainly the key cache;

- quantised values are incompatible with its no-Flash-Attention route;

- it has no Vulkan or Intel SYCL support;

- its unresolved issues were closed as stale;

- the animehacker fork already provides a newer version of the same lineage.

### **Final recommendation**

Main Intel backend  
→ No

Vulkan candidate  
→ No

SYCL candidate  
→ No; animehacker supersedes it

CUDA/HIP research reference  
→ Moderate value

Separate implementation test  
→ Not necessary

**The unixsysdev repository should not be tested as a separate application candidate. It is the older base of the animehacker implementation and contains a misleading, functionally incomplete QJL design. Retain it only as a historical reference for the original CUDA/HIP WHT32 implementation. Use animehacker for the corrected Intel SYCL route and more developed repositories for CUDA or Vulkan research.**

## Original research: unixsysdev llama-turboquant Summary1

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/unixsysdev llama-turboquant/unixsysdev llama-turboquant Summary1.docx`

Repository: llama-turboquant

URL: <https://github.com/unixsysdev/llama-turboquant>

Owner: unixsysdev

Branch: main

Commit: 03fa8abc4708dfc13858de0a74695075702c8e26

Last relevant update: 25 March 2026. The underlying TQ3_0 implementation and the current documentation were added on this date.

Licence: MIT

Purpose: Add a custom TQ3_0 low-bit key-cache compression format to llama.cpp, mainly for CUDA and HIP/ROCm inference. The repository aims to reduce key-cache memory while keeping generation speed close to conventional cache formats.

Claimed method: A 32-value Walsh–Hadamard rotation, a two-bit Lloyd–Max scalar codebook and one-bit QJL residual signs for error correction. The README describes this as a practical implementation of the combined PolarQuant and QJL TurboQuant pipeline.

Actual method: Divides key-cache data into blocks of 32 values, applies fixed sign changes and a 32-value Walsh–Hadamard Transform, maps every transformed value to one of four Lloyd–Max centroids, stores each centroid index using two bits and stores one additional sign bit for each elementwise quantisation residual. One FP16 scale is also stored for every block.

Difference from formal TurboQuant: The repository uses separate 32-value WHT blocks rather than a full random rotation across the complete attention-head vector. More importantly, the stored residual-sign bits are not used during dequantisation or the fused query-key dot product. There is no separate QJL projection matrix, projected-query sketch or residual inner-product correction. Therefore, it does not functionally implement the formal QJL stage, despite claiming to do so.

Weight quantisation: Mainly inherited llama.cpp GGUF weight formats. The repository does not add a separate TurboQuant weight-compression format.

KV-cache quantisation: Yes, although the custom format is mainly designed and documented for the key cache.

Key-cache types: TQ3_0, plus inherited formats such as F16, q8_0 and q4_0.

Value-cache types: Inherited formats such as F16, q8_0 and q4_0. TQ3_0 is not documented or validated as a value-cache format. In practice, FP16 values are the safest compatible option when TQ3_0 is used for keys.

Bits per value: TQ3_0 physically uses 3.5 bits per value, including the two-bit codebook index, one stored residual-sign bit and the FP16 scale overhead.

Block structure: 32 values per block.

Metadata overhead: Eight bytes for two-bit codebook indices, four bytes for residual-sign bits and two bytes for one FP16 scale.

Actual memory packing: Yes. Each 32-value block occupies 14 bytes instead of 64 bytes for FP16, giving approximately 4.57× key-cache compression. However, the four residual-sign bytes are stored without being used by the current inference path.

CPU support: Yes. CPU quantisation and dequantisation paths exist, although they are intended mainly as functional fallback paths and are slower than GPU execution.

CUDA support: Yes. The repository includes CUDA cache-writing, WHT, dequantisation and fused query-key dot-product code. CUDA benchmarking has been confirmed, but normal server use has important Flash Attention and value-cache limitations.

ROCm support: Yes through the shared CUDA/HIP code. The repository’s published benchmarks used an AMD Radeon 8060S with ROCm 7.2. However, one user reported that a HIP build did not recognise tq3_0, so support should be considered experimental and build-dependent.

Vulkan support: Normal llama.cpp Vulkan inference may be inherited, but no custom TQ3_0 Vulkan kernels are provided.

SYCL support: Normal llama.cpp SYCL code may be inherited, but this repository does not implement TQ3_0 through Intel SYCL.

OpenVINO support: No custom TQ3_0/OpenVINO integration.

NPU support: No custom Intel NPU route.

Supported and unsupported routes:

Normal GGUF + llama.cpp CPU  
→ Supported

TQ3_0 key cache + CPU  
→ Supported but slower

Normal GGUF + NVIDIA CUDA  
→ Supported

TQ3_0 key cache + NVIDIA CUDA  
→ Supported, but experimental

TQ3_0 keys + FP16 values + CUDA  
→ Main practical server configuration

TQ3_0 keys + quantised values + CUDA  
→ Currently incompatible because TQ3_0 disables Flash Attention while quantised values require it

Normal GGUF + AMD HIP/ROCm  
→ Supported

TQ3_0 key cache + AMD HIP/ROCm  
→ Implemented and benchmarked, but build and compatibility issues have been reported

Normal GGUF + inherited Vulkan  
→ Potentially supported

TQ3_0 + Vulkan  
→ Not supported through custom kernels

Normal GGUF + inherited Intel SYCL  
→ Potentially supported

TQ3_0 + Intel SYCL  
→ Not supported in this repository

TQ3_0 + OpenVINO  
→ Not supported

TQ3_0 + Intel NPU  
→ Not supported

Windows build: A normal llama.cpp CUDA build should be possible on Windows, but the README primarily documents Linux-style CUDA and HIP build commands. There is no dedicated Intel or Windows SYCL build script.

Required tools: Git, CMake and a C++ compiler. A Windows CUDA build additionally requires Visual Studio C++ Build Tools, the CUDA Toolkit and current NVIDIA drivers. HIP builds require ROCm and a supported AMD GPU.

Build command:

git clone <https://github.com/unixsysdev/llama-turboquant.git>  
cd llama-turboquant

cmake -S . -B build \`  
-DGGML_CUDA=ON \`  
-DCMAKE_BUILD_TYPE=Release

cmake --build build --config Release -j

AMD HIP build:

git clone <https://github.com/unixsysdev/llama-turboquant.git>  
cd llama-turboquant

cmake -S . -B build-hip \\  
-DGGML_HIP=ON \\  
-DCMAKE_BUILD_TYPE=Release \\  
-DAMDGPU_TARGETS="gfx1151"

cmake --build build-hip -j

The repository documents CUDA, HIP and CPU-only build routes.

Run command:

build\bin\Release\llama-server.exe \`  
-m granite.gguf \`  
-ngl 99 \`  
-c 8192 \`  
-fa off \`  
-ctk tq3_0 \`  
-ctv f16 \`  
--port 8080

FP16 should currently be used for the value cache because TQ3_0 keys force Flash Attention off, while quantised value caches require Flash Attention.

Granite compatibility: Normal Granite architecture recognition is inherited from llama.cpp, but Granite with TQ3_0 has not been documented or validated.

Tested Granite model: None documented.

Supported head dimensions: Cache rows must be divisible by the 32-value TQ3_0 block size. The implementation does not provide the more flexible head-dimension padding or dimension-specific fallbacks found in some later repositories.

Flash Attention: Not supported with TQ3_0 keys. The context initialisation code automatically disables Flash Attention when TQ3_0 is selected.

Maximum tested context: No reliable maximum working context is formally published. A user attempted a 200K context with TQ3_0 keys and q8_0 values, but the server failed because of the Flash Attention and value-cache storage conflict.

Published benchmarks: Yes, mainly Qwen3.5 models on AMD Radeon 8060S and one user-reported NVIDIA RTX 5090 test.

Quality tests: WikiText-2 perplexity comparisons.

Memory tests: Yes. The format provides approximately 4.57× compression for the key cache. Because the safest working value cache remains FP16, total combined key-and-value memory reduction is closer to approximately 1.64× when keys and values originally occupy equal memory.

Speed tests: Yes. On the published Radeon 8060S benchmark, TQ3_0 generation was approximately 2.1% slower than FP16. A user-reported RTX 5090 llama-bench test found approximately 1.3% generation slowdown.

Known bugs:

- The repository claims QJL residual correction, but the stored residual bits are not read by the dequantisation or attention kernels.

- TQ3_0 keys automatically disable Flash Attention.

- Quantised value caches require Flash Attention, so TQ3_0 keys cannot currently be combined reliably with q8_0 or q4_0 values.

- llama-server has been reported to crash after context initialisation fails rather than exiting safely.

- One HIP user reported that the built executable did not recognise tq3_0.

- Relevant issues were closed as stale rather than clearly resolved.

Files containing main implementation:

ggml/include/ggml.h  
ggml/src/ggml-common.h  
ggml/src/ggml-quants.c  
ggml/src/ggml-quants.h  
ggml/src/ggml-cpu/quants.c  
ggml/src/ggml-cpu/ggml-cpu.c  
ggml/src/ggml-cuda/cpy-utils.cuh  
ggml/src/ggml-cuda/convert.cu  
ggml/src/ggml-cuda/set-rows.cu  
ggml/src/ggml-cuda/vecdotq.cuh  
ggml/src/ggml-cuda/mmvq.cu  
ggml/src/ggml-cuda/ggml-cuda.cu  
common/arg.cpp  
src/llama-context.cpp  
tools/llama-bench/llama-bench.cpp

Ease of integration: Medium for a basic CUDA or HIP experiment using llama-server, but low for the Intel application because there is no custom Vulkan, SYCL or OpenVINO TQ3_0 path. The value-cache limitation also reduces the practical total memory benefit.

Maintenance risk: High. It is an experimental fork with misleading algorithm documentation, unresolved server limitations and no current Intel implementation.

How we could use it: Retain it as a historical CUDA/HIP reference for the original WHT32 TQ3_0 implementation and fused query-key dot-product design. It does not need to be tested separately because the animehacker repository is directly based on this version and adds a corrected format description, true eight-level three-bit quantisation and Intel SYCL support.

Final recommendation: Do not use this repository as a main implementation candidate. It is superseded by the animehacker fork for Intel SYCL work, and its claimed QJL correction is not functionally applied. Keep it only as a reference for studying the original CUDA/HIP cache-writing, WHT and fused attention approach.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-REPO-UNIXSYSDEV`
- `SRC-LLAMACPP-GITHUB`
- `SRC-PAPER-TURBOQUANT-2025`
