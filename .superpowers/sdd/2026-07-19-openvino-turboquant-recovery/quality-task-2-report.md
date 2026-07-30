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

## Review Fix Round

The review identified that a summary with the right outer fields could be
accepted without proving the five-role campaign that produced it. The adapter
now reuses the measurement controller's `_sequence_spec`, `_role_spec`, and
`_resumable_attempt` helpers for every pilot, warmup, and formal role. It then
recomputes the three formal samples with `measurement_sample()` and the entire
measured summary with `summarize_samples()`. The persisted summary must match
that recomputation exactly (after JSON normalization), and
`attempt-sequence.json` must exactly contain the returned receipts plus the
summary path and raw-file SHA-256. A skeletal or replaced summary therefore
cannot stand in for accepted attempt evidence.

Attempt-sequence JSON is parsed with the strict quality-artifact parser, so
duplicate keys cannot be collapsed by a permissive JSON decoder. Accepted
receipts now omit a null `controller_error` field; this keeps successful
sequence evidence compatible with that strict no-null parser. The sequence's
summary SHA-256 is calculated from the same summary bytes already parsed and
validated, avoiding a second read between validation and hash binding.

`build_quality_worker_spec()` now re-loads the campaign from its retained
paths before deriving a spec. Replaced or directly constructed
`AcceptedQualityCampaign` instances cannot substitute identity or prompt
fields. The sampler path must be an existing file. Finally, Task-1 generation
settings are exported as an immutable mapping and validated against a separate
literal tuple, so mutating a public mapping cannot weaken validation.

The review RED suite failed for each of those cases before the fix: skeletal
summary, replaced/directly constructed campaign identity, missing sampler, and
generation-settings mutation. The post-fix focused command was:

```text
python -m pytest scripts/testing/tests/test_official_openvino_quality_campaign.py \
  scripts/testing/tests/test_official_openvino_quality_worker.py -q
```

Result: 57 passed. These fixtures use the existing five-role sequence test
recorder; they do not import OpenVINO or run model inference.

The strict-sequence follow-up also added a duplicate-key regression. Its
focused campaign, worker, and sequence suite result was: 73 passed.

The final related suite (campaign adapter, worker, measurement sequence,
matrix binding, quality runner, and adjudication) result was: 106 passed.
`py_compile` for every changed Python file and `git diff --check` also exited
successfully.

## Review Fix Round 2

The accepted-attempt resume chain now treats receipts as strict evidence, not
advisory metadata. `_persisted_record()` rejects duplicate JSON object keys and
all non-finite numeric values (including named constants and numeric overflow).
Its explicit null policy permits `null` only as ordinary optional persisted
record data, preserving valid runtime/failed-attempt representations; an
accepted receipt remains a closed non-null schema.

Each accepted receipt must have exactly the published fields, omit
`controller_error`, use the exact integer attempt number encoded by its
directory, and bind `spec_path` and `runtime_record_path` to the exact relative
files in that directory. Duplicate-key receipts and receipts with a wrong
number, either substituted path, or an accepted controller error reject before
any measurement callback can run. A whitespace-only change to an otherwise
identical summary also rejects because the unchanged attempt sequence retains
the prior raw-byte SHA-256.

The RED suite exposed all eight malformed record/receipt cases. The final
related regression command (adapter, worker, sequence, matrix binding, runner,
and adjudication) completed with: 116 passed. All tests use synthetic runtime
records; no OpenVINO launch or inference occurred.
