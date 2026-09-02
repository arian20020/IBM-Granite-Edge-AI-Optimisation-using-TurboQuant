# TurboVec Feasibility Result v1

## Decision

**BLOCKED** — run `EXP-TV-COMP-001-20260902T225731Z-001`.

Candidate: TurboVec 1.0.0, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, tree `0a7141836d01da61e6f3cf53b5c60916b741d811`, MIT licence.

## Executed gates

- Repository baseline: 111/111 Python tests passed.
- Controlled TurboVec harness: 31/31 passed.
- PdfPig extraction: 5/5 passed.
- Upstream TurboVec Python suite: 321 passed, 1 Windows long-path persistence test failed, 157 skipped.
- Rust tests/clippy: unexecuted because Rust 1.89 was absent and installation was not approved.
- Granite/OpenVINO embedding: failed. Pinned Granite revision `2ab6fa8ea2d674564defd37171ae19079b864b33` uses ModernBERT, which the locked OpenVINO 2026.3 exporter rejected for feature extraction; direct tracing also failed.
- Exact/TQ2/TQ3/TQ4 retrieval: unexecuted because the real-model gate failed.

## Claim boundary

This result proves neither suitability nor unsuitability. It supports no retrieval-quality, latency, memory, storage, application, packaging, or production claim. F-M25, F-M26 and F-M27 remain deferred and unimplemented. A new append-only run requires an approved, locked ModernBERT-to-OpenVINO path.
