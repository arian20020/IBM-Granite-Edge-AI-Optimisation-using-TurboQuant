using System.Diagnostics;
using System.Windows.Automation;
using GraniteEdgeAI.EndToEndTests.Automation;
using GraniteEdgeAI.EndToEndTests.Infrastructure;

namespace GraniteEdgeAI.EndToEndTests.Journeys;

[TestClass]
[DoNotParallelize]
[TestCategory("NativeInspectionRoutes")]
public sealed class InspectionRouteJourneys
{
    // A broken inspection handoff must fail these tests, not count as a fit result.
    [TestMethod]
    public void Gguf_import_inspection_and_hardware_reach_a_compatibility_decision() =>
        Run("gguf", "GRANITE_E2E_GGUF_FIT_PATH", false);

    [TestMethod]
    public void OpenVino_import_inspection_and_hardware_offer_configuration() =>
        Run("openvino", "GRANITE_E2E_OPENVINO_FIT_PATH", false);

    // A rejected source must not enable Continue or poison the following valid import.
    [TestMethod]
    public void Rejected_gguf_can_be_replaced_and_continue_through_inspection() =>
        Run("gguf", "GRANITE_E2E_GGUF_FIT_PATH", true);

    private static void Run(string route, string assetVariable, bool recover)
    {
        string path = PackagedJourneyFixture.Require(assetVariable);
        AssetManifest assets = AssetManifest.Load(PackagedJourneyFixture.Require("GRANITE_E2E_ASSET_MANIFEST"));
        assets.VerifyPath(route, path);
        string? invalid = recover ? PackagedJourneyFixture.Require("GRANITE_E2E_MALFORMED_GGUF_PATH") : null;
        if (invalid is not null) assets.VerifyPath("gguf", invalid);

        CandidateManifest candidate = CandidateManifest.Load(
            PackagedJourneyFixture.Require("GRANITE_E2E_CANDIDATE_MANIFEST"),
            PackagedJourneyFixture.Require("GRANITE_E2E_CANDIDATE_COMMIT"),
            PackagedJourneyFixture.Require("GRANITE_E2E_CANDIDATE_TREE"));
        foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(candidate.ExecutablePath)))
        {
            process.Dispose();
            Assert.Inconclusive("Close the application before running native tests; the existing window will not be touched.");
        }

        PackagedJourneyFixture.Run($"InspectionRoute_{route}_{recover}", session =>
        {
            using Process launched = Process.GetProcessById(session.Window(CancellationToken.None).Current.ProcessId);
            Assert.IsTrue(string.Equals(Path.GetFullPath(candidate.ExecutablePath),
                Path.GetFullPath(launched.MainModule!.FileName), StringComparison.OrdinalIgnoreCase),
                "The activated app must be the recorded candidate, not another installed build.");
            var page = new Pages.CurrentInspectionRoutePage(session);
            Assert.IsFalse(page.ById("BtnContinueToInspection").Current.IsEnabled);
            if (invalid is not null)
            {
                page.Import("gguf", invalid);
                page.ById("FailureView");
                Assert.IsFalse(page.ById("BtnContinueToInspection").Current.IsEnabled,
                    "Rejected input must not be allowed to enter inspection.");
                page.ClearSelection();
            }
            page.Import(route, path);
            page.InvokeEnabled("BtnContinueToInspection");
            page.ContinueAfterModelInspection();
            page.AwaitCompatibilityDecision();
            if (route == "openvino") page.AssertConfigurationAvailable();
            string resultRoot = PackagedJourneyFixture.Require("GRANITE_E2E_RESULTS_ROOT");
            FailureDiagnostics.Capture(session.Window(CancellationToken.None),
                Path.Combine(resultRoot, $"Passed_{route}_{recover}", Guid.NewGuid().ToString("N")));
        });
    }
}
