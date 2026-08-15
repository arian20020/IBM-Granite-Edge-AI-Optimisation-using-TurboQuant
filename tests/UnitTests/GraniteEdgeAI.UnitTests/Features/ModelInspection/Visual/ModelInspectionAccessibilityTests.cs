using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Windows.System;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;

[TestClass]
[DoNotParallelize]
public sealed class ModelInspectionAccessibilityTests
{
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void CanonicalPresentationFixture_RemainsX64CpuVocabOnlyWithoutGpu()
    {
        ModelInspectionRuntimeIdentity runtime =
            PresentationTestData.CreateEvidence().Runtime;

        Assert.AreEqual("X64", runtime.ProcessArchitecture);
        Assert.AreEqual("VocabOnly", runtime.InspectionMode);
        Assert.IsFalse(runtime.UsesCuda);
        Assert.IsFalse(runtime.UsesVulkan);
        Assert.AreEqual(0, runtime.GpuLayerCount);
        Assert.AreEqual("llama.dll", runtime.NativeLibraryName);
        StringAssert.Contains(runtime.RuntimeProfile, "cpu-win-x64-vocab-only");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InProcessKeyboardActivation_DisclosureRetainsFocusAndPeerState()
    {
        var disclosure = new InspectionDisclosure
        {
            HeaderContent = new TextBlock { Text = "Inspection details" },
            ViewportContent = new Button { Content = "Report action" }
        };
        AutomationProperties.SetName(disclosure, "Inspection details");
        List<bool> requestedStates = [];
        disclosure.ToggleRequested += (_, args) =>
            requestedStates.Add(args.IsExpanded);

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            disclosure,
            width: 500,
            height: 300);
        await host.CaptureAsync();

        Assert.IsTrue(disclosure.Focus(FocusState.Keyboard));
        Assert.AreSame(
            disclosure,
            FocusManager.GetFocusedElement(disclosure.XamlRoot));
        Assert.IsTrue(disclosure.HandleKeyboardActivation(
            VirtualKey.Enter,
            repeatCount: 1,
            wasKeyDown: false));
        CollectionAssert.AreEqual(new[] { true }, requestedStates);

        disclosure.PrepareTargetState(isExpanded: true);
        disclosure.CompleteTargetState(isExpanded: true);
        await host.CaptureAsync();
        Assert.AreSame(
            disclosure,
            FocusManager.GetFocusedElement(disclosure.XamlRoot));

        AutomationPeer peer = FrameworkElementAutomationPeer
            .CreatePeerForElement(disclosure);
        Assert.IsNotNull(peer);
        IExpandCollapseProvider provider =
            Assert.IsInstanceOfType<IExpandCollapseProvider>(
                peer.GetPattern(PatternInterface.ExpandCollapse));
        Assert.AreEqual(
            ExpandCollapseState.Expanded,
            provider.ExpandCollapseState);

        Assert.IsTrue(disclosure.HandleKeyboardActivation(
            VirtualKey.Space,
            repeatCount: 1,
            wasKeyDown: false));
        disclosure.PrepareTargetState(isExpanded: false);
        disclosure.CompleteTargetState(isExpanded: false);
        CollectionAssert.AreEqual(new[] { true, false }, requestedStates);
        Assert.AreSame(
            disclosure,
            FocusManager.GetFocusedElement(disclosure.XamlRoot));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RenderedTerminal_ActionsStatusAndFutureHelpAreNotColorOnly()
    {
        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionVisualTestScenario.Apply(
            page,
            ModelInspectionVisualTestScenario.CreatePresentation(
                ModelInspectionFigmaState.OperationalFailure));

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            page,
            width: 1440,
            height: 1024);
        await host.CaptureAsync();
        page.UpdateLayout();

        InspectionOutcomeCard outcome = Element<InspectionOutcomeCard>(
            page,
            "InspectionOutcomeCardControl");
        Border iconContainer = Element<Border>(outcome, "OutcomeIconContainer");
        InspectionStatusGlyph glyph = Element<InspectionStatusGlyph>(
            outcome,
            "OutcomeIcon");
        TextBlock title = Element<TextBlock>(outcome, "OutcomeTitle");
        ContentControl focusTarget = Element<ContentControl>(
            outcome,
            "OutcomeFocusTarget");
        Assert.AreEqual(Visibility.Visible, iconContainer.Visibility);
        Assert.AreEqual(Visibility.Visible, glyph.Visibility);
        Assert.AreEqual(InspectionStatusGlyphKind.Error, glyph.Kind);
        Assert.AreEqual(40d, glyph.SurfaceSize, 0.01d);
        Assert.AreEqual(
            AccessibilityView.Raw,
            AutomationProperties.GetAccessibilityView(iconContainer));
        Assert.AreEqual(
            AccessibilityView.Raw,
            AutomationProperties.GetAccessibilityView(glyph));
        Assert.IsFalse(string.IsNullOrWhiteSpace(title.Text));
        Assert.AreEqual(title.Text, AutomationProperties.GetName(focusTarget));
        Assert.AreEqual(
            outcome.Presentation.AutomationName,
            AutomationProperties.GetName(outcome));

        InspectionActionCard actions = Element<InspectionActionCard>(
            page,
            "InspectionActionCardControl");
        Button secondaryOne = Element<Button>(actions, "SecondaryActionOneButton");
        Button secondaryTwo = Element<Button>(actions, "SecondaryActionTwoButton");
        Button primary = Element<Button>(actions, "PrimaryActionButton");
        CollectionAssert.AreEqual(
            new[] { 0, 1, 2 },
            new[]
            {
                secondaryOne.TabIndex,
                secondaryTwo.TabIndex,
                primary.TabIndex
            });
        foreach (Button button in new[] { secondaryOne, secondaryTwo, primary })
        {
            Assert.AreEqual(Visibility.Visible, button.Visibility);
            Assert.IsGreaterThanOrEqualTo(44d, button.ActualHeight);
        }

        Assert.IsFalse(secondaryTwo.IsEnabled);
        Assert.AreEqual(
            "Coming later",
            AutomationProperties.GetHelpText(secondaryTwo));
        Assert.AreEqual("Coming later", ToolTipService.GetToolTip(secondaryTwo));
        TextBlock adjacentHelp = Element<TextBlock>(
            actions,
            "SecondaryActionTwoFutureHelpText");
        Assert.AreEqual(Visibility.Visible, adjacentHelp.Visibility);
        Assert.AreEqual("Coming later", adjacentHelp.Text);
        Assert.AreEqual(
            $"{AutomationProperties.GetName(secondaryTwo)}. Coming later",
            AutomationProperties.GetName(adjacentHelp));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ExpandedReport_ExposesBoundedRowNamesAndKeyboardScrollFocus()
    {
        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionVisualTestScenario.Apply(
            page,
            ModelInspectionVisualTestScenario.CreatePresentation(
                ModelInspectionFigmaState.ReadyWithWarningsExpanded));

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            page,
            width: 1440,
            height: 1024);
        await host.CaptureAsync();
        page.UpdateLayout();

        InspectionContentCard content = Element<InspectionContentCard>(
            page,
            "InspectionContentCardControl");
        ScrollViewer report = Element<ScrollViewer>(
            content,
            "ExpandedReportScrollViewer");
        TextBlock[] namedRows = ModelInspectionRenderedStateTests
            .Descendants(report)
            .OfType<TextBlock>()
            .Where(text =>
                AutomationProperties.GetAccessibilityView(text) ==
                    AccessibilityView.Content &&
                !string.IsNullOrWhiteSpace(
                    AutomationProperties.GetName(text)))
            .ToArray();
        Assert.AreEqual(content.Presentation.ExpandedItems.Count, namedRows.Length);
        Assert.IsNotEmpty(namedRows);
        foreach (TextBlock row in namedRows)
        {
            string name = AutomationProperties.GetName(row);
            Assert.IsTrue(name.Length <= 512, name);
            Assert.IsTrue(
                name.EndsWith(".", StringComparison.Ordinal),
                name);
        }
        CollectionAssert.AreEquivalent(
            content.Presentation.ExpandedItems
                .Select(item => item.AutomationName)
                .ToArray(),
            namedRows
                .Select(AutomationProperties.GetName)
                .ToArray());

        Assert.AreEqual(172d, report.MaxHeight, 0.01d);
        Assert.IsTrue(report.IsTabStop);
        Assert.IsTrue(report.Focus(FocusState.Keyboard));
        Assert.AreSame(report, FocusManager.GetFocusedElement(page.XamlRoot));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task FractionOnlyProgress_DoesNotRepeatTheSamePoliteAnnouncement()
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingAnimationDriver();
        var settings = new RecordingMotionSettings(animationsEnabled: false);
        ModelInspectionPage page = CreateInjectedPage(
            service,
            dispatcher,
            driver,
            settings);
        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            page,
            width: 888,
            height: 900);
        await host.CaptureAsync();
        Task run = page.StartInspectionIfReadyAsync()!;

        try
        {
            dispatcher.RunAll();
            await host.CaptureAsync();
            TextBlock pageHeading = Element<TextBlock>(page, "PageTitle");
            Assert.AreSame(
                pageHeading,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "Startup must retain the initial page heading as the " +
                "semantic focus target.");
            call.Report(ActiveProgress(stageFraction: 0.25));
            dispatcher.RunAll();
            Assert.AreSame(
                pageHeading,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "The first genuine stage must preserve untouched heading focus.");

            InspectionContentCard content = Element<InspectionContentCard>(
                page,
                "InspectionContentCardControl");
            Border completedCountChip = Element<Border>(
                content,
                "ProgressCompletedCountChip");
            TextBlock completedCountText = ModelInspectionRenderedStateTests
                .Descendants(completedCountChip)
                .OfType<TextBlock>()
                .Single();
            Assert.AreEqual(
                AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(completedCountChip));
            Assert.AreEqual(
                AccessibilityView.Content,
                AutomationProperties.GetAccessibilityView(completedCountText));
            Assert.AreEqual(
                page.CurrentPresentation!.ContentCard.ProgressSummary,
                AutomationProperties.GetName(completedCountText));
            int meaningfulAnnouncementCount =
                content.LiveRegionChangeNotificationCount;
            string meaningfulAnnouncement = AutomationProperties.GetName(content);
            var initialProgressRegionKey =
                page.CurrentPresentation!.RegionKeys.Progress;
            InspectionContentItemPresentation rowBefore =
                page.CurrentPresentation.ContentCard.Items[1];
            string rowAutomationName = rowBefore.AutomationName;
            int statusAnimationCount = driver.StageStatusStartCount;
            int detailAnimationCount = driver.ActiveDetailStartCount;
            Assert.AreEqual(
                AutomationLiveSetting.Polite,
                AutomationProperties.GetLiveSetting(content));
            Assert.AreEqual(
                2,
                meaningfulAnnouncementCount,
                "Startup and the first genuine stage each announce once.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(meaningfulAnnouncement));

            InspectionActionCard actions = Element<InspectionActionCard>(
                page,
                "InspectionActionCardControl");
            Button cancel = Element<Button>(actions, "CancelActionButton");
            Assert.IsTrue(cancel.Focus(FocusState.Keyboard));
            Assert.AreSame(cancel, FocusManager.GetFocusedElement(page.XamlRoot));

            call.Report(ActiveProgress(stageFraction: 0.75));
            dispatcher.RunAll();

            Assert.AreSame(
                cancel,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "A later fraction must not steal the user's current focus.");

            Assert.AreEqual(
                meaningfulAnnouncementCount,
                content.LiveRegionChangeNotificationCount,
                "A fraction-only visual update must not repeat identical speech.");
            Assert.AreEqual(
                meaningfulAnnouncement,
                AutomationProperties.GetName(content));
            Assert.AreNotEqual(
                initialProgressRegionKey,
                page.CurrentPresentation!.RegionKeys.Progress,
                "Fraction-only progress must still update the visual progress region.");
            Assert.AreSame(
                rowBefore,
                page.CurrentPresentation.ContentCard.Items[1]);
            Assert.AreEqual(
                0.75d,
                page.CurrentPresentation.ContentCard.Items[1].StageFraction);
            Assert.AreEqual(
                rowAutomationName,
                page.CurrentPresentation.ContentCard.Items[1].AutomationName,
                "A fraction-only update must not change accessible row text.");
            Assert.AreEqual(
                statusAnimationCount,
                driver.StageStatusStartCount,
                "A fraction-only update must not start marker motion.");
            Assert.AreEqual(
                detailAnimationCount,
                driver.ActiveDetailStartCount,
                "A fraction-only update must not start detail motion.");

            const string ChangedDetail =
                "Validated the current model configuration evidence.";
            call.Report(ActiveProgress(
                stageFraction: 0.3,
                userMessage: ChangedDetail));
            dispatcher.RunAll();
            Assert.AreEqual(
                meaningfulAnnouncementCount + 1,
                content.LiveRegionChangeNotificationCount,
                "A meaningful detail change must announce exactly once.");
            Assert.AreEqual(ChangedDetail, AutomationProperties.GetName(content));

            call.Report(new ModelInspectionProgress(
                ModelInspectionStage.ValidateTokenizerAndChatSetup,
                ModelInspectionStageStatus.Active,
                completedStageCount: 2,
                totalStageCount: 5,
                stageFraction: 0.3,
                userMessage: ChangedDetail));
            dispatcher.RunAll();
            Assert.AreSame(
                cancel,
                FocusManager.GetFocusedElement(page.XamlRoot),
                "A later genuine stage must not steal the user's current focus.");
            Assert.AreEqual(
                meaningfulAnnouncementCount + 2,
                content.LiveRegionChangeNotificationCount,
                "A meaningful stage change must announce exactly once.");
        }
        finally
        {
            call.TryComplete(ModelInspectionExecutionResult.Cancelled(
                cooperative: true));
            try
            {
                await run.WaitAsync(UiTimeout);
                dispatcher.RunAll();
            }
            finally
            {
                InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
            }
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InProcessLiveRegionHook_TerminalIsOncePerAttemptAndRetryRepeats()
    {
        var service = new ControlledInspectionService();
        ModelInspectionExecutionResult failure =
            ModelInspectionExecutionResult.OperationalFailure(
                PresentationTestData.CreateFailure());
        service.QueueCall(failure);
        service.QueueCall(failure);
        var dispatcher = new ManualRenderDispatcher();
        ModelInspectionPage page = CreateInjectedPage(
            service,
            dispatcher,
            new RecordingAnimationDriver(),
            new RecordingMotionSettings(animationsEnabled: false));

        try
        {
            Task initialRun = page.StartInspectionIfReadyAsync()!;
            dispatcher.RunAll();
            await initialRun.WaitAsync(UiTimeout);
            dispatcher.RunAll();
            await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
                page,
                width: 1440,
                height: 1024);
            await host.CaptureAsync();
            InspectionOutcomeCard outcome = Element<InspectionOutcomeCard>(
                page,
                "InspectionOutcomeCardControl");
            InspectionContentCard content = Element<InspectionContentCard>(
                page,
                "InspectionContentCardControl");
            InspectionActionCard actions = Element<InspectionActionCard>(
                page,
                "InspectionActionCardControl");
            Button cancel = Element<Button>(actions, "CancelActionButton");
            Assert.AreEqual(
                AutomationLiveSetting.Assertive,
                AutomationProperties.GetLiveSetting(outcome));
            Assert.AreEqual(1, outcome.LiveRegionChangeNotificationCount);
            Assert.AreEqual(
                0d,
                Element<Border>(content, "ContentCardShell").MinHeight,
                0.01d);
            Assert.AreEqual(
                0d,
                Element<Border>(actions, "ResultView").MinHeight,
                0.01d);
            Assert.IsGreaterThan(0d, content.ActualHeight);
            Assert.IsGreaterThan(0d, actions.ActualHeight);
            AssertUniformVisibleCardGaps(page);

            ModelInspectionPagePresentation terminal =
                page.CurrentPresentation!;
            ModelInspectionRenderCoordinator coordinator = Coordinator(page);
            coordinator.RequestRender(new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(
                    terminal.RenderKey.AttemptGeneration,
                    terminal.RenderKey.PresentationRevision + 1),
                isRunActive: true,
                isCancellationRequested: false,
                progress: ActiveProgress(stageFraction: 0.5),
                terminalResult: null));
            dispatcher.RunAll();
            await host.CaptureAsync();
            Assert.AreEqual(Visibility.Collapsed, outcome.CardVisibility);
            Assert.IsGreaterThan(0d, content.ActualHeight);
            Assert.AreEqual(184d, cancel.ActualWidth, 1d);
            Assert.IsGreaterThanOrEqualTo(44d, cancel.ActualHeight);
            AssertUniformVisibleCardGaps(page);
            Assert.AreEqual(
                1,
                outcome.LiveRegionChangeNotificationCount,
                "Hiding the outcome in the same attempt must not announce it.");

            coordinator.RequestRender(new ModelInspectionViewSnapshot(
                new ModelInspectionRenderKey(
                    terminal.RenderKey.AttemptGeneration,
                    terminal.RenderKey.PresentationRevision + 2),
                isRunActive: false,
                isCancellationRequested: false,
                progress: null,
                terminalResult: failure));
            dispatcher.RunAll();
            await host.CaptureAsync();
            Assert.AreEqual(Visibility.Visible, outcome.CardVisibility);
            Assert.AreEqual(
                0d,
                Element<Border>(content, "ContentCardShell").MinHeight,
                0.01d);
            Assert.AreEqual(
                0d,
                Element<Border>(actions, "ResultView").MinHeight,
                0.01d);
            Assert.IsGreaterThan(0d, content.ActualHeight);
            Assert.IsGreaterThan(0d, actions.ActualHeight);
            AssertUniformVisibleCardGaps(page);
            Assert.AreEqual(
                1,
                outcome.LiveRegionChangeNotificationCount,
                "A real same-attempt hide/show render must remain deduplicated.");

            page.ViewModel!.RetryCommand.Execute(null);
            await Task.Yield();
            dispatcher.RunAll();
            if (page.CurrentInspectionTask is not null)
            {
                await page.CurrentInspectionTask.WaitAsync(UiTimeout);
            }
            dispatcher.RunAll();

            Assert.AreEqual(
                2,
                outcome.LiveRegionChangeNotificationCount,
                "Retry creates a new attempt and may announce the same outcome once.");
        }
        finally
        {
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void HighContrastDictionary_DefinesEverySemanticBrushAsASystemResolvedBrush()
    {
        ResourceDictionary theme = ModelInspectionTheme();
        ResourceDictionary light = Assert.IsInstanceOfType<ResourceDictionary>(
            theme.ThemeDictionaries["Light"]);
        ResourceDictionary highContrast =
            Assert.IsInstanceOfType<ResourceDictionary>(
                theme.ThemeDictionaries["HighContrast"]);
        string[] lightKeys = light.Keys
            .Select(key => key.ToString()!)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] highContrastKeys = highContrast.Keys
            .Select(key => key.ToString()!)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.IsNotEmpty(lightKeys);
        CollectionAssert.AreEqual(lightKeys, highContrastKeys);
        foreach (string key in highContrastKeys)
        {
            Assert.IsInstanceOfType<SolidColorBrush>(highContrast[key], key);
        }

        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        Assert.IsTrue(Element<TextBlock>(page, "PageTitle").UseSystemFocusVisuals);
        foreach (Button button in ModelInspectionRenderedStateTests
            .Descendants(page)
            .OfType<Button>())
        {
            Assert.IsTrue(button.UseSystemFocusVisuals, button.Name);
        }
        Assert.IsTrue(Element<ScrollViewer>(
            Element<InspectionModelCard>(page, "InspectionModelCardControl"),
            "InspectionChecksScrollViewer").UseSystemFocusVisuals);
        Assert.IsTrue(Element<ScrollViewer>(
            Element<InspectionContentCard>(page, "InspectionContentCardControl"),
            "ExpandedReportScrollViewer").UseSystemFocusVisuals);
        Assert.IsTrue(Element<ContentControl>(
            Element<InspectionOutcomeCard>(page, "InspectionOutcomeCardControl"),
            "OutcomeFocusTarget").UseSystemFocusVisuals);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task NarrowStandardScale_WrapsRequiredTextAndKeepsTargetsReachable()
    {
        ModelInspectionPage page = ModelInspectionVisualTestScenario.CreatePage();
        ModelInspectionVisualTestScenario.Apply(
            page,
            ModelInspectionVisualTestScenario.CreatePresentation(
                ModelInspectionFigmaState.InvalidExpanded));

        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
            page,
            width: 360,
            height: 1024);
        await host.CaptureAsync();
        page.UpdateLayout();

        FrameworkElement contentHost = Element<FrameworkElement>(
            page,
            "InspectionContentHost");
        Assert.AreEqual(328d, contentHost.ActualWidth, 1d);
        Button[] visibleButtons = ModelInspectionRenderedStateTests
            .Descendants(page)
            .OfType<Button>()
            .Where(button =>
                button.Visibility == Visibility.Visible &&
                button.ActualWidth > 0d &&
                button.ActualHeight > 0d)
            .ToArray();
        Assert.IsNotEmpty(visibleButtons);
        foreach (Button button in visibleButtons)
        {
            Assert.IsGreaterThanOrEqualTo(44d, button.ActualWidth, button.Name);
            Assert.IsGreaterThanOrEqualTo(44d, button.ActualHeight, button.Name);
        }

        TextBlock[] requiredLongText = ModelInspectionRenderedStateTests
            .Descendants(page)
            .OfType<TextBlock>()
            .Where(text =>
                text.Visibility == Visibility.Visible &&
                text.ActualWidth > 0d &&
                text.ActualHeight > 0d &&
                text.Text.Length >= 40 &&
                !(text.FontFamily?.Source ?? string.Empty).StartsWith(
                    "Segoe Fluent Icons",
                    StringComparison.Ordinal))
            .ToArray();
        Assert.IsNotEmpty(requiredLongText);
        foreach (TextBlock text in requiredLongText)
        {
            Assert.AreNotEqual(TextWrapping.NoWrap, text.TextWrapping, text.Text);
            Assert.IsTrue(
                text.ActualHeight + 1d >= text.DesiredSize.Height,
                text.Text);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InjectedReducedMotionPolicy_PreservesTerminalSemanticsAndEndpoints()
    {
        MotionEvidence animated = await RunTerminalMotionEvidenceAsync(
            animationsEnabled: true);
        MotionEvidence reduced = await RunTerminalMotionEvidenceAsync(
            animationsEnabled: false);

        Assert.IsGreaterThan(0, animated.AnimationStartCount);
        Assert.AreEqual(0, reduced.AnimationStartCount);
        Assert.AreEqual(animated.State, reduced.State);
        Assert.AreEqual(animated.OutcomeKind, reduced.OutcomeKind);
        Assert.AreEqual(animated.ContentMode, reduced.ContentMode);
        Assert.AreEqual(animated.OutgoingVisibility, reduced.OutgoingVisibility);
        Assert.AreEqual(animated.ProgressLiveSetting, reduced.ProgressLiveSetting);
        Assert.AreEqual(animated.OutcomeLiveSetting, reduced.OutcomeLiveSetting);
        Assert.AreEqual(animated.OutcomeAnnouncementCount,
            reduced.OutcomeAnnouncementCount);
        Assert.AreEqual(animated.OutcomeAutomationName,
            reduced.OutcomeAutomationName);
        Assert.AreEqual(animated.Geometry.Count, reduced.Geometry.Count);
        for (int index = 0; index < animated.Geometry.Count; index++)
        {
            Assert.AreEqual(
                animated.Geometry[index],
                reduced.Geometry[index],
                1d,
                $"Reduced motion changed terminal geometry at index {index}.");
        }
    }

    private static async Task<MotionEvidence> RunTerminalMotionEvidenceAsync(
        bool animationsEnabled)
    {
        var service = new ControlledInspectionService();
        ControlledCall call = service.QueueCall();
        var dispatcher = new ManualRenderDispatcher();
        var driver = new RecordingAnimationDriver();
        ModelInspectionPage page = CreateInjectedPage(
            service,
            dispatcher,
            driver,
            new RecordingMotionSettings(animationsEnabled));
        Task run = page.StartInspectionIfReadyAsync()!;

        try
        {
            dispatcher.RunAll();
            call.Complete(ModelInspectionExecutionResult.OperationalFailure(
                PresentationTestData.CreateFailure()));
            await run.WaitAsync(UiTimeout);
            dispatcher.RunAll();
            if (driver.TerminalCompletionCount > 0)
            {
                driver.CompleteLastTerminal();
            }

            Assert.IsNotNull(page.CurrentPresentation);
            ModelInspectionPagePresentation presentation =
                page.CurrentPresentation;
            InspectionContentCard outgoing = Element<InspectionContentCard>(
                page,
                "OutgoingProgressContentCard");
            InspectionContentCard content = Element<InspectionContentCard>(
                page,
                "InspectionContentCardControl");
            InspectionOutcomeCard outcome = Element<InspectionOutcomeCard>(
                page,
                "InspectionOutcomeCardControl");
            InspectionActionCard actions = Element<InspectionActionCard>(
                page,
                "InspectionActionCardControl");
            await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(
                page,
                width: 888,
                height: 900);
            await host.CaptureAsync();
            page.UpdateLayout();
            double[] geometry =
            [
                outcome.ActualWidth,
                outcome.ActualHeight,
                content.ActualWidth,
                content.ActualHeight,
                actions.ActualWidth,
                actions.ActualHeight
            ];
            return new MotionEvidence(
                presentation.State,
                presentation.OutcomeCard.Kind,
                presentation.ContentCard.Mode,
                outgoing.Visibility,
                AutomationProperties.GetLiveSetting(content),
                AutomationProperties.GetLiveSetting(outcome),
                outcome.LiveRegionChangeNotificationCount,
                AutomationProperties.GetName(outcome),
                driver.TotalStartCount,
                geometry);
        }
        finally
        {
            call.TryComplete(ModelInspectionExecutionResult.Cancelled(
                cooperative: true));
            InvokeNavigation(page, "OnNavigatedFrom", parameter: null);
        }
    }

    private static ModelInspectionProgress ActiveProgress(
        double stageFraction,
        string userMessage = "Reading model configuration.") =>
        new(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completedStageCount: 1,
            totalStageCount: 5,
            stageFraction,
            userMessage);

    private static ModelInspectionPage CreateInjectedPage(
        IModelInspectionService service,
        ManualRenderDispatcher dispatcher,
        RecordingAnimationDriver driver,
        RecordingMotionSettings settings)
    {
        var page = new ModelInspectionPage(
            service,
            () => dispatcher,
            () => driver,
            () => settings,
            () => new ImmediateMilestoneScheduler())
        {
            RequestedTheme = ElementTheme.Light
        };
        InvokeNavigation(
            page,
            "OnNavigatedTo",
            PresentationTestData.CreateRequest());
        return page;
    }

    private static void InvokeNavigation(
        ModelInspectionPage page,
        string methodName,
        object? parameter)
    {
        NavigationEventArgs navigation = CreateNavigationEventArgs(parameter);
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        try
        {
            method.Invoke(page, [navigation]);
        }
        catch (TargetInvocationException error)
            when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
        }
    }

    private static NavigationEventArgs CreateNavigationEventArgs(
        object? parameter)
    {
        NavigationEventArgs? captured = null;
        var frame = new Frame();
        frame.Navigated += (_, eventArguments) => captured = eventArguments;
        Assert.IsTrue(frame.Navigate(typeof(Page), parameter));
        Assert.IsNotNull(captured);
        return captured;
    }

    private static ResourceDictionary ModelInspectionTheme() =>
        Application.Current.Resources.MergedDictionaries.Single(dictionary =>
            dictionary.Source?.OriginalString.EndsWith(
                "/Features/ModelInspection/Presentation/ModelInspectionTheme.xaml",
                StringComparison.OrdinalIgnoreCase) == true);

    private static ModelInspectionRenderCoordinator Coordinator(
        ModelInspectionPage page)
    {
        FieldInfo? field = typeof(ModelInspectionPage).GetField(
            "_coordinator",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return Assert.IsInstanceOfType<ModelInspectionRenderCoordinator>(
            field.GetValue(page));
    }

    private static T Element<T>(FrameworkElement root, string name)
        where T : DependencyObject =>
        ModelInspectionRenderedStateTests.Element<T>(root, name);

    private static void AssertUniformVisibleCardGaps(ModelInspectionPage page)
    {
        FrameworkElement header = Element<FrameworkElement>(page, "Header");
        FrameworkElement[] visibleCards =
        [
            Element<InspectionOutcomeCard>(page, "InspectionOutcomeCardControl"),
            Element<InspectionModelCard>(page, "InspectionModelCardControl"),
            Element<InspectionContentCard>(page, "InspectionContentCardControl"),
            Element<InspectionActionCard>(page, "InspectionActionCardControl")
        ];
        visibleCards = visibleCards
            .Where(card =>
                card.Visibility == Visibility.Visible &&
                card.ActualHeight > 0d)
            .ToArray();
        Assert.IsNotEmpty(visibleCards);
        Assert.AreEqual(24d, VerticalGap(page, header, visibleCards[0]), 1d);
        for (int index = 1; index < visibleCards.Length; index++)
        {
            Assert.AreEqual(
                16d,
                VerticalGap(page, visibleCards[index - 1], visibleCards[index]),
                1d);
        }
    }

    private static double VerticalGap(
        FrameworkElement root,
        FrameworkElement upper,
        FrameworkElement lower)
    {
        Windows.Foundation.Point upperOrigin = upper
            .TransformToVisual(root)
            .TransformPoint(new Windows.Foundation.Point());
        Windows.Foundation.Point lowerOrigin = lower
            .TransformToVisual(root)
            .TransformPoint(new Windows.Foundation.Point());
        return lowerOrigin.Y - (upperOrigin.Y + upper.ActualHeight);
    }

    private sealed class ControlledInspectionService : IModelInspectionService
    {
        private readonly Queue<ControlledCall> _calls = [];

        internal ControlledCall QueueCall(
            ModelInspectionExecutionResult? completed = null)
        {
            var call = new ControlledCall();
            if (completed is not null)
            {
                call.Complete(completed);
            }

            _calls.Enqueue(call);
            return call;
        }

        public Task<ModelInspectionExecutionResult> InspectAsync(
            ModelInspectionRequest request,
            IProgress<ModelInspectionProgress>? progress,
            CancellationToken cancellationToken)
        {
            if (_calls.Count == 0)
            {
                throw new InvalidOperationException(
                    "No controlled accessibility call was queued.");
            }

            ControlledCall call = _calls.Dequeue();
            call.Bind(progress);
            return call.Completion.Task;
        }
    }

    private sealed class ControlledCall
    {
        internal TaskCompletionSource<ModelInspectionExecutionResult> Completion
            { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private IProgress<ModelInspectionProgress>? Progress { get; set; }

        internal void Bind(IProgress<ModelInspectionProgress>? progress) =>
            Progress = progress;

        internal void Report(ModelInspectionProgress progress)
        {
            Assert.IsNotNull(Progress);
            Progress.Report(progress);
        }

        internal void Complete(ModelInspectionExecutionResult result) =>
            Completion.SetResult(result);

        internal void TryComplete(ModelInspectionExecutionResult result) =>
            Completion.TrySetResult(result);
    }

    private sealed class ImmediateMilestoneScheduler :
        IModelInspectionMilestoneScheduler
    {
        private bool disposed;

        public TimeSpan Elapsed => TimeSpan.Zero;

        public IDisposable Schedule(TimeSpan delay, Action callback)
        {
            ArgumentNullException.ThrowIfNull(callback);
            if (delay <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(delay));
            }

            ObjectDisposedException.ThrowIf(disposed, this);
            callback();
            return EmptyRegistration.Instance;
        }

        public void Dispose()
        {
            disposed = true;
        }

        private sealed class EmptyRegistration : IDisposable
        {
            internal static EmptyRegistration Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class ManualRenderDispatcher : IModelInspectionRenderDispatcher
    {
        private readonly Queue<Action> _callbacks = [];

        public bool TryEnqueue(Action callback)
        {
            _callbacks.Enqueue(callback);
            return true;
        }

        internal void RunAll()
        {
            while (_callbacks.Count > 0)
            {
                _callbacks.Dequeue()();
            }
        }
    }

    private sealed class RecordingMotionSettings : IModelInspectionMotionSettings
    {
        internal RecordingMotionSettings(bool animationsEnabled)
        {
            AnimationsEnabled = animationsEnabled;
        }

        public bool AnimationsEnabled { get; }

        public event EventHandler? AnimationsEnabledChanged
        {
            add { }
            remove { }
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingAnimationDriver :
        IModelInspectionAnimationDriver
    {
        private readonly List<(
            ModelInspectionVisualOperationKey Key,
            Action<ModelInspectionVisualOperationKey> Completed)> _terminal = [];

        internal int StageStatusStartCount { get; private set; }

        internal int ActiveDetailStartCount { get; private set; }

        internal int DisclosureStartCount { get; private set; }

        internal int TerminalCompletionCount => _terminal.Count;

        internal int TotalStartCount =>
            StageStatusStartCount +
            ActiveDetailStartCount +
            DisclosureStartCount +
            _terminal.Count;

        public void StartStageStatus(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            StageStatusStartCount++;

        public void StartActiveDetail(
            UIElement target,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            ActiveDetailStartCount++;

        public void StartDisclosure(
            UIElement chevron,
            FrameworkElement viewport,
            IReadOnlyList<UIElement> followingElements,
            bool isExpanded,
            IReadOnlyList<double> previousTopOffsets,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            DisclosureStartCount++;

        public void StartTerminal(
            UIElement outgoing,
            UIElement incoming,
            ModelInspectionVisualOperationKey key,
            Action<ModelInspectionVisualOperationKey> completed) =>
            _terminal.Add((key, completed));

        internal void CompleteLastTerminal()
        {
            var pending = _terminal[^1];
            pending.Completed(pending.Key);
        }

        public void CancelAll()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed record MotionEvidence(
        ModelInspectionFigmaState State,
        InspectionOutcomePresentationKind OutcomeKind,
        InspectionContentCardMode ContentMode,
        Visibility OutgoingVisibility,
        AutomationLiveSetting ProgressLiveSetting,
        AutomationLiveSetting OutcomeLiveSetting,
        int OutcomeAnnouncementCount,
        string OutcomeAutomationName,
        int AnimationStartCount,
        IReadOnlyList<double> Geometry);
}
