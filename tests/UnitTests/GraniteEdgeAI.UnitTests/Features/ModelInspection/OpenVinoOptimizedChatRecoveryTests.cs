using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.OpenVino.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection;

[TestClass]
public sealed class OpenVinoOptimizedChatRecoveryTests
{
    [TestMethod]
    public void SharedInspectionFailureRetainsActualCodeWithoutPrivateDiagnostic()
    {
        var inspection = new GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoRouteInspectionResult(
            GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoRouteInspectionOutcome.DependencyUnavailable,
            null, new GraniteEdgeAI.Features.Prompting.PromptFailure(
                OpenVinoSupportCode.RuntimeDependencyMissing.ToProtocolValue(), "private filesystem diagnostic", "retry"), null);
        var failure = Assert.IsInstanceOfType<GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoRouteWorkerFailureException>(
            ModelInspectionPage.CreateSharedOpenVinoInspectionFailure(inspection, CancellationToken.None));
        Assert.AreEqual(OpenVinoSupportCode.RuntimeDependencyMissing, failure.SupportCode);
        string visible = GraniteEdgeAI.Features.Onboarding.OnboardingShellPage.CreateSharedOpenVinoChatFailureContent(failure);
        StringAssert.Contains(visible, "Support code: " + OpenVinoSupportCode.RuntimeDependencyMissing.ToProtocolValue());
        Assert.IsFalse(visible.Contains("private filesystem diagnostic", StringComparison.Ordinal));
        var cancelled = inspection with { Outcome = GraniteEdgeAI.Features.OpenVinoRoute.OpenVinoRouteInspectionOutcome.Cancelled,
            Failure = new GraniteEdgeAI.Features.Prompting.PromptFailure(OpenVinoSupportCode.OperationCancelled.ToProtocolValue(), "private", "") };
        Assert.IsInstanceOfType<OperationCanceledException>(ModelInspectionPage.CreateSharedOpenVinoInspectionFailure(cancelled, CancellationToken.None));
    }

    [TestMethod]
    public void TurboQuantTargetsRequireTurboQuantWorker()
    {
        Assert.IsTrue(ModelInspectionPage.RequiresTurboQuantChatWorker(OpenVinoRuntimeOptions.Tbq3));
        Assert.IsTrue(ModelInspectionPage.RequiresTurboQuantChatWorker(OpenVinoRuntimeOptions.Tbq4));
        Assert.IsFalse(ModelInspectionPage.RequiresTurboQuantChatWorker(OpenVinoRuntimeOptions.ReleasedDefault));
        Assert.IsFalse(ModelInspectionPage.RequiresTurboQuantChatWorker(OpenVinoRuntimeOptions.U4));
        Assert.IsFalse(ModelInspectionPage.RequiresTurboQuantChatWorker(OpenVinoRuntimeOptions.U8));
    }
}
