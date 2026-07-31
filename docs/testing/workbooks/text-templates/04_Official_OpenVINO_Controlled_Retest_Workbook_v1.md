# 04 Official OpenVINO Controlled Retest Workbook v1.8

Controlled retest revision 1.8 (WR-036).

# 1. Repository, runtime and host

| Fact | Verified record |
| --- | --- |
| Frozen matrix | `experiments/manifests/official-openvino/retest-matrix.json`; SHA-256 `7db2636b403d285aa3886c9f23560a9e48bd43645e9aca35e0d1fc4f16eaea42`; 60 controlled IDs. |
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

# 5. Accepted formal runtime measurements

All three accepted rows executed on CPU with `fallback=false`; cleanup was verified with zero residual processes.

**Timing metrics — aggregate medians**

| Test ID | Context | Load ms | TTFT ms | Prompt tok/s | TPOT ms | Decode tok/s | Generation ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| OV-TQ-13 | 512 | 8425.953 | 19149.389 | 28.841 | 679.548 | 1.472 | 19863.765 |
| OV-TQ-14 | 512 | 8079.655 | 18234.156 | 29.179 | 666.685 | 1.5 | 19576.744 |
| OV-TQ-14 | 2048 | 8209.418 | 83969.485 | 25.66 | 2048.262 | 0.488 | 86068.419 |

**Memory metrics — aggregate medians**

| Test ID | Context | Peak WS MiB | Peak private MiB | Available RAM min MiB | KV MiB | Cleanup |
| --- | --- | --- | --- | --- | --- | --- |
| OV-TQ-13 | 512 | 7142.34 | 4330 | 3112.461 | 12.573 | 0 |
| OV-TQ-14 | 512 | 7137.219 | 4323.207 | 3025.695 | 10.059 | 0 |
| OV-TQ-14 | 2048 | 8286.062 | 5463.633 | 2153.18 | 40.059 | 0 |

**CPU/GPU utilisation — aggregate summary**

| Test ID | Context | CPU mean/median/peak % | GPU mean/median/peak % | CPU samples | GPU samples | Accepted runs | Fallback count | Evidence ref |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| OV-TQ-13 | 512 | 8.483 / 8.248 / 18.296 | 0 / 0 / 0 | 348 | 51 | 3 | 0 | E1 |
| OV-TQ-14 | 512 | 8.528 / 8.218 / 23.92 | 0 / 0 / 0 | 344 | 51 | 3 | 0 | E2 |
| OV-TQ-14 | 2048 | 8.185 / 8.271 / 19.746 | 0 / 0 / 0 | 1142 | 165 | 3 | 0 | E3 |

# 6. Tests that did not complete

- **Diagnostic only; no formal benchmark:** OV-01
- **Missing validated FP16 artifact:** OV-C01, OV-02
- **RAM safety floor reached:** OV-B04, OV-03, OV-06, OV-TQ-03, OV-TQ-04, OV-TQ-05, OV-TQ-06, OV-TQ-07, OV-TQ-08, OV-TQ-09, OV-TQ-10, OV-TQ-11, OV-TQ-12, OV-TQ-13/2048, OV-TQ-13/4096, OV-TQ-13/8192, OV-TQ-14/4096, OV-TQ-14/8192, OV-TQ-15
- **Larger host required:** OV-C04, OV-C05, OV-C06, OV-07, OV-08, OV-09, OV-10, OV-TQ-16, OV-TQ-17 (larger host required)
- **Strict activation proof incomplete:** OV-B08, OV-B09, OV-B10, OV-B12, OV-TQS-01, OV-TQS-02, OV-TQS-03, OV-TQS-04
- **Governed quality campaign stopped at the RAM floor:** OV-TQ-13/512, OV-TQ-14/512, OV-TQ-14/2048

# 7. Quality boundary

No governed P1-P6 quality campaign completed. The governed evidence therefore provides no numeric quality score and no winner.

[[PAGEBREAK]]

# 8. Final decision and evidence index

**Final decision**

| Decision | Controlled conclusion |
| --- | --- |
| Proven runtime scope | Exactly OV-TQ-13/512, OV-TQ-14/512, and OV-TQ-14/2048 are accepted formal runtime measurements. |
| Best observed runtime facts | The observed runtime facts are: lowest median load 8079.655 ms (OV-TQ-14/512); lowest median TTFT 18234.156 ms (OV-TQ-14/512); highest median decode throughput 1.5 tok/s (OV-TQ-14/512). These are runtime observations; no winner is declared from quality evidence. |
| Execution boundary | The accepted formal scope is CPU-only with fallback=false and cleanup verified for every row. |
| GPU boundary | No accepted GPU formal measurement exists. |
| Non-TurboQuant boundary | No non-TurboQuant formal benchmark exists; the U4/U8 STANDARD results remain diagnostics only. |
| Continuation | Continue the blocked larger-context and larger-model campaigns on a higher-memory host. |
| Quality boundary | No numeric quality score exists and there is no winner. |

**Evidence index**

| Reference | Hash-bound source |
| --- | --- |
| E1 | experiments/raw-results/openvino-turboquant/2026-07-30/runtime/OV-TQ-13/context-512/measurement-summary.json#sha256=5fc814f16d749e863607db5f519ee564b6459f132a0b25c2a6aab800b8c7c550 |
| E2 | experiments/raw-results/openvino-turboquant/2026-07-30/runtime-frozen-85ed31e/OV-TQ-14/context-512/measurement-summary.json#sha256=3aea0c66a05faa64283cec1e59c16a9669fcbcb2c8520bfb3f5fadae0e0e5747 |
| E3 | experiments/raw-results/openvino-turboquant/2026-07-30/runtime/OV-TQ-14/context-2048/measurement-summary.json#sha256=2b3fbde825b91e4768325be0ff58254110de4c5e36981cf9d570806a11e13478 |
| Reconciliation input | experiments/raw-results/openvino-turboquant/2026-07-30/reconciliation-input.json#sha256=96f7355a9446092ddad6d68c75d73bbd956bd24cd3213f9e12db470a87410c65 |
