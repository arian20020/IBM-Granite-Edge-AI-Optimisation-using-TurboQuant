# KV-cache quantisation

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This is the full beginner-friendly explanation of KV-cache quantisation. It keeps the original numerical examples and separates weight quantisation from KV-cache quantisation, because they reduce different parts of memory and can affect quality in different ways.

## Step-by-step quantisation flow

1. Start with a floating-point key or value vector.
2. Divide it into the chosen grouping or channel structure.
3. Calculate the scale, range or codebook information required by the method.
4. Replace each high-precision value with a low-bit representation.
5. Store any required metadata, such as scales or zero points.
6. During attention, reconstruct the values or use a specialised low-bit calculation.
7. Compare memory savings, runtime overhead and answer quality against the original cache.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-PAPER-KIVI-2024](../00-sources/primary-research-papers.md#src-paper-kivi-2024), [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

KV-cache quantisation stores cached key and value numbers with fewer bits. [SRC-PAPER-KIVI-2024]

| Example format | Bits per value | Number of possible codes |
|---|---:|---:|
| FP16 | 16 | Very large floating-point range |
| INT4 | 4 | 16 |
| INT2 | 2 | 4 |

Fewer bits reduce memory and memory traffic, but increase approximation error.

## Scale, range and error

A basic quantiser maps a range of real numbers onto a small set of codes. The decoder needs scale and range information to rebuild approximate values. Outliers can make the range too wide, leaving poor precision for ordinary values.

## Per-token and per-channel grouping

Imagine the cache as a table:

- each row is one token vector;
- each column is one fixed channel.

**Per-token quantisation** groups across a row. It is often suitable for values.

**Per-channel quantisation** groups the same channel across tokens. It can isolate persistent outlier channels and is often safer for keys.

The KIVI research recorded in the source notes uses this asymmetric approach:

- keys: per channel;
- values: per token;
- newest entries: temporarily kept in full precision.

## Streaming cache design

The cache is created during inference, so quantisation must happen as tokens arrive. A practical design keeps a small recent full-precision section and moves older complete groups into the packed low-bit section.

## Mixed-precision attention

The query searches both sections:

```text
scores from older quantised keys
+ scores from recent full-precision keys
-> one softmax
-> one set of attention weights
```

The corresponding quantised and full-precision values are then combined into the final attention result.

## Fake and real quantisation

- **Fake quantisation:** simulate low precision, then use floating-point storage. Useful for quality experiments, but does not prove memory savings.
- **Real quantisation:** physically pack the low-bit codes and use them during attention. Required for genuine memory and performance claims.

## Important reporting rule

A six-times smaller KV cache does not mean six-times lower total application memory. Model weights and other runtime memory remain.

## Sources used

- [SRC-PAPER-KIVI-2024](../00-sources/primary-research-papers.md#src-paper-kivi-2024) — KIVI: A Tuning-Free Asymmetric 2bit Quantization for KV Cache.
- [SRC-HF-TRANSFORMERS-CACHE](../00-sources/official-documentation.md#src-hf-transformers-cache) — Transformers caching explanation.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: 2. KV Cache Quantization

> **Original document:** `KV-Cache Compression/2. KV Cache Quantization.docx`

1.  Introduction to the KV-Cache Problem

2.  What KV-Cache Quantisation Means

3.  Scale, Starting Point and Quantisation Error

4.  The Effect of Outliers

5.  Per-Channel and Per-Token Grouping

6.  Per-Channel Quantisation of Keys

7.  Per-Token Quantisation of Values

8.  Four-Bit Compared with Two-Bit Quantisation

9.  KV-Cache Quantisation as a Streaming Process

10. Quantised and Full-Precision Cache Sections

11. Mixed-Precision Attention

12. Efficient Dequantisation During Attention

13. Fake Quantisation Compared with Real Quantisation

14. KIVI Performance Results

15. Lessons for the TurboQuant Project

16. Evaluation Plan for TurboQuant

#### The Problem

As the number of tokens grows, the cache grows:

More prompt tokens  
+  
more generated tokens  
↓  
more K and V vectors stored  
↓  
more RAM or VRAM required

The cache also has to be read repeatedly during generation. For every new token, the newest query must access the old cached keys and values.

Therefore, a large cache causes two problems:

1.  **Memory use:** more space is required to store the cache.

2.  **Memory movement:** more data has to be loaded for each generated token.

So we want:

- lower RAM use;

- longer usable context lengths;

- possibly faster decoding because less data is moved from memory.

2.  What KV-Cache Quantization Means

The key and value vectors in the KV cache contain decimal numbers. These numbers are often stored using FP16 or BF16. FP means floating point, and FP16 means each number uses 16 bits of memory.

Quantization reduces the number of bits used to store each value. For example, FP16 uses 16 bits, INT4 uses 4 bits and INT2 uses 2 bits. Using fewer bits reduces the size of the KV cache, but it also reduces the number of possible values available.

A 4-bit format has:

2⁴ = 16 possible codes

A 2-bit format has:

2² = 4 possible codes

The four 2-bit codes are 0, 1, 2 and 3. These codes do not directly represent the original decimal values. The system also stores a scale and a starting point so it can understand what each code represents.

### Scale and Starting Point

Suppose a group contains the values:

\[-1, 0, 1, 3\]

The minimum value is -1 and the maximum is 3. In the simplified KIVI formula, the minimum value is used as the starting point of the quantization range.

The starting point is not necessarily the first element in the vector. For example, if the vector is \[3, -1, 1, 0\], the starting point is still -1 because it is the minimum value.

With 2-bit quantization, four levels are available. The scale determines the equal distance between these levels.

Scale = (maximum − minimum) ÷ (number of levels − 1)

Scale = (3 − (-1)) ÷ (4 − 1)

Scale = 4 ÷ 3 ≈ 1.33

The four available levels are therefore:

Code 0 = -1.00  
Code 1 = 0.33  
Code 2 = 1.67  
Code 3 = 3.00

The scale is not the distance between the original values in the vector. It is the fixed distance between the newly created quantization levels.

#### Quantization and Reconstruction

Each original value is mapped to the nearest available level and stored as a small integer code.

For example:

Original value 0 is stored as code 1 and reconstructed as approximately 0.33.

Original value 1 is stored as code 2 and reconstructed as approximately 1.67.

The original vector:

\[-1, 0, 1, 3\]

is stored using the codes:

\[0, 1, 2, 3\]

When reconstructed, it becomes approximately:

\[-1, 0.33, 1.67, 3\]

#### Quantization Error

Quantization error is the difference between the original value and the reconstructed value.

For example:

Original value = 1.00  
Reconstructed value = 1.67  
Quantization error = 0.67

The aim of KV-cache quantization is not to reproduce every number perfectly. The aim is to reduce memory use while keeping the numerical error small enough that the model’s attention calculations and output quality remain accurate.

Using more bits normally produces smaller errors because more quantization levels are available. Using fewer bits saves more memory, but it increases the risk of larger errors. KIVI addresses this problem by carefully choosing how key and value vectors are grouped and quantized.

#### Why quantizing the entire cache in one group would be poor

Suppose a key vector contains:

*\[0.1, 0.2, 0.15, 12.0\]*

The final value, *12.0*, is an **outlier** because it is much larger than the others.

With only four 2-bit levels, the quantizer must cover the full range from approximately *0.10.*

So, outliers increase the range that the quantizer must cover. With a fixed number of low-bit levels, this increases the distance between levels and often causes larger quantization errors for the normal values.

### Viewing the KV Cache as a Table

For one attention head in one layer, the key or value cache can be imagined as a table.

Each row represents the vector produced for one token. Each column represents one channel, which is one fixed position inside every token vector.

For example:

| **Token** | **Channel 1** | **Channel 2** | **Channel 3** | **Channel 4** |
|-----------|---------------|---------------|---------------|---------------|
| Token 1   | 0.2           | 0.1           | 9.8           | 0.4           |
| Token 2   | 0.3           | 0.2           | 10.4          | 0.5           |
| Token 3   | 0.1           | 0.3           | 11.2          | 0.2           |

The first row represents the vector for Token 1:

\[0.2, 0.1, 9.8, 0.4\]

The third column represents Channel 3 across all tokens:

\[9.8, 10.4, 11.2\]

KIVI examines two ways of grouping these values for quantization.

#### Per-Token Quantization

Per-token quantization groups values horizontally across one token vector.

For example:

Token 1: \[0.2, 0.1, 9.8, 0.4\]

These values share the same scale and starting point.

The problem is that the large value 9.8 creates a wide numerical range. This spreads the limited quantization levels farther apart and reduces the accuracy of the smaller values such as 0.1, 0.2 and 0.4.

#### Per-Channel Quantisation

Per-channel quantization groups values vertically using the same channel from several token vectors.

For example:

Channel 1: \[0.2, 0.3, 0.1\] -\> its own scale

Channel 2: \[0.1, 0.2, 0.3\] -\> Its own scale

Channel 3: \[9.8, 10.4, 11.2\] -\> its own scale

Channel 4: \[0.4, 0.5, 0.2\] -\>its own scale

Channel 3 contains consistently large values. With per-channel quantisation, Channel 3 receives its own scale, while the smaller channels receive separate scales suited to their smaller ranges.

The outlier channel is not ignored or removed. It is still stored and used in the attention calculation. It is simply isolated so that its large values do not reduce the quantization accuracy of the normal channels.

##### Which one is better?

KIVI found that this vertical, per-channel grouping works better for keys because key caches often contain fixed channels with large outliers. However, values behave differently, so KIVI quantizes values horizontally on a per-token basis.

Per-channel quantization isolates persistent outlier channels by giving each channel its own quantization settings. This prevents the large values in one channel from increasing the quantization error of the normal channels.

#### 4-bit vs 2-Bit Quantisation

KIVI investigates how reducing the precision of the KV cache affects model accuracy. Four-bit quantization provides 16 possible numerical levels, meaning that the original key and value numbers can usually be approximated reasonably closely. As a result, standard per-token quantization of both keys and values at 4-bit precision was able to preserve model quality relatively well.

Two-bit quantization is more difficult because it provides only four possible numerical levels. The larger gaps between these levels can create greater quantization errors, particularly when values with very different ranges or outliers are placed in the same group. The KIVI experiments showed that applying per-token quantization to both keys and values at 2-bit precision caused a noticeable reduction in accuracy.

To reduce this quality loss, KIVI uses an asymmetric grouping method. Key vectors are quantized per channel because key caches often contain fixed channels with large outliers. Giving each channel its own quantization settings prevent an outlier channel from reducing the precision of the normal channels. Value vectors are quantized per token because attention usually depends strongly on only a small number of important tokens. Giving every token its own scale prevents unusual values from one token from affecting the accuracy of another important token.

KIVI therefore uses per-channel quantization for keys and per-token quantization for values. It also retains a small group of the most recent key and value vectors in full precision. This combination allows 2-bit KV-cache quantization to achieve substantial memory savings while preserving considerably more model quality than straightforward 2-bit quantization.

#### Timing of KV-Cache Quantisation

The timing of KV-cache quantization is important because the cache is created dynamically during inference. Each token produces new key and value vectors when it passes through an attention layer, meaning these vectors cannot be quantized before the model begins generating. Quantization must therefore be integrated into the decoding process and occur shortly after the new vectors are created.

In KIVI, the most recent key and value vectors are first placed in a small full-precision residual section. Once enough tokens have accumulated to form a complete group, the older vectors are quantized and transferred into the compressed section of the cache. This allows quantization to occur continuously during generation without requiring an expensive optimisation process for every individual token.

The process must also be efficient. If quantization takes too long, it may increase token-generation latency and reduce the benefits of compressing the cache. Therefore, quantization should occur during inference, after the key and value vectors have been produced, but in a way that balances timely compression with low processing overhead.

#### Grouped cache and residual cache

KIVI divides the cache into two parts.

#### Quantized grouped section

This contains older tokens that have already formed complete groups.

Older complete groups  
↓  
stored at 2-bit or 4-bit precision

#### Full-precision residual section

This contains recent tokens that have not yet formed a complete group.

Newest incomplete group  
↓  
temporarily stored in full precision

Conceptually:

KV cache

\[quantized older tokens\] \[recent full-precision tokens\]

#### Per-Token Quantisation of the Value Cache

Each token produces a new value vector at every attention layer. KIVI does not immediately quantize this new vector. Instead, it is first stored in a small full-precision residual cache containing the most recent value vectors.

This residual cache has a predefined capacity, such as 128 tokens. Once it reaches that capacity, any newly generated value vector causes the oldest value vector to leave the full-precision section. That older vector is then quantized per token and transferred into the compressed value cache.

The process can be visualised as a moving queue:

Older value vectors Most recent value vectors  
┌─────────────────────────┐ ┌────────────────────────────┐  
│ Quantized value cache │ │ Full-precision residual │  
│ Q(V1), Q(V2), Q(V3) │ │ V4, V5, V6, V7 │  
└─────────────────────────┘ └────────────────────────────┘

Suppose the residual capacity is four vectors. When a new vector, V8, is produced, the residual cache would become too large:

Before V8 arrives:

Quantized cache: Q(V1), Q(V2), Q(V3)  
Full-precision cache: V4, V5, V6, V7

V4 is therefore the oldest vector in the residual cache. It is quantized and moved into the compressed cache, while V8 enters the full-precision section:

After V8 arrives:

Quantized cache: Q(V1), Q(V2), Q(V3), Q(V4)  
Full-precision cache: V5, V6, V7, V8

Because values are quantized per token, V4 receives its own scale and starting point before being converted into low-bit integer codes. The compressed codes and quantization settings are stored in the quantized value cache. Approximate floating-point values are reconstructed later when attention needs to use that vector.

KIVI therefore keeps older value vectors in compressed form while preserving the most recent vectors in full precision. Recent tokens are often particularly relevant when predicting the next token, so retaining them accurately helps protect model quality. Older tokens are not discarded; they remain available to attention in quantized form, reducing the total memory required by the KV cache.

#### Per-Channel Quantisation of the Key Cache (why it is more difficult)

Key quantisation is more difficult than value quantisation because KIVI groups key values vertically by channel across multiple tokens. Although each token immediately produces a complete key vector, the system does not yet have the corresponding channel values from future tokens.

For example, Token 1 may produce:

*K1=\[0.2, 0.1, 9.8, 0.4\]*

At this point, each channel contains only one value:

Channel 1: \[0.2, ?, ?, ...\]  
Channel 2: \[0.1, ?, ?, ...\]  
Channel 3: \[9.8, ?, ?, ...\]  
Channel 4: \[0.4, ?, ?, ...\]

The missing values belong to tokens that have not yet been processed. KIVI therefore cannot calculate reliable per-channel quantisation settings immediately. Instead, new key vectors are temporarily stored in a full-precision residual cache.

As more tokens are processed, the residual cache gradually fills:

Token 1: \[0.2, 0.1, 9.8, 0.4\]  
Token 2: \[0.3, 0.2, 10.4, 0.5\]  
Token 3: \[0.1, 0.3, 11.2, 0.2\]

Once enough tokens have accumulated, KIVI reads the collected vectors vertically:

Channel 1: \[0.2, 0.3, 0.1\]  
Channel 2: \[0.1, 0.2, 0.3\]  
Channel 3: \[9.8, 10.4, 11.2\]  
Channel 4: \[0.4, 0.5, 0.2\]

Each channel group receives its own scale and starting point before being converted into low-bit codes. This isolates persistent outlier channels, such as Channel 3, so their large values do not widen the quantisation range used by the normal channels.

KIVI uses a residual capacity *R* and a smaller quantisation group size *G*. For example, if *R=128* and *G=32*, the residual cache first collects 128 full-precision key vectors. These are then divided into four groups of 32 tokens. Within each group, values from the same channel are quantised together.

Full-precision key residual reaches capacity  
↓  
divide tokens into complete groups  
↓  
group values vertically by channel  
↓  
quantise each channel using its own settings  
↓  
move compressed groups into the quantised key cache  
↓  
clear the residual and begin collecting new key vectors

The older keys are not removed or ignored. They remain available to the attention mechanism in compressed form. The residual cache is then filled again, starting with the next token. This process allows KIVI to apply per-channel key quantisation continuously, even though key vectors are generated one token at a time.

##### Why Recent Tokens Remain in Full Precision

KIVI uses a hybrid KV-cache design. Older key and value vectors are stored in low-bit quantised form, while the most recent vectors remain in a full-precision residual cache.

The idea can be visualised as follows:

Older context Most recent context  
┌─────────────────────────┐ ┌──────────────────────────┐  
│ Quantised KV cache │ │ Full-precision residual │  
│ Q(K1), Q(V1) │ │ K5, V5 │  
│ Q(K2), Q(V2) │ │ K6, V6 │  
│ Q(K3), Q(V3) │ │ K7, V7 │  
│ Q(K4), Q(V4) │ │ K8, V8 │  
└─────────────────────────┘ └──────────────────────────┘

Recent tokens are often especially relevant when predicting the next token because they contain the model’s immediate context. For example, in the sentence “The capital of France is”, the most recent words are highly relevant to predicting “Paris”. Keeping their key and value vectors in full precision prevents quantisation errors from reducing the accuracy of this recent information.

When the residual cache reaches its predefined capacity, older vectors leave the full-precision section, are quantised and are transferred into the compressed cache. This makes space for newly generated vectors while reducing the total memory required by the growing KV cache.

Older tokens are not deleted or ignored. They remain available to the attention mechanism in quantised form because information from much earlier in the context may still be required. Recent tokens are also not always more important, but keeping a recent full-precision window provides additional protection for information that is frequently relevant to the next prediction.

KIVI therefore balances memory efficiency and model accuracy by combining two storage formats:

Older KV vectors → low-bit quantised storage  
Recent KV vectors → full-precision storage

The paper found that this full-precision window was particularly useful for difficult generation tasks such as mathematical reasoning. Quantising every cache entry produced a greater reduction in accuracy, while retaining recent entries in full precision recovered much of the lost performance. KIVI’s hybrid design therefore provides a better balance than treating every KV-cache entry in exactly the same way.

Although each new token representation is influenced by previous tokens, it does not replace the older key and value vectors. Future queries may still need to attend directly to information stored much earlier in the context. KIVI therefore retains older KV entries in quantised form rather than deleting them, while keeping the most recent entries in full precision to protect immediately relevant context.

#### How Attention Uses the Quantised and Full-Precision Cache

KIVI divides the KV cache into two sections: an older quantised section and a recent full-precision residual section. Both sections remain part of the model’s available context and are used when generating each new token.

The current query vector is compared with the keys in both sections. It is first compared with the older quantised keys and separately compared with the recent full-precision keys:

Current query  
├── compared with older quantised keys  
└── compared with recent full-precision keys

These comparisons produce two sets of attention scores. The scores are then joined together and passed through the softmax function, producing one set of attention weights across all previous tokens.

Scores from older keys  
+  
Scores from recent keys  
↓  
join the scores  
↓  
apply softmax  
↓  
one complete set of attention weights

The attention weights are then applied to the corresponding value vectors. Some of these values come from the older quantised cache, while the most recent values come from the full-precision residual cache. Their weighted contributions are combined to form the final attention output.

Contribution from older quantised values  
+  
Contribution from recent full-precision values  
↓  
final attention output

An intuitive way to understand this is to imagine the model searching through two parts of the same memory. The older part is compressed to save space, while the recent part is kept in greater detail. The model searches both sections, decides which previous tokens are most relevant and combines their information into one result.

The two sections are not treated as separate conversations. After their attention scores are joined, all previous tokens compete within the same attention calculation. Older information therefore remains available even though it is stored at lower precision.

This is called mixed-precision attention because the calculation uses low-precision older cache entries together with full-precision recent entries. KIVI reduces the overhead by combining the reconstruction of quantised values with the matrix multiplication used during attention, rather than first rebuilding the entire older cache in full precision. This design reduces memory use while allowing the model to use both older and recent context when predicting the next token.

####

#### Efficient Dequantisation During Attention

The older sections of the KV cache are stored using low-bit quantised codes. However, the attention calculation still needs usable numerical values when comparing the current query with the cached keys and combining the cached values. These compressed values must therefore be dequantised when they are used.

A basic implementation could reconstruct the entire quantised cache into FP16 before performing the attention calculation:

Quantised cache  
↓  
reconstruct the complete FP16 cache  
↓  
store a large temporary copy  
↓  
perform matrix multiplication

This approach would be inefficient because it would temporarily recreate much of the large memory usage that quantisation was intended to remove. It would also require the hardware to write and reload a large full-precision copy of the cache.

KIVI instead processes the quantised cache in small blocks. Each block is dequantised only while it is being used in the matrix multiplication:

Quantised cache:  
\[Block 1\] \[Block 2\] \[Block 3\] \[Block 4\]

Block 1 → dequantise → calculate → add result  
Block 2 → dequantise → calculate → add result  
Block 3 → dequantise → calculate → add result  
Block 4 → dequantise → calculate → complete result

After one block has contributed to the calculation, the same temporary memory space can be reused for the next block. The model still considers the entire older cache; it does not skip tokens that appear unimportant. However, it never needs to hold a complete reconstructed FP16 version of the cache at one time.

An intuitive way to understand this is to imagine reading a compressed video. The whole video is not decompressed into memory before playback begins. Instead, small sections are decoded as they are needed and then replaced by the next section.

KIVI combines, or **fuses**, dequantisation with matrix multiplication through an operation called Q_MatMul. This means each compressed block is reconstructed and immediately used in the calculation, rather than performing dequantisation and matrix multiplication as two separate large operations. The paper implements this fused calculation using CUDA and uses Triton kernels for group-wise quantisation. This reduces temporary memory use, avoids unnecessary memory movement and helps ensure that the cost of dequantisation does not remove the benefits gained from compressing the KV cache.

#### Fake Quantisation and Real Quantisation

KIVI uses fake quantisation in some experiments to study how reducing numerical precision affects model quality. In fake quantisation, the original FP16 or BF16 key and value vectors are converted into simulated low-bit values and then immediately reconstructed into floating-point form before attention is calculated.

The process can be represented as follows:

Original floating-point values  
↓  
simulate 2-bit or 4-bit quantisation  
↓  
reconstruct approximate floating-point values  
↓  
perform normal floating-point attention

This allows the researchers to measure the numerical effect of quantisation, including quantisation error, changes in attention calculations and reductions in model accuracy. It is useful for comparing different approaches, such as per-channel quantisation for keys and per-token quantisation for values.

However, fake quantisation does not prove that the system achieves genuine memory or speed improvements. The reconstructed cache may still be physically stored and processed using floating-point tensors. It therefore does not demonstrate real low-bit storage, efficient bit packing or fast hardware-level dequantisation.

An intuitive comparison is compressing a large file and then immediately expanding it again before placing it into memory. This may show whether the compressed version has lost important information, but it does not provide the full memory benefit because the expanded copy is still being used.

Real quantisation requires the KV cache to be genuinely stored using packed low-bit codes:

New key and value vectors  
↓  
convert values into low-bit codes  
↓  
pack and store the codes using fewer bits  
↓  
store the required scales and starting points  
↓  
process the compressed cache efficiently during attention

For example, four 2-bit values should be packed into one byte. Simply storing the codes 0, 1, 2 and 3 inside a standard 32-bit integer tensor would not provide the expected memory saving.

For this project, fake quantisation can be used as an initial accuracy prototype. It can determine whether the chosen compression method preserves the model’s behaviour before the more difficult low-level implementation is developed. Real quantisation can then be implemented and evaluated using three groups of measurements:

- model quality, including accuracy, reasoning, instruction following and long-context retrieval;

- memory efficiency, including KV-cache size, total RAM use, metadata overhead and maximum usable context length;

- runtime performance, including time to first token, decoding speed, quantisation overhead and dequantisation overhead.

The implementation becomes real when the cache is genuinely stored and processed in a compressed low-bit format. Performance metrics are then used to prove whether that implementation provides meaningful memory savings and acceptable speed while maintaining model quality. A practical development order is therefore to test quality using fake quantisation, implement real packed storage, integrate efficient compressed-cache attention and finally compare quality, memory use and speed against the full-precision baseline.

##### Performance Results and Relevance to TurboQuant

The KIVI experiments demonstrate that KV-cache quantisation can provide meaningful memory and performance improvements while generally maintaining model quality. Its 2-bit implementation reduced overall peak memory usage by up to approximately 2.6 times, enabled batch sizes up to four times larger and achieved around 2.35–3.47 times greater throughput in the tested workloads. The evaluated Llama and Mistral models generally experienced only a small reduction in output quality. However, results varied between model architectures. For example, Falcon already used multi-query attention and therefore had a smaller KV cache, meaning that 4-bit KIVI was safer than 2-bit KIVI for preserving accuracy.

These results should not be interpreted as evidence that KIVI will make every model or device approximately three times faster. The experiments used specific models, datasets, context lengths, batch sizes and NVIDIA GPU kernels. Performance therefore depends on the model architecture, hardware and efficiency of the quantisation and dequantisation implementation.

TurboQuant appears promising because its published results report at least a sixfold reduction in KV-cache memory on long-context retrieval tests, alongside strong quality retention. The TurboQuant paper also reports quality neutrality at approximately 3.5 bits per channel and only marginal quality degradation at approximately 2.5 bits per channel under its tested conditions.

However, TurboQuant’s sixfold figure refers specifically to the KV cache and should not be interpreted as a sixfold reduction in the application’s total RAM use. Total memory also includes model weights, runtime buffers, quantisation metadata and other application resources. Intuitively, reducing the size of one compartment in a suitcase does not reduce the size of everything else inside it.

Consequently, TurboQuant cannot yet be assumed to outperform KIVI for the proposed application. It must be tested using the selected IBM Granite models and target Intel hardware. The evaluation should compare KV-cache memory, total application RAM, context capacity, time to first token, decoding speed and output quality against a full-precision baseline. This will determine whether TurboQuant’s published compression benefits transfer successfully to Granite-based local inference on Windows laptops.

Source: [KIVI: A Tuning-Free Asymmetric 2bit Quantization for KV Cache](https://arxiv.org/html/2402.02750v2#S2)

## What this means for the project

The application should expose the chosen KV format separately from the weight format. A Q8 weight model with an F16 cache is a different experiment from the same Q8 model with an 8-bit or lower-bit cache.

## Summary

- KV-cache quantisation is different from weight quantisation.
- Low-bit storage needs a reconstruction or specialised calculation method.
- Metadata overhead and grouping choices matter.
- Memory, speed and quality must all be measured.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-PAPER-KIVI-2024`
- `SRC-HF-TRANSFORMERS-CACHE`
