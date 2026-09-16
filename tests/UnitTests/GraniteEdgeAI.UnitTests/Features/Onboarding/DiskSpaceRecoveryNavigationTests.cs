using System;
using System.IO;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.Onboarding;

/// <summary>Source contract regressions supplement the loaded navigation tests</summary>
[TestClass]
public sealed class DiskSpaceRecoveryNavigationTests
{
    [TestMethod]
    public void ReimportRecoveryUsesOpenVinoReplanGuardAndExistingOrderedCleanup()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)", "Features", "Onboarding", "OnboardingShellPage.xaml.cs"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        int begin = source.IndexOf("private async Task ImportAnotherModelAsync(\n            OptimizationJourneyCoordinator coordinator)", StringComparison.Ordinal);
        int end = source.IndexOf("private async Task LaunchOptimizedChatAsync(", begin, StringComparison.Ordinal);
        string method = source[begin..end];
        StringAssert.Contains(method, "coordinator.State.Kind == OptimizationJourneyKind.ReplanRequired");
        StringAssert.Contains(method, "coordinator.State.Entry.OptimizationHandoff.Plan.Route == OptimizationRoute.OpenVino");
        StringAssert.Contains(method, "ReferenceEquals(_optimizationCoordinator, coordinator)");
        StringAssert.Contains(method, "ReferenceEquals(StageFrame.Content, sourcePage)");
        StringAssert.Contains(method, "!sourcePage.CanCompleteImportNavigation");
        int navigate = method.IndexOf("StageFrame.Navigate(typeof(ModelImportPage))", StringComparison.Ordinal);
        int retire = method.IndexOf("await RetireOptimizationAsync()", StringComparison.Ordinal);
        int invalidate = method.IndexOf("InvalidateActiveHardwareJourney()", StringComparison.Ordinal);
        int clear = method.IndexOf("StageFrame.BackStack.Clear()", StringComparison.Ordinal);
        int stage = method.IndexOf("CurrentStage = OnboardingStage.ImportModel", StringComparison.Ordinal);
        Assert.IsTrue(navigate > 0 && navigate < retire && retire < invalidate && invalidate < clear && clear < stage);
        StringAssert.Contains(method, "StageFrame.ForwardStack.Clear()");
        StringAssert.Contains(method, "sourcePage.CancelImportNavigation()");
    }

    [TestMethod]
    public void CompatibilityBackReissuesInsteadOfLeavingHardwareDisabled()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)", "Features", "Onboarding", "OnboardingShellPage.xaml.cs"));
        int begin = source.IndexOf("private void CompatibilityPage_BackRequested(", StringComparison.Ordinal);
        int end = source.IndexOf("private void CompatibilityPage_HardwareRetryRequested(", begin, StringComparison.Ordinal);
        string method = source[begin..end];
        StringAssert.Contains(method, "_hardwareHandoffReissuer(modelPage)");
        Assert.IsFalse(method.Contains("SetHardwareRouteAvailable(false)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void OptimizedChatRetainsFailureOutcomeInsteadOfSilentlyDiscardingIt()
    {
        string source = File.ReadAllText(Path.Combine(RepositoryTestPaths.FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)", "Features", "Onboarding", "OnboardingShellPage.xaml.cs"));
        StringAssert.Contains(source, "LaunchSharedOpenVinoOptimizedAsync");
        StringAssert.Contains(source, "Chat could not start");
        StringAssert.Contains(source, "Content = CreateSharedOpenVinoChatFailureContent(error)");
        StringAssert.Contains(source, "ToProtocolValue(failure.SupportCode)");
    }
}
