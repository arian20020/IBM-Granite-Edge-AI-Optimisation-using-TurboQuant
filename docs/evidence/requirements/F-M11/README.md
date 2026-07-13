<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# F-M11 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | F-M11 |
| Category | Functional |
| Priority | Must |
| Release role | Core |
| Baseline | v1.2 Draft |
| Status in RTM when generated | In Progress |
| Test / evidence ID | AC-F-M11 |
| Work package(s) | HE-06 |
| Objective(s) | O4; O8 |
| Research question(s) | RQ3; RQ4 |
| Planned component(s) | ModeSelectionService; ModelPreferences UI |
| Planned evidence path | `docs/evidence/requirements/F-M11/` |

## Requirement

The application must provide Automatic, Quality, Balanced and Efficiency modes when valid alternatives exist.

## Why it exists

Users need goal-based choices rather than low-level flags.

## Acceptance criteria

Each visible mode resolves to a complete valid configuration with a documented ranking rationale; unavailable modes are hidden or disabled; slider label updates correctly.

## Verification method

Mode algorithm tests and UI interaction tests

## Existing evidence at generation time

The model-preferences slider UI exists but final algorithm is not implemented.

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
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-F-M11 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

