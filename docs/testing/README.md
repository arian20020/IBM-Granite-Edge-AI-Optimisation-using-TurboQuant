# Controlled Testing Workspace

**Campaign:** Granite-TurboQuant Controlled Retest Campaign v1  
**Started:** 13 July 2026  
**Status:** Authoritative testing campaign  
**Previous campaign:** Preserved as legacy evidence; not automatically authoritative

This directory controls application testing, model/runtime feasibility, AI-quality evaluation and final cross-route comparison.

## Choose what you need

| Goal | Open this first |
| --- | --- |
| Read the overall findings | [Cross-route comparison report](final-results/06-cross-route-comparison/reports/cross-route-comparison-report.md) |
| Compare the two OpenVINO campaigns in Excel | [Experimental-fork workbook](final-results/04-openvino-experimental-fork/reports/openvino-experimental-fork-results.xlsx) and [official-upstream workbook](final-results/05-openvino-official-upstream/reports/openvino-official-upstream-results.xlsx) |
| Find one route, file or evidence record | [Final-results beginner directory guide](Final-Results-Beginner-Directory-Guide.md) |
| Understand why something failed or did not run | [Combined failure summary](final-results/catalog/failure-summary.csv) and [failure-evidence guide](failure-evidence/README.md) |
| Understand quality scoring and comparison limits | [Quality comparison policy](final-results/standards/quality-comparison-policy.md) and [rubrics guide](../../experiments/rubrics/README.md) |
| Run or validate the testing tools | [Testing commands](../../scripts/testing/README.md) |
| Find editable controlled workbook templates | [Workbooks guide](workbooks/README.md) |
| Audit provenance and checksums | [Evidence manifest](final-results/catalog/evidence-manifest.csv) and [release validation summary](final-results/validation/validation-summary.md) |

## Before starting or changing a test

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
2. Every execution ID exists in the route catalogue or frozen app-specific addendum before execution.
3. Every execution receives a unique run ID.
4. Environment, repository commit, build, model hash, configuration, prompt and command are recorded exactly.
5. Raw evidence is immutable.
6. Failed, blocked and inconclusive runs remain visible.
7. Requested backend, device and optimisation are checked against actual runtime behaviour.
8. Important results are entered in machine-readable registers before being copied into Word.
9. A workbook row or app-evaluation report is incomplete until it links to a run ID, evidence path and evidence commit.
10. Formal benchmarking uses a pilot, one excluded warm-up and at least three measured repetitions unless a documented safety gate prevents it.
11. Quality is reported on the controlled 0-10 rubric.
12. Model weights, secrets, build caches and copied third-party repositories are not committed.

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

Row counts in this table describe the files at the time this guide was updated. They are navigation aids, not fixed requirements; use each file's schema and validation rules as the authority.

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`Animehacker-Large-Host-Completion-Guide.md`](Animehacker-Large-Host-Completion-Guide.md) | Explains how to finish the WB-03 cases that require a larger-memory host while preserving the existing evidence boundary. | Operator guide |
| [`App-Specific-Evaluation-Addendum-v1.md`](App-Specific-Evaluation-Addendum-v1.md) | Freezes application-evaluation IDs, research-question mappings, inputs, schemas, scoring rules and stopping gates. | Controlled test definition |
| [`Build-Register.csv`](Build-Register.csv) | CSV table with 3 data row(s). Main columns are `Build_Check_ID`, `Route`, `Workbook_ID`, `Repository_ID`, `Build_ID`, `Environment_ID`, `Check_Title` and 26 more. | Supporting repository file |
| [`Configuration-Register.csv`](Configuration-Register.csv) | CSV table with 0 data row(s). Main columns are `Configuration_ID`, `Test_ID`, `Route`, `Workbook_ID`, `Model`, `Model_ID`, `Weights` and 22 more. | Supporting repository file |
| [`Cross-Route-Comparison-Register.csv`](Cross-Route-Comparison-Register.csv) | CSV table with 0 data row(s). Main columns are `Comparison_ID`, `Criterion`, `Source_Route`, `Source_Test_ID`, `Source_Run_ID`, `Source_Configuration_ID`, `Target_or_Baseline_Route` and 28 more. | Supporting repository file |
| [`Decision-Log.md`](Decision-Log.md) | Records important testing and interpretation decisions so later readers can understand why the method changed. | Append-only decision record |
| [`Device-Verification-Register.csv`](Device-Verification-Register.csv) | CSV table with 19 data row(s). Main columns are `Test_ID`, `Route`, `Workbook_ID`, `Latest_Run_ID`, `Requested_Device`, `Actual_Device`, `Backend` and 20 more. | Supporting repository file |
| [`Environment-Register.csv`](Environment-Register.csv) | CSV table with 2 data row(s). Main columns are `Environment_ID`, `Capture_Timestamp_UTC`, `Capture_Timestamp_Local`, `Timezone`, `Machine_Name`, `Machine_Role`, `Windows_Edition` and 45 more. | Supporting repository file |
| [`Evidence-Index.csv`](Evidence-Index.csv) | CSV table with 1561 data row(s). Main columns are `Evidence_ID`, `Run_ID`, `Test_ID`, `Route`, `Workbook_ID`, `Evidence_Type`, `Repository_Path` and 12 more. | Supporting repository file |
| [`Execution-Checklist.md`](Execution-Checklist.md) | Step-by-step pre-run, execution, evidence-capture and close-out checklist for a controlled test. | Operator checklist |
| [`Experiment-and-Test-Change-Control-Index.md`](Experiment-and-Test-Change-Control-Index.md) | Entry point for deciding which protocol, manifest, register and workbook records must change together. | Change-control guide |
| [`Failure-Code-Catalogue.md`](Failure-Code-Catalogue.md) | Defines the approved failure codes and meanings used to classify failed, blocked and inconclusive work consistently. | Controlled vocabulary |
| [`Failure-Register.csv`](Failure-Register.csv) | CSV table with 12 data row(s). Main columns are `Failure_ID`, `Date_UTC`, `Date_Local`, `Run_ID`, `Test_ID`, `Route`, `Workbook_ID` and 20 more. | Supporting repository file |
| [`Failure-Register.md`](Failure-Register.md) | Human-readable companion explaining the structured failure register and its current recorded incidents. | Failure record |
| [`Final-Results-Beginner-Directory-Guide.md`](Final-Results-Beginner-Directory-Guide.md) | Complete beginner map of all 71 directories and 226 files in the immutable final-results release. | Navigation guide; does not alter the release |
| [`Master-Test-Plan.md`](Master-Test-Plan.md) | Defines route order, stage gates, dependencies, stop conditions and required evidence for the controlled campaign. | Controlled test plan |
| [`Metric-Definitions.md`](Metric-Definitions.md) | Defines calculation methods, units, aggregation rules and interpretation limits for reported metrics. | Metric authority |
| [`Model-Register.csv`](Model-Register.csv) | CSV table with 0 data row(s). Main columns are `Model_ID`, `Display_Name`, `Family`, `Release`, `Variant`, `Source_URL`, `Source_Revision` and 23 more. | Supporting repository file |
| [`OpenVINO-Codec-Extension-v1.1.md`](OpenVINO-Codec-Extension-v1.1.md) | Extends the test method for OpenVINO KV-cache codecs, including format coverage, activation proof and safety gates. | Controlled test extension |
| [`OpenVINO-Codec-Extension-Validation-v1.1.md`](OpenVINO-Codec-Extension-Validation-v1.1.md) | Records the structural review of the OpenVINO codec extension and the checks required before use. | Validation record |
| [`OpenVINO-Codec-Register-Integration.md`](OpenVINO-Codec-Register-Integration.md) | Explains how codec-specific fields and outcomes map into the common testing registers. | Integration guide |
| [`OpenVINO-Codec-Test-Boundary.md`](OpenVINO-Codec-Test-Boundary.md) | Defines which KV-cache codec claims and configurations are inside or outside the controlled OpenVINO test scope. | Scope boundary |
| [`OpenVINO-Codec-Traceability-Extension-v1.1.csv`](OpenVINO-Codec-Traceability-Extension-v1.1.csv) | CSV table with 119 data row(s). Main columns are `Test_ID`, `Route`, `Category`, `Test_Title`, `Research_Question`, `Workbook_ID`, `Workbook_Section`. | Supporting repository file |
| [`Performance-Measurement-Register.csv`](Performance-Measurement-Register.csv) | CSV table with 60 data row(s). Main columns are `Measurement_ID`, `Run_ID`, `Test_ID`, `Route`, `Workbook_ID`, `Configuration_ID`, `Warmup_or_Measured` and 36 more. | Supporting repository file |
| [`Quality-Evaluation-Register.csv`](Quality-Evaluation-Register.csv) | CSV table with 156 data row(s). Main columns are `Evaluation_ID`, `Route`, `Workbook_ID`, `Test_ID`, `Run_ID`, `Configuration_ID`, `Comparison_Role` and 26 more. | Supporting repository file |
| [`Repository-Register.csv`](Repository-Register.csv) | CSV table with 2 data row(s). Main columns are `Repository_ID`, `Route`, `Repository_URL`, `Branch_or_Tag`, `Commit_SHA`, `Upstream_Base_Commit`, `Detached_HEAD` and 16 more. | Supporting repository file |
| [`Source-to-Control-Mapping.md`](Source-to-Control-Mapping.md) | Maps the supplied source documents to the controlled repository files that preserve or implement them. | Provenance map |
| [`Structural-Validation-Report.json`](Structural-Validation-Report.json) | Stores a JSON object with top-level fields `timestamp_utc`, `validated_scope`, `branch_boundary`, `checks`, `errors`, `passed`, `boundary`. | Supporting repository file |
| [`Test-ID-Catalogue-v1.1.md`](Test-ID-Catalogue-v1.1.md) | Versioned catalogue of controlled test IDs, including the expanded codec-testing definitions. | Versioned test definition |
| [`Test-ID-Catalogue.md`](Test-ID-Catalogue.md) | Base catalogue that assigns stable IDs and route ownership to the original controlled tests. | Controlled test definition |
| [`Test-Run-Register.csv`](Test-Run-Register.csv) | CSV table with 30 data row(s). Main columns are `Run_ID`, `Test_ID`, `Route`, `Workbook_ID`, `Run_Number`, `Run_Purpose`, `Start_Timestamp_UTC` and 116 more. | Supporting repository file |
| [`Test-Strategy.md`](Test-Strategy.md) | Defines the campaign's evidence standard, repeatability rules, comparison limits and claim language. | Method authority |
| [`Test-Traceability-Matrix.csv`](Test-Traceability-Matrix.csv) | CSV table with 0 data row(s). Main columns are `Trace_ID`, `Research_Question`, `Project_Objective`, `Requirement_or_Risk_ID`, `Requirement_or_Risk_Summary`, `Test_ID`, `Route` and 17 more. | Supporting repository file |
| [`TurboVec-Feasibility-Result-v1.md`](TurboVec-Feasibility-Result-v1.md) | Summarises the bounded TurboVec feasibility outcome and explains why it remains demonstrator-only rather than product evidence. | Result summary with bounded claims |
| [`Validation-Audit-2026-07-13.md`](Validation-Audit-2026-07-13.md) | Records the initial structural and methodological audit used to establish the controlled testing workspace. | Historical validation baseline |
| [`Workbook-05-Memory-Frontier-Execution-Index-v1.csv`](Workbook-05-Memory-Frontier-Execution-Index-v1.csv) | CSV table with 119 data row(s). Main columns are `Execution_Record_ID`, `Test_ID`, `Workbook_ID`, `Route_ID`, `Phase_ID`, `Category`, `Test_Title` and 10 more. | Supporting repository file |
| [`Workbook-Completion-Register.csv`](Workbook-Completion-Register.csv) | CSV table with 20 data row(s). Main columns are `Workbook_ID`, `Workbook_File`, `Route`, `Section_Type`, `Section_or_Test_ID`, `Section_Title`, `Required_Source_Evidence` and 13 more. | Supporting repository file |
| [`Workbook-Data-Requirements.md`](Workbook-Data-Requirements.md) | Maps every workbook field to the evidence, identifier and validation needed before that field is complete. | Workbook completion authority |
| [`Workbook-Revision-Control-Validation.json`](Workbook-Revision-Control-Validation.json) | Stores a JSON object with top-level fields `timestamp_utc`, `branch`, `pull_request`, `checks`, `errors`, `passed`, `boundary`. | Supporting repository file |
| [`Workbook-Revision-Control-Validation.md`](Workbook-Revision-Control-Validation.md) | Human-readable result of the workbook source, template, hash and generated-file revision checks. | Validation record |
| [`Workbook-Revision-Control.md`](Workbook-Revision-Control.md) | Defines how workbook templates and generated copies are versioned, regenerated and recorded without losing history. | Revision-control procedure |
| [`Workbook-Revision-Register.csv`](Workbook-Revision-Register.csv) | CSV table with 37 data row(s). Main columns are `Record_ID`, `Workbook_ID`, `Version`, `Date`, `Changed_By`, `Change_Type`, `Change_Summary` and 5 more. | Supporting repository file |

### Important boundaries

- Check the file's status and evidence links before treating it as a current result.

### Related guides

- [Parent guide](../README.md)
- [cleanup guide](cleanup/README.md)
- [failure-evidence guide](failure-evidence/README.md)
- [final-results guide](final-results/README.md)
- [legacy guide](legacy/README.md)
- [manual guide](manual/README.md)
- [plans guide](plans/README.md)
- [runbooks guide](runbooks/README.md)
- [source-material guide](source-material/README.md)
- [strategies guide](strategies/README.md)
- [test-reports guide](test-reports/README.md)
- [ux-evaluation guide](ux-evaluation/README.md)
- [workbook05 guide](workbook05/README.md)
- [workbooks guide](workbooks/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
