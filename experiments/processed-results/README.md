<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Processed Results

## Purpose

Reproducible summaries, comparisons and calibrated metrics derived from named raw runs.

## What belongs here

- Input raw run IDs.
- Processing script/commit.
- Tables with units and uncertainty.
- Interpretation and limitations.

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

Contains results derived from raw evidence. Use validation and provenance before trusting a value.

### Start here

Begin with [`.gitkeep`](.gitkeep). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| [`animehacker-tq3-0/`](animehacker-tq3-0/README.md) | This folder covers the animehacker TQ3_0 route. |
| [`atomicbot-turboquant/`](atomicbot-turboquant/README.md) | This folder covers the AtomicBot TurboQuant route. |
| [`context/`](context/README.md) | Planned home for derived context-stability results associated with requirement R-M07. Folder presence alone does not mean the experiment completed. |
| [`cross-route/`](cross-route/README.md) | Planned home for the derived route-compatibility view associated with requirement R-M11. Use final-results for released comparisons. |
| [`cross-route-comparison/`](cross-route-comparison/README.md) | This folder covers the guarded cross-route comparison. |
| [`custom-openvino-turboquant/`](custom-openvino-turboquant/README.md) | This folder covers the experimental OpenVINO TurboQuant route. |
| [`EST-VALID-001/`](EST-VALID-001/README.md) | Planned location for memory-estimator accuracy and false-safe/false-unsafe analysis under requirement R-M14. |
| [`EXP-TQ-COMP-001/`](EXP-TQ-COMP-001/README.md) | Planned location for a matched TurboQuant-versus-standard-cache comparison under requirement R-M01. |
| [`EXP-TV-COMP-001/`](EXP-TV-COMP-001/README.md) | Contains the processed TurboVec feasibility runs and their bounded `DEMONSTRATOR_ONLY` decision under requirement R-M13. |
| [`final-metrics/`](final-metrics/README.md) | Planned location for normalised memory and runtime-performance measures under requirements R-M04 and R-M05. |
| [`memory-budgets/`](memory-budgets/README.md) | Planned location for derived 4 GB, 8 GB and 16 GB system-memory budget assessments under requirement R-M12. |
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
- [context guide](context/README.md)
- [cross-route guide](cross-route/README.md)
- [cross-route-comparison guide](cross-route-comparison/README.md)
- [custom-openvino-turboquant guide](custom-openvino-turboquant/README.md)
- [EST-VALID-001 guide](EST-VALID-001/README.md)
- [EXP-TQ-COMP-001 guide](EXP-TQ-COMP-001/README.md)
- [EXP-TV-COMP-001 guide](EXP-TV-COMP-001/README.md)
- [final-metrics guide](final-metrics/README.md)
- [memory-budgets guide](memory-budgets/README.md)
- [official-openvino guide](official-openvino/README.md)
- [upstream-llama-cpp guide](upstream-llama-cpp/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
