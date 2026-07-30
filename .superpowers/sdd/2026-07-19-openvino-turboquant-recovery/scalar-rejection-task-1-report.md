# Scalar Semantic Rejection Correction — Task 1 Report

## Status

Task 1 corrects declarations and controller selection only. It does not run
OpenVINO, launch inference, create semantic evidence, alter the workbook or
register, or weaken concrete cache-state validation.

The frozen semantic rejection set is exactly `OV-04`, `OV-05`, `OV-TQ-01`, and
`OV-TQ-02`. Each row declares `STANDARD` runtime key/value algorithms,
`expected-rejection`, no produced attention path, no numeric generation
metrics, and no suitable-host requirement. The 8B scalar rows remain unchanged.

The property controller still constructs exactly 13 probes: the prior 11
property rejection IDs plus the two OV-B11 probes. It checks that the complete
matrix rejection set is the 11 property IDs plus the four semantic IDs, and
refuses to build a property probe for any semantic ID.

## RED Evidence

Focused tests were added before the declaration/selection changes.

```text
python -m pytest scripts/testing/tests/test_official_openvino_matrix.py \
  scripts/testing/tests/test_official_openvino_campaign_spec.py -q
```

The matrix test first failed at collection because
`SEMANTIC_SCALAR_REJECTION_IDS` did not exist. The generator assertions then
demonstrated the previous pass selection: default U8 produced 20 specs instead
of 19 and distinct-U4 binding produced 21 specs instead of 19 (four failing
assertions; 11 passing tests).

## GREEN Verification

```text
python -m pytest scripts/testing/tests/test_official_openvino_matrix.py \
  scripts/testing/tests/test_official_openvino_expected_rejections.py \
  scripts/testing/tests/test_official_openvino_campaign_spec.py -q
```

Result: 35 passed.

```text
python -m pytest scripts/testing/tests/test_official_openvino_matrix.py \
  scripts/testing/tests/test_official_openvino_expected_rejections.py \
  scripts/testing/tests/test_official_openvino_campaign_spec.py \
  scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py \
  scripts/testing/tests/test_measure_official_openvino_sequence.py \
  scripts/testing/tests/test_measure_official_openvino_cli.py \
  scripts/testing/tests/test_official_openvino_measurement.py \
  scripts/testing/tests/test_official_openvino_metrics.py -q
```

Result: 112 passed. Pytest emitted one pre-existing cache-directory permission
warning; it did not affect test execution.

```text
$env:PYTHONPYCACHEPREFIX = Join-Path $env:TEMP 'scalar-rejection-task-1-pycache'
& 'C:\Users\Student\AppData\Local\Programs\Python\Python313\python.exe' \
  -m py_compile scripts/testing/official_openvino/matrix.py \
  scripts/testing/official_openvino/expected_rejections.py \
  scripts/testing/official_openvino/campaign_spec.py
git diff --check
```

Result: both commands exited 0.

## Changed Files

- `experiments/manifests/official-openvino/retest-matrix.json`
- `scripts/testing/official_openvino/matrix.py`
- `scripts/testing/official_openvino/expected_rejections.py`
- `scripts/testing/tests/test_official_openvino_matrix.py`
- `scripts/testing/tests/test_official_openvino_expected_rejections.py`
- `scripts/testing/tests/test_official_openvino_campaign_spec.py`
- `.superpowers/sdd/2026-07-19-openvino-turboquant-recovery/scalar-rejection-task-1-report.md`

## Commit and Concerns

Commit: the Task 1 `scalar semantic rejection correction` commit that contains
this report; its immutable SHA is supplied in the task handoff.

Concern: the working tree contains concurrent unrelated quality/progress edits
and untracked diagnostic artifacts. They are intentionally excluded from this
Task 1 commit.
