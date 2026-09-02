# Testing-results cleanup receipts

This directory is the audit trail for cleanup version
`testing-results-cleanup-2026-09-02-v1`. The cleanup reorganized and reduced the
testing-results working set without changing the scientific release version
`unified-final-results-2026-09-01-v2`, its 169 attempt outcomes, its evidence
relationships, or its guarded comparison decisions.

## Final result

- [`baseline-semantic-snapshot.json`](baseline-semantic-snapshot.json) and
  [`final-semantic-snapshot.json`](final-semantic-snapshot.json) are byte-for-byte
  identical. Each file has SHA-256
  `671bb80ed06438142216b6a1bfda03da39dbb4de7a5359efcaabebf74f70745d`;
  the snapshot's canonical self-hash is
  `e1415852768a586c51692a72f41605fd6334afd640c6bceae2f7e20d17ef5aa6`.
- The settled package retains 169 outcomes: 81 passed, 6 failed, 28 blocked,
  and 54 artifact-unavailable. It retains 1,846 distinct cited evidence paths,
  six Markdown/DOCX parity pairs, and six PDFs totalling 202 pages.
- Both primary portable OpenVINO XLSX derivatives open read-only and contain
  zero machine-specific absolute paths. The immutable source workbooks remain
  evidence-only and byte-identical.
- The comparison matrix and every recorded comparison decision are unchanged.
  Cleanup changes path presentation and metadata, not route comparability.
- [`before-after-summary.md`](before-after-summary.md) reconciles all 5,057
  inventoried paths and 1,977,743,410 bytes to the 1,529-path removal receipt
  and the 3,528-path retained recovery snapshot.
- [`clean-archive-validation.json`](clean-archive-validation.json) binds the
  final no-hydration validation to an immutable staged Git tree and records all
  13 ordered release gates. The receipt excludes only itself from the declared
  content scope, avoiding a self-referential hash.

## Receipt map

| Receipt | Purpose |
| --- | --- |
| [`inventory-metadata.json`](inventory-metadata.json) and [`file-inventory.csv`](file-inventory.csv) | Frozen Task 1 inventory and source-root identities. |
| [`baseline-summary.md`](baseline-summary.md) and [`baseline-semantic-snapshot.json`](baseline-semantic-snapshot.json) | Pre-cleanup semantic baseline. |
| [`duplicate-groups.csv`](duplicate-groups.csv) and [`source-canonical-deltas.csv`](source-canonical-deltas.csv) | Duplicate analysis and source/canonical difference decisions. |
| [`PATH-MIGRATION.csv`](PATH-MIGRATION.csv) | Complete published-path migration ledger. |
| [`test-path-migration.csv`](test-path-migration.csv) | Complete retained-test destination ledger. |
| [`archive-plan.csv`](archive-plan.csv), [`archive-summary.json`](archive-summary.json), and [`removal-receipt.json`](removal-receipt.json) | Planned, archived, verified, and removed path bindings. |
| [`pre-move-pytest-collection.txt`](pre-move-pytest-collection.txt) and [`post-move-pytest-collection.txt`](post-move-pytest-collection.txt) | Frozen pre/post test-identity proof for the move campaign. |
| [`final-semantic-snapshot.json`](final-semantic-snapshot.json) | Post-cleanup semantic reconciliation against the frozen baseline. |
| [`clean-archive-validation.json`](clean-archive-validation.json) | Untouched short-path Git-archive identity and 13-gate receipt. |

The collection-level machine and human finalization receipts are
[`validation.json`](../final-results/validation/validation.json) and
[`validation.md`](../final-results/validation/validation.md).

## Authority and safety boundary

The published package is identified by its release version, canonical
top-level manifest, and enclosing Git tree/commit. A live working tree is not
the release authority. Historical files selected for preservation are bound to
the external archive receipt summarized by [`archive-summary.json`](archive-summary.json);
regenerable removals are limited to the exact cache rule recorded in
[`removal-receipt.json`](removal-receipt.json).

No benchmark, inference, quality adjudication, Microsoft Word export, evidence
regeneration, or source-evidence mutation was performed during finalization.
Pre-existing untracked scientific work was outside the cleanup transaction and
was left untouched.
