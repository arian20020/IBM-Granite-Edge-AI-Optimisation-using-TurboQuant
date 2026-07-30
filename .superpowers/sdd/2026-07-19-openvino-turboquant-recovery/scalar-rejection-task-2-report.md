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
