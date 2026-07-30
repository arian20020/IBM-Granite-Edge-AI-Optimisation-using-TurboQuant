# Governed OpenVINO Quality Adapter — Task 4 Report

## Scope

Task 4 connects one already accepted measurement campaign and the reviewed
Task-3 governed worker to immutable P1–P6 capture evidence. It does not add
adjudication, CLI dispatch, model inference, formal evidence, workbook edits,
or changes to the raw worker and process-guard contracts.

The public interface is:

```python
capture_governed_quality_campaign(
    input: QualityCampaignInput, *, resume: bool
) -> dict[str, Any]
```

## RED

The first focused run was made after adding the Task-4 synthetic tests and
before production code:

```text
python -m pytest -p no:cacheprovider \
  scripts/testing/tests/test_capture_official_openvino_quality.py \
  scripts/testing/tests/test_official_openvino_quality_campaign.py \
  -k "governed_capture or governed_settings_projection" -q
```

Result: 28 failed and 86 deselected. The failures were the intended missing
`_map_governed_generation_settings` and
`capture_governed_quality_campaign` interfaces.

Self-review found two additional failure paths and gave each its own RED/GREEN
cycle:

- A governed P6 record with both turns failed was rejected by the legacy
  single-failure validator. The focused test failed with
  `failed quality response must preserve one failed turn`.
- A worker log altered after the Task-3 runner returned could be published
  because the controller trusted the returned dataclass. The focused test
  failed with `DID NOT RAISE RuntimeError`.

Both defects were reproduced before their production fixes.

## Implementation

- The exact worker settings
  `max_new_tokens=256`, `do_sample=False`, `rng_seed=42`, and
  `apply_chat_template=False` are type-checked before deterministic projection
  to the existing capture vocabulary. The accepted legacy settings are not
  used as independent proof; every record is also bound to the exact canonical
  worker-spec hash.
- Seven validated worker outcomes are projected into exactly six P1–P6
  records. Raw prompt and completed-output text and hashes are copied exactly.
  Failed outcomes remain `failed`, retain exact failure type/message evidence,
  and cannot produce a complete record. P6 retains both turns, including
  multiple failed turns.
- P6 turn two is checked against the frozen transcript constructed from the
  actual unmodified P6 turn-one output. The saved `turn_1`,
  `turn_prompts[1].raw_prompt`, and final `raw_prompt` are cross-bound to the
  worker outcome.
- Every governed record and the capture summary includes the exact
  `quality_worker_spec_sha256`, `worker_result_sha256`, and
  `guard_evidence_sha256`, in addition to the accepted campaign/runtime,
  prompt, generation-settings, and rubric identities.
- `GovernedQualityWorkerResult` was extended only with the worker-spec and
  worker-log hashes needed for the receipt. After a fresh launch, all four
  governed artifacts are loaded again through the Task-3 strict validators
  before any capture artifact is published.
- `governed-execution.json` binds the canonical spec, result, log, and guard
  paths/hashes plus valid, no-timeout, no-low-memory, zero-exit, and
  zero-survivor facts. It has its own canonical content hash.
- Governed execution, receipt, response, and summary state is create-only.
  Governed capture JSON uses compact sorted UTF-8 canonical bytes with one
  trailing newline and atomic create-only publication.
- Resume never calls the launch function. It first requires the exact expected
  tree, then revalidates accepted campaign identity and every persisted
  spec/result/log/guard/receipt/record/summary byte and hash. Missing,
  noncanonical, duplicate-key, altered, partial, and unexpected state closes
  the operation without repair or relaunch.
- The legacy callback-based `capture_quality_responses` path retains its
  existing behavior and schema.

## GREEN Verification

Focused Task-4 tests:

```text
python -m pytest -p no:cacheprovider \
  scripts/testing/tests/test_capture_official_openvino_quality.py \
  scripts/testing/tests/test_official_openvino_quality_campaign.py \
  -k "governed_capture or governed_settings_projection" -q
```

Result: 30 passed and 86 deselected.

Complete capture and campaign suites:

```text
python -m pytest -p no:cacheprovider \
  scripts/testing/tests/test_capture_official_openvino_quality.py \
  scripts/testing/tests/test_official_openvino_quality_campaign.py -q
```

Result: 116 passed.

Required related boundary:

```text
python -m pytest -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_quality_worker.py \
  scripts/testing/tests/test_official_openvino_quality_campaign.py \
  scripts/testing/tests/test_capture_official_openvino_quality.py \
  scripts/testing/tests/test_official_openvino_guarded_build.py \
  scripts/testing/tests/test_measure_official_openvino_sequence.py \
  scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py -q
```

Result: 232 passed.

The four modified Python files compiled successfully with `py_compile` into
explicit temporary bytecode files. `git diff --check` exited 0. The project
Python 3.11 installation supplied pytest because the isolated Python 3.13
environment does not contain pytest; compilation is also repeated under
Python 3.13 before commit.

## Self-review

- Every Task-4 brief requirement is represented by a behavior test: one launch,
  six records, exact new hashes, actual P6 history, raw failures, no-launch
  resume, every governed/capture artifact tampered and missing, four changed
  identity boundaries, pre-existing output, partial state, and unexpected
  state.
- Mutation review confirmed that removing any receipt artifact hash or safety
  fact, changing a record evidence hash, normalizing P6 history, converting a
  failed turn to complete, relaunching on resume, accepting a changed identity,
  or trusting post-launch artifacts breaks at least one focused test.
- Tests use only synthetic accepted attempts, worker results, guards, and small
  filesystem artifacts. No OpenVINO import, pipeline construction, model load,
  inference, formal-evidence publication, or workbook operation occurred.
- Only the four Task-4 source/test files and this report are in task scope.
  Scalar-semantic-rejection work, progress-ledger edits, and retained historical
  evidence remain untouched and excluded from staging.

## Review Fix Round 1/5 - Findings

Independent review of commit `3e9e61c` identified three evidence-integrity
defects and one coverage gap:

- governed failed worker outcomes were converted from exact JSON null output
  fields into invented empty strings and hashes;
- receipt and summary resume comparison used Python value equality without
  independent exact-key, exact-type, or self-hash validation, so values such
  as `False` and `0`, or `6.0` and `6`, could compare equal;
- resume read evidence sequentially without a stable snapshot or final reread,
  accepted hardlink aliases, and fresh publication did not atomically establish
  one operation owner before launching;
- adversarial tests did not cover null failure preservation, P6 failed-history
  substitution, type smuggling, recomputed self-hashes, aliases, replacement
  during validation, or concurrent publication.

## Review Fix Round 1/5 - RED

The new adversarial subset initially reported 14 failed and 12 passed. Four
failures were deliberately accepted rejections whose exception messages did
not match an overly narrow test regex; the other ten reproduced the production
defects: three invented-output failures, four stale self-hash type-smuggling
acceptances, one accepted hardlink, one accepted same-byte replacement, and
both concurrent callers entering the worker boundary.

## Review Fix Round 1/5 - Implementation

- Governed records now retain `raw_output=None` and
  `raw_output_sha256=None` exactly for failed turns. Nullable fields are
  permitted only at the governed failed-output positions, including P6
  `turn_1`; all other record fields remain strictly typed and hash checked.
- The P6 turn-two prompt is still independently verified against the documented
  empty-history substitution when turn one failed. The persisted turn-one
  evidence remains null rather than conflating that substitution with model
  output.
- Receipt and summary validators now require exact fields and JSON scalar
  types, recompute their self-hashes, and compare canonical expected bytes.
  Both stale-hash and attacker-recomputed-hash bool/int or int/float changes
  fail closed.
- A fresh capture atomically creates its output root before the worker can
  launch, so only one caller owns publication. Failed operations retain their
  create-only partial state.
- Governed and complete capture trees are validated from immutable byte and
  file-identity snapshots. Symlinks, reparse points, non-regular paths,
  multi-link files, and duplicate file identities are rejected. Validation
  consumes only snapshot bytes, then re-snapshots the exact tree and requires
  every identity and byte to remain unchanged before returning success.
- The caller's absolute lexical output-root path is retained instead of
  resolving away an alias. Every existing ancestry component is checked with
  `lstat`, and a top-level symlink, Windows junction, or other reparse alias is
  rejected before resume or launch.
- Atomic fresh-root creation records the outer directory's stable device/file
  identity immediately. That same lexical identity is checked before and after
  worker validation, at every create-only publication boundary, and after the
  final tree validation, so replacing the claimed outer root cannot inherit an
  accepted capture by moving the same governed child beneath it.
- The legacy callback capture schema and behavior remain unchanged.

## Review Fix Round 1/5 - GREEN

The final adversarial additions include real nullable failure/resume evidence,
P6 failed-turn empty-history binding, stale and recomputed self-hash
type-smuggling, hash-consistent unexpected fields, invalid self-hashes,
hardlinks, replacement during both resume and fresh validation, concurrent
fresh callers, a real Windows output-root junction, and outer-root identity
replacement.

The two final path/ownership regression tests passed:

```text
2 passed
```

The complete capture and campaign boundary passed:

```text
133 passed in 67.38s
```

The six-file related boundary passed:

```text
249 passed in 211.56s
```

Both Python 3.11 and Python 3.13 compiled the four scoped Python files
successfully using isolated temporary bytecode roots. `git diff --check`
exited 0.

## Review Fix Round 1/5 - Independent Re-review

The first re-review correctly retained two Important findings: resolving the
top-level output root erased alias evidence, and atomic creation did not retain
the outer directory identity through publication. Both received dedicated
failing tests before implementation.

The second independent re-review returned **APPROVED** with no remaining
scoped findings:

- top-level symlink/junction ancestry rejects before launch;
- fresh publication remains bound to the originally claimed outer-root
  identity;
- the two new adversarial cases passed;
- the full capture/campaign suite passed 133/133;
- scoped diff checking passed;
- specification and task quality both passed.
