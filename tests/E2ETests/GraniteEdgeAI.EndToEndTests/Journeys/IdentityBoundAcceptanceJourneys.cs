namespace GraniteEdgeAI.EndToEndTests.Journeys;

[TestClass]
[TestCategory("NativeAcceptance")]
public sealed class IdentityBoundAcceptanceJourneys
{
    private static readonly string[] ProducerEvidence = ["GRANITE_E2E_H1_MANIFEST", "GRANITE_E2E_M1_MANIFEST", "GRANITE_E2E_Q1_MANIFEST"];

    [TestMethod] public void Gguf_picker_fit_source_reaches_direct_chat() => RequireRoute("GRANITE_E2E_GGUF_FIT_PATH", "Choose GGUF file");
    [TestMethod] public void Gguf_drag_drop_optimizes_reinspects_chats_and_exports_exact_output() => RequireRoute("GRANITE_E2E_GGUF_OPTIMIZE_PATH", "Model drop area");
    [TestMethod] public void OpenVino_fit_source_reaches_direct_chat() => RequireRoute("GRANITE_E2E_OPENVINO_FIT_PATH", "Choose OpenVINO folder");
    [TestMethod] public void OpenVino_optimization_preserves_persistent_or_runtime_configuration_identity() => RequireRoute("GRANITE_E2E_OPENVINO_OPTIMIZE_PATH", "Choose OpenVINO folder");
    [TestMethod] public void Compatibility_unknown_or_no_fit_keeps_execution_disabled() => RequireRoute("GRANITE_E2E_NO_FIT_PATH", "Continue to model inspection");
    [TestMethod] public void Optimization_cancellation_restart_rejects_stale_result() => RequireRoute("GRANITE_E2E_GGUF_OPTIMIZE_PATH", "Optimise this model before chatting");
    [TestMethod] public void Artifact_publication_failure_does_not_replace_original_model() => RequireRoute("GRANITE_E2E_GGUF_OPTIMIZE_PATH", "Optimisation destination");
    [TestMethod] public void Chat_supports_enter_newline_stop_second_turn_and_reload() => RequireRoute("GRANITE_E2E_CHAT_READY_PATH", "Message");
    [TestMethod] public void Application_restart_recovers_allowed_state_and_rejects_stale_identity() => RequireRoute("GRANITE_E2E_GGUF_FIT_PATH", "Choose model file");
    [TestMethod] public void Chat_has_no_onboarding_indicator_or_footer() => RequireRoute("GRANITE_E2E_CHAT_READY_PATH", "Conversation messages");

    private static void RequireRoute(string assetVariable, string firstAccessibleSurface)
    {
        PackagedJourneyFixture.Run(firstAccessibleSurface, session =>
        {
            Assert.IsNotNull(session.ByName(firstAccessibleSurface, CancellationToken.None));
        }, ProducerEvidence.Append(assetVariable).ToArray());
    }
}
