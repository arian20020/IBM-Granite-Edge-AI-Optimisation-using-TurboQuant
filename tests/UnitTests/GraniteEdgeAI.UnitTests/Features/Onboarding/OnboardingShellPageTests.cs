using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

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
}
