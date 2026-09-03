# animehacker TQ3_0 Manifests

Store one immutable manifest per run using `test-id/run-id/manifest.json`.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This folder covers the animehacker TQ3_0 route.

### Start here

Begin with [`retest-matrix.json`](retest-matrix.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`retest-matrix.json`](retest-matrix.json) | Stores a JSON object with top-level fields `schema_version`, `campaign`, `cases`. | Controlled test input |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
