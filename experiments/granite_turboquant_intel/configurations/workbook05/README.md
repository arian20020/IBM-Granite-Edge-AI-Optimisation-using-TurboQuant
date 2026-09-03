# Workbook 05

Contains controlled helpers for Workbook 05 source admission, build and measurement stages.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains controlled helpers for Workbook 05 source admission, build and measurement stages.

### Start here

Begin with [`measurement-controls.json`](measurement-controls.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`measurement-controls.json`](measurement-controls.json) | Stores a JSON object with top-level fields `schema_version`, `campaign_id`, `metric_definitions_path`, `prompt_set_path`, `prompt_set_id`, `rubric_path`, `rubric_id`, `quality_amendment_path`, …. | Supporting repository file |
| [`phase3-asset-lock-settings.json`](phase3-asset-lock-settings.json) | Stores a JSON object with top-level fields `schema_version`, `campaign_id`, `model_root`, `probe_root`, `run_root`, `minimum_free_bytes_before_granite_3b`, `granite_3b_repository`, `granite_8b_repository`, …. | Supporting repository file |
| [`phase3-diagnostic-candidates.json`](phase3-diagnostic-candidates.json) | Stores a JSON object with top-level fields `schema_version`, `campaign_id`, `route_id`, `candidate_order`, `required_runtime_source_commit`, `required_backend`, `path_equivalent_requirements`, `model_execution_authorised`, …. | Supporting repository file |
| [`pinned-document-sources.json`](pinned-document-sources.json) | Stores a JSON object with top-level fields `schema_version`, `campaign_id`, `allowed_repositories`, `documents`. | Supporting repository file |
| [`preflight-settings.json`](preflight-settings.json) | Stores a JSON object with top-level fields `schema_version`, `campaign_id`, `expected_runner_name`, `expected_computer_name`, `expected_cpu_substring`, `expected_runner_os`, `expected_runner_arch`, `expected_service_account`, …. | Supporting repository file |
| [`source-admission-settings.json`](source-admission-settings.json) | Stores a JSON object with top-level fields `schema_version`, `campaign_id`, `phase_id`, `workspace_root`, `minimum_free_bytes`, `clone_timeout_seconds`, `submodule_timeout_seconds`, `configure_timeout_seconds`, …. | Supporting repository file |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
