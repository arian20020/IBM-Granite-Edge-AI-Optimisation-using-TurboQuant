# Workbook 05 two-route memory-frontier test campaign

**Date:** 3 August 2026  
**Status:** Concept approved; written specification awaiting user review  
**Approved direction:** Option C — merged OpenVINO and experimental QJL/PolarQuant remain separately labelled routes  
**Target machine:** `LENOVO-PF4HMD0T`, Intel Core i5-12450H, 15.7 GiB usable RAM, Windows x64  
**Automation host:** GitHub Actions self-hosted runner `lenovo-pf4hmd0t-wb05`

## 1. Purpose

Workbook 05 will be revised so that testing begins with the lowest-memory executable KV-cache configuration and increases memory pressure until the laptop reaches a safe, repeatable boundary.

The campaign will:

1. preserve existing test IDs for traceability;
2. add a separate execution order based on verified storage demand;
3. follow the exact build and run procedure belonging to each pinned source repository;
4. test merged OpenVINO TurboQuant separately from experimental QJL and PolarQuant;
5. log performance, memory, activation, fallback, quality, perplexity, stability and failures;
6. run unattended after a trusted manual dispatch;
7. update a dedicated results branch and draft pull request in the actual repository;
8. produce bounded conclusions rather than unsupported claims.

Formal benchmark stages require the Intel laptop to remain idle even though they are automated.

## 2. Evidence rule

A codec is not supported merely because an enum, constant, codebook, property, comment, source file or workbook row exists.

A successful codec result requires all of the following:

1. exact repository, branch and commit are pinned;
2. the source builds using its documented procedure;
3. the runtime accepts the requested configuration;
4. dispatch evidence proves the requested implementation was selected;
5. packed storage is measured and reconciled with the pinned implementation;
6. no unexplained fallback occurs;
7. output correctness and integrity checks pass;
8. raw logs, output, metrics and hashes are preserved.

When documentation and executable behaviour disagree, executable behaviour controls the result and the contradiction is logged.

## 3. Source-of-truth hierarchy

The campaign uses this order of authority:

1. pinned source repository at the recorded commit;
2. observed executable behaviour;
3. versioned campaign specification and controlled workbook;
4. curated project research;
5. historical results.

Lower-priority material may explain a result but cannot override higher-priority evidence.

## 4. Route separation

### 4.1 Route A — merged OpenVINO TurboQuant

Route A represents merged OpenVINO functionality.

The first provenance candidate is OpenVINO PR `#35853`, merged as:

```text
b9a1f201c109e0bed74763934f79483cf6c4cbf4
```

A later commit may replace this candidate only through a recorded compatibility attempt that passes:

- the documented Windows build;
- a standard-cache baseline;
- property-visibility checks;
- TurboQuant activation checks;
- a compatible OpenVINO GenAI baseline.

Capability discovery must inspect the pinned source rather than hard-code assumptions. Expected candidates are:

- scalar U4;
- scalar U8;
- TurboQuant U3;
- TurboQuant U4;
- BF16 or F16 control;
- F32 control where safe.

A lower precision such as U2 is included only when the pinned runtime exposes it through a usable property and activation proof passes. Codebook constants alone do not admit it.

### 4.2 Route B — experimental QJL and PolarQuant

Route B is separately labelled experimental functionality. Candidate formats are:

- TBQ3 plus QJL;
- TBQ4 plus QJL;
- PolarQuant 3-bit;
- PolarQuant 4-bit.

Before model benchmarking, Route B must identify an exact repository, branch and commit containing:

- compilable implementation code;
- documented build instructions;
- a selectable runtime property or explicitly runnable test interface;
- encode and decode paths;
- packed-record sizing;
- activation or dispatch evidence;
- no silent substitution with ordinary TurboQuant or scalar cache.

If no source passes this admission gate, Route B ends as:

```text
Blocked — no pinned executable and selectable QJL/PolarQuant implementation identified.
```

That is a valid campaign finding. Implementing the missing algorithms would become a separate reviewed engineering work package, not a hidden benchmark change.

### 4.3 Cross-route claim boundary

Every result records its route. Route B must never be described as official or merged OpenVINO support.

Cross-route comparison is allowed only when model, tokenizer, prompts, contexts, measurement definitions and activation evidence are matched.

## 5. Exact README and build-document fidelity

Each route follows the instructions at its pinned source revision.

For Route A, the controlling sources are the pinned OpenVINO Windows build document and the pinned OpenVINO GenAI source-build document. Automation will:

1. clone exact revisions;
2. initialise recursive submodules as documented;
3. save the README and build documents into evidence;
4. generate a command manifest in documented order;
5. run each command separately;
6. record working directory, start/end times, exit code, stdout and stderr;
7. use the documented Visual Studio generator and Release configuration unless a logged compatibility attempt proves a change is necessary;
8. build Runtime and GenAI in one compatible source environment;
9. record Runtime, TBB and dependency paths;
10. hash resulting binaries and packages.

Automation must not silently correct documentation. Any necessary difference creates a deviation record containing:

- deviation ID;
- documented command;
- executed command;
- reason and source evidence;
- reproducibility effect;
- approval status;
- retest result.

The same rule applies to Route B after source admission.

## 6. Trust-zone architecture

### 6.1 Self-hosted execution

The Intel runner builds and tests with `contents: read`. It uploads evidence artifacts but cannot push to `main` or a results branch.

### 6.2 Evidence validation

A trusted GitHub-hosted workflow treats the uploaded bundle only as data and validates it with repository-pinned parsers. It checks:

- required files and schemas;
- SHA-256 manifests;
- run-to-metric-to-evidence references;
- requested and verified codec fields;
- failure records for unsuccessful runs;
- absence of model binaries, authentication tokens and secrets.

Invalid evidence remains available as an Actions artifact but is not committed as formal evidence.

### 6.3 Repository ingestion

After validation, a trusted workflow commits one batch per completed stage to:

```text
results/workbook-05-<campaign-id>
```

It creates or updates one draft pull request with:

- stage status;
- passed, failed, blocked and skipped counts;
- pinned source and model identities;
- key metrics and current frontier;
- failures and limitations;
- evidence paths;
- remaining work.

Nothing is pushed directly to `main`.

## 7. Controlled campaign phases

### Phase 0 — machine and repository preflight

Confirm:

- runner, computer, CPU, RAM and architecture;
- restricted runner account;
- AC power and no-sleep state;
- at least 6 GiB available physical RAM before formal execution;
- required free disk budget;
- Python, Git, CMake, MSBuild, MSVC and Windows SDK identities;
- repository ref and campaign ID;
- no competing Workbook 05 job;
- writable evidence and checkpoint locations.

### Phase 1 — source admission and locking

For each route:

- record source URL, branch, head commit, base commit and PR state;
- snapshot README/build documentation;
- inspect exposed properties, codec files and tests;
- record Runtime/GenAI compatibility candidates;
- create `source-lock.json`;
- create `documented-command-manifest.json`;
- record licence and claim boundary.

Route B cannot continue without passing admission.

### Phase 2 — documented build

Preserve:

- configure/build/install commands;
- environment variables;
- compiler, CMake and SDK versions;
- dependency downloads;
- warnings and errors;
- binary paths and hashes;
- elapsed time and peak build memory;
- final status.

### Phase 3 — algorithm and activation conformance

Before Granite inference, every admitted codec receives deterministic tests for:

- round trip;
- packing and unpacking;
- expected versus actual bytes;
- zero, near-zero and large finite values;
- NaN/Inf handling;
- deterministic seed, rotation, projection and codebook behaviour;
- independent K and V dispatch;
- dead-code and silent-fallback detection;
- source repository codec tests.

A codec failure blocks only dependent configurations.

### Phase 4 — diagnostic K/V capability sweep

Use a short diagnostic model for every admitted ordered K/V pair.

Order pairs by:

```text
ascending(measured K bytes + measured V bytes)
```

Tie-breakers are symmetric pair first, lower K bytes, lower V bytes and stable codec-name order.

This phase proves dispatch and completion, not performance superiority.

### Phase 5 — Granite 3B feasibility frontier

Sort symmetric configurations by measured bytes per token, lowest first.

For each configuration, run:

```text
512 → 1,024 → 2,048 → 4,096 → 8,192 → 16,384 → continue doubling
```

The ladder ends at the first of:

- model-declared context limit;
- runtime-declared limit;
- repeated safety or correctness failure.

Each discovery point uses a P5-derived exact-retrieval fixture padded to the target token count, with the marker placed near the end. The discovery output is checked for exact retrieval and corruption but is not treated as a formal quality score.

### Phase 6 — formal Granite 3B evaluation

Every admitted symmetric codec and the standard control receive formal evaluation at one common context supported by all.

The standard control, each route’s lowest-memory stable codec, each route’s strongest quality candidate and any configuration required for a Workbook 05 decision also receive evaluation at their last stable context and immediately below any failure boundary.

Each frozen configuration receives:

1. one pilot, excluded;
2. one warm-up, excluded;
3. at least three measured repetitions;
4. P1–P6 evaluation;
5. perplexity where the pinned route provides a validated method;
6. complete performance, memory, activation and stability evidence.

### Phase 7 — asymmetric and cross-family tests

After symmetric stability is established, run:

- key-only low-bit configurations;
- value-only low-bit configurations;
- scalar/Turbo asymmetric pairs;
- admitted QJL/Polar asymmetric pairs;
- selected cross-family pairs required by Workbook 05.

Order remains based on measured total K/V storage.

### Phase 8 — Granite 8B safety gate

Granite 8B begins only after Granite 3B identifies safe candidates.

For this gate, `quality-valid` means the discovery fixture completed without corruption, exact-retrieval failure or a critical deterministic gate failure. It does not imply that final P1–P6 adjudication is complete.

Start with the lowest-memory quality-valid candidate and restart at 512 tokens. Promote only the standard control and candidates relevant to final memory, quality or speed conclusions to full 8B evaluation.

### Phase 9 — ablations and repeatability

Run only switches actually exposed by pinned source, including as applicable:

- norm correction OFF/ON;
- fused quantisation OFF/ON;
- documented QJL correction controls;
- documented PolarQuant codebook/tree controls;
- restart, repeatability and corruption checks.

### Phase 10 — comparison and conclusion

Report separate leaders for:

- memory reduction;
- quality preservation;
- decode speed;
- prompt-processing speed;
- TTFT;
- maximum stable context;
- stability;
- Granite 8B feasibility;
- merged support boundary;
- experimental support boundary;
- integration and maintenance risk.

Also identify Pareto-nondominated configurations across memory, speed and quality. Do not force all objectives into one unsupported winner.

## 8. Memory-frontier rules

### 8.1 Ordering data

Execution rank is derived from:

- K/V data bytes;
- K/V metadata bytes;
- bytes per token across layers and KV heads;
- expected full-context KV allocation;
- measured full-context KV allocation.

A configuration is excluded from formal comparison until an unexplained expected/measured discrepancy is resolved.

### 8.2 Stable-context definition

A context is stable only when:

- the child process exits normally;
- requested codecs are verified;
- no unexplained fallback occurs;
- output completes without corruption;
- the watchdog does not trigger;
- the stage timeout is not exceeded;
- post-run cleanup returns the machine to a healthy state.

A failure is retried once with the same frozen configuration after cleanup and cooldown. If the same failure class repeats, the preceding stable context becomes the provisional frontier and higher contexts are recorded as `Skipped by frontier`.

### 8.3 Safety watchdog

The parent process records periodic resource samples and terminates the inference process tree when either condition persists for 10 seconds:

- available physical RAM is below 1.5 GiB;
- Windows commit usage exceeds 90% of the commit limit.

It also terminates the child when the stage-specific heartbeat or timeout expires.

After each run, cooldown continues until CPU utilisation remains below 10% and available RAM returns to within 10% of the pre-run value for 60 seconds. Failure to recover blocks the next run and records an unhealthy-machine result.

The workflow never alters the page file, disables memory protection, overclocks hardware or changes firmware settings.

## 9. Frozen formal controls

Formal comparisons hold constant:

- model repository, revision and weight precision;
- tokenizer files and hashes;
- Runtime and GenAI commits;
- build type, compiler, generator and SDK;
- CPU device, stream and thread settings;
- prompt set and text;
- sampling settings and seed;
- output-token limit and context target;
- power source and power plan;
- runner account;
- metric and evaluator versions.

A changed control creates a new configuration ID and is not averaged with the previous configuration.

## 10. Performance and resource metrics

Each measured repetition records:

- model-load time;
- TTFT;
- prompt-processing tokens/s;
- TPOT;
- decode tokens/s;
- total generation time;
- input/output token counts;
- peak process-tree working set;
- peak process-tree private bytes;
- available RAM before, minimum during and after;
- runtime-reported K, V and total KV allocation;
- CPU mean and peak utilisation;
- actual backend, device, model placement and KV placement;
- exit code and completion state.

Formal summaries use median and range across valid measured repetitions. Pilot and warm-up runs remain visible but are excluded.

## 11. Quality and perplexity

Formal quality uses:

```text
GTQ-PROMPTS-v1
GTQ-QUALITY-RUBRIC-v1
0–10 scale
```

P1–P6 remain individually visible.

Evaluation order is:

1. deterministic checks;
2. critical caps;
3. blinded rubric scoring;
4. criterion-level evidence;
5. weighted 0–10 score;
6. matched-baseline delta;
7. manual-adjudication flag.

A subjective evaluator cannot override invalid JSON, a wrong exact marker, a wrong remembered value, a missing mandatory fact, a required-format failure, empty output, truncation or corruption.

The unattended workflow records a provisional score, evaluator identity/version, disagreement information and manual-review requirement. Final quality conclusions remain pending for cases that the controlling rubric requires humans to adjudicate. Performance evidence is not discarded while quality review is pending.

Reports include per-prompt, per-dimension and overall scores, baseline deltas, cap reasons, perplexity/delta where valid, corruption counts and repetition stability. Perplexity supports but does not replace task-level quality.

## 12. Evidence and repository layout

Use the existing root:

```text
experiments/granite_turboquant_intel/
```

Workbook 05 follows existing route/run conventions:

```text
manifests/campaigns/<campaign-id>/
manifests/runs/custom-openvino/<test-id>/<run-id>/
logs/custom-openvino/<test-id>/<run-id>/
outputs/custom-openvino/<test-id>/<run-id>/
metrics/custom-openvino/<test-id>/<run-id>/
results/custom-openvino/<test-id>/<run-id>/
notes/custom-openvino/<test-id>/<run-id>/
```

Each run contains or references:

- run manifest;
- exact command and environment snapshot;
- stdout and stderr;
- raw response;
- resource samples;
- activation and allocation proof;
- processed metrics and quality result;
- failure record where applicable;
- SHA-256 manifest.

Large models, source trees and build outputs are not committed. Their paths, revisions, licences and hashes are committed.

## 13. Checkpoint, resume and failure states

Every validated phase writes a durable checkpoint.

Resume verifies campaign ID, source locks and prior hashes, skips immutable completed work and begins at the first incomplete configuration. Every attempt keeps its own run ID.

Only one Workbook 05 execution may use the Intel runner at a time.

Failure records contain failure ID, test/run ID, route, configuration, context, code, first symptom, root-cause status, retry decision, next action and evidence paths.

Allowed result states are:

- `Passed`;
- `Failed`;
- `Blocked`;
- `Skipped by frontier`;
- `Not applicable`.

## 14. Workbook and traceability revision

Workbook 05 v1.3 remains unchanged.

A new controlled v1.4 revision will:

- retain all existing test IDs;
- add route, phase and memory rank;
- add expected/verified bytes;
- add frontier and skip status;
- add run/evidence references;
- correct QJL/Polar sizing only after conformance evidence;
- update the append-only revision register;
- add a decision-log entry for the two-route, memory-first order;
- regenerate controlled outputs and hashes.

Changing execution order does not change existing test-ID meaning.

## 15. Security boundaries

- The self-hosted workflow uses read-only repository permission.
- Only the trusted ingestion workflow writes results.
- Third-party Actions are pinned to immutable SHAs.
- No personal access token is stored on the Intel laptop.
- Secrets and authentication tokens never enter logs or evidence.
- Fork pull requests cannot target the runner.
- Workflow inputs, paths and commands are allowlisted and validated.
- Nothing auto-merges into `main`.

## 16. Acceptance criteria

Implementation is accepted only when:

1. route labels cannot be confused;
2. Route B cannot run without source admission;
3. pinned documentation and every build command are captured;
4. execution is ordered by verified memory demand;
5. the context ladder advances and stops safely;
6. repeated boundaries skip unsafe higher contexts without losing evidence;
7. activation and fallback proof exists for every requested codec;
8. every measured run has a complete manifest and raw samples;
9. formal quality references the frozen prompts and rubric;
10. P1–P6 and criterion scores remain visible;
11. pilot, warm-up and measured roles are preserved;
12. a trusted workflow commits validated evidence to a results branch;
13. a draft PR reports progress and limitations;
14. no unsupported merged, QJL or PolarQuant claim is generated;
15. final conclusions identify separate memory, quality, speed, context and stability leaders.

## 17. First implementation boundary after approval

The first implementation plan will cover only:

1. controlled Workbook 05 v1.4 scaffolding;
2. Route A/B source-admission manifests;
3. exact-document command capture;
4. preflight workflow;
5. evidence schemas and validation;
6. checkpoint/resume skeleton.

It will not yet build OpenVINO, download large models, run inference, implement QJL/PolarQuant, alter OpenVINO algorithms, rewrite historical evidence or merge automatically.

A later reviewed plan will control the build stage after preflight identifies the exact Runtime/GenAI pair and determines whether an executable Route B source exists.

## 18. Engineering rationale

The design separates merged and experimental capabilities, preserves traceability, makes assumptions visible and discovers capacity incrementally. It treats performance and AI quality as separate dimensions and uses frozen prompts, deterministic gates, repeated measurements and transparent trade-offs.

Separating self-hosted execution from repository-writing permission also reduces the impact of a compromised runner while still allowing validated evidence to update the real repository automatically.
