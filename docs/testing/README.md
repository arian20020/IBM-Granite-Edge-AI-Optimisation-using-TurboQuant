# Controlled Testing Workspace

**Campaign:** Granite-TurboQuant Controlled Retest Campaign v1  
**Started:** 13 July 2026  
**Status:** Authoritative testing campaign  
**Previous campaign:** Legacy / superseded for final conclusions

This directory is the control centre for all application, runtime, optimisation and model-evaluation testing.

## Rules

1. Every planned test must exist in `Test-ID-Catalogue.md` before it is run.
2. Every execution receives a unique run ID.
3. The exact environment, repository commit, model hash and command must be recorded.
4. Raw evidence is never edited after capture.
5. Failed, blocked and inconclusive runs remain in the evidence record.
6. A workbook entry is not complete until it links to the matching run evidence.
7. A test is not authoritative until its evidence has been committed and pushed.
8. Requested backends, devices or optimisations must be checked against the actual runtime state.

## Main control files

- `Test-Strategy.md` — governing testing principles and evidence rules.
- `Master-Test-Plan.md` — campaign sequence, gates and completion criteria.
- `Test-ID-Catalogue.md` — controlled list of tests and existing workbook IDs.
- `Test-Traceability-Matrix.csv` — requirements and research questions mapped to tests and evidence.
- `Test-Run-Register.csv` — one row per execution.
- `Environment-Register.csv` — controlled hardware, software and model environments.
- `Failure-Register.csv` — preserved failures, diagnoses, fixes and retests.
- `Workbook-Completion-Register.csv` — tracks completion of every workbook section.
- `Decision-Log.md` — test-scope and interpretation decisions.

## Status vocabulary

`Planned`, `Ready`, `Running`, `Passed`, `Failed`, `Blocked`, `Inconclusive`, `Superseded`.

## Evidence flow

`plan -> test ID -> run ID -> manifest -> raw evidence -> processed result -> workbook -> conclusion`
