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
| Test operator | Student with Codex-controlled harness |
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

| Test ID | Requested device | Actual device | Backend | Offload evidence | TQ/KV device | CPU fallback? | CPU % | GPU % | Evidence path |
|---|---|---|---|---|---|---|---|---|---|
| AB-11 | Vulkan partial | Intel UHD Vulkan0 + CPU | Vulkan hybrid | 1/41 layers; 200.99 MiB model and 86.77 MiB compute buffers on Vulkan0 | CPU, 320.00 MiB | Yes, intended hybrid | 48.186 median | N/A - counter unavailable | `acquisition/results/AB-11/` |
| AB-12 | Vulkan partial | Intel UHD Vulkan0 + CPU | Vulkan hybrid | 1/41 layers; 200.99 MiB model and 82.77 MiB compute buffers on Vulkan0 | CPU, 125.13 MiB | Yes, intended hybrid | 58.765 median | N/A - counter unavailable | `acquisition/results/AB-12/` |
| AB-13 | Vulkan maximum | Intel UHD Vulkan0 | Vulkan native placement | 41/41 layers; 1998.84 MiB model and 62.01 MiB compute buffers on Vulkan0 | Vulkan0, 125.13 MiB | No unexplained fallback | 2.651 median | N/A - counter unavailable | `acquisition/results/AB-13/` |
| AB-14 | Vulkan partial 8B | Intel UHD Vulkan0 + CPU | Vulkan hybrid | 1/41 layers; 321.58 MiB model and 111.38 MiB compute buffers on Vulkan0 | CPU, 340.00 MiB | Yes, intended hybrid | 50.446 median | N/A - counter unavailable | `acquisition/results/AB-14/` |
| AB-15 | Vulkan partial 8B | Intel UHD Vulkan0 + CPU | Vulkan hybrid | 1/41 layers; 321.58 MiB model and 120.13 MiB compute buffers on Vulkan0 | CPU, 125.13 MiB | Yes, intended hybrid | 49.173 median | N/A - counter unavailable | `acquisition/results/AB-15/` |
| AB-15M | Vulkan maximum 8B | Intel UHD Vulkan0 | Vulkan native placement | 41/41 layers; 4876.27 MiB model and 95.01 MiB compute buffers on Vulkan0 | Vulkan0, 125.13 MiB | No unexplained fallback | N/A - not sampled in bypass collector | N/A - counter unavailable | `safety-bypass/AB-15M/` |

# 7. Formal run results

Numeric rows report median TTFT and generation throughput across measured runs, maximum peak working set and observed KV allocation. Quality is scored separately with the controlled 0-10 rubric.

| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak WS MiB | KV MiB | TTFT ms | Tok/s | Quality /10 | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AB-01 | Gemma 3 1B | Q4_K_M | f16 | f16 | CPU | 1024 | 923.949 | 26.00 | 110.562 | 37.50 | N/A - diagnostic not quality-screened | Pass |
| AB-02 | Gemma 3 1B | Q4_K_M | turbo3 | turbo3 | CPU | 1024 | 903.688 | 5.08 | 81.053 | 30.80 | N/A - diagnostic not quality-screened | Pass |
| AB-03 | 3B | Q4_K_M | f16 | f16 | CPU | 2048 | 3657.688 | 160.00 | 118.903 | 15.90 | N/A - unmatched quality config | Pass |
| AB-KV3-F16-4K | 3B | Q4_K_M | f16 | f16 | CPU | 4096 | 3818.957 | 320.00 | 144.610 | 15.10 | N/A - quality baseline is Q8_0 | Pass |
| AB-04 | 3B | Q4_K_M | q8_0 | q8_0 | CPU | 4096 | 3668.938 | 170.00 | 134.316 | 15.90 | 6.67 | Pass |
| AB-05 | 3B | Q4_K_M | turbo4 | turbo4 | CPU | 4096 | 3669.125 | 170.00 | 166.100 | 11.80 | N/A - not quality-screened | Pass |
| AB-06 | 3B | Q4_K_M | turbo3 | turbo3 | CPU | 4096 | 3624.750 | 125.00 | 137.405 | 11.80 | 6.64 | Pass; quality limitation |
| AB-07 | 3B | Q4_K_M | turbo2 | turbo2 | CPU | 4096 | 3588.754 | 89.25 | 150.086 | 13.10 | N/A - not quality-screened | Pass |
| AB-08F | 8B | Q4_K_M | f16 | f16 | CPU | 2048 | 8914.633 | 320.00 | 459.308 | 6.80 | N/A - not quality-screened | Pass |
| AB-KV8-F16-4K | 8B | Q4_K_M | f16 | f16 | CPU | 4096 | 9235.957 | 640.00 | 334.844 | 8.37 | N/A - not quality-screened | Pass - controlled bypass |
| AB-08Q | 8B | Q4_K_M | q8_0 | q8_0 | CPU | 4096 | 8934.840 | 340.00 | 540.412 | 6.80 | N/A - not quality-screened | Pass after isolated rerun |
| AB-09 | 8B | Q4_K_M | turbo4 | turbo4 | CPU | 4096 | 8767.055 | 170.00 | 332.794 | 5.90 | N/A - not quality-screened | Pass |
| AB-10 | 8B | Q4_K_M | turbo3 | turbo3 | CPU | 4096 | 8720.383 | 125.00 | 339.918 | 5.90 | N/A - not quality-screened | Pass |
| AB-11 | 3B | Q4_K_M | f16 | f16 | Vulkan partial | 4096 | 2903.941 | 320.00 | 203.267 | 14.00 | N/A - not quality-screened | Pass |
| AB-12 | 3B | Q4_K_M | turbo3 | turbo3 | Vulkan partial | 4096 | 2705.563 | 125.00 | 224.509 | 9.60 | N/A - device differs from quality route | Pass |
| AB-13 | 3B | Q4_K_M | turbo3 | turbo3 | Vulkan full | 4096 | 4541.016 | 125.00 | 410.507 | 7.60 | N/A - device differs from quality route | Pass |
| AB-14 | 8B | Q4_K_M | q8_0 | q8_0 | Vulkan partial | 4096 | 6073.473 | 340.00 | 639.484 | 6.00 | N/A - not quality-screened | Pass |
| AB-15 | 8B | Q4_K_M | turbo3 | turbo3 | Vulkan partial | 4096 | 5868.852 | 125.00 | 504.813 | 5.40 | N/A - not quality-screened | Pass |
| AB-15M | 8B | Q4_K_M | turbo3 | turbo3 | Vulkan full | 4096 | 10455.605 | 125.00 | 637.928 | 4.19 | N/A - not quality-screened | Pass - controlled bypass |

# 8. Compression and bounded perplexity supplements

| Configuration | Reference KV MiB | Test KV MiB | Smaller by | Reduction | Interpretation |
|---|---|---|---|---|---|
| Diagnostic 1B turbo3 | 26.00 | 5.08 | 5.118x | 80.46% | Direct 1K matched allocation reduction versus F16 |
| Granite 3B Q8_0 | 320.00 | 170.00 | 1.882x | 46.88% | Direct matched 4K conventional baseline |
| Granite 3B turbo4 | 320.00 | 170.00 | 1.882x | 46.88% | Direct matched 4K allocation reduction; no advantage over Q8_0 here |
| Granite 3B turbo3 | 320.00 | 125.00 | 2.560x | 60.94% | Direct matched 4K allocation reduction; controlled quality score lower than Q8_0 |
| Granite 3B turbo2 | 320.00 | 89.25 | 3.585x | 72.11% | Largest measured reduction; quality not evaluated in P1-P6 screen |
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

| Prompt ID | Task | Baseline /10 | Optimised /10 | Format valid? | Required facts retained? | Repetition/corruption? | Notes |
|---|---|---|---|---|---|---|---|
| P1 | Sanity explanation | 4.00 | 9.00 | Both: Yes | Baseline: No; optimised: Yes | No | Baseline supplies three benefits and only one limitation, so it is critically capped; optimised meets five bullets, 82 words, required terms and all semantic slots |
| P2 | Instruction following | 8.00 | 8.10 | Both: Yes | Both: Yes | No | Both have exactly three non-empty labelled lines; content is usable but verification wording is not perfect |
| P3 | Exact JSON structure | 4.00 | 8.75 | Both: Yes | Baseline: No; optimised: Yes | No | Baseline's `memory_effect: Increased` materially contradicts the optimisation, imposing a 4/10 cap |
| P4 | Summarisation | 10.00 | 4.00 | Baseline: Yes; optimised: No | Baseline: Yes; optimised: No | No | Optimised emits one sentence, omits `upstream`, and collapses explicit K/V wording; critical cap applied |
| P5 | Long-context retrieval | 4.00 | 0.00 | Baseline: No; optimised: No | Baseline: Yes; optimised: No | Optimised timed out | Baseline retrieved the right marker but inserted a prohibited space; Turbo3 returned no response within 2400 s at the required 8706-token prompt |
| P6 | Multi-turn stability | 10.00 | 10.00 | Both: Yes | Both: Yes | No | Both returned `SAVED` then exact `amber:4821` |
| Total | P1-P6 result | 6.67 | 6.64 | Mixed; see rows | Mixed; see rows | Turbo3 P5 timeout | Q8_0 ranks marginally higher; scores are harsh critical-cap results, not precision-based assumptions |

Baseline = Granite 3B Q4_K_M with Q8_0 KV. Optimised = the same model with turbo3 KV. Preserve every raw output and deterministic validation result.

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

# 11. Final repository/runtime decision

| Field | Record |
|---|---|
| TurboQuant implementation class | Substantial partial: CPU reference paths and Vulkan placement work; specialised Vulkan Turbo3 FA shader generation is disabled at the tested source location |
| Source-scope correction | Generic backend presence is not proof of native TQ kernels; SYCL is not claimed and partial Vulkan rows retain CPU KV |
| Granite 3B TurboQuant | Pass for runtime/memory; turbo3 uses 125 MiB versus 320 MiB F16 at 4K, but Q8_0 narrowly wins the controlled quality screen 6.67 to 6.64 |
| Granite 8B TurboQuant | Pass for CPU, partial Vulkan and full Vulkan rows; guarded extremes passed only under isolated serial execution with an emergency memory floor |
| Best CPU cache | Q8_0 for conservative quality/throughput; turbo3 only when its 60.94% KV reduction is worth the measured quality and long-context latency risk |
| Best GPU or hybrid cache | turbo3 full Vulkan is technically validated for 3B and guarded 8B; partial turbo3 keeps KV on CPU and is hybrid, not native KV execution |
| Measured memory benefit | 8B turbo3: 125 versus 640 MiB KV (5.12x smaller, 80.47% reduction); 3B turbo3: 125 versus 320 MiB (60.94%); 1B turbo3: 5.08 versus 26 MiB (80.46%) |
| Silent fallback detected? | No unexplained fallback; partial runs intentionally used 1/41 GPU layers and CPU KV, while AB-13 placed 41/41 layers and KV on Vulkan0 |
| Integration difficulty | Moderate/high on Windows: UI packaging repair, Vulkan toolchain ordering, Device Guard exception, and explicit memory gating required |
| Final status | Accepted with limitations |
| Application role | Experimental memory-saving option behind configuration/quality guardrails; not the unconditional default |
| Main evidence path | `experiments/raw-results/atomicbot-turboquant/2026-07-16/` |
| Quality verification status | Complete for controlled Q8_0 vs turbo3 P1-P6 screen; Q8_0 6.67, turbo3 6.64; P5 uses 16K context to fit the frozen 8706-token fixture |
| Final reasoning | Runtime activation, material KV savings and both guarded 8B extremes are proven under controlled bypass. Lower Turbo3 quality, a 2400-second P5 timeout, hybrid placement on partial Vulkan, high 10.46 GiB full-8B working set, and one policy-blocked repository test still prevent an unrestricted recommendation. |

# 12. Interpretation controls

1. Runtime activation must be proved; accepting a cache flag is insufficient.
2. A blocked 8B GPU run is not proof of backend incompatibility.
3. Do not compare unmatched context lengths or cache precisions as if matched.
4. Process working-set reductions are not identical to directly logged KV-allocation reductions.
5. Six project-specific prompts are a regression screen, not a general quality benchmark.
6. Historical converted scores are not new independent regrades.
7. Objective parsers and exact-match checks take priority over model-judge opinion.
