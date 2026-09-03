<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# R-M04 Evidence Location

## Purpose

Planned evidence location for requirement R-M05: The project must evaluate runtime performance for every final test configuration.

## What belongs here

- Evidence matching MET-MEM.
- Acceptance criteria: Each final row records available RAM before, peak process-tree working set/private where used, relevant GPU shared memory and measurement definition.
- Verification method: Metrics audit
- Evidence index or README linking the artefacts to the requirement.
- Evidence matching MET-PERF.
- Acceptance criteria: Cold/warm load, TTFT, prompt speed, generation speed and total response duration are reported where available using fixed definitions and repetitions.
- Verification method: Performance experiment audit

## Related IDs

R-M04, R-M05

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Use stable requirement, work-package, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.

## Source

RTM Planned Evidence Path

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This folder groups the final metrics material used by the testing workflow.

### Start here

This is a navigation or evidence container. Use the folder explanations below to choose the next level.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

There are no immediate non-README files at this level. Continue into the child folders described above.

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
