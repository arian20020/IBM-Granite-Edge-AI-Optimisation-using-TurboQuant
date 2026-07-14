# UL-B01-R001 Preparation Record

| Field | Value |
|---|---|
| Campaign | Granite-TurboQuant Controlled Retest Campaign v1 |
| Workbook | WB-01 — Upstream llama.cpp |
| Test ID | UL-B01 |
| Run ID | UL-B01-R001 |
| Route | upstream-llama-cpp |
| Operator | Student |
| Testing branch | testing/upstream-llama-cpp-controlled-retest |
| Project base commit | b28f707cd99600c2dd3cb726f897f17552c5f7be |
| Preparation timestamp | 2026-07-14T02:39:51.3611591+01:00 |
| Current classification | Planned |

## Purpose

Prepare the controlled evidence structure for cloning, pinning and verifying one exact upstream llama.cpp repository revision.

## Completed preparation

- Dedicated testing branch confirmed.
- Controlled run ID created.
- Run manifest created.
- Evidence directories created.
- Formal campaign logs made visible to Git.

## Not yet executed

- Upstream repository selection and commit pinning.
- llama.cpp clone.
- Repository licence and provenance verification.
- Clean build.
- Granite model loading.
- Performance, memory or quality testing.

## Next action

Run the controlled workspace validator, capture the target-machine environment and resolve every blocking validation finding before cloning llama.cpp.

## Workspace validation history

| Attempt | Result | Meaning |
|---|---|---|
| 1 | Failed readiness check | All controls passed except that zero of six generated DOCX workbooks were present. No hardware, build or inference test was executed. |
| 2 | Passed readiness check | Six workbooks were generated from canonical Markdown, revision history was applied, hashes matched the controlled manifest and the complete workspace validator passed. |

**Resolution timestamp:** 2026-07-14T02:44:55.9751914+01:00

The run is now Ready, not Passed. The next prerequisite is controlled target-machine environment capture.
