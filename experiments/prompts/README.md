<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Frozen Evaluation Prompts

## Purpose

Versioned prompts and expected task categories used in quality or usability evaluation.

## What belongs here

- Prompt-set version and licence/source.
- Seed/sampling settings.
- Reference or rubric link.
- No private user data.

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

# Versioned Prompts

Store shared prompts and prompt metadata here. Every prompt used in a run must have a stable version or hash. Do not edit a prompt after it has been used; create a new version.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains fixed prompts used to make model-quality tests repeatable.

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
