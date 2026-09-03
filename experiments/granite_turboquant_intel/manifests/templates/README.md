# Templates

This folder contains reusable starting structures for new manifests.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Copy a suitable template when creating a controlled manifest, then replace every placeholder and validate it before execution. A template is not run evidence.

### Start here

Begin with [`build-manifest-template.json`](build-manifest-template.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| `workbook05/` | Contains controlled helpers for Workbook 05 source admission, build and measurement stages. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`build-manifest-template.json`](build-manifest-template.json) | Stores a JSON object with top-level fields `schema_version`, `build_id`, `repository_id`, `environment_id`, `started_at_utc`, `ended_at_utc`, `build_type`, `architecture`, …. | Controlled test input |
| [`configuration-manifest-template.json`](configuration-manifest-template.json) | Stores a JSON object with top-level fields `schema_version`, `configuration_id`, `route`, `model_id`, `build_id`, `weight_precision`, `k_cache_type`, `v_cache_type`, …. | Controlled test input |
| [`evidence-hash-manifest-template.json`](evidence-hash-manifest-template.json) | Stores a JSON object with top-level fields `schema_version`, `run_id`, `created_at_utc`, `algorithm`, `files`, `notes`. | Controlled test input |
| [`machine-manifest-template.json`](machine-manifest-template.json) | Stores a JSON object with top-level fields `schema_version`, `environment_id`, `captured_at_utc`, `captured_at_local`, `timezone`, `machine`, `cpu`, `gpu`, …. | Controlled test input |
| [`model-manifest-template.json`](model-manifest-template.json) | Stores a JSON object with top-level fields `schema_version`, `model_id`, `name`, `family`, `release`, `variant`, `source_url`, `source_revision`, …. | Controlled test input |
| [`repository-manifest-template.json`](repository-manifest-template.json) | Stores a JSON object with top-level fields `schema_version`, `repository_id`, `route`, `repository_url`, `branch_or_tag`, `commit_sha`, `upstream_base_commit`, `detached_head`, …. | Controlled test input |
| [`run-manifest-template.json`](run-manifest-template.json) | Stores a JSON object with top-level fields `schema_version`, `campaign_id`, `test_id`, `run_id`, `route`, `workbook_id`, `run_purpose`, `operator`, …. | Controlled test input |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
