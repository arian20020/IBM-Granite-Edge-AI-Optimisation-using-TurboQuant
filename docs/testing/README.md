# Controlled Testing Workspace

**Campaign:** Granite-TurboQuant Controlled Retest Campaign v1  
**Started:** 13 July 2026  
**Status:** Authoritative testing campaign  
**Previous campaign:** Preserved as legacy evidence; not automatically authoritative

This directory controls application testing, model/runtime feasibility, AI-quality evaluation and final cross-route comparison.

## Start here

1. Read `Validation-Audit-2026-07-13.md`.
2. Read `Test-Strategy.md` and `Master-Test-Plan.md`.
3. Read `App-Specific-Evaluation-Addendum-v1.md` for the frozen mapping from remaining evaluations to RQs, stable IDs, schemas, prompts/rubric, gates and stopping rules.
4. Check `Test-ID-Catalogue.md` before running anything.
5. Read `Workbook-Data-Requirements.md` so every workbook field can be completed from captured evidence.
6. Read `Experiment-and-Test-Change-Control-Index.md` before changing a test, run record or workbook.
7. Create the run ID and evidence directory before execution.
8. Update the registers and workbook before starting the next test.

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
- `App-Specific-Evaluation-Addendum-v1.md` — remaining evaluation IDs, RQ mapping, frozen inputs, schemas, measures, gates, stopping rules and claim boundary.
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

## Hardware Inspection Intel runner controls

- [Stage 0 operator runbook](runbooks/Hardware-Inspection-Intel-Runner-Stage-0-Runbook.md) - hosted repository-identity validation only.
- [Stage A operator runbook](runbooks/Hardware-Inspection-Intel-Runner-Stage-A-Runbook.md) - separately authorised, permission-gated deterministic evidence on one ephemeral UCL-approved runner.
- [Stage A context validator](../../scripts/hardware-inspection/Validate-HardwareInspectionIntelRunnerStageA.ps1) - validates the fixed dispatch, manifest, runner, checkout, and output boundary.
- [Stage A deterministic runner](../../scripts/hardware-inspection/Invoke-HardwareInspectionIntelRunnerStageA.ps1) - owns contained local restore/build/test, strict TRX validation, cleanup, and privacy-safe summaries.

Stage A is not hardware, candidate, trusted-Intel, offline, or Gate 1 evidence. It does not change any current evidence claim: `F-M07`, `HE-01`, and `HE-02` are not verified, Gate 1 remains Blocked, and Gate 2 and Stage B remain prohibited without separate approval.

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

1. Every test exists in the catalogue or frozen app-specific addendum before execution.
1. Every execution ID exists in the route catalogue or frozen app-specific addendum before execution.
2. Every execution receives a unique run ID.
3. Environment, repository commit, build, model hash, configuration, prompt and command are recorded exactly.
4. Raw evidence is immutable.
5. Failed, blocked and inconclusive runs remain visible.
6. Requested backend, device and optimisation are checked against actual runtime behaviour.
7. Important results are entered in machine-readable registers before being copied into Word.
8. A workbook row or app-evaluation report is incomplete until it links to a run ID, evidence path and evidence commit.
9. Formal benchmarking uses a pilot, one excluded warm-up and at least three measured repetitions unless a documented safety gate prevents it.
10. Quality is reported on the controlled 0-10 rubric.
11. Model weights, secrets, build caches and copied third-party repositories are not committed.

## Status vocabulary

`Planned`, `Ready`, `Running`, `Passed`, `Failed`, `Blocked`, `Inconclusive`, `Superseded`, `Not supported`, `Not measured`, `Not applicable`.

## Evidence flow

`source requirement -> evaluation/test ID -> run ID -> manifest -> raw evidence -> processed result -> register -> workbook/test report -> conclusion`

Folder creation is preparation, not evidence that a test passed.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This is the main control centre for the project's testing work.

### Start here

For final benchmark conclusions, open [`final-results/README.md`](final-results/README.md). For campaign rules, begin with `Test-Strategy.md` and `Master-Test-Plan.md`.

### How this folder fits into testing

This folder supports the controlled path from a test requirement to evidence, validation and a bounded conclusion.

### Folders

| Folder | What it contains |
| --- | --- |
| [`cleanup/`](cleanup/README.md) | Records how testing material was reorganised and how removals were checked. |
| [`failure-evidence/`](failure-evidence/README.md) | Contains evidence and explanations for failed, blocked or inconclusive tests. |
| [`final-results/`](final-results/README.md) | Contains the curated, validated result packages used for final reporting. |
| [`legacy/`](legacy/README.md) | Preserves older material for history. It is not automatically the current source of truth. |
| [`manual/`](manual/README.md) | Contains instructions for checks that require a person rather than an automated test. |
| [`plans/`](plans/README.md) | Contains approved plans for testing work that may be complete, active or still proposed. |
| [`runbooks/`](runbooks/README.md) | Contains step-by-step operating instructions for controlled test runs. |
| [`source-material/`](source-material/README.md) | Records the original supplied testing documents and their identity. |
| [`strategies/`](strategies/README.md) | Explains the high-level testing approach and evidence rules. |
| [`test-reports/`](test-reports/README.md) | Contains reports from application or repository test activities. |
| [`ux-evaluation/`](ux-evaluation/README.md) | Contains the planned application usability and accessibility evaluation material. |
| [`workbook05/`](workbook05/README.md) | Contains controlled helpers for Workbook 05 source admission, build and measurement stages. |
| [`workbooks/`](workbooks/README.md) | Contains controlled workbook templates, generated copies and workbook indexes. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`Animehacker-Large-Host-Completion-Guide.md`](Animehacker-Large-Host-Completion-Guide.md) | Readable Markdown document titled “Animehacker WB-03 Large-Host Completion Guide”. | Supporting repository file |
| [`App-Specific-Evaluation-Addendum-v1.md`](App-Specific-Evaluation-Addendum-v1.md) | Readable Markdown document titled “App-Specific Evaluation Addendum”. | Supporting repository file |
| [`Build-Register.csv`](Build-Register.csv) | CSV table with 3 data row(s). Main columns are `Build_Check_ID`, `Route`, `Workbook_ID`, `Repository_ID`, `Build_ID`, `Environment_ID`, `Check_Title` and 26 more. | Supporting repository file |
| [`Configuration-Register.csv`](Configuration-Register.csv) | CSV table with 0 data row(s). Main columns are `Configuration_ID`, `Test_ID`, `Route`, `Workbook_ID`, `Model`, `Model_ID`, `Weights` and 22 more. | Supporting repository file |
| [`Cross-Route-Comparison-Register.csv`](Cross-Route-Comparison-Register.csv) | CSV table with 0 data row(s). Main columns are `Comparison_ID`, `Criterion`, `Source_Route`, `Source_Test_ID`, `Source_Run_ID`, `Source_Configuration_ID`, `Target_or_Baseline_Route` and 28 more. | Supporting repository file |
| [`Decision-Log.md`](Decision-Log.md) | Readable Markdown document titled “Testing Decision Log”. | Supporting repository file |
| [`Device-Verification-Register.csv`](Device-Verification-Register.csv) | CSV table with 19 data row(s). Main columns are `Test_ID`, `Route`, `Workbook_ID`, `Latest_Run_ID`, `Requested_Device`, `Actual_Device`, `Backend` and 20 more. | Supporting repository file |
| [`Environment-Register.csv`](Environment-Register.csv) | CSV table with 2 data row(s). Main columns are `Environment_ID`, `Capture_Timestamp_UTC`, `Capture_Timestamp_Local`, `Timezone`, `Machine_Name`, `Machine_Role`, `Windows_Edition` and 45 more. | Supporting repository file |
| [`Evidence-Index.csv`](Evidence-Index.csv) | CSV table with 1561 data row(s). Main columns are `Evidence_ID`, `Run_ID`, `Test_ID`, `Route`, `Workbook_ID`, `Evidence_Type`, `Repository_Path` and 12 more. | Supporting repository file |
| [`Execution-Checklist.md`](Execution-Checklist.md) | Readable Markdown document titled “Controlled Test Execution Checklist”. | Supporting repository file |
| [`Experiment-and-Test-Change-Control-Index.md`](Experiment-and-Test-Change-Control-Index.md) | Readable Markdown document titled “Experiment and Test Change-Control Index”. | Supporting repository file |
| [`Failure-Code-Catalogue.md`](Failure-Code-Catalogue.md) | Readable Markdown document titled “Controlled Failure Code Catalogue”. | Supporting repository file |
| [`Failure-Register.csv`](Failure-Register.csv) | CSV table with 12 data row(s). Main columns are `Failure_ID`, `Date_UTC`, `Date_Local`, `Run_ID`, `Test_ID`, `Route`, `Workbook_ID` and 20 more. | Supporting repository file |
| [`Failure-Register.md`](Failure-Register.md) | Readable Markdown document titled “Failure Register”. | Supporting repository file |
| [`Master-Test-Plan.md`](Master-Test-Plan.md) | Readable Markdown document titled “Master Test Plan”. | Supporting repository file |
| [`Metric-Definitions.md`](Metric-Definitions.md) | Readable Markdown document titled “Metric Definitions and Calculation Rules”. | Supporting repository file |
| [`Model-Register.csv`](Model-Register.csv) | CSV table with 0 data row(s). Main columns are `Model_ID`, `Display_Name`, `Family`, `Release`, `Variant`, `Source_URL`, `Source_Revision` and 23 more. | Supporting repository file |
| [`OpenVINO-Codec-Extension-v1.1.md`](OpenVINO-Codec-Extension-v1.1.md) | Readable Markdown document titled “OpenVINO Codec Testing Extension v1.1”. | Supporting repository file |
| [`OpenVINO-Codec-Extension-Validation-v1.1.md`](OpenVINO-Codec-Extension-Validation-v1.1.md) | Readable Markdown document titled “OpenVINO Codec Extension Validation v1.1”. | Supporting repository file |
| [`OpenVINO-Codec-Register-Integration.md`](OpenVINO-Codec-Register-Integration.md) | Readable Markdown document titled “OpenVINO Codec Register Integration”. | Supporting repository file |
| [`OpenVINO-Codec-Test-Boundary.md`](OpenVINO-Codec-Test-Boundary.md) | Readable Markdown document titled “OpenVINO KV-Cache Codec Test Boundary”. | Supporting repository file |
| [`OpenVINO-Codec-Traceability-Extension-v1.1.csv`](OpenVINO-Codec-Traceability-Extension-v1.1.csv) | CSV table with 119 data row(s). Main columns are `Test_ID`, `Route`, `Category`, `Test_Title`, `Research_Question`, `Workbook_ID`, `Workbook_Section`. | Supporting repository file |
| [`Performance-Measurement-Register.csv`](Performance-Measurement-Register.csv) | CSV table with 60 data row(s). Main columns are `Measurement_ID`, `Run_ID`, `Test_ID`, `Route`, `Workbook_ID`, `Configuration_ID`, `Warmup_or_Measured` and 36 more. | Supporting repository file |
| [`Quality-Evaluation-Register.csv`](Quality-Evaluation-Register.csv) | CSV table with 156 data row(s). Main columns are `Evaluation_ID`, `Route`, `Workbook_ID`, `Test_ID`, `Run_ID`, `Configuration_ID`, `Comparison_Role` and 26 more. | Supporting repository file |
| [`Repository-Register.csv`](Repository-Register.csv) | CSV table with 2 data row(s). Main columns are `Repository_ID`, `Route`, `Repository_URL`, `Branch_or_Tag`, `Commit_SHA`, `Upstream_Base_Commit`, `Detached_HEAD` and 16 more. | Supporting repository file |
| [`Source-to-Control-Mapping.md`](Source-to-Control-Mapping.md) | Readable Markdown document titled “Source-to-Control Mapping”. | Supporting repository file |
| [`Structural-Validation-Report.json`](Structural-Validation-Report.json) | Stores a JSON object with top-level fields `timestamp_utc`, `validated_scope`, `branch_boundary`, `checks`, `errors`, `passed`, `boundary`. | Supporting repository file |
| [`Test-ID-Catalogue-v1.1.md`](Test-ID-Catalogue-v1.1.md) | Readable Markdown document titled “Test ID Catalogue”. | Supporting repository file |
| [`Test-ID-Catalogue.md`](Test-ID-Catalogue.md) | Readable Markdown document titled “Test ID Catalogue”. | Supporting repository file |
| [`Test-Run-Register.csv`](Test-Run-Register.csv) | CSV table with 30 data row(s). Main columns are `Run_ID`, `Test_ID`, `Route`, `Workbook_ID`, `Run_Number`, `Run_Purpose`, `Start_Timestamp_UTC` and 116 more. | Supporting repository file |
| [`Test-Strategy.md`](Test-Strategy.md) | Readable Markdown document titled “Application and Experimental Testing Strategy”. | Supporting repository file |
| [`Test-Traceability-Matrix.csv`](Test-Traceability-Matrix.csv) | CSV table with 0 data row(s). Main columns are `Trace_ID`, `Research_Question`, `Project_Objective`, `Requirement_or_Risk_ID`, `Requirement_or_Risk_Summary`, `Test_ID`, `Route` and 17 more. | Supporting repository file |
| [`TurboVec-Feasibility-Result-v1.md`](TurboVec-Feasibility-Result-v1.md) | Readable Markdown document titled “TurboVec Feasibility Result v1”. | Supporting repository file |
| [`Validation-Audit-2026-07-13.md`](Validation-Audit-2026-07-13.md) | Readable Markdown document titled “Controlled Testing Workspace Validation Audit”. | Supporting repository file |
| [`Workbook-05-Memory-Frontier-Execution-Index-v1.csv`](Workbook-05-Memory-Frontier-Execution-Index-v1.csv) | CSV table with 119 data row(s). Main columns are `Execution_Record_ID`, `Test_ID`, `Workbook_ID`, `Route_ID`, `Phase_ID`, `Category`, `Test_Title` and 10 more. | Supporting repository file |
| [`Workbook-Completion-Register.csv`](Workbook-Completion-Register.csv) | CSV table with 20 data row(s). Main columns are `Workbook_ID`, `Workbook_File`, `Route`, `Section_Type`, `Section_or_Test_ID`, `Section_Title`, `Required_Source_Evidence` and 13 more. | Supporting repository file |
| [`Workbook-Data-Requirements.md`](Workbook-Data-Requirements.md) | Readable Markdown document titled “Workbook Data Requirements and Evidence Map”. | Supporting repository file |
| [`Workbook-Revision-Control-Validation.json`](Workbook-Revision-Control-Validation.json) | Stores a JSON object with top-level fields `timestamp_utc`, `branch`, `pull_request`, `checks`, `errors`, `passed`, `boundary`. | Supporting repository file |
| [`Workbook-Revision-Control-Validation.md`](Workbook-Revision-Control-Validation.md) | Readable Markdown document titled “Workbook Revision-Control Validation”. | Supporting repository file |
| [`Workbook-Revision-Control.md`](Workbook-Revision-Control.md) | Readable Markdown document titled “Workbook Revision Control”. | Supporting repository file |
| [`Workbook-Revision-Register.csv`](Workbook-Revision-Register.csv) | CSV table with 37 data row(s). Main columns are `Record_ID`, `Workbook_ID`, `Version`, `Date`, `Changed_By`, `Change_Type`, `Change_Summary` and 5 more. | Supporting repository file |

### Important boundaries

- Check the file's status and evidence links before treating it as a current result.

### Related guides

- [Parent guide](../README.md)
- [cleanup guide](cleanup/README.md)
- [failure-evidence guide](failure-evidence/README.md)
- [final-results guide](final-results/README.md)
- [plans guide](plans/README.md)
- [strategies guide](strategies/README.md)
- [test-reports guide](test-reports/README.md)
- [ux-evaluation guide](ux-evaluation/README.md)
- [workbook05 guide](workbook05/README.md)
- [workbooks guide](workbooks/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
