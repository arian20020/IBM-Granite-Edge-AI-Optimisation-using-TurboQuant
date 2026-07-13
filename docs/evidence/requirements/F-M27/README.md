<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# F-M27 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | F-M27 |
| Category | Functional / AI |
| Priority | Must |
| Release role | App-integrated Experimental |
| Baseline | v1.2 Draft |
| Status in RTM when generated | Not Started |
| Test / evidence ID | AC-F-M27 |
| Work package(s) | TV-03; TV-04 |
| Objective(s) | O9; O6 |
| Research question(s) | RQ-TV; RQ4 |
| Planned component(s) | RetrievalService; prompt-context builder; ChatPage |
| Planned evidence path | `docs/evidence/requirements/F-M27/` |

## Requirement

The application must retrieve relevant sections from the local index and provide them to the Granite chat workflow.

## Why it exists

The TurboVec result must be usable by the end application.

## Acceptance criteria

For a fixed question set, retrieved chunks are shown/recorded and passed into Granite; answer and retrieval evidence are retained.

## Verification method

Retrieval and end-to-end RAG-style test

## Existing evidence at generation time

None recorded yet.

## Current note / next action

No additional note recorded.

## What should be stored or linked here

- A short test or verification report that states the **result**, not only the procedure.
- Links to the exact automated test files, commit, pull request or release tag.
- Small screenshots, logs or tables that are useful for review.
- Links to immutable raw results under `experiments/raw-results/`.
- Links to reproducible processed results under `experiments/processed-results/`.
- Environment details when relevant: Windows version, hardware, model hash, runtime commit, build options and requested/actual device.
- A final **Pass**, **Fail**, **Blocked** or **Not tested** conclusion against the acceptance criteria.
- Reviewer/validator name and validation date when the requirement is formally verified.

## Evidence index

| Evidence item | Type | Evidence ID | Date | Commit / tag | Result | Notes |
|---|---|---|---|---|---|---|
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-F-M27 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

