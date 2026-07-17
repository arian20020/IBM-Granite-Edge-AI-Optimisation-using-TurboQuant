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
