# Animehacker Large-Host Completion Design

## Goal

Complete the frozen WB-03 tests AH-06, AH-07, and AH-10 on a Windows host with at least 32 GiB physical RAM, preserving the existing model, cache, context, backend, prompt, measurement, quality, and safety definitions. The laptop's safety-classified evidence remains immutable and the larger-host evidence is stored separately until it passes reconciliation.

## Scope

The implementation adds a portable host preflight, an orchestrating launcher, isolated evidence storage, and an evidence-import/reconciliation path. It does not change the frozen matrix, lower the 2,048 MiB emergency available-RAM floor, substitute models, fabricate unavailable metrics, overwrite prior evidence, or grant a completed status to a partial run.

## Architecture

The existing `run_animehacker_retest.py` and `run_animehacker_quality.py` controllers remain responsible for runtime and quality execution. A focused large-host module validates host capacity and paths, creates a manifest, chooses a collision-free run directory, and builds the exact subprocess commands for one test at a time. A PowerShell entry point provides a convenient Windows interface while delegating validation and orchestration to Python.

Evidence is written beneath `experiments/raw-results/animehacker-tq3-0/<date>/large-host-completion/<run-id>/`. Each run contains its preflight result, immutable manifest, runtime evidence, quality evidence, controller logs, and terminal state. Existing laptop evidence under `runtime/` and `runtime-recovery/` is never modified.

## Host Preflight

Preflight must complete without launching `llama-server.exe`. It verifies:

- Windows reports at least 32 GiB installed physical RAM.
- The matrix and required CPU/SYCL `llama-server.exe` files exist.
- The frozen Granite 8B model paths exist and their SHA-256 hashes are recorded.
- AH-10 has a SYCL build and Level Zero device inventory compatible with `ONEAPI_DEVICE_SELECTOR=level_zero:0`.
- The selected output directory does not overwrite an existing run.
- No `llama-server`, `llama-cli`, or prior WB-03 controller process is active.

Failed preflight writes a diagnostic result and exits before runtime execution. A test-only installed-memory override may be injected through the Python API, but there is no command-line option that bypasses the production RAM requirement.

## Execution Flow

The launcher accepts only AH-06, AH-07, and AH-10. It executes serially and checkpoints after every phase:

1. Run preflight and persist the host/model/build manifest.
2. Run a guarded pilot at the unchanged 2,048 MiB emergency floor.
3. If the pilot is valid, run the excluded warm-up and exactly three formal samples.
4. Validate the runtime summary, including TTFT, throughput, peak working set, private memory, KV allocation, GPU memory, and CPU/GPU mean, median, and peak utilization.
5. Run the frozen P1-P6 quality prompts only after complete runtime evidence exists.
6. Validate response hashes, deterministic gates, individual quality scores, and the harsh weighted mean.
7. Mark the row import-ready only when every required runtime and quality field is present.

On safety stop, timeout, interruption, or invalid output, the launcher terminates the process tree, records the terminal reason, and retains resumable evidence. Resume skips only phases proven complete and never treats an earlier laptop safety classification as a completed large-host run.

## Evidence Import and Workbook Update

An importer accepts one terminal large-host run directory. Before changing controlled artifacts it verifies the run manifest, model hashes, frozen matrix fields, three formal samples, utilization summaries, six quality records, response hashes, process cleanup, and the absence of missing required metrics.

Accepted evidence updates the corresponding AH-06, AH-07, or AH-10 runtime, device, quality, failure-resolution, and recommendation fields in the WB-03 Markdown source. It appends a new workbook revision, regenerates the DOCX through the existing controlled-workbook pipeline, and runs the existing reconciliation, structural audit, revision-register, and controlled-workspace validators. Import is atomic from the user's perspective: validation occurs before controlled files are edited, and a failed validation leaves the workbook unchanged.

## Error Handling and Safety

- The 32 GiB installed-RAM requirement and 2,048 MiB available-RAM floor are independent mandatory gates.
- Runtime remains serial; concurrent model loads are prohibited.
- Every launched process is tracked and its full descendant tree is terminated on failure.
- Existing evidence directories are never reused unless explicit resume state identifies the same run ID and manifest.
- Conflicting model hashes, backend evidence, cache activation, or frozen matrix fields reject the run.
- Missing metrics remain a failed import condition, never a placeholder workbook value.

## Testing

Automated tests will first fail and then cover:

- rejection below 32 GiB and acceptance at or above 32 GiB;
- missing binaries/models, active-process detection, and Level Zero preflight failure;
- selection restricted to AH-06, AH-07, and AH-10;
- collision-free run IDs and immutable manifests;
- exact guarded runtime and quality command construction;
- resume behavior at pilot, runtime, and quality boundaries;
- rejection of fewer than three formal samples, missing CPU/GPU aggregates, missing quality rows, hash conflicts, or residual processes;
- isolation from the prior laptop evidence tree;
- workbook edits occurring only after complete validation.

Final verification runs the complete repository test suite plus WB-03 reconciliation, DOCX structural audit, revision validation, and controlled-workspace validation. Hardware execution is complete only after the portable package is run on the qualifying host and its accepted evidence has been imported.
