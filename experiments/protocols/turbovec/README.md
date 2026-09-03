# TurboVec

This folder covers the TurboVec feasibility experiment.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This folder covers the TurboVec feasibility experiment.

### Start here

Begin with [`corpus-v1.json`](corpus-v1.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`corpus-v1.json`](corpus-v1.json) | Stores a JSON object with top-level fields `schema_version`, `documents`, `canonical_sha256`. | Controlled test input |
| [`feasibility-v1.schema.json`](feasibility-v1.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `type`, `additionalProperties`, `required`, `properties`. | Controlled test input |
| [`queries-v1.json`](queries-v1.json) | Stores a JSON object with top-level fields `schema_version`, `queries`, `canonical_sha256`. | Controlled test input |
| [`relevance-v1.json`](relevance-v1.json) | Stores a JSON object with top-level fields `schema_version`, `judgements`, `canonical_sha256`. | Controlled test input |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
