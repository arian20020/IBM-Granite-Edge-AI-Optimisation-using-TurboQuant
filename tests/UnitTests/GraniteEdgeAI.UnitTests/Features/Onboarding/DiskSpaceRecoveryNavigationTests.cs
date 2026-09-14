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
