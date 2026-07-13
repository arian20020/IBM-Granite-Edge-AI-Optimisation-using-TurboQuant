<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# F-M01 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | F-M01 |
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Baseline | v1.2 Draft |
| Status in RTM when generated | Partially Verified |
| Test / evidence ID | AC-F-M01 |
| Work package(s) | IM-01; FR-04 |
| Objective(s) | O1; O11 |
| Research question(s) | RQ4 |
| Planned component(s) | WinUI shell; packaging |
| Planned evidence path | `docs/evidence/requirements/F-M01/` |

## Requirement

The application must open on the target Windows 11 x64 Intel computer.

## Why it exists

A usable prototype must launch on the target platform.

## Acceptance criteria

Clean checkout builds; release build launches twice on the target laptop without fatal error.

## Verification method

Clean-build test and release demo

## Existing evidence at generation time

Packaged WinUI shell and root Frame already exist.

## Current note / next action

ModelImportPage and release packaging still need completion.

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
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-F-M01 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

