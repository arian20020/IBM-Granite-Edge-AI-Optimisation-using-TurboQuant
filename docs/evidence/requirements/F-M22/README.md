<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# F-M22 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | F-M22 |
| Category | Reliability |
| Priority | Must |
| Release role | Core |
| Baseline | v1.2 Draft |
| Status in RTM when generated | Not Started |
| Test / evidence ID | AC-F-M22 |
| Work package(s) | QX-04; RT-05 |
| Objective(s) | O5; O6; O8 |
| Research question(s) | RQ1; RQ4 |
| Planned component(s) | Fallback orchestration; capability registry |
| Planned evidence path | `docs/evidence/requirements/F-M22/` |

## Requirement

A dependable upstream llama.cpp configuration must remain available when the TurboQuant route is unavailable or fails.

## Why it exists

An experimental backend must not make the core product unusable.

## Acceptance criteria

A forced TQ unavailability/failure returns the user to a verified upstream configuration without claiming TQ was active.

## Verification method

Forced-failure and recovery test

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
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-F-M22 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

