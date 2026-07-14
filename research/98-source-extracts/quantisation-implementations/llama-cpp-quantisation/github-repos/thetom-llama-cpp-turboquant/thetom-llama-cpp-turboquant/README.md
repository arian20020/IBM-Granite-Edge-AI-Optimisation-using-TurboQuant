---
title: "TheTom llama-cpp-turboquant"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/GitHub repos/TheTom llama-cpp-turboquant/TheTom llama-cpp-turboquant.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is preserved in the controlled provenance ZIP."
---

# TheTom’s llama.cpp TurboQuant Repository

## What llama.cpp is

llama.cpp is an open-source C and C++ inference engine used to run large language models locally. It mainly works with models stored in the GGUF format and can perform inference using different hardware backends, including the CPU, NVIDIA CUDA, AMD, Metal, Vulkan and SYCL.

GGUF model  
↓  
llama.cpp loads the model  
↓  
The model performs local inference  
↓  
The available CPU or GPU performs the calculations

## What TheTom’s repository adds

TheTom’s repository is a fork of the original llama.cpp project. It keeps the normal llama.cpp functionality but adds experimental **TurboQuant-inspired**, or **TurboQuant+**, features.

The repository mainly adds:

- model-weight quantisation;

- KV-cache quantisation;

- CPU and GPU implementations for performing the new quantisation operations.

It should be described as TurboQuant-inspired rather than an exact implementation of the complete TurboQuant research paper.

## Model-weight quantisation

Model weights are the learned numerical parameters that control how the model processes information and generates answers. They remain fixed while the model is being used unless it is trained or fine-tuned again.

The repository adds two experimental GGUF weight formats:

| **Format** | **Approximate storage** | **Meaning**                                     |
|------------|-------------------------|-------------------------------------------------|
| TQ4_1S     | 4.5 bits per weight     | Less aggressive and generally safer for quality |
| TQ3_1S     | 3.5 bits per weight     | Smaller model but greater possible quality loss |

Weight quantisation creates a new GGUF model file.

Original F16 GGUF model  
↓  
Weight quantisation  
↓  
New TQ4_1S or TQ3_1S GGUF model

The bit figures are approximate average storage rates. A value is not literally stored using half of a physical bit.

## KV-cache quantisation

The KV cache stores the key and value vectors created for previous tokens. This allows the model to reuse earlier calculations when predicting the next token.

The KV cache grows as the prompt or conversation becomes longer, so compressing it can reduce memory use and potentially allow longer contexts.

KV-cache compression is selected when the model runs. It does not normally create a new GGUF model file.

The available formats, from largest to smallest, are:

| **Format** | **Approximate storage**   |
|------------|---------------------------|
| f16        | 16 bits per cached value  |
| q8_0       | 8 bits per cached value   |
| turbo4     | 4.5 bits per cached value |
| turbo3     | 3.5 bits per cached value |
| turbo2     | 2 bits per cached value   |

Q8_0 is a normal llama.cpp quantisation format, not a TurboQuant format.

More compression generally means lower memory use but a greater risk of affecting model quality.

Higher quality and memory use  
  
F16  
↓  
Q8_0  
↓  
Turbo4  
↓  
Turbo3  
↓  
Turbo2  
  
Lower memory use and greater quality risk

## Key cache and value cache

The command option:

--cache-type-k

selects the storage format for the **key cache**.

The option:

--cache-type-v

selects the storage format for the **value cache**.

Keys act like searchable labels or addresses. The current query is compared with the stored keys to decide which previous tokens are relevant.

Values contain the information that is combined after the model has calculated the attention scores.

Current query  
↓  
Compared with stored keys  
↓  
Model decides which previous tokens matter  
↓  
Attention scores are applied to the stored values  
↓  
Attention output is produced

Keys are normally kept at a higher precision because errors in the keys can make the model focus on the wrong previous tokens. Values can often be compressed more aggressively.

## Recommended testing configurations

### Baseline

--cache-type-k f16 --cache-type-v f16

Both keys and values use high-precision F16. This provides the comparison result before applying low-bit compression.

### Safest TurboQuant+ starting point

--cache-type-k f16 --cache-type-v turbo4

Keys remain at F16 while values are compressed lightly.

### Conservative compression

--cache-type-k q8_0 --cache-type-v turbo4

Keys use the relatively safe 8-bit Q8_0 format, while values use Turbo4.

### Recommended balance

--cache-type-k q8_0 --cache-type-v turbo3

Keys remain at approximately 8 bits while values are reduced to approximately 3.5 bits.

### Aggressive compression

--cache-type-k q8_0 --cache-type-v turbo2

Keys remain at approximately 8 bits, while values use strong 2-bit compression. This saves more memory but requires careful quality testing.

## What the command options mean

The cache options are not complete PowerShell commands by themselves. They are settings added when launching llama.cpp.

For example:

.\llama-cli.exe \`  
-m "C:\Models\granite.gguf" \`  
--cache-type-k q8_0 \`  
--cache-type-v turbo3 \`  
-c 4096 \`  
-p "Explain what a KV cache is."

This means:

- run the llama.cpp command-line program;

- load the selected Granite GGUF model;

- store keys using Q8_0;

- store values using Turbo3;

- use a context length of 4,096 tokens;

- send the provided prompt to the model.

The PowerShell backtick only means that the command continues onto the next line.

# How its TurboQuant+ compression works

The practical method used here can be simplified into four stages.

## Stage 1: Take a KV-cache vector

For example:

\[0.42, -0.71, 0.16, 0.93, ...\]

This vector may contain 128 values representing part of one attention head.

## Stage 2: Record its length

The system calculates the norm:

γ = \|\|x\|\|

This stores the overall size or magnitude of the vector.

The vector is then normalised:

x̂ = x / γ

That separates:

- the vector’s overall size;

- the pattern or direction of its values.

## Stage 3: Rotate the values

The fork uses a fast **Walsh–Hadamard Transform**, together with fixed random sign changes.

The rotation spreads unusually large values across the vector, creating values that are easier to compress consistently.

## Stage 4: Map each value to a small codebook

Instead of keeping every original decimal, it selects the nearest permitted representative value:

turbo2 → 4 possible representatives  
turbo3 → 8 possible representatives  
turbo4 → 16 possible representatives

The stored representation contains approximately:

Compressed indices  
+  
vector or block norm  
+  
small amount of metadata

When the model needs the KV values again, the code reconstructs an approximate version from this compressed information. The companion research repository describes this as norm extraction, Walsh–Hadamard rotation and Lloyd–Max scalar quantisation.

### TurboQuant+ KV-Cache Compression in TheTom’s Repository

TheTom’s llama.cpp fork uses a practical **TurboQuant-inspired** method to compress KV-cache vectors during inference.

#### Compression process

1.  **Calculate and store the vector norm**  
    The norm represents the vector’s overall magnitude.

2.  **Normalise the vector**  
    The vector is divided by its norm so that its direction can be compressed separately from its size.

3.  **Apply a fast rotation**  
    Fixed random sign changes and a Walsh–Hadamard Transform spread large outlier values more evenly.

4.  **Quantise the rotated values**  
    Each coordinate is replaced by the nearest value from a small codebook.

| **Format** | **Codebook size** | **Index size** |
|------------|-------------------|----------------|
| Turbo2     | 4 values          | 2 bits         |
| Turbo3     | 8 values          | 3 bits         |
| Turbo4     | 16 values         | 4 bits         |

The reported storage rates of approximately **2, 3.5 and 4.5 bits per value** also include the saved norm and other small metadata.

#### Reconstruction

When the KV cache is needed, the system:

Decodes the codebook values  
↓  
Reverses the rotation  
↓  
Restores the saved norm  
↓  
Reconstructs an approximation of the original vector

#### Relationship to the original TurboQuant paper

This method is broadly similar to **Stage 1** of the original TurboQuant proposal because it uses rotation followed by low-bit scalar quantisation to reduce mean-squared error.

However, TheTom’s implementation is an adapted **TurboQuant+** system rather than an exact copy of the complete two-stage paper method. It includes practical block layouts, compression policies and hardware-specific kernels, while the full QJL residual-correction stage is not necessarily used in the production path.

#### Why QJL Was Removed from the Production Implementation

The original TurboQuant method uses QJL as a second compression stage to correct the residual error left after the first quantisation stage. This produces an unbiased estimate of the original inner products in expectation.

However, the developers of TheTom’s TurboQuant+ implementation found that QJL could introduce additional variance into individual attention-score estimates. Although the estimates may be correct on average, the increased fluctuation can create attention noise. This is particularly important because softmax can amplify small differences between attention scores and cause the model to focus on the wrong previous tokens.

For this reason, the current production implementation retains QJL mainly as reference or experimental code but uses norm extraction, Walsh–Hadamard rotation and low-bit codebook quantisation without the full QJL residual-correction stage.

### Engineering Additions Beyond the Original Method

TheTom’s repository uses asymmetric KV-cache compression, where the more sensitive key cache is normally kept at a higher precision than the value cache. For example, keys may use the relatively safe q8_0 format while values use turbo3. The system may also automatically replace an unsafe low-bit key configuration with q8_0.

When aggressive turbo2 value compression is used, the Boundary V policy can keep selected sensitive layers at a safer precision. This provides most of the memory reduction without applying the strongest compression uniformly to every layer.

On supported Metal implementations, sparse V dequantisation may skip reconstructing compressed values that have extremely low attention scores because they are unlikely to meaningfully affect the final attention output. This is a speed optimisation that avoids unnecessary dequantisation, not a method that leaves model weights or values uncompressed.

The repository also provides hardware-specific kernels for CUDA, Metal, HIP/ROCm and Vulkan. These kernels execute the TurboQuant compression, reconstruction and attention operations efficiently on supported hardware.

# Current project status

The repository is marked as **work in progress**.

Its important working branch is:

feature/turboquant-kv-cache

This is also currently configured as the repository’s default branch. The merge notes warn that the master branch is an older snapshot and should not be used as the main TurboQuant branch.

Therefore, when cloning it, use:

git clone --branch feature/turboquant-kv-cache \`  
<https://github.com/TheTom/llama-cpp-turboquant.git>

It remains a long-running fork rather than a feature that has been fully merged into official llama.cpp.
