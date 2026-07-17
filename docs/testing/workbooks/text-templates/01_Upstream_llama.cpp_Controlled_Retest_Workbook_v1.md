# 01 Upstream llama.cpp Controlled Retest Workbook v1

**Controlled filename:** `01_Upstream_llama.cpp_Controlled_Retest_Workbook_v1.docx`
**Generated DOCX hash:** recorded in `Controlled-Workbook-Manifest.csv`
**Original source:** `01_Upstream_llama.cpp_Editable_Test_Workbook.docx`
**Original source SHA-256:** `0f25e62db330140b14a4e10eeb8da0be1e9623bbbd55ff6dc1268da3a3964f02`

## Upstream llama.cpp Controlled Retest Workbook

Controlled retest template v1. Historical results were removed from this working copy; the original source document is preserved under docs/testing/source-material/original-workbooks/.

Purpose: establish the dependable GGUF baseline before either TurboQuant fork is introduced.

Use this workbook while testing. Record exact versions, commands, logs and evidence paths. Freeze formal settings only after the pilot tests succeed.

# 1. Repository and environment record

| Field | Record |
|---|---|
| Repository URL | https://github.com/ggml-org/llama.cpp |
| Branch/tag | b9870 |
| Pinned commit | 2d973636e292ee6f75fadcf08d29cb33511f509f |
| Build type | Release; CPU-only static, Vulkan static, and SYCL static builds |
| Compiler/toolchain | MSVC via Visual Studio 18 2026; CMake 4.3.1-msvc1; Vulkan SDK 1.4.350.0; Intel oneAPI 2026.1 SYCL |
| Upstream version string | b9870 |
| Test operator | Student |
| Test start date | 2026-07-14 |
| Test end date | 2026-07-16 |
| Overall status | Conditional pass - UL-01 to UL-13 project workloads completed; CPU and Vulkan suites pass 52/52; the broader SYCL suite retains three edge-case failures at 49/52. |

## 2. Target laptop

| Field | Fixed/current value | Confirm or update |
|---|---|---|
| Machine ID | Lenovo-PF4HMD0T | Confirmed as LENOVO-PF4HMD0T |
| Processor | Intel Core i5-12450H; 8 cores, 12 logical processors | Confirmed: 12th Gen Intel Core i5-12450H, 8 physical cores, 12 logical processors |
| RAM | 16 GB nominal installed; approximately 15.7 GB visible/usable | Confirmed: 16,857,817,088 bytes installed |
| Graphics | Intel UHD Graphics; shared system memory; driver 32.0.101.7076 | Confirmed: Intel UHD Graphics, driver 32.0.101.7076 |
| Operating system | Windows 11 Home, version 10.0.26100, build 26100, 64-bit | Updated: Windows 11 Home 10.0.26200 build 26200, 64-bit |
| NPU | Not available - excluded from current testing | Confirmed: no NPU recorded; excluded |

# 3. Build and setup checklist

| ID | Check | Result | Evidence / command / notes | Evidence Path |
|---|---|---|---|---|
| UL-B01 | Clone exact commit | Passed | Official tag b9870 resolved to pinned commit; clean tree verified after Windows long-path recovery. | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B01/UL-B01-R004/Windows-Long-Path-Recovery.md |
| UL-B02 | Configure clean CPU build | Passed | Clean x64 CPU static Release configuration; local and prebuilt UI disabled. | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-B02/UL-B02-R002/ |
| UL-B03 | Build CLI, server, bench and perplexity tools | Passed | Corrected build produced the four required binaries; initial UI asset failure preserved. | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B03/UL-B03-R002/binary-inventory.csv |
| UL-B04 | Run repository-provided tests | Passed | UL-B04-R002 passed 52/52 after adding Jinja2 3.1.6 to the isolated Python environment. | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-B04/UL-B04-R002/ctest-stdout.txt |
| UL-B05 | Configure Vulkan build | Passed | R001 was a CRLF regex false negative; R002 built Vulkan successfully; R003 passed 52/52 tests in 640.35 s. | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B05/UL-B05-R003/run-summary.json |
| UL-B06 | Configure SYCL build if practical | Passed with edge-suite limitation | oneAPI 2026.1 configured and all 619 targets built. The Granite project workload passes on SYCL0, while the broad repository suite remains 49/52 because of FP64 and device-loss edge cases. | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-B06/UL-B06-R002/build-stdout.txt |
| UL-B07 | Record warnings, dependencies and binary capabilities | Passed with limitation | CPU, Vulkan and SYCL capabilities captured; the project SYCL route is usable, but three broad upstream tests remain unresolved. | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B06/UL-B06-R002/repository-test-failure-analysis.md |

# 4. Ordered test matrix

| ID | Model | Weights | K/V cache | Device | Context | Purpose | Status |
|---|---|---|---|---|---|---|---|
| UL-01 | Gemma 3 1B diagnostic | Q4_K_M | F16/F16 | CPU | 1,024 | Prove standard runtime works | Passed |
| UL-02 | Granite 4.1 3B | BF16 | F16/F16 | CPU | 2,048 | Highest-precision fit test | Passed |
| UL-03 | Granite 4.1 3B | Q8_0 | F16/F16 | CPU | 4,096 | High-quality quantised reference | Passed |
| UL-04 | Granite 4.1 3B | Q4_K_M | F16/F16 | CPU | 4,096 | Isolate weight quantisation | Passed |
| UL-05 | Granite 4.1 3B | Q4_K_M | Q8_0/Q8_0 | CPU | 4,096 | Practical CPU baseline | Passed with quality caveats |
| UL-06 | Granite 4.1 8B | Q8_0 | F16/F16 | CPU | 2,048 | High-quality fit attempt | Passed |
| UL-07 | Granite 4.1 8B | Q4_K_M | F16/F16 | CPU | 4,096 | Practical weight baseline | Passed with quality caveat |
| UL-08 | Granite 4.1 8B | Q4_K_M | Q8_0/Q8_0 | CPU | 4,096 | Practical CPU baseline | Passed with quality caveats |
| UL-09 | Granite 4.1 3B | Q4_K_M | Q8_0/Q8_0 | Vulkan, 1 layer | 4,096 | Confirm Vulkan/SYCL and minimal offload | Passed |
| UL-10 | Granite 4.1 3B | Q4_K_M | Q8_0/Q8_0 | Vulkan, all layers | 4,096 | Maximum/full GPU attempt | Passed; decode regression |
| UL-11 | Granite 4.1 8B | Q4_K_M | Q8_0/Q8_0 | Vulkan, 1 layer | 4,096 | 8B partial-offload attempt | Passed |
| UL-12 | Granite 4.1 8B | Q4_K_M | Q8_0/Q8_0 | Vulkan, all layers | 4,096 | 8B maximum/full GPU attempt | Passed; decode regression |
| UL-13 | Granite 4.1 3B | Q4_K_M | Q8_0/Q8_0 | SYCL, all layers | 4,096 | Validate the Intel SYCL path and compare full SYCL offload with full Vulkan offload | Passed project workload; edge-suite limitation |

## 5. Standard configuration ladder

| Order | Model weights | KV cache | Device | Purpose | Supported? | Selected for formal test? |
|---|---|---|---|---|---|---|
| 1 | BF16, highest precision available | F16 | CPU | Highest-precision fit test | Yes | Yes, UL-02 |
| 2 | Q8_0 | F16 | CPU | High-quality weight reference | Yes | Yes, UL-03 |
| 3 | Q6_K | F16 | CPU | Optional middle-weight reference | Not available in supplied model set | No |
| 4 | Q4_K_M | F16 | CPU | Practical weight baseline | Yes | Yes, UL-04 and UL-07 |
| 5 | Q4_K_M | Q8_0 | CPU | Main conventional compressed baseline | Yes | Yes, UL-05 and UL-08 |
| 6 | Q4_K_M | Q4_0, if supported | CPU | Aggressive standard cache | Backend supports it; excluded by approved matrix | No |
| 7 | Frozen configuration | Frozen | Vulkan/SYCL | Partial then maximum GPU offload | Yes; SYCL has edge-suite limitations | Vulkan UL-09 to UL-12; SYCL UL-13 |
| 8 | Granite 4.1 8B Q8_0 | F16/F16 | CPU | High-quality 8B fit reference | Yes | Yes, UL-06 |
| 9 | Granite 4.1 8B Q4_K_M | F16/F16 | CPU | Practical 8B weight baseline | Yes | Yes, UL-07 |
| 10 | Granite 4.1 8B Q4_K_M | Q8_0/Q8_0 | CPU | Practical conventional 8B CPU baseline | Yes | Yes, UL-08 |
| 11 | Granite 4.1 3B Q4_K_M | Q8_0/Q8_0 on CPU | Vulkan0 — 1 layer | Minimal 3B Vulkan offload | Yes | Yes, UL-09 |
| 12 | Granite 4.1 3B Q4_K_M | Q8_0/Q8_0 on Vulkan0 | Vulkan0 — all layers | Maximum/full 3B Vulkan offload | Yes | Yes, UL-10 |
| 13 | Granite 4.1 8B Q4_K_M | Q8_0/Q8_0 on CPU | Vulkan0 — 1 layer | Minimal 8B Vulkan offload | Yes | Yes, UL-11 |
| 14 | Granite 4.1 8B Q4_K_M | Q8_0/Q8_0 on Vulkan0 | Vulkan0 — all layers | Maximum/full 8B Vulkan offload | Yes | Yes, UL-12 |
| 15 | Q4_K_M | Q8_0/Q8_0 | SYCL, all reported layers | Granite 4.1 3B full-offload SYCL validation | Project workload supported; edge suite 49/52 | Yes, UL-13 |

# Device execution and fallback verification

CPU is a full baseline route. CPU fallback is recorded only when a GPU-requested run moves unsupported work back to the CPU.

| Test ID | Requested device | Actual device | Backend | Offload level | TQ device | CPU fallback? | CPU layer placement | GPU layer placement | Evidence path |
|---|---|---|---|---|---|---|---|---|---|
| UL-01 | CPU | CPU | CPU | None | N/A upstream | No | 100% layer placement | 0% layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-01/UL-01-R003/bench-stderr.jsonl |
| UL-02 | CPU | CPU | CPU | None | N/A upstream | No | 100% layer placement | 0% layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-02/UL-02-R001/bench-stderr.jsonl |
| UL-03 | CPU | CPU | CPU | None | N/A upstream | No | 100% layer placement | 0% layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-03/UL-03-R001/bench-stderr.jsonl |
| UL-04 | CPU | CPU | CPU | None | N/A upstream | No | 100% layer placement | 0% layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-04/UL-04-R001/bench-stderr.jsonl |
| UL-05 | CPU | CPU | CPU | None | N/A upstream | No | 100% layer placement | 0% layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-R001/bench-stderr.jsonl |
| UL-06 | CPU | CPU | CPU | None | N/A upstream | No | 100% layer placement | 0% layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-06/UL-06-R001/bench-stderr.jsonl |
| UL-07 | CPU | CPU | CPU | None | N/A upstream | No | 100% layer placement | 0% layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-07/UL-07-R001/bench-stderr.jsonl |
| UL-08 | CPU | CPU | CPU | None | N/A upstream | No | 100% layer placement | 0% layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-08/UL-08-R001/bench-stderr.jsonl |
| UL-09 | Vulkan0 – Intel UHD Graphics | Vulkan0 plus CPU | Vulkan | 1 layer | N/A upstream | Yes, 40 layers | 97.6% model-layer placement | 2.4% model-layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-09/UL-09-R001/bench-stderr.jsonl |
| UL-10 | Vulkan0 — Intel UHD Graphics | Vulkan0 | Vulkan | All 41 layers | N/A upstream | No model-layer fallback | 0% model-layer placement | 100% model-layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-R001/bench-stderr.jsonl |
| UL-11 | Vulkan0 — Intel UHD Graphics | Vulkan0 plus CPU | Vulkan | 1 of 41 layers | N/A upstream | Yes, 40 layers | 97.6% model-layer placement | 2.4% model-layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-11/UL-11-R001/bench-stderr.jsonl |
| UL-12 | Vulkan0 — Intel UHD Graphics | Vulkan0 | Vulkan | All 41 layers | N/A upstream | No model-layer fallback | 0% model-layer placement | 100% model-layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-12/UL-12-R001/bench-stderr.jsonl |
| UL-13 | SYCL0 – Intel UHD Graphics | SYCL0 – Intel UHD Graphics | SYCL | All reported layers (`-ngl 99`) | N/A upstream | No model-layer fallback | 0% model-layer placement | 100% model-layer placement | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-13/UL-13-R002/bench-stdout.jsonl |

# Formal run results

| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak RAM MB | KV MB | TTFT ms | Tok/s | Quality /10 | Status | Practical interpretation |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| UL-01 | Gemma 3 1B diagnostic | Q4_K_M | F16 | F16 | CPU | 1,024 | 936.00 | 26.0 | 602.99 | 37.191 decode; 96.324 prompt | 4.0 | Passed | Q4 diagnostic runtime; all P1-P4 outputs hit objective caps. |
| UL-02 | Granite 4.1 3B | BF16 | F16 | F16 | CPU | 2,048 | 6,712.36 | 160.0 | 1,430.20 | 6.353 decode; 28.676 prompt | 8.9 | Passed | Highest-quality reference but slow and large. |
| UL-03 | Granite 4.1 3B | Q8_0 | F16 | F16 | CPU | 4,096 | 3,829.81 | 320.0 | 2,394.29 | 11.393 decode; 20.643 prompt | 8.9 | Passed | Best measured quality/size compromise. |
| UL-04 | Granite 4.1 3B | Q4_K_M | F16 | F16 | CPU | 4,096 | 3,828.79 | 320.0 | 945.70 | 15.979 decode; 33.993 prompt | 7.1 | Passed | Fastest measured CPU 3B configuration. |
| UL-05 | Granite 4.1 3B | Q4_K_M | Q8_0 | Q8_0 | CPU | 4,096 | 3,678.64 | 170.0 | 939.58 | 15.771 decode; 15.181 prompt | 5.9 P1-P6 | Passed with quality caveats | KV Q8 reduces allocated KV by 150 MB but hurts prefill; P5/P6 exactness is weak. |
| UL-06 | Granite 4.1 8B | Q8_0 | F16 | F16 | CPU | 2,048 | 8,879.95 | 320.0 | 7,246.13 | 4.703 decode; 9.806 prompt | 6.6 | Passed | Fits, but slower and not better on this rubric. |
| UL-07 | Granite 4.1 8B | Q4_K_M | F16 | F16 | CPU | 4,096 | 9,251.02 | 640.0 | 3,172.40 | 7.087 decode; 16.189 prompt | 6.5 | Passed with quality caveat | Best measured 8B CPU throughput; P4 omission capped. |
| UL-08 | Granite 4.1 8B | Q4_K_M | Q8_0 | Q8_0 | CPU | 4,096 | 8,950.21 | 340.0 | 2,978.72 | 7.092 decode; 9.294 prompt | 6.9 | Passed with quality caveats | Q8 KV saves 300 MB allocation but hurts prefill without material decode gain. |
| UL-09 | Granite 4.1 3B | Q4_K_M | Q8_0 | Q8_0 | Vulkan0, 1 layer | 4,096 | 2,558.20 | 170.0 | 1,677.30 | 15.214 decode; 79.856 prompt | 7.3 | Passed | Best interactive GPU route: large prefill gain, near-CPU decode. |
| UL-10 | Granite 4.1 3B | Q4_K_M | Q8_0 | Q8_0 | Vulkan0, all 41 layers | 4,096 | 4,345.35 | 170.0 | 1,044.02 | 9.632 decode; 84.497 prompt | 7.3 | Passed with performance regression | Full offload slightly improves prefill but sharply reduces decode. |
| UL-11 | Granite 4.1 8B | Q4_K_M | Q8_0 | Q8_0 | Vulkan0, 1 of 41 layers | 4,096 | 5,773.38 | 340.0 | 3,339.78 | 6.219 decode; 31.794 prompt | 6.8 | Passed | Useful prefill gain; moderate decode loss. |
| UL-12 | Granite 4.1 8B | Q4_K_M | Q8_0 | Q8_0 | Vulkan0, all 41 layers | 4,096 | 10,308.56 | 340.0 | 2,492.26 | 4.385 decode; 33.785 prompt | 6.8 | Passed with performance regression | Full offload is worse for interactive decode and uses the most measured RAM. |
| UL-13 | Granite 4.1 3B | Q4_K_M | Q8_0 | Q8_0 | SYCL0, all reported layers | 4,096 | 5,274.96 | 170.0 | 2,314.26 | 7.211 decode; 40.789 prompt | 6.8 | Passed with edge-suite limitation | Formal project inference passes; the broad SYCL suite remains 49/52. |

Use one row per formal configuration. Preserve the detailed command, raw log and output in the evidence folder.

# Quality and context summary

| Prompt ID | Task | Baseline score /10 | Optimised score /10 | Format valid? | Required facts retained? | Repetition/corruption? | Notes |
|---|---|---|---|---|---|---|---|
| P1 | Sanity explanation | 9.5, UL-02 BF16 | 4.0, UL-09 | No; required-term/slot cap | Partly | No | Fluent output is not enough: optimised answer misses exact rubric constraints. |
| P2 | Instruction following | 9.0, UL-02 BF16 | 8.5, UL-09 | Yes | Mostly | No | Optimised answer is useful but slightly generic. |
| P3 | Exact JSON structure | 7.0, UL-02 BF16 | 6.5, UL-09 | Yes | Partly | No | JSON parses, but unsupported labels reduce grounding score. |
| P4 | Summarisation | 10.0, UL-02 BF16 | 10.0, UL-09 | Yes | Yes | No | Required facts retained without corruption. |
| P5 | Long-context retrieval | Not run on BF16 | 4.0, UL-05 | No; missing `MARKER:` prefix | Value retained; prefix omitted | No | 16,384 context required; exact-format cap applied. |
| P6 | Multi-turn stability | Not run on BF16 | 2.0, UL-05 | No; exact value not retained | Partly | No | Turn 2 returned `4821`, not exact `amber:4821`. |

## Failure log

| Failure ID | Test ID | Code | Description | Likely cause | Next action | Resolved? | Evidence |
|---|---|---|---|---|---|---|---|
| UL-F01 | UL-B05 | REPRO | R001 cache validator reported failure although required keys existed. | End-of-line regex rejected CRLF. | Correct validator and rerun. | Yes, R002/R003 | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B05/UL-B05-R002/R001-cache-validation-failure-analysis.md |
| UL-F02 | UL-B05 | DEP | Vulkan repository suite initially passed 51/52. | Jinja2 was absent from test PATH. | Add isolated workbook venv to PATH and rerun. | Yes, 52/52 | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B05/UL-B05-R002/repository-test-failure-analysis.md |
| UL-F03 | UL-B06/UL-13 | GPU | SYCL broad suite passed 49/52; FP64 unsupported, device lost, and two 0xc0000409 crashes. | Edge operations exceed or destabilise current Intel UHD capabilities; they do not reproduce in the Granite Q4 workload. | Keep the edge-suite limitation visible; project workload may pass independently. | Project workload resolved; edge tests unresolved | experiments/granite_turboquant_intel/notes/upstream-llama-cpp/UL-B06/UL-B06-R002/repository-test-failure-analysis.md |
| UL-F04 | UL-05/P5 | QUAL | Long-context output omitted required literal prefix. | Model followed semantic retrieval but not exact output contract. | Retain 4-point cap; use constrained validation for app integration. | No | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P5-R004/stdout.txt |
| UL-F05 | UL-05/P6 | QUAL | Multi-turn exact value degraded from `amber:4821` to `4821`. | Weak exact state retention. | Retain 2-point cap; externalise critical state. | No | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-05/UL-05-P6-R001/P6-turn2-with-history-stdout.txt |
| UL-F06 | UL-10/UL-12 | PERF | Full Vulkan offload reduced decode throughput relative to one-layer offload. | Integrated GPU is better used for prefill than full interactive decode. | Prefer one-layer Vulkan for interactive workloads. | Configuration issue, not test failure | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-10/UL-10-R001/bench-stdout.jsonl |
| UL-F07 | All formal runs | MEM | Initial run omitted peak RAM, KV MB and TTFT because llama-bench does not emit all three. | Measurement-layer omission in the first harness. | Added tested 100 ms process-tree RAM sampling, deduplicated runtime KV parsing and loaded llama-server request-initiation-to-first-streamed-token timing; reran every row three times. | Yes | experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/resource-metrics-2026-07-16.json |

Codes: BF build | DEP dependency | WIN Windows | MODEL format | ARCH architecture | BASE baseline | TQ-ACT activation | TQ-FALLBACK fallback | TQ-CRASH crash | CPU | GPU | HYBRID | OOM | MEM | PERF | QUAL | REPRO | SCOPE

# Final repository/runtime decision

| Field | Record |
|---|---|
| Granite 3B baseline | UL-03 Q8_0 weights, F16 KV, CPU: 11.393 decode tok/s, 20.643 prompt tok/s, quality 8.9/10. |
| Granite 8B baseline | UL-07 Q4_K_M weights, F16 KV, CPU: 7.087 decode tok/s, 16.189 prompt tok/s, quality 6.5/10. |
| Best CPU configuration | UL-04 for speed: Granite 3B Q4_K_M, F16 KV, CPU; UL-03 when quality is primary. |
| Best Intel GPU configuration | UL-09 Granite 3B Q4_K_M, Q8 KV, Vulkan one-layer offload: 79.856 prompt and 15.214 decode tok/s. |
| Maximum stable context | 16,384 tokens demonstrated on UL-05 P5; the formal performance matrix used at most 4,096. |
| Recommended fallback configuration | UL-04 CPU F16 KV for throughput, or UL-03 CPU F16 KV for quality-sensitive work. |
| Final status | Conditional pass: all thirteen project workloads pass with complete RAM/KV/TTFT measurements; CPU and Vulkan suites pass 52/52; the broader SYCL suite retains three edge-case failures. |
| Application role | Use upstream llama.cpp as the conventional reference runtime and Vulkan one-layer route; SYCL is validated for this Granite workload but not for every upstream backend operation. |
| Main evidence path | experiments/granite_turboquant_intel/logs/upstream-llama-cpp/ and experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp/quality-scoring-2026-07-15.md |
| Final reasoning | Q8_0 3B is the quality reference. Q4_K_M/F16 KV is the CPU speed choice. Q8 KV materially reduces allocation but sharply harms prefill here. One-layer Vulkan gives the best interactive balance. Full Vulkan offload regresses decode and raises RAM. SYCL project inference passes but is slower and retains three unrelated edge-suite failures. |
