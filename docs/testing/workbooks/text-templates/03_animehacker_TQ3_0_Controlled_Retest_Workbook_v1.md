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
| Branch/tag |  |
| Pinned commit |  |
| Upstream llama.cpp base commit |  |
| TurboQuant cache format |  |
| CPU path |  |
| Vulkan/SYCL path |  |
| CUDA-only dependencies |  |
| QJL residual correction present? |  |
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
| AH-B01 | Clone exact commit |  |  |
| AH-B02 | Configure and build on Windows |  |  |
| AH-B03 | Build CPU route |  |  |
| AH-B04 | Build benchmark tools |  |  |
| AH-B05 | Run repository-provided tests |  |  |
| AH-B06 | Inspect GPU backend availability |  |  |
| AH-B07 | Use WSL only if Windows fails and evidence is useful |  |  |
| AH-B08 | Record block layout, cache flags and known limits |  |  |

# 4. Ordered test matrix

| ID | Model | KV cache | Execution | Context | Purpose | Status |
|---|---|---|---|---|---|---|
| AH-01 | Diagnostic | Standard | CPU | 1K | Repository baseline |  |
| AH-02 | Diagnostic | TQ3_0 | CPU | 1K | Confirm TQ3_0 activation |  |
| AH-03 | Granite 3B | F16/Q8_0 | CPU | 2K | Granite fork baseline |  |
| AH-04 | Granite 3B | Q8_0 | CPU | 4K | Conventional cache reference |  |
| AH-05 | Granite 3B | TQ3_0 | CPU | 4K | Main 3B TQ test |  |
| AH-06 | Granite 8B | F16/Q8_0 | CPU | 2K | 8B fork baseline |  |
| AH-07 | Granite 8B | TQ3_0 | CPU | 4K | Main 8B TQ test |  |
| AH-08 | Granite 3B | Standard | GPU partial | 4K | GPU baseline if backend exists |  |
| AH-09 | Granite 3B | TQ3_0 | GPU feasible | 4K | GPU TQ attempt if supported |  |
| AH-10 | Granite 8B | Standard/TQ3_0 | GPU feasible | 4K | Optional 8B GPU investigation |  |

## 5. animehacker configuration ladder

| Order | Model weights | KV cache | Device | Purpose | Supported? | Selected for formal test? |
|---|---|---|---|---|---|---|
| 1 | Frozen GGUF weights | F16 | CPU | Fork baseline - TQ off |  |  |
| 2 | Same weights | Q8_0 | CPU | Standard compressed-cache reference |  |  |
| 3 | Same weights | TQ3_0 | CPU | Main TQ3_0 comparison |  |  |
| 4 | Same frozen weights | Standard | Supported GPU backend | GPU baseline only if genuine route exists |  |  |
| 5 | Same frozen weights | TQ3_0 | Supported GPU backend | GPU TQ only if implementation supports it |  |  |

# Device execution and fallback verification

Do not treat a CUDA-only or non-Windows path as suitable for application integration. It may still be retained as research-only evidence.

| Test ID | Requested device | Actual device | Backend | Offload level | TQ/KV device | CPU fallback? | CPU % | GPU % | Evidence path |
|---|---|---|---|---|---|---|---|---|---|
| AH-01 | CPU |  |  |  |  |  |  |  |  |
| AH-02 | CPU |  |  |  |  |  |  |  |  |
| AH-03 | CPU |  |  |  |  |  |  |  |  |
| AH-04 | CPU |  |  |  |  |  |  |  |  |
| AH-05 | CPU |  |  |  |  |  |  |  |  |
| AH-06 | CPU |  |  |  |  |  |  |  |  |
| AH-07 | CPU |  |  |  |  |  |  |  |  |
| AH-08 | GPU partial |  |  |  |  |  |  |  |  |
| AH-09 | GPU partial |  |  |  |  |  |  |  |  |
| AH-10 | GPU partial planned |  |  |  |  |  |  |  |  |

# Formal run results

| Test ID | Model | Weights | K cache | V cache | Device | Context | Llama mem MiB* | Context/KV MiB | TTFT ms | Tok/s | Quality /10 | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AH-01 | Diagnostic | Gemma 1B Q4_K_M | f16 | f16 | CPU (-ngl 0) | 1K |  |  |  |  |  |  |
| AH-02 | Diagnostic | Gemma 1B Q4_K_M | tq3_0 | tq3_0 | CPU (-ngl 0) | 1K |  |  |  |  |  |  |
| AH-03 | Granite 3B | BF16 GGUF | f16 | f16 | CPU (-ngl 0) | 2K |  |  |  |  |  |  |
| AH-04 | Granite 3B | BF16 GGUF | q8_0 | q8_0 | CPU (-ngl 0) | 4K |  |  |  |  |  |  |
| AH-05 | Granite 3B | BF16 GGUF | tq3_0 | tq3_0 | CPU (-ngl 0) | 4K |  |  |  |  |  |  |
| AH-06 | Granite 8B | Q8_0 GGUF | f16 | f16 | CPU (-ngl 0) | 2K |  |  |  |  |  |  |
| AH-07 | Granite 8B | Q8_0 GGUF | tq3_0 | tq3_0 | CPU (-ngl 0) | 4K |  |  |  |  |  |  |
| AH-08 | Granite 3B | BF16 GGUF | f16 | f16 | SYCL partial (-ngl 1) | 4K |  |  |  |  |  |  |
| AH-09 | Granite 3B | BF16 GGUF | tq3_0 | tq3_0 | SYCL partial (-ngl 1) | 4K |  |  |  |  |  |  |
| AH-10 | Granite 8B | Q8_0 GGUF | tq3_0 | tq3_0 | SYCL partial planned | 2K planned |  |  |  |  |  |  |

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
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |

Codes: BF build | DEP dependency | WIN Windows | MODEL format | ARCH architecture | BASE baseline | TQ-ACT activation | TQ-FALLBACK fallback | TQ-CRASH crash | CPU | GPU | HYBRID | OOM | MEM | PERF | QUAL | REPRO | SCOPE

# Final repository/runtime decision

| Field | Record |
|---|---|
| Implementation depth |  |
| QJL present? |  |
| Granite 3B TQ3_0 |  |
| Granite 8B TQ3_0 |  |
| CPU support |  |
| Intel GPU support |  |
| Measured memory benefit |  |
| Research value |  |
| Integration difficulty |  |
| Final status |  |
| Application role |  |
| Main evidence path |  |
| Final reasoning |  |

# Evidence package summary and reviewer notes

| Item | Record |
|---|---|
| Main evidence folder |  |
| Archive ZIP |  |
| Pinned commit |  |
| Git head |  |
| SYCL device |  |
| Formal matrix |  |
| Quality review |  |
| Memory note |  |
