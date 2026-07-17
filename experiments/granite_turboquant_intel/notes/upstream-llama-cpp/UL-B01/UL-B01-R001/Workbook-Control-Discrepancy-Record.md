# Workbook Control Discrepancy Record

| Field | Value |
|---|---|
| Failure ID | FAIL-CTRL-001 |
| Run ID | UL-B01-R001 |
| Test ID | UL-B01 |
| Route | upstream-llama-cpp |
| Workbook | WB-01 |
| Detected UTC | 2026-07-14T02:03:14.9393970Z |
| Detected local | 2026-07-14T03:03:14.9534709+01:00 |
| Evidence commit | d30f6450158ee40e9db6dff8b904ea159d4e1dc1 |
| Current classification | Blocked |

## Observed discrepancy

The general controlled-workspace validator reported a structural pass after six
generated DOCX files became present. However, the detailed evidence showed that:

- all six generated DOCX SHA-256 values differed from the controlled manifest;
- five canonical Markdown template hashes differed from the manifest;
- the detailed workbook revision-control validator returned FAIL;
- its current PR and merge-gate rule was no longer consistent with the merged
  workbook revision records.

## Interpretation

The structural workspace pass confirms file presence only. It does not override the
detailed hash and revision-control failures.

Therefore, UL-B01-R001 is Blocked rather than Ready.

## Scope boundary

No llama.cpp repository was cloned. No source was built. No IBM Granite model was
loaded. No CPU, GPU, memory, performance, quality or inference test was executed.

## Preserved evidence

- experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-B01/UL-B01-R001/workbook-hash-validation.csv
- experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-B01/UL-B01-R001/workbook-revision-control-validation.txt
- experiments/granite_turboquant_intel/logs/upstream-llama-cpp/UL-B01/UL-B01-R001/workspace-validation-attempt-002.txt
- Git commit d30f6450158ee40e9db6dff8b904ea159d4e1dc1

## Required resolution

1. Diagnose line-ending sensitivity in canonical template hashes.
2. diagnose Python, python-docx, lxml and generated-package reproducibility;
3. replace the stale revision-control validation rule with a rule based on the
   current controlled register;
4. regenerate all six workbooks;
5. obtain matching canonical and generated hashes;
6. obtain a detailed workbook revision-control PASS;
7. rerun the complete workspace validator;
8. reclassify the run only after both validators pass.

## Current-branch workbook cleanup

| Field | Value |
|---|---|
| Cleanup UTC | 2026-07-14T02:07:12.1280722Z |
| Cleanup local | 2026-07-14T03:07:12.1351067+01:00 |
| Preserved evidence commit | d30f6450158ee40e9db6dff8b904ea159d4e1dc1 |

The six generated DOCX files that failed detailed hash and revision-control
validation were removed from the current branch state. Their original bytes,
generation logs and validation results remain preserved in Git commit
d30f6450158ee40e9db6dff8b904ea159d4e1dc1.

This cleanup does not resolve FAIL-CTRL-001. The run remains Blocked.

## Stage 4 repair candidate

| Field | Value |
|---|---|
| Prepared UTC | 2026-07-14T03:30:23.8557639Z |
| Prepared local | 2026-07-14T04:30:23.8557639+01:00 |
| Base commit | 4bee757fb8ad40986df33df7e352526bfa4e7338 |
| Two-run DOCX comparison | All six byte-identical |
| Detailed workbook validator | PASS |
| Controlled workspace validator | PASS |
| Current classification | Blocked pending render and review |

The repair candidate appends six administrative workbook revisions, replaces
the stale template and DOCX baselines, pins the complete tested document
toolchain and replaces the hard-coded PR/merge gate with lifecycle-aware
validation.

This candidate does not close FAIL-CTRL-001 or make UL-B01-R001 Ready.
Every generated workbook must still be visually inspected and the complete
repair diff must be reviewed before commit.

## Stage 4 visual-inspection completion

| Field | Value |
|---|---|
| Recorded UTC | 2026-07-14T15:57:47.573928+00:00 |
| Workbooks inspected | 6 |
| Visual-layout result | PASS |
| Non-blocking observation | WB-03 title displays `TQ3 0`; body and identifiers use `TQ3_0` |
| Current classification | Blocked pending complete diff review and controlled commit |

All six generated workbooks were reviewed page by page. Text remained sharp,
tables stayed inside the page margins, revision histories were readable and no
clipping, overlap, malformed page or unexplained blank page was found.

This visual result does not close FAIL-CTRL-001. The repair candidate still
requires complete diff review, a controlled commit and a successful push.
UL-B01-R001 remains Blocked, and no runtime, model, hardware, performance or
inference-quality result was produced by this document-control activity.


## Stage 4B semantic-review finding

| Field | Value |
|---|---|
| Recorded UTC | 2026-07-14T16:28:43.839881+00:00 |
| Review result | Correction required before commit |
| Affected workbooks | WB-01, WB-02 and WB-03 |
| Additional manifest defect | WB-02 Source_SHA256 was 62 characters |
| Validator gap | Source integrity and legacy output-hash metadata were not checked |
| Current classification | Blocked pending regeneration, validation and visual reinspection |

The final semantic review prevented a formally passing but internally
inconsistent workbook baseline from being committed. The correction is part of
the same unmerged administrative repair revision and does not alter any test
scope or execution result.


## Stage 4B trailing-whitespace recovery

| Field | Value |
|---|---|
| Recorded UTC | 2026-07-14T16:34:12.981780+00:00 |
| Detection | `git diff --check` |
| Affected files | Three canonical Markdown templates |
| Correction | Removed nine unnecessary Markdown hard-break suffixes |
| Generated DOCX files changed | No |
| Current classification | Blocked pending post-recovery validators and visual reinspection |

The Stage 4B semantic correction remained uncommitted. This recovery preserves
the generated workbooks and updates only the canonical template hashes required
by the controlled manifest.


## Final Stage 4B review completion

| Field | Value |
|---|---|
| Recorded UTC | 2026-07-14T17:08:52.852622+00:00 |
| Final semantic diff review | PASS |
| Changed files reviewed | 46 |
| Manifest hash verification | PASS |
| Git diff check | PASS |
| Final visual reinspection | PASS - 19 pages across WB-01 to WB-03 |
| Current classification | Blocked pending controlled commit, push, PR and merge |

The technical repair is complete and internally consistent. FAIL-CTRL-001
remains open only because the corrected baseline has not yet completed its
version-control lifecycle. No runtime or model test result is claimed.

## Stage 4E pull-request number synchronization

| Field | Value |
|---|---|
| Recorded UTC | 2026-07-14T17:28:12.927494+00:00 |
| Pull request | #21 |
| Pull-request state | Draft and open |
| Repair commit | `c8ef758be14d863e5fb49fd8e24d61821ace66ca` |
| Revision rows updated | 6 |
| Current classification | Blocked pending merge and post-merge lifecycle finalization |

The six current revision rows now identify the assigned pull-request number.
`Pending merge` remains intentionally unchanged until GitHub creates the final
merge commit.
