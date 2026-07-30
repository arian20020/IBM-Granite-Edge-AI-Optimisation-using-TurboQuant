# WB-04 Expected-Rejection Controller TDD Report

## Scope

- 13 property-validation probes: `OV-TQS-05..12`, `OV-TQ-18..20`,
  `OV-B11-QJL`, and `OV-B11-POLAR`.
- No model generation or hardware inference.
- No numeric performance, memory, KV, utilization, or quality results.

## Task 1 RED

Command:

```text
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_measurement.OfficialOpenVINORuntimeMeasurementTests.test_unsupported_runtime_algorithms_name_field_and_value -v
```

Result: exit 1, one expected failure. The observed generic message was
`runtime algorithm labels must use exact uppercase enums`; the test required
the exact key/value label.

## Task 1 GREEN

Commands:

```text
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_measurement.OfficialOpenVINORuntimeMeasurementTests.test_unsupported_runtime_algorithms_name_field_and_value -v
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_measurement -v
```

Result: exit 0. Focused test 1/1 passed; measurement module 23/23 passed.

## Task 2 RED

Command:

```text
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_expected_rejections -v
```

Result: exit 1 with the expected
`ModuleNotFoundError: scripts.testing.official_openvino.expected_rejections`.

## Task 2 GREEN

Command:

```text
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_generates_exact_fail_closed_probe_set_without_metrics scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_b11_probes_preserve_one_exact_case_and_contract scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_generation_and_both_hash_layers_are_deterministic scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_validator_rejects_missing_duplicate_tampered_or_non_rejection_data scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_atomic_writer_uses_canonical_byte_identical_json -v
```

Result: exit 0, 5/5 passed.

## Task 3 RED

Command:

```text
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_cli_writes_valid_byte_identical_evidence -v
```

Result: exit 1 with the expected missing CLI file error.

## Task 3 GREEN

Commands:

```text
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_cli_writes_valid_byte_identical_evidence -v
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_expected_rejections scripts.testing.tests.test_official_openvino_matrix scripts.testing.tests.test_official_openvino_measurement -v
```

Result: exit 0. CLI integration 1/1 passed; related suite 40/40 passed.

## Broader Test Environment Check

The Python 3.13 OpenVINO runtime environment discovered 174 tests. Every test
that imported ran successfully, but discovery ended with six import errors
because that environment does not contain `pytest` or `python-docx`.

The six modules were rerun with the project Python 3.11 environment:

- DOCX audit: 2/2 passed.
- Pytest modules: 79 passed, 1 failed in the concurrently developed
  campaign-matrix-binding test
  `test_campaign_binding_consumes_the_frozen_explicit_contract_not_a_rederivation`.

That failure is outside this controller's frozen scope. No campaign-spec or
campaign-binding file was edited here.

## Final Evidence and Verification

After the frozen-matrix commits
`a3ebc02d872e0ffdc0d6cefc7291dc181a787d3a` and
`a557ec0`, the Python 3.13 CLI generated and validated:

`experiments/raw-results/openvino-turboquant/2026-07-30/expected-rejections/expected-rejections.json`

- schema: `official-openvino-wb04-expected-rejection-evidence/v1`
- probe count: 13
- matrix SHA-256: `ed34373405748aa186da34314299e24204711db1baea8e3f3c86d1a09688792a`
- controller SHA-256: `de957a2d9624f9cd4ca1bd07c4beebb4eb1d8c7cf3029178ee4ba65ebef8696e`
- aggregate SHA-256: `75e598439aef99ebb9369937f3f84eb72879434b7b2c9504c5a6ffa248a841ec`
- file SHA-256: `d4031ba191cd01df5c5d24e6c95dcd035f3c8a4deff9a875269326efea6638b7`
- file size: 26,508 bytes

The final validator accepted all 13 probes with these exact matrix, controller,
and aggregate hashes. Deterministic bytes were verified using distinct new
destinations. A real second publication to the dated path exited 1 with
`FileExistsError`; the file SHA-256 remained
`d4031ba191cd01df5c5d24e6c95dcd035f3c8a4deff9a875269326efea6638b7`,
its size remained 26,508 bytes, and no temporary file remained. The latest
focused publication tests passed 2/2 and the complete controller suite passed
6/6. No model generation or hardware inference ran.

## Fix Round 1: Create-Only Publication

Independent review found that the first writer accepted arbitrary mappings and
used `os.replace`, allowing an existing evidence file to be overwritten.

### RED

Command:

```text
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_writer_validates_and_publishes_create_only_canonical_json scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_cli_rejects_overwrite_and_is_deterministic_across_new_destinations -v
```

Result: exit 1, 2/2 failed for the intended reasons. The writer did not reject
an added uncontrolled field, and a second CLI publication to the same
destination returned success.

### GREEN

Commands:

```text
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_writer_validates_and_publishes_create_only_canonical_json scripts.testing.tests.test_official_openvino_expected_rejections.OfficialOpenVINOExpectedRejectionTests.test_cli_rejects_overwrite_and_is_deterministic_across_new_destinations -v
.\.venv-official-openvino-turboquant-py313\Scripts\python.exe -m unittest scripts.testing.tests.test_official_openvino_expected_rejections -v
```

Result: exit 0. Focused publication tests 2/2 passed; complete controller suite
6/6 passed. Publication now validates the exact payload against its bound
matrix before touching the destination, writes canonical bytes to a same-folder
temporary file, and uses an atomic hard-link create so an existing destination
cannot be replaced. Both direct-writer and CLI tests verify the rejected second
publication leaves the original bytes unchanged; deterministic equality is
verified across distinct new destinations.

After coordinated matrix remediation completed, the dated evidence was
regenerated once with the final controller and validated as described above.
