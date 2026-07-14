---
title: "unixsysdev llama-turboquant Summary1"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/GitHub repos/unixsysdev llama-turboquant/unixsysdev llama-turboquant Summary1.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

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
