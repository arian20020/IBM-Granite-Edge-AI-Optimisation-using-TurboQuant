# Metrics and quality gates

> **Document status:** Controlled measurement guide
> **Version:** 3.0
> **Last updated:** 14 July 2026

## Why both quality and performance are required

A compressed model that uses less memory but gives unusable answers is not a successful optimisation. A high-quality model that takes too long to load or exceeds available memory is also not useful on the target machine. Every configuration must therefore pass a combined gate.

## Performance metrics

### Peak RAM

The highest system-memory use during model loading and generation. Record the measurement method and whether the value includes the application, backend and operating-system caching.

### Device memory

GPU or NPU memory where it can be measured. Do not combine it with system RAM without labelling the values separately.

### KV-cache memory

Record the cache size for the chosen context length, batch size, number of layers and cache precision. This metric is central to the TurboQuant investigation.

### Model load time

Time from starting the load operation to the model being ready for a prompt.

### Time to first token

Time from submitting a prompt to receiving the first generated token. This includes prompt processing and is often what a user notices first.

### Prompt-processing speed

Tokens per second while the existing prompt is evaluated.

### Generation speed

Tokens generated per second after the first output token.

### Stability

Repeat at least three measured runs after warm-up. Report median and range rather than presenting the fastest run only.

## Quality checks

Use a fixed prompt set covering:

- factual question answering;
- summarisation;
- instruction following;
- structured output;
- code or technical explanation where relevant;
- long-context recall;
- refusal or safety behaviour;
- education and healthcare-style scenarios that do not ask for clinical decisions.

## Ten-point output score

Score each answer from 0 to 10 using the same rubric:

| Area | Points | Question |
|---|---:|---|
| Correctness | 0–3 | Is the answer factually and logically correct? |
| Instruction following | 0–2 | Did it do what the prompt asked? |
| Completeness | 0–2 | Did it include the important parts? |
| Clarity | 0–1 | Is it understandable and well organised? |
| Consistency | 0–1 | Does it avoid contradictions or corrupted text? |
| Safety/relevance | 0–1 | Is it appropriate for the task and domain boundary? |

A score should include a short reason, not only a number.

## Suggested gates

### Correctness gate

- model loads successfully;
- output is valid text;
- no repeated corruption or immediate failure;
- requested configuration is actually active.

### Quality gate

- no severe regression on critical prompts;
- average score remains within the agreed tolerance of the baseline;
- long-context retrieval remains usable at the tested context length.

### Performance gate

- memory reduction is measurable and repeatable;
- runtime overhead is reported honestly;
- the configuration fits the target hardware;
- first-token and generation latency remain acceptable for interaction.

### Evidence gate

- model and code revisions recorded;
- commands and logs retained;
- unsuccessful runs included;
- no result depends on an undocumented manual change.

## Comparing configurations

Use a table like this:

| Test ID | Model | Weights | K cache | V cache | Device | Context | Peak RAM | KV MB | TTFT | tok/s | Quality /10 | Status |
|---|---|---|---|---|---|---:|---:|---:|---:|---:|---:|---|

Only compare rows where the prompt set, generation settings and hardware conditions are equivalent.

## Decision rule

Select a configuration because it provides the best acceptable trade-off, not because it wins one metric. The selected Automatic, Quality, Balanced and Efficiency modes should be backed by tested configurations and should be revisited when the backend or model revision changes.

## Sources used

- `SRC-BOOK-AI-ENGINEERING-2025`
- `SRC-OV-BENCHMARK-2026`
- `SRC-PAPER-TURBOQUANT-2025`
