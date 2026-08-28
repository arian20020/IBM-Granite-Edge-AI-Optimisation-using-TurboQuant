namespace GraniteEdgeAI.EndToEndTests.Journeys;

[TestClass]
[TestCategory("NativeAcceptance")]
public sealed class RequiredAcceptanceJourneys
{
    [TestMethod] public void Gguf_and_openvino_direct_chat_when_fit() => Guard(nameof(Gguf_and_openvino_direct_chat_when_fit), "Choose model file", "Route-specific executable direct-Chat cases require both authorized assets in one campaign.", "GRANITE_E2E_GGUF_FIT_PATH", "GRANITE_E2E_OPENVINO_FIT_PATH");
    [TestMethod] public void Optional_optimization_when_current_model_fits() => Guard(nameof(Optional_optimization_when_current_model_fits), "Optimise this model before chatting", "No producer evidence currently distinguishes optional-fit optimization from required optimization.", "GRANITE_E2E_GGUF_FIT_PATH");
    [TestMethod] public void Required_optimization_when_only_alternative_fits() => Guard(nameof(Required_optimization_when_only_alternative_fits), "Optimise this model before chatting", "No admitted-alternative asset/evidence tuple is currently supplied.", "GRANITE_E2E_GGUF_OPTIMIZE_PATH");
    [TestMethod] public void No_execution_when_no_safe_configuration_R2() => Guard(nameof(No_execution_when_no_safe_configuration_R2), "Continue to model inspection", "The no-fit asset must complete inspection/hardware before execution controls can be asserted.", "GRANITE_E2E_NO_FIT_PATH");
    [TestMethod] public void Gguf_optimization_reinspection_chat_exact_export() => Guard(nameof(Gguf_optimization_reinspection_chat_exact_export), "Optimisation destination", "The accessibility surface exposes no exact output digest/export identity.", "GRANITE_E2E_GGUF_OPTIMIZE_PATH");
    [TestMethod] public void Openvino_persistent_conversion_reinspection_chat_exact_export() => Guard(nameof(Openvino_persistent_conversion_reinspection_chat_exact_export), "Optimisation destination", "The accessibility surface exposes no exact persistent output digest/export identity.", "GRANITE_E2E_OPENVINO_OPTIMIZE_PATH");
    [TestMethod] public void Openvino_runtime_configuration_chat_prohibits_model_export() => Guard(nameof(Openvino_runtime_configuration_chat_prohibits_model_export), "Choose model file", "No runtime-only configuration asset and export-prohibition identity surface are supplied.", "GRANITE_E2E_OPENVINO_RUNTIME_PATH");
    [TestMethod] public void Optimization_cancel_restart_rejects_stale() => Guard(nameof(Optimization_cancel_restart_rejects_stale), "Optimise this model before chatting", "Stale worker result identity is not exposed through public UIA.", "GRANITE_E2E_GGUF_OPTIMIZE_PATH");

    private static void Guard(string name, string surface, string reason, params string[] variables) =>
        R2JourneyGuard.Unsupported(name, surface, reason, R2JourneyGuard.ProducerEvidence.Concat(variables).ToArray());
}
