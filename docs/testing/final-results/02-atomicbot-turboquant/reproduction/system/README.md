# System

Records the hardware, software and repository identity of the test environment. Here it applies to the AtomicBot TurboQuant route.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Records the hardware, software and repository identity of the test environment. Here it applies to the AtomicBot TurboQuant route.

### Start here

Begin with [`environment.txt`](environment.txt). The tables below explain the remaining items.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`environment.txt`](environment.txt) | Plain-text evidence or diagnostic output for environment; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`hardware.json`](hardware.json) | Stores a JSON object with top-level fields `available_memory_mib`, `logical_cpus`, `machine`, `npu`, `platform`, `processor`, `vulkan_sdk_version`. | Supporting repository file |
| [`model-artifacts.csv`](model-artifacts.csv) | CSV table with 19 data row(s). Main columns are `model_id`, `weight_format_id`, `status`. | Supporting repository file |
| [`repository.json`](repository.json) | Stores a JSON object with top-level fields `branch`, `commit`, `deviation_rows`, `measurement_field_evidence`, `quality_adjudications`, `quality_authority`, `quality_calibration`, `quality_contract`, …. | Supporting repository file |
| [`software.json`](software.json) | Stores a JSON object with top-level fields `reporting_interpreter`, `tool_versions`. | Supporting repository file |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
