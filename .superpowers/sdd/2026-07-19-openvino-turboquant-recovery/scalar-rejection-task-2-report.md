# Scalar semantic rejection correction — Task 2 report

## Scope

Implemented only the create-only scalar semantic rejection evidence controller,
its CLI, and focused tests. No matrix, property controller, runtime, quality,
workbook/register, retained raw evidence, or dated aggregate was changed.

## TDD evidence

- RED: 2 focused test runs. Both failed solely because
  `scripts.testing.official_openvino.scalar_semantic_rejections` did not yet
  exist (`ModuleNotFoundError`); each ran 0 tests because import failed.
- GREEN: 2 focused test runs, 6/6 each.
- Mutation coverage exercises diagnostic identity; supplied-spec command path;
  stdout hash; strict JSON BOM/duplicate keys; governed validity and cleanup;
  command injection; worker identity; observed concrete precision; allocation;
  payload schema/hash/type-smuggling; and create-only publication.

## Commands and results

```text
Python313 -m unittest scripts.testing.tests.test_official_openvino_scalar_semantic_rejections -v
  RED ×2 before the controller existed; GREEN 6/6 ×2 after implementation.

Python313 -m unittest -v \
  scripts.testing.tests.test_official_openvino_matrix \
  scripts.testing.tests.test_official_openvino_expected_rejections \
  scripts.testing.tests.test_official_openvino_measurement \
  scripts.testing.tests.test_official_openvino_metrics
  62/62 passed.

Python313 -m unittest -v \
  scripts.testing.tests.test_official_openvino_campaign_spec \
  scripts.testing.tests.test_official_openvino_campaign_matrix_binding \
  scripts.testing.tests.test_measure_official_openvino_sequence
  Blocked at import: pytest is not installed in either the dispatched Python
  3.13 interpreter or .venv-official-openvino-turboquant-py313.

Python311 -m pytest -q \
  scripts/testing/tests/test_official_openvino_scalar_semantic_rejections.py \
  scripts/testing/tests/test_official_openvino_campaign_spec.py \
  scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py \
  scripts/testing/tests/test_official_openvino_matrix.py \
  scripts/testing/tests/test_official_openvino_measurement.py \
  scripts/testing/tests/test_measure_official_openvino_sequence.py
  Supplemental verification executed by the root agent after implementation:
  scalar focused suite 6/6; adjacent selected suites 63/63 in 2.52 seconds.

Python313 -m py_compile (explicit temporary cfiles)
  passed for controller, CLI, and focused tests.

Python313 scripts/testing/generate_official_openvino_scalar_rejections.py --help
Python313 scripts/testing/generate_official_openvino_expected_rejections.py --help
git diff --check -- <four scoped files>
  passed.
```

## Commit

Implementation commit: `400460d12fb7d0cec1dc81f3bf9f5bbe23e852eb`
(`feat(openvino): add scalar semantic rejection evidence`). This report is
committed separately so it can record that exact immutable implementation ID.

## Concerns

Python 3.13 lacks pytest locally, but the required pytest suites are not left
unexecuted: the root agent verified them under Python 3.11 as recorded above.
The dated aggregate is intentionally not published: it must be generated after
independent review once the controller hash is final.

## Hardening review round 1

Independent review found no Critical issues and five Important issues:

- BOM-less UTF-16/UTF-32 bytes could be accepted because strict decoding was
  checked but the original bytes were passed to `json.loads`.
- an arbitrary existing file named `python.exe` could satisfy executable
  validation;
- matrix hash and case semantics came from separate source reads;
- descriptions, the exact formal metric set, and
  `requires_actual_cache_precision_proof = false` were not pinned;
- several mutations failed at captured-stream equality before reaching the
  intended allocation or hash reconciliation boundary.

All five findings were addressed. JSON is parsed only from decoded UTF-8 text;
the complete command is bound to the exact Python 3.13 executable configured by
`.venv-official-openvino-turboquant-py313/pyvenv.cfg`; the matrix is opened
once and validated from an fsynced temporary snapshot of those captured bytes;
all four row descriptions, exact formal metrics, and the complete rejection
contract are frozen; and branch-specific mutations reach allocation plus
stdout, stderr, worker-output, and telemetry hash checks.

## Hardening TDD evidence

- Round 1 RED:
  - 10 focused tests, 4 expected failures;
  - after completing adversarial coverage, 13 focused tests, 4 expected
    failures.
- Round 1 GREEN: 13/13 focused tests.
- Round 2 RED:
  - 16 focused tests, 6 failures, one of which exposed an incorrect hand-written
    expected metric list in the test;
  - after correcting that test literal, 16 focused tests with the 5 intended
    production failures.
- Round 2 GREEN: 16/16 focused tests, repeated after the final same-byte
  refactor.

The new tests cover exact U8/U4 per-ID mapping, all Task-1 scalar semantics,
owned aggregate/probe/nested types, bool/integer smuggling, canonical hash
boundaries, BOM-less UTF-16, same-name fake Python substitution, one-read
matrix/spec/attempt/stdout/stderr snapshots, governed safety and Job Object
proofs, worker schema/model/output, requested/reported activation precision,
concrete state, byte allocation, and each stream/output/telemetry hash.

## Hardening verification and re-review

```text
Python313 -m unittest \
  scripts.testing.tests.test_official_openvino_scalar_semantic_rejections -v
  16/16 passed.

Python311 -m pytest -q \
  scripts/testing/tests/test_official_openvino_scalar_semantic_rejections.py \
  scripts/testing/tests/test_official_openvino_matrix.py \
  scripts/testing/tests/test_official_openvino_expected_rejections.py \
  scripts/testing/tests/test_official_openvino_measurement.py \
  scripts/testing/tests/test_official_openvino_metrics.py \
  scripts/testing/tests/test_official_openvino_campaign_spec.py \
  scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py \
  scripts/testing/tests/test_measure_official_openvino_sequence.py
  130/130 passed; one non-test PytestCacheWarning because the shared worktree
  cache directory is not writable.

Python313 -m py_compile (explicit temporary cfiles)
  passed for controller, CLI, and focused test file.

Python313 scripts/testing/generate_official_openvino_scalar_rejections.py --help
Python313 scripts/testing/generate_official_openvino_expected_rejections.py --help
  both passed.
```

The first independent review returned five Important findings and no Critical
findings. After the fixes, root's independent read-only re-review returned
all findings addressed, `SPEC PASS`, `APPROVED`, and no remaining findings.
No dated evidence was generated or published.
