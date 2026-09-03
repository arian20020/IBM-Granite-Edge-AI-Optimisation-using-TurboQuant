# AtomicBot TurboQuant Protocols

`quality-prompts.json` binds this route to the shared frozen P1-P6 prompt set
and `GTQ-QUALITY-RUBRIC-v1`. Configuration labels remain hidden during
adjudication and no score adjustment is made from the precision name alone.

Protocols for building the AtomicBot fork, reproducing a standard baseline, proving TurboQuant activation, detecting fallback and comparing memory, speed, quality and stability under matched settings.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This folder covers the AtomicBot TurboQuant route.

### Start here

Begin with [`quality-prompts.json`](quality-prompts.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`quality-prompts.json`](quality-prompts.json) | Stores a JSON object with top-level fields `schema_version`, `prompt_set_id`, `prompt_set_path`, `rubric_id`, `rubric_path`, `adjudication`. | Controlled test input |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
