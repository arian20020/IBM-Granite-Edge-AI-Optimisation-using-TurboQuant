---
title: "atomicmilkshake llama-cpp-turboquant"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/GitHub repos/atomicmilkshake lla-cpp-turboquant/atomicmilkshake llama-cpp-turboquant.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

# **Repository analysis: atomicmilkshake/llama-cpp-turboquant**

## **Repository snapshot**

**Repository:** llama-cpp-turboquant  
**URL:** <https://github.com/atomicmilkshake/llama-cpp-turboquant>  
**Owner:** atomicmilkshake  
**Default branch:** feature/triattention  
**Current inspected commit:** 75016369b38c841da095244d07ab86453023b11a  
**Last code/documentation update on the default branch:** 8 April 2026  
**Licence:** MIT

The repository deliberately uses an experimental feature branch as its default. Its branches are organised as follows:

feature/triattention  
→ TurboQuant + TriAttention  
  
feature/turboquant-kv-cache  
→ TurboQuant without TriAttention  
  
master  
→ Upstream llama.cpp base

# **1. Overall purpose**

This is a heavily modified llama.cpp fork with two separate KV-cache optimisation systems:

1.  **TurboQuant-style low-bit KV-cache compression**

2.  **TriAttention KV-cache token pruning**

These solve different problems.

TurboQuant  
→ Keeps every cached token  
→ Stores each token using fewer bits  
  
TriAttention  
→ Limits how many cached tokens are retained  
→ Removes tokens judged to be less important

They can theoretically be combined:

Fewer bits per token  
+  
Fewer tokens retained  
=  
Much lower KV-cache memory

The repository describes this combination as providing up to roughly 40× “effective” KV-cache reduction, although that is a repository claim and requires independent quality validation.

# **2. The three TurboQuant cache formats**

The repository implements:

- turbo2_0

- turbo3_0

- turbo4_0

The current source code uses 128-value storage blocks for all three formats.

| **Format** | **Stored data per 128 values** | **Effective bits/value** | **FP16 compression** |
|------------|--------------------------------|--------------------------|----------------------|
| turbo2_0   | 34 bytes                       | 2.125                    | approximately 7.53×  |
| turbo3_0   | 50 bytes                       | 3.125                    | approximately 5.12×  |
| turbo4_0   | 68 bytes                       | 4.25                     | approximately 3.76×  |

The current source contains some outdated comments describing turbo2 and turbo3 as 32-value, 10-byte and 14-byte blocks. However, the active macros and array sizes now define 128-value blocks. Therefore, the values in the table above are calculated from the actual active structures, not the stale comments.

The block size was increased from 32 to 128 so that only one FP16 norm is stored per full transform group. The project reports that this changed:

turbo3:  
3.50 → 3.125 bits/value  
4.57× → 5.12× compression  
  
turbo2:  
2.50 → 2.125 bits/value  
6.40× → 7.53× compression

The author reports no perplexity regression from this storage-layout change across several models and context lengths.

# **3. How the TurboQuant formats work**

For turbo2 and turbo3, the practical process is:

Take one 128-value KV vector group  
↓  
Calculate its L2 norm  
↓  
Divide the vector by its norm  
↓  
Apply fixed random positive/negative signs  
↓  
Apply a Walsh–Hadamard Transform  
↓  
Approximate each transformed coordinate  
using a small Lloyd–Max codebook  
↓  
Calculate a corrected reconstruction norm  
↓  
Pack the low-bit indices and the norm

The Walsh–Hadamard rotation spreads unusual values and outliers more evenly through the vector. The codebook then has an easier distribution to approximate.

The repository uses deterministic sign patterns and a normalised 128-value WHT. Its CUDA implementation contains separate 2-bit and 3-bit centroid tables and the transform operations needed to rotate values before cache storage.

## **Turbo3**

turbo3_0 uses eight centroids, requiring three bits for each transformed coordinate.

The three-bit index is divided into:

Lower two bits  
→ qs array  
  
Upper one bit  
→ signs array

Despite the array being named signs, this is not QJL residual information. It is simply the third bit of the normal eight-level codebook index.

## **Turbo2**

turbo2_0 uses four centroids and stores each coordinate using two bits. It performs the same normalisation and WHT preparation but does not store any residual correction.

## **Turbo4**

The current default turbo4_0 format uses:

WHT or rotation preparation  
↓  
16 codebook centroids  
↓  
4-bit packed indices  
↓  
No QJL

A legacy 3-bit plus one-bit QJL layout remains in the code behind a compile-time conditional, but it is not the active default. The current default is four-bit PolarQuant-style quantisation without QJL.

The project deliberately replaced the older 3-bit-plus-QJL design because its testing found that the straightforward four-bit version gave better quality and was simpler to implement.

# **4. Does it implement formal TurboQuant?**

It is significantly closer to formal TurboQuant than simpler repositories such as thepradip/turboquant-llamacpp.

It genuinely includes:

- vector normalisation;

- randomised orthogonal preconditioning;

- WHT-based rotation;

- Gaussian-oriented codebooks;

- low-bit coordinate packing;

- physical KV-cache compression;

- attention-graph changes required to handle rotated keys and values.

However, the formats used by default do **not** implement the complete two-stage formal algorithm:

Stage 1: PolarQuant-style rotated approximation  
→ Implemented  
  
Stage 2: QJL residual correction  
→ Not used by the current default formats

The source still contains generation code for a Gaussian QJL matrix and the older QJL reconstruction path, but the active turbo2, turbo3 and turbo4 configurations omit QJL.

A suitable description is therefore:

This repository implements practical PolarQuant-style KV-cache compression with WHT preconditioning at two, three and four bits. It contains legacy QJL code, but the current default formats do not use QJL because the developers found that it reduced practical quality.

# **5. Rotated attention integration**

Compression is not limited to storing smaller structures. The attention graph must also understand that the keys and values have been transformed.

The repository modifies the graph so that:

- queries are handled consistently with rotated keys;

- attention can calculate scores in the rotated space;

- the attention output is inverse-transformed when values were stored in rotated form;

- mixed key and value formats can be used.

The graph applies an inverse WHT to the Flash Attention result when the value cache uses turbo2, turbo3 or turbo4.

This is important because simply compressing the KV values without adapting attention would produce incorrect results.

# **6. Independent key and value formats**

The repository supports asymmetric cache configurations, for example:

q8_0 keys  
+  
turbo3 values

or:

turbo4 keys  
+  
turbo2 values

This is useful because keys and values do not necessarily have equal sensitivity to low-bit compression.

The project added many Flash Attention combinations for:

- turbo2, turbo3 and turbo4 keys;

- turbo2, turbo3 and turbo4 values;

- mixed q8_0 and TurboQuant combinations.

It reports that asymmetric configurations such as q8_0 keys with compressed values can recover much of the quality lost when both keys and values are compressed aggressively.

# **7. Layer-adaptive compression**

The repository also experiments with using different cache formats in different model layers.

One example is **Boundary V**:

First few value-cache layers  
→ q8_0  
  
Middle value-cache layers  
→ turbo2  
  
Last few value-cache layers  
→ q8_0

The idea is that the first and last layers may be more sensitive, while the middle layers can be compressed more aggressively.

The recommended experimental Boundary V mode protects the first two and last two value-cache layers with q8_0 and uses turbo2 for the rest. Tests on several models showed better quality than using turbo2 uniformly.

Another layer-adaptive test used q8_0 in the final eight layers and turbo3 in the first 32 layers. On the reported Qwen test, it achieved nearly the same perplexity as q8_0 while compressing most layers.

This is valuable for your project because it suggests that the application does not have to choose one cache format for every layer.

# **8. Handling unusual attention-head dimensions**

The WHT works most naturally when the head dimension aligns with a power-of-two transform group.

The project initially attempted a 64-value WHT fallback for non-128-aligned head dimensions. This produced catastrophic quality loss on some models:

- DeepSeek head dimension 192;

- GLM head dimension 576.

The implementation temporarily fell back to q8_0 for these models.

It later changed to zero-padding each attention head to the next multiple of 128:

192 dimensions  
→ padded to 256  
  
576 dimensions  
→ padded to 640

Because the additional coordinates are zero, the transform can still preserve the original inner products. However, the padding consumes some extra cache memory.

This is an important engineering feature for model compatibility, but it means the headline compression ratio may be lower for models whose head dimensions are not already multiples of 128.

# **9. TriAttention**

TriAttention is separate from TurboQuant.

Instead of compressing each cached token, it tries to decide which tokens should remain in the cache.

## **Process**

Run an offline calibration pass  
↓  
Collect expected query statistics  
for selected layers and heads  
↓  
During inference, monitor KV-cache size  
↓  
Score cached tokens when the cache  
passes the configured budget  
↓  
Protect recent and prompt tokens  
↓  
Keep the highest-scoring tokens  
↓  
Evict lower-scoring tokens

The scoring system:

- reverses or accounts for RoPE rotation;

- compares cached keys with calibrated future-query patterns;

- considers multiple RoPE frequency bands;

- estimates how likely each cached token is to receive future attention.

## **Why it might help**

Normal long-context attention keeps every token:

128K-token conversation  
→ 128K KV entries

With a TriAttention budget of 4K:

128K logical context  
→ approximately 4K retained KV entries

The model still has positional gaps, but RoPE can represent non-contiguous positions.

## **TriAttention execution paths**

The repository contains:

- a CPU scoring implementation;

- a custom CUDA scoring kernel;

- KV-cache hooks for triggering pruning;

- command-line and public API integration.

The README reports a Qwen3-8B RTX 3080 test where GPU pruning reduced the scoring cost to approximately 4–9 milliseconds per event and increased generation from 17.5 to 75 tokens per second by avoiding an oversized or stalled KV cache.

That speedup should not be interpreted as a general 4.3× acceleration for every model. It resulted from keeping that particular workload inside its useful VRAM budget.

# **10. Serious TriAttention limitation**

TriAttention requires a model-specific .triattention calibration file.

However, a repository issue reports that the documented:

--triattention-calibrate

option was missing, along with the documented calibration and validation scripts.

The issue was later closed automatically as stale rather than being fixed.

This means that a user may not currently have a complete, reliable workflow for producing a valid calibration file for IBM Granite.

The documentation is also inconsistent:

- the README describes one set of default budget, window and trigger values;

- the detailed TriAttention document describes different defaults;

- the README and detailed document report very different CPU/GPU scoring times.

Therefore, TriAttention should currently be regarded as an experimental research feature rather than a dependable application component.

# **11. Hardware backend support**

## **Actual custom optimisation support**

| **Backend**  | **TurboQuant status**                     | **TriAttention status**                   |
|--------------|-------------------------------------------|-------------------------------------------|
| NVIDIA CUDA  | Main and best-supported route             | GPU scorer implemented                    |
| Apple Metal  | Substantial turbo2/3/4 support            | No equivalent Metal GPU scorer documented |
| AMD HIP/ROCm | TurboQuant port exists and was tested     | No custom HIP scorer clearly documented   |
| CPU          | Reference/fallback operations             | CPU scoring available                     |
| Vulkan       | No custom TurboQuant implementation found | No custom scorer                          |
| Intel SYCL   | No custom TurboQuant implementation found | No custom scorer                          |
| OpenVINO     | No TurboQuant integration                 | No integration                            |
| Intel NPU    | No support                                | No support                                |

The README highlights CUDA and provides Windows CUDA binaries requiring CUDA 13 and an NVIDIA RTX GPU.

Commit history also shows HIP/ROCm work tested on AMD Strix Halo and extensive Metal optimisation.

## **Critical correction for the project**

This repository is **not a custom Vulkan TurboQuant implementation**.

It inherits ordinary Vulkan and SYCL code from llama.cpp, but the repository’s custom TurboQuant formats are not connected to either backend.

Normal GGUF + inherited Vulkan  
→ May work  
  
Turbo2/3/4 + Vulkan  
→ No custom implementation found  
  
Normal GGUF + inherited SYCL  
→ May work  
  
Turbo2/3/4 + Intel SYCL  
→ No custom implementation found

Therefore, this repository cannot currently be your primary Intel Vulkan backend.

# **12. Quality and performance evidence**

The repository contains much more quality investigation than most experimental forks.

A reported M5 Max test using Qwen3.5-35B-A3B found:

| **Cache** | **Relative quality** | **Decode speed** |
|-----------|----------------------|------------------|
| q8_0      | baseline             | baseline         |
| turbo4    | +0.23% perplexity    | 0.93× q8_0       |
| turbo3    | +1.06% perplexity    | 0.90× q8_0       |

An earlier turbo2 test reported approximately 6.48% perplexity degradation relative to q8_0, which illustrates the greater quality risk of two-bit compression.

The repository has also identified and corrected serious errors, including:

- turbo3 producing perplexity of 165.6 instead of approximately 6.1;

- incorrect attention behaviour for interleaved sliding-window models;

- out-of-bounds CUDA writes after changing block sizes;

- failures on non-128-aligned head dimensions;

- prompt-cache serialization errors caused by padded cache widths.

Finding and fixing these problems is positive, but their frequency also shows that this is an actively changing experimental implementation.

# **13. Granite compatibility**

The inherited llama.cpp version contains support for:

- standard Granite models;

- Granite Hybrid models;

- Granite Vision-related components;

- Granite chat templates.

Recent inherited changes also include fixes for Granite Hybrid model detection and Granite Flash Attention.

However, no documented benchmark confirms:

Granite  
+  
turbo2, turbo3 or turbo4  
+  
quality and stability testing

Therefore:

Normal Granite GGUF loading  
→ Likely supported through inherited llama.cpp  
  
Granite + CUDA TurboQuant  
→ Plausible but not validated  
  
Granite + Intel TurboQuant  
→ Not supported by a custom backend  
  
Granite + TriAttention  
→ Not validated and lacks a complete calibration workflow

For Granite Hybrid models, TurboQuant would only reduce the attention KV cache. It would not automatically compress every recurrent or state-space buffer in the hybrid architecture. Consequently, the total runtime-memory reduction could be lower than for a pure transformer model.

# **14. Relevance to your application**

## **Direct implementation relevance**

Intel Vulkan backend  
→ Low relevance  
  
Intel SYCL backend  
→ Low relevance  
  
OpenVINO backend  
→ Low relevance  
  
Intel NPU  
→ No relevance currently

The custom performance kernels are primarily CUDA, Metal and HIP-focused.

## **Research relevance**

Practical PolarQuant design  
→ High relevance  
  
2-bit, 3-bit and 4-bit comparison  
→ High relevance  
  
Asymmetric K/V compression  
→ High relevance  
  
Layer-adaptive compression  
→ High relevance  
  
Non-standard head-dimension handling  
→ High relevance  
  
Long-context token pruning  
→ Potentially high future relevance

## **NVIDIA comparison route**

The repository is highly useful for testing on the Lenovo RTX 4060 because:

- prebuilt Windows CUDA binaries exist;

- TurboQuant cache formats are implemented through CUDA;

- Flash Attention integration is extensive;

- turbo2, turbo3 and turbo4 can be compared;

- it provides an advanced NVIDIA reference against which an Intel port could later be measured.

# **15. Recommended role in the project**

This repository should be classified as:

Main Intel application backend  
→ No  
  
Vulkan implementation candidate  
→ No  
  
SYCL implementation candidate  
→ No  
  
CUDA research implementation  
→ Yes  
  
Granite RTX 4060 benchmark candidate  
→ Yes  
  
Reference for porting features to Intel  
→ Yes  
  
TriAttention production component  
→ Not yet

## **Final recommendation**

atomicmilkshake/llama-cpp-turboquant is one of the most technically developed practical TurboQuant forks examined so far. It provides genuine WHT-based PolarQuant-style compression at two, three and four bits, extensive Flash Attention integration, asymmetric K/V configurations, layer-adaptive compression and experimental KV-token pruning through TriAttention. However, the current formats do not use QJL, the custom optimisations are centred on CUDA, Metal and HIP rather than Intel Vulkan or SYCL, Granite has not been validated, and TriAttention currently lacks a dependable calibration workflow. It should therefore be retained as a high-value research and RTX 4060 comparison implementation, but not used as the main Intel application backend.
