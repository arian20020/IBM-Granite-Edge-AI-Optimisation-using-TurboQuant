# ADR-TurboVec: TurboVec release role

**Status:** Accepted — Command-line demonstrator only

**Date:** 2026-08-21

**Decision owner:** Arian B

**Related IDs:** R-M02, R-M13, F-M25, F-M26, F-M27, TV-01 to TV-04

**Related change:** CHG-013 / CR-013

## Context

The project evaluated the exact TurboVec `1.0.0` Windows x64 wheel from upstream
commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49` under its MIT licence. A
controlled, offline command-line workflow now creates a matched float32/2-bit/
4-bit index, reloads both persisted TurboVec routes, executes bounded retrieval,
and atomically retains gate evidence.

The controlled run used an AMD Ryzen 7 8845HS and
`CPUExecutionProvider`, not Intel hardware. Four-bit retrieval quality passed,
but persisted-size and warm median search-latency gates failed. The simultaneous
release gate therefore failed.

## Decision

**Command-line demonstrator only.**

Retain the explicit offline research CLI and its reproducible evidence workflow.
Do not integrate TurboVec into the WinUI application or Granite chat path in
this release. Knowledge-file attachments remain selection-only and display
`Not indexed`; no document text or retrieved context is injected into Granite
prompts.

Promotion to product implementation requires a new approved change and fresh
evidence on representative Intel hardware that passes every existing quality,
storage, and latency criterion without relaxing thresholds.

## Evidence

The controlled run is indexed in
`docs/evidence/turbovec/controlled-evidence-index.json`; reproduction details and
limitations are in `docs/evidence/turbovec/README.md`.

- Windows x64 doctor, dependency/model/wheel identity, and CPU provider: pass.
- Persist/load/query for both 2-bit and 4-bit routes: pass.
- Recall@10 `0.9571428571428573`: pass.
- MRR ratio `1.0`: pass.
- Hit@5 delta `0.0`: pass.
- Four-bit storage ratio `13.352716619318182`: fail.
- Four-bit warm median latency ratio `2.383012969628705`: fail.
- Intel hardware behavior/performance: unknown.

## Consequences

The repository contains a useful, auditable research demonstrator with no
automatic model or package downloads during ordinary operation. It does not
claim TurboVec product readiness, Intel performance, knowledge-file indexing,
retrieval-augmented generation, or application integration. Future experiments
can reuse the pinned contracts and compare new hardware or larger corpora while
preserving the current release boundary.
