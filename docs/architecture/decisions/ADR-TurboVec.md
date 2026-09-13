# ADR-TurboVec: TurboVec First-Release Decision

> **2026-09-03 superseding checkpoint:** **DEMONSTRATOR_ONLY** for TurboVec commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, run `EXP-TV-COMP-001-20260902T231605Z-005`. The embedding blocker was cleared, but no configuration passed every Gate A threshold. Product integration remains deferred.

> **2026-09-02 checkpoint:** **BLOCKED** for TurboVec commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49` (MIT), run `EXP-TV-COMP-001-20260902T225731Z-001`. Granite ModernBERT did not pass the locked OpenVINO embedding gate; product integration remains deferred.

**Status:** Accepted — full application integration deferred  
**Date:** 2026-07-14  
**Decision owner:** Arian B  
**Related IDs:** R-M02, R-M13, F-M25, F-M26, F-M27, TV-01 to TV-04  
**Related change:** CHG-013 / CR-013  

## Context

The project considered a small TurboVec knowledge-file workflow. It would need
document import, text extraction, chunks, embeddings, a vector index, retrieval
and a link to Granite chat.

The project pinned and tested the TurboVec implementation. The first formal
large-scale attempt was blocked by the host-readiness rules. A later supporting
run tested Exact, TQ2, TQ3 and TQ4 at 1,000 and 10,000 chunks. The later run used
a different Python environment and model export, so it supports the decision
but does not replace the blocked formal record.

## Decision

1. Keep **R-M02** and **TV-01** active as the mandatory decision gate.
2. Identify and pin the exact implementation, review provenance/licence and record the build/run result and contract.
3. Record one release-role decision: **Implement**, **Command-line demonstrator only**, **Defer**, or **Exclude**.
4. Defer **F-M25**, **F-M26**, **F-M27**, **R-M13**, **TV-02**, **TV-03** and **TV-04** from the first release.
5. Do not present the deferred knowledge-file, embedding, vector, index, retrieval or chat integration as implemented.
6. Reactivation requires a new approved change after the technical gate passes and the core Must Haves are stable.

## Reasons

- Exact, TQ2, TQ3 and TQ4 all completed the supporting Windows test.
- Every compressed format passed the speed, storage and lifecycle checks but
  failed both fixed retrieval-quality limits.
- The Exact result was also weak, so the embedding model, export or test data
  may have affected the absolute scores.
- Full application integration would be a separate large piece of work.
- R-M02 permits a useful feasibility decision without claiming product
  integration.

## Consequences

### Benefits

- The first-release boundary becomes realistic.
- The project still answers the TurboVec feasibility question.
- No unsupported integration claim is made.
- Future work retains stable IDs and evidence locations.

### Costs

- The first release does not provide a TurboVec knowledge-file interface.
- The formal large-scale campaign remains blocked. The completed large-scale
  run is supporting evidence only.
- The report must keep the feasibility result separate from application
  integration.

## Evidence used

- [TurboVec feasibility result](../../testing/TurboVec-Feasibility-Result-v1.md)
- [Supporting production-scale rerun](../../../experiments/processed-results/EXP-TV-COMP-001/production-scale-v2/supporting-rerun-2026-09-11/README.md)
- Pinned TurboVec commit:
  `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`
- Superseding decision run:
  `EXP-TV-COMP-001-20260902T231605Z-005`

Any future integration needs a new approved change and stronger retrieval
quality on representative, non-sensitive documents.
