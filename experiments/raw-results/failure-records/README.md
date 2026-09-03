# Failure Records

Contains raw records describing failed or blocked runs.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains raw records describing failed or blocked runs.

### Start here

Begin with [`failure-evidence.csv`](failure-evidence.csv). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`failure-evidence.csv`](failure-evidence.csv) | CSV table with 15 data row(s). Main columns are `route`, `test_case_id`, `attempt_id`, `terminal_status`, `evidence_ids`, `retained_path`, `size_bytes` and 1 more. | Preserved evidence; do not edit |

### Important boundaries

- Treat captured evidence as read-only. Add a new run instead of rewriting an old one.
- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
