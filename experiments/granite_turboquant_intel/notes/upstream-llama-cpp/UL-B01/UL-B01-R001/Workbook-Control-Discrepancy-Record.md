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
