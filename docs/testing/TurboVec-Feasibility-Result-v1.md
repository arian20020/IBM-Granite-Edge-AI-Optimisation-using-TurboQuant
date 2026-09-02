# TurboVec Feasibility Result v1

## Decision

**DEMONSTRATOR_ONLY** — superseding run `EXP-TV-COMP-001-20260902T231605Z-005`.

Candidate: TurboVec 1.0.0, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, tree `0a7141836d01da61e6f3cf53b5c60916b741d811`, MIT licence.

## Executed gates

- Repository baseline: 111/111 Python tests passed.
- Controlled TurboVec harness: 31/31 passed.
- PdfPig extraction: 5/5 passed.
- Upstream TurboVec Python suite: 321 passed, 1 Windows long-path persistence test failed, 157 skipped.
- Rust tests/clippy: unexecuted because Rust 1.89 was absent and installation was not approved.
- Granite/OpenVINO embedding: passed after locking Optimum-Intel 1.27.0 with Transformers 4.51.3. OpenVINO CPU embeddings matched the PyTorch reference to floating-point tolerance.
- Exact/TQ2/TQ3/TQ4 retrieval: completed with five warm-ups and 30 measured full-query batches per configuration. TQ3 passed relative nDCG and p95 thresholds, but no configuration passed recall and storage; TQ2 and TQ4 also missed quality thresholds.

## Claim boundary

This result supports a report-side command-line demonstrator only. It does not approve product integration, packaging or release. The 30-vector corpus is too small to amortize TurboVec's fixed index overhead, so the measured storage ratios must not be generalized to production-scale corpora. F-M25, F-M26 and F-M27 remain deferred and unimplemented.
