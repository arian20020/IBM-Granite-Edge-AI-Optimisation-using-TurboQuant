# Official OpenVINO WB-04 Formal Retest Design

## Goal

Complete WB-04 as a fresh, evidence-backed retest of the latest compatible official OpenVINO and OpenVINO GenAI releases on the target Windows Intel laptop. Every controlled build, conversion, baseline, capability, formal TurboQuant, device, performance, memory, utilization, quality, failure, and decision field must be populated from validated evidence without precision bias or fabricated measurements.

## Frozen Official Sources

- OpenVINO release: `2026.2.1`, official signed tag and Python package.
- OpenVINO GenAI release: `2026.2.1.0`, official signed tag and compatible Python package.
- Source repositories: `https://github.com/openvinotoolkit/openvino` and `https://github.com/openvinotoolkit/openvino.genai`.
- Record the full tag commit SHA, remote URL, clean checkout status, submodule state, package hashes, installed package versions, and source-tree evidence at acquisition time.

The release packages are the primary runtime route. Tagged source is retained for codec/API inspection and official diagnostics. Compile a tagged component only if a workbook-required diagnostic is unavailable from the release packages and the build can be isolated without changing the frozen runtime identity.

## Controlled Scope

The campaign covers:

- build/setup gates `OV-B01` through `OV-B12`;
- model conversion gates `OV-C01` through `OV-C06`;
- standard baselines `OV-01` through `OV-10`;
- official codec sweep `OV-TQS-01` through `OV-TQS-12`;
- formal tests `OV-TQ-01` through `OV-TQ-20`;
- every configuration, device/fallback, formal performance, quality, context/stability, failure, and final-decision field in WB-04.

The matrix definitions are frozen before formal execution. QJL and PolarQuant remain negative official-route capability tests unless the pinned releases expose and execute them. A configuration property being accepted is not activation proof.

## Environment and Acquisition

Create a campaign-specific Python 3.11 virtual environment. Pin OpenVINO, OpenVINO GenAI, model-conversion dependencies, numerical packages, and measurement dependencies. Record `pip freeze`, wheel metadata and hashes, Python executable/version, compiler/CMake inventory, Intel driver/device inventory, operating-system state, and installed/available physical memory.

Clone both official repositories at their exact release tags into campaign-specific external checkouts. Do not modify the tagged sources to manufacture support. Source inspection must identify the cache algorithm enum, key/value property names, supported precisions, CPU SDPA preconditions, norm-correction switch, packed-record calculations, fallback behavior, GPU boundary, and official unit/functional diagnostics.

## Model Preparation

Use official IBM Granite source revisions already approved by the project, recording repository, revision, licence, conversion command, conversion tool version, output tree, tokenizer/config validation, file sizes, and SHA-256 hashes. Produce or acquire the exact FP16, INT8, and INT4 OpenVINO forms required by `OV-C01` through `OV-C06`.

Each conversion is independently validated by loading the IR, inspecting its inputs/outputs and metadata, validating tokenizer/config files, and executing a short deterministic generation on CPU where safe. An unsupported conversion receives a sourced terminal classification; it is not silently replaced with a different precision.

## Execution Order

Run serially in this order:

1. source, package, environment, and device gates;
2. official diagnostics and codec source/API boundary audit;
3. conversion and model-load validation;
4. short codec capability sweep;
5. standard CPU baselines and GPU baseline/fallback gates;
6. Granite 3B formal TurboQuant matrix;
7. norm-correction ablations and context-scaling series;
8. repeatability/stability row;
9. guarded Granite 8B feasibility rows;
10. GPU, QJL, and PolarQuant negative/fallback gates;
11. P1-P6 quality, reconciliation, workbook generation, and final validation.

Each runnable formal configuration receives one pilot, one excluded warm-up, and exactly three accepted measured repetitions. A repetition is accepted only when request output, device/codec activation, all required timing and resource fields, and cleanup evidence are valid.

## Measurements

For every runnable formal configuration and each accepted repetition, capture:

- model load/compile time in milliseconds;
- TTFT from request initiation to first generated token;
- prompt throughput in tokens/second;
- TPOT in milliseconds/token;
- decode throughput in tokens/second;
- generation duration and token counts;
- peak working set and peak private memory in MiB;
- available physical RAM minimum in MiB;
- KV allocation in MiB and expected versus actual record bytes;
- GPU dedicated/shared/total memory where exposed;
- CPU utilization mean, median, peak, and sample count;
- GPU utilization mean, median, peak, and sample count;
- requested and actual device, backend, model placement, KV placement, optimization device, and fallback state.

Aggregates preserve mean, median, minimum, maximum, and source sample count for scalar metrics. Utilization aggregates preserve mean, median, peak, and sample count. Zero is valid only when directly measured; an unavailable signal must carry a precise sourced classification rather than a guessed value.

## Activation and Fallback Proof

For every codec request, store exact properties and environment variables, requested key/value algorithm and precision, runtime-verified algorithm and precision, expected and actual packed allocation, selected SDPA path, actual device, and relevant logs/profiling evidence. TBQ3/TBQ4 claims require both configuration and runtime evidence. GPU claims require actual GPU execution and non-ambiguous utilization or profiling evidence. Silent CPU fallback is reported as fallback, not GPU success.

## Quality Evaluation

Run the frozen P1-P6 prompt set after complete runtime evidence exists for `OV-TQ-01` through `OV-TQ-10` and any additional primary configuration required by the workbook decision. Preserve prompts, raw responses, response hashes, timing, model/config identity, and deterministic gate results.

Apply the fixed weighted 0-10 rubric harshly and consistently. Precision, algorithm name, expected capability, speed, and memory savings never increase a response score. Structural or deterministic failures apply their documented caps. Store P1-P6 scores plus arithmetic mean, median, minimum, maximum, caps, and adjudication notes.

## Safety and Recovery

Retain a 2,048 MiB minimum available-physical-RAM emergency floor for guarded Granite 8B runs and any earlier row whose pilot demonstrates comparable risk. Run one model at a time. Every launch has a timeout, process-tree ownership, checkpointing, and post-stop verification. A safety stop kills the full process tree, records preflight/events/measurement/cleanup evidence, and prevents quality execution.

Runs resume only from validated checkpoints. Existing outputs are never overwritten without an explicit replacement attempt and retained lineage. Laptop sleep, restart, controller interruption, or invalid partial output must not convert a row to complete.

## Evidence and Workbook Reconciliation

Store raw evidence under `experiments/raw-results/official-openvino/<campaign-date>/` using stable subdirectories for acquisition, environment, diagnostics, conversion, capability, runtime, quality, failures, and reconciliation. Every terminal row links to commands, logs, measurements, hashes, and cleanup evidence.

Update the Markdown WB-04 source immediately after each row passes reconciliation. Unsupported, safety-blocked, or negative-capability rows receive explicit sourced classifications in every applicable field. Do not leave blank cells or use a bare `N/A`. Do not mark a failed or incomplete run as passing merely to remove an empty cell.

After final evidence acceptance:

- update all WB-04 tables and final reasoning;
- update operational testing registers and the append-only workbook revision register;
- update the controlled workbook manifest and hashes;
- regenerate the WB-04 DOCX;
- verify ZIP integrity, required content, table dimensions, and zero blank cells;
- run WB-04 evidence reconciliation, workbook revision control, OpenVINO extension validation, and controlled-workspace validation.

## Automated Harness Requirements

Develop the harness test-first. Automated tests must prove exact matrix coverage, unknown/duplicate rejection, source/package pinning, activation parsing, device/fallback parsing, metric completeness, CPU/GPU mean-median-peak aggregation, resume semantics, process cleanup, safety-floor behavior, quality gating, harsh score independence from precision, evidence immutability, workbook completeness, and rejection of any row missing required statistics or quality evidence.

## Completion Boundary

WB-04 is complete only when every controlled ID has either validated passing evidence or an accurate sourced terminal classification, every required runtime row has the complete accepted repetition set, every required quality row has six adjudicated responses, all workbook cells are populated, all repository and controlled-workbook validators pass, and no unresolved test failure remains. Hardware or official capability limits remain explicit findings rather than being hidden or bypassed.
