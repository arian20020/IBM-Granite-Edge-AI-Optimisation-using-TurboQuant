# Governed OpenVINO Quality Adapter — Task 2 Report

## Scope

Task 2 adds a validation-only adapter in
`scripts/testing/official_openvino/quality_campaign.py`. It does not create a
process, import OpenVINO, load a model, or run inference.

The adapter exposes immutable `QualityCampaignInput` and
`AcceptedQualityCampaign`, recomputes the accepted measurement identity through
the existing `build_campaign_identity()`, validates the persisted campaign and
measurement summary, constructs the existing `build_worker_environment()`, and
derives the strict Task-1 seven-turn worker spec from the frozen prompt
contract. The accepted object retains the exact build, repository, Python,
library, sampler, prompt, rubric, campaign, and output paths required by the
later governed-launch task.

## RED

Before creating the adapter, the focused API test was run with the project
Python environment:

```text
python -m pytest -q -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_quality_campaign.py \
  -k module_exposes -v
```

It collected the new test and failed as intended with:

```text
ModuleNotFoundError: No module named
'scripts.testing.official_openvino.quality_campaign'
```

The isolated Python 3.13 executable did not provide `pytest`; the project
Python 3.11 environment supplied the RED/GREEN test runner. No inference was
run by either environment.

The added type-boundary tests also produced a second RED: the initial adapter
incorrectly accepted `sample_count=3.0`, `cleanup_process_count=False`,
`context_tokens=4096.0`, and infinite/NaN timeout values. The final adapter
requires exact integers for summary counts/state/context and a finite positive
numeric timeout.

## Validation Rules Implemented

- The raw `campaign-identity.json` must byte-match the canonical recomputation;
  semantic equality alone is not accepted.
- The summary must use schema
  `official-openvino-wb04-measurement-summary/v1`, be measured and accepted,
  contain exactly three unique measured sample sources, have zero cleanup
  survivors, and prove no fallback.
- Summary test ID, context, campaign-identity SHA-256, and runtime-config
  SHA-256 must match the recomputed identity exactly.
- Recomputing identity rechecks the matrix/spec/config, artifact manifest/model,
  build provenance, GenAI module/DLL, OpenVINO libraries, Python executable,
  runtime/controller sources, device/properties, and runtime configuration.
- Frozen prompts and rubric are verified through the existing quality-contract
  loaders. The worker spec contains only the Task-1 schema, bound model/device/
  properties, Task-1 fixed generation settings, and frozen P1–P6 prompts.
- Nested campaign identity, summary, environment, and prompt mappings are
  frozen before being exposed.

## GREEN Verification

```text
python -m pytest -q -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_quality_campaign.py -v
```

Result: 25 passed.

```text
python -m pytest -q -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_quality_worker.py \
  scripts/testing/tests/test_measure_official_openvino_sequence.py \
  scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py -v
```

Result: 51 passed.

```text
$env:PYTHONPYCACHEPREFIX=[System.IO.Path]::GetTempPath() + 'wb04-quality-task2-pyc'
python -m py_compile \
  scripts/testing/official_openvino/quality_campaign.py \
  scripts/testing/tests/test_official_openvino_quality_campaign.py
git diff --check
```

Result: both commands exited 0.

## Producer Contract Alignment

The separate reviewed fix `f7c4295` aligned the measured-summary producer
before Task 2 was finalized. Measured summaries now emit both
`schema: official-openvino-wb04-measurement-summary/v1` and the retained
`schema_version: 1`; terminal evidence envelopes remain intentionally
distinct. Task 2 therefore rejects the old schema-less measured shape, and no
runtime evidence was accepted or manufactured here.
