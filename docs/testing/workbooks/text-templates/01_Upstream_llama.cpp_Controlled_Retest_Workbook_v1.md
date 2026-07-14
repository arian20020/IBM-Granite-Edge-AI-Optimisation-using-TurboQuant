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
| Branch/tag |  |
| Pinned commit |  |
| Build type |  |
| Compiler/toolchain |  |
| Upstream version string |  |
| Test operator |  |
| Test start date |  |
| Test end date |  |
| Overall status |  |

## 2. Target laptop

| Field | Fixed/current value | Confirm or update |
|---|---|---|
| Machine ID | Lenovo-PF4HMD0T |  |
| Processor | Intel Core i5-12450H; 8 cores, 12 logical processors |  |
| RAM | 16 GB nominal installed; approximately 15.7 GB visible/usable |  |
| Graphics | Intel UHD Graphics; shared system memory; driver 32.0.101.7076 |  |
| Operating system | Windows 11 Home, version 10.0.26100, build 26100, 64-bit |  |
| NPU | Not available - excluded from current testing |  |

# 3. Build and setup checklist

| ID | Check | Result | Evidence / command / notes | Evidence Path |
|---|---|---|---|---|
| UL-B01 | Clone exact commit |  |  |  |
| UL-B02 | Configure clean CPU build |  |  |  |
| UL-B03 | Build CLI, server, bench and perplexity tools |  |  |  |
| UL-B04 | Run repository-provided tests |  |  |  |
| UL-B05 | Configure Vulkan build |  |  |  |
| UL-B06 | Configure SYCL build if practical |  |  |  |
| UL-B07 | Record warnings, dependencies and binary capabilities |  |  |  |

# 4. Ordered test matrix

| ID | Model | Weights | K/V cache | Device | Context | Purpose | Status |
|---|---|---|---|---|---|---|---|
| UL-01 | Gemma 3 1B diagnostic | Q4_K_M | F16/F16 | CPU | 1,024 | Prove standard runtime works |  |
| UL-02 | Granite 4.1 3B | BF16 | F16/F16 | CPU | 2,048 | Highest-precision fit test |  |
| UL-03 | Granite 4.1 3B | Q8_0 | F16/F16 | CPU | 4,096 | High-quality quantised reference |  |
| UL-04 | Granite 4.1 3B | Q4_K_M | F16/F16 | CPU | 4,096 | Isolate weight quantisation |  |
| UL-05 | Granite 4.1 3B | Q4_K_M | Q8_0/Q8_0 | CPU | 4,096 | Practical CPU baseline |  |
| UL-06 | Granite 4.1 8B | Q8_0 | F16/F16 | CPU | 2,048 | High-quality fit attempt |  |
| UL-07 | Granite 4.1 8B | Q4_K_M | F16/F16 | CPU | 4,096 | Practical weight baseline |  |
| UL-08 | Granite 4.1 8B | Q4_K_M | Q8_0/Q8_0 | CPU | 4,096 | Practical CPU baseline |  |
| UL-09 | Granite 4.1 3B | Q4_K_M | Q8_0/Q8_0 | Vulkan, 1 layer | 4,096 | Confirm Vulkan/SYCL and minimal offload |  |
| UL-10 | Granite 4.1 3B | Q4_K_M | Q8_0/Q8_0 | Vulkan, all layers | 4,096 | Maximum/full GPU attempt |  |
| UL-11 | Granite 4.1 8B | Q4_K_M | Q8_0/Q8_0 | Vulkan, 1 layer | 4,096 | 8B partial-offload attempt |  |
| UL-12 | Granite 4.1 8B | Q4_K_M | Q8_0/Q8_0 | Vulkan, all layers | 4,096 | 8B maximum/full GPU attempt |  |
| UL-13 | Granite 4.1 3B | Q4_K_M | Q8_0/Q8_0 | SYCL, all layers | 4,096 | Validate the Intel SYCL path and compare full SYCL offload with full Vulkan offload |  |

## 5. Standard configuration ladder

| Order | Model weights | KV cache | Device | Purpose | Supported? | Selected for formal test? |
|---|---|---|---|---|---|---|
| 1 | BF16, highest precision available | F16 | CPU | Highest-precision fit test |  |  |
| 2 | Q8_0 | F16 | CPU | High-quality weight reference |  |  |
| 3 | Q6_K | F16 | CPU | Optional middle-weight reference |  |  |
| 4 | Q4_K_M | F16 | CPU | Practical weight baseline |  |  |
| 5 | Q4_K_M | Q8_0 | CPU | Main conventional compressed baseline |  |  |
| 6 | Q4_K_M | Q4_0, if supported | CPU | Aggressive standard cache |  |  |
| 7 | Frozen configuration | Frozen | Vulkan/SYCL | Partial then maximum GPU offload |  |  |
| 8 | Granite 4.1 8B Q8_0 | F16/F16 | CPU | High-quality 8B fit reference |  |  |
| 9 | Granite 4.1 8B Q4_K_M | F16/F16 | CPU | Practical 8B weight baseline |  |  |
| 10 | Granite 4.1 8B Q4_K_M | Q8_0/Q8_0 | CPU | Practical conventional 8B CPU baseline |  |  |
| 11 | Granite 4.1 3B Q4_K_M | Q8_0/Q8_0 on CPU | Vulkan0 — 1 layer | Minimal 3B Vulkan offload |  |  |
| 12 | Granite 4.1 3B Q4_K_M | Q8_0/Q8_0 on Vulkan0 | Vulkan0 — all layers | Maximum/full 3B Vulkan offload |  |  |
| 13 | Granite 4.1 8B Q4_K_M | Q8_0/Q8_0 on CPU | Vulkan0 — 1 layer | Minimal 8B Vulkan offload |  |  |
| 14 | Granite 4.1 8B Q4_K_M | Q8_0/Q8_0 on Vulkan0 | Vulkan0 — all layers | Maximum/full 8B Vulkan offload |  |  |
| 15 | Q4_K_M | Q8_0/Q8_0 | SYCL, all reported layers | Granite 4.1 3B full-offload SYCL validation |  |  |

# Device execution and fallback verification

CPU is a full baseline route. CPU fallback is recorded only when a GPU-requested run moves unsupported work back to the CPU.

| Test ID | Requested device | Actual device | Backend | Offload level | TQ device | CPU fallback? | CPU % | GPU % | Evidence path |
|---|---|---|---|---|---|---|---|---|---|
| UL-01 | CPU |  |  |  |  |  |  |  |  |
| UL-02 | CPU |  |  |  |  |  |  |  |  |
| UL-03 | CPU |  |  |  |  |  |  |  |  |
| UL-04 | CPU |  |  |  |  |  |  |  |  |
| UL-05 | CPU |  |  |  |  |  |  |  |  |
| UL-06 | CPU |  |  |  |  |  |  |  |  |
| UL-07 | CPU |  |  |  |  |  |  |  |  |
| UL-08 | CPU |  |  |  |  |  |  |  |  |
| UL-09 | Vulkan0 – Intel UHD Graphics |  |  |  |  |  |  |  |  |
| UL-10 | Vulkan0 — Intel UHD Graphics |  |  |  |  |  |  |  |  |
| UL-11 | Vulkan0 — Intel UHD Graphics |  |  |  |  |  |  |  |  |
| UL-12 | Vulkan0 — Intel UHD Graphics |  |  |  |  |  |  |  |  |
| UL-13 | SYCL0 – Intel UHD Graphics |  |  |  |  |  |  |  |  |

# Formal run results

| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak RAM MB | KV MB | TTFT ms | Tok/s | Quality /10 | Status | Practical interpretation |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| UL-01 | Gemma 3 1B diagnostic | Q4_K_M | F16 | F16 | CPU | 1,024 |  |  |  |  |  |  |  |
| UL-02 | Granite 4.1 3B | BF16 | F16 | F16 | CPU | 2,048 |  |  |  |  |  |  |  |
| UL-03 | Granite 4.1 3B | Q8_0 | F16 | F16 | CPU | 4,096 |  |  |  |  |  |  |  |
| UL-04 | Granite 4.1 3B | Q4_K_M | F16 | F16 | CPU | 4,096 |  |  |  |  |  |  |  |
| UL-05 | Granite 4.1 3B | Q4_K_M | Q8_0 | Q8_0 | CPU | 4,096 |  |  |  |  |  |  |  |
| UL-06 | Granite 4.1 8B | Q8_0 | F16 | F16 | CPU | 2,048 |  |  |  |  |  |  |  |
| UL-07 | Granite 4.1 8B | Q4_K_M | F16 | F16 | CPU | 4,096 |  |  |  |  |  |  |  |
| UL-08 | Granite 4.1 8B | Q4_K_M | Q8_0 | Q8_0 | CPU | 4,096 |  |  |  |  |  |  |  |
| UL-09 | Granite 4.1 3B | Q4_K_M | Q8_0 | Q8_0 | Vulkan0, 1 layer | 4,096 |  |  |  |  |  |  |  |
| UL-10 | Granite 4.1 3B | Q4_K_M | Q8_0 | Q8_0 | Vulkan0, all 41 layers | 4,096 |  |  |  |  |  |  |  |
| UL-11 | Granite 4.1 8B | Q4_K_M | Q8_0 | Q8_0 | Vulkan0, 1 of 41 layers | 4,096 |  |  |  |  |  |  |  |
| UL-12 | Granite 4.1 8B | Q4_K_M | Q8_0 | Q8_0 | Vulkan0, all 41 layers | 4,096 |  |  |  |  |  |  |  |
| UL-13 | Granite 4.1 3B | Q4_K_M | Q8_0 | Q8_0 | SYCL0, all 41 layers | 4,096 |  |  |  |  |  |  |  |

Use one row per formal configuration. Preserve the detailed command, raw log and output in the evidence folder.

# Quality and context summary

| Prompt ID | Task | Baseline score /10 | Optimised score /10 | Format valid? | Required facts retained? | Repetition/corruption? | Notes |
|---|---|---|---|---|---|---|---|
| P1 | Sanity explanation |  |  |  |  |  |  |
| P2 | Instruction following |  |  |  |  |  |  |
| P3 | Exact JSON structure |  |  |  |  |  |  |
| P4 | Summarisation |  |  |  |  |  |  |
| P5 | Long-context retrieval |  |  |  |  |  |  |
| P6 | Multi-turn stability |  |  |  |  |  |  |

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
| Granite 3B baseline |  |
| Granite 8B baseline |  |
| Best CPU configuration |  |
| Best Intel GPU configuration |  |
| Maximum stable context |  |
| Recommended fallback configuration |  |
| Final status |  |
| Application role |  |
| Main evidence path |  |
| Final reasoning |  |
