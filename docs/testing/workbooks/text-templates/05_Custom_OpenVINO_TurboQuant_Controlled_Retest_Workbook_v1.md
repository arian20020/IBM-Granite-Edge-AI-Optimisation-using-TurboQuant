# 05 Custom OpenVINO TurboQuant Controlled Retest Workbook v1

**Source file:** `05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.docx`  
**SHA-256:** `3565d4afc434870a0c75459c99be61cc0ddb391a05379f1cdf5368529a092821`

## Custom OpenVINO CPU SDPA TurboQuant Controlled Retest Workbook

Controlled retest template v1. Source: 05_Custom_OpenVINO_TurboQuant_Editable_Test_Workbook.docx. Source SHA-256: 8bab0b7573e7eb26db8721f284518367b3df589dd1be49305aa198a5d4d9672e. Historical results were removed from this working copy; the original source document is preserved under docs/testing/source-material/original-workbooks/.

Purpose: test the experimental TBQ4/TBQ3 CPU SDPA route only after the official OpenVINO baseline has succeeded.

Use this workbook while testing. Record exact versions, commands, logs and evidence paths. Freeze formal settings only after the pilot tests succeed.

# 1. Repository and environment record

| Field | Record |
|---|---|
| OpenVINO source URL | https://github.com/openvinotoolkit/openvino |
| Branch/tag |  |
| Pinned source commit |  |
| Known TurboQuant commit tested |  |
| Compatible OpenVINO GenAI version/commit |  |
| Build type | Custom Windows CPU Runtime |
| Attention path | CPU SDPA |
| TurboQuant formats | TBQ4 / TBQ3 - confirm from source |
| GPU TurboQuant | Excluded unless independently proven |
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
| OVT-B01 | Inspect current source for TBQ options |  |  |
| OVT-B02 | Clone exact source commit |  |  |
| OVT-B03 | Configure custom Windows CPU build |  |  |
| OVT-B04 | Build custom OpenVINO Runtime |  |  |
| OVT-B05 | Pair compatible OpenVINO GenAI |  |  |
| OVT-B06 | Run diagnostic model with TQ disabled |  |  |
| OVT-B07 | If current source fails, test known merge commit |  |  |
| OVT-B08 | Pin known-good Runtime and GenAI pair |  |  |

# 4. Runtime and GenAI compatibility record

| Attempt | Runtime commit | GenAI version/commit | Build result | Standard baseline result | TBQ option visible? | Reason kept/rejected |
|---|---|---|---|---|---|---|
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |

# 5. Ordered test matrix

| ID | Model | Weights | K/V cache | Device | Context | Purpose | Status |
|---|---|---|---|---|---|---|---|
| OVT-01 | Diagnostic IR | Standard | Standard | CPU SDPA | 1K | Custom-runtime baseline |  |
| OVT-02 | Granite 3B | Frozen | Standard | CPU SDPA | 2K | 3B custom baseline |  |
| OVT-03 | Granite 3B | Same | TBQ4 | CPU SDPA | 4K | Conservative 3B TQ |  |
| OVT-04 | Granite 3B | Same | TBQ3 | CPU SDPA | 4K | Aggressive 3B TQ |  |
| OVT-05 | Granite 8B | Frozen | Standard | CPU SDPA | 2K | 8B custom baseline |  |
| OVT-06 | Granite 8B | Same | TBQ4 | CPU SDPA | 4K | Conservative 8B TQ |  |
| OVT-07 | Granite 8B | Same | TBQ3 | CPU SDPA | 4K | Aggressive 8B TQ |  |
| OVT-08 | Selected model | Same | U8 / TBQ4 | CPU SDPA | 4K | Optional mixed cache |  |
| OVT-09 | Selected model | Same | TBQ4 / U8 | CPU SDPA | 4K | Optional mixed cache |  |

## 6. Custom OpenVINO configuration ladder

| Order | Model weights | KV cache | Device | Purpose | Supported? | Selected for formal test? |
|---|---|---|---|---|---|---|
| 1 | Frozen official-baseline weights | Standard cache | CPU SDPA | Custom Runtime baseline - TQ off |  |  |
| 2 | Same weights | TBQ4 / TBQ4 | CPU SDPA | Conservative TurboQuant comparison |  |  |
| 3 | Same weights | TBQ3 / TBQ3 | CPU SDPA | Aggressive TurboQuant comparison |  |  |
| 4 | Same weights | Standard / TBQ4 | CPU SDPA | Optional mixed cache after matched modes pass |  |  |
| 5 | Same weights | TBQ4 / Standard | CPU SDPA | Optional mixed cache after matched modes pass |  |  |

# Device execution and fallback verification

The planned route is CPU SDPA. Do not record GPU TurboQuant support unless source inspection and execution evidence independently prove it.

| Test ID | Requested device | Actual device | Backend | Execution/placement proof | TQ device | CPU fallback? | CPU % | GPU % | Evidence path |
|---|---|---|---|---|---|---|---|---|---|
| OVT-01 | CPU |  |  |  |  |  |  |  |  |
| OVT-02 | CPU |  |  |  |  |  |  |  |  |
| OVT-03 | CPU |  |  |  |  |  |  |  |  |
| OVT-04 | CPU |  |  |  |  |  |  |  |  |
| OVT-05 | CPU |  |  |  |  |  |  |  |  |
| OVT-06 | CPU |  |  |  |  |  |  |  |  |
| OVT-07 | CPU |  |  |  |  |  |  |  |  |
| OVT-08 | CPU |  |  |  |  |  |  |  |  |
| OVT-09 | CPU |  |  |  |  |  |  |  |  |

# Formal run results

| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak RAM MB | KV MB | TTFT ms | Tok/s | Quality /10 | Status |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| OVT-01 | Diagnostic IR | Standard | Standard | Standard | CPU SDPA | 1K |  |  |  |  |  |  |
| OVT-02 | Granite 3B | Frozen | Standard | Standard | CPU SDPA | 2K |  |  |  |  |  |  |
| OVT-03 | Granite 3B | Same | TBQ4 | TBQ4 | CPU SDPA | 4K |  |  |  |  |  |  |
| OVT-04 | Granite 3B | Same | TBQ3 | TBQ3 | CPU SDPA | 4K |  |  |  |  |  |  |
| OVT-05 | Granite 8B | Frozen | Standard | Standard | CPU SDPA | 2K |  |  |  |  |  |  |
| OVT-06 | Granite 8B | Same | TBQ4 | TBQ4 | CPU SDPA | 4K |  |  |  |  |  |  |
| OVT-07 | Granite 8B | Same | TBQ3 | TBQ3 | CPU SDPA | 4K |  |  |  |  |  |  |
| OVT-08 | Selected model | Same | U8 | TBQ4 | CPU SDPA | 4K |  |  |  |  |  |  |
| OVT-09 | Selected model | Same | TBQ4 | U8 | CPU SDPA | 4K |  |  |  |  |  |  |

Use one row per formal configuration. Preserve the custom source commit, compatible GenAI pairing, exact properties, runtime logs, activation proof, process-tree memory and model output.

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

Codes: BF build | DEP dependency | WIN Windows | CMD command | MODEL format | ARCH architecture | BASE baseline | TQ-ACT activation | TQ-FALLBACK fallback | TQ-CRASH crash | CPU | GPU | HYBRID | OOM | MEM | PERF | QUAL | REPRO | SCOPE

# Final repository/runtime decision

| Field | Record |
|---|---|
| Known-good Runtime/GenAI pair |  |
| Granite 3B TBQ4 |  |
| Granite 3B TBQ3 |  |
| Granite 8B TBQ4 |  |
| Granite 8B TBQ3 |  |
| TurboQuant implementation class |  |
| Measured memory benefit |  |
| Best cache mode |  |
| Integration difficulty |  |
| Final status |  |
| Application role |  |
| Main evidence path |  |
| Final reasoning |  |
