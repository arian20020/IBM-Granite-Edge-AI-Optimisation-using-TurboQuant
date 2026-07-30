# OpenVINO WB-04 Expected-Rejection Evidence Design

## Purpose

Create deterministic, fail-closed evidence for the WB-04 configurations whose
declared outcome is rejection. The controller must prove that generation was
never launched and must never synthesize timing, memory, utilization, KV, or
quality measurements.

## Scope

The controller covers:

- `OV-TQS-05` through `OV-TQS-12`;
- `OV-TQ-18` through `OV-TQ-20`;
- the `OV-B11` QJL and Polar build boundary as two isolated probes.

The frozen matrix and `execution_contract()` remain authoritative. `OV-B11`
retains its complete matrix case and contract in both records, while its QJL
and Polar inputs are isolated so each unsupported label is independently
proved.

## Components

`scripts/testing/official_openvino/expected_rejections.py` owns selection,
probe execution, canonical hashing, strict validation, and atomic output.
`scripts/testing/generate_official_openvino_expected_rejections.py` is a thin
CLI. Focused unit tests exercise the real matrix and real runtime-property
translator.

Each probe records its controlled ID, full matrix case, full execution
contract, exact runtime-property input, exact expected and observed
`ValueError`, `generation_not_launched=true`, `cleanup_process_count=0`, and a
hash over the canonical record. The aggregate records the raw matrix hash, the
controller source hash, the exact probe set, and a hash over the canonical
aggregate.

## Failure Rules

Unsupported key and value algorithms identify the exact field and value while
retaining the existing uppercase-enum guidance. GPU TurboQuant retains the
exact CPU-only error. Generation is considered rejected only when the expected
`ValueError` type and full message match exactly.

Validation rejects missing or duplicate probes, unknown fields, changed
contracts/configuration/errors, mismatched matrix or controller hashes,
tampered probe or aggregate hashes, any launched-generation claim, nonzero
cleanup count, and any numeric-metric or quality field.

## Verification

Tests first establish RED for exact error messages, deterministic generation,
tamper rejection, and CLI output. GREEN requires the focused tests, existing
matrix/measurement tests, and the broader official OpenVINO testing suite to
pass. The CLI then writes and validates the dated evidence under
`experiments/raw-results/openvino-turboquant/2026-07-30/expected-rejections/`.
