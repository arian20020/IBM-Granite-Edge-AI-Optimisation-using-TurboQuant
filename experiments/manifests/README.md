<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Experiment Manifests

## Purpose

Planned evidence location for requirement N-M07: Each final run must record requested and actual backend, device and optimisation state.

## What belongs here

- One manifest per formal run/campaign.
- Exact model/runtime revisions and SHA-256.
- Requested and actual backend/device/optimisation state.
- Links to raw and processed directories.
- Evidence matching EVID-AUDIT.
- Acceptance criteria: Each formal experiment has an ID, hardware/model/runtime/config hashes, requested/actual state, command, raw stdout/stderr, measurements, outputs and failure status.
- Verification method: Evidence-manifest audit
- Evidence index or README linking the artefacts to the requirement.
- Evidence matching AC-N-M07.
- Acceptance criteria: Manifest separates requested from actual runtime/backend/device/cache and cites the evidence used to determine actual state.
- Verification method: Manifest audit

## Related IDs

R-M08, N-M07

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Use stable requirement, work-package, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.

## Source

Repository evidence structure; RTM Planned Evidence Path

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Records the exact inputs, versions and intended configurations for experiments.

### Start here

Begin with [`experiment-manifest-template.json`](experiment-manifest-template.json). The tables below explain the remaining items.

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
| [`turbovec/`](turbovec/README.md) | This folder covers the TurboVec feasibility experiment. |
| [`upstream-llama-cpp/`](upstream-llama-cpp/README.md) | This folder covers the upstream llama.cpp route. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`experiment-manifest-template.json`](experiment-manifest-template.json) | Stores a JSON object with top-level fields `experiment_id`, `research_question`, `purpose`, `timestamp_utc`, `hardware`, `model`, `runtime`, `configuration`, …. | Controlled test input |

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
