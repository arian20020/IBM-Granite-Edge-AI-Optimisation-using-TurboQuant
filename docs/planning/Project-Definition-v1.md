# Project Definition and Scope Baseline

**Version:** 1.0  
**Status:** Baseline for supervisor review  
**Created:** 11 July 2026  
**Scope-freeze date:** 14 July 2026  
**Target development-completion date:** 15 August 2026  
**Project:** IBM Granite Edge AI Optimisation using TurboQuant  
**Primary platform:** Windows 11 on consumer Intel hardware  

---

## 1. Problem Statement

Large language models can support tasks such as answering questions, producing summaries, generating code and helping users work with large amounts of information. Running these models locally can reduce dependence on cloud services, allow the system to work without a continuous internet connection and keep prompts and files on the user’s own computer. However, local inference is still difficult on ordinary consumer laptops because capable models can require large amounts of memory, processing power and technical configuration.

A local language model does not only need memory for the model file itself. Memory is also required for the model weights, the key–value cache, temporary runtime buffers, the application and the operating system. The key–value cache stores information from earlier tokens so that the model can continue generating text efficiently. It becomes larger as the context length increases. As a result, a model may load successfully at the start of a session but become slower, run out of memory or fail when a longer document or conversation is used. Model file size alone is therefore not enough to decide whether a model will run reliably on a particular computer. TurboQuant and other KV-cache research identify this growing cache as an important memory bottleneck during long-context inference.

Quantisation can reduce memory use by storing model weights, activations or KV-cache values with fewer bits. However, these methods solve different parts of the problem and do not always produce the same results. More aggressive quantisation may reduce memory, but it may also affect factual correctness, instruction following, output formatting, long-context recall, speed or runtime stability. A smaller numerical representation is also not automatically faster because the result depends on the runtime, available low-precision kernels and the computer hardware. The main engineering challenge is therefore not simply to use the lowest possible number of bits. It is to find a configuration that reduces memory while keeping output quality, performance and reliability at an acceptable level. AI-engineering practice similarly treats model quality, latency, system cost and operational behaviour as connected evaluation concerns rather than measuring only whether a model produces an answer.

TurboQuant is a promising form of online vector quantisation that can be used to reduce the size of the KV cache. Its published results report strong compression with limited quality loss under the models, benchmarks and hardware configurations used in the original research. However, these results cannot automatically be assumed to apply to IBM Granite models running through llama.cpp or OpenVINO on Windows Intel computers. Repository implementations may also support different parts of the method, different cache formats or different hardware backends. A setting may be accepted by a runtime but still fall back to a standard cache or another device. TurboQuant must therefore be treated as an experimental runtime optimisation whose real activation, memory benefit, speed, output quality and stability must be measured directly. It must also remain separate from model-weight quantisation: TurboQuant does not by itself create a smaller GGUF model-weight file. The project requirements already define upstream llama.cpp as the dependable primary route and allow TurboQuant to be exposed only for configurations supported by evidence.

Local inference also remains difficult for users who are not AI or software specialists. A user may need to understand model formats, weight precision, KV-cache precision, context length, available RAM, CPU or GPU support, runtime versions and command-line arguments. The requested device may not be the device that actually runs the model, and different combinations of model format, cache type, runtime and hardware may be supported, unsupported or experimental. This creates a usability and reliability problem: users need help deciding whether a model is compatible, whether it is likely to fit in memory and which tested configuration offers a suitable balance between quality and efficiency. The project baseline therefore requires the application to separate measured results from estimates, display only valid or clearly labelled experimental configurations and record the actual backend, device and optimisation state used during formal tests.

These problems are particularly relevant to areas such as education and healthcare, where local processing may be useful because internet access, hardware budgets, software permissions and the handling of sensitive information can be restricted. However, local execution does not automatically make an AI system private, secure, accurate or safe. The Department for Education warns that generated content can be inaccurate, biased, unsafe, unreliable or out of date, and that professional judgement remains necessary. It also highlights data protection, safeguarding and intellectual-property responsibilities. The World Health Organization similarly states that ethics, human rights, accountability and human oversight must be central to the use of AI in health. The proposed application is therefore a research prototype for local model deployment and evaluation; it is not a clinical system, a medical-advice tool or a replacement for teachers, healthcare professionals or other human experts.

The practical problem addressed by this project is therefore the gap between the growing capability of local language models and the ability of ordinary Windows Intel computers and non-specialist users to run them efficiently, safely and transparently. The project needs to provide a dependable way to import and inspect selected IBM Granite models, examine the available hardware, estimate whether a complete configuration is likely to fit in memory, explain the trade-offs between quality and efficiency, recommend only verified configurations and run the model locally without requiring the user to work through terminal commands. It must also evaluate whether TurboQuant provides a measurable improvement over supported standard KV-cache configurations, rather than assuming that published compression claims will transfer directly to the project environment.

## 2. Project Aim

The aim of this project is to design, build and evaluate a WinUI 3 desktop application for Windows 11 Intel PCs, aimed at workers in education and healthcare. The application will allow users to import or download supported IBM Granite models, check whether they are compatible with the available hardware and likely to fit in memory, select a verified configuration, and run the models locally through a simple user interface. The project will also investigate whether TurboQuant is a practical KV-cache quantisation method for reducing memory use while maintaining acceptable output quality, performance and runtime stability.

## 3. Research Questions

### RQ1 — Intel Hardware and Runtime Compatibility

Which selected IBM Granite models and local inference routes—upstream
llama.cpp, TurboQuant-enabled llama.cpp forks, official OpenVINO GenAI,
and OpenVINO GenAI with TurboQuant—can run reliably on the target
Windows Intel CPU and integrated GPU, and what compatibility, build,
driver, device-offloading and fallback limitations apply?

### RQ2 — TurboQuant Memory, Quality and Performance Trade-off

Compared with matched standard KV-cache baselines, how much can verified
TurboQuant implementations—including TurboQuant-enabled llama.cpp forks
and the OpenVINO GenAI TurboQuant path—reduce KV-cache and total memory
use for selected IBM Granite models, and what effects do they have on
output quality, usable context length, inference speed and runtime stability?

### RQ3 — Feasibility Within Lower-Memory Systems

Which complete IBM Granite configurations are practical within total
system-memory budgets of 4 GB, 8 GB and 16 GB, and how do model size,
weight quantisation, KV-cache format, context length, runtime and execution
device affect memory use, output quality and inference speed?

### RQ4 — End-to-End Application Usefulness and Reliability

How reliably and clearly can a WinUI 3 desktop application enable education
and healthcare workers to import or download a supported IBM Granite model,
inspect model and Intel hardware compatibility, estimate memory requirements,
recommend a verified configuration and run the model locally without
requiring command-line knowledge?

### Exploratory Question — TurboVec Feasibility

Can a clearly identified and reproducible TurboVec implementation run on
the target Windows Intel system and produce a vector-search or retrieval
artefact that can be used with selected IBM Granite models, and would this
provide enough value to justify integration into the application?

## 4. Project Objectives

1. Insert measurable objectives.
2. Link every objective to at least one requirement or RQ.

## 5. First-Release Scope

### 5.1 Must Have

- Insert committed first-release work.

### 5.2 Should Have

- Insert work attempted after the Must Haves are stable.

### 5.3 Experimental

- Insert work that requires a technical evidence gate.

### 5.4 Deferred

- Insert work deliberately excluded from the first release.

## 6. Claims Not Made by This Project

- No universal Granite or Intel compatibility claim.
- No guaranteed TurboQuant or OpenVINO success claim.
- TurboQuant does not create a smaller GGUF weight file.
- Measured results, estimates and paper claims remain separate.

## 7. Definition of a Satisfactory Project Outcome

Insert measurable completion conditions.

## 8. Constraints and Assumptions

### 8.1 Constraints

Insert time, hardware, runtime, licensing and access constraints.

### 8.2 Assumptions

Insert assumptions and explain how each will be checked.

## 9. Scope Change Rule

Version 1.0 becomes the working scope baseline on 14 July 2026.

Any major change must record the date, reason, evidence, schedule effect, risk effect,
work removed to create capacity, final decision and significant supervisor feedback.

No new major feature will be added after 10 August 2026.
