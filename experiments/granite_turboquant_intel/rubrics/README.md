# Rubrics

Defines how model output quality is scored.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Defines how model output quality is scored.

### Start here

Begin with [`quality-rubric-v1.json`](quality-rubric-v1.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`quality-rubric-v1.json`](quality-rubric-v1.json) | Stores a JSON object with top-level fields `rubric_id`, `version`, `status`, `created_date`, `dimensions`, `anchors`, `procedure`. | Controlled test input |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
