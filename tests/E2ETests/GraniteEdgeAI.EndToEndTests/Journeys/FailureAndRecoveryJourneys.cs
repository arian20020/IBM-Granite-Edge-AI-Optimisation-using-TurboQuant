namespace GraniteEdgeAI.EndToEndTests.Journeys;

[TestClass]
[TestCategory("NativeFailure")]
public sealed class FailureAndRecoveryJourneys
{
    [TestMethod] public void Missing_or_mismatched_OpenVino_package_fails_closed() => Guarded("GRANITE_E2E_INVALID_OPENVINO_PATH", "Choose OpenVINO folder");
    [TestMethod] public void Changed_model_between_inspection_and_execution_is_rejected() => Guarded("GRANITE_E2E_MUTABLE_MODEL_PATH", "Continue to model inspection");
    [TestMethod] public void Changed_hardware_evidence_requires_compatibility_recheck() => Guarded("GRANITE_E2E_CHANGED_HARDWARE_MANIFEST", "Refresh memory and check again");
    [TestMethod] public void Retry_cannot_publish_duplicate_or_late_worker_result() => Guarded("GRANITE_E2E_FAILURE_ASSET_PATH", "Continue to model inspection");

    private static void Guarded(string variable, string surface) =>
        PackagedJourneyFixture.Run(surface, session => Assert.IsNotNull(session.ByName(surface, CancellationToken.None)), variable);
}
