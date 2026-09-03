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
  and 54 artifact-unavailable. It retains 1,857 evidence relationship records
  with 1,857 unique evidence IDs across 1,846 distinct cited evidence paths,
  six Markdown/DOCX parity pairs, and six PDFs totalling 202 pages. In the
  frozen semantic-snapshot schema, the legacy field `relationship_count=1846`
  is explicitly the unique-path cardinality; the `relationships` array contains
  all 1,857 records.
- Both primary portable OpenVINO XLSX derivatives open read-only and contain
  zero machine-specific absolute paths. The immutable source workbooks remain
  evidence-only and byte-identical.
- The comparison matrix and every recorded comparison decision are unchanged.
  Cleanup changes path presentation and metadata, not route comparability.
- [`before-after-summary.md`](before-after-summary.md) reconciles all 5,057
  inventoried paths and 1,977,743,410 bytes to the 1,529-path removal receipt
  and the 3,528-path retained recovery snapshot. It separately reconciles the
  final implementation-branch removal of the 693 tracked legacy raw paths whose
  frozen disposition was `archive_external`.
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
| [`implementation-root-removal-receipt.json`](implementation-root-removal-receipt.json) | Final implementation-branch removal of the exact 693 tracked `archive_external` raw paths, including authoritative archive bytes and parent-Git identities. |
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
[`removal-receipt.json`](removal-receipt.json). The external archive is also the
authoritative byte-recovery location for the 693 implementation-root removals;
the parent commit and per-entry identities in
[`implementation-root-removal-receipt.json`](implementation-root-removal-receipt.json)
preserve the corresponding tracked-history forms.

No benchmark, inference, quality adjudication, Microsoft Word export, semantic
evidence regeneration, or semantic evidence mutation was performed during
finalization. The Git boundary did admit EOL-only canonical bytes for scoped
frozen text sources—including the quality register, frozen prompt files, and
official OpenVINO retest matrix—so a plain archive preserves their pre-existing
authoritative working bytes. That byte-level admission changed no scientific
field or meaning. Pre-existing untracked scientific work was outside the cleanup
transaction and was left untouched.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Records how testing material was reorganised and how removals were checked.

### Start here

Begin with [`baseline-summary.md`](baseline-summary.md). The tables below explain the remaining items.

### How this folder fits into testing

This folder supports the controlled path from a test requirement to evidence, validation and a bounded conclusion.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`archive-plan.csv`](archive-plan.csv) | CSV table with 1128 data row(s). Main columns are `source_root_id`, `source_root`, `source_branch`, `source_head`, `source_status_sha256`, `source_path`, `archive_path` and 3 more. | Supporting repository file |
| [`archive-summary.json`](archive-summary.json) | Stores a JSON object with top-level fields `archive_manifest_sha256`, `archive_root`, `entry_count`, `plan_sha256`, `receipt_path`, `receipt_sha256`, `schema`, `total_bytes`, …. | Supporting repository file |
| [`baseline-semantic-snapshot.json`](baseline-semantic-snapshot.json) | Stores a JSON object with top-level fields `comparability`, `evidence`, `outcomes`, `pdfs`, `reports`, `schema`, `scientific_tables`, `snapshot_sha256`. | Supporting repository file |
| [`baseline-summary.md`](baseline-summary.md) | Summarises the testing trees and duplication observed before cleanup began. | Cleanup baseline |
| [`before-after-summary.md`](before-after-summary.md) | Reconciles the pre-cleanup and post-cleanup layouts and explains what moved, remained or was archived. | Cleanup verification record |
| [`clean-archive-validation.json`](clean-archive-validation.json) | Stores a JSON object with top-level fields `schema`, `valid`, `cleanup_version`, `release_version`, `base_commit`, `review_fix_input_commit`, `final_branch_fix_input_commit`, `current_revision_fix_input_commit`, …. | Supporting repository file |
| [`duplicate-groups.csv`](duplicate-groups.csv) | CSV table with 1604 data row(s). Main columns are `duplicate_group`, `path`, `sha256`, `size_bytes`, `action`. | Supporting repository file |
| [`file-inventory.csv`](file-inventory.csv) | CSV table with 5057 data row(s). Main columns are `source_root_id`, `path`, `tracked_status`, `size_bytes`, `sha256`, `route`, `test_case_id` and 8 more. | Supporting repository file |
| [`final-semantic-snapshot.json`](final-semantic-snapshot.json) | Stores a JSON object with top-level fields `comparability`, `evidence`, `outcomes`, `pdfs`, `reports`, `schema`, `scientific_tables`, `snapshot_sha256`. | Supporting repository file |
| [`implementation-root-removal-receipt.json`](implementation-root-removal-receipt.json) | Stores a JSON object with top-level fields `schema`, `valid`, `cleanup_version`, `release_version`, `input_commit`, `input_tree`, `inventory`, `external_archive`, …. | Supporting repository file |
| [`inventory-metadata.json`](inventory-metadata.json) | Stores a JSON object with top-level fields `canonical_branch`, `canonical_head`, `canonical_root`, `canonical_status_capture_phase`, `canonical_status_sha256`, `record_count`, `schema`, `scopes`, …. | Supporting repository file |
| [`PATH-MIGRATION.csv`](PATH-MIGRATION.csv) | CSV table with 1572 data row(s). Main columns are `old_path`, `new_path`, `reason`. | Supporting repository file |
| [`post-move-pytest-collection.txt`](post-move-pytest-collection.txt) | Plain-text evidence or diagnostic output for post move pytest collection; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`pre-move-pytest-collection.txt`](pre-move-pytest-collection.txt) | Plain-text evidence or diagnostic output for pre move pytest collection; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`removal-receipt.json`](removal-receipt.json) | Stores a JSON object with top-level fields `action_counts`, `archive_entry_count`, `archive_manifest_sha256`, `archive_plan_path`, `archive_plan_sha256`, `archive_receipt_path`, `archive_receipt_sha256`, `archive_root`, …. | Supporting repository file |
| [`source-canonical-deltas.csv`](source-canonical-deltas.csv) | CSV table with 5156 data row(s). Main columns are `path`, `state`, `source_sha256`, `canonical_sha256`, `classification`. | Supporting repository file |
| [`test-path-migration.csv`](test-path-migration.csv) | CSV table with 94 data row(s). Main columns are `source_path`, `destination_path`, `bucket`. | Supporting repository file |

### Important boundaries

- Check the file's status and evidence links before treating it as a current result.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
