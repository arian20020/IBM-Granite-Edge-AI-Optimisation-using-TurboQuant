namespace GraniteEdgeAI.EndToEndTests.Journeys;

internal static class JourneyCatalogue
{
    internal static readonly string[] RequiredNames =
    [
        "Package_launch_has_exactly_one_onboarding_shell",
        "Chat_has_no_onboarding_footer_or_indicator_R2",
        "Picker_and_explorer_drag_drop_ingress",
        "Gguf_and_openvino_direct_chat_when_fit",
        "Optional_optimization_when_current_model_fits",
        "Required_optimization_when_only_alternative_fits",
        "No_execution_when_no_safe_configuration_R2",
        "Gguf_optimization_reinspection_chat_exact_export",
        "Openvino_persistent_conversion_reinspection_chat_exact_export",
        "Openvino_runtime_configuration_chat_prohibits_model_export",
        "Recommended_download_success_cancel_integrity_retry_cleanup",
        "Changed_model_or_hardware_evidence_rejected",
        "Optimization_cancel_restart_rejects_stale",
        "Duplicate_or_late_publication_rejected",
        "Publication_failure_preserves_original",
        "Chat_send_newline_stop_continue_second_turn_reload_disposal",
        "Restart_recovery_rejects_stale_identity_R2",
        "Malformed_input_and_import_cancel_recover",
        "Disabled_actions_inaccessible_by_keyboard_and_automation",
        "No_orphan_processes_after_every_test",
    ];
}
