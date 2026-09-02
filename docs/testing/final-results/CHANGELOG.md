# Release history

## 2026-09-02 — Cleanup finalization and clean-archive validation

- Proved byte-for-byte equality between the frozen baseline and final semantic
  snapshots: 169 outcomes (81 passed, 6 failed, 28 blocked, and 54
  artifact-unavailable), 1,857 evidence relationship records across 1,846
  distinct cited evidence paths, six report parity pairs, six PDFs, and 202 PDF
  pages. The frozen `relationship_count=1846` field denotes unique-path
  cardinality; the `relationships` array contains 1,857 records.
- Reconciled the frozen 5,057-path, 1,977,743,410-byte recovery inventory to the
  verified 1,529-path, 18,265,409-byte removal transaction and the retained
  3,528-path, 1,959,478,001-byte snapshot.
- Verified 1,128 archived historical paths byte-for-byte, limited the remaining
  401 removals to regenerable Python bytecode caches, and confirmed every
  selected source path is absent.
- Rendered and inspected all 202 PDF pages with PyMuPDF, opened both portable
  XLSX derivatives read-only with openpyxl, and reconfirmed zero machine-specific
  absolute paths in the portable workbooks.
- Validated an untouched short-path Git archive without evidence hydration and
  recorded all 13 ordered release gates in the cleanup receipt.
- Refreshed collection validation, release readiness, RO-Crate provenance, and
  the canonical staged-blob manifest after the final path set settled.

No benchmark, inference, quality adjudication, Microsoft Word export, semantic
evidence regeneration, or semantic evidence mutation was performed. Scoped
frozen text sources received an EOL-only canonical-byte admission so plain Git
archives preserve their pre-existing authoritative bytes; no scientific field
or meaning changed. The scientific release version remains
`unified-final-results-2026-09-01-v2`; finalization records cleanup version
`testing-results-cleanup-2026-09-02-v1`.

## 2026-09-02 — Compact publication layout cleanup

- Recorded cleanup version `testing-results-cleanup-2026-09-02-v1` for the path-only and metadata refresh of release package `unified-final-results-2026-09-01-v2`.
- Standardized all six published routes on the same `README.md`, `reports/`, `data/`, `evidence/`, `validation/`, and `reproduction/` layout.
- Refreshed the portal, canonical-data catalogs, RO-Crate activities, validation receipts, migration ledger, supported CLI commands, and canonical staged-byte manifest for the settled paths.
- Preserved exact attempt accounting, scientific values, evidence bytes, comparison boundaries, licensing gaps, and authorship limitations.

No benchmark, inference, conversion, quality adjudication, Microsoft Word export, or evidence regeneration was run for this cleanup. The scientific release version remains `unified-final-results-2026-09-01-v2`.

## 2026-09-01 — Unified testing results library v2

- Made the repository release self-contained: a raw Git archive now includes every exact source byte cited by all five evidence indexes and validates without external hydration.
- Admitted 173 previously untracked cited sources, two identical-hash route-regeneration aliases, and five exact controlled testing-register revisions after hash, size, history-scope, and sensitive-content checks.
- Retained the two original OpenVINO XLSX files byte-identically as evidence-only/nonportable sources, and added deterministic portable derivatives plus provenance receipts as the primary workbook handoff.
- Recorded zero machine-specific absolute paths in the portable derivatives and preserved their scientific values, formulas, sheets, dimensions, and structural features.
- Disclosed that experimental formula cells can appear blank in non-calculating readers until Excel recalculation because the source contains no cached formula results.
- Revalidated all 13 ordered gates from an unhydrated Git archive and refreshed route/release manifests, RO-Crate coverage, readiness, and portal language.

No benchmark was rerun and no source evidence byte was modified for v2. Package version `unified-final-results-2026-09-01-v2`, its manifest, and the enclosing Git commit identify the release; commit `5083159bc69e4eee456007705fa9fdeef92c8e95` is the distinct input revision.

## 2026-09-01 — Unified testing results library

- Published an answer-first release portal spanning five route packages and the guarded cross-route comparison.
- Added RO-Crate 1.3 JSON-LD metadata with relative packaged-data identifiers and explicit generation provenance.
- Added a complete, sorted, self-excluding top-level SHA-256 manifest generated from canonical staged Git blobs for archive portability.
- Recorded page-by-page visual and searchable-text QA for six PDFs (202 pages total).
- Recorded read-only formula, sheet, external-reference, and literal-error QA for both OpenVINO workbooks.
- Promoted release metadata to a blocking validator gate and retained licensing, citation, comparability, and reproduction limitations as first-class evidence.

No benchmark was rerun and no raw evidence was modified for this release.
The release is identified by package version `unified-final-results-2026-09-01`, its manifest, and the enclosing Git commit; Task 13 commit `c154a5461c9d54ae3ad2ccfa141a3a18f564e2a5` is the input revision only.
