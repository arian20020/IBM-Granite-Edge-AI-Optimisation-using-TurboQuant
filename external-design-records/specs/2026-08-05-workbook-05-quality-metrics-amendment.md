# Workbook 05 Quality and Metrics Amendment

## Status

Approved requirement from the project owner on 2026-08-05.

This amendment applies to campaign `GTQ-WB05-MF-v1` and supplements the Phase 1 source-admission design. Phase 1 does not run a model, so it cannot produce performance or quality values. It must, however, preserve and validate every contract needed so that later successful conformance and inference runs cannot be marked complete without full performance, resource, activation, fallback, raw-output, and quality evidence.

## Core research question

The campaign must answer:

> Does KV-cache compression cause a practically important loss of model quality, and how does that loss compare across standard cache precision, merged TurboQuant, experimental TurboQuant+QJL, and experimental PolarQuant?

The answer must not be inferred from memory savings or speed alone. Quality is a separate first-class outcome.

## Controlled comparison axes

Every comparison must keep the following matched unless the row explicitly tests that factor:

- exact model repository, revision, and file hashes;
- exact tokenizer and chat template;
- exact weight quantisation;
- exact prompt set and long-context fixture;
- exact prompt text and turn history;
- exact context length;
- exact generation settings;
- exact seed;
- exact maximum output tokens;
- exact Runtime and GenAI commits;
- exact CPU device and placement;
- exact warm-up and repetition policy.

Weight quantisation and KV-cache compression are separate axes. A quality change may not be attributed to a KV codec when the model weights also changed.

## Required matched baselines

Where executable and safe, each model/context group must include matched reference rows for:

- floating cache precision used by the controlled Runtime, normally `f16` or `bf16`;
- standard scalar `u8` cache quantisation;
- standard scalar `u4` cache quantisation;
- merged Route A `TURBO + u3`;
- merged Route A `TURBO + u4`;
- experimental Route B TurboQuant+QJL variants that pass admission and conformance;
- experimental Route B PolarQuant 3-bit and 4-bit variants that pass admission and conformance.

`f32` may be added only where supported and safe. Unsupported configurations are recorded as `Not applicable` or `Blocked`, never silently omitted.

## Performance and resource evidence

A successful measured run is incomplete unless it records all applicable fields defined by `docs/testing/Metric-Definitions.md`:

- model load time in milliseconds;
- TTFT in milliseconds, with tokenisation/prefill inclusion stated;
- prompt-processing throughput in tokens per second;
- TPOT in milliseconds per token;
- decode throughput in tokens per second;
- total generation time in milliseconds;
- peak process-tree working set in bytes;
- peak process-tree private bytes in bytes;
- minimum system available RAM in bytes;
- runtime-reported K-cache allocation in bytes;
- runtime-reported V-cache allocation in bytes;
- CPU mean and peak utilisation;
- dedicated and shared GPU memory separately when a GPU route is ever used;
- actual backend, requested device, actual device, and placement;
- requested K/V precision and codec;
- verified K/V precision and codec;
- activation proof;
- fallback detection result;
- exit code, completion state, and stability classification.

A failed or partial run retains every value that was observed before failure. Missing values use explicit missing-data codes; they are never replaced with zero.

## Quality evidence

Quality uses the frozen controls:

- prompt set `GTQ-PROMPTS-v1`;
- rubric `GTQ-QUALITY-RUBRIC-v1`;
- generation defaults: temperature `0.0`, top-p `1.0`, seed `42`, maximum output tokens `256`.

Every measured configuration must preserve, for every P1-P6 prompt and every valid repetition:

- exact input prompt and supplied context hash;
- exact raw model output bytes and UTF-8 text;
- output SHA-256;
- token counts and stop reason;
- deterministic-check results;
- each failed deterministic condition;
- correctness-and-grounding score;
- instruction-and-format score;
- completeness-and-fact-retention score;
- relevance, clarity, and coherence score;
- stability-and-output-integrity score;
- any critical cap applied;
- final 0-10 task score;
- evaluator label hidden from the scorer;
- pairwise presentation order;
- manual adjudication reason where required.

An automated judge cannot override invalid JSON, incorrect exact output, missing required facts, a wrong remembered value, corruption, repetition loops, or truncation that prevents completion.

## Perplexity and deterministic correctness

Perplexity is recorded when the admitted runtime path supports a controlled and comparable calculation. It is reported with the exact corpus or fixture, token count, context policy, and baseline delta.

Perplexity does not replace task quality. P1-P6 deterministic failures and rubric scores remain visible even when perplexity changes little.

## Practical quality-degradation decision rule

The campaign does not call the six-prompt screen a general benchmark and does not claim population-level statistical significance from P1-P6 alone.

For project decisions, a compressed configuration has **material quality degradation** relative to its matched baseline when any of the following occurs:

1. a new deterministic critical failure appears on any prompt;
2. a new rubric critical cap is triggered;
3. the paired mean or median P1-P6 score falls by at least `1.0` point on the 0-10 scale;
4. at least four of six paired prompt scores decrease and the average decrease is at least `0.5` points;
5. repeated runs show corruption, wrong retained values, or unstable formatting not present in the baseline.

A paired score drop below `0.5` with no new deterministic failure is reported as a small observed difference, not automatically as meaningful degradation. A drop from `0.5` to below `1.0` is reported as moderate and requires manual adjudication. These thresholds are project decision rules, not universal claims about model quality.

All per-prompt deltas remain visible so an average cannot hide one severe failure.

## Repetitions and summaries

- Pilot runs diagnose the protocol and are excluded.
- One defined warm-up is excluded.
- At least three measured repetitions are required unless a documented safety gate blocks them.
- Preserve every valid repetition.
- Report median and range for performance metrics.
- Report per-prompt quality scores, paired deltas, aggregate mean and median, deterministic-failure counts, and critical-cap counts.
- Do not average unmatched models, weight precisions, contexts, prompts, devices, or codec activations.

## Required comparison outputs

Later formal phases must generate machine-readable and human-readable tables that show, for each model, context, weight precision, and K/V codec pair:

- memory saved versus matched floating and scalar baselines;
- TTFT, prompt throughput, TPOT, and decode-throughput deltas;
- peak-memory and KV-allocation deltas;
- P1-P6 scores and deterministic failures;
- aggregate quality delta;
- perplexity delta where available;
- activation and fallback status;
- final classification: `Passed`, `Failed`, `Blocked`, `Skipped by frontier`, or `Not applicable`.

The final conclusion must distinguish:

- merged Route A evidence;
- experimental Route B evidence;
- memory benefit;
- speed benefit or cost;
- quality benefit or degradation;
- stability and maximum supported context.

## Phase 1 obligation

Phase 1 source admission must not create invented metric values. It must:

- verify that `docs/testing/Metric-Definitions.md`, `GTQ-PROMPTS-v1`, and `GTQ-QUALITY-RUBRIC-v1` are present and controlling;
- hash and reference those controls in the source-admission artifact;
- preserve source hooks needed for runtime timing, cache allocation, codec activation, and fallback evidence;
- reject any later evidence schema that allows a successful measured inference row without raw output, quality result, activation proof, fallback result, and applicable metrics;
- keep actual measurement execution deferred to conformance and formal model phases.

## Acceptance condition

Workbook 05 cannot be declared complete merely because compressed inference runs. Completion requires a defensible answer to the quality question for every admitted codec family, supported by raw outputs, deterministic checks, blinded rubric scoring, matched baselines, repeated measurements, and explicit limitations.
