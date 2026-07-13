# 05 Custom OpenVINO TurboQuant Controlled Retest Workbook v1.1

**Controlled filename:** `05_Custom_OpenVINO_TurboQuant_Controlled_Retest_Workbook_v1.docx`  
**Generated DOCX hash:** recorded in `Controlled-Workbook-Manifest.csv`  
**Original source:** `05_Custom_OpenVINO_TurboQuant_Editable_Test_Workbook.docx`  
**Original source SHA-256:** `8bab0b7573e7eb26db8721f284518367b3df589dd1be49305aa198a5d4d9672e`

## Custom OpenVINO CPU SDPA Complete KV-Cache Codec Controlled Retest Workbook

Controlled retest revision 1.1. Historical results remain legacy evidence and are not copied into active result cells.

**Purpose:** test every codec family exposed by the pinned experimental OpenVINO CPU SDPA branch: standard scalar caches, TurboQuant 4-bit, TurboQuant 3-bit, TurboQuant+QJL 4-bit family, TurboQuant+QJL 3-bit family, PolarQuant 4-bit and PolarQuant 3-bit. The campaign includes algorithm-level conformance, all 36 ordered K/V codec pairs, full model quality/performance tests, ablations, context scaling and known-path limitations.

**Important boundary:** this route is experimental and may be an open/unmerged pull request. Source comments, design documents and executable behaviour may disagree. No codec is marked supported merely because an enum or flag exists; it must build, activate, allocate the expected representation and complete the planned tests.

# 1. Repository and environment record

| Field | Record |
| --- | --- |
| OpenVINO source URL | https://github.com/openvinotoolkit/openvino |
| Experimental PR/branch |  |
| Pinned head commit |  |
| Base commit |  |
| PR state at execution |  |
| Compatible GenAI version/commit |  |
| Build type | Custom Windows CPU Runtime |
| Attention path | CPU SDPA |
| Codec families expected | TBQ4, TBQ3, TBQ4_QJL, TBQ3_QJL, POLAR4, POLAR3 |
| Independent K/V properties |  |
| Test operator |  |
| Test start/end date |  |
| Overall status |  |

[[PAGEBREAK]]

# 2. Target laptop

| Field | Fixed/current value | Confirm or update |
| --- | --- | --- |
| Machine ID | Lenovo-PF4HMD0T |  |
| Processor | 12th Gen Intel Core i5-12450H |  |
| RAM | 16.0 GB installed; 15.7 GB usable |  |
| Graphics | Intel UHD Graphics; shared system memory |  |
| Operating system | 64-bit Windows, x64-based processor |  |
| NPU | Not available - excluded from current testing |  |

# 3. Experimental codec inventory and expected representation

These values are source-design expectations, not measured results. Confirm actual allocation and implementation on the pinned commit.

| Codec | Nominal family | Expected bytes/head at dim 128 | Main mechanism | QJL/Polar element | Execution status |
| --- | --- | --- | --- | --- | --- |
| Standard F32 | 32-bit raw | 512 | No cache compression | None |  |
| Scalar U8 GS128 | 8-bit + metadata | 136 | Affine min/max scale and zero point | None |  |
| Scalar U4 GS128 | 4-bit + metadata | 72 | Packed nibbles + scale and zero point | None |  |
| TBQ4 | ~4.25 bits | 68 | Orthogonal rotation + Lloyd-Max codebook + norm | No |  |
| TBQ3 | ~3.25 bits | 52 | Orthogonal rotation + Lloyd-Max codebook + norm | No |  |
| TBQ4_QJL | design target 88 bytes | 88 | Lower-bit TBQ base + 1-bit projected residual signs | QJL |  |
| TBQ3_QJL | design target 72 bytes | 72 | Lower-bit TBQ base + 1-bit projected residual signs | QJL |  |
| POLAR4 | approximately 4-bit family | ~68 | Polar tree/angle decomposition + norm | PolarQuant |  |
| POLAR3 | approximately 3-bit family | ~52 | Polar tree/angle decomposition + norm | PolarQuant |  |

# 4. Build and setup checklist

| ID | Check | Result | Evidence / command / notes |
| --- | --- | --- | --- |
| OVT-B01 | Inspect current source for TBQ options |  |  |
| OVT-B02 | Clone exact source commit |  |  |
| OVT-B03 | Configure custom Windows CPU build |  |  |
| OVT-B04 | Build custom OpenVINO Runtime |  |  |
| OVT-B05 | Pair compatible OpenVINO GenAI |  |  |
| OVT-B06 | Run diagnostic model with codecs disabled |  |  |
| OVT-B07 | If current source fails, test documented known commit |  |  |
| OVT-B08 | Pin known-good Runtime and GenAI pair |  |  |
| OVT-B09 | Pin and inspect the experimental OpenVINO codec branch/PR |  |  |
| OVT-B10 | Verify all six experimental codec values and independent K/V properties |  |  |
| OVT-B11 | Build and run codec unit/functional tests |  |  |
| OVT-B12 | Resolve QJL implementation-status contradictions |  |  |
| OVT-B13 | Verify PolarQuant implementation and codebook generation |  |  |
| OVT-B14 | Validate perplexity/quality scripts and reference procedure |  |  |
| OVT-B15 | Record experimental integration and claim boundary |  |  |

# 5. Algorithm-level conformance tests

Run these before model benchmarking. Unit-level failure blocks the affected codec but does not erase evidence for other codecs.

| ID | Conformance test | Required evidence | Result | Failure/limitation |
| --- | --- | --- | --- | --- |
| OVT-A01 | TBQ4 round-trip, packing, norm and record-size conformance | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A02 | TBQ3 round-trip, packing, norm and record-size conformance | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A03 | TBQ4+QJL residual projection, sign correction and record-size conformance | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A04 | TBQ3+QJL residual projection, sign correction and record-size conformance | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A05 | Polar4 tree/angle encode-decode, norm and record-size conformance | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A06 | Polar3 tree/angle encode-decode, norm and record-size conformance | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A07 | Zero, near-zero, large-value, NaN/Inf rejection and numerical-stability cases across every codec | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A08 | Deterministic rotation, projection, codebook and seed reproducibility | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A09 | Independent K/V codec dispatch and rotation-domain correctness | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A10 | Activation proof and dead-code/silent-fallback detection for every codec | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A11 | Expected versus measured packed record bytes and KV-cache allocation formula | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |
| OVT-A12 | Repository codec tests plus bounded perplexity/quality script smoke test | Repository test/log, deterministic fixture, expected/actual bytes and numerical result |  |  |

# 6. Exhaustive 6 x 6 K/V codec capability sweep

Use a diagnostic model, short context and short output. Every ordered K/V pair receives a separate ID. The pass condition is verified dispatch and completion with no unexplained fallback; it is **not** a performance recommendation.

| ID | K codec | V codec | K storage/family | V storage/family | Expected K bytes | Expected V bytes | Activation/fallback result | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OVT-S01 | TBQ4 | TBQ4 | U4 | U4 | 68 | 68 |  |  |
| OVT-S02 | TBQ4 | TBQ3 | U4 | U3 | 68 | 52 |  |  |
| OVT-S03 | TBQ4 | TBQ4_QJL | U4 | codec | 68 | 88 |  |  |
| OVT-S04 | TBQ4 | TBQ3_QJL | U4 | codec | 68 | 72 |  |  |
| OVT-S05 | TBQ4 | POLAR4 | U4 | codec | 68 | ~68 |  |  |
| OVT-S06 | TBQ4 | POLAR3 | U4 | codec | 68 | ~52 |  |  |
| OVT-S07 | TBQ3 | TBQ4 | U3 | U4 | 52 | 68 |  |  |
| OVT-S08 | TBQ3 | TBQ3 | U3 | U3 | 52 | 52 |  |  |
| OVT-S09 | TBQ3 | TBQ4_QJL | U3 | codec | 52 | 88 |  |  |
| OVT-S10 | TBQ3 | TBQ3_QJL | U3 | codec | 52 | 72 |  |  |
| OVT-S11 | TBQ3 | POLAR4 | U3 | codec | 52 | ~68 |  |  |
| OVT-S12 | TBQ3 | POLAR3 | U3 | codec | 52 | ~52 |  |  |
| OVT-S13 | TBQ4_QJL | TBQ4 | codec | U4 | 88 | 68 |  |  |
| OVT-S14 | TBQ4_QJL | TBQ3 | codec | U3 | 88 | 52 |  |  |
| OVT-S15 | TBQ4_QJL | TBQ4_QJL | codec | codec | 88 | 88 |  |  |
| OVT-S16 | TBQ4_QJL | TBQ3_QJL | codec | codec | 88 | 72 |  |  |
| OVT-S17 | TBQ4_QJL | POLAR4 | codec | codec | 88 | ~68 |  |  |
| OVT-S18 | TBQ4_QJL | POLAR3 | codec | codec | 88 | ~52 |  |  |
| OVT-S19 | TBQ3_QJL | TBQ4 | codec | U4 | 72 | 68 |  |  |
| OVT-S20 | TBQ3_QJL | TBQ3 | codec | U3 | 72 | 52 |  |  |
| OVT-S21 | TBQ3_QJL | TBQ4_QJL | codec | codec | 72 | 88 |  |  |
| OVT-S22 | TBQ3_QJL | TBQ3_QJL | codec | codec | 72 | 72 |  |  |
| OVT-S23 | TBQ3_QJL | POLAR4 | codec | codec | 72 | ~68 |  |  |
| OVT-S24 | TBQ3_QJL | POLAR3 | codec | codec | 72 | ~52 |  |  |
| OVT-S25 | POLAR4 | TBQ4 | codec | U4 | ~68 | 68 |  |  |
| OVT-S26 | POLAR4 | TBQ3 | codec | U3 | ~68 | 52 |  |  |
| OVT-S27 | POLAR4 | TBQ4_QJL | codec | codec | ~68 | 88 |  |  |
| OVT-S28 | POLAR4 | TBQ3_QJL | codec | codec | ~68 | 72 |  |  |
| OVT-S29 | POLAR4 | POLAR4 | codec | codec | ~68 | ~68 |  |  |
| OVT-S30 | POLAR4 | POLAR3 | codec | codec | ~68 | ~52 |  |  |
| OVT-S31 | POLAR3 | TBQ4 | codec | U4 | ~52 | 68 |  |  |
| OVT-S32 | POLAR3 | TBQ3 | codec | U3 | ~52 | 52 |  |  |
| OVT-S33 | POLAR3 | TBQ4_QJL | codec | codec | ~52 | 88 |  |  |
| OVT-S34 | POLAR3 | TBQ3_QJL | codec | codec | ~52 | 72 |  |  |
| OVT-S35 | POLAR3 | POLAR4 | codec | codec | ~52 | ~68 |  |  |
| OVT-S36 | POLAR3 | POLAR3 | codec | codec | ~52 | ~52 |  |  |

# 7. Baseline and primary end-to-end tests

| ID | Model | K codec | V codec | Context | Purpose | Status |
| --- | --- | --- | --- | --- | --- | --- |
| OVT-01 | Diagnostic IR | STANDARD | STANDARD | 1024 | Custom-runtime baseline |  |
| OVT-02 | Granite 3B | STANDARD | STANDARD | 2048 | Granite 3B custom baseline |  |
| OVT-03 | Granite 3B | TBQ4 | TBQ4 | 4096 | Granite 3B symmetric TBQ4 |  |
| OVT-04 | Granite 3B | TBQ3 | TBQ3 | 4096 | Granite 3B symmetric TBQ3 |  |
| OVT-05 | Granite 8B | STANDARD | STANDARD | 2048 | Granite 8B custom baseline |  |
| OVT-06 | Granite 8B | TBQ4 | TBQ4 | 4096 | Granite 8B symmetric TBQ4 |  |
| OVT-07 | Granite 8B | TBQ3 | TBQ3 | 4096 | Granite 8B symmetric TBQ3 |  |
| OVT-08 | Selected model | U8_SCALAR | TBQ4 | 4096 | Mixed U8 key / TBQ4 value |  |
| OVT-09 | Selected model | TBQ4 | U8_SCALAR | 4096 | Mixed TBQ4 key / U8 value |  |
| OVT-10 | Granite 3B | TBQ4_QJL | TBQ4_QJL | 4096 | Symmetric TBQ4+QJL full evaluation |  |
| OVT-11 | Granite 3B | TBQ3_QJL | TBQ3_QJL | 4096 | Symmetric TBQ3+QJL full evaluation |  |
| OVT-12 | Granite 3B | POLAR4 | POLAR4 | 4096 | Symmetric Polar4 full evaluation |  |
| OVT-13 | Granite 3B | POLAR3 | POLAR3 | 4096 | Symmetric Polar3 full evaluation |  |
| OVT-14 | Granite 3B | TBQ4_QJL | U8_SCALAR | 4096 | QJL key-only 4-bit family |  |
| OVT-15 | Granite 3B | U8_SCALAR | TBQ4_QJL | 4096 | QJL value-only 4-bit family |  |
| OVT-16 | Granite 3B | TBQ3_QJL | U8_SCALAR | 4096 | QJL key-only 3-bit family |  |
| OVT-17 | Granite 3B | U8_SCALAR | TBQ3_QJL | 4096 | QJL value-only 3-bit family |  |
| OVT-18 | Granite 3B | POLAR4 | U8_SCALAR | 4096 | Polar4 key-only |  |
| OVT-19 | Granite 3B | U8_SCALAR | POLAR4 | 4096 | Polar4 value-only |  |
| OVT-20 | Granite 3B | POLAR3 | U8_SCALAR | 4096 | Polar3 key-only |  |
| OVT-21 | Granite 3B | U8_SCALAR | POLAR3 | 4096 | Polar3 value-only |  |
| OVT-22 | Granite 3B | TBQ4 | POLAR4 | 4096 | Cross-family TBQ4 key / Polar4 value |  |
| OVT-23 | Granite 3B | POLAR4 | TBQ4 | 4096 | Cross-family Polar4 key / TBQ4 value |  |
| OVT-24 | Granite 3B | TBQ4_QJL | POLAR4 | 4096 | Cross-family QJL key / Polar4 value |  |
| OVT-25 | Granite 3B | POLAR4 | TBQ4_QJL | 4096 | Cross-family Polar4 key / QJL value |  |
| OVT-26 | Granite 3B | TBQ4 | TBQ4 | 4096 | TBQ4 norm-correction OFF/ON ablation |  |
| OVT-27 | Granite 3B | TBQ3 | TBQ3 | 4096 | TBQ3 norm-correction OFF/ON ablation |  |
| OVT-28 | Granite 3B | ALL SIX | ALL SIX | 2048 | Fused-quantize OFF/ON ablation for every codec |  |
| OVT-29 | Granite 3B | ALL SIX | ALL SIX | 4096 | Matched perplexity and P1-P6 quality comparison |  |
| OVT-30 | Granite 3B | ALL SIX | ALL SIX | 512/2048/4096/8192 | Context-scaling comparison for every codec |  |
| OVT-31 | Granite 3B | ALL SIX | ALL SIX | 4096 | Repeatability, stability and corruption check for every codec |  |
| OVT-32 | Granite 8B | Best QJL | Best QJL | 4096 if safe | 8B best-QJL feasibility gate |  |
| OVT-33 | Granite 8B | Best Polar | Best Polar | 4096 if safe | 8B best-Polar feasibility gate |  |
| OVT-34 | Selected model | Selected codec | Selected codec | 1024 | GPU codec request negative/fallback gate |  |
| OVT-35 | Granite 3B | Selected codecs | Selected codecs | 4096 | Regression suite after codec or runtime changes |  |
| OVT-36 | Diagnostic IR | ALL SIX | ALL SIX | 256 | Unsupported-path and limitation checks: PagedAttention, prefill compression, non-SDPA and unsupported head dimensions |  |

# 8. Ablation, quality and context-control plan

| Test ID | Variants that must be run | Control held constant | Decision output |
| --- | --- | --- | --- |
| OVT-26 | OV_TURBOQ_NORM_CORRECTION OFF and ON for TBQ4 | Model, weights, prompt, seed, context, device | Memory/speed/quality effect and default recommendation |
| OVT-27 | OV_TURBOQ_NORM_CORRECTION OFF and ON for TBQ3 | Same | Memory/speed/quality effect and default recommendation |
| OVT-28 | OV_TURBOQ_FUSED_QUANTIZE OFF and ON for all six codecs | Same | Correctness and speed benefit or incompatibility |
| OVT-29 | Standard baseline plus all six symmetric codecs | Same model/weights/context/P1-P6 | Perplexity and weighted quality comparison |
| OVT-30 | 512, 2K, 4K and 8K for all six symmetric codecs | Same prompt family and sampling | Maximum stable context and memory curve |
| OVT-31 | Pilot, excluded warm-up, at least three measured runs per codec | Frozen configuration | Repeatability, variance, crash/corruption rate |
| OVT-36 | PagedAttention, prefill compression, non-SDPA and unsupported head dimensions | Diagnostic fixtures | Supported/blocked/not-applicable boundary |

# 9. Runtime and GenAI compatibility attempts

| Attempt | Runtime commit | GenAI version/commit | Build result | Standard baseline | Codec properties visible | Six-codec activation summary | Reason kept/rejected |
| --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |

# 10. Device, codec activation and fallback verification

| Test ID | Requested K/V codec | Verified K/V codec | Actual device/backend | Model/KV placement | Expected/actual record bytes | Silent fallback check | Activation evidence | Status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |

[[PAGEBREAK]]

# 11. Formal performance and memory results

Use one pilot, one excluded warm-up and at least three measured repetitions for every frozen formal configuration unless a documented safety gate blocks it.

| Test/config | Rep role/no. | Load ms | TTFT ms | Prompt tok/s | TPOT ms | Decode tok/s | Peak private MB | KV MB | CPU mean/peak | Status |
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
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |

# 12. P1-P6 quality and perplexity summary

| Codec/config | Perplexity/delta | P1 | P2 | P3 | P4 | P5 | P6 | Weighted /10 | Failure/cap | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Standard control |  |  |  |  |  |  |  |  |  |  |
| TBQ4 |  |  |  |  |  |  |  |  |  |  |
| TBQ3 |  |  |  |  |  |  |  |  |  |  |
| TBQ4_QJL |  |  |  |  |  |  |  |  |  |  |
| TBQ3_QJL |  |  |  |  |  |  |  |  |  |  |
| POLAR4 |  |  |  |  |  |  |  |  |  |  |
| POLAR3 |  |  |  |  |  |  |  |  |  |  |

# 13. Cross-family and asymmetric correctness summary

| Test IDs | What correctness must be proved | Observed result | Evidence | Decision |
| --- | --- | --- | --- | --- |
| OVT-S01 to OVT-S36 | Every ordered K/V pair dispatches to the requested codecs without silent fallback |  |  |  |
| OVT-A09 | K codec drives query rotation/projection; V codec drives output-domain correction |  |  |  |
| OVT-14 to OVT-21 | QJL and Polar work in key-only and value-only positions against scalar U8 |  |  |  |
| OVT-22 to OVT-25 | Cross-family TBQ/Polar/QJL combinations preserve output correctness |  |  |  |
| OVT-A11 | Measured record size and KV allocation agree with the pinned implementation |  |  |  |

# 14. Failure and limitation log

| Failure ID | Test ID | Codec | Code | Description | Root cause/status | Fix/next action | Retest run | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |

Codes include: BF, DEP, WIN, MODEL, ARCH, BASE, TQ-ACT, QJL-ACT, POLAR-ACT, CODEC-FALLBACK, TQ-CRASH, CPU, GPU, OOM, MEM, PERF, QUAL, REPRO, SCOPE.

[[PAGEBREAK]]

# 15. Final experimental-route decision

| Field | Record |
| --- | --- |
| Pinned Runtime/GenAI pair |  |
| TBQ4 result |  |
| TBQ3 result |  |
| TBQ4_QJL result |  |
| TBQ3_QJL result |  |
| Polar4 result |  |
| Polar3 result |  |
| All 36 pair sweep |  |
| Best memory mode |  |
| Best quality-preserving mode |  |
| Best speed mode |  |
| Maximum stable context |  |
| Granite 8B feasibility |  |
| GPU/unsupported paths |  |
| Integration difficulty and maintenance risk |  |
| Final status |  |
| Application role |  |
| Main evidence path |  |
| Final bounded reasoning |  |
