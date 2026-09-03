# Workbook 05 Phase 3

Contains the dependency and asset controls for Workbook 05 Phase 3.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains the dependency and asset controls for Workbook 05 Phase 3.

### Start here

Start with the documented `Validate-Workbook05-*.ps1` entry points in the parent testing-script folder. Use these helpers only for maintenance or controlled stage execution.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |
| [`asset_bundle_validation.py`](asset_bundle_validation.py) | Validate a Workbook 05 C1 asset bundle strictly as untrusted data. | Executable or importable tooling |
| [`c1_resume.py`](c1_resume.py) | Validate and requalify one failed Workbook 05 live C1 attempt for resume. | Executable or importable tooling |
| [`c1_resume_bundle_validation.py`](c1_resume_bundle_validation.py) | Validate a controlled Workbook 05 C1 source-resume artifact as untrusted data. | Executable or importable tooling |
| [`contracts.py`](contracts.py) | Validate Workbook 05 Phase 3 evidence records against closed schemas. | Executable or importable tooling |
| [`conversion.py`](conversion.py) | Build immutable OpenVINO conversion requests and evidence records. | Executable or importable tooling |
| [`dependency_acceptance.py`](dependency_acceptance.py) | Bind one independently accepted dependency preflight to live C1. | Executable or importable tooling |
| [`dependency_bundle_validation.py`](dependency_bundle_validation.py) | Validate a complete dependency-preflight artifact strictly as untrusted data. | Executable or importable tooling |
| [`dependency_decision.py`](dependency_decision.py) | Build the live Workbook 05 dependency-preflight decision from raw evidence. | Executable or importable tooling |
| [`dependency_decision_cli.py`](dependency_decision_cli.py) | Atomically materialise a live dependency decision and normal inventory. | Executable or importable tooling |
| [`dependency_import_check.py`](dependency_import_check.py) | Import the exact C1 conversion modules in a fresh final-Python process. | Executable or importable tooling |
| [`dependency_import_identity.py`](dependency_import_identity.py) | Describe imported modules without assuming every package has ``__file__``. | Executable or importable tooling |
| [`dependency_lock.py`](dependency_lock.py) | Validate dependency locks and build fail-closed preflight evidence. | Executable or importable tooling |
| [`dependency_lock_cli.py`](dependency_lock_cli.py) | Command-line validation for Workbook 05 dependency locks and pip reports. | Executable or importable tooling |
| [`dependency_no_model_check.py`](dependency_no_model_check.py) | Perform the repository-controlled compatibility check without touching a model. | Executable or importable tooling |
| [`dependency_observation_cli.py`](dependency_observation_cli.py) | Materialise the live Workbook 05 dependency observation from retained evidence. | Executable or importable tooling |
| [`dependency_preflight.py`](dependency_preflight.py) | Build fail-closed evidence for the C1 conversion dependency preflight. | Executable or importable tooling |
| [`dependency_preflight_cli.py`](dependency_preflight_cli.py) | Build and schema-validate one C1 dependency-preflight decision. | Executable or importable tooling |
| [`dependency_preflight_fixture.py`](dependency_preflight_fixture.py) | Generate deterministic text-only dependency-preflight simulation evidence. | Executable or importable tooling |
| [`dependency_source_contract.py`](dependency_source_contract.py) | Inspect reviewed Optimum source metadata without importing or executing it. | Executable or importable tooling |
| [`dependency_source_contract_cli.py`](dependency_source_contract_cli.py) | Validate both reviewed source trees as data and write the ordinary input. | Executable or importable tooling |
| [`diagnostic_selection.py`](diagnostic_selection.py) | Classify diagnostic assets without overstating codec-path equivalence. | Executable or importable tooling |
| [`disk_preflight.py`](disk_preflight.py) | Collect read-only Workbook 05 Phase 3 disk and workspace evidence. | Executable or importable tooling |
| [`hashing.py`](hashing.py) | Deterministic SHA-256 helpers for Workbook 05 Phase 3 evidence. | Executable or importable tooling |
| [`live_asset_bundle_validation.py`](live_asset_bundle_validation.py) | Validate the extra trust relationships present only in a live Workbook 05 C1 bundle. | Executable or importable tooling |
| [`live_asset_lock.py`](live_asset_lock.py) | Live Workbook 05 C1 model acquisition and record materialisation. | Executable or importable tooling |
| [`model_assets.py`](model_assets.py) | Resolve and retain immutable Workbook 05 model snapshots. | Executable or importable tooling |
| [`paths.py`](paths.py) | Fail-closed path primitives for Workbook 05 Phase 3 evidence. | Executable or importable tooling |
| [`prerequisites.py`](prerequisites.py) | Revalidate the exact accepted Workbook 05 Runtime and GenAI prerequisites. | Executable or importable tooling |
| [`source_tree_manifest.py`](source_tree_manifest.py) | Create a complete text-only manifest for one reviewed Git source tree. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
