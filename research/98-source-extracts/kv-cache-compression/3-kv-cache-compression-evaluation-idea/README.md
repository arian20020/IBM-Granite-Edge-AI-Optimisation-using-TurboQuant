#  Evaluation Aim

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

# Evaluation Structure

## Fixed Setup

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

# Paired test matrix

Each prompt should be run once against the baseline and once against every supported compression configuration under identical conditions.

Prompt A at 4K context:  
FP16 → INT8 → INT4 → TurboQuant 3.5 → TurboQuant 2.5  

Prompt B at 4K context:  
FP16 → INT8 → INT4 → TurboQuant 3.5 → TurboQuant 2.5

This is better than comparing unrelated averages because every compressed result has a direct full-precision counterpart.

## Experiment Matrix

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

# Evaluation Setup

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

# Practical evaluation plan

## Stage 1: Use llmfit

Use llmfit once to record:

- laptop hardware;

- available RAM;

- recommended Granite model;

- estimated model fit;

- estimated practical context length;

- any baseline speed results it supports.

This supports the hardware suitability part of your application.

## Stage 2: Add measurements to your backend

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

## Stage 3: Run a small paired quality test

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

## Stage 4: Test several context lengths

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

## Stage 5: Add advanced tools only when necessary

Use LM Evaluation Harness or a LongBench subset after the basic system works.

Use Intel VTune only if:

- TurboQuant is unexpectedly slow;

- you need to identify a bottleneck;

- you have enough time for advanced optimisation.

VTune is not required to prove the basic concept.
