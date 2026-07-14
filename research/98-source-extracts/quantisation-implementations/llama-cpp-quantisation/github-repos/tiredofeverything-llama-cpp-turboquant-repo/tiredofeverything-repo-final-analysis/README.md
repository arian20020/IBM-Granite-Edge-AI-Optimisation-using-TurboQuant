---
title: "TiredOfEverything repo final analysis"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/GitHub repos/TiredOfEverything llama-cpp-turboquant repo/TiredOfEverything repo final analysis.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

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
