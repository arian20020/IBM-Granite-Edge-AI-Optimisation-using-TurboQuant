# TurboVec production-scale campaign v2

This folder is the human-readable index for campaign `turbovec-production-scale-final-evaluation-v2` and experiment `EXP-TV-COMP-001`.

Start with [final-research-report.md](final-research-report.md). The machine-readable records are under `experiments/raw-results/turbovec/production-scale-v2`, while frozen corpus, query and relevance inputs are under `experiments/protocols/turbovec`.

| Need | Read |
|---|---|
| Overall outcome and scope | `final-research-report.md` |
| Exact identities and evidence inventory | `environment-manifest.json`, `evidence-manifest.json` |
| Results and repetition order | `per-scale-results.md` and the run's `evaluation.json` |
| Storage and RAM interpretation | `storage-memory-accounting.md` |
| Admission failures | `machine-readiness-report.md` and each readiness directory |
| PDF behaviour | `pdf-extraction-report.md` |
| Windows path limitation | `windows-long-path-investigation.md` |
| Python/Rust checks | `upstream-verification-report.md` |
| Acceptance decisions | `gate-a-audit.md`, `gate-b-audit.md`, `final-decision.md` |
| Re-running the work | `reproduction-guide.md` |
| Known limitations | `threats-to-validity.md` |
| Handoff boundary | `handoff-receipt.md` |

No file in this campaign integrates TurboVec into the application.
