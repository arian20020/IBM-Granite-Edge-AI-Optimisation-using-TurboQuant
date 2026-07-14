<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Work-Package Evidence

## Purpose

Each work-package folder is an evidence pack that explains what was completed, how the Definition of Done was checked, and where the authoritative proof is stored. The README must link to existing planning, implementation, test, experiment or release artefacts rather than duplicate them.

## Common template

Use the [Evidence Record Template](../templates/Evidence-Record-Template.md) for every work-package README.

Required sections:

- metadata and status;
- statement being evidenced;
- Definition of Done;
- evidence summary;
- authoritative evidence links;
- validation checklist, result and conclusion;
- traceability;
- limitations and change control.

## Current evidence packs

- [PD-01 — Freeze first-release definition and research questions](PD-01/README.md)
- [PD-04 — Must-Have RTM and acceptance criteria](PD-04/README.md)
  - [Must-Have coverage audit](PD-04/Must-Have-Coverage-Audit.md)
- [PD-09 — App-specific evaluation addendum](PD-09/README.md)

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Keep one authoritative source and cross-reference it from related evidence records.
- Use stable requirement, work-package, engineering-practice, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.