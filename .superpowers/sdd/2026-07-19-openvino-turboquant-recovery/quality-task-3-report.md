# Governed OpenVINO Quality Adapter — Task 3 Report

## Scope

Task 3 adds the governed, single-worker launch boundary only. It does not
publish captures, resume a campaign, adjudicate responses, add CLI dispatch,
modify a workbook, or run OpenVINO inference.

## RED

The new guard-environment tests initially failed because
`run_guarded_command()` had no `environment` parameter. The governed-worker
tests then failed because `GovernedQualityWorkerResult` and
`run_governed_quality_worker()` did not exist. These failures were observed
before production code was added.

## Implementation

- `run_guarded_command()` accepts an optional validated environment mapping.
  Supplied mappings are copied, reject blank/non-string keys or values and
  case-insensitive duplicate Windows keys, receive the required
  `MSBUILDDISABLENODEREUSE=1` value, and are SHA-256 bound through compact,
  sorted UTF-8 JSON evidence. Existing ambient-environment callers retain
  their behavior.
- Guard evidence records the exact environment hash and the integer
  `cleanup_process_count` derived from the Job Object survivor proof. The
  existing PowerShell evidence verifier was extended for those two fields so
  existing guarded callers remain compatible.
- `run_governed_quality_worker()` revalidates the accepted campaign, requires a
  fresh output directory, writes one canonical worker spec, and invokes only
  the accepted Python module command behind `GuardLimits` with an exact 2 GiB
  floor and the requested timeout.
- The adapter validates the persisted guard evidence and raw worker result:
  exact command/cwd/log/evidence/environment bindings, zero-survivor cleanup,
  no timeout/low-memory/emergency action, zero exit, seven ordered outcomes,
  and canonical worker-result hash. Failed raw turns remain retained facts and
  are never scored or converted to a pass.

## GREEN Verification

```text
python -m pytest scripts/testing/tests/test_official_openvino_quality_campaign.py \
  -k governed -q
```

Result: 10 passed.

```text
python -m pytest scripts/testing/tests/test_official_openvino_guarded_build.py \
  -k 'supplied_environment or invalid_environment or generic_guard' -q
```

Result: 6 passed.

```text
python -m pytest -q -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_guarded_build.py
```

Result: 55 passed.

```text
python -m pytest -q -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_quality_worker.py \
  scripts/testing/tests/test_official_openvino_quality_campaign.py \
  scripts/testing/tests/test_measure_official_openvino_sequence.py \
  scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py
```

Result: 101 passed.

All execution used synthetic guards/records or small Python child processes;
no model was imported and no inference was run.
