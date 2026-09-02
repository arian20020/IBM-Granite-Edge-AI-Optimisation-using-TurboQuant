<!-- IXN-EVIDENCE-STRUCTURE: GENERATED -->
# Immutable Raw Results

The active, version-controlled evidence library is rooted at `retained/<route>/`.
The five canonical route names are `upstream-llama-cpp`,
`atomicbot-turboquant`, `animehacker-tq3-0`,
`openvino-experimental-fork`, and `openvino-official-upstream`.

`evidence-manifest.csv` binds every Task 1 `retain_active` or `move_active`
raw-evidence source to exactly one retained path, byte size, SHA-256, stable
evidence IDs, and terminal outcome metadata. `failure-records/` indexes the
unique artifacts that support failed, blocked, or artifact-unavailable
outcomes. Historical archive candidates are deliberately excluded from both
the retained tree and its manifest.

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
