---
title: "Provenance Validation Report"
status: "passed"
version: "2.0"
last_updated: "2026-07-14"
---

# Provenance validation report

## Automated checks

| Check | Result | Details |
|---|---|---|
| External source IDs are unique | PASS | 52 unique source IDs |
| Curated technical files have `source_ids` | PASS | 36 files checked |
| Curated technical files have `Sources used` | PASS | 36 files checked |
| Every used source ID exists in the catalogue | PASS | 47 distinct IDs used |
| Claim matrix exists | PASS | 33 important claims mapped |
| Machine-readable source catalogue exists | PASS | 52 sources registered |
| Private SharePoint URL removed from curated research | PASS | Original link remains only in the lossless source layer |
| Original DOCX preservation | PASS | 44 original DOCX files verified by SHA-256 in the controlled provenance ZIP; Git stores the archive notice rather than the binaries |
| Full extract preservation | PASS | 102 files in the extract layer, including the new provenance note |

## Exceptions

- Unknown source IDs: None
- Files missing source frontmatter: None
- Files missing source sections: None
- Curated files containing private SharePoint URLs: None

## Result

**PASS** — the provenance structure is internally consistent. This is a structural validation, not a substitute for reproducing paper or repository results on the target Intel hardware.
