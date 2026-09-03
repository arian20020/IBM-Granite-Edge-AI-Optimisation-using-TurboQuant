# Workbook 05

Contains controlled helpers for Workbook 05 source admission, build and measurement stages.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains controlled helpers for Workbook 05 source admission, build and measurement stages.

### Start here

Start with the documented `Validate-Workbook05-*.ps1` entry points in the parent testing-script folder. Use these helpers only for maintenance or controlled stage execution.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Folders

| Folder | What it contains |
| --- | --- |
| [`phase3/`](phase3/README.md) | Contains the dependency and asset controls for Workbook 05 Phase 3. |

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |
| [`Assert-Workbook05ArtifactIdentity.ps1`](Assert-Workbook05ArtifactIdentity.ps1) | Verifies the immutable GitHub identity of the Runtime artifact used by the controlled-resume workflow. | Executable or importable tooling |
| [`Assert-Workbook05Phase3Prerequisites.ps1`](Assert-Workbook05Phase3Prerequisites.ps1) | Runs the Assert Workbook05Phase3Prerequisites PowerShell workflow. | Executable or importable tooling |
| [`build_bundle_validation.py`](build_bundle_validation.py) | Validate Workbook 05 build evidence as untrusted text-only data. | Executable; may create or change outputs |
| [`build_contracts.py`](build_contracts.py) | Validate Workbook 05 documented-build records and the stage template. | Executable; may create or change outputs |
| [`bundle_validation.py`](bundle_validation.py) | Validate a self-hosted Workbook 05 preflight bundle as untrusted data. | Executable or importable tooling |
| [`capture_documented_commands.py`](capture_documented_commands.py) | Capture exact pinned GitHub documents and fenced commands without executing them. | Executable or importable tooling |
| [`checkpoint.py`](checkpoint.py) | Atomically record and resume Workbook 05 campaign phases. | Executable or importable tooling |
| [`cmake_test_discovery.py`](cmake_test_discovery.py) | Audit the exact Route B target-per-test CMake source-collection pattern. | Executable or importable tooling |
| [`configure_probe.py`](configure_probe.py) | Construct and evaluate the controlled Route A CMake configure-only probe. | Executable or importable tooling |
| [`generate_execution_index.py`](generate_execution_index.py) | Generate the deterministic two-route Workbook 05 memory-frontier execution index. | Executable; may create or change outputs |
| [`hash_manifest.py`](hash_manifest.py) | Create and verify deterministic SHA-256 manifests for Workbook 05 evidence. | Executable or importable tooling |
| [`Invoke-Workbook05Phase3AssetLock.ps1`](Invoke-Workbook05Phase3AssetLock.ps1) | Runs the Invoke Workbook05Phase3AssetLock PowerShell workflow. | Executable; may create or change outputs |
| [`Invoke-Workbook05Phase3AssetLockLive.ps1`](Invoke-Workbook05Phase3AssetLockLive.ps1) | Locks the official IBM Granite 4.1 3B snapshot and one reviewed OpenVINO INT4 conversion after revalidating the exact accepted dependency workspace. | Executable; may create or change outputs |
| [`Invoke-Workbook05Phase3AssetLockResume.ps1`](Invoke-Workbook05Phase3AssetLockResume.ps1) | Resumes only the OpenVINO conversion portion of one failed Workbook 05 C1 attempt after revalidating its prior artifact and retained Granite source. | Executable; may create or change outputs |
| [`Invoke-Workbook05Phase3DependencyPreflight.ps1`](Invoke-Workbook05Phase3DependencyPreflight.ps1) | Runs the Invoke Workbook05Phase3DependencyPreflight PowerShell workflow. | Executable; may create or change outputs |
| [`Invoke-Workbook05Phase3DependencyPreflightLive.ps1`](Invoke-Workbook05Phase3DependencyPreflightLive.ps1) | Runs the Invoke Workbook05Phase3DependencyPreflightLive PowerShell workflow. | Executable; may create or change outputs |
| [`Invoke-Workbook05Preflight.ps1`](Invoke-Workbook05Preflight.ps1) | Orchestrates one read-only Workbook 05 preflight evidence capture. | Executable; may create or change outputs |
| [`Invoke-Workbook05RouteAGenAIBuild.ps1`](Invoke-Workbook05RouteAGenAIBuild.ps1) | Runs the Invoke Workbook05RouteAGenAIBuild PowerShell workflow. | Executable; may create or change outputs |
| [`Invoke-Workbook05RouteARuntimeBuild.ps1`](Invoke-Workbook05RouteARuntimeBuild.ps1) | Builds and installs the exact Workbook 05 Route A OpenVINO Runtime source. | Executable; may create or change outputs |
| [`Invoke-Workbook05RouteARuntimeResume.ps1`](Invoke-Workbook05RouteARuntimeResume.ps1) | Continues one exact, independently validated Route A Runtime build workspace. | Executable; may create or change outputs |
| [`Invoke-Workbook05RouteBBuild.ps1`](Invoke-Workbook05RouteBBuild.ps1) | Builds Route B only after the exact BR8 artifact and owner acceptance pass. | Executable; may create or change outputs |
| [`Invoke-Workbook05RouteBRepair.ps1`](Invoke-Workbook05RouteBRepair.ps1) | Runs the bounded Workbook 05 Route B repair, build and repository-test campaign. | Executable; may create or change outputs |
| [`Invoke-Workbook05SourceAdmission.ps1`](Invoke-Workbook05SourceAdmission.ps1) | Runs the read-only Workbook 05 Phase 1 source-admission pipeline. | Executable; may create or change outputs |
| [`measurement_controls.py`](measurement_controls.py) | Capture and validate the frozen Workbook 05 measurement controls. | Executable or importable tooling |
| [`requirements.phase3-assets.in`](requirements.phase3-assets.in) | Dependency list used to reproduce this Python environment. | Supporting repository file |
| [`requirements.phase3-assets.normal.in`](requirements.phase3-assets.normal.in) | Dependency list used to reproduce this Python environment. | Supporting repository file |
| [`requirements.phase3-bootstrap.in`](requirements.phase3-bootstrap.in) | Dependency list used to reproduce this Python environment. | Supporting repository file |
| [`requirements.phase3-bootstrap.txt`](requirements.phase3-bootstrap.txt) | Plain-text evidence or diagnostic output for requirements.phase3 bootstrap; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`requirements.txt`](requirements.txt) | Pinned or bounded Python packages needed by this testing area. | Supporting repository file |
| [`route_a_runtime_resume_bundle_validation.py`](route_a_runtime_resume_bundle_validation.py) | Python module providing `ValidationResult`, `_issue`, `_is_safe_relative_path`, `_files`, `_load_json`, `_validate_payloads`, and other helpers. | Executable or importable tooling |
| [`route_b_repair.py`](route_b_repair.py) | Apply and audit the bounded Workbook 05 Route B CMake repair. | Executable or importable tooling |
| [`route_b_repair_bundle_validation.py`](route_b_repair_bundle_validation.py) | Validate a Workbook 05 Route B repair artifact strictly as untrusted data. | Executable or importable tooling |
| [`schema_validation.py`](schema_validation.py) | Validate Workbook 05 JSON controls against versioned JSON Schemas. | Executable or importable tooling |
| [`source_admission.py`](source_admission.py) | Apply the non-negotiable source-admission gate for Workbook 05. | Executable or importable tooling |
| [`source_admission_bundle_validation.py`](source_admission_bundle_validation.py) | Validate a Workbook 05 source-admission bundle as untrusted data. | Executable or importable tooling |
| [`source_admission_orchestration.py`](source_admission_orchestration.py) | Run one controlled Workbook 05 source-admission step at a time. | Executable or importable tooling |
| [`source_admission_phase.py`](source_admission_phase.py) | Calculate and assemble truthful Workbook 05 Phase 1 decisions. | Executable or importable tooling |
| [`source_capability.py`](source_capability.py) | Classify codec-related source evidence without making runtime support claims. | Executable or importable tooling |
| [`source_verification.py`](source_verification.py) | Verify or clone immutable Workbook 05 external source trees. | Executable or importable tooling |
| [`Workbook05.Build.psm1`](Workbook05.Build.psm1) | Runs the Workbook05.Build PowerShell workflow. | Executable or importable tooling |
| [`Workbook05.ControlledProcess.psm1`](Workbook05.ControlledProcess.psm1) | Runs the Workbook05.ControlledProcess PowerShell workflow. | Executable or importable tooling |
| [`Workbook05.Preflight.psm1`](Workbook05.Preflight.psm1) | Collects and evaluates the read-only Workbook 05 Intel preflight. | Executable or importable tooling |
| [`Workbook05.SourceAdmission.psm1`](Workbook05.SourceAdmission.psm1) | Quotes one argument for ProcessStartInfo on Windows PowerShell 5.1. | Executable or importable tooling |
| [`workspace_policy.py`](workspace_policy.py) | Pure policy checks for the Workbook 05 external Windows workspace. | Executable or importable tooling |
| [`Write-Workbook05BuildBundleMetadata.ps1`](Write-Workbook05BuildBundleMetadata.ps1) | Binds a Workbook 05 build evidence directory to one exact workflow attempt. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)
- [phase3 guide](phase3/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
