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
| Vulkan/SYCL path | Controlled SYCL OpenCL build and reconciled 40/40 terminal tests passed; standard partial runtime passed; TQ3 SYCL runtime disproven; supplementary Vulkan built but has no TQ3_0-specific source evidence |
| CUDA-only dependencies | CUDA-specific implementation exists, but TQ3_0 is not source-level CUDA-only because CPU and SYCL paths are also present |
| QJL residual correction present? | No - not implemented; fork commit 4381bdde corrects the description to PolarQuant without QJL, and no executable QJL projection/correction path was found |
| Test operator | Codex controlled retest agent |
| Test start date | 2026-07-18 |
| Test end date |  |
| Overall status |  |

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
| AH-B06 | Inspect GPU backend availability | Passed with limitations | SYCL OpenCL device inventory succeeded; reconciled 40/40 terminal tests passed. Standard runtime offloaded 1/41 layers. TQ3 attention offload is runtime-unsupported. Supplementary Vulkan device exists but TQ3 is not source-proven. `experiments/raw-results/animehacker-tq3-0/2026-07-18/build-sycl/reconciliation.json`; `build-vulkan/reconciliation.json` |
| AH-B07 | Use WSL only if Windows fails and evidence is useful | Not used - Windows CPU and controlled SYCL evidence were sufficient | WSL would not change the Intel Windows integration finding and was not used as a substitute route. |
| AH-B08 | Record block layout, cache flags and known limits | Passed (source classification) | TQ3_0 registered with CPU/CUDA/SYCL quantize/dequantize evidence; 14 bytes/32 values; QJL not implemented; Vulkan TQ3_0 not proven. `experiments/raw-results/animehacker-tq3-0/2026-07-18/acquisition/source-audit.json` |

# 4. Ordered test matrix

| ID | Model | KV cache | Execution | Context | Purpose | Status |
|---|---|---|---|---|---|---|
| AH-01 | Diagnostic | Standard | CPU | 1K | Repository baseline | Runtime passed; quality pending |
| AH-02 | Diagnostic | TQ3_0 | CPU | 1K | Confirm TQ3_0 activation | Runtime and activation passed; quality pending |
| AH-03 | Granite 3B | F16 | CPU | 2K | Granite fork baseline | Runtime passed; quality pending |
| AH-04 | Granite 3B | Q8_0 | CPU | 4K | Conventional cache reference | Runtime passed; quality pending |
| AH-05 | Granite 3B | TQ3_0 | CPU | 4K | Main 3B TQ test | Runtime and activation passed; quality pending |
| AH-06 | Granite 8B | F16 | CPU | 2K | 8B fork baseline | Safety-blocked during model load after 3 controlled gates; resolved classification |
| AH-07 | Granite 8B | TQ3_0 | CPU | 4K | Main 8B TQ test | Safety-blocked at final 2 GiB floor; resolved classification |
| AH-08 | Granite 3B | Standard | SYCL partial | 4K | Controlled GPU baseline | Runtime passed; quality pending |
| AH-09 | Granite 3B | TQ3_0 | SYCL partial | 4K | GPU TQ attempt | Unsupported after controlled runtime proof; resolved |
| AH-10 | Granite 8B | TQ3_0 | SYCL partial | 2K | Guarded 8B GPU investigation | Unsupported by AH-09 kernel proof and 8B memory gate; resolved |

## 5. animehacker configuration ladder

| Order | Model weights | KV cache | Device | Purpose | Supported? | Selected for formal test? |
|---|---|---|---|---|---|---|
| 1 | Frozen Q4_K_M GGUF weights | F16 | CPU | Fork baseline - TQ off | Yes | Yes - AH-01/AH-03; AH-06 safety-classified |
| 2 | Same frozen weights | Q8_0 | CPU | Standard compressed-cache reference | Yes | Yes - AH-04 |
| 3 | Same frozen weights | TQ3_0 | CPU | Main TQ3_0 comparison | Yes for 1B/3B | Yes - AH-02/AH-05; AH-07 safety-classified |
| 4 | Same frozen weights | F16 | SYCL OpenCL partial | GPU baseline | Yes with CPU fallback | Yes - AH-08 |
| 5 | Same frozen weights | TQ3_0 | SYCL OpenCL partial | GPU TQ investigation | No usable attention runtime on tested device | Investigated - AH-09; AH-10 classified from prerequisite proof |

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
| AH-09 | GPU partial | Intel UHD Graphics | SYCL OpenCL | Unsupported: TQ3 attention offload aborts in `mmvq.cpp:1149`; flash path emits one-token invalid output | CPU TQ3_0 KV allocation proven before abort | Yes; CPU-only AH-05 is the valid TQ3 route | Rejected attempts measured only; not reported as performance | Rejected attempts measured 4% before abort/invalid response | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-09-rejected-flash-env-only/`; `AH-09-rejected-explicit-flash-off-layer/` |
| AH-10 | GPU partial planned | Intel UHD Graphics | SYCL OpenCL | Not launched: same TQ3 SYCL kernel is unsupported and 8B standard load crossed the memory gate | Not allocated because prerequisite capability failed | CPU AH-07 remains the only potentially valid TQ3 route | Not measured because launch was prohibited by proven prerequisite | Not measured because launch was prohibited by proven prerequisite | AH-09 failure evidence plus `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-06/pilot-emergency-stop-2048/` |

# Formal run results

| Test ID | Model | Weights | K cache | V cache | Device | Context | Llama mem MiB* | Context/KV MiB | TTFT ms | Tok/s | Quality /10 | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AH-01 | Diagnostic | Gemma 1B Q4_K_M | f16 | f16 | CPU (-ngl 0) | 1K | 929.48 peak WS; 730.87 peak private | 26.00 | 140.39 median | 46.32 decode; 95.31 prompt | 6.23 | Runtime and quality passed |
| AH-02 | Diagnostic | Gemma 1B Q4_K_M | tq3_0 | tq3_0 | CPU (-ngl 0) | 1K | 911.05 peak WS; 712.91 peak private | 5.69 | 137.75 median | 37.75 decode; 81.51 prompt | 3.66 | Runtime passed; harsh quality regression recorded |
| AH-03 | Granite 3B | Q4_K_M GGUF | f16 | f16 | CPU (-ngl 0) | 2K | 3662.86 peak WS; 1944.03 peak private | 160.00 | 274.92 median | 15.87 decode; 45.69 prompt | 6.39 | Runtime and quality passed |
| AH-04 | Granite 3B | Q4_K_M GGUF | q8_0 | q8_0 | CPU (-ngl 0) | 4K | 3671.96 peak WS; 1933.01 peak private | 170.00 | 243.23 median | 15.82 decode; 52.03 prompt | Pending P1-P6 | Runtime passed |
| AH-05 | Granite 3B | Q4_K_M GGUF | tq3_0 | tq3_0 | CPU (-ngl 0) | 4K | 3574.63 peak WS; 1837.87 peak private | 70.00 | 252.57 median | 11.54 decode; 50.69 prompt | Pending P1-P6 | Runtime and TQ3_0 activation passed |
| AH-06 | Granite 8B | Q4_K_M GGUF | f16 | f16 | CPU (-ngl 0) | 2K | 8913.65 peak WS before final gate; 4317.87 peak private | 320.00 allocation before stop | Not measured: stopped before request | Not measured: stopped before request | Not scored: safety prerequisite blocked | Safety-classified; no unresolved failed inference |
| AH-07 | Granite 8B | Q4_K_M GGUF | tq3_0 | tq3_0 | CPU (-ngl 0) | 4K | 8737.70 peak WS before gate; 4137.68 peak private | 140.00 allocation before stop | Not measured: stopped before request | Not measured: stopped before request | Not scored: safety prerequisite blocked | Safety-classified; no unresolved failed inference |
| AH-08 | Granite 3B | Q4_K_M GGUF | f16 | f16 | SYCL partial (-ngl 1) | 4K | 3130.27 peak WS; 1289.04 peak private | 320.00 | 309.95 median | 15.18 decode; 40.93 prompt | Pending P1-P6 | Runtime passed; 1/41 layers on SYCL |
| AH-09 | Granite 3B | Q4_K_M GGUF | tq3_0 | tq3_0 | SYCL partial | 4K | Rejected attempt: 2880.77 peak WS | 70.00 allocation proven | Rejected one-token response; not a valid TTFT result | Rejected 1-token sentinel; no valid throughput | Not scored: runnable prerequisite disproven | Unsupported after controlled proof; no unresolved failure |
| AH-10 | Granite 8B | Q4_K_M GGUF | tq3_0 | tq3_0 | SYCL partial planned | 2K | Not measured: launch prohibited by AH-09 kernel proof and 8B gate | Predicted allocation not reported as measurement | Not measured: prerequisite unsupported | Not measured: prerequisite unsupported | Not scored: prerequisite unsupported | Unsupported before launch; no unresolved failure |

Use one row per formal configuration. Preserve detailed commands, raw logs and outputs. Llama memory-breakdown lines are not OS peak working set. A metric not sampled is `Not measured`, not inferred.

# Quality and context summary

| Prompt ID | Task | Baseline /10 | Optimised /10 | Format valid? | Required facts retained? | Repetition/corruption? | Notes |
|---|---|---|---|---|---|---|---|
| P1 | Sanity explanation |  |  |  |  |  |  |
| P2 | Instruction following |  |  |  |  |  |  |
| P3 | Exact JSON structure |  |  |  |  |  |  |
| P4 | Summarisation |  |  |  |  |  |  |
| P5 | Long-context retrieval |  |  |  |  |  |  |
| P6 | Multi-turn stability |  |  |  |  |  |  |
| Average | P1-P6 bounded quality screen |  |  |  |  |  |  |

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
| AH-F09 | AH-09/AH-10 | UNSUP | SYCL TQ3 flash-on emitted a one-token invalid result; flash-off aborted in `mmvq.cpp:1149` | TQ3 K/V attention dequantization is not usable on this SYCL route | Rejected sentinel metrics; proved limitation with explicit flash-off and narrowed tensor placement; classified AH-09/AH-10 unsupported | Yes - capability-classified | `experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime/AH-09-rejected-flash-env-only/`; `AH-09-rejected-explicit-flash-off-layer/` |

Codes: BF build | DEP dependency | WIN Windows | MODEL format | ARCH architecture | BASE baseline | TQ-ACT activation | TQ-FALLBACK fallback | TQ-CRASH crash | CPU | GPU | HYBRID | OOM | MEM | PERF | QUAL | REPRO | SCOPE

# Final repository/runtime decision

| Field | Record |
|---|---|
| Implementation depth | CPU TQ3_0 runtime validated; SYCL conversion/copy code exists but TQ3 attention runtime is unusable on the tested Intel OpenCL device; Vulkan TQ3 path is not source-proven |
| QJL present? | No - source/history audit finds 3-bit PolarQuant only; no QJL projection or residual-correction implementation |
| Granite 3B TQ3_0 | Passed on CPU at 4K: 70.00 MiB KV, 3574.63 MiB peak WS, 11.54 decode tok/s; quality adjudication in progress |
| Granite 8B TQ3_0 | Safety-blocked before request at the final 2 GiB reserve; 140.00 MiB KV allocation and 8737.70 MiB pre-stop peak WS measured |
| CPU support | Validated for Diagnostic 1B and Granite 3B; repository terminal tests 40/40 |
| Intel GPU support | Standard SYCL partial passed with 1/41 layers and 16.31% mean GPU; TQ3 SYCL classified unsupported after invalid flash output and `mmvq.cpp:1149` abort |
| Measured memory benefit | Diagnostic 1K KV: 26.00 to 5.69 MiB (-78.1%); Granite 3B 4K KV: Q8_0 170.00 to TQ3_0 70.00 MiB (-58.8%); memory benefit accompanies lower decode throughput |
| Research value | High for quantized-KV CPU experimentation and failure characterization; insufficient for production Intel GPU integration |
| Integration difficulty | High: fork is behind upstream, QJL claim is absent, CPU vec-dot traits are incomplete, and SYCL TQ3 attention requires kernel/runtime repair |
| Final status | Conditional research-only CPU candidate; reject current SYCL TQ3 route for application integration |
| Application role | CPU fallback experiment only after quality acceptance; standard SYCL remains a separate non-TQ baseline |
| Main evidence path | `experiments/raw-results/animehacker-tq3-0/2026-07-18/` |
| Final reasoning | Compression is substantial, but performance regresses and the Intel GPU path fails correctness/stability gates; precision labels receive no quality bonus |

# Evidence package summary and reviewer notes

| Item | Record |
|---|---|
| Main evidence folder | `experiments/raw-results/animehacker-tq3-0/2026-07-18/` |
| Archive ZIP | Not separately created; controlled evidence is stored directly with hashed artifacts in the repository |
| Pinned commit | 5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc |
| Git head | 5bc5ed3bdc25003aa9f07422753a7b8d4f9190fc (clean detached campaign checkout) |
| SYCL device | Intel UHD Graphics via `opencl:gpu`, driver 32.0.101.7076; Level Zero retained as rejected unstable evidence |
| Formal matrix | AH-01-AH-05 and AH-08 have three valid samples; AH-06/AH-07 safety-classified; AH-09/AH-10 capability-classified |
| Quality review | Frozen P1-P6 screen, deterministic gates first, harsh weighted rubric, no precision bonus; raw response and hash retained per runnable row |
| Memory note | Peak working set includes mapped GGUF pages and is not interchangeable with private bytes; both are reported separately, with physical-RAM floors enforced |
