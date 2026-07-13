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

To achieve the project aim and answer the research questions, the project
will complete the following objectives.

### O1 — Build the WinUI 3 Desktop Application

Design and develop a packaged WinUI 3 desktop application for Windows 11
Intel computers. The application will provide a clear guided workflow for
model import, inspection, hardware analysis, configuration selection,
model processing and local interaction.

The interface will be designed for users who do not have specialist
knowledge of AI runtimes, model formats or command-line tools.

### O2 — Implement Model Import, Download and Inspection

Allow the user to select a supported IBM Granite model stored on the
computer or download an approved recommended model from a trusted source.

The application will validate the selected model, inspect its important
metadata and classify it as ready, ready with warnings, conversion required,
unsupported, or invalid.

The application will show clear reasons and next steps when the model
cannot be used.

### O3 — Analyse Intel Hardware and Estimate Memory Use

Collect the Intel CPU, GPU and system-memory information needed to assess
local inference compatibility.

Estimate the peak memory required by a complete model configuration,
including model weights, KV cache, runtime overhead and a safety reserve.

The application will explain whether the configuration is likely to fit,
may require optimisation, or has no safe supported option.

### O4 — Generate and Explain Supported Configurations

Create a compatibility and configuration-selection component that produces
only complete and verified combinations of model format, weight precision,
KV-cache format, context length, runtime, backend and execution device.

Provide understandable Quality, Balanced, Efficiency and Automatic modes
where valid choices exist, and explain why the selected configuration was
recommended.

### O5 — Integrate Local Command-Line Inference Backends

Develop a secure backend adapter that allows the WinUI application to start
and control supported command-line inference tools such as llama.cpp,
OpenVINO utilities and verified experimental runtime forks.

The adapter will:

- build process arguments safely;
- start the backend without opening a terminal window;
- pass the selected model and configuration;
- capture standard output and error output;
- stream generated output back to the application;
- detect runtime failures and fallback;
- support cancellation;
- stop child processes safely;
- clean up temporary files;
- avoid requiring the user to type terminal commands;
- avoid a local web server or network port where technically possible.

### O6 — Provide a Local Chat Experience

Create a local chat interface that allows the user to interact with at
least one supported IBM Granite model through the application.

The chat workflow will:

- show the active model and configuration;
- accept a user prompt;
- display generated text as it is produced;
- support at least two turns within one session;
- allow generation to be stopped;
- remain responsive while the model is running;
- show loading and generation progress;
- report the actual runtime and device used;
- show understandable errors;
- allow useful output to be copied or saved.

The user will not need to run llama.cpp or another runtime manually.

### O7 — Implement Model Optimisation and Export

Implement at least one verified model-processing workflow that produces a
new optimised model artefact.

The primary model-file optimisation route will use a supported GGUF
weight-quantisation process, such as a controlled llama-quantize workflow,
to produce a smaller validated GGUF file.

The application will:

- preserve the original model;
- show processing progress;
- support safe cancellation;
- validate the new model;
- inspect the new model again;
- compare the original and processed file sizes;
- record the exact tool, settings and hashes;
- allow the processed model to be used in chat or saved for another
  compatible application.

TurboQuant will remain separate because it changes the runtime KV cache
and does not by itself produce a smaller GGUF model file.

### O8 — Verify llama.cpp, OpenVINO and TurboQuant on Intel Hardware

Verify which selected IBM Granite models can run through upstream
llama.cpp on the target Intel CPU and integrated GPU.

Establish at least one official OpenVINO GenAI baseline on Intel hardware.

Build and test selected TurboQuant-enabled llama.cpp forks and the
OpenVINO GenAI TurboQuant route where technically possible.

For every route, record:

- the exact model and runtime version;
- build settings;
- requested device;
- actual device;
- backend activation;
- fallback behaviour;
- memory use;
- model-loading time;
- time to first token;
- prompt-processing speed;
- generation speed;
- output quality;
- context length;
- runtime stability.

Experimental options will only appear in the application when their real
activation and supported conditions have been proved.

### O9 — Implement a TurboVec-Assisted Knowledge-File Workflow

Develop a small local knowledge-file import and retrieval workflow using
a confirmed TurboVec implementation.

The workflow will allow the user to import selected text-based documents
for use with a supported IBM Granite model.

The application will:

- validate the imported document;
- extract and divide its text into suitable sections;
- generate embeddings for those sections;
- use TurboVec to compress or optimise the stored embedding vectors;
- create a local retrieval index;
- retrieve the most relevant sections for a user question;
- provide the retrieved information to the Granite chat workflow;
- keep the original imported document unchanged.

The project will compare the TurboVec route with an uncompressed vector
baseline using storage size, memory use, processing time, retrieval
quality and final answer usefulness.

### O10 — Evaluate Lower-Memory Feasibility and System Quality

Evaluate which complete Granite configurations are practical within
4 GB, 8 GB and 16 GB total system-memory budgets.

Compare baseline and optimised configurations using:

- model file size;
- KV-cache size;
- peak process and system memory;
- model-loading time;
- time to first token;
- prompt-processing speed;
- generation speed;
- maximum stable context;
- output quality;
- retrieval quality where TurboVec is used;
- application responsiveness;
- failures and runtime stability.

Clearly distinguish results measured on real hardware from estimates or
simulated memory budgets.

### O11 — Test, Package and Document the Completed System

Test the application using unit, integration, contract, end-to-end,
failure, security, compatibility, usability and regression testing.

Preserve successful and unsuccessful experiments, exact configurations,
logs, outputs and measurements.

Provide:

- a reproducible build procedure;
- a packaged Windows installation route;
- a user manual;
- a developer manual;
- known limitations;
- exact dependency and runtime versions;
- final requirement and research-question traceability.

## 5. First-Release Scope

This section defines what will and will not be included in the first project
release.

The detailed requirement IDs, priorities, acceptance criteria, implementation
links and verification evidence will be maintained separately in the MoSCoW
Requirements Baseline v1.2.

The first release will be a Windows research prototype aimed at helping
education and healthcare workers use selected IBM Granite models locally
without needing specialist knowledge of command-line tools, model formats or
AI runtimes.

The application is not intended to be a complete commercial model manager,
a clinical system or a replacement for professional judgement.

### 5.1 Main Release Boundaries

The first release will follow these main boundaries:

- The application will target Windows 11 x64 computers using Intel hardware.
- The application will be developed using WinUI 3 and the Windows App SDK.
- Selected IBM Granite language and code models will be supported.
- Granite 3B-class models will be the main working target.
- Selected Granite 8B configurations will be tested where available memory
  and runtime support make this practical.
- Verified GGUF models will be the main supported model format.
- Upstream llama.cpp will be the main dependable inference runtime.
- Intel CPU inference will be the minimum dependable execution route.
- Intel integrated-GPU routes will be used only where real GPU use has been
  proved.
- Official OpenVINO GenAI will be investigated as a second Intel inference
  route.
- At least one TurboQuant route must run through the WinUI application.
- TurboQuant will be treated as a runtime KV-cache optimisation and not as a
  method for creating a smaller GGUF model file.
- GGUF weight quantisation will be treated as a separate model-processing
  operation.
- TurboVec will be used in a bounded knowledge-file workflow to compress or
  optimise vectors created from imported documents.
- The original model and knowledge files will remain unchanged unless the
  user deliberately starts a separate processing operation.
- No Intel NPU result will be claimed without access to suitable NPU hardware.
- The core inference process will operate locally and will not depend on a
  cloud AI service.
- Internet access may be used for an approved model download or for installing
  required dependencies.

---

### 5.2 Must Have Scope

The following capabilities are required for the first release to be considered
complete.

#### 5.2.1 WinUI 3 Desktop Application

The project must provide a working WinUI 3 desktop application that:

- opens on the target Windows 11 Intel computer;
- provides a clear guided workflow;
- is understandable to users without command-line or AI-engineering
  experience;
- provides navigation between model import, inspection, hardware analysis,
  configuration selection, optimisation, knowledge-file processing and chat;
- remains responsive during long-running operations;
- shows loading, progress, completion, warning and failure states;
- provides plain-English explanations;
- gives the user a clear next step after an error;
- clearly separates dependable and experimental functionality;
- supports basic keyboard navigation;
- works with normal Windows text scaling.

The interface will be designed with education and healthcare workers in mind,
but it will not be described as approved for real classroom or clinical
deployment.

#### 5.2.2 Model Import

The application must allow the user to select a supported local IBM Granite
GGUF model using the Windows file picker.

The application must:

- accept model paths containing spaces and normal Windows characters;
- check that the selected path exists;
- check that the selected item is a file;
- reject empty or incomplete files;
- inspect the file contents rather than trusting only its filename;
- preserve the original model file;
- prevent an invalid model from continuing into inference.

#### 5.2.3 Controlled Model Download

The application must allow the user to download at least one approved
recommended IBM Granite model from a fixed and trusted source.

Before the download begins, the application must show:

- model name;
- model size;
- model format;
- weight quantisation;
- source;
- licence information;
- expected storage location;
- expected download size.

During and after the download, the application must:

- show progress;
- show the current download state;
- allow safe cancellation;
- handle interrupted or failed downloads;
- check available disk space;
- prevent partial files from being treated as complete;
- verify the final file using its expected size and cryptographic hash;
- pass the verified model into the normal inspection workflow.

The first release does not need to provide an unrestricted model marketplace
or allow downloading from arbitrary sources.

#### 5.2.4 Model Inspection and Validation

The application must inspect the information needed for later compatibility,
memory and configuration decisions.

Where available, it must show:

- model name;
- model architecture;
- model format;
- parameter-size information;
- file size;
- weight quantisation;
- supported or declared context information;
- tokenizer information;
- chat-template information;
- important missing metadata.

The model must be placed into one of these states:

- Ready;
- Ready with warnings;
- Conversion required;
- Unsupported;
- Invalid or incomplete.

Each state must include:

- a plain-English explanation;
- the reason for the decision;
- a clear next step.

#### 5.2.5 Intel Hardware Inspection

The application must collect the hardware and system information needed for
local-inference analysis.

This must include:

- Windows version;
- system architecture;
- Intel CPU name;
- installed physical RAM;
- currently available RAM;
- available Intel GPU information;
- available disk space;
- available runtime information;
- relevant execution-device limitations.

The application must distinguish between a route that is:

- generally supported by a runtime;
- installed on the current computer;
- verified by this project;
- experimental;
- unavailable.

A device must not be reported as active only because the user requested it.

#### 5.2.6 Transparent Memory Estimation

The application must estimate the memory required by a complete inference
configuration.

The estimate must consider, where relevant:

- model-weight memory;
- KV-cache memory;
- runtime buffers;
- temporary working memory;
- application overhead;
- Windows and background-process allowance;
- shared CPU and GPU memory;
- a safety reserve.

The estimator must not assume that model file size is equal to total runtime
memory.

The application must show:

- the main parts of the estimate;
- the estimated total;
- currently available memory;
- safety reserve;
- assumptions;
- missing information;
- confidence or uncertainty.

The result must use clear outcomes such as:

- Likely to fit;
- Fits with limited headroom;
- Optimisation recommended;
- Unlikely to fit;
- No verified safe option.

The result must be described as an estimate and not as a guarantee.

#### 5.2.7 Configuration Generation

The application must generate only complete configurations that are valid for
the selected model, runtime and hardware.

A complete configuration may include:

- model format;
- weight quantisation;
- KV-cache format;
- requested context length;
- runtime;
- backend;
- CPU or GPU device;
- CPU thread settings;
- GPU offloading settings;
- other required runtime options.

The application must not combine individually valid settings into a complete
configuration unless that full combination is supported.

#### 5.2.8 Optimisation Modes

The application must provide the following user-facing modes when valid
configurations are available:

- Automatic;
- Quality;
- Balanced;
- Efficiency.

The modes must represent different goals rather than fixed bit-width labels.

- Quality should favour output quality and reliability.
- Balanced should consider quality, memory use and speed together.
- Efficiency should favour reduced memory use while remaining within tested
  quality and stability limits.
- Automatic should choose the highest-ranked verified configuration for the
  current model and hardware.

A mode must be hidden or disabled when no valid configuration exists.

Before inference begins, the application must show:

- selected model;
- selected runtime;
- selected backend;
- selected device;
- weight format;
- KV-cache format;
- context length;
- estimated memory;
- whether the route is dependable or experimental;
- why the configuration was selected.

#### 5.2.9 Secure Command-Line Runtime Integration

The application must start and control supported command-line inference tools
internally.

The user must not need to open PowerShell, Command Prompt or another terminal.

The runtime adapter must:

- use structured and safely escaped arguments;
- handle paths containing spaces;
- avoid unsafe command-string concatenation;
- launch the runtime without requiring a visible terminal window;
- capture standard output;
- capture standard error;
- stream useful output into the WinUI application;
- detect startup and loading failures;
- support user cancellation;
- apply suitable timeouts;
- stop child processes safely;
- clean temporary resources after success, cancellation or failure;
- convert technical failures into understandable application errors.

The main inference route should not require a local web server or an open
network port.

#### 5.2.10 Upstream llama.cpp Route

Upstream llama.cpp will be the main dependable inference route.

The first release must demonstrate this complete workflow:

1. select or download a supported Granite GGUF model;
2. validate and inspect the model;
3. inspect the Intel hardware;
4. estimate memory requirements;
5. generate a valid configuration;
6. allow the user to confirm the configuration;
7. launch llama.cpp from the WinUI application;
8. load the model successfully;
9. generate valid text locally;
10. record the actual runtime, backend and device used.

Intel CPU inference is the required minimum route.

Intel integrated-GPU inference may also be included where the real device use,
offloading and stability are proved.

#### 5.2.11 App-Integrated TurboQuant Route

The first release must include at least one end-to-end TurboQuant route inside
the WinUI application.

The preferred first route will use a verified TurboQuant-enabled llama.cpp
fork with a selected IBM Granite GGUF model on the target Intel CPU.

The application must allow the user to:

- select a supported TurboQuant configuration;
- see that the route is labelled Experimental;
- see which model, runtime, device and cache setting are supported;
- view the expected memory effect;
- start the TurboQuant runtime without entering a command;
- view loading and generation progress;
- use the normal local chat interface;
- stop generation safely;
- see the actual runtime, backend, device and KV-cache setting;
- return to the standard llama.cpp route if TurboQuant is unavailable or
  fails.

At least one TurboQuant configuration must complete this application workflow:

1. import or select a supported Granite model;
2. inspect the model;
3. inspect the Intel hardware;
4. select a verified TurboQuant configuration;
5. display the estimated memory and experimental warning;
6. launch the TurboQuant-enabled runtime from WinUI;
7. load the model;
8. generate valid local text;
9. record the actual backend, device and cache setting;
10. save the run measurements.

TurboQuant must not be shown as active unless its use is proved through
runtime output, logs, measurements or other direct evidence.

Upstream llama.cpp with a standard supported KV cache must remain available as
the dependable fallback.

#### 5.2.12 Local Chat

The application must provide a local chat interface for at least one supported
IBM Granite model.

The chat must:

- show the active model;
- show the active runtime;
- show the actual execution device;
- show whether the selected route is dependable or experimental;
- accept a user prompt;
- show generated text while it is produced;
- support at least two turns in one session;
- keep the window responsive;
- allow the user to stop generation;
- show loading and generation states;
- show understandable failures;
- allow generated output to be copied;
- preserve the conversation state during the session.

The application must record basic runtime information where available,
including:

- model-loading time;
- time to first token;
- prompt-processing speed;
- generation speed;
- generated-token count;
- context used;
- requested backend and device;
- actual backend and device.

Prompts and responses must not be uploaded to a cloud AI service by the core
workflow.

#### 5.2.13 Model-Weight Optimisation and Export

The first release must include at least one verified model-file optimisation
workflow.

The main route will use a supported GGUF weight-quantisation tool to create a
new GGUF file.

The application must:

- preserve the original model;
- check that the source model is suitable for processing;
- avoid automatically re-quantising an already quantised model through an
  unsafe route;
- check available disk space;
- show processing progress where available;
- support safe cancellation;
- record the processing tool and version;
- record the selected quantisation settings;
- record the source-model hash;
- record the output-model hash;
- validate the new model;
- inspect the new model again;
- compare the source and output file sizes;
- allow the new model to return to the normal inspection, configuration and
  chat workflow;
- allow the output file to be saved for another compatible local
  application.

TurboQuant must remain separate from this workflow because TurboQuant changes
the runtime KV cache and does not create a new GGUF weight file.

#### 5.2.14 Official OpenVINO Baseline

The project must complete at least one official OpenVINO GenAI baseline test
using a selected IBM Granite model on Intel hardware.

The test must either:

- load the model and produce valid output; or
- produce a repeatable and fully documented blocker.

The test must record:

- model and model revision;
- model format;
- OpenVINO Runtime version;
- OpenVINO GenAI version;
- requested device;
- actual device;
- loading result;
- generation result;
- memory use;
- speed;
- output quality;
- failures and fallback.

The official baseline must remain separate from custom, nightly or
TurboQuant-enabled OpenVINO builds.

A complete official OpenVINO route inside the application will be added only
after its integration gate passes.

#### 5.2.15 TurboQuant Evaluation

The project must compare at least one verified TurboQuant configuration with a
matched standard KV-cache baseline.

The comparison must keep the following equal as far as reasonably possible:

- Granite model;
- model revision;
- weight format;
- prompt;
- chat template;
- context length;
- generation settings;
- hardware;
- runtime environment;
- measurement method.

The comparison must measure:

- reported KV-cache memory;
- measured peak process memory;
- total system-memory effect;
- model-loading time;
- time to first token;
- prompt-processing speed;
- generation speed;
- maximum stable context;
- output quality;
- instruction following;
- output formatting;
- runtime stability;
- crashes;
- out-of-memory failures;
- backend fallback.

The final report must distinguish between:

- the published TurboQuant algorithm;
- the exact repository implementation tested;
- theoretical compression;
- runtime-reported cache allocation;
- measured total-memory reduction.

#### 5.2.16 Lower-Memory Evaluation

The project must assess selected Granite configurations against total
system-memory budgets of:

- 4 GB;
- 8 GB;
- 16 GB.

For each result, the report must say whether it was:

- measured on a real computer;
- measured under a controlled memory limit;
- calculated from measured components;
- predicted by the application.

The evaluation must consider:

- Windows memory use;
- background processes;
- WinUI application memory;
- model-weight memory;
- KV-cache memory;
- runtime buffers;
- shared GPU memory;
- context length;
- output quality;
- speed;
- stability.

The project will identify complete configurations that appear practical
within each budget.

It will not claim that every Granite model will run on every 4 GB, 8 GB or
16 GB computer.

#### 5.2.17 TurboVec Knowledge-File Workflow

The first release must include one bounded TurboVec-assisted knowledge-file
workflow inside the application.

The workflow will allow the user to import selected text-based knowledge
files for use with the Granite chat system.

The application must:

- validate the selected knowledge file;
- preserve the original file;
- extract usable text;
- divide the text into smaller searchable sections;
- generate embeddings for those sections;
- create an uncompressed-vector baseline;
- use the selected TurboVec implementation to compress or optimise the
  vectors;
- create or use a local retrieval index;
- retrieve relevant sections for a user question;
- provide the retrieved sections to the Granite chat workflow;
- clearly show when document-based context is being used.

The project must record:

- exact TurboVec repository or implementation;
- exact version or commit;
- licence;
- supported operating system;
- input format;
- output format;
- vector or embedding format;
- retrieval method;
- known limitations.

The TurboVec route must be compared with an uncompressed-vector baseline
using:

- vector-storage size;
- memory use;
- processing or indexing time;
- query time;
- retrieval relevance;
- final answer usefulness;
- failures and stability.

TurboVec must not be described as compressing the original PDF, Word or text
file. It operates on the vectors or embeddings created from the file.

Because TurboVec support may depend on an early or specialised
implementation, the user-facing route must be labelled Experimental until its
exact input, output and retrieval behaviour have been proved.

#### 5.2.18 Testing and Evidence

The first release must include testing appropriate to the completed system,
including:

- unit tests;
- model-parser tests;
- validation tests;
- memory-estimator tests;
- configuration-registry tests;
- path and process-argument security tests;
- integration tests;
- end-to-end tests;
- cancellation tests;
- child-process cleanup tests;
- failure and fallback tests;
- runtime compatibility tests;
- offline-operation tests;
- basic usability testing;
- basic accessibility checking;
- formal inference experiments;
- regression tests for important defects.

Every final experiment must retain:

- unique experiment ID;
- exact hardware information;
- model name and hash;
- runtime version and hash;
- build settings;
- requested configuration;
- actual configuration;
- raw standard output;
- raw standard error;
- raw memory measurements;
- raw speed measurements;
- model responses;
- failed runs;
- processed results;
- analysis notes.

Every result must be labelled as:

- measured;
- estimated;
- reproduced from another source;
- inferred from evidence;
- not yet verified.

#### 5.2.19 Privacy, Security and Responsible Use

The first release must:

- keep the core inference workflow local;
- avoid uploading models, prompts, knowledge files or answers to a cloud AI
  service;
- avoid committing API keys, passwords or secrets;
- avoid real patient information;
- avoid identifiable pupil information;
- use synthetic, public or properly authorised test material;
- verify the source and integrity of downloaded models;
- handle file paths and child processes safely;
- record third-party model and runtime licences;
- clearly label experimental features;
- explain that generated content can be incorrect;
- state that education and healthcare outputs require human review;
- state that the application does not provide medical diagnosis or treatment
  advice;
- avoid presenting model recommendations as guaranteed.

#### 5.2.20 Build, Release and Documentation

The project must provide:

- source code in the Git repository;
- a reproducible Windows x64 build process;
- recorded dependency and runtime versions;
- a clean-checkout build test;
- documented automated-test commands;
- a basic distributable release build;
- project README;
- user manual;
- developer manual;
- build and installation instructions;
- known-limitations document;
- runtime, model and dependency register;
- final requirements traceability;
- final feature-status table;
- release checksums;
- version tag;
- independent project backup.

---

### 5.3 Should Have Scope

The following features are valuable but may be reduced or deferred if the
Must Have work is not stable.

#### Model Support

- Drag-and-drop model import.
- Recognition of supported OpenVINO IR model folders.
- Recognition of selected Hugging Face or Safetensors model folders.
- Support for additional verified Granite variants.
- More than one approved recommended-model download.
- Resuming an interrupted model download.

#### Knowledge Files

- Support for PDF files through safe local text extraction.
- Support for Word documents through safe local text extraction.
- Support for more than one imported knowledge file.
- A visible list of indexed document sections.
- Saving and reopening a local TurboVec index.

#### Chat and User Interface

- Saving chat responses to a local file.
- Saving inspection reports.
- A local history of benchmark runs.
- Charts showing memory, speed and context trade-offs.
- More detailed explanations of technical terms.
- Expanded screen-reader testing.
- More advanced accessibility evaluation.

#### Runtime and Optimisation

- A fully integrated official OpenVINO GenAI route inside the WinUI
  application.
- An app-integrated OpenVINO GenAI TurboQuant route.
- A verified source-to-OpenVINO model-preparation route.
- A verified Intel integrated-GPU route inside the application.
- Comparison of llama.cpp and OpenVINO using the same model and device.
- Additional GGUF weight-quantisation outputs.
- Export of benchmark results as CSV or JSON.
- A processing manifest saved beside each generated model file.

#### Packaging

- A packaged MSIX installer.
- Automatic checking for required runtime dependencies.
- Clear uninstall and local-data-cleanup instructions.

---

### 5.4 App-Integrated Experimental Capabilities

Some first-release features are required to run inside the application but
must still be labelled Experimental.

This is because their support is limited to exact tested models, runtime
versions, devices and configurations.

The following are app-integrated experimental capabilities:

- the selected TurboQuant-enabled llama.cpp route;
- OpenVINO GenAI TurboQuant if its application gate passes;
- the TurboVec knowledge-file workflow;
- Intel GPU routes that depend on exact Vulkan, SYCL, OpenVINO or driver
  versions;
- very-low-bit KV-cache configurations;
- combined weight and KV-cache optimisation configurations.

An experimental capability may be shown as usable only when:

1. the exact model and runtime version are known;
2. the supported configuration is recorded;
3. the requested setting is accepted;
4. the requested optimisation is proved to be active;
5. the actual backend and device are recorded;
6. successful output has been produced;
7. memory, speed, quality and stability have been measured;
8. fallback and failure behaviour are understood;
9. the interface labels it Experimental;
10. a dependable fallback remains available.

A failed experimental route is still a valid research result and does not
make the dependable core application unsuccessful.

---

### 5.5 Research-Only Comparison Routes

The following routes may be used as research evidence without becoming
separate user-facing application backends:

- additional TurboQuant forks;
- animehacker TQ3_0 where used only as a comparison implementation;
- very aggressive TurboQuant U3 or lower-bit settings that fail the quality
  threshold;
- unsupported GPU TurboQuant combinations;
- unsuccessful OpenVINO or model-conversion attempts;
- TurboVec variants that cannot be integrated reliably.

Their successful and failed results must still be retained and reported.

---

### 5.6 Deferred and Out-of-Scope Work

The following work is deliberately excluded from the first release:

- training an IBM Granite model from scratch;
- fine-tuning a large Granite model;
- support for every IBM Granite model;
- support for every GGUF, OpenVINO or Hugging Face model;
- support for arbitrary or untrusted model formats;
- support for every Intel CPU or GPU;
- Intel NPU claims without suitable test hardware;
- guaranteed support for a 32-billion-parameter model;
- guaranteed operation on every 4 GB, 8 GB or 16 GB computer;
- claims of six-times compression without project evidence;
- claims of near-zero quality loss without project evidence;
- claims of sub-second latency without project evidence;
- automatically re-quantising an already quantised GGUF through an unsafe
  route;
- presenting TurboQuant as a new exported model file;
- presenting TurboVec as directly compressing the original imported
  document;
- unrestricted downloading from unknown or untrusted model sources;
- automatic runtime or model updates;
- a full commercial model marketplace;
- a full enterprise RAG platform;
- a large external vector-database server;
- support for every document format;
- cloud inference as part of the core workflow;
- real patient information;
- clinical diagnosis or treatment recommendations;
- unsupervised pupil-facing deployment;
- replacement of teachers, healthcare workers or other professionals;
- a full replacement for llama.cpp, OpenVINO, LM Studio or Ollama;
- support for non-Granite models in the first release;
- multiple simultaneous model sessions;
- multi-user or network-server operation;
- DirectML integration;
- macOS or Metal delivery;
- full cross-platform delivery;
- a cross-platform CMake product backend;
- Microsoft Store publication;
- enterprise accounts, identity and role management;
- automatic collection of user prompts or documents for analytics;
- production approval for a school, university, hospital or NHS
  organisation.

## 6. Claims Not Made by This Project

This project will make only claims supported by its own evidence or by
clearly identified external sources. The following claims are not made.

### 6.1 Model, Format and Runtime Compatibility

- The project does not claim support for every IBM Granite model.
- The project does not claim support for every Granite model size,
  architecture or release.
- The project does not claim support for every GGUF, OpenVINO IR,
  Hugging Face or Safetensors model.
- The project does not claim that a model is compatible because its filename
  or file extension appears correct.
- The project does not claim that every llama.cpp version supports every
  selected Granite model.
- The project does not claim that every TurboQuant-enabled fork implements
  the complete method described in the TurboQuant research paper.
- The project does not claim that every OpenVINO Runtime or OpenVINO GenAI
  version supports every selected Granite model, precision or cache type.
- The project does not claim that TurboVec is compatible until the exact
  implementation, input, output and retrieval workflow have been verified.
- The first release is not intended to support arbitrary model or document
  formats.

### 6.2 Intel Hardware Compatibility

- The project does not claim compatibility with every Intel processor,
  integrated GPU, discrete GPU, NPU or Intel AI PC.
- A successful result on the main test laptop does not prove that the same
  result will occur on every Intel computer.
- The project does not claim that an Intel GPU or NPU was used merely because
  that device was requested in a configuration.
- Intel GPU use will be claimed only when runtime evidence shows that the
  model or relevant operations actually used the GPU.
- No tested NPU claim will be made without suitable NPU hardware.
- Differences in drivers, operating-system versions, power settings,
  available memory and thermal limits may change results on another computer.

### 6.3 Low-Memory Compatibility

- The project does not guarantee that every selected model will run on a
  computer with 4 GB, 8 GB or 16 GB of RAM.
- A process-memory measurement below 4 GB does not by itself prove that the
  configuration is suitable for a 4 GB Windows computer.
- Windows, background applications, the WinUI application, runtime buffers
  and shared GPU memory must also be considered.
- A memory estimate does not guarantee that a model will load, generate
  successfully or remain stable.
- The project does not guarantee that a 32-billion-parameter model will run
  on the target computer.
- Results based on a controlled memory limit, calculation or estimate will
  not be described as physical-device results.

### 6.4 Quantisation and Optimisation Claims

- The project does not claim a specific compression ratio unless that ratio
  is measured for the exact tested configuration.
- The project does not claim six-times compression, near-zero quality loss,
  sub-second latency or another headline result without direct project
  evidence.
- The project does not claim that using fewer bits always improves speed.
- The project does not claim that a smaller model file produces an equal
  percentage reduction in total runtime memory.
- The project does not claim that a configuration is useful solely because
  it produces text.
- Memory reduction must be considered together with output quality,
  inference speed, usable context and runtime stability.
- The project does not claim that quantisation preserves every model
  capability, task, language, formatting rule or safety behaviour.
- A result obtained from one prompt set will not be presented as proof of
  performance on every possible task.

### 6.5 TurboQuant Claims

- TurboQuant will be treated as a runtime vector and KV-cache quantisation
  method.
- TurboQuant does not by itself create a smaller GGUF model-weight file.
- A smaller GGUF file can be created only through a separate model conversion
  or weight-quantisation process.
- The project does not claim that every TurboQuant route works with every
  Granite model, backend, cache type or device.
- The project does not claim that a TurboQuant option is active merely
  because the runtime accepts its command-line argument.
- TurboQuant activation must be supported by runtime output, logs, cache
  information, measurements or other direct evidence.
- Results from the published TurboQuant paper will be treated as external
  research results rather than results produced by this project.
- Results from AtomicBot, animehacker, OpenVINO or another implementation
  will be labelled using the exact implementation that was tested.
- A quality result for one TurboQuant precision will not be applied to
  another precision without testing.
- The app-integrated TurboQuant feature will be labelled Experimental because
  its support is limited to exact verified combinations.
- Experimental does not mean that TurboQuant is excluded from the
  application. At least one verified TurboQuant route is required to run
  through the WinUI application.

### 6.6 TurboVec and Knowledge-File Claims

- The project does not claim that TurboVec directly compresses the original
  PDF, Word document or text file.
- TurboVec will be evaluated as a method that operates on vectors or
  embeddings created from imported knowledge files.
- The original imported knowledge file will remain unchanged.
- The project does not claim that compressed vectors preserve retrieval
  quality until they are compared with an uncompressed baseline.
- The project does not claim that the TurboVec workflow is a complete
  enterprise retrieval-augmented generation system.
- The project does not claim support for arbitrary file formats, external
  vector databases or large multi-user indexes.
- The exact TurboVec repository, version, licence and technical contract must
  be recorded before the feature is presented as working.

### 6.7 OpenVINO Claims

- The project does not guarantee that every official or experimental
  OpenVINO route will succeed.
- An official OpenVINO result will be kept separate from a nightly, custom or
  TurboQuant-enabled OpenVINO result.
- A community-converted OpenVINO model will not be described as an official
  IBM conversion.
- A CPU result will not be presented as a GPU result.
- A requested GPU route will not be treated as successful when the model
  silently falls back to CPU.
- OpenVINO support on the target laptop does not prove support on every
  Intel device.

### 6.8 Performance and Evaluation Claims

- A faster single run will not be treated as proof that one configuration is
  generally faster.
- Performance comparisons will use repeated runs where practical.
- Prompt-processing speed will be kept separate from generation speed.
- Cold model-loading time will be kept separate from warm loading time.
- Time to first token will use the same start and end points across matched
  tests.
- Memory values from different tools will not be compared without explaining
  what each value represents.
- Failed runs, crashes, out-of-memory events, fallbacks and invalid outputs
  will not be removed from the evidence.
- A negative result will not be changed into a positive claim because another
  paper or repository reports success.

### 6.9 Privacy, Security and Responsible-Use Claims

- Local inference does not automatically make an application private,
  secure, accurate, fair or legally compliant.
- Offline operation does not remove the need for access control, safe file
  handling, secure logs and responsible data retention.
- The application is not a medical device.
- The application will not provide medical diagnosis, treatment advice or
  clinical decisions.
- The application is not approved for operational NHS deployment.
- The application is not approved for unsupervised pupil-facing use.
- The application is not intended to replace teachers, healthcare workers or
  other qualified professionals.
- Generated outputs are not guaranteed to be correct, complete, current,
  unbiased or safe.
- Education and healthcare outputs require human review.
- The project will not use real patient information or identifiable pupil
  information.
- The project does not claim compliance with every education, healthcare,
  privacy or security regulation merely because inference is performed
  locally.

### 6.10 Product Boundaries

- The first release is not a full replacement for llama.cpp, OpenVINO,
  LM Studio, Ollama or another model-management product.
- The first release is not a complete model marketplace.
- The first release is not a full document-management or enterprise RAG
  platform.
- The project will not train or fine-tune a large IBM Granite model.
- The project does not provide full macOS, Metal, DirectML or cross-platform
  delivery.
- The project does not provide guaranteed Microsoft Store deployment.
- The project does not provide enterprise identity, user-account or
  multi-user server features.
- The core workflow will not depend on cloud inference, although internet
  access may be required to download approved models and dependencies.

### 6.11 Evidence Classification

Every important result will be labelled using one of these evidence types:

- **Measured:** recorded directly during a project experiment.
- **Estimated:** produced by the application or another calculation model.
- **Calculated from measurements:** derived from directly measured values.
- **Reproduced:** repeated from a documented external procedure.
- **Externally reported:** taken from a paper, model card or repository.
- **Inferred:** concluded from supporting evidence but not measured directly.
- **Not yet verified:** planned or claimed but not yet proved.

Measured results, estimates, calculations, external claims and assumptions
will not be presented as if they are the same type of evidence.

## 7. Definition of a Satisfactory Project Outcome

The project will be considered satisfactory when all mandatory core release
gates have passed, the required experimental capability has been demonstrated,
and every research question has been answered using traceable evidence.

A requirement is not complete merely because code exists. It must also have
the required verification evidence.

### 7.1 Project-Control Gate

The following project-control conditions must be met:

- the project problem, aim, research questions, objectives and first-release
  scope are version controlled;
- the approved MoSCoW requirements have stable requirement IDs;
- each Must Have requirement has acceptance criteria;
- a requirements traceability matrix links requirements to implementation,
  tests and evidence;
- architecture decisions affecting runtimes, process communication,
  optimisation and fallbacks are recorded;
- constraints, assumptions, risks and licences are maintained in a dated
  register;
- scope changes are recorded rather than silently added or removed;
- each research question is linked to a planned evidence source.

### 7.2 Build and Repository Gate

The following build conditions must pass:

- a clean checkout of the repository builds successfully for Windows x64;
- the WinUI 3 application opens on the target Windows Intel computer;
- all mandatory automated tests pass;
- generated build folders and large model files are not committed;
- the repository contains no known passwords, access tokens, API keys or
  personal test data;
- exact dependency and runtime versions are recorded;
- a basic distributable release build is produced;
- the release is connected to a Git commit and version tag;
- a separate backup of the final repository and evidence exists.

### 7.3 Model Import and Download Gate

The application must demonstrate that:

- at least one supported Granite GGUF model can be selected through the
  Windows file picker;
- at least one approved recommended Granite model can be downloaded through
  the application;
- the download shows progress and failure state;
- an incomplete download is not treated as valid;
- the completed download is checked using its expected size and SHA-256
  value or an equivalent cryptographic hash;
- the downloaded model enters the normal inspection workflow;
- the original imported or downloaded model remains unchanged.

### 7.4 Model-Inspection Gate

The final validation test set must include examples that produce:

- Ready;
- Ready with warnings;
- Conversion required;
- Unsupported;
- Invalid or incomplete.

For each case:

- the correct state must be shown;
- the reason must be understandable;
- the application must show a valid next action;
- unsupported or invalid inputs must not reach the inference runtime.

At least one real supported Granite model must have its useful metadata
displayed correctly.

### 7.5 Hardware-Inspection Gate

The application must correctly record and display:

- Windows and system architecture;
- Intel CPU information;
- installed physical RAM;
- currently available RAM;
- available Intel GPU information;
- relevant runtime/device information;
- available disk space.

The application output must be cross-checked against trusted Windows system
information.

A requested device must be kept separate from the device that actually ran
the model.

### 7.6 Memory-Estimator Gate

The estimator must:

- show model-weight memory;
- show KV-cache memory where relevant;
- include runtime and application overhead;
- include a Windows and background-process allowance;
- account for shared GPU memory where relevant;
- include a safety reserve;
- show the estimated total;
- show important assumptions and missing information;
- display a clear fit result.

Predicted and measured memory must be compared for matched configurations.

For the final estimator test set:

- absolute error must be reported;
- percentage error must be reported;
- no configuration known to exceed the safe memory budget may be shown as a
  normal safe recommendation;
- false-safe and false-unsafe results must be reported;
- confidence must be reduced when evidence is limited.

A useful target is for the final supported configuration set to remain within
20% of measured peak memory. When this target is not reached, the application
must use a larger safety margin and present the result as advisory rather than
certain.

### 7.7 Configuration-Selection Gate

The application must demonstrate that:

- only complete supported configurations are generated;
- invalid model, weight, cache, runtime, backend and device combinations are
  rejected;
- Automatic, Quality, Balanced and Efficiency modes use full configurations;
- unavailable modes are hidden or disabled;
- the recommendation includes an understandable reason;
- the full selected configuration is shown before inference;
- experimental configurations are clearly labelled;
- a standard dependable fallback remains available.

All final configuration-registry acceptance tests must pass.

### 7.8 Secure Runtime-Adapter Gate

The runtime adapter must demonstrate that it can:

- start a supported local executable without asking the user to enter a
  terminal command;
- handle model paths containing spaces and supported special characters;
- build process arguments safely;
- capture standard output and standard error;
- stream useful output to the WinUI application;
- detect loading and generation failures;
- support cancellation;
- stop child processes safely;
- clean temporary resources;
- prevent a stopped runtime from continuing in the background;
- convert technical failures into structured application errors.

Mandatory path, argument, cancellation and cleanup tests must pass.

### 7.9 Dependable llama.cpp Gate

At least one real selected Granite GGUF model must complete this workflow
through the application:

1. model import or approved download;
2. model validation;
3. model inspection;
4. hardware inspection;
5. memory estimation;
6. verified configuration selection;
7. launch of upstream llama.cpp;
8. successful model loading;
9. valid local generation;
10. recording of the actual backend and device;
11. safe completion or cancellation.

Intel CPU is the required minimum dependable route.

The user must not need to open or operate a terminal.

### 7.10 App-Integrated TurboQuant Gate

At least one selected IBM Granite configuration must run through a verified
TurboQuant-enabled runtime launched by the WinUI application.

The working route must:

- be selected inside the application;
- be labelled Experimental;
- use a pinned runtime version or commit;
- use a documented model and cache configuration;
- prove that the TurboQuant cache option was active;
- load the model successfully;
- generate valid local text;
- operate through the normal chat interface;
- support safe cancellation;
- record actual backend, device and cache information;
- preserve upstream llama.cpp as the standard fallback;
- produce a complete experiment manifest.

Accepting a command-line option is not enough to pass this gate.

The TurboQuant route must produce direct evidence that the intended
optimisation was active.

### 7.11 Local-Chat Gate

The local chat feature must:

- display the active model;
- display the active runtime;
- display the actual device;
- display the active configuration;
- accept a user prompt;
- show text while it is being generated;
- complete at least two user turns in one session;
- keep the application responsive;
- allow generation to be stopped;
- preserve the required conversation state;
- display understandable failures;
- allow generated text to be copied.

A complete chat test must be performed using both:

- the dependable standard llama.cpp route; and
- the verified app-integrated TurboQuant route.

### 7.12 Model-Weight Optimisation Gate

At least one supported model-weight quantisation workflow must run through the
application and create a new GGUF artefact.

The workflow must:

- preserve the original model;
- verify that the source is suitable;
- check available disk space;
- record the tool and version;
- record the selected quantisation;
- record the source hash;
- record the output hash;
- validate the output;
- inspect the output again;
- compare source and output size;
- return the validated output to the normal model-selection and chat
  workflow.

The new file must not be described as a TurboQuant export.

### 7.13 Official OpenVINO Research Gate

At least one official OpenVINO GenAI Granite baseline must be completed on
Intel hardware.

The result must record:

- exact model and revision;
- model format and precision;
- OpenVINO Runtime version;
- OpenVINO GenAI version;
- requested device;
- actual device;
- model-loading result;
- generation result;
- memory;
- loading time;
- time to first token;
- generation speed;
- quality result;
- failures and fallback.

This research gate may be satisfied by either:

- a valid repeatable OpenVINO inference result; or
- a repeatable blocker supported by complete diagnostic evidence.

A blocker can answer the OpenVINO research question, but it must not be
presented as a working application feature.

### 7.14 TurboQuant Comparison Gate

At least one app-integrated TurboQuant run must be compared with a matched
standard KV-cache baseline.

The comparison must keep the following equal as far as practical:

- Granite model and revision;
- weight format;
- prompt;
- chat template;
- context length;
- generation settings;
- hardware;
- device;
- measurement method.

The final comparison must include:

- runtime-reported KV-cache allocation;
- measured peak process memory;
- available system memory;
- model-loading time;
- time to first token;
- prompt-processing speed;
- generation speed;
- maximum stable context;
- fixed-prompt quality scores;
- instruction following;
- required output formatting;
- crashes, failures and fallback behaviour.

The project may conclude that TurboQuant is beneficial, neutral, unsuitable or
insufficiently verified. Any of these can be a valid research result when the
experiment is fair and reproducible.

The original TurboQuant paper reports results under its own tested models and
conditions; those published findings are external evidence and do not replace
the project comparison. :contentReference[oaicite:1]{index=1}

### 7.15 Lower-Memory Evaluation Gate

Selected complete configurations must be assessed against:

- 4 GB;
- 8 GB;
- 16 GB total system-memory budgets.

Every result must state whether it was:

- measured on a physical computer;
- measured under a controlled restriction;
- calculated from measured components;
- predicted by the estimator.

The evaluation must include:

- Windows memory allowance;
- application memory;
- model-weight memory;
- KV-cache memory;
- runtime overhead;
- shared GPU memory;
- safety reserve;
- output quality;
- speed;
- stable context;
- failure behaviour.

The project does not need to prove that every model runs within every budget.
It must identify which tested configurations are likely to be practical and
show the strength of the supporting evidence.

### 7.16 TurboVec Knowledge-Retrieval Gate

Before implementation, the project must identify:

- the exact TurboVec implementation;
- repository and commit or version;
- licence;
- supported platform;
- accepted input;
- produced output;
- embedding representation;
- retrieval method.

At least one selected knowledge file must then complete this bounded workflow:

1. file validation;
2. local text extraction;
3. division into searchable sections;
4. local embedding generation;
5. creation of an uncompressed-vector baseline;
6. TurboVec compression or optimisation of the vectors;
7. creation or use of a local index;
8. retrieval using a fixed question set;
9. use of retrieved evidence in the Granite chat workflow.

The compressed and uncompressed routes must be compared using:

- vector storage size;
- memory use;
- index creation time;
- query time;
- retrieval relevance;
- answer usefulness;
- failures and stability.

The original knowledge file must remain unchanged.

Because TurboVec is part of the agreed first-release scope, failure to identify
or integrate the exact implementation must trigger a recorded scope decision
and supervisor review. It must not be silently removed or falsely reported as
complete.

### 7.17 Quality and Performance Evaluation Gate

Every final selected configuration must report, where applicable:

- model file size;
- model precision;
- KV-cache format;
- tested context;
- peak memory;
- cold loading time;
- warm loading time;
- time to first token;
- prompt-processing speed;
- generation speed;
- total response time;
- run-to-run variation;
- output quality;
- instruction following;
- output formatting;
- stability.

The quality evaluation must use:

- a fixed version-controlled prompt set;
- a fixed scoring guide;
- retained raw answers;
- clearly defined pass, warning and fail conditions.

The prompt set and scoring guide must be frozen before the final comparison.

### 7.18 Reliability, Privacy and Security Gate

The final system must demonstrate that:

- the core inference workflow functions without cloud inference;
- models, prompts and generated answers are not sent to a cloud AI service;
- downloaded models come from an approved source and pass integrity checks;
- unsafe process-argument construction is not used;
- path and file-validation tests pass;
- temporary files and child processes are cleaned up;
- no real patient or identifiable pupil data are used;
- secrets are absent from the repository;
- sensitive prompt logging is disabled or clearly controlled;
- experimental options are clearly labelled;
- the application explains that generated content requires human review.

### 7.19 UX and Accessibility Gate

A documented usability evaluation must be completed with at least three
representative non-specialist participants where access permits.

The evaluation must include tasks covering:

- importing or downloading a model;
- understanding an inspection result;
- understanding a memory-fit result;
- selecting a configuration;
- starting local chat;
- recognising an Experimental TurboQuant option;
- recovering from at least one failure.

The project must record:

- task completion;
- errors;
- assistance required;
- confusing terms;
- participant feedback;
- changes made after evaluation;
- unresolved UX limitations.

The main workflow must also be checked using:

- keyboard-only operation;
- Windows text scaling up to at least 200%;
- visible focus;
- readable progress and error messages.

When direct access to education or healthcare workers is unavailable,
representative non-specialist participants may be used, but this limitation
must be stated.

### 7.20 Reproducibility and Documentation Gate

The repository must contain:

- project README;
- user manual;
- developer manual;
- build and installation instructions;
- known limitations;
- dependency and licence register;
- architecture diagrams;
- architecture decision records;
- test strategy and results;
- experiment protocols;
- raw and processed evidence;
- final requirements traceability;
- final feature-status table.

Each formal experiment must contain:

- unique experiment ID;
- date and operator;
- hardware information;
- model name and hash;
- runtime version and hash;
- exact configuration;
- requested backend and device;
- actual backend and device;
- raw output;
- raw errors;
- raw measurements;
- failed-run evidence;
- analysis result.

Another developer must be able to follow the written instructions and
reproduce:

- the clean x64 build;
- the main upstream llama.cpp route;
- at least one formal comparison.

### 7.21 Report and Research-Question Gate

The final report must:

- explain the problem and motivation;
- state the aim, research questions, objectives and scope;
- distinguish original project work from third-party work;
- explain requirements and architecture;
- explain implementation;
- explain software verification;
- present the formal experiments;
- report both positive and negative findings;
- discuss threats to validity;
- answer every research question;
- explain limitations;
- compare the final result with the satisfactory-outcome conditions.

Each Must Have requirement must end with one of these statuses:

- Verified;
- Partially verified;
- Incomplete;
- Deferred through approved change;
- Removed through approved change.

Every status must link to evidence or an explanation.

### 7.22 Overall Pass Rule

The overall project is satisfactory when:

- all core product gates pass;
- upstream llama.cpp works end to end through the application;
- at least one TurboQuant route works end to end through the application;
- the TurboQuant route is compared fairly with a standard baseline;
- all research questions have evidence-based answers;
- unresolved work and negative results are reported honestly;
- the repository and report provide sufficient evidence for another
  developer to understand and reproduce the core result.

Not every experimental route has to succeed.

However, a planned feature cannot be counted as complete merely because it
was researched or described in documentation.

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
