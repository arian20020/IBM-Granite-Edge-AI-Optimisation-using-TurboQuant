# 04 Official OpenVINO Controlled Retest Workbook v1.1

**Controlled filename:** `04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`  
**Generated DOCX hash:** recorded in `Controlled-Workbook-Manifest.csv`  
**Original source:** `04_Official_OpenVINO_Editable_Test_Workbook.docx`  
**Original source SHA-256:** `e892a1a9ea2956e10f3eaa9dba89dcef59a7a6f89cb21b34e274aa3d72baa2b3`

## Official OpenVINO Runtime, GenAI and Merged TurboQuant Controlled Retest Workbook

Controlled retest revision 1.1. Historical results remain legacy evidence and are not copied into active result cells.

**Purpose:** establish the dependable official OpenVINO CPU/GPU baseline and fully evaluate every TurboQuant format exposed by the pinned **official merged CPU SDPA implementation**. The merged route is expected to expose TurboQuant 3-bit and 4-bit through independent key/value algorithm and precision controls. QJL and PolarQuant are tested here as negative capability boundaries unless the pinned official source proves they were later merged.

**Execution rule:** complete source/build gates first, then the short capability sweep, then formal model tests. A property being accepted is not activation proof. Preserve source inspection, runtime logs, actual device, cache allocation, raw output, performance and quality evidence.

# 1. Repository and environment record

| Field | Record |
| --- | --- |
| Runtime source | Official OpenVINO repository |
| Repository URL | https://github.com/openvinotoolkit/openvino |
| Pinned OpenVINO commit |  |
| OpenVINO version |  |
| OpenVINO GenAI version/commit |  |
| TurboQuant merge/PR lineage |  |
| Python version |  |
| Compiler/CMake |  |
| Conversion tool/version |  |
| Device plugins detected | CPU / GPU |
| Model source/revision |  |
| Test operator |  |
| Test start/end date |  |
| Overall status |  |

# 2. Target laptop

| Field | Fixed/current value | Confirm or update |
| --- | --- | --- |
| Machine ID | Lenovo-PF4HMD0T |  |
| Processor | 12th Gen Intel Core i5-12450H |  |
| RAM | 16.0 GB installed; 15.7 GB usable |  |
| Graphics | Intel UHD Graphics; shared system memory |  |
| Operating system | 64-bit Windows, x64-based processor |  |
| NPU | Not available - excluded from current testing |  |

# 3. Official codec capability boundary

| Capability | Expected from verified official source | Must be proved in this campaign |
| --- | --- | --- |
| Standard cache | F32/F16/BF16 plus scalar U8/U4 where supported | Exact precision, quantization mode, group size and allocation |
| Official TurboQuant | TBQ3 and TBQ4 on CPU SDPA | KEY/VALUE cache algorithm, U3/U4 precision, activation path and packed allocation |
| Independent K/V control | Key and value algorithm/precision can differ | All 12 meaningful Turbo/scalar combinations in the capability sweep |
| Norm correction | OV_TURBOQ_NORM_CORRECTION environment switch | OFF/ON ablation for TBQ4 and TBQ3 |
| QJL | Not part of the verified official merged algorithm enum at planning time | Negative source/API/runtime check; move to supported only with new pinned evidence |
| PolarQuant | Not part of the verified official merged algorithm enum at planning time | Negative source/API/runtime check; move to supported only with new pinned evidence |
| GPU TurboQuant | Not assumed | Explicit negative/fallback gate; no GPU claim without execution proof |

# 4. Build and setup checklist

| ID | Check | Result | Evidence / command / notes |
| --- | --- | --- | --- |
| OV-B01 | Install or build pinned official OpenVINO |  |  |
| OV-B02 | Install compatible OpenVINO GenAI |  |  |
| OV-B03 | Confirm CPU plugin |  |  |
| OV-B04 | Confirm Intel GPU plugin and driver |  |  |
| OV-B05 | Run official sample or diagnostic IR model |  |  |
| OV-B06 | Record PerfMetrics availability |  |  |
| OV-B07 | Record versions and environment packages |  |  |
| OV-B08 | Verify the official merged TurboQuant source/API boundary |  |  |
| OV-B09 | Verify official TBQ3/TBQ4 precision and independent K/V controls |  |  |
| OV-B10 | Run official TurboQuant unit/functional diagnostics |  |  |
| OV-B11 | Verify QJL and PolarQuant are not exposed by the official merged route |  |  |
| OV-B12 | Verify official CPU SDPA preconditions and limitations |  |  |

# 5. Model conversion and validation

| ID | Model | Target precision | Conversion command/version | Tokenizer/config valid? | Model loads? | Hash/output path | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| OV-C01 | Granite 3B | FP16 |  |  |  |  |  |
| OV-C02 | Granite 3B | INT8 |  |  |  |  |  |
| OV-C03 | Granite 3B | INT4, if supported |  |  |  |  |  |
| OV-C04 | Granite 8B | FP16, fit attempt |  |  |  |  |  |
| OV-C05 | Granite 8B | INT8 |  |  |  |  |  |
| OV-C06 | Granite 8B | INT4, if supported |  |  |  |  |  |

# 6. Standard official baseline matrix

| ID | Model | Weights | K cache | V cache | Device | Context | Purpose | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OV-01 | Diagnostic IR | Supported | Standard | Standard | CPU | 1K | Environment baseline |  |
| OV-02 | Granite 3B | FP16 | F16/BF16 | F16/BF16 | CPU | 2K | High-quality CPU reference |  |
| OV-03 | Granite 3B | INT8 | F16/BF16 | F16/BF16 | CPU | 4K | Isolate weight compression |  |
| OV-04 | Granite 3B | INT8 | U8 scalar | U8 scalar | CPU | 4K | Practical CPU baseline |  |
| OV-05 | Granite 3B | INT4 | U4 scalar | U4 scalar | CPU | 4K | Aggressive standard route |  |
| OV-06 | Granite 3B | Frozen | Frozen | Frozen | GPU | 4K | Intel GPU baseline |  |
| OV-07 | Granite 8B | FP16, if safe | F16/BF16 | F16/BF16 | CPU | 2K | High-quality fit attempt |  |
| OV-08 | Granite 8B | INT8 | U8 scalar | U8 scalar | CPU | 4K | Practical CPU baseline |  |
| OV-09 | Granite 8B | INT4 | U4/U8 scalar | U4/U8 scalar | CPU | 4K | Aggressive standard route |  |
| OV-10 | Granite 8B | Frozen | Frozen | Frozen | GPU | 4K | Intel GPU attempt |  |

# 7. Official TurboQuant format capability sweep

Run these with a diagnostic IR or Granite 3B, short context and short output. The purpose is exhaustive configuration acceptance **and activation**, not performance ranking. Record unsupported combinations honestly.

| ID | K algorithm | V algorithm | K precision | V precision | CPU SDPA loads? | Activation proof | Fallback? | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OV-TQS-01 | TBQ4 | TBQ4 | U4 | U4 |  |  |  |  |
| OV-TQS-02 | TBQ3 | TBQ3 | U3 | U3 |  |  |  |  |
| OV-TQS-03 | TBQ4 | TBQ3 | U4 | U3 |  |  |  |  |
| OV-TQS-04 | TBQ3 | TBQ4 | U3 | U4 |  |  |  |  |
| OV-TQS-05 | TBQ4 | SCALAR | U4 | U8 |  |  |  |  |
| OV-TQS-06 | SCALAR | TBQ4 | U8 | U4 |  |  |  |  |
| OV-TQS-07 | TBQ3 | SCALAR | U3 | U8 |  |  |  |  |
| OV-TQS-08 | SCALAR | TBQ3 | U8 | U3 |  |  |  |  |
| OV-TQS-09 | TBQ4 | SCALAR | U4 | U4 |  |  |  |  |
| OV-TQS-10 | SCALAR | TBQ4 | U4 | U4 |  |  |  |  |
| OV-TQS-11 | TBQ3 | SCALAR | U3 | U4 |  |  |  |  |
| OV-TQS-12 | SCALAR | TBQ3 | U4 | U3 |  |  |  |  |

# 8. Formal official TurboQuant tests

| ID | Model | K algorithm | V algorithm | K precision | V precision | Context | Purpose | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OV-TQ-01 | Granite 3B | SCALAR | SCALAR | U8 | U8 | 4096 | Standard U8/U8 control baseline |  |
| OV-TQ-02 | Granite 3B | SCALAR | SCALAR | U4 | U4 | 4096 | Standard U4/U4 control baseline |  |
| OV-TQ-03 | Granite 3B | TBQ4 | TBQ4 | U4 | U4 | 4096 | Official symmetric TurboQuant 4-bit |  |
| OV-TQ-04 | Granite 3B | TBQ3 | TBQ3 | U3 | U3 | 4096 | Official symmetric TurboQuant 3-bit |  |
| OV-TQ-05 | Granite 3B | TBQ4 | TBQ3 | U4 | U3 | 4096 | Official asymmetric TBQ4 key / TBQ3 value |  |
| OV-TQ-06 | Granite 3B | TBQ3 | TBQ4 | U3 | U4 | 4096 | Official asymmetric TBQ3 key / TBQ4 value |  |
| OV-TQ-07 | Granite 3B | TBQ4 | SCALAR | U4 | U8 | 4096 | TurboQuant key-only 4-bit |  |
| OV-TQ-08 | Granite 3B | SCALAR | TBQ4 | U8 | U4 | 4096 | TurboQuant value-only 4-bit |  |
| OV-TQ-09 | Granite 3B | TBQ3 | SCALAR | U3 | U8 | 4096 | TurboQuant key-only 3-bit |  |
| OV-TQ-10 | Granite 3B | SCALAR | TBQ3 | U8 | U3 | 4096 | TurboQuant value-only 3-bit |  |
| OV-TQ-11 | Granite 3B | TBQ4 | TBQ4 | U4 | U4 | 4096 | TBQ4 norm-correction OFF/ON ablation |  |
| OV-TQ-12 | Granite 3B | TBQ3 | TBQ3 | U3 | U3 | 4096 | TBQ3 norm-correction OFF/ON ablation |  |
| OV-TQ-13 | Granite 3B | TBQ4 | TBQ4 | U4 | U4 | 512/2048/4096/8192 | TBQ4 context-scaling series |  |
| OV-TQ-14 | Granite 3B | TBQ3 | TBQ3 | U3 | U3 | 512/2048/4096/8192 | TBQ3 context-scaling series |  |
| OV-TQ-15 | Granite 3B | Selected best | Selected best | Pinned | Pinned | 4096 | Repeatability and stability: pilot, warm-up, three measured runs |  |
| OV-TQ-16 | Granite 8B | TBQ4 | TBQ4 | U4 | U4 | 4096 if safe | 8B TBQ4 feasibility and memory-safety gate |  |
| OV-TQ-17 | Granite 8B | TBQ3 | TBQ3 | U3 | U3 | 4096 if safe | 8B TBQ3 feasibility and memory-safety gate |  |
| OV-TQ-18 | Selected model | TBQ4 | TBQ4 | U4 | U4 | 1024 | GPU request negative/fallback gate; no GPU claim without proof |  |
| OV-TQ-19 | Diagnostic IR | QJL probe | QJL probe | N/A | N/A | 256 | Negative capability test: official merged route must not be labelled QJL-capable unless exposed and executed |  |
| OV-TQ-20 | Diagnostic IR | Polar probe | Polar probe | N/A | N/A | 256 | Negative capability test: official merged route must not be labelled PolarQuant-capable unless exposed and executed |  |

# 9. Configuration and activation record

| Test ID | Run/config ID | Exact properties/env vars | Requested codec | Verified codec | Expected/actual record bytes | Activation evidence | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |

# 10. Device execution and fallback verification

| Test ID | Requested device | Actual device | Backend | Model placement | KV placement | Optimisation device | Silent fallback check | CPU/GPU utilisation evidence | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |

# 11. Formal performance and memory results

Use one pilot, one excluded warm-up and at least three measured repetitions per frozen configuration unless a documented safety gate blocks the run.

| Test ID | Configuration ID | Rep role/no. | Load ms | TTFT ms | Prompt tok/s | TPOT ms | Decode tok/s | Peak private MB | KV MB | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |

# 12. Quality evaluation by format

Complete P1-P6 for the U8/U4 controls and every primary official TurboQuant format. Preserve raw responses and apply the fixed weighted 0-10 rubric.

| Test/config | P1 | P2 | P3 | P4 | P5 | P6 | Mean /10 | Critical failure/cap | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OV-TQ-01 U8 control |  |  |  |  |  |  |  |  |  |
| OV-TQ-02 U4 control |  |  |  |  |  |  |  |  |  |
| OV-TQ-03 TBQ4/TBQ4 |  |  |  |  |  |  |  |  |  |
| OV-TQ-04 TBQ3/TBQ3 |  |  |  |  |  |  |  |  |  |
| OV-TQ-05 TBQ4/TBQ3 |  |  |  |  |  |  |  |  |  |
| OV-TQ-06 TBQ3/TBQ4 |  |  |  |  |  |  |  |  |  |
| OV-TQ-07 TBQ4/U8 |  |  |  |  |  |  |  |  |  |
| OV-TQ-08 U8/TBQ4 |  |  |  |  |  |  |  |  |  |
| OV-TQ-09 TBQ3/U8 |  |  |  |  |  |  |  |  |  |
| OV-TQ-10 U8/TBQ3 |  |  |  |  |  |  |  |  |  |

[[PAGEBREAK]]

# 13. Context, stability and claim-boundary summary

| Test | Required result | Observed result | Evidence | Decision |
| --- | --- | --- | --- | --- |
| OV-TQ-13 | TBQ4 at 512/2K/4K/8K or bounded stop point |  |  |  |
| OV-TQ-14 | TBQ3 at 512/2K/4K/8K or bounded stop point |  |  |  |
| OV-TQ-15 | Pilot + warm-up + >=3 measured repetitions without unexplained fallback |  |  |  |
| OV-TQ-16/17 | 8B only within memory-safety gate |  |  |  |
| OV-TQ-18 | GPU support or explicit unsupported/fallback evidence |  |  |  |
| OV-TQ-19 | QJL unavailable or newly proved on pinned official source |  |  |  |
| OV-TQ-20 | PolarQuant unavailable or newly proved on pinned official source |  |  |  |

# 14. Failure log

| Failure ID | Test ID | Code | Description | Root cause/status | Fix or next action | Retest run | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |

Codes include: BF, DEP, WIN, MODEL, ARCH, BASE, TQ-ACT, TQ-FALLBACK, TQ-CRASH, QJL, POLAR, CPU, GPU, OOM, MEM, PERF, QUAL, REPRO, SCOPE.

[[PAGEBREAK]]

# 15. Final official-route decision

| Field | Record |
| --- | --- |
| Official TBQ4 support |  |
| Official TBQ3 support |  |
| Independent K/V combinations |  |
| Norm-correction finding |  |
| QJL official status |  |
| PolarQuant official status |  |
| Granite 3B best configuration |  |
| Granite 8B safe configuration |  |
| Maximum stable context |  |
| GPU result |  |
| Recommended official fallback |  |
| Application role |  |
| Main evidence path |  |
| Final bounded reasoning |  |
