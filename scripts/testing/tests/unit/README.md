# Unit

Checks small functions and modules in isolation.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Checks small functions and modules in isolation.

### Start here

These files test the testing framework. Start with the parent test guide, then choose `unit`, `integration` or `acceptance` according to the scope you need to check.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`test_animehacker_activation.py`](test_animehacker_activation.py) | Python module providing `AnimehackerActivationTests`. | Executable or importable tooling |
| [`test_animehacker_build_reconcile.py`](test_animehacker_build_reconcile.py) | Python module providing `AnimehackerBuildReconcileTests`. | Executable or importable tooling |
| [`test_animehacker_matrix.py`](test_animehacker_matrix.py) | Python module providing `AnimehackerMatrixTests`. | Executable or importable tooling |
| [`test_animehacker_quality.py`](test_animehacker_quality.py) | Python module providing `AnimehackerQualityTests`. | Executable or importable tooling |
| [`test_animehacker_source_audit.py`](test_animehacker_source_audit.py) | Python module providing `AnimehackerSourceAuditTests`. | Executable or importable tooling |
| [`test_archive_transaction.py`](test_archive_transaction.py) | Python module providing `_git`, `_status_hash`, `_sha`, `_record`, `source_repo`, `test_source_is_never_removed_by_copy_or_verify`, and other helpers. | Executable or importable tooling |
| [`test_atomicbot_full_quality.py`](test_atomicbot_full_quality.py) | Python module providing `test_call_with_deadline_returns_completed_value`, `test_call_with_deadline_enforces_total_wall_clock_limit`, `test_runner_lock_rejects_a_second_controller`, `test_completion_requires_six_terminal_prompt_measurements`. | Executable or importable tooling |
| [`test_atomicbot_matrix.py`](test_atomicbot_matrix.py) | Python module providing `AtomicBotMatrixTests`. | Executable or importable tooling |
| [`test_atomicbot_metrics.py`](test_atomicbot_metrics.py) | Python module providing `AtomicBotMetricTests`. | Executable or importable tooling |
| [`test_atomicbot_quality.py`](test_atomicbot_quality.py) | Python module providing `AtomicBotQualityTests`. | Executable or importable tooling |
| [`test_atomicbot_utilization.py`](test_atomicbot_utilization.py) | Python module providing `AtomicBotUtilizationTests`. | Executable or importable tooling |
| [`test_build_openvino_turboquant_wrapper.py`](test_build_openvino_turboquant_wrapper.py) | Python module providing `powershell_executable`, `test_wrapper_has_identity_bound_guarded_build_contract`, `test_wrapper_is_valid_powershell_and_rejects_parallelism_above_one`, `test_wrapper_rejects_an_existing_cache_for_another_source`, `test_python_build_rejects_cache_bound_to_another_interpreter`. | Executable or importable tooling |
| [`test_campaign_import_boundary.py`](test_campaign_import_boundary.py) | Python module providing `test_llama_atomicbot_and_animehacker_modules_use_campaign_namespace`, `test_superseded_campaign_import_paths_are_absent`. | Executable or importable tooling |
| [`test_cleanup_inventory.py`](test_cleanup_inventory.py) | Python module providing `_write`, `_git`, `cleanup_repo`, `test_inventory_preserves_referenced_failure_and_classifies_cleanup`, `test_inventory_is_deterministic_complete_and_hash_bound`, `test_inventory_rejects_a_source_outside_its_git_worktree`. | Executable or importable tooling |
| [`test_cleanup_semantics.py`](test_cleanup_semantics.py) | Python module providing `_csv`, `test_semantic_snapshot_is_deterministic_and_records_scientific_values`. | Executable or importable tooling |
| [`test_code_migration_inventory.py`](test_code_migration_inventory.py) | Python module providing `_inventory_rows`, `_migration_rows`, `_task1_root_scripts`, `_assert_category_path`, `_legacy_imports_and_paths`, `test_tracked_task1_root_scripts_have_a_complete_migration_inventory`, and other helpers. | Executable or importable tooling |
| [`test_final_results_csvio.py`](test_final_results_csvio.py) | Python module providing `test_write_csv_is_utf8_deterministic_and_preserves_declared_columns`, `test_write_json_is_utf8_and_byte_stable`, `test_validate_json_returns_json_pointer_paths_for_schema_errors`, `test_all_final_results_schemas_are_strict_and_accept_canonical_rows`, `test_attempt_schema_requires_non_whitespace_reason_for_each_non_passed_status`, `_evidence_row`, and other helpers. | Executable or importable tooling |
| [`test_final_results_docx.py`](test_final_results_docx.py) | Python module providing `_renderer`, `_parity_comparator`, `_report`, `_word_xml`, `test_render_docx_applies_professional_accessible_document_structure`, `test_render_docx_uses_landscape_only_beyond_the_wide_table_threshold`, and other helpers. | Executable or importable tooling |
| [`test_final_results_evidence.py`](test_final_results_evidence.py) | Python module providing `test_repo_relative_returns_portable_path_and_rejects_outside_path`, `test_repo_relative_rejects_symlink_resolution_escape`, `test_resolve_repository_path_translates_exact_legacy_ledger_entry`, `test_resolve_repository_path_translates_consistent_legacy_directory`, `test_resolve_repository_path_prefers_existing_path_and_rejects_escape`, `test_migrate_repository_references_rewrites_only_exact_ledger_tokens`, and other helpers. | Executable or importable tooling |
| [`test_final_results_layout.py`](test_final_results_layout.py) | Python module providing `_write_text`, `_write_bytes`, `_build_old_route`, `_materialize_new_layout`, `test_route_migration_plan_is_deterministic_collision_free_and_six_part`, `test_validation_consolidation_preserves_receipts_and_computes_overall_status`, and other helpers. | Executable or importable tooling |
| [`test_final_results_markdown.py`](test_final_results_markdown.py) | Python module providing `test_render_markdown_writes_canonical_two_section_report`, `test_render_markdown_rejects_table_rows_with_unequal_widths`. | Executable or importable tooling |
| [`test_final_results_models.py`](test_final_results_models.py) | Python module providing `test_source_statuses_map_without_losing_failure_kind`, `test_non_passed_attempt_requires_reason`, `test_passed_attempt_must_be_executed`, `test_status_exposes_normalized_value_and_display_label`, `test_canonical_records_serialize_stable_ids_and_nullable_metrics`, `test_evidence_record_rejects_nonportable_paths`, and other helpers. | Executable or importable tooling |
| [`test_generate_official_openvino_capability_specs.py`](test_generate_official_openvino_capability_specs.py) | Python module providing `_matrix`, `test_generates_four_exact_context_row_bound_specs`. | Executable or importable tooling |
| [`test_moved_tool_root_resolution.py`](test_moved_tool_root_resolution.py) | Python module providing `_powershell_executable`, `_ps_quote`, `_extract_snippet`, `_evaluate_script_snippet`, `test_run_openvino_reference_capability_supports_direct_help_execution`, `test_acquire_official_openvino_defaults_stay_anchored_to_repository_root`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_acquisition.py`](test_official_openvino_acquisition.py) | Python module providing `OfficialOpenVINOAcquisitionTests`. | Executable or importable tooling |
| [`test_official_openvino_adaptive_campaign_spec.py`](test_official_openvino_adaptive_campaign_spec.py) | Exact workload spec tests for the adaptive comparison contract. | Executable or importable tooling |
| [`test_official_openvino_adaptive_matrix.py`](test_official_openvino_adaptive_matrix.py) | Contract tests for the isolated adaptive format comparison matrix. | Executable or importable tooling |
| [`test_official_openvino_adaptive_metrics.py`](test_official_openvino_adaptive_metrics.py) | Python module providing `_binary_mib_receipt`, `_utilization`, `_activation`, `_command_hash`, `complete_record`, `_sample`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_artifact_inventory.py`](test_official_openvino_artifact_inventory.py) | Contract tests for hash-bound WB-04 adaptive artifact inventory. | Executable or importable tooling |
| [`test_official_openvino_campaign_matrix_binding.py`](test_official_openvino_campaign_matrix_binding.py) | Python module providing `_case`, `_spec`, `_record`, `test_worker_spec_is_bound_to_exact_matrix_algorithms_precision_and_norm`, `test_runtime_activation_is_bound_to_matrix_and_rejects_fallback`, `test_campaign_binding_consumes_the_frozen_explicit_contract_not_a_rederivation`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_campaign_spec.py`](test_official_openvino_campaign_spec.py) | Python module providing `_clean_inputs`, `_clean_u4_model`, `_generate`, `_read_spec`, `test_generation_expands_every_runnable_context_and_records_gpu_rejection`, `test_worker_specs_bind_exact_matrix_runtime_contract`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_diagnostics.py`](test_official_openvino_diagnostics.py) | Python module providing `OfficialOpenVINODiagnosticTests`. | Executable or importable tooling |
| [`test_official_openvino_format_boundary.py`](test_official_openvino_format_boundary.py) | Python module providing `_controlled_build_root`, `_stub_committed_model_hashes`, `projected_inputs`, `test_projection_rejects_non_executable_build_before_emitting_inputs`, `test_projection_rejects_campaign_build_overlap_before_emitting_inputs`, `test_projection_binds_executable_build_and_detects_same_size_drift`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_matrix.py`](test_official_openvino_matrix.py) | Python module providing `OfficialOpenVINOMatrixTests`. | Executable or importable tooling |
| [`test_official_openvino_metrics.py`](test_official_openvino_metrics.py) | Python module providing `utilization`, `sample`, `OfficialOpenVINOMetricTests`. | Executable or importable tooling |
| [`test_official_openvino_patched_build.py`](test_official_openvino_patched_build.py) | Python module providing `sha256`, `patch_series_sha256`, `PatchedBuildManifestTests`. | Executable or importable tooling |
| [`test_official_openvino_quality.py`](test_official_openvino_quality.py) | Python module providing `canonical_sha256`, `gate_response`, `evaluate_gate`, `review_metadata`, `review_span`, `p1_evidence`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_reconcile.py`](test_official_openvino_reconcile.py) | Python module providing `OfficialOpenVINOReconcileTests`. | Executable or importable tooling |
| [`test_official_openvino_runner.py`](test_official_openvino_runner.py) | Python module providing `OfficialOpenVINORunnerTests`. | Executable or importable tooling |
| [`test_official_openvino_source_audit.py`](test_official_openvino_source_audit.py) | Python module providing `OfficialOpenVINOSourceAuditTests`. | Executable or importable tooling |
| [`test_official_openvino_worker_contract.py`](test_official_openvino_worker_contract.py) | Python module providing `test_formal_worker_requires_exact_context_token_count`, `test_formal_worker_rejects_context_token_mismatch`. | Executable or importable tooling |
| [`test_official_openvino_workload.py`](test_official_openvino_workload.py) | Python module providing `test_context_workload_is_deterministic_and_declares_exact_prefill`, `test_context_workload_rejects_out_of_contract_context`. | Executable or importable tooling |
| [`test_openvino_campaign_import_boundary.py`](test_openvino_campaign_import_boundary.py) | Python module providing `test_openvino_modules_use_the_campaign_namespace`, `test_superseded_openvino_package_is_absent`. | Executable or importable tooling |
| [`test_parse_llama_measurement.py`](test_parse_llama_measurement.py) | Python module providing `load_module`, `MeasurementParserTests`. | Executable or importable tooling |
| [`test_pytest_scope.py`](test_pytest_scope.py) | Regression coverage for repository-owned pytest collection scope. | Executable or importable tooling |
| [`test_release_reproduction_contract.py`](test_release_reproduction_contract.py) | Python module providing `test_release_reproduction_guide_is_cli_only_read_only_and_no_rerun`, `test_published_reproduction_guide_matches_maintained_renderer`. | Executable or importable tooling |
| [`test_reporting_import_boundary.py`](test_reporting_import_boundary.py) | Python module providing `test_reporting_is_the_only_active_results_package`, `test_reporting_public_interfaces_are_stable`. | Executable or importable tooling |
| [`test_test_path_migration.py`](test_test_path_migration.py) | Python module providing `_tracked_tests`, `_migration_rows`, `_collection_node_ids`, `_manifest_node_ids`, `_normalize_post_move_node_id`, `_historical_collection_node_ids`, and other helpers. | Executable or importable tooling |
| [`test_workbook_filter.py`](test_workbook_filter.py) | Python module providing `load_script`, `WorkbookFilterTests`. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
