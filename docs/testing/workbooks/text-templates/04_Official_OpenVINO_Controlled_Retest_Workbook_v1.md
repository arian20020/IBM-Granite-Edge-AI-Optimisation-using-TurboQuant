# 04 Official OpenVINO Controlled Retest Workbook v1.9

**Controlled filename:** `04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`
**Generated DOCX hash:** recorded in `Controlled-Workbook-Manifest.csv`
**Original source:** `04_Official_OpenVINO_Editable_Test_Workbook.docx`
**Original source SHA-256:** `e892a1a9ea2956e10f3eaa9dba89dcef59a7a6f89cb21b34e274aa3d72baa2b3`

Controlled retest revision 1.9 (WR-037).

Workbook version: 1.9
Revision ID: WR-037
Revision date: 2026-08-02

# 1. Repository, runtime and host

| Fact | Verified record |
| --- | --- |
| Adaptive comparison matrix | `experiments/manifests/official-openvino/adaptive-format-comparison-matrix-v1.json`; SHA-256 `3a26bd8bd979a539fb1eee3f08641e409963cc477a6fda5b5688bebe4e523aca`; five controlled route IDs. |
| Runtime source | Patched OpenVINO GenAI campaign: upstream `7dea0459b2ac7d8dfd877fd9df6737674`, patch `00edae3bfd40a968c964ea4878128dceeeb22d1a`, derived tree `2e872dd4817c42d91cb7c3094954d7b56fa12b0a`. |
| Runtime versions | OpenVINO `2026.2.1-21919-ede283a88e3`; OpenVINO GenAI `2026.2.1.0-2-00edae3bfd4`; Python 3.13.14. |
| Host | Lenovo 83ER; Intel Core i5-12450H; Intel UHD driver 32.0.101.7076; Windows 11 build 26200; 16,857,817,088 installed physical bytes. |
| Host evidence | `experiments/granite_turboquant_intel/manifests/environments/ENV-20260730-INTEL-LAPTOP-01-WB04/machine-manifest.json`; SHA-256 `23b4fb6e849ae6cf7538d0d3ae17d5859cb88a664a2f83135105146b50488b17`. |

# 2. Successful build and recovery checks

| Controlled checks | Verified positive result | Source-bound evidence |
| --- | --- | --- |
| OV-B01, OV-B02 | Pinned source identity and patched GenAI build passed. | `provenance/source-identity-bounded.json` SHA-256 `44f4bfc111c78258320c0788ddba288f0f54840845cefac6fdfcae9b772e6574`; `provenance/build-00edae3b-attempt-001/build-provenance.json` SHA-256 `57fb318a55db56fb409a60f0b1d516a988f8543c0446153e63efdee3a748e262`. |
| OV-B03, OV-B05, OV-B06, OV-B07 | CPU route, bounded diagnostic execution, metrics controller, and package/environment binding gates passed. | Frozen matrix, build provenance, accepted summary schema, and reviewed host manifest. |
| Official source suite | 505/505 tests passed across 77 suites; zero failed and zero skipped. | `experiments/raw-results/openvino-turboquant/2026-07-29/guards/task03-genai-official-tests/genai-tests-full-005.json`. |
| Allocation observer recovery | Focused discovery found 46 tests; run 1 passed 46/46 and run 2 passed 46/46. The production CPU plugin and CPU functional binary were verified, `query_state()` passed, and the invalid-destination fail-closed check passed. | `experiments/raw-results/openvino-turboquant/2026-07-29/guards/task03-attempt-006/t3a6-list2.json`, `t3a6-run1.json`, `t3a6-run2.json`, `t3a6-plugin.json`, `t3a6-funcbuild.json`, and `t3-bad.json`. |

# 3. Successful bounded diagnostics

| Test ID | Verified diagnostic result | Diagnostic-only caveat | Evidence |
| --- | --- | --- | --- |
| OV-C02 | Granite 3B U8 short CPU STANDARD generation produced valid output with 7 input tokens, 4 generated tokens, `fallback=false`, cleanup 0, and concrete K/V state f32/f32. | Diagnostic only; this weight-artifact success is not a formal benchmark, not scalar-cache proof, and not a quality result. | `diagnostics/u8-standard-load-probe-attempt-002/run/attempt.json`; SHA-256 `720741b6eed42475abe6c23fad8a63d0850e78490145dc1368721490f7444605`. |
| OV-C03 | Granite 3B U4 short CPU STANDARD generation produced valid output with 7 input tokens, 4 generated tokens, `fallback=false`, cleanup 0, and concrete K/V state f32/f32. | Diagnostic only; this weight-artifact success is not a formal benchmark, not scalar-cache proof, and not a quality result. | `diagnostics/u4-standard-load-probe-attempt-001/run/attempt.json`; SHA-256 `90a9b83423dcc1dbff7d3eb7a49e57262b23d253541385af937f914a87a0af9c`. |

# 4. Successful expected-rejection controls

| Control group | Explicit controlled IDs | Verified successful boundary |
| --- | --- | --- |
| Scalar-state controls | OV-04/4096, OV-05/4096, OV-TQ-01/4096, OV-TQ-02/4096 | Passed expected rejection after bounded STANDARD activation showed concrete f32/f32 K/V state rather than the requested scalar precision; these are successful boundary checks, not benchmark or quality results. |
| Property-boundary controls | OV-TQS-05, OV-TQS-06, OV-TQS-07, OV-TQS-08, OV-TQS-09, OV-TQS-10, OV-TQS-11, OV-TQS-12 | Passed the setup/property boundary by rejecting unsupported scalar/TurboQuant combinations before generation; these are successful boundary checks, not benchmark or quality results. |
| Device/codec controls | OV-TQ-18/1024, OV-TQ-19/256, OV-TQ-20/256, OV-B11 | Passed the device/codec boundary by rejecting GPU TurboQuant, QJL, Polar, and the invalid destination before generation; these are successful boundary checks, not benchmark or quality results. |

[[PAGEBREAK]]

<!-- BEGIN WB-04 V1.9 COMPARISON -->

# 5. U8 STANDARD, TBQ4 and TBQ3 shared-context comparison

No shared completed context exists for the three U8-weight cache routes. The
campaign closed after the OV-11 control, so no direct cache winner is reported.

| Test ID | Cache route | Terminal status | Controlled interpretation |
| --- | --- | --- | --- |
| OV-12 | U8 STANDARD | Not launched | Follow-on route stopped at the terminal laptop checkpoint. |
| OV-TQ-21 | TBQ4 | Not launched | Follow-on route stopped at the terminal laptop checkpoint. |
| OV-TQ-22 | TBQ3 | Not launched | Follow-on route stopped at the terminal laptop checkpoint. |

# 6. U4, U8 and FP16 STANDARD deployment comparison

Only the U4 STANDARD route completed a formal measurement. The absence of a
shared completed context prevents a direct weight-format comparison.

| Test ID | Weight route | Context | Outcome | Evidence boundary |
| --- | --- | --- | --- | --- |
| OV-11 | U4 STANDARD | 512 | Passed | Three accepted CPU repetitions; fallback false; cleanup zero. |
| OV-12 | U8 STANDARD | 512 | Not launched | Campaign closed before this route. |
| OV-13 | FP16 STANDARD | 512 | Artefact boundary | No validated FP16 artefact was admitted. |

# 7. Complete timing results

The first row is the v1.9 STANDARD control. The remaining rows retain the
earlier v1.8 governed TurboQuant measurements for historical continuity; they
are not treated as a matched adaptive comparison.

| Evidence stage | Test ID | Context | Load ms | TTFT ms | Prompt tok/s | TPOT ms | Decode tok/s | Generation ms |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| v1.9 formal control | OV-11 | 512 | 4727.2827 | 32537.6674 | 15.826758 | 174.974472 | 5.715119 | 33484.0263 |
| v1.8 retained measurement | OV-TQ-13 | 512 | 8425.953 | 19149.389 | 28.841 | 679.548 | 1.472 | 19863.765 |
| v1.8 retained measurement | OV-TQ-14 | 512 | 8079.655 | 18234.156 | 29.179 | 666.685 | 1.5 | 19576.744 |
| v1.8 retained measurement | OV-TQ-14 | 2048 | 8209.418 | 83969.485 | 25.66 | 2048.262 | 0.488 | 86068.419 |

# 8. Complete memory and CPU/GPU results

| Evidence stage | Test ID | Context | Peak WS | Peak private | Minimum available RAM | KV allocation | CPU mean/peak % | GPU mean/peak % | Cleanup |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| v1.9 formal control | OV-11 | 512 | 4325171200 bytes | 2905325568 bytes | 4278976512 bytes | 84377600 bytes | 9.107368 / 68.450225 | 0 / 0 | 0 residual processes |
| v1.8 retained measurement | OV-TQ-13 | 512 | 7142.34 MiB | 4330 MiB | 3112.461 MiB | 12.573 MiB | 8.483 / 18.296 | 0 / 0 | 0 residual processes |
| v1.8 retained measurement | OV-TQ-14 | 512 | 7137.219 MiB | 4323.207 MiB | 3025.695 MiB | 10.059 MiB | 8.528 / 23.92 | 0 / 0 | 0 residual processes |
| v1.8 retained measurement | OV-TQ-14 | 2048 | 8286.062 MiB | 5463.633 MiB | 2153.18 MiB | 40.059 MiB | 8.185 / 19.746 | 0 / 0 | 0 residual processes |

# 9. P1–P6 and aggregate quality results

The OV-11 sequence completed P1--P4, stopped P5 when available memory reached
1993.03125 MiB against the unchanged 2048 MiB floor, and did not launch P6.
No governed P1--P6 sequence completed, so no aggregate score or winner exists.

| Test ID | Context | P1 | P2 | P3 | P4 | P5 | P6 | Aggregate |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OV-11 | 512 | Completed | Completed | Completed | Completed | RAM-floor stop | Not run | Withheld: incomplete sequence |

# 10. Laptop runtime-capable and fully-comparable boundaries

| Test ID | Highest runtime context | Highest fully comparable context | First confirmed boundary | Terminal stage |
| --- | --- | --- | --- | --- |
| OV-11 | 512 | Not established | P5 quality step | Quality RAM floor |
| OV-12 | Not observed | Not observed | 512 | Campaign checkpoint |
| OV-13 | Not observed | Not observed | 512 | Artefact admission |
| OV-TQ-21 | Not observed | Not observed | 512 | Campaign checkpoint |
| OV-TQ-22 | Not observed | Not observed | 512 | Campaign checkpoint |

# 11. Terminal attempts and hash-bound evidence

| Identity | Status | Principal reason | Hash-bound evidence |
| --- | --- | --- | --- |
| OV-11/512 runtime | Passed | Three accepted formal CPU samples; fallback false; cleanup zero. | Measurement SHA-256 `ec933862686e399c19e3ce69bdc11baae2cf10025d1a245db3dcc85c0ae3c3e0` |
| OV-11/512 quality | Terminal | P5 crossed the fixed RAM floor; P6 was not launched. | Campaign-state SHA-256 `92101643ff26a2ccc27dd0965a6ae1a0788586c3edcbd6cab9d9b2c4d266c8e8` |
| OV-13/512 | Terminal | Validated FP16 artefact was unavailable. | Recorded in the controlled failure and evidence registers. |
| OV-12/512 | Not launched | Campaign closed after the OV-11 quality terminal. | Recorded in the controlled campaign state. |
| OV-TQ-21/512 | Not launched | Campaign closed after the OV-11 quality terminal. | Recorded in the controlled campaign state. |
| OV-TQ-22/512 | Not launched | Campaign closed after the OV-11 quality terminal. | Recorded in the controlled campaign state. |

<!-- END WB-04 V1.9 COMPARISON -->
