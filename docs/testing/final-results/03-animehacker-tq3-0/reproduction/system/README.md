# System

Records the hardware, software and repository identity of the test environment. Here it applies to the animehacker TQ3_0 route.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Records the hardware, software and repository identity of the test environment. Here it applies to the animehacker TQ3_0 route.

### Start here

Begin with [`environment.txt`](environment.txt). The tables below explain the remaining items.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`environment.txt`](environment.txt) | Plain-text evidence or diagnostic output for environment; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`hardware.json`](hardware.json) | Stores a JSON object with top-level fields `graphics`, `machine_id`, `npu`, `operating_system`, `processor`, `ram`. | Supporting repository file |
| [`model-artifacts.csv`](model-artifacts.csv) | CSV table with 10 data row(s). Main columns are `model_id`, `weight_format_id`, `status`. | Supporting repository file |
| [`repository.json`](repository.json) | Stores a JSON object with top-level fields `branch`, `commit`, `formal_runtime_summary_count`, `historical_failure_attempt_count`, `historical_failure_entity_hashes`, `historical_failure_rows`, `intended_matrix_entities`, `matrix_entity_hashes`, …. | Supporting repository file |
| [`software.json`](software.json) | Stores a JSON object with top-level fields `cmake`, `cpu_tests`, `msvc`, `ninja`, `sycl_tests`, `vulkan_tq3`. | Supporting repository file |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
