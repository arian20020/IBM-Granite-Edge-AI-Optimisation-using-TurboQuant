# Project scope and research questions

> **Document status:** Detailed project research guide
> **Version:** 3.0
> **Last updated:** 14 July 2026

## Problem being investigated

Large language models can answer questions, summarise information, generate text and code, and help people work with large collections of information. Many common products run these models through cloud services. That creates practical problems for people who need to work offline, who handle private information, or who use machines where network access and local ports are restricted.

The project is therefore investigating a Windows desktop application that runs IBM Granite models locally on Intel PCs. The application is aimed particularly at education and healthcare contexts. It is not intended to make clinical decisions or replace a teacher. Its purpose is to provide a private local assistant that can run without sending prompts and documents to a cloud model.

## Why local inference is difficult

Running locally moves responsibility from the cloud provider to the user's computer. The application must deal with:

- limited RAM and device memory;
- large model-weight files;
- a KV cache that grows as the conversation becomes longer;
- different CPU, GPU and NPU capabilities;
- model-format and backend compatibility;
- slower loading or generation on lower-powered hardware;
- quality loss caused by aggressive quantisation;
- installation, model import and error messages that non-expert users can understand.

The engineering problem is therefore not simply “run an LLM.” It is to build a usable local system that selects a realistic model and optimisation route, runs it through a reproducible backend, and explains its behaviour to the user.

## Main technical routes

### IBM Granite

Granite Language models are the main model family. Granite 4.1 3B is the first practical validation target because it is small enough to test on ordinary machines while still being a real instruction-following model.

### llama.cpp and GGUF

llama.cpp provides the command-line GGUF route. This supports local prompting without requiring a web server. It is useful for restricted machines where a localhost port may be blocked.

### OpenVINO GenAI

OpenVINO provides the Intel-focused route. It can compile supported model graphs for Intel CPU, GPU and NPU devices and can be used through a direct API.

### TurboQuant research

TurboQuant is being investigated as a way to reduce vector and KV-cache memory. It must be treated as experimental until its implementation and published behaviour are reproduced on IBM Granite and the target Intel hardware.

## Target users

### Education

Students and staff may need a local assistant in classrooms, libraries, laboratories or locations with unreliable internet. Local inference can also keep draft work and imported documents on the machine.

### Healthcare

Healthcare staff may work with sensitive information and devices with strict network policies. A local route reduces cloud dependence, although the application must still be designed carefully and must not be described as a clinical decision system.

## Project objectives

1. Build a beginner-friendly WinUI 3 desktop application.
2. Let users import supported GGUF or OpenVINO model files.
3. Analyse whether the selected model fits the available hardware.
4. Run prompts locally through a command-line or direct API route.
5. Compare original and optimised model configurations.
6. Measure memory, first-token latency, generation speed and answer quality.
7. Investigate KV-cache compression and TurboQuant-related implementations.
8. Keep every technical claim and experiment traceable to a source, command, log or decision record.

## Research questions

1. Can the selected IBM Granite models run reliably through llama.cpp and OpenVINO on the available Windows and Intel hardware?
2. Which model sizes and weight formats are realistic for machines with approximately 4–16 GB of available memory?
3. How much memory is used by the model weights and by the KV cache at different context lengths?
4. Which KV-cache quantisation methods reduce memory without unacceptable quality loss?
5. Can a TurboQuant implementation be reproduced and adapted to the selected backend?
6. Do CPU, GPU, NPU, AUTO or HETERO execution routes provide the best measured result for each model?
7. How should Automatic, Quality, Balanced and Efficiency modes map to real tested configurations?
8. How can the application explain model compatibility and failures in simple language?

## Scope boundaries

### Included

- local text generation;
- IBM Granite Language models;
- GGUF and OpenVINO model routes;
- Windows and Intel-focused validation;
- model import, hardware checks, chat and metrics;
- weight and KV-cache quantisation experiments;
- command-line prompting and direct APIs;
- reproducible testing and evidence.

### Not part of the first implementation

- cloud-hosted inference as the main route;
- clinical diagnosis or treatment recommendations;
- replacing teachers or assessed academic judgement;
- automatic trust in third-party TurboQuant forks;
- unsupported claims that an NPU, GPU or compression method is faster before testing;
- macOS production implementation during the first Windows milestone.

## Reading order

1. [`02-ibm-granite`](../02-ibm-granite/README.md)
2. [`03-kv-cache-and-turboquant`](../03-kv-cache-and-turboquant/README.md)
3. [`04-openvino`](../04-openvino/README.md)
4. [`05-local-inference-tools`](../05-local-inference-tools/README.md)
5. [`06-repository-reviews`](../06-repository-reviews/README.md)
6. [`07-evaluation-and-decisions`](../07-evaluation-and-decisions/README.md)

## Summary

The project is solving a combined model, systems and user-experience problem. Local inference offers privacy and offline operation, but it is only useful when the model fits the hardware, the backend works reliably, the quality remains acceptable and the application is simple enough for non-expert users.
