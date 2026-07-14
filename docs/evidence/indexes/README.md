<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Evidence Indexes and Validation

## Purpose

This directory contains reviewable indexes that connect requirement, work-package and engineering-practice records to their authoritative evidence.

## Current indexes

- [MoSCoW and RTM Evidence Index](MoSCoW-and-RTM-Evidence-Index.md)
  - links G-M02, PD-04, EP-004, EP-005 and EP-006;
  - links the controlled workbook, checksum, RTM, reverse indexes and change-control records;
  - states the validation boundary clearly.

## Generated index outputs

Generated CSV, JSON or Markdown indexes may also be stored here for:

- requirement-to-path mappings;
- work-package-to-output mappings;
- engineering-practice-to-evidence mappings;
- source workbook version, checksum and generation date.

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Use stable requirement, work-package, engineering-practice, test/evidence and research-question IDs.
- Link to the authoritative source rather than duplicating it.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.