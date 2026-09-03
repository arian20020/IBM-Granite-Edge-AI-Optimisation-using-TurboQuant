# Protocol

Defines what was meant to be tested, in what order, and under which rules. Here it applies to the AtomicBot TurboQuant route.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Defines what was meant to be tested, in what order, and under which rules. Here it applies to the AtomicBot TurboQuant route.

### Start here

Begin with [`execution-sequence.md`](execution-sequence.md). The tables below explain the remaining items.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`execution-sequence.md`](execution-sequence.md) | Readable Markdown document titled “Execution sequence”. | Controlled test input |
| [`intended-test-matrix.csv`](intended-test-matrix.csv) | CSV table with 19 data row(s). Main columns are `ID`, `Model`, `KV cache`, `Execution`, `Context`, `Purpose / limitation`, `Status`. | Controlled test input |
| [`metric-definitions.md`](metric-definitions.md) | Readable Markdown document titled “Metric definitions”. | Controlled test input |
| [`test-plan.md`](test-plan.md) | Readable Markdown document titled “Test plan”. | Controlled test input |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
