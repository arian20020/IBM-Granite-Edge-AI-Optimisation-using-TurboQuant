# Project Evidence Index

This directory stores the small, reviewable evidence records that prove requirements, work packages and engineering practices. Large raw outputs belong in `experiments/raw-results/`; processed results belong in `experiments/processed-results/`.

## Evidence model

An Evidence / Link cell points to one of two things:

1. **A direct evidence file** — the linked document itself is the authoritative proof.
2. **An evidence-pack folder** — the folder README summarises the claim, links to the authoritative files and records validation.

Folder creation alone is not evidence of completion.

## Common template

All new requirement, work-package and engineering-practice evidence READMEs must follow the common template:

- [Evidence Record Template](templates/Evidence-Record-Template.md)
- [Template usage guidance](templates/README.md)

The required sections are metadata, statement being evidenced, Definition of Done or acceptance criteria, evidence summary, authoritative evidence, validation record, traceability, limitations and change control.

## Evidence collections

- [Requirement evidence](requirements/README.md)
- [Work-package evidence](work-packages/README.md)
- [Engineering-practice evidence](engineering-practices/README.md)
- [Evidence recovery and checksums](Evidence-Recovery-Index.md)
- [Evidence indexes and validation](indexes/README.md)
- [MoSCoW and RTM Evidence Index](indexes/MoSCoW-and-RTM-Evidence-Index.md)

## Completed requirements-baseline evidence

The current MoSCoW/RTM planning completion is evidenced by:

- [G-M02 — versioned MoSCoW baseline](requirements/G-M02/README.md)
- [PD-04 — Must-Have RTM and acceptance criteria](work-packages/PD-04/README.md)
- [EP-004 — stable MoSCoW catalogue](engineering-practices/EP-004/README.md)
- [EP-005 — bidirectional traceability](engineering-practices/EP-005/README.md)
- [EP-006 — derived-requirement and change log](engineering-practices/EP-006/README.md)

## Evidence rules

- Keep one authoritative source and cross-reference it instead of copying it into several folders.
- Use stable requirement, work-package, engineering-practice, experiment and research-question IDs.
- Record status, validation state, owner, date, validator and method.
- Link implementation, tests, logs, screenshots, manifests, hashes, commits or pull requests where relevant.
- Preserve raw evidence and generate processed results with version-controlled scripts.
- Record negative results, blockers and limitations rather than deleting failed evidence.
- Do not commit secrets, API keys, personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.