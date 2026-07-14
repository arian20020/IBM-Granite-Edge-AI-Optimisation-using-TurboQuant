Repository: llama-cpp-turboquant

URL: <https://github.com/atomicmilkshake/llama-cpp-turboquant>

Owner: atomicmilkshake

Branch: feature/triattention

Commit: 75016369b38c841da095244d07ab86453023b11a

Last relevant update: 8 April 2026 on the inspected default branch

Licence: MIT

Purpose: Implement practical low-bit TurboQuant-style KV-cache compression directly inside llama.cpp and combine it with an experimental TriAttention token-pruning system. The repository focuses mainly on CUDA, Metal and HIP/ROCm execution rather than Intel acceleration.

Claimed method: Walsh–Hadamard pre-rotation, Lloyd–Max codebook quantisation and packed 2-bit, 3-bit and 4-bit KV-cache formats. It also claims GPU-accelerated TriAttention pruning, which scores cached tokens and removes those predicted to be less useful.

Actual method: KV vectors are grouped, normalised using their L2 norm, transformed using fixed random sign patterns and a Walsh–Hadamard Transform, mapped to low-bit Lloyd–Max centroids and packed into custom cache structures. The attention graph is modified so queries, keys, values and attention outputs remain mathematically consistent with the rotated cache representation.

Difference from formal TurboQuant: The repository implements the main PolarQuant-style preconditioning and low-bit approximation stage, but the current default formats do not use formal QJL residual correction. Legacy 3-bit-plus-QJL code remains available behind a compile-time option, but the active turbo4 format uses direct 4-bit PolarQuant because the developers found it produced better practical quality. The repository also adds zero-padding for unsuitable head dimensions, asymmetric key/value compression, layer-adaptive formats, Flash Attention integration and TriAttention pruning.

Weight quantisation: Mainly inherited llama.cpp GGUF weight formats. The repository’s TurboQuant work is focused on the runtime KV cache rather than creating a new primary weight-quantisation system.

KV-cache quantisation: Yes; one of the repository’s main features.

Key-cache types: f16, bf16, q8_0 and other inherited llama.cpp formats, plus turbo2, turbo3 and turbo4.

Value-cache types: f16, bf16, q8_0 and other inherited llama.cpp formats, plus turbo2, turbo3 and turbo4.

Bits per value:

turbo2  
→ Approximately 2.125 bits per value  
→ Approximately 7.53× smaller than FP16

turbo3  
→ Approximately 3.125 bits per value  
→ Approximately 5.12× smaller than FP16

turbo4  
→ Approximately 4.25 bits per value  
→ Approximately 3.76–3.8× smaller than FP16

The active 128-value structures contain approximately 34 bytes for turbo2, 50 bytes for turbo3 and 68 bytes for turbo4. The repository contains some outdated comments referring to earlier 32-value layouts, so the active macro and structure definitions should be treated as authoritative.

Block structure: The current formats use 128-value storage and transform groups. Attention-head dimensions that are not naturally aligned to 128 may be zero-padded to the next multiple of 128, such as 192 to 256 or 576 to 640.

Metadata overhead: An FP16 corrected norm is stored for each transform block. Turbo3 also stores the third codebook-index bit separately. Turbo4 contains a reserved residual-norm field, but the current default four-bit mode does not use QJL residual signs.

Actual memory packing: Yes. The KV cache is physically stored in packed 2-bit, 3-bit or 4-bit structures rather than merely converting values temporarily during computation.

CPU support: Standard llama.cpp CPU inference is supported. CPU reference and fallback TurboQuant operations are present, but the main performance and quality validation is centred on GPU execution. CPU TurboQuant should therefore be treated as a reference or fallback route rather than the main deployment path.

CUDA support: Yes; the main and best-supported Windows/Linux route. The repository contains custom cache-writing, WHT, dequantisation and Flash Attention kernels for turbo2, turbo3 and turbo4.

Metal support: Yes; substantial support exists for turbo2, turbo3 and turbo4 on Apple Silicon. The project includes custom quantisation, dequantisation, cache-writing and Flash Attention paths, although the development history shows that significant correctness and performance debugging was required.

ROCm support: Yes, but less mature than CUDA and Metal. The repository contains a HIP/ROCm port and reports testing on AMD hardware including a Radeon 7900 XTX and Ryzen AI Max+ 395/Strix Halo.

Vulkan support: Normal inherited llama.cpp Vulkan inference may work, but no custom Vulkan implementation for turbo2, turbo3 or turbo4 was found.

SYCL support: Normal inherited llama.cpp SYCL code exists, but no completed Intel SYCL implementation for these TurboQuant formats was found.

OpenVINO support: No completed TurboQuant/OpenVINO integration documented.

NPU support: No.

TriAttention support: A CPU scorer and custom CUDA scorer are implemented. However, TriAttention requires a model-specific calibration file, and a repository issue reported that the documented calibration flag and scripts were missing. The issue was closed as stale rather than being clearly resolved.

Supported and unsupported routes:

Normal GGUF + llama.cpp CPU  
→ Supported

Normal GGUF + NVIDIA CUDA  
→ Supported

Turbo2/3/4 + NVIDIA CUDA  
→ Supported; main route

Turbo2/3/4 + Apple Metal  
→ Supported; substantially developed and tested

Turbo2/3/4 + AMD HIP/ROCm  
→ Supported experimentally; secondary route

Turbo2/3/4 entirely on CPU  
→ Reference/fallback implementation present, but not the primary validated route

Turbo2/3/4 + Vulkan  
→ Not supported through custom kernels

Turbo2/3/4 + Intel SYCL  
→ Not supported through custom kernels

Turbo2/3/4 + OpenVINO  
→ Not supported

Turbo2/3/4 + Intel NPU  
→ Not supported

TriAttention + CUDA  
→ Implemented, but calibration workflow remains questionable

TriAttention + Intel GPU or NPU  
→ Not supported

Windows build: Windows 10 and 11 are documented. Pre-built Windows x64 binaries are provided for CUDA 13 and NVIDIA RTX 2000-series or newer GPUs. The repository is therefore directly testable on the Lenovo RTX 4060, provided the required CUDA runtime is installed.

Required tools: Git, CMake 3.21 or later, Visual Studio 2022 with the C++ workload, CUDA Toolkit 12.x or 13.x and current NVIDIA drivers.

Build command:

git clone <https://github.com/atomicmilkshake/llama-cpp-turboquant.git>  
cd llama-cpp-turboquant  
git checkout feature/triattention  

cmake -S . -B build \`  
-DGGML_CUDA=ON \`  
-DCMAKE_CUDA_ARCHITECTURES="75;80;86;89"  

cmake --build build --config Release --target llama-server -j

Run command:

.\build\bin\Release\llama-server.exe \`  
-m granite.gguf \`  
-ngl 99 \`  
-fa on \`  
-c 8192 \`  
-ctk turbo3 \`  
-ctv turbo3 \`  
--port 8080

A higher-quality asymmetric test could use:

.\build\bin\Release\llama-server.exe \`  
-m granite.gguf \`  
-ngl 99 \`  
-fa on \`  
-c 8192 \`  
-ctk q8_0 \`  
-ctv turbo3 \`  
--port 8080

Granite compatibility: Normal Granite and Granite Hybrid support is inherited from the repository’s llama.cpp base. However, Granite with turbo2, turbo3, turbo4 or TriAttention has not been specifically validated.

Tested Granite model: None documented for the custom TurboQuant or TriAttention paths.

Supported head dimensions: A head dimension divisible by 128 is the natural route. Other dimensions may be zero-padded to the next multiple of 128. Earlier attempts to use weaker 64-value WHT groups caused severe quality problems on some models, so the current padding approach is safer.

Flash Attention: Yes. The repository includes extensive CUDA, Metal and HIP Flash Attention integration for symmetric and asymmetric key/value cache combinations.

Maximum tested context: The TriAttention documentation contains a 131,072-token example. No single independently validated maximum context length is clearly published for all TurboQuant formats and backends.

Published benchmarks: Yes, mainly through README tables and detailed commit results rather than a single formal benchmark report. Models mentioned include Qwen3.5-35B-A3B, Qwen2.5-7B, Llama 3.1 8B, Phi-4, Gemma 2 and GLM-4.7.

Quality tests: Perplexity comparisons, round-trip cosine similarity, generation-output inspection, context-length tests, model-specific debugging and some long-context coherence or retrieval-style checks.

Memory tests: Yes. The active formats provide approximately 7.53× compression for turbo2, 5.12× for turbo3 and 3.8× for turbo4 before considering head-dimension padding or layer-adaptive use.

Speed tests: Yes. The repository reports prompt-processing and generation tests on CUDA and Metal, as well as TriAttention pruning overhead. Reported results should be treated as hardware- and model-specific rather than guaranteed general speedups.

Known bugs and risks:

- Earlier turbo3 builds produced catastrophic perplexity, showing that incorrect graph rotation could produce fast but unusable output.

- Non-128-aligned attention heads initially suffered severe quality degradation.

- A CUDA block-size change caused out-of-bounds cache writes before being fixed.

- Some interleaved sliding-window attention models initially omitted required TurboQuant graph transformations.

- Mixed key/value formats previously had silent dispatch failures before additional kernels were added.

- TriAttention’s documented calibration process appears incomplete or inconsistent.

- The default branch is experimental and contains rapidly changing research features.

Several of these problems have been fixed, but they demonstrate the need for independent Granite quality and stability testing.

Files containing main implementation:

ggml/src/ggml-common.h  
ggml/src/ggml-turbo-quant.c  
ggml/src/ggml-cuda/turbo-quant.cuh  
ggml/src/ggml-cuda/turbo-wht.cu  
ggml/src/ggml-cuda/turbo-innerq.cuh  
ggml/src/ggml-cuda/set-rows.cu  
ggml/src/ggml-cuda/fattn.cu  
ggml/src/ggml-cuda/fattn-common.cuh  
ggml/src/ggml-metal/  
ggml/src/ggml-hip/  
src/llama-kv-cache.cpp  
src/llama-graph.cpp  
src/llama-context.cpp  
src/llama-triattention.cpp  
src/llama-triattention.h  
common/arg.cpp  
docs/TRIATTENTION.md

Ease of integration: Medium for using the repository as a separate CUDA llama-server process. Difficult for integrating the code directly into the application or porting the custom WHT, cache-writing and Flash Attention kernels to Vulkan, SYCL or OpenVINO.

Maintenance risk: High. The repository is feature-rich but experimental, uses a feature branch as its default branch and has undergone frequent corrections to cache layout, attention transformations, head-dimension handling and backend kernels.

How we could use it: Run IBM Granite GGUF on the Lenovo RTX 4060 and compare FP16, q8_0, turbo4, turbo3 and turbo2 KV caches. It could also be used to test asymmetric configurations such as q8_0 keys with turbo3 or turbo2 values, and to study layer-adaptive compression. Its WHT, packed cache structures and attention-graph changes could provide technical guidance when designing a future Intel Vulkan or SYCL port.

TriAttention could be investigated later as a separate long-context optimisation, but it should not be included in the first implementation stage because its calibration workflow and Granite compatibility are not sufficiently established.

Final recommendation: Use as an important secondary CUDA research and benchmarking implementation. It is one of the strongest practical repositories for comparing two-, three- and four-bit WHT-based PolarQuant-style KV-cache compression on the RTX 4060. Do not use it as the main Intel, Vulkan, SYCL, OpenVINO or NPU backend. Use its implementation patterns and benchmark results to guide the Intel design, while validating all Granite quality, memory and performance results independently.
