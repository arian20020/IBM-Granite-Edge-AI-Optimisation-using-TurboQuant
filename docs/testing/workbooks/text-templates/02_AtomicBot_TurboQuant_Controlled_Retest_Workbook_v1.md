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
| Branch/tag |  |
| Pinned commit |  |
| Upstream llama.cpp base commit |  |
| Exposed TurboQuant formats |  |
| CPU backend |  |
| Vulkan support |  |
| SYCL support |  |
| Binary version |  |
| Test operator |  |
| Test start date |  |
| Test end date |  |
| Overall status |  |

## 2. Target laptop

| Field | Fixed/current value | Confirm or update |
|---|---|---|
| Machine ID | Lenovo-PF4HMD0T |  |
| Processor | 12th Gen Intel Core i5-12450H |  |
| RAM | 16.0 GB installed; 15.7 GB usable |  |
| Graphics | Intel UHD Graphics; shared system memory |  |
| Operating system | 64-bit Windows, x64-based processor |  |
| NPU | Not available - excluded from current testing |  |

# 3. Build and setup checklist

| ID | Check | Result | Evidence / command / notes |
|---|---|---|---|
| AB-B01 | Clone exact commit |  |  |
| AB-B02 | Configure clean Windows CPU build |  |  |
| AB-B03 | Build required tools |  |  |
| AB-B04 | Run repository-provided tests |  |  |
| AB-B05 | Configure and verify Vulkan build |  |  |
| AB-B06 | Configure SYCL if supported |  |  |
| AB-B07 | Confirm cache flags |  |  |
| AB-B08 | Record Intel limitations |  |  |

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
| AB-01 | Gemma 3 1B diagnostic | F16 | CPU | 1K | Fork baseline - TurboQuant off |  |
| AB-02 | Gemma 3 1B diagnostic | turbo3 | CPU | 1K | Prove TurboQuant switch activates |  |
| AB-03 | Granite 4.1 3B | F16 | CPU | 2K | High-quality 3B fork baseline |  |
| AB-KV3-F16-4K | Granite 4.1 3B | F16 | CPU | 4K | Supplemental matched 3B F16 allocation baseline |  |
| AB-04 | Granite 4.1 3B | Q8_0 | CPU | 4K | Conventional compressed 3B baseline |  |
| AB-05 | Granite 4.1 3B | turbo4 | CPU | 4K | Conservative 3B TurboQuant |  |
| AB-06 | Granite 4.1 3B | turbo3 | CPU | 4K | Main 3B TurboQuant candidate |  |
| AB-07 | Granite 4.1 3B | turbo2 | CPU | 4K | Aggressive 3B TurboQuant |  |
| AB-08F | Granite 4.1 8B | F16 | CPU | 2K | 8B high-quality fork baseline |  |
| AB-KV8-F16-4K | Granite 4.1 8B | F16 | CPU | 4K | Supplemental matched 8B F16 allocation baseline; guarded by memory gate |  |
| AB-08Q | Granite 4.1 8B | Q8_0 | CPU | 4K | 8B matched conventional baseline |  |
| AB-09 | Granite 4.1 8B | turbo4 | CPU | 4K | 8B conservative TurboQuant |  |
| AB-10 | Granite 4.1 8B | turbo3 | CPU | 4K | 8B main TurboQuant |  |
| AB-11 | Granite 4.1 3B | F16 | Vulkan partial (ngl=1) | 4K | 3B partial Vulkan baseline |  |
| AB-12 | Granite 4.1 3B | turbo3 | Vulkan partial (ngl=1) | 4K | 3B partial Vulkan TurboQuant and fallback check |  |
| AB-13 | Granite 4.1 3B | turbo3 | Vulkan full/max (ngl=999) | 4K | 3B maximum/full Vulkan TurboQuant |  |
| AB-14 | Granite 4.1 8B | Q8_0 | Vulkan partial (ngl=1) | 4K | 8B safe partial Vulkan baseline |  |
| AB-15 | Granite 4.1 8B | turbo3 | Vulkan partial (ngl=1) | 4K | 8B safe partial Vulkan TurboQuant |  |
| AB-15M | Granite 4.1 8B | turbo3 | Vulkan full/max (ngl=999) | 4K | Conditional 8B maximum Vulkan TurboQuant |  |

AB-KV8-F16-4K and AB-15M are research-only because of the 16 GB shared-memory ceiling. A test not run because of a safety gate is `Blocked` or `Not run`, not evidence that the backend is unsupported.

# 5. AtomicBot configuration ladder

| Order | Model weights | KV cache | Device | Purpose | Supported? | Selected for formal test? |
|---|---|---|---|---|---|---|
| 1 | Frozen Q4_K_M GGUF weights | F16 | CPU | Fork baseline; TurboQuant off |  |  |
| 2 | Same weights | Q8_0 | CPU | Conventional compressed baseline |  |  |
| 3 | Same weights | turbo4 | CPU | Conservative TurboQuant |  |  |
| 4 | Same weights | turbo3 | CPU | Main memory/quality candidate |  |  |
| 5 | Same weights | turbo2 | CPU | Aggressive compression |  |  |
| 6 | Same weights | F16 | Vulkan | GPU baseline |  |  |
| 7 | Same weights | turbo3 | Vulkan | Hybrid and GPU-native verification |  |  |
| 8 | Same weights | Q8_0/turbo3 | Vulkan 8B | Separate memory-safe campaign |  |  |

# 6. Device execution and fallback verification

Generation alone is not proof of GPU execution. The gate requires selected device, exact layer placement, model/compute buffer evidence, correct cache location and no unexplained fallback.

| Test ID | Requested device | Actual device | Backend | Offload evidence | TQ/KV device | CPU fallback? | CPU % | GPU % | Evidence path |
|---|---|---|---|---|---|---|---|---|---|
| AB-11 | Vulkan partial |  |  |  |  |  |  |  |  |
| AB-12 | Vulkan partial |  |  |  |  |  |  |  |  |
| AB-13 | Vulkan maximum |  |  |  |  |  |  |  |  |
| AB-14 | Vulkan partial 8B |  |  |  |  |  |  |  |  |
| AB-15 | Vulkan partial 8B |  |  |  |  |  |  |  |  |
| AB-15M | Vulkan maximum 8B |  |  |  |  |  |  |  |  |

# 7. Formal run results

Numeric rows report median TTFT and generation throughput across measured runs, maximum peak working set and observed KV allocation. Quality is scored separately with the controlled 0-10 rubric.

| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak WS MiB | KV MiB | TTFT ms | Tok/s | Quality /10 | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AB-01 | Gemma 3 1B | Q4_K_M | f16 | f16 | CPU | 1024 |  |  |  |  |  |  |
| AB-02 | Gemma 3 1B | Q4_K_M | turbo3 | turbo3 | CPU | 1024 |  |  |  |  |  |  |
| AB-03 | 3B | Q4_K_M | f16 | f16 | CPU | 2048 |  |  |  |  |  |  |
| AB-KV3-F16-4K | 3B | Q4_K_M | f16 | f16 | CPU | 4096 |  |  |  |  |  |  |
| AB-04 | 3B | Q4_K_M | q8_0 | q8_0 | CPU | 4096 |  |  |  |  |  |  |
| AB-05 | 3B | Q4_K_M | turbo4 | turbo4 | CPU | 4096 |  |  |  |  |  |  |
| AB-06 | 3B | Q4_K_M | turbo3 | turbo3 | CPU | 4096 |  |  |  |  |  |  |
| AB-07 | 3B | Q4_K_M | turbo2 | turbo2 | CPU | 4096 |  |  |  |  |  |  |
| AB-08F | 8B | Q4_K_M | f16 | f16 | CPU | 2048 |  |  |  |  |  |  |
| AB-KV8-F16-4K | 8B | Q4_K_M | f16 | f16 | CPU | 4096 |  |  |  |  |  |  |
| AB-08Q | 8B | Q4_K_M | q8_0 | q8_0 | CPU | 4096 |  |  |  |  |  |  |
| AB-09 | 8B | Q4_K_M | turbo4 | turbo4 | CPU | 4096 |  |  |  |  |  |  |
| AB-10 | 8B | Q4_K_M | turbo3 | turbo3 | CPU | 4096 |  |  |  |  |  |  |
| AB-11 | 3B | Q4_K_M | f16 | f16 | Vulkan partial | 4096 |  |  |  |  |  |  |
| AB-12 | 3B | Q4_K_M | turbo3 | turbo3 | Vulkan partial | 4096 |  |  |  |  |  |  |
| AB-13 | 3B | Q4_K_M | turbo3 | turbo3 | Vulkan full | 4096 |  |  |  |  |  |  |
| AB-14 | 8B | Q4_K_M | q8_0 | q8_0 | Vulkan partial | 4096 |  |  |  |  |  |  |
| AB-15 | 8B | Q4_K_M | turbo3 | turbo3 | Vulkan partial | 4096 |  |  |  |  |  |  |
| AB-15M | 8B | Q4_K_M | turbo3 | turbo3 | Vulkan full | 4096 |  |  |  |  |  |  |

# 8. Compression and bounded perplexity supplements

| Configuration | Reference KV MiB | Test KV MiB | Smaller by | Reduction | Interpretation |
|---|---|---|---|---|---|
| Diagnostic 1B turbo3 |  |  |  |  |  |
| Granite 3B Q8_0 |  |  |  |  |  |
| Granite 3B turbo4 |  |  |  |  |  |
| Granite 3B turbo3 |  |  |  |  |  |
| Granite 3B turbo2 |  |  |  |  |  |
| Granite 8B turbo4 |  |  |  |  |  |
| Granite 8B turbo3 |  |  |  |  |  |

| KV cache | PPL | Exit code | Bounded interpretation |
|---|---|---|---|
| f16 |  |  |  |
| q8_0 |  |  |  |
| turbo4 |  |  |  |
| turbo3 |  |  |  |
| turbo2 |  |  |  |

Perplexity must use a named, versioned dataset. A synthetic repeated sentence is only a narrow implementation-regression check and cannot support general quality claims.

# 9. Model quality verifier and context summary

| Prompt ID | Task | Baseline /10 | Optimised /10 | Format valid? | Required facts retained? | Repetition/corruption? | Notes |
|---|---|---|---|---|---|---|---|
| P1 | Sanity explanation |  |  |  |  |  |  |
| P2 | Instruction following |  |  |  |  |  |  |
| P3 | Exact JSON structure |  |  |  |  |  |  |
| P4 | Summarisation |  |  |  |  |  |  |
| P5 | Long-context retrieval |  |  |  |  |  |  |
| P6 | Multi-turn stability |  |  |  |  |  |  |
| Total | P1-P6 result |  |  |  |  |  |  |

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
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |

# 11. Final repository/runtime decision

| Field | Record |
|---|---|
| TurboQuant implementation class |  |
| Source-scope correction |  |
| Granite 3B TurboQuant |  |
| Granite 8B TurboQuant |  |
| Best CPU cache |  |
| Best GPU or hybrid cache |  |
| Measured memory benefit |  |
| Silent fallback detected? |  |
| Integration difficulty |  |
| Final status |  |
| Application role |  |
| Main evidence path |  |
| Quality verification status |  |
| Final reasoning |  |

# 12. Interpretation controls

1. Runtime activation must be proved; accepting a cache flag is insufficient.
2. A blocked 8B GPU run is not proof of backend incompatibility.
3. Do not compare unmatched context lengths or cache precisions as if matched.
4. Process working-set reductions are not identical to directly logged KV-allocation reductions.
5. Six project-specific prompts are a regression screen, not a general quality benchmark.
6. Historical converted scores are not new independent regrades.
7. Objective parsers and exact-match checks take priority over model-judge opinion.
