# Workbook Data Requirements and Evidence Map

**Version:** 1.0  
**Campaign:** Granite-TurboQuant Controlled Retest Campaign v1  
**Status:** Controlling data-capture specification

This file defines the information that must be collected so every cell in the six controlled workbooks can be completed without guessing or reconstructing results later.

## 1. Shared information required by every route

### Repository and runtime

- repository URL, branch/tag and exact commit;
- upstream base commit where relevant;
- clean/dirty working-tree state;
- build type, compiler, CMake, Python and package versions;
- build options and environment variables;
- runtime/binary version and executable SHA-256;
- supported backends, cache formats and device flags;
- repository-provided test results, warnings and known limitations;
- licence, provenance and integration implications.

### Target machine

- environment and machine ID;
- Windows edition, version and build;
- CPU model, physical cores and logical processors;
- installed and available RAM;
- GPU model, driver and shared/dedicated memory;
- NPU presence;
- free storage, power mode and thermal state;
- relevant background processes;
- Visual Studio, oneAPI, Vulkan and OpenVINO versions where applicable.

### Model manifest

- exact model ID, family, release and variant;
- source, revision/snapshot and licence;
- architecture, parameter count and advertised context;
- source and converted formats;
- weight precision;
- tokenizer source/revision and chat template;
- file/folder name, byte size and SHA-256;
- intended test use;
- conversion command, tool version and output hashes where applicable.

### Per-run identity and configuration

- test ID, run ID, route and repetition role;
- start/end timestamps in UTC and local time;
- environment, repository, build, model, configuration, prompt-set and rubric IDs;
- exact command, working directory and environment variables;
- seed, sampling settings and maximum output tokens;
- requested and actual backend/device;
- offload level, layer placement and KV-cache placement;
- K-cache and V-cache types;
- TurboQuant, TQ3_0 or TBQ state and activation proof;
- context target, actual input tokens and prompt ID;
- exit code and result classification.

### Correctness and device proof

- model-load success, generation success and output completion;
- compiled/execution device properties;
- actual model-layer and KV-cache placement;
- CPU fallback or hybrid behaviour;
- silent-fallback check;
- CPU mean/peak utilisation;
- GPU engine mean/peak utilisation;
- profiler and device-log evidence.

### Memory and performance

- model-load time;
- time to first token (TTFT);
- prompt-processing throughput;
- time per output token (TPOT);
- decode tokens per second;
- total generation time;
- input/output token counts;
- peak working set and private bytes;
- available RAM before, minimum during and after;
- KV-cache allocation where exposed;
- shared/dedicated GPU memory where measurable;
- output/model/conversion size;
- unload duration and cleanup result;
- repetition count, warm-up policy, median, variability and outliers.

### Quality and stability

- complete raw response;
- deterministic validation result;
- score out of 10;
- criterion-level scores and reasons;
- format/schema validity;
- required facts retained;
- unsupported statements;
- repetition, corruption or truncation;
- exact/normalised retrieval result;
- multi-turn memory result;
- bounded perplexity result where supported;
- repeated-output stability;
- crash/OOM state.

### Evidence references

- command file, stdout and stderr;
- raw response and resource samples;
- device/activation proof;
- processed summary;
- evidence hash manifest;
- evidence directory and Git commit;
- workbook update date and reviewer.

## 2. Workbook-specific completion map

### WB-01 — Upstream llama.cpp

Capture repository/build evidence for `UL-B01` to `UL-B07`, formal results for `UL-01` to `UL-13`, the standard configuration ladder, CPU/Vulkan/SYCL placement, matched performance data, P1-P6 quality results, failures and the final dependable baseline decision.

### WB-02 — AtomicBot TurboQuant

In addition to shared fields, capture:

- every exposed TurboQuant cache format;
- activation proof for turbo4, turbo3 and turbo2;
- source-level implementation classification;
- active/inactive QJL status;
- matched F16/Q8_0 and TurboQuant KV allocation;
- bounded perplexity evidence;
- partial/full Vulkan placement;
- separate 3B and 8B safety gates.

### WB-03 — animehacker TQ3_0

In addition to shared fields, capture:

- TQ3_0 flags and activation proof;
- implementation depth and QJL status;
- Windows/SYCL/Level Zero device inventory;
- host versus SYCL memory lines;
- whether KV cache remains host-side during partial offload;
- missing values as `Not measured`, never inferred.

### WB-04 — Official OpenVINO

In addition to shared fields, capture:

- OpenVINO Runtime, GenAI, tokenizers and conversion-tool versions;
- CPU/GPU plugins and compiled execution devices;
- source-model and converted-IR provenance;
- conversion command, tokenizer/generation-config validation and output hashes;
- PerfMetrics fields;
- Model0 device separately from tokenizer/detokenizer auxiliaries;
- context-scaling trials;
- process-tree memory rather than launcher-only memory.

### WB-05 — Custom OpenVINO TurboQuant

In addition to shared fields, capture:

- source commit containing TBQ support;
- compatible Runtime/GenAI pairing;
- CPU SDPA proof;
- scalar and Turbo U8/U4/U3 or TBQ4/TBQ3 configuration;
- algorithm/precision properties from runtime logs;
- standard baseline before TurboQuant;
- direct matched precision/context comparisons;
- no GPU TurboQuant claim unless independently proved.

### WB-06 — Cross-route comparison

Fill this only from validated route workbooks and processed results. For every comparison record:

- source test ID and run ID;
- equivalence status: `Matched`, `Partially matched` or `Not directly comparable`;
- model, weights, cache precision, context, prompt set and device;
- median metrics and repetition count;
- quality score and rubric version;
- stability and failure state;
- evidence path and Git commit;
- bounded conclusion.

## 3. Quality-scale control

The active campaign uses a **0-10 scale**. Original workbooks containing `/5` remain unchanged as source material; active controlled workbooks use `/10`.

Weighted rubric:

- correctness and grounding: 30%;
- instruction and format adherence: 25%;
- completeness and fact retention: 20%;
- relevance, clarity and coherence: 15%;
- stability and output integrity: 10%.

Deterministic checks take priority over subjective scoring. Equal averages must not hide different task-level failures.

## 4. Formal performance rule

Unless a documented safety gate prevents it:

1. run a pilot;
2. run one warm-up excluded from formal statistics;
3. run at least three measured repetitions;
4. preserve every valid measured run;
5. report median and variability;
6. investigate outliers;
7. separate cold-load from warm-generation measurements.

## 5. Missing-data rule

Use one of these exact values instead of a blank or guess:

- `Not run`;
- `Not measured`;
- `Not supported`;
- `Blocked`;
- `Not applicable`;
- `Inconclusive`;
- `Pending validation`.

Every missing value must have a reason and next action in the failure or workbook-completion register.
