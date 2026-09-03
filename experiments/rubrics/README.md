<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Evaluation Rubrics

## Purpose

Human or model-judge criteria, scoring instructions and limitations.

## What belongs here

- Scoring dimensions and scale.
- Examples/anchors.
- Judge model/version and prompt if used.
- Inter-rater or repeatability notes.

## Related IDs

None assigned

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Use stable requirement, work-package, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.

## Source

Repository evidence structure

# Evaluation Rubrics

Store versioned output-quality, instruction-following, long-context and failure-classification rubrics here. Each processed evaluation must record the rubric version used.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Defines how model output quality is scored.

### Start here

Begin with [`.gitkeep`](.gitkeep). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`.gitkeep`](.gitkeep) | Supporting data file for .gitkeep. | Controlled test input |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
