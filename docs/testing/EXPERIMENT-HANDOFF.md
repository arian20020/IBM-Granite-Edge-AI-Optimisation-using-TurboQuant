# Experiment handoff

This is the starting point for the experiment side of the project.

## What is finished

The frozen inference result package is
[`unified-final-results-2026-09-01-v2`](final-results/README.md). It contains five
inference routes and one guarded comparison. It accounts for all 169 planned
attempts:

| Result | Count |
| --- | ---: |
| Passed | 81 |
| Failed | 6 |
| Blocked by a safety or prerequisite check | 28 |
| Model artefact unavailable | 54 |

The package includes normalised data, reports, evidence links, checksums and
validation records. Failed and blocked work is kept in the record. Missing
measurements have not been changed to zero.

The separate TurboVec study also finished as a feasibility study. The tested
compressed indexes were faster and smaller than Exact search, but none passed
the retrieval-quality gate. The decision is therefore **demonstrator only**;
TurboVec is not approved for application integration from this evidence.

## How to read the results

1. Read the [report pack](EXPERIMENT-REPORT-PACK.md) for the main facts and safe
   wording.
2. Read the [cross-route report](final-results/06-cross-route-comparison/reports/cross-route-comparison-report.md)
   for the full comparison.
3. Use the route report and claim map when checking one result.
4. Use the [research requirement review](EXPERIMENT-TRACEABILITY-REVIEW.md) when
   closing the controlled traceability matrix.

The only direct cross-repository performance and quality comparison is the set
of 15 matched OpenVINO cases. Other route comparisons are descriptive because
their models, prompts, settings or measurement methods differ.

## Check the saved package

Run this from the repository root:

```powershell
python -m scripts.testing.cli.build_results --route all --output-root docs/testing/final-results --validate-only
```

This command reads the saved package. It does not run inference, repeat quality
scoring or change the evidence.

To run the tests for the result-building and validation code:

```powershell
$releaseTests =
  (Get-ChildItem scripts/testing/tests/integration/test_final_results_*.py | ForEach-Object FullName) +
  (Get-ChildItem scripts/testing/tests/acceptance/test_final_results_*.py | ForEach-Object FullName) +
  (Resolve-Path scripts/testing/tests/acceptance/test_build_final_results.py).Path +
  (Resolve-Path scripts/testing/tests/acceptance/test_official_openvino_docx.py).Path
python -m pytest @releaseTests
```

These are software checks. They do not repeat the model experiments.

## What is not complete

- The planned standalone `EXP-TQ-COMP-001` package was not completed. The
  frozen route packages still contain useful matched TurboQuant observations.
- There was no full context-length sweep across every model and cache format.
- The 4 GB and 8 GB memory budgets were not tested on physical 4 GB and 8 GB
  systems. The memory estimator was not calibrated, so false-safe and
  false-unsafe rates are unknown.
- OpenVINO did not have an independent device trace for every case.
- Quality scores from different route families use different tests and must not
  be ranked together.
- The completed large TurboVec rerun used a different Python environment and
  model export from the blocked formal environment. It is supporting evidence,
  not a replacement for the formal record.
- The application evaluation is outside this experiment handoff and is not
  proved by these benchmark files.
- An independent developer reproduction has not been recorded.

## When a new experiment is needed

Do not change raw evidence or reuse an old run ID. A new experiment needs an
approved protocol change, a new run ID, pinned inputs, a separate output folder
and its own evidence record. Keep the current release as the historical
baseline.

## Licence and attribution warning

The result package records source revisions and evidence history, but it does
not grant a licence. Repository-level and route-level reuse rights are not fully
closed. Read [the licence status](final-results/LICENSES.md) before copying or
publishing packaged material, and check the licence of each model, runtime and
third-party source.
