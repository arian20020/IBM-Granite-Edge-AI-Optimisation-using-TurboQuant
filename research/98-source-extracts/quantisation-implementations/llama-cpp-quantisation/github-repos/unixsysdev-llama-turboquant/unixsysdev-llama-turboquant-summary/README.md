---
title: "unixsysdev llama-turboquant Summary"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/GitHub repos/unixsysdev llama-turboquant/unixsysdev llama-turboquant Summary.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

# **Repository analysis: unixsysdev/llama-turboquant**

**Repository:** llama-turboquant  
**URL:** <https://github.com/unixsysdev/llama-turboquant>  
**Owner:** unixsysdev  
**Branch:** main  
**Current commit:** 03fa8abc4708dfc13858de0a74695075702c8e26  
**Last relevant update:** 25 March 2026  
**Licence:** MIT

## **Main conclusion**

This repository is the **original base implementation behind the animehacker fork** that we already examined.

It adds one format:

TQ3_0

However, its README overstates how closely it follows formal TurboQuant. The source calculates and stores one-bit residual signs, but those residual bits are not used during CUDA dequantisation or the fused attention dot product. Therefore, the repository does **not actually apply QJL residual correction during inference**.

It is also limited to CPU, CUDA and HIP/ROCm. There is no custom Vulkan, SYCL, OpenVINO or Intel NPU implementation.

For our application, this repository is therefore largely superseded by animehacker/llama-turboquant, which is based on this exact commit but later corrects the quantisation description and adds Intel SYCL support.

# **1. What the repository claims to implement**

The README describes TQ3_0 as a combination of:

Walsh–Hadamard rotation  
+  
2-bit Lloyd–Max codebook  
+  
1-bit QJL residual correction

Each block contains 32 values and is stored in 14 bytes:

| **Field** | **Size**     | **Claimed purpose**                 |
|-----------|--------------|-------------------------------------|
| qs\[8\]   | 8 bytes      | Thirty-two 2-bit codebook indices   |
| qr\[4\]   | 4 bytes      | Thirty-two 1-bit QJL residual signs |
| gamma     | 2 bytes      | FP16 scale                          |
| **Total** | **14 bytes** | **3.5 bits per value**              |

Compared with 64 bytes for 32 FP16 values, this provides:

64 ÷ 14 ≈ 4.57× K-cache compression

# **2. What the source code actually does**

The actual GPU quantisation process is:

32 original key-cache values  
↓  
Apply fixed positive and negative sign changes  
↓  
Apply a 32-value Walsh–Hadamard Transform  
↓  
Find the maximum absolute transformed value  
↓  
Calculate scale = maximum ÷ 1.510  
↓  
Map every value to one of four centroids  
↓  
Store each centroid index using 2 bits  
↓  
Calculate the sign of each elementwise residual  
↓  
Store those signs in qr

The four codebook values are approximately:

-1.510  
-0.453  
+0.453  
+1.510

The GPU quantiser sets gamma to the main scalar-quantisation scale and stores whether each residual is positive or negative.

## **Critical problem: the residual bits are not used**

During GPU dequantisation, the implementation reads:

- qs;

- gamma.

It does **not** read qr. It reconstructs the four-level approximation and applies the inverse WHT, but never adds a residual correction.

The fused query-key dot-product kernel also reads:

- qs;

- gamma.

It likewise ignores qr.

A repository-wide source search finds the qr field only in:

- the block definition;

- the README;

- the quantisation code that writes it.

No inference code was found that reads those bits and converts them into an attention-score correction.

Therefore, the practical representation is:

Useful information:  
2-bit centroid indices  
+  
FP16 scale  
  
Stored but currently unused:  
1-bit residual signs

Without the unused four residual bytes, the structure would require only ten bytes per 32 values, or 2.5 bits per value. Instead, the repository stores 3.5 bits per value while receiving no demonstrated QJL benefit from the additional bit.

# **3. Why this is not genuine QJL**

Formal QJL residual correction requires more than recording whether each direct residual coordinate is positive or negative.

The intended process is broadly:

Calculate the residual vector  
↓  
Apply a separate random projection to the residual  
↓  
Store the signs of the projected values  
↓  
Store the residual norm  
↓  
Project the query in the same way  
↓  
Use both sign sketches to estimate the lost inner product

This repository does not provide that complete process:

- no separate QJL projection matrix is applied;

- the signs are taken directly from elementwise residuals;

- gamma stores the main quantiser scale rather than a separately used residual norm;

- the query-key dot product never reads the residual signs;

- no QJL correction is added to the attention score.

The source comment describes gamma as a residual norm, but the actual GPU quantisation code assigns it the primary scale amax / 1.510.

This repository is therefore more accurately described as:

**A 2-bit WHT-preconditioned scalar K-cache quantiser stored in a 3.5-bit structure, with residual-sign metadata that is written but not used during inference.**

# **4. Difference from the PolarQuant stage**

The repository does implement a useful form of random preconditioning:

Fixed sign changes  
+  
WHT32

However, the WHT is applied independently to every 32-value block. Formal PolarQuant applies its random preconditioning across the vector or attention-head dimension.

The README openly acknowledges this difference:

- the paper uses a full d × d rotation;

- this implementation uses separate 32 × 32 transforms;

- the smaller transform avoids changing the full attention graph;

- it is an engineering approximation rather than an exact paper implementation.

Therefore:

Random/WHT preconditioning  
→ Implemented in simplified 32-value blocks  
  
Scalar Gaussian codebook quantisation  
→ Implemented  
  
Full head-dimensional PolarQuant transformation  
→ Not implemented exactly  
  
QJL residual correction  
→ Not functionally implemented

# **5. Attention calculation**

The repository provides a useful fused CUDA/HIP query-key operation.

Because WHT is orthogonal:

q · k = WHT(q) · WHT(k)

The implementation transforms the query inside the fused dot-product kernel and calculates the attention score directly against the compressed, rotated key representation. This avoids reconstructing the complete original key vector before every dot product.

This is a useful engineering idea and helps explain why its reported throughput remains close to Q4_0 despite the extra transform.

However, the fused operation only uses the two-bit centroid approximation. It does not include the stored residual signs.

# **6. Key cache versus value cache**

This implementation is mainly a **key-cache format**.

The documented command is:

--cache-type-k tq3_0

The README also suggests combining TQ3_0 keys with a quantised value cache:

--cache-type-k tq3_0 --cache-type-v q8_0

However, that combination is not currently functional in normal server operation.

## **Flash Attention conflict**

The code automatically disables Flash Attention when TQ3_0 is used for keys:

TQ3_0 key cache  
↓  
Flash Attention forced off

But llama.cpp requires Flash Attention for quantised value-cache storage:

Quantised value cache  
↓  
Flash Attention required

This creates a conflict:

TQ3_0 K + q8_0 V  
↓  
TQ3_0 disables Flash Attention  
↓  
q8_0 V requires Flash Attention  
↓  
Context creation fails

The practical working configuration is therefore:

TQ3_0 keys  
+  
FP16 values

A user confirmed that llama-bench worked on an RTX 5090, but llama-server failed with a quantised value cache. The issue was closed automatically as stale rather than being fixed.

If keys and values occupy approximately equal memory, compressing only keys by 4.57× reduces the **complete KV cache** by only about:

Total original size:  
1 K + 1 V = 2 units  
  
Compressed:  
0.219 K + 1 V = 1.219 units  
  
Overall reduction:  
2 ÷ 1.219 ≈ 1.64×

Therefore, the headline 4.57× result applies to the key cache, not necessarily to total KV-cache memory.

# **7. Backend support**

## **CPU**

A CPU quantisation and dequantisation path exists. The README describes it as functional but slower than GPU execution.

## **CUDA**

Custom CUDA code exists for:

- TQ3_0 cache writing;

- WHT transformation;

- dequantisation;

- fused query-key dot products;

- MMVQ dispatch.

CUDA benchmarking has been confirmed by a user on NVIDIA Blackwell, although the server/value-cache problem remains.

## **HIP/ROCm**

The same shared GPU implementation can be compiled through HIP. The repository’s own published tests used an AMD Radeon 8060S with ROCm 7.2.

## **Unsupported custom routes**

Repository-wide TQ3_0 references are confined to:

- common type registration;

- CPU;

- CUDA/HIP;

- llama.cpp integration and benchmark code.

No custom implementation appears in:

- Vulkan;

- SYCL;

- Metal;

- OpenVINO;

- Intel NPU code.

The supported route summary is:

Normal llama.cpp CPU  
→ Supported  
  
TQ3_0 + CPU  
→ Functional fallback  
  
Normal NVIDIA CUDA  
→ Supported  
  
TQ3_0 + CUDA  
→ Supported for K-cache benchmarking and inference  
  
Normal AMD HIP/ROCm  
→ Supported  
  
TQ3_0 + HIP/ROCm  
→ Implemented and benchmarked  
  
TQ3_0 + Vulkan  
→ Not implemented  
  
TQ3_0 + Intel SYCL  
→ Not implemented  
  
TQ3_0 + OpenVINO  
→ Not implemented  
  
TQ3_0 + Intel NPU  
→ Not implemented

# **8. Published results**

The repository reports tests on an AMD Radeon 8060S using Qwen3.5-0.8B.

One perplexity comparison reports:

| **Cache** | **Perplexity** | **Change** |
|-----------|----------------|------------|
| FP16      | 15.49          | Baseline   |
| TQ3_0     | 16.20          | +4.6%      |

A second run using a Q5_K_M model reports:

| **Cache** | **Perplexity** | **Change** |
|-----------|----------------|------------|
| FP16      | 20.05          | Baseline   |
| Q4_0      | 20.14          | +0.4%      |
| TQ3_0     | 21.21          | +5.8%      |

Generation speed was reported as:

| **Cache** | **Generation speed** |
|-----------|----------------------|
| FP16      | 181.8 tokens/s       |
| Q4_0      | 179.1 tokens/s       |
| TQ3_0     | 177.9 tokens/s       |

Thus, TQ3_0 was approximately 2.1% slower than FP16 in that particular test, but its quality loss was significantly larger than ordinary Q4_0.

# **9. Granite compatibility**

The inherited llama.cpp code recognises Granite architectures.

However:

Normal Granite GGUF  
→ Likely supported through inherited llama.cpp  
  
Granite + TQ3_0 K cache  
→ Not documented or tested  
  
Granite + TQ3_0 + Intel GPU  
→ Not supported  
  
Granite + TQ3_0 K + quantised V  
→ Blocked by the Flash Attention limitation

For Granite Hybrid models, the compression would apply only to transformer attention KV states. It would not automatically compress recurrent or state-space memory, so total application memory savings could be smaller.

# **10. Relationship with the animehacker repository**

This is the most important point for our repository selection.

The current unixsysdev head commit:

03fa8abc4708dfc13858de0a74695075702c8e26

also appears in the animehacker repository’s history, confirming that animehacker was built on top of this implementation.

The later animehacker lineage then:

1.  corrected the misleading QJL description;

2.  converted the extra bit into the upper bit of a genuine 3-bit, eight-centroid index;

3.  explicitly documented that QJL was not implemented;

4.  added an Intel SYCL path;

5.  added the SET_ROWS support needed for Intel KV-cache writes.

Therefore:

unixsysdev  
→ Older original implementation  
→ Misleading QJL claim  
→ CPU/CUDA/HIP only  
  
animehacker  
→ Newer corrected format  
→ Honest PolarQuant-only description  
→ Adds Intel SYCL

# **Relevance to the application**

## **Useful parts**

The repository remains useful for studying:

- how a custom KV-cache type is registered in GGML;

- fixed-sign WHT32 preconditioning;

- physically packed low-bit key-cache storage;

- fused query transformation and key dot products;

- CUDA/HIP cache-writing kernels;

- memory and perplexity comparisons.

## **Limited direct relevance**

It is not a strong implementation candidate because:

- there is only one custom format;

- the actual code does not use QJL;

- its one residual bit appears wasted;

- it compresses mainly the key cache;

- quantised values are incompatible with its no-Flash-Attention route;

- it has no Vulkan or Intel SYCL support;

- its unresolved issues were closed as stale;

- the animehacker fork already provides a newer version of the same lineage.

# **Final recommendation**

Main Intel backend  
→ No  
  
Vulkan candidate  
→ No  
  
SYCL candidate  
→ No; animehacker supersedes it  
  
CUDA/HIP research reference  
→ Moderate value  
  
Separate implementation test  
→ Not necessary

**The unixsysdev repository should not be tested as a separate application candidate. It is the older base of the animehacker implementation and contains a misleading, functionally incomplete QJL design. Retain it only as a historical reference for the original CUDA/HIP WHT32 implementation. Use animehacker for the corrected Intel SYCL route and more developed repositories for CUDA or Vulkan research.**
