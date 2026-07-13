# Metric Definitions and Calculation Rules

These definitions prevent different routes from using the same label for different measurements.

| Metric | Controlling definition | Unit | Required source |
|---|---|---:|---|
| Model load time | Start of runtime model-load operation to model-ready state, excluding download/conversion. | ms | runtime timing or timestamped log |
| TTFT | Submission of the complete prompt to availability of the first generated token. State whether tokenization/prefill is included. | ms | runtime PerfMetrics or timestamped collector |
| Prompt-processing throughput | Input tokens processed divided by prompt/prefill duration. | tokens/s | token counts and prefill duration |
| TPOT | Decode duration divided by generated tokens after the first token, using a documented zero/one-token rule. | ms/token | token counts and timing |
| Decode throughput | Generated tokens divided by decode duration. | tokens/s | token counts and timing |
| Total generation time | Prompt submission to completed or cancelled response. | ms | timestamped collector |
| Peak working set | Maximum process-tree physical working set during the controlled interval. | bytes | process-tree sampler |
| Peak private bytes | Maximum process-tree committed private memory during the controlled interval. | bytes | process-tree sampler |
| Minimum available RAM | Lowest system available-memory sample during the controlled interval. | bytes | system sampler |
| KV-cache allocation | Runtime-reported K/V allocation, not inferred from process working set. | bytes | allocation log/property |
| GPU memory | Dedicated and shared values recorded separately; never combine without labelling. | bytes | GPU sampler/runtime |
| Quality score | Weighted score after deterministic gates and critical caps using `GTQ-QUALITY-RUBRIC-v1`. | 0-10 | raw output, validator and adjudication |
| Stability | Completion across planned repetitions without crash, hang, corruption, unsafe OOM or unexplained fallback. | classification | all run evidence |

## Statistics

- Pilot runs diagnose the protocol and are excluded.
- One defined warm-up is excluded.
- Use at least three measured repetitions unless a documented safety gate blocks them.
- Report the median plus range or another explicitly selected variability measure.
- Preserve all valid repetitions.
- Label cold-load and warm-generation measurements separately.
- Do not average unmatched contexts, devices, prompts or models.
