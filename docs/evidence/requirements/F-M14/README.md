<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# F-M14 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | F-M14 |
| Category | Data Integrity |
| Priority | Must |
| Release role | Core |
| Baseline | v1.2 Draft |
| Status in RTM when generated | Not Started |
| Test / evidence ID | AC-F-M14 |
| Work package(s) | IM-04; QX-01; QX-02 |
| Objective(s) | O2; O7 |
| Research question(s) | RQ4 |
| Planned component(s) | File processing services |
| Planned evidence path | `docs/evidence/requirements/F-M14/` |

## Requirement

The application must keep the original model file unchanged.

## Why it exists

Inspection and optimisation must not damage the user's source asset.

## Acceptance criteria

SHA-256 before and after inspection/processing is identical; generated artefacts use a different path.

## Verification method

Hash integrity test

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
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-F-M14 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

