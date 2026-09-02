# ADR-TurboVec: TurboVec First-Release Decision

> **2026-09-02 checkpoint:** **BLOCKED** for TurboVec commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49` (MIT), run `EXP-TV-COMP-001-20260902T225731Z-001`. Granite ModernBERT did not pass the locked OpenVINO embedding gate; product integration remains deferred.

**Status:** Accepted — full application integration deferred  
**Date:** 2026-07-14  
**Decision owner:** Arian B  
**Related IDs:** R-M02, R-M13, F-M25, F-M26, F-M27, TV-01 to TV-04  
**Related change:** CHG-013 / CR-013  

## Context

The project considered a bounded TurboVec-assisted knowledge-file workflow covering document import, text extraction, chunking, embeddings, vector optimisation, indexing, retrieval and Granite chat integration.

The exact TurboVec implementation, repository, commit, licence, Windows build process, input/output contract and matched uncompressed baseline have not yet passed the technical gate. Implementing the full subsystem would also compete with the core WinUI, Granite, llama.cpp, Intel hardware, TurboQuant and evidence work.

## Decision

1. Keep **R-M02** and **TV-01** active as the mandatory decision gate.
2. Identify and pin the exact implementation, review provenance/licence and record the build/run result and contract.
3. Record one release-role decision: **Implement**, **Command-line demonstrator only**, **Defer**, or **Exclude**.
4. Defer **F-M25**, **F-M26**, **F-M27**, **R-M13**, **TV-02**, **TV-03** and **TV-04** from the first release.
5. Do not present the deferred knowledge-file, embedding, vector, index, retrieval or chat integration as implemented.
6. Reactivation requires a new approved change after the technical gate passes and the core Must Haves are stable.

## Reasons

- The implementation and licence are not yet pinned.
- Windows/Intel compatibility is not established.
- No matched uncompressed retrieval baseline is controlled.
- Full integration is a separate substantial subsystem.
- Deferral protects the project’s essential application and TurboQuant contribution.
- R-M02 still permits a useful evidence-based feasibility conclusion.

## Consequences

### Benefits

- The first-release boundary becomes realistic.
- The project still answers the TurboVec feasibility question.
- No unsupported integration claim is made.
- Future work retains stable IDs and evidence locations.

### Costs

- The first release will not provide a TurboVec-assisted knowledge-file UI.
- EXP-TV-COMP-001 remains conditional and may not run.
- The report must clearly distinguish the feasibility decision from application integration.

## Evidence gate

The decision may be reviewed only after all of the following exist:

- repository, commit/version and licence;
- Windows build/run instructions;
- minimal run result with raw evidence;
- requested and actual device/backend state;
- input/output/vector/retrieval contract;
- matched uncompressed baseline;
- schedule assessment showing no displacement of core Must work.
