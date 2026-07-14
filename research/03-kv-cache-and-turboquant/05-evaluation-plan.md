# KV-cache compression evaluation plan

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This document turns the compression research into a controlled experiment. The same model, prompts, context lengths and hardware conditions must be used for the baseline and every compressed configuration. Otherwise, a faster or smaller result cannot be attributed confidently to the quantisation method.

## Step-by-step evaluation sequence

1. Freeze the model revision, backend revision and hardware details.
2. Run an uncompressed or higher-precision baseline.
3. Record weight format and KV-cache format separately.
4. Test one change at a time.
5. Measure peak memory, KV-cache memory, load time, time to first token and tokens per second.
6. Use the same quality prompts and scoring rules for every run.
7. Repeat runs to detect unstable results.
8. Store commands, logs, checksums and failures as evidence.
9. Only call an approach successful when it passes both performance and quality gates.

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025), [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020), [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026), [SRC-OLLAMA-USAGE](../00-sources/official-documentation.md#src-ollama-usage). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

## Baseline first

Run a full-precision or standard cache configuration before testing a custom format. At minimum compare: [SRC-BOOK-AI-ENGINEERING-2025]

1. F16 key and value cache.
2. Q8_0 key and value cache.
3. Repository-specific low-bit format.
4. Safer asymmetric combinations, such as higher-precision keys and lower-precision values.

## Quality measurements

- fixed task prompts;
- instruction following;
- factual and answer relevance checks;
- structured JSON validity;
- coding correctness where applicable;
- perplexity when the runtime supports it;
- long-context retrieval at several positions;
- repeated-run consistency;
- manual review of obvious corruption or looping.

## Memory measurements

- theoretical cache bytes;
- actual allocated KV-cache bytes;
- peak process RAM;
- peak VRAM/shared GPU memory;
- metadata overhead;
- maximum stable context.

## Performance measurements

- model-load time;
- time to first token;
- prompt tokens per second;
- decode tokens per second;
- total response time;
- quantisation/dequantisation overhead;
- device utilisation and power where available.

## Test dimensions

Test more than one context size, for example short, medium and long. A low-bit format may have little benefit at short context but become important at long context. Run warm-up trials before recorded trials and repeat each measured case.

## Acceptance logic

A candidate should only progress when it:

- builds reproducibly;
- runs Granite without a crash;
- produces usable output;
- gives real packed-memory savings;
- stays within an agreed quality-loss limit;
- has acceptable speed on the target hardware;
- has saved evidence for the result.

## Further reading

- *AI Engineering*, Chapters 3-4 and 9.
- *Systems Engineering: Principles and Practice*, Chapter 17.

## Sources used

- [SRC-BOOK-AI-ENGINEERING-2025](../00-sources/books-and-project-guidance.md#src-book-ai-engineering-2025) — AI Engineering: Building Applications with Foundation Models.
- [SRC-BOOK-SYSTEMS-ENGINEERING-2020](../00-sources/books-and-project-guidance.md#src-book-systems-engineering-2020) — Systems Engineering: Principles and Practice, Third Edition.
- [SRC-OV-BENCHMARK-2026](../00-sources/official-documentation.md#src-ov-benchmark-2026) — OpenVINO Benchmark Tool.
- [SRC-OLLAMA-USAGE](../00-sources/official-documentation.md#src-ollama-usage) — Ollama generate endpoint and performance fields.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: 3. KV Cache compression Evaluation Idea

> **Original document:** `KV-Cache Compression/3. KV Cache compression Evaluation Idea.docx`

The purpose of the evaluation is to determine whether TurboQuant can reduce the memory required by the KV cache while preserving acceptable model quality and maintaining efficient local inference when applied to IBM Granite models on Intel Windows hardware.

The evaluation will answer three research questions:

1.  **Memory efficiency:** How much does TurboQuant reduce KV-cache memory and total application RAM compared with the full-precision baseline?

2.  **Model quality:** How much of the original model’s factual accuracy, reasoning ability, long-context retrieval and general output quality is retained after KV-cache compression?

3.  **Inference performance:** What effect does TurboQuant have on prompt-processing time, time to first token, decoding speed and total response latency?

These three areas should be considered together:

Memory reduction  
+  
Quality retention  
+  
Inference performance  
↓  
Overall effectiveness of TurboQuant

A TurboQuant configuration should only be considered successful if it produces genuine memory savings without causing unacceptable quality loss or excessive inference slowdown. This reflects KIVI’s own evaluation logic, which considered memory usage, model accuracy and throughput rather than judging compression only by its nominal bit width.

### Evaluation Structure

#### Fixed Setup

Sowe want to first design which metrics must remain the same throughout,to ensure consistency in our evaluation:

| **Category**      | **Information recorded**                         |
|-------------------|--------------------------------------------------|
| Model             | Exact Granite model, version and parameter count |
| Weights           | Weight format and weight quantisation            |
| Runtime           | llama.cpp or OpenVINO version and build          |
| Hardware          | CPU, GPU/NPU, RAM and memory type                |
| Execution         | Device allocation, thread count and GPU offload  |
| Prompting         | Chat template and tokenizer                      |
| Generation        | Temperature, top-p, top-k, seed and output limit |
| System            | Windows version and power mode                   |
| Software revision | TurboQuant implementation commit or version      |

### Paired test matrix

Each prompt should be run once against the baseline and once against every supported compression configuration under identical conditions.

Prompt A at 4K context:  
FP16 → INT8 → INT4 → TurboQuant 3.5 → TurboQuant 2.5

Prompt B at 4K context:  
FP16 → INT8 → INT4 → TurboQuant 3.5 → TurboQuant 2.5

This is better than comparing unrelated averages because every compressed result has a direct full-precision counterpart.

#### Experiment Matrix

| **Test ID** | **KV-cache method** | **Context** | **Output limit** | **Workload** | **Repetition** |
|-------------|---------------------|-------------|------------------|--------------|----------------|
| B-1         | FP16/BF16           | 1K          | 256              | Short QA     | 1–10           |
| I8-1        | INT8                | 1K          | 256              | Short QA     | 1–10           |
| I4-1        | INT4                | 1K          | 256              | Short QA     | 1–10           |
| TQ35-1      | TurboQuant 3.5-bit  | 1K          | 256              | Short QA     | 1–10           |
| TQ25-1      | TurboQuant 2.5-bit  | 1K          | 256              | Short QA     | 1–10           |

Repeat the same pattern for:

- reasoning;

- summarisation;

- code generation;

- long-context retrieval;

- 2K, 4K and 8K contexts.

Only include INT8, INT4 or particular TurboQuant bit rates if the final runtime genuinely supports those KV-cache formats.

### Evaluation Setup

| **Purpose**                             | **Tool or method**                      | **Required?**                                |
|-----------------------------------------|-----------------------------------------|----------------------------------------------|
| Check whether Granite suits the laptop  | **llmfit**                              | Yes, because you were advised to use it      |
| Measure exact KV-cache memory           | Counters inside your backend            | Yes                                          |
| Measure quantisation and inference time | Internal C++ timers                     | Yes                                          |
| Measure total application RAM           | A simple Windows/Python process monitor | Yes                                          |
| Test output quality                     | Fixed paired prompt set                 | Yes                                          |
| Test long-context retrieval             | Small custom Needle-in-a-Haystack test  | Yes                                          |
| Save and analyse results                | CSV plus Python                         | Yes                                          |
| Standard academic benchmarks            | LM Evaluation Harness                   | Optional later                               |
| Larger long-context benchmark           | LongBench subset                        | Optional later                               |
| Detailed Intel profiling                | Intel VTune                             | Optional final stage                         |
| Separate runtime benchmark              | llama-bench or OpenVINO metrics         | Use only the one matching your final runtime |

You do not need to run llama-bench and OpenVINO performance tools together unless your application supports and compares both runtimes.

### Practical evaluation plan

#### Stage 1: Use llmfit

Use llmfit once to record:

- laptop hardware;

- available RAM;

- recommended Granite model;

- estimated model fit;

- estimated practical context length;

- any baseline speed results it supports.

This supports the hardware suitability part of your application.

#### Stage 2: Add measurements to your backend

Your implementation should directly record:

- compressed key-cache bytes;

- compressed value-cache bytes;

- TurboQuant metadata;

- full-precision residual memory;

- quantisation time;

- compressed-cache processing time;

- prefill time;

- decoding tokens per second.

This is the most important stage because external tools cannot automatically understand the internal memory used by your TurboQuant implementation.

#### Stage 3: Run a small paired quality test

Create a manageable set, such as:

- 10 factual questions;

- 10 reasoning questions;

- 5 summaries;

- 5 coding tasks;

- 10 long-context retrieval tests.

Run every prompt using:

FP16/BF16 baseline  
INT8, if supported  
INT4, if supported  
TurboQuant 3.5-bit  
TurboQuant 2.5-bit

We don’t need hundreds of prompts for the first prototype.

#### Stage 4: Test several context lengths

Start with:

1K  
2K  
4K  
8K

At each length, measure:

- KV-cache memory;

- total RAM;

- time to first token;

- tokens per second;

- quality score.

#### Stage 5: Add advanced tools only when necessary

Use LM Evaluation Harness or a LongBench subset after the basic system works.

Use Intel VTune only if:

- TurboQuant is unexpectedly slow;

- you need to identify a bottleneck;

- you have enough time for advanced optimisation.

VTune is not required to prove the basic concept.

## What this means for the project

The final report should be able to trace every result back to a model hash, backend commit, command, hardware configuration and log file. This prevents performance claims from becoming unsupported screenshots or isolated observations.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-BOOK-AI-ENGINEERING-2025`
- `SRC-BOOK-SYSTEMS-ENGINEERING-2020`
- `SRC-OV-BENCHMARK-2026`
- `SRC-OLLAMA-USAGE`
