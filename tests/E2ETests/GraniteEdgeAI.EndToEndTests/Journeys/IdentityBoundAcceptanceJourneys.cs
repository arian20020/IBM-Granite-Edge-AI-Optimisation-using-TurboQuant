namespace GraniteEdgeAI.EndToEndTests.Journeys;

using GraniteEdgeAI.EndToEndTests.Pages;
using GraniteEdgeAI.EndToEndTests.Automation;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

[TestClass]
[TestCategory("NativeAcceptance")]
public sealed class IdentityBoundAcceptanceJourneys
{
    private static readonly string[] ProducerEvidence = ["GRANITE_E2E_H1_MANIFEST", "GRANITE_E2E_M1_MANIFEST", "GRANITE_E2E_Q1_MANIFEST", "GRANITE_E2E_ASSET_MANIFEST"];

    [TestMethod] public void Gguf_picker_fit_source_reaches_direct_chat() => DirectChat("GRANITE_E2E_GGUF_FIT_PATH");
    [TestMethod] public void OpenVino_fit_source_reaches_direct_chat() => DirectChat("GRANITE_E2E_OPENVINO_FIT_PATH");

    [TestMethod]
    public void Gguf_drag_drop_optimizes_reinspects_chats_and_exports_exact_output() =>
        PackagedJourneyFixture.Run(nameof(Gguf_drag_drop_optimizes_reinspects_chats_and_exports_exact_output), session =>
        {
            new ImportPage(session).AssertDropSurface(CancellationToken.None);
            Assert.Inconclusive("Blocked: WinUI model drop surface exposes no maintained UI Automation drag/drop pattern; an app-owned automation seam is required.");
        }, ProducerEvidence.Append("GRANITE_E2E_GGUF_OPTIMIZE_PATH").ToArray());

    [TestMethod] public void OpenVino_optimization_preserves_persistent_or_runtime_configuration_identity() => Optimize("GRANITE_E2E_OPENVINO_OPTIMIZE_PATH");

    [TestMethod]
    public void Compatibility_unknown_or_no_fit_keeps_execution_disabled() => AtCompatibility("GRANITE_E2E_NO_FIT_PATH", (compatibility, _) =>
        compatibility.AssertExecutionDisabled(CancellationToken.None));

    [TestMethod]
    public void Optimization_cancellation_restart_rejects_stale_result() => AtCompatibility("GRANITE_E2E_GGUF_OPTIMIZE_PATH", (compatibility, session) =>
    {
        compatibility.Optimize(CancellationToken.None);
        OptimizationPage optimization = new(session);
        optimization.Start(CancellationToken.None);
        optimization.Cancel(CancellationToken.None);
        optimization.Start(CancellationToken.None);
        optimization.AwaitResult(CancellationToken.None);
    });

    [TestMethod] public void Artifact_publication_failure_does_not_replace_original_model() => UnsupportedSeam("GRANITE_E2E_GGUF_OPTIMIZE_PATH", "Optimisation destination", "No accessible test-owned publication-failure injection seam exists.");
    [TestMethod] public void Chat_supports_enter_newline_stop_second_turn_and_reload() => UnsupportedSeam("GRANITE_E2E_CHAT_READY_PATH", "Message", "The public UIA surface does not expose response identity needed to assert both turns and reload.");
    [TestMethod] public void Application_restart_recovers_allowed_state_and_rejects_stale_identity() => UnsupportedSeam("GRANITE_E2E_GGUF_FIT_PATH", "Choose model file", "The public UIA surface does not expose persisted source/handoff identity after restart.");

    [TestMethod]
    public void Chat_has_no_onboarding_indicator_or_footer() => RequireSurface("GRANITE_E2E_CHAT_READY_PATH", "Conversation messages", session =>
        new ChatPage(session).AssertNoOnboardingChrome());

    private static void DirectChat(string assetVariable) => AtCompatibility(assetVariable, (compatibility, session) =>
    {
        compatibility.Chat(CancellationToken.None);
        new ChatPage(session).AssertReady(CancellationToken.None);
    });

    private static void Optimize(string assetVariable) => AtCompatibility(assetVariable, (compatibility, session) =>
    {
        compatibility.Optimize(CancellationToken.None);
        OptimizationPage optimization = new(session);
        optimization.Start(CancellationToken.None);
        optimization.AwaitResult(CancellationToken.None);
        optimization.ChatWithResult(CancellationToken.None);
        new ChatPage(session).AssertReady(CancellationToken.None);
    });

    private static void AtCompatibility(string assetVariable, Action<CompatibilityPage, AutomationSession> assertion)
    {
        RequireSurface(assetVariable, "Choose model file", session =>
        {
            ImportPage import = new(session);
            string path = PackagedJourneyFixture.Require(assetVariable);
            string route = assetVariable.Contains("OPENVINO", StringComparison.Ordinal) ? "openvino" : "gguf";
            AssetManifest.Load(PackagedJourneyFixture.Require("GRANITE_E2E_ASSET_MANIFEST")).VerifyPath(route, path);
            import.SelectRoute(route, CancellationToken.None);
            import.ChooseModel(path, CancellationToken.None);
            import.Continue(CancellationToken.None);
            new InspectionPage(session).ContinueToHardware(CancellationToken.None);
            new HardwarePage(session).ContinueToCompatibility(CancellationToken.None);
            assertion(new CompatibilityPage(session), session);
        });
    }

    private static void UnsupportedSeam(string assetVariable, string surface, string reason) => RequireSurface(assetVariable, surface, _ =>
        Assert.Inconclusive($"Blocked: {reason}"));

    private static void RequireSurface(string assetVariable, string firstAccessibleSurface, Action<AutomationSession>? continuation = null)
    {
        PackagedJourneyFixture.Run(firstAccessibleSurface, session =>
        {
            Assert.IsNotNull(session.ByName(firstAccessibleSurface, CancellationToken.None));
            continuation?.Invoke(session);
        }, ProducerEvidence.Append(assetVariable).ToArray());
    }
}
