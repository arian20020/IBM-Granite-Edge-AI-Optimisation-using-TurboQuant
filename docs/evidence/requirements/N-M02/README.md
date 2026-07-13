<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# N-M02 â€” Requirement Evidence

> **Important:** Creating this folder does **not** prove that the requirement is complete. The requirement can be marked **Verified** only after the acceptance criteria have been tested and the evidence is linked below.

## Requirement record

| Field | Value |
|---|---|
| Requirement ID | N-M02 |
| Category | Privacy |
| Priority | Must |
| Release role | Core |
| Baseline | v1.2 Draft |
| Status in RTM when generated | Not Started |
| Test / evidence ID | AC-N-M02 |
| Work package(s) | FR-02 |
| Objective(s) | O1; O8; O9 |
| Research question(s) | RQ4; RQ-TV |
| Planned component(s) | Network/data-flow controls; logging policy |
| Planned evidence path | `docs/evidence/requirements/N-M02/` |

## Requirement

The application must not upload models, prompts, knowledge files or answers to a cloud AI service in the core workflow.

## Why it exists

Target sectors require careful data handling.

## Acceptance criteria

Network inspection shows no cloud AI upload during core model/document/chat workflow; any initial approved download is separately documented.

## Verification method

Network/offline check and data-flow review

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
| _Add a relative path or external controlled link_ | _test report / log / screenshot / measurement_ | AC-N-M02 | YYYY-MM-DD | _SHA or tag_ | _Pass / Fail / Blocked_ | _Why this evidence is sufficient_ |

## Completion rule

Do not change the RTM status to **Verified** until the acceptance criteria above are satisfied and this index points to the evidence needed to reproduce or review the conclusion.

