# System

Records the hardware, software and repository identity of the test environment. Here it applies to the upstream llama.cpp baseline route.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Records the hardware, software and repository identity of the test environment. Here it applies to the upstream llama.cpp baseline route.

### Start here

Begin with [`environment.txt`](environment.txt). The tables below explain the remaining items.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`environment.txt`](environment.txt) | Plain-text evidence or diagnostic output for environment; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`hardware.json`](hardware.json) | Stores a JSON object with top-level fields `cpu`, `gpu`, `gpu_dedicated_memory`, `machine_id`, `npu`, `ram_bytes`. | Supporting repository file |
| [`model-artifacts.csv`](model-artifacts.csv) | CSV table with 6 data row(s). Main columns are `model_id`, `weight_format_id`, `status`. | Supporting repository file |
| [`repository.json`](repository.json) | Stores a JSON object with top-level fields `best_observed_cpu_test_id`, `best_observed_intel_gpu_test_id`, `commit`, `deviation_rows`, `historical_missing_metric_display`, `performance_register_reason`, `performance_register_status`, `raw_results_reason`, …. | Supporting repository file |
| [`software.json`](software.json) | Stores a JSON object with top-level fields `cmake`, `compiler`, `os`, `performance_register_rows`, `sycl`, `vulkan_sdk`. | Supporting repository file |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
