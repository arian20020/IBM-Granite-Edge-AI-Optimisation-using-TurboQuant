<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# F-M18 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | F-M18 |
| Category | Functional / Security |
| Priority | Must |
| Release role | Core |
| Baseline | v1.2 Draft |
| Status in RTM when generated | Not Started |
| Test / evidence ID | AC-F-M18 |
| Work package(s) | RT-01; RT-02; RT-05 |
| Objective(s) | O5; O6; O8 |
| Research question(s) | RQ1; RQ4 |
| Planned component(s) | ProcessRuntimeAdapter; runtime contracts |
| Planned evidence path | `docs/evidence/requirements/F-M18/` |

## Requirement

The WinUI application must launch and control supported local command-line runtimes without requiring the user to enter terminal commands.

## Why it exists

The app's main contribution is to hide specialist CLI operation behind a safe adapter.

## Acceptance criteria

Structured arguments, redirected stdout/stderr, hidden console, startup/error result, timeout and process-tree cleanup all pass tests.

## Verification method

Contract, path-safety and integration tests

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
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-F-M18 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

