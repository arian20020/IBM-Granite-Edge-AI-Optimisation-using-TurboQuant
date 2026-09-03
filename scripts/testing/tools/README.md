# Tools

Contains lower-level migration, capture, conversion, reconciliation and validation utilities.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains lower-level migration, capture, conversion, reconciliation and validation utilities.

### Start here

Use the [supported CLI](../cli/README.md) when it provides the task you need. Run a lower-level tool directly only after reading its source and required arguments.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`__init__.py`](__init__.py) | Marks this directory as a Python package and may expose its public imports. | Executable or importable tooling |
| [`acquire_animehacker_retest.ps1`](acquire_animehacker_retest.ps1) | Runs the acquire animehacker retest PowerShell workflow. | Executable or importable tooling |
| [`acquire_official_openvino.ps1`](acquire_official_openvino.ps1) | Runs the acquire official openvino PowerShell workflow. | Executable or importable tooling |
| [`adjudicate_animehacker_quality.py`](adjudicate_animehacker_quality.py) | Apply the frozen harsh rubric to WB-03 response evidence. | Executable or importable tooling |
| [`adjudicate_atomicbot_quality.py`](adjudicate_atomicbot_quality.py) | Create content-keyed, harsh GTQ-QUALITY-RUBRIC-v1 adjudications. | Executable or importable tooling |
| [`adjudicate_official_openvino_adaptive_quality.py`](adjudicate_official_openvino_adaptive_quality.py) | Blind, two-order adjudication for adaptive OpenVINO quality captures. | Executable or importable tooling |
| [`adjudicate_official_openvino_quality.py`](adjudicate_official_openvino_quality.py) | Build blind WB-04 scoring inputs and apply the frozen harsh rubric. | Executable or importable tooling |
| [`Apply-Workbook-Revision-History.py`](Apply-Workbook-Revision-History.py) | Apply the Git-controlled revision history to the six generated testing workbooks. | Executable or importable tooling |
| [`archive_transaction.py`](archive_transaction.py) | Plan, copy, and verify a copy-only external testing-history archive. | Executable or importable tooling |
| [`audit_animehacker_docx.py`](audit_animehacker_docx.py) | Structural fallback QA when a compatible DOCX renderer is unavailable. | Executable or importable tooling |
| [`audit_animehacker_source.py`](audit_animehacker_source.py) | Evidence-first source audit for the animehacker TQ3_0 fork. | Executable or importable tooling |
| [`audit_official_openvino_docx.py`](audit_official_openvino_docx.py) | Audit generated WB-04 and persist the structural QA record. | Executable or importable tooling |
| [`build_animehacker.ps1`](build_animehacker.ps1) | Runs the build animehacker PowerShell workflow. | Executable; may create or change outputs |
| [`build_official_openvino_adaptive_matrix.py`](build_official_openvino_adaptive_matrix.py) | Materialize the isolated adaptive format comparison matrix from receipts. | Executable; may create or change outputs |
| [`build_official_openvino_boundary_index.py`](build_official_openvino_boundary_index.py) | Build an explicit reusable-boundary index from one historical root. | Executable; may create or change outputs |
| [`build_official_openvino_comparison_release_evidence.py`](build_official_openvino_comparison_release_evidence.py) | Build and validate immutable adaptive-comparison release evidence. | Executable; may create or change outputs |
| [`build_official_openvino_release_evidence.py`](build_official_openvino_release_evidence.py) | Build deterministic, source-bound release evidence for controlled WB-04. | Executable; may create or change outputs |
| [`build_openvino_turboquant.ps1`](build_openvino_turboquant.ps1) | Runs the build openvino turboquant PowerShell workflow. | Executable; may create or change outputs |
| [`Capture-TestEnvironment.ps1`](Capture-TestEnvironment.ps1) | Captures a Windows machine manifest for controlled testing. | Executable or importable tooling |
| [`cleanup_inventory.py`](cleanup_inventory.py) | Build a deterministic, Git-aware inventory for the testing cleanup. | Executable or importable tooling |
| [`cleanup_semantics.py`](cleanup_semantics.py) | Freeze a deterministic semantic snapshot of the published result library. | Executable or importable tooling |
| [`collect_openvino_runtime_utilization.ps1`](collect_openvino_runtime_utilization.ps1) | Runs the collect openvino runtime utilization PowerShell workflow. | Executable or importable tooling |
| [`collect_process_utilization.ps1`](collect_process_utilization.ps1) | Runs the collect process utilization PowerShell workflow. | Executable or importable tooling |
| [`convert_official_openvino_models.py`](convert_official_openvino_models.py) | Safely execute or terminally classify WB-04 OpenVINO conversions. | Executable; may create or change outputs |
| [`evaluate_atomicbot_full_quality.py`](evaluate_atomicbot_full_quality.py) | Validate manual adjudications and publish all-row AtomicBot quality evidence. | Executable or importable tooling |
| [`finalize_animehacker_build_evidence.py`](finalize_animehacker_build_evidence.py) | Reconcile WB-03 build routes from raw logs and binary hashes. | Executable; may create or change outputs |
| [`finalize_animehacker_runtime_state.py`](finalize_animehacker_runtime_state.py) | Rebuild the crash-resume state from terminal WB-03 evidence. | Executable; may create or change outputs |
| [`finalize_animehacker_workbook_control.py`](finalize_animehacker_workbook_control.py) | Update WB-03 controlled-manifest hashes after deterministic generation. | Executable; may create or change outputs |
| [`finalize_atomicbot_registers.py`](finalize_atomicbot_registers.py) | Reconcile WB-02 evidence into the controlled CSV registers. | Executable; may create or change outputs |
| [`finalize_official_openvino_comparison_workbook.py`](finalize_official_openvino_comparison_workbook.py) | Render and validate the hash-bound WB-04 adaptive comparison workbook. | Executable; may create or change outputs |
| [`finalize_official_openvino_workbook.py`](finalize_official_openvino_workbook.py) | Fail-closed reconciliation and Markdown finalization for controlled WB-04. | Executable; may create or change outputs |
| [`Generate-Controlled-Workbooks.py`](Generate-Controlled-Workbooks.py) | Generate the six controlled DOCX testing workbooks from source-controlled Markdown. | Executable; may create or change outputs |
| [`Generate-P5-Long-Context-Fixture.py`](Generate-P5-Long-Context-Fixture.py) | Generate the deterministic P5 long-context retrieval fixture. | Executable; may create or change outputs |
| [`generate_official_openvino_adaptive_specs.py`](generate_official_openvino_adaptive_specs.py) | CLI for generating adaptive comparison worker specs without inference. | Executable; may create or change outputs |
| [`generate_official_openvino_capability_specs.py`](generate_official_openvino_capability_specs.py) | Generate exact-context, row-bound WB-04 TurboQuant capability specs. | Executable; may create or change outputs |
| [`generate_official_openvino_expected_rejections.py`](generate_official_openvino_expected_rejections.py) | Write and validate deterministic WB-04 expected-rejection evidence. | Executable; may create or change outputs |
| [`generate_official_openvino_scalar_rejections.py`](generate_official_openvino_scalar_rejections.py) | Generate create-only WB-04 scalar semantic rejection evidence. | Executable; may create or change outputs |
| [`generate_official_openvino_specs.py`](generate_official_openvino_specs.py) | Generate formal U8 Granite 3B WB-04 worker-spec templates. | Executable; may create or change outputs |
| [`import_animehacker_large_host.py`](import_animehacker_large_host.py) | Validate a completed large-host WB-03 evidence package before workbook import. | Executable or importable tooling |
| [`index_atomicbot_quality_evidence.py`](index_atomicbot_quality_evidence.py) | Index the complete 2026-07-17 AtomicBot quality evidence tree. | Executable or importable tooling |
| [`Initialize-Controlled-Testing-Workspace.ps1`](Initialize-Controlled-Testing-Workspace.ps1) | Creates the required controlled testing directories without overwriting evidence. | Executable or importable tooling |
| [`invoke_guarded_command.ps1`](invoke_guarded_command.ps1) | Runs the invoke guarded command PowerShell workflow. | Executable; may create or change outputs |
| [`measure_official_openvino.py`](measure_official_openvino.py) | Run one fail-closed, fully instrumented OpenVINO WB-04 measurement. | Executable or importable tooling |
| [`New-Controlled-TestRun.ps1`](New-Controlled-TestRun.ps1) | Creates the complete folder and manifest skeleton for one controlled test execution. | Executable; may create or change outputs |
| [`New-EvidenceHashManifest.ps1`](New-EvidenceHashManifest.ps1) | Creates a SHA-256 manifest for all evidence belonging to one controlled run. | Executable; may create or change outputs |
| [`prepare_official_openvino_adaptive_artifacts.py`](prepare_official_openvino_adaptive_artifacts.py) | Build a hash-bound WB-04 adaptive artifact inventory without inference. | Executable or importable tooling |
| [`prepare_openvino_cpu_observer_patch.ps1`](prepare_openvino_cpu_observer_patch.ps1) | Runs the prepare openvino cpu observer patch PowerShell workflow. | Executable or importable tooling |
| [`prepare_openvino_turboquant_patch.ps1`](prepare_openvino_turboquant_patch.ps1) | Runs the prepare openvino turboquant patch PowerShell workflow. | Executable or importable tooling |
| [`publish_official_openvino_comparison.py`](publish_official_openvino_comparison.py) | Publish evidence-bound WB-04 adaptive comparison checkpoints. | Executable; may create or change outputs |
| [`reconcile_ab15m_utilization.py`](reconcile_ab15m_utilization.py) | Reconcile the accepted AB-15M utilization rerun into WB-02 controls. | Executable or importable tooling |
| [`reconcile_animehacker_workbook.py`](reconcile_animehacker_workbook.py) | Final completeness and evidence reconciliation for WB-03. | Executable or importable tooling |
| [`reconcile_atomicbot_all_utilization.py`](reconcile_atomicbot_all_utilization.py) | Reconcile all-row AtomicBot CPU/GPU utilization into WB-02 v1.7. | Executable or importable tooling |
| [`run_animehacker_large_host.py`](run_animehacker_large_host.py) | Portable serial launcher for guarded WB-03 completion on a 32 GiB+ host. | Executable; may create or change outputs |
| [`run_animehacker_quality.py`](run_animehacker_quality.py) | Run the frozen P1-P6 quality screen for validated WB-03 runtime rows. | Executable; may create or change outputs |
| [`run_animehacker_retest.py`](run_animehacker_retest.py) | Crash-resumable WB-03 runtime measurement controller. | Executable; may create or change outputs |
| [`run_atomicbot_full_quality.py`](run_atomicbot_full_quality.py) | Run GTQ-PROMPTS-v1 for every formal AtomicBot runtime row. | Executable; may create or change outputs |
| [`run_atomicbot_quality_retest.py`](run_atomicbot_quality_retest.py) | Run the frozen GTQ-PROMPTS-v1 quality screen against blind cache labels. | Executable; may create or change outputs |
| [`run_atomicbot_retest.py`](run_atomicbot_retest.py) | Command-line entry point for the controlled AtomicBot retest. | Executable; may create or change outputs |
| [`run_atomicbot_server_metrics.py`](run_atomicbot_server_metrics.py) | Collect loaded-server TTFT, process-tree RAM, and KV allocation for WB-02. | Executable; may create or change outputs |
| [`run_official_openvino_adaptive_comparison.py`](run_official_openvino_adaptive_comparison.py) | CLI for the guarded resumable adaptive OpenVINO comparison. | Executable; may create or change outputs |
| [`run_official_openvino_adaptive_quality.py`](run_official_openvino_adaptive_quality.py) | CLI for isolated adaptive OpenVINO quality capture and recovery. | Executable; may create or change outputs |
| [`run_official_openvino_diagnostics.py`](run_official_openvino_diagnostics.py) | Execute and reconcile WB-04 official OpenVINO capability diagnostics. | Executable; may create or change outputs |
| [`run_official_openvino_format_boundary.py`](run_official_openvino_format_boundary.py) | Run, preflight, resume, or inspect the guarded OpenVINO boundary campaign. | Executable; may create or change outputs |
| [`run_official_openvino_quality.py`](run_official_openvino_quality.py) | Run the frozen WB-04 P1-P6 screen through an evidence-only worker contract. | Executable; may create or change outputs |
| [`run_official_openvino_retest.py`](run_official_openvino_retest.py) | Reconcile every WB-04 runtime row to measured or sourced terminal state. | Executable; may create or change outputs |
| [`run_openvino_reference_capability.py`](run_openvino_reference_capability.py) | Measure two fresh production-core processes and publish strict capability evidence. | Executable; may create or change outputs |
| [`update_animehacker_quality_register.py`](update_animehacker_quality_register.py) | Idempotently append WB-03 P1-P6 adjudications to the central quality register. | Executable or importable tooling |
| [`Validate-Controlled-Testing-Workspace.ps1`](Validate-Controlled-Testing-Workspace.ps1) | Validates the controlled testing workspace before a hardware test is started. | Executable or importable tooling |
| [`Validate-OpenVINO-Codec-Extension.ps1`](Validate-OpenVINO-Codec-Extension.ps1) | Validates the OpenVINO codec workbook extension before any codec run starts. | Executable or importable tooling |
| [`Validate-Workbook-Revision-Control.py`](Validate-Workbook-Revision-Control.py) | Validate workbook revision, source, template and generated-DOCX controls. | Executable or importable tooling |
| [`verify_openvino_turboquant_build.py`](verify_openvino_turboquant_build.py) | Validate the immutable build/test manifest for the patched WB-04 runtime. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
