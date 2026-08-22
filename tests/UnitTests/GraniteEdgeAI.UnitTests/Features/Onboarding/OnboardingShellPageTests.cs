using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class OnboardingShellPageTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_PresentsModelImportAsInitialStage()
    {
        var shell = new OnboardingShellPage();
        var stageFrame = shell.FindName("StageFrame") as Frame;

        Assert.IsNotNull(stageFrame);
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
        Assert.AreEqual(typeof(ModelImportPage), stageFrame.SourcePageType);
        Assert.IsInstanceOfType<ModelImportPage>(stageFrame.Content);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ShowsPersistentIndicatorSynchronizedWithCurrentStage()
    {
        var shell = new OnboardingShellPage();

        var stageIndicator = shell.FindName("StageIndicator")
            as OnboardingStageIndicator;
        var stageFrame = shell.FindName("StageFrame") as Frame;

        Assert.IsNotNull(stageIndicator);
        Assert.IsNotNull(stageFrame);
        Assert.AreEqual(shell.CurrentStage, stageIndicator.CurrentStage);
        Assert.AreNotSame(stageIndicator, stageFrame.Content);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_KeepsProductionHardwareInspectionCompositionUnavailable()
    {
        var shell = new OnboardingShellPage();
        FieldInfo? serviceField = typeof(OnboardingShellPage).GetField(
            "_hardwareInspectionService",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(serviceField);
        object? service = serviceField.GetValue(shell);
        Assert.IsNotNull(service);
        Assert.AreEqual(
            "GraniteEdgeAI.Features.HardwareInspection.Application." +
            "UnavailableHardwareInspectionService",
            service.GetType().FullName);
    }
}
