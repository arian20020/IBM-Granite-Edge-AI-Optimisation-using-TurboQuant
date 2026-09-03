# Data

Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the animehacker TQ3_0 route.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the animehacker TQ3_0 route.

### Start here

Open [`route.json`](route.json) for scope, then choose the CSV whose name matches the question you are answering.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`attempts.csv`](attempts.csv) | One row per attempted configuration, including its final status. | Canonical published data |
| [`availability-matrix.csv`](availability-matrix.csv) | CSV table with 10 data row(s). Main columns are `test_case_id`, `model_id`, `weight_format_id`, `cache_format_id`, `backend_id`, `status`. | Canonical published data |
| [`deviations.csv`](deviations.csv) | CSV table with 11 data row(s). Main columns are `deviation_id`, `scope_ids`, `code`, `description`, `resolution`, `terminal`. | Canonical published data |
| [`failures.csv`](failures.csv) | Structured failed, blocked or unavailable outcomes and their evidence references. | Canonical published data |
| [`measurements.csv`](measurements.csv) | Individual measured observations before route-level summarisation. | Canonical published data |
| [`quality.csv`](quality.csv) | Detailed quality scores at the prompt or criterion level. | Canonical published data |
| [`resource-observations.csv`](resource-observations.csv) | CSV table with 10 data row(s). Main columns are `test_case_id`, `inclusion_status`, `summary_evidence_id`, `time_to_first_token_ms`, `prompt_tokens_per_second`, `generation_tokens_per_second`, `peak_working_set_mb` and 5 more. | Canonical published data |
| [`route.json`](route.json) | Machine-readable identity and scope for this route package. | Canonical published data |
| [`summaries.csv`](summaries.csv) | CSV table with 35 data row(s). Main columns are `route_id`, `campaign_id`, `test_case_id`, `summary_id`, `metric_name`, `value`, `unit` and 2 more. | Canonical published data |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
