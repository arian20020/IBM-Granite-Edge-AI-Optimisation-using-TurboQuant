---
title: "Final Analysis of thepradip repo"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/GitHub repos/thepradip turboquant-llama.cpp/Final Analysis of thepradip repo.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

Repository: turboquant-llamacpp  
URL: <https://github.com/thepradip/turboquant-llamacpp>  
Owner: thepradip  
Branch: master  
Commit: 8598f2edfbfd87b92d8f70e95f9d3de623ceff3d  
Last relevant update: 8 April 2026  
Licence: MIT

Purpose: Add a simple 4-bit Lloyd–Max KV-cache compression format called TQ4_0 to llama.cpp.

Claimed method: Lloyd–Max optimal 4-bit quantisation for normally distributed KV-cache values, providing approximately 3.56× memory reduction.

Actual method: Divides KV-cache values into 32-value blocks, calculates the block RMS, normalises each value, assigns it to one of 16 fixed Lloyd–Max codebook values, calculates a least-squares scale and packs the 4-bit indices with one FP16 scale.

Difference from formal TurboQuant: It does not implement random or Walsh–Hadamard rotation, proper PolarQuant preconditioning, polar-coordinate quantisation or QJL residual correction. It is a TurboQuant-inspired scalar Lloyd–Max quantiser rather than a complete TurboQuant implementation.

Weight quantisation: Uses inherited llama.cpp GGUF weight formats. No custom TurboQuant weight compression is provided.

KV-cache quantisation: Yes; main feature.

Key-cache types: TQ4_0, plus inherited formats such as F16, q8_0 and q4_0.

Value-cache types: TQ4_0, plus inherited formats such as F16, q8_0 and q4_0.

Bits per value: Approximately 4.5 effective bits per value, including the FP16 scale.

Block structure: 32 values per block.

Metadata overhead: One FP16 scale per 32-value block.

Actual memory packing: Yes. Each block stores 16 bytes of packed 4-bit indices and a 2-byte scale, giving 18 bytes instead of 64 bytes for FP16.

CPU support: Yes. The latest and most complete Lloyd–Max implementation is in the CPU quantisation path.

CUDA support: Claimed and partially integrated, but correctness is uncertain because CUDA cache writes currently route TQ4_0 through the standard Q4_0 quantisation kernel.

Metal support: Claimed and tested for memory usage, but parts of the implementation reuse standard Q4_0 Metal kernels. The numerical correctness of the corrected Lloyd–Max format therefore needs independent validation.

ROCm support: Normal llama.cpp may support ROCm, but no dedicated or validated TQ4_0 ROCm route is documented.

Vulkan support: No custom TQ4_0 Vulkan implementation documented.

SYCL support: No custom TQ4_0 Intel SYCL implementation documented.

OpenVINO support: No TQ4_0/OpenVINO integration documented.

NPU support: No.

Supported and unsupported routes:

Normal GGUF + llama.cpp CPU  
→ Supported

TQ4_0 + CPU  
→ Supported; most complete implementation

Normal GGUF + NVIDIA CUDA  
→ Supported

TQ4_0 + NVIDIA CUDA  
→ Partially integrated, but correctness requires validation

Normal GGUF + Apple Metal  
→ Supported

TQ4_0 + Apple Metal  
→ Claimed and memory-tested, but kernel consistency requires validation

TQ4_0 + AMD ROCm  
→ Not clearly supported or validated

TQ4_0 + Vulkan  
→ Not supported as a custom route

TQ4_0 + Intel SYCL  
→ Not supported

TQ4_0 + OpenVINO  
→ Not supported

TQ4_0 + Intel NPU  
→ Not supported

Windows build: Standard llama.cpp CPU or CUDA builds may be possible, but the repository documents macOS Metal, Linux CUDA and CPU builds. No Windows Intel GPU route is provided.

Required tools: Git, CMake and a C++ compiler. CUDA builds additionally require the CUDA Toolkit; Metal builds require Apple development tools.

Build command:

git clone <https://github.com/thepradip/turboquant-llamacpp.git>  
cd turboquant-llamacpp  
  
cmake -S . -B build  
cmake --build build --config Release -j

CUDA build:

cmake -S . -B build -DGGML_CUDA=ON  
cmake --build build --config Release -j

Run command:

./build/bin/llama-cli \\  
-m granite.gguf \\  
-ngl 99 \\  
-fa on \\  
-c 8192 \\  
--turbo \\  
-p "Test prompt"

--turbo is shorthand for:

-ctk tq4_0 -ctv tq4_0

Granite compatibility: Normal Granite GGUF support is inherited from llama.cpp, but Granite with TQ4_0 has not been confirmed.

Tested Granite model: None documented.

Tested models: Gemma 4 E4B and Bonsai-8B are mentioned by the implementation commit.

Supported head dimensions: No specific list is documented. Cache rows must be compatible with the 32-value block structure.

Flash Attention: Used in the documented run command and claimed for Metal and CUDA, but the corrected Lloyd–Max GPU paths require validation.

Maximum tested context: 32K tokens in the published Gemma 4 E4B memory table.

Published benchmarks: Limited. The repository mainly publishes KV-cache memory measurements on an Apple M2 Pro.

Quality tests: No detailed perplexity, exact-answer or long-context retrieval results are published.

Memory tests: Yes. It reports approximately 3.56× KV-cache compression, including a reduction from 552 MiB to 155 MiB at 32K context.

Speed tests: No detailed prompt-processing or generation-speed comparison is published.

Known bugs and risks: The corrected CPU implementation uses a real Lloyd–Max codebook, but CUDA and Metal still reuse parts of the standard Q4_0 path. This may cause cache data to be written using Q4_0 rules and later interpreted using TQ4_0 rules. The repository also removed many inherited automated build and test workflows, increasing regression risk.

Files containing main implementation:  
ggml/src/ggml-common.h  
ggml/src/ggml-quants.c  
ggml/src/ggml-cpu/quants.c  
ggml/src/ggml-cpu/ggml-cpu.c  
ggml/src/ggml-cuda/set-rows.cu  
ggml/src/ggml-cuda/ggml-cuda.cu  
ggml/src/ggml-metal/ggml-metal-device.cpp  
common/arg.cpp

Ease of integration: Low to medium for a CPU experiment, but low for the Intel application because no Vulkan, SYCL, OpenVINO or NPU TQ4_0 route is provided.

Maintenance risk: High. It is experimental, has limited evaluation, lacks dedicated Intel support and removed much of the inherited automated CI coverage.

How we could use it: At most, use it as an optional CPU comparison between standard Q4_0 and Lloyd–Max TQ4_0 at the same memory size.

Final recommendation: Exclude it from the main implementation candidates. It does not implement PolarQuant or QJL properly, provides no custom Intel acceleration route and has uncertain GPU correctness. Prioritise AtomicBot for Vulkan and animehacker for Intel SYCL instead.
