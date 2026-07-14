---
title: "AtomicBot Repository Implementation and Testing Plan"
status: "full-source-extract"
version: "1.0"
last_updated: "2026-07-14"
source_documents:
  - "Quantisation implementations/llama.cpp quantisation/GitHub repos/AtomicBot AI/AtomicBot Repository Implementation and Testing Plan.docx"
verification_note: "Direct Markdown extraction of the supplied DOCX. Formatting may differ, so the original DOCX is also preserved."
---

# AtomicBot Repository: Implementation and Testing Plan

## 1. Proposed implementation approach

The AtomicBot repository should initially be used through its prebuilt Windows llama-server package. This is the simplest way to connect the backend to the application while keeping the inference process separate from the user interface.

The preferred route is to run an IBM Granite GGUF model on the Intel GPU through Vulkan, using turbo3 KV-cache quantisation. The repository presents turbo3 as the recommended balance between memory reduction and model accuracy.

Granite GGUF model  
↓  
AtomicBot llama-server  
↓  
Check available hardware and backend support  
↓  
Select the most suitable execution route

## 2. Backend selection and fallback plan

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

## 3. Intended final route

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

# Testing Plan

## 4. Stage 1: confirm Granite compatibility

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

## 5. Stage 2: confirm Intel Vulkan operation

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

## 6. Stage 3: test KV-cache quantisation

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

## 7. Stage 4: test Flash Attention

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

## 8. Stage 5: increase context length

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

## 9. What must be measured

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

## 10. Quality testing

The same prompts and generation settings should be used for every configuration.

Useful tests include:

- factual questions;

- document summarisation;

- long-context information retrieval;

- multi-turn conversation;

- structured JSON output;

- coding tasks for Granite Code models.

The purpose is to check that memory savings do not cause unacceptable reductions in accuracy or coherence.

## 11. Weight quantisation testing

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

# Main Issues to Watch For

## 12. Vulkan compatibility

A device may support Vulkan generally but still lack the features required for the fastest TurboQuant path.

Basic Vulkan works  
but accelerated turbo3 fails  
↓  
Use a safer Vulkan configuration

## 13. Shared memory limitations

Intel integrated GPUs normally share system RAM with the CPU and Windows.

Therefore:

- an 8 GB laptop may still be heavily restricted;

- model weights and the KV cache compete for the same memory;

- available memory must be checked before loading the model.

## 14. CPU performance

The CPU implementation is described as a reference path. It may generate correct output but still be too slow for a practical application.

CPU route works and speed is acceptable  
↓  
Keep as a supported fallback

CPU route works but is too slow  
↓  
Use only for diagnostics  
or recommend a smaller model

## 15. Granite validation

Granite is supported generally by llama.cpp, but Granite with AtomicBot TurboQuant on an Intel Vulkan GPU has not yet been proven.

The investigation must therefore confirm:

Granite loading  
+  
TurboQuant cache  
+  
Intel Vulkan GPU  
+  
acceptable speed and quality

# Final Implementation Decision

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
