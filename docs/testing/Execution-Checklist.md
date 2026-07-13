# Controlled Test Execution Checklist

Use this checklist for every build check, conversion check and inference run.

## Before execution

- [ ] The exact test ID exists in `Test-ID-Catalogue.md`.
- [ ] Prerequisite tests passed or a documented decision permits continuation.
- [ ] Repository, machine, build, model and configuration manifests are complete.
- [ ] Model and executable hashes are verified.
- [ ] Prompt set and rubric versions are frozen.
- [ ] A unique run ID and evidence folder exist.
- [ ] Exact command, working directory and environment variables were saved before execution.
- [ ] Requested backend, device, offload, cache and optimisation are recorded.
- [ ] Expected activation/device proof is stated before the run.
- [ ] RAM, disk, timeout and thermal stop limits are recorded.
- [ ] Non-essential applications are closed and power mode is recorded.

## During execution

- [ ] Start/end timestamps are captured.
- [ ] stdout and stderr are separate.
- [ ] Original model response is saved without cleanup or rewriting.
- [ ] Process-tree and system-memory samples are captured.
- [ ] CPU/GPU utilization and device evidence are captured where required.
- [ ] Actual backend, device, layer and KV placement are recorded.
- [ ] Fallback, warning, crash, hang, OOM and cancellation behaviour is preserved.
- [ ] Pilot, warm-up and measured repetitions are labelled correctly.

## After execution

- [ ] Exit code and completion status are classified.
- [ ] Missing values use an approved missing-data code with a reason.
- [ ] Deterministic quality checks are run.
- [ ] Human scoring/adjudication is completed where required.
- [ ] Evidence SHA-256 manifest is generated.
- [ ] Run, performance, device, quality, failure and evidence registers are updated.
- [ ] Processed results can be regenerated from raw evidence.
- [ ] The exact workbook row/section is updated.
- [ ] `Workbook-Completion-Register.csv` is updated.
- [ ] Evidence and workbook changes are committed and pushed before the next test.
- [ ] The route conclusion remains within the tested scope.
