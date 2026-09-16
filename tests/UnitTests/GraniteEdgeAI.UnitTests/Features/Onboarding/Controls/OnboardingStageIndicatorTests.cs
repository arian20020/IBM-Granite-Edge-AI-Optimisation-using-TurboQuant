using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
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
    public TestContext TestContext { get; set; } = null!;

    private void AssertAnimationSetting(Windows.UI.ViewManagement.UISettings settings)
    {
        TestContext.WriteLine($"Actual AnimationsEnabled: {settings.AnimationsEnabled}");
        if (TestContext.Properties.TryGetValue("ExpectedAnimationsEnabled", out object? expected)
            && bool.TryParse(expected?.ToString(), out bool enabled))
        {
            Assert.AreEqual(enabled, settings.AnimationsEnabled);
        }
    }

    [UITestMethod]
    public async Task AnimationSettingCallbackFromWorkerUsesUiThreadAndCurrentStage()
    {
        var indicator = new OnboardingStageIndicator();
        var settings = new Windows.UI.ViewManagement.UISettings();
        AssertAnimationSetting(settings);
        try
        {
            InvokeAnimationCallbackFromWorker(indicator, settings);
            if (!settings.AnimationsEnabled)
            {
                indicator.CurrentStage = OnboardingStage.ConfigureModel;
            }
            // Deliberately distinguish a snap from the normal stage update.
            var connector = (ScaleTransform)indicator.FindName("ChooseToInspectConnectorScale");
            connector.ScaleX = 0.37;
            await DrainIndicatorQueueAsync(indicator);
            Assert.AreEqual(settings.AnimationsEnabled ? 0.37 : 1d, connector.ScaleX, 0.001);
            Assert.AreEqual(settings.AnimationsEnabled
                ? OnboardingStage.ImportModel : OnboardingStage.ConfigureModel, indicator.CurrentStage);
        }
        finally { UnloadIndicator(indicator); }
    }

    [UITestMethod]
    public void AnimationSettingCallbackOnUiThreadKeepsDisabledOnlyBehavior()
    {
        var indicator = new OnboardingStageIndicator();
        var settings = new Windows.UI.ViewManagement.UISettings();
        AssertAnimationSetting(settings);
        try
        {
            var connector = (ScaleTransform)indicator.FindName("ChooseToInspectConnectorScale");
            connector.ScaleX = 0.37;
            typeof(OnboardingStageIndicator).GetMethod("UiSettings_AnimationsEnabledChanged",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(indicator, [settings, null]);
            Assert.AreEqual(settings.AnimationsEnabled ? 0.37 : 0d, connector.ScaleX, 0.001);
        }
        finally { UnloadIndicator(indicator); }
    }

    [UITestMethod]
    public async Task AnimationSettingCallbackQueuedBeforeUnloadCannotTouchRetiredIndicator()
    {
        var indicator = new OnboardingStageIndicator();
        var settings = new Windows.UI.ViewManagement.UISettings();
        AssertAnimationSetting(settings);
        InvokeAnimationCallbackFromWorker(indicator, settings);
        UnloadIndicator(indicator);
        var connector = (ScaleTransform)indicator.FindName("ChooseToInspectConnectorScale");
        connector.ScaleX = 0.37;
        await DrainIndicatorQueueAsync(indicator);
        Assert.AreEqual(0.37, connector.ScaleX, 0.001);
        InvokeAnimationCallbackFromWorker(indicator, settings);
        await DrainIndicatorQueueAsync(indicator);
        Assert.AreEqual(0.37, connector.ScaleX, 0.001);
    }

    private static void InvokeAnimationCallbackFromWorker(OnboardingStageIndicator indicator,
        Windows.UI.ViewManagement.UISettings settings)
    {
        // Keep the UI thread here until the worker returns, so queued work cannot
        // execute until the test changes stage or unloads the indicator.
        Task.Run(() => typeof(OnboardingStageIndicator).GetMethod(
            "UiSettings_AnimationsEnabledChanged",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(indicator, [settings, null])).GetAwaiter().GetResult();
    }

    private static void UnloadIndicator(OnboardingStageIndicator indicator) =>
        typeof(OnboardingStageIndicator).GetMethod("OnboardingStageIndicator_Unloaded",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(indicator, [indicator, new RoutedEventArgs()]);

    private static async Task DrainIndicatorQueueAsync(OnboardingStageIndicator indicator)
    {
        var drained = new TaskCompletionSource<bool>();
        Assert.IsTrue(indicator.DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => drained.SetResult(true)));
        await drained.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private const string ActiveBrushKey = "OnboardingIndicatorActiveBrush";
    private const string ActiveSurfaceBrushKey =
        "OnboardingIndicatorActiveSurfaceBrush";
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
    private const string SuccessSurfaceBrushKey =
        "OnboardingIndicatorSuccessSurfaceBrush";
    private const string SuccessBorderBrushKey =
        "OnboardingIndicatorSuccessBorderBrush";
    private const string SuccessTextBrushKey =
        "OnboardingIndicatorSuccessTextBrush";
    private const string NotCompleteSurfaceBrushKey =
        "OnboardingIndicatorNotCompleteSurfaceBrush";
    private const string NotCompleteBorderBrushKey =
        "OnboardingIndicatorNotCompleteBorderBrush";
    private const string ErrorBrushKey = "OnboardingIndicatorErrorBrush";

    private static readonly StepElementNames[] Steps =
    [
        new("ChooseModelStepBox", "ChooseModelStepValue", "ChooseModelStepGlyph", "ChooseModelStepLabel", "Choose model"),
        new("InspectModelStepBox", "InspectModelStepValue", "InspectModelStepGlyph", "InspectModelStepLabel", "Inspect model"),
        new("CheckFitStepBox", "CheckFitStepValue", "CheckFitStepGlyph", "CheckFitStepLabel", "Check hardware fit"),
        new("ConfigureModelStepBox", "ConfigureModelStepValue", "ConfigureModelStepGlyph", "ConfigureModelStepLabel", "Configure model"),
        new("ReadyToChatStepBox", "ReadyToChatStepValue", "ReadyToChatStepGlyph", "ReadyToChatStepLabel", "Ready to chat")
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
        Assert.AreEqual(960d, layoutGrid.MaxWidth);

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
    public void Measure_NarrowWidthPreservesNaturalHeightAndAllStages()
    {
        var wideIndicator = new OnboardingStageIndicator();
        var narrowIndicator = new OnboardingStageIndicator();

        wideIndicator.Measure(new Windows.Foundation.Size(1200, double.PositiveInfinity));
        narrowIndicator.Measure(new Windows.Foundation.Size(500, double.PositiveInfinity));

        Assert.AreEqual(128d, wideIndicator.DesiredSize.Height);
        Assert.IsGreaterThanOrEqualTo(128d, narrowIndicator.DesiredSize.Height);

        foreach (StepElementNames step in Steps)
        {
            Border stepBox = GetBorder(narrowIndicator, step.BoxName);
            Assert.AreEqual(36d, stepBox.Width);
            Assert.AreEqual(36d, stepBox.Height);
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
            new(OnboardingStage.InspectModel, [null, "2", "3", "4", "5"], [1, 0, 0, 0], "Inspect model"),
            new(OnboardingStage.CheckHardwareFit, [null, null, "3", "4", "5"], [1, 1, 0, 0], "Check hardware fit"),
            new(OnboardingStage.ConfigureModel, [null, null, null, "4", "5"], [1, 1, 1, 0], "Configure model"),
            new(OnboardingStage.ReadyToChat, [null, null, null, null, "5"], [1, 1, 1, 1], "Ready to chat")
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
            [null, "2", "3", "4", "5"],
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
                    [null, null, "3", "4", "5"],
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
            "Model setup. Inspect model, step 2 of 5.",
            AutomationProperties.GetName(indicator));
        Assert.AreEqual(
            "Inspection in progress",
            AutomationProperties.GetItemStatus(indicator));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow((int)InspectionFooterStatus.InProgress, true, (int)InspectionStatusGlyphKind.Success, "IN PROGRESS", "Inspection in progress")]
    [DataRow((int)InspectionFooterStatus.Complete, true, (int)InspectionStatusGlyphKind.Success, "COMPLETE", "Inspection complete")]
    [DataRow((int)InspectionFooterStatus.NotComplete, false, (int)InspectionStatusGlyphKind.NotComplete, "NOT COMPLETE", "Inspection not complete")]
    [DataRow((int)InspectionFooterStatus.Interrupted, false, (int)InspectionStatusGlyphKind.Error, "INTERRUPTED", "Inspection interrupted")]
    public void InspectionStatus_MapsAllFourNonNavigatingStates(
        int statusValue,
        bool showsStepNumber,
        int glyphKindValue,
        string expectedEyebrowStatus,
        string expectedAutomationStatus)
    {
        var indicator = new OnboardingStageIndicator
        {
            CurrentStage = OnboardingStage.InspectModel
        };
        int liveNotifications = indicator.LiveRegionChangeNotificationCount;

        indicator.InspectionStatus = (InspectionFooterStatus)statusValue;

        Assert.AreEqual(OnboardingStage.InspectModel, indicator.CurrentStage);
        TextBlock stepValue = GetTextBlock(indicator, "InspectModelStepValue");
        InspectionStatusGlyph statusGlyph = GetGlyph(
            indicator,
            "InspectModelStepGlyph");
        Assert.AreEqual("2", stepValue.Text);
        Assert.AreEqual(
            showsStepNumber ? Visibility.Visible : Visibility.Collapsed,
            stepValue.Visibility);
        Assert.AreEqual(
            showsStepNumber ? Visibility.Collapsed : Visibility.Visible,
            statusGlyph.Visibility);
        Assert.AreEqual(
            (InspectionStatusGlyphKind)glyphKindValue,
            statusGlyph.Kind);
        Assert.AreEqual(36d, statusGlyph.SurfaceSize, 0.01d);
        Assert.AreEqual(
            AccessibilityView.Raw,
            AutomationProperties.GetAccessibilityView(statusGlyph));
        Border statusBox = GetBorder(indicator, "InspectModelStepBox");
        (string backgroundKey, string borderKey) =
            (InspectionFooterStatus)statusValue switch
            {
                InspectionFooterStatus.InProgress =>
                    (ActiveSurfaceBrushKey, ActiveBrushKey),
                InspectionFooterStatus.Complete =>
                    (ActiveSurfaceBrushKey, ActiveBrushKey),
                InspectionFooterStatus.NotComplete =>
                    (NotCompleteSurfaceBrushKey, NotCompleteBorderBrushKey),
                InspectionFooterStatus.Interrupted =>
                    (SurfaceBrushKey, ErrorBrushKey),
                _ => throw new ArgumentOutOfRangeException(nameof(statusValue))
            };
        AssertBrushColorEquals(
            GetBrush(indicator, backgroundKey),
            statusBox.Background);
        AssertBrushColorEquals(
            GetBrush(indicator, borderKey),
            statusBox.BorderBrush);
        Assert.IsTrue(
            GetTextBlock(indicator, "StageEyebrowText").Text.EndsWith(
                expectedEyebrowStatus,
                StringComparison.Ordinal));
        Assert.AreEqual(
            expectedAutomationStatus,
            AutomationProperties.GetItemStatus(indicator));
        Assert.AreEqual(
            liveNotifications +
                ((InspectionFooterStatus)statusValue == InspectionFooterStatus.InProgress
                    ? 0
                    : 1),
            indicator.LiveRegionChangeNotificationCount,
            "A changed inspection status must raise one live-region notification.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void InspectionStatus_OverridesOnlyInspectAndRestoresNavigationState()
    {
        var indicator = new OnboardingStageIndicator
        {
            InspectionStatus = InspectionFooterStatus.Interrupted
        };

        Assert.AreEqual("1", GetTextBlock(indicator, "ChooseModelStepValue").Text);
        Assert.AreEqual("2", GetTextBlock(indicator, "InspectModelStepValue").Text);
        Assert.AreEqual(
            Visibility.Collapsed,
            GetGlyph(indicator, "InspectModelStepGlyph").Visibility);

        indicator.CurrentStage = OnboardingStage.InspectModel;
        Assert.AreEqual(
            Visibility.Collapsed,
            GetTextBlock(indicator, "InspectModelStepValue").Visibility);
        Assert.AreEqual(
            InspectionStatusGlyphKind.Error,
            GetGlyph(indicator, "InspectModelStepGlyph").Kind);
        Assert.AreEqual(
            Visibility.Visible,
            GetGlyph(indicator, "InspectModelStepGlyph").Visibility);

        indicator.CurrentStage = OnboardingStage.CheckHardwareFit;
        Assert.AreEqual(
            InspectionStatusGlyphKind.Success,
            GetGlyph(indicator, "InspectModelStepGlyph").Kind);
        Assert.AreEqual(
            "Model setup. Check hardware fit, step 3 of 5.",
            AutomationProperties.GetName(indicator));
        Assert.AreEqual(string.Empty, AutomationProperties.GetItemStatus(indicator));
    }

    private static void AssertProgress(
        OnboardingStageIndicator indicator,
        OnboardingStage expectedStage,
        string?[] expectedValues,
        int[] expectedConnectorScales,
        string expectedDisplayName)
    {
        Assert.AreEqual(expectedStage, indicator.CurrentStage);
        int currentStepNumber = (int)expectedStage;
        SolidColorBrush activeBrush = GetBrush(indicator, ActiveBrushKey);
        SolidColorBrush activeSurfaceBrush = GetBrush(
            indicator,
            ActiveSurfaceBrushKey);
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
        SolidColorBrush successSurfaceBrush = GetBrush(
            indicator,
            SuccessSurfaceBrushKey);
        SolidColorBrush successBorderBrush = GetBrush(
            indicator,
            SuccessBorderBrushKey);
        SolidColorBrush successTextBrush = GetBrush(
            indicator,
            SuccessTextBrushKey);

        for (int index = 0; index < Steps.Length; index++)
        {
            StepElementNames step = Steps[index];
            int stepNumber = index + 1;
            Border stepBox = GetBorder(indicator, step.BoxName);
            TextBlock stepValue = GetTextBlock(indicator, step.ValueName);
            InspectionStatusGlyph stepGlyph = GetGlyph(
                indicator,
                step.GlyphName);
            TextBlock stepLabel = GetTextBlock(indicator, step.LabelName);

            Assert.AreEqual(stepNumber.ToString(), stepValue.Text);

            if (stepNumber < currentStepNumber)
            {
                Assert.IsNull(expectedValues[index]);
                Assert.AreEqual(Visibility.Collapsed, stepValue.Visibility);
                Assert.AreEqual(Visibility.Visible, stepGlyph.Visibility);
                Assert.AreEqual(
                    InspectionStatusGlyphKind.Success,
                    stepGlyph.Kind);
                Assert.AreEqual(36d, stepGlyph.SurfaceSize, 0.01d);
                Assert.AreEqual(
                    AccessibilityView.Raw,
                    AutomationProperties.GetAccessibilityView(stepGlyph));
                AssertBrushColorEquals(successSurfaceBrush, stepBox.Background);
                AssertBrushColorEquals(successBorderBrush, stepBox.BorderBrush);
                AssertBrushColorEquals(successTextBrush, stepLabel.Foreground);
                Assert.AreEqual(FontWeights.SemiBold, stepLabel.FontWeight);
            }
            else if (stepNumber == currentStepNumber)
            {
                Assert.AreEqual(expectedValues[index], stepValue.Text);
                Assert.AreEqual(Visibility.Visible, stepValue.Visibility);
                Assert.AreEqual(Visibility.Collapsed, stepGlyph.Visibility);
                AssertBrushColorEquals(activeSurfaceBrush, stepBox.Background);
                AssertBrushColorEquals(activeBrush, stepBox.BorderBrush);
                AssertBrushColorEquals(activeBrush, stepValue.Foreground);
                AssertBrushColorEquals(primaryTextBrush, stepLabel.Foreground);
                Assert.AreEqual(FontWeights.SemiBold, stepLabel.FontWeight);
            }
            else
            {
                Assert.AreEqual(expectedValues[index], stepValue.Text);
                Assert.AreEqual(Visibility.Visible, stepValue.Visibility);
                Assert.AreEqual(Visibility.Collapsed, stepGlyph.Visibility);
                AssertBrushColorEquals(inactiveSurfaceBrush, stepBox.Background);
                AssertBrushColorEquals(inactiveBorderBrush, stepBox.BorderBrush);
                AssertBrushColorEquals(mutedTextBrush, stepValue.Foreground);
                AssertBrushColorEquals(secondaryTextBrush, stepLabel.Foreground);
                Assert.AreEqual(FontWeights.Normal, stepLabel.FontWeight);
            }
        }

        for (int index = 0; index < ConnectorScaleNames.Length; index++)
        {
            Assert.AreEqual(
                expectedConnectorScales[index],
                GetScaleTransform(indicator, ConnectorScaleNames[index]).ScaleX);
        }

        string expectedEyebrow = expectedStage == OnboardingStage.InspectModel
            ? "MODEL SETUP · STEP 2 OF 5 IN PROGRESS"
            : $"MODEL SETUP · STEP {currentStepNumber} OF 5";
        string expectedAutomation =
            $"Model setup. {expectedDisplayName}, step {currentStepNumber} of 5.";
        Assert.AreEqual(
            expectedEyebrow,
            GetTextBlock(indicator, "StageEyebrowText").Text);
        Assert.AreEqual(expectedAutomation, AutomationProperties.GetName(indicator));
        Assert.AreEqual(
            expectedStage == OnboardingStage.InspectModel
                ? "Inspection in progress"
                : string.Empty,
            AutomationProperties.GetItemStatus(indicator));
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

    private static InspectionStatusGlyph GetGlyph(
        OnboardingStageIndicator indicator,
        string elementName)
    {
        return (InspectionStatusGlyph)indicator.FindName(elementName);
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

    private static void AssertBrushColorEquals(
        SolidColorBrush expected,
        Brush actual)
    {
        Assert.AreEqual(expected.Color, ((SolidColorBrush)actual).Color);
    }

    private sealed record StepElementNames(
        string BoxName,
        string ValueName,
        string GlyphName,
        string LabelName,
        string Label);

    private sealed record StageExpectation(
        OnboardingStage Stage,
        string?[] Values,
        int[] ConnectorScales,
        string DisplayName);
}
