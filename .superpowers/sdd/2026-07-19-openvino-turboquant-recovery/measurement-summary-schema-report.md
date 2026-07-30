# Measurement-summary schema producer fix

## Scope

Added the governed-quality measurement-summary schema to measured summaries
produced by `scripts/testing/official_openvino/metrics.py`:

`official-openvino-wb04-measurement-summary/v1`

The established `schema_version: 1` field is retained for existing consumers.
Terminal measurement summaries remain the intentionally distinct
status/evidence envelope; they do not claim the measured-summary schema.

## Strict TDD evidence

1. Added producer and persisted-artifact assertions before changing production
   code, including explicit terminal-envelope boundary assertions.
2. RED: `python -m pytest -q -p no:cacheprovider
   scripts/testing/tests/test_official_openvino_metrics.py` produced `1 failed,
   9 passed`; the expected failure was `KeyError: 'schema'`.
3. Added `MEASUREMENT_SUMMARY_SCHEMA` only to the measured branch of
   `summarize_samples`.
4. GREEN: `python -m pytest -q -p no:cacheprovider
   scripts/testing/tests/test_official_openvino_metrics.py
   scripts/testing/tests/test_official_openvino_measurement.py
   scripts/testing/tests/test_measure_official_openvino_sequence.py
   scripts/testing/tests/test_measure_official_openvino_cli.py` produced
   `53 passed in 1.12s`.

## Verification and review

- Downstream summary-shape search found no exact-dictionary comparison needing
  compatibility work. The persisted sequence summary now asserts both
  `schema` and the preserved `schema_version`.
- The governed-quality fixture already expects the same schema and was not
  modified.
- `python -m py_compile scripts/testing/official_openvino/metrics.py` passed
  with an isolated bytecode-cache prefix.
- `git diff --check` passed for the scoped change.
- Scope is limited to the measurement-summary producer, its focused tests, and
  this report; no quality-campaign source or tests were changed.
