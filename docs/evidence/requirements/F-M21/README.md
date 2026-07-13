<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# F-M21 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | F-M21 |
| Category | Functional / Experimental |
| Priority | Must |
| Release role | App-integrated Experimental |
| Baseline | v1.2 Draft |
| Status in RTM when generated | Not Started |
| Test / evidence ID | AC-F-M21 |
| Work package(s) | QX-03; QX-04 |
| Objective(s) | O5; O6; O8 |
| Research question(s) | RQ1; RQ2; RQ4 |
| Planned component(s) | TurboQuantRuntimeAdapter; experimental capability registry |
| Planned evidence path | `docs/evidence/requirements/F-M21/` |

## Requirement

The application must run at least one verified TurboQuant-enabled Granite configuration end to end.

## Why it exists

TurboQuant is a central contribution and must be more than an external benchmark.

## Acceptance criteria

A pinned supported model/runtime/cache/device combination is selected in WinUI, loads, generates through normal chat, proves TQ activation, records actual state, and supports cancellation.

## Verification method

App-integrated TurboQuant E2E and activation audit

## Existing evidence at generation time

AtomicBot feasibility: 15 formal passes; OpenVINO TQ U4 also validated outside app.

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
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-F-M21 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

