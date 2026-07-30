# WB-04 Task-9 Matrix Freeze Report

Status: DONE

## Scope

- Added exact top-level source and build receipt references.
- Added explicit, validated execution-contract fields to all 60 controlled rows.
- Bound campaign worker/runtime validation to those frozen fields instead of
  independently deriving runtime algorithm, norm, or attention values.
- Updated generated expected-rejection and synthetic campaign fixtures.

## TDD evidence

RED commands and outcomes:

```powershell
python -m unittest scripts.testing.tests.test_official_openvino_matrix.OfficialOpenVINOMatrixTests.test_matrix_freezes_receipt_references_and_every_execution_contract_field scripts.testing.tests.test_official_openvino_matrix.OfficialOpenVINOMatrixTests.test_matrix_rejects_missing_or_drifting_frozen_execution_contract_fields -v
python -m pytest -q scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py
```

The matrix tests failed because `source_identity` and explicit contract fields
were absent; the campaign-binding test failed because tampered explicit fields
were ignored. A later broader sequence run failed because its synthetic matrix
fixture still emitted the old schema. The fixture was updated rather than
weakening the production fail-closed path.

GREEN verification:

```powershell
python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_measure_official_openvino_sequence.py scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py scripts/testing/tests/test_official_openvino_campaign_spec.py
# 31 passed in 1.33s

python -m unittest scripts.testing.tests.test_official_openvino_matrix scripts.testing.tests.test_official_openvino_measurement -v
# 49 tests passed
```

`git diff --check` completed with exit code 0.

## Concerns

No inference, model execution, or hardware launch was run. Existing unrelated
uncommitted changes in `runtime_measurement.py` and
`test_official_openvino_measurement.py` were deliberately not staged.

## Matrix Task fix round 1/5

RED verification:

```powershell
python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py
# 5 failed, 4 passed
```

The failures proved that worker prelaunch accepted missing/tampered
`attention_path` and `suitable_host_required`, did not stop a suitable-host
row, and derived scalar proof from mutable legacy `k_algorithm`/`v_algorithm`
labels instead of the frozen route.

GREEN verification:

```powershell
python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py
# 9 passed in 0.05s

python -m pytest -q -p no:cacheprovider scripts/testing/tests/test_measure_official_openvino_sequence.py scripts/testing/tests/test_official_openvino_campaign_matrix_binding.py scripts/testing/tests/test_official_openvino_campaign_spec.py
# 35 passed in 1.28s

python -m unittest scripts.testing.tests.test_official_openvino_matrix scripts.testing.tests.test_official_openvino_measurement -v
# Ran 36 tests in 0.060s; OK
```

The worker now reads all eleven frozen fields before launch, validates
attention against the frozen route/outcome, rejects a suitable-host row from
the local worker path, and keys scalar precision proof only to
`execution_route == "upstream-scalar"`.
