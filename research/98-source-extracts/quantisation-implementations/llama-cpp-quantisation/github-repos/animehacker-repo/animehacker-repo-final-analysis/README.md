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
