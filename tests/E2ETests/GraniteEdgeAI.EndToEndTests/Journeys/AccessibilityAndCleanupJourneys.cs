namespace GraniteEdgeAI.EndToEndTests.Journeys;

[TestClass]
[TestCategory("NativeFailure")]
public sealed class AccessibilityAndCleanupJourneys
{
    [TestMethod]
    public void Changed_model_or_hardware_evidence_rejected() => Unsupported(nameof(Changed_model_or_hardware_evidence_rejected), "Continue to model inspection", "No safe mid-journey evidence mutation seam exists.", "GRANITE_E2E_MUTABLE_MODEL_PATH", "GRANITE_E2E_CHANGED_HARDWARE_MANIFEST");

    [TestMethod]
    public void Duplicate_or_late_publication_rejected() => Unsupported(nameof(Duplicate_or_late_publication_rejected), "Continue to model inspection", "No app-owned late/duplicate worker publication seam exists.", "GRANITE_E2E_FAILURE_ASSET_PATH");

    [TestMethod]
    public void Publication_failure_preserves_original() => Unsupported(nameof(Publication_failure_preserves_original), "Optimisation destination", "No app-owned artifact publication failure seam exists.", "GRANITE_E2E_GGUF_OPTIMIZE_PATH");

    [TestMethod]
    public void Disabled_actions_inaccessible_by_keyboard_and_automation() => Unsupported(nameof(Disabled_actions_inaccessible_by_keyboard_and_automation), "Continue to model inspection", "The no-fit campaign must reach compatibility before keyboard/UIA focusability can be asserted.", "GRANITE_E2E_NO_FIT_PATH");

    private static void Unsupported(string name, string surface, string reason, params string[] variables) =>
        R2JourneyGuard.Unsupported(name, surface, reason, R2JourneyGuard.ProducerEvidence.Concat(variables).ToArray());
}
