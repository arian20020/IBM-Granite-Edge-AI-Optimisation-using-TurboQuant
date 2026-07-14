# Controlled Testing Workspace

**Campaign:** Granite-TurboQuant Controlled Retest Campaign v1  
**Started:** 13 July 2026  
**Status:** Authoritative testing campaign  
**Previous campaign:** Preserved as legacy evidence; not automatically authoritative

This directory controls application testing, model/runtime feasibility, AI-quality evaluation and final cross-route comparison.

## Start here

1. Read `Validation-Audit-2026-07-13.md`.
2. Read `Test-Strategy.md` and `Master-Test-Plan.md`.
3. Check `Test-ID-Catalogue.md` before running anything.
4. Read `Workbook-Data-Requirements.md` so every workbook field can be completed from captured evidence.
5. Read `Experiment-and-Test-Change-Control-Index.md` before changing a test, run record or workbook.
6. Create the run ID and evidence directory before execution.
7. Update the registers and workbook before starting the next test.

## Exact source control

The supplied folder contained nineteen DOCX files, including the testing standard, ten feasibility-plan documents, seven workbook files and one engineering journal. Their original archive paths, sizes and SHA-256 values are recorded in `source-material/Source-Document-Manifest.csv`.

The six active route workbooks preserve the source route order and exact test IDs:

1. Upstream llama.cpp.
2. AtomicBot TurboQuant.
3. animehacker TQ3_0.
4. Official OpenVINO.
5. Custom OpenVINO TurboQuant.
6. Cross-route comparison.

The additional completed OpenVINO-with-TurboQuant workbook is a legacy evidence/reference document, not the blank controlled template.

## Main controls

- `Test-Strategy.md` — evidence, repeatability and claim rules.
- `Master-Test-Plan.md` — route order, stage gates and stop conditions.
- `Test-ID-Catalogue.md` — exact workbook IDs.
- `Workbook-Data-Requirements.md` — complete data needed for all workbook fields.
- `Experiment-and-Test-Change-Control-Index.md` — entry point for experiment, run and workbook change control.
- `Test-Traceability-Matrix.csv` — research questions and requirements mapped to tests.
- `Test-Run-Register.csv` — one row per execution.
- `Environment-Register.csv` — target machine and software state.
- `Repository-Register.csv`, `Build-Register.csv`, `Model-Register.csv` and `Configuration-Register.csv` — pinned test inputs.
- `Device-Verification-Register.csv` — requested versus actual execution, placement and fallback.
- `Performance-Measurement-Register.csv` — pilot, warm-up and measured repetitions.
- `Quality-Evaluation-Register.csv` — P1-P6 deterministic checks and weighted scoring.
- `Failure-Register.csv` — failure, diagnosis, fix and retest evidence.
- `Evidence-Index.csv` — file-level provenance and hashes.
- `Workbook-Revision-Control.md` and `Workbook-Revision-Register.csv` — append-only workbook revision procedure and history.
- `Workbook-Completion-Register.csv` — section-by-section completion control.
- `Cross-Route-Comparison-Register.csv` — matched, partially matched and non-comparable results.
- `Decision-Log.md` — testing and interpretation decisions.

## Evidence root

Formal campaign evidence belongs under:

`experiments/granite_turboquant_intel/`

The source operational structure is:

```text
manifests/
configurations/
scripts/
logs/
outputs/
metrics/
results/
notes/
```

## Non-negotiable rules

1. Every test exists in the catalogue before execution.
2. Every execution receives a unique run ID.
3. Environment, repository commit, build, model hash, configuration, prompt and command are recorded exactly.
4. Raw evidence is immutable.
5. Failed, blocked and inconclusive runs remain visible.
6. Requested backend, device and optimisation are checked against actual runtime behaviour.
7. Important results are entered in machine-readable registers before being copied into Word.
8. A workbook row is incomplete until it links to a run ID, evidence path and evidence commit.
9. Formal benchmarking uses a pilot, one excluded warm-up and at least three measured repetitions unless a documented safety gate prevents it.
10. Quality is reported on the controlled 0-10 rubric.
11. Model weights, secrets, build caches and copied third-party repositories are not committed.

## Status vocabulary

`Planned`, `Ready`, `Running`, `Passed`, `Failed`, `Blocked`, `Inconclusive`, `Superseded`, `Not supported`, `Not measured`, `Not applicable`.

## Evidence flow

`source requirement -> test ID -> run ID -> manifest -> raw evidence -> processed result -> register -> workbook -> conclusion`

Folder creation is preparation, not evidence that a test passed.
