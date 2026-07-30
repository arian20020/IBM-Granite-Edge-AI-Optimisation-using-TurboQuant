using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class OnboardingStageIndicatorTests
{
    private const string ActiveBrushKey = "OnboardingIndicatorActiveBrush";
    private const string SurfaceBrushKey = "OnboardingIndicatorSurfaceBrush";
    private const string InactiveSurfaceBrushKey =
        "OnboardingIndicatorInactiveSurfaceBrush";
    private const string InactiveBorderBrushKey =
        "OnboardingIndicatorInactiveBorderBrush";
    private const string PrimaryTextBrushKey =
        "OnboardingIndicatorPrimaryTextBrush";
    private const string SecondaryTextBrushKey =
        "OnboardingIndicatorSecondaryTextBrush";
    private const string MutedTextBrushKey =
        "OnboardingIndicatorMutedTextBrush";

    private static readonly StepElementNames[] Steps =
    [
        new("ChooseModelStepBox", "ChooseModelStepValue", "ChooseModelStepLabel", "Choose model"),
        new("InspectModelStepBox", "InspectModelStepValue", "InspectModelStepLabel", "Inspect model"),
        new("CheckFitStepBox", "CheckFitStepValue", "CheckFitStepLabel", "Check hardware fit"),
        new("ConfigureModelStepBox", "ConfigureModelStepValue", "ConfigureModelStepLabel", "Configure model"),
        new("ReadyToChatStepBox", "ReadyToChatStepValue", "ReadyToChatStepLabel", "Ready to chat")
    ];

    private static readonly string[] ConnectorScaleNames =
    [
        "ChooseToInspectConnectorScale",
        "InspectToFitConnectorScale",
        "FitToConfigureConnectorScale",
        "ConfigureToReadyConnectorScale"
    ];

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_DisplaysImportModelAsInitialStage()
    {
        var indicator = new OnboardingStageIndicator();

        Assert.AreEqual(OnboardingStage.ImportModel, indicator.CurrentStage);
        AssertProgress(
            indicator,
            OnboardingStage.ImportModel,
            ["1", "2", "3", "4", "5"],
            [0, 0, 0, 0],
            "Choose model");

        foreach (StepElementNames step in Steps)
        {
            Assert.AreEqual(
                step.Label,
                GetTextBlock(indicator, step.LabelName).Text);
        }

        Assert.AreEqual(
            "Shows progress through the five-stage model setup process.",
            AutomationProperties.GetHelpText(indicator));
        Assert.AreEqual(
            AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(indicator));
        Assert.IsFalse(indicator.IsHitTestVisible);
        Assert.IsFalse(indicator.IsTabStop);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Constructor_ConfiguresResponsiveLabelsAndHeight()
    {
        var indicator = new OnboardingStageIndicator();
        var layoutGrid = (Grid)indicator.FindName("IndicatorLayoutGrid");

        Assert.IsTrue(double.IsNaN(indicator.Height));
        Assert.AreEqual(128d, indicator.MinHeight);
        Assert.AreEqual(1200d, layoutGrid.MaxWidth);

        foreach (StepElementNames step in Steps)
        {
            TextBlock label = GetTextBlock(indicator, step.LabelName);
            Assert.AreEqual(TextWrapping.WrapWholeWords, label.TextWrapping);
            Assert.AreEqual(TextTrimming.None, label.TextTrimming);
            Assert.IsTrue(double.IsNaN(label.Width));
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Measure_NarrowWidthRequestsMoreHeightWithoutLosingStages()
    {
        var wideIndicator = new OnboardingStageIndicator();
        var narrowIndicator = new OnboardingStageIndicator();

        wideIndicator.Measure(new Windows.Foundation.Size(1200, double.PositiveInfinity));
        narrowIndicator.Measure(new Windows.Foundation.Size(500, double.PositiveInfinity));

        Assert.AreEqual(128d, wideIndicator.DesiredSize.Height);
        Assert.IsGreaterThan(
            wideIndicator.DesiredSize.Height,
            narrowIndicator.DesiredSize.Height);

        foreach (StepElementNames step in Steps)
        {
            Assert.AreEqual(
                step.Label,
                GetTextBlock(narrowIndicator, step.LabelName).Text);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CurrentStage_ForEveryDefinedStage_UpdatesProgress()
    {
        var indicator = new OnboardingStageIndicator();
        StageExpectation[] expectations =
        [
            new(OnboardingStage.ImportModel, ["1", "2", "3", "4", "5"], [0, 0, 0, 0], "Choose model"),
            new(OnboardingStage.InspectModel, ["✓", "2", "3", "4", "5"], [1, 0, 0, 0], "Inspect model"),
            new(OnboardingStage.CheckHardwareFit, ["✓", "✓", "3", "4", "5"], [1, 1, 0, 0], "Check hardware fit"),
            new(OnboardingStage.ConfigureModel, ["✓", "✓", "✓", "4", "5"], [1, 1, 1, 0], "Configure model"),
            new(OnboardingStage.ReadyToChat, ["✓", "✓", "✓", "✓", "5"], [1, 1, 1, 1], "Ready to chat")
        ];

        foreach (StageExpectation expectation in expectations)
        {
            indicator.CurrentStage = expectation.Stage;

            AssertProgress(
                indicator,
                expectation.Stage,
                expectation.Values,
                expectation.ConnectorScales,
                expectation.DisplayName);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void MovingFromReadyToChatBackToInspectModel_RestoresFutureStates()
    {
        var indicator = new OnboardingStageIndicator
        {
            CurrentStage = OnboardingStage.ReadyToChat
        };

        indicator.CurrentStage = OnboardingStage.InspectModel;

        AssertProgress(
            indicator,
            OnboardingStage.InspectModel,
            ["✓", "2", "3", "4", "5"],
            [1, 0, 0, 0],
            "Inspect model");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void UndefinedCurrentStage_ThrowsArgumentOutOfRangeException()
    {
        OnboardingStage[] invalidStages =
        [
            (OnboardingStage)0,
            (OnboardingStage)6
        ];

        foreach (OnboardingStage invalidStage in invalidStages)
        {
            var indicator = new OnboardingStageIndicator
            {
                CurrentStage = OnboardingStage.CheckHardwareFit
            };

            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => indicator.CurrentStage = invalidStage);
                AssertProgress(
                    indicator,
                    OnboardingStage.CheckHardwareFit,
                    ["\u2713", "\u2713", "3", "4", "5"],
                    [1, 1, 0, 0],
                    "Check hardware fit");
            }
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CurrentStage_ProvidesAutomationPeerForLiveRegionNotifications()
    {
        var indicator = new OnboardingStageIndicator();

        AutomationPeer peer =
            FrameworkElementAutomationPeer.CreatePeerForElement(indicator);

        Assert.IsNotNull(peer);
        Assert.AreSame(
            peer,
            FrameworkElementAutomationPeer.FromElement(indicator));

        indicator.CurrentStage = OnboardingStage.InspectModel;

        Assert.AreEqual(
            "Model setup progress. Step 2 of 5: Inspect model.",
            AutomationProperties.GetName(indicator));
    }

    private static void AssertProgress(
        OnboardingStageIndicator indicator,
        OnboardingStage expectedStage,
        string[] expectedValues,
        int[] expectedConnectorScales,
        string expectedDisplayName)
    {
        Assert.AreEqual(expectedStage, indicator.CurrentStage);
        int currentStepNumber = (int)expectedStage;
        SolidColorBrush activeBrush = GetBrush(indicator, ActiveBrushKey);
        SolidColorBrush surfaceBrush = GetBrush(indicator, SurfaceBrushKey);
        SolidColorBrush inactiveSurfaceBrush = GetBrush(
            indicator,
            InactiveSurfaceBrushKey);
        SolidColorBrush inactiveBorderBrush = GetBrush(
            indicator,
            InactiveBorderBrushKey);
        SolidColorBrush primaryTextBrush = GetBrush(
            indicator,
            PrimaryTextBrushKey);
        SolidColorBrush secondaryTextBrush = GetBrush(
            indicator,
            SecondaryTextBrushKey);
        SolidColorBrush mutedTextBrush = GetBrush(indicator, MutedTextBrushKey);

        for (int index = 0; index < Steps.Length; index++)
        {
            StepElementNames step = Steps[index];
            int stepNumber = index + 1;
            Border stepBox = GetBorder(indicator, step.BoxName);
            TextBlock stepValue = GetTextBlock(indicator, step.ValueName);
            TextBlock stepLabel = GetTextBlock(indicator, step.LabelName);

            Assert.AreEqual(expectedValues[index], stepValue.Text);

            if (stepNumber < currentStepNumber)
            {
                Assert.AreSame(activeBrush, stepBox.Background);
                Assert.AreSame(activeBrush, stepBox.BorderBrush);
                Assert.AreSame(surfaceBrush, stepValue.Foreground);
                Assert.AreSame(activeBrush, stepLabel.Foreground);
                Assert.AreEqual(FontWeights.SemiBold, stepLabel.FontWeight);
            }
            else if (stepNumber == currentStepNumber)
            {
                Assert.AreSame(activeBrush, stepBox.Background);
                Assert.AreSame(activeBrush, stepBox.BorderBrush);
                Assert.AreSame(surfaceBrush, stepValue.Foreground);
                Assert.AreSame(primaryTextBrush, stepLabel.Foreground);
                Assert.AreEqual(FontWeights.SemiBold, stepLabel.FontWeight);
            }
            else
            {
                Assert.AreSame(inactiveSurfaceBrush, stepBox.Background);
                Assert.AreSame(inactiveBorderBrush, stepBox.BorderBrush);
                Assert.AreSame(mutedTextBrush, stepValue.Foreground);
                Assert.AreSame(secondaryTextBrush, stepLabel.Foreground);
                Assert.AreEqual(FontWeights.Normal, stepLabel.FontWeight);
            }
        }

        for (int index = 0; index < ConnectorScaleNames.Length; index++)
        {
            Assert.AreEqual(
                expectedConnectorScales[index],
                GetScaleTransform(indicator, ConnectorScaleNames[index]).ScaleX);
        }

        Assert.AreEqual(
            $"MODEL SETUP · STEP {currentStepNumber} OF 5",
            GetTextBlock(indicator, "StageEyebrowText").Text);
        Assert.AreEqual(
            $"Model setup progress. Step {currentStepNumber} of 5: " +
            $"{expectedDisplayName}.",
            AutomationProperties.GetName(indicator));
    }

    private static Border GetBorder(
        OnboardingStageIndicator indicator,
        string elementName)
    {
        return (Border)indicator.FindName(elementName);
    }

    private static TextBlock GetTextBlock(
        OnboardingStageIndicator indicator,
        string elementName)
    {
        return (TextBlock)indicator.FindName(elementName);
    }

    private static ScaleTransform GetScaleTransform(
        OnboardingStageIndicator indicator,
        string elementName)
    {
        return (ScaleTransform)indicator.FindName(elementName);
    }

    private static SolidColorBrush GetBrush(
        OnboardingStageIndicator indicator,
        string resourceKey)
    {
        return (SolidColorBrush)indicator.Resources[resourceKey];
    }

    private sealed record StepElementNames(
        string BoxName,
        string ValueName,
        string LabelName,
        string Label);

    private sealed record StageExpectation(
        OnboardingStage Stage,
        string[] Values,
        int[] ConnectorScales,
        string DisplayName);
}
