# Historical Testing Code Archive

`archive/testing-code/2026-09-01/` preserves superseded root-level testing scripts exactly as they existed before the cleanup moved maintained code under `scripts/testing/cli`, `scripts/testing/campaigns`, and `scripts/testing/tools`.

Use [MIGRATION.csv](./MIGRATION.csv) as the authority for every tracked Task 1 root script:

- `canonical_cli` rows point to the supported contributor-facing command surface.
- `campaign_module` rows point to maintained route implementation modules.
- `active_tool` rows point to maintained internal utilities and evidence-management tools.
- `archived_code` rows preserve historical shims that are no longer maintained entrypoints.

This archive is read-only historical context. It does not authorize rerunning benchmarks or rewriting preserved scientific evidence.
