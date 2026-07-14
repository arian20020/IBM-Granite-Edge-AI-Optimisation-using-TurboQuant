---
title: "animehacker repo review"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/GitHub repos/animehacker repo/animehacker repo review.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

# Detailed review: animehacker/llama-turboquant

## Main conclusion

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

# 1. What the repository implements

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

# 2. How TQ3_0 works

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

# 3. What the rotation accomplishes

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

# 4. This is not complete formal TurboQuant

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

# 5. How it works through Intel SYCL

The SYCL port adds the operations required to store and retrieve TQ3_0 blocks on an Intel GPU.

## Writing to the cache

When the model generates new keys and values:

New F32 KV values  
↓  
SYCL SET_ROWS operation  
↓  
WHT and 3-bit quantisation on the Intel GPU  
↓  
Packed TQ3_0 block stored in GPU/shared memory

SET_ROWS is essential because llama.cpp uses it when inserting newly generated values into the KV cache. An earlier version of the port could convert TQ3_0 but aborted during actual inference because this cache-write operation was missing. It was subsequently added and tested.

## Reading from the cache

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

# 6. It saves memory but still dequantises for attention

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

# 7. Intel validation already completed

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

# 8. Flash Attention requires clarification

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

# 9. Granite compatibility

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

# 10. Windows implementation route

The repository includes a Windows SYCL build script that:

- loads the Intel oneAPI environment;

- enables the SYCL backend;

- uses Ninja;

- builds the llama.cpp tools and server.

A corrected Windows build sequence for this actual repository would be:

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

# 11. Proposed application integration

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

## Device-selection outcomes

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

# 12. What we should test first

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

# 13. Main risks

### Limited Intel validation

Only one custom TQ3_0 Intel configuration is documented: Battlemage B70. Integrated Arc, Iris Xe, Meteor Lake and Lunar Lake remain unverified for this custom format.

### No Granite benchmark

The successful Qwen test proves the Intel kernel works, but not that Granite will preserve the same quality.

### Additional dequantisation overhead

The cache remains compressed, but the attention path reconstructs values during use. This explains the reported generation slowdown.

### Experimental maintenance state

An open HIP issue reports a segmentation fault with TQ3_0 on an AMD RX 9060 XT and Gemma 3. Another CUDA issue required an additional GPU copy path and was closed after becoming stale rather than after confirmed user validation.

### Documentation inconsistency

The README, current source and merged SYCL PR do not always describe Flash Attention and QJL consistently. We need to rely on the exact tested commit rather than general README claims.

# Final recommendation

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
