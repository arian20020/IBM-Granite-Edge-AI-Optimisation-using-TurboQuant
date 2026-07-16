# Upstream llama.cpp Evidence Guide

This directory contains the raw evidence supporting WB-01, the upstream llama.cpp controlled baseline. The completed workbook is revision 1.4 and uses llama.cpp tag `b9870`, commit `2d973636e292ee6f75fadcf08d29cb33511f509f`.

## Where to start

Use these paths for current results:

```text
UL-B01/ ... UL-B06/                    build, dependency and repository-test evidence
UL-01/ ... UL-13/                      current formal benchmark and quality runs
UL-XX/UL-XX-SERVER-METRICS-R001/      current peak RAM, KV allocation and TTFT samples
../../processed-results/upstream-llama-cpp/
  quality-scoring-2026-07-15.md        scored output review
  resource-metrics-2026-07-16.json     machine-readable metric medians
archive/calibration-and-superseded/    retained non-current evidence
```

The canonical workbook is [01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md](../../../../docs/testing/workbooks/text-templates/01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.md). File-level paths, sizes and SHA-256 hashes are recorded in [Evidence-Index.csv](../../../../docs/testing/Evidence-Index.csv).

## What happened during this campaign

- CPU built successfully and its repository suite passed 52/52.
- The first Vulkan gate was a CRLF validation false negative, not a backend failure. After correcting that check, the build completed; adding the missing isolated Jinja2 dependency produced a 52/52 repository-test pass.
- The first SYCL attempt was interrupted during a long build. The completed oneAPI build produced all 619 targets.
- The broad SYCL repository suite passed 49/52. Three Intel UHD edge cases remain: unsupported FP64 work, a Level Zero device-loss path, and related process crashes.
- UL-13 is not a blanket SYCL-suite pass. It is a passing pinned Granite Q4_K_M project workload on `SYCL0`, with the separate 49/52 edge-suite limitation retained.
- Early CLI timing included startup and was unsuitable for workbook TTFT. Current TTFT starts immediately before the HTTP request to an already-ready `llama-server` and ends at the first streamed generated token. It includes request transfer, tokenization and prefill, and excludes model loading.
- Peak RAM is the maximum aggregate physical working set of the server process tree sampled every 100 ms. KV MB is parsed from deduplicated runtime allocation groups. Each workbook value is the median of exactly three valid samples after one warm-up.

## Reading a test folder

A current test folder normally contains:

- `UL-XX-Rnnn`: the formal `llama-bench` run and device-placement evidence;
- `UL-XX-QUALITY-Rnnn`: raw prompt outputs used by the quality rubric;
- `UL-XX-SERVER-METRICS-R001`: one warm-up and three recorded RAM/KV/TTFT samples.

Use the workbook and processed-result files to identify the current run. Do not substitute values from `archive/` merely because their filenames look similar.

## Adding a future run

1. Read `docs/testing/Master-Test-Plan.md`, `Metric-Definitions.md`, the current WB-01 Markdown and this guide.
2. Create a new immutable run ID. Never overwrite a previous run or reuse its evidence directory.
3. Freeze the model, context, KV formats, backend, device, layer placement, seed and command before execution.
4. Capture command, stdout, stderr, exit state and device identity. For resource metrics, run one warm-up plus exactly three valid recorded samples.
5. Keep raw evidence byte-for-byte. Put corrections in a new run or processed summary.
6. Update `docs/testing/Evidence-Index.csv`, processed results and the canonical workbook together. Regenerate the DOCX and controlled hashes only when canonical workbook content changes.
7. Run the measurement tests, workbook revision validator, workspace validator, evidence hash/path checks and `git diff --check` before publishing.

Do not commit model weights, executables, build directories, caches, credentials, tokens or full environment dumps. Record required versions, paths and non-sensitive configuration instead.

## Archive policy

Calibration, pilot and superseded measurement runs remain available under [archive/calibration-and-superseded](archive/calibration-and-superseded/README.md). They explain how the measurement method was corrected, but they are not current workbook evidence.
