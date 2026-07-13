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

