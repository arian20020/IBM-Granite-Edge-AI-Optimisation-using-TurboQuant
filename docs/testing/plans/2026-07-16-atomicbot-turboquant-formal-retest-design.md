# AtomicBot TurboQuant Formal Retest Design

Date: 2026-07-16  
Workbook: `WB-02`  
Target repository: `https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant.git`  
Target branch: `feature/turboquant-kv-cache`  
Verified branch tip at design time: `519f0c594a8e31467d2e2f2cf17054c9e7e11536`

## Objective

Execute a fresh, controlled retest of the AtomicBot TurboQuant route and completely populate the AtomicBot controlled retest workbook with traceable, reproducible evidence. Historical results may be used to identify failure risks and comparison points, but no historical measurement may be copied into the new execution result.

Complete means that every result field contains one of:

- a freshly measured and cross-checked value;
- `N/A` with a precise protocol reason;
- `Unsupported` with repository or runtime evidence; or
- `Blocked` with a failure code, evidence reference, and recovery requirement.

An unexplained blank is not a completed field. A fabricated or inferred measurement is not an acceptable substitute for a missing measurement.

## Execution boundary

The controlled scope is the current `WB-02` workbook, the Master Test Plan, metric definitions, execution checklist, failure-code catalogue, and the AtomicBot repository-specific research and testing plan. The target commit is pinned in the run manifest before configuration or build.

The retest covers:

- clean CPU and Vulkan build/configuration gates;
- all formal `AB-*` rows in the workbook;
- F16, Q8 and supported TurboQuant formats specified by the workbook;
- supported model sizes and context lengths, subject to explicit safety gates;
- CPU and Vulkan placement verification;
- prompt-processing speed, decode speed, TTFT, peak process-tree working set, runtime-reported KV allocation, and GPU memory where applicable;
- output quality, perplexity supplements where a controlled dataset is available, and stability; and
- build/runtime failures and unsupported paths.

## Repository and branch strategy

The test work lives on `testing/atomicbot-turboquant-formal-retest`, created from freshly fetched `origin/main`. The AtomicBot source is obtained in an isolated test location and pinned to the manifest commit. Generated build directories, model weights, caches, and other large or unlicensed artifacts are not committed.

CPU and Vulkan builds use separate clean build directories. Vulkan configuration records the SDK and generator inputs explicitly and disables optional UI embedding when necessary so a UI asset failure cannot be mistaken for a backend failure. Backend availability and actual placement must be proven at runtime; successful compilation alone is insufficient.

## Harness design

A repository-owned runner orchestrates the matrix and supports:

- dry-run matrix inspection;
- individual test selection and bounded ranges;
- checkpointing after each repetition and row;
- safe resume after interruption or machine restart;
- per-process timeouts;
- process-tree termination and cleanup;
- disk-space and available-memory preflight checks;
- guarded high-memory rows;
- immutable raw logs and structured processed summaries; and
- deterministic workbook generation or update from the structured result source.

The state file is written atomically. A row is marked complete only after raw evidence, parsed metrics, and validation status have been persisted. Re-running a completed row requires an explicit replacement mode and retains provenance for the superseded attempt.

## Measurement protocol

Each applicable performance configuration receives one diagnostic pilot, one excluded warm-up, and at least three measured repetitions unless a documented safety gate blocks execution.

### TTFT

TTFT is measured from submission of the complete prompt to receipt of the first generated token from an already loaded server. The runner uses a streamed response and monotonic high-resolution timestamps. Model load time is recorded separately and is not folded into TTFT. The evidence records whether tokenization and prefill are included.

### Memory

Peak RAM is the maximum physical working set of the complete runtime process tree during the controlled interval, sampled at 100 ms or faster. The runner also records peak private bytes and minimum available system RAM when available. Baseline and sampling bounds are retained so the result can be audited.

KV-cache allocation is taken only from runtime allocation output or a documented runtime property. It is never inferred from working-set growth. Duplicate log reports for the same allocation are deduplicated before aggregation.

GPU memory records dedicated and shared memory separately when the platform exposes both. If the device/runtime cannot expose a defensible value, the field is `N/A` with the collector limitation rather than a guessed value.

### Throughput

Prompt-processing and decode throughput use runtime token counts and timings. Compact and verbose timing formats are both parsed and tested. Metrics from unmatched prompts, contexts, models, formats, backends, or repetition classes are never combined.

The workbook reports the median and range of valid measured repetitions and preserves every repetition in processed evidence.

## Quality and perplexity

Quality uses the repository-wide `GTQ-QUALITY-RUBRIC-v1`, including deterministic gates, critical caps, and raw-output retention. The same prompts, generation controls, adjudication rules, and baseline are used for every compared format. Scoring is evidence-based and conservative: precision labels do not receive an assumed quality advantage, while formatting errors, contradictions, instruction failures, factual failures, repetition, corruption, and incomplete answers are penalized consistently.

Automated checks and human adjudication are stored separately. The final score is recalculated from recorded component scores and independently checked before workbook publication.

Perplexity comparisons require a named, versioned, locally reproducible dataset with a recorded hash and identical evaluation settings. If no compliant dataset is available, affected cells are marked `Blocked` with the missing-fixture reason; an unrelated proxy value is not substituted.

## Safety and crash resistance

Before every high-memory row, the runner checks available physical memory, commit headroom, disk space, model size, and expected KV allocation. Guarded rows such as the large-context 8B cases execute only when the workbook gate is satisfied. A safety refusal is a valid blocked outcome with evidence, not permission to leave cells blank.

The runner limits concurrency to one model execution, applies bounded timeouts, streams logs to disk, checkpoints promptly, and terminates the entire spawned process tree after success, failure, timeout, or interruption. Vulkan and CPU smoke tests precede the full matrix. Repeated failure signatures are added to failure evidence and used to improve subsequent commands.

## Evidence and workbook flow

For each row, evidence is handled in this order:

1. record the exact command, environment, commit, model hash, and start time;
2. stream immutable raw stdout, stderr, responses, and sampler data;
3. parse into a structured per-repetition result;
4. validate units, bounds, repetition identity, and required source fields;
5. cross-check parsed metrics against raw evidence;
6. checkpoint the row state;
7. update the processed result and workbook immediately; and
8. validate the workbook row before advancing.

The workbook is not the sole data store. Machine-readable processed results are the calculation source; the workbook is a reviewed presentation of those results. The evidence index, run register, failure register, revision register, controlled workbook manifest, and route README are updated as applicable.

## Accuracy controls

Accuracy is protected by:

- schema validation for all structured results;
- unit normalization at ingestion with original values retained;
- rejection of ambiguous parser matches;
- independent recalculation of medians, ranges, and quality totals;
- workbook-to-source reconciliation for every populated result cell;
- manifest hashes for committed evidence and generated workbook artifacts;
- explicit device/backend placement evidence;
- anomaly review against historical results without forcing agreement; and
- a final no-blank and allowed-status audit.

Any unexplained discrepancy is resolved against raw evidence before publication. If it cannot be resolved, the result is blocked and documented rather than reported as certain.

## Completion gates

The retest is complete only when:

- every workbook row and field satisfies the completion definition;
- every applicable test has a passing result, or a truthful unsupported/blocked disposition where passing is technically impossible or unsafe;
- all reported measurements reconcile to raw evidence;
- quality and perplexity calculations have been independently recalculated;
- failures have codes, logs, impact, and recovery notes;
- CPU/Vulkan placement claims are supported by runtime evidence;
- generated workbook artifacts pass structural and visual validation;
- registers, manifests, and guidance are current; and
- the complete repository validation suite passes.

The process will not redefine a genuine product failure as a pass merely to satisfy the matrix. Product or harness defects are diagnosed and corrected when in scope, then the affected row is rerun with new evidence.
