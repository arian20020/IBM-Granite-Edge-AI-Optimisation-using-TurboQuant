# TheTom/llama-cpp-turboquant

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-THETOM](../00-sources/github-repositories.md#src-repo-thetom), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/TheTom/llama-cpp-turboquant> |
| Branch | feature/turboquant-kv-cache |
| Commit | `Pin before testing; not selected in supplied summary` |
| Last relevant update in supplied research | Active development in June 2026 |
| Project position | **Main general GGUF prototype** |

## What it does

Norm extraction, fixed signs/WHT rotation and low-bit codebook formats; experimental weight and cache quantisation. [SRC-REPO-THETOM]

## Difference from formal TurboQuant

TurboQuant-inspired/TurboQuant+; production formats do not necessarily use the complete formal QJL stage.

## Storage

Real turbo2/turbo3/turbo4 cache formats and TQ3_1S/TQ4_1S weight formats.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Present |
| CUDA | Strong and suitable for NVIDIA testing |
| ROCm/HIP | Experimental |
| Vulkan | General backend inherited; custom support not established |
| Intel SYCL | Incomplete/experimental in supplied summary |
| OpenVINO | Custom Turbo formats not documented |
| Intel NPU | No |

## Granite status

Must be tested

## Project recommendation

Use as the main general GGUF/TurboQuant prototype and NVIDIA comparison, but not as proof of finished Intel support.

## Required evidence before adoption

- pin and record the exact commit;
- build on the target Windows machine;
- run the same Granite GGUF baseline and prompts;
- prove real packed cache allocation;
- record memory, speed, stability and quality;
- compare against standard cache formats;
- save logs and known limitations.

The full, longer analysis and commands are preserved in `98-source-extracts` and mapped in the source register.

## Sources used

- [SRC-REPO-THETOM](../00-sources/github-repositories.md#src-repo-thetom) — TheTom/llama-cpp-turboquant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: TheTom llama-cpp-turboquant

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/TheTom llama-cpp-turboquant/TheTom llama-cpp-turboquant.docx`

#### What llama.cpp is

llama.cpp is an open-source C and C++ inference engine used to run large language models locally. It mainly works with models stored in the GGUF format and can perform inference using different hardware backends, including the CPU, NVIDIA CUDA, AMD, Metal, Vulkan and SYCL.

GGUF model  
↓  
llama.cpp loads the model  
↓  
The model performs local inference  
↓  
The available CPU or GPU performs the calculations

#### What TheTom’s repository adds

TheTom’s repository is a fork of the original llama.cpp project. It keeps the normal llama.cpp functionality but adds experimental **TurboQuant-inspired**, or **TurboQuant+**, features.

The repository mainly adds:

- model-weight quantisation;

- KV-cache quantisation;

- CPU and GPU implementations for performing the new quantisation operations.

It should be described as TurboQuant-inspired rather than an exact implementation of the complete TurboQuant research paper.

#### Model-weight quantisation

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

#### KV-cache quantisation

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

#### Key cache and value cache

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

#### Recommended testing configurations

##### Baseline

--cache-type-k f16 --cache-type-v f16

Both keys and values use high-precision F16. This provides the comparison result before applying low-bit compression.

##### Safest TurboQuant+ starting point

--cache-type-k f16 --cache-type-v turbo4

Keys remain at F16 while values are compressed lightly.

##### Conservative compression

--cache-type-k q8_0 --cache-type-v turbo4

Keys use the relatively safe 8-bit Q8_0 format, while values use Turbo4.

##### Recommended balance

--cache-type-k q8_0 --cache-type-v turbo3

Keys remain at approximately 8 bits while values are reduced to approximately 3.5 bits.

##### Aggressive compression

--cache-type-k q8_0 --cache-type-v turbo2

Keys remain at approximately 8 bits, while values use strong 2-bit compression. This saves more memory but requires careful quality testing.

#### What the command options mean

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

### How its TurboQuant+ compression works

The practical method used here can be simplified into four stages.

#### Stage 1: Take a KV-cache vector

For example:

\[0.42, -0.71, 0.16, 0.93, ...\]

This vector may contain 128 values representing part of one attention head.

#### Stage 2: Record its length

The system calculates the norm:

γ = \|\|x\|\|

This stores the overall size or magnitude of the vector.

The vector is then normalised:

x̂ = x / γ

That separates:

- the vector’s overall size;

- the pattern or direction of its values.

#### Stage 3: Rotate the values

The fork uses a fast **Walsh–Hadamard Transform**, together with fixed random sign changes.

The rotation spreads unusually large values across the vector, creating values that are easier to compress consistently.

#### Stage 4: Map each value to a small codebook

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

##### TurboQuant+ KV-Cache Compression in TheTom’s Repository

TheTom’s llama.cpp fork uses a practical **TurboQuant-inspired** method to compress KV-cache vectors during inference.

###### Compression process

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

###### Reconstruction

When the KV cache is needed, the system:

Decodes the codebook values  
↓  
Reverses the rotation  
↓  
Restores the saved norm  
↓  
Reconstructs an approximation of the original vector

###### Relationship to the original TurboQuant paper

This method is broadly similar to **Stage 1** of the original TurboQuant proposal because it uses rotation followed by low-bit scalar quantisation to reduce mean-squared error.

However, TheTom’s implementation is an adapted **TurboQuant+** system rather than an exact copy of the complete two-stage paper method. It includes practical block layouts, compression policies and hardware-specific kernels, while the full QJL residual-correction stage is not necessarily used in the production path.

###### Why QJL Was Removed from the Production Implementation

The original TurboQuant method uses QJL as a second compression stage to correct the residual error left after the first quantisation stage. This produces an unbiased estimate of the original inner products in expectation.

However, the developers of TheTom’s TurboQuant+ implementation found that QJL could introduce additional variance into individual attention-score estimates. Although the estimates may be correct on average, the increased fluctuation can create attention noise. This is particularly important because softmax can amplify small differences between attention scores and cause the model to focus on the wrong previous tokens.

For this reason, the current production implementation retains QJL mainly as reference or experimental code but uses norm extraction, Walsh–Hadamard rotation and low-bit codebook quantisation without the full QJL residual-correction stage.

##### Engineering Additions Beyond the Original Method

TheTom’s repository uses asymmetric KV-cache compression, where the more sensitive key cache is normally kept at a higher precision than the value cache. For example, keys may use the relatively safe q8_0 format while values use turbo3. The system may also automatically replace an unsafe low-bit key configuration with q8_0.

When aggressive turbo2 value compression is used, the Boundary V policy can keep selected sensitive layers at a safer precision. This provides most of the memory reduction without applying the strongest compression uniformly to every layer.

On supported Metal implementations, sparse V dequantisation may skip reconstructing compressed values that have extremely low attention scores because they are unlikely to meaningfully affect the final attention output. This is a speed optimisation that avoids unnecessary dequantisation, not a method that leaves model weights or values uncompressed.

The repository also provides hardware-specific kernels for CUDA, Metal, HIP/ROCm and Vulkan. These kernels execute the TurboQuant compression, reconstruction and attention operations efficiently on supported hardware.

### Current project status

The repository is marked as **work in progress**.

Its important working branch is:

feature/turboquant-kv-cache

This is also currently configured as the repository’s default branch. The merge notes warn that the master branch is an older snapshot and should not be used as the main TurboQuant branch.

Therefore, when cloning it, use:

git clone --branch feature/turboquant-kv-cache \`  
<https://github.com/TheTom/llama-cpp-turboquant.git>

It remains a long-running fork rather than a feature that has been fully merged into official llama.cpp.

## Original research: TheToms information we need

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/TheTom llama-cpp-turboquant/TheToms information we need.docx`

**Repository:** llama-cpp-turboquant  
**URL:** [https://github.com/TheTom/llama-cpp-turboquant](https://github.com/TheTom/llama-cpp-turboquant)<br>
**Owner:** TheTom  
**Branch:** feature/turboquant-kv-cache  
**Commit:** Pin the exact commit used during testing; not selected yet  
**Last relevant update:** Active development in June 2026  
**Licence:** MIT

**Purpose:** Run GGUF models in llama.cpp with TurboQuant+ weight and KV-cache compression.  
**Claimed method:** Walsh–Hadamard rotation, PolarQuant-style codebooks, asymmetric K/V compression and hardware kernels.  
**Actual method:** Normalise each cache block, rotate it, map values to low-bit centroids and store packed indices plus the block norm.  
**Difference from formal TurboQuant:** Production mode does not normally use the formal QJL correction stage and adds several custom engineering improvements.

**Weight quantisation:** TQ3_1S, TQ4_1S  
**KV-cache quantisation:** turbo2, turbo3, turbo4  
**Key-cache types:** Prefer f16 or q8_0; Turbo types technically available  
**Value-cache types:** turbo4, turbo3, turbo2  
**Bits per value:** Approximately 2, 3.5 and 4.5 bits, depending on format  
**Block structure:** Mainly 128-value blocks  
**Metadata overhead:** A small norm and packing metadata per block  
**Actual memory packing:** Yes, compressed indices are physically packed in memory.

**CPU support:** Yes  
**CUDA support:** Yes; suitable for the RTX 4060  
**ROCm support:** Yes  
**SYCL support:** Incomplete and experimental  
**OpenVINO support:** Standard GGUF types only; Turbo types not documented as supported  
**NPU support:** Standard OpenVINO route only; no TurboQuant NPU support confirmed.

**Windows build:** Supported  
**Required tools:** Git, CMake, Visual Studio C++ Build Tools, CUDA Toolkit  
**Build command:**

cmake -B build -DGGML_CUDA=ON  
cmake --build build --config Release -j

**Run command:**

llama-cli.exe -m granite.gguf -ngl 99 -fa on -c 8192 \`  
--cache-type-k q8_0 --cache-type-v turbo3 -p "Test prompt"

**Granite compatibility:** Likely through normal llama.cpp support, but TurboQuant compatibility must be tested  
**Tested Granite model:** None documented  
**Supported head dimensions:** Mainly designed around 128-value groups; Granite 3B compatibility must be checked  
**Flash Attention:** Yes on CUDA, Metal, ROCm and parts of Vulkan  
**Maximum tested context:** Published tests reach 128K context.

**Published benchmarks:** Yes, mainly in TheTom/turboquant_plus  
**Quality tests:** Perplexity, KL divergence, output coherence and needle retrieval  
**Memory tests:** KV-cache size and peak-memory comparisons  
**Speed tests:** Prefill and generation tokens per second  
**Known bugs:** Incomplete SYCL support, backend-specific failures and no published Granite validation.

**Files containing main implementation:**  
ggml/src/ggml-turbo-quant.c  
ggml/src/ggml-common.h  
src/llama-kv-cache.cpp  
ggml/src/ggml-cuda/

**Ease of integration:** Medium; easiest through llama-server as a separate backend process  
**Maintenance risk:** High because it is a WIP fork and not fully upstreamed

**How we could use it:** Test Granite GGUF with f16/f16, f16/turbo4, q8_0/turbo4, q8_0/turbo3 and q8_0/turbo2 on the Lenovo, then repeat the best configurations on Intel.

**Final recommendation:** Use it as the main GGUF and TurboQuant KV-cache prototype. Do not depend on it for finished Intel SYCL, OpenVINO or NPU TurboQuant support.

Pasted text(21).txt

Document

Pasted text (2)(2).txt

Document

Im currently reading research papers on turbo quant. here is someproject information im working on:

2504.19874v1.pdf

PDF

2406.03482v2.pdf

PDF

2502.02617v1.pdf

PDF

Can you internalise these pdfs and learn it properly and use everything that is on there to explain concepts and decode it for me

2502.02617v1(1).pdf

PDF

okay so im reading the polar quant article and did a quick, skim. So from my surface level understanding. We transform coordinates of the kv vectors into polar coordinates with an angle. We then twist the vector using the angle and compress the angle. Is this correct?

> **Archived image:** `21fac5df90ef3b7b3fdb48765f76fe8c42744938.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.

random precntioning is mentioned many times in the report, and i will show you everything mentioned and i want you to disect everything so i am able to understand it, as well as visualisation images that im going to send:RandomPreconditioning. We apply a random rotation to the vectors before quantization, which preserves inner products while randomizing the distribution of each vector. This preconditioning causes the angles in polar coordinates to concentrate, allowing us to quantize them with high precision using small bit-widths. We derive the analytical distribution of angles after preconditioning and leverage this insight to construct an optimized quantization codebook, minimizing quantization error. RandomPreconditioning Acritical stepinthePolarQuantalgorithmisrandompreconditioningof theKVvectorspriorto quantization.This involvesapplyingarandomprojectionmatrixtotheembeddingvectorsbefore quantizingthem. Toanalyzethealgorithmeffectively,werelyonspecific factsandpropertiesof multivariatenormalrandomvariables,whichareoutlinedbelow. Fact 1. Foranypositive integerd, ifx∈Rd is a zeromeanunit variance isotropicGaussian randomvariableindimensiond, i.e.,x∼N(0,Id), thenits2-norm,denotedbyr:=∥x∥2, follows ageneralizedgammadistributionwiththefollowingprobabilitydensityforanyr≥0: fR(r)= 2 2d/2·Γ(d/2) rd−1exp−r2/2 TheproofofFact1isprovidedinAppendixA.Wealsousethefollowingfactsaboutthemoments oftheunivariatenormaldistribution. Fact2(MomentsofNormalRandomVariable). Ifxisanormal randomvariablewithzeromean andunitvariancex∼N(0,1), thenforanyintegerℓ,Ex∼N(0,1) \|x\|ℓ =2ℓ/2Γ((ℓ+1)/2)/√π. PolarQuantalgorithmappliesarandompreconditioningpriortoquantization.Thispreconditioning involvesmultiplyingeachembeddingvectorbyasharedrandomsketchmatrixSwithi.i.d. normal entries. BytheJohnson-Lindenstrauss(JL) lemma\[10\], thispreconditioningpreservesthenorms and inner products of the embeddingvectorswithminimal distortion. Akeypropertyof this preconditioning,whichwewill leverage inour lateranalysis, is that theembeddingvectorsafter preconditioningfollowamultivariatenormaldistribution.Thisisformalizedinthefollowingfact. Fact 3. For any vectorx∈Rd if S∈Rm×d is a randommatrixwith i.i.d. normal entries Si,j∼N(0,1), thenthevectorS·xhasmultivariatenormaldistributionS·x∼N(0,∥x∥2 ·Im). Thefollowinglemmaestablishesthedistributionof thepolarangleofapoint(x,y) indimension 2,wherethexandycoordinatesareindependentsamplesfromtheEuclideannormofmultivariate normalrandomvariables. 4 Lemma 1. For any positive integer d, if x,y ≥ 0 are two i.i.d. random variables with generalized gamma distribution with probability density function fZ(z) = 2 2d/2·Γ(d/2) zd−1 exp−z2/2 , then the angle variable θ := tan−1(y/x) follows the probability density function: fΘ(θ) = Γ(d) 2d−2 · Γ(d/2)2 · sind−1(2θ). Additionally, E\[Θ\] = π/4 and Var(Θ) = O(1/√d). See Appendix B for a proof 3.2 Distribution of Polar Angles Under Random Preconditioning One of our primary objectives is to eliminate the need for explicit normalization (e.g., mini mum/maximum values) of the KV cache data prior to quantization, thereby reducing quantization overhead. To achieve this, our algorithm applies random preconditioning to the embedding vectors. This preconditioning involves multiplying each embedding vector by a shared random sketch matrix S with i.i.d. normal entries. By the Johnson-Lindenstrauss (JL) lemma \[10\], this preconditioning preserves the norms and inner products\* of the embedding vectors with minimal distortion. A key property of this preconditioning, which we will leverage in our later analysis, is that the embedding vectors after preconditioning follow a multivariate normal distribution. This has been formalized in Fact 3. During the preconditioning stage, the sketch is applied to all embedding vectors in the KV cache, allowing the analysis of PolarQuant to effectively treat the vectors being quantized as samples from a multivariate normal distribution. So for the analysis and design of PolarQuant we can assume without loss of generality that our goal is to quantize a random vector with multivariate Gaussian distribution. A critical insight is that the distribution of angles after random preconditioning be comes predictable and can be analytically derived, which enables the design of optimal quantization schemes. The polar distribution of a Gaussian vector is derived in the following lemma. Lemma 2 (Distribution of a Gaussian Vector Under Polar Transformation). For an integer power of two d, suppose that x ∼ N(0,Id) is a random zero mean isotropic Gaussian random variable in dimension d. Let ψd(x) := ψ(1),ψ(2),...ψ(log2d) denote the set of polar angles obtained by applying the polar transformation defined in Definition 1 on x. Denote the radius of x by r = ∥x∥2. The joint probability density function for r,ψ(1),ψ(2),...ψ(log2d) is the following: log2 d fR,Ψd (r,ψd(x)) = fR(r) · ℓ=1 fΨ(ℓ) ψ(ℓ) , (1) where fR(r) is the p.d.f. defined in Fact 1, fΨ(1) is p.d.f. of the uniform distribution over \[0,2π)d/2: fΨ(1) : \[0,2π)d/2 → (2π)−d/2, \*For our implementation, we use random rotation matrices (square matrices P satisfying P⊤P = I), which preserve the norms and inner products exactly while removing the independence across projected coordinates which we use for our theoretical results. 6 and for every ℓ ∈ {2,3,...log2 d} the p.d.f. fΨ(ℓ) is the following: fΨ(ℓ) : \[0,π/2\]d/2ℓ → R+ d/2ℓ fΨ(ℓ)(ψ) = i=1 Γ(2ℓ−1) 22ℓ−1−2 · Γ(2ℓ−2)2 sin(2ℓ−1−1)(2ψi). Proof. The proof is by induction on d. First for the base of induction we prove the result in dimension d = 2. So we prove that for a 2-dimensional random Gaussian vector y = (y1,y2) ∈ R2 if (r, θ) is the polar representation of this vector then following holds: fR,Θ(r,θ) = 1 2π ·rexp−r2/2 , To prove this, let fY (y) be the probability density function of the vector random variable y. We know y has a normal distribution so we have: fR,Θ(r,θ) = r · fY (y) = r · 1 2πe−y2 1+y2 2 2 = 1 2π ·re−r2 2 , where the first equality above follows from the change of variable from (y1,y2) to r = θ = tan−1(y2/y1). This proves the base of induction for d = 2. y2 1 + y2 2 and Now we prove the inductive step. Suppose that the lemma holds for dimension d/2 and we want to prove it for dimension d. Denote θ := ψ(log2d), ϕ1 := ψ(l) 1:d/2l+1 log2 d−1 ℓ=1 log2 d−1 , ϕ2 := ψ(l) d/2ℓ+1+1:d/2ℓ ℓ=1 r1 := x1:d/2 , and r2 := xd/2+1:d . Essentially we sliced all the angle vectors ψ(ℓ) in half and named the collection of first half vectors ϕ1 and the collection of second halves ϕ2. Using the definition of ψ(ℓ)’s in Definition 1, ϕ1 is exactly the polar transformation of x1:d/2, and ϕ2 is the polar transformation of xd/2+1:d, so by the definition of ψd(x) in the lemma statement we have ϕ1 =ψd/2(x1:d/2) and ϕ2 = ψd/2(xd/2+1:d). Thus, we can write: fR,Ψd (r,ψd(x)) = fR,Θ,Φ1,Φ2 (r,θ,ϕ1,ϕ2) =r·fR1,R2,Φ1,Φ2 (r cosθ,r sinθ,ϕ1,ϕ2) =r·fR1,Φ1 (rcosθ,ϕ1) · fR2,Φ2 (rsinθ,ϕ2) =r·fR,Ψd/2 (r1,ϕ1) · fR,Ψd/2 (r2,ϕ2), (2) where the third line above follows from the change of variable from (r1,r2) = (rcosθ,rsinθ) to r = r2 1+r2 2 and θ = tan−1(r2/r1). In the fourth line above we used the definition of θ = ψ(log2 d) = tan−1 ∥xd/2+1:d ∥2 ∥x1:d/2 ∥2 from Definition 1. Now if we let fΨd/2 (ϕ1) := log2d−1 ℓ=1 fΨ(ℓ) ψ(ℓ) 1:d/2ℓ+1 and fΨd/2 (ϕ2) := log2d−1 ℓ=1 fΨ(ℓ) ψ(ℓ) , d/2ℓ+1+1:d/2ℓ , by the inductive hypothesis we have fR,Ψd/2 (r1,ϕ1) = 2 2d/4·Γ(d/4) rd/2−1 1 exp−r2 1/2 · fΨd/2 (ϕ1) and 7 fR,Ψd/2 (r2,ϕ2)= 2 2d/4·Γ(d/4) rd/2−1 2 exp−r2 2/2 ·fΨd/2 (ϕ2).PluggingthesevaluesintoEq. (2)gives: fR,Ψd (r,ψd(x))=4·(r1r2)d−1 2d/2Γ(d/4)2 exp−r2/2 ·fΨd/2 (ϕ1)·fΨd/2 (ϕ2) =2rd−1·sind/2−1(2θ) 23d/2−2·Γ(d/4)2 e−r2/2·fΨd/2 (ϕ1)·fΨd/2 (ϕ2) =fR(r)· Γ(d/2)·sind/2−1(2θ) 2d/2−2·Γ(d/4)2 fΨd/2 (ϕ1)·fΨd/2 (ϕ2) =fR(r)·fΨd (ψd(x)), (3) whichcompletestheinductiveproofofthislemma. Lemma2demonstratesthattheanglesofGaussianvectors inpolarcoordinateshaveindependent distributions,astheprobabilitydensityfunctionisseparable.Moreover,allangleswithinthesame level share identicaldistributions. Specifically, at level ℓall angles followthedistributionψ(ℓ) i ∼ d/2ℓ i=1 Γ(2ℓ−1) 22ℓ−1−2·Γ(2ℓ−2)2 sin2ℓ−1−1 2ψ(ℓ) i .Thisdensitybecomesincreasinglyconcentratedaroundπ/4, particularlyathigherlevelsℓ.Thispropertyishighlybeneficial forreducingquantizationerrorfor theanglesathigherlevels. PolarQuant startsbyfirst applying randompreconditioning, thentransforming thevectors into polar coordinates, andfinallyquantizingeachangle. SinceLemma2 shows that theangles in polar coordinatesare independent randomvariables, eachanglecanbequantized independently tominimizethetotalmeansquarederror. Jointlyquantizingmultipleanglecoordinatesoffersno additionalbenefitduetotheir independence,makingourapproachbothcomputationallyefficient andeffective. Therefore,we can focus ononeangleat level l anddesignoptimal quantization schemeforitsoastominimizethemeansquarederror. Consideranangleψ(ℓ) i atsomelevelℓ.AccordingtoLemma2,itsvaluesliewithintherange\[0,π/2\] forℓ≥2andforℓ=1ittakesvaluesintherange\[0,2π)withaprobabilitydensityfunctiongiven byfℓ(ψ(ℓ) i ):= Γ(2ℓ−1) 22ℓ−1−2·Γ(2ℓ−2)2 sin2ℓ−1−1 2ψ(ℓ) i .Thegoalofquantizationtob-bitsistopartitionthe range \[0,π/2\] (or \[0,2π) incaseof ℓ=1) into2b intervalsI(ℓ) 1 ,I(ℓ) 2 ,···I(ℓ) 2b andfindcorresponding centroidsθ(ℓ) 1 ,θ(ℓ) 2 ,...θ(ℓ) 2b suchthatthefollowingismeansquarederrorisminimized: E ψ(ℓ) i ∼fℓ(ψ(ℓ) i )    j∈\[2b\]:ψ(ℓ) i ∈I(ℓ) j ψ(ℓ) i −θ(ℓ) j 2   . (4) Thisproblemisacontinuousanalogof thek-meansclusteringproblemindimension1. Sincewe haveanexplicit formulafor thep.d.f. ofangleψ(ℓ) i ∼fℓ(ψ(ℓ) i )= Γ(2ℓ−1) 22ℓ−1−2·Γ(2ℓ−2)2 sin2ℓ−1−1 2ψ(ℓ) i theoptimalintervalpartitionsandcentroidsforEq.(4)canbeefficientlycomputedusingnumerical 8 Algorithm1PolarQuant 1: input: embeddingX∈Rn×d,preconditionmatrixS∈Rd×d,bitwidthb //CartesiantoPolartransform 2:Ri,Ψ(1) i ,...,Ψ(log2d) i ←Polar(Xi ·S)fori∈\[n\] //CodebookConstruction 3: Findpartitionintervalsandcentroids(I(ℓ) k ,θ(ℓ) k )k∈\[2b\] ofΨ(ℓ)∈Rn×(d/2ℓ) thatminimizethecost inEq. (4)forℓ∈\[log2d\] (SeeSection4.1fordetails) //AnglesQuantization 4: J(ℓ) i ←Quant Ψ(ℓ) i ,(I(ℓ) k ,θ(ℓ) k )k∈\[2b\] fori∈\[n\]andℓ∈\[log2d\] 5: output:R∈Rn×1,J(1)∈\[2b\]n×d/2,...,J(log2d)∈\[2b\]n×1,(I(ℓ) k ,θ(ℓ) k )k∈\[2b\] 6:ProcedurePolar(y) 7: r(0)←y∈Rd 8: forℓ=1,...,log2ddo 9: forj=1,...,d/2ℓdo 10: ψ(ℓ) j ←tan−1 r(ℓ−1) 2j /r(ℓ−1) 2j−1 11: r(ℓ) j ← r(ℓ−1) 2j−1:2j 2 12: endfor 13: endfor 14: output: r(log2d),ψ(1),...,ψ(log2d) 15:ProcedureQuant ψ,(Ik,θk)k∈\[2b\] 16: ji←argmink∈\[2b\] \|θk−ψi\| fori∈\[d′\]s.t.ψ∈Rd′ 17: output: j 18:ProcedureDeQuant(r,(j(ℓ))ℓ∈\[log2d\],(θ(ℓ) k )k∈\[2b\],S) 19: forℓ=log2d,...,1do 20: forj=1,...,d/2ℓdo 21: i←j(ℓ) j 22: r(ℓ−1) 2j−1←r(ℓ) j ·cosθ(ℓ) i 23: r(ℓ−1) 2j ←r(ℓ) j ·sinθ(ℓ) i 24: endfor 25: endfor 26: output: r(0) ·S⊤ methods. Forexample,onecanrunk-meansclusteringonthegatheredanglevalueswhichcanbe consideredsamples fromthedistribution. Thisapproachensuresminimal quantizationerror for eachangleindependentlyandtheoverallreconstructionerroraswell. WeprovideapseudocodeofPolarQuant inAlgorithm1. Ourmainresultanderrorboundare provedinthefollowing

> **Archived image:** `21fac5df90ef3b7b3fdb48765f76fe8c42744938.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.

okay lets disect this further. So explain figure 1, but in each stepin figure 1 explain it in a more focused deeper way so i understand exactly what is going on, and exactly hw polar quant implements it using their notations and formulas.

4\. TurboQuant.pdf

PDF

2504.19874v1(1).pdf

PDF

okay lets move into the actual turboquant article, so weve done polar quant and qjl. From my understanding polarquant is not used in the originl turboquant paper, its a different approach for step 1 right, this is what ive gone through so ffar regarding turboquant:

> **Archived image:** `21fac5df90ef3b7b3fdb48765f76fe8c42744938.png` is preserved in the controlled provenance ZIP and is not duplicated in Git.

i feel like with this one, we should at least try to compress something whilst keeping quality at a maximum whilst also keeping right below the ram requirements

Thought for 32s

Yes. The missing part is that **the model format must be checked before the application chooses an optimisation route**.

The decision order should be:

1\. What format has the user imported?  
2. Does that model fit safely at the requested context length?  
3. Which performance profile has the user selected?  
4. Which compatible backend and hardware route best satisfies that profile?

The performance profile expresses the user’s goal, but the file format determines what the application can technically do.

TurboQuant primarily reduces the growing KV cache rather than replacing conventional model-weight quantisation, so the architecture must continue treating these as separate decisions. This also aligns with the project requirement to investigate at least one TurboQuant/TurboVec workflow and one OpenVINO workflow without assuming that every format follows the same processing path.

User imports or selects a Granite model  
\|  
v  
Detect imported model format  
\|  
+----------------------+----------------------+----------------------+  
\| \| \|  
v v v  
GGUF Hugging Face folder OpenVINO IR  
or model XML + BIN  
\| \| \|  
v v v  
Inspect current weight precision, model architecture and size  
\|  
v  
Inspect device  
- available RAM  
- available VRAM or shared GPU memory  
- Intel CPU  
- Intel GPU  
- Intel NPU  
- OpenVINO availability  
- SYCL availability  
\|  
v  
Read requested context length and selected profile  
\|  
v  
Estimate total memory

model weights  
+  
KV cache  
+  
runtime overhead  
+  
Windows/application safety margin  
\|  
v  
Does the candidate configuration fit safely?  
\|  
+------------------------------+  
\| \|  
YES NO  
\| \|  
v v  
Apply selected profile Search for the least aggressive  
using compatible routes configuration that satisfies  
the selected profile and fits safely

### Route 1: User imports a GGUF model

A GGUF model is normally routed through llama.cpp. Its weight precision has usually already been selected, for example Q8_0, Q6_K, Q5_K_M or Q4_K_M.

GGUF imported  
\|  
v  
Inspect existing GGUF weight precision  
\|  
v  
Estimate weights + requested KV cache + overhead  
\|  
v  
Does it fit safely?

#### GGUF fits safely

GGUF fits safely  
\|  
+------------------+------------------+------------------+  
\| \| \|  
v v v  
Quality Balanced Efficiency

##### Quality Mode

Use the imported GGUF without further  
weight compression  
\|  
Standard llama.cpp  
\|  
Use high-precision KV cache  
- F16 where practical  
- Q8_0 if a small saving is required  
\|  
Use Intel CPU or another validated backend

The purpose is to preserve the model’s existing quality.

Quality Mode does not mean that nothing happens. It means:

Load and run the model  
without applying additional aggressive optimisation

##### Balanced Mode

Keep the existing GGUF weight precision  
\|  
llama.cpp  
\|  
Apply conservative TurboQuant KV-cache compression  
\|  
Prefer:  
K cache = F16 or Q8_0  
V cache = turbo4 or turbo3  
\|  
Intel CPU fallback  
or Intel SYCL GPU when validated

This is likely the default route for many smaller and medium Granite models.

##### Efficiency Mode

Check whether the GGUF can run through  
a validated Intel OpenVINO backend  
\|  
+------------------------------+  
\| \|  
Yes No  
\| \|  
v v  
OpenVINO Intel device llama.cpp  
CPU/GPU/NPU + lower-memory cache settings  
\| \|  
Standard KV cache initially TurboQuant on K/V as validated  
\|  
TurboQuant only when the  
OpenVINO integration is validated

An important limitation is that OpenVINO cannot automatically improve the existing GGUF weight precision. If the imported file is Q8_0, it remains Q8_0 unless another model version is created from suitable source weights.

#### GGUF does not fit safely

##### Quality Mode

Keep the highest possible weight quality  
\|  
Try progressively:  
1. Slightly reduce requested context  
2. Keep existing weights and use Q8_0 KV cache  
3. Keep existing weights and use conservative TurboQuant  
4. Recommend a Q8_0, Q6 or Q5 GGUF variant if available  
\|  
Select the highest-quality configuration that fits  
\|  
If none fits:  
recommend a smaller model or lower profile

The application should not jump immediately to Q4 or aggressive KV compression.

##### Balanced Mode

Try moderate optimisation  
\|  
1. Use or create Q5/Q4 GGUF where conversion is supported  
2. Apply conservative TurboQuant  
3. Use Q8_0 keys and turbo3/turbo4 values  
4. Reduce context only where still necessary  
\|  
Run through llama.cpp

##### Efficiency Mode

Use the strongest validated GGUF route  
\|  
Lower-bit GGUF such as Q4  
\|  
llama.cpp + TurboQuant  
\|  
Best validated CPU or SYCL device

If the user wants an OpenVINO INT4 model, the application may need the original Hugging Face model or an existing OpenVINO IR version. Converting a heavily quantised GGUF into a clean OpenVINO INT4 model should not be assumed.

### Route 2: User imports a Hugging Face model folder

A Hugging Face folder is the most flexible input because it can potentially be converted into either:

GGUF for llama.cpp

or:

OpenVINO IR for OpenVINO Runtime

The application should not try to chat directly with the unprocessed source folder unless it deliberately supports a separate Transformers runtime.

Hugging Face model imported  
\|  
v  
Inspect architecture, original precision  
and conversion compatibility  
\|  
v  
Estimate memory for possible outputs  
- high-precision GGUF  
- quantised GGUF  
- OpenVINO FP16/INT8/INT4  
\|  
v  
Does an appropriate high-quality representation fit?

#### Hugging Face model can fit safely

##### Quality Mode

Create the highest-quality supported representation  
\|  
Possible candidates:  
- FP16/BF16 GGUF  
- Q8_0 GGUF  
- FP16 OpenVINO IR  
- INT8 OpenVINO IR where quality is validated  
\|  
Select the candidate with the lowest measured quality loss  
\|  
Normally:  
GGUF + llama.cpp + high-precision KV cache

##### Balanced Mode

Create a moderately compressed representation  
\|  
Option A:  
Q5/Q4/Q8 GGUF  
+  
llama.cpp  
+  
conservative TurboQuant KV cache

Option B:  
OpenVINO INT8  
+  
normal OpenVINO KV cache  
\|  
Choose using benchmark results

The GGUF TurboQuant route is likely to be the first Balanced implementation because TurboQuant is the central research focus.

##### Efficiency Mode

Prefer OpenVINO export  
\|  
INT4 or INT8 OpenVINO IR  
\|  
Select best validated Intel device  
- GPU  
- CPU  
- NPU where supported  
\|  
Use normal OpenVINO KV cache initially  
\|  
Add TurboQuant when OpenVINO integration  
is validated

Fallback:

Q4 GGUF  
+  
llama.cpp  
+  
TurboQuant

#### Hugging Face model does not fit safely in high precision

##### Quality Mode

Search for least aggressive conversion that fits  
\|  
Try:  
1. Q8_0 GGUF or INT8 OpenVINO  
2. Conservative TurboQuant for the KV cache  
3. Q6/Q5 GGUF only if required  
4. Slightly reduce context if still necessary  
\|  
Choose candidate with lowest measured quality loss

##### Balanced Mode

Generate moderate candidates  
\|  
Q5/Q4 GGUF  
+  
llama.cpp  
+  
TurboQuant

or

OpenVINO INT8/INT4  
+  
normal KV cache  
\|  
Choose the best quality-memory compromise

##### Efficiency Mode

OpenVINO INT4 export  
\|  
Best validated Intel device  
\|  
Optional TurboQuant OpenVINO integration  
\|  
Fallback:  
Q4 GGUF + llama.cpp + TurboQuant

This is the format where the application has the greatest freedom to create both output routes.

### Route 3: User imports an OpenVINO IR model

An OpenVINO IR model normally consists of:

model.xml  
+  
model.bin

Its weight representation may already be FP16, INT8 or INT4. The application should inspect it before deciding whether further weight compression is possible or necessary.

OpenVINO IR imported  
\|  
v  
Inspect:  
- existing precision  
- architecture support  
- available OpenVINO devices  
- KV-state accessibility  
\|  
v  
Estimate total OpenVINO memory  
\|  
v  
Does it fit safely?

#### OpenVINO IR fits safely

##### Quality Mode

Run the imported IR without further  
weight compression  
\|  
OpenVINO Runtime  
\|  
Use normal OpenVINO KV cache  
\|  
Select highest-quality validated device

If the imported IR is already INT4, Quality Mode cannot restore the information lost during its original compression. The application should clearly show:

Imported precision: INT4  
Quality Mode will preserve this model as imported  
but cannot return it to FP16

##### Balanced Mode

Keep existing OpenVINO weights  
\|  
OpenVINO CPU or GPU  
\|  
Use normal KV cache by default  
\|  
Apply moderate TurboQuant state processing  
only if validated for Granite

If the TurboQuant OpenVINO implementation remains slower or fails to reduce actual memory, Balanced Mode should retain the normal OpenVINO cache.

##### Efficiency Mode

Use the most efficient validated Intel device  
\|  
Prefer GPU or NPU where benchmarks support it  
\|  
If model is FP16/INT8 and recompression is supported:  
offer INT8/INT4 conversion  
\|  
Use TurboQuant OpenVINO cache only when validated

#### OpenVINO IR does not fit safely

##### Quality Mode

Preserve existing model precision where possible  
\|  
Try:  
1. Reduce requested context slightly  
2. Use conservative KV-cache compression  
if OpenVINO TurboQuant is validated  
3. Move to INT8 only if necessary and supported  
\|  
If no acceptable configuration fits:  
recommend smaller OpenVINO model  
or a more compressed IR variant

##### Balanced Mode

Try moderate OpenVINO weight compression  
\|  
FP16 -\> INT8  
or INT8 -\> INT4 where supported  
\|  
Use OpenVINO CPU/GPU  
\|  
Add conservative TurboQuant only if validated

##### Efficiency Mode

Use INT4 OpenVINO representation  
\|  
Select best validated Intel device  
- GPU  
- NPU  
- CPU fallback  
\|  
Use experimental TurboQuant KV cache  
only if it provides a measured benefit  
\|  
If still unsafe:  
recommend smaller model

### Complete combined diagram

User imports Granite model  
\|  
v  
Detect format  
\|  
+------------------------+-------------------------+------------------------+  
\| \| \|  
v v v  
GGUF Hugging Face OpenVINO IR  
\| \| \|  
\| \| \|  
Existing weight Source model can Existing IR precision  
precision fixed produce either route already selected  
\| \| \|  
v v v  
Estimate memory for model weights, KV cache,  
runtime overhead and safety margin  
\|  
v  
Does candidate configuration fit safely?  
\|  
+-----------------------------------------------+  
\| \|  
YES NO  
\| \|  
v v  
Use normal profile logic Search for least aggressive  
profile-compatible configuration

GGUF  
------------------------------------------------------------

Fits:  
Quality -\> Existing weights + standard llama.cpp  
+ F16/Q8 KV cache

Balanced -\> Existing weights + llama.cpp  
+ conservative TurboQuant

Efficiency -\> Validated OpenVINO GGUF route where available  
+ optional OpenVINO TurboQuant  
otherwise lower-memory llama.cpp TurboQuant

Does not fit:  
Quality -\> Reduce context or compress KV cache first  
then use highest-quality GGUF variant that fits

Balanced -\> Q5/Q4 GGUF + llama.cpp + TurboQuant

Efficiency -\> Low-bit GGUF + aggressive validated TurboQuant  
or request HF/OpenVINO source for INT4 OpenVINO

HUGGING FACE  
------------------------------------------------------------

Fits:  
Quality -\> Create high-precision GGUF or OpenVINO IR

Balanced -\> Q5/Q4/Q8 GGUF + llama.cpp + TurboQuant  
or OpenVINO INT8

Efficiency -\> OpenVINO INT4/INT8  
+ best Intel device  
+ optional TurboQuant

Does not fit:  
Quality -\> Q8/INT8 first  
+ conservative TurboQuant if necessary

Balanced -\> Q5/Q4 GGUF + TurboQuant  
or OpenVINO INT8/INT4

Efficiency -\> OpenVINO INT4 + optional TurboQuant  
fallback Q4 GGUF + TurboQuant

OPENVINO IR  
------------------------------------------------------------

Fits:  
Quality -\> Run imported IR as-is  
+ normal OpenVINO KV cache

Balanced -\> Existing IR + OpenVINO CPU/GPU  
+ optional moderate TurboQuant

Efficiency -\> Best Intel device  
+ further INT4 compression if supported  
+ optional TurboQuant

Does not fit:  
Quality -\> Preserve precision  
+ reduce context or conservatively compress KV cache

Balanced -\> Moderate INT8/INT4 compression  
+ optional TurboQuant

Efficiency -\> INT4 + best Intel device  
+ TurboQuant only if measured to help

### Automatic Mode across all formats

Automatic Mode should not have one fixed backend. It should generate only candidates that are valid for the imported format.

Automatic Mode  
\|  
v  
Generate format-compatible candidate configurations  
\|  
+-- GGUF:  
\| llama.cpp standard  
\| llama.cpp TurboQuant  
\| OpenVINO backend where supported  
\|  
+-- Hugging Face:  
\| GGUF candidates  
\| OpenVINO IR candidates  
\|  
+-- OpenVINO IR:  
OpenVINO CPU/GPU/NPU candidates  
optional TurboQuant candidates  
\|  
v  
Reject candidates that:  
- exceed safe memory  
- are unsupported  
- fail startup self-test  
- produce unacceptable quality loss  
\|  
v  
Score successful candidates using:  
- quality  
- memory  
- speed  
- context capacity  
- energy use  
- stability  
\|  
v  
Recommend the best candidate

### Custom Mode across all formats

Custom Mode should only display technically valid options.

For example, for a GGUF file:

Backend:  
llama.cpp CPU  
llama.cpp SYCL  
OpenVINO backend where supported

KV cache:  
F16  
Q8_0  
turbo4  
turbo3

For an OpenVINO IR model:

Backend:  
OpenVINO CPU  
OpenVINO GPU  
OpenVINO NPU

Weight precision:  
existing precision  
INT8/INT4 conversion where supported

KV cache:  
OpenVINO standard  
experimental TurboQuant where supported

The final decision hierarchy is therefore:

Model format  
↓  
Technical compatibility  
↓  
Does it fit safely?  
↓  
User profile  
↓  
Least aggressive configuration  
that satisfies the profile  
↓  
Validated hardware backend

This avoids incorrectly assuming that every model can be passed through every backend or converted freely between GGUF and OpenVINO IR.

Thought for 4s

Yes, that is the right **main division**, with one important correction:

GGUF workflows can be developed and initially tested on your Lenovo, but the final successful GGUF configurations should still be repeated on the Intel laptop.

#### Lenovo laptop

Use your Lenovo for:

GGUF + standard llama.cpp  
GGUF weight formats such as Q8_0, Q5_K_M and Q4_K_M  
GGUF + TurboQuant KV-cache compression  
TurboQuant CPU testing  
TurboQuant CUDA testing on the RTX 4060  
Granite model compatibility  
Quality testing  
WinUI development  
Memory-estimation and profile-selection logic  
Benchmarking tools

This establishes whether Granite works with the compression techniques and whether the application workflow functions correctly.

#### Intel laptop

Use the Intel laptop for:

OpenVINO IR INT8  
OpenVINO IR INT4  
OpenVINO CPU inference  
OpenVINO Intel GPU inference  
OpenVINO NPU inference  
OpenVINO + experimental TurboQuant  
Intel SYCL llama.cpp  
TurboQuant on Intel CPU  
TurboQuant on Intel GPU  
Final target-device RAM, speed and power testing

#### Important distinction

It is better to separate **file format** from **runtime backend**.

For example, a GGUF model could still be tested through:

GGUF + llama.cpp CPU  
GGUF + llama.cpp CUDA  
GGUF + llama.cpp SYCL  
GGUF + llama.cpp OpenVINO backend

Therefore, anything using the **OpenVINO backend**, even when the input file is GGUF, belongs in the Intel test group.

Similarly, you may be able to create an OpenVINO IR model on your Lenovo, but its meaningful CPU/GPU/NPU testing should happen on Intel hardware.

#### Recommended workflow

Lenovo  
\|  
Develop and test GGUF, llama.cpp and TurboQuant  
\|  
Identify configurations that work with Granite  
\|  
Push code and scripts to GitHub  
\|  
Intel laptop  
\|  
Repeat successful GGUF tests on Intel CPU/SYCL  
\|  
Test OpenVINO IR INT8/INT4  
\|  
Test Intel CPU, GPU and NPU  
\|  
Collect final project results

So the simplified rule is:

Lenovo:  
Main development and initial GGUF/TurboQuant testing

Intel laptop:  
OpenVINO testing and final Intel validation

But do not limit the Intel laptop to OpenVINO only. You also need it to confirm that the GGUF and TurboQuant routes work properly on the project’s intended Intel hardware.

okay so the gguf and llama.cppstuff can be tested on my lenovo and have the same results as if i were using intel?#

Thought for 4s

Not exactly.

The **same GGUF model and llama.cpp configuration should behave broadly the same functionally**, but the hardware results will not be the same.

#### What should transfer from Lenovo to Intel

Using the same:

- Granite GGUF file

- weight quantisation

- KV-cache type

- prompt

- context length

- generation settings

- llama.cpp version

you can test on your Lenovo whether:

- the model loads;

- Granite is supported;

- TurboQuant works without crashing;

- the output remains coherent;

- the selected cache type is accepted;

- the application communicates with llama.cpp correctly;

- the Quality, Balanced and Efficiency routing logic works.

These findings should usually transfer to the Intel device.

Works with Granite on Lenovo  
\|  
Strong evidence that the model and format are compatible  
\|  
But Intel backend must still be tested

#### What will not be the same

##### Performance

You cannot use Lenovo results to claim Intel performance.

Lenovo RTX 4060  
-\> CUDA

Intel integrated GPU  
-\> SYCL or OpenVINO

Lenovo Ryzen CPU  
-\> AMD CPU kernels

Intel CPU  
-\> Intel CPU kernels

Therefore, these results will differ:

- tokens per second;

- loading time;

- time to first token;

- CPU and GPU utilisation;

- power use;

- battery use;

- peak RAM and VRAM;

- maximum practical context length.

##### Memory behaviour

Your RTX 4060 has separate dedicated VRAM. An Intel integrated GPU commonly uses shared system memory.

For example:

Lenovo:  
System RAM + separate NVIDIA VRAM

Intel AI PC:  
System RAM shared with integrated GPU

A model fitting comfortably on the RTX 4060 does not prove it will fit on a 16 GB Intel laptop.

##### Output may not be perfectly identical

The answers should normally be very similar when generation is deterministic, but exact token-for-token output is not guaranteed because:

- CUDA, CPU and SYCL use different kernels;

- floating-point calculations may be performed in a different order;

- multithreading can introduce small numerical differences;

- a tiny difference in logits can change the selected next token.

For reliable comparison, use:

temperature = 0  
fixed seed  
same model  
same prompt  
same context  
same llama.cpp commit  
same cache settings

Even then, small backend differences are possible.

#### Best testing division

##### On your Lenovo

Fully develop and initially test:

Granite GGUF loading  
llama.cpp integration  
GGUF weight quantisation  
F16, Q8 and Q4 KV-cache baselines  
TurboQuant turbo4, turbo3 and turbo2  
CPU and CUDA functionality  
Output-quality tests  
WinUI application  
Benchmarking scripts

##### Repeat on the Intel device

Repeat the most important configurations:

GGUF + llama.cpp Intel CPU  
GGUF + llama.cpp SYCL GPU  
GGUF + TurboQuant Intel CPU  
GGUF + TurboQuant SYCL GPU

Then separately test:

OpenVINO IR INT8  
OpenVINO IR INT4  
OpenVINO CPU  
OpenVINO GPU  
OpenVINO NPU

#### Practical conclusion

Your Lenovo can establish:

“This Granite GGUF model and TurboQuant configuration work correctly.”

The Intel device must establish:

“This configuration works efficiently, fits in memory and performs suitably on the actual target hardware.”

So you do not need to repeat every small development test on Intel. You should repeat the **final shortlisted GGUF configurations** and collect the official Intel performance and memory results there.

Okay, lets look into this one first. First give me a full overview of this github, and where to look to find this information. <https://github.com/TheTom/llama-cpp-turboquant>

Received app response

Received app response

Received app response

Received app response

Received app response

Thought for 2m 36s

### Overview of TheTom/llama-cpp-turboquant

The most important point is:

This is a modified version of **llama.cpp** that adds experimental **TurboQuant+ weight and KV-cache compression** for GGUF models.

It is not a small TurboQuant demonstration. It contains almost the entire llama.cpp codebase, with extra TurboQuant code added to the model-loading, quantisation, KV-cache and hardware-backend systems.

It is also not a completely paper-faithful copy of the original TurboQuant algorithm. The developer describes it as **TurboQuant+**, meaning a practical and extended version inspired by TurboQuant, with additional compression policies and hardware optimisations.

#### 1. What is llama.cpp?

llama.cpp is a C and C++ inference engine that runs language models locally, particularly models stored in the **GGUF** format.

In simple terms:

GGUF model file  
↓  
llama.cpp loads the model  
↓  
The model generates text locally  
↓  
CPU, NVIDIA GPU, AMD GPU or another backend performs the calculations

This repository takes llama.cpp and adds new TurboQuant-related options:

Standard llama.cpp  
+  
TurboQuant KV-cache compression  
+  
TurboQuant-style weight compression  
+  
Hardware-specific TurboQuant kernels

The TurboQuant features are optional. Normal llama.cpp models, quantisations and commands are intended to continue working normally.

### 2. What this repository compresses

There are **two separate forms of compression** in the repository.

#### A. Model-weight compression

Model weights are the learned numbers permanently stored inside the model.

The fork adds two GGUF weight formats:

| **Weight type** | **Approximate precision**  | **Main purpose**                            |
|-----------------|----------------------------|---------------------------------------------|
| TQ4_1S          | Around 4.5 bits per weight | Better quality and faster supported kernels |
| TQ3_1S          | Around 3.5 bits per weight | Smaller model, with greater quality risk    |

Weight quantisation creates a **new GGUF model file**.

For example:

Original F16 GGUF model  
↓  
llama-quantize  
↓  
TQ4_1S GGUF model

The commands are:

llama-quantize model.f16.gguf model.tq4_1s.gguf TQ4_1S

llama-quantize model.f16.gguf model.tq3_1s.gguf TQ3_1S

These formats are additions made by this fork and are not standard portable GGUF formats that every normal llama.cpp build will necessarily understand.

#### B. KV-cache compression

The KV cache stores information from earlier tokens while the model is generating text. It grows as the context becomes longer.

KV-cache compression does **not normally change the GGUF model file**. It is selected when the model is run:

GGUF model remains unchanged  
↓  
The model begins running  
↓  
New keys and values are stored in a compressed KV cache

The fork provides:

| **KV-cache type** | **Approximate precision** | **Meaning**                                   |
|-------------------|---------------------------|-----------------------------------------------|
| turbo4            | Around 4.5 bits           | Lightest Turbo compression and safest quality |
| turbo3            | Around 3.5 bits           | Middle option and recommended default         |
| turbo2            | Around 2 bits             | Most aggressive compression                   |

These are selected using:

--cache-type-k  
--cache-type-v

The **K cache** stores keys and the **V cache** stores values. The repository recommends keeping K at a higher precision because errors in keys can damage how attention selects important tokens.

### 3. Recommended KV-cache configurations

The repository recommends testing progressively rather than immediately choosing the strongest compression.

#### Baseline

--cache-type-k f16 --cache-type-v f16

This gives you the uncompressed reference result.

#### Safest TurboQuant+ starting point

--cache-type-k f16 --cache-type-v turbo4

The keys remain full precision while only the values are compressed lightly.

#### Conservative compression

--cache-type-k q8_0 --cache-type-v turbo4

Both sides use less memory, but the key cache is kept at a relatively safe 8-bit precision.

#### Recommended default

--cache-type-k q8_0 --cache-type-v turbo3

This is presented by the repository as its normal balance between memory reduction and quality.

#### Aggressive compression

--cache-type-k q8_0 --cache-type-v turbo2

This targets long-context situations where memory is limited, but it requires more careful quality testing.

For your Granite tests, this gives you a sensible comparison ladder:

F16 K + F16 V  
↓  
F16 K + Turbo4 V  
↓  
Q8 K + Turbo4 V  
↓  
Q8 K + Turbo3 V  
↓  
Q8 K + Turbo2 V

### 4. How its TurboQuant+ compression works

The practical method used here can be simplified into four stages.

#### Stage 1: Take a KV-cache vector

For example:

\[0.42, -0.71, 0.16, 0.93, ...\]

This vector may contain 128 values representing part of one attention head.

#### Stage 2: Record its length

The system calculates the norm:

γ = \|\|x\|\|

This stores the overall size or magnitude of the vector.

The vector is then normalised:

x̂ = x / γ

That separates:

- the vector’s overall size;

- the pattern or direction of its values.

#### Stage 3: Rotate the values

#### Stage 4: Map each value to a small codebook

The stored representation contains approximately:

Compressed indices  
+  
vector or block norm  
+  
small amount of metadata

### 5. Important correction about QJL

One part of the source code still describes the implementation as:

KV cache compression via PolarQuant + QJL

You can see that at the beginning of:

ggml/src/ggml-turbo-quant.c

The file also contains QJL-related constants and structures.

However, the current TurboQuant+ research repository says:

QJL is kept as a paper-reference implementation, but the production implementation drops QJL.

Its developers report that QJL reduced bias but introduced additional variance, which could become attention noise after softmax. Their current production recommendation therefore uses the rotation, norm and PolarQuant-style codebook stages without QJL.

Therefore, in your report, the safest description is:

TheTom’s llama.cpp fork implements TurboQuant+, a practical extension inspired by TurboQuant. Its production KV-cache codecs use norm extraction, Walsh–Hadamard rotation and low-bit codebook quantisation, while QJL remains primarily for reference and experimental investigation.

You should not describe the current production fork as an exact implementation of the complete original two-stage TurboQuant paper without qualification.

### 6. What the repository adds beyond the paper

The fork contains several engineering additions.

#### Asymmetric K and V compression

Rather than compressing keys and values equally, the project generally recommends:

K = higher precision  
V = stronger compression

For example:

K = q8_0  
V = turbo3

The fork can also automatically replace an unsafe low-bit K configuration with q8_0 in certain model arrangements.

#### Boundary V

When turbo2 is used for values, the fork can protect particularly sensitive model layers by leaving them at a safer precision.

This attempts to gain most of the memory saving without compressing every layer equally aggressively.

#### Sparse V dequantisation

On supported Metal paths, values with extremely low attention weights may be skipped rather than fully reconstructed.

This is mainly a speed optimisation rather than simply a storage compression method.

#### Hardware-specific kernels

The fork contains additional implementations for:

- NVIDIA CUDA;

- Apple Metal;

- AMD HIP/ROCm;

- Vulkan.

The README describes different levels of kernel and Flash Attention coverage for these backends.

### 7. Current project status

The repository is marked as **work in progress**.

Its important working branch is:

feature/turboquant-kv-cache

Therefore, when cloning it, use:

### 8. Hardware support relevant to your project

#### Your Lenovo RTX 4060

Your Lenovo is suitable for investigating this fork through:

CPU  
+  
NVIDIA CUDA

The README provides this CUDA build option:

cmake -B build -DGGML_CUDA=ON  
cmake --build build --config Release -j

The merge notes describe CUDA implementations for turbo2, turbo3, turbo4, the TQ weight formats, Walsh–Hadamard transforms and related operations.

This makes the CUDA route the most appropriate place to begin.

#### Intel SYCL

The Intel situation is much less complete.

There is currently an open issue specifically asking for TurboQuant SET_ROWS support for turbo2, turbo3 and turbo4 on SYCL. The maintainer states that the project does not have its own Intel GPUs and that SYCL support is community-maintained.

There is also an open pull request titled:

SYCL Turboquant implementation attempt

It says it was tested to a limited extent on an Intel Arc A380 with oneAPI, but it remains unmerged and the contributor states that the code has not been properly reviewed.

That means:

CUDA TurboQuant  
→ available for your Lenovo testing

Intel SYCL TurboQuant  
→ currently incomplete and experimental

This Intel gap could actually become a valuable implementation or investigation area for your project, but you should not begin by assuming that the normal branch already provides complete Intel GPU support.

#### OpenVINO

The repository contains OpenVINO-related code inherited from upstream llama.cpp, but the TurboQuant+ backend-support table does not list OpenVINO as a supported TurboQuant backend.

Therefore, the safest project architecture is currently:

GGUF + llama.cpp + TurboQuant+  
→ test through CPU/CUDA first  
→ investigate SYCL separately on Intel

OpenVINO IR INT8/INT4  
→ use the normal OpenVINO/Optimum Intel route  
→ treat TurboQuant integration as a separate experiment

It would be unsafe to describe this repository as an established OpenVINO plus TurboQuant implementation based only on the existence of an OpenVINO folder. The public support table currently names Metal, CUDA, HIP/ROCm and Vulkan instead.

### 9. Where to look inside the GitHub repository

#### README.md

Start here.

Read these sections:

Lineage — why the +  
What this fork adds  
Quantization types  
Compression policies  
Backend coverage  
Quick start  
Usage  
KV-cache quantization  
Weight quantization  
Automatic behavior

This tells you:

- what the project claims to implement;

- available quantisation types;

- recommended configurations;

- build commands;

- supported backends;

- example commands.

#### TURBOQUANT_UPSTREAM_MERGE.md

Use this to understand:

- which branch is current;

- how far the fork has moved from normal llama.cpp;

- what hardware has been tested;

- which backend problems remain;

- which work has been deferred.

It is especially useful because it is more honest about unfinished backend work than the high-level README. For example, it records deferred Vulkan work and additional CUDA validation still required after an upstream merge.

#### ggml/src/ggml-turbo-quant.c

This is one of the most important implementation files.

Look here for:

- TurboQuant constants;

- 2-bit, 3-bit and 4-bit codebooks;

- normalisation;

- rotation generation;

- Walsh–Hadamard Transform operations;

- quantisation and reconstruction functions;

- QJL-related reference code.

For example, the 2-bit and 3-bit representative values are defined near the beginning of this file.

The CPU Walsh–Hadamard implementation is also in this file.

#### src/llama-kv-cache.cpp

This is where TurboQuant connects to the actual llama.cpp KV-cache system.

Look here for:

- creation of K and V caches;

- selecting the cache type;

- allocating cache memory;

- model-layer handling;

- automatic asymmetric K/V decisions;

- attention-rotation behaviour;

- layer-sensitive compression policies.

This is likely one of the most relevant files when you later study how the method is applied during real model inference.

#### ggml/include/ggml.h

Look here for the registered tensor types, including the TurboQuant and TQ weight types.

This tells the rest of llama.cpp that types such as the following exist:

TURBO2  
TURBO3  
TURBO4  
TQ3_1S  
TQ4_1S

#### ggml/src/ggml-quants.c

#### ggml/src/ggml-quants.h

These contain the wider GGML quantisation framework.

Use them to investigate:

- quantised block structures;

- packing and unpacking;

- quantise functions;

- dequantise functions;

- vector dot-product functions;

- how new quantisation types connect to GGML.

#### ggml/src/ggml-cuda/

This is the main folder for your RTX 4060 testing.

It contains NVIDIA implementations for:

- TurboQuant quantisation and dequantisation;

- matrix and vector operations;

- Flash Attention;

- cache-writing operations;

- CUDA kernels for the added TQ formats.

You do not need to understand every CUDA file before running the project. Initially, use this folder to confirm that a Turbo operation has a real CUDA implementation rather than silently falling back to the CPU.

#### ggml/src/ggml-cpu/

This contains CPU implementations and fallback paths.

It will help you separate:

The algorithm works on CPU

from:

The hardware-specific CUDA kernel works

#### ggml/src/ggml-sycl/

This is the Intel GPU backend area.

However, its existence does not mean TurboQuant is complete there. You should read it alongside:

Issue \#120  
Pull request \#144

Those two pages explain the missing Intel SET_ROWS support and the unmerged SYCL implementation attempt.

#### tools/quantize/

This is where the llama-quantize program is built.

Use it when studying:

F16 GGUF  
↓  
TQ4_1S or TQ3_1S GGUF

#### tests/test-turbo-quant.c

This is the most directly relevant TurboQuant test file.

Look here to see:

- which codec functions are tested;

- expected numerical behaviour;

- whether values can be quantised and reconstructed;

- allowed error margins;

- whether packing and unpacking work correctly.

#### Other useful tests

tests/test-quantize-fns.cpp  
tests/test-quantize-perf.cpp  
tests/test-quantize-stats.cpp  
tests/test-backend-ops.cpp

These are useful for understanding:

- correctness;

- quantisation error;

- performance;

- backend operation support.

### 10. The separate research repository

The main runtime repository regularly points to:

TheTom/turboquant_plus

That companion repository is described as the **research home**. It contains:

- the Python reference implementation;

- experimental reports;

- benchmark results;

- configuration recommendations;

- explanations of the design decisions.

The llama.cpp repository is mainly where the method is integrated into actual GGUF inference.

The most useful companion files are:

docs/getting-started.md  
docs/turboquant-recommendations.md  
docs/benchmarks.md  
docs/papers/asymmetric-kv-compression.md  
docs/papers/layer-aware-v-compression.md  
docs/papers/turbo4-resurrection.md  
docs/papers/block-size-experiment.md

Use the two repositories like this:

turboquant_plus  
→ Why the method works  
→ Experimental evidence  
→ Quality and compression recommendations

llama-cpp-turboquant  
→ C/C++ implementation  
→ GGUF model execution  
→ CUDA/CPU/backend integration  
→ Commands you can actually run

### 11. Best reading order for you

I recommend going through the repository in this order:

1\. README.md  
Understand what the fork claims to provide

2. Companion configuration recommendations  
Understand which cache combinations should be tested

3. tests/test-turbo-quant.c  
See the simplest correctness tests

4. ggml/src/ggml-turbo-quant.c  
Understand the codec itself

5. src/llama-kv-cache.cpp  
See how it is used by the model

6. ggml/src/ggml-cuda/  
See how it runs on your RTX 4060

7. Issue \#120 and PR \#144  
Understand the current Intel SYCL gap

8. Companion benchmarks  
Plan your own evaluation matrix

Do not start by reading every folder in the repository. Most of it is normal llama.cpp code and is not directly related to TurboQuant.

### 12. What this repository can provide for your project

This repository could support three different project investigations:

A. TurboQuant KV-cache compression  
turbo4, turbo3 and turbo2

B. TurboQuant+ GGUF weight compression  
TQ4_1S and TQ3_1S

C. Intel compatibility investigation  
whether the successful CPU/CUDA implementation  
can be completed or adapted for SYCL

Your first practical evaluation should concentrate on **KV-cache compression**, because that is closest to the original project brief:

Same Granite GGUF model  
Same prompt  
Same context  
Same generation settings

Compare:  
F16/F16  
F16/Turbo4  
Q8/Turbo4  
Q8/Turbo3  
Q8/Turbo2

That will show:

- whether Granite loads successfully;

- whether the Turbo cache types work;

- how much memory is saved;

- whether output quality changes;

- whether speed improves or declines;

- whether CUDA operations remain on the RTX 4060;

- which configurations should later be repeated on Intel.

The repository is promising for your project, but it should currently be treated as a **WIP research and production fork**, with strong CUDA relevance and incomplete Intel SYCL support—not as a finished cross-platform TurboQuant solution.

okay could you now provide me with what this repository could provide me for my project and fill out this too: Repository: URL: Owner: Branch: Commit: Last relevant update: Licence: Purpose: Claimed method: Actual method: Difference from formal TurboQuant: Weight quantisation: KV-cache quantisation: Key-cache types: Value-cache types: Bits per value: Block structure: Metadata overhead: Actual memory packing: CPU support: CUDA support: ROCm support: SYCL support: OpenVINO support: NPU support: Windows build: Required tools: Build command: Run command: Granite compatibility: Tested Granite model: Supported head dimensions: Flash Attention: Maximum tested context: Published benchmarks: Quality tests: Memory tests: Speed tests: Known bugs: Files containing main implementation: Ease of integration: Maintenance risk: How we could use it: Final recommendation:

Received app response

Received app response

Received app response

Thought for 1m 57s

#### What this repository could provide for your project

This repository could give you a **working end-to-end implementation of GGUF inference with TurboQuant-style KV-cache compression**. It would allow you to use the same Granite GGUF file and compare normal llama.cpp cache formats against turbo4, turbo3 and turbo2.

For your project, it could provide:

- a practical TurboQuant prototype rather than requiring you to implement the complete algorithm from the beginning;

- CPU and CUDA testing on your Lenovo;

- real packed KV-cache storage, so you can measure genuine memory reductions;

- llama-cli, llama-server and llama-bench for inference and benchmarking;

- custom TurboQuant+ GGUF weight formats;

- source code showing where the cache is compressed, packed, reconstructed and used by Flash Attention;

- a standard OpenVINO backend for Intel hardware, although the custom TurboQuant cache types are not currently supported by that backend;

- an identified Intel SYCL implementation gap that could become part of your investigation or contribution.

The best use would be to treat it as your **main GGUF and llama.cpp TurboQuant implementation**, while treating Intel SYCL and OpenVINO integration as separate work.

### Repository assessment

**Repository:**  
llama-cpp-turboquant

**URL:**  
[https://github.com/TheTom/llama-cpp-turboquant](https://github.com/TheTom/llama-cpp-turboquant)

**Owner:**  
TheTom

**Branch:**  
feature/turboquant-kv-cache

This is the active and default TurboQuant branch. The repository says it is approximately 300 commits ahead of upstream llama.cpp and has not been merged into the official llama.cpp project.

**Commit:**  
4595fff0bbd15ee01663699b788eea70e7e1cd69

This is the branch-head commit visible during this review. For your evaluation, you should save the complete commit hash so that later changes do not alter your results.

**Last relevant update:**  
18 June 2026 — merged Vulkan TurboQuant K/V dequantisation into the Flash Attention path.

The most recent TurboQuant-specific branch update shown in the history added fused TurboQuant K/V dequantisation for Vulkan Flash Attention.

**Licence:**  
MIT licence, matching upstream llama.cpp.

#### Method

**Purpose:**  
To integrate TurboQuant+ weight and KV-cache quantisation into llama.cpp, allowing GGUF models to run locally with reduced model-weight memory and reduced KV-cache memory.

It is the runtime and hardware-integration repository. The associated turboquant_plus repository contains most of the research explanations, benchmark reports and Python reference implementation.

**Claimed method:**  
The README describes the method as:

- Walsh–Hadamard-rotated polar quantisation;

- low-bit Lloyd–Max codebooks;

- asymmetric K and V compression;

- layer-aware Boundary V protection;

- attention-gated sparse V dequantisation;

- custom low-bit weight formats;

- backend-specific CPU, CUDA, ROCm, Vulkan and Metal kernels.

**Actual method:**  
The current production KV-cache path appears to perform:

1.  L2 norm extraction from each 128-value block.

2.  Normalisation of the block.

3.  Walsh–Hadamard rotation with predetermined sign changes.

4.  Mapping to a Lloyd–Max centroid codebook.

5.  Bit-packing of the centroid indices.

6.  Storage of an FP16 norm alongside the packed indices.

7.  Approximate reconstruction when the cache is read.

8.  Independent selection of the key-cache and value-cache formats.

The current source contains genuine packed structures rather than simply storing decompressed FP16 values under a different label.

**Difference from formal TurboQuant:**  
The formal TurboQuant design contains an MSE-focused first compression stage followed by a QJL residual or inner-product correction stage.

This production fork differs because:

- QJL is disabled in the normal production path;

- the production turbo2, turbo3 and current turbo4 formats use direct PolarQuant-style codebook reconstruction;

- it uses structured Walsh–Hadamard rotation and sign changes;

- it adds asymmetric K/V policies;

- it adds Boundary V layer protection;

- it adds sparse V dequantisation;

- it includes custom weight formats not defined by the original paper;

- it contains hardware-specific engineering that is outside the formal algorithm.

The companion repository explicitly says QJL remains for reference and research, but production drops it because the developers found that its increased variance could harm attention after softmax.

Therefore, it should be described as:

**A practical TurboQuant-inspired implementation called TurboQuant+, rather than an exact implementation of the complete formal two-stage TurboQuant algorithm.**

#### Quantisation formats

**Weight quantisation:**  
Supported custom offline weight formats:

- TQ3_1S

- TQ4_1S

They are created using llama-quantize:

llama-quantize.exe model-f16.gguf model-tq3.gguf TQ3_1S

llama-quantize.exe model-f16.gguf model-tq4.gguf TQ4_1S

The README describes these as approximately 3.5-bit and 4.5-bit formats. However, the current packed source structures occupy:

- TQ3_1S: 16 bytes for 32 weights = **4.0 actual bits per weight**;

- TQ4_1S: 20 bytes for 32 weights = **5.0 actual bits per weight**.

The difference is caused by the two FP16 scale values stored in each weight block.

For your report, distinguish between the approximate codebook precision and the **actual complete packed memory cost**.

**KV-cache quantisation:**  
Runtime KV-cache formats added by the fork:

- turbo2

- turbo3

- turbo4

They are selected independently for K and V using:

--cache-type-k  
--cache-type-v

The model GGUF file does not need to be changed when only KV-cache compression is selected.

**Key-cache types:**  
Relevant options include:

- f16

- q8_0

- q4_0

- turbo4

- turbo3

- turbo2

The fork technically permits Turbo formats for the key cache, but it recommends keeping K at f16 or q8_0.

Recommended K settings:

f16  
q8_0

Aggressive TurboQuant K compression is discouraged because K errors can change which tokens attention selects.

**Value-cache types:**  
Relevant options include:

- f16

- q8_0

- q4_0

- turbo4

- turbo3

- turbo2

The recommended compression ladder is:

turbo4 → lightest Turbo compression  
turbo3 → medium compression  
turbo2 → strongest compression

**Bits per value:**

Actual packed size calculated from the current structures:

| **Format** | **Packed block**        | **Actual total bits/value** |
|------------|-------------------------|-----------------------------|
| turbo2     | 34 bytes for 128 values | **2.125 bits/value**        |
| turbo3     | 50 bytes for 128 values | **3.125 bits/value**        |
| turbo4     | 68 bytes for 128 values | **4.25 bits/value**         |

These code-derived figures are more useful for your memory estimator than the rounded figures in the README. The comments around turbo2 and turbo3 still contain some older 32-value calculations, but the active macros and structures use 128-value blocks.

**Block structure:**  
All three current Turbo KV formats use blocks of **128 values**.

One 128-value cache block  
↓  
One normalisation value  
↓  
Packed codebook indices

The transform group is also defined as 128 values.

**Metadata overhead:**

turbo2:

32 bytes packed 2-bit indices  
2 bytes FP16 norm  
= 34 bytes

Metadata overhead:

2 bytes per 128 values  
= 0.125 bits/value

turbo3:

32 bytes lower two index bits  
16 bytes upper index bits  
2 bytes FP16 norm  
= 50 bytes

Metadata overhead:

2 bytes per 128 values  
= 0.125 bits/value

turbo4:

64 bytes packed 4-bit indices  
2 bytes FP16 norm  
2 bytes reserved residual norm  
= 68 bytes

Metadata overhead:

4 bytes per 128 values  
= 0.25 bits/value

The second FP16 field in the current turbo4 block is reserved and unused in the default 4-bit production mode.

**Actual memory packing:**  
Yes.

The compression is physically represented using packed byte arrays:

- turbo2: four 2-bit indices per byte;

- turbo3: lower two bits packed four per byte, with the third bit in a separate bit array;

- turbo4: two 4-bit indices per byte;

- norms stored as FP16 values.

Therefore, the fork can provide genuine KV-cache memory savings, subject to backend buffers and temporary workspace allocations.

#### Hardware support

**CPU support:**  
Yes.

The repository includes CPU implementations for:

- Walsh–Hadamard rotation;

- quantisation;

- dequantisation;

- vector operations;

- TurboQuant cache reads and writes.

This is suitable for functional testing on both your Ryzen Lenovo and an Intel CPU.

**CUDA support:**  
Yes.

This is one of the strongest paths for your current Lenovo. The repository includes:

- CUDA kernels for turbo2, turbo3 and turbo4;

- CUDA weight-format handling;

- warp-cooperative dequantisation;

- Flash Attention integration;

- multi-token and multi-GPU work;

- Windows CUDA prebuilt releases.

The published community benchmarks include an RTX 3090 test where Turbo formats ran within approximately 4–7% of q8_0 speed, although these figures must be reproduced on your RTX 4060 and Granite.

**ROCm support:**  
Yes, through HIP/ROCm.

The README lists support for:

- RDNA3;

- RDNA4;

- CDNA3;

- CDNA4.

There are also community results for an AMD RX 9070 XT, although some configuration failures were reported.

**SYCL support:**  
Not production-ready for the TurboQuant formats.

Normal llama.cpp has a SYCL backend, but this fork has an open issue for adding the required SET_ROWS support for turbo2, turbo3 and turbo4. The maintainer states that they do not possess Intel GPU hardware and that SYCL support is community-maintained.

There is an open pull request containing an attempted SYCL implementation tested to a limited extent on an Intel Arc A380, but it is unmerged and is described as not properly reviewed.

Therefore:

Normal SYCL llama.cpp  
→ available

TurboQuant+ SYCL  
→ incomplete and experimental

**OpenVINO support:**  
Yes for the repository’s **standard llama.cpp GGUF backend**, but not for its custom TurboQuant types.

The included OpenVINO backend supports normal GGUF types such as:

- FP16;

- BF16;

- Q8_0;

- Q4_0;

- Q4_1;

- Q4_K;

- Q4_K_M.

The documented list does not include:

- turbo2;

- turbo3;

- turbo4;

- TQ3_1S;

- TQ4_1S.

Therefore, the OpenVINO backend is useful for your standard GGUF/OpenVINO comparison, but it is not currently evidence of OpenVINO plus TurboQuant support.

**NPU support:**  
Standard OpenVINO GGUF inference: experimental support exists.

TurboQuant+ NPU inference: no documented support.

The OpenVINO backend can target Intel CPUs, GPUs and NPUs, but its primary documented NPU precision is Q4_0, and there is no documented handling of the custom TurboQuant cache or weight formats.

#### Windows setup

**Windows build:**  
Supported.

For your Lenovo, build the CUDA version from the pinned branch and commit.

**Required tools:**

For the Lenovo CUDA build:

- Windows 11;

- Git;

- CMake;

- Visual Studio 2022 Build Tools;

- “Desktop development with C++” workload;

- Windows SDK;

- NVIDIA CUDA Toolkit compatible with your Visual Studio installation;

- current NVIDIA drivers.

For the OpenVINO build on Intel, you would additionally need:

- OpenVINO Runtime;

- Ninja;

- vcpkg;

- OpenCL development files;

- Intel GPU and NPU drivers.

**Build command:**

git clone --branch feature/turboquant-kv-cache <https://github.com/TheTom/llama-cpp-turboquant.git>

cd llama-cpp-turboquant

git checkout 4595fff0bbd15ee01663699b788eea70e7e1cd69

cmake -S . -B build -DGGML_CUDA=ON

cmake --build build --config Release -j

The README provides the normal CUDA option -DGGML_CUDA=ON. The additional checkout step pins your experiments to a reproducible version.

**Run command:**

Initial baseline:

.\build\bin\Release\llama-cli.exe \`  
-m "C:\Models\granite.gguf" \`  
-ngl 99 \`  
-fa on \`  
-c 8192 \`  
--cache-type-k f16 \`  
--cache-type-v f16 \`  
-p "Explain the purpose of a balance sheet."

Safest TurboQuant+ test:

.\build\bin\Release\llama-cli.exe \`  
-m "C:\Models\granite.gguf" \`  
-ngl 99 \`  
-fa on \`  
-c 8192 \`  
--cache-type-k f16 \`  
--cache-type-v turbo4 \`  
-p "Explain the purpose of a balance sheet."

Recommended asymmetric test:

.\build\bin\Release\llama-cli.exe \`  
-m "C:\Models\granite.gguf" \`  
-ngl 99 \`  
-fa on \`  
-c 8192 \`  
--cache-type-k q8_0 \`  
--cache-type-v turbo3 \`  
-p "Explain the purpose of a balance sheet."

The executable may instead appear in build\bin\\ depending on the CMake generator.

#### Granite compatibility

**Granite compatibility:**  
Not yet confirmed by this repository’s published evidence.

The fork says it retains all model families supported by upstream llama.cpp. IBM also provides Granite quantisations intended for llama.cpp-compatible applications. However, no published TurboQuant+ test involving Granite was found in the repository, its issues or the companion benchmark records.

Therefore, the correct status is:

Standard Granite GGUF + llama.cpp:  
Likely supported

Granite GGUF + TurboQuant+:  
Must be experimentally verified

Granite + TurboQuant+ + Intel SYCL:  
Not currently established

**Tested Granite model:**  
None documented by the repository.

Your proposed first model should be:

IBM Granite 4.1 3B GGUF

Then test Granite 4.1 8B if the 3B model reveals a head-dimension or kernel limitation.

**Supported head dimensions:**  
The current TurboQuant packed cache structures and transform groups use **128 values**.

IBM Granite 4.1 uses:

| **Model**       | **Attention head size** |
|-----------------|-------------------------|
| Granite 4.1 3B  | 64                      |
| Granite 4.1 8B  | 128                     |
| Granite 4.1 30B | 128                     |

Therefore:

- Granite 4.1 8B and 30B naturally match the fork’s 128-value grouping;

- Granite 4.1 3B has a 64-value attention head and needs explicit compatibility testing;

- some CPU rotation code appears to account for a 64-element transform, but the packed cache blocks remain defined as 128 values;

- this should not be assumed safe until the model is run and its outputs are validated.

This is an important test for your project.

**Flash Attention:**  
Yes on the main documented backends.

The repository describes TurboQuant Flash Attention support for:

- CUDA;

- HIP/ROCm;

- Metal;

- Vulkan.

Flash Attention is automatically enabled where the selected backend and cache type support it. Intel SYCL TurboQuant Flash Attention is not established.

**Maximum tested context:**  
Published complete large-model stress test: **128K context**.

The companion benchmarks report Command-R+ 104B at 128K using turbo3 and turbo4, including successful needle-in-a-haystack retrieval. There is also a 262K cache-memory measurement, but that should not be treated as the same level of full model-quality validation.

#### Evaluation evidence

**Published benchmarks:**  
Yes, mainly in the companion TheTom/turboquant_plus repository.

They include:

- M5 Max benchmarks;

- RTX 3090 community benchmarks;

- AMD RX 9070 XT benchmarks;

- M1 Max benchmarks;

- context scaling from 2K to 32K;

- 70B and 104B large-model tests;

- 128K context tests;

- asymmetric K/V tests;

- Boundary V tests;

- sparse V tests.

These are useful engineering results, but they should be described as repository and community benchmarks, not as independent proof that Granite will achieve the same results.

**Quality tests:**

Published quality methods include:

- WikiText perplexity;

- percentage change from q8_0;

- KL divergence from FP16;

- probability RMS difference;

- same-top-token agreement;

- needle-in-a-haystack retrieval;

- coherent-output checks;

- real-document tests.

A particularly important result shows that symmetric turbo3/turbo3 caused catastrophic perplexity for one Qwen2.5 7B Q4_K_M test, while q8_0-K/turbo3-V remained usable. This supports testing asymmetric settings first.

**Memory tests:**

Published memory evidence includes:

- theoretical block compression;

- measured KV-cache MiB;

- peak memory on large models;

- maximum context before memory limits;

- a 262K cache comparison.

For one M1 Max measurement at 262K context:

q8_0 cache: 2782 MiB  
turbo4 cache: 1422 MiB

The measured reduction was approximately 1.96× relative to q8_0, rather than relative to FP16.

**Speed tests:**

Published performance measures include:

- prompt-processing tokens per second;

- decode tokens per second;

- short-context generation;

- long-context generation;

- context scaling;

- comparisons against q8_0;

- backend-specific results.

On the published RTX 3090 tests, Turbo configurations were generally within 4–7% of q8_0 for both prefill and decode. This is a useful starting point, but your RTX 4060 and Granite results may differ.

**Known bugs:**

Relevant risks include:

1.  TurboQuant SYCL support is incomplete and remains in an open implementation attempt.

2.  No published Granite testing exists.

3.  OpenVINO supports standard GGUF types but not the custom Turbo formats.

4.  Some symmetric low-bit K/V configurations produce severe quality degradation.

5.  Backend results vary significantly by hardware.

6.  Vulkan SET_ROWS and view-tensor problems have previously been reported and require testing against the latest commit.

7.  Some Metal configurations have backend-specific corruption or performance regressions.

8.  Several comments and headline precision figures are stale compared with the current packed structures.

9.  The project is explicitly marked work in progress.

10. OpenVINO performance, precision coverage and model compatibility are themselves documented as work in progress.

#### Implementation and integration

**Files containing main implementation:**

Core data structures and packing:

ggml/src/ggml-common.h

CPU codec implementation:

ggml/src/ggml-turbo-quant.c

General quantisation interfaces:

ggml/src/ggml-quants.c  
ggml/src/ggml-quants.h

KV-cache integration:

src/llama-kv-cache.cpp

Model and context integration:

src/llama-context.cpp  
src/llama-model.cpp

CUDA implementation:

ggml/src/ggml-cuda/

ROCm/HIP implementation:

ggml/src/ggml-cuda/

HIP shares parts of the CUDA code structure.

Vulkan implementation:

ggml/src/ggml-vulkan/

Intel SYCL area:

ggml/src/ggml-sycl/

OpenVINO backend:

ggml/src/ggml-openvino/  
docs/backend/OPENVINO.md

Command-line and server programs:

examples/  
tools/  
common/

Tests:

tests/

Companion research and benchmark documents:

<https://github.com/TheTom/turboquant_plus>

**Ease of integration:**  
**Medium**, if you use the executables as separate worker processes.

The simplest integration is:

Your WinUI application  
↓  
Start llama-server.exe  
↓  
Pass model and cache options  
↓  
Use the local OpenAI-compatible HTTP API  
↓  
Receive generated response and metrics

This avoids directly linking a rapidly changing C++ library into your C# interface.

Using llama-cli is also easy for early tests, but llama-server is more appropriate for the final application.

Directly embedding libllama into your own C++ backend would offer more control, but it would increase development difficulty and expose you to upstream API changes.

**Maintenance risk:**  
**High.**

Reasons:

- it is a long-lived feature branch;

- it is not upstreamed;

- it is marked WIP;

- it carries hundreds of changes beyond official llama.cpp;

- the fork must continually merge upstream llama.cpp changes;

- APIs and backend internals can change;

- different hardware backends have different levels of coverage;

- Intel TurboQuant support is incomplete;

- some documentation figures do not match current source packing.

The risk can be reduced by:

Pinning one exact commit  
Keeping the fork as a separate backend process  
Recording all build options  
Saving your compiled executable  
Avoiding unnecessary updates during evaluation

### How we could use it

**How we could use it:**

##### Phase 1: Confirm normal Granite inference

Run an official Granite GGUF using:

f16 K + f16 V

Check:

- model loads;

- chat template works;

- output is coherent;

- CUDA offloading works;

- memory and speed can be recorded.

##### Phase 2: Establish standard llama.cpp baselines

Test:

f16 / f16  
q8_0 / q8_0  
q4_0 / q4_0

##### Phase 3: Test TurboQuant+ progressively

Test:

f16 K / turbo4 V  
q8_0 K / turbo4 V  
q8_0 K / turbo3 V  
q8_0 K / turbo2 V

Do not begin with symmetric Turbo K and V.

##### Phase 4: Measure the project metrics

For every configuration, record:

Successful model load  
Output coherence  
Peak system RAM  
Peak VRAM  
Reported KV-cache allocation  
Model-loading time  
Time to first token  
Prompt-processing speed  
Generation speed  
Maximum stable context  
Perplexity or quality score  
Errors and warnings

##### Phase 5: Integrate the winning configuration

Use llama-server as a worker behind your WinUI interface:

WinUI frontend  
↓  
Backend configuration service  
↓  
llama-server from this repository  
↓  
Granite GGUF + selected KV-cache format

##### Phase 6: Repeat the shortlist on Intel

Do not repeat every experimental configuration. Take the best two or three and test:

- Intel CPU;

- Vulkan on Intel GPU if available;

- experimental SYCL branch if it becomes usable;

- normal OpenVINO GGUF as a separate comparison.

##### Phase 7: Compare against OpenVINO IR

The final comparison could be:

GGUF Q4_K_M + standard llama.cpp  
GGUF Q4_K_M + TurboQuant+ KV cache  
OpenVINO IR INT8  
OpenVINO IR INT4

This directly compares the two main optimisation routes in your project.

### Final recommendation

**Final recommendation:**  
Use this repository as the **main candidate for implementing and evaluating GGUF KV-cache compression** in your project.

Specifically:

Use its turbo4, turbo3 and turbo2 KV-cache formats through llama.cpp on your Lenovo’s CPU and RTX 4060.

It is currently one of the most complete practical TurboQuant-style llama.cpp implementations because it includes:

- real memory packing;

- end-to-end model inference;

- CPU and CUDA kernels;

- Flash Attention integration;

- independent K/V cache selection;

- benchmark tools;

- a local server suitable for application integration.

However:

Do not rely on this repository as your complete Intel solution.

The custom TurboQuant formats are not currently established through:

- Intel SYCL;

- OpenVINO;

- Intel NPU.

The safest project decision is:

Use this repository  
→ GGUF + llama.cpp + TurboQuant prototype

Use official OpenVINO tools/backend  
→ Intel/OpenVINO optimisation prototype

Compare the two routes  
→ then investigate whether TurboQuant can be extended to Intel

I would initially focus on **KV-cache compression only**, not its custom weight formats. KV compression is more closely aligned with your project brief, is activated without creating a new model file, and allows a controlled comparison using exactly the same Granite GGUF weights.

Granite

just fill it out briefly this is too long to keep a track of

Thought for 6s

**Repository:** llama-cpp-turboquant  
**URL:** [https://github.com/TheTom/llama-cpp-turboquant](https://github.com/TheTom/llama-cpp-turboquant)<br>
**Owner:** TheTom  
**Branch:** feature/turboquant-kv-cache  
**Commit:** Pin the exact commit used during testing; not selected yet  
**Last relevant update:** Active development in June 2026  
**Licence:** MIT

**Purpose:** Run GGUF models in llama.cpp with TurboQuant+ weight and KV-cache compression.  
**Claimed method:** Walsh–Hadamard rotation, PolarQuant-style codebooks, asymmetric K/V compression and hardware kernels.  
**Actual method:** Normalise each cache block, rotate it, map values to low-bit centroids and store packed indices plus the block norm.  
**Difference from formal TurboQuant:** Production mode does not normally use the formal QJL correction stage and adds several custom engineering improvements.

**Weight quantisation:** TQ3_1S, TQ4_1S  
**KV-cache quantisation:** turbo2, turbo3, turbo4  
**Key-cache types:** Prefer f16 or q8_0; Turbo types technically available  
**Value-cache types:** turbo4, turbo3, turbo2  
**Bits per value:** Approximately 2, 3.5 and 4.5 bits, depending on format  
**Block structure:** Mainly 128-value blocks  
**Metadata overhead:** A small norm and packing metadata per block  
**Actual memory packing:** Yes, compressed indices are physically packed in memory.

**CPU support:** Yes  
**CUDA support:** Yes; suitable for the RTX 4060  
**ROCm support:** Yes  
**SYCL support:** Incomplete and experimental  
**OpenVINO support:** Standard GGUF types only; Turbo types not documented as supported  
**NPU support:** Standard OpenVINO route only; no TurboQuant NPU support confirmed.

**Windows build:** Supported  
**Required tools:** Git, CMake, Visual Studio C++ Build Tools, CUDA Toolkit  
**Build command:**

cmake -B build -DGGML_CUDA=ON  
cmake --build build --config Release -j

**Run command:**

**Granite compatibility:** Likely through normal llama.cpp support, but TurboQuant compatibility must be tested  
**Tested Granite model:** None documented  
**Supported head dimensions:** Mainly designed around 128-value groups; Granite 3B compatibility must be checked  
**Flash Attention:** Yes on CUDA, Metal, ROCm and parts of Vulkan  
**Maximum tested context:** Published tests reach 128K context.

**Published benchmarks:** Yes, mainly in TheTom/turboquant_plus  
**Quality tests:** Perplexity, KL divergence, output coherence and needle retrieval  
**Memory tests:** KV-cache size and peak-memory comparisons  
**Speed tests:** Prefill and generation tokens per second  
**Known bugs:** Incomplete SYCL support, backend-specific failures and no published Granite validation.

**Files containing main implementation:**  
ggml/src/ggml-turbo-quant.c  
ggml/src/ggml-common.h  
src/llama-kv-cache.cpp  
ggml/src/ggml-cuda/

**Ease of integration:** Medium; easiest through llama-server as a separate backend process  
**Maintenance risk:** High because it is a WIP fork and not fully upstreamed

**How we could use it:** Test Granite GGUF with f16/f16, f16/turbo4, q8_0/turbo4, q8_0/turbo3 and q8_0/turbo2 on the Lenovo, then repeat the best configurations on Intel.

**Final recommendation:** Use it as the main GGUF and TurboQuant KV-cache prototype. Do not depend on it for finished Intel SYCL, OpenVINO or NPU TurboQuant support.

could you add a short and concise how we could implement it and for what to finish this

Thought for a few seconds

**How we could implement it:** Clone and pin the repository, build the CUDA version on the Lenovo, run Granite GGUF through llama-server, test standard and TurboQuant cache settings, then connect the server to the WinUI application through its local API. Repeat the best settings on Intel hardware.

**What we would use it for:** To implement the project’s GGUF and TurboQuant KV-cache compression route, reduce memory use, support longer contexts, and compare it against standard llama.cpp and OpenVINO INT8/INT4.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-REPO-THETOM`
- `SRC-LLAMACPP-GITHUB`
- `SRC-PAPER-TURBOQUANT-2025`
