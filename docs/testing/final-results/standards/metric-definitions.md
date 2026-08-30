# Metric definitions

Measurements are individual observations. A blank measurement field represents an unavailable observation and is never treated as zero.

Derived summaries name their source observations and aggregation rule. A median is calculated over the included repetitions only; unavailable observations are excluded rather than replaced with zero. Worst-observed peak memory is `max(peak_working_set_bytes)` over the included observations with a recorded peak working-set value. If no observation is available, the derived value remains unavailable.

Latency is recorded in milliseconds. Prompt and generation throughput are recorded in tokens per second. `peak_working_set_bytes`, `input_tokens`, and `output_tokens` are counts and must not be negative.
