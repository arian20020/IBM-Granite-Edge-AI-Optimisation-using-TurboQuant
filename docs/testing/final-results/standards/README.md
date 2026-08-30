# Final-results standards

These standards define the portable, machine-readable records used by the unified testing-results library. Canonical rows use the schemas in `schemas/`; reports use the same definitions in human-readable form.

- [Data dictionary](data-dictionary.md) defines record fields and identifier rules.
- [Status taxonomy](status-taxonomy.md) defines normalized tokens and report labels.
- [Metric definitions](metric-definitions.md) defines observation and aggregation semantics.
- [Provenance policy](provenance-policy.md) defines evidence identity and portable paths.
- [Quality comparison policy](quality-comparison-policy.md) defines when scores may be ranked.

The schemas are Draft 2020-12 JSON Schemas. They reject unrecognized top-level fields and retain unavailable observations as `null` rather than converting them to zero.
