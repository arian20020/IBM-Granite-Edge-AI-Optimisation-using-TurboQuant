<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Requirement Evidence

## Purpose

Use one folder per active requirement only when the RTM points to a requirement-specific evidence directory. When the RTM points directly to an authoritative file, that file is the evidence and should not be duplicated here.

## Common template

Requirement evidence READMEs must follow the [Evidence Record Template](../templates/Evidence-Record-Template.md).

Each record must contain:

- the exact requirement statement;
- acceptance criteria and verification method;
- authoritative implementation, document, test or experiment links;
- pass, fail, blocked or partially verified status;
- commit or pull-request references;
- validator and validation date;
- traceability, limitations and change-control instructions.

## Current evidence packs

- [G-M02 — Versioned MoSCoW requirements baseline](G-M02/README.md)
  - [MoSCoW v1.2 review checklist](G-M02/MoSCoW-v1.2-Review-Checklist.md)

## Direct-file examples

- `G-M01` uses [`docs/planning/Project-Definition-v1.md`](../../planning/Project-Definition-v1.md) as its direct authoritative evidence.
- Some research requirements point directly to controlled experiment or processed-result locations rather than to a requirement folder.

Related work-package and engineering-practice evidence packs must cross-reference the authoritative source instead of copying it.

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Keep one authoritative source and cross-reference it.
- Use stable requirement, work-package, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.