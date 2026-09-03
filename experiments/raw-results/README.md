<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Immutable Raw Results

The active, version-controlled evidence library is rooted at `retained/<route>/`.
The canonical evidence namespaces are the five result routes plus `shared`.
The result route names are `upstream-llama-cpp`,
`atomicbot-turboquant`, `animehacker-tq3-0`,
`openvino-experimental-fork`, and `openvino-official-upstream`.

`evidence-manifest.csv` binds every Task 1 `retain_active` or `move_active`
raw-evidence source to exactly one retained path, byte size, SHA-256, stable
evidence IDs, and terminal outcome metadata. `failure-records/` contains the
derived terminal-evidence index, while its unique retained source README lives
under `retained/shared/failure-records/`. The 693 tracked legacy raw paths whose
frozen inventory action is `archive_external` were removed from this
implementation branch only after their 125,662 authoritative bytes were
matched to the inventory and verified external archive. They remain recoverable
from that archive; the parent Git forms remain recoverable from history. The
per-path byte identities and the 128 disclosed EOL-only Git-normalized parent
blobs are recorded in
[`implementation-root-removal-receipt.json`](../../docs/testing/cleanup/implementation-root-removal-receipt.json).
Historical archive candidates are excluded from both the retained tree and its
manifest, and no published evidence citation resolves to a removed path.

Only paths changed during this migration. Captured bytes, evidence IDs,
outcome meanings, and source labels that describe scientific provenance remain
unchanged. Use `docs/testing/cleanup/PATH-MIGRATION.csv` to translate a legacy
raw-evidence path to its canonical retained location.

## Purpose

Unedited stdout, stderr, logs, system measurements, manifests and screenshots from formal experiments.

## What belongs here

- Do not manually change raw files after capture.
- Include failed/cancelled runs.
- Keep secrets and personal paths out.
- Use checksums for irreplaceable packs.

## Related IDs

None assigned

## Evidence rules

- Folder creation is only preparation; it is not proof of completion.
- Use stable requirement, work-package, test/evidence and research-question IDs.
- Preserve raw evidence; derive processed results with version-controlled scripts.
- Record dates, versions, hashes, units, actual device/backend state and failures where relevant.
- Do not commit secrets, API keys, private personal data, large model weights or unlicensed material.
- Prefer relative repository links so evidence remains usable after cloning.

## Source

Repository evidence structure

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Preserves direct outputs from test runs. Start with final-results for conclusions.

### Start here

Begin with [`evidence-manifest.csv`](evidence-manifest.csv). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| [`animehacker-tq3-0/`](animehacker-tq3-0/README.md) | This folder covers the animehacker TQ3_0 route. |
| [`atomicbot-turboquant/`](atomicbot-turboquant/README.md) | This folder covers the AtomicBot TurboQuant route. |
| [`cross-route-comparison/`](cross-route-comparison/README.md) | This folder covers the guarded cross-route comparison. |
| [`custom-openvino-turboquant/`](custom-openvino-turboquant/README.md) | This folder covers the experimental OpenVINO TurboQuant route. |
| [`EXP-OV-OFFICIAL-001/`](EXP-OV-OFFICIAL-001/README.md) | This folder groups the EXP OV OFFICIAL 001 material used by the testing workflow. |
| [`failure-records/`](failure-records/README.md) | Contains raw records describing failed or blocked runs. |
| [`official-openvino/`](official-openvino/README.md) | This folder covers the official OpenVINO route. |
| [`openvino-community/`](openvino-community/README.md) | This folder groups the openvino community material used by the testing workflow. |
| [`openvino-conversion/`](openvino-conversion/README.md) | This folder groups the openvino conversion material used by the testing workflow. |
| [`openvino-official/`](openvino-official/README.md) | This folder groups the openvino official material used by the testing workflow. |
| [`openvino-turboquant/`](openvino-turboquant/README.md) | This folder groups the openvino turboquant material used by the testing workflow. |
| [`quality/`](quality/README.md) | Contains prompts, scoring rules, output indexes and quality-evaluation evidence. |
| [`retained/`](retained/README.md) | Preserves raw evidence moved from older layouts without rewriting its contents. |
| [`turbovec/`](turbovec/README.md) | This folder covers the TurboVec feasibility experiment. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`evidence-manifest.csv`](evidence-manifest.csv) | CSV table with 1400 data row(s). Main columns are `route`, `source_path`, `retained_path`, `size_bytes`, `sha256`, `evidence_ids`, `test_case_id` and 4 more. | Preserved evidence; do not edit |

### Important boundaries

- Treat captured evidence as read-only. Add a new run instead of rewriting an old one.
- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)
- [animehacker-tq3-0 guide](animehacker-tq3-0/README.md)
- [atomicbot-turboquant guide](atomicbot-turboquant/README.md)
- [cross-route-comparison guide](cross-route-comparison/README.md)
- [custom-openvino-turboquant guide](custom-openvino-turboquant/README.md)
- [EXP-OV-OFFICIAL-001 guide](EXP-OV-OFFICIAL-001/README.md)
- [failure-records guide](failure-records/README.md)
- [official-openvino guide](official-openvino/README.md)
- [openvino-community guide](openvino-community/README.md)
- [openvino-conversion guide](openvino-conversion/README.md)
- [openvino-official guide](openvino-official/README.md)
- [openvino-turboquant guide](openvino-turboquant/README.md)
- [quality guide](quality/README.md)
- [retained guide](retained/README.md)
- [turbovec guide](turbovec/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
