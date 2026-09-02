# Granite + TurboQuant unified testing results

> **Release status — ready with documented limitations.** The five route packages account for all **169 intended attempts**: 81 passed, 6 failed, 28 blocked, and 54 artifact-unavailable. All ordered validation gates pass for self-contained release package `unified-final-results-2026-09-01-v2`. Commit `5083159bc69e4eee456007705fa9fdeef92c8e95` is its distinct input revision, not the completed release identity. This is a bounded evidence release, not a universal route ranking or a new benchmark run.

## Start here

The [guarded cross-route report](06-cross-route-comparison/reports/cross-route-comparison-report.md) is the fastest technical overview. Use its [PDF](06-cross-route-comparison/reports/cross-route-comparison-report.pdf) for review and its [DOCX](06-cross-route-comparison/reports/cross-route-comparison-report.docx) for an editable handoff. Comparisons are direct only when required dimensions match; otherwise they are descriptive or explicitly not comparable.

For audit and reuse:

- [Release readiness evidence](validation/release-readiness.json) records the 202-page PDF review, four workbook audits, self-contained archive validation, and limitations.
- [Cleanup finalization](validation/validation.md) and its [machine receipt](validation/validation.json) reconcile the semantic baseline, verified reduction, migration ledgers, and clean Git archive.
- [RO-Crate 1.3 metadata](ro-crate-metadata.json) connects canonical data, system context, and generated reports to their creation activities.
- [Top-level SHA-256 manifest](manifest-sha256.txt) covers every packaged file except itself.
- [Reproduction guide](REPRODUCING.md), [licensing and attribution gaps](LICENSES.md), and [release history](CHANGELOG.md) define the operating boundary.

## Common route layout

Every numbered route uses the same six-part publication surface: the route `README.md`, generated and source-derived deliverables in `reports/`, canonical normalized records in `data/`, cited material and checksum receipts in `evidence/`, structured and human-readable receipts in `validation/`, and bounded instructions in `reproduction/`. The table links each of those surfaces explicitly; route-specific files may vary where a category is not applicable.

| Route | Accounting | README | Reports | Data | Evidence | Validation | Reproduction |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Upstream llama.cpp | 13 passed | [README](01-upstream-llama-cpp/README.md) | [Markdown](01-upstream-llama-cpp/reports/upstream-llama-cpp-report.md), [DOCX](01-upstream-llama-cpp/reports/upstream-llama-cpp-report.docx), [PDF](01-upstream-llama-cpp/reports/upstream-llama-cpp-report.pdf) | [Route data](01-upstream-llama-cpp/data/route.json) | [Claim map](01-upstream-llama-cpp/evidence/claim-evidence-map.csv) | [Summary](01-upstream-llama-cpp/validation/validation.md) | [Guide](01-upstream-llama-cpp/reproduction/README.md) |
| AtomicBot TurboQuant | 19 passed | [README](02-atomicbot-turboquant/README.md) | [Markdown](02-atomicbot-turboquant/reports/atomicbot-turboquant-report.md), [DOCX](02-atomicbot-turboquant/reports/atomicbot-turboquant-report.docx), [PDF](02-atomicbot-turboquant/reports/atomicbot-turboquant-report.pdf) | [Route data](02-atomicbot-turboquant/data/route.json) | [Claim map](02-atomicbot-turboquant/evidence/claim-evidence-map.csv) | [Summary](02-atomicbot-turboquant/validation/validation.md) | [Guide](02-atomicbot-turboquant/reproduction/README.md) |
| animehacker tq3-0 | 7 passed, 1 failed, 3 blocked | [README](03-animehacker-tq3-0/README.md) | [Markdown](03-animehacker-tq3-0/reports/animehacker-tq3-0-report.md), [DOCX](03-animehacker-tq3-0/reports/animehacker-tq3-0-report.docx), [PDF](03-animehacker-tq3-0/reports/animehacker-tq3-0-report.pdf) | [Route data](03-animehacker-tq3-0/data/route.json) | [Claim map](03-animehacker-tq3-0/evidence/claim-evidence-map.csv) | [Summary](03-animehacker-tq3-0/validation/validation.md) | [Guide](03-animehacker-tq3-0/reproduction/README.md) |
| Experimental OpenVINO fork | 27 passed, 54 artifact-unavailable | [README](04-openvino-experimental-fork/README.md) | [Markdown](04-openvino-experimental-fork/reports/openvino-experimental-fork-report.md), [DOCX](04-openvino-experimental-fork/reports/openvino-experimental-fork-report.docx), [PDF](04-openvino-experimental-fork/reports/openvino-experimental-fork-report.pdf) | [Route data](04-openvino-experimental-fork/data/route.json) | [Claim map](04-openvino-experimental-fork/evidence/claim-evidence-map.csv) | [Summary](04-openvino-experimental-fork/validation/validation.md) | [Guide](04-openvino-experimental-fork/reproduction/README.md) |
| Official upstream OpenVINO | 15 passed, 5 failed, 25 blocked | [README](05-openvino-official-upstream/README.md) | [Markdown](05-openvino-official-upstream/reports/openvino-official-upstream-report.md), [DOCX](05-openvino-official-upstream/reports/openvino-official-upstream-report.docx), [PDF](05-openvino-official-upstream/reports/openvino-official-upstream-report.pdf) | [Route data](05-openvino-official-upstream/data/route.json) | [Claim map](05-openvino-official-upstream/evidence/claim-evidence-map.csv) | [Summary](05-openvino-official-upstream/validation/validation.md) | [Guide](05-openvino-official-upstream/reproduction/README.md) |
| Guarded cross-route comparison | Reconciles all 169 attempts | [README](06-cross-route-comparison/README.md) | [Markdown](06-cross-route-comparison/reports/cross-route-comparison-report.md), [DOCX](06-cross-route-comparison/reports/cross-route-comparison-report.docx), [PDF](06-cross-route-comparison/reports/cross-route-comparison-report.pdf) | [Route data](06-cross-route-comparison/data/route.json) | [Claim map](06-cross-route-comparison/evidence/claim-evidence-map.csv) | [Summary](06-cross-route-comparison/validation/validation.md) | [Guide](06-cross-route-comparison/reproduction/README.md) |

## Machine-readable catalogs

- [Route register](catalog/route-register.csv) and [campaign summary](catalog/campaign-summary.csv)
- [Performance summary](catalog/performance-summary.csv), [quality summary](catalog/quality-summary.csv), and [failure summary](catalog/failure-summary.csv)
- [Evidence manifest](catalog/evidence-manifest.csv), [claim-to-evidence map](catalog/claim-evidence-map.csv), and [comparability matrix](catalog/comparability-matrix.csv)

## Workbook portability and archive boundary

The primary OpenVINO workbooks are sanitized derivatives with stable repository-relative evidence references. Their scientific values, formulas, and sheet structure match the originals:

- Experimental: [portable XLSX](04-openvino-experimental-fork/reports/openvino-experimental-fork-results.xlsx), [provenance](04-openvino-experimental-fork/reports/openvino-experimental-fork-results-provenance.json), and byte-identical [evidence-only source XLSX](04-openvino-experimental-fork/evidence/source/Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx).
- Official: [portable XLSX](05-openvino-official-upstream/reports/openvino-official-upstream-results.xlsx), [provenance](05-openvino-official-upstream/reports/openvino-official-upstream-results-provenance.json), and byte-identical [evidence-only source XLSX](05-openvino-official-upstream/evidence/source/Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx).

The evidence-only originals remain explicitly nonportable because they contain machine-specific absolute paths. The experimental workbook also contains formulas without cached results, so formula cells can appear blank in non-calculating readers until Excel opens and recalculates the workbook; the formulas themselves are preserved in both original and portable copies.

A raw `git archive` of the enclosing release commit contains every byte cited by all five evidence indexes and passes validation without external hydration. No hidden worktree files, Downloads content, or post-extraction copy step are required.

## Interpretation boundary

Availability and complete attempt accounting precede performance comparison. A passed observation is not proof of deployment suitability, clinical safety, educational efficacy, or causal superiority. OpenVINO quality values are published only for passed configurations. Missing artifacts, blocked conversions, failed attempts, different prompts, and unmatched runtime dimensions remain visible rather than being converted to zero or silently discarded.

The top manifest describes canonical Git-index bytes for release package `unified-final-results-2026-09-01-v2` and excludes itself. The manifest plus the enclosing Git commit identifies a concrete release; no metadata file makes a cyclic claim about its own hash.
