<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Experiment and Processing Scripts

## Purpose

Repeatable commands/scripts for running experiments, collecting metrics and producing processed tables/figures.

## What belongs here

- Usage and prerequisites.
- Input/output paths.
- Pinned dependencies.
- Error handling and deterministic settings where possible.

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

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains route-specific experiment scripts and compatibility entry points.

### Start here

Begin with [`.gitkeep`](.gitkeep). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| [`animehacker-tq3-0/`](animehacker-tq3-0/README.md) | This folder covers the animehacker TQ3_0 route. |
| [`atomicbot-turboquant/`](atomicbot-turboquant/README.md) | This folder covers the AtomicBot TurboQuant route. |
| [`cross-route-comparison/`](cross-route-comparison/README.md) | This folder covers the guarded cross-route comparison. |
| [`custom-openvino-turboquant/`](custom-openvino-turboquant/README.md) | This folder covers the experimental OpenVINO TurboQuant route. |
| [`official-openvino/`](official-openvino/README.md) | This folder covers the official OpenVINO route. |
| [`upstream-llama-cpp/`](upstream-llama-cpp/README.md) | This folder covers the upstream llama.cpp route. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`.gitkeep`](.gitkeep) | Supporting data file for .gitkeep. | Supporting repository file |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)
- [animehacker-tq3-0 guide](animehacker-tq3-0/README.md)
- [atomicbot-turboquant guide](atomicbot-turboquant/README.md)
- [cross-route-comparison guide](cross-route-comparison/README.md)
- [custom-openvino-turboquant guide](custom-openvino-turboquant/README.md)
- [official-openvino guide](official-openvino/README.md)
- [upstream-llama-cpp guide](upstream-llama-cpp/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
