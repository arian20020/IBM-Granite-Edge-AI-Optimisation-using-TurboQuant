# Examples

Contains example input files that show the expected structure without being real results.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains example input files that show the expected structure without being real results.

### Start here

Begin with [`official-openvino-wb04-comparison-reconciliation-input.example.json`](official-openvino-wb04-comparison-reconciliation-input.example.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`official-openvino-wb04-comparison-reconciliation-input.example.json`](official-openvino-wb04-comparison-reconciliation-input.example.json) | Stores a JSON object with top-level fields `schema`, `documentation_only`, `notice`, `matrix`, `campaign_state`, `steps`, `release_input_sha256`. | Supporting repository file |
| [`official-openvino-wb04-reconciliation-input.example.json`](official-openvino-wb04-reconciliation-input.example.json) | Stores a JSON object with top-level fields `schema`, `campaign_date`, `workbook_version`, `revision_id`, `matrix_path`, `matrix_sha256`, `selected_measurement_summaries`, `terminal_records`, …. | Supporting repository file |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
