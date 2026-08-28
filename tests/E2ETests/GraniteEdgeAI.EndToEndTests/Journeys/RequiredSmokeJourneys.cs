namespace GraniteEdgeAI.EndToEndTests.Journeys;

[TestClass]
[TestCategory("NativeSmoke")]
public sealed class RequiredSmokeJourneys
{
    [TestMethod]
    public void Package_launch_has_exactly_one_onboarding_shell() =>
        R2JourneyGuard.Surface(nameof(Package_launch_has_exactly_one_onboarding_shell), "Choose model file", session =>
            Assert.AreEqual(1, session.CountByName("Choose model file"), "Expected exactly one onboarding/import shell."));

    [TestMethod]
    public void Picker_and_explorer_drag_drop_ingress() =>
        R2JourneyGuard.Unsupported(
            nameof(Picker_and_explorer_drag_drop_ingress),
            "Model drop area",
            "Picker ingress is executable, but Explorer-to-WinUI drag/drop has no maintained UIA pattern or app-owned automation seam.",
            "GRANITE_E2E_GGUF_FIT_PATH");

    [TestMethod]
    public void Malformed_input_and_import_cancel_recover() =>
        R2JourneyGuard.Unsupported(
            nameof(Malformed_input_and_import_cancel_recover),
            "Choose model file",
            "The two executable smoke cases are separate; no authorized combined malformed-and-cancel asset campaign is configured.",
            "GRANITE_E2E_MALFORMED_GGUF_PATH",
            "GRANITE_E2E_CANCEL_GGUF_PATH");

    [TestMethod]
    public void No_orphan_processes_after_every_test() =>
        R2JourneyGuard.Surface(nameof(No_orphan_processes_after_every_test), "Choose model file");
}
