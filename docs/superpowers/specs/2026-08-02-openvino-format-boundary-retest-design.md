# OpenVINO Format-Boundary Retest Design

Date: 2026-08-02

Status: Approved for implementation
Branch: `testing/openvino-turboquant-recovery`

## Purpose

Run one fresh, energy-bounded OpenVINO GenAI comparison from the lowest supported model-weight and K/V-cache formats upward. Stop as soon as a backend reaches a repeatable RAM, timeout, or runtime boundary. Publish the fresh results in a new, human-readable workbook without the historical `OV-*` test codes or inherited workbook clutter.

This campaign does not rewrite or reinterpret WB-04. Its evidence, workbook, and outputs are separate.

## Questions answered

1. Which combined model-weight and K/V-cache formats complete safely on this laptop?
2. What is the first format above the laptop's safe CPU execution boundary?
3. How do TurboQuant TBQ3/TBQ4 compare with standard F16 K/V cache using complete performance, memory, utilisation, and quality measurements?
4. Can the laptop complete one small standard OpenVINO GPU control, without claiming GPU TurboQuant support?

## Fixed host and backend facts

- OpenVINO detects `CPU` and `GPU`.
- The CPU is a 12th Gen Intel Core i5-12450H.
- The GPU is Intel UHD Graphics integrated GPU and shares system memory.
- The project TurboQuant implementation supports TBQ3/TBQ4 on CPU only and deliberately rejects TurboQuant on GPU.
- Standard OpenVINO GenAI may use the GPU, but requested device is never treated as proof; actual execution and fallback must be captured.

## Ordered format ladder

All CPU rows initially use a 512-token input context, deterministic generation settings, the same Granite 4.1 3B family, the same prompt set, and the same runtime build.

1. U4 weights + TBQ3 K/V cache
2. U4 weights + TBQ4 K/V cache
3. U4 weights + standard F16 K/V cache
4. U8 weights + TBQ3 K/V cache
5. U8 weights + TBQ4 K/V cache
6. U8 weights + standard F16 K/V cache
7. F16 weights + TBQ3 K/V cache
8. F16 weights + TBQ4 K/V cache
9. F16 weights + standard F16 K/V cache

The ordering is a practical low-to-high memory-pressure ladder at the fixed 512-token context. Observed peak memory, not the label alone, is authoritative.

A separate U4 weights + standard F16 K/V GPU control runs after the matching CPU row. It is not part of the TurboQuant ladder and a GPU-lane failure does not stop later CPU rows.

## Execution and stop policy

Only one model worker may run at a time. No parallel model loading is permitted.

For each row:

1. Check available RAM, runtime identities, model artifact, owned-process cleanup, and output destination.
2. Run one short pilot.
3. If the pilot passes and stays above the safety floor, run three fresh measured repetitions.
4. Run the compact quality suite in one model session where technically possible.
5. Reconcile all required values from raw receipts before accepting the row.
6. Unload the worker and verify that no owned process remains before advancing.

Hard guards:

- Minimum free physical RAM floor: 3 GiB.
- Pilot wall time: 3 minutes.
- Individual measured repetition wall time: 3 minutes.
- Individual quality prompt wall time: 90 seconds.
- Whole configuration wall time: 12 minutes.
- One clean retry is allowed only after cleanup and only for a failed pilot or required quality step.
- Two matching failures at the same configuration close that backend's ladder immediately.
- A single hard RAM-floor breach, operating-system allocation failure, or owned-worker emergency termination may close the ladder immediately when a retry would put the laptop at risk.
- No speculative fixes, rebuild loops, artifact conversions, or repeated tuning are allowed during the measured campaign.

The terminal wording must be evidence-bounded: `outside this laptop's configured safe RAM/time envelope`. It must not claim that the format is impossible on all hardware.

## Energy controls

- Serial execution only.
- Fixed one-stream latency configuration and no concurrent benchmark jobs.
- Reuse a loaded pipeline for the compact quality prompts when possible.
- No oversized long-context quality prompt from the earlier campaign.
- Stop timed-out work instead of waiting indefinitely.
- Sample utilisation at a modest interval sufficient for mean, median, and peak statistics.
- Do not terminate unrelated user applications. Only processes launched and owned by the campaign may be stopped.

## Required measurements

Every accepted row must contain numeric, raw-evidence-backed values for:

- model load time;
- time to first token;
- prompt tokens per second;
- time per output token;
- decode tokens per second;
- generation duration;
- peak working-set RAM;
- peak private RAM;
- minimum available physical RAM;
- K/V-cache bytes and MiB;
- GPU memory peak;
- CPU utilisation mean, median, peak, and sample count;
- GPU utilisation mean, median, peak, and sample count;
- requested device, actual device, execution route, and fallback status;
- cleanup and residual-owned-process counts.

Measured zero GPU use on a CPU row is recorded as `0`, not left blank. A row is not accepted if a required metric is absent, non-numeric, internally inconsistent, or only inferred.

## Quality scoring

Use the existing `quality-rubric-v1.json` scoring rules with a new compact prompt set bounded for the 512-token campaign. The compact suite preserves the existing capability categories while removing the previous oversized retrieval prompt that crossed the RAM floor.

For every runtime-successful format:

- capture the exact prompt, exact output, hashes, and deterministic settings;
- score every prompt against explicit rubric criteria;
- record prompt-level scores and a composite score on the existing 0-10 scale;
- apply the same prompts and rubric to every format;
- score only what the response demonstrates, with no precision-based expectation or preferred-format bias;
- include terse deductions explaining lost points;
- withhold overall acceptance if the required quality suite does not complete.

Scores from this compact suite are comparable within the new workbook only. They are not silently mixed with scores produced by a different historical prompt set.

## New evidence and workbook layout

Campaign evidence root:

`experiments/raw-results/openvino-format-boundary/2026-08-02/`

New workbook sources:

- `docs/testing/workbooks/text-templates/07_OpenVINO_Format_Boundary_Workbook.md`
- `docs/testing/workbooks/generated/07_OpenVINO_Format_Boundary_Workbook.docx`

The workbook uses descriptive row names such as `U4 weights + TBQ3 cache`; it does not expose historical controlled test IDs.

Workbook sections:

1. Outcome and tested boundary
2. Fixed host, runtime, model, and method
3. Successful CPU format comparison
4. Standard GPU control
5. Quality comparison and deductions
6. Stopped format and concise reason
7. Evidence index and reproducibility note

The main comparison contains only fully accepted rows. A stopped row is shown once in a short boundary section with its primary reason and evidence path. Higher formats skipped after the boundary are listed compactly as not attempted by policy, without fabricated metrics or a large failure table.

## Evidence integrity

Every raw receipt is immutable after acceptance and receives a SHA-256 hash. Aggregate values must be recomputed independently from the three accepted sample receipts and compared with the generated summary. The workbook may be generated only from accepted summaries and terminal boundary receipts.

Validation must prove:

- exactly three accepted measured repetitions for each successful row;
- pilot and warm-up exclusion from aggregates;
- complete required metrics and quality fields;
- arithmetic agreement for mean, median, peak, and sample counts;
- requested/activated format and device agreement;
- valid output and evidence hashes;
- zero residual owned processes;
- no blank workbook cells;
- no accepted row after the first terminal CPU boundary;
- no GPU TurboQuant claim.

## Completion condition

The task is complete when the guarded ladder has either reached its first repeatable CPU boundary or completed all reachable formats, the separate GPU control has a passed or concise terminal result, every successful row has all required performance/utilisation/quality metrics, the new Markdown and DOCX workbook are generated and validated, and all claims can be traced to fresh raw receipts.
