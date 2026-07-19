# OpenVINO TurboQuant WB-04 Recovery Design

## Goal

Replace WB-04's terminal placeholders with genuine, measured Granite inference evidence. Preserve the unmodified OpenVINO `2026.2.1` route as the official baseline and create a separately identified project-patched OpenVINO build that implements independently selectable TBQ3 and TBQ4 KV-cache codecs. Record complete performance, memory, utilization, activation, stability, and quality evidence without describing project-added functionality as upstream-shipped capability.

## Provenance Boundary

The following source identities remain distinct throughout acquisition, build, execution, evidence, and workbook reconciliation:

1. **Official baseline:** unmodified OpenVINO `2026.2.1` and OpenVINO GenAI `2026.2.1.0` at the already pinned commits.
2. **Experimental merged route:** the same pinned upstream commits plus one exact project TurboQuant patch commit.

Every raw result and workbook row records the relevant upstream commits, patch commit, clean-state result, build identifier, executable/library hashes, and runtime package or binary identity. Experimental results must never be called capability shipped by upstream OpenVINO.

## Codec Architecture

The experimental route adds explicit `TBQ3` and `TBQ4` KV-cache algorithms to the OpenVINO GenAI CPU SDPA cache path. Key and value caches are independently selectable. Each encoded vector stores:

- its preserved norm;
- packed quantized coordinate indices;
- sufficient codec metadata to decode deterministically;
- exact allocation accounting that distinguishes payload and metadata bytes.

The first implementation boundary is TurboQuant's norm-preserving MSE codec. It must not claim QJL residual correction, PolarQuant, or GPU support unless those mechanisms are separately implemented and proven. Unsupported head dimensions, layouts, devices, attention paths, and prefill modes fail explicitly rather than silently using a standard cache.

Runtime telemetry exposes requested and activated key/value algorithms, precisions, expected and actual packed bytes, selected attention path, actual device, and fallback state. A configuration-property acceptance alone is not activation proof.

## Conformance Gates

Granite execution is prohibited until deterministic codec tests prove:

- TBQ3 and TBQ4 encode/decode correctness against frozen vectors;
- bounded reconstruction error using documented tolerances;
- norm preservation and restoration;
- bit packing/unpacking and exact byte calculations;
- independent and asymmetric K/V dispatch;
- deterministic results for fixed seeds and inputs;
- explicit rejection of unsupported shapes and paths;
- no unexplained fallback;
- memory safety and clean ownership/lifetime behavior.

The patched OpenVINO build must pass relevant repository tests and a short synthetic CPU SDPA activation test that observes real compressed-cache allocation.

## Model Preparation

Use the exact approved IBM Granite 3B and 8B revisions already frozen by WB-04. Prefer a verified pre-converted OpenVINO artifact only when its source revision, conversion command, tool versions, precision, tokenizer/config identity, complete file inventory, and hashes are independently provable. Otherwise perform local conversion serially.

Granite 3B conversion may start only when available physical RAM exceeds the validated requirement. Granite 8B execution requires a host that satisfies the existing 2,048 MiB emergency floor and the measured preflight requirement; it must not be forced on the 16 GiB laptop when the estimated requirement exceeds installed memory.

## Execution Flow

For each runnable WB-04 model configuration:

1. validate immutable source, build, model, tokenizer, prompt, rubric, and environment identities;
2. run a short pilot with activation, output, memory, and cleanup checks;
3. run one excluded warm-up;
4. run exactly three accepted measured repetitions serially;
5. reject and retain any invalid attempt without counting it toward the three samples;
6. run the frozen P1-P6 quality set only after runtime reconciliation passes;
7. update raw evidence, operational registers, and WB-04 immediately after acceptance.

Matched standard and TurboQuant comparisons use the same model revision, weight precision, context, prompt, sampling settings, token limit, device, and measurement method. Only the KV-cache algorithm or explicitly named ablation may differ.

## Required Measurements

Every accepted repetition records:

- model load/compile time;
- TTFT from request start to first generated token;
- prompt throughput;
- TPOT;
- decode throughput;
- generation duration and token counts;
- peak working set and private bytes;
- available physical RAM before, minimum during, and after;
- KV allocation and expected/actual packed record bytes;
- GPU dedicated/shared/total memory where exposed;
- CPU utilization mean, median, peak, and sample count;
- GPU utilization mean, median, peak, and sample count;
- requested/actual device, model placement, KV placement, optimization device, and fallback;
- exit code, timeout state, output validity, cleanup duration, and surviving owned-process count.

Scalar aggregates preserve mean, median, minimum, maximum, and sample count. Zero is accepted only when measured directly. Missing counters receive a sourced failure or blocked classification and prevent a required measured row from passing.

## Quality Contract

Every configuration that performs Granite generation receives the complete frozen P1-P6 quality evaluation, including standard baselines, TBQ3, TBQ4, asymmetric K/V cases, norm variants, context/stability cases, and device/fallback cases that genuinely execute.

For each prompt preserve the prompt identity, exact raw response, response hash, runtime/configuration identity, criterion scores, deterministic checks, caps, and adjudication notes. Each configuration records P1-P6 scores plus arithmetic mean, median, minimum, and maximum. Scoring follows the frozen harsh rubric. Precision, codec name, expected compression, speed, and memory savings never increase quality scores.

Build checks, codec unit tests, and synthetic numerical probes do not receive language-quality scores because they produce no model response. They use deterministic conformance and numerical-error results instead.

## Safety and Recovery

Only one conversion, model process, or benchmark runs at a time. Every launch has an absolute timeout, owned process-tree tracking, periodic memory/resource sampling, a validated minimum-available-RAM floor, atomic checkpoints, and post-stop survivor verification.

On timeout, memory-floor violation, invalid output, activation failure, or collector failure, terminate the full owned process tree and preserve the attempt. Resume only from validated checkpoints. Never bypass the floor merely to populate a workbook cell. Do not overwrite prior evidence; supersede it with explicit lineage.

## Workbook Reconciliation

The existing WB-04 v1.4 terminal placeholders are retained as historical evidence and superseded by new attempts. WB-04 must distinguish official baseline rows from project-patched TurboQuant rows visibly and in every configuration identifier.

A model-performance row passes only when it has three accepted repetitions, every required metric, correct activation/device evidence, valid output, zero surviving owned processes, and its required P1-P6 evidence. A TurboQuant row additionally requires the exact requested K/V codecs and packed allocation to be proven at runtime.

WB-04 is not called fully executed while required Granite 3B rows remain unmeasured. Granite 8B hardware limits are reported separately from implementation failures, but the workbook cannot claim that those rows executed until they run on a suitable host.

After each accepted result, update the operational testing registers and Markdown workbook. At controlled release, update the append-only workbook revision register and manifest hashes, regenerate the DOCX, verify structure and every rendered page, and rerun all repository validators.

## Acceptance Criteria

The recovery is accepted only when:

- the official baseline remains reproducible and independently identified;
- the project patch has deterministic TBQ3/TBQ4 conformance evidence;
- every runnable Granite configuration has pilot, warm-up, and three measured repetitions;
- every Granite-generating configuration has complete P1-P6 quality evidence;
- activation, packed allocation, device, fallback, metrics, and cleanup evidence are complete;
- no required row is represented by a fabricated number or a terminal placeholder described as an execution pass;
- all required repository/build tests and controlled-workbook validators pass;
- 8B rows execute on a suitable host before the workbook is described as fully executed.

## Explicit Non-Goals

- Reusing llama.cpp measurements as OpenVINO evidence.
- Relabelling AtomicBot or animehacker results as WB-04 results.
- Claiming QJL, PolarQuant, GPU TurboQuant, or upstream support without implementation and runtime proof.
- Weakening safety gates to force Granite 8B onto the current 16 GiB laptop.
