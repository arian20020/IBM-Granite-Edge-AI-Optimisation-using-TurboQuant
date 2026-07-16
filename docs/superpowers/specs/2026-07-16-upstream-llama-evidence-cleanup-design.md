# Upstream llama.cpp Evidence Cleanup Design

## Objective

Make the upstream llama.cpp evidence route easier to navigate without deleting audit evidence, changing captured raw files, or weakening WB-01 traceability.

## Final layout

The active route remains:

```text
experiments/granite_turboquant_intel/logs/upstream-llama-cpp/
|-- README.md
|-- UL-B01/ ... UL-B06/       # build and repository-test evidence
|-- UL-01/ ... UL-13/         # current formal benchmark, quality and server-metric runs
`-- archive/
    `-- calibration-and-superseded/
        |-- README.md
        `-- UL-01/ ... UL-13/ # retained pilots, calibrations and superseded metric runs
```

The archive is evidence storage, not a source of current workbook medians. Raw files are moved byte-for-byte and remain immutable.

## Classification rules

Move these runs to the archive:

- every `UL-XX-METRICS-R001` run superseded by `UL-XX-SERVER-METRICS-R001`;
- all `UL-01-METRICS-CALIBRATION*` and `UL-01-TTFT-CALIBRATION-R001` runs;
- UL-01 pilot generations `UL-01-R001` and `UL-01-R002`, because `UL-01-R003` is the retained formal benchmark;
- `UL-13-QUALITY-R001`, because `UL-13-QUALITY-R002` is the corrected formal quality run.

Keep current formal benchmarks, current quality runs, all `SERVER-METRICS-R001` runs, and build evidence directly beneath their test IDs. Failed build and repository-test evidence remains in `UL-B01` through `UL-B06`, because it explains validated corrections rather than exploratory measurement clutter.

## Guidance documentation

Add `logs/upstream-llama-cpp/README.md` covering:

- a short route map and which directories are current;
- the b9870 pin and WB-01 v1.4 relationship;
- why initial Vulkan and SYCL gates failed;
- the distinction between the passing UL-13 Granite workload and the 49/52 broad SYCL suite;
- the corrected TTFT definition: ready server, HTTP request initiation to first streamed generated token;
- how to find benchmark, quality, RAM/KV/TTFT and processed-result evidence;
- how to create future run IDs, preserve raw evidence and update the evidence index/workbook;
- explicit warnings not to copy calibration values into the workbook or commit environment dumps, models, binaries or secrets.

Add an archive README explaining why retained files should not be used as current results. Update the general campaign and workbook READMEs only where their current guidance or WB-01 version is stale.

## Traceability updates

Moving evidence changes repository paths but not file bytes. Update all affected rows in `docs/testing/Evidence-Index.csv` while preserving each SHA-256 and stable evidence ID. Search the workbook, processed results, notes and plans for old paths and update only references that point to moved files.

The canonical workbook content does not change unless it currently references a moved run. If it changes, regenerate WB-01, apply revision history and refresh the controlled manifest hashes. A structural cleanup alone does not create a new workbook revision.

## Safety and validation

- Use Git-aware moves and preserve each archived file hash.
- Remove only empty directories and generated cache files; do not delete evidence.
- Verify that every `Evidence-Index.csv` path exists and matches its recorded size and SHA-256.
- Verify that no active workbook evidence path points into a missing location.
- Run measurement unit tests, workbook revision control, workspace validation, DOCX semantic checks and `git diff --check`.
- Confirm a clean worktree after committing and push the cleanup to draft PR #29.

## Out of scope

- Renaming controlled test IDs or formal run IDs;
- rewriting raw output for formatting;
- changing scores or performance medians;
- resolving the three upstream SYCL edge failures;
- reorganising other execution routes.
