# GPU STANDARD Controller Readiness Report

## Scope

This controller-only change prepares the governed WB-04 runtime path for the
frozen OV-06 GPU STANDARD row. It does not import OpenVINO, load a model, or
run inference.

The change is limited to:

- GPU-safe runtime property construction in `runtime_measurement.py`;
- parsed GPU engine-count persistence and device-aware observability gates in
  `runtime_process.py`; and
- adversarial and compatibility tests in
  `test_official_openvino_measurement.py`.

`campaign_spec.py` and generator-owned files were not changed.

## RED Evidence

The GPU runtime-property tests were added before the implementation. The
focused command ran two tests and failed with three errors and three failures:

```text
python -m unittest \
  ...test_gpu_standard_runtime_properties_are_plugin_owned_and_gpu_safe \
  ...test_gpu_runtime_property_spec_rejects_ambiguous_routes_and_overrides

FAILED (failures=3, errors=3)
```

The failures proved that the old builder rejected `GPU.0`, rejected the frozen
plugin-owned cache marker, and could not enforce the GPU-specific property
contract.

The telemetry tests were also added before their implementation. The focused
command ran five tests and failed with one error and six failures:

```text
python -m unittest \
  ...test_gpu_parser_preserves_every_observation_and_recomputes_statistics \
  ...test_governed_process_persists_parsed_gpu_engine_evidence \
  ...test_gpu_measurement_sample_requires_repeated_nonzero_observability \
  ...test_integrated_gpu_shared_memory_observability_is_valid \
  ...test_cpu_measurement_sample_allows_honest_zero_gpu_evidence

FAILED (failures=6, errors=1)
```

The error was the missing `gpu_engine_count` field. The six failures proved
that one-shot, zero-engine, zero-utilization, zero-memory, and failed-query GPU
records were not rejected.

## Implemented Contract

- Runtime devices accept exact `CPU`, `GPU`, and canonical indexed GPU names
  such as `GPU.0` and `GPU.12`; ambiguous routes and malformed device labels
  fail closed.
- GPU execution is restricted to `STANDARD`/`STANDARD`.
- Both controlled GPU cache-precision labels must be `frozen`, which means the
  OpenVINO plugin owns cache precision. No precision override is emitted.
- GPU properties are exactly `ATTENTION_BACKEND`, `CACHE_DIR`, `NUM_STREAMS`,
  and `PERFORMANCE_HINT`. CPU thread count, CPU pinning, TurboQuant, and cache
  precision properties are not sent to GPU.
- Existing CPU property behavior is unchanged.
- Governed records now persist the parser's full `gpu_engine_count` summary.
- An actual GPU activation requires matching repeated utilization and engine
  observations, successful queries, internally reconciled statistics, a
  positive engine-count peak, a positive GPU-utilization peak, and a positive
  combined GPU-memory peak.
- GPU memory components are range-checked. An integrated GPU may report zero
  dedicated memory when shared and combined memory are positive.
- Actual CPU activations bypass the GPU-positive gate and may retain honest
  zero GPU observations.
- The same GPU evidence validator runs during governed-record reconciliation
  and again before a record is converted into a formal measurement sample.

## GREEN Verification

Focused GPU property and CPU compatibility tests:

```text
Ran 5 tests in 0.001s
OK
```

Focused parser, persistence, GPU observability, integrated-GPU, and CPU-zero
tests:

```text
Ran 5 tests in 0.027s
OK
```

Complete runtime measurement suite under Python 3.13:

```text
Ran 29 tests in 0.070s
OK
```

Runtime, metrics, sequence, and campaign-binding suites under the available
Python 3.11 pytest environment:

```text
72 passed in 1.98s
```

`py_compile` compiled all three changed Python files to a temporary writable
cache under Python 3.13. The repository cache was not used because an existing
cache file was locked by the host. `git diff --check` exited zero for all
scoped files.

All verification used synthetic records and parsers. No inference was run.
