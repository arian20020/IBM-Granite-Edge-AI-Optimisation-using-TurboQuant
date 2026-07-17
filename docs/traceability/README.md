# Repository Traceability System

The controlled RTM workbook is the authoritative editable source for requirement definitions, status, validation and dashboard calculations.

## Current working baseline

- [MoSCoW Requirements Baseline v1.2](../requirements/MoSCoW-Requirements-v1.2.md)
- [Requirements Traceability Matrix v1.3](../requirements/Requirements-Traceability-Matrix-v1.3.md)
- [Project Definition v1.1](../planning/Project-Definition-v1.1.md)
- [Requirements and Scope Change Log](../change-control/Requirements-and-Scope-Change-Log.md)

The working baseline contains:

- 111 lifecycle records;
- 72 active requirements;
- 56 active Must Haves;
- 14 active Should Haves;
- 2 active Could Haves.

## Source-of-truth rules

| Layer | Purpose | Authority |
|---|---|---|
| Controlled RTM workbook | Editable definitions, status, validation and dashboard | Authoritative |
| MoSCoW v1.2 | Human-readable controlled requirements baseline | Authoritative for reviewed scope |
| RTM Markdown v1.3 | Human-readable forward and reverse traceability | Authoritative review snapshot |
| Evidence folders | Proof of acceptance and Definition of Done | Authoritative for evidence claims |
| Issues | Assignment and discussion | Operational |
| Pull requests and commits | Reviewed implementation history | Authoritative for repository changes |

## Important rule

A requirement or task is Verified only when:

1. the implementation or required deliverable exists;
2. the planned evidence exists;
3. the acceptance criteria or Definition of Done are checked;
4. Validation is `Validated` in the controlled RTM.

A closed issue does not automatically make a requirement Verified.

## Regeneration

After a material RTM change, run the repository traceability update script against the controlled workbook and review the diff before commit. Generated draft views from an older baseline must not be used to override the current v1.2/v1.3 working baseline.
