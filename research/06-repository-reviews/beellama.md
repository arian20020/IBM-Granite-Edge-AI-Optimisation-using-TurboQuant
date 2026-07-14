# BeeLlama.cpp

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-BEELLAMA](../00-sources/github-repositories.md#src-repo-beellama), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/Anbeeld/beellama.cpp> |
| Branch | Not recorded |
| Commit | `Not recorded` |
| Last relevant update in supplied research | Not recorded |
| Project position | **Research reference / CUDA comparison** |

## What it does

Combines DFlash speculative decoding, TurboQuant/TCQ cache compression and adaptive server controls. [SRC-REPO-BEELLAMA]

## Difference from formal TurboQuant

A broader combined fork, not one exact formal TurboQuant implementation.

## Storage

Includes classic TurboQuant and TCQ formats; extreme low-bit modes have larger quality risk.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Inherited llama.cpp route |
| CUDA | Strongest feature routes |
| ROCm/HIP | Inherited/varies |
| Vulkan | Inherited, but custom feature support not proven |
| Intel SYCL | Inherited, but custom feature support not proven |
| OpenVINO | Inherited references do not prove feature compatibility |
| Intel NPU | No proven custom route |

## Granite status

No Granite-specific evidence recorded

## Project recommendation

Use to study speculative decoding and TCQ, or as an NVIDIA comparison. Not a ready Intel TurboQuant backend.

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

- [SRC-REPO-BEELLAMA](../00-sources/github-repositories.md#src-repo-beellama) — Anbeeld/beellama.cpp.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: BeeLlama repo summary

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/BeeLlama repo/BeeLlama repo summary.docx`

### BeeLlama.cpp — simple study summary

#### What this repository is

BeeLlama.cpp is a heavily modified llama.cpp fork designed to make local GGUF inference:

- faster during token generation;

- capable of handling longer contexts;

- more memory-efficient;

- more reliable when models get stuck in reasoning loops.

It combines features from several projects rather than introducing one single new compression method. Its main components are **DFlash speculative decoding**, **TurboQuant/TCQ KV-cache compression**, adaptive performance controls and multimodal support.

The easiest way to remember it is:

BeeLlama  
=  
modern llama.cpp  
+  
DFlash speed acceleration  
+  
TurboQuant/TCQ memory compression  
+  
extra server safety and tuning features

#### 1. Its main feature: DFlash

DFlash is a form of **speculative decoding**.

In normal inference, the large target model generates one token at a time:

Large model predicts token 1  
Large model predicts token 2  
Large model predicts token 3

With speculative decoding, a smaller draft model predicts several possible future tokens first. The large model then checks them together:

Small draft model guesses several tokens  
↓  
Large target model verifies the guesses  
↓  
Accepted guesses are kept  
Incorrect guesses are rejected

DFlash improves this by giving the draft model access to recent **hidden states** from the large model. Hidden states are the target model’s internal representation of what it currently understands about the conversation.

Target model produces hidden states  
↓  
DFlash draft model reads them  
↓  
Draft model makes better-informed guesses  
↓  
Target model verifies several guesses together

This can reduce how often the expensive target model must run. Bee stores recent hidden states in a CPU ring buffer and normally exposes a smaller recent window to the drafter.

##### Where DFlash works best

DFlash performs best when the next tokens are relatively predictable, such as:

- programming code;

- JSON;

- tests;

- templates;

- repetitive structured output.

It offers smaller gains for unpredictable, open-ended prose.

The published RTX 3090 tests report approximately:

- 4.3–4.9× generation speedups on highly structured coding tasks;

- around 1.7–1.9× on longer multi-turn coding;

- little or no improvement to initial prompt processing.

Therefore, DFlash mainly accelerates **output generation**, not the reading of the original prompt.

#### 2. Adaptive draft control

Using a larger number of speculative tokens is not always faster. If the draft model makes poor guesses, the target rejects them and the extra work is wasted.

BeeLlama monitors whether speculation is actually profitable and changes the number of proposed tokens while the server is running.

Draft predictions are accurate  
→ increase or maintain draft length

Draft predictions are frequently rejected  
→ reduce draft length

Speculation becomes slower than normal inference  
→ temporarily disable it and test again later

This is important because the best draft length changes depending on:

- the model;

- the prompt;

- the stage of the conversation;

- the hardware;

- how predictable the output is.

Bee provides profit and fringe controllers for this automatic adjustment.

#### 3. DDTree

Ordinary speculative decoding normally proposes one sequence:

A → B → C → D

DDTree can propose alternative branches:

C  
/  
A → B  
\\  
X → Y

The target model can verify multiple possible continuations together. This may improve the chance that one proposed route is accepted, but it also increases memory and computation.

Bee controls this with:

- a main draft length;

- a separate branch-node budget;

- the number of alternatives considered at each position.

It is experimental and is disabled automatically in some configurations, including multimodal inference.

### 4. TurboQuant KV-cache compression

DFlash tries to improve **speed**. TurboQuant mainly reduces **KV-cache memory**.

The KV cache stores information about previous tokens so the model does not have to process the entire conversation again for every new token.

As context grows:

More tokens  
→ larger KV cache  
→ more RAM or VRAM required

Bee includes three classic TurboQuant formats:

| **Format** | **Effective storage** | **Approximate compression vs FP16** |
|------------|-----------------------|-------------------------------------|
| turbo4     | 4.125 bits/value      | 3.88×                               |
| turbo3     | 3.125 bits/value      | 5.12×                               |
| turbo2     | 2.125 bits/value      | 7.53×                               |

##### How its TurboQuant works

In simple terms:

Original KV vector  
↓  
Walsh–Hadamard rotation  
↓  
Values become easier to quantise evenly  
↓  
Each value is replaced with a low-bit code  
↓  
Codes and one vector norm are physically packed

The vector norm preserves its overall size, while the low-bit indices approximate its individual coordinates.

Bee uses 128-value blocks:

- turbo2: one norm plus packed 2-bit indices;

- turbo3: one norm plus packed 3-bit indices;

- turbo4: one norm plus packed 4-bit indices.

These are practical scalar TurboQuant formats inherited mainly from TheTom’s implementation. They do **not** use the complete formal TurboQuant QJL residual-correction stage.

### 5. TCQ compression

Bee also includes:

- turbo3_tcq;

- turbo2_tcq.

TCQ means **Trellis-Coded Quantization**.

Classic TurboQuant approximately chooses the closest code for each value individually. TCQ instead considers a sequence of choices through a trellis and searches for a better overall path.

Classic TurboQuant  
→ choose the best code for each value separately

TCQ  
→ find a good sequence of codes across the whole block

This can preserve more useful information at very low bit widths.

The formats use:

| **Format** | **Effective storage** | **Approximate compression vs FP16** |
|------------|-----------------------|-------------------------------------|
| turbo3_tcq | 3.25 bits/value       | 4.92×                               |
| turbo2_tcq | 2.25 bits/value       | 7.11×                               |

The repository’s own quality results suggest:

- turbo3_tcq is usually preferable to ordinary turbo3;

- turbo2_tcq is an extreme last-resort format;

- aggressive 2-bit compression may be unsuitable for code and tool use.

Its benchmark table estimates around 81.6% tail precision for symmetric turbo3_tcq, but only 54.4% for symmetric turbo2_tcq.

#### 6. Keys and values can use different formats

The key and value caches do not have to use the same precision.

For example:

Keys: q8_0  
Values: turbo3_tcq

This can be useful because keys are often more sensitive to compression than values.

Bee recommends several safer conventional combinations before the most aggressive TurboQuant options, including:

q8_0 keys + q5_1 values  
q5_0 keys + q4_1 values  
q4_0 keys + turbo3_tcq values

This is an important lesson for your project: **the best solution may not be symmetric TurboQuant for both K and V**.

### 7. Other useful features

##### Reasoning-loop protection

Some reasoning models become trapped repeatedly producing similar hidden reasoning.

Bee can detect repeated patterns and either:

- force the reasoning section to close;

- stop generation.

This is primarily a reliability feature rather than an optimisation method.

##### Model-free speculation

Bee also supports methods that predict future tokens without loading a separate draft model, including:

- CopySpec;

- n-gram matching;

- suffix-tree prediction;

- recycle speculation.

These search for repeated token patterns in the existing context. They use less memory than DFlash but only help when similar text patterns have already appeared.

##### Multimodal support

Bee allows flat DFlash during multimodal use, but disables tree-shaped DFlash and other incompatible speculative modes.

### 8. Where the repository came from

BeeLlama is essentially a combined and expanded fork:

llama.cpp  
→ main GGUF runtime and server

TheTom  
→ classic TurboQuant formats

spiritbuun/buun  
→ TCQ and initial DFlash work

BeeLlama  
→ combines them and adds adaptive controls,  
multimodal rules and server improvements

Therefore, Bee is not simply another independent TurboQuant implementation. It is a broader **high-performance local inference platform**.

### 9. Relevance to your Intel AI PC project

This repository is technically interesting but is **not currently a ready Intel TurboQuant solution**.

Bee inherits general llama.cpp backends including:

- Vulkan;

- SYCL;

- OpenVINO;

- CPU;

- CUDA;

- Metal;

- HIP.

However, inherited backend availability does not mean every Bee-specific feature works on every backend.

The important limitations are:

Normal llama.cpp inference through Intel backends  
→ Potentially supported through inherited code

TCQ on Intel GPU  
→ Not supported; documented as CUDA-only

DFlash’s fastest implementation  
→ Strongly CUDA-focused

Classic TurboQuant on Intel SYCL/Vulkan/OpenVINO  
→ Not clearly documented or validated

Published Bee benchmarks  
→ RTX 3090, not Intel hardware

Granite  
→ No Granite-specific example in the main documentation inspected

The repository explicitly labels turbo2_tcq and turbo3_tcq as CUDA-only. Its optimised DFlash replay also uses direct CUDA kernels, and its Windows prebuilt downloads focus on CUDA versions.

So for your project:

Useful for understanding advanced local inference  
→ Yes

Useful for studying TCQ and DFlash  
→ Yes

Useful as an RTX 4060 research comparison  
→ Yes

Ready-made Intel Vulkan/SYCL TurboQuant backend  
→ No clear evidence

Ready-made OpenVINO or Intel NPU TurboQuant route  
→ No

### 10. The main idea to internalise

BeeLlama attacks two different bottlenecks:

DFlash  
→ reduces how often the full model must generate tokens  
→ mainly improves generation speed

TurboQuant / TCQ  
→ reduces the size of stored past-token information  
→ mainly improves memory use and maximum context

Adaptive controls  
→ decide whether the acceleration is currently worthwhile

Reasoning guard  
→ prevents wasted generation from repeated reasoning loops

The simplest summary is:

BeeLlama is a CUDA-focused, feature-rich llama.cpp fork that combines speculative decoding and aggressive KV-cache compression. DFlash aims to make predictable generation faster, while TurboQuant and TCQ aim to fit longer contexts into limited memory. It is valuable for studying advanced inference optimisation, but its strongest custom routes are not yet proven for Intel GPUs, OpenVINO or NPUs.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-REPO-BEELLAMA`
- `SRC-LLAMACPP-GITHUB`
- `SRC-PAPER-TURBOQUANT-2025`
