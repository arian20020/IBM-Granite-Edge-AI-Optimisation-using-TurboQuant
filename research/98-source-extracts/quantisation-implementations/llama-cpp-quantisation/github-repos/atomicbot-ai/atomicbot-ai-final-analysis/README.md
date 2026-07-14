Repository: atomic-llama-cpp-turboquant  
URL: <https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant>  
Owner: AtomicBot-ai  
Branch: feature/turboquant-kv-cache  
Commit: b0e900a28ee4172bbb97df0d1ea1c78e86bc0ac6  
Last relevant update: 17 June 2026  
Licence: MIT

Purpose: Provide TurboQuant KV-cache and model-weight compression inside llama.cpp, with CPU, Vulkan, CUDA, Metal and partial ROCm support. The Vulkan route makes it relevant to Intel GPUs.

Claimed method: Walsh–Hadamard rotation followed by low-bit PolarQuant or Lloyd–Max codebook quantisation, with hardware-specific kernels.

Actual method: Rotates KV vectors in 128-value groups, stores each vector’s norm and physically packs 2-bit, 3-bit or 4-bit codebook indices. It also includes automatic precision fallbacks for sensitive models and layers.

Difference from formal TurboQuant: The default formats do not use the complete QJL correction stage. turbo2 and turbo3 are MSE-focused formats without QJL, while default turbo4 uses direct 4-bit PolarQuant. A legacy turbo4 mode can use 3-bit PolarQuant plus 1-bit QJL.

Weight quantisation: Yes. TQ3_1S and TQ4_1S use WHT rotation and Lloyd–Max codebooks.

KV-cache quantisation: Yes; main feature.

Key-cache types: turbo2, turbo3, turbo4, q8_0 and standard llama.cpp types.

Value-cache types: turbo2, turbo3, turbo4, q8_0 and standard llama.cpp types.

Bits per value: turbo2 is approximately 2.1 effective bits, turbo3 approximately 3.1 bits and turbo4 approximately 4.25 bits after block metadata. TQ3_1S weights use approximately 4 effective bits and TQ4_1S approximately 5 effective bits.

Block structure: KV-cache formats use 128-value blocks. TQ3_1S and TQ4_1S weights use 32-value blocks.

Metadata overhead: FP16 vector norms, packed codebook indices and, in legacy QJL mode, a residual norm and one-bit correction signs.

Actual memory packing: Yes; the cache is physically stored in packed low-bit blocks.

CPU support: Yes, including Intel CPUs, but mainly as a correctness/reference route rather than a high-performance implementation.

CUDA support: Yes. turbo3 and turbo4 have the strongest CUDA support; turbo2 uses a more limited reference path.

ROCm support: Partial. turbo3 KV-cache support and mixed F16-key/TurboQuant-value routes are documented.

SYCL support: General llama.cpp SYCL code may exist, but no completed TurboQuant-specific SYCL route is documented.

OpenVINO support: No TurboQuant/OpenVINO integration.

NPU support: No Intel NPU TurboQuant route.

Supported and unsupported routes:

Normal Granite GGUF + Intel CPU  
→ Supported

Granite + TurboQuant + Intel CPU  
→ Supported as a slower reference route

Granite + Intel GPU through Vulkan  
→ Potentially supported

Granite + turbo3 through Intel Vulkan  
→ Main route worth testing

Granite + TurboQuant through Intel SYCL  
→ Not currently supported

Granite + TurboQuant through OpenVINO  
→ Not supported

Granite + TurboQuant through Intel NPU  
→ Not supported

Windows build: Yes. Prebuilt Windows packages are documented for CPU, Vulkan and CUDA. The relevant Intel package is the Windows x64 Vulkan build.

Required tools: For prebuilt binaries, an updated Intel graphics driver and Vulkan-capable GPU. For source builds, Git, CMake, Visual Studio C++ Build Tools and the Vulkan SDK.

Build command:

git clone -b feature/turboquant-kv-cache <https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant.git>  
cd atomic-llama-cpp-turboquant  
cmake -S . -B build -DGGML_VULKAN=ON -DLLAMA_BUILD_SERVER=ON  
cmake --build build --config Release -j

Run command:

llama-server.exe -m granite.gguf -ngl 99 -c 8192 \`  
-ctk turbo3 -ctv turbo3 -fa on

Granite compatibility: General Granite GGUF support is listed through llama.cpp, but Granite with TurboQuant on Intel Vulkan has not been confirmed.

Tested Granite model: None documented.

Supported head dimensions: Native transform groups are 128 values. Other head dimensions are padded to the next multiple of 128.

Flash Attention: Yes. The accelerated Vulkan turbo3 route includes Flash Attention, but it requires compatible GPU and driver features.

Maximum tested context: A 32,768-token example is documented. No maximum is published specifically for Granite on Intel hardware.

Published benchmarks: Yes, mainly Gemma and Qwen models, with much of the detailed performance testing carried out on Apple Metal hardware rather than Intel Vulkan.

Quality tests: Perplexity, generation tests and speculative-decoding acceptance measurements.

Memory tests: Yes. Claimed KV-cache compression is approximately 6.4× for turbo2, 4.3× for turbo3 and 3.8× for turbo4.

Speed tests: Yes, but no published Granite plus Intel Vulkan speed results.

Known bugs: TurboQuant key compression can severely reduce quality on models with high grouped-query-attention ratios, so the implementation can automatically change keys to q8_0. Flash Attention or advanced Vulkan features may also fail on some Intel GPUs even when basic Vulkan works.

Files containing main implementation:  
ggml/src/ggml-common.h  
ggml/src/ggml-quants.c  
ggml/src/ggml-vulkan/  
src/llama-kv-cache.cpp  
common/arg.cpp

Ease of integration: Medium. Easiest through the prebuilt llama-server and its local HTTP API.

Maintenance risk: Medium to high. It is an active feature branch with backend-specific behaviour, automatic fallback rules and several unrelated experimental features.

How we could use it: Test Granite GGUF on Intel CPU and Intel Vulkan GPU, comparing F16, q8_0, turbo4, turbo3 and turbo2. turbo3 Vulkan should be the main candidate, with CPU as the slower fallback.

Final recommendation: High-priority repository for practical testing. It is currently one of the strongest candidates for running TurboQuant on low-end Intel PCs through Vulkan, but Granite compatibility, Intel GPU feature support, speed, memory savings and quality must all be validated experimentally.
