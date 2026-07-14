<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# ADR-TurboVec: TurboVec First-Release Decision

**Status:** Proposed — technical and supervisor gate required  
**Date:** YYYY-MM-DD  
**Decision owner:** Arian B  
**Related IDs:** R-M02, R-M13, F-M25, F-M26, F-M27, TV-01 to TV-04

## Context

The project intends to evaluate a bounded TurboVec-assisted knowledge-file workflow. The decision must be based on a pinned implementation, licence/provenance review, successful build/run on the target Windows Intel system and a matched uncompressed retrieval baseline.

## Decision Gate

Record **Implement**, **Defer** or **Exclude** after the following evidence exists:

- Repository, commit, licence and build instructions are pinned.
- A minimal vector creation/compression/retrieval run succeeds or fails with preserved evidence.
- Requested and actual device/backend state is recorded.
- A matched uncompressed baseline is available.
- Retrieval usefulness, compression, latency, memory and limitations can be measured.
- The schedule effect does not displace Core Must-Have work.

## Options

1. Implement the bounded app-integrated workflow.
2. Keep a command-line research demonstrator only.
3. Defer implementation and report the feasibility result.
4. Exclude because provenance, licence, compatibility or time is unacceptable.

## Decision

_To be completed._

## Evidence

- `experiments/raw-results/turbovec/`
- `experiments/processed-results/EXP-TV-COMP-001/`
- `docs/evidence/requirements/F-M25/` to `F-M27/`
- `docs/evidence/requirements/R-M02/` if a requirement-specific summary is later added

## Consequences and Claim Boundary

State exactly what ran, what did not run, what comparison was performed and whether the result is app-integrated, command-line only or deferred.
