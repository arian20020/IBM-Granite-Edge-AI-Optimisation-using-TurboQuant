# Granite + TurboQuant unified testing results

> **Release status — ready with documented limitations.** The five route packages account for all **169 intended attempts**: 81 passed, 6 failed, 28 blocked, and 54 artifact-unavailable. All ordered validation gates pass for the committed clean snapshot at `c154a5461c9d54ae3ad2ccfa141a3a18f564e2a5`. This is a bounded evidence release, not a universal route ranking or a new benchmark run.

## Start here

The [guarded cross-route report](06-cross-route-comparison/workbook/source/cross-route-comparison-final-report.md) is the fastest technical overview. Use its [PDF](06-cross-route-comparison/workbook/generated/cross-route-comparison-final-report.pdf) for review and its [DOCX](06-cross-route-comparison/workbook/generated/cross-route-comparison-final-report.docx) for an editable handoff. Comparisons are direct only when required dimensions match; otherwise they are descriptive or explicitly not comparable.

For audit and reuse:

- [Release readiness evidence](validation/release-readiness.json) records the 202-page PDF review, workbook formula checks, limitations, and clean-snapshot boundary.
- [RO-Crate 1.3 metadata](ro-crate-metadata.json) connects canonical data, system context, and generated reports to their creation activities.
- [Top-level SHA-256 manifest](manifest-sha256.txt) covers every packaged file except itself.
- [Reproduction guide](REPRODUCING.md), [licensing and attribution gaps](LICENSES.md), and [release history](CHANGELOG.md) define the operating boundary.

## Route packages

| Route | Attempt accounting | Canonical report | Editable | PDF | Source workbook |
| --- | --- | --- | --- | --- | --- |
| [Upstream llama.cpp](01-upstream-llama-cpp/route-manifest.json) | 13 passed | [Markdown](01-upstream-llama-cpp/workbook/source/upstream-llama-cpp-final-report.md) | [DOCX](01-upstream-llama-cpp/workbook/generated/upstream-llama-cpp-final-report.docx) | [PDF](01-upstream-llama-cpp/workbook/generated/upstream-llama-cpp-final-report.pdf) | — |
| [AtomicBot TurboQuant](02-atomicbot-turboquant/route-manifest.json) | 19 passed | [Markdown](02-atomicbot-turboquant/workbook/source/atomicbot-turboquant-final-report.md) | [DOCX](02-atomicbot-turboquant/workbook/generated/atomicbot-turboquant-final-report.docx) | [PDF](02-atomicbot-turboquant/workbook/generated/atomicbot-turboquant-final-report.pdf) | — |
| [animehacker tq3-0](03-animehacker-tq3-0/route-manifest.json) | 7 passed, 1 failed, 3 blocked | [Markdown](03-animehacker-tq3-0/workbook/source/animehacker-tq3-0-final-report.md) | [DOCX](03-animehacker-tq3-0/workbook/generated/animehacker-tq3-0-final-report.docx) | [PDF](03-animehacker-tq3-0/workbook/generated/animehacker-tq3-0-final-report.pdf) | — |
| [Experimental OpenVINO fork](04-openvino-experimental-fork/route-manifest.json) | 27 passed, 54 artifact-unavailable | [Markdown](04-openvino-experimental-fork/workbook/source/openvino-experimental-fork-final-report.md) | [DOCX](04-openvino-experimental-fork/workbook/generated/openvino-experimental-fork-final-report.docx) | [PDF](04-openvino-experimental-fork/workbook/generated/openvino-experimental-fork-final-report.pdf) | [XLSX](04-openvino-experimental-fork/results/source/Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx) |
| [Official upstream OpenVINO](05-openvino-official-upstream/route-manifest.json) | 15 passed, 5 failed, 25 blocked | [Markdown](05-openvino-official-upstream/workbook/source/openvino-official-upstream-final-report.md) | [DOCX](05-openvino-official-upstream/workbook/generated/openvino-official-upstream-final-report.docx) | [PDF](05-openvino-official-upstream/workbook/generated/openvino-official-upstream-final-report.pdf) | [XLSX](05-openvino-official-upstream/results/source/Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx) |
| [Guarded cross-route comparison](06-cross-route-comparison/route-manifest.json) | Reconciles all 169 attempts | [Markdown](06-cross-route-comparison/workbook/source/cross-route-comparison-final-report.md) | [DOCX](06-cross-route-comparison/workbook/generated/cross-route-comparison-final-report.docx) | [PDF](06-cross-route-comparison/workbook/generated/cross-route-comparison-final-report.pdf) | — |

## Machine-readable catalogs

- [Route register](catalog/route-register.csv) and [campaign summary](catalog/campaign-summary.csv)
- [Performance summary](catalog/performance-summary.csv), [quality summary](catalog/quality-summary.csv), and [failure summary](catalog/failure-summary.csv)
- [Evidence manifest](catalog/evidence-manifest.csv), [claim-to-evidence map](catalog/claim-evidence-map.csv), and [comparability matrix](catalog/comparability-matrix.csv)

## Interpretation boundary

Availability and complete attempt accounting precede performance comparison. A passed observation is not proof of deployment suitability, clinical safety, educational efficacy, or causal superiority. OpenVINO quality values are published only for passed configurations. Missing artifacts, blocked conversions, failed attempts, different prompts, and unmatched runtime dimensions remain visible rather than being converted to zero or silently discarded.

The top manifest describes the **committed clean release snapshot**. A live worktree containing protected, uncommitted AtomicBot bytes can legitimately fail only the two corresponding top-manifest hashes; those bytes are not silently blessed by this release.
