# Experiment and Test Change-Control Index

**Status:** Controlling testing index  
**Campaign:** Granite-TurboQuant Controlled Retest Campaign v1  
**Owner:** Project team  
**Last reviewed:** 2026-07-14

This page identifies the authoritative logs and registers used to control experiments, application evaluations, test executions and workbook changes.

## Decisions and scope changes

- [Testing Decision Log](Decision-Log.md) — append-only decisions about campaign scope, evidence rules, quality scale and route coverage.
- [Master Test Plan](Master-Test-Plan.md) — route order, stage gates, stop conditions and execution boundaries.
- [App-Specific Evaluation Addendum](App-Specific-Evaluation-Addendum-v1.md) — stable app-evaluation IDs, RQ mappings, frozen prompts/rubric, evidence schemas, measures, gates, stopping rules and claim boundaries.
- [Test ID Catalogue](Test-ID-Catalogue.md) — stable route-workbook test identifiers that must exist before execution.
- [Source-to-Control Mapping](Source-to-Control-Mapping.md) — mapping from supplied testing material into the controlled campaign.

## Experiment and run records

- `Test-Run-Register.csv` — one row per execution and unique run ID.
- `Environment-Register.csv` — operating system, hardware and software state.
- `Repository-Register.csv` — source repository, branch and commit.
- `Build-Register.csv` — compiler, options, produced binaries and hashes.
- `Model-Register.csv` — model identity, revision, format and hash.
- `Configuration-Register.csv` — controlled runtime and optimisation settings.
- `Device-Verification-Register.csv` — requested versus actual device/backend/placement.
- `Performance-Measurement-Register.csv` — pilot, warm-up and measured repetitions.
- `Quality-Evaluation-Register.csv` — prompt/rubric versions and scored outputs.
- `Failure-Register.csv` — failures, diagnosis, fix, retest and negative evidence.
- `Evidence-Index.csv` — file-level provenance and checksums.

## Workbook change control

- [Workbook Revision Control](Workbook-Revision-Control.md) — controlling append-only procedure.
- [Workbook Revision Register](Workbook-Revision-Register.csv) — revision history for WB-01 through WB-06.
- [Workbook Completion Register](Workbook-Completion-Register.csv) — confirms whether each workbook section has run IDs, evidence paths, processed results and an evidence commit.
- [Controlled Workbook Manifest](workbooks/Controlled-Workbook-Manifest.csv) — canonical template and generated-DOCX hashes.
- [Controlled Testing Workbooks](workbooks/README.md) — current workbook versions and deterministic generation process.

## Evidence flow

`requirement/RQ -> evaluation/test ID -> run ID -> environment/repository/build/model/configuration -> immutable raw evidence -> processed result -> machine-readable register -> workbook/test report -> conclusion`

## Change rule

A later correction creates a new run ID, processed result, workbook revision, addendum revision or decision-log entry. Raw evidence and historical revision rows are retained.
