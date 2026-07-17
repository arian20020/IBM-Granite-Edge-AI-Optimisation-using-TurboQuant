# App-Specific Evaluation Addendum

**Document ID:** `EVAL-ADDENDUM-v1`  
**Version:** 1.0  
**Status:** Frozen controlled baseline  
**Effective date:** 14 July 2026  
**Related work package:** `PD-09`  
**Related engineering practice:** `EP-019`

## 1. Purpose

This addendum freezes the project-specific evaluation work that remains after the initial feasibility investigations. It connects the research questions to controlled experiment or evaluation IDs, evidence schemas, metrics, prompt and rubric versions, entry and exit gates, stopping rules and claim boundaries.

It supplements, but does not replace:

- `Test-Strategy.md`;
- `Master-Test-Plan.md`;
- `Test-ID-Catalogue-v1.1.md`;
- the six controlled route workbooks;
- the machine-readable testing registers;
- the final requirements and acceptance testing planned under `FR-01` to `FR-05`.

## 2. Frozen evaluation identifiers

### 2.1 Existing controlled runtime and codec campaign

The controlled route campaign retains **224 unique test IDs**. Those IDs remain governed by `Test-ID-Catalogue-v1.1.md` and the route workbooks.

| Evaluation family | Controlled IDs | Main research questions | Main purpose |
|---|---|---|---|
| Upstream llama.cpp baseline | `UL-B01`–`UL-B07`; `UL-01`–`UL-13` | `RQ1`, `RQ2`, `RQ3` | Establish reproducible Granite baseline behaviour, device placement, memory, performance, quality and stability. |
| AtomicBot TurboQuant | `AB-B01`–`AB-B08`; controlled `AB-*` formal IDs | `RQ1`, `RQ2`, `RQ3` | Verify TurboQuant activation and compare it with matched conventional cache baselines. |
| animehacker TQ3_0 | `AH-B01`–`AH-B08`; `AH-01`–`AH-10` | `RQ1`, `RQ2`, `RQ3` | Evaluate the comparator implementation, activation, memory, performance, quality and limitations. |
| Official OpenVINO | `OV-B01`–`OV-B12`; `OV-C01`–`OV-C06`; `OV-01`–`OV-10`; `OV-TQS-01`–`OV-TQS-12`; `OV-TQ-01`–`OV-TQ-20` | `RQ1`, `RQ2`, `RQ3` | Evaluate official Granite/OpenVINO support, TBQ3/TBQ4 capability, fallback and unsupported QJL/Polar boundaries. |
| Experimental OpenVINO codecs | `OVT-B01`–`OVT-B15`; `OVT-A01`–`OVT-A12`; `OVT-S01`–`OVT-S36`; `OVT-01`–`OVT-36` | `RQ1`, `RQ2`, `RQ3` | Evaluate TBQ3, TBQ4, QJL and PolarQuant paths, including all ordered K/V codec pairs. |
| Cross-route comparison | WB-06 comparison rows referencing validated route test and run IDs | `RQ1`, `RQ2`, `RQ3` | Compare only matched or explicitly qualified configurations and support bounded conclusions. |

### 2.2 App-integrated evaluation addendum IDs

The following IDs are separate from the 224 route-workbook IDs. They control the later application evaluation and must be used in test reports, run records and evidence folders.

| Addendum ID | Evaluation | Research question(s) | Related work package(s) | Required evidence and decision |
|---|---|---|---|---|
| `APP-EVAL-01` | Model import, download hand-off and inspection | `RQ4` | `IM-02`–`IM-07`, `DL-01`–`DL-03` | Valid, invalid, corrupt, unsupported and conversion-required cases; expected state, reason and recovery action. |
| `APP-EVAL-02` | Hardware snapshot and memory-estimate correctness | `RQ3`, `RQ4` | `HE-01`, `HE-03`, `HE-04` | Captured hardware manifest, estimate components, measured comparison, uncertainty and false-safe/false-unsafe analysis. |
| `APP-EVAL-03` | Candidate generation and mode-selection correctness | `RQ3`, `RQ4` | `HE-02`, `HE-05`–`HE-07` | Supported-combination matrix, rejected candidates, selected mode, explanation and boundary tests. |
| `APP-EVAL-04` | Secure local single-turn inference without a required network port | `RQ1`, `RQ4` | `RT-01`, `RT-02` | Exact child-process command, no-port/network observation, valid response, stdout/stderr and cleanup evidence. |
| `APP-EVAL-05` | Streaming, cancellation, retry and process cleanup | `RQ4` | `RT-03`, `RT-05` | Timed interaction trace, cancellation result, child-process state, temporary-file state and failure recovery. |
| `APP-EVAL-06` | Multi-turn context and displayed metrics | `RQ4` | `RT-04` | Two-turn transcript, context behaviour, generated-token and timing fields, and consistency with raw runtime output. |
| `APP-EVAL-07` | Actual backend, device and optimisation disclosure | `RQ1`, `RQ2`, `RQ4` | `RT-01`–`RT-05`, `QX-03`, `OV-02` | Requested versus actual backend/device/cache/optimisation, fallback detection and user-visible disclosure. |
| `APP-EVAL-08` | Offline, privacy-boundary and path-safety checks | `RQ4` | `FR-02` | Network observation, sensitive-path tests, command-argument safety, temporary/log file review and residual limitations. |
| `APP-EVAL-09` | Education and healthcare usability and accessibility tasks | `RQ4` | `FR-02` | Defined participant/task scope, completion, errors, recovery, keyboard use, 200% scaling, feedback and limitations. |
| `APP-EVAL-10` | App-integrated AI output quality and instruction following | `RQ2`, `RQ4` | `FR-03` | Frozen `GTQ-PROMPTS-v1`, `GTQ-QUALITY-RUBRIC-v1`, deterministic gates, blind scoring and per-prompt results. |
| `APP-EVAL-11` | Clean build, installation and reproduction | `RQ4` | `FR-04` | Clean-environment build/install logs, dependency versions, package hash, two successful demonstrations or a reproducible blocker. |
| `APP-EVAL-12` | Final regression, requirement acceptance and bounded project conclusions | `RQ1`, `RQ2`, `RQ3`, `RQ4` | `FR-01`, `FR-03`, `FR-05` | Final RTM statuses, cross-route comparison, unresolved failures, known limitations, evidence audit and release checksum. |
| `EXP-TQ-COMP-001` | Matched TurboQuant versus standard KV-cache comparison | `RQ2`, `RQ3` | `FR-03`, `QX-04`, `OV-03` | Same model, weights, prompts, context, hardware and measurement method; activation proved; differences reported. |
| `EXP-TV-COMP-001` | Matched TurboVec versus uncompressed-vector comparison | Exploratory TurboVec question | `TV-02`–`TV-04` | Same documents, chunking, embeddings and queries; storage, memory, timing, relevance, answer usefulness and failures reported. |

No addendum ID may be renamed or reused. A physical run must use a unique run identifier and must not overwrite an earlier failed, blocked or inconclusive run.

## 3. Frozen evaluation inputs

| Input | Frozen identifier or controlling source |
|---|---|
| Project problem, aim, RQs and objectives | `docs/planning/Project-Definition-v1.md` |
| Runtime/codec test catalogue | `docs/testing/Test-ID-Catalogue-v1.1.md` |
| Prompt set | `GTQ-PROMPTS-v1` in `experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json` |
| Quality rubric | `GTQ-QUALITY-RUBRIC-v1` in `experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json` |
| Test strategy | `docs/testing/Test-Strategy.md` |
| Stage gates and stopping rules | `docs/testing/Master-Test-Plan.md` |
| Per-run operational checklist | `docs/testing/Execution-Checklist.md` |
| Experiment manifest | `experiments/manifests/experiment-manifest-template.json` and controlled route manifests |
| Route plans and data fields | Controlled workbook templates and `Workbook-Data-Requirements.md` |

Changing any frozen input requires a new version or a documented change decision. Existing evidence remains tied to the version used when it was collected.

## 4. Frozen evidence schemas

Every applicable execution must populate the relevant controlled records below. Blank schemas are preparation; they become evidence only when linked to preserved artefacts.

| Schema | Required purpose |
|---|---|
| `Test-Traceability-Matrix.csv` | Maps RQs, objectives, requirements/risks, test IDs, expected evidence and claim boundaries. |
| `Test-Run-Register.csv` | One row for every physical execution and retest. |
| `Environment-Register.csv` | Machine, OS, hardware, drivers, toolchain, power and capture state. |
| `Repository-Register.csv` | Exact source repository, branch, commit, licence and local state. |
| `Build-Register.csv` | Configure/build commands, options, outputs, warnings and result. |
| `Model-Register.csv` | Model source, revision, format, size and SHA-256. |
| `Configuration-Register.csv` | Requested model, weights, K/V cache, context, device, sampling and comparison group. |
| `Device-Verification-Register.csv` | Requested versus actual backend, device, placement, fallback and proof path. |
| `Performance-Measurement-Register.csv` | Pilot, excluded warm-up and measured repetitions with units and definitions. |
| `Quality-Evaluation-Register.csv` | Prompt/rubric IDs, deterministic gates, weighted scores, adjudication and evidence. |
| `Failure-Register.csv` | Failure, diagnosis, fix/workaround, retest and final status. |
| `Evidence-Index.csv` | File-level path, size, SHA-256, origin, processing and validation state. |
| `Workbook-Completion-Register.csv` | Workbook section, run ID, raw/processed paths, evidence commit and reviewer state. |
| `Cross-Route-Comparison-Register.csv` | Equivalence checks, matched values, differences, quality, stability and bounded conclusion. |

For app-integrated evaluations, the test report must additionally record the application commit, package/build identity, exact user task, expected UI state, observed UI state, screenshots or recording where appropriate, and links to the underlying runtime evidence.

## 5. Frozen measures

The measure selected depends on the evaluation ID, but definitions and units must be fixed before a formal run.

- compatibility/build result and reproducible blocker classification;
- requested versus actual runtime, device, layer placement, KV-cache placement and optimisation state;
- process and system memory in bytes, including peak definitions;
- context length in tokens;
- prompt-processing and generation speed in tokens per second;
- latency and time to first token in milliseconds or seconds, with the definition stated;
- stability across repeated runs;
- deterministic prompt checks;
- weighted quality score from 0 to 10, plus per-prompt results;
- import/classification accuracy over the controlled fixture set;
- memory-estimator error, false-safe and false-unsafe outcomes;
- task completion, critical error, recovery and accessibility observations for app/UX evaluation;
- storage, indexing/query timing and relevance for TurboVec, where retained in scope.

Published claims, estimates, measured values and calculated values must remain visibly separate.

## 6. Entry, continuation and exit rules

### Entry gate

An evaluation may move to `Ready` only when:

- its ID exists in this addendum or the controlled runtime catalogue;
- the RQ, purpose, preconditions, configuration and pass/decision rule are recorded;
- the required manifests and evidence directory exist;
- model, executable, source and application identities are pinned where applicable;
- prompt and rubric versions are recorded for quality work;
- expected backend/device/optimisation proof and safety limits are stated.

### Continuation gate

A dependent evaluation continues only when its prerequisite passed or an explicit documented decision permits continuation. A later success must not hide an earlier prerequisite failure.

### Exit gate

An evaluation is complete only when:

- the run is classified;
- raw evidence is preserved without rewriting;
- the relevant schemas and workbook/test report are updated;
- processed values can be regenerated or checked from raw evidence;
- the evidence commit or independently backed-up location is recorded;
- the conclusion stays within the tested configuration and documented limitations.

## 7. Stopping rules

Stop and preserve the evidence when any of the following occurs:

- unsafe memory, thermal, storage or system-pressure threshold;
- repeated crash, hang or instability;
- corrupted, incomplete or untraceable evidence;
- unverified fallback or inability to prove requested optimisation/device state;
- prerequisite failure that invalidates later measurements;
- test-environment drift that breaks comparability;
- privacy, path-safety or command-injection concern;
- participant discomfort or consent withdrawal during usability work.

A stopped run is classified as failed, blocked or inconclusive with a reason. It is not deleted.

## 8. Claim boundary

This addendum proves that the remaining evaluation work is identified, mapped and controlled. It does not prove that the application, Granite model, Intel device, TurboQuant implementation, OpenVINO codec, QJL path, PolarQuant path or TurboVec path has passed its later execution tests.

Results may be claimed only after the matching ID has preserved raw evidence, populated records, validation and a bounded conclusion.

## 9. Change control

A later change to an evaluation ID, RQ mapping, prompt, rubric, metric, schema, gate or stopping rule must update:

1. this addendum or a versioned successor;
2. the relevant test strategy, catalogue, workbook or register schema;
3. the `PD-09` and `EP-019` evidence records;
4. the controlled RTM and regenerated traceability snapshot;
5. the project change log and affected report section.
