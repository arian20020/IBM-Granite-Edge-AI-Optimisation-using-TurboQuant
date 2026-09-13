# 02 AtomicBot TurboQuant Controlled Retest Workbook v1

**Controlled filename:** `02_AtomicBot_TurboQuant_Controlled_Retest_Workbook_v1.docx`
**Generated DOCX hash:** recorded in `Controlled-Workbook-Manifest.csv`
**Original source:** `AtomicBot_TurboQuant_Workbook_Quality_Verifier_v3.docx`
**Original source SHA-256:** `2c399f960b9a1e15e62a24fcb2208e8757db96d077dfdf4aa6ffab1e79df7570`

## AtomicBot TurboQuant Controlled Retest Workbook

Controlled retest template v1. Historical results were removed from this working copy; the original source document is preserved under docs/testing/source-material/original-workbooks/.

Purpose: evaluate the AtomicBot llama.cpp TurboQuant fork from a clean, pinned checkout using matched non-TurboQuant baselines, turbo4/turbo3/turbo2 CPU tests, and guarded Intel GPU tests where supported.

Use this workbook during the controlled retest. Record exact versions, commands, logs, hashes and evidence paths. Run one pilot, one excluded warm-up and at least three measured repetitions for formal performance results unless a documented safety gate prevents this.

# 1. Repository and environment record

| Field | Record |
|---|---|
| Repository URL | https://github.com/AtomicBot-ai/atomic-llama-cpp-turboquant |
| Branch/tag | Detached exact-commit checkout (remote default branch tip at acquisition) |
| Pinned commit | `519f0c594a8e31467d2e2f2cf17054c9e7e11536` |
| Upstream llama.cpp base commit | Not published as a single derivable base commit at the tested pin; source audit retained instead |
| Exposed TurboQuant formats | KV: `turbo2`, `turbo3`, `turbo4`; source also exposes TQ3_1S/TQ4_1S weight types |
| CPU backend | Pass; reference CPU paths exercised |
| Vulkan support | Pass with limitations; partial and full placement exercised on Intel UHD |
| SYCL support | Unsupported for a TurboQuant-specific route at this pin; generic SYCL source exists but no TQ-specific implementation evidence |
| Binary version | AtomicBot fork commit `519f0c5`; MSVC 19.51; CMake 4.3.1; Ninja 1.13.2 |
| Test operator | Student with project tooling-controlled harness |
| Test start date | 2026-07-16 |
| Test end date | 2026-07-16 |
| Overall status | Accepted with limitations: all 19 runtime rows passed, including controlled safety-gate bypasses; one repository test remains blocked by Device Guard |

## 2. Target laptop

| Field | Fixed/current value | Confirm or update |
|---|---|---|
| Machine ID | Lenovo-PF4HMD0T | Confirmed |
| Processor | 12th Gen Intel Core i5-12450H | Confirmed; 12 logical processors |
| RAM | 16.0 GB installed; 15.7 GB usable | Confirmed; shared-memory safety gates enforced |
| Graphics | Intel UHD Graphics; shared system memory | Confirmed as Vulkan0 |
| Operating system | 64-bit Windows, x64-based processor | Confirmed; Windows build reported as 10.0.26200 |
| NPU | Not available - excluded from current testing | Confirmed; N/A |

# 3. Build and setup checklist

| ID | Check | Result | Evidence / command / notes |
|---|---|---|---|
| AB-B01 | Clone exact commit | Pass | Clean detached checkout and origin verified in `acquisition/results/AtomicBot_Environment_Manifest.json` |
| AB-B02 | Configure clean Windows CPU build | Pass | Fresh build root `C:/Users/Student/ab-retest-20260716/cpu`; UI bundle repair copied the pinned `loading.html` into generated build output only |
| AB-B03 | Build required tools | Pass | `llama-cli`, `llama-server`, `llama-perplexity` and test binaries built; hashes retained in `acquisition/results/AtomicBot_cpu_Binary_Hashes.json` |
| AB-B04 | Run repository-provided tests | Blocked/partial | 42/43 effective passes after installing Jinja2 3.1.6; `test-barrier.exe` blocked by Windows Device Guard (exit 4551), not a product assertion failure |
| AB-B05 | Configure and verify Vulkan build | Pass | Fresh Vulkan build plus runtime placement evidence; Vulkan SDK 1.4.350.0 |
| AB-B06 | Configure SYCL if supported | Unsupported | No TurboQuant-specific SYCL implementation evidence at pin; generic SYCL presence is not counted as TQ support |
| AB-B07 | Confirm cache flags | Pass | `-ctk/-ctv` accepted and runtime allocations prove `turbo2/3/4` activation |
| AB-B08 | Record Intel limitations | Pass | Shared-memory gates, missing GPU utilisation counter, hybrid placement and Device Guard limitation recorded |

## 3.1 Frozen model manifest

| Model | Frozen file | Size | SHA-256 |
|---|---|---|---|
| Diagnostic | gemma-3-1b-it-Q4_K_M.gguf | 0.751 GiB | 8ccc5cd1f1b3602548715ae25a66ed73fd5dc68a210412eea643eb20eb75a135 |
| Granite 3B | granite-4.1-3b-Q4_K_M.gguf | 1.955 GiB | 662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29 |
| Granite 8B | granite-4.1-8b-Q4_K_M.gguf | 4.981 GiB | ed902ac9eb6adce5a90c6a08c8ea201b50e23fdc5976d1cd0362006afac5309e |

All formal runs must use exact frozen GGUF files whose hashes are confirmed before execution.

# 4. Ordered test matrix

| ID | Model | KV cache | Execution | Context | Purpose / limitation | Status |
|---|---|---|---|---|---|---|
| AB-01 | Gemma 3 1B diagnostic | F16 | CPU | 1K | Fork baseline - TurboQuant off | Pass |
| AB-02 | Gemma 3 1B diagnostic | turbo3 | CPU | 1K | Prove TurboQuant switch activates | Pass |
| AB-03 | Granite 4.1 3B | F16 | CPU | 2K | High-quality 3B fork baseline | Pass |
| AB-KV3-F16-4K | Granite 4.1 3B | F16 | CPU | 4K | Supplemental matched 3B F16 allocation baseline | Pass |
| AB-04 | Granite 4.1 3B | Q8_0 | CPU | 4K | Conventional compressed 3B baseline | Pass |
| AB-05 | Granite 4.1 3B | turbo4 | CPU | 4K | Conservative 3B TurboQuant | Pass |
| AB-06 | Granite 4.1 3B | turbo3 | CPU | 4K | Main 3B TurboQuant candidate | Pass |
| AB-07 | Granite 4.1 3B | turbo2 | CPU | 4K | Aggressive 3B TurboQuant | Pass |
| AB-08F | Granite 4.1 8B | F16 | CPU | 2K | 8B high-quality fork baseline | Pass |
| AB-KV8-F16-4K | Granite 4.1 8B | F16 | CPU | 4K | Supplemental matched 8B F16 allocation baseline; guarded by memory gate | Pass - controlled bypass |
| AB-08Q | Granite 4.1 8B | Q8_0 | CPU | 4K | 8B matched conventional baseline | Pass after isolated rerun |
| AB-09 | Granite 4.1 8B | turbo4 | CPU | 4K | 8B conservative TurboQuant | Pass |
| AB-10 | Granite 4.1 8B | turbo3 | CPU | 4K | 8B main TurboQuant | Pass |
| AB-11 | Granite 4.1 3B | F16 | Vulkan partial (ngl=1) | 4K | 3B partial Vulkan baseline | Pass |
| AB-12 | Granite 4.1 3B | turbo3 | Vulkan partial (ngl=1) | 4K | 3B partial Vulkan TurboQuant and fallback check | Pass; hybrid |
| AB-13 | Granite 4.1 3B | turbo3 | Vulkan full/max (ngl=999) | 4K | 3B maximum/full Vulkan TurboQuant | Pass; 41/41 layers |
| AB-14 | Granite 4.1 8B | Q8_0 | Vulkan partial (ngl=1) | 4K | 8B safe partial Vulkan baseline | Pass; hybrid |
| AB-15 | Granite 4.1 8B | turbo3 | Vulkan partial (ngl=1) | 4K | 8B safe partial Vulkan TurboQuant | Pass; hybrid |
| AB-15M | Granite 4.1 8B | turbo3 | Vulkan full/max (ngl=999) | 4K | Conditional 8B maximum Vulkan TurboQuant | Pass - controlled bypass; 41/41 layers |

AB-KV8-F16-4K and AB-15M began as research-only because of the 16 GB shared-memory ceiling. They were later executed alone from an idle system with a 256 MiB emergency-stop floor; both passed without crossing the floor. This bypass does not make concurrent or unattended execution safe.

# 5. AtomicBot configuration ladder

| Order | Model weights | KV cache | Device | Purpose | Supported? | Selected for formal test? |
|---|---|---|---|---|---|---|
| 1 | Frozen Q4_K_M GGUF weights | F16 | CPU | Fork baseline; TurboQuant off | Yes | Yes |
| 2 | Same weights | Q8_0 | CPU | Conventional compressed baseline | Yes | Yes |
| 3 | Same weights | turbo4 | CPU | Conservative TurboQuant | Yes | Yes |
| 4 | Same weights | turbo3 | CPU | Main memory/quality candidate | Yes | Yes |
| 5 | Same weights | turbo2 | CPU | Aggressive compression | Yes | Yes |
| 6 | Same weights | F16 | Vulkan | GPU baseline | Yes | Yes, partial 3B |
| 7 | Same weights | turbo3 | Vulkan | Hybrid and GPU-native verification | Yes | Yes, partial and full 3B |
| 8 | Same weights | Q8_0/turbo3 | Vulkan 8B | Separate memory-safe campaign | Yes with high-memory controls | Yes; partial and full bypass validated serially |

# 6. Device execution and fallback verification

Generation alone is not proof of GPU execution. The gate requires selected device, exact layer placement, model/compute buffer evidence, correct cache location and no unexplained fallback.

| Test ID | Requested device | Actual device | Backend | Offload evidence | TQ/KV device | CPU fallback? | CPU utilization | GPU utilization | Evidence path |
|---|---|---|---|---|---|---|---|---|---|
| AB-01 | CPU | CPU | CPU | All layers on CPU | CPU, 26.00 MiB | N/A - CPU requested | mean 64.51%; median 64.09%; peak 66.41% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-01/` |
| AB-02 | CPU | CPU | CPU | All layers on CPU | CPU, 5.08 MiB | N/A - CPU requested | mean 63.66%; median 63.41%; peak 65.85% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-02/` |
| AB-03 | CPU | CPU | CPU | All layers on CPU | CPU, 160.00 MiB | N/A - CPU requested | mean 63.90%; median 65.14%; peak 67.38% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-03/` |
| AB-KV3-F16-4K | CPU | CPU | CPU | All layers on CPU | CPU, 320.00 MiB | N/A - CPU requested | mean 63.40%; median 64.42%; peak 65.92% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-KV3-F16-4K/` |
| AB-04 | CPU | CPU | CPU | All layers on CPU | CPU, 170.00 MiB | N/A - CPU requested | mean 64.94%; median 65.34%; peak 67.43% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-04/` |
| AB-05 | CPU | CPU | CPU | All layers on CPU | CPU, 170.00 MiB | N/A - CPU requested | mean 64.71%; median 65.17%; peak 66.92% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-05/` |
| AB-06 | CPU | CPU | CPU | All layers on CPU | CPU, 125.00 MiB | N/A - CPU requested | mean 65.00%; median 64.89%; peak 67.43% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-06/` |
| AB-07 | CPU | CPU | CPU | All layers on CPU | CPU, 89.25 MiB | N/A - CPU requested | mean 65.45%; median 65.27%; peak 67.58% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-07/` |
| AB-08F | CPU | CPU | CPU | All layers on CPU | CPU, 320.00 MiB | N/A - CPU requested | mean 65.07%; median 65.28%; peak 67.58% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-08F/` |
| AB-KV8-F16-4K | CPU | CPU | CPU | All layers on CPU | CPU, 640.00 MiB | N/A - CPU requested | mean 65.15%; median 65.38%; peak 67.71% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-KV8-F16-4K/` |
| AB-08Q | CPU | CPU | CPU | All layers on CPU | CPU, 340.00 MiB | N/A - CPU requested | mean 64.95%; median 65.51%; peak 67.57% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-08Q/` |
| AB-09 | CPU | CPU | CPU | All layers on CPU | CPU, 170.00 MiB | N/A - CPU requested | mean 65.36%; median 65.74%; peak 67.29% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-09/` |
| AB-10 | CPU | CPU | CPU | All layers on CPU | CPU, 125.00 MiB | N/A - CPU requested | mean 65.30%; median 65.71%; peak 67.39% | mean 0.00%; median 0.00%; peak 0.00% | `2026-07-17/all-row-utilization-v1/AB-10/` |
| AB-11 | Vulkan partial | Intel UHD Vulkan0 + CPU | Vulkan hybrid | 1/41 layers; 200.99 MiB model and 86.77 MiB compute buffers on Vulkan0 | CPU, 320.00 MiB | Yes, intended hybrid | mean 64.43%; median 64.88%; peak 66.82% | mean 25.74%; median 25.00%; peak 43.00% | `2026-07-17/all-row-utilization-v1/AB-11/` |
| AB-12 | Vulkan partial | Intel UHD Vulkan0 + CPU | Vulkan hybrid | 1/41 layers; 200.99 MiB model and 82.77 MiB compute buffers on Vulkan0 | CPU, 125.13 MiB | Yes, intended hybrid | mean 65.30%; median 65.80%; peak 67.28% | mean 25.71%; median 23.00%; peak 41.00% | `2026-07-17/all-row-utilization-v1/AB-12/` |
| AB-13 | Vulkan maximum | Intel UHD Vulkan0 | Vulkan native placement | 41/41 layers; 1998.84 MiB model and 62.01 MiB compute buffers on Vulkan0 | Vulkan0, 125.13 MiB | No unexplained fallback | mean 2.17%; median 2.21%; peak 2.96% | mean 92.44%; median 96.00%; peak 100.00% | `2026-07-17/all-row-utilization-v1/AB-13/` |
| AB-14 | Vulkan partial 8B | Intel UHD Vulkan0 + CPU | Vulkan hybrid | 1/41 layers; 321.58 MiB model and 111.38 MiB compute buffers on Vulkan0 | CPU, 340.00 MiB | Yes, intended hybrid | mean 65.41%; median 65.41%; peak 67.44% | mean 26.00%; median 26.00%; peak 44.00% | `2026-07-17/all-row-utilization-v1/AB-14/` |
| AB-15 | Vulkan partial 8B | Intel UHD Vulkan0 + CPU | Vulkan hybrid | 1/41 layers; 321.58 MiB model and 120.13 MiB compute buffers on Vulkan0 | CPU, 125.13 MiB | Yes, intended hybrid | mean 65.14%; median 65.51%; peak 67.63% | mean 24.94%; median 23.00%; peak 45.00% | `2026-07-17/all-row-utilization-v1/AB-15/` |
| AB-15M | Vulkan maximum 8B | Intel UHD Vulkan0 | Vulkan native placement | 41/41 layers; 4876.27 MiB model and 95.01 MiB compute buffers on Vulkan0 | Vulkan0, 125.13 MiB | No unexplained fallback | mean 2.08%; median 2.10%; peak 2.88% | mean 95.72%; median 97.50%; peak 100.00% | `2026-07-17/all-row-utilization-v1/AB-15M/` |

# 7. Formal run results

All 19 runtime rows were repeated under one utilization protocol on 2026-07-17. CPU is normalized across logical processors; GPU is the busiest PID-attributable Windows GPU Engine at each timestamp. CPU-only rows measured 0.00% attributable GPU use; this value was sampled, not inferred.

Numeric rows report median TTFT and generation throughput across measured runs, maximum peak working set and observed KV allocation. Quality is scored separately with the controlled 0-10 rubric.

| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak WS MiB | KV MiB | TTFT ms | Tok/s | CPU mean/median/peak % | GPU mean/median/peak % | Quality /10 | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AB-01 | Gemma 3 1B | Q4_K_M | f16 | f16 | CPU | 1024 | 926.988 | 26.00 | 72.314 | 37.50 | 64.51 / 64.09 / 66.41 | 0.00 / 0.00 / 0.00 | 64.51 / 64.09 / 66.41 | 0.00 / 0.00 / 0.00 |
| AB-02 | Gemma 3 1B | Q4_K_M | turbo3 | turbo3 | CPU | 1024 | 906.898 | 5.08 | 77.017 | 30.80 | 63.66 / 63.41 / 65.85 | 0.00 / 0.00 / 0.00 | 63.66 / 63.41 / 65.85 | 0.00 / 0.00 / 0.00 |
| AB-03 | 3B | Q4_K_M | f16 | f16 | CPU | 2048 | 3659.074 | 160.00 | 143.190 | 15.90 | 63.90 / 65.14 / 67.38 | 0.00 / 0.00 / 0.00 | 63.90 / 65.14 / 67.38 | 0.00 / 0.00 / 0.00 |
| AB-KV3-F16-4K | 3B | Q4_K_M | f16 | f16 | CPU | 4096 | 3819.336 | 320.00 | 161.621 | 15.10 | 63.40 / 64.42 / 65.92 | 0.00 / 0.00 / 0.00 | 63.40 / 64.42 / 65.92 | 0.00 / 0.00 / 0.00 |
| AB-04 | 3B | Q4_K_M | q8_0 | q8_0 | CPU | 4096 | 3669.191 | 170.00 | 155.285 | 15.90 | 64.94 / 65.34 / 67.43 | 0.00 / 0.00 / 0.00 | 64.94 / 65.34 / 67.43 | 0.00 / 0.00 / 0.00 |
| AB-05 | 3B | Q4_K_M | turbo4 | turbo4 | CPU | 4096 | 3670.609 | 170.00 | 148.400 | 11.80 | 64.71 / 65.17 / 66.92 | 0.00 / 0.00 / 0.00 | 64.71 / 65.17 / 66.92 | 0.00 / 0.00 / 0.00 |
| AB-06 | 3B | Q4_K_M | turbo3 | turbo3 | CPU | 4096 | 3624.723 | 125.00 | 138.507 | 11.80 | 65.00 / 64.89 / 67.43 | 0.00 / 0.00 / 0.00 | 65.00 / 64.89 / 67.43 | 0.00 / 0.00 / 0.00 |
| AB-07 | 3B | Q4_K_M | turbo2 | turbo2 | CPU | 4096 | 3589.137 | 89.25 | 140.418 | 13.10 | 65.45 / 65.27 / 67.58 | 0.00 / 0.00 / 0.00 | 65.45 / 65.27 / 67.58 | 0.00 / 0.00 / 0.00 |
| AB-08F | 8B | Q4_K_M | f16 | f16 | CPU | 2048 | 8916.195 | 320.00 | 366.771 | 6.80 | 65.07 / 65.28 / 67.58 | 0.00 / 0.00 / 0.00 | 65.07 / 65.28 / 67.58 | 0.00 / 0.00 / 0.00 |
| AB-KV8-F16-4K | 8B | Q4_K_M | f16 | f16 | CPU | 4096 | 9236.535 | 640.00 | 350.377 | 8.37 | 65.15 / 65.38 / 67.71 | 0.00 / 0.00 / 0.00 | 65.15 / 65.38 / 67.71 | 0.00 / 0.00 / 0.00 |
| AB-08Q | 8B | Q4_K_M | q8_0 | q8_0 | CPU | 4096 | 8936.547 | 340.00 | 341.672 | 6.80 | 64.95 / 65.51 / 67.57 | 0.00 / 0.00 / 0.00 | 64.95 / 65.51 / 67.57 | 0.00 / 0.00 / 0.00 |
| AB-09 | 8B | Q4_K_M | turbo4 | turbo4 | CPU | 4096 | 8767.629 | 170.00 | 278.364 | 5.90 | 65.36 / 65.74 / 67.29 | 0.00 / 0.00 / 0.00 | 65.36 / 65.74 / 67.29 | 0.00 / 0.00 / 0.00 |
| AB-10 | 8B | Q4_K_M | turbo3 | turbo3 | CPU | 4096 | 8721.734 | 125.00 | 304.354 | 5.90 | 65.30 / 65.71 / 67.39 | 0.00 / 0.00 / 0.00 | 65.30 / 65.71 / 67.39 | 0.00 / 0.00 / 0.00 |
| AB-11 | 3B | Q4_K_M | f16 | f16 | Vulkan partial | 4096 | 2905.742 | 320.00 | 177.658 | 14.00 | 64.43 / 64.88 / 66.82 | 25.74 / 25.00 / 43.00 | 64.43 / 64.88 / 66.82 | 25.74 / 25.00 / 43.00 |
| AB-12 | 3B | Q4_K_M | turbo3 | turbo3 | Vulkan partial | 4096 | 2707.188 | 125.00 | 179.577 | 9.60 | 65.30 / 65.80 / 67.28 | 25.71 / 23.00 / 41.00 | 65.30 / 65.80 / 67.28 | 25.71 / 23.00 / 41.00 |
| AB-13 | 3B | Q4_K_M | turbo3 | turbo3 | Vulkan full | 4096 | 4542.570 | 125.00 | 438.734 | 7.60 | 2.17 / 2.21 / 2.96 | 92.44 / 96.00 / 100.00 | 2.17 / 2.21 / 2.96 | 92.44 / 96.00 / 100.00 |
| AB-14 | 8B | Q4_K_M | q8_0 | q8_0 | Vulkan partial | 4096 | 6075.352 | 340.00 | 540.764 | 6.00 | 65.41 / 65.41 / 67.44 | 26.00 / 26.00 / 44.00 | 65.41 / 65.41 / 67.44 | 26.00 / 26.00 / 44.00 |
| AB-15 | 8B | Q4_K_M | turbo3 | turbo3 | Vulkan partial | 4096 | 5869.441 | 125.00 | 514.448 | 5.40 | 65.14 / 65.51 / 67.63 | 24.94 / 23.00 / 45.00 | 65.14 / 65.51 / 67.63 | 24.94 / 23.00 / 45.00 |
| AB-15M | 8B | Q4_K_M | turbo3 | turbo3 | Vulkan full | 4096 | 10456.277 | 125.00 | 687.208 | 4.19 | 2.08 / 2.10 / 2.88 | 95.72 / 97.50 / 100.00 | 2.08 / 2.10 / 2.88 | 95.72 / 97.50 / 100.00 |

# 8. Compression and bounded perplexity supplements

| Configuration | Reference KV MiB | Test KV MiB | Smaller by | Reduction | Interpretation |
|---|---|---|---|---|---|
| Diagnostic 1B turbo3 | 26.00 | 5.08 | 5.118x | 80.46% | Direct 1K matched allocation reduction versus F16 |
| Granite 3B Q8_0 | 320.00 | 170.00 | 1.882x | 46.88% | Direct matched 4K conventional baseline |
| Granite 3B turbo4 | 320.00 | 170.00 | 1.882x | 46.88% | Direct matched 4K allocation reduction; no advantage over Q8_0 here |
| Granite 3B turbo3 | 320.00 | 125.00 | 2.560x | 60.94% | Direct matched 4K allocation reduction; CPU quality 5.95 versus Q8_0 6.72 |
| Granite 3B turbo2 | 320.00 | 89.25 | 3.585x | 72.11% | Largest measured reduction; severe all-row quality failure at 0.70 |
| Granite 8B turbo4 | 640.00 | 170.00 | 3.765x | 73.44% | Direct matched 4K allocation reduction after isolated F16 safety-gate bypass |
| Granite 8B turbo3 | 640.00 | 125.00 | 5.120x | 80.47% | Direct matched 4K allocation reduction after isolated F16 safety-gate bypass |

| KV cache | PPL | Exit code | Bounded interpretation |
|---|---|---|---|
| f16 | 1.0010 | 0 | Synthetic regression corpus v1 only; not general perplexity evidence |
| q8_0 | 1.0009 | 0 | Synthetic regression corpus v1 only; delta is not a broad quality claim |
| turbo4 | 1.0014 | 0 | Synthetic regression corpus v1 only; narrow implementation check |
| turbo3 | 1.0197 | 0 | Synthetic regression corpus v1 only; narrow degradation signal |
| turbo2 | 1.8613 | 0 | Synthetic regression corpus v1 only; strong narrow degradation signal |

Perplexity must use a named, versioned dataset. A synthetic repeated sentence is only a narrow implementation-regression check and cannot support general quality claims.

# 9. Model quality verifier and context summary

Every runtime row was independently run on P1-P6. P1-P4 and P6 use a practical 4K quality server for 8B rows; P5 alone uses the frozen 8,706-token fixture at 16K. A controlled timeout or RAM safety block is a measured zero for P5, not a pass or an imputed response.

| Test ID | Cache | Backend | P1 | P2 | P3 | P4 | P5 | P6 | Mean /10 | Main cap or limitation |
|---|---|---|---|---|---|---|---|---|---|---|
| AB-01 | F16 | CPU | 9.00 | 8.30 | 2.00 | 4.00 | 4.00 | 10.00 | 6.22 | Fenced/wrong-schema JSON; P4 missing runtime; P5 spacing |
| AB-02 | turbo3 | CPU | 4.00 | 8.30 | 2.00 | 4.00 | 4.00 | 10.00 | P1 required-term failure; wrong JSON schema; P5 spacing |
| AB-03 | F16 | CPU | 4.00 | 8.30 | 4.00 | 10.00 | 10.00 | 2.00 | P1 semantic-slot failure; wrong memory claim; P6 lost prefix |
| AB-KV3-F16-4K | F16 | CPU | 4.00 | 8.30 | 4.00 | 10.00 | 10.00 | 2.00 | Same independently observed response failures as AB-03 |
| AB-04 | Q8_0 | CPU | 4.00 | 8.30 | 4.00 | 10.00 | 4.00 | 10.00 | P1 slot failure; wrong memory claim; P5 spacing |
| AB-05 | turbo4 | CPU | 4.00 | 8.30 | 8.70 | 10.00 | 0.00 | 10.00 | P1 incomplete; P5 2,400-second timeout |
| AB-06 | turbo3 | CPU | 9.00 | 4.00 | 8.70 | 4.00 | 0.00 | 10.00 | Lossless-check contradiction; P4 omissions; P5 timeout |
| AB-07 | turbo2 | CPU | 0.60 | 0.70 | 2.00 | 1.00 | 0.00 | 0.00 | 0.70 | Severe corruption/incompleteness across the screen |
| AB-08F | F16 | CPU | 4.00 | 8.30 | 7.80 | 4.00 | 0.00 | 10.00 | 5.69 | P1/P4 omissions; 16K P5 safety-blocked |
| AB-KV8-F16-4K | F16 | CPU | 4.00 | 8.30 | 7.80 | 4.00 | 0.00 | 10.00 | 5.69 | Independently observed same caps; P5 safety-blocked |
| AB-08Q | Q8_0 | CPU | 4.00 | 8.30 | 7.80 | 10.00 | 0.00 | 10.00 | 6.69 | P1 slot failure; P5 safety-blocked |
| AB-09 | turbo4 | CPU | 4.00 | 4.00 | 7.80 | 10.00 | 0.00 | 10.00 | 5.97 | Quantum-circuit hallucination; P5 safety-blocked |
| AB-10 | turbo3 | CPU | 9.00 | 8.30 | 8.70 | 4.00 | 0.00 | 10.00 | 6.67 | P4 missing upstream wording; P5 safety-blocked |
| AB-11 | F16 | Vulkan partial | 4.00 | 8.30 | 4.00 | 10.00 | 4.00 | 10.00 | 6.72 | P1/P3 caps; P5 marker spacing |
| AB-12 | turbo3 | Vulkan partial | 4.00 | 7.30 | 4.00 | 10.00 | 10.00 | 2.00 | 6.22 | P1 count, wrong memory claim and P6 prefix loss |
| AB-13 | turbo3 | Vulkan full | 9.00 | 8.30 | 2.00 | 4.00 | 10.00 | 10.00 | 7.22 | Missing JSON key; P4 model wording cap |
| AB-14 | Q8_0 | Vulkan partial | 4.00 | 8.30 | 7.80 | 10.00 | 10.00 | 10.00 | 8.36 | Highest mean; P1 still critically capped |
| AB-15 | turbo3 | Vulkan partial | 4.00 | 8.30 | 8.70 | 4.00 | 10.00 | 10.00 | 7.51 | P1 slot and P4 sentence-count caps |
| AB-15M | turbo3 | Vulkan full | 9.00 | 8.30 | 8.70 | 4.00 | 0.00 | 10.00 | 6.67 | P4 wording cap; 16K P5 safety-blocked |

These scores are response-derived, not ordered by nominal precision. AB-14 ranks highest at 8.36; AB-07 ranks lowest at 0.70 because its outputs are visibly corrupted. Preserve every raw output, hash and deterministic validation result under `2026-07-17/quality-all-rows/`.

## 9.1 Quality Verifier v2.0 methodology

| Quality dimension | Weight | How it is verified | Critical cap / rule |
|---|---|---|---|
| Correctness and grounding | 30% | Check claims against prompt, supplied facts, reference answer or deterministic oracle. | Material contradiction, fabricated required fact or wrong exact answer: maximum 4/10. |
| Instruction and format adherence | 25% | Programmatic checks for word limits, required terms, exact structure, JSON parsing/schema and prohibited content. | Failed required format/schema: maximum 4/10; unnecessary refusal: 0/10. |
| Completeness and fact retention | 20% | Verify every required fact, step, field and constraint. | Missing critical required fact: maximum 4/10. |
| Relevance, clarity and coherence | 15% | Assess directness, understandability and internal consistency. | Severe incoherence/off-topic output: maximum 2/10. |
| Stability and output integrity | 10% | Detect repetition, corruption, truncation and multi-turn memory failure. | Corruption, repeated loops or wrong remembered value: maximum 2/10. |

| Score | Anchored interpretation |
|---|---|
| 10 | Fully correct, complete, relevant and exactly compliant; no material weakness. |
| 8 | Correct and usable with only a minor, non-material weakness. |
| 6 | Mostly correct, but one meaningful omission, ambiguity or quality weakness remains. |
| 4 | Partially correct or useful, but a critical instruction, fact or format requirement failed. |
| 2 | Minimal usable content; major errors, instability or severe incompleteness. |
| 0 | No usable answer, empty output, unnecessary refusal or wholly incorrect/corrupted output. |

Evaluation order: deterministic gates, independent blinded scoring, reverse-order pairwise comparison, manual adjudication of critical failures/disagreement/ranking reversal, and per-task reporting. An LLM judge never overrides an objective failed check.

# 10. Failure and issue log

| Issue ID | Test ID | Code | Description | Likely cause | Action / resolution | Resolved? | Evidence |
|---|---|---|---|---|---|---|---|
| AB-I01 | AB-B02/AB-B05 | UI-BUNDLE-MISSING | Generated UI embed target lacked `loading.html` | Fork build always provisions UI despite `LLAMA_BUILD_UI=OFF`; downloaded bundle incomplete | Copied exact pinned source file into each generated build `tools/ui/dist`, hash-checked, then resumed | Yes | `build-cpu/` and `build-vulkan/` logs |
| AB-I02 | AB-B04 | DEVICE-GUARD-4551 | `test-barrier.exe` cannot launch | Windows Device Guard content policy; renamed binary and dependency audit gave same result | Classified external-policy blocked; 42 other repository tests effective-pass | No - external policy | `build-cpu/ctest*` and `acquisition/results/AtomicBot_Failure_Log.json` |
| AB-I03 | AB-08Q | MEMORY-GUARD | Initial 8B Q8_0 batch stopped at 470.7 MiB free | Earlier 8B work left insufficient shared memory | Isolated rerun from idle passed pilot, warm-up and all three samples | Yes | `acquisition/logs/AB-08Q/` |
| AB-I04 | AB-KV8-F16-4K/AB-15M | SAFETY-GATE | Initial preventive gate blocked high-risk 8B F16/full-Vulkan rows | 16 GB shared-memory ceiling and prior matched-class low-memory evidence | Retried serially from idle with 256 MiB emergency floor; pilot, warm-up and three samples passed for both without emergency stop | Yes | `safety-bypass/` |
| AB-I05 | Quality P5 turbo3 | QUALITY-TIMEOUT-2400 | Turbo3 produced no P5 response within 2400 seconds | 8706-token frozen fixture is extremely slow on CPU Turbo3 | Empty output retained and scored 0; no truncation or substitution | No | `quality/configuration-B/P5.json` |
| AB-I06 | AB-08F through AB-15M P5 | QUALITY-RAM-GATE | Several 8B CPU/full-Vulkan 16K P5 servers crossed the 1.5 GiB free-RAM reserve | 8,706-token fixture requires a 16K server and large KV allocation on a 16 GB shared-memory laptop | Short prompts measured at 4K; isolated P5 attempt retained as safety-blocked and scored 0 | No - hardware limit | `2026-07-17/quality-all-rows/` |
| AB-I07 | Quality harness | SLEEP-DEADLINE | A Windows sleep/resume interval bypassed one long blocking wait | A single Windows wait did not advance as UTC wall time advanced during system sleep | Replaced with absolute UTC polling; duplicate-controller lock and per-prompt checkpoints added | Yes | `scripts/testing/run_atomicbot_full_quality.py` |

# 11. Final repository/runtime decision

| Field | Record |
|---|---|
| TurboQuant implementation class | Substantial partial: CPU reference paths and Vulkan placement work; specialised Vulkan Turbo3 FA shader generation is disabled at the tested source location |
| Source-scope correction | Generic backend presence is not proof of native TQ kernels; SYCL is not claimed and partial Vulkan rows retain CPU KV |
| Granite 3B TurboQuant | Pass for runtime/memory; CPU turbo3 uses 125 MiB versus 320 MiB F16 at 4K and scores 5.95; Vulkan full turbo3 scores 7.22 |
| Granite 8B TurboQuant | Pass for CPU, partial Vulkan and full Vulkan rows; guarded extremes passed only under isolated serial execution with an emergency memory floor |
| Best CPU cache | Workload-dependent: 3B turbo4 scores 6.84, Q8_0 6.72 and turbo3 5.95; 8B Q8_0/turbo3 are close at 6.69/6.67 but both have safety-blocked P5 |
| Best GPU or hybrid cache | Q8_0 Vulkan partial 8B has the highest all-row screen at 8.36; turbo3 Vulkan partial 8B scores 7.51; device placement limitations remain |
| Measured memory benefit | 8B turbo3: 125 versus 640 MiB KV (5.12x smaller, 80.47% reduction); 3B turbo3: 125 versus 320 MiB (60.94%); 1B turbo3: 5.08 versus 26 MiB (80.46%) |
| Silent fallback detected? | No unexplained fallback; partial runs intentionally used 1/41 GPU layers and CPU KV, while AB-13 placed 41/41 layers and KV on Vulkan0 |
| Integration difficulty | Moderate/high on Windows: UI packaging repair, Vulkan toolchain ordering, Device Guard exception, and explicit memory gating required |
| Final status | Accepted with limitations |
| Application role | Experimental memory-saving option behind configuration/quality guardrails; not the unconditional default |
| Main evidence path | `experiments/raw-results/atomicbot-turboquant/2026-07-16/` |
| Quality verification status | Complete all-row P1-P6 coverage: 114 terminal records, 114 matching hashes and 19 row means; P5 uses 16K, with explicit 0 scores for controlled timeouts/safety blocks |
| Final reasoning | Runtime activation and material KV savings are proven, but quality is response- and backend-dependent rather than precision-ordered. AB-14 leads at 8.36; AB-07 fails severely at 0.70. CPU long-context timeouts, 8B 16K safety blocks, hybrid placement, high full-8B working set and one policy-blocked repository test prevent an unrestricted recommendation. |

# 12. Interpretation controls

1. Runtime activation must be proved; accepting a cache flag is insufficient.
2. A blocked 8B GPU run is not proof of backend incompatibility.
3. Do not compare unmatched context lengths or cache precisions as if matched.
4. Process working-set reductions are not identical to directly logged KV-allocation reductions.
5. Six project-specific prompts are a regression screen, not a general quality benchmark.
6. Historical converted scores are not new independent regrades.
7. Objective parsers and exact-match checks take priority over model-judge opinion.
