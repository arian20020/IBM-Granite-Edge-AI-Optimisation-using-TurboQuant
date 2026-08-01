# OpenVINO Adaptive Format Comparison Design

**Status:** Approved design

**Date:** 2026-08-01

**Branch:** `testing/openvino-turboquant-recovery`

**Workbook:** WB-04, Official OpenVINO Controlled Retest

## 1. Purpose

WB-04 currently contains formal three-sample runtime measurements for only three TurboQuant configurations. The successful U4 and U8 STANDARD executions were short conversion diagnostics with seven input tokens and four generated tokens. The frozen matrix explicitly marked those rows as non-runtime and disabled numeric generation metrics, so they cannot support a fair comparison.

This work will add a controlled comparison campaign that:

1. compares U8 STANDARD, U8+TBQ4, and U8+TBQ3 while holding the model, weight artifact, device, prompts, output length, and context constant;
2. separately compares the deployable U4, U8, and FP16 weight formats under STANDARD execution;
3. advances each viable format through increasing contexts until the laptop reaches a repeatable functional or memory boundary; and
4. records complete runtime, memory, utilisation, activation, cleanup, and quality evidence for every successful row.

The large comparison tables will contain only complete successful measurements. Terminal attempts will remain visible in a short boundary/failure list with a stage, reason, and evidence reference.

## 2. Scope and controlled identities

The historical `retest-matrix.json`, its hashes, and its accepted evidence remain immutable. The comparison matrix will be stored at `experiments/manifests/official-openvino/adaptive-format-comparison-matrix-v1.json`, and raw evidence will be stored below `experiments/raw-results/openvino-turboquant/2026-08-01/adaptive-format-comparison/`. The new matrix will use these unused formal IDs:

| Test ID | Model weights | K/V treatment | Device | Comparison role |
| --- | --- | --- | --- | --- |
| `OV-11` | Granite 3B U4 | STANDARD | CPU | Low-memory deployment baseline |
| `OV-12` | Granite 3B U8 | STANDARD | CPU | U8 deployment and cache baseline |
| `OV-13` | Granite 3B FP16 | STANDARD | CPU | Highest-precision deployment candidate |
| `OV-TQ-21` | Same Granite 3B U8 artifact as `OV-12` | TBQ4/TBQ4 | CPU | Controlled four-bit cache comparison |
| `OV-TQ-22` | Same Granite 3B U8 artifact as `OV-12` | TBQ3/TBQ3 | CPU | Controlled three-bit cache comparison |

The new matrix declares contexts `512`, `1024`, `2048`, `4096`, and `8192` for all five identities. Declaration permits the controller to validate a step; it does not require unsafe higher-context launches after a lower step fails.

STANDARD rows must be labelled with the observed K/V runtime state. A request or model-conversion label is not evidence of K/V storage precision. If the observer again reports f32/f32 state, the workbook will say “CPU STANDARD; observed f32/f32 state” rather than “U4 K/V” or “U8 K/V.”

The FP16 row may run only after an artifact is acquired or converted, hashed, structurally validated, and bound to the matrix. Failure to prepare that artifact is an artifact-preparation boundary, not an inference-capability result.

## 3. Adaptive ladder

The campaign is breadth-first by context so that common comparison points are measured under closely matched machine conditions.

At each context, candidates run from lower to higher expected memory pressure:

1. `OV-11` — U4 STANDARD;
2. `OV-TQ-22` — U8+TBQ3;
3. `OV-TQ-21` — U8+TBQ4;
4. `OV-12` — U8 STANDARD; and
5. `OV-13` — FP16 STANDARD.

Every candidate begins at context 512. A candidate that completes the current context may advance to 1024, then 2048, 4096, and 8192. A candidate with a confirmed boundary is removed from all higher levels. No intermediate level may be skipped.

The formal performance workload uses the same frozen prompt construction for every identity, pads to exactly the declared input context, and requests exactly four generated tokens. Context promotion depends on a runtime pass. A `quality-blocked` result does not prevent the runtime ladder from advancing, but that row is not fully comparable and cannot enter a quality ranking.

Each context follows this sequence:

1. guarded pilot;
2. warmup, excluded from aggregates;
3. samples 1–3;
4. governed P1–P6 quality capture, one fresh worker process per prompt; and
5. validation, reconciliation, and workbook/register update.

Existing equivalent boundary evidence may satisfy a stop decision only when the artifact, model, device, route, context, safety floor, and failure condition match. In particular, the three existing U8 STANDARD 4096 pilots may establish that boundary without another dangerous launch. Historical TurboQuant measurements are reference evidence only; they are not silently relabelled as new comparison-campaign samples.

## 4. Runtime measurements

A successful formal row must retain the three individual sample values and calculate at least the mean and median for:

- model load time in milliseconds;
- time to first token in milliseconds;
- prompt throughput in tokens per second;
- time per output token in milliseconds;
- decode throughput in tokens per second; and
- generation duration in milliseconds.

It must also record:

- peak working set and peak private memory in MiB;
- minimum available physical RAM in MiB;
- actual K/V allocation in MiB;
- input and generated token counts;
- CPU mean, median, and peak utilisation with sample count;
- GPU mean, median, and peak utilisation with sample count;
- requested and actual device;
- requested, activated, and observed K/V treatment/state;
- fallback count;
- process exit status;
- residual owned-process count; and
- artifact, prompt, matrix, build, command, and evidence hashes.

Units are explicit. MiB values must not be relabelled as decimal MB. Zero GPU utilisation is recorded only when supported by the GPU sampler; it is not inferred merely because CPU was requested.

Timing means and medians are calculated across the three accepted run-level values. For memory, the evidence retains each run's peak; the workbook reports the median and worst-case maximum peak, plus the minimum available RAM observed across all accepted events. Campaign CPU/GPU mean, median, and peak are calculated from all timestamped utilisation samples across the three accepted runs, and the total sample count is shown. K/V allocation and token counts are retained per run and checked for consistency before aggregation.

## 5. Quality measurement

Every runtime-complete configuration/context receives a separately governed quality campaign using the frozen P1–P6 prompt set and `GTQ-QUALITY-RUBRIC-v1`.

The controlling dimensions and weights are:

- correctness and grounding — 30%;
- instruction and format adherence — 25%;
- completeness and fact retention — 20%;
- relevance, clarity, and coherence — 15%; and
- stability and output integrity — 10%.

Deterministic gates run before subjective adjudication. Critical failures apply the rubric’s caps. Configuration labels remain hidden during scoring, and no precision or repository receives a bonus. Pairwise subjective review uses both presentation orders. Critical-gate failures, judge disagreements greater than one point, and ranking reversals require manual adjudication.

The workbook records P1–P6 individually plus mean, median, minimum, and maximum quality scores. A numeric quality result exists only after exactly six valid prompt adjudications. Partial campaigns are labelled `quality-blocked` with their terminal evidence and are excluded from quality rankings.

## 6. Pass and boundary definitions

**Runtime pass** requires:

- one excluded warmup and three accepted formal samples;
- valid output with the controlled token counts;
- matrix-consistent device and activation evidence;
- no fallback;
- zero surviving owned processes; and
- no breach of the 2,048 MiB available-RAM floor.

**Quality scored** requires complete, validated P1–P6 outputs and adjudications.

**Fully comparable** requires both a runtime pass and a quality score at the same context.

Two boundaries are reported separately:

- **runtime-capable boundary:** the highest context with a runtime pass; and
- **fully comparable boundary:** the highest context with both runtime and quality completion.

The highest working weight precision is determined from the STANDARD deployment ladder at a common context. The maximum safe context is determined independently for each format. Artifact preparation, runtime resource exhaustion, activation mismatch, invalid output, timeout, and quality resource exhaustion are distinct terminal categories.

## 7. Safety and error handling

Only one model worker may run at a time. Before every launch, the controller verifies:

- at least 4,096 MiB available physical RAM;
- the expected artifact and build hashes;
- a clean campaign destination; and
- zero surviving test-owned processes.

The existing 2,048 MiB runtime emergency stop remains mandatory and cannot be bypassed. Every worker has a timeout, memory sampler, CPU/GPU sampler, and owned-process cleanup guard. The controller may terminate only processes it owns; it must not kill VS Code, user applications, or unrelated system processes.

After a clean guarded RAM-floor or functional failure, the controller waits for cleanup and may retry once only if preflight conditions recover. Two matching guarded failures confirm the boundary. A second attempt is prohibited after OS instability, incomplete cleanup, or failure to restore the 4,096 MiB launch reserve; the first event and its telemetry then establish the safety boundary. Existing repeated equivalent evidence avoids redundant reruns.

Quality prompts use fresh isolated worker processes so memory and model state cannot leak between P1–P6. A failed prompt does not erase prior evidence, but it prevents a numeric aggregate until the complete campaign succeeds.

## 8. Evidence and update flow

Every attempt receives a new immutable directory containing:

- controller and worker specifications;
- exact command and environment identity;
- stdout and stderr;
- memory events;
- CPU and GPU samples;
- activation and observed-state proof;
- token counts and generated output;
- cleanup evidence; and
- file hashes.

After each successful or terminal configuration/context, validators recompute aggregates directly from raw samples, verify identity and hashes, and reconcile the row. Only then are the Markdown workbook, generated DOCX, and affected CSV registers updated. Existing raw evidence is never overwritten, and a resumed run may append only through the controlled resume contract.

## 9. Workbook presentation

WB-04 will advance to revision v1.9 under register entry `WR-037` and will contain these readable sections:

1. a fair U8 STANDARD/TBQ4/TBQ3 comparison at shared contexts;
2. a separate U4/U8/FP16 STANDARD deployment comparison;
3. complete timing metrics;
4. complete memory and CPU/GPU utilisation metrics;
5. per-prompt and aggregate quality results;
6. a laptop-boundary table showing the highest completed and first confirmed blocked context per format; and
7. a short failed-test list containing only the stage, principal reason, and evidence reference.

Successful tables contain no blank cells or placeholders. Failed configurations are not padded with fabricated metrics or `N/A` cells; they appear in the compact terminal list. Runtime-only rankings and quality-complete rankings remain separate. No overall winner is declared unless candidates share a context and have complete P1–P6 evidence.

## 10. Verification

Verification occurs at two independent layers:

1. the campaign controller validates each raw attempt against the comparison matrix and recomputes its summary; and
2. the release audit independently reads raw evidence, summaries, workbook tables, DOCX structure, and registers and verifies exact agreement.

Automated coverage must include:

- comparison-matrix schema and identity rules;
- generation of all five identities and five declared contexts;
- adaptive promotion and stop behaviour;
- RAM preflight and emergency-stop behaviour;
- retry limits and owned-process cleanup;
- three-sample mean and median calculations;
- complete timing, memory, CPU, GPU, token, activation, and quality fields;
- blind quality scoring and critical caps;
- evidence and reconciliation hashes;
- zero blank successful-result cells;
- compact terminal-row rendering; and
- synchronized Markdown, DOCX, and affected CSV registers.

Focused tests and the full repository test suite must pass before release. DOCX structural audit is mandatory. Rendered-page inspection is required when a compatible renderer is available; if no renderer is available, the release notes must state that limitation rather than claim visual QA.

## 11. Acceptance criteria

The work is complete when:

1. the comparison matrix and adaptive controller are validated;
2. U4 STANDARD, U8 STANDARD, U8+TBQ4, and U8+TBQ3 have each been attempted formally from context 512 upward under the approved stop rules;
3. FP16 has either completed the same ladder or has a precise, evidenced artifact-preparation or runtime boundary;
4. every successful row contains the complete runtime and utilisation metric set;
5. every runtime-complete row has a completed quality score or a precise governed quality boundary;
6. the laptop’s highest working STANDARD weight precision and maximum safe context per format are stated without conflating artifact and runtime failures;
7. fair cache comparisons use only the identical U8 weight artifact at common contexts;
8. all workbook values match raw evidence and independently recomputed aggregates;
9. successful tables have zero blank cells and terminal attempts are presented compactly;
10. all affected repository registers and the DOCX are synchronized; and
11. focused and full repository tests pass.

The implementation must not claim that all formats passed, invent unavailable metrics, lower the RAM guard, or declare a quality winner from incomplete evidence.
