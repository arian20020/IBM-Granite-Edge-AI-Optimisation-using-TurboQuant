<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# F-M25 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | F-M25 |
| Category | Functional / Data |
| Priority | Must |
| Release role | App-integrated Experimental |
| Baseline | v1.2 Draft |
| Status in RTM when generated | Not Started |
| Test / evidence ID | AC-F-M25 |
| Work package(s) | TV-01; TV-02 |
| Objective(s) | O9 |
| Research question(s) | RQ4; RQ-TV |
| Planned component(s) | KnowledgeFileImportService; text extractor; chunker |
| Planned evidence path | `docs/evidence/requirements/F-M25/` |

## Requirement

The application must import and preserve at least one supported text-based knowledge file for the bounded retrieval workflow.

## Why it exists

TurboVec requires a document-to-vector workflow distinct from model import.

## Acceptance criteria

A supported local file is validated, text is extracted/chunked, and the original file hash remains unchanged.

## Verification method

Fixture, extraction, chunking and hash tests

## Existing evidence at generation time

None recorded yet.

## Current note / next action

Exact supported file type and size limit must be frozen.

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
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-F-M25 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

