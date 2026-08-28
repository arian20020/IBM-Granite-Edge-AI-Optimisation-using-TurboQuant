namespace GraniteEdgeAI.EndToEndTests.Journeys;

[TestClass]
[TestCategory("NativeAcceptance")]
public sealed class ChatLifecycleJourneys
{
    [TestMethod]
    public void Chat_has_no_onboarding_footer_or_indicator_R2() =>
        R2JourneyGuard.Surface(
            nameof(Chat_has_no_onboarding_footer_or_indicator_R2),
            "Conversation messages",
            session => new Pages.ChatPage(session).AssertNoOnboardingChrome(),
            R2JourneyGuard.ProducerEvidence.Append("GRANITE_E2E_CHAT_READY_PATH").ToArray());

    [TestMethod]
    public void Chat_send_newline_stop_continue_second_turn_reload_disposal() =>
        R2JourneyGuard.Unsupported(
            nameof(Chat_send_newline_stop_continue_second_turn_reload_disposal),
            "Message",
            "The public UIA surface cannot bind response/turn identity across continuation, reload and disposal.",
            R2JourneyGuard.ProducerEvidence.Append("GRANITE_E2E_CHAT_READY_PATH").ToArray());
}

[TestClass]
[TestCategory("NativeRestart")]
public sealed class RestartRecoveryJourneys
{
    [TestMethod]
    public void Restart_recovery_rejects_stale_identity_R2() =>
        R2JourneyGuard.Unsupported(
            nameof(Restart_recovery_rejects_stale_identity_R2),
            "Choose model file",
            "The packaged accessibility surface does not expose persisted source/handoff identity after restart.",
            R2JourneyGuard.ProducerEvidence.Append("GRANITE_E2E_GGUF_FIT_PATH").ToArray());
}
