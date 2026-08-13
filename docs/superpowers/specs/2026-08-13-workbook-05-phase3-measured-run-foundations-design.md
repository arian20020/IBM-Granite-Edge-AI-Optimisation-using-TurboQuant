# Workbook 05 Phase 3 and Measured-Run Foundations Design

**Date:** 13 August 2026  
**Campaign:** `GTQ-WB05-MF-v1`  
**Route in scope:** `route-a-merged-openvino`  
**Starting checkpoint:** Route A Phase 2 `BuildCandidate` on `main@b85562753df07c5ecbe95c44e3203733a89a23df`  
**Status:** Design proposed for project-owner review  
**Implementation status:** Not started

## 1. Purpose

Phase 2 proved that one exact OpenVINO Runtime and source-matched OpenVINO GenAI pair could be configured, built, installed, retained, and independently validated on the controlled Intel Windows laptop. It did **not** prove that IBM Granite loads, that TurboQuant is selected at runtime, that compressed K/V records occupy the expected number of bytes, or that any configuration improves memory, speed, context length, or quality.

This design defines the engineering boundary between that accepted build and the later scientific campaign. It introduces the model-asset, process-execution, activation, storage, performance, quality, checkpoint, and evidence foundations required before a compressed Granite result may be called valid.

The immediate goal is not to run the full benchmark matrix. The immediate goal is to make invalid, incomplete, silently-fallbacking, or non-reproducible model runs unable to pass.

## 2. Accepted prerequisites

The following installations and decisions are immutable prerequisites and are consumed read-only:

```text
OpenVINO Runtime install:
C:\w5a\phase2-31391119557-4\i-ov

OpenVINO Runtime decision:
C:\w5a\accepted-route-a-runtime-31656417607-1\decision.json

OpenVINO GenAI install:
C:\w5a\phase2-31661571860-1\i-genai

OpenVINO GenAI decision:
C:\w5a\accepted-route-a-genai-31661571860-1\decision.json
```

Accepted source identities:

| Component | Repository | Exact commit |
|---|---|---|
| OpenVINO Runtime | `openvinotoolkit/openvino` | `b9a1f201c109e0bed74763934f79483cf6c4cbf4` |
| OpenVINO GenAI | `openvinotoolkit/openvino.genai` | `bd8d6542e3ca1ac30042d5d8d4202ce00b5f4af0` |

The Phase 3 implementation must recheck the accepted decision hashes before every live job. It must never patch, rebuild in place, delete, rename, or write generated files inside either accepted installation.

## 3. Primary research boundary

### 3.1 IBM Granite assets

The primary formal model is the official IBM instruct repository:

```text
ibm-granite/granite-4.1-3b
```

IBM identifies it as a 3B dense, long-context instruct model released on 29 April 2026 under Apache 2.0. Its published architecture has 40 layers, 8 KV heads, and a declared sequence length of 131,072 tokens. The later safety gate targets:

```text
ibm-granite/granite-4.1-8b
```

The 8B model is not downloaded during the first implementation package. Only its repository identity and later-gate role are recorded. This prevents the 8B asset from consuming disk space before the 3B route establishes a safe executable frontier.

The mutable Hugging Face branch name `main` is not an acceptable experimental revision. A model-asset freeze must query the official repository, record the returned immutable full revision, download only that revision, and hash every retained source and tokenizer file locally.

### 3.2 Exact merged TurboQuant control surface

OpenVINO PR `#35853`, merged as the accepted Runtime commit, defines the Route A control surface:

- `KEY_CACHE_QUANT_ALG` and `VALUE_CACHE_QUANT_ALG` select `SCALAR` or `TURBO` independently;
- `KEY_CACHE_PRECISION` and `VALUE_CACHE_PRECISION` select the bit width independently;
- the exact CPU configuration accepts `u3`, `u4`, `u8`, and supported floating controls;
- selecting `TURBO` without an explicit precision defaults to `u4`;
- the SDPA cache-compression path calls `turboq_quantize` only when the selected algorithm is `TURBO` and passes the selected precision bit width to that implementation.

Therefore, **precision alone does not prove TurboQuant activation**. Every candidate must set and record both the algorithm and precision for K and V. A request for `u3` without verified `TURBO` dispatch cannot be labelled TurboQuant.

The initial Route A families are:

| Family | K algorithm / precision | V algorithm / precision | Role |
|---|---|---|---|
| Scalar U8 control | `SCALAR / u8` | `SCALAR / u8` | Standard quantized control |
| Scalar U4 control | `SCALAR / u4` | `SCALAR / u4` | Lower-bit scalar control |
| TurboQuant 4-bit | `TURBO / u4` | `TURBO / u4` | Merged TurboQuant candidate |
| TurboQuant 3-bit | `TURBO / u3` | `TURBO / u3` | Lowest nominal merged TurboQuant candidate |
| Floating control | same supported floating type | same supported floating type | Safety/quality control when feasible |

Scalar U3 is not admitted merely because the property parser accepts `u3`. The exact cache-compression implementation treats the Turbo path separately from scalar quantization, so a scalar-U3 row remains blocked unless executable evidence proves a valid scalar-U3 implementation.

QJL and PolarQuant are not part of Route A. They remain Route B experimental candidates and cannot be relabelled as merged OpenVINO support.

### 3.3 Performance and evaluation basis

OpenVINO and AI-engineering guidance distinguish first-token latency from later-token throughput. The harness must therefore retain separate model-load, prefill, TTFT, TPOT, decode-throughput, and total-generation measurements instead of reporting one undifferentiated elapsed time.

The existing project controls remain authoritative:

```text
Prompt set: GTQ-PROMPTS-v1
Quality rubric: GTQ-QUALITY-RUBRIC-v1
Formal score: 0–10
Formal prompts: P1–P6
```

The strict existing `measured-run-manifest` remains the contract for **formal measured runs**. It must not be weakened to accommodate incomplete conformance or smoke attempts.

## 4. Scope

### 4.1 Included

This design covers five checkpointed implementation packages:

1. immutable model/tokenizer and conversion-asset locking;
2. safe child-process execution, sampling, watchdog, cooldown, retry, and resume;
3. Route A codec conformance, activation, fallback, and storage proof;
4. deterministic and rubric-based quality evidence;
5. a standard-cache smoke workflow with independent hosted validation.

### 4.2 Excluded

This design does not yet execute:

- the complete K/V capability matrix;
- the Granite 3B context-doubling frontier;
- the full formal performance and P1–P6 campaign;
- asymmetric K/V comparison;
- Granite 8B model download or evaluation;
- norm-correction or fused-quantisation ablations;
- Route B QJL or PolarQuant repair/build/model runs;
- application UI integration.

Those depend on the foundations defined here and receive separate reviewed implementation or execution checkpoints.

## 5. Approaches considered

### 5.1 One monolithic workflow

A single workflow could download a model, convert it, run conformance, run all prompts, calculate scores, and update results.

**Rejected.** A failure would mix asset, runtime, algorithm, process, quality, and infrastructure causes. It would also make restart behaviour expensive and encourage rerunning successful outputs unnecessarily.

### 5.2 Reuse the formal measured-run manifest for every early attempt

The existing formal schema could be populated for unit, conformance, and smoke attempts.

**Rejected.** Early attempts do not always have a matched baseline, P1–P6 result, or formal quality score. Filling those fields with placeholders would create apparently complete but scientifically invalid evidence. Weakening the schema would reduce the trustworthiness of all later results.

### 5.3 Layered evidence pipeline

Each stage produces a narrow record and the later formal manifest references those records:

```text
asset lock
  -> process attempt
  -> activation/fallback proof
  -> storage proof
  -> conformance result
  -> quality result
  -> formal measured-run manifest
```

**Selected.** It preserves strict formal requirements, isolates failures, allows durable resume, and supports independent validation at every trust boundary.

## 6. System architecture

### 6.1 Controlled roots

```text
C:\w5a\   accepted Runtime/GenAI build prerequisites; read-only
C:\w5m\   immutable model sources and converted model assets
C:\w5c\   Phase 3 probe source/build/install workspaces
C:\w5r\   durable measured-run workspaces and local checkpoints
```

Each new root must be a normal local directory, not a link, junction, device path, UNC path, or reparse point. Every workflow run receives a unique `<run-id>-<attempt>` directory. Existing directories are never silently cleaned or reused.

### 6.2 Components

#### A. Asset locker

Responsibilities:

- resolve an official repository to one immutable revision;
- download by that revision only;
- record repository, revision, file list, size, licence, and SHA-256;
- identify tokenizer files separately;
- record declared architecture and context metadata without treating it as observed runtime capability;
- invoke one pinned local OpenVINO conversion command;
- hash every converted IR/tokenizer/config output;
- record weight format, quantisation mode, group size, ratio, dataset use, and tool versions;
- reject any unrecorded file, revision drift, hash drift, or model path outside `C:\w5m`.

The first Granite conversion candidate is a locally generated OpenVINO IR with weight-only INT4 compression, because weight precision must stay fixed while KV-cache configurations change. The exact INT4 settings are not silently assumed. A conversion compatibility spike must select and record one configuration before the asset can be frozen. The preferred candidate is asymmetric INT4, group size 128, ratio 1.0, with no calibration dataset unless a reviewed compatibility result requires otherwise. An INT8 or FP16 fallback becomes a new model-configuration identity and cannot overwrite the failed INT4 attempt.

The asset locker records Granite 4.1 8B metadata but defers its binary download until the separate 8B safety gate.

#### B. Inference driver

A project-controlled C++ executable is preferred for Route A because the exact merged control surface includes internal K/V algorithm properties that may not be available through every high-level Python wrapper.

Responsibilities:

- consume a validated JSON run request;
- load the frozen OpenVINO IR and tokenizer through the accepted GenAI installation;
- set CPU as the requested device;
- set K and V algorithm and precision independently;
- use frozen generation settings and seed;
- stream tokens so first-token time is directly observable;
- emit structured event records rather than relying on human-readable console parsing alone;
- write complete raw output without truncation;
- return a machine-readable exit/failure class.

The driver does not score its own output and does not decide whether activation is scientifically proven.

#### C. Process supervisor

Responsibilities:

- start the inference driver as a child process without shell-string execution;
- retain stdout, stderr, structured events, command arguments, working directory, and environment allowlist;
- sample the complete process tree;
- enforce memory, commit, heartbeat, and stage deadlines;
- terminate descendants before the parent;
- classify normal failure separately from safety stop, timeout, runner disconnect, and infrastructure interruption;
- run cooldown health checks;
- permit one controlled retry only for approved transient failure classes;
- write atomic checkpoints before moving to another attempt.

#### D. Resource sampler

The sampler records at a fixed interval:

- process-tree working set and private bytes;
- system available physical memory;
- Windows commit use and limit;
- process CPU use;
- optional GPU dedicated/shared memory where applicable;
- heartbeat age;
- active descendant process identities.

Safety thresholds remain:

```text
Available physical RAM below 1.5 GiB for 10 seconds -> terminate
Windows commit use above 90% for 10 seconds        -> terminate
No heartbeat for 15 minutes                        -> terminate
```

No workflow may alter the page file, overclock hardware, disable memory protection, or change firmware settings.

#### E. Activation verifier

Activation proof has four required layers:

1. **Request proof:** exact K/V algorithm and precision sent to the driver.
2. **Property proof:** runtime accepted the exact independent K/V properties without substitution.
3. **Dispatch proof:** executable evidence identifies the scalar or Turbo cache path that ran.
4. **Storage proof:** measured K and V record/allocation sizes agree with the selected implementation within a documented exact or bounded rule.

A passed compressed result requires all four. Property acceptance plus a successful generated answer is insufficient.

Preferred dispatch proof order:

1. existing runtime diagnostic or execution-graph evidence from the unmodified accepted build;
2. existing source-matched CPU functional-test evidence linked to the exact configuration;
3. a separately labelled trace-only build of the exact source with a minimal reviewed marker, used for conformance only and never for formal performance.

If no direct dispatch proof is obtainable, the result is `ActivationUnproven`. Measured memory reduction may be recorded as an observation, but the run cannot be admitted as a TurboQuant result.

#### F. Storage verifier

Responsibilities:

- record expected data bytes and metadata bytes separately for K and V;
- record expected bytes per token across layers and KV heads;
- measure actual K and V record sizes where the runtime exposes them;
- measure or derive actual full K and V allocation from executable evidence;
- reference the exact source formula used for reconciliation;
- reject unexplained discrepancies;
- provide the measured total used for low-memory-first ordering.

Nominal bit width never determines execution rank on its own.

#### G. Quality evaluator

Responsibilities:

- retain immutable raw output and SHA-256 first;
- run deterministic checks before subjective scoring;
- apply critical caps for wrong markers, invalid structure, missing required facts, empty output, corruption, or truncation;
- score the five frozen dimensions on the 0–10 scale;
- hide baseline/candidate labels during comparative scoring;
- evaluate both presentation orders where the rubric requires it;
- record evaluator identity/version, disagreement, reversal, and manual-adjudication state;
- calculate per-prompt, overall, and matched-baseline deltas;
- prevent a judge from overriding deterministic failure.

Perplexity is nullable and remains unavailable unless a source-matched, validated procedure is implemented.

#### H. Hosted evidence validator

A clean GitHub-hosted runner treats the uploaded bundle only as data. It:

- verifies the complete SHA-256 manifest;
- validates every JSON record against repository-pinned schemas;
- verifies run, commit, attempt, route, model, build, and configuration identities;
- rejects unsafe paths, secrets, executables, libraries, model files, archives inside the evidence payload, and shell-command records;
- verifies that a compressed pass has activation, no-fallback, and storage proof;
- verifies that formal measured runs have raw output and complete quality evidence;
- writes a Markdown validation report even on failure.

## 7. Evidence contracts

The implementation adds the following narrow contracts rather than weakening `measured-run-manifest.schema.json`:

```text
model-asset-lock.schema.json
model-conversion-record.schema.json
process-attempt.schema.json
resource-summary.schema.json
activation-proof.schema.json
storage-proof.schema.json
conformance-result.schema.json
quality-result.schema.json
smoke-summary.schema.json
```

### 7.1 Model asset lock

Required fields include:

- asset ID and role (`diagnostic`, `granite-3b`, or deferred `granite-8b`);
- official repository and immutable revision;
- licence identifier;
- source file list and hashes;
- tokenizer file list and hashes;
- declared model metadata and source of that declaration;
- local source path;
- conversion record reference;
- converted IR file list and hashes;
- model/tokenizer aggregate identity;
- acceptance status and reasons.

### 7.2 Process attempt

Required fields include:

- attempt ID, run role, and retry relation;
- exact executable and argument array;
- environment allowlist;
- timestamps and elapsed time;
- process and descendant exit state;
- stdout/stderr/event/output paths;
- watchdog state;
- safety stop and trigger details;
- pre-run and post-run health state;
- resource-summary reference;
- classification and failure IDs.

### 7.3 Activation proof

Required fields include:

- requested and verified K/V algorithms;
- requested and verified K/V precisions;
- property names and serialized values;
- direct dispatch evidence type and path;
- fallback observed flag and evidence;
- exact Runtime source commit;
- exact probe/runtime binary hashes;
- confidence classification: `Proven`, `Rejected`, or `Unproven`.

### 7.4 Storage proof

Required fields include:

- selected K/V family;
- exact source formula reference and source hash;
- K/V data and metadata bytes per record;
- K/V bytes per token;
- expected and measured full allocations;
- tolerance rule, discrepancy, and explanation;
- ordering total;
- status: `Reconciled`, `Mismatch`, or `Unavailable`.

### 7.5 Conformance result

A compressed candidate passes only when:

```text
process completed normally
AND requested properties were accepted
AND K activation is Proven
AND V activation is Proven
AND no fallback was observed
AND K storage is Reconciled
AND V storage is Reconciled
AND output integrity checks passed
AND the evidence bundle passed hosted validation
```

### 7.6 Formal measured-run manifest

The existing strict manifest remains the final aggregation record. It references the accepted asset, process, activation, storage, performance, and quality evidence. Pilot and warm-up attempts remain visible but are excluded from formal statistics.

## 8. Asset strategy

### 8.1 Diagnostic asset

The first live asset is the smallest model or deterministic stateful IR that can reach the same CPU SDPA/KV-cache path as Granite. Selection is a compatibility spike, not an assumption.

Candidate order:

1. a minimal project-generated stateful IR proven to reach the exact codec dispatch;
2. a narrow official/source-matched OpenVINO CPU SDPA diagnostic target;
3. a small official text-generation model converted through the same toolchain.

The selected diagnostic asset must prove path equivalence. If it cannot exercise the same cache path, it may test the process harness but cannot provide codec activation evidence.

### 8.2 Granite 4.1 3B

The official IBM instruct repository is frozen at one immutable revision. Conversion is local and provenance-bound. The source, tokenizer, conversion command, tool versions, weight format, output files, and hashes are all retained.

Weight compression and KV-cache compression remain separate axes:

```text
fixed Granite weight artefact
x
variable K/V cache configuration
```

No formal KV comparison may mix different model-weight artefacts.

### 8.3 Granite 4.1 8B

Phase 3 records only repository metadata and deferred status. Binary download and conversion are permitted only after Granite 3B identifies a low-memory, quality-valid candidate and the 8B disk/memory preflight passes.

## 9. Disk and machine preflight

The latest accepted local hand-off reported approximately 75.63 GiB free. Before asset conversion:

1. inventory `C:\w5a`, `C:\w5m`, `C:\w5c`, and `C:\w5r`;
2. preserve the four accepted Runtime/GenAI install and acceptance directories;
3. identify obsolete failed/cancelled workspaces by immutable run identity;
4. retain their text evidence before deletion;
5. require an approved deletion list and explicit project-owner confirmation;
6. require at least 50 GiB free before starting the Granite 3B source download/conversion;
7. calculate a separate budget before any 8B download.

The asset workflow stops before download if the threshold is not met. It does not run automatic broad cleanup.

## 10. Standard-cache smoke sequence

The first complete model pipeline uses a standard scalar cache, not TurboQuant.

Sequence:

```text
1 excluded pilot
1 excluded warm-up
3 measured repetitions
```

The smoke sequence proves:

- asset identity and hashes;
- model and tokenizer loading;
- CPU device placement;
- complete prompt and response retention;
- first-token and later-token timing boundaries;
- resource sampling;
- deterministic generation controls;
- checkpoint and resume;
- independent artifact validation.

The smoke does not establish a final quality comparison. It uses a separate smoke summary and at least one deterministic prompt check. Formal P1–P6 scoring begins only after the quality evaluator checkpoint passes.

## 11. Low-memory-first promotion rule

After conformance, executable configurations are ordered by:

```text
ascending(
    measured K data bytes
  + measured K metadata bytes
  + measured V data bytes
  + measured V metadata bytes
)
```

Tie-breakers:

1. symmetric configuration first;
2. lower K bytes;
3. lower V bytes;
4. stable codec-name order.

The likely nominal sequence begins with TurboQuant U3, followed by U4 and scalar controls, but the campaign does not freeze that order until measured storage is reconciled.

A configuration that lacks activation or reconciled storage remains blocked and receives no memory rank.

## 12. Workflow architecture and permissions

Each live workflow uses three boundaries:

```text
GitHub-hosted repository contract
  -> self-hosted Intel evidence collection
  -> GitHub-hosted untrusted-data validation
```

Rules:

- manual dispatch from `main` only for laptop execution;
- same-repository guard;
- exact labels: `self-hosted`, `Windows`, `X64`, `workbook05`, `intel-target`;
- `contents: read` and `actions: read` only;
- `persist-credentials: false`;
- immutable action SHAs;
- `cancel-in-progress: false`;
- no repository push from the laptop;
- no model or executable upload;
- no pull-request-triggered model execution;
- unique same-attempt artifact names;
- hosted validator downloads exactly one expected artifact;
- results ingestion, when implemented, is a separate trusted workflow and draft PR.

## 13. Failure model

Failure classes remain distinct:

| Class | Meaning |
|---|---|
| `IntegrityFailure` | Identity, hash, schema, path, or trust-boundary violation |
| `UnsupportedConfiguration` | Runtime rejects requested property or precision |
| `ActivationRejected` | Direct evidence proves another path executed |
| `ActivationUnproven` | Requested configuration completed but dispatch proof is insufficient |
| `StorageMismatch` | Expected and measured cache storage do not reconcile |
| `OutputIntegrityFailure` | Empty, corrupt, malformed, or deterministically wrong output |
| `ResourceSafetyStop` | RAM or commit threshold reached |
| `Timeout` | Stage or heartbeat deadline reached |
| `InfrastructureInterrupted` | Runner disconnect, cancellation, restart, or external termination |
| `ModelCompatibilityFailure` | Frozen model/tokenizer cannot load through the accepted pair |
| `Passed` | Every required checkpoint for that record type passed |

An infrastructure interruption is not reported as an algorithm failure. A successful process exit is not reported as activation proof.

## 14. Checkpoint and resume

A checkpoint is updated atomically after each independently valid stage. It records:

- campaign and route;
- project repository head;
- Runtime and GenAI decision hashes;
- model asset and tokenizer hashes;
- conversion identity;
- configuration identity;
- completed attempt IDs and bundle hashes;
- first incomplete stage;
- generation number.

Resume verifies all identities before skipping work. A mismatched checkpoint is rejected; it is never automatically rewritten to match the current machine.

Successful raw outputs and metric records are reused by reference rather than regenerated. A changed prompt, model, tokenizer, build, weight format, runtime setting, quality rubric, or evaluator version creates a new configuration or evaluation identity.

## 15. Testing strategy

### 15.1 Unit tests

- schema acceptance/rejection;
- path and hash validation;
- command argument preservation;
- metric calculations;
- TTFT/TPOT boundary calculation;
- storage formula reconciliation;
- activation decision rules;
- deterministic quality caps;
- checkpoint generation and stale-resume rejection.

### 15.2 Component tests

- child process normal completion;
- stdout/stderr/event draining;
- descendant process termination order;
- heartbeat timeout;
- simulated RAM and commit safety stop;
- cooldown success/failure;
- one permitted retry;
- raw-output hashing;
- hosted bundle validation with adversarial fixtures.

### 15.3 Integration tests

- diagnostic model standard cache;
- property rejection fixture;
- scalar positive fixture;
- Turbo positive fixture only when direct activation evidence exists;
- silent-fallback fixture;
- storage mismatch fixture;
- corrupt/truncated output fixture.

### 15.4 Live smoke test

The standard-cache pilot/warm-up/three-measured sequence is the final checkpoint for this design. The live artifact and independent validator must pass on the same immutable project head before compressed capability execution is authorised.

## 16. Implementation packages and review gates

This design is implemented as separate pull requests so one subsystem cannot hide failures in another.

### Package C1 — asset locking

Outputs:

- asset/conversion schemas and validators;
- immutable-download and conversion orchestrator;
- disk preflight;
- diagnostic selection spike;
- Granite 4.1 3B asset lock;
- hosted validator.

Gate: exact model/tokenizer/conversion identities accepted; no model execution claim.

### Package C2 — process harness

Outputs:

- process-attempt and resource contracts;
- child-process supervisor;
- sampler, watchdog, cooldown, retry, and resume;
- synthetic process fixtures.

Gate: normal and every controlled failure path are tested and independently reviewable.

### Package C3 — activation and storage conformance

Outputs:

- project-controlled C++ Route A driver/probe;
- K/V property adapter;
- direct dispatch evidence;
- storage formula and reconciliation;
- scalar and Turbo conformance fixtures;
- negative fallback tests.

Gate: only proven/reconciled configurations become executable candidates.

### Package C4 — quality evidence

Outputs:

- deterministic P1–P6 checks;
- critical-cap logic;
- blinded five-dimension scoring records;
- matched-baseline comparison and adjudication;
- adversarial evaluator tests.

Gate: no subjective score can override a deterministic failure.

### Package C5 — standard baseline smoke

Outputs:

- dedicated read-only workflow;
- exact asset/build inputs;
- pilot, warm-up, and three measured standard-cache runs;
- complete text evidence;
- independent hosted validation;
- durable smoke checkpoint.

Gate: project owner accepts the exact validated smoke artifact and digest.

Only C5 authorises the next capability-sweep implementation/execution package.

## 17. Scientific claim boundary

Before C5 passes, all of the following remain false:

```text
Granite model campaign authorised
TurboQuant activation claim authorised
Packed-storage claim authorised
Performance comparison authorised
Quality comparison authorised
```

After C5, only standard-baseline model execution and harness operation may be described as proven. TurboQuant, storage, performance, and quality claims require their own later accepted evidence.

## 18. Route B boundary

The Route A harness is designed for reuse, not for silently importing Route B. Reopening QJL/PolarQuant requires a separate reviewed package that first supplies:

- an accepted Route B executable candidate;
- successful narrow build/test discovery;
- direct QJL/Polar dispatch evidence;
- packed record evidence;
- compatible Runtime/GenAI or explicit runnable interface;
- independent artifact validation;
- explicit project-owner digest acceptance.

Until then, every dependent Route B row remains blocked with its recorded reason.

## 19. Success criteria for this design

This design is complete when:

1. all five implementation packages have explicit inputs, outputs, schemas, tests, and trust boundaries;
2. accepted Runtime/GenAI state is read-only and revalidated;
3. model and weight provenance cannot drift silently;
4. early evidence does not weaken the formal measured-run schema;
5. compressed passes require direct activation, no fallback, and reconciled storage;
6. process safety and failure classifications are independently tested;
7. raw outputs, metrics, and quality evidence are retained rather than regenerated;
8. the standard-cache smoke artifact passes hosted validation;
9. low-memory-first ordering is based on measured bytes rather than nominal labels;
10. Route B remains separately labelled and blocked until its own gate passes.

## 20. Professional and primary-source basis

This design is grounded in:

- the official IBM Granite 4.1 model cards for the 3B and 8B instruct models;
- OpenVINO PR `#35853` and the exact merged Runtime source at `b9a1f201...`;
- the exact accepted GenAI source at `bd8d6542...`;
- OpenVINO documentation for local IR conversion, weight compression, and GenAI performance terminology;
- the existing `GTQ-PROMPTS-v1`, `GTQ-QUALITY-RUBRIC-v1`, strict measured-run schema, memory-frontier design, and completion checkpoint plan;
- systems-integration, test-traceability, configuration-management, defensive-programming, trustworthy-testing, systematic-debugging, and AI-evaluation practices from the project’s curated engineering textbooks.
