# animehacker TQ3_0 Controlled Retest Design

## Purpose

Complete WB-03 as a fresh, evidence-backed retest of the animehacker `llama-turboquant` repository on the target Windows laptop. The campaign covers controlled IDs `AH-B01` through `AH-B08` and `AH-01` through `AH-10`, measures every feasible required metric, proves actual backend and TQ3_0 state, scores quality without format bias, and leaves no blank or `N/A` workbook cell.

## Repository and scope control

- Clone the latest commit from the repository's default branch at campaign start and record the exact branch, commit SHA, remote URL, submodule state, upstream-base evidence, and source-tree hash evidence.
- Preserve recovered historical material as context only. It cannot substitute for fresh build or runtime evidence.
- Test SYCL as WB-03's controlled Intel GPU route.
- Test Vulkan only when the pinned source and build system expose a genuine Vulkan path. Record it as supplementary evidence, not as a substitute SYCL pass.
- Use WSL only after a documented native-Windows prerequisite failure and only when the result clarifies repository capability.
- Execute all eight build/setup IDs and all ten formal IDs. No ID may disappear because its preferred backend is unsupported.

## Model and configuration controls

- Use the model identities and weight precisions defined by the controlled workbook: diagnostic Gemma 1B Q4_K_M, Granite 3B BF16, and Granite 8B Q8_0, subject to hash verification against available controlled artifacts.
- If an exact controlled artifact is absent, reacquire and hash it when licensing and storage permit. If reacquisition is impossible, retain the row with an explicit `Blocked - exact model artifact unavailable` classification and evidence.
- Freeze context, cache type, device, thread count, batch settings, seed, temperature, prompt, generation length, warm-up policy, and repetition count before formal measurements.
- Treat accepted command-line flags as configuration intent only. TQ3_0 activation requires runtime cache allocation/type evidence or source-backed proof tied to the executed binary.
- Preserve host-versus-device KV placement, offloaded-layer count, and any silent fallback evidence separately.

## Execution architecture

Create one route-specific controller with these responsibilities:

1. Load and validate a version-controlled matrix containing exactly the 18 controlled IDs.
2. Record command, working directory, environment, repository commit, binary hash, model hash, and safety-gate inputs before launch.
3. Own the launched process group and terminate the complete process tree on completion, timeout, emergency stop, or interruption.
4. Write state atomically after every pilot, warm-up, formal repetition, quality prompt, and reconciliation step.
5. Resume only from validated terminal evidence; incomplete or corrupt samples must rerun.
6. Run one model process at a time and prevent duplicate controllers.

Each formal runtime configuration uses one pilot, one excluded warm-up, and three measured repetitions. High-memory 8B rows run alone from an idle state with a conservative available-RAM reserve and an emergency-stop floor. A safety stop is evidence of a blocked attempt, never a pass.

## Required measurements

For every successfully launched formal configuration, retain per-repetition and aggregate values for:

- peak process-tree working set in bytes and MiB;
- peak private bytes when the platform collector exposes it;
- KV/cache allocation in bytes and MiB;
- TTFT from HTTP request initiation to first generated token;
- prompt processing rate when emitted;
- decode rate and total generation duration;
- CPU utilization mean, median, and peak, normalized across logical processors;
- GPU utilization mean, median, and peak from GPU Engine instances attributable to the active model PID;
- GPU dedicated/shared memory when exposed by a reliable platform counter;
- cleanup, stability, timeout, and fallback status.

GPU utilization at each timestamp is the busiest attributable process engine, matching the Windows engine model. Engines must not be summed because that can exceed 100%. CPU-only rows still sample GPU counters; a recorded 0% means zero was observed, not assumed.

The workbook's device table must show actual device, backend, offload level, layer placement, KV device, CPU fallback, and CPU/GPU mean-median-peak values. SYCL partial rows must state host-versus-SYCL memory and whether KV remained host-side.

## Quality and stability policy

- Execute frozen P1-P6 prompts independently for every runnable formal configuration.
- Store raw request, response, timing, exit status, model/configuration identity, and response hash per prompt.
- Apply deterministic gates first, followed by harsh content-based rubric scoring.
- Do not grant a quality advantage based on F16, Q8_0, TQ3_0, CPU, SYCL, or Vulkan labels.
- A launched configuration that times out, returns an empty response, corrupts required structure, or omits critical required facts receives the rubric-defined penalty, including zero where applicable.
- A configuration blocked before inference does not receive invented model-quality measurements. Its quality cells contain `Blocked - <specific prerequisite>` and link to the blocking evidence; a supplementary fallback quality result may be shown in a separate labelled field.
- Run bounded repeatability and failure checks and preserve every failed attempt in the failure register.

## No-blank and no-N/A classification contract

Every workbook cell must contain one of:

1. A directly measured or observed value with an evidence path.
2. `0` only when zero was directly measured or the scoring rubric assigns zero to an executed response.
3. `Blocked - <specific prerequisite failure>` when execution cannot begin or cannot safely continue.
4. `Unsupported - <source/build/runtime proof>` when the pinned implementation lacks the requested capability.
5. `Not applicable - <structural reason>` only when the field genuinely does not apply to that test type, such as model latency for a build-only ID.

The literal token `N/A`, empty strings, generic `Not measured`, and inferred numeric replacements are forbidden in the completed WB-03 workbook. Blocked and unsupported classifications must include specific evidence and cannot be counted as passes.

## Evidence and data flow

Evidence flows through:

`matrix -> preflight gate -> raw run folder -> validated summary -> central registers -> WB-03 Markdown -> generated DOCX -> manifest hashes`

Raw logs are immutable. Processed summaries are reproducible from raw files. Every indexed evidence file records repository-relative path, size, SHA-256, timestamp, run ID, test ID, derivation, and validation status. Workbook values must map to a raw or processed source, and an independent reconciliation pass must recompute all reported metrics.

The controller updates the raw state and processed summary immediately after each terminal step. Central registers and workbook source update after each validated test row, limiting data loss if Windows sleeps or the laptop restarts.

## Failure handling

- Read and classify the complete error before changing commands or dependencies.
- Record dependency, build, Windows, model, architecture, activation, fallback, crash, CPU, GPU, hybrid, OOM, memory, performance, quality, reproducibility, and scope failures with stable IDs.
- Do not retry unchanged commands repeatedly. Each retry must test a documented hypothesis.
- Stop after repeated equivalent failures and reassess the prerequisite or architecture.
- Never bypass a safety gate by removing memory monitoring. A controlled retry may lower concurrency, close model processes, reduce supplementary scope, or raise the reserve, but cannot falsify the formal configuration.
- The completed campaign must contain zero unresolved failed tests. Any failing build, repository test, harness test, formal runtime, quality validator, evidence audit, or workbook-control check must be root-caused, corrected where the pinned source and environment permit, and rerun successfully before completion.
- `Blocked` and `Unsupported` are not escape labels for failures. They require evidence that execution cannot validly begin because a prerequisite or capability is absent. A test that begins and produces an incorrect result remains failed until fixed and rerun successfully.

## Workbook presentation

WB-03 will contain:

- repository/environment and exact dependency records;
- all AH-B01-AH-B08 results and evidence;
- all AH-01-AH-10 classifications;
- complete device placement and utilization values;
- per-test performance/resource results;
- activation, implementation-depth, and QJL findings;
- P1-P6 quality scores per runnable row and bounded aggregate conclusions;
- failure log with resolved/unresolved status;
- final integration/research decision with tested boundaries;
- evidence summary and controlled revision history.

Supplementary Vulkan or WSL results must be visibly separated from the controlled SYCL matrix.

## Completion gates

The route is complete only when:

- all 18 controlled IDs have a final classification;
- no in-scope test remains in a failed or unresolved state;
- every runnable formal row has one pilot, one excluded warm-up, and three valid measured repetitions;
- all required resource and utilization fields contain sourced measurements or explicit blocked/unsupported classifications;
- every runnable formal row has independent P1-P6 quality evidence;
- no WB-03 cell is blank or contains the literal token `N/A`;
- TQ3_0 activation and QJL state are source/runtime evidenced;
- actual CPU/SYCL/Vulkan placement and fallback are reconciled;
- evidence paths and hashes validate;
- the generated DOCX matches the manifest and embedded revision history;
- repository validators, harness tests, semantic audits, and Git diff checks pass;
- the final DOCX receives page-by-page render review when a compatible renderer is available, otherwise the unavailable renderer is explicitly recorded as a verification limitation.
