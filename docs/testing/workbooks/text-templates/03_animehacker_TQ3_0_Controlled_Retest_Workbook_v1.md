# 03 animehacker TQ3_0 Controlled Retest Workbook v1

**Controlled filename:** `03_animehacker_TQ3_0_Controlled_Retest_Workbook_v1.docx`
**Generated DOCX hash:** recorded in `Controlled-Workbook-Manifest.csv`
**Original source:** `03_animehacker_TQ3_0_Completed_Test_Workbook_Quality10_Checked (1).docx`
**Original source SHA-256:** `1fa3528d7aa871d3570cdb93ad91d57851fcd7e7ed946ce48a98adab295d31b2`

## animehacker TQ3_0 Controlled Retest Workbook

Controlled retest template v1. Historical results were removed from this working copy; the original source document is preserved under docs/testing/source-material/original-workbooks/.

Purpose: evaluate the alternative llama.cpp TurboQuant-style implementation as an integration candidate or research comparator.

Use this workbook while testing. Record exact versions, commands, logs and evidence paths. Freeze formal settings only after the pilot tests succeed.

# 1. Repository and environment record

| Field | Record |
|---|---|
| Repository URL | https://github.com/animehacker/llama-turboquant |
| Branch/tag | main (detached campaign pin; tag main-b8532-5bc5ed3) |
| Pinned commit | 5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc |
| Upstream llama.cpp base commit | 914eb5ff0c74c88c7ef8aec115878d8f64c81e56 |
| TurboQuant cache format | TQ3_0, 14-byte blocks per 32 values (3.5 bits/value); repository history identifies implementation as 3-bit PolarQuant without QJL |
| CPU path | Fresh Windows MSVC/AVX2 build passed; 40/40 repository tests passed after test-only capability/threshold correction; AH-01-AH-05 runtime complete; 8B rows safety-classified |
| Vulkan/SYCL path | Controlled SYCL build and reconciled 40/40 terminal tests passed; AH-08 standard partial runtime used OpenCL; recovered TQ3 rows used `ONEAPI_DEVICE_SELECTOR=level_zero:0`; supplementary Vulkan built but has no TQ3_0-specific source evidence |
| CUDA-only dependencies | CUDA-specific implementation exists, but TQ3_0 is not source-level CUDA-only because CPU and SYCL paths are also present |
| QJL residual correction present? | No - not implemented; fork commit 4381bdde corrects the description to PolarQuant without QJL, and no executable QJL projection/correction path was found |
| Test operator | Codex controlled retest agent |
| Test start date | 2026-07-18 |
| Test end date | 2026-07-18 |
| Overall status | Complete: 7 runnable rows passed runtime/quality evidence collection; 3 rows safety-classified; zero unresolved failures |

## 2. Target laptop

| Field | Fixed/current value | Confirm or update |
|---|---|---|
| Machine ID | Lenovo-PF4HMD0T | Confirmed for 2026-07-18 campaign |
| Processor | 12th Gen Intel Core i5-12450H | Confirmed: 8 cores, 12 logical processors |
| RAM | 16.0 GB installed; 15.7 GB usable | Confirmed: 16,462,712 KiB visible at inventory |
| Graphics | Intel UHD Graphics; shared system memory | Confirmed: driver 32.0.101.7076; Level Zero and OpenCL GPU enumerated |
| Operating system | 64-bit Windows, x64-based processor | Windows 11 Home 10.0.26200 build 26200 |
| NPU | Not available - excluded from current testing | Confirmed excluded; no NPU route in controlled matrix |

# 3. Build and setup checklist

| ID | Check | Result | Evidence / command / notes |
|---|---|---|---|
| AH-B01 | Clone exact commit | Passed | Clean detached checkout of latest default branch at campaign start; `experiments/raw-results/animehacker-tq3-0/2026-07-18/acquisition/repository.json` |
| AH-B02 | Configure and build on Windows | Passed | CMake 4.4.0, Ninja 1.13.0, MSVC 19.51; fresh RelWithDebInfo tree completed 418/418 actions. `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-cpu/` |
| AH-B03 | Build CPU route | Passed | AVX2/FMA/F16C CPU backend built; binary inventory and hashes in `build-cpu/reconciliation.json` |
| AH-B04 | Build benchmark tools | Passed | `llama-cli`, `llama-server`, `llama-bench`, `llama-quantize`, and `llama-perplexity` built and SHA-256 hashed in `build-cpu/reconciliation.json` |
| AH-B05 | Run repository-provided tests | Passed - 40/40 | Initial TQ3_0 test segfault traced to absent optional vec-dot trait; subsequent RMSE 0.003872 was below the unchanged 3-bit 0.0040 limit. Test-only patch recorded in `build-cpu/test-capability-guard.patch`; complete rerun in `build-cpu/ctest-final.log` |
| AH-B06 | Inspect GPU backend availability | Passed with limitations | SYCL OpenCL inventory and tests supported the AH-08 standard baseline. Recovered AH-09 completed with Level Zero `level_zero:0`, CPU-resident TQ3 KV and 1/41 layers offloaded; AH-10 reached the safety floor on the same Level Zero route. Supplementary Vulkan exists but TQ3 is not source-proven. `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-sycl/reconciliation.json`; `runtime-recovery/`; `build-vulkan/reconciliation.json` |
| AH-B07 | Use WSL only if Windows fails and evidence is useful | Not used - Windows CPU and controlled SYCL evidence were sufficient | WSL would not change the Intel Windows integration finding and was not used as a substitute route. |
| AH-B08 | Record block layout, cache flags and known limits | Passed (source classification) | TQ3_0 registered with CPU/CUDA/SYCL quantize/dequantize evidence; 14 bytes/32 values; QJL not implemented; Vulkan TQ3_0 not proven. `experiments/raw-results/animehacker-tq3-0/2026-07-18/acquisition/source-audit.json` |

# 4. Ordered test matrix

| ID | Model | KV cache | Execution | Context | Purpose | Status |
|---|---|---|---|---|---|---|
| AH-01 | Diagnostic | Standard | CPU | 1K | Repository baseline | Complete - runtime passed; quality 6.23 |
| AH-02 | Diagnostic | TQ3_0 | CPU | 1K | Confirm TQ3_0 activation | Complete - runtime/activation passed; quality 3.66 |
| AH-03 | Granite 3B | F16 | CPU | 2K | Granite fork baseline | Complete - runtime passed; quality 6.39 |
| AH-04 | Granite 3B | Q8_0 | CPU | 4K | Conventional cache reference | Complete - runtime passed; quality 6.73 |
| AH-05 | Granite 3B | TQ3_0 | CPU | 4K | Main 3B TQ test | Complete - runtime/activation passed; quality 3.28 |
| AH-06 | Granite 8B | F16 | CPU | 2K | 8B fork baseline | Safety-blocked during model load after 3 controlled gates; resolved classification |
| AH-07 | Granite 8B | TQ3_0 | CPU | 4K | Main 8B TQ test | Safety-blocked at final 2 GiB floor; resolved classification |
| AH-08 | Granite 3B | Standard | SYCL partial | 4K | Controlled GPU baseline | Runtime and quality passed |
| AH-09 | Granite 3B | TQ3_0 | SYCL Level Zero `level_zero:0` partial | 4K | GPU TQ attempt | Complete - 3 measured samples and P1-P6 quality; CPU KV with 1/41 layers offloaded |
| AH-10 | Granite 8B | TQ3_0 | SYCL Level Zero `level_zero:0` partial | 2K | Guarded 8B GPU investigation | Safety-classified at unchanged 2048 MiB floor; final pilot measured 70.0 MiB KV and 8969.625 MiB peak WS; no request or quality run |

## 5. animehacker configuration ladder

| Order | Model weights | KV cache | Device | Purpose | Supported? | Selected for formal test? |
|---|---|---|---|---|---|---|
| 1 | Frozen Q4_K_M GGUF weights | F16 | CPU | Fork baseline - TQ off | Yes | Yes - AH-01/AH-03; AH-06 safety-classified |
| 2 | Same frozen weights | Q8_0 | CPU | Standard compressed-cache reference | Yes | Yes - AH-04 |
| 3 | Same frozen weights | TQ3_0 | CPU | Main TQ3_0 comparison | Yes for 1B/3B | Yes - AH-02/AH-05; AH-07 safety-classified |
| 4 | Same frozen weights | F16 | SYCL OpenCL partial | GPU baseline | Yes with CPU fallback | Yes - AH-08 |
| 5 | Same frozen weights | TQ3_0 | SYCL Level Zero `level_zero:0` partial | GPU TQ investigation | Yes with CPU-resident TQ3 KV and 1/41 layers offloaded | AH-09 measured; AH-10 safety-classified |

# Device execution and fallback verification

Do not treat a CUDA-only or non-Windows path as suitable for application integration. It may still be retained as research-only evidence.

| Test ID | Requested device | Actual device | Backend | Offload level | TQ/KV device | CPU fallback? | CPU % | GPU % | Evidence path |
|---|---|---|---|---|---|---|---|---|---|
| AH-01 | CPU | CPU | CPU AVX2 | 0 layers requested for offload (`-ngl 0`) | CPU KV | No GPU route requested | mean 56.90%; median 65.88%; peak 67.00% | mean/median/peak 0.00% | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-01/summary.json` |
| AH-02 | CPU | CPU | CPU AVX2 | 0 layers requested for offload (`-ngl 0`) | CPU TQ3_0 KV | No GPU route requested | mean 58.44%; median 65.75%; peak 67.86% | mean/median/peak 0.00% | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-02/summary.json` |
| AH-03 | CPU | CPU | CPU AVX2 | 0 layers requested for offload (`-ngl 0`) | CPU F16 KV | No GPU route requested | mean 62.04%; median 65.67%; peak 67.36% | mean/median/peak 0.00% | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-03/summary.json` |
| AH-04 | CPU | CPU | CPU AVX2 | 0 layers requested for offload (`-ngl 0`) | CPU Q8_0 KV | No GPU route requested | mean 61.97%; median 65.84%; peak 67.01% | mean/median/peak 0.00% | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-04/summary.json` |
| AH-05 | CPU | CPU | CPU AVX2 | 0 layers requested for offload (`-ngl 0`) | CPU TQ3_0 KV | No GPU route requested | mean 62.70%; median 65.62%; peak 67.39% | mean/median/peak 0.00% | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-05/summary.json` |
| AH-06 | CPU | CPU load attempted | CPU AVX2 | 0 layers requested (`-ngl 0`) | CPU F16 KV allocation reached 320.00 MiB | No GPU route requested | Not measured: emergency stop occurred before request | Measured 0% before request; no request-window result | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-06/pilot-emergency-stop-3072/`; `pilot-emergency-stop-2560/`; `pilot-emergency-stop-2048/` |
| AH-07 | CPU | CPU load attempted | CPU AVX2 | 0 layers requested (`-ngl 0`) | CPU TQ3_0 KV allocation reached 140.00 MiB | No GPU route requested | Not measured: emergency stop occurred before request | Measured 0% before request; no request-window result | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-07/pilot/` |
| AH-08 | GPU partial | Intel UHD Graphics | SYCL OpenCL | 1/41 layers | CPU F16 KV | Yes, remaining 40 layers and KV on CPU | mean 62.03%; median 66.11%; peak 67.60% | mean 16.31%; median 16.00%; peak 20.00%; 407.98 MiB peak process GPU memory | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-08/summary.json` |
| AH-09 | GPU partial | Intel UHD Graphics | SYCL Level Zero `level_zero:0` | 1/41 layers offloaded; actual device classification remains CPU because KV and 40 layers stayed on CPU | CPU TQ3_0 KV, 70.00 MiB | Yes; this is partial placement, not full GPU acceleration | mean 63.14%; median 65.81%; peak 67.71% | mean 12.61%; median 12.00%; peak 17.00%; 411.63 MiB peak shared process GPU memory | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime-recovery/AH-09/summary.json` |
| AH-10 | GPU partial requested | Intel UHD Graphics load attempted | SYCL Level Zero `level_zero:0` | Final pilot stopped before request when available RAM crossed the unchanged 2048 MiB floor; 8969.625 MiB peak WS | CPU TQ3_0 KV, 70.0 MiB before stop; placement not accepted as a completed run | Safety stop; no request-window utilization | Not measured: stopped before request | Not measured: stopped before request | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime-recovery/AH-10/pilot/measurement.json`; schema-v2 process evidence |

# Formal run results

| Test ID | Model | Weights | K cache | V cache | Device | Context | Llama mem MiB* | Context/KV MiB | TTFT ms | Tok/s | Quality /10 | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AH-01 | Diagnostic | Gemma 1B Q4_K_M | f16 | f16 | CPU (-ngl 0) | 1K | 929.48 peak WS; 730.87 peak private | 26.00 | 140.39 median | 46.32 decode; 95.31 prompt | 6.23 | Runtime and quality passed |
| AH-02 | Diagnostic | Gemma 1B Q4_K_M | tq3_0 | tq3_0 | CPU (-ngl 0) | 1K | 911.05 peak WS; 712.91 peak private | 5.69 | 137.75 median | 37.75 decode; 81.51 prompt | 3.66 | Runtime passed; harsh quality regression recorded |
| AH-03 | Granite 3B | Q4_K_M GGUF | f16 | f16 | CPU (-ngl 0) | 2K | 3662.86 peak WS; 1944.03 peak private | 160.00 | 274.92 median | 15.87 decode; 45.69 prompt | 6.39 | Runtime and quality passed |
| AH-04 | Granite 3B | Q4_K_M GGUF | q8_0 | q8_0 | CPU (-ngl 0) | 4K | 3671.96 peak WS; 1933.01 peak private | 170.00 | 243.23 median | 15.82 decode; 52.03 prompt | 6.73 | Runtime and quality passed after bounded P5 retry |
| AH-05 | Granite 3B | Q4_K_M GGUF | tq3_0 | tq3_0 | CPU (-ngl 0) | 4K | 3574.63 peak WS; 1837.87 peak private | 70.00 | 252.57 median | 11.54 decode; 50.69 prompt | 3.28 | Runtime passed; harsh quality regression recorded |
| AH-06 | Granite 8B | Q4_K_M GGUF | f16 | f16 | CPU (-ngl 0) | 2K | 8913.65 peak WS before final gate; 4317.87 peak private | 320.00 allocation before stop | Not measured: stopped before request | Not measured: stopped before request | Not scored: safety prerequisite blocked | Safety-classified; no unresolved failed inference |
| AH-07 | Granite 8B | Q4_K_M GGUF | tq3_0 | tq3_0 | CPU (-ngl 0) | 4K | 8737.70 peak WS before gate; 4137.68 peak private | 140.00 allocation before stop | Not measured: stopped before request | Not measured: stopped before request | Not scored: safety prerequisite blocked | Safety-classified; no unresolved failed inference |
| AH-08 | Granite 3B | Q4_K_M GGUF | f16 | f16 | SYCL partial (-ngl 1) | 4K | 3130.27 peak WS; 1289.04 peak private | 320.00 | 309.95 median | 15.18 decode; 40.93 prompt | 6.27 | Runtime and quality passed; 1/41 layers on SYCL |
| AH-09 | Granite 3B | Q4_K_M GGUF | tq3_0 | tq3_0 | SYCL Level Zero `level_zero:0` partial (-ngl 1) | 4K | 2891.66 peak WS; 1040.09 peak private | 70.00 | 491.00 median | 12.11 median decode; 40.90 median prompt | 6.06 | Runtime and quality passed; CPU-resident TQ3 KV and 1/41 layers offloaded |
| AH-10 | Granite 8B | Q8_0 GGUF | tq3_0 | tq3_0 | SYCL Level Zero `level_zero:0` partial requested | 2K | 8969.625 MiB peak WS; 1145.535 MiB peak private before stop | 70.0 MiB before stop | Not measured: stopped before request | Not measured: stopped before request | Not scored: safety prerequisite blocked | Final pilot safety-classified at unchanged 2048 MiB floor; cleanup verified |

Use one row per formal configuration. Preserve detailed commands, raw logs and outputs. Llama memory-breakdown lines are not OS peak working set. A metric not sampled is `Not measured`, not inferred.

# Quality and context summary

| Prompt ID | Task | Baseline /10 | Optimised /10 | Format valid? | Required facts retained? | Repetition/corruption? | Notes |
|---|---|---|---|---|---|---|---|
| P1 | Sanity explanation | 4.00 | 4.00 | Baseline Yes; TQ3 No | Baseline No; TQ3 No | No | TQ3 returned 3 bullets and omitted required semantic slots |
| P2 | Instruction following | 8.35 | 8.35 | Yes / Yes | Yes / Yes | No | Both passed the exact three-line structure |
| P3 | Exact JSON structure | 4.00 | 2.00 | Baseline Yes; TQ3 No | No / No | No | TQ3 emitted invalid schema/JSON and received the structural cap |
| P4 | Summarisation | 10.00 | 4.00 | Baseline Yes; TQ3 No | Baseline Yes; TQ3 No | No | TQ3 used one sentence and omitted the runtime fact |
| P5 | Long-context retrieval | 10.00 | 1.30 | Baseline Yes; TQ3 No | Baseline Yes; TQ3 No | No | TQ3 returned the wrong marker; raw response retained |
| P6 | Multi-turn stability | 2.00 | 0.00 | No / No | No / No | Yes / Yes | Baseline omitted the amber prefix; TQ3 response was unusable |
| Average | P1-P6 bounded quality screen | 6.39 | 3.28 | Mixed; deterministic gates applied | TQ3 retained fewer required facts | TQ3 failed stability | No precision bonus; response evidence controls the score |

## AH-09 recovered quality adjudication

| Prompt | Score /10 | Deterministic result | Evidence hash | Note |
|---|---|---|---|---|
| P1 | 4.00 | Fail | 5301df6d2025c9d86efbf01602c1979dae8b8d6d7c6102db38d1cb429f07e2c9 | Required semantic slots were not retained |
| P2 | 8.35 | Pass | 0ddac73d955e51ab4e2af46db74551ae917e8f8ac17f23a3e447adbad0332b85 | Exact labelled structure passed |
| P3 | 8.70 | Pass | 70bc9a0c03802c4a71ed664d39b072b6cd1e6018689ff70cc4efff631e9e5c98 | Exact JSON structure passed |
| P4 | 4.00 | Fail | 8b808310880d35799378cdc1cec3262a72ddca960cef9d6de21972f66c1c6e85 | Required cache and TurboQuant facts were missing |
| P5 | 1.30 | Fail | 824f2908f1a04296801a3f2c86103f2bc8eca36c7935bb93b0ce5eb085d23aec | Wrong long-context marker |
| P6 | 10.00 | Pass | 215dbcb32c4f1a1d61eedf623ed4800482e0c68b651e464352bdc4dee733871e | Multi-turn stability passed |
| Mean | 6.0583 | Mixed | Recorded per response above | Harsh rubric; no precision bonus |

## Failure log

| Failure ID | Test ID | Code | Description | Likely cause | Next action | Resolved? | Evidence |
|---|---|---|---|---|---|---|---|
| AH-F01 | AH-B02 | DEP | CMake and Ninja absent from PATH; machine-wide silent installer stalled | Missing user build tools and MSI stall | Stopped winget route; installed user-local CMake 4.4.0/Ninja 1.13.0 and verified versions | Yes | `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-cpu/` |
| AH-F02 | AH-B02 | BF | First CMake invocation could not find MSVC because cmd expanded PATH before VsDevCmd updated it | Command expansion order erased compiler PATH entries | Prepend user tools before calling VsDevCmd; fresh configure detected MSVC 19.51 | Yes | `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-cpu/configure.log`; `configure-retry.log` |
| AH-F03 | AH-B05 | REPRO | Two quantization tests segfaulted on TQ3_0 and then one failed its numeric gate | Generic tests called absent optional vec-dot trait; new 3-bit type omitted from existing 3-bit RMSE category | Added test-only capability guards and mapped TQ3_0 to unchanged 0.0040 3-bit limit; full suite passed 40/40 | Yes | `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-cpu/test-capability-guard.patch`; `ctest-final.log` |
| AH-F04 | AH-B05 | REPRO | One controller invocation ran CTest from source root and found zero tests | Missing explicit test directory | Rejected invalid zero-test result; reran with `--test-dir build-cpu-controlled`, parsed exactly 40/40 | Yes | `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-cpu/ctest-final.log` |
| AH-F05 | AH-B06 | REPRO | SYCL Level Zero tests aborted during teardown with device-lost errors | Level Zero runtime instability on the installed Intel UHD driver | Selected the independently inventoried OpenCL SYCL device; final terminal reconciliation is 40/40 | Yes | `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-sycl/device-level-zero.log`; `reconciliation.json` |
| AH-F06 | AH-B05 | REPRO | SYCL F16 EXP backend cases returned invalid numeric results | Backend declared unsupported behavior as supported | Restricted SYCL EXP support to verified F32; targeted regression and terminal suite passed | Yes | `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-sycl/backend-ops-minimal-final.log`; `campaign-fixes.patch` |
| AH-F07 | AH-B05 | REPRO | Thread-safety test exhausted OpenCL resources when run immediately after exhaustive backend operations | Integrated-GPU resource lifetime/order sensitivity | Preserved failed attempt and reran the exact indexed test in isolation; terminal pass included in 40/40 reconciliation | Yes | `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-sycl/thread-safety-minimal-final.log`; `thread-safety-exact-minimal-final.log` |
| AH-F08 | AH-06/AH-07 | SAFETY | 8B model loads crossed 3 GiB, 2.5 GiB and final 2 GiB emergency floors before a request | 16 GB system cannot preserve the controlled reserve with these 8B mappings | Stopped and cleaned every process tree; classified both rows from measured gate evidence without unsafe lowering | Yes - safety-classified | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-06/`; `AH-07/pilot/` |
| AH-F09 | AH-09 | RECOVERY | Initial SYCL TQ3 attempts produced invalid/aborted attention paths | Broad GPU placement was not viable; narrowed placement retained KV and 40/41 layers on CPU | Recovered with controlled `-ngl 1` placement; accepted three samples and six quality records without claiming GPU-resident TQ3 cache | Yes - recovered with bounded partial placement | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime-recovery/AH-09/`; `quality-recovery/AH-09/` |
| AH-F11 | AH-10 | SAFETY | Guarded 8B SYCL pilot crossed the unchanged 2048 MiB available-RAM floor before request | 9.35 GB model mapping on a 16 GB shared-memory system exhausted the controlled reserve | Controller stopped synchronously; schema-v2 preflight, wrapper and post-stop cleanup prove zero residual processes; no quality run | Yes - safety-classified | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime-recovery/AH-10/` |
| AH-F10 | AH-04/P5 | QUAL | First 16K long-context attempt reached the 900-second absolute deadline | Q8_0 CPU prefill exceeded the initial bounded deadline | Preserved empty-response hash and reran P5 only with a 1,800-second ceiling; completed in 1,024.6 seconds | Yes | `experiments/raw-results/animehacker-tq3-0/2026-07-18/quality/AH-04/P5-timeout-900.json`; `P5.json` |

Codes: BF build | DEP dependency | WIN Windows | MODEL format | ARCH architecture | BASE baseline | TQ-ACT activation | TQ-FALLBACK fallback | TQ-CRASH crash | CPU | GPU | HYBRID | OOM | MEM | PERF | QUAL | REPRO | SCOPE

# Final repository/runtime decision

| Field | Record |
|---|---|
| Implementation depth | CPU TQ3_0 runtime validated; recovered SYCL TQ3 ran on Level Zero `level_zero:0` with CPU-resident KV and only 1/41 layers offloaded; Vulkan TQ3 path is not source-proven |
| QJL present? | No - source/history audit finds 3-bit PolarQuant only; no QJL projection or residual-correction implementation |
| Granite 3B TQ3_0 | Passed on CPU at 4K: 70.00 MiB KV, 3574.63 MiB peak WS, 11.54 decode tok/s; harsh quality score 3.28 versus 6.39 F16 baseline |
| Granite 8B TQ3_0 | Final Level Zero `level_zero:0` pilot safety-blocked before request at the 2048 MiB reserve; 70.0 MiB KV and 8969.625 MiB peak WS measured in `runtime-recovery/AH-10/pilot/measurement.json` |
| CPU support | Validated for Diagnostic 1B and Granite 3B; repository terminal tests 40/40 |
| Intel GPU support | Standard SYCL partial passed; recovered TQ3 also completed with 1/41 layers and 12.61% mean GPU, while TQ3 KV and most compute remained on CPU |
| Measured memory benefit | Diagnostic 1K KV: 26.00 to 5.69 MiB (-78.1%); Granite 3B 4K KV: Q8_0 170.00 to TQ3_0 70.00 MiB (-58.8%); memory benefit accompanies lower decode throughput |
| Research value | High for quantized-KV CPU experimentation and failure characterization; insufficient for production Intel GPU integration |
| Integration difficulty | High: fork is behind upstream, QJL claim is absent, CPU vec-dot traits are incomplete, and SYCL TQ3 attention requires kernel/runtime repair |
| Final status | Conditional research comparator: CPU and bounded SYCL-partial evidence accepted; no full-GPU TQ3 acceleration claim |
| Application role | CPU-resident TQ3 cache experiment with limited SYCL layer offload only after quality acceptance; AH-10 remains safety-blocked |
| Main evidence path | `experiments/raw-results/animehacker-tq3-0/2026-07-18/` |
| Final reasoning | Compression is substantial and AH-09 recovered, but placement is predominantly CPU and quality mean is 6.0583; precision labels receive no quality bonus |

# Evidence package summary and reviewer notes

| Item | Record |
|---|---|
| Main evidence folder | `experiments/raw-results/animehacker-tq3-0/2026-07-18/` |
| Archive ZIP | Not separately created; controlled evidence is stored directly with hashed artifacts in the repository |
| Pinned commit | 5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc |
| Git head | 5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc (clean detached campaign checkout) |
| SYCL device | AH-08 standard-cache baseline used Intel UHD Graphics via `opencl:gpu`; recovered AH-09/AH-10 TQ3 evidence used `ONEAPI_DEVICE_SELECTOR=level_zero:0`. These routes are reported separately. |
| Formal matrix | AH-01-AH-05, AH-08 and AH-09 have three valid samples; AH-06/AH-07/AH-10 are safety-classified |
| Quality review | Frozen P1-P6 screen, deterministic gates first, harsh weighted rubric, no precision bonus; AH-09 mean 6.0583 from six hashed recovery responses; AH-10 had no quality run |
| Memory note | Peak working set includes mapped GGUF pages and is not interchangeable with private bytes; both are reported separately, with physical-RAM floors enforced |
