# Quality evidence

Forty-two P1-P6 observations preserve the WB-03 historical harsh content screen. Deterministic gates and manual adjudication are source-bound. Calibration: Not collected. Direct OpenVINO ranking is prohibited.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the animehacker TQ3_0 route.

### Start here

Begin with [`calibration.md`](calibration.md). The tables below explain the remaining items.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`adjudication-log.csv`](adjudication-log.csv) | CSV table with 42 data row(s). Main columns are `test_case_id`, `prompt_id`, `score`, `deterministic_pass`, `dimensions_json`, `critical_caps_json`, `critical_cap_reason`. | Supporting repository file |
| [`calibration.md`](calibration.md) | Readable Markdown document titled “Calibration”. | Supporting repository file |
| [`outputs-index.csv`](outputs-index.csv) | CSV table with 42 data row(s). Main columns are `test_case_id`, `prompt_id`, `output_sha256`, `source_evidence_id`. | Supporting repository file |
| [`prompt-suite.csv`](prompt-suite.csv) | CSV table with 6 data row(s). Main columns are `prompt_id`, `task`, `deterministic_checks_json`, `scope`, `comparability`. | Supporting repository file |
| [`rubric.md`](rubric.md) | Readable Markdown document titled “GTQ-QUALITY-RUBRIC-v1 historical application”. | Supporting repository file |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
