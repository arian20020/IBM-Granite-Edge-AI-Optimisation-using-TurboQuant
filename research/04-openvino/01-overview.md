# OpenVINO overview

> **Document status:** Detailed curated research
> **Version:** 3.0
> **Last updated:** 14 July 2026
> **Approach:** The original explanations are preserved in the same learning order, with only exact repetition and formatting noise removed.

## What this document explains

This document introduces OpenVINO in the simplest order: what it is, what problem it solves, what happens to a model before inference, and where OpenVINO GenAI fits. OpenVINO is not the same as TurboQuant. OpenVINO is the runtime and optimisation route; TurboQuant is a proposed compression method for vectors such as those in the KV cache.

## Simplified OpenVINO flow

```text
Model files
-> convert or load into an OpenVINO-supported representation
-> OpenVINO Core reads the model
-> compile the model for CPU, GPU, NPU or a device strategy
-> create an inference or generation pipeline
-> send input
-> execute the graph
-> receive output
```

## Quick overview

> **Evidence basis:** The main factual claims in this note are traced to [SRC-OV-GENAI-2026](../00-sources/official-documentation.md#src-ov-genai-2026), [SRC-OV-GENAI-GITHUB](../00-sources/github-repositories.md#src-ov-genai-github). Recommendations, rankings and proposed test steps are project decisions, not claims made by those sources.

OpenVINO is Intel's toolkit and runtime for preparing and running AI models on supported hardware. [SRC-OV-GENAI-2026]

A simple view is:

```text
Model
-> convert or load into an OpenVINO-supported representation
-> compile for a chosen device
-> run inference through OpenVINO Runtime
```

For generative AI, OpenVINO GenAI sits above the runtime and manages tasks such as tokenisation, generation settings and repeated model calls.

OpenVINO and TurboQuant solve different parts of the problem:

- OpenVINO: graph compilation and hardware execution.
- TurboQuant-style work: low-bit storage and use of KV-cache data.

They can be compared in the project, but a custom integration is required before they become one combined backend.

## Sources used

- [SRC-OV-GENAI-2026](../00-sources/official-documentation.md#src-ov-genai-2026) — Generative AI workflow.
- [SRC-OV-GENAI-GITHUB](../00-sources/github-repositories.md#src-ov-genai-github) — openvinotoolkit/openvino.genai.

See [`00-governance/claim-source-matrix.md`](../00-governance/claim-source-matrix.md) for claim-level mappings.

## Detailed research notes

The sections below retain the substance, examples and step-by-step reasoning from the supplied research documents. They are included here so the main curated file is useful on its own rather than acting as a very short summary.

## Original research: 1.OpenVINO Overall Summary

> **Original document:** `OpenVINO/1. Simplest overall explanation/1.OpenVINO Overall Summary.docx`

#### **OpenVINO, OpenVINO IR and OpenVINO GenAI**

OpenVINO is a software toolkit used to prepare, optimise and run AI models efficiently, mainly on Intel CPUs, GPUs and NPUs. It is designed for local inference, meaning that the model runs directly on the user’s computer rather than relying on a cloud service.

OpenVINO IR is OpenVINO’s native model representation. It stores the model’s computation graph and weights in a format that OpenVINO can understand, optimise and compile for the selected hardware.

OpenVINO GenAI is a higher-level library for running generative AI models such as large language models. It manages tasks including tokenisation, text generation, streaming, chat handling and KV-cache management. OpenVINO GenAI uses OpenVINO Runtime as the underlying inference engine that executes the model locally on the CPU, GPU or NPU.

Application  
│  
▼  
OpenVINO GenAI  
Manages generative AI tasks  
│  
▼  
OpenVINO Runtime  
Compiles and executes the model  
│  
▼  
CPU / GPU / NPU  
Performs the calculations

## Original research: 2. What OpenVINO Does

> **Original document:** `OpenVINO/2. What OpenVINO Actually Does/2. What OpenVINO Does.docx`

#### **OpenVINO as an Inference and Deployment Toolkit**

OpenVINO is primarily an AI inference and deployment toolkit. Its main purpose is to take models that have already been trained and prepare them for efficient local execution on supported hardware, particularly Intel CPUs, GPUs and NPUs.

OpenVINO does not normally train AI models from scratch. Model training is usually completed using frameworks such as PyTorch or TensorFlow before the model is introduced into the OpenVINO workflow.

The main OpenVINO process includes:

- importing or converting the trained model;

- representing the model as an OpenVINO computation graph;

- optimising the graph;

- optionally compressing the model;

- compiling the model for a selected device;

- running inference;

- returning the result to the application.

A simplified workflow is shown below:

Already-trained model  
│  
▼  
Import or convert the model  
│  
▼  
Optimise and optionally compress it  
│  
▼  
Compile it for CPU, GPU or NPU  
│  
▼  
Run inference locally  
│  
▼  
Return the result to the application

OpenVINO can work with models originating from frameworks and formats such as PyTorch, TensorFlow and ONNX. However, the exact conversion process and hardware compatibility depend on the model architecture, operations and target device.

Overall, OpenVINO focuses on making trained AI models more practical, efficient and suitable for local deployment rather than developing or training the models themselves.

## What this means for the project

OpenVINO provides the main Intel-focused backend route. It should be integrated through direct APIs rather than requiring a local HTTP server, which supports the project requirement for offline and restricted environments.

## Summary

- OpenVINO loads, compiles and runs AI graphs.
- OpenVINO GenAI adds generation-focused pipeline support.
- The chosen device and actual execution device must both be recorded.
- OpenVINO and TurboQuant solve different parts of the project.

## Sources used

External source details and reliability notes are recorded in [`../00-sources`](../00-sources/README.md).

- `SRC-OV-GENAI-2026`
- `SRC-OV-GENAI-GITHUB`
