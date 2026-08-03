# Workbook 05 two-route memory-frontier test campaign

**Date:** 3 August 2026  
**Status:** Concept approved; written specification awaiting user review  
**Approved direction:** Option C — keep merged OpenVINO testing and experimental QJL/PolarQuant testing as two separately labelled routes  
**Target machine:** `LENOVO-PF4HMD0T`, Intel Core i5-12450H, 15.7 GiB usable RAM, Windows x64  
**Automation host:** GitHub Actions self-hosted runner `lenovo-pf4hmd0t-wb05`

## 1. Purpose

This specification redesigns Workbook 05 so that the campaign:

1. starts with the lowest-memory executable KV-cache formats;
2. increases memory pressure and context length in controlled steps;
3. stops safely when the target laptop reaches a verified limit;
4. follows the exact build and run procedure belonging to each pinned source repository;
5. tests merged OpenVINO TurboQuant separately from an experimental QJL/PolarQuant extension;
6. records all performance, memory, activation, fallback, quality, perplexity, stability and failure evidence;
7. updates a dedicated results branch and pull request in the actual project repository;
8. produces bounded conclusions rather than claiming unsupported functionality.

The campaign is designed to run unattended after a trusted manual GitHub Actions dispatch. Formal benchmark stages still require the Intel laptop to remain idle.

## 2. Why Workbook 05 must be revised

The current Workbook 05 revision assumes six complete selectable experimental codecs:

- TBQ4;
- TBQ3;
- TBQ4 plus QJL;
- TBQ3 plus QJL;
- PolarQuant 4-bit;
- PolarQuant 3-bit.

The merged OpenVINO TurboQuant implementation does not justify treating all six as merged OpenVINO features. The merged OpenVINO route exposes the TurboQuant algorithm separately from cache precision, while QJL and PolarQuant require their own executable-source proof.

The current workbook also lists tests primarily by historical test ID rather than by memory pressure. That ordering is useful for traceability but is not the safest or fastest order for discovering the limit of a 16 GB laptop.

The revision will therefore preserve all existing IDs while adding a separate execution order based on verified storage and safety.

## 3. Non-negotiable evidence rule

A codec or configuration is not considered supported merely because any of the following exists:

- an enum;
- a property name;
- a constant;
- a codebook;
- a comment;
- a design document;
- a workbook row;
- a source file that is never reached;
- a process that completes after silently falling back.

A successful codec result requires all of the following:

1. exact source repository, branch and commit are pinned;
2. the source builds using its documented procedure;
3. the runtime accepts the requested configuration;
4. dispatch evidence proves the requested implementation was selected;
5. measured packed storage agrees with the pinned implementation or the discrepancy is explained;
6. the inference request completes without unexplained fallback;
7. output correctness and integrity checks pass;
8. raw logs, output, metrics and hashes are preserved.

When documentation and executable behaviour disagree, executable behaviour controls the result and the contradiction is recorded.

## 4. Source-of-truth hierarchy

The campaign uses this order of authority:

1. **Pinned source repository at the recorded commit** — README, build documentation, source and tests.
2. **Observed executable behaviour** — build logs, runtime properties, dispatch logs, allocation logs and outputs.
3. **Versioned campaign specification and controlled workbook.**
4. **Curated project research notes.**
5. **Historical or legacy results.**

Lower-priority material may explain a result but cannot override higher-priority evidence.

## 5. Route separation

### 5.1 Route A — merged OpenVINO TurboQuant

Route A represents functionality contained in merged OpenVINO source.

The initial provenance point is OpenVINO PR `#35853`, merged as commit:

```text
b9a1f201c109e0bed74763934f79483cf6c4cbf4
```

The preflight stage may test a later OpenVINO commit only through a recorded compatibility attempt. A later commit becomes the formal pin only after it passes:

- the documented Windows build;
- a standard-cache baseline;
- property visibility checks;
- TurboQuant activation checks;
- a compatible OpenVINO GenAI baseline.

Route A capability discovery must inspect the pinned source instead of hard-coding assumed formats. The expected primary candidates are:

- scalar U4;
- scalar U8;
- TurboQuant U3;
- TurboQuant U4;
- BF16 or F16 control;
- F32 control where safe.

A lower precision such as U2 is included only when the pinned runtime exposes it through a usable property and activation proof passes. Codebook constants alone are insufficient.

### 5.2 Route B — experimental QJL and PolarQuant extension

Route B is a separately labelled experimental route. It may contain:

- TBQ3 plus QJL;
- TBQ4 plus QJL;
- PolarQuant 3-bit;
- PolarQuant 4-bit.

Route B has a hard source-admission gate. Before any model benchmark, the preflight must identify and record an exact repository, branch and commit that provides:

- compilable implementation code;
- a documented build procedure;
- a selectable runtime property or an explicitly runnable test interface;
- encode and decode paths;
- packed record sizing;
- activation or dispatch evidence;
- no silent substitution with ordinary TurboQuant or scalar cache.

If no source satisfies this gate, Route B is recorded as:

```text
Blocked — no pinned executable and selectable QJL/PolarQuant implementation identified.
```

That blocked result is a valid and honest campaign conclusion. Creating a new implementation would then become a separate engineering work package and would not be hidden inside the benchmark workflow.

### 5.3 Cross-route claims

Results must always identify their route. The report must not describe Route B functionality as official or merged OpenVINO support.

Cross-route comparison is permitted only after both routes have:

- pinned source identities;
- compatible model and prompt identities;
- matched contexts;
- matched measurement definitions;
- verified activation;
- valid quality evidence.

## 6. Exact README/build-procedure fidelity

Each route must follow the instructions belonging to its pinned repository revision.

For Route A, the controlling sequence starts from the OpenVINO Windows build documentation and the OpenVINO GenAI source-build documentation. The automation will:

1. clone the exact pinned repositories;
2. initialise recursive submodules exactly as documented;
3. save the pinned README and build documents into the evidence bundle;
4. generate a command manifest containing every documented command in execution order;
5. run each command separately with start time, end time, working directory, exit code, stdout and stderr;
6. use the documented Visual Studio generator and Release configuration unless a documented compatibility attempt proves a change is required;
7. build OpenVINO Runtime and OpenVINO GenAI in one compatible source environment;
8. record all runtime and TBB environment paths;
9. hash the resulting binaries and packages.

The automation must not silently “fix” README commands. Every necessary difference creates a deviation record containing:

- deviation ID;
- original documented command;
- executed command;
- reason;
- source evidence;
- effect on reproducibility;
- approval status;
- retest result.

The same rule applies to Route B after its source is admitted.

## 7. Campaign architecture

The automation is divided into three trust zones.

### 7.1 Self-hosted execution workflow

The Intel runner performs source builds and tests. It receives read-only repository permissions and uploads evidence artifacts.

It does not receive direct permission to push to `main` or to a results branch.

### 7.2 Evidence-validation workflow

A trusted GitHub-hosted workflow downloads each completed stage artifact and checks:

- required files exist;
- JSON and CSV schemas are valid;
- file hashes match the hash manifest;
- every metric row references a run manifest;
- every run manifest references raw evidence;
- no model weights, tokens or secrets are present;
- requested and verified codec fields are populated;
- failed runs include a failure record.

Invalid evidence is retained as an Actions artifact but is not committed as formal campaign evidence.

### 7.3 Repository-ingestion workflow

After validation, a trusted workflow commits one batch per completed stage to a dedicated branch such as:

```text
results/workbook-05-<campaign-id>
```

It creates or updates one draft pull request containing:

- stage status;
- passed, failed, blocked and skipped counts;
- pinned source and model identities;
- important metrics;
- the latest verified frontier;
- failures and limitations;
- evidence paths;
- remaining work.

No workflow pushes directly to `main`.

## 8. Controlled campaign phases

### Phase 0 — repository and machine preflight

Confirm:

- expected runner, computer, CPU, RAM and architecture;
- runner service account;
- AC power and no-sleep state;
- sufficient free disk space;
- sufficient available RAM;
- Python, Git, CMake, MSBuild, MSVC and Windows SDK identities;
- repository branch and commit;
- no competing Workbook 05 job;
- evidence directory is writable;
- previous checkpoint can be read.

### Phase 1 — source admission and source locking

For each route:

- record source URL, branch, head commit, base commit and PR state;
- snapshot README/build documentation;
- inspect exposed cache properties and codec files;
- identify the exact Runtime/GenAI compatibility candidates;
- create `source-lock.json`;
- create `documented-command-manifest.json`;
- record licence and claim boundary.

Route B cannot proceed without passing its source-admission gate.

### Phase 2 — documented build

Run the source repository’s documented build procedure and preserve:

- configure command;
- build command;
- environment variables;
- compiler and SDK versions;
- dependency downloads;
- warnings and errors;
- binary locations;
- binary hashes;
- elapsed build time;
- maximum build memory;
- final build status.

### Phase 3 — algorithm and activation conformance

Before Granite inference, test each executable codec with deterministic fixtures:

- round trip;
- packing and unpacking;
- expected versus actual bytes;
- zero and near-zero vectors;
- large finite values;
- NaN/Inf handling;
- deterministic seed, rotation, projection and codebook behaviour;
- independent K and V dispatch;
- dead-code and silent-fallback detection;
- source repository codec tests.

A codec failure blocks only dependent configurations. It does not erase evidence from other codecs.

### Phase 4 — diagnostic K/V capability sweep

Run the short diagnostic model across every admitted ordered K/V combination.

The sweep order is generated from measured bytes:

```text
ascending(expected K bytes + expected V bytes)
```

Ties are resolved by:

1. symmetric pair first;
2. lower K bytes;
3. lower V bytes;
4. stable codec name order.

The sweep proves dispatch and completion, not performance superiority.

### Phase 5 — Granite 3B feasibility frontier

For each symmetric admitted configuration, sort configurations by verified bytes per token, lowest first.

For each configuration, test the context ladder:

```text
512 → 1,024 → 2,048 → 4,096 → 8,192 → 16,384 → continue doubling
```

The ladder stops at the earliest of:

- model-declared context limit;
- runtime-declared context limit;
- campaign maximum configured for that run;
- repeated safety or correctness failure.

The discovery run at each point uses one deterministic prompt and complete resource logging. It is not a formal performance result.

### Phase 6 — matched formal Granite 3B evaluation

For each important admitted configuration, select:

- one shared lower reference context supported by all compared configurations;
- the last stable context for that configuration;
- any context immediately below an observed failure boundary.

Each frozen formal configuration receives:

1. one pilot run, excluded;
2. one defined warm-up run, excluded;
3. at least three measured repetitions;
4. P1–P6 quality evaluation;
5. perplexity where the pinned route provides a validated method;
6. complete performance, memory, activation and stability evidence.

### Phase 7 — asymmetric and cross-family evaluation

After symmetric formats are stable, run:

- key-only low-bit configurations;
- value-only low-bit configurations;
- scalar/Turbo asymmetric pairs;
- QJL/Polar asymmetric pairs on Route B;
- selected cross-family pairs required by Workbook 05.

Execution remains ordered by measured total K/V storage.

### Phase 8 — Granite 8B safety-gated frontier

Granite 8B begins only after Granite 3B identifies safe candidates.

The 8B route starts from the lowest-memory quality-valid candidate and restarts the context ladder at 512 tokens. It does not assume that a 3B context is safe for 8B.

Only candidates relevant to the final decision are promoted to full 8B formal evaluation.

### Phase 9 — ablations and repeatability

Run applicable controls such as:

- norm correction OFF/ON;
- fused quantisation OFF/ON;
- any documented QJL correction switch;
- any documented PolarQuant codebook or tree variant;
- repeated restart and corruption checks.

An ablation is run only when the pinned source actually exposes the switch.

### Phase 10 — comparison and conclusion

Generate separate conclusions for:

- best memory reduction;
- best quality preservation;
- best decode speed;
- best prompt-processing speed;
- lowest TTFT;
- maximum stable context;
- most stable configuration;
- Granite 8B feasibility;
- merged OpenVINO support boundary;
- experimental QJL/PolarQuant support boundary;
- integration and maintenance risk.

The campaign must not force all objectives into one unsupported “winner.” It will also identify Pareto-nondominated configurations across memory, speed and quality.

## 9. Memory-frontier algorithm

### 9.1 Configuration ordering

A configuration’s execution rank is derived from verified storage, not its marketing name.

Required ranking fields:

- K record bytes;
- V record bytes;
- K metadata bytes;
- V metadata bytes;
- bytes per token across all layers and KV heads;
- expected full-context KV allocation;
- measured full-context KV allocation.

If measured and expected storage disagree beyond the documented tolerance, the configuration is removed from formal comparison until explained.

### 9.2 Safe stopping rule

A context point is `stable` only when:

- the process exits normally;
- requested codecs are verified;
- no unexplained fallback occurs;
- output completes without corruption;
- the memory watchdog does not trigger;
- the run does not exceed its timeout;
- the system returns to a healthy post-run state.

A failed context is retried once with the same frozen configuration after cleanup and cooldown.

If the same class of failure repeats, the previous stable context becomes the provisional frontier and higher contexts for that configuration are skipped.

### 9.3 Safety watchdog

The execution process is supervised externally. The watchdog records samples and terminates the child process when any hard boundary is sustained:

- available physical RAM below 1.5 GiB for 10 seconds;
- Windows commit usage above 90% of the commit limit for 10 seconds;
- child process and descendants stop producing a heartbeat for the configured timeout;
- process-tree private bytes exceed the configuration’s precomputed safety budget;
- the runner service becomes unhealthy.

A watchdog termination is recorded as a safety result, not a normal benchmark failure.

The workflow never disables Windows memory protection, alters the page file, overclocks the machine or changes firmware settings.

## 10. Frozen formal controls

Formal comparisons hold constant:

- model repository and revision;
- model weight precision;
- tokenizer files and hashes;
- Runtime and GenAI commits;
- build type, compiler, CMake generator and SDK;
- CPU device and stream/thread settings;
- prompt set and prompt text;
- sampling parameters;
- seed;
- maximum output tokens;
- context target;
- power source and power plan;
- runner account;
- measurement scripts;
- metric schema version.

Any changed control creates a new configuration ID and cannot be averaged with the old configuration.

## 11. Performance and resource metrics

Each measured repetition records:

- model load time in milliseconds;
- TTFT in milliseconds;
- prompt-processing tokens per second;
- TPOT in milliseconds per token;
- decode tokens per second;
- total generation time;
- actual input and output token counts;
- peak process-tree working set;
- peak process-tree private bytes;
- available RAM before the run;
- minimum available RAM during the run;
- available RAM after the run;
- runtime-reported K-cache allocation;
- runtime-reported V-cache allocation;
- total KV-cache allocation;
- CPU mean and peak utilisation;
- actual backend and device;
- actual model and KV placement;
- exit code and completion state.

Formal summaries report median and range across valid repetitions. Pilot and warm-up runs remain visible but are excluded from formal statistics.

## 12. Quality and perplexity design

### 12.1 Frozen assets

All formal quality runs use:

```text
Prompt set: GTQ-PROMPTS-v1
Rubric: GTQ-QUALITY-RUBRIC-v1
Scale: 0–10
```

P1–P6 remain individually visible:

- P1 constrained explanation;
- P2 exact instruction and formatting;
- P3 exact JSON structure;
- P4 summarisation and fact retention;
- P5 long-context exact retrieval;
- P6 multi-turn memory and stability.

### 12.2 Evaluation order

For every output:

1. run deterministic checks;
2. apply critical caps;
3. score rubric dimensions with configuration labels hidden;
4. preserve criterion-level evidence;
5. calculate the weighted 0–10 score;
6. compare against the matched standard-cache baseline;
7. flag cases requiring manual adjudication.

The scoring system must not allow a subjective evaluator to override:

- invalid JSON;
- wrong exact marker;
- wrong remembered value;
- missing mandatory fact;
- wrong required format;
- empty output;
- truncation or corruption.

### 12.3 Automated and final scores

The unattended workflow produces:

- deterministic result;
- provisional weighted score;
- evaluator identity and version;
- confidence or disagreement fields;
- manual-review requirement.

A final quality conclusion is withheld for any prompt flagged by the controlling rubric for manual adjudication. Performance evidence remains valid even when quality adjudication is pending.

### 12.4 Comparative interpretation

The report shows:

- per-prompt score;
- per-dimension score;
- overall weighted score;
- delta from the matched baseline;
- critical-cap reason;
- perplexity and delta where available;
- corruption/truncation count;
- repetition stability.

Perplexity is supporting evidence, not a replacement for task-level quality.

## 13. Evidence structure

The existing evidence root remains:

```text
experiments/granite_turboquant_intel/
```

Workbook 05 additions follow existing route/run conventions and include:

```text
manifests/campaigns/<campaign-id>/
manifests/runs/custom-openvino/<test-id>/<run-id>/
logs/custom-openvino/<test-id>/<run-id>/
outputs/custom-openvino/<test-id>/<run-id>/
metrics/custom-openvino/<test-id>/<run-id>/
results/custom-openvino/<test-id>/<run-id>/
notes/custom-openvino/<test-id>/<run-id>/
```

Each run directory contains or references:

- run manifest;
- exact command;
- environment snapshot;
- stdout;
- stderr;
- raw response;
- resource samples;
- activation proof;
- allocation proof;
- processed metrics;
- quality result;
- failure record when applicable;
- SHA-256 manifest.

Large models, source trees and build outputs are not committed. Their identities, paths, licences and hashes are committed.

## 14. Checkpoint and resume behaviour

Every phase writes a durable checkpoint after evidence validation.

A resumed campaign:

- verifies the campaign ID and source locks;
- verifies prior artifact hashes;
- skips completed immutable phases;
- resumes at the first incomplete configuration;
- never converts an earlier failure into a pass without a new run ID;
- preserves every attempt.

The workflow’s concurrency group permits only one active Workbook 05 execution on the Intel runner.

## 15. Failure handling

Every failure receives:

- failure ID;
- test ID;
- run ID;
- route;
- codec/configuration;
- context;
- failure code;
- first failing command or observable symptom;
- root-cause status;
- retry decision;
- next action;
- evidence paths.

Failure codes include the existing Workbook 05 categories and may be extended through controlled revision. A downstream summary must distinguish:

- `Failed` — attempted and did not satisfy the gate;
- `Blocked` — prerequisite absent or invalid;
- `Skipped by frontier` — higher context not attempted after a repeated boundary;
- `Not applicable` — feature not exposed by the pinned route;
- `Passed` — all required evidence gates satisfied.

## 16. Workbook and traceability revision

The current controlled workbook is preserved unchanged.

A new controlled revision will:

- retain all existing test IDs;
- add route classification;
- add execution phase;
- add memory rank;
- add expected and verified bytes;
- add frontier status;
- add skip reason;
- add run and evidence references;
- correct QJL/Polar record sizes only after source conformance evidence;
- preserve prior revision hashes;
- add an append-only revision-register entry;
- add a testing decision-log entry approving the two-route execution order.

Changing execution order does not change the semantic meaning of existing test IDs.

## 17. Security boundaries

- The self-hosted execution workflow uses `contents: read`.
- Repository writes occur only from the trusted ingestion workflow.
- Third-party Actions are pinned to immutable commit SHAs.
- No personal access token is stored on the Intel laptop.
- Registration tokens, model access tokens and other secrets never enter evidence files.
- Pull-request workflows from forks cannot target the self-hosted runner.
- User-supplied workflow inputs are allowlisted.
- Model paths and commands are validated before execution.
- The runner cannot push directly to `main`.

## 18. Acceptance criteria for the design’s implementation

The implementation is accepted only when all of the following are demonstrated:

1. Route A and Route B cannot be confused in manifests or conclusions.
2. Route B cannot run without a pinned executable-source admission record.
3. Exact source documentation is snapshotted and every build command is logged.
4. Configurations are ordered by verified memory demand.
5. Context increases automatically until a safe, repeatable boundary is found.
6. A repeated boundary skips unsafe higher contexts without losing evidence.
7. Every requested codec has activation and fallback evidence.
8. Every measured repetition has a complete run manifest.
9. Every formal performance result has raw resource samples.
10. Every formal quality result references the frozen prompt set and rubric.
11. P1–P6 and criterion scores remain visible.
12. The workflow preserves pilot, warm-up and measured-run roles.
13. Results are committed to a dedicated branch by a trusted ingestion workflow.
14. A draft pull request summarizes progress and limitations.
15. No unsupported QJL, PolarQuant or merged-OpenVINO claim is produced.
16. The final report identifies separate memory, quality, speed, context and stability leaders.

## 19. Explicit non-goals for the first implementation plan

The first implementation plan will not yet:

- implement QJL or PolarQuant from scratch;
- modify OpenVINO algorithms;
- download multi-gigabyte models without a model-source and storage plan;
- run full Granite benchmarks before source/build preflight passes;
- auto-merge evidence into `main`;
- rewrite historical workbook revisions;
- invent values for unmeasured fields.

## 20. First implementation boundary after approval

After this specification is approved, the first implementation plan will cover only:

1. controlled Workbook 05 revision scaffolding;
2. Route A and Route B source-admission manifests;
3. exact-README command capture;
4. preflight workflow;
5. evidence schemas and validation;
6. checkpoint/resume skeleton;
7. no OpenVINO build and no model inference yet.

A separate reviewed plan will control the build stage after preflight evidence identifies the exact pinned Runtime/GenAI pair and confirms whether an executable Route B source exists.

## 21. Engineering rationale

This design uses staged test planning, traceability and operationally realistic evidence rather than treating workbook rows as proof. It separates merged and experimental capabilities, makes assumptions visible, and discovers the laptop’s capacity incrementally.

It also separates the high-privilege act of committing evidence from the untrusted execution surface of a self-hosted runner. Performance and AI quality are treated as different dimensions, with frozen prompts, deterministic checks, repeated measurements and transparent trade-offs.
