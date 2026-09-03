# Quality evidence

Original upstream adjudication is bound to tracked `GTQ-QUALITY-RUBRIC-v1`, frozen `GTQ-PROMPTS-v1`, and the hashed 2026-07-15 scoring source. It is not directly comparable with OpenVINO scoring.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the upstream llama.cpp baseline route.

### Start here

Begin with [`calibration.md`](calibration.md). The tables below explain the remaining items.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`adjudication-log.csv`](adjudication-log.csv) | CSV table with 1 data row(s). Main columns are `adjudication_id`, `method`, `status`. | Supporting repository file |
| [`calibration.md`](calibration.md) | Readable Markdown document titled “Calibration”. | Supporting repository file |
| [`outputs-index.csv`](outputs-index.csv) | CSV table with 54 data row(s). Main columns are `test_case_id`, `prompt_id`, `source_evidence_id`. | Supporting repository file |
| [`prompt-suite.csv`](prompt-suite.csv) | CSV table with 6 data row(s). Main columns are `prompt_id`, `task_type`, `scope`, `deterministic_checks_json`, `generation_settings_json`, `prompt_set_id`. | Supporting repository file |
| [`rubric.md`](rubric.md) | Readable Markdown document titled “GTQ-QUALITY-RUBRIC-v1”. | Supporting repository file |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
