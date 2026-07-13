<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# N-M11 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | N-M11 |
| Category | Evidence / Reliability |
| Priority | Must |
| Release role | Core |
| Baseline | v1.2 Draft |
| Status in RTM when generated | Partially Verified |
| Test / evidence ID | AC-N-M11 |
| Work package(s) | QX-03; QX-04; OV-03; TV-03 |
| Objective(s) | O8; O11 |
| Research question(s) | RQ1; RQ2 |
| Planned component(s) | Activation verifier; BackendRunResult |
| Planned evidence path | `docs/evidence/requirements/N-M11/` |

## Requirement

An experimental option must not be reported as active unless activation is proved.

## Why it exists

Accepted flags and successful output are insufficient proof.

## Acceptance criteria

When proof is absent the run is labelled Unverified/Fallback rather than active; supported routes have direct activation evidence.

## Verification method

Log/manifest audit and forced-unverified test

## Existing evidence at generation time

AtomicBot/OpenVINO workbooks contain repository-specific activation evidence and cautions.

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
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-N-M11 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

