# Controlled Testing Workbooks

The six workbooks preserve the route order, controlled test IDs, planned configurations and result sections defined by the supplied testing material.

## Canonical sources

The Git-reviewable workbook content is stored under `text-templates/`. The append-only document history is stored in `../Workbook-Revision-Register.csv`. Generated DOCX files are working/reporting artefacts and are not the only authoritative record.

## Current versions

| Workbook | Version | Technical scope |
|---|---:|---|
| WB-01 Upstream llama.cpp | 1.1 | Original controlled baseline plus formal revision history |
| WB-02 AtomicBot TurboQuant | 1.1 | Original controlled route plus formal revision history |
| WB-03 animehacker TQ3_0 | 1.1 | Original controlled comparator plus formal revision history |
| WB-04 Official OpenVINO | 1.2 | PR #5 TBQ3/TBQ4 coverage plus formal revision history |
| WB-05 Custom OpenVINO | 1.2 | PR #5 TBQ/QJL/Polar/all-pairs coverage plus formal revision history |
| WB-06 Cross-route comparison | 1.2 | PR #5 expanded comparisons plus formal revision history |

The v1.1 changes for WB-01–WB-03 and v1.2 changes for WB-04–WB-06 are administrative change-control revisions. They do not add tests or imply test success.

## Generate and validate

```powershell
python -m pip install -r .\scripts\testing\requirements.txt
python .\scripts\testing\Generate-Controlled-Workbooks.py
python .\scripts\testing\Apply-Workbook-Revision-History.py
python .\scripts\testing\Validate-Workbook-Revision-Control.py
```

The first command generates the content, the second inserts the synchronized revision table and current version, and the third checks the register, hashes and generated documents.

## Completion rule

A workbook result section is complete only when its run ID, processed-result path, raw-evidence path and evidence commit are recorded in `../Workbook-Completion-Register.csv`. Important measured values must first be stored in the relevant machine-readable register rather than only typed into Word.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains controlled workbook templates, generated copies and workbook indexes.

### Start here

Begin with [`Controlled-Workbook-Manifest.csv`](Controlled-Workbook-Manifest.csv). The tables below explain the remaining items.

### How this folder fits into testing

This folder supports the controlled path from a test requirement to evidence, validation and a bounded conclusion.

### Folders

| Folder | What it contains |
| --- | --- |
| [`generated/`](generated/README.md) | Contains files produced from controlled templates. Edit the source template or generator when possible. |
| [`text-templates/`](text-templates/README.md) | Contains readable Markdown sources used to generate workbook documents. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`Controlled-Workbook-Manifest.csv`](Controlled-Workbook-Manifest.csv) | CSV table with 6 data row(s). Main columns are `Workbook_ID`, `Controlled_File`, `Canonical_Text_Template`, `Source_File`, `Source_SHA256`, `Canonical_Template_SHA256`, `Last_Validated_DOCX_SHA256` and 5 more. | Supporting repository file |

### Important boundaries

- Check the file's status and evidence links before treating it as a current result.

### Related guides

- [Parent guide](../README.md)
- [generated guide](generated/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
