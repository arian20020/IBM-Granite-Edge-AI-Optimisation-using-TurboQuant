# Acceptance

Checks complete contributor-facing workflows and release behaviour.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Checks complete contributor-facing workflows and release behaviour.

### Start here

These files test the testing framework. Start with the parent test guide, then choose `unit`, `integration` or `acceptance` according to the scope you need to check.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`test_build_final_results.py`](test_build_final_results.py) | Python module providing `_write_pdf`, `_write_compact_build_fixture`, `_write_route`, `_fixture_bundle`, `_write_cross_route`, `_collection`, and other helpers. | Executable or importable tooling |
| [`test_build_official_openvino_release_evidence.py`](test_build_official_openvino_release_evidence.py) | Python module providing `_sha256`, `_canonical_sha256`, `_copy_builder_sources`, `StaticSectionParsingTests`, `ReleaseEvidenceBuildTests`. | Executable or importable tooling |
| [`test_cleanup_finalization.py`](test_cleanup_finalization.py) | Python module providing `_rows`, `_sha256`, `_canonical_hash`, `test_implementation_root_removal_receipt_covers_exact_archived_raw_set`, `test_frozen_text_bytes_survive_the_git_tree_and_plain_archive`, `test_compact_final_semantic_snapshot_equals_frozen_task_one_baseline`, and other helpers. | Executable or importable tooling |
| [`test_cleanup_removal_receipt.py`](test_cleanup_removal_receipt.py) | Python module providing `_git`, `_git_bytes`, `_status_sha256`, `_sha256_bytes`, `_sha256`, `_json_bytes`, and other helpers. | Executable or importable tooling |
| [`test_final_results_pdf_export.py`](test_final_results_pdf_export.py) | Python module providing `_find_word_executable`, `_renderer`, `_powershell`, `_invoke_export`, `_word_process_ids`, `_process_exists`, and other helpers. | Executable or importable tooling |
| [`test_final_results_release.py`](test_final_results_release.py) | Python module providing `_git_index_release_blobs`, `_published_evidence_sources`, `_write_filesystem_manifest`, `canonical_release`, `_markdown_targets`, `_type_names`, and other helpers. | Executable or importable tooling |
| [`test_final_results_workbook_portability.py`](test_final_results_workbook_portability.py) | Python module providing `_sha256`, `test_sanitizer_replaces_machine_paths_without_changing_workbook_semantics`, `test_real_openvino_portable_workbooks_preserve_values_and_publish_provenance`, `test_route_writer_publishes_only_the_selected_openvino_derivative`. | Executable or importable tooling |
| [`test_finalize_official_openvino_comparison_workbook.py`](test_finalize_official_openvino_comparison_workbook.py) | Behavioral tests for the fail-closed WB-04 v1.9 comparison renderer. | Executable or importable tooling |
| [`test_finalize_official_openvino_workbook.py`](test_finalize_official_openvino_workbook.py) | Python module providing `_sha256`, `_canonical_sha256`, `_runtime_json_sha256`, `_aggregate`, `_case`, `_write_measurement`, and other helpers. | Executable or importable tooling |
| [`test_llama_route_layout_migration.py`](test_llama_route_layout_migration.py) | Python module providing `_rows`, `_sha256`, `_migrated_relationships`, `_baseline_rows`, `test_llama_route_migration_preserves_frozen_semantics_and_report_evidence`. | Executable or importable tooling |
| [`test_measure_official_openvino_cli.py`](test_measure_official_openvino_cli.py) | Python module providing `_fake_build`, `test_worker_environment_prepends_exact_build_and_runtime_libraries`, `test_worker_environment_rejects_build_without_python_module`, `test_single_measurement_binds_worker_command_and_returns_guard_record`, `test_single_measurement_rejects_spec_role_mismatch`, `test_cli_can_be_invoked_directly_from_repository_root`. | Executable or importable tooling |
| [`test_official_openvino_docx.py`](test_official_openvino_docx.py) | Python module providing `OfficialOpenVINODocxAuditTests`. | Executable or importable tooling |
| [`test_openvino_route_layout_migration.py`](test_openvino_route_layout_migration.py) | Python module providing `_sha256`, `_rows`, `_migrated_relationships`, `_baseline_rows`, `_assert_portable_workbook_has_no_absolute_paths`, `test_openvino_routes_preserve_semantics_evidence_and_portable_derivatives`, and other helpers. | Executable or importable tooling |
| [`test_publish_official_openvino_comparison.py`](test_publish_official_openvino_comparison.py) | Publication tests for governed WB-04 adaptive-comparison checkpoints. | Executable or importable tooling |
| [`test_testing_cli.py`](test_testing_cli.py) | Python module providing `run_module`, `test_supported_cli_help_is_side_effect_free`, `test_route_cli_help_lists_documented_modes`, `test_build_results_help_lists_documented_routes`, `test_validate_results_help_reuses_build_routes_without_exposing_toggle`, `test_route_cli_missing_mode_exits_2`, and other helpers. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
