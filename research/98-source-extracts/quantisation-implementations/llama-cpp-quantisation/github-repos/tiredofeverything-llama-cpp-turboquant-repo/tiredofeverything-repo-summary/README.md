# **Overall summary: TiredOfEverything/llama-cpp-turboquant**

## **Repository overview**

**Repository:** llama-cpp-turboquant  
**URL:** <https://github.com/TiredOfEverything/llama-cpp-turboquant>  
**Owner:** TiredOfEverything  
**Branch:** master  
**Current inspected commit:** 71ecbd7e82a4c7fd39b9d079a6fb40b9a6f5d5c1  
**Last relevant update:** 29 March 2026  
**Licence:** MIT

The latest commit specifically corrected Windows compilation problems and was tested using both Clang and Microsoft Visual C++.

## **1. What this repository is**

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

# **2. Available cache formats**

The current source defines three TurboQuant-style formats:

| **Format** | **Physical storage**    | **Effective precision** | **Compression vs FP16** |
|------------|-------------------------|-------------------------|-------------------------|
| turbo2     | 10 bytes per 32 values  | 2.5 bits/value          | 6.4×                    |
| turbo3     | 14 bytes per 32 values  | 3.5 bits/value          | 4.57×                   |
| turbo4     | 66 bytes per 128 values | 4.125 bits/value        | approximately 3.88×     |

Some README figures describe these as 3.25-bit and 4.25-bit formats. Those descriptions are outdated. The current block structures show that the active formats use 3.5 and 4.125 effective bits per value.

## **Turbo2**

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

## **Turbo3**

Turbo3 uses eight Lloyd–Max centroids. Each three-bit index is split into:

Lower two bits  
→ qs array  

Upper one bit  
→ signs array

Despite the field being called signs, it is not QJL residual information. It is simply the third bit of the ordinary eight-level codebook index.

## **Turbo4**

The current Turbo4 implementation uses:

16 Lloyd–Max centroids  
+  
four-bit packed indices  
+  
one corrected FP16 norm

The current source explicitly states that QJL signs were removed and that Turbo4 now uses pure four-bit PolarQuant-style quantisation.

# **3. How the actual algorithm works**

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

## **Norm correction**

One of the repository’s most important additions is norm correction:

corrected norm  
=  
original group norm ÷ reconstructed centroid-vector norm

This means that when the quantised vector is reconstructed, its overall length remains close to the original vector’s length.

The CUDA Turbo3 kernel calculates one corrected norm across the full 128-value group and writes it to the four associated 32-value blocks.

Turbo4 applies the same broad principle to its single 128-value block.

This is not QJL. It is an additional scaling correction intended to preserve attention-score magnitude.

# **4. Does it implement formal TurboQuant?**

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

# **5. CUDA implementation**

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

## **Prefill optimisation**

The repository can temporarily dequantise Turbo3 KV data into FP16 buffers during prompt processing and then use NVIDIA tensor-core MMA kernels.

The reported result for Turbo3 was:

Old vector-kernel prefill  
→ approximately 631 tokens/s  

Dequantise + tensor-core MMA  
→ approximately 1,121 tokens/s

The decode path continues using direct low-bit cache access.

The equivalent optimisation was not retained for the older QJL-based Turbo4 experiment because the small correction signal was lost during the FP16 round trip. Current source has since redesigned Turbo4 without QJL, which illustrates that parts of the benchmark documentation refer to an older format revision.

# **6. Layer-adaptive compression**

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

# **7. CPU and other backends**

## **CPU**

Normal llama.cpp CPU inference works, but full TurboQuant CPU execution is incomplete.

The CPU reference code shows:

- Turbo2 quantisation only calculates a norm and writes zero indices;

- Turbo3 quantisation is explicitly described as a simplified stub;

- Turbo4 has a more complete CPU rotation, quantisation and inverse-rotation implementation.

Therefore, this should not be considered a complete CPU TurboQuant implementation.

## **Metal**

The fork inherited substantial Apple Metal TurboQuant work from TheTom’s implementation. The repository’s own documentation says that Metal shaders, CLI integration and graph scaffolding already existed before this fork added CUDA support.

## **Intel and other routes**

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

# **8. Windows support**

The latest commit fixes Windows compilation problems and was tested with:

- Microsoft Visual C++;

- Clang on Windows.

A suitable NVIDIA CUDA build is:

git clone <https://github.com/TiredOfEverything/llama-cpp-turboquant.git>  
cd llama-cpp-turboquant  

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

# **9. Published results**

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

# **10. Known risks**

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

# **11. Granite relevance**

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

# **12. Relevance to the application**

## **Direct implementation relevance**

Main Intel backend  
→ No  

Vulkan backend candidate  
→ No  

Intel SYCL candidate  
→ No  

OpenVINO or NPU candidate  
→ No

## **Research relevance**

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

# **Final assessment**

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
