# Integration

Checks that several testing components work together.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Checks that several testing components work together.

### Start here

These files test the testing framework. Start with the parent test guide, then choose `unit`, `integration` or `acceptance` according to the scope you need to check.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`canonical_fixture.py`](canonical_fixture.py) | Canonical disposable-fixture helpers for the cleaned raw-evidence tree. | Executable or importable tooling |
| [`test_adjudicate_official_openvino_adaptive_quality.py`](test_adjudicate_official_openvino_adaptive_quality.py) | Python module providing `_canonical_hash`, `_canonical_sha256`, `_privacy_surface`, `PassingGuardRunner`, `_install_standard_sampler`, `_write_task5_pretty_json`, and other helpers. | Executable or importable tooling |
| [`test_adjudicate_official_openvino_quality.py`](test_adjudicate_official_openvino_quality.py) | Python module providing `score_sheet`, `_write_governed_json`, `_write_guard_json`, `_governed_capture_root`, `_add_governed_capture`, `_governed_scoring_input`, and other helpers. | Executable or importable tooling |
| [`test_animehacker_large_host.py`](test_animehacker_large_host.py) | Python module providing `AnimehackerLargeHostTests`. | Executable or importable tooling |
| [`test_animehacker_reconcile.py`](test_animehacker_reconcile.py) | Python module providing `AnimehackerReconcileTests`. | Executable or importable tooling |
| [`test_animehacker_runner.py`](test_animehacker_runner.py) | Python module providing `case`, `complete_runtime_summary`, `AnimehackerRunnerTests`. | Executable or importable tooling |
| [`test_atomicbot_reconcile.py`](test_atomicbot_reconcile.py) | Python module providing `AtomicBotReconcileTests`. | Executable or importable tooling |
| [`test_atomicbot_runner.py`](test_atomicbot_runner.py) | Python module providing `AtomicBotRunnerTests`. | Executable or importable tooling |
| [`test_capture_official_openvino_quality.py`](test_capture_official_openvino_quality.py) | Python module providing `FakeGenerator`, `_sha256_text`, `_sha256_json`, `_read_exact`, `_write_measurement_summary`, `_capture`, and other helpers. | Executable or importable tooling |
| [`test_final_results_animehacker.py`](test_final_results_animehacker.py) | Python module providing `isolated_route`, `_copy_sources`, `test_final_status_authority_has_seven_completed_and_three_safety_rows`, `test_runtime_summaries_are_measurement_authority_and_rejected_rows_are_excluded`, `test_rejected_runtime_is_retained_as_historical_attempt_and_failure`, `test_missing_os_metrics_remain_literal_not_collected`, and other helpers. | Executable or importable tooling |
| [`test_final_results_atomicbot.py`](test_final_results_atomicbot.py) | Python module providing `canonical_repository`, `isolated_route`, `_materialized_sources`, `_copy_sources`, `test_clean_index_excludes_archived_atomicbot_rows_and_fixture_recovers_them`, `test_clean_head_builds_atomicbot_from_retained_canonical_evidence`, and other helpers. | Executable or importable tooling |
| [`test_final_results_comparison.py`](test_final_results_comparison.py) | Python module providing `_bundle`, `_text`, `_tables`, `test_throughput_direct_comparison_requires_every_protocol_dimension`, `test_keyed_throughput_comparison_rejects_relationship_preserving_set_swap`, `test_throughput_preserves_repetition_keyed_token_relationships`, and other helpers. | Executable or importable tooling |
| [`test_final_results_openvino_experimental.py`](test_final_results_openvino_experimental.py) | Python module providing `_use_retained_fv6_authority`, `_rows`, `_sha256`, `_write_rows`, `_isolated_fv6_repo`, `_coexisting_fv6_repo`, and other helpers. | Executable or importable tooling |
| [`test_final_results_openvino_official.py`](test_final_results_openvino_official.py) | Python module providing `_canonical_source_path`, `_canonical_relative`, `_rows`, `_write_rows`, `_isolated_official_repo`, `_canonical_official_repository`, and other helpers. | Executable or importable tooling |
| [`test_final_results_openvino_reports.py`](test_final_results_openvino_reports.py) | Python module providing `_canonical_openvino_repository`, `_module`, `_text`, `_tables`, `_local_repo_paths`, `test_reports_follow_the_approved_sections_and_derive_campaign_accounting`, and other helpers. | Executable or importable tooling |
| [`test_final_results_upstream_llama.py`](test_final_results_upstream_llama.py) | Python module providing `_module`, `_text`, `test_bundle_covers_exact_matrix_completed_workloads_and_narrow_claims`, `test_repetitions_expand_from_source_evidence_and_missing_values_are_not_zero`, `test_evidence_index_paths_resolve_and_hashes_match_every_admitted_log`, `test_absent_raw_results_are_documented_not_fabricated`, and other helpers. | Executable or importable tooling |
| [`test_measure_official_openvino_sequence.py`](test_measure_official_openvino_sequence.py) | Python module providing `_binary_mib_receipt`, `_write_json`, `_setup_campaign`, `_identity_kwargs`, `_utilization`, `_record`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_adaptive_campaign.py`](test_official_openvino_adaptive_campaign.py) | Policy and orchestration tests for the adaptive comparison controller. | Executable or importable tooling |
| [`test_official_openvino_adaptive_quality.py`](test_official_openvino_adaptive_quality.py) | Python module providing `_canonical`, `_sha256_json`, `_resign_recovery`, `_signed_result`, `RecordingGuardRunner`, `_capture_three_summary_history`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_comparison_reconcile.py`](test_official_openvino_comparison_reconcile.py) | Behavioral tests for independent adaptive comparison reconciliation. | Executable or importable tooling |
| [`test_official_openvino_conversion.py`](test_official_openvino_conversion.py) | Python module providing `OfficialOpenVINOConversionTests`, `OfficialOpenVINOArtifactTests`. | Executable or importable tooling |
| [`test_official_openvino_expected_rejections.py`](test_official_openvino_expected_rejections.py) | Python module providing `canonical_bytes`, `OfficialOpenVINOExpectedRejectionTests`. | Executable or importable tooling |
| [`test_official_openvino_format_boundary_controller.py`](test_official_openvino_format_boundary_controller.py) | Python module providing `_controlled_tokenizer_dependency`, `_write_json`, `_adaptive_input`, `test_runtime_reconciliation_reopens_and_hashes_all_canonical_evidence`, `test_runtime_reconciliation_rejects_drift_and_incomplete_metrics`, `test_quality_reconciliation_reopens_six_prompt_receipts_and_hashes`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_guarded_build.py`](test_official_openvino_guarded_build.py) | Python module providing `_guarded_build`, `_owned_guard`, `_limits`, `_run`, `_assert_zero_survivors`, `_assert_memory_evidence`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_measurement.py`](test_official_openvino_measurement.py) | Python module providing `write_csv`, `utilization_summary`, `activation_telemetry`, `worker_result`, `standard_cpu_telemetry`, `governed_record`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_patch_identity.py`](test_official_openvino_patch_identity.py) | Python module providing `git`, `PatchIdentityTests`, `PatchWorkspaceControllerTests`. | Executable or importable tooling |
| [`test_official_openvino_quality_campaign.py`](test_official_openvino_quality_campaign.py) | Python module providing `_write_json`, `_full_quality_cli_args`, `_without_cli_option`, `_legacy_quality_cli_args`, `test_quality_cli_requires_identity_paths_and_exact_ram_floor`, `test_quality_cli_help_runs_directly_as_a_script`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_quality_worker.py`](test_official_openvino_quality_worker.py) | Python module providing `_sha256_text`, `_canonical_worker_hash`, `_spec`, `_FakeGenAI`, `_worker`, `_prompt_worker_cli_fixture`, and other helpers. | Executable or importable tooling |
| [`test_official_openvino_scalar_semantic_rejections.py`](test_official_openvino_scalar_semantic_rejections.py) | Python module providing `canonical_bytes`, `generate`, `copied_evidence`, `rewrite_json`, `generate_paths`, `rewrite_attempt_streams`, and other helpers. | Executable or importable tooling |
| [`test_retained_evidence_migration.py`](test_retained_evidence_migration.py) | Python module providing `_rows`, `_sha256_bytes`, `_sha256`, `_active_raw_rows`, `_destination`, `_mapping`, and other helpers. | Executable or importable tooling |
| [`test_run_official_openvino_quality.py`](test_run_official_openvino_quality.py) | Python module providing `contains_null`, `RecordingExecutor`, `OfficialOpenVINOQualityRunnerTests`. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
