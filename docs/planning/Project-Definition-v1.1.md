# Project Definition and Scope Baseline

**Document ID:** PLAN-PROJ-001  
**Version:** 1.1  
**Status:** Developer-approved working baseline — supervisor review pending  
**Prepared and approved by:** Arian B  
**Effective date:** 14 July 2026  
**Supersedes:** Project Definition v1.0  
**Related change:** CHG-013 / CR-013  
**Target development-completion date:** 15 August 2026  
**Project:** IBM Granite Edge AI Optimisation using TurboQuant  
**Primary platform:** Windows 11 x64 on consumer Intel hardware  

> This is the working first-release definition used for development and verification. “Supervisor review pending” means that no supervisor approval is being claimed. Later supervisor feedback must be recorded through a new dated change entry rather than silently editing this baseline.

---

## 1. Problem statement

Large language models can support document review, summarisation, question answering, content creation and coding. These capabilities may be useful in education and healthcare, but many services depend on cloud infrastructure and continuous internet access. Cloud dependence can create availability, privacy, governance and organisational restrictions, especially where prompts or documents may contain sensitive information.

Running a language model locally can reduce dependence on external services, but ordinary Windows laptops have limited memory and processing capacity. A working configuration needs memory for the model weights, the key–value cache, runtime data, the desktop application and the operating system. The KV cache grows as context length increases, so model-file size alone does not show whether a configuration will remain stable.

Quantisation may reduce memory use, but weight quantisation and KV-cache quantisation are different operations. More aggressive compression may affect output quality, speed, context capacity or stability. A requested backend or device may also silently fall back to another route, so the project must record the route and device that actually ran.

TurboQuant is the project’s main experimental optimisation contribution. Published results cannot be assumed to transfer directly to IBM Granite, Windows, Intel CPUs or integrated GPUs, llama.cpp, OpenVINO or the project’s application. The project therefore needs a narrow, reproducible app-integrated test with activation proof and a dependable upstream fallback.

The practical problem is the gap between the potential benefits of local AI and the ability of non-specialist users to select a valid model, understand whether it will fit, choose a supported configuration and run it without command-line knowledge.

---

## 2. Project aim

Design, build and evaluate a WinUI 3 desktop research prototype for Windows 11 Intel PCs that allows a non-specialist user to select and inspect a supported IBM Granite model, analyse the local hardware, estimate memory fit, choose a verified configuration and run the model locally through a guided interface.

The project will also determine whether one pinned TurboQuant-enabled configuration can reduce KV-cache memory use while maintaining acceptable output quality, performance and runtime stability, with activation evidence and a dependable upstream llama.cpp fallback.

---

## 3. Research questions

### RQ1 — Intel hardware and runtime compatibility

Which selected IBM Granite models and local inference routes run reliably on the tested Windows Intel CPU and integrated GPU, and what build, device, backend, compatibility and fallback limitations apply?

### RQ2 — TurboQuant memory, quality and performance trade-off

Compared with matched standard KV-cache baselines, how much does the verified TurboQuant configuration reduce KV-cache and total memory use, and what effects does it have on output quality, context capacity, speed and stability?

### RQ3 — Feasibility within lower-memory systems

Which complete Granite configurations are practical within 4 GB, 8 GB and 16 GB total system-memory budgets, and how do model size, weight precision, KV-cache format, context length, runtime and execution device affect the result?

### RQ4 — End-to-end application usefulness and reliability

How reliably and clearly can the WinUI application allow a non-specialist user to import, inspect, assess, configure and run a local Granite model without command-line knowledge?

Optional download, multi-turn chat and persistent model-processing routes are evaluated only if they are implemented after the core Must-Have route is stable.

### RQ-TV — TurboVec feasibility and release decision

Can a clearly identified and reproducible TurboVec implementation build or run on the target Windows Intel system, what contract and limitations apply, and should it be implemented, retained as a command-line demonstrator, deferred or excluded?

This question does not require full first-release application integration.

---

## 4. Objectives

### O1 — Build the WinUI 3 desktop application

Develop a packaged Windows 11 x64 WinUI 3 application with a guided workflow and clear dependable, experimental and unsupported states.

### O2 — Implement model import, inspection and optional download

Require local model selection, validation and trustworthy inspection. Treat approved model downloading as a Should Have after the core route is stable.

### O3 — Analyse Intel hardware and estimate memory

Collect the relevant CPU, GPU and memory information and provide a transparent estimate that includes weights, KV cache, runtime overhead, application/OS allowance and safety reserve.

### O4 — Generate and explain supported configurations

Generate only complete combinations of model artefact, runtime, runtime build, KV-cache method, context length and target device. Explain recommendations and limitations.

### O5 — Integrate local command-line inference backends

Safely launch and control supported local runtimes from WinUI, capture output and errors, stream progress, support cancellation and terminate child processes without requiring terminal commands.

### O6 — Provide a local chat experience

Require one complete local prompt-and-response route with streaming, cancellation and actual runtime/device reporting. Multi-turn chat remains a Should Have.

### O7 — Provide optional model optimisation and export

After the core Must Haves are stable, attempt one controlled GGUF weight-quantisation and manifest route. This remains separate from TurboQuant runtime KV-cache optimisation.

### O8 — Verify llama.cpp, OpenVINO and TurboQuant on Intel

Preserve exact model/runtime/build/device evidence, prove actual activation and fallback state, and keep the upstream llama.cpp route dependable.

### O9 — Assess TurboVec feasibility and decide its release role

Identify and pin the exact implementation, review provenance and licence, run a bounded technical spike and record an Implement, Demonstrator Only, Defer or Exclude decision. Full knowledge-file, embedding and retrieval integration is deferred.

### O10 — Evaluate memory, quality and performance trade-offs

Use controlled definitions and matched configurations to assess memory, speed, quality, context capacity, failures and stability.

### O11 — Test, package and document the system

Provide traceable verification, reproducible evidence, release controls, manuals and evidence-based answers to the research questions.

---

## 5. First-release scope

### 5.1 Core Must-Have boundary

The first release must provide:

- a working Windows 11 x64 WinUI 3 application;
- local selection and validation of a supported Granite GGUF model;
- trustworthy model inspection and classified outcomes;
- Intel hardware detection and transparent memory-fit estimation;
- complete supported configuration selection;
- safe command-line runtime control;
- one dependable upstream llama.cpp local prompt-and-response route;
- streaming/progress and cancellation for core long-running operations;
- requested-versus-actual runtime, backend and device reporting;
- one pinned, clearly Experimental app-integrated TurboQuant configuration;
- proof that TurboQuant actually activated;
- a dependable upstream recovery route when TurboQuant fails or is unavailable;
- controlled experiments, evidence, limitations, documentation and release traceability;
- an evidence-based TurboVec implementation/release-role decision.

### 5.2 Should-Have scope

The following are attempted only after the core Must Haves are stable:

- approved recommended-model download;
- download disk-space, cancellation and SHA-256 integrity controls;
- multi-turn chat;
- persistent GGUF weight quantisation;
- a processing manifest for any generated model artefact;
- selected additional import or export conveniences;
- optional packaging improvements such as MSIX where not already required by another active requirement.

A Should-Have feature that is implemented must still satisfy all of its stated acceptance and safety criteria.

### 5.3 Experimental scope

The first release requires only one exact tested TurboQuant combination:

- one pinned model;
- one pinned runtime/build;
- one pinned cache configuration;
- one tested CPU or GPU route;
- clear Experimental labelling;
- activation proof;
- requested and actual state recording;
- controlled cancellation and failure handling;
- verified upstream fallback.

No universal Granite, TurboQuant, Intel, CPU, GPU, NPU, llama.cpp or OpenVINO compatibility claim is permitted.

### 5.4 Deferred scope

The following are deferred from the first release:

- full TurboVec knowledge-file import, extraction and chunking;
- application-integrated embedding generation;
- TurboVec vector compression/index integration;
- retrieval-to-Granite chat integration;
- the full matched TurboVec retrieval comparison;
- unsupported NPU inference claims;
- unrestricted model downloading;
- multi-user or server operation;
- multiple simultaneous model sessions;
- automatic runtime/model updates or prompt/document analytics collection.

### 5.5 Explicit technical distinctions

The project must keep these concepts separate:

- model artefact format, such as GGUF or OpenVINO IR;
- runtime route, such as llama.cpp or OpenVINO GenAI;
- exact runtime build or executable;
- requested and actual target device;
- persistent model-weight processing;
- runtime-only KV-cache configuration;
- runtime smoke testing;
- model-quality evaluation.

TurboQuant changes runtime KV-cache behaviour and does not by itself create a smaller GGUF model file.

---

## 6. Satisfactory project outcome

The project is satisfactory when:

1. the active Must-Have catalogue is versioned and traceable;
2. the WinUI application completes the core local-model workflow;
3. the dependable upstream route runs end to end;
4. one pinned TurboQuant route is integrated or a reproducible evidence-backed blocker is recorded according to the controlling requirement;
5. no fallback or unsupported device is misreported;
6. memory, speed, quality, context and stability evidence is preserved with exact configurations;
7. failures and limitations are retained;
8. the final report answers each research question using project evidence;
9. the release documentation and evidence pack match the final RTM;
10. deferred and unimplemented features are not represented as complete.

---

## 7. Claims not made

The project does not claim:

- universal IBM Granite compatibility;
- universal Intel CPU, GPU or NPU compatibility;
- clinical, NHS, school or classroom approval;
- automatic privacy, security or legal compliance merely because inference is local;
- guaranteed TurboQuant activation or benefit outside the tested configuration;
- that TurboQuant creates a new smaller model file;
- that estimated results are measured hardware results;
- that a runtime smoke test proves quality preservation;
- that the deferred TurboVec subsystem was integrated.

---

## 8. Constraints and assumptions

### Constraints

- Development is time-limited to the project schedule.
- Validation is limited to available Windows Intel hardware.
- Model files and third-party runtimes may have licence and distribution restrictions.
- Experimental forks may fail to build, run or activate.
- Large models and long contexts may exceed available memory.
- NPU evidence requires suitable hardware and is not inferred from device detection alone.

### Assumptions requiring evidence

- selected model files are obtained from a legitimate source;
- the exact runtime and model revision can be pinned;
- requested and actual backend/device state can be recorded;
- memory measurements use a controlled definition;
- experimental activation can be demonstrated;
- evidence files can be backed up and checksummed;
- the selected TurboVec implementation has an identifiable repository, version and licence.

---

## 9. Change-control rule

This v1.1 document and MoSCoW Requirements Baseline v1.2 form the developer-approved working scope baseline from 14 July 2026.

A material change must record:

- a stable change ID;
- date and reason;
- affected requirement IDs;
- affected objectives, research questions and work packages;
- schedule and risk effect;
- decision owner and approval state;
- superseded or replacement links where applicable;
- affected tests, evidence and report sections;
- GitHub issue, pull request or commit.

Supervisor feedback does not retrospectively change this baseline. It creates a new reviewed change record.
