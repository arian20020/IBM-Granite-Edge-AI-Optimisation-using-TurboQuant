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

## Review Fix Round 1/5 — Findings

CRITICAL 1 — quality_campaign.py:420, :519-530: adapter validates hash of
accepted environment before guard’s required MSBUILDDISABLENODEREUSE=1
augmentation. Real guard hashes effective mapping after adding key
(guarded_build.py:137-149), so normal accepted env lacking key always rejects;
fake masks by hashing unaugmented mapping.

CRITICAL 2 — quality_campaign.py:455-476: worker-result validation does not
enforce Task-1 outcome schema. Seven objects with only expected turn_ids plus
recomputed aggregate hash are accepted; required raw prompt/output/failure
fields and hashes/status relationships not checked. It also accepts
noncanonical result bytes (BOM/whitespace) despite exact-bytes requirement.

CRITICAL 3 — quality_campaign.py:507-509, :534-546: spec SHA is never
calculated/bound, and neither spec nor log bytes are read/validated after
launch. Guard/invoker may delete/alter spec or log and still be accepted;
violates fresh/tampered/missing contract and exact path/evidence binding.

IMPORTANT 1 — quality_campaign.py:440-452: timeout validation permits
maximum_runtime_seconds=None instead of exact requested timeout. Required
primitive types use equality rather than exact types;
cleanup_process_count=False/exit_code=False satisfy ==0 and int 0 satisfies
false guard flags.

IMPORTANT 2 — tests missing for pre-existing/missing/tampered
spec/result/log/guard, reordered/malformed/hash-tampered result records,
runtime timeout evidence, canonical bytes, type-smuggling; fake guard
environment behavior hides defect.

MINOR — redundant target existence checks after output-root absence gate (may
clean up if natural).

## Review Fix Round 1/5 — RED

The real-guard environment fake was changed to use the guard's effective
environment semantics. Before the production fix, the focused test failed
with `guard environment-sha256 is invalid`:

```text
python -m pytest -q -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_quality_campaign.py \
  -k effective_environment
```

Result: 1 failed, 54 deselected.

After fixing that first dependency, the focused schema, canonical-byte,
artifact-integrity, and exact-type tests reproduced the remaining findings:

```text
python -m pytest -q -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_quality_campaign.py \
  -k "skeletal or noncanonical or missing_or_tampered or type_smuggling or preexisting"
```

Result: 13 failed, 2 passed, 40 deselected. The failures proved that skeletal
outcomes and noncanonical bytes were accepted; altered or missing spec/log
artifacts were accepted; missing guard/result artifacts leaked
`FileNotFoundError`; and absent/type-smuggled guard fields were accepted.

The additional malformed-result cycle observed two intended failures for
hash-consistent per-outcome hash and status/field relationship corruption;
the already-present order and aggregate-hash checks passed.

## Review Fix Round 1/5 — Implementation

- Guard-environment validation now shares `_effective_environment()` with the
  real guard, so the required MSBuild override is included exactly once with
  the guard's case-insensitive key semantics. Ambient blank entries are
  excluded when constructing the accepted worker environment because the
  guard contract rejects blank values before launch.
- Worker results must be byte-for-byte canonical Task-1 JSON. Every outcome
  has the exact field set, ordered turn ID, raw prompt/hash, exact status,
  raw output/hash, and status-dependent failure relationships before the
  aggregate hash is accepted.
- The canonical worker spec is SHA-256 bound before launch and its bytes are
  re-read afterward. The log, guard evidence, and worker result are also
  re-read with explicit missing-artifact errors. Log bytes are bound to
  `log_sha256`; guard bytes must be canonical and exactly match the returned
  guard record.
- Guard validation now requires exact primitive/container types, the exact
  requested floating-point timeout, exact 2 GiB integer floor, false stop
  flags, empty emergency actions, zero exit/cleanup counts, and the full Job
  Object zero-survivor proof.
- Tests now cover all four pre-existing targets; missing and tampered spec,
  result, log, and guard files; reordered, skeletal, malformed,
  aggregate-hash-tampered, and noncanonical worker results; timeout evidence;
  and bool/int/float type smuggling.
- The redundant post-creation target-existence gate was removed.

## Review Fix Round 1/5 — GREEN

```text
python -m pytest -q -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_quality_campaign.py
```

Result: 73 passed.

```text
python -m pytest -q -p no:cacheprovider \
  scripts/testing/tests/test_official_openvino_guarded_build.py \
  scripts/testing/tests/test_official_openvino_quality_worker.py \
  scripts/testing/tests/test_official_openvino_quality_campaign.py \
  scripts/testing/tests/test_measure_official_openvino_sequence.py \
  scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py
```

Result: 189 passed.

`py_compile` completed with exit code 0 for the modified production and test
modules using explicit files in a temporary output directory. `git diff
--check` also completed with exit code 0; its only output was existing
LF-to-CRLF conversion warnings.

Self-review found no capture publication/resume, adjudication, CLI dispatch,
inference, workbook, or GPU changes in this fix. All tests remained synthetic
or used the existing small guarded Python-child coverage; no model was loaded.
