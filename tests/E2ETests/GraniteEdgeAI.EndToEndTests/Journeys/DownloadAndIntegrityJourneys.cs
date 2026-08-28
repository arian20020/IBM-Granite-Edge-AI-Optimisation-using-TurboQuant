namespace GraniteEdgeAI.EndToEndTests.Journeys;

[TestClass]
[TestCategory("NativeRealModel")]
public sealed class DownloadAndIntegrityJourneys
{
    [TestMethod]
    public void Recommended_download_success_cancel_integrity_retry_cleanup() =>
        R2JourneyGuard.Unsupported(
            nameof(Recommended_download_success_cancel_integrity_retry_cleanup),
            "Choose model file",
            "No authorized installed download source and no accessible integrity-failure injection seam are supplied; E1 will not use the network.",
            "GRANITE_E2E_RECOMMENDED_DOWNLOAD_MANIFEST");
}
