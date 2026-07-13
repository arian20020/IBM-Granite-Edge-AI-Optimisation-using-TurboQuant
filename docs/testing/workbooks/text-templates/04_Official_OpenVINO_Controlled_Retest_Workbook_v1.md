# 04 Official OpenVINO Controlled Retest Workbook v1

**Source file:** `04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`  
**SHA-256:** `6db2480859cb9878286a3d3201afd9fe16ac6191a832b2025ace8e5fedc18d19`

## Official OpenVINO Runtime and GenAI Controlled Retest Workbook

Controlled retest template v1. Source: 04_Official_OpenVINO_Editable_Test_Workbook.docx. Source SHA-256: e892a1a9ea2956e10f3eaa9dba89dcef59a7a6f89cb21b34e274aa3d72baa2b3. Historical results were removed from this working copy; the original source document is preserved under docs/testing/source-material/original-workbooks/.

Purpose: establish the dependable OpenVINO IR CPU/GPU route and the standard fallback before any custom TurboQuant Runtime is introduced.

Use this workbook while testing. Record exact versions, commands, logs and evidence paths. Freeze formal settings only after the pilot tests succeed.

# 1. Repository and environment record

| Field | Record |
|---|---|
| Runtime source | Official OpenVINO |
| OpenVINO version |  |
| OpenVINO GenAI version |  |
| Python version |  |
| Conversion tool/version |  |
| Device plugins detected | CPU / GPU |
| Model source/revision |  |
| Model output folders |  |
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
| OV-B01 | Install or build pinned official OpenVINO |  |  |
| OV-B02 | Install compatible OpenVINO GenAI |  |  |
| OV-B03 | Confirm CPU plugin |  |  |
| OV-B04 | Confirm Intel GPU plugin and driver |  |  |
| OV-B05 | Run official sample or diagnostic IR model |  |  |
| OV-B06 | Record PerfMetrics availability |  |  |
| OV-B07 | Record versions and environment packages |  |  |

# 4. Model conversion and validation

| ID | Model | Target precision | Conversion command/version | Tokenizer and generation config valid? | Model loads? | Hash/output path | Status |
|---|---|---|---|---|---|---|---|
| OV-C01 | Granite 3B | FP16 |  |  |  |  |  |
| OV-C02 | Granite 3B | INT8 |  |  |  |  |  |
| OV-C03 | Granite 3B | INT4, if supported |  |  |  |  |  |
| OV-C04 | Granite 8B | FP16, fit attempt |  |  |  |  |  |
| OV-C05 | Granite 8B | INT8 |  |  |  |  |  |
| OV-C06 | Granite 8B | INT4, if supported |  |  |  |  |  |

# 5. Ordered inference matrix

| ID | Model | Weights | KV cache | Device | Context | Purpose | Status |
|---|---|---|---|---|---|---|---|
| OV-01 | Diagnostic IR | Supported | Standard | CPU | 1K | Environment baseline |  |
| OV-02 | Granite 3B | FP16 | F16/BF16 | CPU | 2K | High-quality CPU reference |  |
| OV-03 | Granite 3B | INT8 | F16/BF16 | CPU | 4K | Isolate weight compression |  |
| OV-04 | Granite 3B | INT8 | U8 | CPU | 4K | Practical CPU baseline |  |
| OV-05 | Granite 3B | INT4 | U4, if supported | CPU | 4K | Aggressive standard route |  |
| OV-06 | Granite 3B | Frozen | Frozen | GPU | 4K | Intel GPU baseline |  |
| OV-07 | Granite 8B | FP16, if safe | F16/BF16 | CPU | 2K | High-quality fit attempt |  |
| OV-08 | Granite 8B | INT8 | U8 | CPU | 4K | Practical CPU baseline |  |
| OV-09 | Granite 8B | INT4 | U4/U8 | CPU | 4K | Aggressive standard route |  |
| OV-10 | Granite 8B | Frozen | Frozen | GPU | 4K | Intel GPU attempt |  |

## 6. Official OpenVINO configuration ladder

| Order | Model weights | KV cache | Device | Purpose | Supported? | Selected for formal test? |
|---|---|---|---|---|---|---|
| 1 | FP16 | F16/BF16 | CPU | Highest-quality reference |  |  |
| 2 | INT8 | F16/BF16 | CPU | Isolate weight compression |  |  |
| 3 | INT8 | U8 | CPU | Main practical baseline |  |  |
| 4 | INT4 | U8 | CPU | Aggressive weight compression |  |  |
| 5 | INT4 | U4, if supported | CPU | Aggressive standard combination |  |  |
| 6 | Frozen supported configuration | Frozen | OpenVINO GPU | GPU baseline and fallback check |  |  |

# Device execution and fallback verification

OpenVINO CPU and OpenVINO GPU are separate baseline routes. Record compiled devices, Model0 execution device and any tokenizer/detokenizer auxiliary placement separately. A GPU request is not proven by successful generation alone.

| Test ID | Requested device | Actual device | Backend | Execution/placement proof | CPU fallback? | CPU % | GPU % | Evidence path |
|---|---|---|---|---|---|---|---|---|
| OV-01 | CPU |  |  |  |  |  |  |  |
| OV-02 | CPU |  |  |  |  |  |  |  |
| OV-03 | CPU |  |  |  |  |  |  |  |
| OV-04 | CPU |  |  |  |  |  |  |  |
| OV-05 | CPU |  |  |  |  |  |  |  |
| OV-06 | GPU |  |  |  |  |  |  |  |
| OV-07 | CPU |  |  |  |  |  |  |  |
| OV-08 | CPU |  |  |  |  |  |  |  |
| OV-09 | CPU |  |  |  |  |  |  |  |
| OV-10 | GPU |  |  |  |  |  |  |  |

# Formal run results

| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak RAM MB | KV MB | TTFT ms | Tok/s | Quality /10 | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| OV-01 | Diagnostic IR |  |  |  | CPU | 1K |  |  |  |  |  |  |
| OV-02 | Granite 3B | FP16 | F16/BF16 | F16/BF16 | CPU | 2K |  |  |  |  |  |  |
| OV-03 | Granite 3B | INT8 | F16/BF16 | F16/BF16 | CPU | 4K |  |  |  |  |  |  |
| OV-04 | Granite 3B | INT8 | U8 | U8 | CPU | 4K |  |  |  |  |  |  |
| OV-05 | Granite 3B | INT4 | U4 | U4 | CPU | 4K |  |  |  |  |  |  |
| OV-06 | Granite 3B | Frozen | Frozen | Frozen | GPU | 4K |  |  |  |  |  |  |
| OV-07 | Granite 8B | FP16 | F16/BF16 | F16/BF16 | CPU | 2K |  |  |  |  |  |  |
| OV-08 | Granite 8B | INT8 | U8 | U8 | CPU | 4K |  |  |  |  |  |  |
| OV-09 | Granite 8B | INT4 | U4/U8 | U4/U8 | CPU | 4K |  |  |  |  |  |  |
| OV-10 | Granite 8B | Frozen | Frozen | Frozen | GPU | 4K |  |  |  |  |  |  |

Use one row per formal configuration. Preserve conversion logs, runtime command, process-tree samples, PerfMetrics, raw model output and device proof.

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

Codes: BF build | DEP dependency | WIN Windows | CMD command | MODEL format | ARCH architecture | TOK tokenizer | BASE baseline | CPU | GPU | HYBRID | OOM | MEM | PERF | QUAL | REPRO | SCOPE

# Final repository/runtime decision

| Field | Record |
|---|---|
| Granite 3B CPU |  |
| Granite 3B GPU |  |
| Granite 8B CPU |  |
| Granite 8B GPU |  |
| Best CPU configuration |  |
| Best GPU configuration |  |
| Maximum stable context |  |
| Recommended official fallback |  |
| Final status |  |
| Application role |  |
| Main evidence path |  |
| Final reasoning |  |
