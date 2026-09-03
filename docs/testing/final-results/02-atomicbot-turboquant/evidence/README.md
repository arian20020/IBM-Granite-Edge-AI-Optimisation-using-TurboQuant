# Evidence

Connects published claims to the source material and checksums that support them. Here it applies to the AtomicBot TurboQuant route.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Connects published claims to the source material and checksums that support them. Here it applies to the AtomicBot TurboQuant route.

### Start here

Begin with [`claim-evidence-map.csv`](claim-evidence-map.csv). The tables below explain the remaining items.

### How this folder fits into testing

This folder is part of the curated publication layer: evidence has been normalised, linked and validated for review.

### Folders

| Folder | What it contains |
| --- | --- |
| [`failures/`](failures/README.md) | Preserves failed or blocked outcomes so they are not hidden from the final record. Here it applies to the AtomicBot TurboQuant route. |
| [`source/`](source/README.md) | Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the AtomicBot TurboQuant route. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`claim-evidence-map.csv`](claim-evidence-map.csv) | Maps each published claim to the evidence that supports or limits it. | Supporting repository file |
| [`evidence-index.csv`](evidence-index.csv) | Lists evidence files and their provenance or checksum details. | Supporting repository file |
| [`manifest-sha256.txt`](manifest-sha256.txt) | SHA-256 checksums used to detect missing or changed packaged files. | Supporting repository file |
| [`source-locations.csv`](source-locations.csv) | CSV table with 286 data row(s). Main columns are `evidence_id`, `relative_path`, `source_or_derived`. | Supporting repository file |

### Important boundaries

- A missing, blocked or unavailable result is not zero and must not be compared as if it passed.
- Use cross-route performance comparisons only when the published comparability matrix marks them as eligible.

### Related guides

- [Parent guide](../README.md)
- [failures guide](failures/README.md)
- [source guide](source/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
