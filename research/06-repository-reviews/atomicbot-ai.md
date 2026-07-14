# AtomicBot-ai/atomic-llama-cpp-turboquant

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-REPO-ATOMICBOT](../00-sources/github-repositories.md#src-repo-atomicbot), [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github), [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

## Recorded identity

| Field | Recorded value |
|---|---|
| URL | <https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant> |
| Branch | feature/turboquant-kv-cache |
| Commit | `b0e900a28ee4172bbb97df0d1ea1c78e86bc0ac6` |
| Last relevant update in supplied research | 17 June 2026 |
| Project position | **High-priority Intel Vulkan candidate** |

## What it does

128-value rotation plus packed turbo2/turbo3/turbo4 codebook formats; also experimental weight formats. [SRC-REPO-ATOMICBOT]

## Difference from formal TurboQuant

Default formats focus on the main approximation; complete formal QJL is not the default path.

## Storage

Real packed cache; about 2.1, 3.1 and 4.25 effective bits recorded.

## Backend status recorded in the research

| Backend | Recorded status |
|---|---|
| CPU | Reference/correctness route |
| CUDA | Supported |
| ROCm/HIP | Partial |
| Vulkan | Important candidate, including Intel GPU possibility |
| Intel SYCL | No completed custom route |
| OpenVINO | No |
| Intel NPU | No |

## Granite status

Must be tested

## Project recommendation

Test early on Intel Vulkan. Do not promote to default until Granite quality, device support, speed and memory are reproduced.

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

- [SRC-REPO-ATOMICBOT](../00-sources/github-repositories.md#src-repo-atomicbot) — AtomicBot-ai/atomic-llama-cpp-turboquant.
- [SRC-LLAMACPP-GITHUB](../00-sources/github-repositories.md#src-llamacpp-github) — ggml-org/llama.cpp.
- [SRC-PAPER-TURBOQUANT-2025](../00-sources/primary-research-papers.md#src-paper-turboquant-2025) — TurboQuant: Online Vector Quantization with Near-optimal Distortion Rate.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: AtomicBot AI final analysis

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/AtomicBot AI/AtomicBot AI final analysis.docx`

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

## Original research: AtomicBot Repository Implementation and Testing Plan

> **Original document:** `Quantisation implementations/llama.cpp quantisation/GitHub repos/AtomicBot AI/AtomicBot Repository Implementation and Testing Plan.docx`

#### 1. Proposed implementation approach

The AtomicBot repository should initially be used through its prebuilt Windows llama-server package. This is the simplest way to connect the backend to the application while keeping the inference process separate from the user interface.

The preferred route is to run an IBM Granite GGUF model on the Intel GPU through Vulkan, using turbo3 KV-cache quantisation. The repository presents turbo3 as the recommended balance between memory reduction and model accuracy.

Granite GGUF model  
↓  
AtomicBot llama-server  
↓  
Check available hardware and backend support  
↓  
Select the most suitable execution route

#### 2. Backend selection and fallback plan

Full Vulkan and TurboQuant support  
↓  
Use Intel GPU with turbo3  
and Flash Attention enabled

Vulkan works, but some accelerated  
features are unavailable or unstable  
↓  
Use a safer Vulkan configuration:  
- turbo3 without Flash Attention  
- turbo4  
- standard q8_0 KV cache

Vulkan is unavailable or fails  
↓  
Use the Intel CPU reference route  
or investigate another repository  
with SYCL or OpenVINO support

No stable CPU or GPU route works  
↓  
Display a clear unsupported-device message  
and recommend a smaller model or driver update

“Partial Vulkan support” does not mean that Vulkan has an official partial mode. It means that Vulkan is available, but the Intel GPU or its driver may not support every feature required by the repository’s fastest TurboQuant kernels.

#### 3. Intended final route

Preferred route  
Granite + Intel Vulkan GPU + turbo3

First fallback  
Granite + Vulkan + turbo3  
without Flash Attention

Second fallback  
Granite + Vulkan + turbo4  
or q8_0 KV cache

Slow fallback  
Granite + Intel CPU reference path

Alternative future route  
Separate SYCL or OpenVINO backend

The CPU route is useful for compatibility and correctness testing, but it may be too slow for practical use on a low-end PC.

### Testing Plan

#### 4. Stage 1: confirm Granite compatibility

The first test should use a small Granite GGUF model with normal CPU inference and no TurboQuant.

The purpose is to confirm that:

- the model file is valid;

- Granite is supported by the fork;

- the model loads correctly;

- normal text generation works.

Granite works on CPU  
↓  
Continue to Vulkan testing

Granite fails on CPU  
↓  
Investigate the model format,  
model architecture or GGUF conversion  
before testing TurboQuant

#### 5. Stage 2: confirm Intel Vulkan operation

The next test should use the Windows Vulkan build with a normal KV-cache format.

The purpose is to confirm that:

- the Intel GPU is detected;

- the Vulkan driver works;

- model layers can be offloaded to the GPU;

- a basic prompt can be completed successfully.

Intel GPU detected and inference succeeds  
↓  
Continue to TurboQuant testing

Vulkan starts but inference fails  
↓  
Check Intel graphics drivers,  
GPU memory and backend compatibility

Vulkan does not detect a usable GPU  
↓  
Use the CPU route or another Intel backend

#### 6. Stage 3: test KV-cache quantisation

The Granite model and its weight format must remain unchanged while the KV-cache format is changed.

The following formats should be compared:

F16  
q8_0  
turbo4  
turbo3  
turbo2

Their purposes are:

| **KV-cache format** | **Purpose**                                  |
|---------------------|----------------------------------------------|
| F16                 | Original high-precision baseline             |
| q8_0                | Standard llama.cpp compressed-cache baseline |
| turbo4              | Higher-accuracy TurboQuant option            |
| turbo3              | Main recommended TurboQuant option           |
| turbo2              | Maximum-compression experiment               |

The main candidate is:

Granite + Intel Vulkan GPU + turbo3

#### 7. Stage 4: test Flash Attention

The turbo3 configuration should be tested with Flash Attention both enabled and disabled.

turbo3 + Flash Attention works  
↓  
Use the accelerated Vulkan route

turbo3 works only without Flash Attention  
↓  
Use turbo3 with Flash Attention disabled

turbo3 fails in both configurations  
↓  
Try turbo4 or q8_0

This test is important because the Intel GPU may support basic Vulkan inference but not every feature used by the accelerated attention kernel.

#### 8. Stage 5: increase context length

Testing should start with a short context and increase only when the previous configuration is stable.

2K context  
↓  
4K context  
↓  
8K context  
↓  
16K context  
↓  
32K context

The purpose is to determine whether TurboQuant allows longer contexts while remaining stable and producing acceptable output.

#### 9. What must be measured

For every configuration, record:

- whether the model loads successfully;

- whether the prompt completes;

- RAM usage;

- shared GPU-memory usage;

- KV-cache memory usage;

- time to first token;

- prompt-processing speed;

- generation speed;

- maximum stable context length;

- answer quality;

- crashes, hangs or driver errors.

A results table can be used:

| **Backend** | **KV format** | **Flash Attention** | **Context** | **RAM** | **GPU memory** | **TTFT** | **Generation speed** | **Quality** | **Stable** |
|-------------|---------------|---------------------|-------------|---------|----------------|----------|----------------------|-------------|------------|
| CPU         | F16           | Off                 | 2K          |         |                |          |                      |             |            |
| CPU         | turbo3        | Off                 | 2K          |         |                |          |                      |             |            |
| Vulkan      | F16           | Off                 | 2K          |         |                |          |                      |             |            |
| Vulkan      | q8_0          | Off                 | 2K          |         |                |          |                      |             |            |
| Vulkan      | turbo4        | Off                 | 2K          |         |                |          |                      |             |            |
| Vulkan      | turbo3        | Off                 | 2K          |         |                |          |                      |             |            |
| Vulkan      | turbo3        | On                  | 2K          |         |                |          |                      |             |            |
| Vulkan      | turbo2        | Off                 | 2K          |         |                |          |                      |             |            |

#### 10. Quality testing

The same prompts and generation settings should be used for every configuration.

Useful tests include:

- factual questions;

- document summarisation;

- long-context information retrieval;

- multi-turn conversation;

- structured JSON output;

- coding tasks for Granite Code models.

The purpose is to check that memory savings do not cause unacceptable reductions in accuracy or coherence.

#### 11. Weight quantisation testing

Weight quantisation should be tested only after the best KV-cache configuration has been selected.

The repository provides:

- TQ3_1S;

- TQ4_1S.

Best KV-cache configuration selected  
↓  
Test normal GGUF weights  
↓  
Test TQ4_1S  
↓  
Test TQ3_1S  
↓  
Compare model size, RAM, speed and quality

Testing KV-cache and weight quantisation separately ensures that any performance or quality change can be traced to the correct compression method.

### Main Issues to Watch For

#### 12. Vulkan compatibility

A device may support Vulkan generally but still lack the features required for the fastest TurboQuant path.

Basic Vulkan works  
but accelerated turbo3 fails  
↓  
Use a safer Vulkan configuration

#### 13. Shared memory limitations

Intel integrated GPUs normally share system RAM with the CPU and Windows.

Therefore:

- an 8 GB laptop may still be heavily restricted;

- model weights and the KV cache compete for the same memory;

- available memory must be checked before loading the model.

#### 14. CPU performance

The CPU implementation is described as a reference path. It may generate correct output but still be too slow for a practical application.

CPU route works and speed is acceptable  
↓  
Keep as a supported fallback

CPU route works but is too slow  
↓  
Use only for diagnostics  
or recommend a smaller model

#### 15. Granite validation

Granite is supported generally by llama.cpp, but Granite with AtomicBot TurboQuant on an Intel Vulkan GPU has not yet been proven.

The investigation must therefore confirm:

Granite loading  
+  
TurboQuant cache  
+  
Intel Vulkan GPU  
+  
acceptable speed and quality

### Final Implementation Decision

Full support  
↓  
Use Intel Vulkan GPU  
with turbo3 and Flash Attention

Supported with limitations  
↓  
Use turbo3 without Flash Attention,  
turbo4 or q8_0

Vulkan unavailable  
↓  
Use CPU reference route  
or a separate SYCL/OpenVINO backend

Insufficient memory or no stable backend  
↓  
Reject the configuration safely  
and explain the exact reason

The AtomicBot repository should therefore remain a high-priority implementation candidate, but it must be labelled as:

Promising for Granite on Intel Vulkan hardware, but requiring device-specific compatibility, performance and quality testing before final integration.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-REPO-ATOMICBOT`
- `SRC-LLAMACPP-GITHUB`
- `SRC-PAPER-TURBOQUANT-2025`
