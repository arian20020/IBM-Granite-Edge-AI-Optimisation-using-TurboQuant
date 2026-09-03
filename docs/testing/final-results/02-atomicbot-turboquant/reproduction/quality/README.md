# Quality evidence

The 114 P1-P6 observations bind each output hash to GTQ-PROMPTS-v1, GTQ-QUALITY-RUBRIC-v1, the content-keyed adjudication, the Quality-Evaluation register row, and the per-test summary. Summary, adjudication, and register authorities are independently pinned so coordinated internally consistent edits are rejected. The application remains a limited/provisional regression screen, is not directly comparable with OpenVINO, and is not a general healthcare or education benchmark. Calibration: Not collected.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the AtomicBot TurboQuant route.

### Start here

Begin with [`calibration.md`](calibration.md). The tables below explain the remaining items.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`adjudication-log.csv`](adjudication-log.csv) | CSV table with 114 data row(s). Main columns are `quality_id`, `test_case_id`, `prompt_id`, `adjudication_key`, `dimensions_json`, `weighted_score`, `critical_caps_json` and 5 more. | Supporting repository file |
| [`calibration.md`](calibration.md) | Readable Markdown document titled “Calibration”. | Supporting repository file |
| [`outputs-index.csv`](outputs-index.csv) | CSV table with 114 data row(s). Main columns are `test_case_id`, `prompt_id`, `output_sha256`, `adjudication_key`, `source_evidence_id`. | Supporting repository file |
| [`prompt-suite.csv`](prompt-suite.csv) | CSV table with 6 data row(s). Main columns are `prompt_id`, `task`, `deterministic_checks_json`, `generation_settings_json`, `scope`, `comparability`. | Supporting repository file |
| [`rubric.md`](rubric.md) | Readable Markdown document titled “GTQ-QUALITY-RUBRIC-v1 — limited/provisional application”. | Supporting repository file |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
