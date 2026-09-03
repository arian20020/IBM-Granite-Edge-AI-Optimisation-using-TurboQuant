# OpenVINO Campaign Modules

Implements the lower-level campaign logic for the OpenVINO routes. Use the supported CLI instead of calling these modules directly.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Implements the lower-level campaign logic for the OpenVINO routes. Use the supported CLI instead of calling these modules directly.

### Start here

Most users should not call these modules directly. Start with the [supported CLI guide](../../cli/README.md), which selects the correct campaign module and forwards validated options.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |
| [`acquisition.py`](acquisition.py) | Validation helpers for reproducible Official OpenVINO acquisition evidence. | Executable or importable tooling |
| [`adaptive_campaign.py`](adaptive_campaign.py) | Resumable breadth-first policy for the adaptive OpenVINO comparison. | Executable or importable tooling |
| [`adaptive_campaign_spec.py`](adaptive_campaign_spec.py) | Generate runtime-free worker specs for the five-row adaptive comparison. | Executable or importable tooling |
| [`adaptive_metrics.py`](adaptive_metrics.py) | Lossless formal runtime samples for adaptive OpenVINO comparison. | Executable or importable tooling |
| [`adaptive_quality.py`](adaptive_quality.py) | Isolated, append-only P1-P6 quality capture for adaptive campaigns. | Executable or importable tooling |
| [`artifact_inventory.py`](artifact_inventory.py) | Hash-bound inventory and preparation boundary for WB-04 artifacts. | Executable or importable tooling |
| [`campaign_spec.py`](campaign_spec.py) | Generate fail-closed WB-04 worker specs from the frozen formal matrix. | Executable or importable tooling |
| [`comparison_reconcile.py`](comparison_reconcile.py) | Independent, hash-bound reconciliation for adaptive comparison evidence. | Executable or importable tooling |
| [`conversion.py`](conversion.py) | Validation contract for official OpenVINO model conversion evidence. | Executable or importable tooling |
| [`diagnostics.py`](diagnostics.py) | Strict reconciliation for official OpenVINO diagnostic evidence. | Executable or importable tooling |
| [`docx_audit.py`](docx_audit.py) | Structural and control audit for the generated WB-04 DOCX. | Executable or importable tooling |
| [`expected_rejections.py`](expected_rejections.py) | Deterministic fail-closed evidence for WB-04 expected rejections. | Executable or importable tooling |
| [`format_boundary.py`](format_boundary.py) | Typed, fixed-order input contract for the OpenVINO format boundary retest. | Executable or importable tooling |
| [`guarded_build.py`](guarded_build.py) | Run one owned Windows process tree with strict resource and evidence gates. | Executable or importable tooling |
| [`matrix.py`](matrix.py) | Typed loader for the frozen official OpenVINO WB-04 matrix. | Executable or importable tooling |
| [`measurement_worker.py`](measurement_worker.py) | One fresh-process OpenVINO GenAI generation for WB-04 measurement. | Executable or importable tooling |
| [`metrics.py`](metrics.py) | Strict measurement reconciliation for Official OpenVINO WB-04 runs. | Executable or importable tooling |
| [`owned_process_guard.py`](owned_process_guard.py) | Own Windows process trees and expose shared RAM/process sampling primitives. | Executable or importable tooling |
| [`patch_identity.py`](patch_identity.py) | Prepare and validate a reproducible project-patched OpenVINO checkout. | Executable or importable tooling |
| [`quality.py`](quality.py) | Hash-bound, label-blind quality scoring for Official OpenVINO WB-04. | Executable or importable tooling |
| [`quality_campaign.py`](quality_campaign.py) | Validate one accepted measurement campaign before quality capture. | Executable or importable tooling |
| [`quality_contracts.py`](quality_contracts.py) | Allow-listed immutable prompt-contract identities for OpenVINO quality. | Executable or importable tooling |
| [`quality_worker.py`](quality_worker.py) | Governed, score-free P1-P6 OpenVINO quality-capture worker. | Executable or importable tooling |
| [`reconcile.py`](reconcile.py) | Validation helpers for the fully populated WB-04 Markdown workbook. | Executable or importable tooling |
| [`runner.py`](runner.py) | Terminal-safe runtime reconciliation for WB-04. | Executable; may create or change outputs |
| [`runtime_measurement.py`](runtime_measurement.py) | Crash-safe WB-04 runtime measurement primitives. | Executable; may create or change outputs |
| [`runtime_process.py`](runtime_process.py) | Govern one WB-04 worker process and preserve complete raw evidence. | Executable; may create or change outputs |
| [`scalar_semantic_rejections.py`](scalar_semantic_rejections.py) | Fail-closed evidence for post-activation scalar cache rejection rows. | Executable or importable tooling |
| [`source_audit.py`](source_audit.py) | Conservative source evidence audit for the official OpenVINO KV codec boundary. | Executable or importable tooling |
| [`workload.py`](workload.py) | Deterministic prefill workloads for the controlled WB-04 context screen. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
