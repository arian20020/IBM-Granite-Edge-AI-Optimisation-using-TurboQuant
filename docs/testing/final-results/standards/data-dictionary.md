# Data dictionary

Every canonical row has stable identifiers. `route_id` identifies the tested repository route, `campaign_id` identifies the campaign within that route, `test_case_id` identifies the intended configuration, and `attempt_id` identifies one execution attempt. IDs are explicit values, never inferred from a row position or display name.

`measurements` records individual observations, with `measurement_id` and, where available, `run_id` or `repetition_id`. `results` records derived values with `summary_id` and the source `measurement_id` values. `quality` records an observation with `quality_id`, and may name its `prompt_id`, `criterion_id`, prompt suite, rubric, and scoring version. `failures` records a structured explanation with `failure_id`. `evidence` records provenance with `evidence_id`.

`model_id`, `weight_format_id`, `cache_format_id`, and `backend_id` identify the configuration components when that information exists. Human-readable names and source-specific labels are separate from these stable IDs.

Nullable measurement and score fields are unavailable observations. They are not zero values, successful values, or inferred replacements. Status fields are controlled strings and are never nullable.
