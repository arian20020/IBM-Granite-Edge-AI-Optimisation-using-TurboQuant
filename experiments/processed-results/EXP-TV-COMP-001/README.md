<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# R-M13 Evidence Location

Latest decision: **DEMONSTRATOR_ONLY** for run `EXP-TV-COMP-001-20260902T231605Z-005`. The real Granite/OpenVINO and Exact/TQ2/TQ3/TQ4 campaign executed, but no configuration passed every Gate A threshold.

Earlier decision: **BLOCKED** for TurboVec commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, run `EXP-TV-COMP-001-20260902T225731Z-001`. The later demonstrator-only run above replaces this as the current decision. Product status remains deferred.

## Purpose

Planned evidence location for requirement R-M13: The project must compare TurboVec-compressed or optimised vectors with an uncompressed-vector baseline.

## What belongs here

- Evidence matching EXP-TV-COMP-001.
- Acceptance criteria: Same documents, chunking, embeddings and queries; compare storage, memory, index/query time, relevance, answer usefulness, failures and stability.
- Verification method: Matched retrieval experiment
- Evidence index or README linking the artefacts to the requirement.

## Related IDs

R-M13

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Use stable requirement, work-package, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.

## Source

RTM Planned Evidence Path

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This folder contains processed outputs for the TurboVec matched-retrieval feasibility experiment. Its latest formal interpretation remains `DEMONSTRATOR_ONLY`; these results do not establish product readiness.

The production-scale v2 [supporting rerun from 11 September 2026](production-scale-v2/supporting-rerun-2026-09-11/README.md) completed quiet-host comparisons at 1,000 and 10,000 documents. It retained a `DEMONSTRATOR_ONLY` disposition and recommends no application integration because every TurboVec format failed the locked retrieval-quality thresholds. Its documented Python/model-export deviation means it supplements rather than replaces the historical formal record.

### Start here

This is a navigation or evidence container. Use the folder explanations below to choose the next level.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Generated child folders

**Generated run or test-case folders:** 4 folder(s), for example `EXP-TV-COMP-001-20260902T225731Z-001/`, `EXP-TV-COMP-001-20260902T231605Z-003/`, `EXP-TV-COMP-001-20260902T231605Z-004/`, `EXP-TV-COMP-001-20260902T231605Z-005/`. These hold individual runs, test cases or generated stages. They do not receive separate README files because adding documentation inside captured run folders could blur the evidence boundary.

### Files

There are no immediate non-README files at this level. Continue into the child folders described above.

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
